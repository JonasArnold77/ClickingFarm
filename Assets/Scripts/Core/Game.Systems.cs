using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    public partial class Game
    {
        // ---------------------------------------------------------------- Laden

        public void SelectSeed(string cropId)
        {
            var c = Balance.Crop(cropId);
            if (c == null || !IsCropUnlocked(c)) return;
            S.selectedSeed = cropId;
            CancelPlacing();
            MarkDirty();
        }

        public void ToolUpgradeCost(int tool, out double coins, out int wood, out int stone)
        {
            coins = 0; wood = 0; stone = 0;
            int next = S.toolTiers[tool] + 1;
            if (next >= B.toolTiers.Length) return;
            var tier = B.toolTiers[next];
            float m = B.tools[tool].costMultiplier;
            coins = Math.Round(tier.coins * m);
            wood = Mathf.RoundToInt(tier.wood * m);
            stone = Mathf.RoundToInt(tier.stone * m);
        }

        public bool BuyToolUpgrade(int tool)
        {
            int next = S.toolTiers[tool] + 1;
            if (next >= B.toolTiers.Length || B.toolTiers[next].unlockLevel > S.level) return false;
            ToolUpgradeCost(tool, out var c, out var w, out var s);
            if (!Pay(c, w, s)) return false;
            S.toolTiers[tool] = next;
            var tier = B.toolTiers[next];
            ShowBigReward(Loc.T("tier." + tier.id) + "-" + Loc.T("tool." + B.tools[tool].id) + "!",
                Loc.T("pattern." + tier.pattern) + " · Kraft ×" + tier.power, "tool:" + B.tools[tool].id + ":" + tier.id);
            AddXp(XpNeeded * 0.05);
            MarkDirty();
            return true;
        }

        public bool BuyUpgrade(string id)
        {
            var u = Balance.Upgrade(id);
            int lvl = UpgradeLevel(id);
            if (u == null || lvl >= u.levels.Length) return false;
            var l = u.levels[lvl];
            if (l.unlockLevel > S.level || !Pay(l.coins)) return false;
            S.upgrades.Set(id, lvl + 1);
            ShowBigReward(Loc.T("upgrade." + id) + " " + Roman(lvl + 1), UpgradeDescription(id, l.value), "upgrade:" + id);
            MarkDirty();
            return true;
        }

        public string UpgradeDescription(string id, float value)
        {
            string v = id == "lucky_star" ? Fmt.Pct(value) : Fmt.Num(value);
            return Loc.T("upgradedesc." + id, v);
        }

        public bool BuyItem(string id)
        {
            var def = Balance.Item(id);
            if (def == null || def.unlockLevel > S.level) return false;
            if (!Pay(def.coins, def.wood, def.stone, def.fiber)) return false;
            S.items.Add(id, 1);
            Discover(id);
            Sfx.Play("coin");
            StartPlacing(id);
            MarkDirty();
            return true;
        }

        // ---------------------------------------------------------------- Farm-Ausbau

        public bool AreaRequirementMet(AreaDef a, out string reason)
        {
            reason = "";
            if (S.level < a.unlockLevel) { reason = Loc.T("ui.locked_level", a.unlockLevel); return false; }
            if (!string.IsNullOrEmpty(a.requiresProject) && !ProjectDone(a.requiresProject))
            { reason = Loc.T("ui.area_requires_project", Loc.T("project." + a.requiresProject)); return false; }
            return true;
        }

        public bool UnlockArea(AreaDef a)
        {
            if (a == null || IsAreaUnlocked(a) || !AreaRequirementMet(a, out _)) return false;
            if (!Pay(a.coins, a.wood, a.stone)) return false;
            S.unlockedAreas.Add(a.id);
            RecalcCoverage();
            AddXp(a.xpOnUnlock);
            ShowBigReward(Loc.T("ui.toast_area", Loc.T("area." + a.id)), Loc.T("areadesc." + a.id), "map");
            AllTilesChanged?.Invoke();
            MarkDirty();
            return true;
        }

        // ---------------------------------------------------------------- Gebaeude

        public BuildingLevelDef[] BuildingLevels(string building) => building == "house" ? B.houseLevels : B.binLevels;
        public int BuildingLevel(string building) => building == "house" ? S.houseLevel : S.binLevel;
        public BuildingLevelDef CurrentBin => B.binLevels[Mathf.Clamp(S.binLevel, 0, B.binLevels.Length - 1)];

        public bool StartConstruction(string building)
        {
            if (S.construction.Active) return false;
            var levels = BuildingLevels(building);
            int next = BuildingLevel(building) + 1;
            if (next >= levels.Length) return false;
            var l = levels[next];
            if (l.unlockLevel > S.level) return false;
            if (!Pay(l.coins, l.wood, l.stone, l.fiber)) return false;
            S.construction = new ConstructionState { building = building, targetLevel = next, clicksLeft = Mathf.Max(1, l.buildClicks) };
            Sfx.Play("build");
            MarkDirty();
            return true;
        }

        /// <summary>Ein Hammerschlag auf die Baustelle.</summary>
        public void HammerConstruction()
        {
            var c = S.construction;
            if (!c.Active) return;
            c.clicksLeft--;
            Raise(FxType.Build, -1, -1, null, 1f, c.building);
            if (c.clicksLeft <= 0)
            {
                var levels = BuildingLevels(c.building);
                var l = levels[c.targetLevel];
                if (c.building == "house") { S.houseLevel = c.targetLevel; S.energy = MaxEnergy; }
                else S.binLevel = c.targetLevel;
                string name = Loc.T((c.building == "house" ? "house." : "bin.") + l.id);
                string sub = c.building == "house" ? Loc.T("ui.house_energy", Fmt.Num(l.maxEnergy)) : Loc.T("ui.bin_bonus", Fmt.Pct(l.payoutBonus));
                S.construction = new ConstructionState();
                AddXp(XpNeeded * 0.1);
                ShowBigReward(Loc.T("ui.build_done", name), sub, c.building);
            }
            MarkDirty();
        }

        public void TapBuilding(string building)
        {
            if (Sleeping) return;
            if (S.construction.Active && S.construction.building == building) { HammerConstruction(); return; }
            BuildingTapped?.Invoke(building);
        }

        // ---------------------------------------------------------------- Versandkiste

        public void SellBinNow()
        {
            if (S.binValue <= 0) return;
            double v = Math.Round(S.binValue * CurrentBin.instantRate);
            S.bin.Clear();
            S.binValue = 0;
            AddCoins(v);
            Raise(FxType.Coins, -1, -1, "+" + Fmt.Num(v), 1f);
            if (S.tutorialStep == 5) AdvanceTutorial(6);
            MarkDirty();
        }

        // ---------------------------------------------------------------- Grossprojekte

        public ProjectState ProjectStateOf(string id)
        {
            foreach (var p in S.projects) if (p.id == id) return p;
            var ps = new ProjectState { id = id };
            S.projects.Add(ps);
            return ps;
        }

        public bool ProjectDone(string id)
        {
            var def = Balance.Project(id);
            return def != null && ProjectStateOf(id).stage >= def.stages.Length;
        }

        public bool DeliverProject(string id)
        {
            var def = Balance.Project(id);
            var st = ProjectStateOf(id);
            if (def == null || def.unlockLevel > S.level || st.stage >= def.stages.Length) return false;
            var stage = def.stages[st.stage];
            if (!Pay(stage.coins, stage.wood, stage.stone, stage.fiber)) return false;
            st.stage++;
            Raise(FxType.Build, -1, -1);
            AddXp(XpNeeded * 0.1);
            if (st.stage >= def.stages.Length)
                ShowBigReward(Loc.T("project." + id), Loc.T("projectdesc." + id), "hammer");
            else ShowToast(Loc.T("ui.stage", st.stage, def.stages.Length), "hammer");
            MarkDirty();
            return true;
        }

        // ---------------------------------------------------------------- Auftraege

        public void EnsureQuests()
        {
            S.quests.RemoveAll(q => q.claimed);
            int guard = 0;
            while (S.quests.Count < B.smallQuestCount && guard++ < 20) S.quests.Add(GenerateQuest());
            MarkDirty();
        }

        QuestState GenerateQuest()
        {
            var r = rng ?? new System.Random();
            for (int tries = 0; tries < 10; tries++)
            {
                var tpl = B.questTemplates[r.Next(B.questTemplates.Length)];
                string target = tpl.target;
                if (tpl.type == "harvest" && target == "best")
                {
                    var best = BestPlantableCrop(); if (best == null) continue;
                    target = best.id;
                }
                if (tpl.type == "clear" && Balance.Obstacle(target) == null) continue;
                // keine doppelten Auftraege
                if (S.quests.Exists(q => q.type == tpl.type && q.target == target)) continue;
                double amount = Math.Max(1, Math.Round(tpl.baseAmount + tpl.amountPerLevel * (S.level - 1)));
                return new QuestState
                {
                    type = tpl.type, target = target, amount = amount, progress = 0,
                    rewardCoins = Math.Round(tpl.coinFactor * B.questCoinBase * LevelScale * 3),
                    rewardXp = Math.Max(1, Math.Round(tpl.xpFactor * XpNeeded))
                };
            }
            return new QuestState { type = "grow", target = "", amount = 30, rewardCoins = 20, rewardXp = 5 };
        }

        void QuestProgress(string type, string target, double n)
        {
            if (n <= 0) return;
            bool changed = false;
            foreach (var q in S.quests)
            {
                if (q.type != type || q.Done) continue;
                if (type == "harvest" && q.target != "*" && q.target != target) continue;
                if (type == "clear" && q.target != target) continue;
                q.progress = Math.Min(q.amount, q.progress + n);
                if (q.Done) ShowToast(Loc.T("ui.done") + " " + QuestText(q), "scroll");
                changed = true;
            }
            if (changed) MarkDirty();
        }

        public string QuestText(QuestState q)
        {
            string amt = Fmt.Num(q.amount);
            switch (q.type)
            {
                case "harvest": return q.target == "*" ? Loc.T("ui.quest.harvest_any", amt) : Loc.T("ui.quest.harvest", amt, Loc.T("crop." + q.target));
                case "clear": return Loc.T("ui.quest.clear", amt, Loc.T("obstacle." + q.target));
                default: return Loc.T("ui.quest." + q.type, amt);
            }
        }

        public void ClaimQuest(QuestState q)
        {
            if (!q.Done || q.claimed) return;
            q.claimed = true;
            AddCoins(q.rewardCoins);
            AddXp(q.rewardXp);
            Raise(FxType.Coins, -1, -1, "+" + Fmt.Num(q.rewardCoins));
            EnsureQuests();
        }

        // ---------------------------------------------------------------- Tag, Nacht, Schlaf

        public void BeginSleep(bool exhausted)
        {
            if (Sleeping) return;
            if (BattleActive) EndBattle(false);
            Sleeping = true;
            CancelPlacing();
            SleepStarted?.Invoke(exhausted);
        }

        /// <summary>Wird von der UI aufgerufen, wenn der Bildschirm dunkel ist.</summary>
        public NightSummary CompleteNight(bool exhausted)
        {
            var sum = new NightSummary { exhausted = exhausted };
            // Auszahlung der Versandkiste
            sum.payout = Math.Round(S.binValue * (1 + CurrentBin.payoutBonus));
            if (sum.payout > 0) AddCoins(sum.payout);
            S.bin.Clear(); S.binValue = 0;
            if (S.tutorialStep == 5) AdvanceTutorial(6);

            float night = B.houseLevels[Mathf.Clamp(S.houseLevel, 0, B.houseLevels.Length - 1)].nightGrowth;
            var r = new System.Random(S.seed + S.day * 31);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = Tile(x, y);
                    var c = Balance.Crop(t.crop);
                    if (c != null && !IsRipe(t) && IsWatered(x, y))
                    {
                        t.growth = Mathf.Min(c.growth, t.growth + c.growth * night);
                        sum.grown++;
                    }
                    t.watered = false;
                    t.dmg = 0;
                    // Neues Unkraut auf freien, nicht umgegrabenen Feldern (Materialnachschub)
                    if (IsUsable(x, y) && !t.tilled && !t.HasCrop && !t.HasItem && !t.HasObstacle && r.NextDouble() < B.nightWeedChance)
                    {
                        double roll = r.NextDouble();
                        SetObstacle(t, roll < 0.5 ? "grass" : roll < 0.8 ? "twig" : "stone");
                        sum.weeds++;
                    }
                }

            S.day++;
            S.timeOfDay = 0;
            S.energy = MaxEnergy;
            sum.newDay = S.day;
            rng = new System.Random(S.seed + S.day * 7919);
            // Tagesauftraege: erledigte werden ersetzt
            EnsureQuests();
            AllTilesChanged?.Invoke();
            Save();
            return sum;
        }

        public void EndSleep()
        {
            Sleeping = false;
            AdService.TryInterstitial();
            MarkDirty();
        }

        // ---------------------------------------------------------------- Offline-Fortschritt

        void SimulateOffline(double seconds)
        {
            double capped = Math.Min(seconds, B.offlineCapHours * 3600.0);
            var sum = new OfflineSummary { seconds = seconds };
            float step = Mathf.Max(1f, B.offlineStepSeconds);
            int steps = (int)(capped / step);
            for (int i = 0; i < steps; i++) SimulateStep(step, sum);
            bool anything = sum.grown > 0 || sum.harvested > 0 || sum.honey > 0;
            if (!anything) return;
            LastOffline = sum;
            OfflineReturned?.Invoke(sum);
        }

        // ---------------------------------------------------------------- Einfuehrung

        public void AdvanceTutorial(int to)
        {
            if (to <= S.tutorialStep) return;
            S.tutorialStep = to;
            MarkDirty();
        }

        public bool TutorialActive => S.tutorialStep < 7;

        /// <summary>Start-Samen waehlen (wie bei Pokemon). Startet die Einfuehrung.</summary>
        public void ChooseStarter(string cropId)
        {
            var c = Balance.Crop(cropId);
            if (c == null || !c.starter || !string.IsNullOrEmpty(S.starter)) return;
            S.starter = cropId;
            S.selectedSeed = cropId;
            S.freeSeeds.Add(cropId, B.starterFreeSeeds);
            S.freeSeeds.Add("cress", 6);
            Discover(cropId);
            S.tutorialStep = 0;
            MarkDirty();
            Save();
        }

        /// <summary>Feld, auf das die Einfuehrung gerade zeigt (oder -1).</summary>
        public Vector2Int TutorialTile()
        {
            var a = Balance.Area("start");
            Vector2Int best = new Vector2Int(-1, -1);
            foreach (var rr in a.rects)
                for (int y = rr.y + rr.h - 1; y >= rr.y; y--)
                    for (int x = rr.x; x < rr.x + rr.w; x++)
                    {
                        var t = Tile(x, y);
                        var act = Resolve(x, y);
                        var cd = Balance.Crop(t.crop);
                        bool def = cd != null && cd.IsDefender;
                        bool match = S.tutorialStep switch
                        {
                            0 => act == TileAction.Sow,
                            1 => act == TileAction.Water && def,
                            2 => act == TileAction.Grow && def,
                            3 => act == TileAction.Sow,
                            4 => (act == TileAction.Grow || act == TileAction.Water || act == TileAction.Harvest) && !def,
                            _ => false
                        };
                        if (match) return new Vector2Int(x, y);
                    }
            return best;
        }
    }
}
