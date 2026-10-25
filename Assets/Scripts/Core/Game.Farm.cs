using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    public enum TileAction { None, Locked, Clear, Blocked, Till, Sow, Water, Grow, Harvest, Place, Fertilize, ItemInfo, Push, DefenderInfo }

    public partial class Game
    {
        const int Hoe = 0, Can = 1, Scythe = 4;

        // ---------------------------------------------------------------- Aktion bestimmen

        /// <summary>Welche Aktion ein Tipp auf dieses Feld ausloesen wuerde (automatische Werkzeugwahl).</summary>
        public TileAction Resolve(int x, int y)
        {
            var t = Tile(x, y);
            var area = Balance.AreaAt(x, y);
            if (t == null || area == null) return TileAction.None;
            if (!IsAreaUnlocked(area)) return TileAction.Locked;
            if (area.water) return TileAction.None;

            if (!string.IsNullOrEmpty(PlacingItem))
            {
                var def = Balance.Item(PlacingItem);
                if (def == null) return TileAction.None;
                if (def.kind == "fertilizer") return t.tilled && !t.HasItem && t.fert < def.power ? TileAction.Fertilize : TileAction.None;
                return !t.HasObstacle && !t.HasItem && !t.HasCrop ? TileAction.Place : TileAction.None;
            }

            if (t.HasObstacle)
            {
                var ob = Balance.Obstacle(t.obstacle);
                int ti = Balance.ToolIndex(ob.tool);
                return ti >= 0 && S.toolTiers[ti] < ob.minToolTier ? TileAction.Blocked : TileAction.Clear;
            }
            if (t.HasItem) return TileAction.ItemInfo;
            if (t.HasCrop)
            {
                if (IsRipe(t))
                {
                    var cd = Balance.Crop(t.crop);
                    if (cd != null && cd.IsDefender) return BattleActive ? TileAction.Push : TileAction.DefenderInfo;
                    return TileAction.Harvest;
                }
                if (!IsWatered(x, y)) return TileAction.Water;
                return TileAction.Grow;
            }
            if (t.tilled) return TileAction.Sow;
            return TileAction.Till;
        }

        /// <summary>Aktionen, die man per Wischen ueber mehrere Felder "malen" kann.</summary>
        public static bool IsPaintable(TileAction a) => a == TileAction.Grow || a == TileAction.Harvest || a == TileAction.Water || a == TileAction.Sow || a == TileAction.Push;

        // ---------------------------------------------------------------- Aktion ausfuehren

        public bool Perform(int x, int y, TileAction action)
        {
            if (Sleeping) return false;
            switch (action)
            {
                case TileAction.Locked: AreaTapped?.Invoke(Balance.AreaAt(x, y)); return true;
                case TileAction.ItemInfo: ItemTapped?.Invoke(x, y); return true;
                case TileAction.DefenderInfo: DefenderTapped?.Invoke(x, y); Raise(FxType.Push, x, y, null, 1f, Tile(x, y).crop); return true;
                case TileAction.Push: return DoPush(x, y);
                case TileAction.Clear: return DoClear(x, y);
                case TileAction.Blocked:
                    {
                        var ob = Balance.Obstacle(Tile(x, y).obstacle);
                        Raise(FxType.Error, x, y, Loc.T("ui.need_tool", Loc.T("tool." + ob.tool), Loc.T("tier." + B.toolTiers[ob.minToolTier].id)));
                        return false;
                    }
                case TileAction.Till: return DoTill(x, y);
                case TileAction.Sow: return DoSow(x, y);
                case TileAction.Water: return DoWater(x, y);
                case TileAction.Grow: return DoGrow(x, y);
                case TileAction.Harvest: return DoHarvest(x, y, true) > 0;
                case TileAction.Place: return DoPlace(x, y);
                case TileAction.Fertilize: return DoFertilize(x, y);
            }
            return false;
        }

        List<Vector2Int> Pattern(int x, int y, int pattern)
        {
            var patternBuf = new List<Vector2Int>(25);
            patternBuf.Add(new Vector2Int(x, y)); // Mitte zuerst
            int r = pattern >= 3 ? 2 : pattern >= 2 ? 1 : 0;
            if (pattern == 1) { patternBuf.Add(new Vector2Int(x - 1, y)); patternBuf.Add(new Vector2Int(x + 1, y)); }
            else
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx != 0 || dy != 0) patternBuf.Add(new Vector2Int(x + dx, y + dy));
            return patternBuf;
        }

        bool DoClear(int x, int y)
        {
            var t = Tile(x, y);
            var ob = Balance.Obstacle(t.obstacle);
            int ti = Balance.ToolIndex(ob.tool);
            var tier = ToolTier(ti);
            if (!UseEnergy(tier.energy)) return false;
            string tool = ob.tool;
            foreach (var p in Pattern(x, y, tier.pattern))
            {
                var tt = Tile(p.x, p.y);
                if (tt == null || !tt.HasObstacle || !IsUsable(p.x, p.y)) continue;
                var od = Balance.Obstacle(tt.obstacle);
                if (od.tool != tool || S.toolTiers[ti] < od.minToolTier) continue;
                tt.obstacleHp -= tier.power;
                if (tt.obstacleHp <= 0)
                {
                    string id = tt.obstacle;
                    SetObstacle(tt, "");
                    AddMaterial(od.drop, od.dropAmount);
                    AddXp(od.xp);
                    QuestProgress("clear", id, 1);
                    Raise(FxType.Break, p.x, p.y, "+" + od.dropAmount, 1f, id);
                }
                else Raise(FxType.Hit, p.x, p.y, null, 1f, tt.obstacle);
                Changed(p.x, p.y);
            }
            return true;
        }

        public int CountObstacles(string areaId)
        {
            int n = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    var a = Balance.AreaAt(x, y);
                    if (a != null && a.id == areaId && Tile(x, y).HasObstacle) n++;
                }
            return n;
        }

        bool DoTill(int x, int y)
        {
            var tier = ToolTier(Hoe);
            if (!UseEnergy(tier.energy)) return false;
            int n = 0;
            foreach (var p in Pattern(x, y, tier.pattern))
            {
                var t = Tile(p.x, p.y);
                if (t == null || !IsUsable(p.x, p.y) || t.HasObstacle || t.HasItem || t.HasCrop || t.tilled) continue;
                t.tilled = true; n++;
                Raise(FxType.Till, p.x, p.y);
                Changed(p.x, p.y);
            }
            AddXp(n * B.tillXp);
            QuestProgress("till", "", n);
            return n > 0;
        }

        int CountTilled() { int n = 0; foreach (var t in S.tiles) if (t.tilled) n++; return n; }

        public bool CanPlantHere(CropDef c, int x, int y)
        {
            if (string.IsNullOrEmpty(c.requiresArea)) return true;
            var a = Balance.AreaAt(x, y);
            return a != null && a.id == c.requiresArea;
        }

        bool DoSow(int x, int y)
        {
            var c = Balance.Crop(S.selectedSeed);
            if (c == null || !IsCropUnlocked(c)) { c = Balance.Crop("cress"); S.selectedSeed = c.id; }
            int n = 0;
            bool wrongArea = false, broke = false;
            foreach (var p in Pattern(x, y, ToolTier(Hoe).pattern))
            {
                var t = Tile(p.x, p.y);
                if (t == null || !IsUsable(p.x, p.y) || !t.tilled || t.HasCrop || t.HasItem) continue;
                if (!CanPlantHere(c, p.x, p.y)) { wrongArea = true; continue; }
                if (!PaySeed(c)) { broke = true; break; }
                t.crop = c.id; t.growth = 0; t.harvests = 0; n++;
                Raise(FxType.Sow, p.x, p.y, null, 1f, c.id);
                Changed(p.x, p.y);
            }
            if (n == 0)
            {
                if (wrongArea) Raise(FxType.Error, x, y, Loc.T("ui.wrong_area", Loc.T("area." + c.requiresArea)));
                else if (broke) { Raise(FxType.Error, x, y, Loc.T("ui.not_enough")); CheckSoftlock(); }
                return false;
            }
            Discover(c.id);
            if (c.IsDefender) AdvanceTutorial(1);
            else if (S.tutorialStep >= 3) AdvanceTutorial(4);
            return true;
        }

        bool PaySeed(CropDef c)
        {
            if (S.freeSeeds.Get(c.id) >= 1) { S.freeSeeds.Add(c.id, -1); MarkDirty(); return true; }
            if (S.coins < c.seedCost) return false;
            S.coins -= c.seedCost; MarkDirty();
            return true;
        }

        /// <summary>Verhindert, dass man ohne Geld und Samen feststeckt.</summary>
        void CheckSoftlock()
        {
            var cress = Balance.Crop("cress");
            if (S.coins >= cress.seedCost || S.binValue > 0) return;
            foreach (var t in S.tiles) if (t.HasCrop) return;
            foreach (var e in S.freeSeeds) if (e.v >= 1) return;
            S.freeSeeds.Add("cress", 8);
            ShowToast(Loc.T("ui.free_seeds", 8, Loc.T("crop.cress")), "gift");
        }

        bool DoWater(int x, int y)
        {
            var tier = ToolTier(Can);
            if (!UseEnergy(tier.energy)) return false;
            int n = 0;
            foreach (var p in Pattern(x, y, tier.pattern))
            {
                var t = Tile(p.x, p.y);
                if (t == null || !IsUsable(p.x, p.y) || !t.tilled || IsWatered(p.x, p.y)) continue;
                t.watered = true; n++;
                Raise(FxType.Water, p.x, p.y);
                Changed(p.x, p.y);
            }
            AddXp(n * B.waterXp);
            QuestProgress("water", "", n);
            if (S.tutorialStep <= 1) AdvanceTutorial(2);
            return n > 0;
        }

        public float GrowthMultiplier(int x, int y, TileData t)
        {
            float m = IsWatered(x, y) ? 1f : B.unwateredGrowthFactor;
            m *= t.fert == 1 ? 1.5f : t.fert >= 2 ? 2f : 1f;
            if (scarecrow != null) m *= scarecrow[y * W + x];
            var a = Balance.AreaAt(x, y);
            if (a != null) m *= a.growthMult;
            return m;
        }

        bool DoGrow(int x, int y)
        {
            var t = Tile(x, y);
            var c = Balance.Crop(t.crop);
            if (c == null || IsRipe(t)) return false;

            // Combo: schnelle Tipps hintereinander multiplizieren das Wachstum
            if (Time.time - lastGrowTap <= B.comboWindow) ComboTaps++; else ComboTaps = 1;
            lastGrowTap = Time.time;
            float maxCombo = UpgradeValue("combo_master");
            ComboMult = Mathf.Min(maxCombo, 1f + (ComboTaps / Mathf.Max(1, B.comboTapsPerStep)) * B.comboStep);

            float amount = UpgradeValue("green_thumb") * GrowthMultiplier(x, y, t) * ComboMult;
            bool crit = UnityEngine.Random.value < UpgradeValue("lucky_star");
            if (crit) amount *= B.critMultiplier;

            int before = Stage(t);
            t.growth = Mathf.Min(c.growth, t.growth + amount);
            int after = Stage(t);
            QuestProgress("grow", "", 1);

            if (after == 3)
            {
                Raise(FxType.Ripe, x, y, null, 1f, c.id);
                if (c.IsDefender && S.tutorialStep <= 2)
                {
                    AdvanceTutorial(3);
                    S.selectedSeed = "cress";
                    ShowBigReward(Loc.T("ui.defender_ready", Loc.T("crop." + c.id)), Loc.T("ui.defender_ready_sub"), "crop:" + c.id);
                }
            }
            else Raise(crit ? FxType.Crit : FxType.Grow, x, y, crit ? Loc.T("ui.crit") : null, after != before ? 1.6f : 1f, c.id);
            Changed(x, y);
            MarkDirty();
            return true;
        }

        /// <summary>Erntet ab (x,y) mit der Flaeche der Sichel. Gibt die Anzahl geernteter Felder zurueck.</summary>
        int DoHarvest(int x, int y, bool usePattern)
        {
            int n = 0;
            var tier = ToolTier(Scythe);
            var pts = usePattern ? Pattern(x, y, tier.pattern) : new List<Vector2Int> { new Vector2Int(x, y) };
            foreach (var p in pts)
            {
                var t = Tile(p.x, p.y);
                if (t == null || !t.HasCrop || !IsRipe(t) || Balance.Crop(t.crop).IsDefender) continue;
                HarvestTile(p.x, p.y, t, true);
                n++;
            }
            return n;
        }

        double HarvestTile(int x, int y, TileData t, bool fx)
        {
            var c = Balance.Crop(t.crop);
            int count = Mathf.Max(1, c.yield) * (t.fert >= 2 ? 2 : 1);
            double value = SellValue(c) * count;
            S.bin.Add(c.id, count);
            S.binValue += value;
            double total = S.harvestCounts.Add(c.id, count);
            Discover(c.id);
            AddXp(c.xp * count);
            QuestProgress("harvest", c.id, count);
            CheckMilestone(c, total);

            if (c.perennial) { t.growth = c.growth * (1f - c.regrowFraction); t.harvests++; }
            else { t.crop = ""; t.growth = 0; t.harvests = 0; }

            if (fx)
            {
                Raise(FxType.Harvest, x, y, "+" + Fmt.Num(value), 1f, c.id);
                Harvested?.Invoke(x, y, c.id, count);
            }
            if (S.tutorialStep >= 3 && S.tutorialStep <= 4) AdvanceTutorial(5);
            Changed(x, y);
            MarkDirty();
            return value;
        }

        void CheckMilestone(CropDef c, double total)
        {
            int ms = (int)S.milestones.Get(c.id);
            while (ms < B.milestones.Length && total >= B.milestones[ms])
            {
                ms++;
                S.milestones.Set(c.id, ms);
                ShowBigReward(Loc.T("ui.milestone", B.milestones[ms - 1], Loc.T("crop." + c.id)),
                    Loc.T("ui.milestone_bonus", Fmt.Pct(B.milestoneBonus[ms - 1])), "crop:" + c.id);
            }
        }

        // ---------------------------------------------------------------- Gegenstaende platzieren

        public void StartPlacing(string itemId)
        {
            if (S.items.Get(itemId) < 1) return;
            PlacingItem = itemId;
            MarkDirty();
        }

        public void CancelPlacing() { PlacingItem = null; MarkDirty(); }

        bool DoPlace(int x, int y)
        {
            var def = Balance.Item(PlacingItem);
            if (def == null || S.items.Get(def.id) < 1) { CancelPlacing(); return false; }
            var t = Tile(x, y);
            t.item = def.id; t.itemTimer = 0;
            t.tilled = false; t.watered = false;
            S.items.Add(def.id, -1);
            Discover(def.id);
            RecalcCoverage();
            Raise(FxType.Place, x, y, null, 1f, def.id);
            Changed(x, y);
            RefreshArea(x, y, def.range + 1);
            if (S.items.Get(def.id) < 1) CancelPlacing();
            MarkDirty();
            return true;
        }

        bool DoFertilize(int x, int y)
        {
            var def = Balance.Item(PlacingItem);
            if (def == null || S.items.Get(def.id) < 1) { CancelPlacing(); return false; }
            var t = Tile(x, y);
            t.fert = Mathf.Max(t.fert, (int)def.power);
            S.items.Add(def.id, -1);
            Discover(def.id);
            Raise(FxType.Fertilize, x, y, null, 1f, def.id);
            Changed(x, y);
            if (S.items.Get(def.id) < 1) CancelPlacing();
            MarkDirty();
            return true;
        }

        public void PickupItem(int x, int y)
        {
            var t = Tile(x, y);
            if (t == null || !t.HasItem) return;
            var def = Balance.Item(t.item);
            S.items.Add(t.item, 1);
            t.item = "";
            RecalcCoverage();
            Changed(x, y);
            RefreshArea(x, y, (def != null ? def.range : 1) + 1);
            MarkDirty();
        }

        void RefreshArea(int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++) for (int x = cx - r; x <= cx + r; x++) if (Tile(x, y) != null) Changed(x, y);
        }

        /// <summary>Berechnet Sprinkler-, Teich- und Vogelscheuchen-Wirkung neu.</summary>
        public void RecalcCoverage()
        {
            int n = W * H;
            sprinkled = new bool[n]; pondWater = new bool[n]; scarecrow = new float[n];
            for (int i = 0; i < n; i++) scarecrow[i] = 1f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var a = Balance.AreaAt(x, y);
                    if (a != null && a.water && IsAreaUnlocked(a))
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx >= 0 && yy >= 0 && xx < W && yy < H) pondWater[yy * W + xx] = true;
                            }
                    var t = Tile(x, y);
                    if (!t.HasItem) continue;
                    var def = Balance.Item(t.item);
                    if (def == null) continue;
                    if (def.kind == "sprinkler")
                    {
                        int r = def.range >= 3 ? 2 : 1;
                        for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                            {
                                if (def.range == 1 && Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;
                                int xx = x + dx, yy = y + dy;
                                if (xx >= 0 && yy >= 0 && xx < W && yy < H) sprinkled[yy * W + xx] = true;
                            }
                    }
                    else if (def.kind == "scarecrow")
                    {
                        for (int dy = -def.range; dy <= def.range; dy++) for (int dx = -def.range; dx <= def.range; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx >= 0 && yy >= 0 && xx < W && yy < H) scarecrow[yy * W + xx] = Mathf.Max(scarecrow[yy * W + xx], def.power);
                            }
                    }
                }
        }

        public bool IsSprinkled(int x, int y) => sprinkled != null && (sprinkled[y * W + x] || pondWater[y * W + x]);

        // ---------------------------------------------------------------- Laufende Automatisierung

        float tickAcc;

        void TickFarm(float dt)
        {
            tickAcc += dt;
            if (tickAcc < 0.2f) return;
            float step = tickAcc; tickAcc = 0;
            SimulateStep(step, null);
        }

        /// <summary>Passives Wachstum, Bienenstoecke, Erntehelfer. Wird live und fuer Offline-Fortschritt genutzt.</summary>
        void SimulateStep(float dt, OfflineSummary sum)
        {
            bool fx = sum == null;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = S.tiles[y * W + x];
                    if (t.HasCrop)
                    {
                        var c = Balance.Crop(t.crop);
                        if (c != null && t.growth < c.growth && IsWatered(x, y))
                        {
                            int before = Stage(t);
                            t.growth = Mathf.Min(c.growth, t.growth + B.passiveGrowthPerSecond * dt * GrowthMultiplier(x, y, t));
                            int after = Stage(t);
                            if (after != before)
                            {
                                if (after == 3) { if (fx) Raise(FxType.Ripe, x, y, null, 0.5f, c.id); else sum.grown++; }
                                Changed(x, y);
                            }
                        }
                    }
                    if (!t.HasItem) continue;
                    var def = Balance.Item(t.item);
                    if (def == null || def.interval <= 0) continue;
                    t.itemTimer += dt;
                    while (t.itemTimer >= def.interval)
                    {
                        t.itemTimer -= def.interval;
                        if (def.kind == "beehive") ProduceHoney(x, y, def, sum);
                        else if (def.kind == "harvester") { if (!AutoHarvest(x, y, def, sum)) { t.itemTimer = 0; break; } }
                    }
                }
        }

        void ProduceHoney(int x, int y, ItemDef def, OfflineSummary sum)
        {
            int flowers = 0;
            for (int dy = -def.range; dy <= def.range; dy++) for (int dx = -def.range; dx <= def.range; dx++)
                {
                    var t = Tile(x + dx, y + dy);
                    var c = t != null ? Balance.Crop(t.crop) : null;
                    if (c != null && (c.shape == "flower" || c.shape == "crystal")) flowers++;
                }
            double value = def.power * (1 + 0.5 * flowers);
            S.bin.Add("honey", 1);
            S.binValue += value;
            if (sum != null) sum.honey += value;
            else Raise(FxType.Honey, x, y, "+" + Fmt.Num(value), 1f, def.id);
            MarkDirty();
        }

        bool AutoHarvest(int x, int y, ItemDef def, OfflineSummary sum)
        {
            for (int dy = -def.range; dy <= def.range; dy++) for (int dx = -def.range; dx <= def.range; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    var t = Tile(xx, yy);
                    if (t == null || !t.HasCrop || !IsRipe(t) || Balance.Crop(t.crop).IsDefender) continue;
                    string id = t.crop;
                    double v = HarvestTile(xx, yy, t, sum == null);
                    if (sum != null) { sum.harvested++; sum.value += v; }
                    // Nachsaeen, wenn es keine mehrjaehrige Pflanze war
                    var c = Balance.Crop(id);
                    if (!t.HasCrop && t.tilled && PaySeed(c)) { t.crop = id; t.growth = 0; t.harvests = 0; Changed(xx, yy); }
                    return true;
                }
            return false;
        }

        // ---------------------------------------------------------------- Werbe-Belohnungen

        public void RewardGrowAll()
        {
            int n = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    var t = Tile(x, y);
                    var c = Balance.Crop(t.crop);
                    if (c == null || IsRipe(t)) continue;
                    t.growth = Mathf.Min(c.growth, t.growth + c.growth * B.adGrowAllFraction);
                    n++;
                    Raise(IsRipe(t) ? FxType.Ripe : FxType.Grow, x, y, null, 1.6f, c.id);
                    Changed(x, y);
                }
            MarkDirty();
        }

        public void RewardEnergy() { S.energy = MaxEnergy; MarkDirty(); }

        public void RewardSeeds()
        {
            var best = BestPlantableCrop();
            if (best == null) return;
            S.freeSeeds.Add(best.id, B.freeSeedPackSize);
            ShowToast(Loc.T("ui.free_seeds", B.freeSeedPackSize, Loc.T("crop." + best.id)), "crop:" + best.id);
            MarkDirty();
        }

        public void RewardOfflineDouble()
        {
            if (LastOffline == null) return;
            S.binValue += LastOffline.value + LastOffline.honey;
            LastOffline = null;
            MarkDirty();
        }

        // ---------------------------------------------------------------- Glueckskaefer

        public double LuckyBugReward() => Math.Max(5, Math.Round(8 * LevelScale));

        public void CatchLuckyBug()
        {
            double v = LuckyBugReward();
            AddCoins(v);
            AddXp(Math.Max(1, XpNeeded * 0.03));
            ShowToast(Loc.T("ui.lucky_bug", Fmt.Num(v)), "bug");
        }
    }
}
