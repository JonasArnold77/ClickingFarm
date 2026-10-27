using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Bildschirm-Feedback fuer das Klick-Wachstum: Combo-Zaehler ueber der Pflanze (springt bei jedem Tipp),
    /// weisser Blitz ueber den ganzen Bildschirm, wenn eine Pflanze reif wird.
    /// </summary>
    public partial class WorldUI
    {
        Text comboText;
        Vector2 comboWorld;
        float comboPunch, comboAlpha, comboIdle, flashA;
        Image flashImg;

        void BuildJuice()
        {
            comboText = UIKit.Txt(canvasRt, "", 64, Color.white, TextAnchor.MiddleCenter, true, true);
            comboText.rectTransform.sizeDelta = new Vector2(400, 110);
            comboText.horizontalOverflow = HorizontalWrapMode.Overflow;
            comboText.raycastTarget = false;
            flashImg = UIKit.Rect(canvasRt, "Flash").Stretch().gameObject.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 0.95f, 0f);
            flashImg.raycastTarget = false;
            if (wb == null) return;
            wb.PlantPumped += (pos, combo, heat) =>
            {
                comboWorld = pos + new Vector2(0.45f, 0.25f);
                if (combo < 2) return;
                comboText.text = "×" + combo;
                comboText.color = Color.Lerp(new Color(1f, 1f, 0.9f), new Color(1f, 0.55f, 0.2f), heat);
                comboText.fontSize = Mathf.RoundToInt(Mathf.Lerp(54, 92, heat));
                comboPunch = 1f; comboAlpha = 1f; comboIdle = 0f;
            };
            wb.PlantGrown += p =>
            {
                flashA = 0.75f;
                comboAlpha = 0f;
                Banner(p.Genome.name + " ist reif!", "Gleich antippen und in einen Blumentopf setzen");
            };
            wb.PlacingChanged += h => hintText.text = h ?? ToolHint(WorldCamera.Tool);
        }

        void UpdateJuice(float dt)
        {
            if (comboText == null) return;
            comboIdle += dt;
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 7f);
            if (comboIdle > 0.6f) comboAlpha = Mathf.MoveTowards(comboAlpha, 0f, dt * 3f);
            var cam = Camera.main;
            if (cam != null && comboAlpha > 0f)
            {
                var sp = cam.WorldToScreenPoint(comboWorld);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
                comboText.rectTransform.anchoredPosition = local + new Vector2(0, comboPunch * 18f);
            }
            float e = 1f + comboPunch * comboPunch * 0.6f;
            comboText.rectTransform.localScale = Vector3.one * e;
            comboText.rectTransform.localRotation = Quaternion.Euler(0, 0, comboPunch * 8f);
            var c = comboText.color; c.a = comboAlpha; comboText.color = c;

            flashA = Mathf.MoveTowards(flashA, 0f, dt * 2.2f);
            flashImg.color = new Color(1f, 1f, 0.95f, flashA);
        }
    }
}
