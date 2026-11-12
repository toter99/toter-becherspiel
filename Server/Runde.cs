using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using ToterBecher.Shared;

namespace ToterBecher.Server
{
    public partial class Main
    {
        private bool Laeuft(Tisch t, int runde)
        {
            Tisch aktuell;
            return tische.TryGetValue(t.Host, out aktuell) && aktuell == t && t.Runde == runde;
        }

        private List<int[]> Tausche(int anzahl)
        {
            var liste = new List<int[]>();
            for (var i = 0; i < anzahl; i++)
            {
                var a = zufall.Next(1, 4);
                var b = zufall.Next(1, 3);
                if (b >= a) b++;
                liste.Add(new[] { a, b });
            }
            return liste;
        }

        private static int Nachher(int start, List<int[]> tausche)
        {
            var pos = start;
            foreach (var t in tausche)
            {
                if (pos == t[0]) pos = t[1];
                else if (pos == t[1]) pos = t[0];
            }
            return pos;
        }

        [EventHandler("toter-becherspiel:server:starten")]
        private async void OnStarten([FromSource] Player player)
        {
            if (Gedrosselt(player, "starten", 1000)) return;
            Tisch t;
            if (!tische.TryGetValue(player.Handle, out t)) return;
            if (t.Zustand != "frei")
            {
                Hinweis(player, "Die Runde läuft schon.", "error");
                return;
            }
            var runde = ++t.Runde;
            t.Ball = zufall.Next(1, 4);
            t.Wetten.Clear();
            t.Auswahl.Clear();
            SpielerListe(t);
            t.Zustand = "zeigen";
            An(t, "zeigen", t.Ball, Config.ZeigenMs);

            await Delay(Config.ZeigenMs);
            if (!Laeuft(t, runde)) return;
            var tausche = Tausche(t.Stufe.Tausche);
            t.Ende = Nachher(t.Ball, tausche);
            t.Zustand = "mischen";
            An(t, "mischen", tausche.Select(x => (object)x.Cast<object>().ToList()).ToList(), t.Stufe.TauschMs);

            await Delay(tausche.Count * t.Stufe.TauschMs + 400);
            if (!Laeuft(t, runde)) return;
            t.Zustand = "wetten";
            An(t, "wettenAuf", Config.WettSekunden);

            // Höchstens WettSekunden. Haben alle gesetzt, läuft nur noch AlleGesetztSekunden.
            var ende = DateTime.UtcNow.AddSeconds(Config.WettSekunden);
            var verkuerzt = false;
            while (DateTime.UtcNow < ende)
            {
                await Delay(200);
                if (!Laeuft(t, runde)) return;
                if (verkuerzt || !AlleGesetzt(t)) continue;
                verkuerzt = true;
                var kurz = DateTime.UtcNow.AddSeconds(Config.AlleGesetztSekunden);
                if (kurz < ende) ende = kurz;
                An(t, "wettenUhr", (int)Math.Ceiling((ende - DateTime.UtcNow).TotalSeconds));
            }
            if (!Laeuft(t, runde)) return;
            t.Zustand = "aufdecken";
            An(t, "wettenZu");

            if (!t.Gezinkt)
            {
                Abrechnen(t, t.Ende);
                return;
            }
            var host = SpielerVon(t.Host);
            if (host != null)
            {
                var wetten = t.Wetten.Select(w => (object)new Dictionary<string, object>
                {
                    { "name", Name(SpielerVon(w.Key)) }, { "becher", w.Value.Becher }, { "betrag", w.Value.Betrag }
                }).ToList();
                TriggerClientEvent(host, "toter-becherspiel:client:zinken", wetten, t.Ende, Config.ZinkenMs / 1000);
            }
            await Delay(Config.ZinkenMs);
            if (Laeuft(t, runde) && t.Zustand == "aufdecken") Abrechnen(t, t.Ende);
        }

