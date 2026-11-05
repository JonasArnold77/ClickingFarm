using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Feste Comic-Linie in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Klassische Sprite-Kontur: Unter dem Bild liegen einfarbige Kopien seiner Form, rundherum um eine feste Strecke
    /// versetzt (Pflanze 16 Richtungen, Erntestueck 8). Zusammen ergeben sie eine gleichmaessig dicke, glatte Linie,
    /// die sich genau an die Aussenlinie legt – egal wie gross das Bild ist.
    /// Einfarbig fuellt der eingebaute Shader "GUI/Text Shader" (Farbe vom SpriteRenderer, Form aus dem Alpha des Bildes).
    /// Dazu bei der Pflanze selbst (nicht bei den kleinen Erntestuecken) ein leichtes Strahlen wie bei verzauberten Waffen:
    /// Strahlen ragen wie bei einer Sonne aus der Kontur und wabern, die Linie pulsiert hell, leuchtende Funken
    /// steigen langsam auf. Gold strahlt am staerksten.
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host;
        readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> glow = new List<SpriteRenderer>();   // Sonnenstrahlen rund um die Kontur
        float moteT;
        int level, dirs = 16;
        float width = 0.0175f;   // Linienbreite in Welt-Einheiten
        (Sprite, int, int, bool, int, int) lastKey;
        bool shown;
        static Material silhouetteMat;
        static bool shaderLooked;

        /// <summary>Stufe setzen (unter 2 = keine Linie). width: Linienbreite in Welt-Einheiten, dirs: Zahl der Kopien
        /// (mehr = glatter; fuer viele kleine Erntestuecke reichen 8).</summary>
        public static void Set(SpriteRenderer host, int level, float width = 0.0175f, int dirs = 16)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
            }
            a.level = level; a.width = width; a.dirs = Mathf.Clamp(dirs, 4, 24);
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2 && Mat != null;
            if (!on) { if (shown) { Show(0); shown = false; } return; }
            bool big = dirs >= 16;   // die Pflanze selbst strahlt, die kleinen Erntestuecke nicht
            if (big) UpdateGlow();
            while (copies.Count < dirs) copies.Add(Make());
            if (!shown) Show(dirs);

            // nur neu setzen, wenn sich Bild, Stufe, Reihenfolge, Spiegelung oder Groesse geaendert haben
            var ls0 = host.transform.lossyScale;
            var key = (host.sprite, level, host.sortingOrder, host.flipX, Mathf.RoundToInt(ls0.x * 200f), Mathf.RoundToInt(ls0.y * 200f));
            if (shown && key.Equals(lastKey)) return;
            lastKey = key; shown = true;
            var c = WorldBuilder.MedalColor(level);
            var col = new Color(c.r, c.g, c.b, 1f);
            // feste Breite in der Welt, umgerechnet in die (skalierte) Einheit des Bildes
            var ls = host.transform.lossyScale;
            float rx = width / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), ry = width / Mathf.Max(0.0001f, Mathf.Abs(ls.y));
            for (int i = 0; i < dirs; i++)
            {
                var sr = copies[i];
                float ang = i * Mathf.PI * 2f / dirs;
                sr.sprite = host.sprite;
                sr.flipX = host.flipX; sr.flipY = host.flipY;
                sr.transform.localPosition = new Vector3(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry, 0f);
                sr.color = col;
                sr.sortingLayerID = host.sortingLayerID;
                sr.sortingOrder = host.sortingOrder - 1;   // unter dem Bild: nur der Rand ragt heraus
            }
        }

        void Show(int n)
        {
            for (int i = 0; i < copies.Count; i++) if (copies[i] != null) copies[i].enabled = i < n;
            if (n == 0) foreach (var g in glow) if (g != null) g.enabled = false;
        }

        /// <summary>Sonnenstrahlen: ragen rundherum aus der Kontur heraus und wabern (Laenge, Breite, Helligkeit, leichtes
        /// Schwanken), dazu pulsiert die Linie und Funken steigen auf – nur solange die Pflanze im Bild ist.</summary>
        void UpdateGlow()
        {
            while (glow.Count < RayCount) glow.Add(MakeRay());
            if (!host.isVisible) return;   // ausserhalb des Bilds nichts bewegen (grosse Hoefe)
            var c = WorldBuilder.MedalColor(level);
            float power = level >= 4 ? 1.3f : level == 3 ? 1f : 0.8f;
            float t = Time.time * 2.2f + transform.position.x * 1.7f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t);

            var b = host.bounds;
            Vector2 center = b.center;
            float rx = b.extents.x * 0.72f, ry = b.extents.y * 0.8f, size = Mathf.Max(b.size.x, b.size.y);
            var rayCol = Color.Lerp(c, Color.white, 0.25f);
            for (int i = 0; i < glow.Count; i++)
            {
                var r = glow[i];
                r.enabled = true;
                float ph = i * 2.39996f + transform.position.y * 3.1f;   // jeder Strahl im eigenen Takt
                float ang = (i + 0.5f) * Mathf.PI * 2f / RayCount + Mathf.Sin(t * 0.45f + ph) * 0.12f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float len = size * (0.36f + 0.2f * Mathf.Sin(t * 1.15f + ph)) * power;
                float wid = size * (0.13f + 0.04f * Mathf.Sin(t * 0.8f + ph * 1.3f));
                r.transform.position = center + new Vector2(dir.x * rx, dir.y * ry);
                r.transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg - 90f);
                // Groesse in der Welt (die Strahlen liegen in einem eigenen, unskalierten Behaelter)
                float rw = RaySprite.bounds.size.x, rh = RaySprite.bounds.size.y;
                r.transform.localScale = new Vector3(wid / rw, len / rh, 1f);
                float a = (0.55f + 0.3f * Mathf.Sin(t * 1.6f + ph * 0.7f)) * power;
                r.color = new Color(rayCol.r, rayCol.g, rayCol.b, Mathf.Clamp01(a));
                r.sortingLayerID = host.sortingLayerID;
                r.sortingOrder = host.sortingOrder + 30;   // ueber den Nachbarpflanzen; sie setzen an der Kontur an und blenden dort weich ein
            }
            // die Linie selbst strahlt (heller, pulsiert), Funken steigen auf
            var lineCol = Color.Lerp(c, Color.white, 0.12f + 0.28f * (pulse - 0.5f) * power);
            lineCol.a = 1f;
            for (int i = 0; i < copies.Count && i < dirs; i++) if (copies[i] != null) copies[i].color = lineCol;
            moteT -= Time.deltaTime * power;
            if (moteT > 0f) return;
            moteT = Random.Range(0.35f, 0.8f);
            var p = new Vector2(Random.Range(b.min.x + b.size.x * 0.2f, b.max.x - b.size.x * 0.2f), Random.Range(b.min.y + b.size.y * 0.3f, b.max.y));
            var mc = Color.Lerp(c, Color.white, 0.35f);
            Particles.I?.Spawn(p, "spark", mc, new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(0.25f, 0.45f), 0), 1.4f, 0.06f, 0.015f, 0f);
        }

        const int RayCount = 10;
        static Sprite raySprite;
        static Material rayMat;
        static Transform rayRoot;

        SpriteRenderer MakeRay()
        {
            // nicht ans Bild haengen: dessen ungleiche Skalierung wuerde gedrehte Strahlen verzerren
            if (rayRoot == null) rayRoot = new GameObject("MedalRays").transform;
            var sr = new GameObject("MedalRay").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(rayRoot, false);
            sr.enabled = false;   // erst zeigen, wenn er einmal platziert ist
            sr.sprite = RaySprite;
            if (rayMat == null) { var sh = Shader.Find("Sprites/Default"); if (sh != null) rayMat = new Material(sh); }   // unbeleuchtet: Licht leuchtet auch nachts
            if (rayMat != null) sr.sharedMaterial = rayMat;
            return sr;
        }

        /// <summary>Spitz zulaufender Lichtstrahl: am Fuss breit und hell, zur Spitze schmal und durchsichtig.</summary>
        static Sprite RaySprite
        {
            get
            {
                if (raySprite != null) return raySprite;
                const int w = 32, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fy = (y + 0.5f) / h, fx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float half = Mathf.Lerp(1f, 0.15f, fy);                       // verjuengt sich zur Spitze
                        float side = Mathf.Clamp01(1f - fx / half);
                        float a = side * side * Mathf.Pow(1f - fy, 1.3f) * Mathf.SmoothStep(0f, 1f, fy * 6f);
                        px[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(px); tex.Apply();
                return raySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
            }
        }

        void OnDisable() { Show(0); shown = false; }

        void OnDestroy() { foreach (var g in glow) if (g != null) Destroy(g.gameObject); }

        SpriteRenderer Make()
        {
            var sr = new GameObject("MedalLine").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(host.transform, false);
            sr.sharedMaterial = Mat;
            return sr;
        }

        /// <summary>Einfarbige Silhouette: Farbe vom SpriteRenderer, Form aus dem Alpha des Bildes.</summary>
        static Material Mat
        {
            get
            {
                if (!shaderLooked)
                {
                    shaderLooked = true;
                    var sh = Shader.Find("GUI/Text Shader");
                    if (sh != null) silhouetteMat = new Material(sh);
                }
                return silhouetteMat;
            }
        }
    }
}
