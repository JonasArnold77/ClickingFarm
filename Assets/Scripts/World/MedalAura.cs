using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Wabernde Sphaere in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Haengt als Komponente am Bild und folgt ihm selbst (sichtbar, Groesse, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host, sphere, inner;
        int level;
        float size, seed;
        static Sprite sphereSprite, innerSprite;
        static Material unlit;

        /// <summary>Stufe setzen (unter 2 = keine Sphaere). size: Durchmesser im Verhaeltnis zur groessten Bildkante.</summary>
        public static void Set(SpriteRenderer host, int level, float size)
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
            a.level = level; a.size = size;
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2;
            if (!on) { if (sphere != null) sphere.enabled = inner.enabled = false; return; }
            if (sphere == null) Build();
            sphere.enabled = inner.enabled = true;

            var b = host.sprite.bounds;
            float d = Mathf.Max(b.size.x, b.size.y) * size;
            float t = Time.time * 2.2f + seed;
            // wabern: Breite und Hoehe pulsieren gegeneinander, die innere Schicht dreht sich langsam
            float wx = 1f + 0.07f * Mathf.Sin(t * 1.3f) + 0.03f * Mathf.Sin(t * 3.1f);
            float wy = 1f + 0.07f * Mathf.Sin(t * 1.3f + 1.9f) + 0.03f * Mathf.Sin(t * 2.7f + 0.6f);
            float k = d / sphereSprite.bounds.size.x;
            sphere.transform.localPosition = b.center;
            sphere.transform.localScale = new Vector3(k * wx, k * wy, 1f);
            inner.transform.localPosition = b.center;
            inner.transform.localScale = new Vector3(k * 0.92f * wy, k * 0.92f * wx, 1f);
            inner.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 40f + seed * 30f);

            var c = WorldBuilder.MedalColor(level);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.7f);
            sphere.color = new Color(c.r, c.g, c.b, 0.7f + 0.25f * pulse);
            inner.color = new Color(Mathf.Lerp(c.r, 1f, 0.4f), Mathf.Lerp(c.g, 1f, 0.4f), Mathf.Lerp(c.b, 1f, 0.4f), 0.35f + 0.25f * (1f - pulse));
            sphere.sortingLayerID = inner.sortingLayerID = host.sortingLayerID;
            sphere.sortingOrder = host.sortingOrder + 1;   // vorn: die Mitte ist durchsichtig, man sieht das Bild in der Blase
            inner.sortingOrder = host.sortingOrder + 1;
        }

        void Build()
        {
            if (unlit == null) unlit = new Material(Shader.Find("Sprites/Default"));   // leuchtet auch nachts
            sphere = Make("MedalSphere", SphereSprite);
            inner = Make("MedalSwirl", InnerSprite);
        }

        SpriteRenderer Make(string name, Sprite spr)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(host.transform, false);
            sr.sprite = spr;
            sr.sharedMaterial = unlit;
            return sr;
        }

        void OnDisable() { if (sphere != null) sphere.enabled = inner.enabled = false; }

        /// <summary>Blase: fast durchsichtige Mitte, leuchtender Rand, Glanzpunkt oben links.</summary>
        static Sprite SphereSprite
        {
            get
            {
                if (sphereSprite != null) return sphereSprite;
                sphereSprite = Paint(64, (r, x, y) =>
                {
                    if (r > 1f) return 0f;
                    float rim = Mathf.SmoothStep(0.6f, 0.97f, r) * (1f - Mathf.SmoothStep(0.97f, 1f, r));
                    float fill = 0.12f;
                    float hx = x + 0.38f, hy = y - 0.4f;
                    float spot = Mathf.Clamp01(1f - (hx * hx + hy * hy) / 0.05f);
                    return Mathf.Clamp01(fill + rim * 0.85f + spot * 0.8f);
                });
                return sphereSprite;
            }
        }

        /// <summary>Innere Schicht: zwei weiche Schlieren, die sich drehen (das Wabern im Inneren).</summary>
        static Sprite InnerSprite
        {
            get
            {
                if (innerSprite != null) return innerSprite;
                innerSprite = Paint(64, (r, x, y) =>
                {
                    if (r > 0.95f) return 0f;
                    float ang = Mathf.Atan2(y, x);
                    float swirl = 0.5f + 0.5f * Mathf.Sin(ang * 2f + r * 5f);
                    return swirl * Mathf.SmoothStep(0.3f, 0.85f, r) * (1f - Mathf.SmoothStep(0.85f, 0.95f, r)) * 0.6f;
                });
                return innerSprite;
            }
        }

        static Sprite Paint(int n, System.Func<float, float, float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(u * u + v * v), u, v));
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
