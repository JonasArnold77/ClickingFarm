using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Zufallskarte mit Lanes im Feldraster, uebersichtlich wie ein Strassennetz: drei Strassen kommen gerade vom
    /// Waldrand unten herauf (hoechstens ein rechtwinkliger Versatz je Strasse) und muenden in eine Querstrasse;
    /// von dort fuehrt ein gerader Weg zum Haus. Manchmal kommt eine Seitenstrasse vom Rand und trifft im rechten
    /// Winkel auf eine aeussere Strasse. Neben jeder Strasse bleiben je drei Feldreihen frei zum Bebauen (sie
    /// schimmern als moegliche Felder durch); erst dahinter liegen die Felseninseln.
    /// Nachts tauchen die Zombies am Anfang der Strassen auf und folgen ihnen zum Haus (NightWave).
    /// Alles haengt am Karten-Seed im Spielstand – "Neues Spiel" wuerfelt einen neuen.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly List<List<Vector2>> lanes = new List<List<Vector2>>();
        int salt;   // Rausch-Versatz je Karte

        public IReadOnlyList<List<Vector2>> Lanes => lanes;

        /// <summary>Querstrasse (Zeile) und gerader Weg zum Vorplatz (Spalte), in Feldzellen.</summary>
        const int CollectorRow = -6, TrunkCol = 3, TrunkTop = 4;
        /// <summary>So viele Feldreihen bleiben neben jeder Strasse frei zum Bebauen.</summary>
        public const int BuildBand = 3;

        readonly HashSet<Cell> laneCells = new HashSet<Cell>();
        readonly HashSet<Cell> buildable = new HashSet<Cell>();
        Sprite laneSprite;

        public bool IsLaneCell(Cell c) => laneCells.Contains(c);
        public bool IsBuildable(Cell c) => buildable.Contains(c);

        void BuildLanes()
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            int minX = Mathf.CeilToInt((-hw + 1.2f) / CellSize), maxX = Mathf.FloorToInt((hw - 1.2f) / CellSize) - 1;
            int bottomY = Mathf.FloorToInt((-hh + 0.05f) / CellSize);
            // drei Strassen: links, Mitte (laeuft gerade in den Weg zum Haus), rechts – weit genug auseinander
            int[] cols = { -11 + rnd.Next(-1, 2), TrunkCol + rnd.Next(-1, 2), 11 + rnd.Next(-1, 2) };
            var paths = new List<List<Cell>>();
            foreach (var x0 in cols)
            {
                var cells = new List<Cell>();
                var cur = new Cell(x0, bottomY);
                cells.Add(cur);
                // hoechstens ein Versatz, im rechten Winkel, um 2 Zellen (Mitte: nur wenn sie dadurch nicht zu nah kommt)
                int jogRow = R() < 0.6f ? rnd.Next(bottomY + 5, CollectorRow - 6) : int.MinValue;
                int jog = R() < 0.5f ? -2 : 2;
                if (x0 == cols[1] && Mathf.Abs(x0 + jog - TrunkCol) > 2) jog = -jog;
                while (cur.y < CollectorRow)
                {
                    if (cur.y == jogRow) for (int k = 0; k < Mathf.Abs(jog); k++) { cur = new Cell(cur.x + System.Math.Sign(jog), cur.y); cells.Add(cur); }
                    cur = new Cell(cur.x, cur.y + 1); cells.Add(cur);
                }
                while (cur.x != TrunkCol) { cur = new Cell(cur.x + (TrunkCol > cur.x ? 1 : -1), cur.y); cells.Add(cur); }
                while (cur.y < TrunkTop) { cur = new Cell(cur.x, cur.y + 1); cells.Add(cur); }
                paths.Add(cells);
            }
            // Seitenstrasse: vom linken oder rechten Rand waagerecht auf die aeussere Strasse, dann ihr nach
            if (R() < 0.45f)
            {
                bool left = R() < 0.5f;
                var outer = paths[left ? 0 : 2];
                int row = rnd.Next(bottomY + 7, CollectorRow - 5);
                int join = outer.FindIndex(c => c.y == row);
                if (join >= 0)
                {
                    var cells = new List<Cell>();
                    int x = left ? minX - 2 : maxX + 2, tx = outer[join].x;
                    for (; x != tx; x += left ? 1 : -1) cells.Add(new Cell(x, row));
                    cells.AddRange(outer.GetRange(join, outer.Count - join));
                    paths.Add(cells);
                }
            }
            foreach (var cells in paths)
            {
                var pts = new List<Vector2>();
                foreach (var c in cells) { laneCells.Add(c); pts.Add(CellCenter(c)); }
                lanes.Add(pts);
            }
            // Bauflaeche: je drei Reihen neben den Strassen, dazu das Startbeet
            foreach (var c in laneCells)
                for (int dx = -BuildBand; dx <= BuildBand; dx++)
                    for (int dy = -BuildBand; dy <= BuildBand; dy++)
                    {
                        var b = new Cell(c.x + dx, c.y + dy);
                        if (!laneCells.Contains(b) && b.x >= minX - 1 && b.x <= maxX + 1 && b.y > bottomY) buildable.Add(b);
                    }
            var a0 = CellAt(StartArea.min + new Vector2(0.01f, 0.01f));
            var a1 = CellAt(StartArea.max - new Vector2(0.01f, 0.01f));
            for (int x = a0.x; x <= a1.x; x++) for (int y = a0.y; y <= a1.y; y++) if (!laneCells.Contains(new Cell(x, y))) buildable.Add(new Cell(x, y));
        }

        /// <summary>Die Strassen als Erdkacheln im Feldraster zeichnen (wie die Felder).</summary>
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
            // Strasse und die drei Bau-Reihen daneben bleiben frei; dahinter Fels mit ausgefranstem Rand (nur nach aussen)
            float edge = (Fbm(p.x * 0.6f, p.y * 0.6f, 51 + salt) - 0.5f) * 1.1f;
            float free = (BuildBand + 0.75f) * CellSize;
            if (d < free) return 0f;
            float v = Mathf.InverseLerp(free, free + 0.5f, d - Mathf.Abs(edge) * 0.8f);
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
