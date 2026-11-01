using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Berufswahl: Ist ein Knolli ausgewaehlt, erscheint unten ein Holzschild mit fuenf Knoepfen (Traeger, Giesser,
    /// Holzfaeller, Maurer, Wache). Der aktuelle Beruf leuchtet. Feld-Berufe warten danach auf den Tipp aufs Feld.
    /// </summary>
    public partial class WorldUI
    {
        RectTransform rolePanel;
        CanvasGroup roleGroup;
        Image[] roleImgs;
        Text roleTitle;

        static readonly Color RoleOff = new Color(0.55f, 0.48f, 0.4f), RoleOn = new Color(0.93f, 0.6f, 0.25f);

        void BuildKnolliRoles()
        {
            var p = Panel(safe, "KnolliRoles", new Color(0.12f, 0.1f, 0.08f, 0.62f));
            rolePanel = p.rectTransform;
            rolePanel.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(-40, 250));
            roleGroup = p.gameObject.AddComponent<CanvasGroup>();
            roleTitle = UIKit.Txt(p.transform, "Beruf wählen", 36, new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter, true, true);
            roleTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-40, 56));
            int n = WorldBuilder.KnolliRoles.Length;
            roleImgs = new Image[n];
            for (int i = 0; i < n; i++)
            {
                var r = WorldBuilder.KnolliRoles[i];
                string id = r.id;
                var b = Btn(p.transform, r.name, RoleOff, () => wb.ChooseRole(id), 28);
                float x0 = 0.02f + i * (0.96f / n), x1 = x0 + 0.96f / n - 0.012f;
                b.GetComponent<RectTransform>().Place(new Vector2(x0, 0), new Vector2(x1, 0), new Vector2(0, 0), new Vector2(0, 26), new Vector2(0, 130));
                roleImgs[i] = b.GetComponent<Image>();
            }
            p.gameObject.SetActive(false);
        }

        void UpdateKnolliRoles()
        {
            if (rolePanel == null) return;
            bool show = wb.KnolliSelected && modal == null;
            if (rolePanel.gameObject.activeSelf != show)
            {
                rolePanel.gameObject.SetActive(show);
                if (show) StartCoroutine(PopIn(rolePanel));
            }
            if (!show) return;
            string cur = wb.SelectedRole;
            for (int i = 0; i < roleImgs.Length; i++)
                roleImgs[i].color = WorldBuilder.KnolliRoles[i].id == cur ? RoleOn : RoleOff;
            foreach (var r in WorldBuilder.KnolliRoles) if (r.id == cur) roleTitle.text = "Beruf: " + r.name + " – " + r.desc;
        }
    }
}
