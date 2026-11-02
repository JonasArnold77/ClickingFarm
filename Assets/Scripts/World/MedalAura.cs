using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Wabernde Sphaere in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Die Blase zeichnet der Shader "SeedClicker/MedalAura2D" (Rand verformt sich staendig, innen Schlieren,
    /// Glanzpunkt). Um Pflanzen laeuft zusaetzlich "CFXR Magical Source" (Cartoon FX) in Medaillenfarbe:
    /// Leuchten mit aufsteigenden Glitzerpunkten.
    /// Haengt als Komponente am Bild und folgt ihm selbst (sichtbar, Groesse, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host, sphere;
        Material mat;
        GameObject fx;
        int level, fxLevel;
        float size, seed;
        bool sparkles;
        static Sprite quadSprite, fallbackSprite;
        static Shader auraShader;
        static bool shaderLooked;
        static Material fallbackMat;

        /// <summary>Stufe setzen (unter 2 = keine Sphaere). size: Durchmesser im Verhaeltnis zur groessten Bildkante.
        /// sparkles: zusaetzlich das Glitzer-Leuchten (nur fuer Pflanzen, nicht fuer jedes Erntestueck).</summary>
        public static void Set(SpriteRenderer host, int level, float size, bool sparkles = false)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
                a.seed = Random.value * 10f;
            }
            a.level = level; a.size = size; a.sparkles = sparkles;
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2;
            if (!on) { Hide(); return; }
            if (sphere == null) Build();
            sphere.enabled = true;

            var b = host.sprite.bounds;
            float d = Mathf.Max(b.size.x, b.size.y) * size;
            var spr = sphere.sprite;
            float k = d / Mathf.Max(0.0001f, spr.bounds.size.x);
            // leichtes Atmen der ganzen Blase, das eigentliche Wabern macht der Shader am Rand
            float t = Time.time * 1.6f + seed;
            sphere.transform.localPosition = b.center;
            sphere.transform.localScale = new Vector3(k * (1f + 0.03f * Mathf.Sin(t)), k * (1f + 0.03f * Mathf.Sin(t + 1.7f)), 1f);
            sphere.transform.rotation = Quaternion.identity;   // nicht mit der Pflanze/dem Stueck mitdrehen

            var c = WorldBuilder.MedalColor(level);
            sphere.color = new Color(c.r, c.g, c.b, 0.85f + 0.15f * Mathf.Sin(t * 1.3f));
            sphere.sortingLayerID = host.sortingLayerID;
            sphere.sortingOrder = host.sortingOrder + 1;   // vorn: die Mitte ist fast klar, man sieht die Pflanze in der Blase

            // Glitzer-Leuchten um Pflanzen (bei neuer Medaille in der neuen Farbe neu starten)
            if (sparkles && (fx == null || fxLevel != level))
            {
                if (fx != null) Destroy(fx);
                fxLevel = level;
                var world = host.transform.TransformPoint(b.center);
                float scale = d * 0.45f;   // in Bild-Einheiten: das Leuchten fuellt etwa die Blase
                fx = WorldFx.Play("magicalSource", world, 1f, host.sortingOrder + 2, c, host.transform, 1e6f);
                if (fx != null) { fx.transform.localPosition = b.center; fx.transform.localScale = Vector3.one * scale; }
            }
            if (!sparkles && fx != null) { Destroy(fx); fx = null; }
        }

        void Hide()
        {
            if (sphere != null) sphere.enabled = false;
            if (fx != null) { Destroy(fx); fx = null; }
        }

        void OnDisable() => Hide();

        void Build()
        {
            sphere = new GameObject("MedalSphere").AddComponent<SpriteRenderer>();
            sphere.transform.SetParent(host.transform, false);
            if (!shaderLooked) { shaderLooked = true; auraShader = Shader.Find("SeedClicker/MedalAura2D"); }
            if (auraShader != null)
            {
                mat = new Material(auraShader);
                mat.SetFloat("_Seed", seed * 7f);
                sphere.sprite = QuadSprite;
                sphere.sharedMaterial = mat;
            }
            else
            {
                // Ersatz ohne Shader: gemalte Blase
                if (fallbackMat == null) fallbackMat = new Material(Shader.Find("Sprites/Default"));
                sphere.sprite = FallbackSprite;
                sphere.sharedMaterial = fallbackMat;
            }
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        /// <summary>Weisses Quadrat: der Shader malt die Blase ueber die UV-Koordinaten.</summary>
        static Sprite QuadSprite
        {
            get
            {
                if (quadSprite != null) return quadSprite;
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[64];
                for (int i = 0; i < 64; i++) px[i] = Color.white;
                tex.SetPixels(px); tex.Apply();
                return quadSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8, 0, SpriteMeshType.FullRect);
            }
        }

        /// <summary>Gemalte Blase fuer den Fall, dass der Shader fehlt: klare Mitte, leuchtender Rand, Glanzpunkt.</summary>
        static Sprite FallbackSprite
        {
            get
            {
                if (fallbackSprite != null) return fallbackSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(u * u + v * v);
                        float rim = Mathf.SmoothStep(0.6f, 0.97f, r) * (1f - Mathf.SmoothStep(0.97f, 1f, r));
                        float hx = u + 0.38f, hy = v - 0.4f;
                        float spot = Mathf.Clamp01(1f - (hx * hx + hy * hy) / 0.05f);
                        px[y * n + x] = new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(0.08f + rim * 0.85f + spot * 0.8f));
                    }
                tex.SetPixels(px); tex.Apply();
                return fallbackSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }
    }
}
