using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Fenster des Runen-Altars (unten, blockiert nichts): Runensteine, Fluch-Stufe der naechsten Welle, was sie bewirkt
    /// (staerker und groesser – aber mehr Beute), "Rune opfern" und "Zurücknehmen". Beginnt eine verfluchte Nacht,
    /// kommt ein Banner.
    /// </summary>
    public partial class WorldUI
    {
        CanvasGroup altarGroup;
        RectTransform altarPanel;
        Text altarRunesText, altarLevelText, altarEffectText, altarActText;
        Image altarBarFill;
        float altarTimer;

        void BuildAltarInfo()
        {
            var root = UIKit.Rect(safe, "AltarInfo");
            root.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 195), new Vector2(-40, 470));
            altarGroup = root.gameObject.AddComponent<CanvasGroup>();
            altarGroup.alpha = 0f; altarGroup.blocksRaycasts = false;
            Panel(root, "Bg", PanelCol).rectTransform.Stretch();
            altarPanel = root;

            var glow = UIKit.Rect(root, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = WorldBuilder.Mist; glow.raycastTarget = false; glow.color = new Color(0.8f, 0.5f, 1f, 0.55f);
            glow.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(230, 230));
            var icon = UIKit.Rect(root, "Icon").gameObject.AddComponent<Image>();
            icon.sprite = WorldBuilder.AltarSprite; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(200, 180));

            var name = Label(root, "Runen-Altar", 46, Ink, TextAnchor.MiddleLeft, true);
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -22), new Vector2(-560, 64));
            altarRunesText = Label(root, "", 36, new Color(0.55f, 0.3f, 0.75f), TextAnchor.MiddleRight, true);
            altarRunesText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -22), new Vector2(330, 64));
            altarLevelText = Label(root, "", 32, AccentWarm, TextAnchor.MiddleLeft, true);
            altarLevelText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -88), new Vector2(-280, 44));
            altarEffectText = Label(root, "", 27, InkSoft, TextAnchor.UpperLeft);
            altarEffectText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -136), new Vector2(-280, 100));

            // Fluch-Leiste: wie viele von hoechstens maxLevel Runen liegen auf dem Altar
            var barBg = UIKit.Rect(root, "BarBg").gameObject.AddComponent<Image>();
            barBg.color = new Color(0.25f, 0.2f, 0.15f, 0.25f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-60, 46));
            altarBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            altarBarFill.raycastTarget = false; altarBarFill.color = new Color(0.7f, 0.4f, 0.95f);
            altarBarFill.rectTransform.anchorMin = Vector2.zero; altarBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            altarBarFill.rectTransform.offsetMin = altarBarFill.rectTransform.offsetMax = Vector2.zero;

            var act = Btn(root, "Rune opfern", new Color(0.6f, 0.35f, 0.85f), () => { altarTimer = InfoShowTime * 2f; wb.OfferRune(); }, 40);
            act.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(30, 88), new Vector2(-330, 104));
            altarActText = act.GetComponentInChildren<Text>();
            var back = Btn(root, "Zurücknehmen", new Color(0.55f, 0.5f, 0.45f), () => { altarTimer = InfoShowTime * 2f; wb.TakeBackRune(); }, 28);
            back.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 88), new Vector2(270, 104));

            var x = Btn(root, "X", new Color(0.8f, 0.45f, 0.35f), () => altarTimer = 0f, 40);
            x.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(96, 84));

            wb.AltarTapped += () =>
            {
                if (modal != null) return;
                if (altarTimer <= 0f) altarPanel.localScale = Vector3.one * 0.9f;
                altarTimer = InfoShowTime * 2f;
                // die anderen Fenster sofort weg
                HidePlantInfo(); infoGroup.alpha = 0f;
                benchTimer = 0f; if (benchGroup != null) benchGroup.alpha = 0f;
                structTimer = 0f; if (structGroup != null) structGroup.alpha = 0f;
            };
            wb.PlantTapped += _ => { altarTimer = 0f; altarGroup.alpha = 0f; };
            wb.BenchTapped += () => { altarTimer = 0f; altarGroup.alpha = 0f; };
            wb.StructureTapped += _ => { altarTimer = 0f; altarGroup.alpha = 0f; };
            if (nw != null)
                nw.WaveStarted += w =>
                {
                    altarTimer = 0f;
                    if (nw.Curse > 0) Banner("Fluchnacht! Stufe " + nw.Curse, "Größere, stärkere Zombies – aber ×" + (1f + PlantBalance.I.Altar.coinsPerLevel * nw.Curse).ToString("0.#") + " Beute");
                };
        }

        void UpdateAltarInfo(float dt)
        {
            if (altarGroup == null) return;
            if (modal != null || (nw != null && nw.Active)) altarTimer = 0f;
            altarTimer -= dt;
            bool show = altarTimer > 0f;
            altarGroup.alpha = Mathf.MoveTowards(altarGroup.alpha, show ? 1f : 0f, dt * (show ? 8f : 2.5f));
            altarGroup.blocksRaycasts = show;
            wb.AltarOpen = show;
            altarPanel.localScale = Vector3.one * Mathf.MoveTowards(altarPanel.localScale.x, 1f, dt * 1.5f);
            if (show) hintText.enabled = false;
            if (altarGroup.alpha <= 0f) return;

            var A = PlantBalance.I.Altar;
            int lv = wb.AltarLevel;
            altarRunesText.text = "Runensteine: " + wb.Runes;
            altarLevelText.text = lv > 0 ? "Nächste Welle: Fluch " + lv + " / " + A.maxLevel : "Nächste Welle: normal";
            altarEffectText.text = lv > 0
                ? "Zombies: Leben ×" + (1f + A.hpPerLevel * lv).ToString("0.#") + ", Schaden ×" + (1f + A.damagePerLevel * lv).ToString("0.#") + ", größer, +" + Mathf.RoundToInt(A.countPerLevel * lv * 100) + " % mehr\n"
                  + "Beute ×" + (1f + A.coinsPerLevel * lv).ToString("0.#") + " · Bonus beim Überstehen · mehr Runensteine"
                : "Opfere Runensteine: die nächste Welle wird gefährlicher – und viel ertragreicher. Runensteine fallen von Zombies und aus Felsen.";
            altarBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(lv / (float)Mathf.Max(1, A.maxLevel)), 1f);
            altarActText.text = lv >= A.maxLevel ? "Altar ist voll" : wb.Runes <= 0 ? "Keine Runensteine" : "Rune opfern";
        }
    }
}
