using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Zufallskarte mit Lanes: Vom Waldrand unten (manchmal auch von der Seite) fuehren 3–4 geschwungene Erdpfade
    /// zum Hof. Dazwischen liegen natuerliche Felseninseln (Felsbrocken auf dunklem Grund), die die Lanes bilden.
    /// Nachts tauchen die Zombies am Anfang der Lanes auf und folgen ihnen zum Haus (NightWave).
    /// Alles haengt am Karten-Seed im Spielstand – "Neues Spiel" wuerfelt einen neuen.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly List<List<Vector2>> lanes = new List<List<Vector2>>();
        int salt;   // Rausch-Versatz je Karte

        public IReadOnlyList<List<Vector2>> Lanes => lanes;

        /// <summary>Alle Lanes laufen hier zusammen (rechts unterhalb des Startbeets) und fuehren dann zum Vorplatz.</summary>
        static readonly Vector2 LaneMerge = new Vector2(2.2f, -3.4f);
        static readonly Vector2[] LaneTrunk = { new Vector2(1.9f, -1.4f), new Vector2(1.5f, 0.6f), new Vector2(1.0f, 2.4f) };

        void BuildLanes()
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            int n = 3 + (R() < 0.5f ? 1 : 0);
            bool sideLane = n == 4 && R() < 0.6f;
            int bottom = sideLane ? 3 : n;
            for (int i = 0; i < n; i++)
            {
                Vector2 start;
                if (sideLane && i == n - 1)
                {
                    float side = R() < 0.5f ? -1f : 1f;
                    start = new Vector2(side * (hw - 0.2f), -5f - R() * 4f);
                }
                else
                {
                    float slot = (i + 0.5f) / bottom;
                    start = new Vector2(-hw + 2f + slot * (MapW - 4f) + (R() - 0.5f) * 1.6f, -hh + 0.1f);
                }
                lanes.Add(MakeLane(start));
            }
        }

        List<Vector2> MakeLane(Vector2 start)
        {
            var pts = new List<Vector2>();
            var d = LaneMerge - start;
            var nrm = new Vector2(-d.y, d.x).normalized;
            float amp = 0.9f + R() * 1.3f, freq = 1.2f + R() * 1.4f, ph = R() * 6.28f;
            int steps = Mathf.Max(4, Mathf.CeilToInt(d.magnitude / 0.7f));
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                // schwingt in der Mitte am staerksten, Anfang und Zusammenfluss ruhig
                float sw = Mathf.Sin(t * Mathf.PI * freq + ph) * amp * Mathf.Sin(t * Mathf.PI);
                pts.Add(start + d * t + nrm * sw);
            }
            pts.AddRange(LaneTrunk);
            return pts;
        }

        /// <summary>0..1: wie stark hier eine Felseninsel liegt (zwischen den Lanes, nicht am Hof).</summary>
        float IslandValue(Vector2 p)
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            if (p.y > -1.6f || Mathf.Abs(p.x) > hw - 0.4f || p.y < -hh + 0.4f) return 0f;
            if ((p - HousePos).magnitude < 6.5f || NearStartArea(p, 1.6f, 1.2f)) return 0f;
            float d = DistToPath(p);
            if (d < 1.1f) return 0f;   // die Lane selbst bleibt frei
            // alles zwischen den Lanes wird Fels, mit ausgefranstem Rand; nach oben (zum Hof) laeuft es aus
            float edge = (Fbm(p.x * 0.6f, p.y * 0.6f, 51 + salt) - 0.5f) * 1.1f;
            float v = Mathf.InverseLerp(1.35f, 2.0f, d + edge);
            v *= Mathf.InverseLerp(-1.6f, -3.4f, p.y + edge * 0.6f);
            v *= Mathf.InverseLerp(hw - 0.4f, hw - 1.6f, Mathf.Abs(p.x));
            return Mathf.Clamp01(v);
        }

        /// <summary>Felsbrocken auf die Inseln setzen (dicht, mit Schatten); reserviert den Platz, damit dort nichts anderes waechst.</summary>
        void PlaceRockIslands()
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            var cells = new List<Vector2>();
            for (float y = -hh + 0.5f; y < -1.4f; y += 0.36f)
                for (float x = -hw + 0.5f; x < hw - 0.4f; x += 0.36f)
                {
                    var p = new Vector2(x + (R() - 0.5f) * 0.3f, y + (R() - 0.5f) * 0.3f);
                    if (IslandValue(p) > 0.5f) cells.Add(p);
                }
            // gemischt, damit grosse und kleine Brocken verteilt liegen
            for (int i = cells.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }
            foreach (var p in cells)
            {
                float core = IslandValue(p);
                bool big = core > 0.9f && R() < 0.7f;
                float r = big ? 0.42f : 0.24f;
                if (!Free(p, r)) continue;
                Reserve(p, r);
                float roll = R();
                if (big)
                {
                    if (roll < 0.6f) { PlaceSized("hh_rock_big", p, 1.0f + R() * 0.45f, R() < 0.5f); Shadow(p, 1.25f, 0.3f, 0.38f); }
                    else Place("sk_Stones", p, 1.2f + R() * 0.5f, R() < 0.5f, 0.04f);
                }
                else
                {
                    if (roll < 0.45f) { PlaceSized("hh_rock_medium", p, 0.45f + R() * 0.15f, R() < 0.5f); Shadow(p, 0.65f, 0.16f); }
                    else if (roll < 0.8f) Place("sk_Stones", p, 0.65f + R() * 0.3f, R() < 0.5f, 0.04f);
                    else { PlaceSized("hh_rock_small", p, 0.3f, R() < 0.5f); Shadow(p, 0.4f, 0.1f); }
                }
            }
        }
    }
}
