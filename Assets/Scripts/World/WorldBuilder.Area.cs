using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Wirkungsbereich: Ist das Info-Fenster einer Pflanze offen oder wird sie gerade gezogen, leuchten alle Felder in
    /// ihrer Reichweite gruen und durchsichtig – beim Ziehen fuer den Platz unter dem Finger, damit man vorher sieht,
    /// was sie dort erreicht. Gilt fuer alle Pflanzen mit Reichweite: Wasser, Duenger, Verstaerker, Automatik, Honig,
    /// Licht, Kampf (Schussweite), Stuetzranke und die Helfer.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly List<SpriteRenderer> areaPool = new List<SpriteRenderer>();
        static readonly Color AreaCol = new Color(0.35f, 1f, 0.4f, 0.22f);

        /// <summary>Zellen, die diese Pflanze erreichen wuerde, wenn sie auf at stuende (ohne at selbst).</summary>
        public List<Cell> AreaCells(PlantedState p, Cell at)
        {
            var list = new List<Cell>();
            if (p == null) return list;
            // kurz an den Zielplatz versetzen, damit ortsabhaengige Boni (Saftrohr, Wurzelherz) stimmen
            int ox = p.x, oy = p.y;
            p.x = at.x; p.y = at.y;
            try
            {
                var h = PlantBalance.I.Helpers;
                switch (p.Role)
                {
                    case "water": Square(list, at, WaterRadiusOf(p)); break;
                    case "fert": case "auto": Square(list, at, 1); break;
                    case "boost": case "bee": Square(list, at, 2); break;
                    case "transport": Square(list, at, h.transportRadius); break;
                    case "sower": Square(list, at, h.sowerRadius); break;
                    case "pipe": Square(list, at, h.pipeRadius); break;
                    case "light": Circle(list, at, 2.2f + 0.3f * (p.Lv - 1)); break;
                    case "support": Circle(list, at, SupportRadius(p)); break;
                    case "combat":
                        {
                            var g = p.Genome;
                            float r = g.Range + PlantBalance.I.LevelRange(p.Lv) + PotRange(p);
                            Circle(list, at, r);
                            break;
                        }
                }
            }
            finally { p.x = ox; p.y = oy; }
            return list;
        }

        static void Square(List<Cell> list, Cell at, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                    if (dx != 0 || dy != 0) list.Add(new Cell(at.x + dx, at.y + dy));
        }

        static void Circle(List<Cell> list, Cell at, float r)
        {
            int n = Mathf.CeilToInt(r / CellSize);
            var c0 = CellCenter(at);
            for (int dx = -n; dx <= n; dx++)
                for (int dy = -n; dy <= n; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var c = new Cell(at.x + dx, at.y + dy);
                    if ((CellCenter(c) - c0).sqrMagnitude <= r * r) list.Add(c);
                }
        }

        /// <summary>Bereich zeichnen: beim Ziehen fuer den Zielplatz, sonst fuer die Pflanze im offenen Info-Fenster.</summary>
        void UpdateAreaView()
        {
            List<Cell> cells = null;
            if (dragging && carrying != null && hoverSlot.HasValue) cells = AreaCells(carrying, hoverSlot.Value);
            else if (!dragging && Inspected != null && save.planted.Contains(Inspected)) cells = AreaCells(Inspected, new Cell(Inspected.x, Inspected.y));
            int n = cells != null ? cells.Count : 0;
            while (areaPool.Count < n)
            {
                var sr = new GameObject("Area").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Art.Get("white");
                areaPool.Add(sr);
            }
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
            for (int i = 0; i < areaPool.Count; i++)
            {
                var sr = areaPool[i];
                sr.enabled = i < n;
                if (i >= n) continue;
                var c = cells[i];
                // auf Mauer oder Pult: oben auf die Flaeche heben und davor zeichnen
                float lift = SurfaceH(c);
                sr.transform.position = CellCenter(c) + new Vector2(0, lift);
                sr.sortingOrder = lift > 0f ? Order(c.y * CellSize) + 4 : -32289;
                float k = sr.sprite != null ? CellSize * 0.96f / sr.sprite.bounds.size.x : CellSize;
                sr.transform.localScale = new Vector3(k, k, 1);
                sr.color = new Color(AreaCol.r, AreaCol.g, AreaCol.b, AreaCol.a * pulse);
            }
        }
    }
}
