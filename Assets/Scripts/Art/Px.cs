using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Pixel-Leinwand zum prozeduralen Zeichnen. Gezeichnet wird in "logischen" Koordinaten
    /// (16 pro Feld); intern wird mit <see cref="scale"/>-facher Aufloesung gerastert, damit Formen,
    /// Lichtverlaeufe und Konturen feiner werden (malerischer Look).
    /// </summary>
    public class Px
    {
        /// <summary>Standard-Aufloesungsfaktor fuer Welt-Grafik und Icons (2 = 32 px pro Feld).</summary>
        public static int DefaultScale = 2;

        public readonly int w, h;          // logisch
        public readonly int pw, ph, s;     // physisch
        public readonly Color[] c;
        public System.Random r;
        readonly int seed;

        // Licht kommt von oben links, leicht von vorn
        static readonly Vector3 LightDir = new Vector3(-0.5f, 0.62f, 0.6f).normalized;

        public Px(int w, int h, int seed = 1, int scale = -1)
        {
            this.w = w; this.h = h;
            s = scale > 0 ? scale : DefaultScale;
            pw = w * s; ph = h * s;
            c = new Color[pw * ph];
            r = new System.Random(seed);
            this.seed = seed;
        }

        public float Rand() => (float)r.NextDouble();
        public int RandI(int a, int b) => r.Next(a, b);

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        bool InP(int x, int y) => x >= 0 && y >= 0 && x < pw && y < ph;
        public Color Get(int x, int y) => In(x, y) ? c[(y * s) * pw + x * s] : Color.clear;
        public bool Solid(int x, int y) => In(x, y) && c[(y * s) * pw + x * s].a > 0.05f;
        public bool SolidP(int x, int y) => InP(x, y) && c[y * pw + x].a > 0.05f;
        public Color GetP(int x, int y) => InP(x, y) ? c[y * pw + x] : Color.clear;

        public void SetP(int x, int y, Color col)
        {
            if (!InP(x, y)) return;
            int i = y * pw + x;
            if (col.a >= 0.999f || col.a <= 0.001f) { c[i] = col; return; } // Alpha 0 radiert
            var d = c[i];
            float a = col.a + d.a * (1 - col.a);
            if (a <= 0) return;
            c[i] = new Color((col.r * col.a + d.r * d.a * (1 - col.a)) / a, (col.g * col.a + d.g * d.a * (1 - col.a)) / a, (col.b * col.a + d.b * d.a * (1 - col.a)) / a, a);
        }

        /// <summary>Ein logisches Pixel (= s×s physische Pixel).</summary>
        public void Set(int x, int y, Color col)
        {
            for (int yy = 0; yy < s; yy++) for (int xx = 0; xx < s; xx++) SetP(x * s + xx, y * s + yy, col);
        }

        public void Fill(Color col) { for (int i = 0; i < c.Length; i++) c[i] = col; }

        public void Rect(int x, int y, int rw, int rh, Color col)
        {
            for (int yy = y * s; yy < (y + rh) * s; yy++) for (int xx = x * s; xx < (x + rw) * s; xx++) SetP(xx, yy, col);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color col)
        {
            int x0 = Mathf.FloorToInt((cx - rx - 1) * s), x1 = Mathf.CeilToInt((cx + rx + 1) * s);
            int y0 = Mathf.FloorToInt((cy - ry - 1) * s), y1 = Mathf.CeilToInt((cy + ry + 1) * s);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = ((x + 0.5f) / s - cx) / Mathf.Max(0.01f, rx), dy = ((y + 0.5f) / s - cy) / Mathf.Max(0.01f, ry);
                    if (dx * dx + dy * dy <= 1f) SetP(x, y, col);
                }
        }

        public void Circle(float cx, float cy, float rad, Color col) => Ellipse(cx, cy, rad, rad, col);

        public void Line(float x0, float y0, float x1, float y1, Color col, int thick = 1)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * s) + 1;
            int tp = Mathf.Max(1, thick * s - (thick == 1 ? s / 2 : 0)); // duenne Linien werden feiner
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) * s), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) * s);
                for (int ky = 0; ky < tp; ky++) for (int kx = 0; kx < tp; kx++) SetP(x + kx, y + ky, col);
            }
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 d, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, d.x)) * s), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, d.x)) * s);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, d.y)) * s), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, d.y)) * s);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
                    float s1 = Cross(b - a, p - a), s2 = Cross(d - b, p - b), s3 = Cross(a - d, p - d);
                    bool neg = s1 < 0 || s2 < 0 || s3 < 0, pos = s1 > 0 || s2 > 0 || s3 > 0;
                    if (!(neg && pos)) SetP(x, y, col);
                }
        }
        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        // ------------------------------------------------------------------ Malerisches Licht

        /// <summary>Deterministisches Rauschen 0..1 pro physischem Pixel.</summary>
        public float Hash(int x, int y)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 2147483;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Weiches Wertrauschen (logische Einheiten, freq = Flecken pro Einheit).</summary>
        public float Noise(float x, float y, float freq)
        {
            x *= freq; y *= freq;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), cc = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(cc, d, fx), fy);
        }

        /// <summary>
        /// Plastische Kugel/Ellipse mit Licht von oben links: dunkle, kuehle Schatten unten rechts,
        /// warme Lichter oben links. ramp: Farben von Schatten bis Licht. Das ist der Kern des
        /// malerischen "Ghibli"-Looks (Laubbuesche, Steine, Fruechte).
        /// </summary>
        public void Blob(float cx, float cy, float rx, float ry, Color[] ramp, float bias = 0f, float noise = 0.12f, float alpha = 1f)
        {
            int x0 = Mathf.FloorToInt((cx - rx - 1) * s), x1 = Mathf.CeilToInt((cx + rx + 1) * s);
            int y0 = Mathf.FloorToInt((cy - ry - 1) * s), y1 = Mathf.CeilToInt((cy + ry + 1) * s);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = ((x + 0.5f) / s - cx) / Mathf.Max(0.01f, rx), ny = ((y + 0.5f) / s - cy) / Mathf.Max(0.01f, ry);
                    float d2 = nx * nx + ny * ny;
                    if (d2 > 1f) continue;
                    float nz = Mathf.Sqrt(1f - d2);
                    float lam = Vector3.Dot(new Vector3(nx, ny, nz), LightDir);
                    float v = lam * 0.55f + 0.45f + bias + (Hash(x, y) - 0.5f) * noise;
                    int idx = Mathf.Clamp(Mathf.FloorToInt(v * ramp.Length), 0, ramp.Length - 1);
                    var col = ramp[idx]; col.a = alpha;
                    SetP(x, y, col);
                }
        }

        /// <summary>Blob mit automatisch erzeugtem Farbverlauf aus einer Grundfarbe.</summary>
        public void BlobC(float cx, float cy, float rx, float ry, Color baseCol, float bias = 0f) => Blob(cx, cy, rx, ry, Art.Ramp(baseCol), bias, 0.1f);

        /// <summary>Ein einzelnes physisches Pixel an einer logischen (Komma-)Position.</summary>
        public void Dot(float x, float y, Color col) => SetP(Mathf.FloorToInt(x * s), Mathf.FloorToInt(y * s), col);

        /// <summary>Faerbt vorhandene Pixel nach Hoehe ein (oben heller, unten dunkler) – fuer flaechige Formen.</summary>
        public void VerticalLight(float strength = 0.18f)
        {
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    int i = y * pw + x;
                    if (c[i].a < 0.05f) continue;
                    float t = y / (float)ph - 0.5f;
                    c[i] = t > 0 ? Art.Light(c[i], t * strength) : Art.Dark(c[i], 1 + t * strength * 1.4f);
                }
        }

        /// <summary>Aufhellen oben, Abdunkeln unten an den Kanten: gibt Volumen.</summary>
        public void Shade(float light = 0.18f, float dark = 0.22f)
        {
            var copy = (Color[])c.Clone();
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    var col = copy[y * pw + x];
                    if (col.a < 0.05f) continue;
                    bool topEdge = y + s >= ph || copy[(y + s) * pw + x].a < 0.05f;
                    bool botEdge = y - s < 0 || copy[(y - s) * pw + x].a < 0.05f;
                    if (topEdge) c[y * pw + x] = Art.Light(col, light);
                    else if (botEdge) c[y * pw + x] = Art.Dark(col, 1 - dark);
                }
        }

        /// <summary>Feine, farbige Kontur (1 physisches Pixel): dunkler, kuehler Ton der Nachbarfarbe.</summary>
        public void Outline(float darkness = 0.45f, float alpha = 1f)
        {
            var copy = (Color[])c.Clone();
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    if (copy[y * pw + x].a > 0.05f) continue;
                    Color n = Color.clear;
                    if (x > 0 && copy[y * pw + x - 1].a > 0.05f) n = copy[y * pw + x - 1];
                    else if (x < pw - 1 && copy[y * pw + x + 1].a > 0.05f) n = copy[y * pw + x + 1];
                    else if (y > 0 && copy[(y - 1) * pw + x].a > 0.05f) n = copy[(y - 1) * pw + x];
                    else if (y < ph - 1 && copy[(y + 1) * pw + x].a > 0.05f) n = copy[(y + 1) * pw + x];
                    if (n.a > 0.05f)
                    {
                        var o = Art.Dark(n, darkness); o.r *= 0.8f; o.g *= 0.88f; o.b = Mathf.Min(1, o.b * 1.15f + 0.05f); o.a = alpha;
                        c[y * pw + x] = o;
                    }
                }
        }

        public void Speckle(int x, int y, int rw, int rh, Color col, float density)
        {
            for (int yy = y * s; yy < (y + rh) * s; yy++) for (int xx = x * s; xx < (x + rw) * s; xx++) if (SolidP(xx, yy) && Rand() < density) SetP(xx, yy, col);
        }

        /// <summary>Kachelbare Textur (fuer Boden-Materialien).</summary>
        public Texture2D ToTexture(TextureWrapMode wrap = TextureWrapMode.Repeat, FilterMode filter = FilterMode.Point)
        {
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = wrap };
            tex.SetPixels(c);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Setzt ein physisches Pixel mit Umbruch an den Raendern (nahtlose Texturen).</summary>
        public void SetWrap(int x, int y, Color col) => SetP(((x % pw) + pw) % pw, ((y % ph) + ph) % ph, col);

        public Sprite ToSprite(Vector2 pivot, float ppu = 16, Vector4 border = default)
        {
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(c);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, pw, ph), pivot, ppu * s, 0, SpriteMeshType.FullRect, border * s);
        }
    }
}
