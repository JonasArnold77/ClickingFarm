using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Pflanzen-Info: Ein Tipp auf eine Pflanze oeffnet unten ein Fenster mit Bild, Name, Stufe, Rolle, was sie macht,
    /// ihren Werten und dem Fortschritt zur naechsten Stufe. Der Tipp selbst laesst die Pflanze nicht wachsen – dafuer hat
    /// das Fenster einen grossen Knopf (Wachsen lassen / Ernten / Giessen / Staerker machen), der wie der fruehere
    /// Klick auf die Pflanze wirkt; das Fenster zaehlt live mit und blendet nach ein paar Sekunden ohne Tipp aus.
    /// Mit der Maus (Browser/Editor) zeigt ein kurzes Schild ueber der Pflanze Name und Stufe, das wieder ausblendet.
    /// </summary>
    public partial class WorldUI
    {
        CanvasGroup infoGroup;
        RectTransform infoPanel;
        Image infoIcon, infoBarFill;
        Text infoName, infoLevel, infoRole, infoDesc, infoStats, infoBarText;
        PlantedState infoPlant;
        float infoTimer;
        string infoBuiltFor;

        Button infoAct, infoTurn;
        Text infoActText;
        Image infoActImg;

        Text hoverText;
        PlantedState hoverPlant;
        float hoverT = 99f;

        const float InfoShowTime = 6f;

        void BuildPlantInfo()
        {
            var root = UIKit.Rect(safe, "PlantInfo");
            root.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 195), new Vector2(-40, 470));
            infoGroup = root.gameObject.AddComponent<CanvasGroup>();
            infoGroup.alpha = 0f; infoGroup.blocksRaycasts = false;
            var bg = Panel(root, "Bg", PanelCol);
            bg.rectTransform.Stretch();
            infoPanel = root;

            // Bild links auf einem hellen Kreis
            var glow = UIKit.Rect(root, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = WorldBuilder.Mist; glow.raycastTarget = false; glow.color = new Color(1f, 0.9f, 0.6f, 0.5f);
            glow.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(230, 230));
            infoIcon = UIKit.Rect(root, "Icon").gameObject.AddComponent<Image>();
            infoIcon.preserveAspect = true; infoIcon.raycastTarget = false;
            infoIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(125, -130), new Vector2(190, 190));

            infoName = Label(root, "", 50, Ink, TextAnchor.MiddleLeft, true);
            infoName.resizeTextForBestFit = true; infoName.resizeTextMinSize = 26; infoName.resizeTextMaxSize = 50;
            infoName.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -22), new Vector2(-560, 64));
            infoLevel = Label(root, "", 44, AccentWarm, TextAnchor.MiddleRight, true);
            infoLevel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -22), new Vector2(240, 64));
            infoRole = Label(root, "", 32, AccentWarm, TextAnchor.MiddleLeft, true);
            infoRole.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -88), new Vector2(-280, 44));
            infoDesc = Label(root, "", 30, InkSoft, TextAnchor.UpperLeft);
            infoDesc.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -138), new Vector2(-280, 84));
            infoStats = Label(root, "", 27, Ink, TextAnchor.UpperLeft, true);
            infoStats.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(250, -218), new Vector2(-270, 70));

            // Fortschrittsbalken unten
            var barBg = UIKit.Rect(root, "BarBg").gameObject.AddComponent<Image>();
            barBg.color = new Color(0.25f, 0.2f, 0.15f, 0.25f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-60, 46));
            infoBarFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            infoBarFill.raycastTarget = false;
            infoBarFill.rectTransform.anchorMin = Vector2.zero; infoBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            infoBarFill.rectTransform.offsetMin = infoBarFill.rectTransform.offsetMax = Vector2.zero;
            infoBarText = Label(barBg.transform, "", 28, Ink, TextAnchor.MiddleCenter, true);
            infoBarText.rectTransform.Stretch();

            // Grosser Knopf: hier wird geklickt – wachsen lassen, ernten, leveln (der Tipp in der Welt oeffnet nur das Fenster)
            infoAct = Btn(root, "", Accent, () => { if (infoPlant != null) wb.TapPlant(new Cell(infoPlant.x, infoPlant.y)); }, 40);
            infoAct.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 88), new Vector2(-60, 104));
            infoActText = infoAct.GetComponentInChildren<Text>();
            infoActImg = infoAct.GetComponent<Image>();

            // Wasser- und Klick-Pflanzen: Blickrichtung drehen
            // gross im Fenster neben dem Hauptknopf; der wird dafuer schmaler (siehe FillPlantInfo)
            infoTurn = Btn(root, "Drehen", new Color(0.3f, 0.62f, 0.4f), () => { if (infoPlant != null) { infoTimer = InfoShowTime; wb.TurnPlant(infoPlant); } }, 42);
            infoTurn.GetComponent<RectTransform>().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 88), new Vector2(400, 104));
            infoTurn.gameObject.SetActive(false);

            var x = Btn(root, "X", new Color(0.8f, 0.45f, 0.35f), HidePlantInfo, 40);
            x.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(96, 84));

            // Hover-Schild (nur mit Maus)
            hoverText = UIKit.Txt(canvasRt, "", 40, Color.white, TextAnchor.MiddleCenter, true, true);
            hoverText.rectTransform.sizeDelta = new Vector2(600, 70);
            hoverText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hoverText.raycastTarget = false;
            hoverText.color = new Color(1, 1, 1, 0);

            wb.PlantTapped += ShowPlantInfo;
        }

        void ShowPlantInfo(PlantedState p)
        {
            if (p == null || modal != null) return;
            infoPlant = p;
            infoTimer = InfoShowTime;
            if (infoGroup.alpha < 0.05f) infoPanel.localScale = Vector3.one * 0.9f;
        }

        void HidePlantInfo() { infoPlant = null; infoTimer = 0f; }

        void UpdatePlantInfo(float dt)
        {
            if (infoGroup == null) return;
            // Pflanze weg (geerntet, zusammengefuehrt, Mauer zerstoert) oder Fenster offen: ausblenden
            if (infoPlant != null && (!wb.Planted.Contains(infoPlant) || modal != null)) infoPlant = null;
            infoTimer -= dt;
            bool show = infoPlant != null && infoTimer > 0f;
            infoGroup.alpha = Mathf.MoveTowards(infoGroup.alpha, show ? 1f : 0f, dt * (show ? 8f : 2.5f));
            infoGroup.blocksRaycasts = show;
            wb.Inspected = show ? infoPlant : null;
            infoPanel.localScale = Vector3.one * Mathf.MoveTowards(infoPanel.localScale.x, 1f, dt * 1.5f);
            hintText.enabled = infoGroup.alpha < 0.5f;
            if (infoPlant != null) FillPlantInfo(infoPlant);
            UpdateHover(dt);
        }

        void FillPlantInfo(PlantedState p)
        {
            var def = PlantBalance.I.Plant(p.type);
            if (def == null) return;
            var g = p.Genome;
            string key = p.type + "|" + PlantBalance.I.Tier(p.Lv);
            if (infoBuiltFor != key)
            {
                infoBuiltFor = key;
                infoIcon.sprite = SpeciesSprite(g);
                infoIcon.color = Color.Lerp(Color.white, PlantBalance.MutationTint(g, PlantBalance.I.Tier(p.Lv)), 0.8f);
                infoName.text = def.name;
                infoDesc.text = Roles.Desc(def);
            }
            infoLevel.text = "Stufe " + p.Lv;
            int tier = PlantBalance.I.Tier(p.Lv);
            if (def.IsCombat)
            {
                var sp = PlantBalance.I.Species(def.species);
                string form = sp.mutationNames != null && sp.mutationNames.Length >= tier ? sp.mutationNames[tier - 1] : "";
                infoRole.text = "Kampf · Mutation " + tier + "/4" + (form != "" ? " – " + form : "");
            }
            else infoRole.text = Roles.Name(def.role);

            infoStats.text = Stats(p, def, g);
            bool turn = p.Grown && WorldBuilder.HasDirection(p);
            infoTurn.gameObject.SetActive(turn);
            var ar = infoAct.GetComponent<RectTransform>();
            ar.offsetMin = new Vector2(30, ar.offsetMin.y);
            ar.offsetMax = new Vector2(turn ? -450 : -30, ar.offsetMax.y);
            if (WorldBuilder.HasDirection(p)) infoStats.text += (infoStats.text != "" ? " · " : "") + "Richtung " + WorldBuilder.DirName(p.dir);

            float f; string bar;
            if (!p.Grown) { f = p.growth / Mathf.Max(1f, p.GrowTaps); bar = "Wächst: " + Mathf.FloorToInt(p.growth) + " / " + p.GrowTaps; }
            else
            {
                int need = Mathf.Max(1, PlantBalance.I.levelTaps);
                f = p.xp / (float)need;
                int next = NextMutationLevel(p.Lv);
                bar = "Nächste Stufe: " + p.xp + " / " + need + (next > 0 ? "   ·   Mutation ab Stufe " + next : "");
            }
            infoBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            infoBarFill.color = !p.Grown ? new Color(0.55f, 0.82f, 0.35f) : new Color(0.7f, 0.55f, 1f);
            infoBarText.text = bar;
            bool locked = p.Grown && !wb.IsReadyForTaps(p);
            infoActText.text = locked ? "Gleich bereit …" : !p.Grown ? "Wachsen lassen!" : def.role == "harvest" ? "Ernten!" : def.role == "water" ? "Gießen!" : "Stärker machen!";
            infoActImg.color = !p.Grown ? Accent : def.role == "harvest" ? new Color(0.92f, 0.68f, 0.2f) : def.role == "water" ? new Color(0.35f, 0.62f, 0.92f) : new Color(0.6f, 0.45f, 0.9f);
        }

        static int NextMutationLevel(int lv)
        {
            var m = PlantBalance.I.mutationLevels;
            if (m == null) return 0;
            foreach (var l in m) if (l > lv) return l;
            return 0;
        }

        string Stats(PlantedState p, CatalogDef def, PlantGenome g)
        {
            string s;
            switch (def.role)
            {
                case "harvest": s = "Pro Tipp " + Num.Short(wb.HarvestPerTap(p)) + " · von selbst " + Num.Short(wb.PlantCps(p)) + "/s"; break;
                case "bee": s = "Honig: " + Num.Short(wb.PlantCps(p)) + " Münzen/s"; break;
                case "water": s = "Gießt gerade " + wb.WaterTargetCount(p) + " Pflanzen"; break;
                case "support": s = p.Grown ? wb.SupportSummary(p) : ""; break;
                case "transport": case "carrier": case "sower": case "pipe": s = p.Grown ? wb.HelperSummary(p) : ""; break;
                case "transform":
                    s = !p.Grown ? "" : def.transform switch
                    {
                        "stone" => "Bereit – zieh sie auf eine Holzmauer oder einen Wachturm",
                        "thorns" => "Bereit – zieh sie auf eine Mauer",
                        "heart" => "Bereit – zieh sie in ein Steinpult",
                        "moon" => "Bereit – zieh sie auf Laterne oder Wachturm",
                        _ => "Bereit – zieh sie auf die Waffenbank"
                    };
                    break;
                case "combat": s = PlantGenome.Describe(g.projectile) + " · " + PlantGenome.Describe(g.element) + " · " + PlantGenome.Describe(g.range); break;
                default: s = ""; break;
            }
            if (!p.Grown && wb.IsWatered(p)) s = (s != "" ? s + " · " : "") + "gegossen ×2";
            if (!p.Grown && s == "") s = "Tippe, damit sie wächst";
            return s;
        }

        /// <summary>Maus ueber einer Pflanze: Name und Stufe kurz einblenden, dann ausfaden (auch wenn die Maus bleibt).</summary>
        void UpdateHover(float dt)
        {
            var cam = Camera.main;
            PlantedState over = null;
            if (cam != null && Input.mousePresent && Input.touchCount == 0 && modal == null)
            {
                Vector2 w = cam.ScreenToWorldPoint(Input.mousePosition);
                // Pflanzen ragen nach oben aus ihrem Feld: auch die Zelle darunter pruefen
                over = wb.PlantAt(WorldBuilder.CellAt(w)) ?? wb.PlantAt(WorldBuilder.CellAt(w - new Vector2(0, WorldBuilder.CellSize * 0.5f)));
            }
            if (over != hoverPlant)
            {
                hoverPlant = over;
                hoverT = over != null ? 0f : 99f;
            }
            hoverT += dt;
            // 0,15 s einblenden, 1,1 s stehen, 0,6 s ausblenden
            float a = hoverT < 0.15f ? hoverT / 0.15f : hoverT < 1.25f ? 1f : Mathf.Clamp01(1f - (hoverT - 1.25f) / 0.6f);
            if (hoverPlant == null || cam == null) a = Mathf.Max(0f, hoverText.color.a - dt * 4f);
            hoverText.color = new Color(1f, 1f, 1f, a);
            if (hoverPlant != null && cam != null)
            {
                var def = PlantBalance.I.Plant(hoverPlant.type);
                hoverText.text = (def != null ? def.name : "") + " · Stufe " + hoverPlant.Lv;
                var sp = cam.WorldToScreenPoint(WorldBuilder.PlantBase(hoverPlant) + new Vector2(0, WorldBuilder.CellSize * 1.7f + a * 0.06f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
                hoverText.rectTransform.anchoredPosition = local;
                hoverText.rectTransform.localScale = Vector3.one * (hoverT < 0.15f ? Mathf.Lerp(0.8f, 1f, hoverT / 0.15f) : 1f);
            }
        }
    }
}
