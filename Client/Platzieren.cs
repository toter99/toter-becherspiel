using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterBecher.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterBecher.Client
{
    public partial class Main
    {
        private bool platzierenAktiv;
        private readonly List<int> geister = new List<int>();

        // Höhe jedes Tischteils über dem Boden. Gestapelte Teile landen mit ihrer Unterkante genau auf der
        // Oberkante des vorherigen, egal wo der Ursprung des Modells liegt. Rückgabe: Oberkante = Becherhöhe.
        private async Task<float> Hoehen(List<float> hoehen)
        {
            var oben = 0f;
            foreach (var e in Config.Tisch)
            {
                var m = new Model(e.Modell);
                float minZ = 0f, maxZ = Config.EbeneErsatzHoehe;
                if (m.IsInCdImage && await m.Request(2000))
                {
                    var min = Vector3.Zero;
                    var max = Vector3.Zero;
                    GetModelDimensions((uint)m.Hash, ref min, ref max);
                    if (max.Z > min.Z)
                    {
                        minZ = min.Z;
                        maxZ = max.Z;
                    }
                }
                var h = e.Stapeln ? oben - minZ : e.Z;
                oben = h + maxZ;
                hoehen.Add(h);
            }
            return oben;
        }

        private async void Platzieren(string stufe, bool gezinkt)
        {
            if (platzierenAktiv || Config.Tisch.Count == 0) return;
            platzierenAktiv = true;
            var hoehen = new List<float>();
            var oben = await Hoehen(hoehen);

            foreach (var e in Config.Tisch)
            {
                var obj = await Objekt(e.Modell, 0f, 0f, -100f);
                if (obj != 0) SetEntityAlpha(obj, 170, 0);
                geister.Add(obj);
            }

            var richtung = GetEntityHeading(PlayerPedId());
            var bestaetigt = false;
            float bx = 0, by = 0, bz = 0;

            while (platzierenAktiv)
            {
                await Delay(0);
                var ped = PlayerPedId();
                var pos = GetEntityCoords(ped, true);
                var vor = GetEntityForwardVector(ped);
                bx = pos.X + vor.X * Config.PlatzierAbstand;
                by = pos.Y + vor.Y * Config.PlatzierAbstand;
                bz = pos.Z - 1f;
                var boden = 0f;
                if (GetGroundZFor_3dCoord(bx, by, pos.Z + 1.5f, ref boden, false)) bz = boden;

                float rx, ry, vx, vy;
                Rechnen.Rechts(richtung, out rx, out ry);
                Rechnen.Vorne(richtung, out vx, out vy);
                for (var i = 0; i < geister.Count; i++)
                {
                    var g = geister[i];
                    if (g == 0) continue;
                    var e = Config.Tisch[i];
                    SetEntityCoordsNoOffset(g, bx + rx * e.X + vx * e.Y, by + ry * e.X + vy * e.Y, bz + hoehen[i], false, false, false);
                    SetEntityHeading(g, richtung);
                }
                FrontMarker(bx, by, bz, richtung);

                foreach (var c in new[] { 14, 15, 16, 17, 24, 25, 140, 141, 142, 37, 44, 177, 200, 241, 242 }) DisableControlAction(0, c, true);
                BeginTextCommandDisplayHelp("STRING");
                AddTextComponentSubstringPlayerName("Drehen: Mausrad oder Pfeiltasten links/rechts~n~");
                AddTextComponentSubstringPlayerName("~INPUT_PICKUP~ [E] aufstellen~n~");
                AddTextComponentSubstringPlayerName("~INPUT_FRONTEND_RRIGHT~ [Rücktaste] abbrechen");
                EndTextCommandDisplayHelp(0, false, false, -1);

                if (IsDisabledControlJustPressed(0, 14) || IsDisabledControlJustPressed(0, 241)) richtung = (richtung + Config.MausradSchritt) % 360f;
                if (IsDisabledControlJustPressed(0, 15) || IsDisabledControlJustPressed(0, 242)) richtung = (richtung - Config.MausradSchritt + 360f) % 360f;
                if (IsControlPressed(0, 174)) richtung = (richtung + Config.DrehGeschwindigkeit * GetFrameTime()) % 360f;
                if (IsControlPressed(0, 175)) richtung = (richtung - Config.DrehGeschwindigkeit * GetFrameTime() + 360f) % 360f;

                if (IsControlJustPressed(0, 38))
                {
                    bestaetigt = true;
                    break;
                }
                if (IsDisabledControlJustPressed(0, 177) || IsDisabledControlJustPressed(0, 200) || IsPedInAnyVehicle(PlayerPedId(), false)) break;
            }

            GeisterEntfernen();
            platzierenAktiv = false;
            if (!bestaetigt)
            {
                Melden("Aufstellen abgebrochen.", "error", 3000);
                return;
            }
            TriggerServerEvent("toter-becherspiel:server:aufstellen", stufe, gezinkt, bx, by, bz, richtung,
                hoehen.Cast<object>().ToList(), oben);
        }

        private void GeisterEntfernen()
        {
            platzierenAktiv = false;
            for (var i = 0; i < geister.Count; i++)
            {
                var g = geister[i];
                if (g != 0 && DoesEntityExist(g)) DeleteObject(ref g);
            }
            geister.Clear();
        }

        private void FrontMarker(float x, float y, float z, float richtung)
        {
            float fx, fy;
            Rechnen.Front(richtung, out fx, out fy);
            DrawMarker(25, x + fx * Config.MarkerAbstand, y + fy * Config.MarkerAbstand, z + 0.03f, 0f, 0f, 0f, 0f, 0f, 0f,
                0.7f, 0.7f, 0.7f, 34, 224, 107, 170, false, false, 2, false, null, null, false);
        }
    }
}
