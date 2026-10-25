using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    // Alle Balancing-Werte kommen aus Resources/Balance/balance.json.
    // Neue Pflanzen, Gegenstaende usw. werden nur dort eingetragen (plus Name in Lang/de.txt).

    [Serializable] public class ObstacleDef { public string id; public int hp; public string tool; public string drop; public int dropAmount; public float xp; public int minToolTier; }
    [Serializable] public class ToolDef { public string id; public float costMultiplier; }
    [Serializable] public class ToolTierDef { public string id; public int power; public int pattern; public float energy; public double coins; public int wood; public int stone; public int unlockLevel; public string color; }

    [Serializable]
    public class CropDef
    {
        public string id; public int unlockLevel; public double seedCost; public float growth; public double sellValue;
        public float xp; public int yield; public bool perennial; public float regrowFraction; public string requiresArea;
        public string shape; public string leafColor; public string fruitColor;
        public string category; public bool starter; public DefenseDef defense;
        public bool IsDefender => category == "defender" && defense != null && defense.damage > 0;
    }

    [Serializable]
    public class DefenseDef
    {
        public string element; public float damage, cooldown, range, area, slow, slowTime, burn, burnTime, hp;
        public string projectile; public bool pulse;
    }

    [Serializable]
    public class MonsterDef
    {
        public string id; public string element; public float hp, speed, damage, reward, xp; public int firstNight; public float weight; public string color;
    }

    [Serializable]
    public class NightDef
    {
        public float startHour, maxSeconds, spawnInterval, waveBreak;
        public float wavesBase, wavesPerNight; public int maxWaves;
        public float monstersBase, monstersPerNight, hpGrowthPerNight, rewardGrowthPerNight;
        public float tapDamage; public int pushMaxStacks; public float pushSpeedPerStack, pushDamageMult, pushMinInterval;
        public float elementStrong, elementWeak, defenderLevelScale;
        public float cropBite, houseSteal, victoryBonus, wiltRegrow;
    }

    [Serializable] public class RectDef { public int x, y, w, h; }

    [Serializable]
    public class AreaDef
    {
        public string id; public RectDef[] rects; public bool startUnlocked; public int unlockLevel; public double coins; public int wood; public int stone;
        public string requiresProject; public float growthMult; public bool autoWater; public bool water; public string groundTint;
        public float wildGrass, wildTwig, wildStone, wildStump, wildBoulder; public float xpOnUnlock;
        [NonSerialized] public int index;
        [NonSerialized] public Vector2 center;
    }

    [Serializable]
    public class BuildingLevelDef
    {
        public string id; public int unlockLevel; public double coins; public int wood, stone, fiber; public int buildClicks;
        public float maxEnergy; public float nightGrowth; public float payoutBonus; public float instantRate;
    }

    [Serializable]
    public class ItemDef
    {
        public string id; public string kind; public int unlockLevel; public double coins; public int wood, stone, fiber;
        public int range; public float power; public float interval; public string color;
    }

    [Serializable] public class UpgradeLevelDef { public double coins; public int unlockLevel; public float value; }
    [Serializable] public class UpgradeDef { public string id; public float baseValue; public UpgradeLevelDef[] levels; }
    [Serializable] public class ProjectStageDef { public int wood, stone, fiber; public double coins; }
    [Serializable] public class ProjectDef { public string id; public int unlockLevel; public string unlocksArea; public ProjectStageDef[] stages; }
    [Serializable] public class QuestTemplateDef { public string type; public string target; public float baseAmount; public float amountPerLevel; public float coinFactor; public float xpFactor; }

    [Serializable]
    public class GameBalance
    {
        public int gridWidth, gridHeight;
        public double startCoins;
        public float xpBase, xpExponent; public int maxLevel; public float levelUpCoinBase, levelUpCoinGrowth;
        public float dayLengthSeconds, startHour, endHour, nightWeedChance;
        public float unwateredGrowthFactor, passiveGrowthPerSecond, critMultiplier, comboWindow, comboStep; public int comboTapsPerStep;
        public float tillXp, waterXp;
        public float offlineCapHours, offlineStepSeconds, autosaveSeconds;
        public int[] milestones; public float[] milestoneBonus;
        public int smallQuestCount; public float questCoinBase, questCoinGrowth;
        public float interstitialMinPlaySeconds, interstitialCooldownSeconds;
        public int freeSeedPackSize; public float adGrowAllFraction;
        public int otherStartersLevel; public int starterFreeSeeds;
        public NightDef night;
        public MonsterDef[] monsters;

        public ObstacleDef[] obstacles;
        public ToolDef[] tools;
        public ToolTierDef[] toolTiers;
        public CropDef[] crops;
        public AreaDef[] areas;
        public BuildingLevelDef[] houseLevels;
        public BuildingLevelDef[] binLevels;
        public ItemDef[] items;
        public UpgradeDef[] upgrades;
        public ProjectDef[] projects;
        public QuestTemplateDef[] questTemplates;
    }

    /// <summary>Zentrale, nur lesende Sicht auf das Balancing inkl. Nachschlage-Tabellen.</summary>
    public static class Balance
    {
        public static GameBalance B { get; private set; }
        static readonly Dictionary<string, CropDef> crops = new Dictionary<string, CropDef>();
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        static readonly Dictionary<string, AreaDef> areas = new Dictionary<string, AreaDef>();
        static readonly Dictionary<string, ObstacleDef> obstacles = new Dictionary<string, ObstacleDef>();
        static readonly Dictionary<string, UpgradeDef> upgrades = new Dictionary<string, UpgradeDef>();
        static readonly Dictionary<string, ProjectDef> projects = new Dictionary<string, ProjectDef>();
        static int[] tileArea;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Balance/balance");
            if (ta == null) { Debug.LogError("balance.json fehlt in Resources/Balance"); B = new GameBalance(); return; }
            B = JsonUtility.FromJson<GameBalance>(ta.text);
            crops.Clear(); items.Clear(); areas.Clear(); obstacles.Clear(); upgrades.Clear(); projects.Clear();
            foreach (var c in B.crops) crops[c.id] = c;
            foreach (var i in B.items) items[i.id] = i;
            foreach (var o in B.obstacles) obstacles[o.id] = o;
            foreach (var u in B.upgrades) upgrades[u.id] = u;
            foreach (var p in B.projects) projects[p.id] = p;

            tileArea = new int[B.gridWidth * B.gridHeight];
            for (int i = 0; i < tileArea.Length; i++) tileArea[i] = -1;
            for (int a = 0; a < B.areas.Length; a++)
            {
                var ar = B.areas[a];
                ar.index = a; areas[ar.id] = ar;
                Vector2 sum = Vector2.zero; int n = 0;
                foreach (var r in ar.rects)
                    for (int x = r.x; x < r.x + r.w; x++)
                        for (int y = r.y; y < r.y + r.h; y++)
                        {
                            if (x < 0 || y < 0 || x >= B.gridWidth || y >= B.gridHeight) continue;
                            int idx = y * B.gridWidth + x;
                            if (tileArea[idx] < 0) { tileArea[idx] = a; sum += new Vector2(x, y); n++; }
                        }
                ar.center = n > 0 ? sum / n : Vector2.zero;
            }
        }

        public static CropDef Crop(string id) => id != null && crops.TryGetValue(id, out var c) ? c : null;
        public static ItemDef Item(string id) => id != null && items.TryGetValue(id, out var c) ? c : null;
        public static AreaDef Area(string id) => id != null && areas.TryGetValue(id, out var c) ? c : null;
        public static ObstacleDef Obstacle(string id) => id != null && obstacles.TryGetValue(id, out var c) ? c : null;
        public static UpgradeDef Upgrade(string id) => id != null && upgrades.TryGetValue(id, out var c) ? c : null;
        public static ProjectDef Project(string id) => id != null && projects.TryGetValue(id, out var c) ? c : null;
        public static AreaDef AreaAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= B.gridWidth || y >= B.gridHeight) return null;
            int a = tileArea[y * B.gridWidth + x];
            return a >= 0 ? B.areas[a] : null;
        }
        public static MonsterDef Monster(string id) { foreach (var m in B.monsters) if (m.id == id) return m; return null; }

        /// <summary>Elemente wie bei Pokemon: Feuer schlaegt Pflanze, Pflanze schlaegt Wasser, Wasser schlaegt Feuer.</summary>
        public static float ElementMult(string attacker, string defender)
        {
            if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(defender) || attacker == "none" || defender == "none") return 1f;
            if ((attacker == "fire" && defender == "plant") || (attacker == "plant" && defender == "water") || (attacker == "water" && defender == "fire")) return B.night.elementStrong;
            if ((defender == "fire" && attacker == "plant") || (defender == "plant" && attacker == "water") || (defender == "water" && attacker == "fire")) return B.night.elementWeak;
            return 1f;
        }

        public static int ToolIndex(string toolId)
        {
            for (int i = 0; i < B.tools.Length; i++) if (B.tools[i].id == toolId) return i;
            return -1;
        }

        /// <summary>XP, die man auf Stufe <paramref name="level"/> fuer den naechsten Aufstieg braucht.</summary>
        public static double XpForLevel(int level) => Math.Round(B.xpBase * Math.Pow(level, B.xpExponent));

        public static Color Hex(string hex, Color fallback = default)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback == default ? Color.white : fallback;
        }
    }
}
