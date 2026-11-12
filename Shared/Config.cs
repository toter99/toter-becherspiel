using System.Collections.Generic;

namespace ToterBecher.Shared
{
    public class Stufe
    {
        public string Id, Name;
        public int Tausche;      // wie oft zwei Becher getauscht werden
        public int TauschMs;     // Dauer eines Tauschs
    }

    // Ein Teil des Tisches. Stapeln = true setzt das Teil genau auf das vorherige (Höhe über GetModelDimensions),
    // sonst steht es mit X (seitlich), Y (nach vorne) und Z (Höhe) frei neben der Tischmitte.
    public class Ebene
    {
        public string Modell;
        public bool Stapeln = true;
        public float X, Y, Z;
    }

    public static class Config
    {
        public const string Ressource = "toter-becherspiel";
        public const string Autor = "toter";
        public const string Projektseite = "https://github.com/toter99/toter-becherspiel";
        public const string CoreRessource = "qb-core";   // siehe Server/Bridge.cs

        // Geld: "cash" oder "bank" (QBCore). Ein Hütchenspiel auf der Straße läuft normalerweise bar.
        public const string Konto = "cash";

        public const int EinsatzMin = 10;
        // Praktisch keine Obergrenze. Die Zahl schützt nur vor Rechenüberlauf, wenn mehrere Gewinner ausgezahlt werden.
        public const int EinsatzTechnischMax = 500000000;

        // Nur Gang-Mitglieder (QBCore PlayerData.gang, nicht "none") dürfen das Becherset aufstellen bzw. mitspielen.
        // Gangs leer = jede Gang, sonst nur die genannten (Namen wie in qb-core/shared/gangs.lua).
        public const bool NurGangsAufstellen = true;
        public const bool NurGangsMitspielen = false;
        public static readonly string[] Gangs = new string[0];

        public const int ZeigenMs = 3000;          // so lange liegt die Kugel am Anfang offen
        public const int WettSekunden = 60;        // Zeit zum Setzen
        public const int AlleGesetztSekunden = 5;  // haben alle gesetzt, läuft nur noch diese Zeit
        public const int ZinkenMs = 10000;         // Zeit für den Host, nach den Wetten den Becher mit der Kugel zu wählen

        // false = niemand kann einen gezinkten Tisch aufstellen
        public static readonly bool ZinkenErlaubt = true;
        // true = jeder Tisch ist gezinkt, beim Aufstellen gibt es keine Auswahl dafür
        public static readonly bool ImmerGezinkt = true;

        public static readonly List<Stufe> Stufen = new List<Stufe>
        {
            new Stufe { Id = "leicht", Name = "Leicht", Tausche = 5, TauschMs = 700 },
            new Stufe { Id = "mittel", Name = "Mittel", Tausche = 9, TauschMs = 550 },
            new Stufe { Id = "schwer", Name = "Schwer", Tausche = 14, TauschMs = 400 }
        };

        public const int MaxSpieler = 6;              // ohne Host
        public const float BeitrittAbstand = 3.0f;
        public const float VorneToleranz = 70f;       // Grad. 180 = von überall beitreten
        public static readonly bool VorneUmdrehen = false;      // true, falls die Vorderseite falsch herum berechnet wird

        public const float MarkerSichtweite = 12f;
        public const float MarkerAbstand = 1.3f;      // Abstand des grünen Markers von der Tischmitte

        public const float AbbauAbstand = 100f;       // Host weiter weg = Tisch wird abgebaut, Einsätze zurück
        public const int AbbauPruefenMs = 5000;

        public const float AufstellAbstand = 4f;      // Tisch nur direkt beim Spieler
        public const float TischAbstand = 3f;         // Mindestabstand zwischen zwei Tischen
        public const int AufstellPauseSekunden = 30;  // Pause zwischen zwei neuen Tischen eines Spielers

        // Das Becherset im Inventar benutzen öffnet das Aufstellen. Es bleibt beim Aufstellen im Inventar.
        public const string SetItem = "becherset";
        public const string SetLabel = "Becherset";
        public const string SetText = "Drei Becher, eine Kugel und eine Klapptisch-Auflage. Benutzen, um ein Becherspiel aufzustellen.";

        public const string KameraTaste = "F6";       // Standard, jeder kann es unter Einstellungen > Tastenbelegung > FiveM ändern

        public const float Sichtweite = 100f;         // Streaming-Distanz der Tisch-Props

        public static readonly List<Ebene> Tisch = new List<Ebene>
        {
            new Ebene { Modell = "prop_hat_box_05" },
            new Ebene { Modell = "prop_apple_box_02" }
        };
        public const float EbeneErsatzHoehe = 1.0f;   // falls die Maße eines Modells nicht lesbar sind

        public const string BecherModell = "prop_plastic_cup_02";
        public static readonly bool BecherUmgedreht = true;
        public const float BecherErsatzHoehe = 1.15f;
        public const float BecherExtraHoehe = 0.0f;   // Luft zwischen Tischplatte und Becher, darf negativ sein
        public const float BecherAbstand = 0.25f;     // Abstand der äußeren Becher zur Mitte
        public const float BecherAnheben = 0.35f;

        public const string KugelModell = "prop_poolball_9";

        public const float PlatzierAbstand = 0.5f;    // Tisch so weit vor dem Spieler
        public const float DrehGeschwindigkeit = 60f;  // Grad pro Sekunde mit den Pfeiltasten
        public const float MausradSchritt = 7.5f;      // Grad pro Raste am Mausrad

        public static Stufe StufeVon(string id)
        {
            foreach (var s in Stufen) if (s.Id == id) return s;
            return null;
        }
    }
}
