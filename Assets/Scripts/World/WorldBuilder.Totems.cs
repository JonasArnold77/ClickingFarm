using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Totems: Bauwerke, die man mehrmals kaufen kann. Ein Totem gehoert immer zu genau einem Feld (Beet = alle
    /// zusammenhaengenden Feldzellen) – man stellt es auf oder direkt neben das Beet. Es verstaerkt eine Sache fuer alle
    /// Pflanzen dieses Beets: Wachstum (Wachstumstotem), Ernte pro Tipp (Erntetotem) oder die Ernte, die von selbst
    /// entsteht (Fleisstotem). Damit man nicht ein Riesenfeld boostet: jedes Beet traegt nur ein Totem, und die volle
    /// Wirkung reicht nur fuer fullCells Felder – ein groesseres Beet bekommt anteilig weniger. Werte in plants.json
    /// (structures mit effect "totem", multi, boost, fullCells, costGrowth).
    /// </summary>
    public partial class WorldBuilder
    {
        readonly Dictionary<Cell, StructureState> totemMap = new Dictionary<Cell, StructureState>();
        readonly Dictionary<StructureState, HashSet<Cell>> totemBeds = new Dictionary<StructureState, HashSet<Cell>>();
        int totemFrame = -1;

        public static bool IsTotem(StructureDef d) => d != null && d.effect == "totem";
        public static bool IsTotem(StructureState s) => s != null && IsTotem(PlantBalance.I.Structure(s.id));

        public int StructCount(string id) { int n = 0; foreach (var s in save.structures) if (s.id == id) n++; return n; }

        /// <summary>Preis: mehrfach kaufbare Bauwerke werden mit jedem Stueck teurer.</summary>
        public float StructCost(StructureDef d) => d == null ? 0f : d.multi ? d.cost * Mathf.Pow(Mathf.Max(1f, d.costGrowth), StructCount(d.id)) : d.cost;

        /// <summary>Welche Feldzelle zu welchem Totem gehoert (einmal pro Bild neu, das erste Totem eines Beets gewinnt).</summary>
        void RefreshTotems()
        {
            if (totemFrame == Time.frameCount) return;
            totemFrame = Time.frameCount;
            totemMap.Clear(); totemBeds.Clear();
            foreach (var s in save.structures)
            {
                if (!s.placed || !(s.anchored || s.area) || !IsTotem(s)) continue;
                var bed = s.area ? AreaOf(s) : Bed(new Cell(s.bx, s.by));
                totemBeds[s] = bed;
                bool free = true;
                foreach (var c in bed) if (totemMap.ContainsKey(c)) { free = false; break; }
                if (!free) continue;   // Beet hat schon ein Totem (z. B. zwei Beete wurden verbunden)
                foreach (var c in bed) totemMap[c] = s;
            }
        }
        static HashSet<Cell> AreaOf(StructureState s)
        {
            var set = new HashSet<Cell>();
            for (int y = Mathf.Min(s.ay0, s.ay1); y <= Mathf.Max(s.ay0, s.ay1); y++)
                for (int x = Mathf.Min(s.ax0, s.ax1); x <= Mathf.Max(s.ax0, s.ax1); x++) set.Add(new Cell(x, y));
            return set;
        }

        /// <summary>Liegt diese Zelle schon in der Flaeche eines anderen Totems?</summary>
        bool TotemTaken(Cell c) { RefreshTotems(); return totemMap.TryGetValue(c, out var o) && o != pendingStruct; }

        /// <summary>Totemflaeche aufziehen wie Felder: 1. Tipp Ecke, 2. Tipp Reihe fest, 3. Tipp Flaeche anlegen.</summary>
        public bool TotemClick(Vector2 w)
        {
            if (pendingStruct == null || !IsTotem(pendingStruct)) return false;
            var c = TapCell(w);
            if (!wallAnchor.HasValue)
            {
                if (TotemTaken(c)) { Sfx.Play("error", 1f, 0.5f); Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Hier wirkt schon ein Totem", new Color(1f, 0.6f, 0.5f)); return true; }
                wallAnchor = c; planFields = true; planTotem = true; fieldRowFixed = false;
                UpdateWallPlan(w);
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Reihe aufziehen – nochmal tippen", new Color(1f, 0.92f, 0.6f));
                Sfx.Play("tick", 1.2f, 0.5f); Haptics.Tap(8);
                return true;
            }
            if (!fieldRowFixed)
            {
                UpdateWallPlan(w);
                fieldRowFixed = true;
                Floater?.Invoke(CellCenter(fieldRowEnd) + new Vector2(0, 0.6f), "In die Breite ziehen – nochmal tippen", new Color(1f, 0.92f, 0.6f));
                Sfx.Play("tick", 1.35f, 0.5f); Haptics.Tap(8);
                UpdateWallPlan(w);
                return true;
            }
            UpdateWallPlan(w);
            var rect = FieldPath(planTo);
            foreach (var rc in rect)
                if (TotemTaken(rc))
                {
                    Sfx.Play("error", 1f, 0.5f);
                    Floater?.Invoke(CellCenter(rc) + new Vector2(0, 0.6f), "Überschneidet ein anderes Totem", new Color(1f, 0.6f, 0.5f));
                    return true;
                }
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var rc in rect) { x0 = Mathf.Min(x0, rc.x); y0 = Mathf.Min(y0, rc.y); x1 = Mathf.Max(x1, rc.x); y1 = Mathf.Max(y1, rc.y); }
            CancelWallPlan();
            var st = pendingStruct;
            pendingStruct = null;
            st.area = true; st.anchored = false;
            st.ax0 = x0; st.ay0 = y0; st.ax1 = x1; st.ay1 = y1;
            st.x = (x0 + x1) / 2; st.y = (y0 + y1) / 2; st.placed = true;
            CreateStructView(st, true);
            TotemPlaced(st);
            StructurePlacing?.Invoke(null);
            RefreshGhosts();
            save.Save();
            return true;
        }

        /// <summary>Nach Aenderungen an Feldern oder Totems sofort neu zuordnen.</summary>
        void DirtyTotems() => totemFrame = -1;

        public HashSet<Cell> TotemBed(StructureState s) { RefreshTotems(); return totemBeds.TryGetValue(s, out var b) ? b : null; }
        bool TotemActive(StructureState s) { var b = TotemBed(s); if (b == null || b.Count == 0) return false; foreach (var c in b) return totemMap.TryGetValue(c, out var o) && o == s; return false; }

        /// <summary>Anteil der Wirkung: 1 bis fullCells Felder, darueber anteilig weniger.</summary>
        public float TotemShare(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            var b = TotemBed(s);
            if (d == null || b == null || b.Count == 0) return 0f;
            return Mathf.Min(1f, Mathf.Max(1, d.fullCells) / (float)b.Count);
        }

        /// <summary>Faktor fuer diese Pflanze: 1 + Wert × Anteil, wenn ihr Beet ein Totem mit dieser Wirkung hat.</summary>
        float TotemMult(PlantedState p, string boost)
        {
            RefreshTotems();
            if (totemMap.Count == 0 || !totemMap.TryGetValue(new Cell(p.x, p.y), out var s)) return 1f;
            var d = PlantBalance.I.Structure(s.id);
            if (d == null || d.boost != boost) return 1f;
            return 1f + StructValue(s) * TotemShare(s);
        }

        /// <summary>Ein Totem hat keinen Pfahl mehr: man tippt ein Feld an (auch mit Pflanze), das Beet braucht noch kein Totem.</summary>
        public bool CanPlaceTotem(Cell c, out Cell anchor)
        {
            anchor = c;
            if (!HasField(c)) return false;
            RefreshTotems();
            return !totemMap.TryGetValue(c, out var o) || o == pendingStruct;
        }

        /// <summary>Mitte des Beets (dort erscheinen Zahlen und Effekte des Totems).</summary>
        Vector2 TotemCenter(StructureState s)
        {
            var bed = TotemBed(s);
            if (bed == null || bed.Count == 0) return CellCenter(new Cell(s.bx, s.by));
            var sum = Vector2.zero;
            foreach (var c in bed) sum += CellCenter(c);
            return sum / bed.Count;
        }

        /// <summary>Tipp auf die Umrandung: knapp innerhalb oder etwas ausserhalb einer Aussenkante des Beets.</summary>
        bool HitTotemEdge(StructureState s, Vector2 w)
        {
            var bed = TotemBed(s);
            if (bed == null || bed.Count == 0) return false;
            var c = CellAt(w);
            bool inside = bed.Contains(c);
            var local = w - CellCenter(c);
            for (int d = 0; d < 4; d++)
            {
                var nb = Step(c, d, 1);
                if (bed.Contains(nb) == inside) continue;   // nur Kanten zwischen Beet und aussen
                var n = (CellCenter(nb) - CellCenter(c)).normalized;
                float dist = CellSize * 0.5f - Vector2.Dot(local, n);
                if (dist <= (inside ? 0.06f : 0.16f)) return true;
            }
            return false;
        }

        /// <summary>Totem gesetzt: Beet aufleuchten lassen und sagen, wie gut es dort wirkt.</summary>
        void TotemPlaced(StructureState st)
        {
            DirtyTotems();
            var bed = TotemBed(st);
            if (bed == null) return;
            bedFlash = bed; bedFlashT = 1.6f;
            var d = PlantBalance.I.Structure(st.id);
            float share = TotemShare(st);
            string msg = share >= 1f ? "Totem wirkt auf " + bed.Count + " Felder"
                                     : "Fläche zu groß (" + bed.Count + " Felder) – wirkt nur zu " + Mathf.RoundToInt(share * 100) + " %";
            Floater?.Invoke(StructPos(st) + new Vector2(0, 1.2f), msg, share >= 1f ? new Color(0.8f, 1f, 0.7f) : new Color(1f, 0.8f, 0.5f));
        }

        // ------------------------------------------------------------------ Leuchtende Umrandung des Beets

        readonly List<SpriteRenderer> outlinePool = new List<SpriteRenderer>();
        int outlineUsed;

        /// <summary>Farbe nach Staerke (Bonus, z. B. 1 = +100 %): Gruen → Tuerkis → Blau → Violett → Gold.</summary>
        static readonly (float at, Color col)[] OutlineTiers =
        {
            (0f, new Color(0.55f, 1f, 0.45f)), (0.75f, new Color(0.3f, 1f, 0.85f)), (1.25f, new Color(0.35f, 0.6f, 1f)),
            (2f, new Color(0.8f, 0.4f, 1f)), (3f, new Color(1f, 0.82f, 0.25f)),
        };

        public static Color OutlineColor(float strength)
        {
            for (int i = 1; i < OutlineTiers.Length; i++)
                if (strength < OutlineTiers[i].at)
                    return Color.Lerp(OutlineTiers[i - 1].col, OutlineTiers[i].col, Mathf.InverseLerp(OutlineTiers[i - 1].at, OutlineTiers[i].at, strength));
            return OutlineTiers[OutlineTiers.Length - 1].col;
        }

        /// <summary>Wirkung, die tatsaechlich ankommt (Wert × Anteil) – bestimmt Farbe, Tempo und Dicke der Umrandung.</summary>
        public float TotemStrength(StructureState s) => TotemActive(s) ? StructValue(s) * TotemShare(s) : 0f;

        SpriteRenderer OutlineSr()
        {
            if (outlineUsed >= outlinePool.Count)
            {
                var sr = new GameObject("TotemOutline").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = Art.Get("white");
                sr.sharedMaterial = UnlitMat;   // leuchtet auch nachts
                outlinePool.Add(sr);
            }
            var r = outlinePool[outlineUsed++];
            r.enabled = true;
            return r;
        }

        void Strip(Vector2 mid, Vector2 dir, float len, float thick, Color col, int order)
        {
            var sr = OutlineSr();
            float w = Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            sr.transform.position = mid;
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(len / w, thick / w, 1f);
            sr.color = col;
            sr.sortingOrder = order;
        }

        /// <summary>Jedes Beet mit Totem bekommt eine leuchtende Umrandung, auf der Lichtstriche ringsum laufen.
        /// Staerkeres Totem: andere Farbe, schneller, dicker, mehr Striche.</summary>
        void UpdateTotemOutlines()
        {
            outlineUsed = 0;
            foreach (var s in save.structures)
            {
                if (!s.placed || !IsTotem(s)) continue;
                float str = TotemStrength(s);
                if (str <= 0f) continue;
                var bed = TotemBed(s);
                var col = OutlineColor(str);
                float k = Mathf.Clamp01(str / 3f);
                float thick = 0.025f + 0.025f * k, speed = 0.6f + 1.4f * k;
                var view = structViews.Find(x => x.st == s);
                float flash = view != null ? view.squash : 0f;
                if (OpenStructure == s) flash = Mathf.Max(flash, 0.25f + 0.1f * Mathf.Sin(Time.time * 6f));   // Fenster offen
                thick *= 1f + flash;
                col = Color.Lerp(col, Color.white, flash * 0.5f);
                int dashes = 1 + Mathf.RoundToInt(2f * k);
                float pulse = 0.55f + 0.2f * Mathf.Sin(Time.time * (2f + 3f * k) + s.x);
                float t = Time.time * speed;
                foreach (var c in bed)
                    for (int d = 0; d < 4; d++)
                    {
                        var nb = Step(c, d, 1);
                        if (bed.Contains(nb)) continue;
                        // Kante nach aussen; gegen den Uhrzeigersinn umlaufen (Beet liegt links), so laufen die Striche nahtlos weiter
                        var n = CellCenter(nb) - CellCenter(c); n.Normalize();
                        var tan = new Vector2(-n.y, n.x);
                        var mid = CellCenter(c) + n * (CellSize * 0.5f - thick * 0.5f);
                        var start = mid - tan * CellSize * 0.5f;
                        Strip(mid, tan, CellSize + thick, thick, new Color(col.r, col.g, col.b, 0.5f * pulse + 0.3f), -32280);
                        for (int i = 0; i < dashes; i++)
                        {
                            float u = Mathf.Repeat(t + i / (float)dashes, 1f);
                            Strip(start + tan * CellSize * u, tan, CellSize * 0.28f, thick * 1.9f, Color.Lerp(col, Color.white, 0.25f), -32279);
                        }
                    }
            }
            for (int i = outlineUsed; i < outlinePool.Count; i++) outlinePool[i].enabled = false;
        }

        static string BoostName(string boost) => boost == "growth" ? "Wachstum" : boost == "harvest" ? "Ernte pro Tipp" : "Ernte von selbst";

        string TotemSummary(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            var bed = TotemBed(s);
            if (bed == null || bed.Count == 0) return "Keine Fläche mehr – versetze das Totem";
            if (!TotemActive(s)) return "Hier wirkt schon ein anderes Totem";
            float share = TotemShare(s);
            string txt = BoostName(d.boost) + " ×" + (1f + StructValue(s) * share).ToString("0.##") + " · " + bed.Count + " Felder";
            if (share < 1f) txt += "\nFläche zu groß: voll wirkt es nur bis " + d.fullCells + " Felder";
            else txt += " (voll bis " + d.fullCells + ")";
            return txt;
        }
    }
}
