using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>Baukasten fuer die per Code erzeugte Oberflaeche.</summary>
    public static class UIKit
    {
        static Font font;
        public static Font Font => font != null ? font : (font = LoadFont());

        static Font LoadFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return f;
        }

        public static readonly Color TextDark = Balance.Hex("#4A2E1A");
        public static readonly Color TextMid = Balance.Hex("#7A5A3A");
        public static readonly Color Green = Balance.Hex("#6CCB3F");
        public static readonly Color Red = Balance.Hex("#E8574C");
        public const string BtnGreen = "6CCB3F", BtnOrange = "FFA53D", BtnBlue = "4FA8F0", BtnGray = "B9A88F", BtnPink = "FF6FA5", BtnPurple = "9B6BFF", BtnRed = "E8574C";

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false, float sliceScale = 4f)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color;
            if (sliced && sprite != null) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = (100f / sprite.pixelsPerUnit) / sliceScale; } // sliceScale = UI-Einheiten pro Sprite-Pixel
            else img.preserveAspect = true;
            return img;
        }

        public static Image Icon(Transform parent, string key, float size)
        {
            var img = Img(parent, "Icon", Art.IconFor(key), Color.white);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size; le.preferredHeight = size; le.minWidth = size;
            return img;
        }

        public static Text Txt(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, bool outline = false)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.18f, 0.1f, 0.05f, 0.9f); o.effectDistance = new Vector2(3, -3);
            }
            return t;
        }

        public static Button Btn(Transform parent, string label, string colorHex, Action onClick, string icon = null, int fontSize = 40)
        {
            var img = Img(parent, "Button", Art.Get("btn:" + colorHex), Color.white, true, 5f);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors; cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.85f); cb.pressedColor = new Color(0.9f, 0.9f, 0.9f); b.colors = cb;
            img.gameObject.AddComponent<Juicy>();
            b.onClick.AddListener(() => { Sfx.Play("click"); Haptics.Tap(6); onClick?.Invoke(); });

            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 10; h.padding = new RectOffset(18, 18, 8, 12);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon)) Icon(img.transform, icon, fontSize * 1.3f);
            if (!string.IsNullOrEmpty(label))
            {
                var t = Txt(img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, true, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return b;
        }

        public static void SetLabel(this Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static LayoutElement LE(this Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (prefW >= 0) { le.preferredWidth = prefW; le.minWidth = prefW; }
            if (prefH >= 0) { le.preferredHeight = prefH; le.minHeight = prefH; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static HorizontalLayoutGroup HRow(Transform parent, string name, float spacing = 12, TextAnchor align = TextAnchor.MiddleLeft, RectOffset pad = null)
        {
            var rt = Rect(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align; h.padding = pad ?? new RectOffset(0, 0, 0, 0);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup VCol(Transform parent, string name, float spacing = 10, TextAnchor align = TextAnchor.UpperLeft, RectOffset pad = null)
        {
            var rt = Rect(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align; v.padding = pad ?? new RectOffset(0, 0, 0, 0);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>Scrollbare Liste. Gibt den Inhalts-Container (vertikales Layout) zurueck.</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect sr)
        {
            var view = Rect(parent, "Scroll");
            var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(1, 1, 1, 0.003f);
            view.gameObject.AddComponent<RectMask2D>();
            sr = view.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 40;
            var content = Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.padding = new RectOffset(6, 6, 6, 24);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = content.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = view;
            return content;
        }

        /// <summary>Fortschrittsbalken; gibt das Fuell-Bild zurueck (Breite ueber SetBar).</summary>
        public static RectTransform Bar(Transform parent, string name, Color back, Color fill, out RectTransform fillRt)
        {
            var bg = Img(parent, name, Art.Get("bar"), back, true, 4f);
            bg.raycastTarget = false;
            var f = Img(bg.transform, "Fill", Art.Get("bar"), fill, true, 4f);
            f.raycastTarget = false;
            fillRt = f.rectTransform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0.5f, 1); fillRt.offsetMin = new Vector2(4, 4); fillRt.offsetMax = new Vector2(-4, -4);
            return bg.rectTransform;
        }

        public static void SetBar(RectTransform fill, float v)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
            fill.gameObject.SetActive(v > 0.001f);
        }

        public static Image Panel(Transform parent, string name, string style = "cream")
        {
            var p = Img(parent, name, Art.Get("panel:" + style), Color.white, true, 5f);
            return p;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Kostenanzeige: Muenzen + Material, rot wenn nicht bezahlbar.</summary>
        public static void CostChips(Transform parent, double coins, double wood = 0, double stone = 0, double fiber = 0, int size = 34)
        {
            var g = Game.I;
            void Chip(string icon, double v, double have)
            {
                if (v <= 0) return;
                var row = HRow(parent, "Cost", 4);
                Icon(row.transform, icon, size * 1.1f);
                var t = Txt(row.transform, Fmt.Num(v), size, have >= v ? TextDark : Red, TextAnchor.MiddleLeft, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            Chip("coin", coins, g.S.coins); Chip("wood", wood, g.S.wood); Chip("stone", stone, g.S.stone); Chip("fiber", fiber, g.S.fiber);
        }
    }

    /// <summary>Knopf federt beim Druecken.</summary>
    public class Juicy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f, cur = 1f, vel;
        public void OnPointerDown(PointerEventData e) { target = 0.9f; }
        public void OnPointerUp(PointerEventData e) { target = 1f; vel += 4f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        void OnDisable() { cur = target = 1f; vel = 0; transform.localScale = Vector3.one; }
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            vel += (target - cur) * 300f * dt;
            vel *= Mathf.Exp(-18f * dt);
            cur += vel * dt;
            transform.localScale = new Vector3(cur, cur, 1);
        }
    }

    /// <summary>Sanftes Pulsieren (z. B. fuer Hinweise und volle Kiste).</summary>
    public class Pulse : MonoBehaviour
    {
        public float amount = 0.06f, speed = 4f;
        public bool active = true;
        void Update()
        {
            float s = active ? 1f + Mathf.Sin(Time.unscaledTime * speed) * amount : 1f;
            transform.localScale = new Vector3(s, s, 1);
        }
    }

    /// <summary>Zaehlt eine Zahl sichtbar hoch statt umzuspringen.</summary>
    public class CountUp
    {
        public double shown;
        bool init;
        public double Step(double target, float dt)
        {
            if (!init) { shown = target; init = true; return shown; }
            double diff = target - shown;
            if (Math.Abs(diff) < 0.5) shown = target;
            else shown += diff * Math.Min(1.0, dt * 8.0) + Math.Sign(diff) * 0.5;
            if ((diff > 0 && shown > target) || (diff < 0 && shown < target)) shown = target;
            return shown;
        }
    }
}
