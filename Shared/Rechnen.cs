using System;

namespace ToterBecher.Shared
{
    public static class Rechnen
    {
        public static uint Hash(string text)
        {
            uint h = 0;
            foreach (var c in text.ToLowerInvariant())
            {
                h += c;
                h += h << 10;
                h ^= h >> 6;
            }
            h += h << 3;
            h ^= h >> 11;
            h += h << 15;
            return h;
        }

        public static void Rechts(float richtung, out float x, out float y)
        {
            var r = richtung * Math.PI / 180.0;
            x = (float)Math.Cos(r);
            y = (float)Math.Sin(r);
        }

        public static void Vorne(float richtung, out float x, out float y)
        {
            var r = richtung * Math.PI / 180.0;
            x = (float)-Math.Sin(r);
            y = (float)Math.Cos(r);
        }

        // Richtung, in der die Spieler stehen sollen (dort liegt auch der grüne Marker).
        public static void Front(float richtung, out float x, out float y)
        {
            Vorne(richtung, out x, out y);
            if (Config.VorneUmdrehen) return;
            x = -x;
            y = -y;
        }

        public static float Abstand(float ax, float ay, float az, float bx, float by, float bz)
        {
            var dx = ax - bx;
            var dy = ay - by;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static bool VonVorne(float tx, float ty, float richtung, float px, float py)
        {
            var dx = px - tx;
            var dy = py - ty;
            var flach = (float)Math.Sqrt(dx * dx + dy * dy);
            if (flach < 0.01f) return true;
            float fx, fy;
            Front(richtung, out fx, out fy);
            var dot = Math.Max(-1.0, Math.Min(1.0, (dx * fx + dy * fy) / flach));
            return Math.Acos(dot) * 180.0 / Math.PI <= Config.VorneToleranz;
        }
    }
}
