using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Besondere Pflanzenformen. "single" (Weizen): statt drei Aehren eine einzige, breitere Aehre als Koerper
    /// (mittleres Drittel des Happy-Harvest-Bildes, in die Breite gezogen); die aeusseren Aehren dienen als Zweige,
    /// die aus ihr herauswachsen (siehe PlantCreature).
    /// </summary>
    public static class PlantForms
    {
        static readonly Dictionary<(Sprite, int), Sprite> cache = new Dictionary<(Sprite, int), Sprite>();

        public static bool IsSingle(SpeciesDef sp) => sp != null && sp.form == "single";

        /// <summary>Breiten-Faktor des Koerpers (nur bei "single").</summary>
        public static float Widen(SpeciesDef sp) => IsSingle(sp) ? Mathf.Max(1f, sp.widen) : 1f;

        /// <summary>Drittel eines Bildes: 0 links, 1 Mitte, 2 rechts (Fusspunkt bleibt unten).</summary>
        public static Sprite Third(Sprite src, int part)
        {
            if (src == null) return null;
            if (cache.TryGetValue((src, part), out var s) && s != null) return s;
            var r = src.rect;
            float w = r.width / 3f;
            var rect = new Rect(r.x + part * w, r.y, w, r.height);
            s = Sprite.Create(src.texture, rect, new Vector2(0.5f, src.pivot.y / r.height), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = src.name + "_" + part;
            cache[(src, part)] = s;
            return s;
        }

        /// <summary>Koerper-Sprite fuer eine Wachstumsstufe (Index in species.stages).</summary>
        public static Sprite Body(WorldArtSet art, SpeciesDef sp, int stageIndex)
        {
            if (art == null || sp == null) return null;
            var src = art.Get(sp.art + sp.stages[Mathf.Clamp(stageIndex, 0, sp.stages.Length - 1)]);
            return IsSingle(sp) ? Third(src, 1) : src;
        }

        /// <summary>Referenzbreite fuer die Skalierung: bei "single" die Breite des ganzen Originalbildes,
        /// so bleibt die Aehre genauso hoch wie vorher und wird nur ueber Widen breiter.</summary>
        public static float RefWidth(SpeciesDef sp, Sprite body) => body == null ? 1f : body.bounds.size.x * (IsSingle(sp) ? 3f : 1f);
    }
}
