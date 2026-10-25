using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    public enum FxType { Hit, Break, Till, Sow, Water, Grow, Crit, Ripe, Harvest, Place, Fertilize, Build, Error, Coins, Honey, MonsterSpawn, MonsterHit, MonsterDie, Push, Splash, Eaten, Wilt, Steal }

    public struct Feedback
    {
        public FxType type; public int x, y; public string text; public Color color; public float strength;
        public string id;
        public Vector2 world; public bool hasWorld;   // fuer Ereignisse ausserhalb des Rasters (Monster)
    }

    public struct Unlock
    {
        public string kind; public string id; public string name;
        public Unlock(string kind, string id, string name) { this.kind = kind; this.id = id; this.name = name; }
    }

    public class NightSummary
    {
        public double payout; public int grown; public int weeds; public int newDay; public bool exhausted;
    }

    public class OfflineSummary
    {
        public double seconds; public int grown; public int harvested; public double value; public double honey;
    }

    /// <summary>
    /// Zentrale Spiellogik. Haelt den Spielstand und stellt alle Aktionen bereit.
    /// Darstellung (FarmView) und Oberflaeche (UI) hoeren nur auf die Events.
    /// </summary>
    public partial class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public GameState S { get; private set; }
        public GameBalance B => Balance.B;
        public int W => B.gridWidth;
        public int H => B.gridHeight;

        // --- Events fuer Darstellung und UI ---
        public event Action<int, int> TileChanged;
        public event Action AllTilesChanged;
        public event Action<Feedback> FeedbackRaised;
        public event Action<int, List<Unlock>> LeveledUp;
        public event Action<string, string> Toast;                 // Text, Icon-Schluessel
        public event Action<int, int, string, int> Harvested;      // x, y, Pflanze, Anzahl
        public event Action<string, string, string> BigReward;     // Titel, Untertitel, Icon-Schluessel
        public event Action<AreaDef> AreaTapped;
        public event Action<int, int> ItemTapped;
        public event Action<bool> SleepStarted;                    // erschoepft?
        public event Action<OfflineSummary> OfflineReturned;
        public event Action StateChanged;
        public event Action<string> BuildingTapped;
        public event Action<int, int> DefenderTapped;
        public event Action StarterNeeded;

        public bool Sleeping { get; private set; }
        public int ModalCount { get; set; }                        // offene Fenster pausieren die Uhr
        public string PlacingItem { get; private set; }
        public float ComboMult { get; private set; } = 1f;
        public int ComboTaps { get; private set; }
        public OfflineSummary LastOffline { get; private set; }

        float lastGrowTap = -10f;
        float autosaveTimer;
        float stateChangedTimer;
        bool stateDirty;
        bool[] sprinkled;
        bool[] pondWater;
        float[] scarecrow;
        System.Random rng;

        void Awake()
        {
            I = this;
            Balance.Load();
            Loc.Load("de");
            S = SaveSystem.Load();
            bool fresh = S == null || S.tiles == null || S.tiles.Count != W * H;
            if (fresh) NewGame();
            rng = new System.Random(S.seed + S.day * 7919);
            RecalcCoverage();
        }

        void Start()
        {
            if (S.lastSaveTicks > 0)
            {
                double away = (DateTime.UtcNow - new DateTime(S.lastSaveTicks, DateTimeKind.Utc)).TotalSeconds;
                if (away > 60) SimulateOffline(away);
            }
            EnsureQuests();
            MarkDirty();
            if (string.IsNullOrEmpty(S.starter)) StarterNeeded?.Invoke();
        }

        public void NewGame()
        {
            S = new GameState();
            S.seed = UnityEngine.Random.Range(1, int.MaxValue);
            S.coins = B.startCoins;
            S.energy = MaxEnergy;
            S.toolTiers = new int[B.tools.Length];
            foreach (var a in B.areas) if (a.startUnlocked) S.unlockedAreas.Add(a.id);
            foreach (var p in B.projects) S.projects.Add(new ProjectState { id = p.id, stage = 0 });
            GenerateTiles();
            sprinkled = null;
        }

        void GenerateTiles()
        {
            S.tiles = new List<TileData>(W * H);
            var r = new System.Random(S.seed);
            var startRng = new System.Random(4242); // Startbeet immer gleich, fuer eine verlaessliche Einfuehrung
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = new TileData();
                    var area = Balance.AreaAt(x, y);
                    if (area != null && !area.water)
                    {
                        var rr = area.id == "start" ? startRng : r;
                        double roll = rr.NextDouble();
                        string ob = "";
                        float acc = area.wildBoulder; if (roll < acc) ob = "boulder";
                        else if (roll < (acc += area.wildStump)) ob = "stump";
                        else if (roll < (acc += area.wildStone)) ob = "stone";
                        else if (roll < (acc += area.wildTwig)) ob = "twig";
                        else if (roll < (acc += area.wildGrass)) ob = "grass";
                        SetObstacle(t, ob);
                    }
                    S.tiles.Add(t);
                }
            // Startbeet: ein fertig umgegrabenes, leeres 3x3-Feld
            var start = Balance.Area("start");
            if (start != null)
            {
                var r0 = start.rects[0];
                for (int y = r0.y; y < r0.y + 3; y++) for (int x = r0.x; x < r0.x + 3; x++) { var t = Tile(x, y); SetObstacle(t, ""); t.tilled = true; }
            }
        }

        static void SetObstacle(TileData t, string id)
        {
            t.obstacle = id ?? "";
            var def = Balance.Obstacle(id);
            t.obstacleHp = def != null ? def.hp : 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            S.playTime += dt;

            if (Time.time - lastGrowTap > B.comboWindow && ComboTaps > 0)
            {
                ComboTaps = 0; ComboMult = 1f; MarkDirty();
            }

            if (!Sleeping)
            {
                TickFarm(dt);
                TickBattle(dt);
                CheckBattleStart();
                if (ModalCount <= 0 && !BattleActive && !string.IsNullOrEmpty(S.starter))
                {
                    S.timeOfDay += dt / Mathf.Max(10f, B.dayLengthSeconds);
                    if (S.timeOfDay >= 1f) { S.timeOfDay = 1f; BeginSleep(false); }
                }
            }

            autosaveTimer += dt;
            if (autosaveTimer >= B.autosaveSeconds) { autosaveTimer = 0; Save(); }

            stateChangedTimer -= dt;
            if (stateDirty && stateChangedTimer <= 0f)
            {
                stateDirty = false; stateChangedTimer = 0.05f;
                StateChanged?.Invoke();
            }
        }

        void OnApplicationPause(bool pause) { if (pause) Save(); }
        void OnApplicationQuit() { Save(); }

        public void Save() { if (S != null) SaveSystem.Save(S); }

        public void ResetGame()
        {
            SaveSystem.Delete();
            NewGame();
            RecalcCoverage();
            EnsureQuests();
            AllTilesChanged?.Invoke();
            MarkDirty();
            Save();
            StarterNeeded?.Invoke();
        }

        public void MarkDirty() => stateDirty = true;

        // ---------------------------------------------------------------- Ressourcen

        public float MaxEnergy => B.houseLevels[Mathf.Clamp(S != null ? S.houseLevel : 0, 0, B.houseLevels.Length - 1)].maxEnergy;
        public float Hour => Mathf.Lerp(B.startHour, B.endHour, S.timeOfDay);

        public bool CanAfford(double coins, double wood = 0, double stone = 0, double fiber = 0)
            => S.coins >= coins && S.wood >= wood && S.stone >= stone && S.fiber >= fiber;

        public bool Pay(double coins, double wood = 0, double stone = 0, double fiber = 0)
        {
            if (!CanAfford(coins, wood, stone, fiber)) { Sfx.Play("error"); Toast?.Invoke(Loc.T("ui.not_enough"), "error"); return false; }
            S.coins -= coins; S.wood -= wood; S.stone -= stone; S.fiber -= fiber;
            MarkDirty();
            return true;
        }

        public void AddCoins(double v)
        {
            S.coins += v;
            if (v > 0) S.totalEarned += v;
            MarkDirty();
        }

        public void AddMaterial(string id, double v)
        {
            if (id == "wood") S.wood += v; else if (id == "stone") S.stone += v; else if (id == "fiber") S.fiber += v;
            MarkDirty();
        }

        public bool UseEnergy(float amount)
        {
            if (S.energy + 0.0001f < amount)
            {
                Raise(FxType.Error, -1, -1, Loc.T("ui.energy_empty"));
                BeginSleep(true);
                return false;
            }
            S.energy = Mathf.Max(0, S.energy - amount);
            MarkDirty();
            if (S.energy <= 0.0001f) Invoke(nameof(ExhaustedSleep), 0.9f);
            return true;
        }

        void ExhaustedSleep() { if (S.energy <= 0.0001f) BeginSleep(true); }

        // ---------------------------------------------------------------- XP und Stufen

        public double XpNeeded => Balance.XpForLevel(S.level);

        public void AddXp(double v)
        {
            if (v <= 0) return;
            S.xp += v;
            while (S.level < B.maxLevel && S.xp >= XpNeeded)
            {
                S.xp -= XpNeeded;
                S.level++;
                var unlocks = UnlocksAt(S.level);
                double bonus = Math.Round(B.levelUpCoinBase * Math.Pow(B.levelUpCoinGrowth, S.level));
                if (unlocks.Count == 0)
                {
                    // Keine Stufe ohne Belohnung: Bonuspaket
                    var best = BestPlantableCrop();
                    bonus *= 4;
                    if (best != null) S.freeSeeds.Add(best.id, B.freeSeedPackSize);
                    S.wood += 20 * S.level; S.stone += 20 * S.level;
                    unlocks.Add(new Unlock("bonus", best != null ? best.id : "", Loc.T("ui.bonus_pack")));
                }
                AddCoins(bonus);
                S.energy = Mathf.Max(S.energy, MaxEnergy);
                EnsureQuests();
                LeveledUp?.Invoke(S.level, unlocks);
            }
            MarkDirty();
        }

        public List<Unlock> UnlocksAt(int level)
        {
            var list = new List<Unlock>();
            foreach (var c in B.crops)
            {
                if (c.starter) { if (level == B.otherStartersLevel && c.id != S.starter) list.Add(new Unlock("crop", c.id, Loc.T("crop." + c.id))); }
                else if (c.unlockLevel == level) list.Add(new Unlock("crop", c.id, Loc.T("crop." + c.id)));
            }
            for (int i = 1; i < B.toolTiers.Length; i++)
                if (B.toolTiers[i].unlockLevel == level) list.Add(new Unlock("tier", B.toolTiers[i].id, Loc.T("tier." + B.toolTiers[i].id) + "-" + Loc.T("ui.tools")));
            foreach (var it in B.items) if (it.unlockLevel == level) list.Add(new Unlock("item", it.id, Loc.T("item." + it.id)));
            foreach (var a in B.areas) if (!a.startUnlocked && a.unlockLevel == level) list.Add(new Unlock("area", a.id, Loc.T("area." + a.id)));
            for (int i = 1; i < B.houseLevels.Length; i++) if (B.houseLevels[i].unlockLevel == level) list.Add(new Unlock("house", B.houseLevels[i].id, Loc.T("house." + B.houseLevels[i].id)));
            for (int i = 1; i < B.binLevels.Length; i++) if (B.binLevels[i].unlockLevel == level) list.Add(new Unlock("bin", B.binLevels[i].id, Loc.T("bin." + B.binLevels[i].id)));
            foreach (var u in B.upgrades)
                for (int i = 0; i < u.levels.Length; i++)
                    if (u.levels[i].unlockLevel == level) list.Add(new Unlock("upgrade", u.id, Loc.T("upgrade." + u.id) + " " + Roman(i + 1)));
            foreach (var p in B.projects) if (p.unlockLevel == level) list.Add(new Unlock("project", p.id, Loc.T("project." + p.id)));
            return list;
        }

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };

        /// <summary>Naechstes Freischaltziel: (Stufe, Name, Icon-Schluessel, fehlende XP).</summary>
        public bool NextGoal(out int level, out string name, out string icon, out double xpLeft)
        {
            level = 0; name = ""; icon = ""; xpLeft = 0;
            if (S.level >= B.maxLevel) return false;
            double acc = XpNeeded - S.xp;
            for (int l = S.level + 1; l <= B.maxLevel; l++)
            {
                var u = UnlocksAt(l);
                if (u.Count > 0)
                {
                    var pick = u.Find(x => x.kind == "crop");
                    if (string.IsNullOrEmpty(pick.kind)) pick = u[0];
                    level = l; name = pick.name; icon = UnlockIcon(pick); xpLeft = acc;
                    return true;
                }
                acc += Balance.XpForLevel(l);
            }
            level = S.level + 1; name = Loc.T("ui.bonus_pack"); icon = "star"; xpLeft = XpNeeded - S.xp;
            return true;
        }

        public static string UnlockIcon(Unlock u) => u.kind switch
        {
            "crop" => "crop:" + u.id,
            "item" => "item:" + u.id,
            "tier" => "tool:hoe:" + u.id,
            "house" => "house",
            "bin" => "bin",
            "area" => "map",
            "upgrade" => "upgrade:" + u.id,
            "project" => "hammer",
            _ => "gift"
        };

        // ---------------------------------------------------------------- Hilfen

        public TileData Tile(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? null : S.tiles[y * W + x];
        public bool IsAreaUnlocked(AreaDef a) => a != null && S.unlockedAreas.Contains(a.id);
        public bool IsUsable(int x, int y) { var a = Balance.AreaAt(x, y); return a != null && !a.water && IsAreaUnlocked(a); }

        public bool IsWatered(int x, int y)
        {
            var t = Tile(x, y); if (t == null) return false;
            if (t.watered) return true;
            int i = y * W + x;
            if (sprinkled != null && (sprinkled[i] || pondWater[i])) return true;
            var a = Balance.AreaAt(x, y);
            return a != null && a.autoWater;
        }

        public bool IsRipe(TileData t)
        {
            var c = Balance.Crop(t.crop);
            return c != null && t.growth >= c.growth;
        }

        /// <summary>0 Samen, 1 Keimling, 2 junge Pflanze, 3 erntereif.</summary>
        public int Stage(TileData t)
        {
            var c = Balance.Crop(t.crop);
            if (c == null) return -1;
            float f = t.growth / c.growth;
            if (f >= 1f) return 3;
            if (c.perennial && t.harvests > 0) return 2;
            if (f >= 0.5f) return 2;
            if (f >= 0.15f) return 1;
            return 0;
        }

        public float UpgradeValue(string id)
        {
            var u = Balance.Upgrade(id); if (u == null) return 0;
            int lvl = (int)S.upgrades.Get(id);
            return lvl <= 0 ? u.baseValue : u.levels[Mathf.Min(lvl, u.levels.Length) - 1].value;
        }

        public int UpgradeLevel(string id) => (int)S.upgrades.Get(id);

        public ToolTierDef ToolTier(int toolIndex) => B.toolTiers[Mathf.Clamp(S.toolTiers[toolIndex], 0, B.toolTiers.Length - 1)];

        public CropDef BestPlantableCrop()
        {
            CropDef best = null;
            foreach (var c in B.crops)
            {
                if (c.IsDefender || c.unlockLevel > S.level) continue;
                if (!string.IsNullOrEmpty(c.requiresArea) && !S.unlockedAreas.Contains(c.requiresArea)) continue;
                if (best == null || c.unlockLevel > best.unlockLevel) best = c;
            }
            return best;
        }

        public double SellValue(CropDef c)
        {
            int ms = (int)S.milestones.Get(c.id);
            double mult = ms > 0 ? 1 + B.milestoneBonus[Mathf.Min(ms, B.milestoneBonus.Length) - 1] : 1;
            return c.sellValue * mult;
        }

        public double LevelScale => Math.Pow(B.questCoinGrowth, S.level - 1);

        void Raise(FxType type, int x, int y, string text = null, float strength = 1f, string id = null, Color color = default)
            => FeedbackRaised?.Invoke(new Feedback { type = type, x = x, y = y, text = text, strength = strength, id = id, color = color });

        void Changed(int x, int y) => TileChanged?.Invoke(x, y);

        public void ShowToast(string text, string icon) => Toast?.Invoke(text, icon);
        public void ShowBigReward(string title, string sub, string icon) => BigReward?.Invoke(title, sub, icon);

        public void Discover(string id)
        {
            if (!S.discovered.Contains(id)) S.discovered.Add(id);
        }
    }
}
