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
        public string character;         // Charakter bei "single": "fire" (Feuerweizen), "spark" (Blitzmais)
        public float sway = 1f;          // Wiegen im Wind (0 = feste Pflanze, z. B. Pilze)
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


    /// <summary>Verbesserung im Laden (wie bei Cookie Clicker). effect: click, harvest, growth, water, fert, auto, bee, light, wall, cps, offline, golden.</summary>
    [Serializable] public class ShopUpgradeDef { public string id, name, desc, effect; public float value, cost, unlockEarned; }
    /// <summary>Gebaeude: erzeugt dauerhaft Muenzen pro Sekunde, Preis steigt mit jedem Kauf.</summary>
    [Serializable] public class BuildingDef { public string id, name, desc, art; public float baseCost, cps, scale = 1f; }
    /// <summary>Hof erweitern: Kosten der naechsten Stufe.</summary>
    [Serializable] public class FarmLevelDef { public float cost; public int wood; }
    [Serializable] public class WallDef { public int wood = 2; public float hp = 30f; }

    /// <summary>Rollen der Pflanzen: combat (verteidigt), water (giesst Nachbarn), harvest (bringt Muenzen),
    /// fert (Duenger fuer Nachbarn), boost (Klicks in der Naehe zaehlen doppelt), auto (klickt selbst Nachbarn an),
    /// bee (Honig = Muenzen, mehr bei Blumen), light (Zombies im Licht nehmen mehr Schaden).</summary>
    public static class Roles
    {
        public static string Name(string r) => r switch
        {
            "water" => "Wasser", "harvest" => "Ernte", "fert" => "Dünger", "boost" => "Verstärker", "auto" => "Automatik",
            "bee" => "Honig", "light" => "Licht", _ => "Kampf"
        };
        public static string Desc(CatalogDef d) => d.role switch
        {
            "water" => "Gießt Felder im Umkreis von 2: dort wächst alles doppelt so schnell und Ernte bringt +50 %.",
            "harvest" => "Reif: jeder Tipp bringt " + d.value + " Münzen, dazu verdient sie von selbst.",
            "fert" => "Direkte Nachbarn wachsen schneller und bringen +50 % Münzen.",
            "boost" => "Tipps auf Pflanzen im Umkreis von 2 zählen doppelt.",
            "auto" => "Klickt von selbst auf ihre direkten Nachbarn – je höher die Stufe, desto schneller.",
            "bee" => "Macht Honig: verdient von selbst, mehr mit Blumen in der Nähe.",
            "light" => "Leuchtet nachts: Zombies in ihrem Licht nehmen +50 % Schaden.",
            _ => "Verteidigt den Hof in der Nacht."
        };
    }

    /// <summary>Eine Pflanze im Katalog: feste Eigenschaften, Rolle, Preis; wird freigeschaltet (Welle oder verdiente Muenzen).</summary>
    [Serializable]
    public class CatalogDef
    {
        public string id, name, species; public bool starter; public int unlockWave;
        public string role = "combat";
        public float cost, value, unlockEarned;
        public int growTaps;   // 0 = Standard (growTaps)
        public bool IsCombat => string.IsNullOrEmpty(role) || role == "combat";
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
        public int levelTaps = 40;   // Klicks auf eine reife Pflanze bis zur naechsten Stufe
        public int[] growMilestones;
        public OptionDef[] patterns, projectiles, elements, effects;
        public SpeciesDef[] species;
        public CatalogDef[] catalog;
        public MonsterDefW[] monsters;
        public WaveDef waves;
        public WeaponDef[] weapons;
        public ShopUpgradeDef[] upgrades;
        public BuildingDef[] buildings;
        public FarmLevelDef[] farmLevels;
        public WallDef wall;
        public float startCoins = 30f;
        public bool testUnlockAll;   // Test: alle Pflanzenarten sofort freigeschaltet
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
            var spc = I.Species(g.species);
            if (I.RoleOf(g.typeId) != "combat") return Color.white;   // Nutzpflanzen behalten ihre Farbe
            if (spc != null && spc.character == "toxic")
            {
                // Giftruebe: wird kraenklich gruen, dann violett geadert
                return tier <= 1 ? Color.Lerp(Color.white, el, 0.06f)
                     : tier == 2 ? new Color(0.93f, 1.04f, 0.86f) : tier == 3 ? new Color(0.9f, 0.86f, 1.02f) : new Color(0.82f, 0.98f, 0.78f);
            }
            if (PlantForms.IsSingle(spc) && spc.character == "spark")
            {
                // Blitzmais: wird mit jeder Mutation heller, die Koerner leuchten wie Gluehbirnen
                return tier <= 1 ? Color.Lerp(Color.white, el, 0.06f)
                     : tier == 2 ? new Color(1f, 1f, 0.86f) : tier == 3 ? new Color(1.04f, 1.02f, 0.8f) : new Color(1.08f, 1.05f, 0.74f);
            }
            if (PlantForms.IsSingle(spc))
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
        public int GrowTapsFor(string id) { var c = Plant(id); return c != null && c.growTaps > 0 ? c.growTaps : growTaps; }
        public string RoleOf(string id) { var c = Plant(id); return c == null || string.IsNullOrEmpty(c.role) ? "combat" : c.role; }
        public WallDef Wall => wall ?? (wall = new WallDef());
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

    /// <summary>Ein Zaunstueck auf einer Rasterzelle (Spielstand).</summary>

    /// <summary>Ein Mauerstueck (Holz) auf einer Rasterzelle: blockiert Zombies, darauf kann eine Pflanze stehen.</summary>
    [Serializable] public class WallState { public int x, y; public float hp; }

    [Serializable] public class BuildingCount { public string id; public int n; }

    /// <summary>Eine gepflanzte Pflanze auf einem Feld oder Mauerplatz (Spielstand).</summary>
    [Serializable]
    public class PlantedState
    {
        public int x, y;          // Feldzelle
        public string type;       // Katalog-Id
        public int growth;        // Tipps bisher
        public int level, w, h;   // Stufe (Klicken / Zusammenfuehren) und Flaechenbedarf in Feldern (0 = 1)
        public int xp;            // Klicks seit der letzten Stufe (reife Pflanze)
        public int Lv => Mathf.Max(1, level);
        public int W => Mathf.Max(1, w);
        public int H => Mathf.Max(1, h);
        public bool Covers(Cell c) => c.x >= x && c.x < x + W && c.y >= y && c.y < y + H;
        public int GrowTaps => PlantBalance.I.GrowTapsFor(type);
        public bool Grown => growth >= GrowTaps;
        public string Role => PlantBalance.I.RoleOf(type);
        public bool IsCombat => Role == "combat";
        public PlantGenome Genome { get { var c = PlantBalance.I.Plant(type); return c != null ? c.Genome : PlantBalance.I.catalog[0].Genome; } }
    }
}
