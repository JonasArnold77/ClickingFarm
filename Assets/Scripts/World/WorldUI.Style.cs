using UnityEngine;
using UnityEngine.UI;

namespace SeedClicker
{
    /// <summary>
    /// Look der Oberflaeche, passend zur gemalten Welt: dunkle Holzschilder fuer die Anzeigen oben (warmes Braun,
    /// heller Rand, Lichtkante oben), Pergament fuer Fenster und Karten (cremig, zum Rand hin waermer, brauner Rahmen)
    /// und gewoelbte Knoepfe (helle Oberkante, dunklere Unterlippe, Kontur), alle mit weichem Schlagschatten.
    /// Die Bilder werden einmal im Code gemalt und als 9-Slice gestreckt.
    /// </summary>
    public partial class WorldUI
    {
        static Sprite plaqueSprite, parchmentSprite, buttonSprite;
        const int StyleN = 96; const float StyleR = 26f;

        /// <summary>Abstand zum Rand des abgerundeten Rechtecks (negativ = innen).</summary>
        static float RoundDist(float x, float y, int n, float r)
        {
            float dx = Mathf.Max(0, Mathf.Max(r - x, x - (n - r)));
            float dy = Mathf.Max(0, Mathf.Max(r - y, y - (n - r)));
            float outside = Mathf.Sqrt(dx * dx + dy * dy) - r;
            if (dx > 0 || dy > 0) return outside;
            return -Mathf.Min(Mathf.Min(x, n - x), Mathf.Min(y, n - y));
        }

        static Sprite MakeStyle(System.Func<float, float, float, Color> paint)
        {
            var tex = new Texture2D(StyleN, StyleN, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[StyleN * StyleN];
            for (int y = 0; y < StyleN; y++)
                for (int x = 0; x < StyleN; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = RoundDist(fx, fy, StyleN, StyleR);
                    var c = paint(d, fx / StyleN, fy / StyleN);
                    c.a *= Mathf.Clamp01(0.5f - d);   // weiche Kante
                    px[y * StyleN + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, StyleN, StyleN), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(32, 32, 32, 32));
        }

        /// <summary>Dunkles Holzschild: Maserung, heller Rand, Lichtkante oben.</summary>
        static Sprite Plaque => plaqueSprite != null ? plaqueSprite : plaqueSprite = MakeStyle((d, u, v) =>
        {
            var wood = new Color(0.30f, 0.20f, 0.12f); var woodHi = new Color(0.38f, 0.26f, 0.16f);
            var rim = new Color(0.62f, 0.44f, 0.26f); var ink = new Color(0.14f, 0.08f, 0.04f);
            if (d > -2.5f) return ink;
            if (d > -6f) return Color.Lerp(rim, rim * 1.15f, v);
            float grain = Mathf.PerlinNoise(u * 3f, v * 22f) * 0.6f + Mathf.PerlinNoise(u * 9f, v * 60f) * 0.4f;
            var c = Color.Lerp(wood, woodHi, grain);
            if (d > -9f && v > 0.55f) c = Color.Lerp(c, new Color(0.55f, 0.4f, 0.26f), 0.5f);   // Lichtkante oben
            c.a = 1f;
            return c;
        });

        /// <summary>Pergament: cremige Mitte, waermerer Rand, brauner Rahmen mit feiner Innenlinie.</summary>
        static Sprite Parchment => parchmentSprite != null ? parchmentSprite : parchmentSprite = MakeStyle((d, u, v) =>
        {
            var paper = new Color(1f, 0.97f, 0.88f); var edge = new Color(0.94f, 0.85f, 0.66f);
            var frame = new Color(0.56f, 0.38f, 0.21f); var ink = new Color(0.32f, 0.2f, 0.1f);
            if (d > -2.5f) return ink;
            if (d > -7f) return Color.Lerp(frame, frame * 1.18f, v);
            if (d > -8.5f) return new Color(0.98f, 0.92f, 0.78f);                       // helle Innenlinie
            float k = Mathf.Clamp01((-d - 8.5f) / 22f);
            var c = Color.Lerp(edge, paper, k);
            c = Color.Lerp(c, edge, Mathf.PerlinNoise(u * 7f, v * 7f) * 0.18f);          // leichte Flecken
            c.a = 1f;
            return c;
        });

        /// <summary>Gewoelbter Knopf (wird mit der Knopffarbe eingefaerbt): helle Oberkante, dunkle Unterlippe, Kontur.</summary>
        static Sprite ButtonStyle => buttonSprite != null ? buttonSprite : buttonSprite = MakeStyle((d, u, v) =>
        {
            if (d > -2.5f) return new Color(0.36f, 0.3f, 0.26f);                          // Kontur
            if (v < 0.17f) return new Color(0.66f, 0.62f, 0.6f);                          // Unterlippe
            float g = Mathf.Lerp(0.86f, 1f, Mathf.InverseLerp(0.17f, 0.9f, v));
            var c = new Color(g, g, g);
            if (v > 0.74f && d < -5f && d > -12f) c = Color.Lerp(c, Color.white, 0.6f);  // Glanzkante
            c.a = 1f;
            return c;
        });

        static void SoftShadow(Graphic g, float dist = 6f, float alpha = 0.32f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0.1f, 0.06f, 0.02f, alpha);
            s.effectDistance = new Vector2(0, -dist);
        }

        /// <summary>Flaeche im Spielstil: dunkle Farben werden Holzschild, helle Pergament, der Rest ein schlichtes Rund.</summary>
        static Image StyledPanel(Transform parent, string name, Color col)
        {
            var img = UIKit.Rect(parent, name).gameObject.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            float lum = col.r * 0.3f + col.g * 0.59f + col.b * 0.11f;
            if (lum < 0.3f)
            {
                img.sprite = Plaque;
                img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(col.a * 1.4f));
                SoftShadow(img, 5f, 0.3f * col.a);
            }
            else if (lum > 0.82f)
            {
                img.sprite = Parchment;
                img.color = new Color(1f, 1f, 1f, col.a);
                SoftShadow(img, 7f, 0.28f);
            }
            else
            {
                img.sprite = Rounded;
                img.color = col;
            }
            return img;
        }
    }
}
