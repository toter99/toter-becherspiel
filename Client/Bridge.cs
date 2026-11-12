using CitizenFX.Core;

namespace ToterBecher.Client
{
    // ======================================================================================
    //  ANSCHLÜSSE (Client)
    //  Standard ist QBCore (QBCore:Notify). Für einen anderen Server nur diese Datei anpassen (siehe ANSCHLUESSE.md).
    // ======================================================================================
    public partial class Main
    {
        // Meldung am Bildschirm, wenn das Becherspiel-Fenster zu ist. typ: "success", "error", "primary".
        private void Melden(string text, string typ = "primary", int dauer = 5000)
        {
            TriggerEvent("QBCore:Notify", text, typ, dauer);
        }
    }
}
