using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterBecher.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterBecher.Client
{
    public partial class Main
    {
        private readonly int[] becher = new int[4];
        private readonly int[] platzHat = { 0, 1, 2, 3 };
        private int kugel;
        private bool tischAktiv;
        private float tX, tY, tZ, tRichtung, tHoehe;
        private int mischNummer;
        private float becherVersatz, kugelVersatz;   // Ursprung des Modells über seiner Unterseite

        // Abstand vom Ursprung zur Unterseite, auch wenn das Modell auf dem Kopf steht.
        private static float Unterseite(string modell, bool umgedreht)
        {
            var min = Vector3.Zero;
            var max = Vector3.Zero;
            GetModelDimensions((uint)new Model(modell).Hash, ref min, ref max);
            if (max.Z <= min.Z) return 0f;
            return umgedreht ? max.Z : -min.Z;
        }

        // Oberkante des Tisches: Strahl von oben auf die Tisch-Props. Die kommen vom Server und brauchen evtl. kurz.
        private async Task<float> Tischplatte(float x, float y, float z, float ersatz)
        {
            var bis = GetGameTimer() + 3000;
            while (GetGameTimer() < bis)
            {
                var test = StartShapeTestRay(x, y, z + 3f, x, y, z - 0.5f, 16, PlayerPedId(), 0);
                var getroffen = false;
                var punkt = Vector3.Zero;
                var normale = Vector3.Zero;
                var entity = 0;
                var stand = GetShapeTestResult(test, ref getroffen, ref punkt, ref normale, ref entity);
                while (stand == 1)
                {
                    await Delay(0);
                    stand = GetShapeTestResult(test, ref getroffen, ref punkt, ref normale, ref entity);
                }
                if (getroffen && punkt.Z > z + 0.05f) return punkt.Z - z;
                await Delay(150);
            }
            return ersatz;
        }

        private void Platz(int platz, float anheben, out float x, out float y, out float z)
        {
            float rx, ry;
            Rechnen.Rechts(tRichtung, out rx, out ry);
            var versatz = (platz - 2) * Config.BecherAbstand;
            x = tX + rx * versatz;
            y = tY + ry * versatz;
            z = tZ + tHoehe + anheben;
        }

        private async Task<int> Objekt(string modell, float x, float y, float z)
        {
            var m = new Model(modell);
            if (!m.IsInCdImage || !await m.Request(2000)) return 0;
            var obj = Function.Call<int>(Hash.CREATE_OBJECT, m.Hash, x, y, z, false, false, false);
            m.MarkAsNoLongerNeeded();
            if (obj == 0) return 0;
            SetEntityCollision(obj, false, false);
            FreezeEntityPosition(obj, true);
            return obj;
        }

        private async Task BecherSetzen(float x, float y, float z, float richtung, float hoehe)
        {
            BecherEntfernen();
            tX = x;
            tY = y;
            tZ = z;
            tRichtung = richtung;
            tischAktiv = true;
            await new Model(Config.BecherModell).Request(2000);
            await new Model(Config.KugelModell).Request(2000);
            becherVersatz = Unterseite(Config.BecherModell, Config.BecherUmgedreht);
            kugelVersatz = Unterseite(Config.KugelModell, false);
            tHoehe = await Tischplatte(x, y, z, hoehe) + Config.BecherExtraHoehe;
            if (!tischAktiv) return;
            for (var i = 1; i <= 3; i++)
            {
                platzHat[i] = i;
                float px, py, pz;
                Platz(i, 0f, out px, out py, out pz);
                var obj = await Objekt(Config.BecherModell, px, py, pz + becherVersatz);
                if (!tischAktiv)
                {
                    if (obj != 0) DeleteObject(ref obj);
                    return;
                }
                becher[i] = obj;
                if (obj == 0) continue;
                if (Config.BecherUmgedreht) SetEntityRotation(obj, 180f, 0f, richtung, 2, true);
                else SetEntityHeading(obj, richtung);
            }
        }

        private void BecherEntfernen()
        {
            tischAktiv = false;
            mischNummer++;
            for (var i = 1; i <= 3; i++)
            {
                var obj = becher[i];
                if (obj != 0 && DoesEntityExist(obj)) DeleteObject(ref obj);
                becher[i] = 0;
                platzHat[i] = i;
            }
            KugelWeg();
        }

        private void Setzen(int obj, int platz, float anheben)
        {
            if (obj == 0 || !DoesEntityExist(obj)) return;
            float x, y, z;
            Platz(platz, anheben, out x, out y, out z);
            SetEntityCoordsNoOffset(obj, x, y, z + becherVersatz, false, false, false);
        }

        private void BecherZuruecksetzen()
        {
            mischNummer++;
            for (var i = 1; i <= 3; i++)
            {
                platzHat[i] = i;
                Setzen(becher[i], i, 0f);
            }
        }

        private void BecherHeben(int platz, float anheben)
        {
            if (!tischAktiv || platz < 1 || platz > 3) return;
            Setzen(becher[platzHat[platz]], platz, anheben);
        }

        private async Task KugelZeigen(int platz)
        {
            KugelWeg();
            if (!tischAktiv || platz < 1 || platz > 3) return;
            float x, y, z;
            Platz(platz, 0f, out x, out y, out z);
            var obj = await Objekt(Config.KugelModell, x, y, z + kugelVersatz);
            if (!tischAktiv)
            {
                if (obj != 0) DeleteObject(ref obj);
                return;
            }
            kugel = obj;
        }

        private void KugelWeg()
        {
            if (kugel != 0 && DoesEntityExist(kugel)) DeleteObject(ref kugel);
            kugel = 0;
        }

        private async void Mischen(List<int[]> tausche, int dauer)
        {
            var nummer = ++mischNummer;
            foreach (var t in tausche)
            {
                if (nummer != mischNummer || !tischAktiv) return;
                await Tauschen(t[0], t[1], dauer, nummer);
            }
        }

        private async Task Tauschen(int a, int b, int dauer, int nummer)
        {
            var objA = becher[platzHat[a]];
            var objB = becher[platzHat[b]];
            float ax, ay, az, bx, by, bz;
            Platz(a, 0f, out ax, out ay, out az);
            Platz(b, 0f, out bx, out by, out bz);
            az += becherVersatz;
            bz += becherVersatz;
            var start = GetGameTimer();
            while (true)
            {
                if (nummer != mischNummer) return;
                var t = Math.Min(1f, (GetGameTimer() - start) / (float)Math.Max(1, dauer));
                var e = t * t * (3f - 2f * t);
                var bogen = (float)Math.Sin(t * Math.PI) * 0.06f;
                if (objA != 0) SetEntityCoordsNoOffset(objA, ax + (bx - ax) * e, ay + (by - ay) * e, az + bogen, false, false, false);
                if (objB != 0) SetEntityCoordsNoOffset(objB, bx + (ax - bx) * e, by + (ay - by) * e, bz, false, false, false);
                if (t >= 1f) break;
                await Delay(0);
            }
            var tmp = platzHat[a];
            platzHat[a] = platzHat[b];
            platzHat[b] = tmp;
        }
    }
}
