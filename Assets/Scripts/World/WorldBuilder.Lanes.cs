using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Zufallskarte mit Lanes: Vom Waldrand unten (manchmal auch von der Seite) fuehren 3–4 Erdpfade im Feldraster
    /// (gerade Abschnitte nach oben und zur Seite) zum Hof. Dazwischen liegen natuerliche Felseninseln (Felsbrocken auf dunklem Grund), die die Lanes bilden.
    /// Nachts tauchen die Zombies am Anfang der Lanes auf und folgen ihnen zum Haus (NightWave).
    /// Alles haengt am Karten-Seed im Spielstand – "Neues Spiel" wuerfelt einen neuen.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly List<List<Vector2>> lanes = new List<List<Vector2>>();
        int salt;   // Rausch-Versatz je Karte

        public IReadOnlyList<List<Vector2>> Lanes => lanes;

        /// <summary>Zusammenfluss (rechts unterhalb des Startbeets) und der gemeinsame Weg zum Vorplatz, in Feldzellen.</summary>
        static readonly Cell MergeCell = new Cell(3, -6);
        static readonly Cell[] TrunkCorners = { new Cell(3, 1), new Cell(1, 1), new Cell(1, 4) };

        readonly HashSet<Cell> laneCells = new HashSet<Cell>();
        Sprite laneSprite;

        public bool IsLaneCell(Cell c) => laneCells.Contains(c);

        /// <summary>Lanes im Feldraster: Abschnitte gehen nur gerade nach oben oder zur Seite.</summary>
        void BuildLanes()
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            int minX = Mathf.CeilToInt((-hw + 1.2f) / CellSize), maxX = Mathf.FloorToInt((hw - 1.2f) / CellSize) - 1;
            int bottomY = Mathf.FloorToInt((-hh + 0.05f) / CellSize);
            int n = 3 + (R() < 0.5f ? 1 : 0);
            bool sideLane = n == 4 && R() < 0.6f;
            int bottom = sideLane ? 3 : n;
            for (int i = 0; i < n; i++)
            {
                Cell start;
                if (sideLane && i == n - 1)
                {
                    bool left = R() < 0.5f;
                    start = new Cell(left ? minX - 1 : maxX + 1, Mathf.FloorToInt((-5f - R() * 4f) / CellSize));
                }
                else
                {
                    float slot = (i + 0.5f) / bottom;
                    int x = Mathf.RoundToInt(Mathf.Lerp(minX + 1, maxX - 1, slot) + (R() - 0.5f) * 3f);
                    start = new Cell(Mathf.Clamp(x, minX, maxX), bottomY);
                }
                lanes.Add(MakeLane(start, minX, maxX));
            }
        }

        List<Vector2> MakeLane(Cell start, int minX, int maxX)
        {
            var cells = new List<Cell> { start };
            var cur = start;
            // Seiteneingang: erst ein Stueck nach innen
            if (start.x < minX || start.x > maxX)
            {
                int dx = start.x < minX ? 1 : -1;
                for (int k = 0; k < 3 + rnd.Next(3); k++) { cur = new Cell(cur.x + dx, cur.y); cells.Add(cur); }
            }
            while (cur.y < MergeCell.y || cur.x != MergeCell.x)
            {
                int dy = MergeCell.y - cur.y, dx = MergeCell.x - cur.x;
                if (dy <= Mathf.Abs(dx) + 1)
                {
                    // kurz vor dem Zusammenfluss: erst zur Seite, dann hoch
                    if (dx != 0) cur = new Cell(cur.x + (dx > 0 ? 1 : -1), cur.y);
                    else cur = new Cell(cur.x, cur.y + 1);
                    cells.Add(cur);
                    continue;
                }
                if (R() < 0.35f)
                {
                    // seitlicher Abschnitt: meist Richtung Ziel, manchmal weg davon
                    int dir = R() < 0.7f ? (dx >= 0 ? 1 : -1) : (dx >= 0 ? -1 : 1);
                    int len = 2 + rnd.Next(4);
                    for (int k = 0; k < len; k++)
                    {
                        int nx = cur.x + dir;
                        if (nx < minX || nx > maxX) break;
                        cur = new Cell(nx, cur.y); cells.Add(cur);
                    }
                }
                int up = 2 + rnd.Next(5);
                for (int k = 0; k < up && cur.y < MergeCell.y; k++) { cur = new Cell(cur.x, cur.y + 1); cells.Add(cur); }
            }
            // gemeinsamer Weg zum Vorplatz
            foreach (var corner in TrunkCorners)
                while (cur.x != corner.x || cur.y != corner.y)
                {
                    if (cur.x != corner.x) cur = new Cell(cur.x + (corner.x > cur.x ? 1 : -1), cur.y);
                    else cur = new Cell(cur.x, cur.y + (corner.y > cur.y ? 1 : -1));
                    cells.Add(cur);
                }
            var pts = new List<Vector2>();
            foreach (var c in cells) { laneCells.Add(c); pts.Add(CellCenter(c)); }
            return pts;
        }

        /// <summary>Die Lanes als Erdkacheln im Feldraster zeichnen (wie die Felder).</summary>
        void PlaceLaneTiles()
        {
            if (laneSprite == null) laneSprite = GroundMaterials.TileSprite(1, 5);
            if (laneSprite == null) return;
            foreach (var c in laneCells)
            {
                var sr = new GameObject("Lane").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.transform.position = CellCenter(c);
                sr.transform.localScale = Vector3.one * CellSize * 1.02f;
                sr.sprite = laneSprite;
                sr.color = new Color(0.95f, 0.9f, 0.85f);
                sr.sortingOrder = -32310;
            }
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
