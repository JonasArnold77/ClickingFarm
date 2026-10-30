using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Mauern planen: Im Mauer-Modus setzt der erste Klick den Anfang. Bis zum Finger bzw. zur Maus zeigt eine gruene,
    /// durchsichtige Mauer, wie weit sie gerade reicht (rot: dort geht es nicht oder das Holz reicht nicht mehr).
    /// Der zweite Klick macht daraus echte Mauern. Zweimal auf dieselbe leere Mauer klicken baut sie ab.
    /// Holz pro Mauerstueck darf ein Bruchteil sein (plants.json -> wall.wood), der Rest wird als Schuld mitgezaehlt.
    /// </summary>
    public partial class WorldBuilder
    {
        Cell? wallAnchor;
        readonly List<SpriteRenderer> planViews = new List<SpriteRenderer>();
        readonly List<Cell> planOk = new List<Cell>();
        Cell planTo;

        static readonly Color PlanGreen = new Color(0.45f, 1f, 0.45f, 0.55f), PlanRed = new Color(1f, 0.4f, 0.35f, 0.45f);

        public bool WallAnchorActive => wallAnchor.HasValue;

        // ------------------------------------------------------------------ Holz (auch Bruchteile)

        float WallWood => PlantBalance.I.Wall.wood;
        /// <summary>Holz, das noch frei ist (ganze Scheite minus angefangene Schuld).</summary>
        float WoodLeft => save.wood - save.woodDebt;

        void PayWallWood()
        {
            save.woodDebt += WallWood;
            while (save.woodDebt >= 0.999f && save.wood > 0) { save.wood--; save.woodDebt -= 1f; }
            if (save.woodDebt < 0.001f) save.woodDebt = 0f;
            WoodChanged?.Invoke(save.wood);
        }

        void RefundWallWood(Vector2 at)
        {
            save.woodDebt -= WallWood;
            int whole = 0;
            while (save.woodDebt < -0.001f) { whole++; save.woodDebt += 1f; }
            if (whole > 0) AddWood(whole, at); else WoodChanged?.Invoke(save.wood);
        }

        // ------------------------------------------------------------------ Planen und Bauen

        /// <summary>Klick im Mauer-Modus. false = hier kann man keine Mauer anfangen (dann Baum faellen / Fels abbauen).</summary>
        public bool WallClick(Vector2 w)
        {
            var c = TapCell(w);
            if (!wallAnchor.HasValue)
            {
                if (!HasWall(c) && !CanPlaceWall(c)) return false;
                wallAnchor = c;
                UpdateWallPlan(w);
                Sfx.Play("tick", 1.2f, 0.5f);
                Haptics.Tap(8);
                return true;
            }
            var a = wallAnchor.Value;
            if (c.Equals(a) && walls.TryGetValue(a, out var v))
            {
                // zweimal dieselbe Mauer: abbauen (nur leer)
                if (PlantAt(a) == null && !HasPedestal(a)) { RemoveWall(a, v, false); RefundWallWood(CellCenter(a) + new Vector2(0, 0.5f)); }
                else Sfx.Play("error", 1f, 0.5f);
                CancelWallPlan();
                save.Save();
                return true;
            }
            UpdateWallPlan(w);
            if (planOk.Count == 0)
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), WoodLeft < WallWood ? "Zu wenig Holz – fälle Bäume!" : "Hier geht keine Mauer", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                return true;
            }
            int i = 0;
            foreach (var pc in planOk) { BuildWall(pc, i++ < 3); }
            CancelWallPlan();
            save.Save();
            return true;
        }

        public void CancelWallPlan()
        {
            wallAnchor = null;
            planOk.Clear();
            foreach (var sr in planViews) if (sr != null) sr.enabled = false;
        }

        /// <summary>Gruene Vorschau vom Anfang bis zur Stelle unter Finger/Maus neu zeichnen.</summary>
        public void UpdateWallPlan(Vector2 w)
        {
            if (!wallAnchor.HasValue) return;
            var to = TapCell(w);
            planTo = to;
            var path = new List<Cell> { wallAnchor.Value };
            var cur = wallAnchor.Value;
            int guard = 0;
            while (!cur.Equals(to) && guard++ < 80)
            {
                int dx = to.x - cur.x, dy = to.y - cur.y;
                cur = Mathf.Abs(dx) >= Mathf.Abs(dy) ? new Cell(cur.x + System.Math.Sign(dx), cur.y) : new Cell(cur.x, cur.y + System.Math.Sign(dy));
                path.Add(cur);
            }
            planOk.Clear();
            float budget = WoodLeft;
            int n = 0;
            foreach (var c in path)
            {
                if (HasWall(c)) continue;   // schon da: nichts zeigen
                bool ok = CanPlaceWall(c) && budget >= WallWood - 0.0001f;
                if (ok) { budget -= WallWood; planOk.Add(c); }
                var sr = PlanView(n++);
                sr.enabled = true;
                sr.transform.position = WallFoot(c);
                sr.sortingOrder = Order(WallFoot(c).y + 0.1f) + 3;
                sr.color = ok ? PlanGreen : PlanRed;
            }
            for (int i = n; i < planViews.Count; i++) if (planViews[i] != null) planViews[i].enabled = false;
        }

        SpriteRenderer PlanView(int i)
        {
            while (planViews.Count <= i)
            {
                var sr = new GameObject("WallPlan").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = WallSprite;
                sr.sharedMaterial = UnlitMat;
                sr.transform.localScale = Vector3.one * CellSize / Mathf.Max(0.01f, WallSprite.bounds.size.x);
                planViews.Add(sr);
            }
            return planViews[i];
        }

        void UpdateWallPlanPulse()
        {
            if (!wallAnchor.HasValue) return;
            // sanftes Pulsieren, damit die Vorschau als Vorschau erkennbar bleibt
            float k = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var sr in planViews)
            {
                if (sr == null || !sr.enabled) continue;
                var c = sr.color; c.a = (c.g > 0.9f ? PlanGreen.a : PlanRed.a) * k; sr.color = c;
            }
        }
    }
}
