using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    // ------------------------------------------------------------------ Balancing (Resources/Balance/plants.json)

    [Serializable] public class OptionDef { public string id, name, desc, color; public float speed, damage, cooldown, homing; }
    [Serializable] public class SpeciesDef { public string id, name, art; public int[] stages; public float scale; }
    [Serializable] public class MonsterDefW { public string id, name, art, weakTo; public float hp, speed, damage, scale, weight, seedChance, plantChance; public int firstWave; }
    [Serializable] public class WaveDef
    {
        public bool autoWave; public float firstWaveDelay, dayDuration, spawnInterval, hpGrowthPerWave, seedLevelPerWave, houseHp, tapDamage;
        public int baseCount, countPerWave, woodLossOnDefeat;
    }

    [Serializable]
    public class PlantBalance
    {
        public float baseDamage, baseCooldown, damagePerLevel, rangeBase, rangePerLevel;
        public int growTaps, maxLevel;
        public OptionDef[] patterns, projectiles, elements, effects;
        public SpeciesDef[] species;
        public MonsterDefW[] monsters;
        public WaveDef waves;

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
    }

    // ------------------------------------------------------------------ Erbgut einer Angriffspflanze

    /// <summary>Die fuenf Eigenschaften einer Angriffspflanze.</summary>
    public enum PlantAttr { Pattern, Range, Projectile, Element, Effect }

    [Serializable]
    public class Gene
    {
        public PlantAttr attr;
        public string id;     // bei Distanz leer
        public int level;
        public Gene Clone() => new Gene { attr = attr, id = id, level = level };
    }

    [Serializable]
    public class PlantGenome
    {
        public int uid;
        public string name;
        public string species;
        public Gene pattern, range, projectile, element, effect;
        // Feldzustand
        public bool planted;
        public int cellX, cellY;
        public int growth;

        public Gene Get(PlantAttr a) => a switch
        {
            PlantAttr.Pattern => pattern,
            PlantAttr.Range => range,
            PlantAttr.Projectile => projectile,
            PlantAttr.Element => element,
            _ => effect
        };

        public void Set(PlantAttr a, Gene g)
        {
            g = g.Clone(); g.attr = a;
            switch (a)
            {
                case PlantAttr.Pattern: pattern = g; break;
                case PlantAttr.Range: range = g; break;
                case PlantAttr.Projectile: projectile = g; break;
                case PlantAttr.Element: element = g; break;
                default: effect = g; break;
            }
        }

        public static readonly PlantAttr[] All = { PlantAttr.Pattern, PlantAttr.Range, PlantAttr.Projectile, PlantAttr.Element, PlantAttr.Effect };

        public int LevelSum { get { int s = 0; foreach (var a in All) s += Get(a).level; return s; } }
        /// <summary>Sterne 1..5 als Gesamtqualitaet.</summary>
        public int Stars => Mathf.Clamp(Mathf.RoundToInt(LevelSum / 5f), 1, 5);
        public bool Grown => growth >= PlantBalance.I.growTaps;

        // ---------------------------------------------------------------- Kampfwerte

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

        /// <summary>Lesbare Beschreibung einer Eigenschaft (z. B. "Feuer  Stufe 3").</summary>
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

        public PlantGenome Clone() => JsonUtility.FromJson<PlantGenome>(JsonUtility.ToJson(this));
    }

    // ------------------------------------------------------------------ Erzeugen, Veredeln, Kreuzen

    public static class Genetics
    {
        static readonly Dictionary<string, string> elementPrefix = new Dictionary<string, string>
        { { "nature", "Moos" }, { "fire", "Glut" }, { "ice", "Frost" }, { "poison", "Gift" }, { "lightning", "Blitz" } };

        public static string MakeName(PlantGenome g)
        {
            var sp = PlantBalance.I.Species(g.species);
            string pre = elementPrefix.TryGetValue(g.element.id, out var p) ? p : "";
            return pre + sp.name;
        }

        static Gene RandomGene(PlantAttr a, System.Random r, int minLv, int maxLv)
        {
            var g = new Gene { attr = a, level = r.Next(minLv, maxLv + 1) };
            var opts = PlantBalance.I.Options(a);
            if (opts != null) g.id = opts[r.Next(opts.Length)].id;
            return g;
        }

        /// <summary>Zufaellige Angriffspflanze (Start-Pflanzen, gefundene Pflanzen).</summary>
        public static PlantGenome Random(int uid, System.Random r, int minLv = 1, int maxLv = 2)
        {
            var b = PlantBalance.I;
            var g = new PlantGenome
            {
                uid = uid,
                species = b.species[r.Next(b.species.Length)].id,
                pattern = RandomGene(PlantAttr.Pattern, r, minLv, maxLv),
                range = RandomGene(PlantAttr.Range, r, minLv, maxLv + 1),
                projectile = RandomGene(PlantAttr.Projectile, r, minLv, maxLv),
                element = RandomGene(PlantAttr.Element, r, minLv, maxLv),
                effect = RandomGene(PlantAttr.Effect, r, minLv, maxLv)
            };
            g.name = MakeName(g);
            return g;
        }

        /// <summary>Samen, den ein Monster fallen laesst: eine einzelne Eigenschaft.</summary>
        public static Gene RandomSeed(System.Random r, int wave)
        {
            var attr = PlantGenome.All[r.Next(PlantGenome.All.Length)];
            float lv = 1 + wave * PlantBalance.I.waves.seedLevelPerWave * (float)r.NextDouble() + (float)r.NextDouble();
            var g = RandomGene(attr, r, 1, 1);
            g.level = Mathf.Clamp(Mathf.FloorToInt(lv), 1, PlantBalance.I.maxLevel);
            return g;
        }

        /// <summary>Ergebnis, wenn ein Samen in eine Pflanze eingekreuzt wird (Vorschau und Anwendung).</summary>
        public static Gene Infuse(Gene current, Gene seed)
        {
            int max = PlantBalance.I.maxLevel;
            var result = current.Clone();
            if (seed.attr == PlantAttr.Range || seed.id == current.id)
                result.level = Mathf.Min(max, Mathf.Max(current.level, seed.level) + 1);   // gleiche Eigenschaft: verstaerken
            else
            {
                result.id = seed.id;                                                       // andere Eigenschaft: ersetzen
                result.level = Mathf.Max(1, seed.level);
            }
            return result;
        }

        /// <summary>
        /// Zwei Pflanzen kreuzen: Jede Eigenschaft kommt von einem Elternteil (meist vom staerkeren),
        /// gleiche Eigenschaften verstaerken sich, manchmal mutiert etwas nach oben.
        /// </summary>
        public static PlantGenome Cross(PlantGenome a, PlantGenome b, int uid, System.Random r)
        {
            int max = PlantBalance.I.maxLevel;
            var child = a.Clone();
            child.uid = uid;
            child.species = r.NextDouble() < 0.5 ? a.species : b.species;
            foreach (var attr in PlantGenome.All)
            {
                var ga = a.Get(attr); var gb = b.Get(attr);
                Gene pick;
                if (ga.id == gb.id) { pick = ga.Clone(); pick.level = Mathf.Min(max, Mathf.Max(ga.level, gb.level) + (r.NextDouble() < 0.6 ? 1 : 0)); }
                else pick = (r.NextDouble() < 0.7 ? (ga.level >= gb.level ? ga : gb) : (r.NextDouble() < 0.5 ? ga : gb)).Clone();
                child.Set(attr, pick);
            }
            if (r.NextDouble() < 0.3)
            {
                var attr = PlantGenome.All[r.Next(PlantGenome.All.Length)];
                var g = child.Get(attr).Clone(); g.level = Mathf.Min(max, g.level + 1);
                child.Set(attr, g);
            }
            child.name = MakeName(child);
            child.planted = a.planted; child.cellX = a.cellX; child.cellY = a.cellY; child.growth = a.growth;
            return child;
        }
    }
}
