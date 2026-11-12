using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterBecher.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterBecher.Server
{
    public partial class Main : BaseScript
    {
        private class Wette
        {
            public int Becher, Betrag;
        }

        private class Tisch
        {
            public string Host;
            public string HostName;
            public Stufe Stufe;
            public bool Gezinkt;
            public string Zustand = "frei";
            public int Ball, Ende, Runde;
            public int Einsatz = Config.EinsatzMin;   // fester Betrag oder Mindesteinsatz, je nach Fest
            public bool Fest;
            public float X, Y, Z, Richtung, BecherHoehe;
            public readonly Dictionary<string, Wette> Wetten = new Dictionary<string, Wette>();
            public readonly Dictionary<string, int> Auswahl = new Dictionary<string, int>();   // angeklickt, noch nicht gesetzt
            public readonly HashSet<string> Zuschauer = new HashSet<string>();
            public readonly List<int> Props = new List<int>();
        }

        private readonly Dictionary<string, Tisch> tische = new Dictionary<string, Tisch>();
        private readonly Dictionary<string, long> letzterAufbau = new Dictionary<string, long>();
        private readonly Random zufall = new Random();

        // Schutz gegen Event-Spam: pro Spieler und Event eine Mindestpause.
        private readonly Dictionary<string, long> drossel = new Dictionary<string, long>();

        private bool Gedrosselt(Player player, string was, int ms)
        {
            if (player == null) return true;
            var schluessel = player.Handle + ":" + was;
            long jetzt = GetGameTimer();   // auf dem Server long, im Client int
            long zuletzt;
            if (drossel.TryGetValue(schluessel, out zuletzt) && jetzt - zuletzt < ms) return true;
            drossel[schluessel] = jetzt;
            return false;
        }

        private bool InGang(Player player)
        {
            var gang = GangVon(player);
            if (string.IsNullOrEmpty(gang) || gang == "none") return false;
            return Config.Gangs.Length == 0 || Config.Gangs.Contains(gang);
        }
        private const Hash ObjektOhneVersatz = (Hash)0x58040420;   // CREATE_OBJECT_NO_OFFSET auf dem Server

        public Main()
        {
            var version = GetResourceMetadata(Config.Ressource, "version", 0) ?? "";
            Debug.WriteLine($"^5{Config.Ressource}^7 {version} von {Config.Autor} · {Config.Projektseite}");
            EventHandlers["playerDropped"] += new Action<Player, string>(OnDropped);
            EventHandlers["onResourceStop"] += new Action<string>(name =>
            {
                if (name != GetCurrentResourceName()) return;
                foreach (var t in tische.Values.ToList()) Abbauen(t, null);
            });
            Tick += AbbauTick;
            Tick += Starten;
        }

        private async Task Starten()
        {
            Tick -= Starten;
            await Delay(1000);
            ItemAnlegen(Config.SetItem, Config.SetLabel, 500, Config.SetText);
            ItemBenutzbar(Config.SetItem, (quelle, item) =>
            {
                var p = SpielerVon(quelle.ToString());
                if (p == null) return;
                if (Config.NurGangsAufstellen && !InGang(p))
                {
                    Melden(p, "Damit kannst du nichts anfangen. Das Becherspiel stellen nur Gangs auf.", "error");
                    return;
                }
                TriggerClientEvent(p, "toter-becherspiel:client:set");
            });
        }

        private static long Jetzt()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private Player SpielerVon(string handle)
        {
            int id;
            if (!int.TryParse(handle, out id)) return null;
            var p = Players[id];
            return p != null && GetPlayerPed(handle) != 0 ? p : null;
        }

        private bool Position(Player player, out float x, out float y, out float z)
        {
            x = y = z = 0;
            var ped = GetPlayerPed(player.Handle);
            if (ped == 0) return false;
            var p = GetEntityCoords(ped);
            x = p.X;
            y = p.Y;
            z = p.Z;
            return true;
        }

        private static float Zahl(object wert)
        {
            try { return Convert.ToSingle(wert); }
            catch { return float.NaN; }
        }

        private void An(Tisch t, string ereignis, params object[] daten)
        {
            var args = new object[daten.Length + 1];
            args[0] = int.Parse(t.Host);
            Array.Copy(daten, 0, args, 1, daten.Length);
            foreach (var z in t.Zuschauer.ToList())
            {
                var p = SpielerVon(z);
                if (p != null) TriggerClientEvent(p, "toter-becherspiel:client:" + ereignis, args);
            }
        }

        private void Hinweis(Player player, string text, string typ = "info")
        {
            if (player != null) TriggerClientEvent(player, "toter-becherspiel:client:hinweis", text, typ);
        }

        private Dictionary<string, object> TischInfo(Tisch t)
        {
            return new Dictionary<string, object>
            {
                { "host", int.Parse(t.Host) }, { "name", t.HostName },
                { "x", t.X }, { "y", t.Y }, { "z", t.Z }, { "richtung", t.Richtung }
            };
        }

        // ---------------------------------------------------------------- Aufstellen

        [EventHandler("toter-becherspiel:server:tische")]
        private void OnTische([FromSource] Player player)
        {
            if (Gedrosselt(player, "tische", 1000)) return;
            TriggerClientEvent(player, "toter-becherspiel:client:tische", tische.Values.Select(t => (object)TischInfo(t)).ToList());
        }

        [EventHandler("toter-becherspiel:server:aufstellen")]
        private void OnAufstellen([FromSource] Player player, string stufe, bool gezinkt, object ox, object oy, object oz, object orichtung, List<object> ebenen, object obecher)
        {
            if (Gedrosselt(player, "aufstellen", 1000)) return;
            if (tische.ContainsKey(player.Handle))
            {
                Hinweis(player, "Du hast schon einen Tisch stehen.", "error");
                return;
            }
            if (Config.NurGangsAufstellen && !InGang(player))
            {
                Hinweis(player, "Das Becherspiel stellen nur Gangs auf.", "error");
                return;
            }
            long zuletzt;
            if (letzterAufbau.TryGetValue(player.Handle, out zuletzt) && Jetzt() - zuletzt < Config.AufstellPauseSekunden)
            {
                Hinweis(player, $"Warte noch {Config.AufstellPauseSekunden - (Jetzt() - zuletzt)} Sekunden.", "error");
                return;
            }
            if (!HatItem(player, Config.SetItem))
            {
                Hinweis(player, $"Dafür brauchst du ein {Config.SetLabel}.", "error");
                return;
            }
            var s = Config.StufeVon(stufe) ?? Config.Stufen[0];
            if (gezinkt && !Config.ZinkenErlaubt) gezinkt = false;
            if (Config.ZinkenErlaubt && Config.ImmerGezinkt) gezinkt = true;

            float x = Zahl(ox), y = Zahl(oy), z = Zahl(oz), richtung = Zahl(orichtung), becher = Zahl(obecher);
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsNaN(richtung)) return;
            if (float.IsNaN(becher) || becher < 0f || becher > 5f) becher = Config.BecherErsatzHoehe;

            float px, py, pz;
            if (!Position(player, out px, out py, out pz) || Rechnen.Abstand(px, py, pz, x, y, z) > Config.AufstellAbstand)
            {
                Hinweis(player, "Der Tisch muss direkt bei dir stehen.", "error");
                return;
            }
            if (tische.Values.Any(t => Rechnen.Abstand(t.X, t.Y, t.Z, x, y, z) < Config.TischAbstand))
            {
                Hinweis(player, "Hier steht schon ein Tisch.", "error");
                return;
            }

            var tisch = new Tisch
            {
                Host = player.Handle, HostName = Name(player), Stufe = s, Gezinkt = gezinkt,
                X = x, Y = y, Z = z, Richtung = richtung, BecherHoehe = becher
            };
            tisch.Zuschauer.Add(player.Handle);
            try { PropsSetzen(tisch, ebenen); }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Tisch aufstellen fehlgeschlagen: {e.Message}");
                foreach (var p in tisch.Props) if (DoesEntityExist(p)) DeleteEntity(p);
                Hinweis(player, "Der Tisch konnte nicht aufgestellt werden.", "error");
                return;
            }
            tische[player.Handle] = tisch;
            letzterAufbau[player.Handle] = Jetzt();

            TriggerClientEvent("toter-becherspiel:client:tisch", int.Parse(player.Handle), TischInfo(tisch));
            Oeffnen(player, tisch, true);
            Debug.WriteLine($"[{Config.Ressource}] {player.Name} hat einen Tisch aufgestellt ({s.Name}{(gezinkt ? ", gezinkt" : "")}).");
        }

        private void PropsSetzen(Tisch t, List<object> hoehen)
        {
            float rx, ry, vx, vy;
            Rechnen.Rechts(t.Richtung, out rx, out ry);
            Rechnen.Vorne(t.Richtung, out vx, out vy);
            for (var i = 0; i < Config.Tisch.Count; i++)
            {
                var e = Config.Tisch[i];
                var h = hoehen != null && i < hoehen.Count ? Zahl(hoehen[i]) : float.NaN;
                if (float.IsNaN(h) || Math.Abs(h) > 5f) h = e.Stapeln ? i * Config.EbeneErsatzHoehe : e.Z;
                var obj = Function.Call<int>(ObjektOhneVersatz, Rechnen.Hash(e.Modell), t.X + rx * e.X + vx * e.Y, t.Y + ry * e.X + vy * e.Y, t.Z + h, true, true, false);
                if (obj == 0) continue;
                SetEntityHeading(obj, t.Richtung);
                FreezeEntityPosition(obj, true);
                SetEntityDistanceCullingRadius(obj, Config.Sichtweite);
                t.Props.Add(obj);
            }
        }

        private void Oeffnen(Player player, Tisch t, bool istHost)
        {
            TriggerClientEvent(player, "toter-becherspiel:client:offen", int.Parse(t.Host), new Dictionary<string, object>
            {
                { "host", int.Parse(t.Host) }, { "hostName", t.HostName }, { "istHost", istHost }, { "ich", int.Parse(player.Handle) },
                { "stufe", t.Stufe.Name }, { "gezinkt", istHost && t.Gezinkt }, { "zustand", t.Zustand },
                { "x", t.X }, { "y", t.Y }, { "z", t.Z }, { "richtung", t.Richtung }, { "becherHoehe", t.BecherHoehe },
                { "min", Config.EinsatzMin }, { "taste", Config.KameraTaste }, { "einsatz", t.Einsatz }, { "fest", t.Fest }
            });
            if (istHost) SpielerListe(t);
        }

        [EventHandler("toter-becherspiel:server:wiederOeffnen")]
        private void OnWiederOeffnen([FromSource] Player player)
        {
            if (Gedrosselt(player, "wiederOeffnen", 500)) return;
            Tisch t;
            if (tische.TryGetValue(player.Handle, out t)) Oeffnen(player, t, true);
        }

        [EventHandler("toter-becherspiel:server:abbauen")]
        private void OnAbbauen([FromSource] Player player)
        {
            if (Gedrosselt(player, "abbauen", 1000)) return;
            Tisch t;
            if (tische.TryGetValue(player.Handle, out t)) Abbauen(t, null);
        }

        private void Abbauen(Tisch t, string grund)
        {
            t.Runde++;
            foreach (var w in t.Wetten.ToList()) Auszahlen(SpielerVon(w.Key), w.Value.Betrag, "becherspiel-erstattung");
            t.Wetten.Clear();
            foreach (var obj in t.Props)
                if (DoesEntityExist(obj)) DeleteEntity(obj);
            An(t, "zu");
            tische.Remove(t.Host);
            TriggerClientEvent("toter-becherspiel:client:tisch", int.Parse(t.Host), null);
            if (grund != null)
            {
                var host = SpielerVon(t.Host);
                if (host != null) Melden(host, grund, "error");
            }
        }

        private async Task AbbauTick()
        {
            await Delay(Config.AbbauPruefenMs);
            foreach (var t in tische.Values.ToList())
            {
                var host = SpielerVon(t.Host);
                float x, y, z;
                if (host == null || !Position(host, out x, out y, out z)) continue;
                if (Rechnen.Abstand(x, y, z, t.X, t.Y, t.Z) > Config.AbbauAbstand)
                    Abbauen(t, "Dein Becherspiel wurde abgebaut, weil du zu weit weg warst. Offene Einsätze sind zurück.");
            }
        }

        // ---------------------------------------------------------------- Beitreten

        [EventHandler("toter-becherspiel:server:beitreten")]
        private void OnBeitreten([FromSource] Player player, int hostId)
        {
            if (Gedrosselt(player, "beitreten", 1000)) return;
            Tisch t;
            if (!tische.TryGetValue(hostId.ToString(), out t))
            {
                Melden(player, "Diesen Tisch gibt es nicht mehr.", "error");
                return;
            }
            if (t.Zuschauer.Contains(player.Handle)) return;
            if (Config.NurGangsMitspielen && !InGang(player))
            {
                Melden(player, "An diesem Tisch spielen nur Gang-Mitglieder.", "error");
                return;
            }
            if (t.Zuschauer.Count - 1 >= Config.MaxSpieler)
            {
                Melden(player, "Am Tisch ist kein Platz mehr.", "error");
                return;
            }
            float x, y, z;
            if (!Position(player, out x, out y, out z) || Rechnen.Abstand(x, y, z, t.X, t.Y, t.Z) > Config.BeitrittAbstand + 0.5f)
            {
                Melden(player, "Geh näher an den Tisch.", "error");
                return;
            }
            if (!Rechnen.VonVorne(t.X, t.Y, t.Richtung, x, y))
            {
                Melden(player, "Stell dich vor den Tisch, auf die Seite mit dem grünen Kreis.", "error");
                return;
            }
            t.Zuschauer.Add(player.Handle);
            Oeffnen(player, t, false);
            SpielerListe(t);
            Hinweis(SpielerVon(t.Host), $"{Name(player)} ist dazugekommen.");
        }

        [EventHandler("toter-becherspiel:server:verlassen")]
        private void OnVerlassen([FromSource] Player player, int hostId)
        {
            if (Gedrosselt(player, "verlassen", 500)) return;
            Tisch t;
            if (tische.TryGetValue(hostId.ToString(), out t) && t.Host != player.Handle) Rausnehmen(t, player, true);
        }

        private void Rausnehmen(Tisch t, Player player, bool melden)
        {
            if (!t.Zuschauer.Remove(player.Handle)) return;
            Wette w;
            if (t.Wetten.TryGetValue(player.Handle, out w))
            {
                t.Wetten.Remove(player.Handle);
                Auszahlen(player, w.Betrag, "becherspiel-erstattung");
            }
            t.Auswahl.Remove(player.Handle);
            SpielerListe(t);
            if (melden) Hinweis(SpielerVon(t.Host), $"{Name(player)} ist gegangen.");
        }

        // Wer auf welchen Becher wie viel gesetzt hat: alle am Tisch sehen es über den Bechern, der Host zusätzlich in seiner Liste.
        private void SpielerListe(Tisch t)
        {
            var wetten = t.Wetten.Select(w => (object)new Dictionary<string, object>
            {
                { "id", int.Parse(w.Key) }, { "name", Name(SpielerVon(w.Key)) }, { "becher", w.Value.Becher }, { "betrag", w.Value.Betrag }, { "gesetzt", true }
            }).Concat(t.Auswahl.Where(a => !t.Wetten.ContainsKey(a.Key) && t.Zuschauer.Contains(a.Key)).Select(a => (object)new Dictionary<string, object>
            {
                { "id", int.Parse(a.Key) }, { "name", Name(SpielerVon(a.Key)) }, { "becher", a.Value }, { "betrag", 0 }, { "gesetzt", false }
            })).ToList();
            An(t, "wetten", wetten);

            var host = SpielerVon(t.Host);
            if (host == null) return;
            var liste = t.Zuschauer.Where(z => z != t.Host).Select(z => SpielerVon(z)).Where(p => p != null)
                .Select(p =>
                {
                    Wette w;
                    t.Wetten.TryGetValue(p.Handle, out w);
                    return (object)new Dictionary<string, object>
                    {
                        { "id", int.Parse(p.Handle) }, { "name", Name(p) },
                        { "becher", w?.Becher ?? 0 }, { "betrag", w?.Betrag ?? 0 }
                    };
                }).ToList();
            TriggerClientEvent(host, "toter-becherspiel:client:spieler", liste);
        }

        private void OnDropped([FromSource] Player player, string grund)
        {
            Tisch eigener;
            if (tische.TryGetValue(player.Handle, out eigener)) Abbauen(eigener, null);
            foreach (var t in tische.Values.ToList())
                if (t.Zuschauer.Contains(player.Handle)) Rausnehmen(t, player, false);
            letzterAufbau.Remove(player.Handle);
            foreach (var k in drossel.Keys.Where(k => k.StartsWith(player.Handle + ":")).ToList()) drossel.Remove(k);
        }
    }
}
