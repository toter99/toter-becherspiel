using System.Threading.Tasks;
using CitizenFX.Core;
using ToterBecher.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterBecher.Client
{
    public partial class Main
    {
        private int naechster;
        private int naechsterPruefen;

        private async Task NaeheTick()
        {
            if (bekannte.Count == 0 || platzierenAktiv)
            {
                await Delay(1000);
                return;
            }
            var pos = GetEntityCoords(PlayerPedId(), true);
            var irgendwas = false;
            foreach (var t in bekannte.Values)
            {
                if (Rechnen.Abstand(pos.X, pos.Y, pos.Z, t.X, t.Y, t.Z) > Config.MarkerSichtweite) continue;
                irgendwas = true;
                FrontMarker(t.X, t.Y, t.Z, t.Richtung);
            }
            if (!irgendwas)
            {
                await Delay(500);
                return;
            }

            if (GetGameTimer() > naechsterPruefen)
            {
                naechsterPruefen = GetGameTimer() + 250;
                naechster = 0;
                var bester = Config.BeitrittAbstand;
                foreach (var kv in bekannte)
                {
                    var d = Rechnen.Abstand(pos.X, pos.Y, pos.Z, kv.Value.X, kv.Value.Y, kv.Value.Z);
                    if (d < bester)
                    {
                        bester = d;
                        naechster = kv.Key;
                    }
                }
            }
            TischC tisch;
            if (uiOffen || naechster == 0 || !bekannte.TryGetValue(naechster, out tisch)) return;

            var eigener = naechster == MeineId();
            if (!eigener && !Rechnen.VonVorne(tisch.X, tisch.Y, tisch.Richtung, pos.X, pos.Y))
            {
                Hilfe("Stell dich auf die Seite mit dem grünen Kreis.");
                return;
            }
            Hilfe(eigener ? "~INPUT_PICKUP~ Deinen Tisch öffnen" : $"~INPUT_PICKUP~ Beim Becherspiel von {tisch.Name} mitspielen");
            if (!IsControlJustPressed(0, 38)) return;
            if (eigener) TriggerServerEvent("toter-becherspiel:server:wiederOeffnen");
            else TriggerServerEvent("toter-becherspiel:server:beitreten", naechster);
        }

        private static void Hilfe(string text)
        {
            BeginTextCommandDisplayHelp("STRING");
            AddTextComponentSubstringPlayerName(text);
            EndTextCommandDisplayHelp(0, false, false, -1);
        }
    }
}