        // Host bricht die laufende Runde ab, ohne aufzudecken. Alle Einsätze gehen zurück.
        [EventHandler("toter-becherspiel:server:stoppen")]
        private void OnStoppen([FromSource] Player player)
        {
            if (Gedrosselt(player, "stoppen", 1000)) return;
            Tisch t;
            if (!tische.TryGetValue(player.Handle, out t) || t.Zustand == "frei") return;
            t.Runde++;
            t.Zustand = "frei";
            var zurueck = 0;
            foreach (var w in t.Wetten.ToList())
            {
                var p = SpielerVon(w.Key);
                Auszahlen(p, w.Value.Betrag, "becherspiel-erstattung");
                Hinweis(p, $"Der Host hat die Runde gestoppt. Du bekommst deine {Geld(w.Value.Betrag)} zurück.", "info");
                zurueck++;
            }
            t.Wetten.Clear();
            t.Auswahl.Clear();
            An(t, "gestoppt");
            SpielerListe(t);
            Hinweis(player, zurueck == 0 ? "Runde gestoppt." : $"Runde gestoppt, {zurueck} {(zurueck == 1 ? "Einsatz geht" : "Einsätze gehen")} zurück.", "info");
        }

        // Einsatz der nächsten Runde: fest (genau der Betrag) oder Mindesteinsatz (mehr geht). Nur zwischen den Runden.
        [EventHandler("toter-becherspiel:server:einsatz")]
        private void OnEinsatz([FromSource] Player player, int betrag, bool fest)
        {
            if (Gedrosselt(player, "einsatz", 300)) return;
            Tisch t;
            if (!tische.TryGetValue(player.Handle, out t)) return;
            if (t.Zustand != "frei")
            {
                Hinweis(player, "Den Einsatz änderst du zwischen den Runden.", "error");
                An(t, "einsatz", t.Einsatz, t.Fest);
                return;
            }
            t.Einsatz = Math.Max(Config.EinsatzMin, Math.Min(Config.EinsatzTechnischMax, betrag));
            t.Fest = fest;
            An(t, "einsatz", t.Einsatz, t.Fest);
        }

        private static string Geld(int betrag)
        {
            return "$" + betrag.ToString("N0", new System.Globalization.CultureInfo("de-DE"));
        }

        private bool AlleGesetzt(Tisch t)
        {
            var spieler = t.Zuschauer.Where(z => z != t.Host && SpielerVon(z) != null).ToList();
            return spieler.Count > 0 && spieler.All(z => t.Wetten.ContainsKey(z));
        }

        // Spieler hat einen Becher angeklickt, aber noch nicht gesetzt. Alle sehen es über den Bechern.
        [EventHandler("toter-becherspiel:server:auswahl")]
        private void OnAuswahl([FromSource] Player player, int hostId, int becher)
        {
            if (Gedrosselt(player, "auswahl", 150)) return;
            Tisch t;
            if (!tische.TryGetValue(hostId.ToString(), out t) || t.Zustand != "wetten" || player.Handle == t.Host) return;
            if (!t.Zuschauer.Contains(player.Handle) || t.Wetten.ContainsKey(player.Handle) || becher < 1 || becher > 3) return;
            int vorher;
            if (t.Auswahl.TryGetValue(player.Handle, out vorher) && vorher == becher) return;
            t.Auswahl[player.Handle] = becher;
            SpielerListe(t);
        }

        [EventHandler("toter-becherspiel:server:zinken")]
        private void OnZinken([FromSource] Player player, int becher)
        {
            if (Gedrosselt(player, "zinken", 500)) return;
            Tisch t;
            if (!tische.TryGetValue(player.Handle, out t) || !t.Gezinkt || t.Zustand != "aufdecken") return;
            if (becher < 1 || becher > 3) return;
            Abrechnen(t, becher);
        }

