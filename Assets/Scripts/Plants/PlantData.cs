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
        public int trampleMax = 8;   // Niederlage: so viele Ernte-Pflanzen zertrampeln die Zombies hoechstens (eine je uebriger Zombie)
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
    [Serializable] public class ShopUpgradeDef { public string id, name, desc, effect; public float value, cost, unlockEarned; public string[] needs; }   // needs: siehe WorldBuilder.Needs
    /// <summary>Gebaeude: erzeugt dauerhaft Muenzen pro Sekunde, Preis steigt mit jedem Kauf.</summary>
    /// <summary>Hof erweitern: Kosten der naechsten Stufe.</summary>
    [Serializable] public class FarmLevelDef { public float cost; public int wood; public string[] crops; }   // crops: Erntekosten, z. B. "weizen:30"
    /// <summary>Ernte im Vorrat (gelieferte Stuecke je Sorte, fuer Baukosten).</summary>
    [Serializable] public class CropCount { public string id; public double n; }
    /// <summary>Mauer: Holz pro Stueck (darf ein Bruchteil sein, z. B. 0,2 = fuenf Mauern pro Holz) und Haltbarkeit.</summary>
    [Serializable] public class WallDef { public float wood = 2f; public float hp = 30f; }
    /// <summary>Steinpult: Preis in Muenzen (Abbauen gibt ihn zurueck).</summary>
    [Serializable] public class PedestalDef { public float cost = 20f; }
    /// <summary>Gartenfelder: die ersten free Felder gratis, danach kostet jedes neue Feld cost * growth^(Anzahl - free).</summary>
    [Serializable] public class FieldDef { public int free = 6; public float cost = 10f, growth = 1.1f; }
    /// <summary>Tagesaufgabe: so viele Stueck des Tages-Ernteguts fuer Bronze, Silber, Gold.</summary>
    /// <summary>Ein Ziel: genau vier Aufgaben (Bedingungen wie bei needs) und was danach frei wird.</summary>
    [Serializable] public class MilestoneDef { public string name, desc; public string[] tasks, unlocks; public float coinShare = 0.25f; public int runes = 1; public bool project; }
    /// <summary>Grossprojekte nach dem letzten Ziel: endlos, die Mengen wachsen je Projekt um den Faktor.</summary>
    [Serializable] public class BigProjectDef
    {
        public string[] names = { "Erntegigant", "Zombiewall", "Lieferimperium", "Goldene Ära", "Ewiger Sommer", "Knolli-Königreich" };
        public double delivered = 25000, deliveredGrowth = 2.5, kills = 300, killsGrowth = 2, earned = 2e7, earnedGrowth = 6;
        public float bonusPerProject = 0.2f, coinShare = 0.3f; public int runes = 3;
    }

    [Serializable] public class ChallengeDef
    {
        public int[] tiers = { 10, 30, 60 };
        public float growthPerDay = 0.5f;   // die Mengen wachsen jeden Tag mit (Tag 9: x5) – man braucht mehrere Felder derselben Pflanze
        public float[] earnedShare = { 0.12f, 0.2f, 0.35f };   // Muenzen je Stufe als Anteil am bisher Verdienten (gross, waechst mit dem Hof)
    }
    /// <summary>Ernte-Lieferung: Vorrat je Pflanze, Knollis (Preis, Reichweite, Tempo).</summary>
    /// <summary>Bach: Muenzen je Stueck; Pflanzen am Bach gelten als gegossen und wachsen/produzieren schneller.</summary>
    [Serializable] public class RiverDef { public float cost = 10f, growth = 1.5f, produce = 1.5f; }
    [Serializable] public class RiverState { public int x, y, dir, seq; }
    [Serializable] public class DeliveryDef
    {
        public int startKnollis = 1;
        public float knolliRange = 1.6f;   // Umkreis, aus dem ein Knolli alle Ernte auf einmal einsammelt (Magnet)
        public float knolliCost = 40f, knolliCostGrowth = 1.6f, knolliSpeed = 1.6f;
        // Berufe und Wohnplaetze
        public int baseHousing = 2, workWaterPerTrip = 4;
        // Sammelrunde der Traeger: bis zu tripStops Haufen (je hoechstens tripReach entfernt), bis tripLoad Stueck dabei sind
        public int tripLoad = 30, tripStops = 5;
        public float tripReach = 4f;
        public float workWetSeconds = 15f, workChopInterval = 0.8f, workRepair = 0.08f, guardInterval = 1.4f, guardRange = 2.6f, guardDamage = 2f;
    }
    /// <summary>Runen-Altar: pro geopfertem Runenstein (Fluch-Stufe) werden Zombies staerker, groesser und zahlreicher,
    /// dafuer gibt es mehr Muenzen pro Zombie, einen Bonus beim Ueberstehen (victoryBonus * Stufe * Welle) und oefter Runensteine.</summary>
    [Serializable] public class AltarDef
    {
        public int maxLevel = 5, startRunes = 3;
        public float hpPerLevel = 0.6f, damagePerLevel = 0.35f, sizePerLevel = 0.12f, countPerLevel = 0.25f;
        public float coinsPerLevel = 1f, victoryBonus = 40f, runeDrop = 0.05f, runeDropPerLevel = 0.05f, rockRune = 0.2f;
    }
    /// <summary>Verwandlungen: wie viele Holzmauern Steinmoos umwandelt, wie viel Stein haelt, hoechste Element-Stufe einer Waffe.</summary>
    /// <summary>Klick-Regler (plants.json -> clicks): scale gilt fuer alles, die anderen nur fuer ihren Bereich –
    /// grow: Wachsen, medal: Medaillen und Kampf-Stufen, clear: Baeume, Steine, Geruempel, forge: Waffenbank, build: Bauwerke.</summary>
    /// <summary>Klick-Belohnung (plants.json -> clickReward): jeder Klick, der selbst nichts erntet (Wachsen, Aufraeumen,
    /// Schmieden, Bauwerk ausbauen), bringt base + cpsShare × Muenzen pro Sekunde, mal Handschuh-Verbesserungen.</summary>
    /// <summary>Laden neben dem Haus (plants.json -> shopLevel): jede Stufe bringt coinBonusPerLevel mehr Muenzen auf alles.
    /// Klicks je Stufe wie bei Bauwerken (structureTaps).</summary>
    /// <summary>Gebaeude schimmern ab diesen Stufen ueber dem Dach: Bronze, Silber, Gold.</summary>
    /// <summary>Nebel: die Sonnenlilie kostet costMult × den Preis der naechsten Ausbaustufe (dazu deren Holz und Ernte).</summary>
    [Serializable] public class FogDef { public float costMult = 2f, growth = 1.6f; }   // growth: jedes Areal nach den Ausbaustufen so viel teurer
    [Serializable] public class BuildingGlowDef { public int[] levels = { 5, 10, 15 }; }
    [Serializable] public class ShopLevelDef { public float coinBonusPerLevel = 0.04f; }
    [Serializable] public class ClickRewardDef { public float @base = 1f, cpsShare = 0.05f; }
    [Serializable] public class ClicksDef { public float scale = 3f, grow = 1f, medal = 1f, clear = 1f, forge = 1f, build = 1f; }
    [Serializable] public class TransformsDef
    {
        public float stoneHpMult = 3f; public int maxElementLevel = 5;   // Steinmoos wandelt immer den ganzen Mauerabschnitt
        public float thornDps = 2.5f; public int maxThorns = 5;              // Dornenranke: Schaden pro Sekunde je Stufe an schlagenden Zombies
        public float heartRange = 0.6f; public int maxHeart = 3;              // Wurzelherz: Reichweite je Stufe fuer die Pflanze im Pult
        public float stoneTowerDamage = 1.75f, stoneTowerRange = 1f;         // Steinmoos auf Wachturm
        public string[] wallTierNames = { "Granit", "Kristall" };          // Stufen nach Stein
        public float[] wallTierHpMults = { 7f, 16f };                        // Haltbarkeit je Stufe (Stein: stoneHpMult)
        public int[] wallTierMoss = { 3, 6 };                                // Steinmoos je Mauer fuer die Stufe
        public int mossWallsPerFeed = 6;                                     // so viele Mauern versorgt ein Steinmoos beim Verstaerken
        public float mossHealBelow = 0.6f;                                   // darunter flickt Steinmoos den ganzen Abschnitt
    }
    /// <summary>Waffenbank: Schlaege bis zur naechsten Waffenstufe (tapsBase * tapsGrowth^(Stufe-1)) und was eine Stufe bringt.</summary>
    [Serializable] public class BenchDef { public float tapsBase = 12f, tapsGrowth = 1.3f, damagePerLevel = 0.25f, cooldownPerLevel = 0.04f, minCooldown = 0.5f; public int maxLevel = 30;
        public float weaponPower = 0.4f; }   // Faktor auf allen Waffenschaden: Zombies werden hauptsaechlich von Pflanzen besiegt
    /// <summary>Bauwerk zum Aufstellen (einmal kaufen, auf einen freien Platz im Hof setzen, per Klick verbessern).
    /// effect: scare (Tipps in der Naehe staerker), water (giesst Umkreis), light (Zombies nehmen mehr Schaden), tower (schiesst nachts),
    /// seedcost (Samen billiger), totem (verstaerkt eine aufgezogene Flaeche).
    /// radius 0 = wirkt ueberall.</summary>
    [Serializable] public class StructureDef
    {
        public string id, name, desc, effect;
        public float cost, unlockEarned, value, valuePerLevel, radius, radiusPerLevel, scale = 1f;
        public string[] needs;   // Bedingungen auf dem Hof (siehe WorldBuilder.Needs)
        public string[] crops;   // Erntekosten, z. B. "weizen:30" (WorldBuilder.Storage)
        public int wood;
        // Totems (effect "totem"): mehrfach kaufbar (multi, jedes Stueck costGrowth-mal teurer), boost = growth | harvest | produce,
        // volle Wirkung fuer ein Beet bis fullCells Felder, darueber anteilig weniger
        public bool multi; public string boost; public int fullCells = 6; public float costGrowth = 1.8f;
    }
    /// <summary>Klicks bis zur naechsten Stufe eines Bauwerks: tapsBase * tapsGrowth^(Stufe-1).</summary>
    [Serializable] public class StructureTapsDef { public float tapsBase = 15f, tapsGrowth = 1.35f; public int maxLevel = 20; }
    /// <summary>Helfer-Pflanzen: Wurzeltraeger (carrier), Pustesamen (sower), Saftrohr (pipe).</summary>
    [Serializable] public class HelperDef
    {
        public float carrierInterval = 7f; public int carrierWood = 1;
        public float sowerInterval = 4f; public int sowerRadius = 2;
        // Giessen: je Tropfen so viele Stueck (+1 je zwei Stufen des Tauschilfs) zu diesem Anteil des Ernte-Werts; die Kanne eines Giessers zaehlt mehrfach
        public int waterDropItems = 2; public float waterDropValue = 0.5f, waterCanMult = 4f;
        // Tauschilf tropft alle waterInterval s (schneller mit Stufe), jeder Tropfen bringt auch Stufen-Fortschritt
        public float waterInterval = 1.2f; public int waterDropXp = 1;
        // Klopfwurz: Tipp alle autoInterval s, schneller mit Stufe und Upgrades, aber hoechstens autoMaxRate Tipps/s
        // (ein fleissiger Spieler schafft ~4 – Klicken bleibt am staerksten); jeder Tipp erntet zu autoValue des Werts
        public float autoInterval = 0.7f, autoMaxRate = 6f, autoValue = 0.5f;
        // Ernte-Pflanzen in Reichweite eines reifen Tauschilfs bekommen so viel mal mehr Stufen-Fortschritt (Klicks und Tropfen)
        public float waterLevelMult = 2f;
        public float friendBonus = 0.5f;   // je reifer Lieblingsnachbar (8 Felder rundherum) +50 % Ernte
        // Kombo: je mehr verschiedene Quellen ein Feld verstaerken (Freunde, Tauschilf, Bach, Duenger, Trommelkraut, Totem, Gruppe),
        // desto mehr obendrauf – Index = Zahl der Quellen
        public float[] comboMults = { 1f, 1f, 1f, 1.5f, 2f, 3f, 4f, 5f };
        public int pipeRadius = 2, pipeBonus = 1;
    }
    /// <summary>Stuetzranke: Reichweite, Gebaeude-Bonus, Hammerschlaege an der Waffenbank, Mauer-Reparatur (Anteil der vollen Haltbarkeit).</summary>
    [Serializable] public class SupportDef { public float radius = 2.6f, radiusPerLevel = 0.2f, interval = 2.4f, hammer = 1f, repair = 0.1f; }

    /// <summary>Rollen der Pflanzen: combat (verteidigt), water (giesst Nachbarn), harvest (bringt Muenzen),
    /// fert (Duenger fuer Nachbarn), boost (Klicks in der Naehe zaehlen doppelt), auto (klickt selbst Nachbarn an),
    /// light (Zombies im Licht nehmen mehr Schaden).</summary>
    public static class Roles
    {
        public static string Name(string r) => r switch
        {
            "water" => "Wasser", "harvest" => "Ernte", "fert" => "Dünger", "boost" => "Verstärker", "auto" => "Automatik",
            "light" => "Licht", "support" => "Bauhelfer",
            "transport" => "Transport", "carrier" => "Träger", "sower" => "Säer", "pipe" => "Leitung", "transform" => "Verwandlung", _ => "Kampf"
        };
        static string HarvestDesc(CatalogDef d)
        {
            string prod = string.IsNullOrEmpty(d.product) ? d.name : d.product;
            string s = "Reif: jeder Tipp bringt " + (d.yield > 1 ? d.yield + "× " : "") + prod + ", dazu wächst von selbst nach.";
            if (d.prod >= 2f) s += " Macht besonders viel von selbst.";
            else if (d.prod <= 0.5f) s += " Macht kaum etwas von selbst – lohnt sich zum Klicken.";
            if (d.group > 0f) s += " +" + Mathf.RoundToInt(d.group * 100) + " % je gleicher Nachbar.";
            return s + " Knollis tragen es zur Lieferkiste.";
        }
        public static string Desc(CatalogDef d) => d.role switch
        {
            "water" => "Tropft auf die Felder links, rechts, oben und unten: reife Pflanzen werfen dabei Ernte ab und steigen schneller auf.",
            "harvest" => HarvestDesc(d),
            "fert" => "Direkte Nachbarn wachsen schneller und bringen +50 % Münzen.",
            "boost" => "Tipps auf Pflanzen im Umkreis von 2 zählen doppelt.",
            "auto" => "Klickt von selbst das Feld vor sich – schneller mit jeder Stufe.",
            "light" => "Leuchtet nachts: Zombies in ihrem Licht nehmen +50 % Schaden.",
            "transport" => "Erntet reife Ernte-Pflanzen im Umkreis von 2 – Münzen +50 %.",
            "carrier" => "Trägt Holz aus dem Wald zum Haus, schneller mit jeder Stufe.",
            "sower" => "Sät die gewählte Pflanze auf leere Felder im Umkreis von 2.",
            "pipe" => "Tauschilf im Umkreis von 2 gießt 1 Feld weiter.",
            "transform" => d.transform switch
            {
                "stone" => "Reif auf einen Mauerabschnitt oder hölzernen Wachturm ziehen: wird ganz zu Stein (Mauern 3× so stabil, Turm trifft härter und weiter).",
                "thorns" => "Reif auf einen Mauerabschnitt ziehen: der ganze Abschnitt bekommt Dornen – schlagende Zombies verletzen sich.",
                "heart" => "Reif in ein Steinpult ziehen: die Pflanze darin schießt weiter.",
                "moon" => "Reif auf einen Wachturm ziehen: der Turm schießt doppelt.",
                "fire" => "Reif auf die Waffenbank ziehen: alle Waffen setzen Zombies in Brand (ersetzt ein anderes Element).",
                "ice" => "Reif auf die Waffenbank ziehen: Treffer aller Waffen verlangsamen Zombies (ersetzt ein anderes Element).",
                "lightning" => "Reif auf die Waffenbank ziehen: Treffer aller Waffen springen als Blitz über (ersetzt ein anderes Element).",
                "poison" => "Reif auf die Waffenbank ziehen: Treffer aller Waffen vergiften Zombies (ersetzt ein anderes Element).",
                _ => "Reif auf ein Ziel ziehen – dort verwandelt sie etwas."
            },
            "support" => "Hämmert an der Waffenbank und flickt Mauern in ihrer Nähe.",
            _ => "Verteidigt den Hof in der Nacht."
        };
    }

    /// <summary>Eine Pflanze im Katalog: feste Eigenschaften, Rolle, Preis; wird freigeschaltet (Welle oder verdiente Muenzen).</summary>
    [Serializable]
    public class CatalogDef
    {
        public string id, name, species; public bool starter; public int unlockWave;
        public string product;     // Ernte-Produkt (Name), z. B. "Radieschen-Bund" – sonst der Pflanzenname
        public string transform;   // Rolle transform: stone (Holzmauer -> Stein) oder fire/ice/lightning/poison (Waffenbank)
        public string role = "combat";
        public float cost, value, unlockEarned;
        public int unlockFields;   // zusaetzlich so viele Felder noetig (Ausbau gehoert zum Fortschritt)
        public string buildsOn;    // worauf sie aufbaut: eine Pflanzen-ID oder ein Ding auf dem Hof (Mauer, Waffenbank, Wachturm, Steinpult, Knollis, Kampfpflanzen)
        public string helps;       // konkret: was sie deiner Farm bringt und wie man sie einsetzt
        public string[] needs;     // Bedingungen auf dem Hof, z. B. walls:6, plant:tauschilf:2, medal:3 (siehe WorldBuilder.Needs)
        public string[] friends;   // Lieblingsnachbarn (Ernte-Sorten): je reifer Freund rundherum mehr Ertrag
        public int growTaps;   // 0 = Standard (growTaps)
        // Ernte-Charakter: yield = Stueck pro Ernte-Tipp, prod = Faktor fuer Ernte von selbst, group = Bonus je gleicher
        // Nachbarpflanze (auch schraeg), taps = Faktor fuer den Wert eines Ernte-Tipps
        public int yield = 1; public float prod = 1f, group, taps = 1f;
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
        public int levelTaps = 60;   // Klicks auf eine reife Pflanze von Stufe 1 auf 2 ...
        public float levelGrowth = 1.35f;   // ... und jede weitere Stufe braucht so viel mal mehr
        // Ernte-Pflanzen: Medaillen statt Zahlen-Stufen – normal -> Bronze -> Silber -> Gold (Klicks je Aufstieg, Ertrag je Stufe)
        public int[] harvestLevelTaps = { 60, 300, 1500 };
        public float[] harvestLevelMult = { 1f, 2.5f, 6f, 15f };
        public int HarvestMaxLevel => (harvestLevelTaps?.Length ?? 0) + 1;

        /// <summary>Klicks, die eine reife Pflanze auf Stufe lv bis zur naechsten Stufe braucht.</summary>
        public int LevelTapsFor(int lv) => Mathf.Max(1, Mathf.RoundToInt(levelTaps * Mathf.Pow(levelGrowth, Mathf.Max(0, lv - 1)) * Click("medal")));

        /// <summary>Zentraler Klick-Regler: alle Klickzahlen mal clicks.scale, dazu je Bereich ein eigener Faktor.</summary>
        public ClicksDef clicks = new ClicksDef();
        public ClickRewardDef clickReward = new ClickRewardDef();
        public ShopLevelDef shopLevel = new ShopLevelDef();
        public BuildingGlowDef buildingGlow = new BuildingGlowDef();
        public FogDef fog = new FogDef();
        public FogDef Fog => fog ?? (fog = new FogDef());
        public float Click(string kind)
        {
            var c = clicks ?? new ClicksDef();
            float k = kind switch { "grow" => c.grow, "medal" => c.medal, "clear" => c.clear, "forge" => c.forge, "build" => c.build, _ => 1f };
            return Mathf.Max(0.01f, c.scale * k);
        }
        /// <summary>Klickzahl mit Regler, mindestens 1.</summary>
        public int Clicks(float baseTaps, string kind) => Mathf.Max(1, Mathf.RoundToInt(baseTaps * Click(kind)));
        public int[] growMilestones;
        public OptionDef[] patterns, projectiles, elements, effects;
        public SpeciesDef[] species;
        public CatalogDef[] catalog;
        public MilestoneDef[] milestones;   // Ziele mit je vier Aufgaben, nacheinander
        public BigProjectDef projects = new BigProjectDef();   // danach endlose Grossprojekte
        public MonsterDefW[] monsters;
        public WaveDef waves;
        public WeaponDef[] weapons;
        public ShopUpgradeDef[] upgrades;
        public FarmLevelDef[] farmLevels;
        public WallDef wall;
        public PedestalDef pedestal;
        public FieldDef field;
        public DeliveryDef delivery;
        public RiverDef river;
        public ChallengeDef challenge;
        public AltarDef altar;
        public TransformsDef transforms;
        public BenchDef bench;
        public SupportDef support;
        public StructureDef[] structures;
        public StructureTapsDef structureTaps;
        public HelperDef helpers;
        public float startCoins = 30f;
        public bool testUnlockAll;   // Test: alle Pflanzenarten sofort freigeschaltet
        public int treeRegrowNights = 3;   // nach so vielen Naechten steht ein gefaellter Baum wieder
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
        public int GrowTapsFor(string id) { var c = Plant(id); return Clicks(c != null && c.growTaps > 0 ? c.growTaps : growTaps, "grow"); }
        public string RoleOf(string id) { var c = Plant(id); return c == null || string.IsNullOrEmpty(c.role) ? "combat" : c.role; }
        public WallDef Wall => wall ?? (wall = new WallDef());
        public PedestalDef Pedestal => pedestal ?? (pedestal = new PedestalDef());
        public DeliveryDef Delivery => delivery ?? (delivery = new DeliveryDef());
        public FieldDef Field => field ?? (field = new FieldDef());
        public RiverDef River => river ?? (river = new RiverDef());
        public ChallengeDef Challenge => challenge != null && challenge.tiers != null && challenge.tiers.Length > 0 ? challenge : (challenge = new ChallengeDef());
        public AltarDef Altar => altar ?? (altar = new AltarDef());
        public TransformsDef Transforms => transforms ?? (transforms = new TransformsDef());
        public BenchDef Bench => bench ?? (bench = new BenchDef());
        public StructureTapsDef StructureTaps => structureTaps ?? (structureTaps = new StructureTapsDef());
        public HelperDef Helpers => helpers ?? (helpers = new HelperDef());
        public StructureDef Structure(string id) { if (structures != null) foreach (var s in structures) if (s.id == id) return s; return null; }
        public SupportDef Support => support ?? (support = new SupportDef());
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
    /// <summary>Wann ein Baum gefaellt wurde (Nacht-Zaehler), fuers Nachwachsen.</summary>
    [Serializable] public class TreeFell { public int id, night; }

    [Serializable] public class WallState { public int x, y; public float hp; public bool stone; public int thorns; public bool gate; public int tier, moss; }   // tier: 0 Stein, 1 Granit, 2 Kristall; moss: Steinmoos zur naechsten Stufe
    /// <summary>Ein Steinpult (Spielstand): auf dem Boden oder auf einer Mauer.</summary>
    [Serializable] public class PedestalState { public int x, y; public int heart; }

    [Serializable] public class BuildingCount { public string id; public int n; }
    /// <summary>Welchem Feld ein Knolli zugeordnet ist (eine Zelle des Beets; set = false: noch keinem).</summary>
    [Serializable] public class KnolliJob { public bool set; public int x, y; public string role; }   // role: carrier, water, wood, mason, guard
    /// <summary>Stufe einer Waffe (an der Waffenbank geschmiedet) und Schlaege zur naechsten Stufe.</summary>
    /// <summary>Ein aufgestelltes Bauwerk (Spielstand). placed = false: gekauft, wartet auf seinen Platz.</summary>
    [Serializable] public class StructureState { public string id; public int x, y, lv = 1; public float xp; public bool placed, stone, moon; public bool anchored; public int bx, by; public bool area; public int ax0, ay0, ax1, ay1; }   // Totem: aufgezogene Flaeche (area) oder altes Beet (bx/by)
    [Serializable] public class WeaponLevel
    {
        public string id; public int lv = 1; public float xp;
        public string element; public int elementLv;   // alt (ein Element); wird beim Laden in elements uebernommen
        public List<WeaponElementLv> elements = new List<WeaponElementLv>();   // alle Elemente, die die Waffe je bekommen hat
    }
    /// <summary>Ein Element an einer Waffe und seine Stufe (bleibt fuer immer).</summary>
    [Serializable] public class WeaponElementLv { public string id; public int lv; }

    /// <summary>Eine gepflanzte Pflanze auf einem Feld oder Mauerplatz (Spielstand).</summary>
    [Serializable]
    public class PlantedState
    {
        public int x, y;          // Feldzelle
        public string type;       // Katalog-Id
        public int growth;        // Tipps bisher
        public int level, w, h;   // Stufe (Klicken / Zusammenfuehren) und Flaechenbedarf in Feldern (0 = 1)
        public int xp;            // Klicks seit der letzten Stufe (reife Pflanze)
        public int dir;           // Blickrichtung von Klick-Pflanzen: 0 Nord, 1 Ost, 2 Sued, 3 West
        public int stock; public double stockValue;   // Ernte-Vorrat neben der Pflanze (Knollis holen ihn ab)
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
