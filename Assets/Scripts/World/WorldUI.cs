using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>Oberflaeche der freien Welt: Holz-Anzeige oben, Werkzeugleiste unten, schwebende Texte.</summary>
    public partial class WorldUI : MonoBehaviour
    {
        RectTransform canvasRt, safe;
        CanvasScaler scaler;
        Text woodText, hintText;
        readonly CountUp woodCount = new CountUp();
        readonly List<(Image slot, RectTransform rt, WorldTool tool)> slots = new List<(Image, RectTransform, WorldTool)>();
        WorldArtSet art;
        static Sprite rounded;

        class Floater { public Text t; public Vector2 world; public float life; }
        readonly List<Floater> floaters = new List<Floater>();

        void Start()
        {
            art = WorldArtSet.I;
            BuildCanvas();
            BuildWood();
            BuildToolbar();
            if (WorldBuilder.I != null)
            {
                WorldBuilder.I.Floater += (w, txt, col) => SpawnFloater(w, txt, col);
            }
            SelectTool(WorldTool.Axe);
            InitPlantsUI();
        }

        /// <summary>Weiches, abgerundetes Rechteck (9-Slice) fuer schlichte, saubere Flaechen.</summary>
        static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int n = 64; const float r = 22f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r)));
                        float dy = Mathf.Max(0, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
                    }
                tex.SetPixels(px); tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                return rounded;
            }
        }

        static Image Panel(Transform parent, string name, Color col)
        {
            var img = UIKit.Rect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = col;
            return img;
        }

        void BuildCanvas()
        {
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var go = new GameObject("WorldUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            go.AddComponent<GraphicRaycaster>();
            canvasRt = (RectTransform)go.transform;
            safe = UIKit.Rect(canvasRt, "Safe").Stretch();
            var sa = Screen.safeArea;
            if (Screen.width > 0)
            {
                safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
        }

        void BuildWood()
        {
            var p = Panel(safe, "Wood", new Color(0.12f, 0.1f, 0.08f, 0.55f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(300, 110));
            p.raycastTarget = false;
            var icon = UIKit.Rect(p.transform, "Icon").gameObject.AddComponent<Image>();
            icon.sprite = art != null ? art.Get("hh_log") : null; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(100, 70));
            woodText = UIKit.Txt(p.transform, "0", 58, Color.white, TextAnchor.MiddleLeft, true, true);
            woodText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(135, 0), new Vector2(-145, 0));
        }

        void BuildToolbar()
        {
            var bar = Panel(safe, "Toolbar", new Color(0.12f, 0.1f, 0.08f, 0.5f));
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(840, 270));
            var row = UIKit.HRow(bar.transform, "Row", 14, TextAnchor.MiddleCenter, new RectOffset(20, 20, 16, 16));
            row.GetComponent<RectTransform>().Stretch();
            AddSlot(row.transform, WorldTool.Axe, Icon("sk_Tree_0"), "Fällen");
            AddSlot(row.transform, WorldTool.Field, Icon("hh_hoe_icon"), "Felder");
            AddSlot(row.transform, WorldTool.Plant, Icon("hh_corn_seedbag"), "Pflanzen");
            AddSlot(row.transform, WorldTool.Fence, Icon("sk_Fence_11"), "Zaun");

            hintText = UIKit.Txt(safe, "", 32, Color.white, TextAnchor.MiddleCenter, true, true);
            hintText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 312), new Vector2(-60, 80));
        }


        Sprite Icon(string name) => art != null ? art.Get(name) : null;

        Image AddSlot(Transform parent, WorldTool tool, Sprite iconSprite, string label)
        {
            var holder = UIKit.Rect(parent, "Slot_" + tool);
            holder.LE(182, 236);
            var slot = UIKit.Rect(holder, "Frame").gameObject.AddComponent<Image>();
            slot.sprite = art != null ? art.Get("hh_slot") : null;
            slot.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(170, 170));
            var icon = UIKit.Rect(slot.transform, "Icon").gameObject.AddComponent<Image>();
            icon.sprite = iconSprite; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.rectTransform.Stretch(24, 24, 20, 30);
            var t = UIKit.Txt(holder, label, 30, Color.white, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(40, 48));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (tool == WorldTool.Plant) plantSlotIcon = icon;
            var b = slot.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                bool again = WorldCamera.Tool == tool;
                SelectTool(tool); Sfx.Play("click"); Haptics.Tap(8);
                if (tool == WorldTool.Plant) OnPlantToolTapped(again);
            });
            slot.gameObject.AddComponent<Juicy>();
            slots.Add((slot, slot.rectTransform, tool));
            return icon;
        }

        void SelectTool(WorldTool tool)
        {
            WorldCamera.Tool = tool;
            foreach (var (slot, rt, t) in slots)
            {
                bool sel = t == tool;
                slot.sprite = art != null ? art.Get(sel ? "hh_slot_selected" : "hh_slot") : null;
                slot.color = sel ? new Color(1f, 0.95f, 0.75f) : new Color(1, 1, 1, 0.85f);
                rt.sizeDelta = sel ? new Vector2(182, 182) : new Vector2(158, 158);
            }
            hintText.text = ToolHint(tool);
        }

        string ToolHint(WorldTool tool)
        {
            var wbi = WorldBuilder.I;
            switch (tool)
            {
                case WorldTool.Axe: return "Tippe oft auf einen Baum, um ihn zu fällen (Zäune abbauen geht auch)";
                case WorldTool.Field: return "Tippe oder wische über die Wiese, um Felder anzulegen";
                case WorldTool.Plant: return "Tippe auf ein Feld zum Einsetzen – dann ~100× auf die Pflanze tippen";
                case WorldTool.Fence:
                    return "Wische, um Zäune zu ziehen (" + (wbi != null ? wbi.FenceCost : 1) + " Holz pro Stück) – sie halten Zombies auf";
                default:
                    return "Reife Pflanzen antippen und in einen Blumentopf setzen";
            }
        }

        void SpawnFloater(Vector2 world, string text, Color col)
        {
            var t = UIKit.Txt(canvasRt, text, 52, col, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.sizeDelta = new Vector2(500, 90);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            floaters.Add(new Floater { t = t, world = world, life = 0 });
        }

        void Update()
        {
            UpdatePlantsUI(Time.unscaledDeltaTime);
            scaler.matchWidthOrHeight = Screen.width > Screen.height * 0.75f ? 1f : 0f;
            if (WorldBuilder.I != null) woodText.text = Mathf.RoundToInt((float)woodCount.Step(WorldBuilder.I.Wood, Time.unscaledDeltaTime)).ToString();

            var cam = Camera.main;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.life += Time.deltaTime;
                float k = f.life / 1.2f;
                if (k >= 1f || cam == null) { Destroy(f.t.gameObject); floaters.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(f.world + new Vector2(0, k * 0.8f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
                f.t.rectTransform.anchoredPosition = local;
                f.t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) * 3f));
                var c = f.t.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; f.t.color = c;
            }
        }
    }
}
