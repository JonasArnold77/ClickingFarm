using UnityEngine;

namespace SeedClicker
{
    /// <summary>Kleine Pixel-Leinwand zum prozeduralen Zeichnen von Platzhalter-Pixel-Art.</summary>
    public class Px
    {
        public readonly int w, h;
        public readonly Color[] c;
        public System.Random r;

        public Px(int w, int h, int seed = 1)
        {
            this.w = w; this.h = h;
            c = new Color[w * h];
            r = new System.Random(seed);
        }

        public float Rand() => (float)r.NextDouble();
        public int RandI(int a, int b) => r.Next(a, b);

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        public Color Get(int x, int y) => In(x, y) ? c[y * w + x] : Color.clear;
        public bool Solid(int x, int y) => In(x, y) && c[y * w + x].a > 0.05f;

        public void Set(int x, int y, Color col)
        {
            if (!In(x, y)) return;
            int i = y * w + x;
            if (col.a >= 0.999f) c[i] = col;
            else
            {
                var d = c[i];
                float a = col.a + d.a * (1 - col.a);
                if (a <= 0) return;
                c[i] = new Color((col.r * col.a + d.r * d.a * (1 - col.a)) / a, (col.g * col.a + d.g * d.a * (1 - col.a)) / a, (col.b * col.a + d.b * d.a * (1 - col.a)) / a, a);
            }
        }

        public void Fill(Color col) { for (int i = 0; i < c.Length; i++) c[i] = col; }

        public void Rect(int x, int y, int rw, int rh, Color col)
        {
            for (int yy = y; yy < y + rh; yy++) for (int xx = x; xx < x + rw; xx++) Set(xx, yy, col);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color col)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / Mathf.Max(0.01f, rx), dy = (y + 0.5f - cy) / Mathf.Max(0.01f, ry);
                    if (dx * dx + dy * dy <= 1f) Set(x, y, col);
                }
        }

        public void Circle(float cx, float cy, float rad, Color col) => Ellipse(cx, cy, rad, rad, col);

        public void Line(float x0, float y0, float x1, float y1, Color col, int thick = 1)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int k = 0; k < thick; k++) Set(x + k, y, col);
            }
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 d, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, d.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, d.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, d.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, d.y)));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float s1 = Cross(b - a, p - a), s2 = Cross(d - b, p - b), s3 = Cross(a - d, p - d);
                    bool neg = s1 < 0 || s2 < 0 || s3 < 0, pos = s1 > 0 || s2 > 0 || s3 > 0;
                    if (!(neg && pos)) Set(x, y, col);
                }
        }
        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        /// <summary>Aufhellen oben, Abdunkeln unten: gibt Volumen.</summary>
        public void Shade(float light = 0.18f, float dark = 0.22f)
        {
            var copy = (Color[])c.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var col = copy[y * w + x];
                    if (col.a < 0.05f) continue;
                    bool topEdge = y + 1 >= h || copy[(y + 1) * w + x].a < 0.05f;
                    bool botEdge = y - 1 < 0 || copy[(y - 1) * w + x].a < 0.05f;
                    if (topEdge) c[y * w + x] = Art.Light(col, light);
                    else if (botEdge) c[y * w + x] = Art.Dark(col, 1 - dark);
                }
        }

        /// <summary>Dunkle Kontur um alle Formen – der typische Pixel-Art-Look.</summary>
        public void Outline(float darkness = 0.45f, float alpha = 1f)
        {
            var copy = (Color[])c.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a > 0.05f) continue;
                    Color n = Color.clear;
                    if (x > 0 && copy[y * w + x - 1].a > 0.05f) n = copy[y * w + x - 1];
                    else if (x < w - 1 && copy[y * w + x + 1].a > 0.05f) n = copy[y * w + x + 1];
                    else if (y > 0 && copy[(y - 1) * w + x].a > 0.05f) n = copy[(y - 1) * w + x];
                    else if (y < h - 1 && copy[(y + 1) * w + x].a > 0.05f) n = copy[(y + 1) * w + x];
                    if (n.a > 0.05f)
                    {
                        var o = Art.Dark(n, darkness); o.r *= 0.85f; o.g *= 0.8f; o.b = Mathf.Min(1, o.b * 1.1f + 0.04f); o.a = alpha;
                        c[y * w + x] = o;
                    }
                }
        }

        public void Speckle(int x, int y, int rw, int rh, Color col, float density)
        {
            for (int yy = y; yy < y + rh; yy++) for (int xx = x; xx < x + rw; xx++) if (Solid(xx, yy) && Rand() < density) Set(xx, yy, col);
        }

        public Sprite ToSprite(Vector2 pivot, float ppu = 16, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(c);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        }
    }
}
