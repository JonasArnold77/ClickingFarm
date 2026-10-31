using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Kleine Liste neben dem Mauszeiger, wenn er ueber einem geboosteten Feld steht: jede Quelle in ihrer Farbe.
    /// Die Linien von den Quellen zum Feld zeichnet WorldBuilder.Boosts.
    /// </summary>
    public partial class WorldUI
    {
        RectTransform boostTip;
        Text boostTipText;

        void BuildBoostTip()
        {
            var p = Panel(canvasRt, "BoostTip", new Color(0.1f, 0.08f, 0.06f, 0.82f));
            p.raycastTarget = false;
            boostTip = p.rectTransform;
            boostTip.anchorMin = boostTip.anchorMax = new Vector2(0.5f, 0.5f);
            boostTip.pivot = new Vector2(0f, 1f);
            boostTipText = UIKit.Txt(p.transform, "", 28, Color.white, TextAnchor.UpperLeft, false, true);
            boostTipText.supportRichText = true;
            boostTipText.rectTransform.Stretch();
            boostTipText.rectTransform.offsetMin = new Vector2(18, 10); boostTipText.rectTransform.offsetMax = new Vector2(-14, -10);
            boostTip.gameObject.SetActive(false);
        }

        void UpdateBoostTip()
        {
            if (boostTip == null) return;
            var src = wb.HoverSources;
            bool show = src.Count > 0 && modal == null;
            boostTip.gameObject.SetActive(show);
            if (!show) return;
            var sb = new System.Text.StringBuilder("<b>Geboostet durch:</b>");
            foreach (var s in src) sb.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(s.col)).Append(">●</color> ").Append(s.label);
            boostTipText.text = sb.ToString();
            boostTip.sizeDelta = new Vector2(620, 62 + 38 * src.Count);
            // neben den Zeiger, aber im Bild halten
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, Input.mousePosition, null, out var local);
            var half = canvasRt.rect.size * 0.5f;
            var pos = local + new Vector2(36, -30);
            if (pos.x + boostTip.sizeDelta.x > half.x) pos.x = local.x - 36 - boostTip.sizeDelta.x;
            if (pos.y - boostTip.sizeDelta.y < -half.y) pos.y = -half.y + boostTip.sizeDelta.y;
            boostTip.anchoredPosition = pos;
            boostTip.SetAsLastSibling();
        }
    }
}
