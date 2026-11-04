using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Knopf am Gebaeude: Faehrt der Zeiger ueber ein Gebaeude (Laden oder Bauwerk), erscheint im oberen Teil des
    /// Gebaeudes (nicht in der Mitte, wo man klickt) ein kleines Schild mit Stufe, Fortschrittsbalken und Knopf. Er oeffnet die Funktion des Gebaeudes (Laden-Fenster bzw.
    /// Bauwerk-Fenster mit Wirkung und "Versetzen"). Klicks aufs Gebaeude selbst leveln es.
    /// Ohne Maus (Handy) erscheint der Knopf nach dem Antippen fuer ein paar Sekunden.
    /// </summary>
    public partial class WorldUI
    {
        RectTransform bbRoot;
        Text bbLevel, bbLabel;
        Image bbFill;
        bool bbShop; StructureState bbStruct;
        float bbKeep;          // so lange bleibt der Knopf noch, wenn der Zeiger das Gebaeude verlassen hat
        float bbTapShow;       // nach einem Tipp (Handy) sichtbar
        CanvasGroup bbGroup;

        void BuildBuildingButton()
        {
            bbRoot = UIKit.Rect(canvasRt, "BuildingButton");
            bbRoot.sizeDelta = new Vector2(250, 150);
            bbGroup = bbRoot.gameObject.AddComponent<CanvasGroup>();
            bbGroup.alpha = 0f; bbGroup.blocksRaycasts = false;
            bbLevel = UIKit.Txt(bbRoot, "", 30, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter, true, true);
            bbLevel.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(80, 40));
            bbLevel.horizontalOverflow = HorizontalWrapMode.Overflow;
            bbLevel.raycastTarget = false;   // nur der Knopf faengt Klicks ab
            // Fortschrittsbalken bis zur naechsten Stufe
            var barBg = UIKit.Rect(bbRoot, "Bar").gameObject.AddComponent<Image>();
            barBg.color = new Color(0.12f, 0.09f, 0.06f, 0.75f); barBg.raycastTarget = false;
            barBg.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-16, 22));
            bbFill = UIKit.Rect(barBg.transform, "Fill").gameObject.AddComponent<Image>();
            bbFill.color = new Color(1f, 0.8f, 0.3f); bbFill.raycastTarget = false;
            bbFill.rectTransform.anchorMin = Vector2.zero; bbFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            bbFill.rectTransform.offsetMin = new Vector2(3, 3); bbFill.rectTransform.offsetMax = new Vector2(-3, -3);
            var b = Btn(bbRoot, "", new Color(0.88f, 0.62f, 0.2f, 0.95f), PressBuildingButton, 32);
            b.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 76));
            bbLabel = b.GetComponentInChildren<Text>();

            wb.ShopTapped += () => { bbShop = true; bbStruct = null; bbTapShow = 3f; };
            wb.StructureTapped += st => { bbShop = false; bbStruct = st; bbTapShow = 3f; };
        }

        float bbShown;
        void SetBar(float f, float dt)
        {
            // weich nachziehen, bei einer neuen Stufe sofort leer
            f = Mathf.Clamp01(f);
            bbShown = f < bbShown - 0.3f ? f : Mathf.MoveTowards(bbShown, f, dt * 3f);
            bbFill.rectTransform.anchorMax = new Vector2(bbShown, 1f);
        }

        void PressBuildingButton()
        {
            var shop = bbShop; var st = bbStruct;
            bbTapShow = 0f; bbKeep = 0f;
            if (modal != null) return;
            if (shop) OpenShop(shopTab);
            else if (st != null) ShowStructureInfo(st);
        }

        void UpdateBuildingButton(float dt)
        {
            if (bbRoot == null) return;
            var cam = Camera.main;
            bool overButton = Input.mousePresent && RectTransformUtility.RectangleContainsScreenPoint(bbRoot, Input.mousePosition, null);
            // Zeiger ueber einem Gebaeude? (nur mit Maus und nicht ueber anderer Oberflaeche)
            if (Input.mousePresent && cam != null && !overButton && modal == null
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                var w = (Vector2)cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
                if (wb.BuildingUnder(w, out bool shop, out var st, out _)) { bbShop = shop; bbStruct = st; bbKeep = 0.5f; }
            }
            if (overButton) bbKeep = 0.5f;
            bbKeep -= dt; bbTapShow -= dt;
            bool show = modal == null && (bbKeep > 0f || bbTapShow > 0f) && (bbShop || (bbStruct != null && bbStruct.placed));
            bbGroup.alpha = Mathf.MoveTowards(bbGroup.alpha, show ? 1f : 0f, dt * (show ? 10f : 4f));
            bbGroup.blocksRaycasts = show;
            if (bbGroup.alpha <= 0f || cam == null) return;

            // in den oberen Teil des Gebaeudes stellen
            var sp = cam.WorldToScreenPoint(wb.BuildingAnchor(bbShop, bbStruct));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
            bbRoot.anchoredPosition = local;

            if (bbShop)
            {
                bbLevel.text = "Laden · Stufe " + wb.ShopLv + (wb.ShopMaxed ? "" : " · " + Mathf.FloorToInt(wb.ShopXp) + "/" + wb.ShopTapsNeeded);
                bbLabel.text = "Laden öffnen";
                SetBar(wb.ShopMaxed ? 1f : wb.ShopXp / wb.ShopTapsNeeded, dt);
            }
            else if (bbStruct != null)
            {
                var d = PlantBalance.I.Structure(bbStruct.id);
                bbLevel.text = (d != null ? d.name : "") + " · Stufe " + bbStruct.lv + (wb.StructMaxed(bbStruct) ? "" : " · " + Mathf.FloorToInt(bbStruct.xp) + "/" + wb.StructTapsNeeded(bbStruct));
                bbLabel.text = "Ansehen";
                SetBar(wb.StructMaxed(bbStruct) ? 1f : bbStruct.xp / wb.StructTapsNeeded(bbStruct), dt);
            }
        }
    }
}
