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
            BuildSeasonButton();
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

        static Image Panel(Transform parent, string name, Color col) => StyledPanel(parent, name, col);   // siehe WorldUI.Style

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

        /// <summary>Knopf oben neben dem Holz: Jahreszeit wechseln (Sommer -> Herbst -> Winter).</summary>
        void BuildSeasonButton()
        {
            var wbi = WorldBuilder.I;
            if (wbi == null) return;
            var p = Panel(safe, "Season", new Color(0.12f, 0.1f, 0.08f, 0.55f));
            p.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(340, -28), new Vector2(250, 110));
            var t = UIKit.Txt(p.transform, "", 40, Color.white, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Stretch(10, 10, 6, 6);
            void Refresh(Season s)
            {
                t.text = WorldBuilder.SeasonName(s);
                t.color = s == Season.Herbst ? new Color(1f, 0.75f, 0.35f) : s == Season.Winter ? new Color(0.8f, 0.92f, 1f) : new Color(1f, 0.97f, 0.7f);
            }
            Refresh(wbi.Season);
            wbi.SeasonChanged += s => { Refresh(s); Banner(WorldBuilder.SeasonName(s), s == Season.Winter ? "Schnee liegt über dem Hof" : s == Season.Herbst ? "Das Laub färbt sich" : "Alles grünt und blüht"); };
            var b = p.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.Play("click"); wbi.NextSeason(); });
            p.gameObject.AddComponent<Juicy>();
        }

        void BuildToolbar()
        {
            // keine Werkzeuge mehr: ein Tipp auf die Welt macht das Naheliegende (siehe WorldCamera)
            hintText = UIKit.Txt(safe, "", 32, Color.white, TextAnchor.MiddleCenter, true, true);
            hintText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(-60, 80));
        }

        Sprite Icon(string name) => art != null ? art.Get(name) : null;

        void SelectTool(WorldTool tool)
        {
            WorldCamera.Tool = tool;
            hintText.text = ToolHint(tool);
        }

        string ToolHint(WorldTool tool) => "Hand: auf ein leeres Feld tippen pflanzt, auf eine Pflanze tippen pflegt sie, ziehen topft um. Neue Felder gibt es mit der Schaufel";

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
