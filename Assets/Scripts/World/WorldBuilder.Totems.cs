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
                if (!s.placed || !s.anchored || !IsTotem(s)) continue;
                var bed = Bed(new Cell(s.bx, s.by));
                totemBeds[s] = bed;
                bool free = true;
                foreach (var c in bed) if (totemMap.ContainsKey(c)) { free = false; break; }
                if (!free) continue;   // Beet hat schon ein Totem (z. B. zwei Beete wurden verbunden)
                foreach (var c in bed) totemMap[c] = s;
            }
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

        /// <summary>Wo darf das Totem stehen? Auf einem freien Feld des Beets oder direkt daneben; das Beet braucht noch kein Totem.</summary>
        public bool CanPlaceTotem(Cell c, out Cell anchor)
        {
            anchor = c;
            if (!CanPlaceStructure(c)) return false;
            RefreshTotems();
            var tries = new List<Cell>();
            if (HasField(c)) tries.Add(c);
            else foreach (var d in new[] { new Cell(c.x + 1, c.y), new Cell(c.x - 1, c.y), new Cell(c.x, c.y + 1), new Cell(c.x, c.y - 1) }) if (HasField(d)) tries.Add(d);
            foreach (var t in tries)
            {
                var mine = totemMap.TryGetValue(t, out var o) ? o : null;
                if (mine != null && mine != pendingStruct) continue;
                anchor = t;
                return true;
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
            string msg = share >= 1f ? "Wacht über dieses Beet (" + bed.Count + " Felder)"
                                     : "Beet zu groß (" + bed.Count + " Felder) – wirkt nur zu " + Mathf.RoundToInt(share * 100) + " %";
            Floater?.Invoke(StructPos(st) + new Vector2(0, 1.2f), msg, share >= 1f ? new Color(0.8f, 1f, 0.7f) : new Color(1f, 0.8f, 0.5f));
        }

        static string BoostName(string boost) => boost == "growth" ? "Wachstum" : boost == "harvest" ? "Ernte pro Tipp" : "Ernte von selbst";

        string TotemSummary(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            var bed = TotemBed(s);
            if (bed == null || bed.Count == 0) return "Kein Beet mehr – versetze das Totem an ein Feld";
            if (!TotemActive(s)) return "Dieses Beet hat schon ein Totem – hier wirkt es nicht";
            float share = TotemShare(s);
            string txt = BoostName(d.boost) + " ×" + (1f + StructValue(s) * share).ToString("0.##") + " · " + bed.Count + " Felder";
            if (share < 1f) txt += "\nBeet zu groß: voll wirkt es nur bis " + d.fullCells + " Felder";
            else txt += " (voll bis " + d.fullCells + ")";
            return txt;
        }
    }
}
