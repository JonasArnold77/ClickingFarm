using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Fenster der Waffenbank (unten, blockiert nichts): Waffe, Stufe, was die Stufe bringt, Schlaege bis zur naechsten
    /// Stufe, ein grosser "Hämmern!"-Knopf und kleine Knoepfe, um die Waffe zu wechseln, an der geschmiedet wird.
    /// Solange es offen ist, haemmert auch ein Tipp auf die Bank in der Welt. Ohne Tipp blendet es nach ein paar Sekunden aus.
    /// </summary>
    public partial class WorldUI
    {
        CanvasGroup benchGroup;
        RectTransform benchPanel;
        Image benchIcon, benchBarFill;
        Text benchName, benchLevel, benchStats, benchBarText, benchActText;
        readonly List<(WeaponDef def, Image img, Text txt)> benchSlots = new List<(WeaponDef, Image, Text)>();
        float benchTimer;
        string benchIconFor;

        void BuildBenchInfo()
        {
            var B = PlantBalance.I;
            if (B.weapons == null || B.weapons.Length == 0) return;
            var root = UIKit.Rect(safe, "BenchInfo");
            root.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 195), new Vector2(-40, 470));
            benchGroup = root.gameObject.AddComponent<CanvasGroup>();
            benchGroup.alpha = 0f; benchGroup.blocksRaycasts = false;
            Panel(root, "Bg", PanelCol).rectTransform.Stretch();
            benchPanel = root;

            var glow = UIKit.Rect(root, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = WorldBuilder.Mist; glow.raycastTarget = false; glow.color = new Color(1f, 0.75f, 0.45f, 0.5f);
            glow.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -120), new Vector2(230, 200));
            benchIcon = UIKit.Rect(root, "Icon").gameObject.AddComponent<Image>();
            benchIcon.preserveAspect = true; benchIcon.raycastTarget = false;
            benchIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -120), new Vector2(210, 110));

            benchName = Label(root, "", 46, Ink, TextAnchor.MiddleLeft, true);
            benchName.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -22), new Vector2(-480, 64));
            benchLevel = Label(root, "", 44, AccentWarm, TextAnchor.MiddleRight, true);
            benchLevel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -22), new Vector2(240, 64));
            var sub = Label(root, "Waffenbank", 30, AccentWarm, TextAnchor.MiddleLeft, true);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -88), new Vector2(-280, 44));
            benchStats = Label(root, "", 28, Ink, TextAnchor.UpperLeft, true);
            benchStats.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -136), new Vector2(-280, 44));

            // Waffe wechseln
            var row = UIKit.HRow(root, "Weapons", 10, TextAnchor.MiddleCenter, new RectOffset(30, 30, 0, 0));
            row.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(0, 76));
            foreach (var w in B.weapons)
            {
                var wd = w;
                var b = Btn(row.transform, w.name, new Color(0.55f, 0.5f, 0.45f), () => PickBenchWeapon(wd), 24);
                b.GetComponent<RectTransform>().LE(0, 72, 1);
                benchSlots.Add((w, b.GetComponent<Image>(), b.GetComponentInChildren<Text>()));
            }

            var barBg = UIKit.Rect(root, "BarBg").gameObject.AddComponent<Image>();
            barBg.color = new Color(0.25f, 0.2f, 0.15f, 0.25f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-60, 46));
            benchBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            benchBarFill.raycastTarget = false; benchBarFill.color = new Color(1f, 0.62f, 0.3f);
            benchBarFill.rectTransform.anchorMin = Vector2.zero; benchBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            benchBarFill.rectTransform.offsetMin = benchBarFill.rectTransform.offsetMax = Vector2.zero;
            benchBarText = Label(barBg.transform, "", 28, Ink, TextAnchor.MiddleCenter, true);
            benchBarText.rectTransform.Stretch();

            var act = Btn(root, "Hämmern!", new Color(0.85f, 0.5f, 0.25f), () => { benchTimer = InfoShowTime; wb.Hammer(1f, true); }, 40);
            act.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 88), new Vector2(-60, 104));
            benchActText = act.GetComponentInChildren<Text>();

            var x = Btn(root, "X", new Color(0.8f, 0.45f, 0.35f), () => benchTimer = 0f, 40);
            x.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(96, 84));

            wb.BenchTapped += () =>
            {
                if (modal != null) return;
                if (benchTimer <= 0f) benchPanel.localScale = Vector3.one * 0.9f;
                benchTimer = InfoShowTime;
                HidePlantInfo();
            };
            wb.PlantTapped += _ => benchTimer = 0f;
            wb.WeaponLeveled += (w, lv) => Banner(w.name + " – Stufe " + lv + "!",
                "Schaden ×" + wb.WeaponDamageMult(w.id).ToString("0.##") + " · lädt " + Mathf.RoundToInt((1f - wb.WeaponCooldownMult(w.id)) * 100) + " % schneller nach");
        }

        void PickBenchWeapon(WeaponDef w)
        {
            benchTimer = InfoShowTime;
            bool unlocked = nw != null ? nw.IsWeaponUnlocked(w) : wb.Save.wave >= w.unlockWave;
            if (!unlocked) { Sfx.Play("error", 1.1f, 0.5f); hintText.text = w.name + " gibt es nach Welle " + w.unlockWave; return; }
            if (nw != null) nw.SelectWeapon(w.id); else { wb.Save.weapon = w.id; wb.Save.Save(); }
            Haptics.Tap(8);
        }

        void UpdateBenchInfo(float dt)
        {
            if (benchGroup == null) return;
            if (modal != null) benchTimer = 0f;
            benchTimer -= dt;
            bool show = benchTimer > 0f;
            benchGroup.alpha = Mathf.MoveTowards(benchGroup.alpha, show ? 1f : 0f, dt * (show ? 8f : 2.5f));
            benchGroup.blocksRaycasts = show;
            wb.BenchOpen = show;
            benchPanel.localScale = Vector3.one * Mathf.MoveTowards(benchPanel.localScale.x, 1f, dt * 1.5f);
            if (show) hintText.enabled = false;
            if (benchGroup.alpha <= 0f) return;

            var w = PlantBalance.I.Weapon(wb.Save.weapon);
            if (w == null) return;
            if (benchIconFor != w.id) { benchIconFor = w.id; benchIcon.sprite = WeaponArt.Icon(w.id); benchName.text = w.name; }
            int lv = wb.WeaponLv(w.id);
            bool maxed = wb.WeaponMaxed(w.id);
            benchLevel.text = "Stufe " + lv;
            benchStats.text = "Schaden ×" + wb.WeaponDamageMult(w.id).ToString("0.##") + " · Nachladen " + (w.cooldown * wb.WeaponCooldownMult(w.id)).ToString("0.00") + " s";
            int need = wb.WeaponTapsNeeded(w.id);
            float xp = wb.WeaponXp(w.id);
            benchBarFill.rectTransform.anchorMax = new Vector2(maxed ? 1f : Mathf.Clamp01(xp / need), 1f);
            benchBarText.text = maxed ? "Höchste Stufe erreicht" : "Schläge bis Stufe " + (lv + 1) + ": " + Mathf.FloorToInt(xp) + " / " + need;
            benchActText.text = maxed ? "Fertig geschmiedet" : "Hämmern!";
            foreach (var s in benchSlots)
            {
                bool unlocked = nw != null ? nw.IsWeaponUnlocked(s.def) : wb.Save.wave >= s.def.unlockWave;
                bool sel = s.def.id == w.id;
                s.img.color = sel ? AccentWarm : unlocked ? new Color(0.55f, 0.5f, 0.45f) : new Color(0.55f, 0.5f, 0.45f, 0.45f);
                s.txt.text = unlocked ? s.def.name + " " + wb.WeaponLv(s.def.id) : "Welle " + s.def.unlockWave;
            }
        }
    }
}
