using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Bildlook im Stil gemalter Ghibli-Hintergruende: weiches warmes Bloom, warme Weissbalance,
    /// kuehle blaugruene Schatten + goldene Lichter (Split Toning), etwas mehr Farbe,
    /// sanfte Vignette und leise Sonnenstrahlen von oben links.
    /// Alle Werte stehen hier an einer Stelle zum Nachjustieren.
    /// </summary>
    public class WorldPostFX : MonoBehaviour
    {
        readonly List<(SpriteRenderer sr, float baseA, float phase)> rays = new List<(SpriteRenderer, float, float)>();
        Transform rayRoot;
        Camera cam;
        ColorAdjustments adjust;
        WhiteBalance balance;
        Bloom bloomFx;
        float bloomBase, exposureBase;
        static float punch;

        /// <summary>Kurzer Licht-Stoss (Bloom + Helligkeit), z. B. bei einem kraeftigen Klick. 0..1</summary>
        public static void Punch(float amount) => punch = Mathf.Min(1.5f, Mathf.Max(punch, amount));

        void Start()
        {
            cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GhibliLook";

            var bloom = profile.Add<Bloom>(true);
            bloomFx = bloom;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.78f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.78f));

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var wb = profile.Add<WhiteBalance>(true);
            balance = wb;
            wb.temperature.Override(9f);
            wb.tint.Override(-4f);

            var ca = profile.Add<ColorAdjustments>(true);
            adjust = ca;
            ca.colorFilter.Override(Color.white);
            ca.postExposure.Override(0.12f);
            ca.contrast.Override(10f);
            ca.saturation.Override(14f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.32f, 0.52f, 0.6f));
            split.highlights.Override(new Color(1f, 0.84f, 0.62f));
            split.balance.Override(-10f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.93f, 1f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.02f, 0.95f, 0.02f));

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.55f);
            vig.color.Override(new Color(0.12f, 0.14f, 0.1f));

            var volGo = new GameObject("PostFX Volume");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = profile;

            BuildSunRays();
        }

        /// <summary>Weiche, schraege Lichtbahnen, die langsam atmen (haengen an der Kamera).</summary>
        void BuildSunRays()
        {
            rayRoot = new GameObject("SunRays").transform;
            rayRoot.SetParent(cam.transform, false);
            rayRoot.localPosition = new Vector3(0, 0, 10);
            var tex = new Texture2D(16, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Ray" };
            var px = new Color[16 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 16; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / 16f - 0.5f) * 2f;
                    float along = Mathf.Sin((y + 0.5f) / 64f * Mathf.PI);
                    float a = across * across * along;
                    px[y * 16 + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 64), new Vector2(0.5f, 0.5f), 16);
            var rnd = new System.Random(5);
            for (int i = 0; i < 6; i++)
            {
                var sr = new GameObject("Ray").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(rayRoot, false);
                sr.sprite = sprite;
                float w = 1.2f + (float)rnd.NextDouble() * 2.2f;
                sr.transform.localScale = new Vector3(w, 6f, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, 32f + (float)rnd.NextDouble() * 6f);
                sr.transform.localPosition = new Vector3(-4f + i * 1.9f + (float)rnd.NextDouble(), 2f - i * 0.6f, 0);
                sr.color = new Color(1f, 0.95f, 0.8f, 0f);
                sr.sortingOrder = 30500;
                rays.Add((sr, 0.05f + (float)rnd.NextDouble() * 0.05f, (float)rnd.NextDouble() * 10f));
            }
        }

        void Update()
        {
            UpdateTimeOfDay();
            UpdatePunch();
            if (cam == null || rayRoot == null) return;
            // Strahlen passen sich der Zoomstufe an und atmen sanft
            float s = cam.orthographicSize / 8f;
            rayRoot.localScale = new Vector3(s, s, 1);
            float t = Time.time;
            foreach (var (sr, baseA, phase) in rays)
            {
                var c = sr.color;
                c.a = baseA * (0.55f + 0.45f * Mathf.Sin(t * 0.35f + phase));
                sr.color = c;
            }
        }
        void UpdatePunch()
        {
            if (bloomFx == null || adjust == null) return;
            if (bloomBase <= 0f) { bloomBase = bloomFx.intensity.value; exposureBase = adjust.postExposure.value; }
            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 4f);
            bloomFx.intensity.value = bloomBase + punch * 2.2f;
            adjust.postExposure.value = exposureBase + punch * 0.45f;
        }

        /// <summary>Abend: warmes Orange-Rosa ueber allem, die Sonnenstrahlen werden golden; nachts wieder neutral.</summary>
        void UpdateTimeOfDay()
        {
            var nw = NightWave.I;
            if (nw == null || adjust == null) return;
            float dusk = nw.DuskAmount * (1f - nw.NightAmount);
            adjust.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.78f, 0.62f), dusk);
            balance.temperature.value = Mathf.Lerp(9f, 30f, dusk);
            foreach (var (sr, _, _) in rays) if (sr != null) { var c = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.35f), dusk); c.a = sr.color.a; sr.color = c; }
        }
    }
}