        [EventHandler("toter-becherspiel:server:wetten")]
        private void OnWetten([FromSource] Player player, int hostId, int becher, int betrag)
        {
            if (Gedrosselt(player, "wetten", 500)) return;
            Tisch t;
            if (!tische.TryGetValue(hostId.ToString(), out t) || !t.Zuschauer.Contains(player.Handle)) return;
            if (player.Handle == t.Host) return;
            float x, y, z;
            if (!Position(player, out x, out y, out z) || Rechnen.Abstand(x, y, z, t.X, t.Y, t.Z) > Config.BeitrittAbstand + 3f)
            {
                Hinweis(player, "Du bist zu weit vom Tisch weg.", "error");
                return;
            }
            if (t.Zustand != "wetten")
            {
                Hinweis(player, "Gerade nimmt niemand Wetten an.", "error");
                return;
            }
            if (t.Wetten.ContainsKey(player.Handle))
            {
                Hinweis(player, "Du hast diese Runde schon gesetzt.", "error");
                return;
            }
            if (becher < 1 || becher > 3) return;
            var mindestens = Math.Max(Config.EinsatzMin, t.Einsatz);
            if (t.Fest) betrag = t.Einsatz;
            if (betrag < mindestens)
            {
                Hinweis(player, $"Der Einsatz ist mindestens {Geld(mindestens)}.", "error");
                return;
            }
            if (betrag > Config.EinsatzTechnischMax)
            {
                Hinweis(player, $"Mehr als {Geld(Config.EinsatzTechnischMax)} geht auf einmal nicht.", "error");
                return;
            }
            if (!Bezahlen(player, betrag, "becherspiel-einsatz"))
            {
                Hinweis(player, "So viel Geld hast du nicht dabei.", "error");
                return;
            }
            t.Wetten[player.Handle] = new Wette { Becher = becher, Betrag = betrag };
            TriggerClientEvent(player, "toter-becherspiel:client:wetteOk", becher, betrag);
            SpielerListe(t);
        }

        // Verlierer zuerst: deren Einsätze gehen an den Host. Danach bekommen Gewinner ihren Einsatz zurück
        // und denselben Betrag vom Host. Reicht sein Geld nicht, wird es nach Einsatzhöhe aufgeteilt.
        private void Abrechnen(Tisch t, int becher)
        {
            t.Zustand = "frei";
            var host = SpielerVon(t.Host);
            var ergebnisse = new List<Dictionary<string, object>>();
            var gewinner = new List<KeyValuePair<string, Wette>>();

            foreach (var w in t.Wetten)
            {
                if (w.Value.Becher == becher)
                {
                    gewinner.Add(w);
                    continue;
                }
                Auszahlen(host, w.Value.Betrag, "becherspiel-gewinn");
                ergebnisse.Add(Ergebnis(w.Key, false, w.Value.Betrag));
            }

            if (gewinner.Count > 0)
            {
                var summe = gewinner.Sum(w => (long)w.Value.Betrag);
                long kasse = host == null ? 0 : Kontostand(host);
                var reicht = kasse >= summe;
                foreach (var w in gewinner)
                {
                    var anteil = reicht ? w.Value.Betrag : (int)Math.Floor(w.Value.Betrag / (double)summe * kasse);
                    anteil = Math.Max(0, Math.Min(w.Value.Betrag, anteil));
                    if (anteil > 0 && !Bezahlen(host, anteil, "becherspiel-auszahlung")) anteil = 0;
                    var spieler = SpielerVon(w.Key);
                    Auszahlen(spieler, w.Value.Betrag + anteil, "becherspiel-gewinn");
                    if (anteil < w.Value.Betrag)
                        Hinweis(spieler, $"Der Host konnte nicht alles zahlen. Du bekommst deinen Einsatz zurück und {Geld(anteil)} dazu.", "error");
                    ergebnisse.Add(Ergebnis(w.Key, true, anteil));
                }
                if (!reicht) Hinweis(host, "Dein Geld hat nicht für alle Gewinner gereicht. Es wurde nach Einsatz aufgeteilt.", "error");
            }

            foreach (var z in t.Zuschauer.ToList())
            {
                var p = SpielerVon(z);
                if (p == null) continue;
                var fuer = z == t.Host ? ergebnisse : ergebnisse.Where(e => (int)e["id"] == int.Parse(z)).ToList();
                TriggerClientEvent(p, "toter-becherspiel:client:aufdecken", int.Parse(t.Host), becher, fuer.Cast<object>().ToList());
            }
            t.Wetten.Clear();
            t.Auswahl.Clear();
            SpielerListe(t);
        }

        private Dictionary<string, object> Ergebnis(string handle, bool gewonnen, int betrag)
        {
            return new Dictionary<string, object>
            {
                { "id", int.Parse(handle) }, { "name", Name(SpielerVon(handle)) }, { "gewonnen", gewonnen }, { "betrag", betrag }
            };
        }
    }
}
