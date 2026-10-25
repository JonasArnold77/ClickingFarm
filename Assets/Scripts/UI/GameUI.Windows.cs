using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    public partial class GameUI
    {
        // ------------------------------------------------------------------ Overlays (pausieren die Uhr)

        GameObject MakeOverlay(string name, float dim, Action onBackground)
        {
            var rt = UIKit.Rect(overlayLayer, name).Stretch();
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.03f, dim);
            if (onBackground != null)
            {
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => onBackground());
            }
            g.ModalCount++;
            return rt.gameObject;
        }

        void CloseOverlay(GameObject go)
        {
            if (go == null) return;
            Destroy(go);
            g.ModalCount = Mathf.Max(0, g.ModalCount - 1);
        }

        IEnumerator PopIn(Transform t, float from = 0.8f)
        {
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.22f)
            {
                if (t == null) yield break;
                float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, e);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ Grosses Fenster mit Reitern

        class Win { public GameObject root; public RectTransform content; public int tab; public Action build; public Transform tabRow; public string[] tabs; public Action<Transform, int> fill; }
        Win win;

        void OpenWindow(string title, string icon, string[] tabs, int tab, Action<Transform, int> fill)
        {
            CloseWindow();
            var w = new Win { tab = tab, tabs = tabs, fill = fill };
            w.root = MakeOverlay("Window", 0.55f, CloseWindow);
            var panel = UIKit.Panel(w.root.transform, "Panel");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = new Vector2(22, 140); prt.offsetMax = new Vector2(-22, -120);
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // Klicks nicht zum Hintergrund durchlassen

            var head = UIKit.HRow(panel.transform, "Head", 14, TextAnchor.MiddleLeft, new RectOffset(30, 20, 10, 10));
            head.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(0, 120));
            UIKit.Icon(head.transform, icon, 84);
            var t = UIKit.Txt(head.transform, title, 58, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            t.LE(-1, -1, 1);
            var close = UIKit.Btn(head.transform, "X", UIKit.BtnRed, CloseWindow, null, 44);
            close.LE(100, 96);

            float top = 140;
            if (tabs != null && tabs.Length > 0)
            {
                var tr = UIKit.HRow(panel.transform, "Tabs", 8, TextAnchor.MiddleCenter, new RectOffset(20, 20, 0, 0));
                tr.childForceExpandWidth = true;
                tr.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(0, 96));
                w.tabRow = tr.transform;
                top = 245;
            }
            UIKit.Scroll(panel.transform, out var scroll);
            scroll.GetComponent<RectTransform>().Stretch(22, 22, top, 26);
            w.content = scroll.content;
            win = w;
            w.build = () => RebuildWindow(w);
            w.build();
            StartCoroutine(PopIn(panel.transform));
            Sfx.Play("pop", 0.9f, 0.6f);
        }

        void RebuildWindow(Win w)
        {
            if (w.root == null) return;
            if (w.tabRow != null)
            {
                UIKit.Clear(w.tabRow);
                for (int i = 0; i < w.tabs.Length; i++)
                {
                    int k = i;
                    var b = UIKit.Btn(w.tabRow, w.tabs[i], i == w.tab ? UIKit.BtnOrange : UIKit.BtnGray, () => { w.tab = k; w.build(); }, null, 32);
                    b.LE(-1, 90, 1);
                }
            }
            UIKit.Clear(w.content);
            w.fill(w.content, w.tab);
        }

        void RefreshWindow() { if (win != null && win.root != null) win.build(); }

        void CloseWindow()
        {
            if (win == null) return;
            CloseOverlay(win.root);
            win = null;
        }

        /// <summary>Eine Zeile im Fenster: Icon, Titel, Beschreibung, rechts Platz fuer Kosten und Knopf.</summary>
        Transform Row(Transform content, string icon, string title, string desc, bool dimmed = false, Color? iconTint = null)
        {
            var p = UIKit.Panel(content, "Row", "paper");
            p.LE(-1, 170);
            var row = UIKit.HRow(p.transform, "H", 18, TextAnchor.MiddleLeft, new RectOffset(22, 22, 14, 18));
            row.GetComponent<RectTransform>().Stretch();
            var ic = UIKit.Icon(row.transform, icon, 116);
            if (iconTint.HasValue) ic.color = iconTint.Value;
            else if (dimmed) ic.color = new Color(0.25f, 0.2f, 0.2f, 0.6f);
            var col = UIKit.VCol(row.transform, "Text", 4, TextAnchor.MiddleLeft);
            col.childForceExpandHeight = false;
            col.LE(-1, -1, 1);
            var tt = UIKit.Txt(col.transform, title, 42, dimmed ? UIKit.TextMid : UIKit.TextDark, TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(desc)) UIKit.Txt(col.transform, desc, 31, UIKit.TextMid, TextAnchor.MiddleLeft);
            var right = UIKit.VCol(row.transform, "Right", 6, TextAnchor.MiddleRight);
            right.childControlWidth = true; right.childForceExpandWidth = false;
            right.LE(330, -1);
            return right.transform;
        }

        void Header(Transform content, string text)
        {
            var t = UIKit.Txt(content, text, 44, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            t.LE(-1, 70);
        }

        void Note(Transform content, string text)
        {
            var t = UIKit.Txt(content, text, 34, UIKit.TextMid, TextAnchor.MiddleCenter);
            t.LE(-1, 80);
        }

        Button RowButton(Transform right, string label, string color, Action onClick, bool interactable = true, string icon = null)
        {
            var b = UIKit.Btn(right, label, color, () => { onClick(); RefreshWindow(); }, icon, 34);
            b.LE(-1, 84);
            b.interactable = interactable;
            return b;
        }

        void Chips(Transform right, double coins, double wood = 0, double stone = 0, double fiber = 0)
        {
            var row = UIKit.HRow(right, "Chips", 12, TextAnchor.MiddleRight);
            UIKit.CostChips(row.transform, coins, wood, stone, fiber, 32);
        }

        void LockedLabel(Transform right, int level)
        {
            var row = UIKit.HRow(right, "Lock", 8, TextAnchor.MiddleRight);
            UIKit.Icon(row.transform, "lock", 44);
            UIKit.Txt(row.transform, Loc.T("ui.locked_level", level), 32, UIKit.TextMid, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ------------------------------------------------------------------ Laden

        void OpenShop(int tab)
        {
            OpenWindow(Loc.T("ui.shop"), "bag", new[] { Loc.T("ui.seeds"), Loc.T("ui.tools"), Loc.T("ui.skills"), Loc.T("ui.items") }, tab, FillShop);
        }

        void FillShop(Transform c, int tab)
        {
            var s = g.S;
            switch (tab)
            {
                case 0:
                    {
                        var r = Row(c, "gift", Loc.T("ui.ad_seeds"), Loc.T("ui.free_seeds", g.B.freeSeedPackSize, g.BestPlantableCrop() != null ? Loc.T("crop." + g.BestPlantableCrop().id) : ""));
                        RowButton(r, Loc.T("ui.ad_free"), UIKit.BtnPink, () => AdService.ShowRewarded(AdService.FreeSeeds, () => { g.RewardSeeds(); RefreshWindow(); }), true, "video");
                        bool headerDef = false, headerCrop = false;
                        foreach (var crop in g.B.crops)
                        {
                            if (crop.IsDefender && !headerDef) { headerDef = true; Header(c, Loc.T("ui.defenders")); Note(c, Loc.T("ui.defender_hint")); }
                            if (!crop.IsDefender && !headerCrop) { headerCrop = true; Header(c, Loc.T("ui.seeds")); }
                            bool unlocked = g.IsCropUnlocked(crop);
                            if (crop.IsDefender)
                            {
                                var d = crop.defense;
                                string ddesc = Loc.T("ui.type", Loc.T("element." + d.element)) + " · " + Loc.T("ui.defender_info", Fmt.Num(g.DefenderDamage(crop)), d.range.ToString("0.#"), Fmt.Num(g.DefenderMaxHp(crop)))
                                    + (d.pulse ? "\n" + Loc.T("ui.defender_pulse") : d.area > 0 ? "\n" + Loc.T("ui.defender_area") : "")
                                    + "\n" + Loc.T("ui.seed_cost_short", Fmt.Num(crop.seedCost)) + (s.freeSeeds.Get(crop.id) >= 1 ? " · " + Loc.T("ui.ad_free") + ": " + s.freeSeeds.Get(crop.id) : "");
                                var dr = Row(c, "crop:" + crop.id, unlocked || s.discovered.Contains(crop.id) ? Loc.T("crop." + crop.id) : Loc.T("ui.unknown"), unlocked ? ddesc : "", !unlocked);
                                if (!unlocked) { LockedLabel(dr, crop.starter ? g.B.otherStartersLevel : crop.unlockLevel); continue; }
                                bool dsel = s.selectedSeed == crop.id;
                                RowButton(dr, dsel ? Loc.T("ui.selected") : Loc.T("ui.select"), dsel ? UIKit.BtnGray : UIKit.BtnPurple, () => { g.SelectSeed(crop.id); CloseWindow(); }, !dsel);
                                continue;
                            }
                            string desc = Loc.T("ui.seed_info", Fmt.Num(crop.seedCost), Fmt.Num(crop.growth), Fmt.Num(g.SellValue(crop) * crop.yield));
                            if (crop.perennial) desc += " · " + Loc.T("ui.perennial");
                            if (!string.IsNullOrEmpty(crop.requiresArea)) desc += "\n" + Loc.T("ui.needs_area", Loc.T("area." + crop.requiresArea));
                            double free = s.freeSeeds.Get(crop.id);
                            if (free >= 1) desc += "\n" + Loc.T("ui.ad_free") + ": " + free;
                            var right = Row(c, "crop:" + crop.id, unlocked ? Loc.T("crop." + crop.id) : (s.discovered.Contains(crop.id) ? Loc.T("crop." + crop.id) : Loc.T("ui.unknown")), unlocked ? desc : "", !unlocked);
                            if (!unlocked) { LockedLabel(right, crop.unlockLevel); continue; }
                            bool sel = s.selectedSeed == crop.id;
                            RowButton(right, sel ? Loc.T("ui.selected") : Loc.T("ui.select"), sel ? UIKit.BtnGray : UIKit.BtnGreen, () => { g.SelectSeed(crop.id); CloseWindow(); }, !sel);
                        }
                        break;
                    }
                case 1:
                    for (int i = 0; i < g.B.tools.Length; i++)
                    {
                        int ti = i;
                        var tool = g.B.tools[i];
                        var cur = g.ToolTier(i);
                        string name = Loc.T("tier." + cur.id) + "-" + Loc.T("tool." + tool.id);
                        string desc = Loc.T("pattern." + cur.pattern) + " · Kraft ×" + cur.power + " · " + Fmt.Num(cur.energy) + " " + Loc.T("res.energy");
                        var right = Row(c, "tool:" + tool.id + ":" + cur.id, name, desc);
                        int next = s.toolTiers[i] + 1;
                        if (next >= g.B.toolTiers.Length) { UIKit.Txt(right, Loc.T("ui.max"), 36, UIKit.Green, TextAnchor.MiddleRight, true); continue; }
                        var nt = g.B.toolTiers[next];
                        if (nt.unlockLevel > s.level) { LockedLabel(right, nt.unlockLevel); continue; }
                        g.ToolUpgradeCost(i, out var co, out var wo, out var st);
                        Chips(right, co, wo, st);
                        RowButton(right, Loc.T("tier." + nt.id), UIKit.BtnOrange, () => g.BuyToolUpgrade(ti), g.CanAfford(co, wo, st), "tool:" + tool.id + ":" + nt.id);
                    }
                    break;
                case 2:
                    foreach (var u in g.B.upgrades)
                    {
                        int lvl = g.UpgradeLevel(u.id);
                        string name = Loc.T("upgrade." + u.id) + (lvl > 0 ? " " + Game.Roman(lvl) : "");
                        string desc = g.UpgradeDescription(u.id, g.UpgradeValue(u.id));
                        if (lvl < u.levels.Length) desc += "  →  " + g.UpgradeDescription(u.id, u.levels[lvl].value).Split(':')[1].Trim();
                        var right = Row(c, "upgrade:" + u.id, name, desc);
                        if (lvl >= u.levels.Length) { UIKit.Txt(right, Loc.T("ui.max"), 36, UIKit.Green, TextAnchor.MiddleRight, true); continue; }
                        var l = u.levels[lvl];
                        if (l.unlockLevel > s.level) { LockedLabel(right, l.unlockLevel); continue; }
                        Chips(right, l.coins);
                        string id = u.id;
                        RowButton(right, Loc.T("ui.buy"), UIKit.BtnGreen, () => g.BuyUpgrade(id), g.CanAfford(l.coins));
                    }
                    break;
                case 3:
                    foreach (var it in g.B.items)
                    {
                        bool unlocked = it.unlockLevel <= s.level;
                        double owned = s.items.Get(it.id);
                        string desc = Loc.T("itemdesc." + it.id) + (owned > 0 ? "\n" + Loc.T("ui.owned", owned) : "");
                        var right = Row(c, "item:" + it.id, unlocked || s.discovered.Contains(it.id) ? Loc.T("item." + it.id) : Loc.T("ui.unknown"), unlocked ? desc : "", !unlocked);
                        if (!unlocked) { LockedLabel(right, it.unlockLevel); continue; }
                        Chips(right, it.coins, it.wood, it.stone, it.fiber);
                        string id = it.id;
                        RowButton(right, Loc.T("ui.buy"), UIKit.BtnGreen, () => { if (g.BuyItem(id)) CloseWindow(); }, g.CanAfford(it.coins, it.wood, it.stone, it.fiber));
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ Bauen

        void OpenBuild(int tab)
        {
            OpenWindow(Loc.T("ui.build"), "hammer", new[] { Loc.T("ui.items"), Loc.T("ui.areas"), Loc.T("ui.buildings") }, tab, FillBuild);
        }

        void FillBuild(Transform c, int tab)
        {
            var s = g.S;
            switch (tab)
            {
                case 0:
                    {
                        bool any = false;
                        foreach (var it in g.B.items)
                        {
                            double owned = s.items.Get(it.id);
                            if (owned < 1) continue;
                            any = true;
                            var right = Row(c, "item:" + it.id, Loc.T("item." + it.id) + "  ×" + owned, Loc.T("itemdesc." + it.id));
                            string id = it.id;
                            RowButton(right, Loc.T("ui.place"), UIKit.BtnGreen, () => { g.StartPlacing(id); CloseWindow(); });
                        }
                        if (!any)
                        {
                            Note(c, Loc.T("ui.items") + ": 0");
                            var b = UIKit.Btn(c, Loc.T("ui.shop") + " → " + Loc.T("ui.items"), UIKit.BtnOrange, () => OpenShop(3), "bag", 38);
                            b.LE(-1, 110);
                        }
                        break;
                    }
                case 1:
                    foreach (var a in g.B.areas)
                    {
                        if (a.startUnlocked) continue;
                        bool unlocked = g.IsAreaUnlocked(a);
                        var right = Row(c, "map", Loc.T("area." + a.id), Loc.T("areadesc." + a.id), false, unlocked ? (Color?)null : new Color(0.7f, 0.7f, 0.7f));
                        if (unlocked) { UIKit.Txt(right, Loc.T("ui.done"), 36, UIKit.Green, TextAnchor.MiddleRight, true); continue; }
                        if (!g.AreaRequirementMet(a, out var reason))
                        {
                            UIKit.Txt(right, reason, 30, UIKit.TextMid, TextAnchor.MiddleRight, true);
                            continue;
                        }
                        Chips(right, a.coins, a.wood, a.stone);
                        var area = a;
                        RowButton(right, Loc.T("ui.unlock_area"), UIKit.BtnGreen, () => { if (g.UnlockArea(area)) { CloseWindow(); CameraController.I.FocusOn(area.center + Vector2.one * 0.5f); } }, g.CanAfford(a.coins, a.wood, a.stone));
                    }
                    break;
                case 2:
                    BuildingRow(c, "house");
                    BuildingRow(c, "bin");
                    break;
            }
        }

        void BuildingRow(Transform c, string building)
        {
            var levels = g.BuildingLevels(building);
            int lvl = g.BuildingLevel(building);
            var cur = levels[lvl];
            string prefix = building == "house" ? "house." : "bin.";
            string desc = building == "house"
                ? Loc.T("ui.house_energy", Fmt.Num(cur.maxEnergy)) + " · " + Loc.T("ui.house_night", Fmt.Pct(cur.nightGrowth))
                : Loc.T("ui.bin_bonus", Fmt.Pct(cur.payoutBonus)) + " · " + Loc.T("ui.sell_now", Mathf.RoundToInt(cur.instantRate * 100));
            var right = Row(c, building + ":" + lvl, Loc.T(prefix + cur.id), desc);
            var con = g.S.construction;
            if (con.Active && con.building == building) { UIKit.Txt(right, Loc.T("ui.construction_left", con.clicksLeft), 30, UIKit.TextMid, TextAnchor.MiddleRight, true); return; }
            if (lvl + 1 >= levels.Length) { UIKit.Txt(right, Loc.T("ui.max"), 36, UIKit.Green, TextAnchor.MiddleRight, true); return; }
            var next = levels[lvl + 1];
            if (next.unlockLevel > g.S.level) { LockedLabel(right, next.unlockLevel); return; }
            if (con.Active) { UIKit.Txt(right, Loc.T("ui.under_construction"), 28, UIKit.TextMid, TextAnchor.MiddleRight); return; }
            Chips(right, next.coins, next.wood, next.stone, next.fiber);
            RowButton(right, "→ " + Loc.T(prefix + next.id), UIKit.BtnOrange, () =>
            {
                if (g.StartConstruction(building))
                {
                    CloseWindow();
                    var sr = building == "house" ? FarmView.I.House : FarmView.I.Bin;
                    CameraController.I.FocusOn(sr.bounds.center);
                    ShowToast(Loc.T("ui.under_construction"), "hammer");
                }
            }, g.CanAfford(next.coins, next.wood, next.stone, next.fiber));
        }

        // ------------------------------------------------------------------ Auftraege

        void OpenQuests() => OpenWindow(Loc.T("ui.quests"), "scroll", new[] { Loc.T("ui.quests_small"), Loc.T("ui.projects") }, 0, FillQuests);

        void FillQuests(Transform c, int tab)
        {
            var s = g.S;
            if (tab == 0)
            {
                foreach (var q in s.quests)
                {
                    var right = Row(c, QuestIcon(q), g.QuestText(q), Loc.T("ui.reward") + ": " + Fmt.Num(q.rewardCoins) + " " + Loc.T("res.coins") + " · " + Fmt.Num(q.rewardXp) + " XP");
                    if (q.Done)
                    {
                        var qq = q;
                        RowButton(right, Loc.T("ui.claim"), UIKit.BtnGreen, () => g.ClaimQuest(qq)).gameObject.AddComponent<Pulse>();
                    }
                    else
                    {
                        var bar = UIKit.Bar(right, "Progress", new Color(0.3f, 0.2f, 0.1f, 0.25f), UIKit.Green, out var fill);
                        bar.LE(300, 50);
                        UIKit.SetBar(fill, (float)(q.progress / q.amount));
                        var t = UIKit.Txt(bar, Fmt.Num(q.progress) + "/" + Fmt.Num(q.amount), 28, Color.white, TextAnchor.MiddleCenter, true, true);
                        t.rectTransform.Stretch();
                    }
                }
            }
            else
            {
                foreach (var p in g.B.projects)
                {
                    var st = g.ProjectStateOf(p.id);
                    bool done = st.stage >= p.stages.Length;
                    string desc = Loc.T("projectdesc." + p.id) + "\n" + (done ? Loc.T("ui.done") : Loc.T("ui.stage", st.stage + 1, p.stages.Length));
                    var right = Row(c, "hammer", Loc.T("project." + p.id), desc, p.unlockLevel > s.level);
                    if (done) { UIKit.Txt(right, Loc.T("ui.done"), 36, UIKit.Green, TextAnchor.MiddleRight, true); continue; }
                    if (p.unlockLevel > s.level) { LockedLabel(right, p.unlockLevel); continue; }
                    var stage = p.stages[st.stage];
                    Chips(right, stage.coins, stage.wood, stage.stone, stage.fiber);
                    string id = p.id;
                    RowButton(right, Loc.T("ui.deliver"), UIKit.BtnOrange, () => g.DeliverProject(id), g.CanAfford(stage.coins, stage.wood, stage.stone, stage.fiber));
                }
            }
        }

        static string QuestIcon(QuestState q) => q.type switch
        {
            "harvest" => q.target == "*" ? "bin:0" : "crop:" + q.target,
            "clear" => "ob:" + q.target,
            "till" => "tool:hoe:wood",
            "water" => "tool:can:wood",
            _ => "upgrade:green_thumb"
        };

        // ------------------------------------------------------------------ Almanach

        void OpenAlmanac() => OpenWindow(Loc.T("ui.almanac"), "book", new[] { Loc.T("ui.seeds"), Loc.T("ui.items") }, 0, FillAlmanac);

        void FillAlmanac(Transform c, int tab)
        {
            var s = g.S;
            int total = g.B.crops.Length + g.B.items.Length, found = 0;
            foreach (var cr in g.B.crops) if (s.discovered.Contains(cr.id)) found++;
            Note(c, Loc.T("ui.battle_kills", Fmt.Num(s.monstersDefeated)) + " · " + Loc.T("ui.nights_survived", s.nightsSurvived));
            foreach (var it in g.B.items) if (s.discovered.Contains(it.id)) found++;
            Header(c, Loc.T("ui.almanac_progress", found, total));

            var gridRt = UIKit.Rect(c, "Grid");
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230, 300); grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            void Cell(string id, string icon, string name, string sub, int stars)
            {
                bool known = s.discovered.Contains(id);
                var p = UIKit.Panel(gridRt, "Cell", known ? "paper" : "cream");
                var col = UIKit.VCol(p.transform, "V", 4, TextAnchor.UpperCenter, new RectOffset(8, 8, 14, 10));
                col.GetComponent<RectTransform>().Stretch();
                col.childForceExpandWidth = false; col.childControlWidth = true;
                var ic = UIKit.Icon(col.transform, icon, 140);
                if (!known) ic.color = new Color(0.12f, 0.08f, 0.06f, 0.75f); // Silhouette
                var t = UIKit.Txt(col.transform, known ? name : "?", 30, UIKit.TextDark, TextAnchor.MiddleCenter, true);
                t.LE(-1, 44);
                if (known && !string.IsNullOrEmpty(sub)) UIKit.Txt(col.transform, sub, 26, UIKit.TextMid, TextAnchor.MiddleCenter).LE(-1, 34);
                if (known && stars >= 0)
                {
                    var sr = UIKit.HRow(col.transform, "Stars", 2, TextAnchor.MiddleCenter);
                    for (int i = 0; i < g.B.milestones.Length; i++)
                    {
                        var st = UIKit.Icon(sr.transform, "star", 40);
                        if (i >= stars) st.color = new Color(0.3f, 0.25f, 0.25f, 0.35f);
                    }
                }
            }

            if (tab == 0)
                foreach (var cr in g.B.crops)
                    Cell(cr.id, "crop:" + cr.id, Loc.T("crop." + cr.id), Loc.T("ui.harvested", Fmt.Num(s.harvestCounts.Get(cr.id))), (int)s.milestones.Get(cr.id));
            else
                foreach (var it in g.B.items)
                    Cell(it.id, "item:" + it.id, Loc.T("item." + it.id), "", -1);

            if (tab == 0) Note(c, string.Join(" · ", Array.ConvertAll(g.B.milestones, m => m + "×")) + "  →  " + string.Join(" / ", Array.ConvertAll(g.B.milestoneBonus, b => "+" + Fmt.Pct(b))));
        }

        // ------------------------------------------------------------------ Einstellungen

        bool resetArmed;

        void OpenSettings() { resetArmed = false; OpenWindow(Loc.T("ui.settings"), "gear", null, 0, FillSettings); }

        void FillSettings(Transform c, int tab)
        {
            var s = g.S;
            var r1 = Row(c, "bell", Loc.T("ui.sound"), s.sound ? Loc.T("ui.on") : Loc.T("ui.off"));
            RowButton(r1, s.sound ? Loc.T("ui.on") : Loc.T("ui.off"), s.sound ? UIKit.BtnGreen : UIKit.BtnGray, () => { s.sound = !s.sound; g.Save(); });
            var r2 = Row(c, "energy", Loc.T("ui.vibration"), s.vibration ? Loc.T("ui.on") : Loc.T("ui.off"));
            RowButton(r2, s.vibration ? Loc.T("ui.on") : Loc.T("ui.off"), s.vibration ? UIKit.BtnGreen : UIKit.BtnGray, () => { s.vibration = !s.vibration; g.Save(); Haptics.Tap(40); });
            Header(c, Loc.T("ui.playtime") + ": " + Fmt.Duration(s.playTime) + " · " + Loc.T("ui.day", s.day));
            Note(c, Loc.T("res.coins") + " gesamt: " + Fmt.Num(s.totalEarned));
            var b = UIKit.Btn(c, resetArmed ? Loc.T("ui.reset_confirm") : Loc.T("ui.reset"), UIKit.BtnRed, () =>
            {
                if (!resetArmed) { resetArmed = true; RefreshWindow(); return; }
                resetArmed = false;
                CloseWindow();
                g.ResetGame();
                CameraController.I.FocusStart();
            }, "error", 36);
            b.LE(-1, 110);
        }

        // ------------------------------------------------------------------ Kleine Popups

        GameObject popup;

        Transform OpenPopup(string title, string icon, float iconSize = 200)
        {
            ClosePopup();
            popup = MakeOverlay("Popup", 0.5f, ClosePopup);
            var p = UIKit.Panel(popup.transform, "Panel");
            p.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var rt = p.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(920, 0);
            var col = p.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(40, 40, 36, 40); col.spacing = 18; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ic = UIKit.Icon(p.transform, icon, iconSize);
            ic.preserveAspect = true;
            var t = UIKit.Txt(p.transform, title, 56, UIKit.TextDark, TextAnchor.MiddleCenter, true);
            t.LE(-1, 80);
            StartCoroutine(PopIn(p.transform));
            Sfx.Play("pop", 0.9f, 0.6f);
            return p.transform;
        }

        void ClosePopup() { if (popup != null) { CloseOverlay(popup); popup = null; } }

        void PopupText(Transform p, string text, int size = 36, Color? col = null)
        {
            var t = UIKit.Txt(p, text, size, col ?? UIKit.TextMid, TextAnchor.MiddleCenter);
            t.LE(-1, size * 1.6f);
        }

        Button PopupButton(Transform p, string label, string color, Action a, string icon = null, bool interactable = true)
        {
            var b = UIKit.Btn(p, label, color, a, icon, 40);
            b.LE(-1, 120);
            b.interactable = interactable;
            return b;
        }

        void PopupChips(Transform p, double coins, double wood = 0, double stone = 0, double fiber = 0)
        {
            var row = UIKit.HRow(p, "Chips", 26, TextAnchor.MiddleCenter);
            row.LE(-1, 60);
            UIKit.CostChips(row.transform, coins, wood, stone, fiber, 40);
        }

        void OpenAreaPopup(AreaDef a)
        {
            if (a == null) return;
            var p = OpenPopup(Loc.T("area." + a.id), "map", 160);
            PopupText(p, Loc.T("areadesc." + a.id));
            if (!g.AreaRequirementMet(a, out var reason))
            {
                PopupText(p, reason, 40, UIKit.Red);
                PopupButton(p, Loc.T("ui.close"), UIKit.BtnGray, ClosePopup);
                return;
            }
            PopupChips(p, a.coins, a.wood, a.stone);
            PopupButton(p, Loc.T("ui.unlock_area"), UIKit.BtnGreen, () => { if (g.UnlockArea(a)) ClosePopup(); }, "map", g.CanAfford(a.coins, a.wood, a.stone));
        }

        void OpenItemPopup(int x, int y)
        {
            var t = g.Tile(x, y);
            if (t == null || !t.HasItem) return;
            var p = OpenPopup(Loc.T("item." + t.item), "item:" + t.item, 180);
            PopupText(p, Loc.T("itemdesc." + t.item));
            PopupButton(p, Loc.T("ui.pickup"), UIKit.BtnOrange, () => { g.PickupItem(x, y); ClosePopup(); });
        }

        void OpenHouse()
        {
            var lv = g.B.houseLevels[g.S.houseLevel];
            var p = OpenPopup(Loc.T("house." + lv.id), "house:" + g.S.houseLevel, 260);
            PopupText(p, Loc.T("ui.house_energy", Fmt.Num(lv.maxEnergy)) + " · " + Loc.T("ui.house_night", Fmt.Pct(lv.nightGrowth)));
            HouseUpgradeSection(p, "house");
            PopupButton(p, Loc.T("ui.ad_energy"), UIKit.BtnPink, () => AdService.ShowRewarded(AdService.Energy, g.RewardEnergy), "video");
            PopupButton(p, Loc.T("ui.sleep"), UIKit.BtnBlue, () => { ClosePopup(); g.BeginSleep(false); }, "moon");
        }

        void HouseUpgradeSection(Transform p, string building)
        {
            var levels = g.BuildingLevels(building);
            int lvl = g.BuildingLevel(building);
            var con = g.S.construction;
            if (con.Active && con.building == building) { PopupText(p, Loc.T("ui.under_construction") + "\n" + Loc.T("ui.construction_left", con.clicksLeft), 36, UIKit.TextDark); return; }
            if (lvl + 1 >= levels.Length) return;
            var next = levels[lvl + 1];
            string prefix = building == "house" ? "house." : "bin.";
            if (next.unlockLevel > g.S.level) { PopupText(p, "→ " + Loc.T(prefix + next.id) + ": " + Loc.T("ui.locked_level", next.unlockLevel)); return; }
            if (con.Active) return;
            PopupChips(p, next.coins, next.wood, next.stone, next.fiber);
            PopupButton(p, Loc.T("ui.upgrade") + " → " + Loc.T(prefix + next.id), UIKit.BtnOrange, () =>
            {
                if (g.StartConstruction(building)) { ClosePopup(); ShowToast(Loc.T("ui.under_construction"), "hammer"); }
            }, "hammer", g.CanAfford(next.coins, next.wood, next.stone, next.fiber));
        }

        void OpenBin()
        {
            if (g.S.construction.Active && g.S.construction.building == "bin") { g.HammerConstruction(); return; }
            var lv = g.CurrentBin;
            var p = OpenPopup(Loc.T("bin." + lv.id), "bin:" + g.S.binLevel, 200);
            PopupText(p, Loc.T("ui.bin_content", Fmt.Num(g.S.binValue)), 44, UIKit.TextDark);
            if (g.S.bin.Count > 0)
            {
                var row = UIKit.HRow(p, "Items", 18, TextAnchor.MiddleCenter);
                row.LE(-1, 90);
                int shown = 0;
                foreach (var e in g.S.bin)
                {
                    if (e.v < 1 || shown++ >= 6) continue;
                    UIKit.Icon(row.transform, e.id == "honey" ? "honey" : "crop:" + e.id, 70);
                    UIKit.Txt(row.transform, "×" + Fmt.Num(e.v), 34, UIKit.TextDark, TextAnchor.MiddleLeft, true).horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }
            PopupText(p, Loc.T("ui.bin_info") + (lv.payoutBonus > 0 ? " (" + Loc.T("ui.bin_bonus", Fmt.Pct(lv.payoutBonus)) + ")" : ""));
            PopupButton(p, Loc.T("ui.sell_now", Mathf.RoundToInt(lv.instantRate * 100)) + "  +" + Fmt.Num(Math.Round(g.S.binValue * lv.instantRate)), UIKit.BtnGreen,
                () => { g.SellBinNow(); ClosePopup(); }, "coin", g.S.binValue > 0);
            HouseUpgradeSection(p, "bin");
        }
    }
}
