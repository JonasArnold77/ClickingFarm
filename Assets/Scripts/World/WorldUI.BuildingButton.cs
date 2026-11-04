using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Knopf oben am Gebaeude: Faehrt der Zeiger ueber ein Gebaeude (Laden oder Bauwerk), erscheint ueber dessen
    /// Oberkante ein kleiner Knopf mit Stufe und Fortschritt. Er oeffnet die Funktion des Gebaeudes (Laden-Fenster bzw.
    /// Bauwerk-Fenster mit Wirkung und "Versetzen"). Klicks aufs Gebaeude selbst leveln es.
    /// Ohne Maus (Handy) erscheint der Knopf nach dem Antippen fuer ein paar Sekunden.
    /// </summary>
    public partial class WorldUI
    {
        RectTransform bbRoot;
        Text bbLevel, bbLabel;
        bool bbShop; StructureState bbStruct;
        float bbKeep;          // so lange bleibt der Knopf noch, wenn der Zeiger das Gebaeude verlassen hat
        float bbTapShow;       // nach einem Tipp (Handy) sichtbar
        CanvasGroup bbGroup;

        void BuildBuildingButton()
        {
            bbRoot = UIKit.Rect(canvasRt, "BuildingButton");
            bbRoot.sizeDelta = new Vector2(260, 140);
            bbGroup = bbRoot.gameObject.AddComponent<CanvasGroup>();
            bbGroup.alpha = 0f; bbGroup.blocksRaycasts = false;
            bbLevel = UIKit.Txt(bbRoot, "", 30, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter, true, true);
            bbLevel.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(80, 44));
            bbLevel.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = Btn(bbRoot, "", new Color(0.88f, 0.62f, 0.2f), PressBuildingButton, 34);
            b.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 86));
            bbLabel = b.GetComponentInChildren<Text>();

            wb.ShopTapped += () => { bbShop = true; bbStruct = null; bbTapShow = 3f; };
            wb.StructureTapped += st => { bbShop = false; bbStruct = st; bbTapShow = 3f; };
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

            // ueber die Oberkante des Gebaeudes stellen
            var top = wb.BuildingTop(bbShop, bbStruct) + new Vector2(0, 0.15f);
            var sp = cam.WorldToScreenPoint(top);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
            bbRoot.anchoredPosition = local + new Vector2(0, bbRoot.sizeDelta.y * 0.5f);

            if (bbShop)
            {
                bbLevel.text = "Laden · Stufe " + wb.ShopLv + (wb.ShopMaxed ? "" : " · " + Mathf.FloorToInt(wb.ShopXp) + "/" + wb.ShopTapsNeeded);
                bbLabel.text = "Laden öffnen";
            }
            else if (bbStruct != null)
            {
                var d = PlantBalance.I.Structure(bbStruct.id);
                bbLevel.text = (d != null ? d.name : "") + " · Stufe " + bbStruct.lv + (wb.StructMaxed(bbStruct) ? "" : " · " + Mathf.FloorToInt(bbStruct.xp) + "/" + wb.StructTapsNeeded(bbStruct));
                bbLabel.text = "Ansehen";
            }
        }
    }
}
