using System;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Oberflaeche der Wirtschaft: Muenzen und Muenzen pro Sekunde (hochzaehlend), Laden mit Verbesserungen,
    /// Gebaeuden und Hof-Ausbau, Mauer-Knopf, "Willkommen zurueck"-Fenster und Meldungen zum goldenen Schmetterling.
    /// Laden- und Mauer-Knopf sind nur tagsueber da (nachts sitzt unten die Waffenleiste).
    /// </summary>
    public partial class WorldUI
    {
        Text coinText, cpsText;
        readonly CountUp coinCount = new CountUp();
        CanvasGroup dayButtons;
        Image wallBtnImg;
        Text wallBtnText;
        int shopTab;

        void BuildEconomyUI()
        {
            var wbi = WorldBuilder.I;
            if (wbi == null) return;
            // Muenzen gross oben links unter Holz und Jahreszeit
            var p = Panel(safe, "Coins", new Color(0.12f, 0.1f, 0.08f, 0.6f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -150), new Vector2(560, 130));
            p.raycastTarget = false;
            var icon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            icon.sprite = art != null ? art.Get("hh_coin") : null; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66, 0), new Vector2(92, 92));
            coinText = UIKit.Txt(p.transform, "0", 64, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleLeft, true, true);
            coinText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(125, -4), new Vector2(-135, 76));
            cpsText = UIKit.Txt(p.transform, "", 28, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft, false, true);
            cpsText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(128, 8), new Vector2(-135, 42));

            // Knoepfe unten: Mauer bauen (links), Laden (rechts)
            var row = UIKit.Rect(safe, "DayButtons");
            row.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(-56, 150));
            dayButtons = row.gameObject.AddComponent<CanvasGroup>();
            var wall = Btn(row, "Mauer bauen", new Color(0.62f, 0.45f, 0.28f), () => wbi.SetWallMode(!wbi.WallMode), 36);
            wall.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(380, 0));
            wallBtnImg = wall.GetComponent<Image>(); wallBtnText = wall.GetComponentInChildren<Text>();
            var shop = Btn(row, "Laden", new Color(0.36f, 0.62f, 0.3f), () => OpenShop(shopTab), 44);
            shop.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(380, 0));
            wbi.WallModeChanged += on =>
            {
                wallBtnText.text = on ? "Fertig" : "Mauer bauen";
                wallBtnImg.color = on ? new Color(0.85f, 0.6f, 0.25f) : new Color(0.62f, 0.45f, 0.28f);
                hintText.text = on ? "Tippe auf einen hellen Holzplatz: Mauer bauen (" + PlantBalance.I.Wall.wood + " Holz). Leere Mauer antippen: abbauen" : ToolHint(WorldCamera.Tool);
            };

            wbi.OfflineEarned += OpenWelcomeBack;
            wbi.GoldenCaught += msg => Banner("Goldener Schmetterling!", msg);
            wbi.PlantUnlocked += id =>
            {
                var d = PlantBalance.I.Plant(id);
                if (d != null && !d.IsCombat) Banner("Neue Pflanze: " + d.name, Roles.Name(d.role) + " – " + Roles.Desc(d));
                RefreshGoal();
            };
        }

        void UpdateEconomyUI(float dt)
        {
            var wbi = WorldBuilder.I;
            if (wbi == null || coinText == null) return;
            coinText.text = Num.Short(coinCount.Step(wbi.Coins, dt));
            string cps = "+" + Num.Short(wbi.Cps) + " pro Sekunde";
            if (wbi.GoldenBoostLeft > 0f) cps += "  ×7 (" + Mathf.CeilToInt(wbi.GoldenBoostLeft) + " s)";
            cpsText.text = cps;
            cpsText.color = wbi.GoldenBoostLeft > 0f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.85f);
            if (dayButtons != null)
            {
                bool night = nw != null && (nw.Active || nw.Evening);
                dayButtons.alpha = Mathf.MoveTowards(dayButtons.alpha, night ? 0f : 1f, dt * 4f);
                dayButtons.blocksRaycasts = dayButtons.alpha > 0.5f;
                if (night && wbi.WallMode) wbi.SetWallMode(false);
            }
        }

        // ------------------------------------------------------------------ Laden

        void OpenShop(int tab)
        {
            var wbi = WorldBuilder.I;
            shopTab = tab;
            OpenModal(out var panel, 110, 40);
            Header(panel, "Laden");
            // Reiter
            var tabs = UIKit.HRow(panel, "Tabs", 12, TextAnchor.MiddleCenter, new RectOffset(20, 20, 0, 0));
            tabs.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(0, 90));
            string[] names = { "Verbesserungen", "Gebäude", "Hof" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                var b = Btn(tabs.transform, names[i], i == tab ? AccentWarm : new Color(0.55f, 0.5f, 0.45f), () => OpenShop(k), 27);
                b.GetComponent<RectTransform>().LE(0, 80, 1);   // Reiter teilen sich die Breite
            }
            var coins = Label(panel, "Münzen: " + Num.Short(wbi.Coins) + "   Holz: " + wbi.Wood + "   (+" + Num.Short(wbi.Cps) + "/s)", 32, InkSoft, TextAnchor.MiddleCenter, true);
            coins.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -215), new Vector2(-40, 50));
            var content = UIKit.Scroll(panel, out var scroll);
            scroll.GetComponent<RectTransform>().Stretch(24, 24, 275, 30);

            if (tab == 0)
            {
                bool any = false;
                foreach (var u in PlantBalance.I.upgrades)
                {
                    if (wbi.HasUpgrade(u.id) || wbi.Earned < u.unlockEarned) continue;
                    any = true;
                    var uu = u;
                    ShopRow(content, u.name, u.desc, Num.Short(u.cost), wbi.Coins >= u.cost, () => { if (wbi.BuyUpgrade(uu)) OpenShop(0); });
                }
                if (!any) Label(content, "Gerade nichts – verdiene mehr Münzen, dann gibt es neue Verbesserungen.", 30, InkSoft, TextAnchor.MiddleCenter).LE(-1, 120);
                int owned = wbi.Save.upgrades.Count;
                if (owned > 0) Label(content, "Gekauft: " + owned, 28, InkSoft, TextAnchor.MiddleCenter).LE(-1, 60);
            }
            else if (tab == 1)
            {
                foreach (var d in PlantBalance.I.buildings)
                {
                    var dd = d;
                    double cost = wbi.BuildingCost(d);
                    int n = wbi.BuildingCount(d.id);
                    ShopRow(content, d.name + (n > 0 ? "  ×" + n : ""), d.desc + " – +" + Num.Short(d.cps) + " Münzen/s", Num.Short(cost), wbi.Coins >= cost,
                        () => { if (wbi.BuyBuilding(dd)) OpenShop(1); });
                }
            }
            else
            {
                var next = wbi.NextFarmLevel();
                if (next != null)
                    ShopRow(content, "Hof erweitern (Stufe " + (wbi.FarmLevel + 1) + ")", "Mehr Platz für Felder und Mauern – braucht auch " + next.wood + " Holz",
                        Num.Short(next.cost), wbi.Coins >= next.cost && wbi.Wood >= next.wood, () => { if (wbi.BuyFarm()) { CloseModal(); Banner("Der Hof wächst!", "Neue Fläche zum Bebauen"); } });
                else Label(content, "Der Hof ist voll ausgebaut.", 32, InkSoft, TextAnchor.MiddleCenter).LE(-1, 120);
                Label(content, "Tipp: Bäume fällen gibt Holz, Felsbrocken antippen gibt Münzen und macht Platz.", 28, InkSoft, TextAnchor.MiddleCenter).LE(-1, 110);
            }
        }

        void ShopRow(Transform parent, string title, string desc, string price, bool affordable, Action buy)
        {
            var row = Panel(parent, "Row", new Color(1f, 1f, 1f, 0.92f));
            row.LE(-1, 170);
            var t = Label(row.transform, title, 38, Ink, TextAnchor.UpperLeft, true);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(-330, 50));
            var dsc = Label(row.transform, desc, 28, InkSoft, TextAnchor.UpperLeft);
            dsc.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -70), new Vector2(-330, 90));
            var b = Btn(row.transform, price, affordable ? Accent : new Color(0.65f, 0.62f, 0.58f), buy, 38);
            b.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(280, 110));
            b.interactable = affordable;
        }

        // ------------------------------------------------------------------ Willkommen zurueck

        void OpenWelcomeBack(double coins, double secs)
        {
            OpenModal(out var panel, 560, 520);
            Header(panel, "Willkommen zurück!");
            int mins = Mathf.Max(1, (int)(secs / 60));
            string time = mins >= 60 ? (mins / 60) + " Std. " + (mins % 60) + " Min." : mins + " Min.";
            var t = Label(panel, "Während du " + time + " weg warst, hat deine Farm\n+" + Num.Short(coins) + " Münzen verdient.", 40, Ink, TextAnchor.MiddleCenter);
            t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(-60, 160));
            var b = Btn(panel, "Super!", Accent, CloseModal, 44);
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(480, 110));
            Sfx.Play("coin", 1f, 1f);
        }
    }
}
