using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Oberflaeche fuer Angriffspflanzen: Start-Auswahl (wie bei Pokemon), Pflanzenart waehlen,
    /// Pflanzenbuch (freigeschaltet / noch gesperrt), Anzeige der naechsten Freischaltung,
    /// Wellen-Anzeige mit Test-Knopf und Freischalt-Feier nach jeder Welle.
    /// </summary>
    public partial class WorldUI
    {
        static readonly Color PanelCol = new Color(0.98f, 0.95f, 0.88f, 0.97f);
        static readonly Color Ink = new Color(0.25f, 0.18f, 0.12f);
        static readonly Color InkSoft = new Color(0.45f, 0.36f, 0.28f);
        static readonly Color Accent = new Color(0.36f, 0.64f, 0.32f);
        static readonly Color AccentWarm = new Color(0.93f, 0.6f, 0.25f);

        Text waveText, goalText;
        Image goalIcon, plantSlotIcon;
        RectTransform houseBar, houseFill;
        GameObject modal;
        WorldBuilder wb;
        NightWave nw;

        void InitPlantsUI()
        {
            wb = WorldBuilder.I;
            nw = NightWave.I;
            BuildWaveHud();
            BuildGoalHud();
            BuildWeaponBar();
            if (wb == null) return;
            wb.PotTapped += OpenPotInfo;
            BuildJuice();
            wb.EmptyFieldTapped += c => OpenPlantPicker();
            wb.BookTapped += OpenPlantBook;
            wb.InventoryChanged += RefreshGoal;
            if (nw != null)
            {
                nw.EveningStarted += d => Banner("Der Abend dämmert …", "In " + Mathf.CeilToInt(d) + " s kommen die Zombies – mach dich bereit!");
                nw.WaveStarted += n => { CloseModal(); Banner("Die Nacht bricht herein", "Welle " + n + " – tippe auf Zombies, um zu schießen!"); };
                nw.WaveEnded += OpenWaveResult;
            }
            RefreshGoal();
            if (!wb.Save.starterChosen) OpenStarterChoice();
        }

        /// <summary>Wird von der Werkzeugleiste aufgerufen, wenn "Pflanzen" angetippt wird.</summary>
        void OnPlantToolTapped(bool alreadySelected)
        {
            if (wb == null) return;
            if (alreadySelected || string.IsNullOrEmpty(wb.Save.selectedPlant)) OpenPlantPicker();
        }

        // ------------------------------------------------------------------ Bausteine

        Text Label(Transform parent, string text, int size, Color col, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false) => UIKit.Txt(parent, text, size, col, a, bold);

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

        /// <summary>Karte einer Pflanzenart: Bild, Name, Sterne, fuenf Eigenschaften. Gesperrt = Silhouette.</summary>
        RectTransform PlantCard(Transform parent, CatalogDef def, Vector2 size, bool selected, bool locked, Action onClick, int fontSize = 30)
        {
            var g = def.Genome;
            var card = Panel(parent, "Card", locked ? new Color(0.85f, 0.82f, 0.78f, 0.9f) : selected ? new Color(1f, 0.93f, 0.7f) : new Color(1f, 1f, 1f, 0.92f));
            var rt = card.rectTransform;
            rt.sizeDelta = size;
            var le = card.LE(size.x, size.y); le.minWidth = 0;
            if (onClick != null)
            {
                var b = card.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Sfx.Play("click"); onClick(); });
                card.gameObject.AddComponent<Juicy>();
            }
            if (selected) { var o = card.gameObject.AddComponent<Outline>(); o.effectColor = AccentWarm; o.effectDistance = new Vector2(5, -5); }

            float imgH = size.y * 0.34f;
            if (!locked)
            {
                var glow = UIKit.Rect(rt, "Glow").gameObject.AddComponent<Image>();
                glow.sprite = WorldBuilder.Mist; var gc = g.ElementColor; glow.color = new Color(gc.r, gc.g, gc.b, 0.45f); glow.raycastTarget = false;
                glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(imgH * 1.3f, imgH));
            }
            var img = UIKit.Rect(rt, "Img").gameObject.AddComponent<Image>();
            img.sprite = SpeciesSprite(g); img.preserveAspect = true; img.raycastTarget = false;
            img.color = locked ? new Color(0.15f, 0.12f, 0.1f, 0.7f) : Color.Lerp(Color.white, g.ElementColor, 0.18f);
            img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(size.x * 0.7f, imgH - 8));

            var name = Label(rt, locked ? "???" : def.name, fontSize + 6, Ink, TextAnchor.MiddleCenter, true);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -imgH - 6), new Vector2(-16, fontSize + 14));
            if (locked)
            {
                var l = Label(rt, "Freigeschaltet nach Welle " + def.unlockWave, fontSize, InkSoft, TextAnchor.MiddleCenter);
                l.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -imgH - fontSize - 30), new Vector2(-20, fontSize * 2 + 10));
                return rt;
            }
            var stars = Label(rt, Dots(g.Stars), fontSize - 4, AccentWarm, TextAnchor.MiddleCenter);
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

        // ------------------------------------------------------------------ HUD: Welle, naechste Freischaltung

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

        void BuildGoalHud()
        {
            var p = Panel(safe, "Goal", new Color(0.12f, 0.1f, 0.08f, 0.55f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -150), new Vector2(520, 110));
            p.raycastTarget = false;
            goalIcon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            goalIcon.preserveAspect = true; goalIcon.raycastTarget = false;
            goalIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(84, 84));
            goalText = UIKit.Txt(p.transform, "", 30, Color.white, TextAnchor.MiddleLeft, true, true);
            goalText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(-120, 0));
        }

        void RefreshGoal()
        {
            if (wb == null || goalText == null) return;
            var next = wb.NextUnlock();
            if (next == null) { goalText.text = "Alle Pflanzen freigeschaltet!"; goalIcon.enabled = false; }
            else
            {
                goalText.text = "Nächste Pflanze:\nnach Welle " + next.unlockWave;
                goalIcon.enabled = true;
                goalIcon.sprite = SpeciesSprite(next.Genome);
                goalIcon.color = new Color(0.12f, 0.1f, 0.08f, 0.85f);   // als Silhouette, Spannung!
            }
            var sel = PlantBalance.I.Plant(wb.Save.selectedPlant);
            if (plantSlotIcon != null && sel != null) { plantSlotIcon.sprite = SpeciesSprite(sel.Genome); plantSlotIcon.color = Color.Lerp(Color.white, sel.Genome.ElementColor, 0.18f); }
        }

        void UpdatePlantsUI(float dt)
        {
            UpdateJuice(dt);
            if (nw == null || waveText == null) return;
            UpdateWeaponBar(dt);
            if (nw.Evening)
            {
                houseBar.gameObject.SetActive(false);
                waveText.text = "Abend – Welle " + nw.WaveNumber + "\nZombies in " + Mathf.CeilToInt(Mathf.Max(0, nw.EveningLeft)) + " s";
            }
            else if (nw.Active)
            {
                waveText.text = "Welle " + nw.WaveNumber + " – Morgen in " + Mathf.CeilToInt(Mathf.Max(0, nw.NightLeft)) + " s\nZombies übrig: " + nw.MonstersLeft;
                houseBar.gameObject.SetActive(true);
                UIKit.SetBar(houseFill, nw.HouseHp / Mathf.Max(1, nw.HouseHpMax));
            }
            else
            {
                houseBar.gameObject.SetActive(false);
                bool auto = PlantBalance.I.waves.autoWave && wb != null && wb.Save.starterChosen;
                waveText.text = "Welle " + nw.WaveNumber + (auto ? "\nAbend in " + Mathf.CeilToInt(Mathf.Max(0, nw.Countdown)) + " s" : "\n–");
            }
        }

        void Banner(string title, string sub) => StartCoroutine(BannerRoutine(title, sub));

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
            var options = new List<CatalogDef>();
            foreach (var c in PlantBalance.I.catalog) if (c.starter) options.Add(c);
            CatalogDef chosen = null;
            void Build()
            {
                OpenModal(out var panel, 110, 60);
                Header(panel, "Wähle deine erste Angriffspflanze!", false);
                var sub = Label(panel, "Weitere Pflanzen schaltest du frei, indem du Nachtwellen überstehst.", 30, InkSoft, TextAnchor.UpperLeft);
                sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(-80, 90));
                var col = UIKit.VCol(panel, "Cards", 14, TextAnchor.UpperCenter, new RectOffset(24, 24, 0, 0));
                col.GetComponent<RectTransform>().Stretch(0, 0, 220, 180);
                col.childForceExpandWidth = true; col.childControlWidth = true; col.childControlHeight = true;
                foreach (var c in options)
                {
                    var cc = c;
                    var rt = PlantCard(col.transform, c, new Vector2(900, 440), chosen == c, false, () => { chosen = cc; Build(); }, 28);
                    var le = rt.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = -1; le.flexibleWidth = 1;
                }
                var b = Btn(panel, chosen != null ? chosen.name + " nehmen!" : "Tippe eine Pflanze an", chosen != null ? Accent : new Color(0.6f, 0.6f, 0.6f), () =>
                {
                    if (chosen == null) return;
                    wb.ChooseStarter(chosen.id);
                    CloseModal();
                    RefreshGoal();
                    Banner(chosen.name + " ist dein Partner!", "Lege Felder an und setze sie mit „Pflanzen“ ein.");
                    SelectTool(WorldTool.Field);
                }, 44);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(760, 120));
                b.interactable = chosen != null;
            }
            Build();
        }

        // ------------------------------------------------------------------ Pflanzenart waehlen / Infos / Pflanzenbuch

        void OpenPlantPicker()
        {
            OpenModal(out var panel, 420, 360);
            Header(panel, "Welche Pflanze setzen?");
            HScroll(panel, "List", out var content).Stretch(20, 20, 130, 30);
            foreach (var c in PlantBalance.I.catalog)
            {
                if (!wb.IsUnlocked(c.id)) continue;
                var cc = c;
                PlantCard(content, c, new Vector2(400, 640), wb.Save.selectedPlant == c.id, false, () =>
                {
                    wb.SelectPlant(cc.id);
                    SelectTool(WorldTool.Plant);
                    CloseModal();
                    hintText.text = cc.name + " ausgewählt – tippe oder wische über leere Felder";
                }, 26);
            }
        }

        void OpenPlantInfo(PlantedState p)
        {
            var def = PlantBalance.I.Plant(p.type);
            if (def == null) return;
            var g = def.Genome;
            OpenModal(out var panel, 380, 300);
            Header(panel, def.name);
            var card = PlantCard(panel, def, new Vector2(700, 640), false, false, null, 32);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1); card.anchoredPosition = new Vector2(0, -130);
            var stats = Label(panel, $"Schaden {g.Damage:0.0} · alle {g.Cooldown:0.00} s · Reichweite {g.Range:0.0}", 32, InkSoft, TextAnchor.MiddleCenter);
            stats.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, p.Grown ? 290 : 160), new Vector2(-40, 50));
            bool armed = false;
            Button dig = null;
            dig = Btn(panel, "Ausgraben", new Color(0.7f, 0.5f, 0.3f), () =>
            {
                if (!armed) { armed = true; dig.GetComponentInChildren<Text>().text = "Wirklich? Wachstum geht verloren"; return; }
                wb.Unplant(p); CloseModal();
            });
            dig.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(640, 110));
        }

        void OpenPotInfo(PotState p)
        {
            var def = PlantBalance.I.Plant(p.type);
            if (def == null) return;
            var g = def.Genome;
            OpenModal(out var panel, 380, 300);
            Header(panel, def.name + (p.Lv > 1 ? " · Stufe " + p.Lv : " im Topf"));
            var card = PlantCard(panel, def, new Vector2(700, 640), false, false, null, 32);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1); card.anchoredPosition = new Vector2(0, -130);
            var B = PlantBalance.I;
            var stats = Label(panel, $"Schaden {g.Damage * B.LevelDamage(p.Lv):0.0} · alle {g.Cooldown * B.LevelCooldown(p.Lv):0.00} s · Reichweite {g.Range + B.LevelRange(p.Lv):0.0}", 32, InkSoft, TextAnchor.MiddleCenter);
            stats.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(-40, 50));
            bool armed = false;
            Button back = null;
            back = Btn(panel, "Pflanze entfernen", new Color(0.7f, 0.5f, 0.3f), () =>
            {
                if (!armed) { armed = true; back.GetComponentInChildren<Text>().text = "Wirklich? Die Pflanze ist dann weg"; return; }
                wb.ClearPot(p); CloseModal();
            }, 38);
            back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(640, 110));
        }

        void OpenPlantBook()
        {
            OpenModal(out var panel, 120, 50);
            Header(panel, "Pflanzenbuch");
            int unlocked = 0; foreach (var c in PlantBalance.I.catalog) if (wb.IsUnlocked(c.id)) unlocked++;
            var sub = Label(panel, "Freigeschaltet: " + unlocked + " / " + PlantBalance.I.catalog.Length, 32, InkSoft, TextAnchor.MiddleLeft, true);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(-80, 50));
            UIKit.Scroll(panel, out var scroll);
            scroll.GetComponent<RectTransform>().Stretch(20, 20, 180, 30);
            var gridRt = UIKit.Rect(scroll.content, "Grid");
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            var cell = new Vector2(440, 600);
            grid.cellSize = cell; grid.spacing = new Vector2(14, 14);
            // Spaltenbreite folgt der sichtbaren Breite (Handys haben unterschiedliche Seitenverhaeltnisse)
            var fit = gridRt.gameObject.AddComponent<FitGridColumns>();
            fit.view = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
            fit.maxWidth = 470f;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            foreach (var c in PlantBalance.I.catalog)
            {
                bool locked = !wb.IsUnlocked(c.id);
                var cc = c;
                PlantCard(gridRt, c, cell, !locked && wb.Save.selectedPlant == c.id, locked,
                    locked ? (Action)null : () => { wb.SelectPlant(cc.id); SelectTool(WorldTool.Plant); CloseModal(); }, 26);
            }
        }

        // ------------------------------------------------------------------ Wellen-Ergebnis + Freischaltung

        void OpenWaveResult(WaveResult r)
        {
            OpenModal(out var panel, r.unlocked.Count > 0 ? 260 : 560, r.unlocked.Count > 0 ? 220 : 520);
            Header(panel, r.victory ? "Nacht überstanden!" : "Der Hof wurde überrannt");
            string txt = $"Besiegte Zombies: {r.kills}";
            if (!r.victory) txt += $"\nVerlorenes Holz: {r.woodLost}";
            foreach (var w in r.weapons) txt += "\nNeue Waffe: " + w.name + "!";
            if (r.weapons.Count > 0) { Banner("Neue Waffe: " + r.weapons[0].name, r.weapons[0].desc); }
            var t = Label(panel, txt, 40, Ink, TextAnchor.UpperCenter);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(-80, 120));
            if (r.unlocked.Count > 0)
            {
                var u = Label(panel, "Neu freigeschaltet!", 48, AccentWarm, TextAnchor.MiddleCenter, true);
                u.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(-40, 70));
                var card = PlantCard(panel, r.unlocked[0], new Vector2(640, 620), true, false, null, 30);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1); card.anchoredPosition = new Vector2(0, -310);
                StartCoroutine(PopIn(card));
                Sfx.Play("levelup");
                Haptics.Tap(50);
                var sel = Btn(panel, r.unlocked[0].name + " auswählen", Accent, () => { wb.SelectPlant(r.unlocked[0].id); SelectTool(WorldTool.Plant); CloseModal(); }, 40);
                sel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(640, 110));
            }
            else
            {
                var b = Btn(panel, "Weiter", Accent, CloseModal, 44);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(480, 110));
            }
            RefreshGoal();
        }
    }

    /// <summary>Passt die Spaltenbreite eines GridLayoutGroup an die Breite des sichtbaren Bereichs an.</summary>
    public class FitGridColumns : MonoBehaviour
    {
        public RectTransform view;
        public float maxWidth = 470f;
        GridLayoutGroup grid;
        float lastW = -1;

        void Awake() => grid = GetComponent<GridLayoutGroup>();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        void Fit()
        {
            if (grid == null || view == null) return;
            float w = view.rect.width;
            if (Mathf.Approximately(w, lastW) || w <= 0) return;
            lastW = w;
            int n = Mathf.Max(1, grid.constraintCount);
            float cw = Mathf.Min(maxWidth, (w - grid.padding.horizontal - grid.spacing.x * (n - 1) - 8f) / n);
            grid.cellSize = new Vector2(cw, grid.cellSize.y);
        }
    }
}
