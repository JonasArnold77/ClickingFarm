using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Bauwerke in der Oberflaeche: Laden-Reiter "Bauwerke" (kaufen, gesperrte mit Hinweis), Hinweis beim Aufstellen und
    /// das Fenster eines Bauwerks (unten, blockiert nichts) mit Stufe, Wirkung, Fortschritt, "Verbessern!" und "Versetzen".
    /// Solange das Fenster offen ist, verbessert auch ein Tipp auf das Bauwerk in der Welt.
    /// </summary>
    public partial class WorldUI
    {
        CanvasGroup structGroup;
        RectTransform structPanel;
        Image structIcon, structBarFill;
        Text structName, structLevel, structDesc, structStats, structBarText, structActText;
        StructureState structShown;
        float structTimer;

        void BuildStructureInfo()
        {
            var root = UIKit.Rect(safe, "StructureInfo");
            root.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 195), new Vector2(-40, 470));
            structGroup = root.gameObject.AddComponent<CanvasGroup>();
            structGroup.alpha = 0f; structGroup.blocksRaycasts = false;
            Panel(root, "Bg", PanelCol).rectTransform.Stretch();
            structPanel = root;

            var glow = UIKit.Rect(root, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = WorldBuilder.Mist; glow.raycastTarget = false; glow.color = new Color(1f, 0.9f, 0.6f, 0.5f);
            glow.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(230, 230));
            structIcon = UIKit.Rect(root, "Icon").gameObject.AddComponent<Image>();
            structIcon.preserveAspect = true; structIcon.raycastTarget = false;
            structIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(190, 200));

            structName = Label(root, "", 46, Ink, TextAnchor.MiddleLeft, true);
            structName.resizeTextForBestFit = true; structName.resizeTextMinSize = 26; structName.resizeTextMaxSize = 46;
            structName.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -22), new Vector2(-560, 64));
            structLevel = Label(root, "", 44, AccentWarm, TextAnchor.MiddleRight, true);
            structLevel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -22), new Vector2(240, 64));
            structDesc = Label(root, "", 29, InkSoft, TextAnchor.UpperLeft);
            structDesc.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -92), new Vector2(-280, 84));
            structStats = Label(root, "", 28, Ink, TextAnchor.UpperLeft, true);
            structStats.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -180), new Vector2(-280, 70));

            var barBg = UIKit.Rect(root, "BarBg").gameObject.AddComponent<Image>();
            barBg.color = new Color(0.25f, 0.2f, 0.15f, 0.25f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-60, 46));
            structBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            structBarFill.raycastTarget = false; structBarFill.color = new Color(0.95f, 0.75f, 0.3f);
            structBarFill.rectTransform.anchorMin = Vector2.zero; structBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            structBarFill.rectTransform.offsetMin = structBarFill.rectTransform.offsetMax = Vector2.zero;
            structBarText = Label(barBg.transform, "", 28, Ink, TextAnchor.MiddleCenter, true);
            structBarText.rectTransform.Stretch();

            var act = Btn(root, "Verbessern!", new Color(0.88f, 0.62f, 0.2f), () =>
            {
                if (structShown == null) return;
                structTimer = InfoShowTime;
                wb.UpgradeStructure(structShown, 1f, true);
            }, 40);
            act.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(30, 88), new Vector2(-330, 104));
            structActText = act.GetComponentInChildren<Text>();
            var move = Btn(root, "Versetzen", new Color(0.55f, 0.5f, 0.45f), () =>
            {
                if (structShown == null) return;
                var st = structShown;
                structTimer = 0f; structShown = null;
                wb.MoveStructure(st);
            }, 32);
            move.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 88), new Vector2(270, 104));

            var x = Btn(root, "X", new Color(0.8f, 0.45f, 0.35f), () => structTimer = 0f, 40);
            x.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(96, 84));

            wb.StructureTapped += st =>
            {
                if (modal != null) return;
                if (structTimer <= 0f || structShown != st) structPanel.localScale = Vector3.one * 0.9f;
                structShown = st;
                structTimer = InfoShowTime;
                // die anderen Fenster sofort weg, nicht ueberblenden
                HidePlantInfo(); infoGroup.alpha = 0f;
                benchTimer = 0f; if (benchGroup != null) benchGroup.alpha = 0f;
            };
            wb.PlantTapped += _ => { structTimer = 0f; structGroup.alpha = 0f; if (benchGroup != null) { benchTimer = 0f; benchGroup.alpha = 0f; } };
            wb.BenchTapped += () => { structTimer = 0f; structGroup.alpha = 0f; infoGroup.alpha = 0f; };
            wb.StructureLeveled += (d, st) => Banner(d.name + " – Stufe " + st.lv + "!", wb.StructSummary(st));
            wb.StructurePlacing += d =>
            {
                if (d != null) { CloseModal(); hintText.text = WorldBuilder.IsTotem(d) ? d.name + ": Ecke antippen, Reihe aufziehen, in die Breite ziehen" : "Tippe auf einen freien Platz im Hof: " + d.name + " aufstellen"; }
                else hintText.text = ToolHint(WorldCamera.Tool);
            };
        }

        void UpdateStructureInfo(float dt)
        {
            if (structGroup == null) return;
            if (modal != null || (structShown != null && !structShown.placed)) structTimer = 0f;
            structTimer -= dt;
            bool show = structTimer > 0f && structShown != null;
            structGroup.alpha = Mathf.MoveTowards(structGroup.alpha, show ? 1f : 0f, dt * (show ? 8f : 2.5f));
            structGroup.blocksRaycasts = show;
            wb.OpenStructure = show ? structShown : null;
            structPanel.localScale = Vector3.one * Mathf.MoveTowards(structPanel.localScale.x, 1f, dt * 1.5f);
            if (show) hintText.enabled = false;
            else if (wb.PlacingStructure != null) hintText.enabled = true;
            if (structGroup.alpha <= 0f || structShown == null) return;

            var st = structShown;
            var d = PlantBalance.I.Structure(st.id);
            if (d == null) return;
            structIcon.sprite = WorldBuilder.StructSprite(st);
            structName.text = d.name;
            structLevel.text = "Stufe " + st.lv;
            structDesc.text = d.desc;
            structStats.text = wb.StructSummary(st);
            bool maxed = wb.StructMaxed(st);
            int need = wb.StructTapsNeeded(st);
            structBarFill.rectTransform.anchorMax = new Vector2(maxed ? 1f : Mathf.Clamp01(st.xp / need), 1f);
            structBarText.text = maxed ? "Höchste Stufe erreicht" : "Bis Stufe " + (st.lv + 1) + ": " + Mathf.FloorToInt(st.xp) + " / " + need;
            structActText.text = maxed ? "Fertig ausgebaut" : "Verbessern!";
        }

        /// <summary>Laden-Reiter "Bauwerke".</summary>
        void StructureShop(Transform content)
        {
            var wbi = WorldBuilder.I;
            var defs = PlantBalance.I.structures;
            if (defs == null || defs.Length == 0) { Label(content, "Noch keine Bauwerke.", 30, InkSoft, TextAnchor.MiddleCenter).LE(-1, 120); return; }
            foreach (var d in defs)
            {
                var dd = d;
                var st = wbi.Struct(d.id);
                float cost = wbi.StructCost(d);
                string price = Num.Short(cost) + (d.wood > 0 ? " + " + d.wood + " Holz" : "");
                if (st != null && !d.multi)
                {
                    ShopRow(content, d.name + "  · Stufe " + st.lv, st.placed ? wbi.StructSummary(st) : "Wartet auf seinen Platz", "Gebaut", false, () => { });
                    continue;
                }
                if (wbi.Earned < d.unlockEarned)
                {
                    ShopRow(content, d.name, "Ab " + Num.Short(d.unlockEarned) + " verdienten Münzen", price, false, () => { });
                    continue;
                }
                bool ok = wbi.Coins >= cost && wbi.Wood >= d.wood && wbi.PlacingStructure == null;
                int have = d.multi ? wbi.StructCount(d.id) : 0;
                string title = d.name + (have > 0 ? "  · " + have + " aufgestellt" : "");
                string hint = WorldBuilder.IsTotem(dd) ? "Zieh seine Fläche über deine Felder auf – wie beim Felder anlegen" : "Tippe auf einen freien Platz im Hof";
                ShopRow(content, title, d.desc, price, ok, () => { if (wbi.BuyStructure(dd)) Banner(dd.name + " gekauft!", hint); });
            }
        }
    }
}
