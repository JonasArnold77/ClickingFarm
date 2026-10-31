using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Anzeige der Tagesaufgabe oben links (unter dem naechsten Ziel): welches Erntegut heute zaehlt, wie viele schon
    /// abgeliefert sind und wie weit es bis Bronze/Silber/Gold ist. Gewinne kommen als Banner.
    /// </summary>
    public partial class WorldUI
    {
        Image chIcon, chBarFill;
        Text chText, chMedals, chCount;
        RectTransform chPanel;
        float chPunch;

        void BuildChallengeHud()
        {
            var p = Panel(safe, "Challenge", new Color(0.12f, 0.1f, 0.08f, 0.6f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -415), new Vector2(560, 104));
            p.raycastTarget = false;
            chPanel = p.rectTransform;
            chIcon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            chIcon.preserveAspect = true; chIcon.raycastTarget = false;
            chIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56, 4), new Vector2(76, 76));
            // oben: Erntegut links, Stueckzahl rechts; darunter der Balken mit der Medaillen-Zeile darin
            chText = UIKit.Txt(p.transform, "", 27, Color.white, TextAnchor.MiddleLeft, true, true);
            chText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(104, -8), new Vector2(-230, 44));
            chCount = UIKit.Txt(p.transform, "", 30, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleRight, true, true);
            chCount.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(130, 44));
            var barBg = UIKit.Rect(p.transform, "BarBg").gameObject.AddComponent<Image>();
            barBg.color = new Color(1f, 1f, 1f, 0.15f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(104, 12), new Vector2(-120, 34));
            chBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            chBarFill.raycastTarget = false; chBarFill.color = new Color(1f, 0.78f, 0.3f);
            chBarFill.rectTransform.anchorMin = Vector2.zero; chBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            chBarFill.rectTransform.offsetMin = chBarFill.rectTransform.offsetMax = Vector2.zero;
            chMedals = UIKit.Txt(barBg.transform, "", 22, Color.white, TextAnchor.MiddleCenter, true, true);
            chMedals.rectTransform.Stretch();

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
            chIcon.sprite = SpeciesSprite(def.Genome);
            chText.text = "Heute: " + WorldBuilder.ProductName(def);
            chCount.text = tier < tiers.Length ? count + "/" + tiers[tier] : count.ToString();
            string[] medal = { "Bronze", "Silber", "Gold" };
            chMedals.text = tier >= tiers.Length ? "Gold geschafft!" : "nächste: " + medal[Mathf.Min(tier, 2)] + (tier > 0 ? " · " + medal[tier - 1] + " geschafft" : "");
            int prev = tier > 0 ? tiers[Mathf.Min(tier, tiers.Length) - 1] : 0;
            float f = tier < tiers.Length ? (count - prev) / (float)Mathf.Max(1, tiers[tier] - prev) : 1f;
            chBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            chBarFill.color = tier == 0 ? new Color(0.85f, 0.55f, 0.3f) : tier == 1 ? new Color(0.8f, 0.82f, 0.88f) : new Color(1f, 0.82f, 0.3f);
            chPunch = Mathf.MoveTowards(chPunch, 0f, dt * 3f);
            chPanel.localScale = Vector3.one * (1f + chPunch * 0.06f);
        }
    }
}
