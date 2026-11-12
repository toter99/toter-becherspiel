using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterBecher.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterBecher.Client
{
    public partial class Main : BaseScript
    {
        private class TischC
        {
            public string Name;
            public float X, Y, Z, Richtung;
        }

        private readonly Dictionary<int, TischC> bekannte = new Dictionary<int, TischC>();
        private bool uiOffen;
        private bool mausModus = true;
        private int aktuellerHost;
        private bool istHost;

        public Main()
        {
            EventHandlers["toter-becherspiel:client:tische"] += new Action<object>(OnTische);
            EventHandlers["toter-becherspiel:client:tisch"] += new Action<object, object>(OnTisch);
            EventHandlers["toter-becherspiel:client:offen"] += new Action<object, object>(OnOffen);
            EventHandlers["toter-becherspiel:client:zu"] += new Action<object>(host =>
            {
                if (Id(host) != aktuellerHost) return;
                Verlassen();
                Melden("Der Tisch wurde abgebaut.", "error");
            });
            EventHandlers["toter-becherspiel:client:zeigen"] += new Action<object, object, object>(OnZeigen);
            EventHandlers["toter-becherspiel:client:mischen"] += new Action<object, object, object>(OnMischen);
            EventHandlers["toter-becherspiel:client:wettenAuf"] += new Action<object, object>((host, sek) =>
            {
                if (Id(host) != aktuellerHost) return;
                Nui("wettenAuf", "sekunden", Id(sek), "istHost", istHost);
            });
            EventHandlers["toter-becherspiel:client:wettenUhr"] += new Action<object, object>((host, sek) =>
            {
                if (Id(host) == aktuellerHost) Nui("wettenUhr", "sekunden", Id(sek));
            });
            EventHandlers["toter-becherspiel:client:wetteOk"] += new Action<object, object>((becher, betrag) =>
                Nui("wetteOk", "becher", Id(becher), "betrag", Id(betrag)));
            EventHandlers["toter-becherspiel:client:wettenZu"] += new Action<object>(host =>
            {
                if (Id(host) == aktuellerHost) Nui("wettenZu");
            });
            EventHandlers["toter-becherspiel:client:zinken"] += new Action<object, object, object>((wetten, ehrlich, sek) =>
            {
                if (!uiOffen) Melden("Die Wetten sind zu. Öffne deinen Tisch, um den Gewinner zu wählen.", "primary");
                Nui("zinken", "wetten", wetten, "ehrlich", Id(ehrlich), "sekunden", Id(sek));
            });
            EventHandlers["toter-becherspiel:client:aufdecken"] += new Action<object, object, object>(OnAufdecken);
            EventHandlers["toter-becherspiel:client:gestoppt"] += new Action<object>(host =>
            {
                if (Id(host) != aktuellerHost) return;
                Nui("gestoppt", "istHost", istHost);
                BecherZuruecksetzen();
                KugelWeg();
            });
            EventHandlers["toter-becherspiel:client:einsatz"] += new Action<object, object, object>((host, betrag, fest) =>
            {
                if (Id(host) == aktuellerHost) Nui("einsatz", "betrag", Id(betrag), "fest", fest is bool f && f);
            });
            EventHandlers["toter-becherspiel:client:wetten"] += new Action<object, object>((host, liste) =>
            {
                if (Id(host) == aktuellerHost) Nui("wettenListe", "liste", liste);
            });
            EventHandlers["toter-becherspiel:client:hinweis"] += new Action<object, object>((text, typ) =>
            {
                var t = Convert.ToString(typ);
                if (uiOffen) Nui("hinweis", "text", Convert.ToString(text), "typ", t);
                else Melden(Convert.ToString(text), t == "error" ? "error" : t == "success" ? "success" : "primary");
            });
            EventHandlers["toter-becherspiel:client:spieler"] += new Action<object>(liste => Nui("spieler", "liste", liste));
            EventHandlers["onResourceStop"] += new Action<string>(OnResourceStop);

            NuiCallback("close", d => NuiClose());
            NuiCallback("aufstellen", d =>
            {
                FensterZu();
                object stufe, gezinkt;
                d.TryGetValue("stufe", out stufe);
                d.TryGetValue("gezinkt", out gezinkt);
                Platzieren(Convert.ToString(stufe), gezinkt is bool g && g);
            });
            NuiCallback("abbauen", d =>
            {
                TriggerServerEvent("toter-becherspiel:server:abbauen");
                Verlassen();
            });
            NuiCallback("starten", d => TriggerServerEvent("toter-becherspiel:server:starten"));
            NuiCallback("stoppen", d => TriggerServerEvent("toter-becherspiel:server:stoppen"));
            NuiCallback("auswahl", d =>
            {
                object becher;
                if (aktuellerHost != 0 && d.TryGetValue("becher", out becher))
                    TriggerServerEvent("toter-becherspiel:server:auswahl", aktuellerHost, Id(becher));
            });
            NuiCallback("einsatz", d =>
            {
                object betrag, fest;
                d.TryGetValue("betrag", out betrag);
                d.TryGetValue("fest", out fest);
                TriggerServerEvent("toter-becherspiel:server:einsatz", Id(betrag), fest is bool f && f);
            });
            NuiCallback("wetten", d =>
            {
                object becher, betrag;
                if (aktuellerHost == 0 || !d.TryGetValue("becher", out becher) || !d.TryGetValue("betrag", out betrag)) return;
                TriggerServerEvent("toter-becherspiel:server:wetten", aktuellerHost, Id(becher), Id(betrag));
            });
            NuiCallback("zinken", d =>
            {
                object becher;
                if (d.TryGetValue("becher", out becher)) TriggerServerEvent("toter-becherspiel:server:zinken", Id(becher));
            });

            EventHandlers["toter-becherspiel:client:set"] += new Action(SetBenutzt);
            RegisterCommand("becherspiel_kamera", new Action<int, List<object>, string>((quelle, args, roh) => KameraUmschalten()), false);
            RegisterKeyMapping("becherspiel_kamera", "Becherspiel: Maus oder Kamera", "keyboard", Config.KameraTaste);

            Tick += Starten;
            Tick += SperreTick;
            Tick += NaeheTick;
        }

        private static int Id(object wert)
        {
            try { return Convert.ToInt32(wert); }
            catch { return 0; }
        }

        private static float Zahl(object wert, float ersatz = 0f)
        {
            try { return Convert.ToSingle(wert); }
            catch { return ersatz; }
        }

        private int MeineId()
        {
            return GetPlayerServerId(PlayerId());
        }

        private void NuiCallback(string name, Action<IDictionary<string, object>> handler)
        {
            RegisterNuiCallbackType(name);
            EventHandlers["__cfx_nui:" + name] += new Action<IDictionary<string, object>, CallbackDelegate>((daten, cb) =>
            {
                try { handler(daten ?? new Dictionary<string, object>()); }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] NUI {name}: {e}"); }
                cb(new { ok = true });
            });
        }

        private void Nui(string aktion, params object[] werte)
        {
            var d = new Dictionary<string, object> { { "action", aktion } };
            for (var i = 0; i + 1 < werte.Length; i += 2) d[(string)werte[i]] = Normal(werte[i + 1]);
            SendNuiMessage(Json.Schreiben(d));
        }

        // Daten vom Server kommen als ExpandoObject oder Listen an. Für Json.Schreiben in Dictionary und List umwandeln.
        private static object Normal(object wert)
        {
            if (wert == null || wert is string) return wert;
            if (wert is IDictionary<string, object> d) return d.ToDictionary(kv => kv.Key, kv => Normal(kv.Value));
            if (wert is IEnumerable e) return e.Cast<object>().Select(Normal).ToList();
            return wert;
        }

        private async Task Starten()
        {
            Tick -= Starten;
            await Delay(2000);
            TriggerServerEvent("toter-becherspiel:server:tische");
            TriggerEvent("chat:removeSuggestion", "/becherspiel_kamera");
        }

        // ---------------------------------------------------------------- Becherset

        private void SetBenutzt()
        {
            if (platzierenAktiv || uiOffen) return;
            if (IsPedInAnyVehicle(PlayerPedId(), false))
            {
                Melden("Steig erst aus.", "error");
                return;
            }
            if (bekannte.ContainsKey(MeineId()))
            {
                TriggerServerEvent("toter-becherspiel:server:wiederOeffnen");
                return;
            }
            Fenster(false);
            Nui("aufbau", "stufen", Config.Stufen.Select(s => (object)new Dictionary<string, object> { { "id", s.Id }, { "name", s.Name }, { "tausche", s.Tausche } }).ToList(),
                "zinken", Config.ZinkenErlaubt && !Config.ImmerGezinkt);
        }

        // ---------------------------------------------------------------- Fenster

        private void Fenster(bool tischAnsicht)
        {
            uiOffen = true;
            mausModus = true;
            SetNuiFocus(true, true);
            SetNuiFocusKeepInput(tischAnsicht);
        }

        private void FensterZu()
        {
            uiOffen = false;
            SetNuiFocus(false, false);
            SetNuiFocusKeepInput(false);
            Nui("schliessen");
            BecherEntfernen();
        }

        private void Verlassen()
        {
            aktuellerHost = 0;
            istHost = false;
            FensterZu();
        }

        private void NuiClose()
        {
            if (aktuellerHost != 0 && !istHost) TriggerServerEvent("toter-becherspiel:server:verlassen", aktuellerHost);
            if (istHost)
            {
                FensterZu();
                Melden("Dein Tisch läuft weiter. Mit E am Tisch oder dem Becherset kommst du zurück.", "primary");
                return;
            }
            Verlassen();
        }

        private void KameraUmschalten()
        {
            if (!uiOffen || aktuellerHost == 0) return;
            mausModus = !mausModus;
            SetNuiFocus(true, mausModus);
            Nui("kamera", "maus", mausModus);
        }

        private static readonly int[] Gesperrt =
        {
            1, 2, 21, 22, 24, 25, 30, 31, 37, 44, 140, 141, 142, 157, 158, 159, 160, 161, 162, 163, 164, 165, 199, 200, 245
        };

        private async Task SperreTick()
        {
            if (!uiOffen || !mausModus || aktuellerHost == 0)
            {
                await Delay(200);
                return;
            }
            foreach (var c in Gesperrt) DisableControlAction(0, c, true);
            DisableControlAction(2, 1, true);
            DisableControlAction(2, 2, true);
            DisablePlayerFiring(PlayerId(), true);
        }

        // ---------------------------------------------------------------- Server-Ereignisse

        private void OnTische(object liste)
        {
            bekannte.Clear();
            if (!(liste is IEnumerable e)) return;
            foreach (var roh in e)
            {
                var d = roh as IDictionary<string, object>;
                if (d != null) TischMerken(Id(d["host"]), d);
            }
        }

        private void OnTisch(object host, object info)
        {
            var id = Id(host);
            var d = info as IDictionary<string, object>;
            if (d == null)
            {
                bekannte.Remove(id);
                return;
            }
            TischMerken(id, d);
            if (id != MeineId()) Melden($"{bekannte[id].Name} hat ein Becherspiel aufgestellt.", "primary", 4000);
        }

        private void TischMerken(int id, IDictionary<string, object> d)
        {
            bekannte[id] = new TischC
            {
                Name = Convert.ToString(d["name"]),
                X = Zahl(d["x"]), Y = Zahl(d["y"]), Z = Zahl(d["z"]), Richtung = Zahl(d["richtung"])
            };
        }

        private async void OnOffen(object host, object daten)
        {
            var d = daten as IDictionary<string, object>;
            if (d == null) return;
            aktuellerHost = Id(host);
            istHost = d["istHost"] is bool b && b;
            Fenster(true);
            Nui("tisch", "daten", d);
            await BecherSetzen(Zahl(d["x"]), Zahl(d["y"]), Zahl(d["z"]), Zahl(d["richtung"]), Zahl(d["becherHoehe"], Config.BecherErsatzHoehe));
        }

        private async void OnZeigen(object host, object ball, object dauer)
        {
            if (Id(host) != aktuellerHost) return;
            var b = Id(ball);
            var ms = Id(dauer);
            Nui("zeigen", "ball", b, "dauer", ms);
            BecherZuruecksetzen();
            await KugelZeigen(b);
            BecherHeben(b, Config.BecherAnheben);
            await Delay(Math.Max(ms - 400, 0));
            BecherHeben(b, 0f);
            KugelWeg();
        }

        private void OnMischen(object host, object tausche, object dauer)
        {
            if (Id(host) != aktuellerHost || !(tausche is IEnumerable e)) return;
            var liste = new List<int[]>();
            foreach (var paar in e)
            {
                var p = (paar as IEnumerable)?.Cast<object>().Select(Id).ToArray();
                if (p != null && p.Length == 2) liste.Add(p);
            }
            Nui("mischen", "tausche", liste.Select(p => (object)new List<object> { p[0], p[1] }).ToList(), "dauer", Id(dauer));
            Mischen(liste, Id(dauer));
        }

        private async void OnAufdecken(object host, object becher, object ergebnisse)
        {
            if (Id(host) != aktuellerHost) return;
            var b = Id(becher);
            Nui("aufdecken", "becher", b, "ergebnisse", ergebnisse, "istHost", istHost);
            BecherHeben(b, Config.BecherAnheben);
            await KugelZeigen(b);
        }

        private void OnResourceStop(string name)
        {
            if (name != GetCurrentResourceName()) return;
            if (uiOffen) SetNuiFocus(false, false);
            SetNuiFocusKeepInput(false);
            BecherEntfernen();
            GeisterEntfernen();
        }
    }
}
