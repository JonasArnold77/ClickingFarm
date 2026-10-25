using System;
using System.Collections.Generic;

namespace SeedClicker
{
    [Serializable]
    public class TileData
    {
        public string obstacle = "";   // "" = kein Hindernis
        public int obstacleHp;
        public bool tilled;
        public bool watered;
        public string crop = "";       // "" = nichts gepflanzt
        public float growth;
        public int harvests;           // fuer mehrjaehrige Pflanzen
        public int fert;               // 0 = kein Duenger, 1/2 = Duengerstufe
        public string item = "";       // platzierter Gegenstand
        public float itemTimer;        // fuer Bienenstock/Erntehelfer
        public float dmg;              // erlittener Schaden einer Abwehrpflanze

        public bool HasCrop => !string.IsNullOrEmpty(crop);
        public bool HasObstacle => !string.IsNullOrEmpty(obstacle);
        public bool HasItem => !string.IsNullOrEmpty(item);
    }

    [Serializable] public class CountEntry { public string id; public double v; }

    [Serializable]
    public class QuestState
    {
        public string type; public string target; public double amount; public double progress;
        public double rewardCoins; public double rewardXp; public bool claimed;
        public bool Done => progress >= amount;
    }

    [Serializable] public class ProjectState { public string id; public int stage; }

    [Serializable]
    public class ConstructionState
    {
        public string building = "";   // "house" oder "bin"
        public int targetLevel;
        public int clicksLeft;
        public bool Active => !string.IsNullOrEmpty(building);
    }

    /// <summary>Der komplette Spielstand. Wird als JSON gespeichert.</summary>
    [Serializable]
    public class GameState
    {
        public int version = 1;
        public double coins;
        public double wood, stone, fiber;
        public int level = 1;
        public double xp;
        public float energy;
        public int day = 1;
        public float timeOfDay;          // 0..1 vom Tagesbeginn bis Tagesende
        public int[] toolTiers = new int[5];
        public List<string> unlockedAreas = new List<string>();
        public int houseLevel;
        public int binLevel;
        public ConstructionState construction = new ConstructionState();
        public List<TileData> tiles = new List<TileData>();

        public List<CountEntry> items = new List<CountEntry>();          // Inventar platzierbarer Gegenstaende
        public List<CountEntry> harvestCounts = new List<CountEntry>();
        public List<CountEntry> freeSeeds = new List<CountEntry>();
        public List<CountEntry> upgrades = new List<CountEntry>();
        public List<CountEntry> milestones = new List<CountEntry>();     // erreichte Meilenstein-Stufe je Pflanze
        public List<CountEntry> bin = new List<CountEntry>();            // Inhalt der Versandkiste (Anzahl je Pflanze)
        public double binValue;
        public List<string> discovered = new List<string>();
        public List<QuestState> quests = new List<QuestState>();
        public List<ProjectState> projects = new List<ProjectState>();

        public string selectedSeed = "cress";
        public string starter = "";      // gewaehlter Start-Samen ("" = noch nicht gewaehlt)
        public int nightsSurvived;
        public double monstersDefeated;
        public bool battleTutorialDone;
        public int battleDay;            // an diesem Tag lief der Nachtkampf schon
        public int tutorialStep;
        public long lastSaveTicks;
        public float playTime;
        public float lastInterstitial;
        public bool sound = true;
        public bool vibration = true;
        public double totalEarned;
        public int seed;
    }

    public static class CountListExt
    {
        public static double Get(this List<CountEntry> list, string id)
        {
            foreach (var e in list) if (e.id == id) return e.v;
            return 0;
        }
        public static void Set(this List<CountEntry> list, string id, double v)
        {
            foreach (var e in list) if (e.id == id) { e.v = v; return; }
            list.Add(new CountEntry { id = id, v = v });
        }
        public static double Add(this List<CountEntry> list, string id, double delta)
        {
            foreach (var e in list) if (e.id == id) { e.v += delta; return e.v; }
            list.Add(new CountEntry { id = id, v = delta });
            return delta;
        }
    }
}
