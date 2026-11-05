using System.Collections.Generic;
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
        CanvasGroup dayButtons;   // Schnellauswahl unten (tagsueber)
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

            // Schnellauswahl unten: Hand, Schaufel (Felder), Mauer, Steinpult, Bach – und der Laden
            BuildQuickbar(wbi);

            wbi.OfflineEarned += OpenWelcomeBack;
            wbi.DeliveryPaid += (n, v) => Banner("Lieferung bezahlt: +" + Num.Short(v) + " Münzen", n + " Stück Ernte sind verkauft");
            wbi.GoldenCaught += msg => Banner("Goldener Schmetterling!", msg);
            wbi.FieldsChanged += RefreshGoal;   // Ziel zeigt, wie viele Felder noch fehlen
            wbi.PlantUnlocked += id =>
            {
                var d = PlantBalance.I.Plant(id);
                if (d != null && !d.IsCombat && !wbi.GrantingMilestone) Banner("Neue Pflanze: " + d.name, Helps(d));
                RefreshGoal();
            };
        }

        // ------------------------------------------------------------------ Schnellauswahl

        class QuickTool { public string id; public Image img; public Text txt; public Color col; }
        readonly List<QuickTool> quickTools = new List<QuickTool>();

        void BuildQuickbar(WorldBuilder wbi)
        {
            var row = UIKit.Rect(safe, "Quickbar");
            row.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(-40, 150));
            dayButtons = row.gameObject.AddComponent<CanvasGroup>();
            (string id, string label, Color col)[] tools =
            {
                ("hand", "Hand", new Color(0.5f, 0.45f, 0.4f)),
                ("shovel", "Schaufel", new Color(0.55f, 0.42f, 0.25f)),
                ("wall", "Mauer", new Color(0.62f, 0.45f, 0.28f)),
                ("pedestal", "Steinpult", new Color(0.55f, 0.55f, 0.58f)),
                ("river", "Bach", new Color(0.3f, 0.6f, 0.85f)),
                ("shop", "Laden", new Color(0.36f, 0.62f, 0.3f)),
            };
            float w = 1f / tools.Length;
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                string id = t.id;
                var b = Btn(row, t.label, t.col, () => PickQuickTool(id), t.label.Length > 7 ? 28 : 32);
                b.GetComponent<RectTransform>().Place(new Vector2(i * w + 0.004f, 0), new Vector2((i + 1) * w - 0.004f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                quickTools.Add(new QuickTool { id = id, img = b.GetComponent<Image>(), txt = b.GetComponentInChildren<Text>(), col = t.col });
            }
            wbi.WallModeChanged += on => { RefreshQuickbar(); hintText.text = on ? "Mauer: Klick setzt den Anfang, die grüne Mauer folgt dir – nächster Klick baut sie (" + PlantBalance.I.Wall.wood.ToString("0.##") + " Holz je Stück). Zweimal auf eine Mauer: Tor, zweimal auf ein Tor: abbauen" : ToolHint(WorldCamera.Tool); };
            wbi.PedestalModeChanged += on => { RefreshQuickbar(); hintText.text = on ? "Steinpult (" + Num.Short(PlantBalance.I.Pedestal.cost) + " Münzen) auf Boden oder Mauer setzen – halten und ziehen setzt viele. Ins Loch passen Kampfpflanzen" : ToolHint(WorldCamera.Tool); };
            wbi.RiverModeChanged += on => { RefreshQuickbar(); hintText.text = on ? "Bach: Klick setzt den Anfang, das blaue Wasser folgt dir – nächster Klick gräbt ihn (" + Num.Short(PlantBalance.I.River.cost) + " Münzen je Stück). Felder am Bach wachsen schneller und tragen mehr" : ToolHint(WorldCamera.Tool); };
            wbi.ShovelModeChanged += on => { RefreshQuickbar(); hintText.text = on ? "Schaufel: Tipp auf freien Boden setzt den Anfang, die Vorschau folgt – nächster Tipp legt die Felder an (nächstes Feld: " + Num.Short(wbi.FieldCost()) + " Münzen)" : ToolHint(WorldCamera.Tool); };
            RefreshQuickbar();
        }

        /// <summary>Werkzeug aus der Schnellauswahl waehlen (nochmal tippen = zurueck zur Hand).</summary>
        void PickQuickTool(string id)
        {
            var wbi = WorldBuilder.I;
            if (wbi == null) return;
            Haptics.Tap(8);
            switch (id)
            {
                case "shop": OpenShop(shopTab); return;
                case "hand": HandTool(wbi); break;
                case "shovel": if (wbi.ShovelMode) HandTool(wbi); else wbi.SetShovelMode(true); break;
                case "wall": wbi.SetWallMode(!wbi.WallMode); break;
                case "pedestal": wbi.SetPedestalMode(!wbi.PedestalMode); break;
                case "river": wbi.SetRiverMode(!wbi.RiverMode); break;
            }
            RefreshQuickbar();
        }

        static void HandTool(WorldBuilder wbi)
        {
            if (wbi.WallMode) wbi.SetWallMode(false);
            if (wbi.PedestalMode) wbi.SetPedestalMode(false);
            if (wbi.RiverMode) wbi.SetRiverMode(false);
            if (wbi.ShovelMode) wbi.SetShovelMode(false);
        }

        /// <summary>Gewaehltes Werkzeug leuchtet (Hand, wenn kein Bau-Modus an ist).</summary>
        void RefreshQuickbar()
        {
            var wbi = WorldBuilder.I;
            if (wbi == null) return;
            string cur = wbi.ShovelMode ? "shovel" : wbi.WallMode ? "wall" : wbi.PedestalMode ? "pedestal" : wbi.RiverMode ? "river" : "hand";
            foreach (var q in quickTools)
            {
                bool on = q.id == cur;
                q.img.color = on ? new Color(0.95f, 0.72f, 0.3f) : q.col;
                q.img.rectTransform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
        }

        /// <summary>Laden-Reiter "Bauen": Felder, Mauer, Steinpult, Bach – mit Preis; "Auswählen" nimmt das Werkzeug in die Hand.</summary>
        void BuildShop(Transform content, WorldBuilder wbi)
        {
            void Row(string title, string desc, string price, string tool)
                => ShopRow(content, title, desc, price, true, () => { CloseModal(); PickQuickTool(tool); });
            Row("Felder (Schaufel)", "Neue Felder anlegen: Tipp setzt den Anfang, die Vorschau folgt, zweiter Tipp baut. Du hast " + wbi.FieldCount + " Felder.",
                Num.Short(wbi.FieldCost()) + " je Feld", "shovel");
            Row("Mauer", "Hält Zombies auf. Zweimal auf eine Mauer tippen: Tor (Knollis gehen durch).",
                PlantBalance.I.Wall.wood.ToString("0.##") + " Holz je Stück", "wall");
            Row("Steinpult", "Erhöhtes Pult für Kampf- und Verteidigungspflanzen, auch auf Mauern.",
                Num.Short(PlantBalance.I.Pedestal.cost) + " je Stück", "pedestal");
            Row("Bach", "Fließt um deine Gärten: Felder daneben wachsen schneller und tragen mehr.",
                Num.Short(PlantBalance.I.River.cost) + " je Stück", "river");
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
                if (night && wbi.PedestalMode) wbi.SetPedestalMode(false);
                if (night && wbi.RiverMode) wbi.SetRiverMode(false);
                if (night && wbi.ShovelMode) wbi.SetShovelMode(false);
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
            if (tab == 1) tab = shopTab = 0;   // frueher "Gebaeude" – gibt es nicht mehr
            string[] names = { "Verbesserungen", "Bauen", "Hof", "Bauwerke" };
            int[] ids = { 0, 4, 2, 3 };
            for (int i = 0; i < names.Length; i++)
            {
                int k = ids[i];
                var b = Btn(tabs.transform, names[i], k == tab ? AccentWarm : new Color(0.55f, 0.5f, 0.45f), () => OpenShop(k), names[i].Length > 10 ? 22 : 27);
                b.GetComponent<RectTransform>().LE(0, 80, 1);   // Reiter teilen sich die Breite
            }
            var coins = Label(panel, "Münzen: " + Num.Short(wbi.Coins) + "   Holz: " + wbi.Wood + "   (+" + Num.Short(wbi.Cps) + "/s)\nVorrat: " + StockLine(wbi), 28, InkSoft, TextAnchor.MiddleCenter, true);
            coins.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -215), new Vector2(-40, 50));
            var content = UIKit.Scroll(panel, out var scroll);
            scroll.GetComponent<RectTransform>().Stretch(24, 24, 275, 30);

            if (tab == 0)
            {
                bool any = false;
                foreach (var u in PlantBalance.I.upgrades)
                {
                    if (wbi.HasUpgrade(u.id) || !wbi.UpgradeAvailable(u)) continue;
                    any = true;
                    var uu = u;
                    ShopRow(content, u.name, u.desc, Num.Short(u.cost), wbi.Coins >= u.cost, () => { if (wbi.BuyUpgrade(uu)) OpenShop(0); });
                }
                if (!any) Label(content, "Gerade nichts zu kaufen – die nächsten Verbesserungen stehen unten.", 30, InkSoft, TextAnchor.MiddleCenter).LE(-1, 120);
                // die naechsten zwei gesperrten: was sie bringen und ab wann es sie gibt (man sieht immer, was kommt)
                var soon = new List<ShopUpgradeDef>();
                foreach (var u in PlantBalance.I.upgrades) if (!wbi.HasUpgrade(u.id) && !wbi.UpgradeAvailable(u)) soon.Add(u);
                soon.Sort((x, y) => wbi.MilestoneOf(x.id).CompareTo(wbi.MilestoneOf(y.id)));
                for (int i = 0; i < soon.Count && i < 2; i++)
                    ShopRow(content, soon[i].name + " (bald)", soon[i].desc + "\n" + UnlockWhen(wbi, soon[i].id, soon[i].unlockEarned, soon[i].needs),
                        Num.Short(soon[i].cost), false, () => Sfx.Play("error", 1.1f, 0.4f));
                int owned = wbi.Save.upgrades.Count;
                if (owned > 0) Label(content, "Gekauft: " + owned, 28, InkSoft, TextAnchor.MiddleCenter).LE(-1, 60);
            }
            else if (tab == 3) StructureShop(content);
            else if (tab == 4) BuildShop(content, wbi);
            else
            {
                var next = wbi.NextFarmLevel();
                if (next != null)
                {
                    var fc = wbi.CropCost(next.crops);
                    string fm = wbi.CropMissing(fc);
                    // Ausbau nur ueber die Sonnenlilie: sie lichtet den Nebel, wenn sie reif ist
                    var lily = PlantBalance.I.Plant("sonnenlilie");
                    float lc = lily != null ? wbi.SeedCost(lily) : next.cost;
                    ShopRow(content, "Nebel lichten (Hof-Stufe " + (wbi.FarmLevel + 1) + ")", "Pflanze eine Sonnenlilie und lass sie reifen – ihr Licht vertreibt den Nebel und der Hof wird größer. Braucht auch " + next.wood + " Holz" + (fc.Count > 0 ? " und " + wbi.CropCostText(fc) : "")
                        + (fm != "" ? "\nFehlt im Vorrat: " + fm : ""),
                        Num.Short(lc), wbi.Coins >= lc && wbi.Wood >= next.wood && wbi.HasCrops(fc), () =>
                        {
                            if (lily == null) return;
                            wbi.SelectPlant(lily.id);
                            CloseModal();
                            Banner("Sonnenlilie gewählt", "Tippe auf ein freies Feld und lass sie wachsen");
                        });
                }
                else Label(content, "Der Hof ist voll ausgebaut.", 32, InkSoft, TextAnchor.MiddleCenter).LE(-1, 120);
                double kc = wbi.KnolliCost;
                bool full = wbi.HousingFull;
                ShopRow(content, "Knolli anheuern (" + wbi.KnolliCount + "/" + wbi.Housing + " Plätze)",
                    full ? "Alle Plätze belegt – bau eine Knolli-Hütte (Bauwerke)" : "Ein neuer Helfer – antippen und Beruf wählen: Träger, Gießer, Holzfäller, Maurer oder Wache",
                    Num.Short(kc), wbi.Coins >= kc && !full,
                    () => { if (wbi.HireKnolli()) OpenShop(2); });
                Label(content, "Tipp: Bäume fällen gibt Holz, Felsbrocken antippen gibt Münzen und macht Platz.", 28, InkSoft, TextAnchor.MiddleCenter).LE(-1, 110);
            }
        }

        /// <summary>Offene Hof-Bedingungen mit Stand als Zusatzzeile ("· Mauerstücke 3 / 5").</summary>
        /// <summary>Wann es frei wird: mit welchem Ziel – oder (ohne Ziel) ab welchen Muenzen und Bedingungen.</summary>
        static string UnlockWhen(WorldBuilder w, string id, float earned, string[] needs)
        {
            int k = w.MilestoneOf(id);
            if (k >= 0) return "Kommt mit Ziel " + (k + 1) + ": " + PlantBalance.I.milestones[k].name;
            return "Ab " + Num.Short(earned) + " verdienten Münzen – jetzt " + Num.Short(w.Earned) + NeedsLine(w, needs);
        }

        /// <summary>Vorrat an Ernte (Baumaterial), z. B. "Radieschen 40 · Karotte 12".</summary>
        static string StockLine(WorldBuilder w)
        {
            var parts = new List<string>();
            foreach (var c in PlantBalance.I.catalog)
                if (c.role == "harvest" && w.Stored(c.id) > 0) parts.Add(c.name + " " + Num.Short(w.Stored(c.id)));
            return parts.Count > 0 ? string.Join(" · ", parts) : "noch leer – gelieferte Ernte landet hier";
        }

        static string NeedsLine(WorldBuilder w, string[] needs)
        {
            string s = "";
            foreach (var n in w.NeedsList(needs)) s += "\n" + (n.ok ? "✓ " : "") + n.text;
            return s;
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
