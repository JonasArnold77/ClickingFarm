using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    // ------------------------------------------------------------------ Balancing (Resources/Balance/plants.json)

    [Serializable] public class OptionDef { public string id, name, desc, color; public float speed, damage, cooldown, homing; }
    /// <summary>Lage eines "Kopfes" (Gesicht) auf dem reifen Pflanzen-Sprite: x/y relativ (0..1), s = Kopfbreite relativ zur Spritebreite.</summary>
    [Serializable] public class HeadDef { public float x, y, s; }
    [Serializable]
    public class SpeciesDef
    {
        public string id, name, art; public int[] stages; public float scale; public HeadDef[] heads;
        public string form;              // "single": eine breite Aehre mit Zweigen (Weizen)
        public float widen = 1f;         // Breiten-Faktor des Koerpers bei "single"
        public string[] mutationNames;   // Namen der 4 Formen (Grundform, Mutation 1-3)
    }
    [Serializable] public class MonsterDefW { public string id, name, art, tint, weakTo; public float hp, speed, damage, scale, weight; public int firstWave; }
    [Serializable] public class WaveDef
    {
        public bool autoWave; public float firstWaveDelay, dayDuration, eveningDuration, nightDuration, testEveningDuration, spawnInterval, hpGrowthPerWave, houseHp, tapDamage;
        public int baseCount, countPerWave, woodLossOnDefeat;
    }

    /// <summary>Waffe des Spielers (Tipp auf einen Zombie schiesst). kind: bullet, pellet, arrow, grenade.</summary>
    [Serializable]
    public class WeaponDef
    {
        public string id, name, kind, desc; public int unlockWave, pellets = 1, pierce;
        public float damage, cooldown, speed, spread, radius, range, damagePerWave;
    }

    /// <summary>Staerke pro Stufe (Pflanzen zusammenfuehren): Zuwachs je zusaetzlicher Stufe.</summary>
    [Serializable] public class LevelDef { public float damage = 0.6f, range = 0.3f, speed = 0.12f, size = 0.08f; }
    /// <summary>Ab welcher Stufe eine Pflanze wie viele Felder braucht (Breite x Hoehe).</summary>
    [Serializable] public class MergeSizeDef { public int minLevel = 1, w = 1, h = 1; }

    /// <summary>Bauen mit Holz: Zaeune und Blumentoepfe.</summary>
    [Serializable] public class BuildDef { public int fenceWood, fenceHp, potWood; }

    /// <summary>Eine Angriffspflanze im Katalog: feste Eigenschaften, wird nach einer Welle freigeschaltet.</summary>
    [Serializable]
    public class CatalogDef
    {
        public string id, name, species; public bool starter; public int unlockWave;
        public string pattern; public int patternLv; public int rangeLv;
        public string projectile; public int projectileLv;
        public string element; public int elementLv;
        public string effect; public int effectLv;

        [NonSerialized] PlantGenome genome;
        public PlantGenome Genome => genome ??= new PlantGenome
        {
            name = name, species = species, typeId = id,
            pattern = new Gene { attr = PlantAttr.Pattern, id = pattern, level = patternLv },
            range = new Gene { attr = PlantAttr.Range, level = rangeLv },
            projectile = new Gene { attr = PlantAttr.Projectile, id = projectile, level = projectileLv },
            element = new Gene { attr = PlantAttr.Element, id = element, level = elementLv },
            effect = new Gene { attr = PlantAttr.Effect, id = effect, level = effectLv }
        };
    }

    [Serializable]
    public class PlantBalance
    {
        public float baseDamage, baseCooldown, damagePerLevel, rangeBase, rangePerLevel, comboWindow;
        public int growTaps, maxLevel;
        public int[] growMilestones;
        public OptionDef[] patterns, projectiles, elements, effects;
        public SpeciesDef[] species;
        public CatalogDef[] catalog;
        public MonsterDefW[] monsters;
        public WaveDef waves;
        public WeaponDef[] weapons;
        public BuildDef build;
        public LevelDef levels;
        public MergeSizeDef[] mergeSizes;
        public int[] mutationLevels;   // ab welcher Stufe Mutation 2, 3, 4 beginnt (erste Zahl = Mutation 1)

        /// <summary>Mutationsstufe 1..4 fuer eine Pflanzenstufe.</summary>
        public int Tier(int lv)
        {
            var m = mutationLevels != null && mutationLevels.Length > 0 ? mutationLevels : new[] { 1, 3, 6, 10 };
            int t = 1;
            for (int i = 0; i < m.Length; i++) if (lv >= m[i]) t = i + 1;
            return Mathf.Clamp(t, 1, 4);
        }

        /// <summary>Koerperfarbe je Mutation: ab Mutation 2 dunkler und in die Farbe der Wirkung getaucht.</summary>
        public static Color MutationTint(PlantGenome g, int tier)
        {
            var el = g.ElementColor;
            if (PlantForms.IsSingle(I.Species(g.species)))
            {
                // Feuerweizen glueht mit jeder Mutation mehr von innen statt dunkler zu werden
                return tier <= 1 ? Color.Lerp(Color.white, el, 0.1f)
                     : tier == 2 ? new Color(1f, 0.9f, 0.74f) : tier == 3 ? new Color(1.05f, 0.78f, 0.55f) : new Color(1.1f, 0.68f, 0.45f);
            }
            if (tier <= 1) return Color.Lerp(Color.white, el, 0.15f);
            var dark = Color.Lerp(el, new Color(0.28f, 0.12f, 0.24f), 0.55f);
            float k = tier == 2 ? 0.22f : tier == 3 ? 0.38f : 0.52f;
            var c = Color.Lerp(Color.Lerp(Color.white, el, 0.2f), dark, k);
            c.a = 1f;
            return c;
        }

        LevelDef L => levels ?? (levels = new LevelDef());
        public float LevelDamage(int lv) => 1f + L.damage * (Mathf.Max(1, lv) - 1);
        public float LevelRange(int lv) => L.range * (Mathf.Max(1, lv) - 1);
        public float LevelCooldown(int lv) => 1f / (1f + L.speed * (Mathf.Max(1, lv) - 1));
        public float LevelSize(int lv) => 1f + L.size * Mathf.Min(8, Mathf.Max(1, lv) - 1);

        /// <summary>Flaechenbedarf (in Feldern) fuer eine Stufe.</summary>
        public Vector2Int Footprint(int lv)
        {
            var best = new Vector2Int(1, 1); int bestMin = 0;
            if (mergeSizes != null)
                foreach (var m in mergeSizes)
                    if (lv >= m.minLevel && m.minLevel >= bestMin) { bestMin = m.minLevel; best = new Vector2Int(Mathf.Max(1, m.w), Mathf.Max(1, m.h)); }
            return best;
        }

        public WeaponDef Weapon(string id)
        {
            if (weapons == null || weapons.Length == 0) return null;
            foreach (var w in weapons) if (w.id == id) return w;
            return weapons[0];
        }

        static PlantBalance inst;
        public static PlantBalance I
        {
            get
            {
                if (inst == null)
                {
                    var ta = Resources.Load<TextAsset>("Balance/plants");
                    inst = ta != null ? JsonUtility.FromJson<PlantBalance>(ta.text) : new PlantBalance();
                }
                return inst;
            }
        }

        public OptionDef[] Options(PlantAttr a) => a switch
        {
            PlantAttr.Pattern => patterns,
            PlantAttr.Projectile => projectiles,
            PlantAttr.Element => elements,
            PlantAttr.Effect => effects,
            _ => null
        };

        public OptionDef Option(PlantAttr a, string id)
        {
            var arr = Options(a);
            if (arr == null) return null;
            foreach (var o in arr) if (o.id == id) return o;
            return arr.Length > 0 ? arr[0] : null;
        }

        public SpeciesDef Species(string id) { foreach (var s in species) if (s.id == id) return s; return species[0]; }
        public MonsterDefW Monster(string id) { foreach (var m in monsters) if (m.id == id) return m; return monsters[0]; }
        public CatalogDef Plant(string id) { foreach (var c in catalog) if (c.id == id) return c; return null; }
    }

    // ------------------------------------------------------------------ Eigenschaften einer Angriffspflanze

    /// <summary>Die fuenf Eigenschaften einer Angriffspflanze.</summary>
    public enum PlantAttr { Pattern, Range, Projectile, Element, Effect }

    [Serializable]
    public class Gene
    {
        public PlantAttr attr;
        public string id;     // bei Distanz leer
        public int level;
    }

    /// <summary>Kampfwerte einer Pflanzenart (aus dem Katalog).</summary>
    public class PlantGenome
    {
        public string typeId, name, species;
        public Gene pattern, range, projectile, element, effect;

        public Gene Get(PlantAttr a) => a switch
        {
            PlantAttr.Pattern => pattern,
            PlantAttr.Range => range,
            PlantAttr.Projectile => projectile,
            PlantAttr.Element => element,
            _ => effect
        };

        public static readonly PlantAttr[] All = { PlantAttr.Pattern, PlantAttr.Range, PlantAttr.Projectile, PlantAttr.Element, PlantAttr.Effect };

        public int LevelSum { get { int s = 0; foreach (var a in All) s += Get(a).level; return s; } }
        /// <summary>Sterne 1..5 als Gesamtqualitaet.</summary>
        public int Stars => Mathf.Clamp(Mathf.RoundToInt(LevelSum / 5f), 1, 5);

        public float Range => PlantBalance.I.rangeBase + PlantBalance.I.rangePerLevel * range.level;

        public float Damage
        {
            get
            {
                var b = PlantBalance.I;
                var proj = b.Option(PlantAttr.Projectile, projectile.id);
                float d = b.baseDamage * (1 + b.damagePerLevel * (LevelSum - 5)) * (proj != null ? proj.damage : 1f);
                d *= 1 + 0.08f * (projectile.level - 1);
                if (element.id == "nature") d *= 1 + 0.12f * element.level;
                return d;
            }
        }

        public float Cooldown
        {
            get
            {
                var proj = PlantBalance.I.Option(PlantAttr.Projectile, projectile.id);
                return PlantBalance.I.baseCooldown * (proj != null ? proj.cooldown : 1f) * Mathf.Pow(0.94f, projectile.level - 1);
            }
        }

        public static string Describe(Gene g)
        {
            if (g.attr == PlantAttr.Range) return (PlantBalance.I.rangeBase + PlantBalance.I.rangePerLevel * g.level).ToString("0.0") + " Felder";
            var o = PlantBalance.I.Option(g.attr, g.id);
            return o != null ? o.name : g.id;
        }

        public static string AttrName(PlantAttr a) => a switch
        {
            PlantAttr.Pattern => "Fläche",
            PlantAttr.Range => "Distanz",
            PlantAttr.Projectile => "Projektil",
            PlantAttr.Element => "Wirkung",
            _ => "Zusatzeffekt"
        };

        public Color ElementColor
        {
            get
            {
                var o = PlantBalance.I.Option(PlantAttr.Element, element.id);
                return o != null && ColorUtility.TryParseHtmlString(o.color, out var c) ? c : Color.white;
            }
        }
    }

    /// <summary>Eine ausgewachsene Pflanze im Blumentopf, frei auf der Karte aufgestellt (verteidigt den Hof).</summary>
    [Serializable]
    public class PotState
    {
        public float x, y;
        public string type;
        public int level;   // Stufe der Pflanze im Topf (0/1 = Grundstufe)
        public int Lv => Mathf.Max(1, level);
        public Vector2 Pos => new Vector2(x, y);
        public PlantGenome Genome { get { var c = PlantBalance.I.Plant(type); return c != null ? c.Genome : PlantBalance.I.catalog[0].Genome; } }
    }

    /// <summary>Ein Zaunstueck auf einer Rasterzelle (Spielstand).</summary>
    [Serializable] public class FenceState { public int x, y; public float hp; }

    /// <summary>Eine gepflanzte Angriffspflanze auf einem Feld (Spielstand).</summary>
    [Serializable]
    public class PlantedState
    {
        public int x, y;          // Feldzelle
        public string type;       // Katalog-Id
        public int growth;        // Tipps bisher
        public int level, w, h;   // Stufe (Zusammenfuehren) und Flaechenbedarf in Feldern (0 = 1)
        public int Lv => Mathf.Max(1, level);
        public int W => Mathf.Max(1, w);
        public int H => Mathf.Max(1, h);
        public bool Covers(Cell c) => c.x >= x && c.x < x + W && c.y >= y && c.y < y + H;
        public bool Grown => growth >= PlantBalance.I.growTaps;
        public PlantGenome Genome { get { var c = PlantBalance.I.Plant(type); return c != null ? c.Genome : PlantBalance.I.catalog[0].Genome; } }
    }
}
