using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Helfer-Pflanzen, die Dinge bewegen: Wurzeltraeger (traegt Holz aus dem Wald zum Haus), Pustesamen (weht Samen auf leere Felder) und
    /// Saftrohr (leitet Wasser: Tauschilf in der Naehe giesst weiter). Werte in plants.json -> helpers.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly Dictionary<PlantedState, float> helperTimers = new Dictionary<PlantedState, float>();
        readonly HashSet<Cell> sowing = new HashSet<Cell>();   // Samen ist unterwegs – nicht doppelt anfliegen

        /// <summary>Zusaetzliche Giess-Reichweite eines Tauschilfs durch Saftrohre in der Naehe.</summary>
        int PipeBonus(PlantedState w)
        {
            var h = PlantBalance.I.Helpers;
            int best = 0;
            foreach (var q in save.planted)
                if (q.Grown && q.Role == "pipe" && Cheb(w, q) <= h.pipeRadius) best = Mathf.Max(best, h.pipeBonus + (q.Lv - 1) / 3);
            return best;
        }

        int WaterRadiusOf(PlantedState w) => WaterRadius + PipeBonus(w);

        /// <summary>Kurzer Text fuer das Info-Fenster.</summary>
        public string HelperSummary(PlantedState p)
        {
            var h = PlantBalance.I.Helpers;
            switch (p.Role)
            {
                case "carrier": return "+" + h.carrierWood + " Holz alle " + HelperInterval(p, h.carrierInterval).ToString("0.#") + " s";
                case "sower": { var d = PlantBalance.I.Plant(SowType()); return d != null ? "Sät " + d.name + " (" + Num.Short(SeedCost(d)) + " Münzen)" : "Wähle zuerst eine Pflanze"; }
                case "pipe": { int n = 0; foreach (var q in save.planted) if (q.Grown && q.Role == "water" && Cheb(p, q) <= h.pipeRadius) n++; return n > 0 ? n + " Tauschilf gießt +" + (h.pipeBonus + (p.Lv - 1) / 3) + " Feld weiter" : "Kein Tauschilf in Reichweite"; }
            }
            return "";
        }

        float HelperInterval(PlantedState p, float baseT) => baseT / Mathf.Max(1f, 0.6f + 0.4f * p.Lv);

        string SowType()
        {
            var d = PlantBalance.I.Plant(save.selectedPlant);
            if (d != null && IsUnlocked(d.id) && d.role != "sower") return d.id;
            return IsUnlocked("radieschen") ? "radieschen" : null;
        }

        void UpdateHelpers(float dt)
        {
            var h = PlantBalance.I.Helpers;
            for (int i = 0; i < save.planted.Count; i++)
            {
                var p = save.planted[i];
                if (!p.Grown) continue;
                float baseT = p.Role == "carrier" ? h.carrierInterval : p.Role == "sower" ? h.sowerInterval : 0f;
                if (baseT <= 0f) continue;
                if (!helperTimers.TryGetValue(p, out float t)) t = Random.Range(0.5f, baseT);
                t -= dt;
                if (t <= 0f)
                {
                    t = HelperInterval(p, baseT);
                    if (p.Role == "carrier") Carry(p, h);
                    else Sow(p, h);
                }
                helperTimers[p] = t;
            }
        }

        Vector2 HelperTop(PlantedState p) => PlantBase(p) + new Vector2(0, CellSize * 0.8f);

        void Nudge(PlantedState p) { if (plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) { v.sy = Mathf.Min(v.sy, -0.06f); v.flash = Mathf.Max(v.flash, 0.25f); } }

        /// <summary>Wurzeltraeger: Holzscheit fliegt vom naechsten Baum ueber die Pflanze zum Haus.</summary>
        void Carry(PlantedState p, HelperDef h)
        {
            var me = HelperTop(p);
            Vector2 src = me + new Vector2(Random.Range(-4f, 4f), Random.Range(2f, 4f));
            float bd = float.MaxValue;
            foreach (var t in trees) { float d = (t.foot - me).sqrMagnitude; if (d < bd && d > 1f) { bd = d; src = t.foot + new Vector2(0, 0.4f); } }
            var log = art.Get("hh_log");
            int n = h.carrierWood;
            Nudge(p);
            StartCoroutine(Leaf(src, me, () =>
            {
                Nudge(p);
                StartCoroutine(Leaf(me, HousePos + new Vector2(0, -0.4f), () => AddWood(n, HousePos + new Vector2(0, 0.2f)), log, null, 0.3f));
            }, log, new Color(0.6f, 0.42f, 0.25f), 0.3f));
        }

        /// <summary>Pustesamen: Samen fliegt auf ein leeres Feld in der Naehe und wird dort gesaet (kostet den Samenpreis).</summary>
        void Sow(PlantedState p, HelperDef h)
        {
            var type = SowType();
            var def = PlantBalance.I.Plant(type);
            if (def == null || save.coins < SeedCost(def)) return;
            var free = new List<Cell>();
            for (int dx = -h.sowerRadius; dx <= h.sowerRadius; dx++)
                for (int dy = -h.sowerRadius; dy <= h.sowerRadius; dy++)
                {
                    var c = new Cell(p.x + dx, p.y + dy);
                    if (HasField(c) && PlantAt(c) == null && !HasStructure(c) && !sowing.Contains(c)) free.Add(c);
                }
            if (free.Count == 0) return;
            var target = free[Random.Range(0, free.Count)];
            sowing.Add(target);
            Nudge(p);
            Particles.I?.Burst(HelperTop(p), "square", new Color(1f, 1f, 0.95f, 0.8f), 5, 1.2f, 0.8f, 0.05f, -0.4f, 0.5f);   // Pusteflocken
            StartCoroutine(Leaf(HelperTop(p), CellCenter(target), () =>
            {
                sowing.Remove(target);
                if (PlantAt(target) == null && HasField(target)) PlantInto(target, type);
            }, null, new Color(0.95f, 0.92f, 0.8f), 0.1f));
        }
    }
}
