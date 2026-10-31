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
                    case "water": list.AddRange(DirCells(p, at, WaterRadiusOf(p))); break;
                    case "auto": list.AddRange(DirCells(p, at, 1)); break;
                    case "fert": Square(list, at, 1); break;
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

        // ------------------------------------------------------------------ Blickrichtung (Wasser- und Klick-Pflanzen)

        public static bool HasDirection(PlantedState p) => p != null && (p.Role == "water" || p.Role == "auto");
        static readonly string[] DirNames = { "Norden", "Osten", "Süden", "Westen" };
        public static string DirName(int d) => DirNames[((d % 4) + 4) % 4];

        static Cell Step(Cell c, int dir, int n) => (((dir % 4) + 4) % 4) switch
        {
            0 => new Cell(c.x, c.y + n), 1 => new Cell(c.x + n, c.y), 2 => new Cell(c.x, c.y - n), _ => new Cell(c.x - n, c.y)
        };

        /// <summary>Die len Felder vor der Pflanze in Blickrichtung.</summary>
        static List<Cell> DirCells(PlantedState p, Cell at, int len)
        {
            var list = new List<Cell>();
            for (int i = 1; i <= len; i++) list.Add(Step(at, p.dir, i));
            return list;
        }

        /// <summary>Liegt target in Blickrichtung von p, hoechstens len Felder weit?</summary>
        static bool InLine(PlantedState p, Cell target, int len)
        {
            for (int i = 1; i <= len; i++) if (Step(new Cell(p.x, p.y), p.dir, i).Equals(target)) return true;
            return false;
        }

        /// <summary>Pflanze eine Vierteldrehung weiter drehen (im Uhrzeigersinn).</summary>
        public void TurnPlant(PlantedState p)
        {
            if (!HasDirection(p)) return;
            p.dir = (p.dir + 1) % 4;
            var c = new Cell(p.x, p.y);
            PunchField(c, 0.5f);
            if (plantViews.TryGetValue(c, out var v)) { v.sy = Mathf.Min(v.sy, -0.08f); v.flash = Mathf.Max(v.flash, 0.3f); }
            arrowPop = 1f;
            Sfx.Play("tick", 1.3f, 0.6f);
            Haptics.Tap(10);
            save.Save();
        }

        SpriteRenderer dirArrow;
        float arrowPop;
        static Sprite arrowTri;

        /// <summary>Pfeil an der Pflanze, der in ihre Blickrichtung zeigt (nur solange ihr Bereich angezeigt wird).</summary>
        void UpdateDirArrow(PlantedState p, Cell at)
        {
            if (dirArrow == null)
            {
                dirArrow = new GameObject("DirArrow").AddComponent<SpriteRenderer>();
                dirArrow.transform.SetParent(transform, false);
                dirArrow.sprite = ArrowTri;
                dirArrow.sortingOrder = 31090;
            }
            bool show = HasDirection(p);
            dirArrow.enabled = show;
            if (!show) return;
            arrowPop = Mathf.MoveTowards(arrowPop, 0f, Time.unscaledDeltaTime * 3f);
            var step = Step(new Cell(0, 0), p.dir, 1);
            var dv = new Vector2(step.x, step.y);
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 0.03f;
            dirArrow.transform.position = CellCenter(at) + new Vector2(0, SurfaceH(at)) + dv * (CellSize * 0.5f + bob);
            dirArrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg);
            dirArrow.transform.localScale = Vector3.one * (0.22f * (1f + arrowPop * 0.4f)) / Mathf.Max(0.01f, ArrowTri.bounds.size.x);
            dirArrow.color = new Color(0.35f, 1f, 0.45f, 0.95f);
        }

        static Sprite ArrowTri
        {
            get
            {
                if (arrowTri != null) return arrowTri;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // Dreieck nach rechts, mit dunklem Rand
                        float half = (n - 1 - x) * 0.5f;
                        float d = Mathf.Abs(y - n / 2f);
                        bool inside = x >= 6 && d <= half - 2;
                        bool rim = x >= 4 && d <= half && !inside;
                        px[y * n + x] = inside ? Color.white : rim ? new Color(0.1f, 0.3f, 0.1f, 1f) : Color.clear;
                    }
                tex.SetPixels(px); tex.Apply();
                arrowTri = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return arrowTri;
            }
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
            PlantedState who = null; Cell at = default;
            if (dragging && carrying != null && hoverSlot.HasValue) { who = carrying; at = hoverSlot.Value; }
            else if (!dragging && Inspected != null && save.planted.Contains(Inspected)) { who = Inspected; at = new Cell(Inspected.x, Inspected.y); }
            if (who != null) cells = AreaCells(who, at);
            if (who != null) UpdateDirArrow(who, at); else if (dirArrow != null) dirArrow.enabled = false;
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
