using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Die komplette Oberflaeche: HUD (immer sichtbar), Werkzeugleiste, Menues,
    /// schwebende Zahlen, fliegende Ernte, Einfuehrung. Fenster und Feiern: siehe GameUI.Windows/Celebrate.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        Game g;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform canvasRt, safe, fxLayer, overlayLayer, worldLabels;

        // HUD
        Text coinText, woodText, stoneText, fiberText, levelText, xpText, energyText, dayText, goalText, comboText, seedText, seedCost, binText, questBadge;
        RectTransform xpFill, energyFill, comboRt, coinIconRt, binBtnRt, goalRt, placingBar, growAllBtn, energyRt;
        Image dayIcon, goalIcon, seedIcon;
        Image[] toolIcons;
        Text placingText;
        readonly CountUp coinCount = new CountUp(), binCount = new CountUp();
        Pulse binPulse, questPulse;

        // Einfuehrung
        RectTransform tutBubble, tutHand;
        Text tutText;
        float tutDoneTimer;

        void Awake()
        {
            g = Game.I;
            BuildCanvas();
            BuildHud();
            BuildBottom();
            BuildTutorial();
            g.StateChanged += RefreshHud;
            g.FeedbackRaised += OnFeedback;
            g.Harvested += OnHarvested;
            g.Toast += ShowToast;
            g.LeveledUp += (lvl, unlocks) => QueueCelebration(() => ShowLevelUp(lvl, unlocks));
            g.BigReward += (t, s, i) => QueueCelebration(() => ShowBigReward(t, s, i));
            g.AreaTapped += OpenAreaPopup;
            g.ItemTapped += OpenItemPopup;
            g.BuildingTapped += b => { if (b == "house") OpenHouse(); else OpenBin(); };
            g.SleepStarted += ex => StartCoroutine(SleepRoutine(ex));
            g.OfflineReturned += s => QueueCelebration(() => ShowWelcomeBack(s));
            AdService.Presenter = ShowFakeAd;
            g.StarterNeeded += ShowStarterChoice;
            g.BattleStarted += OnBattleStarted;
            g.BattleEnded += r => QueueCelebration(() => ShowBattleResult(r));
            g.DefenderTapped += OnDefenderTapped;
        }

        void Start() { RefreshHud(); }

        // ------------------------------------------------------------------ Canvas

        void BuildCanvas()
        {
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var cgo = new GameObject("UI", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cgo.AddComponent<GraphicRaycaster>();
            canvasRt = (RectTransform)cgo.transform;

            safe = UIKit.Rect(canvasRt, "SafeArea").Stretch();
            worldLabels = UIKit.Rect(safe, "WorldLabels").Stretch();
            fxLayer = UIKit.Rect(canvasRt, "Fx").Stretch();
            overlayLayer = UIKit.Rect(canvasRt, "Overlays").Stretch();
            ApplySafeArea();
        }

        Rect lastSafe;
        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == lastSafe || Screen.width <= 0) return;
            lastSafe = sa;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ HUD oben

        Image Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var p = UIKit.Panel(parent, name);
            p.rectTransform.Place(anchor, anchor, pivot, pos, size);
            return p;
        }

        void BuildHud()
        {
            // Links: Stufe + XP, Muenzen
            var left = Box(safe, "LeftBox", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(560, 210)).transform;
            var badge = UIKit.Img(left, "Badge", Art.Get("star"), Color.white);
            badge.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(62, -62), new Vector2(118, 118));
            badge.raycastTarget = false;
            levelText = UIKit.Txt(badge.transform, "1", 46, Color.white, TextAnchor.MiddleCenter, true, true);
            levelText.rectTransform.Stretch(0, 0, 0, 6);
            var xpBar = UIKit.Bar(left, "XpBar", new Color(0.3f, 0.2f, 0.12f, 0.35f), Balance.Hex("#9B6BFF"), out xpFill);
            xpBar.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(126, -30), new Vector2(-146, 56));
            xpText = UIKit.Txt(xpBar, "", 30, Color.white, TextAnchor.MiddleCenter, true, true);
            xpText.rectTransform.Stretch();

            var coinIcon = UIKit.Img(left, "CoinIcon", Art.Get("coin"), Color.white);
            coinIcon.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(66, 50), new Vector2(76, 76));
            coinIconRt = coinIcon.rectTransform;
            coinText = UIKit.Txt(left, "0", 60, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            coinText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0.5f), new Vector2(118, 50), new Vector2(-130, 80));

            // Material-Leiste
            var mats = Box(safe, "Materials", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -232), new Vector2(560, 74)).transform;
            var mrow = UIKit.HRow(mats, "Row", 10, TextAnchor.MiddleLeft, new RectOffset(18, 10, 4, 8));
            mrow.GetComponent<RectTransform>().Stretch();
            woodText = MatChip(mrow.transform, "wood"); stoneText = MatChip(mrow.transform, "stone"); fiberText = MatChip(mrow.transform, "fiber");

            // Rechts: Tag/Uhrzeit, Energie
            var right = Box(safe, "RightBox", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(470, 210)).transform;
            dayIcon = UIKit.Img(right, "DayIcon", Art.Get("sun"), Color.white);
            dayIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(58, -56), new Vector2(72, 72));
            dayText = UIKit.Txt(right, "", 40, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            dayText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(104, -56), new Vector2(-120, 70));

            var eIcon = UIKit.Img(right, "EnergyIcon", Art.Get("energy"), Color.white);
            eIcon.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(54, 54), new Vector2(68, 68));
            var eBar = UIKit.Bar(right, "EnergyBar", new Color(0.3f, 0.2f, 0.12f, 0.35f), Balance.Hex("#FFC21A"), out energyFill);
            eBar.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0.5f), new Vector2(96, 54), new Vector2(-200, 56));
            energyRt = eBar;
            energyText = UIKit.Txt(eBar, "", 30, Color.white, TextAnchor.MiddleCenter, true, true);
            energyText.rectTransform.Stretch();
            var adE = UIKit.Btn(right, "", UIKit.BtnPink, () => AdService.ShowRewarded(AdService.Energy, g.RewardEnergy), "video", 34);
            adE.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-14, 54), new Vector2(86, 78));

            // Naechstes Ziel
            var goal = UIKit.Panel(safe, "Goal", "paper");
            goalRt = goal.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -322), new Vector2(940, 92));
            goal.raycastTarget = false;
            var grow = UIKit.HRow(goal.transform, "Row", 14, TextAnchor.MiddleCenter, new RectOffset(16, 16, 6, 10));
            grow.GetComponent<RectTransform>().Stretch();
            goalIcon = UIKit.Icon(grow.transform, "gift", 66);
            goalText = UIKit.Txt(grow.transform, "", 38, UIKit.TextDark, TextAnchor.MiddleCenter, true);
            goalText.horizontalOverflow = HorizontalWrapMode.Overflow;
            goal.gameObject.AddComponent<Pulse>().amount = 0.015f;

            // Combo-Anzeige
            comboText = UIKit.Txt(safe, "", 74, Balance.Hex("#FFE45C"), TextAnchor.MiddleCenter, true, true);
            comboRt = comboText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -640), new Vector2(800, 110));
            comboText.GetComponent<Outline>().effectDistance = new Vector2(5, -5);
        }

        Text MatChip(Transform parent, string icon)
        {
            var row = UIKit.HRow(parent, icon, 6);
            row.LE(-1, -1, 1);
            UIKit.Icon(row.transform, icon, 50);
            var t = UIKit.Txt(row.transform, "0", 36, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        // ------------------------------------------------------------------ Werkzeugleiste und Menue unten

        void BuildBottom()
        {
            // Menueleiste
            var menu = UIKit.Panel(safe, "Menu", "dark");
            menu.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(-16, 150));
            var mrow = UIKit.HRow(menu.transform, "Row", 10, TextAnchor.MiddleCenter, new RectOffset(14, 14, 14, 18));
            mrow.childForceExpandWidth = true;
            mrow.GetComponent<RectTransform>().Stretch();
            MenuBtn(mrow.transform, "bag", Loc.T("ui.shop"), UIKit.BtnOrange, () => OpenShop(0));
            MenuBtn(mrow.transform, "hammer", Loc.T("ui.build"), UIKit.BtnBlue, () => OpenBuild(0));
            var qb = MenuBtn(mrow.transform, "scroll", Loc.T("ui.quests"), UIKit.BtnGreen, OpenQuests);
            questBadge = UIKit.Txt(qb.transform, "!", 46, Color.white, TextAnchor.MiddleCenter, true, true);
            var qbr = questBadge.rectTransform; qbr.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -10), new Vector2(60, 60));
            var badgeBg = UIKit.Img(qbr, "bg", Art.Get("dot"), UIKit.Red); badgeBg.rectTransform.Stretch(-4, -4, -4, -4); badgeBg.transform.SetAsFirstSibling();
            questBadge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            questPulse = questBadge.gameObject.AddComponent<Pulse>(); questPulse.amount = 0.15f; questPulse.speed = 7;
            MenuBtn(mrow.transform, "book", Loc.T("ui.almanac"), UIKit.BtnPurple, OpenAlmanac);
            MenuBtn(mrow.transform, "gear", Loc.T("ui.menu"), UIKit.BtnGray, OpenSettings);

            // Werkzeugleiste
            var bar = UIKit.Panel(safe, "Toolbar");
            bar.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 166), new Vector2(-16, 168));
            var seedBtn = UIKit.Btn(bar.transform, "", UIKit.BtnGreen, () => OpenShop(0));
            DestroyImmediate(seedBtn.GetComponent<HorizontalLayoutGroup>());
            seedBtn.GetComponent<RectTransform>().Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(370, 140));
            seedIcon = UIKit.Icon(seedBtn.transform, "crop:cress", 96);
            seedIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66, 4), new Vector2(96, 96));
            seedText = UIKit.Txt(seedBtn.transform, "", 36, Color.white, TextAnchor.MiddleLeft, true, true);
            seedText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(124, 22), new Vector2(-130, 56));
            seedCost = UIKit.Txt(seedBtn.transform, "", 30, Color.white, TextAnchor.MiddleLeft, false, true);
            seedCost.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(124, -24), new Vector2(-130, 46));

            // Werkzeuge (zeigen Stufe und Flaeche, Tipp oeffnet Werkzeug-Laden)
            var tools = UIKit.HRow(bar.transform, "Tools", 4, TextAnchor.MiddleCenter);
            tools.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 0), new Vector2(330, 120));
            toolIcons = new Image[g.B.tools.Length];
            for (int i = 0; i < g.B.tools.Length; i++)
            {
                var slot = UIKit.Img(tools.transform, "Tool" + i, Art.Get("panel:paper"), Color.white, true, 3f);
                slot.LE(62, 92);
                var b = slot.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { Sfx.Play("click"); OpenShop(1); });
                slot.gameObject.AddComponent<Juicy>();
                var ic = UIKit.Img(slot.transform, "i", Art.Get("tool:hoe:wood"), Color.white);
                ic.rectTransform.Stretch(4, 4, 8, 8); ic.raycastTarget = false;
                toolIcons[i] = ic;
            }

            var binBtn = UIKit.Btn(bar.transform, "", UIKit.BtnOrange, OpenBin);
            DestroyImmediate(binBtn.GetComponent<HorizontalLayoutGroup>());
            binBtnRt = binBtn.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(300, 140));
            var bi = UIKit.Icon(binBtn.transform, "bin:0", 90);
            bi.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60, 6), new Vector2(90, 90));
            binText = UIKit.Txt(binBtn.transform, "0", 40, Color.white, TextAnchor.MiddleCenter, true, true);
            binText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(106, 4), new Vector2(-114, 80));
            binPulse = binBtn.gameObject.AddComponent<Pulse>(); binPulse.amount = 0.04f;

            // Platzier-Modus
            var pb = UIKit.Panel(safe, "Placing", "dark");
            placingBar = pb.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 342), new Vector2(-40, 110));
            var prow = UIKit.HRow(pb.transform, "Row", 16, TextAnchor.MiddleCenter, new RectOffset(20, 16, 10, 14));
            prow.GetComponent<RectTransform>().Stretch();
            placingText = UIKit.Txt(prow.transform, "", 36, Color.white, TextAnchor.MiddleLeft, true);
            placingText.LE(-1, -1, 1);
            UIKit.Btn(prow.transform, Loc.T("ui.cancel"), UIKit.BtnRed, () => g.CancelPlacing(), null, 34);
            placingBar.gameObject.SetActive(false);

            // Werbe-Knopf: alles wachsen lassen
            var ga = UIKit.Btn(safe, "", UIKit.BtnPink, () => AdService.ShowRewarded(AdService.GrowAll, g.RewardGrowAll), "video", 34);
            DestroyImmediate(ga.GetComponent<HorizontalLayoutGroup>());
            growAllBtn = ga.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 350), new Vector2(150, 150));
            var gai = UIKit.Icon(ga.transform, "crop:sunflower", 70);
            gai.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(70, 70));
            var gav = UIKit.Icon(ga.transform, "video", 40);
            gav.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(40, 40));
            var gat = UIKit.Txt(ga.transform, Loc.T("ui.ad_grow_all"), 24, Color.white, TextAnchor.MiddleCenter, true, true);
            gat.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-10, 60));
            ga.gameObject.AddComponent<Pulse>().amount = 0.03f;
        }

        Button MenuBtn(Transform parent, string icon, string label, string color, Action onClick)
        {
            var b = UIKit.Btn(parent, "", color, onClick);
            DestroyImmediate(b.GetComponent<HorizontalLayoutGroup>());
            b.LE(-1, 118, 1);
            var ic = UIKit.Icon(b.transform, icon, 60);
            ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(60, 60));
            var t = UIKit.Txt(b.transform, label, 28, Color.white, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(0, 42));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        // ------------------------------------------------------------------ HUD aktualisieren

        void RefreshHud()
        {
            var s = g.S;
            levelText.text = s.level.ToString();
            double need = g.XpNeeded;
            bool max = s.level >= g.B.maxLevel;
            UIKit.SetBar(xpFill, max ? 1f : (float)(s.xp / need));
            xpText.text = max ? Loc.T("ui.max_level") : Fmt.Num(s.xp) + " / " + Fmt.Num(need) + " XP";
            woodText.text = Fmt.Num(s.wood); stoneText.text = Fmt.Num(s.stone); fiberText.text = Fmt.Num(s.fiber);
            UIKit.SetBar(energyFill, s.energy / g.MaxEnergy);
            energyText.text = Mathf.CeilToInt(s.energy) + " / " + Fmt.Num(g.MaxEnergy);

            if (g.BattleActive) { goalText.text = BattleBanner(); goalIcon.sprite = Art.Get("moon"); }
            else if (g.NextGoal(out _, out var gname, out var gicon, out var left))
            {
                goalText.text = Loc.T("ui.next_goal", Fmt.Num(Math.Max(0, Math.Ceiling(left))), gname);
                goalIcon.sprite = Art.IconFor(gicon);
            }
            else { goalText.text = Loc.T("ui.max_level"); goalIcon.sprite = Art.Get("star"); }

            var c = Balance.Crop(s.selectedSeed) ?? Balance.Crop("cress");
            seedIcon.sprite = Art.IconFor("crop:" + c.id);
            seedText.text = Loc.T("crop." + c.id);
            double free = s.freeSeeds.Get(c.id);
            seedCost.text = free >= 1 ? Loc.T("ui.ad_free") + ": " + free : Loc.T("ui.seed_cost_short", Fmt.Num(c.seedCost));
            seedCost.color = free >= 1 || s.coins >= c.seedCost ? Color.white : new Color(1f, 0.6f, 0.55f);

            for (int i = 0; i < toolIcons.Length; i++)
                toolIcons[i].sprite = Art.Get("tool:" + g.B.tools[i].id + ":" + g.ToolTier(i).id);

            binPulse.active = s.binValue > 0;
            bool claimable = s.quests.Exists(q => q.Done && !q.claimed);
            questBadge.gameObject.SetActive(claimable);

            placingBar.gameObject.SetActive(!string.IsNullOrEmpty(g.PlacingItem));
            if (!string.IsNullOrEmpty(g.PlacingItem))
            {
                var it = Balance.Item(g.PlacingItem);
                placingText.text = (it != null && it.kind == "fertilizer" ? Loc.T("ui.fertilize_hint") : Loc.T("ui.placing"))
                    + "  (" + Loc.T("item." + g.PlacingItem) + " ×" + s.items.Get(g.PlacingItem) + ")";
            }

            bool anyGrowing = false;
            foreach (var t in s.tiles) if (t.HasCrop && !g.IsRipe(t)) { anyGrowing = true; break; }
            growAllBtn.gameObject.SetActive(anyGrowing && !g.TutorialActive);

            RefreshWorldLabels();
        }

        float timeRefresh;

        void Update()
        {
            ApplySafeArea();
            scaler.matchWidthOrHeight = Screen.width > Screen.height * 0.75f ? 1f : 0f;

            float dt = Time.unscaledDeltaTime;
            coinText.text = Fmt.Num(coinCount.Step(g.S.coins, dt));
            binText.text = "+" + Fmt.Num(binCount.Step(g.S.binValue, dt));

            timeRefresh -= dt;
            if (timeRefresh <= 0)
            {
                timeRefresh = 0.25f;
                dayText.text = Loc.T("ui.day", g.S.day) + " · " + Fmt.Clock(g.Hour);
                dayIcon.sprite = Art.Get(g.Hour >= 19f ? "moon" : "sun");
                if (g.BattleActive) goalText.text = BattleBanner();
            }

            // Combo
            if (g.ComboMult > 1.01f)
            {
                comboText.text = Loc.T("ui.combo", g.ComboMult.ToString("0.#"));
                float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.08f + (g.ComboMult - 1) * 0.05f;
                comboRt.localScale = Vector3.one * pulse;
                comboText.color = Color.Lerp(Balance.Hex("#FFE45C"), Balance.Hex("#FF6FA5"), (g.ComboMult - 1f) / 4f);
            }
            else comboText.text = "";

            UpdateFloaters(dt);
            UpdateFlyers(dt);
            UpdateConfetti(dt);
            UpdateTutorial(dt);
            UpdateWorldLabels();
            UpdateToasts(dt);
            PumpCelebrations();
        }

        // ------------------------------------------------------------------ Schwebende Zahlen

        class Floater { public Text t; public RectTransform rt; public float life, max; public Vector2 start; public float rise; }
        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Floater> floaterPool = new Stack<Floater>();

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local);
            return local;
        }

        Vector2 TileToCanvas(int x, int y, float yOff = 0.6f)
        {
            var sp = Camera.main.WorldToScreenPoint(FarmView.TileCenter(x, y) + new Vector3(0, yOff, 0));
            return ScreenToCanvas(sp);
        }

        public void FloatText(Vector2 canvasPos, string text, Color col, int size = 46, float life = 0.9f)
        {
            Floater f;
            if (floaterPool.Count > 0) { f = floaterPool.Pop(); f.t.gameObject.SetActive(true); }
            else
            {
                f = new Floater();
                f.t = UIKit.Txt(fxLayer, "", size, col, TextAnchor.MiddleCenter, true, true);
                f.rt = f.t.rectTransform;
                f.rt.sizeDelta = new Vector2(600, 100);
                f.t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            f.t.text = text; f.t.color = col; f.t.fontSize = size;
            f.rt.anchorMin = f.rt.anchorMax = new Vector2(0.5f, 0.5f);
            f.start = canvasPos + new Vector2(UnityEngine.Random.Range(-20f, 20f), 0);
            f.rt.anchoredPosition = f.start;
            f.life = 0; f.max = life; f.rise = 130f;
            floaters.Add(f);
        }

        void UpdateFloaters(float dt)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.life += dt;
                float t = f.life / f.max;
                if (t >= 1) { f.t.gameObject.SetActive(false); floaters.RemoveAt(i); floaterPool.Push(f); continue; }
                f.rt.anchoredPosition = f.start + new Vector2(0, f.rise * (1 - (1 - t) * (1 - t)));
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
                f.rt.localScale = Vector3.one * pop;
                var c = f.t.color; c.a = t < 0.7f ? 1 : 1 - (t - 0.7f) / 0.3f; f.t.color = c;
            }
        }

        void OnFeedback(Feedback f)
        {
            if (f.hasWorld)
            {
                var wp = ScreenToCanvas(Camera.main.WorldToScreenPoint((Vector3)f.world + new Vector3(0, 0.7f, 0)));
                switch (f.type)
                {
                    case FxType.MonsterHit:
                        FloatText(wp, f.text, f.strength >= 2f ? Balance.Hex("#FFE45C") : f.strength >= 1.5f ? Balance.Hex("#FFB0E0") : Color.white, f.strength >= 2f ? 50 : 38, 0.6f);
                        break;
                    case FxType.MonsterDie:
                        FloatText(wp, f.text, Balance.Hex("#FFE45C"), 46);
                        for (int i = 0; i < 3; i++) FlyIcon(wp, "coin", coinIconRt, i * 0.05f, 0.6f);
                        break;
                    case FxType.Steal: if (!string.IsNullOrEmpty(f.text)) FloatText(wp, f.text, Balance.Hex("#FF8A80"), 44); break;
                }
                return;
            }
            if (f.type == FxType.Wilt || f.type == FxType.Eaten) { if (!string.IsNullOrEmpty(f.text)) FloatText(TileToCanvas(f.x, f.y), f.text, Balance.Hex("#FF8A80"), 40); return; }
            if (f.x < 0 && f.type != FxType.Coins && f.type != FxType.Error) return;
            Vector2 pos = f.x >= 0 ? TileToCanvas(f.x, f.y) : ScreenToCanvas(new Vector2(Screen.width / 2f, Screen.height * 0.55f));
            switch (f.type)
            {
                case FxType.Break:
                    {
                        var ob = Balance.Obstacle(f.id);
                        if (ob != null) FloatText(pos, f.text + " " + Loc.T("res." + ob.drop), Balance.Hex("#FFF4D6"), 40);
                        break;
                    }
                case FxType.Crit: FloatText(pos + new Vector2(0, 30), f.text, Balance.Hex("#FFE45C"), 64, 1.1f); break;
                case FxType.Harvest: FloatText(pos, f.text, Balance.Hex("#FFE45C"), 48); break;
                case FxType.Honey: FloatText(pos, f.text, Balance.Hex("#FFB627"), 40); break;
                case FxType.Error: if (!string.IsNullOrEmpty(f.text)) FloatText(pos, f.text, Balance.Hex("#FF8A80"), 40, 1.3f); break;
                case FxType.Coins:
                    FloatText(ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, coinIconRt.position)) + new Vector2(140, -60), f.text, Balance.Hex("#FFE45C"), 54, 1.2f);
                    for (int i = 0; i < 8; i++) FlyIcon(pos + UnityEngine.Random.insideUnitCircle * 80, "coin", coinIconRt, i * 0.04f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Fliegende Icons (Ernte -> Kiste)

        class Flyer { public Image img; public Vector2 from, ctrl; public RectTransform target; public float t, delay, dur; }
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Stack<Image> flyerPool = new Stack<Image>();

        void OnHarvested(int x, int y, string crop, int count)
        {
            if (flyers.Count > 30) return;
            var from = TileToCanvas(x, y, 0.3f);
            FlyIcon(from, "crop:" + crop, binBtnRt, 0f, 1.25f);
        }

        void FlyIcon(Vector2 from, string icon, RectTransform target, float delay, float scale = 1f)
        {
            Image img = flyerPool.Count > 0 ? flyerPool.Pop() : UIKit.Img(fxLayer, "fly", null, Color.white);
            img.gameObject.SetActive(true);
            img.sprite = Art.IconFor(icon);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(90, 90) * scale;
            rt.anchoredPosition = from;
            rt.localScale = Vector3.zero;
            var f = new Flyer { img = img, from = from, target = target, delay = delay, dur = 0.65f + UnityEngine.Random.Range(0, 0.15f) };
            f.ctrl = from + new Vector2(UnityEngine.Random.Range(-150f, 150f), 320f);
            flyers.Add(f);
        }

        void UpdateFlyers(float dt)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                if (f.delay > 0) { f.delay -= dt; continue; }
                f.t += dt / f.dur;
                var to = ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, f.target.position));
                float t = Mathf.Clamp01(f.t);
                float e = t * t;
                var p = Vector2.Lerp(Vector2.Lerp(f.from, f.ctrl, e), Vector2.Lerp(f.ctrl, to, e), e);
                // erst herausspringen, dann einsaugen
                float s = t < 0.2f ? Mathf.Lerp(0f, 1.3f, t / 0.2f) : Mathf.Lerp(1.3f, 0.6f, (t - 0.2f) / 0.8f);
                f.img.rectTransform.anchoredPosition = p;
                f.img.rectTransform.localScale = Vector3.one * s;
                f.img.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 360f);
                if (f.t >= 1)
                {
                    f.img.gameObject.SetActive(false); flyerPool.Push(f.img); flyers.RemoveAt(i);
                    StartCoroutine(PunchRt(f.target));
                    Sfx.Play("coin", 1.2f + UnityEngine.Random.Range(0, 0.3f), 0.35f);
                }
            }
        }

        System.Collections.IEnumerator PunchRt(RectTransform rt)
        {
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.18f * Mathf.PI) * 0.15f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ Hinweise (Toasts)

        class ToastItem { public RectTransform rt; public float life; }
        readonly List<ToastItem> toasts = new List<ToastItem>();

        public void ShowToast(string text, string icon)
        {
            var p = UIKit.Panel(overlayLayer, "Toast", "paper");
            var rt = p.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 100), new Vector2(900, 110));
            p.raycastTarget = false;
            var row = UIKit.HRow(p.transform, "Row", 16, TextAnchor.MiddleCenter, new RectOffset(20, 20, 8, 12));
            row.GetComponent<RectTransform>().Stretch();
            UIKit.Icon(row.transform, icon, 70);
            var t = UIKit.Txt(row.transform, text, 38, UIKit.TextDark, TextAnchor.MiddleLeft, true);
            t.LE(-1, -1, 1);
            toasts.Add(new ToastItem { rt = rt, life = 0 });
            if (toasts.Count > 3) { Destroy(toasts[0].rt.gameObject); toasts.RemoveAt(0); }
            Sfx.Play("pop", 1.3f, 0.6f);
        }

        void UpdateToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var ts = toasts[i];
                ts.life += dt;
                if (ts.life > 2.8f) { Destroy(ts.rt.gameObject); toasts.RemoveAt(i); continue; }
                float slot = (toasts.Count - 1 - i) * 120f;
                float targetY = -430 - slot;
                float inT = Mathf.Clamp01(ts.life / 0.25f), outT = Mathf.Clamp01((ts.life - 2.4f) / 0.4f);
                float y = Mathf.Lerp(120, targetY, 1 - (1 - inT) * (1 - inT));
                ts.rt.anchoredPosition = new Vector2(outT * 1100f, y);
            }
        }

        // ------------------------------------------------------------------ Einfuehrung

        void BuildTutorial()
        {
            var b = UIKit.Panel(safe, "TutBubble", "paper");
            b.raycastTarget = false;
            tutBubble = b.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 350), new Vector2(900, 150));
            tutText = UIKit.Txt(b.transform, "", 40, UIKit.TextDark, TextAnchor.MiddleCenter, true);
            tutText.rectTransform.Stretch(24, 24, 10, 16);
            tutBubble.gameObject.AddComponent<Pulse>().amount = 0.02f;
            var hand = UIKit.Img(fxLayer, "Hand", Art.Get("arrow"), Balance.Hex("#FFE45C"));
            hand.raycastTarget = false;
            tutHand = hand.rectTransform;
            tutHand.sizeDelta = new Vector2(100, 100);
            hand.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.15f, 0, 1);
        }

        void UpdateTutorial(float dt)
        {
            int step = g.S.tutorialStep;
            bool battleHint = g.BattleActive && !g.S.battleTutorialDone;
            bool show = (step < 7 || battleHint) && g.ModalCount == 0 && !g.Sleeping && !string.IsNullOrEmpty(g.S.starter);
            tutBubble.gameObject.SetActive(show);
            if (!show) { tutHand.gameObject.SetActive(false); return; }
            tutText.text = battleHint ? Loc.T("ui.battle_hint") : Loc.T("tut." + Mathf.Min(step, 6));
            if (step == 6 && !battleHint)
            {
                tutDoneTimer += dt;
                if (tutDoneTimer > 6f) g.AdvanceTutorial(7);
            }
            Vector2? target = null;
            if (battleHint)
            {
                for (int i = 0; i < g.S.tiles.Count; i++)
                    if (g.IsDefenderReady(g.S.tiles[i])) { target = TileToCanvas(i % g.W, i / g.W, 1.3f); break; }
            }
            else if (step == 5) target = ScreenToCanvas(RectTransformUtility.WorldToScreenPoint(null, binBtnRt.position)) + new Vector2(0, 110);
            else if (step < 5)
            {
                var tile = g.TutorialTile();
                if (tile.x >= 0) target = TileToCanvas(tile.x, tile.y, 1.0f);
            }
            tutHand.gameObject.SetActive(target.HasValue);
            if (target.HasValue)
            {
                tutHand.anchorMin = tutHand.anchorMax = new Vector2(0.5f, 0.5f);
                tutHand.anchoredPosition = target.Value + new Vector2(0, Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 30f);
                tutHand.localRotation = Quaternion.Euler(0, 0, 180);
            }
        }

        // ------------------------------------------------------------------ Schilder ueber gesperrten Bereichen und Baustellen

        class WorldLabel { public RectTransform rt; public Text text; public Vector3 world; }
        readonly Dictionary<string, WorldLabel> labels = new Dictionary<string, WorldLabel>();

        WorldLabel Label(string key)
        {
            if (labels.TryGetValue(key, out var l)) return l;
            var p = UIKit.Panel(worldLabels, "Label_" + key, "dark");
            p.raycastTarget = false;
            l = new WorldLabel { rt = p.rectTransform };
            l.rt.anchorMin = l.rt.anchorMax = new Vector2(0.5f, 0.5f);
            l.rt.sizeDelta = new Vector2(300, 74);
            l.text = UIKit.Txt(p.transform, "", 32, Color.white, TextAnchor.MiddleCenter, true);
            l.text.rectTransform.Stretch(10, 10, 4, 8);
            l.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            labels[key] = l;
            return l;
        }

        void RefreshWorldLabels()
        {
            foreach (var a in g.B.areas)
            {
                var l = Label("area_" + a.id);
                bool locked = !g.IsAreaUnlocked(a);
                l.rt.gameObject.SetActive(locked);
                if (!locked) continue;
                l.world = new Vector3(a.center.x + 0.5f, a.center.y + 1.5f, 0);
                bool ok = g.AreaRequirementMet(a, out var reason);
                l.text.text = Loc.T("area." + a.id) + "\n" + (ok ? Fmt.Num(a.coins) + " " + Loc.T("res.coins") : reason);
                l.text.color = ok ? Balance.Hex("#B8FF8A") : Color.white;
                l.rt.sizeDelta = new Vector2(ok ? 330 : 360, 110);
            }
            var c = g.S.construction;
            var cl = Label("construction");
            cl.rt.gameObject.SetActive(c.Active);
            if (c.Active && FarmView.I != null)
            {
                var sr = c.building == "house" ? FarmView.I.House : FarmView.I.Bin;
                cl.world = new Vector3(sr.bounds.center.x, sr.bounds.max.y + 0.4f, 0);
                cl.text.text = Loc.T("ui.under_construction") + "\n" + Loc.T("ui.construction_left", c.clicksLeft);
                cl.rt.sizeDelta = new Vector2(560, 110);
            }
        }

        void UpdateWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var kv in labels)
            {
                var l = kv.Value;
                if (!l.rt.gameObject.activeSelf) continue;
                var sp = cam.WorldToScreenPoint(l.world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(worldLabels, sp, null, out var local);
                l.rt.anchoredPosition = local;
                float s = Mathf.Clamp(6f / cam.orthographicSize, 0.55f, 1.1f);
                l.rt.localScale = Vector3.one * s;
            }
        }
    }
}
