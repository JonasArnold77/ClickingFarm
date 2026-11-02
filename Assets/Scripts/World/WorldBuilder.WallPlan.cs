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
        bool planFields;   // Vorschau fuer Felder statt Mauern
        bool planTotem;    // ... bzw. fuer eine Totemflaeche (gleiche drei Klicks)
        // Felder in drei Klicks: Anfang -> Reihe festlegen -> in die andere Richtung aufziehen -> anlegen
        bool fieldRowFixed, fieldRowHoriz;
        Cell fieldRowEnd;
        const int MaxFieldSpan = 15;   // hoechstens so viele Felder je Richtung
        readonly List<SpriteRenderer> planViews = new List<SpriteRenderer>();
        readonly List<Cell> planOk = new List<Cell>();
        Cell planTo;

        static readonly Color PlanGreen = new Color(0.45f, 1f, 0.45f, 0.55f), PlanRed = new Color(1f, 0.4f, 0.35f, 0.45f), PlanGold = new Color(1f, 0.85f, 0.35f, 0.5f);

        public bool WallAnchorActive => wallAnchor.HasValue && !planFields;
        public bool FieldPlanActive => wallAnchor.HasValue && planFields;
        /// <summary>Vorschau folgt Finger/Maus (Mauer im Mauer-Modus oder Felder).</summary>
        public bool PlanFollows => FieldPlanActive || ((wallMode || riverMode) && WallAnchorActive);

        /// <summary>Felder anlegen: 1. Klick setzt die Ecke, eine gerade Reihe folgt der Maus; 2. Klick legt die Reihe fest,
        /// die Vorschau waechst dann in die andere Richtung zu einer Flaeche (z. B. 4×4); 3. Klick legt alle gruenen Felder an.
        /// false = hier geht kein Feld (dann macht der Tipp etwas anderes).</summary>
        public bool FieldClick(Vector2 w)
        {
            if (planTotem) return TotemClick(w);
            var c = TapCell(w);
            if (!wallAnchor.HasValue)
            {
                if (!CanPlaceField(c)) return false;
                wallAnchor = c; planFields = true; fieldRowFixed = false;
                UpdateWallPlan(w);
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Reihe aufziehen – nochmal tippen", new Color(0.75f, 1f, 0.7f));
                Sfx.Play("tick", 1.2f, 0.5f);
                Haptics.Tap(8);
                return true;
            }
            if (!fieldRowFixed)
            {
                // Reihe steht: jetzt in die andere Richtung aufziehen
                UpdateWallPlan(w);
                fieldRowFixed = true;
                Floater?.Invoke(CellCenter(fieldRowEnd) + new Vector2(0, 0.6f), "In die Breite ziehen – nochmal tippen", new Color(0.75f, 1f, 0.7f));
                Sfx.Play("tick", 1.35f, 0.5f);
                Haptics.Tap(8);
                UpdateWallPlan(w);
                return true;
            }
            UpdateWallPlan(w);
            var ok = new List<Cell>(planOk);
            CancelWallPlan();
            if (ok.Count == 0) { Sfx.Play("error", 1f, 0.5f); return true; }
            foreach (var pc in ok) PlaceField(pc, true);
            save.Save();
            return true;
        }

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
                // zweimal dieselbe Mauer: wird zum Tor; zweimal dasselbe Tor: abbauen
                if (!v.st.gate) MakeGate(a, v);
                else { RemoveWall(a, v, false); RefundWallWood(CellCenter(a) + new Vector2(0, 0.5f)); }
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

        /// <summary>Escape: erst das angefangene Ziehen abbrechen, beim zweiten Mal den Bau-Modus verlassen.</summary>
        public void EscapePressed()
        {
            if (wallAnchor.HasValue)
            {
                CancelWallPlan();
                Sfx.Play("pop", 0.8f, 0.4f);
                return;
            }
            if (wallMode) SetWallMode(false);
            else if (riverMode) SetRiverMode(false);
            else if (pedestalMode) SetPedestalMode(false);
        }

        public void CancelWallPlan()
        {
            wallAnchor = null;
            planFields = false;
            planTotem = false;
            fieldRowFixed = false;
            planOk.Clear();
            foreach (var sr in planViews) if (sr != null) sr.enabled = false;
        }

        /// <summary>Gruene Vorschau vom Anfang bis zur Stelle unter Finger/Maus neu zeichnen.</summary>
        public void UpdateWallPlan(Vector2 w)
        {
            if (!wallAnchor.HasValue) return;
            var to = TapCell(w);
            planTo = to;
            List<Cell> path;
            if (planFields) path = FieldPath(to);
            else
            {
                path = new List<Cell> { wallAnchor.Value };
                var cur = wallAnchor.Value;
                int guard = 0;
                while (!cur.Equals(to) && guard++ < 80)
                {
                    int dx = to.x - cur.x, dy = to.y - cur.y;
                    cur = Mathf.Abs(dx) >= Mathf.Abs(dy) ? new Cell(cur.x + System.Math.Sign(dx), cur.y) : new Cell(cur.x, cur.y + System.Math.Sign(dy));
                    path.Add(cur);
                }
            }
            planOk.Clear();
            float budget = WoodLeft;
            double coinBudget = save.coins;
            bool river = riverMode && !planFields;
            bool riverStop = false;   // ein Bach fliesst nur um Gaerten herum: am ersten Hindernis (z. B. Feld) endet er
            int n = 0;
            foreach (var c in path)
            {
                if (!planTotem && (river ? HasRiver(c) : planFields ? HasField(c) : HasWall(c))) continue;   // schon da: nichts zeigen
                bool ok = planTotem ? !TotemTaken(c) : river ? !riverStop && CanPlaceRiver(c) && coinBudget >= PlantBalance.I.River.cost
                        : planFields ? CanPlaceField(c) : CanPlaceWall(c) && budget >= WallWood - 0.0001f;
                if (river && !ok) riverStop = true;
                if (ok && planFields && !planTotem)
                {
                    double fc = FieldCost(planOk.Count);
                    if (fc > coinBudget) ok = false; else coinBudget -= fc;   // Felder kosten: rot, wenn das Geld nicht reicht
                }
                if (ok) { if (river) coinBudget -= PlantBalance.I.River.cost; else if (!planFields) budget -= WallWood; planOk.Add(c); }
                var sr = PlanView(n++);
                sr.enabled = true;
                if (planFields || river)
                {
                    // flaches, deutlich gruenes (beim Bach blaues) Feld auf dem Boden
                    sr.sprite = Art.Get("white");
                    sr.transform.localScale = Vector3.one * CellSize * 0.9f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
                    sr.transform.position = CellCenter(c);
                    sr.sortingOrder = -32288;
                }
                else
                {
                    sr.sprite = WallSprite;
                    sr.transform.localScale = Vector3.one * CellSize / Mathf.Max(0.01f, WallSprite.bounds.size.x);
                    sr.transform.position = WallFoot(c);
                    sr.sortingOrder = Order(WallFoot(c).y + 0.1f) + 3;
                }
                sr.color = ok ? (planTotem ? PlanGold : river ? PlanBlue : PlanGreen) : PlanRed;
            }
            for (int i = n; i < planViews.Count; i++) if (planViews[i] != null) planViews[i].enabled = false;
        }

        /// <summary>Felder-Vorschau: erst eine gerade Reihe (waagerecht oder senkrecht, je nachdem wohin die Maus weiter weg ist),
        /// nach dem 2. Klick ein Rechteck von der Reihe bis zur Maus in der anderen Richtung.</summary>
        List<Cell> FieldPath(Cell to)
        {
            var a = wallAnchor.Value;
            int Clamp(int from, int v) => from + Mathf.Clamp(v - from, -(MaxFieldSpan - 1), MaxFieldSpan - 1);
            var list = new List<Cell>();
            if (!fieldRowFixed)
            {
                fieldRowHoriz = Mathf.Abs(to.x - a.x) >= Mathf.Abs(to.y - a.y);
                fieldRowEnd = fieldRowHoriz ? new Cell(Clamp(a.x, to.x), a.y) : new Cell(a.x, Clamp(a.y, to.y));
                int n = Mathf.Max(Mathf.Abs(fieldRowEnd.x - a.x), Mathf.Abs(fieldRowEnd.y - a.y));
                int sx = System.Math.Sign(fieldRowEnd.x - a.x), sy = System.Math.Sign(fieldRowEnd.y - a.y);
                for (int i = 0; i <= n; i++) list.Add(new Cell(a.x + sx * i, a.y + sy * i));
                return list;
            }
            // Rechteck: Reihe von a bis fieldRowEnd, aufgezogen bis zur Maus in der anderen Richtung
            int x0 = a.x, x1 = fieldRowEnd.x, y0 = a.y, y1 = fieldRowEnd.y;
            if (fieldRowHoriz) y1 = Clamp(a.y, to.y); else x1 = Clamp(a.x, to.x);
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
                for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++) list.Add(new Cell(x, y));
            return list;
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
                var c = sr.color; c.a = (c.g > 0.7f ? PlanGreen.a : PlanRed.a) * k; sr.color = c;
            }
        }
    }
}
