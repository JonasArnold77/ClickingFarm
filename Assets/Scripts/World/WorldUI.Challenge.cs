using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Grosse Tafel mit dem Auftrag fuer heute (oben links, unter dem naechsten Ziel): welches Erntegut zaehlt, wie viele
    /// schon in der Kiste sind, ein Balken ueber alle drei Stufen mit Markierungen und darunter, was Bronze, Silber und
    /// Gold jeweils bringen. Dazu, wie lange der Auftrag noch laeuft (bis zur naechsten Nacht). Gewinne kommen als Banner.
    /// </summary>
    public partial class WorldUI
    {
        Image chIcon, chBarFill;
        Text chTitle, chText, chCount, chTime;
        Text[] chTierText = new Text[3];
        Image[] chMarks = new Image[2];
        RectTransform chPanel, chBarBg;
        float chPunch;

        static readonly Color BronzeUi = new Color(0.78f, 0.55f, 0.36f), SilverUi = new Color(0.82f, 0.85f, 0.92f), GoldUi = new Color(0.95f, 0.78f, 0.36f);

        void BuildChallengeHud()
        {
            var p = Panel(safe, "Challenge", new Color(0.12f, 0.1f, 0.08f, 0.68f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -900), new Vector2(620, 270));   // unter der Ziel-Liste
            p.raycastTarget = false;
            chPanel = p.rectTransform;
            chTitle = UIKit.Txt(p.transform, "Auftrag für heute", 32, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true, true);
            chTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -10), new Vector2(-220, 44));
            chTime = UIKit.Txt(p.transform, "", 22, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleRight, false, true);
            chTime.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -12), new Vector2(220, 40));
            chIcon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            chIcon.preserveAspect = true; chIcon.raycastTarget = false;
            chIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(72, -112), new Vector2(104, 104));
            chText = UIKit.Txt(p.transform, "", 28, Color.white, TextAnchor.MiddleLeft, true, true);
            chText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(140, -62), new Vector2(-300, 50));
            chCount = UIKit.Txt(p.transform, "", 46, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleRight, true, true);
            chCount.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -56), new Vector2(170, 62));

            // Balken ueber alle drei Stufen, mit Strichen bei Bronze und Silber
            var barBg = UIKit.Rect(p.transform, "BarBg").gameObject.AddComponent<Image>();
            barBg.sprite = Rounded; barBg.color = new Color(1f, 1f, 1f, 0.15f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(140, -128), new Vector2(-160, 36));
            chBarBg = barBg.rectTransform;
            chBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            chBarFill.sprite = Rounded; chBarFill.raycastTarget = false;
            chBarFill.rectTransform.anchorMin = Vector2.zero; chBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            chBarFill.rectTransform.offsetMin = chBarFill.rectTransform.offsetMax = Vector2.zero;
            for (int i = 0; i < 2; i++)
            {
                chMarks[i] = UIKit.Rect(barBg.transform, "Mark").gameObject.AddComponent<Image>();
                chMarks[i].color = new Color(1f, 1f, 1f, 0.8f); chMarks[i].raycastTarget = false;
            }
            // darunter: was jede Stufe bringt
            for (int i = 0; i < 3; i++)
            {
                chTierText[i] = UIKit.Txt(p.transform, "", 22, Color.white, TextAnchor.UpperCenter, true, true);
                chTierText[i].supportRichText = true;
                chTierText[i].rectTransform.Place(new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-10, 92));
            }

            wb.ChallengeChanged += () => chPunch = 1f;
            wb.ChallengeReward += (title, sub) => Banner(title, sub);
        }

        void UpdateChallengeHud(float dt)
        {
            if (chPanel == null) return;
            var def = PlantBalance.I.Plant(wb.ChallengeType);
            chPanel.gameObject.SetActive(def != null);
            if (def == null) return;
            var tiers = wb.ChallengeTiers;
            int tier = wb.ChallengeTier, count = wb.ChallengeCount;
            int goal = tiers[tiers.Length - 1];
            chIcon.sprite = SpeciesSprite(def.Genome);
            chText.text = "Liefere " + WorldBuilder.ProductName(def) + " in die Kiste";
            chCount.text = tier < tiers.Length ? count + " / " + tiers[tier] : count + " ✓";
            if (nw != null) chTime.text = nw.Active ? "Nacht – neuer Auftrag morgen" : "noch " + Mathf.CeilToInt(nw.Countdown / 60f) + " min bis zur Nacht";

            // Balken: ganze Strecke bis Gold, Farbe der naechsten Medaille, Striche bei Bronze/Silber
            chBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(count / (float)Mathf.Max(1, goal)), 1f);
            chBarFill.color = tier == 0 ? BronzeUi : tier == 1 ? SilverUi : GoldUi;
            for (int i = 0; i < 2; i++)
            {
                float x = tiers[i] / (float)Mathf.Max(1, goal);
                chMarks[i].rectTransform.anchorMin = new Vector2(x, 0f); chMarks[i].rectTransform.anchorMax = new Vector2(x, 1f);
                chMarks[i].rectTransform.sizeDelta = new Vector2(4f, 0f); chMarks[i].rectTransform.anchoredPosition = Vector2.zero;
            }
            // was die Stufen bringen; geschaffte mit Haken, die naechste hervorgehoben
            string[] name = { "Bronze", "Silber", "Gold" };
            Color[] col = { BronzeUi, SilverUi, GoldUi };
            for (int i = 0; i < 3; i++)
            {
                string reward = "+" + Num.Short(wb.ChallengePreview(i)) + " Münzen" + (i == 1 ? "\n+2 Runensteine" : i == 2 ? "\n+ ein Schatz" : "");
                string head = (i < tier ? "✓ " : "") + name[i] + " (" + tiers[i] + ")";
                chTierText[i].text = "<b>" + head + "</b>\n<size=19>" + reward + "</size>";
                var c = col[i];
                chTierText[i].color = i < tier ? new Color(0.6f, 0.9f, 0.5f) : i == tier ? c : new Color(c.r, c.g, c.b, 0.55f);
            }
            chPunch = Mathf.MoveTowards(chPunch, 0f, dt * 3f);
            chPanel.localScale = Vector3.one * (1f + chPunch * 0.06f);
        }
    }
}
