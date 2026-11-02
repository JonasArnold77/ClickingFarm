using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>Waffenleiste: erscheint am Abend und in der Nacht ueber der Werkzeugleiste.</summary>
    public partial class WorldUI
    {
        class WeaponSlot { public WeaponDef def; public Image frame, icon, cd; public Text label; public RectTransform rt; public readonly List<Image> dots = new List<Image>(); }
        readonly List<WeaponSlot> weaponSlots = new List<WeaponSlot>();
        CanvasGroup weaponGroup;
        float weaponShow;

        void BuildWeaponBar()
        {
            var B = PlantBalance.I;
            if (B.weapons == null || B.weapons.Length == 0) return;
            var bar = Panel(safe, "Weapons", new Color(0.1f, 0.08f, 0.12f, 0.6f));
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1030, 190));
            weaponGroup = bar.gameObject.AddComponent<CanvasGroup>();
            weaponGroup.alpha = 0f; weaponGroup.blocksRaycasts = false;
            var row = UIKit.HRow(bar.transform, "Row", 14, TextAnchor.MiddleCenter, new RectOffset(16, 16, 10, 10));
            row.GetComponent<RectTransform>().Stretch();
            foreach (var w in B.weapons)
            {
                var holder = UIKit.Rect(row.transform, "W_" + w.id);
                holder.LE(235, 170);
                var frame = Panel(holder, "Frame", new Color(1f, 1f, 1f, 0.9f));
                frame.rectTransform.Stretch(0, 0, 0, 0);
                var icon = UIKit.Rect(frame.transform, "Icon").gameObject.AddComponent<Image>();
                icon.sprite = WeaponArt.Icon(w.id); icon.preserveAspect = true; icon.raycastTarget = false;
                icon.rectTransform.Stretch(16, 16, 14, 52);
                var cd = UIKit.Rect(frame.transform, "Cooldown").gameObject.AddComponent<Image>();
                cd.sprite = Rounded; cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Vertical; cd.fillOrigin = 0;
                cd.color = new Color(0.1f, 0.08f, 0.12f, 0.45f); cd.raycastTarget = false; cd.fillAmount = 0f;
                cd.rectTransform.Stretch();
                var label = UIKit.Txt(frame.transform, w.name, 26, Ink, TextAnchor.MiddleCenter, true);
                label.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(-8, 42));
                var b = frame.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                var wd = w;
                b.onClick.AddListener(() =>
                {
                    if (nw == null) return;
                    if (!nw.IsWeaponUnlocked(wd)) { Sfx.Play("error", 1.1f, 0.5f); hintText.text = wd.name + " gibt es nach Welle " + wd.unlockWave; return; }
                    nw.SelectWeapon(wd.id); Haptics.Tap(8);
                    hintText.text = wd.name + ": " + wd.desc;
                });
                frame.gameObject.AddComponent<Juicy>();
                weaponSlots.Add(new WeaponSlot { def = w, frame = frame, icon = icon, cd = cd, label = label, rt = frame.rectTransform });
            }
        }

        /// <summary>Jedes Element der Waffenbank als eigener Farbpunkt oben am Waffenfeld (groesser mit der Stufe) –
        /// nebeneinander statt zu einer Mischfarbe verrechnet.</summary>
        void ElementDots(WeaponSlot s, bool unlocked)
        {
            var list = WorldBuilder.I != null && unlocked ? WorldBuilder.I.WeaponElements(s.def.id) : null;
            int n = list != null ? list.Count : 0;
            while (s.dots.Count < n)
            {
                var d = UIKit.Rect(s.frame.transform, "Element").gameObject.AddComponent<Image>();
                d.sprite = Rounded; d.raycastTarget = false;
                s.dots.Add(d);
            }
            for (int i = 0; i < s.dots.Count; i++)
            {
                var d = s.dots[i];
                d.enabled = i < n;
                if (!d.enabled) continue;
                var e = list[i];
                float size = 22f + 4f * Mathf.Min(e.lv, 5);
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f + i * 1.3f);
                d.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10 - i * 34f, -10), new Vector2(size, size) * pulse);
                d.color = WorldBuilder.ElementCol(e.id);
            }
        }

        void UpdateWeaponBar(float dt)
        {
            if (weaponGroup == null || nw == null) return;
            bool show = nw.Active || nw.Evening;
            weaponShow = Mathf.MoveTowards(weaponShow, show ? 1f : 0f, dt * 4f);
            weaponGroup.alpha = weaponShow;
            weaponGroup.blocksRaycasts = weaponShow > 0.5f;
            weaponGroup.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, weaponShow);
            // Hinweistext ueber die Waffenleiste schieben, solange sie sichtbar ist
            hintText.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(200f, 240f, weaponShow));
            if (weaponShow <= 0f) return;
            var cur = nw.Weapon;
            foreach (var s in weaponSlots)
            {
                bool unlocked = nw.IsWeaponUnlocked(s.def);
                bool sel = cur != null && cur.id == s.def.id;
                s.frame.color = !unlocked ? new Color(0.55f, 0.52f, 0.5f, 0.8f) : sel ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 1f, 1f, 0.9f);
                s.icon.color = !unlocked ? new Color(0.1f, 0.1f, 0.1f, 0.55f) : Color.white;
                ElementDots(s, unlocked);
                s.label.text = unlocked ? s.def.name : "nach Welle " + s.def.unlockWave;
                s.cd.fillAmount = sel ? nw.WeaponCooldown01 : 0f;
                s.rt.localScale = Vector3.one * (sel ? 1.06f : 0.96f);
            }
        }
    }
}
