using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Pflanzen zusammenfuehren: Eine reife Pflanze wird aufgenommen (antippen oder mit dem Finger ziehen) und
    /// auf eine gleiche Pflanze gezogen. Die Stufen addieren sich, die Pflanze wird groesser und staerker.
    /// Ab bestimmten Stufen braucht sie mehr Felder (plants.json -> mergeSizes); beim Ziehen werden die
    /// benoetigten Felder gruen (frei) bzw. rot (belegt / kein Feld) eingefaerbt.
    /// </summary>
    public partial class WorldBuilder
    {
        bool dragging;
        Vector2 dragPos;
        PlantedState hoverTarget;
        readonly List<SpriteRenderer> footprintPool = new List<SpriteRenderer>();
        List<Cell> fpCells;
        bool fpOk;
        float fpFlashT;

        public bool Dragging => dragging;

        /// <summary>Reif und nicht mehr gesperrt?</summary>
        public bool IsReady(PlantedState p)
        {
            return p != null && p.Grown && plantViews.TryGetValue(new Cell(p.x, p.y), out var v) && Time.unscaledTime >= v.readyAt;
        }

        bool IsMergeTarget(PlantedState p) => carrying != null && p != null && p != carrying && p.Grown && p.type == carrying.type;

        bool AnyMergeTargetFor(PlantedState p)
        {
            foreach (var q in save.planted) if (q != p && q.Grown && q.type == p.type) return true;
            return false;
        }

        // ------------------------------------------------------------------ Ziehen mit dem Finger

        /// <summary>Finger zieht eine reife Pflanze: aufnehmen, falls noch nicht geschehen.</summary>
        public bool BeginDrag(PlantedState p, Vector2 w)
        {
            if (carrying != p) { StartPlacing(p); if (carrying != p) return false; }
            dragging = true;
            dragPos = w;
            return true;
        }

        public void DragTo(Vector2 w)
        {
            dragPos = w;
            var t = PlantAt(CellAt(w));
            if (IsMergeTarget(t) && hoverTarget != t) { Sfx.Play("tick", 1.3f, 0.5f); Haptics.Tap(8); }
            hoverTarget = IsMergeTarget(t) ? t : null;
        }

        /// <summary>Finger loslassen: gleiche Pflanze unter dem Finger -> ablegen, sonst zurueck.</summary>
        public void Drop(Vector2 w)
        {
            dragging = false;
            hoverTarget = null;
            TryPlaceAt(w);
        }

        // ------------------------------------------------------------------ Flaeche finden

        /// <summary>Sucht eine Lage fuer die Flaeche der neuen Stufe, die das Ziel enthaelt und nur freie Felder nutzt.
        /// cells enthaelt immer die (beste) Vorschau, auch wenn es nicht passt.</summary>
        bool FindFootprint(PlantedState target, int lv, out Cell anchor, out List<Cell> cells)
        {
            var size = PlantBalance.I.Footprint(lv);
            int sw = Mathf.Max(size.x, target.W), sh = Mathf.Max(size.y, target.H);
            var options = new List<Cell>();
            for (int ax = target.x - (sw - target.W); ax <= target.x; ax++)
                for (int ay = target.y - (sh - target.H); ay <= target.y; ay++)
                    options.Add(new Cell(ax, ay));
            // bevorzugt nach rechts und nach oben wachsen
            options.Sort((a, b) => (Mathf.Abs(target.x - a.x) * 2 + Mathf.Abs(target.y - a.y)).CompareTo(Mathf.Abs(target.x - b.x) * 2 + Mathf.Abs(target.y - b.y)));
            anchor = options[0];
            cells = Rect(anchor, sw, sh);
            foreach (var o in options)
            {
                var cs = Rect(o, sw, sh);
                bool ok = true;
                foreach (var c in cs)
                {
                    if (!HasField(c)) { ok = false; break; }
                    var occ = PlantAt(c);
                    if (occ != null && occ != target && occ != carrying) { ok = false; break; }
                }
                if (ok) { anchor = o; cells = cs; return true; }
            }
            return false;
        }

        static List<Cell> Rect(Cell a, int w, int h)
        {
            var l = new List<Cell>();
            for (int dx = 0; dx < w; dx++) for (int dy = 0; dy < h; dy++) l.Add(new Cell(a.x + dx, a.y + dy));
            return l;
        }

        // ------------------------------------------------------------------ Zusammenfuehren

        bool MergeInto(PlantedState target)
        {
            var p = carrying;
            int lv = target.Lv + p.Lv;
            int oldLv = target.Lv;
            if (!FindFootprint(target, lv, out var anchor, out var cells))
            {
                var size = PlantBalance.I.Footprint(lv);
                fpCells = cells; fpOk = false; fpFlashT = 1.4f;
                Floater?.Invoke(PlantBase(target) + new Vector2(0, 1.4f), "Stufe " + lv + " braucht " + size.x + "×" + size.y + " freie Felder", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                Haptics.Tap(30);
                CancelPlacing();
                return false;
            }
            fpCells = cells; fpOk = true; fpFlashT = 0.9f;
            var oldAnchor = new Cell(target.x, target.y);
            var size2 = PlantBalance.I.Footprint(lv);
            // Zustand sofort umstellen (Spielstand), die Darstellung waechst bei der Landung
            target.level = lv;
            target.x = anchor.x; target.y = anchor.y;
            target.w = Mathf.Max(size2.x, target.W); target.h = Mathf.Max(size2.y, target.H);
            var f = LaunchCarried(PlantBase(target) + new Vector2(0, CellSize * 1.2f * target.H), 1f);
            f.mergeTarget = target; f.oldAnchor = oldAnchor; f.newLevel = lv; f.oldLevel = oldLv;
            save.Save();
            return true;
        }

        /// <summary>Getragene Pflanze vom Feld nehmen und als Flieger zum Ziel schicken.</summary>
        Flyer LaunchCarried(Vector2 to, float endScale)
        {
            var p = carrying;
            carrying = null;
            dragging = false;
            PlacingChanged?.Invoke(null);
            var c = new Cell(p.x, p.y);
            var g = p.Genome;
            Vector2 from = PlantBase(p);
            Sprite sprite = null; float sx = 0.5f;
            if (plantViews.TryGetValue(c, out var pv) && pv.sr != null) { sprite = pv.sr.sprite; from = pv.sr.bounds.center; sx = pv.sr.transform.lossyScale.x; }
            foreach (var fc in PlantCells(p)) PunchField(fc, 0.6f);
            RemovePlantView(c);
            save.planted.Remove(p);
            var fsr = new GameObject("FlyingPlant").AddComponent<SpriteRenderer>();
            fsr.transform.SetParent(transform, false);
            fsr.sprite = sprite;
            if (PlantMaterialShared != null) fsr.sharedMaterial = PlantMaterialShared;
            fsr.color = Color.Lerp(Color.white, g.ElementColor, 0.15f);
            fsr.sortingOrder = 31200;
            var sh = new GameObject("FlyShadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(transform, false);
            sh.sprite = MistSprite; sh.color = new Color(0, 0, 0, 0.3f); sh.sortingOrder = -31300;
            float dist = (to - from).magnitude;
            var f = new Flyer { sr = fsr, shadow = sh, from = from, to = to, dur = Mathf.Clamp(0.3f + dist * 0.06f, 0.35f, 0.8f), s0 = sx, s1 = sx * endScale, g = g };
            flyers.Add(f);
            Particles.I?.Burst(from, "square", new Color(0.45f, 0.3f, 0.18f), 10, 2.6f, 0.5f, 0.1f, 9f, 2f);
            Sfx.Play("dig", 1f, 0.9f);
            Sfx.Play("launch", 1.5f, 0.6f);
            Haptics.Tap(20);
            return f;
        }

        /// <summary>Landung auf der Zielpflanze: sie waechst auf ihre neue Groesse – mit Wucht.</summary>
        void LandMerge(Flyer f)
        {
            var t = f.mergeTarget;
            RemovePlantView(f.oldAnchor);
            if (!save.planted.Contains(t)) return;
            CreatePlantView(t, true);
            if (plantViews.TryGetValue(new Cell(t.x, t.y), out var v)) { v.sy = 0.45f; v.flash = 1f; v.pop = 0.2f; }
            var at = PlantBase(t);
            foreach (var c in PlantCells(t)) PunchField(c, 1.3f);
            Shockwave(at, CellSize * 3.5f * t.W, 0.5f, Color.white);
            Shockwave(at, CellSize * 2.2f * t.W, 0.35f, f.g.ElementColor);
            Burst(at + new Vector2(0, CellSize), CellSize * 3f * t.W, 0.22f, Color.white);
            Particles.I?.Burst(at + new Vector2(0, 0.5f), "star", f.g.ElementColor, 22, 3.5f, 0.8f, 0.35f, 1f, 1.5f);
            Particles.I?.Burst(at, "square", new Color(0.45f, 0.3f, 0.18f), 14, 3f, 0.5f, 0.11f, 10f, 3f);
            Sfx.Play("thump", 0.7f, 1f);
            Sfx.Play("levelup", 1.15f, 0.8f);
            Sfx.Play("note", Mathf.Pow(2f, Mathf.Min(f.newLevel, 12) / 12f), 0.5f);
            Haptics.Tap(60);
            WorldCamera.I?.Shake(0.1f);
            WorldCamera.I?.Punch(0.05f);
            WorldPostFX.Punch(0.9f);
            HitStop(0.06f);
            if (PlantBalance.I.Tier(f.newLevel) > PlantBalance.I.Tier(f.oldLevel)) Mutated(at, f.g, PlantBalance.I.Tier(f.newLevel), t.W);
            else Floater?.Invoke(at + new Vector2(0, 1.4f), f.g.name + " Stufe " + f.newLevel + "!", new Color(1f, 0.9f, 0.4f));
        }

        static readonly string[] MutationNames = { "", "", "Wilde Form", "Bestie", "Albtraum" };

        /// <summary>Neue Mutationsstufe erreicht: dunkler Blitz, Lichtsaeule, Donner, grosser Schriftzug.</summary>
        void Mutated(Vector2 at, PlantGenome g, int tier, float width)
        {
            var dark = Color.Lerp(g.ElementColor, new Color(0.35f, 0.05f, 0.2f), 0.5f);
            StartCoroutine(RipeSlowMo());
            Pillar(at, CellSize * 2.2f * width, dark);
            Shockwave(at, CellSize * 7f * width, 0.8f, dark);
            Shockwave(at, CellSize * 4.5f * width, 0.55f, new Color(1f, 0.3f, 0.2f, 0.9f));
            Burst(at + new Vector2(0, CellSize * 1.5f), CellSize * 5f * width, 0.35f, new Color(1f, 0.4f, 0.3f, 0.9f));
            Particles.I?.Burst(at + new Vector2(0, 0.6f), "spark", new Color(1f, 0.35f, 0.25f), 30, 4.5f, 0.8f, 0.35f, 0.5f, 2f);
            Particles.I?.Burst(at, "square", new Color(0.2f, 0.08f, 0.12f), 18, 3.5f, 0.9f, 0.14f, 4f, 3f);
            Sfx.Play("boom", 0.8f, 0.9f);
            Sfx.Play("groan", 0.6f, 0.8f);
            Sfx.Play("levelup", 0.75f, 0.9f);
            Haptics.Tap(120);
            WorldCamera.I?.Shake(0.22f);
            WorldCamera.I?.Punch(0.09f);
            WorldPostFX.Punch(1.4f);
            var spn = PlantBalance.I.Species(g.species);
            string mname = spn != null && spn.mutationNames != null && spn.mutationNames.Length >= tier ? spn.mutationNames[tier - 1] : MutationNames[Mathf.Clamp(tier, 0, 4)];
            Floater?.Invoke(at + new Vector2(0, 1.8f), "MUTATION " + tier + "/4 – " + mname + "!", new Color(1f, 0.4f, 0.3f));
        }

        // ------------------------------------------------------------------ Anzeige

        void UpdateMerge(float udt)
        {
            // Vorschau: beim Ziehen ueber eine gleiche Pflanze die benoetigten Felder gruen/rot
            List<Cell> show = null; bool ok = true;
            if (dragging && hoverTarget != null && carrying != null)
            {
                ok = FindFootprint(hoverTarget, hoverTarget.Lv + carrying.Lv, out _, out show);
            }
            else if (fpFlashT > 0f) { fpFlashT -= udt; show = fpCells; ok = fpOk; }

            int n = show != null ? show.Count : 0;
            while (footprintPool.Count < n)
            {
                var sr = new GameObject("Footprint").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Art.Get("white");
                sr.sortingOrder = -32290;
                footprintPool.Add(sr);
            }
            float pulse = 0.38f + 0.14f * Mathf.Sin(Time.unscaledTime * 10f);
            for (int i = 0; i < footprintPool.Count; i++)
            {
                var sr = footprintPool[i];
                sr.enabled = i < n;
                if (i >= n) continue;
                var c = show[i];
                sr.transform.position = CellCenter(c);
                float k = sr.sprite != null ? CellSize * 0.94f / sr.sprite.bounds.size.x : CellSize;
                sr.transform.localScale = new Vector3(k, k, 1);
                bool cellOk = HasField(c) && (PlantAt(c) == null || PlantAt(c) == hoverTarget || PlantAt(c) == carrying || ok);
                sr.color = ok ? new Color(0.35f, 1f, 0.4f, pulse) : cellOk ? new Color(0.35f, 1f, 0.4f, pulse * 0.6f) : new Color(1f, 0.3f, 0.25f, pulse + 0.1f);
            }
        }

        /// <summary>Pfeil ueber gleichen Pflanzen, solange man eine traegt.</summary>
        void UpdateMergeMarker(PlantedState p, PlantView v)
        {
            bool show = IsMergeTarget(p) && hoverTarget != p;
            if (v.marker == null)
            {
                if (!show) return;
                v.marker = WorldSprite(v.root, "Marker", ArrowSprite, 31100);
                v.marker.transform.localScale = Vector3.one * 0.3f;
            }
            v.marker.enabled = show;
            if (show) v.marker.transform.localPosition = new Vector3(0, v.sr.bounds.size.y + 0.15f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 0.15f, 0);
        }
    }
}
