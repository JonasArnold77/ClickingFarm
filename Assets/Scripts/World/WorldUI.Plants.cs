using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Oberflaeche fuer Angriffspflanzen: Start-Auswahl (wie bei Pokemon), Pflanze einsetzen,
    /// Pflanzen-Infos, Werkbank (Veredeln mit Samen, Kreuzen zweier Pflanzen),
    /// Wellen-Anzeige mit Test-Knopf und Beute, die in die Anzeige fliegt.
    /// </summary>
    public partial class WorldUI
    {
        static readonly Color PanelCol = new Color(0.98f, 0.95f, 0.88f, 0.97f);
        static readonly Color Ink = new Color(0.25f, 0.18f, 0.12f);
        static readonly Color InkSoft = new Color(0.45f, 0.36f, 0.28f);
        static readonly Color Accent = new Color(0.36f, 0.64f, 0.32f);
        static readonly Color AccentWarm = new Color(0.93f, 0.6f, 0.25f);

        Text waveText, seedText;
        RectTransform houseBar, houseFill, seedIconRt;
        GameObject modal;
        readonly List<(Image img, Vector2 from, RectTransform to, float t)> flyers = new List<(Image, Vector2, RectTransform, float)>();
        WorldBuilder wb;
        NightWave nw;

        // Werkbank-Zustand
        int benchTab;
        PlantGenome benchPlant, benchPartner;
        Gene benchSeed;

        void InitPlantsUI()
        {
            wb = WorldBuilder.I;
            nw = NightWave.I;
            BuildWaveHud();
            BuildInventoryHud();
            if (wb == null) return;
            wb.EmptyFieldTapped += OpenPlantPicker;
            wb.PlantTapped += OpenPlantInfo;
            wb.WorkbenchTapped += OpenWorkbench;
            wb.InventoryChanged += RefreshCounts;
            if (nw != null)
            {
                nw.WaveStarted += n => { CloseModal(); Banner("Die Nacht bricht herein", "Welle " + n + " – verteidige den Hof!"); };
                nw.WaveEnded += OpenWaveResult;
                nw.SeedDropped += (g, w) => FlyFromWorld(w, SeedIcon(g), seedIconRt);
                nw.PlantFound += (g, w) => { FlyFromWorld(w, SpeciesSprite(g), seedIconRt); Banner("Neue Angriffspflanze!", g.name + " gefunden"); };
            }
            RefreshCounts();
            if (!wb.Save.starterChosen) OpenStarterChoice();
        }

        // ------------------------------------------------------------------ Bausteine

        Text Label(Transform parent, string text, int size, Color col, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
        {
            var t = UIKit.Txt(parent, text, size, col, a, bold);
            return t;
        }

        Button Btn(Transform parent, string text, Color col, Action onClick, int size = 40)
        {
            var img = Panel(parent, "Btn", col);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors; cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f); b.colors = cb;
            img.gameObject.AddComponent<Juicy>();
            b.onClick.AddListener(() => { Sfx.Play("click"); Haptics.Tap(8); onClick?.Invoke(); });
            var t = UIKit.Txt(img.transform, text, size, Color.white, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Stretch(10, 10, 4, 6);
            return b;
        }

        GameObject OpenModal(out RectTransform panel, float marginTop = 140, float marginBottom = 80)
        {
            CloseModal();
            var root = UIKit.Rect(canvasRt, "Modal").Stretch();
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0.05f, 0.06f, 0.04f, 0.6f);
            var p = Panel(root, "Panel", PanelCol);
            panel = p.rectTransform;
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(28, marginBottom); panel.offsetMax = new Vector2(-28, -marginTop);
            modal = root.gameObject;
            StartCoroutine(PopIn(panel));
            Sfx.Play("pop", 0.9f, 0.6f);
            return modal;
        }

        System.Collections.IEnumerator PopIn(Transform t)
        {
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.2f)
            {
                if (t == null) yield break;
                float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                t.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, e);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        void CloseModal() { if (modal != null) Destroy(modal); modal = null; }

        void Header(RectTransform panel, string title, bool closable = true)
        {
            var t = Label(panel, title, title.Length > 22 ? 44 : 58, Ink, TextAnchor.MiddleLeft, true);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -24), new Vector2(-180, 90));
            if (closable)
            {
                var x = Btn(panel, "X", new Color(0.8f, 0.45f, 0.35f), CloseModal, 44);
                x.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(100, 90));
            }
        }

        Sprite SpeciesSprite(PlantGenome g)
        {
            var sp = PlantBalance.I.Species(g.species);
            return art != null ? art.Get(sp.art + sp.stages[sp.stages.Length - 1]) : null;
        }

        Sprite SeedIcon(Gene g)
        {
            string n = g.attr switch { PlantAttr.Pattern => "hh_corn_seedbag", PlantAttr.Range => "hh_wheat_seedbag", PlantAttr.Projectile => "hh_carrot_seedbag", PlantAttr.Element => "hh_corn_seedbag", _ => "hh_wheat_seedbag" };
            return art != null ? art.Get(n) : null;
        }

        static string Dots(int level) => new string('●', Mathf.Clamp(level, 0, 5)) + new string('○', Mathf.Clamp(5 - level, 0, 5));

        static Color AttrColor(Gene g)
        {
            if (g.attr == PlantAttr.Element)
            {
                var o = PlantBalance.I.Option(PlantAttr.Element, g.id);
                if (o != null && ColorUtility.TryParseHtmlString(o.color, out var c)) return c * 0.8f + new Color(0, 0, 0, 0.2f);
            }
            return InkSoft;
        }

        /// <summary>Karte einer Angriffspflanze: Bild, Name, Sterne, fuenf Eigenschaften mit Stufe.</summary>
        RectTransform PlantCard(Transform parent, PlantGenome g, Vector2 size, bool selected, Action onClick, int fontSize = 30)
        {
            var card = Panel(parent, "Card", selected ? new Color(1f, 0.93f, 0.7f) : new Color(1f, 1f, 1f, 0.9f));
            var rt = card.rectTransform;
            rt.sizeDelta = size;
            card.LE(size.x, size.y);
            if (onClick != null)
            {
                var b = card.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Sfx.Play("click"); onClick(); });
                card.gameObject.AddComponent<Juicy>();
            }
            if (selected) { var o = card.gameObject.AddComponent<Outline>(); o.effectColor = AccentWarm; o.effectDistance = new Vector2(5, -5); }

            float imgH = size.y * 0.34f;
            var glow = UIKit.Rect(rt, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = WorldBuilder.Mist; var gc = g.ElementColor; glow.color = new Color(gc.r, gc.g, gc.b, 0.45f); glow.raycastTarget = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(imgH * 1.3f, imgH));
            var img = UIKit.Rect(rt, "Img").gameObject.AddComponent<Image>();
            img.sprite = SpeciesSprite(g); img.preserveAspect = true; img.raycastTarget = false;
            img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(size.x * 0.7f, imgH - 8));

            var name = Label(rt, g.name, fontSize + 6, Ink, TextAnchor.MiddleCenter, true);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -imgH - 6), new Vector2(-16, fontSize + 14));
            var stars = Label(rt, Dots(g.Stars) + (g.planted ? "   (im Feld)" : ""), fontSize - 4, AccentWarm, TextAnchor.MiddleCenter);
            stars.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -imgH - fontSize - 18), new Vector2(-16, fontSize + 4));

            float y = -imgH - fontSize * 2 - 34;
            foreach (var a in PlantGenome.All)
            {
                var gene = g.Get(a);
                var row = Label(rt, PlantGenome.AttrName(a), fontSize - 6, InkSoft, TextAnchor.MiddleLeft);
                row.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(18, y), new Vector2(-30, fontSize));
                var val = Label(rt, PlantGenome.Describe(gene) + "  " + Dots(gene.level), fontSize - 6, AttrColor(gene), TextAnchor.MiddleRight, true);
                val.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, y), new Vector2(-30, fontSize));
                y -= fontSize + 6;
            }
            return rt;
        }

        RectTransform HScroll(Transform parent, string name, out RectTransform content)
        {
            var view = UIKit.Rect(parent, name);
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.04f);
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.vertical = false; sr.scrollSensitivity = 40;
            content = UIKit.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18; h.padding = new RectOffset(16, 16, 10, 10); h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = view;
            return view;
        }

        // ------------------------------------------------------------------ HUD: Welle, Samen

        void BuildWaveHud()
        {
            var p = Panel(safe, "Wave", new Color(0.12f, 0.1f, 0.08f, 0.55f));
            p.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(430, 236));
            p.raycastTarget = false;
            waveText = UIKit.Txt(p.transform, "", 34, Color.white, TextAnchor.UpperLeft, true, true);
            waveText.rectTransform.Stretch(24, 20, 16, 100);
            houseBar = UIKit.Bar(p.transform, "HouseHp", new Color(0, 0, 0, 0.35f), new Color(0.95f, 0.45f, 0.35f), out houseFill);
            houseBar.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(24, 102), new Vector2(-48, 24));
            var b = Btn(p.transform, "Welle starten (Test)", new Color(0.55f, 0.42f, 0.75f), () => nw?.StartWave(), 28);
            b.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(20, 16), new Vector2(-40, 70));
        }

        void BuildInventoryHud()
        {
            var p = Panel(safe, "Seeds", new Color(0.12f, 0.1f, 0.08f, 0.55f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -150), new Vector2(430, 100));
            p.raycastTarget = false;
            var icon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            icon.sprite = art != null ? art.Get("hh_corn_seedbag") : null; icon.preserveAspect = true; icon.raycastTarget = false;
            seedIconRt = icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55, 0), new Vector2(80, 80));
            seedText = UIKit.Txt(p.transform, "0", 44, Color.white, TextAnchor.MiddleLeft, true, true);
            seedText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(105, 0), new Vector2(-110, 0));
        }

        void RefreshCounts()
        {
            if (wb == null) return;
            int plants = 0; foreach (var _ in wb.Plants) plants++;
            seedText.text = wb.Save.seeds.Count + " Samen · " + plants + " Pfl.";
        }

        void UpdatePlantsUI(float dt)
        {
            if (nw != null && waveText != null)
            {
                if (nw.Active)
                {
                    waveText.text = "Welle " + nw.WaveNumber + "\nMonster übrig: " + nw.MonstersLeft;
                    houseBar.gameObject.SetActive(true);
                    UIKit.SetBar(houseFill, nw.HouseHp / Mathf.Max(1, nw.HouseHpMax));
                }
                else
                {
                    houseBar.gameObject.SetActive(false);
                    bool auto = PlantBalance.I.waves.autoWave && wb != null && wb.Save.starterChosen;
                    waveText.text = "Welle " + nw.WaveNumber + (auto ? "\nkommt in " + Mathf.CeilToInt(Mathf.Max(0, nw.Countdown)) + " s" : "\n–");
                }
            }
            // Beute fliegt in die Anzeige
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var (img, from, to, t) = flyers[i];
                t += dt / 0.8f;
                if (img == null || to == null) { flyers.RemoveAt(i); continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, RectTransformUtility.WorldToScreenPoint(null, to.position), null, out var target);
                float e = t * t;
                var ctrl = from + new Vector2(0, 300);
                var p = Vector2.Lerp(Vector2.Lerp(from, ctrl, e), Vector2.Lerp(ctrl, target, e), e);
                img.rectTransform.anchoredPosition = p;
                float s = t < 0.2f ? t / 0.2f * 1.3f : Mathf.Lerp(1.3f, 0.6f, (t - 0.2f) / 0.8f);
                img.rectTransform.localScale = Vector3.one * s;
                if (t >= 1f) { Destroy(img.gameObject); flyers.RemoveAt(i); Sfx.Play("coin", 1.3f, 0.5f); continue; }
                flyers[i] = (img, from, to, t);
            }
        }

        void FlyFromWorld(Vector2 world, Sprite sprite, RectTransform to)
        {
            var cam = Camera.main;
            if (cam == null || sprite == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, cam.WorldToScreenPoint(world), null, out var from);
            var img = UIKit.Rect(canvasRt, "Fly").gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(110, 110);
            img.rectTransform.anchoredPosition = from;
            flyers.Add((img, from, to, 0f));
        }

        void Banner(string title, string sub)
        {
            StartCoroutine(BannerRoutine(title, sub));
        }

        System.Collections.IEnumerator BannerRoutine(string title, string sub)
        {
            var t = UIKit.Txt(canvasRt, title, 76, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1060, 130));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var s = UIKit.Txt(canvasRt, sub, 42, Color.white, TextAnchor.MiddleCenter, false, true);
            s.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 205), new Vector2(1000, 80));
            Sfx.Play("unlock", 0.9f);
            for (float k = 0; k < 2.6f; k += Time.unscaledDeltaTime)
            {
                float a = k < 0.3f ? k / 0.3f : k > 2.1f ? 1 - (k - 2.1f) / 0.5f : 1;
                t.color = new Color(t.color.r, t.color.g, t.color.b, a); s.color = new Color(1, 1, 1, a);
                t.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0, 0.3f - k));
                yield return null;
            }
            Destroy(t.gameObject); Destroy(s.gameObject);
        }

        // ------------------------------------------------------------------ Start-Pflanze waehlen

        void OpenStarterChoice()
        {
            var rnd = new System.Random();
            var options = new List<PlantGenome>();
            for (int i = 0; i < 3; i++)
            {
                var g = Genetics.Random(0, rnd, 1, 2);
                // drei verschiedene Arten zur Auswahl
                g.species = PlantBalance.I.species[i % PlantBalance.I.species.Length].id;
                g.name = Genetics.MakeName(g);
                options.Add(g);
            }
            PlantGenome chosen = null;
            void Build()
            {
                OpenModal(out var panel, 110, 60);
                Header(panel, "Wähle deine erste Angriffspflanze!", false);
                var sub = Label(panel, "Jede hat zufällige Eigenschaften. Später kannst du sie kreuzen und verbessern.", 30, InkSoft, TextAnchor.UpperLeft);
                sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(-80, 90));
                var col = UIKit.VCol(panel, "Cards", 14, TextAnchor.UpperCenter, new RectOffset(24, 24, 0, 0));
                col.GetComponent<RectTransform>().Stretch(0, 0, 220, 180);
                col.childForceExpandWidth = true; col.childControlWidth = true; col.childControlHeight = true;
                foreach (var g in options)
                {
                    var gg = g;
                    var rt = PlantCard(col.transform, g, new Vector2(900, 440), chosen == g, () => { chosen = gg; Build(); }, 28);
                    var le = rt.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = -1; le.flexibleWidth = 1;
                }
                var b = Btn(panel, chosen != null ? chosen.name + " nehmen!" : "Tippe eine Pflanze an", chosen != null ? Accent : new Color(0.6f, 0.6f, 0.6f), () =>
                {
                    if (chosen == null) return;
                    chosen.uid = 0;
                    wb.AddPlant(chosen);
                    wb.Save.starterChosen = true;
                    wb.NotifyInventory();
                    CloseModal();
                    Banner(chosen.name + " ist dein Partner!", "Lege ein Feld an und setze sie mit „Pflanzen“ ein.");
                    SelectTool(WorldTool.Field);
                }, 44);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(760, 120));
                b.interactable = chosen != null;
            }
            Build();
        }

        // ------------------------------------------------------------------ Einsetzen / Infos

        void OpenPlantPicker(Cell cell)
        {
            OpenModal(out var panel, 520, 380);
            Header(panel, "Pflanze einsetzen");
            var free = new List<PlantGenome>();
            foreach (var g in wb.Plants) if (!g.planted) free.Add(g);
            if (free.Count == 0)
            {
                var t = Label(panel, "Keine freie Angriffspflanze.\nBesiege nachts Monster oder kreuze neue an der Werkbank.", 36, InkSoft, TextAnchor.MiddleCenter);
                t.rectTransform.Stretch(40, 40, 140, 40);
                return;
            }
            HScroll(panel, "List", out var content).Stretch(20, 20, 130, 30);
            foreach (var g in free)
            {
                var gg = g;
                PlantCard(content, g, new Vector2(420, 640), false, () => { wb.PlantInto(cell, gg); CloseModal(); }, 28);
            }
        }

        void OpenPlantInfo(PlantGenome g)
        {
            OpenModal(out var panel, 380, 300);
            Header(panel, g.name);
            var card = PlantCard(panel, g, new Vector2(700, 640), false, null, 32);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1); card.anchoredPosition = new Vector2(0, -130);
            var stats = Label(panel, $"Schaden {g.Damage:0.0} · alle {g.Cooldown:0.00} s · Reichweite {g.Range:0.0}", 32, InkSoft, TextAnchor.MiddleCenter);
            stats.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(-40, 50));
            var b = Btn(panel, "Ausgraben", new Color(0.7f, 0.5f, 0.3f), () => { wb.Unplant(g); CloseModal(); });
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(520, 110));
        }

        // ------------------------------------------------------------------ Werkbank

        void OpenWorkbench()
        {
            benchPlant = null; benchPartner = null; benchSeed = null;
            foreach (var g in wb.Plants) { benchPlant = g; break; }
            BuildWorkbench();
        }

        void BuildWorkbench()
        {
            OpenModal(out var panel, 120, 50);
            Header(panel, "Werkbank");
            // Reiter
            var tabs = UIKit.HRow(panel, "Tabs", 16, TextAnchor.MiddleLeft, new RectOffset(36, 36, 0, 0));
            tabs.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(0, 90));
            tabs.childForceExpandWidth = true;
            Btn(tabs.transform, "Veredeln", benchTab == 0 ? AccentWarm : new Color(0.6f, 0.55f, 0.5f), () => { benchTab = 0; BuildWorkbench(); }, 34).LE(-1, 84, 1);
            Btn(tabs.transform, "Kreuzen", benchTab == 1 ? AccentWarm : new Color(0.6f, 0.55f, 0.5f), () => { benchTab = 1; benchPartner = null; BuildWorkbench(); }, 34).LE(-1, 84, 1);

            var plants = new List<PlantGenome>(wb.Plants);
            if (plants.Count == 0)
            {
                var t = Label(panel, "Du hast noch keine Angriffspflanze.", 38, InkSoft, TextAnchor.MiddleCenter);
                t.rectTransform.Stretch(40, 40, 260, 40);
                return;
            }
            if (benchPlant == null || !plants.Contains(benchPlant)) benchPlant = plants[0];

            var l1 = Label(panel, "1. Pflanze wählen", 34, Ink, TextAnchor.MiddleLeft, true);
            l1.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -220), new Vector2(-80, 50));
            HScroll(panel, "Plants", out var plist).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(-40, 560));
            foreach (var g in plants)
            {
                var gg = g;
                PlantCard(plist, g, new Vector2(360, 540), g == benchPlant, () => { benchPlant = gg; if (benchPartner == gg) benchPartner = null; BuildWorkbench(); }, 26);
            }

            if (benchTab == 0) BuildInfuse(panel); else BuildCross(panel, plants);
        }

        void BuildInfuse(RectTransform panel)
        {
            var l2 = Label(panel, "2. Samen wählen (" + wb.Save.seeds.Count + ")", 34, Ink, TextAnchor.MiddleLeft, true);
            l2.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -845), new Vector2(-80, 50));
            if (wb.Save.seeds.Count == 0)
            {
                var t = Label(panel, "Noch keine Samen. Besiegte Monster lassen nachts Samen fallen.", 32, InkSoft, TextAnchor.UpperCenter);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -910), new Vector2(-80, 120));
                return;
            }
            if (benchSeed != null && !wb.Save.seeds.Contains(benchSeed)) benchSeed = null;
            HScroll(panel, "Seeds", out var slist).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -895), new Vector2(-40, 250));
            foreach (var s in wb.Save.seeds)
            {
                var ss = s;
                bool sel = s == benchSeed;
                var item = Panel(slist, "Seed", sel ? new Color(1f, 0.93f, 0.7f) : new Color(1, 1, 1, 0.9f));
                item.LE(260, 230);
                if (sel) { var o = item.gameObject.AddComponent<Outline>(); o.effectColor = AccentWarm; o.effectDistance = new Vector2(5, -5); }
                var b = item.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Sfx.Play("click"); benchSeed = ss; BuildWorkbench(); });
                item.gameObject.AddComponent<Juicy>();
                var ic = UIKit.Rect(item.transform, "Icon").gameObject.AddComponent<Image>();
                ic.sprite = SeedIcon(s); ic.preserveAspect = true; ic.raycastTarget = false;
                ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(110, 100));
                var a = Label(item.transform, PlantGenome.AttrName(s.attr), 24, InkSoft, TextAnchor.MiddleCenter);
                a.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 86), new Vector2(-10, 32));
                var v = Label(item.transform, PlantGenome.Describe(s), 26, AttrColor(s), TextAnchor.MiddleCenter, true);
                v.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(-10, 34));
                var d = Label(item.transform, Dots(s.level), 24, AccentWarm, TextAnchor.MiddleCenter);
                d.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-10, 30));
            }

            string preview = "Wähle einen Samen.";
            if (benchSeed != null)
            {
                var cur = benchPlant.Get(benchSeed.attr);
                var res = Genetics.Infuse(cur, benchSeed);
                preview = PlantGenome.AttrName(benchSeed.attr) + ":  " + PlantGenome.Describe(cur) + " " + Dots(cur.level) + "   →   " + PlantGenome.Describe(res) + " " + Dots(res.level);
            }
            var pv = Label(panel, preview, 32, Ink, TextAnchor.MiddleCenter, true);
            pv.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(-60, 70));
            var go = Btn(panel, "Veredeln", Accent, () =>
            {
                if (benchSeed == null) return;
                var res = Genetics.Infuse(benchPlant.Get(benchSeed.attr), benchSeed);
                benchPlant.Set(benchSeed.attr, res);
                benchPlant.name = Genetics.MakeName(benchPlant);
                wb.Save.seeds.Remove(benchSeed);
                benchSeed = null;
                wb.RefreshPlant(benchPlant);
                wb.NotifyInventory();
                Sfx.Play("levelup"); Haptics.Tap(30);
                BuildWorkbench();
                Banner("Veredelt!", benchPlant.name + " ist stärker geworden");
            }, 44);
            go.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(560, 120));
            go.interactable = benchSeed != null;
        }

        void BuildCross(RectTransform panel, List<PlantGenome> plants)
        {
            var l2 = Label(panel, "2. Partner wählen (wird dabei verbraucht)", 34, Ink, TextAnchor.MiddleLeft, true);
            l2.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -845), new Vector2(-80, 50));
            var others = plants.FindAll(p => p != benchPlant);
            if (others.Count == 0)
            {
                var t = Label(panel, "Du brauchst eine zweite Pflanze. Manche Monster lassen ganze Pflanzen fallen.", 32, InkSoft, TextAnchor.UpperCenter);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -910), new Vector2(-80, 120));
                return;
            }
            HScroll(panel, "Partners", out var list).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -895), new Vector2(-40, 560));
            foreach (var g in others)
            {
                var gg = g;
                PlantCard(list, g, new Vector2(360, 540), g == benchPartner, () => { benchPartner = gg; BuildWorkbench(); }, 26);
            }
            var info = Label(panel, "Jede Eigenschaft kommt von einem Elternteil – meist vom stärkeren. Gleiche Eigenschaften verstärken sich, manchmal gibt es eine Mutation.", 28, InkSoft, TextAnchor.MiddleCenter);
            info.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(-80, 90));
            var go = Btn(panel, "Kreuzen", Accent, () =>
            {
                if (benchPartner == null) return;
                var child = Genetics.Cross(benchPlant, benchPartner, wb.NextUid(), new System.Random());
                var donor = benchPartner;
                if (donor.planted) wb.Unplant(donor);
                wb.RemovePlant(donor);
                wb.ReplacePlant(benchPlant, child);
                benchPlant = child; benchPartner = null;
                Sfx.Play("levelup"); Haptics.Tap(35);
                BuildWorkbench();
                Banner("Gekreuzt!", "Neue Pflanze: " + child.name);
            }, 44);
            go.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(560, 120));
            go.interactable = benchPartner != null;
        }

        // ------------------------------------------------------------------ Wellen-Ergebnis

        void OpenWaveResult(WaveResult r)
        {
            OpenModal(out var panel, 560, 520);
            Header(panel, r.victory ? "Nacht überstanden!" : "Der Hof wurde überrannt");
            string txt = $"Besiegte Monster: {r.kills}\nGefundene Samen: {r.seeds}\nNeue Pflanzen: {r.plants}";
            if (!r.victory) txt += $"\nVerlorenes Holz: {r.woodLost}";
            var t = Label(panel, txt, 40, Ink, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(40, 40, 130, 170);
            var b = Btn(panel, "Weiter", Accent, CloseModal, 44);
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(480, 110));
            if (r.seeds > 0 || r.plants > 0)
            {
                var hint = Label(panel, "Verbessere deine Pflanzen an der Werkbank neben dem Haus.", 28, InkSoft, TextAnchor.MiddleCenter);
                hint.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(-60, 50));
            }
        }
    }
}
