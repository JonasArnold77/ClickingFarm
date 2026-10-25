using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Malerische Pixel-Art im Stil von Ghibli-Landschaften (Prinzessin Mononoke):
    /// sattes, sonnendurchflutetes Gruen, kuehle blaugruene Schatten, weiche Lichtkanten,
    /// buschige Laubkronen aus vielen Blattballen, Kopfsteinpflaster, gruene Ziegeldaecher.
    /// Alles prozedural und eigenstaendig gezeichnet.
    /// </summary>
    public static partial class Art
    {
        // ------------------------------------------------------------------ Farbverlaeufe (Schatten -> Licht)

        static Color[] R(params string[] hex) { var r = new Color[hex.Length]; for (int i = 0; i < hex.Length; i++) r[i] = H(hex[i]); return r; }

        public static readonly Color[] RGrass = R("#38623A", "#4B7A3D", "#5F9142", "#76A848", "#93BF55", "#B9D46E");
        public static readonly Color[] RLeaf = R("#173229", "#1F4634", "#29603B", "#3B7E40", "#589E45", "#82BD51", "#BAD86C");
        public static readonly Color[] RLeafWarm = R("#2A3A22", "#3D5228", "#55702E", "#728E36", "#97AE44", "#C3CB62", "#E8DE8A");
        public static readonly Color[] RPine = R("#0F2A2A", "#163B33", "#1F4F3C", "#2A6545", "#3F8150", "#64A05E");
        public static readonly Color[] RBark = R("#24160F", "#382218", "#4F3221", "#68452C", "#83593A", "#A0744C");
        public static readonly Color[] RStone = R("#363A46", "#4C515E", "#666C79", "#848A96", "#A6ACB6", "#CBD0D6", "#E6E8EA");
        public static readonly Color[] RMossStone = R("#2E3A34", "#435246", "#5C6A58", "#7A8770", "#9AA68A", "#BCC4A6");
        public static readonly Color[] RDirt = R("#6E5034", "#86643F", "#9E7B4E", "#B4925F", "#C9AA76", "#DCC394");
        public static readonly Color[] RSoil = R("#2E1C13", "#3F271A", "#533423", "#68432D", "#7D5538", "#946A46");
        public static readonly Color[] RSoilWet = R("#1E120D", "#2A1912", "#382218", "#462C20", "#56382A", "#6A4836");
        public static readonly Color[] RWater = R("#1B4565", "#235C80", "#2D769C", "#3E92B6", "#64B0CC", "#A2D4E2");
        public static readonly Color[] RRoof = R("#11302A", "#1A4337", "#245A45", "#317454", "#4A9268", "#73B485");
        public static readonly Color[] RBrick = R("#3E1914", "#5A241B", "#783222", "#94432D", "#AE5A3E", "#C77B5C");
        public static readonly Color[] RPlaster = R("#7A6E5E", "#9C8F7A", "#BCAE94", "#D6CAAE", "#EAE1C9", "#F7F1E0");
        public static readonly Color[] RWood = R("#2E1C12", "#47291A", "#633B24", "#7F5030", "#9E6A40", "#C08D5A");
        public static readonly Color[] RStraw = R("#5A4520", "#7A5E2A", "#9A7A36", "#B89746", "#D3B45E", "#E8D184");
        public static readonly Color[] RCloth = R("#6E4A36", "#93684A", "#B98C62", "#D4AE80", "#E8CCA2", "#F6E6C8");

        public static readonly Color Plank0 = H("#3A2416"), Plank1 = H("#5A3820"), Plank2 = H("#7A5030"), Plank3 = H("#9C6C42");

        static readonly Dictionary<Color, Color[]> rampCache = new Dictionary<Color, Color[]>();

        /// <summary>Erzeugt aus einer Grundfarbe einen malerischen Verlauf: kuehle Schatten, warme Lichter.</summary>
        public static Color[] Ramp(Color b)
        {
            if (rampCache.TryGetValue(b, out var r)) return r;
            Color Cool(Color c, float f) => new Color(c.r * f * 0.82f, c.g * f * 0.92f, Mathf.Min(1, c.b * f * 1.05f + 0.06f), c.a);
            Color Warm(Color c, float f) => new Color(Mathf.Min(1, c.r + (1 - c.r) * f + 0.03f), Mathf.Min(1, c.g + (1 - c.g) * f + 0.02f), c.b + (1 - c.b) * f * 0.6f, c.a);
            r = new[] { Cool(b, 0.42f), Cool(b, 0.62f), Cool(b, 0.82f), b, Warm(b, 0.18f), Warm(b, 0.38f) };
            rampCache[b] = r;
            return r;
        }

        internal static Color At(Color[] ramp, float t) => ramp[Mathf.Clamp(Mathf.FloorToInt(t * ramp.Length), 0, ramp.Length - 1)];

        /// <summary>Periodisches Rauschen ueber eine Kachel (nahtlos kachelbar).</summary>
        internal static float TileNoise(Px c, float x, float y, int period, int salt)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H2(int a, int b) => c.Hash(((a % period) + period) % period + salt * 101, ((b % period) + period) % period + salt * 37);
            return Mathf.Lerp(Mathf.Lerp(H2(xi, yi), H2(xi + 1, yi), fx), Mathf.Lerp(H2(xi, yi + 1), H2(xi + 1, yi + 1), fx), fy);
        }

        // ------------------------------------------------------------------ Boden

        static Sprite GrassTile(int v)
        {
            var c = new Px(16, 16, 77);
            int P = c.pw;
            for (int y = 0; y < c.ph; y++)
                for (int x = 0; x < c.pw; x++)
                {
                    // grosse, weiche Licht-/Schattenflecken (kachelbar ueber 4 Zellen)
                    float n = TileNoise(c, x / 8f, y / 8f, 4, v) * 0.6f + TileNoise(c, x / 3f, y / 3f, 10, v + 7) * 0.4f;
                    float t = 0.28f + n * 0.5f + (c.Hash(x + v * 97, y) - 0.5f) * 0.12f;
                    c.SetP(x, y, At(RGrass, t));
                }
            // Grashalme: dunkler Fuss, heller Spitzenstrich
            var rnd = new System.Random(v * 13 + 5);
            for (int i = 0; i < 46; i++)
            {
                int x = rnd.Next(0, P), y = rnd.Next(0, c.ph - 3);
                int len = 2 + rnd.Next(0, 3);
                c.SetP(x, y, RGrass[1]);
                for (int k = 1; k <= len; k++) c.SetP(x + (k == len && rnd.Next(2) == 0 ? 1 : 0), y + k, k == len ? RGrass[5] : RGrass[3 + (k > 1 ? 1 : 0)]);
            }
            if (v == 3)
            {
                // ein paar Wildblumen
                foreach (var q in new[] { new Vector2(4, 5), new Vector2(11, 11), new Vector2(13, 3) })
                {
                    var fc = q.x > 10 ? H("#F4F1E6") : H("#F3D36B");
                    c.Dot(q.x, q.y, fc); c.Dot(q.x + 0.5f, q.y, fc); c.Dot(q.x, q.y + 0.5f, fc); c.Dot(q.x + 0.5f, q.y + 0.5f, H("#E89A3C"));
                }
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>Beet, das sich mit Nachbarbeeten verbindet. mask: 1=N, 2=O, 4=S, 8=W.</summary>
        static Sprite SoilTile(int mask, bool wet)
        {
            var c = new Px(16, 16, 300 + mask);
            var ramp = wet ? RSoilWet : RSoil;
            bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
            float x0 = w ? -1 : 1, x1 = e ? 17 : 15, y0 = s ? -1 : 1.2f, y1 = n ? 17 : 14.6f;
            for (int y = 0; y < c.ph; y++)
                for (int x = 0; x < c.pw; x++)
                {
                    float lx = (x + 0.5f) / c.s, ly = (y + 0.5f) / c.s;
                    if (lx < x0 || lx > x1 || ly < y0 || ly > y1) continue;
                    // runde Ecken
                    float rr = 2.2f;
                    float cx = Mathf.Clamp(lx, x0 + rr, x1 - rr), cy = Mathf.Clamp(ly, y0 + rr, y1 - rr);
                    bool cornerZone = (!w || !e) && (!s || !n);
                    if (cornerZone && (lx - cx) * (lx - cx) + (ly - cy) * (ly - cy) > rr * rr &&
                        ((lx < x0 + rr && !w) || (lx > x1 - rr && !e)) && ((ly < y0 + rr && !s) || (ly > y1 - rr && !n))) continue;
                    float tn = TileNoise(c, x / 4f, y / 4f, 8, mask) ;
                    float t = 0.3f + tn * 0.35f + (c.Hash(x, y) - 0.5f) * 0.15f;
                    // Rand oben im Schatten des Grases, unten ein heller Erdwall
                    if (!n && ly > y1 - 1.6f) t -= 0.25f;
                    if (!s && ly < y0 + 1.2f) t += 0.3f;
                    if (!w && lx < x0 + 0.8f) t -= 0.1f;
                    c.SetP(x, y, At(ramp, t));
                }
            // Erdkrumen als kleine plastische Klumpen
            var rnd = new System.Random(mask * 7 + (wet ? 3 : 0));
            for (int i = 0; i < 9; i++)
            {
                float cx = (float)(rnd.NextDouble() * (x1 - x0 - 3) + x0 + 1.5f), cy = (float)(rnd.NextDouble() * (y1 - y0 - 3) + y0 + 1.5f);
                c.Blob(cx, cy, 0.9f + (float)rnd.NextDouble() * 0.6f, 0.6f + (float)rnd.NextDouble() * 0.4f, ramp, 0.12f, 0.05f);
            }
            if (wet) for (int i = 0; i < 6; i++) c.Dot((float)rnd.NextDouble() * 12 + 2, (float)rnd.NextDouble() * 12 + 2, H("#6E7E8E"));
            c.Outline(0.6f, 0.55f);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite PathTile(int v)
        {
            var c = new Px(16, 16, 700 + v);
            for (int y = 0; y < c.ph; y++)
                for (int x = 0; x < c.pw; x++)
                    c.SetP(x, y, At(RDirt, 0.25f + TileNoise(c, x / 5f, y / 5f, 6, v) * 0.4f + (c.Hash(x, y) - 0.5f) * 0.12f));
            // Kopfsteinpflaster
            var rnd = new System.Random(v * 31 + 1);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    float cx = col * 4 + 2 + (row % 2) * 2 + (float)(rnd.NextDouble() - 0.5);
                    float cy = row * 4 + 2 + (float)(rnd.NextDouble() - 0.5);
                    if (cx > 15.5f) cx -= 16;
                    c.Blob(cx, cy, 1.7f + (float)rnd.NextDouble() * 0.3f, 1.4f, RMossStone, 0.05f, 0.08f);
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite WaterTile(int v)
        {
            var c = new Px(16, 16, 800);
            for (int y = 0; y < c.ph; y++)
                for (int x = 0; x < c.pw; x++)
                {
                    float t = 0.35f + TileNoise(c, x / 6f, y / 6f, 5, 1) * 0.25f;
                    float wave = Mathf.Sin((x + v * 4) * 0.4f + y * 0.9f);
                    if (wave > 0.92f) t += 0.35f; else if (wave > 0.8f) t += 0.15f;
                    c.SetP(x, y, At(RWater, t));
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite FloorTile()
        {
            var c = new Px(16, 16, 810);
            for (int y = 0; y < c.ph; y++)
                for (int x = 0; x < c.pw; x++)
                {
                    int bx = (x + (y / 16 % 2) * 8) % 16, by = y % 16;
                    bool seam = bx == 0 || by == 0;
                    c.SetP(x, y, seam ? RPlaster[1] : At(RPlaster, 0.45f + (c.Hash(x, y) - 0.5f) * 0.2f - by / 64f));
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Hindernisse

        static Sprite ObstacleG(string id)
        {
            var c = new Px(16, 16, id.Length * 31);
            switch (id)
            {
                case "grass":
                    {
                        // hohes Wildgras / Farn
                        var rnd = new System.Random(5);
                        for (int i = 0; i < 26; i++)
                        {
                            float bx = 3 + (float)rnd.NextDouble() * 10, top = 7 + (float)rnd.NextDouble() * 7, lean = ((float)rnd.NextDouble() - 0.5f) * 5;
                            int steps = 20;
                            for (int k = 0; k <= steps; k++)
                            {
                                float t = k / (float)steps;
                                c.Dot(bx + lean * t * t, 1.5f + (top - 1.5f) * t, At(RLeaf, 0.15f + t * 0.8f + (float)rnd.NextDouble() * 0.1f));
                            }
                        }
                        c.Dot(6, 11, H("#F3E7B6")); c.Dot(6.5f, 11, H("#F3E7B6")); c.Dot(11, 10, H("#E7B8D8"));
                        c.Outline(0.55f, 0.6f);
                        break;
                    }
                case "twig":
                    {
                        for (float t = 0; t <= 1; t += 0.02f)
                        {
                            float x = 3 + t * 10, y = 4 + t * 4;
                            c.Dot(x, y, At(RBark, 0.55f)); c.Dot(x, y - 0.5f, At(RBark, 0.3f)); c.Dot(x, y + 0.5f, At(RBark, 0.8f));
                        }
                        c.Line(8, 6, 10, 10, RBark[3]);
                        c.BlobC(11, 11.5f, 2, 1.3f, H("#5E9A3E")); c.BlobC(9.5f, 12.3f, 1.4f, 1, H("#7AB04A"));
                        c.Outline(0.5f);
                        break;
                    }
                case "stone":
                    c.Blob(8, 5.5f, 5.6f, 4.3f, RStone);
                    c.Blob(5.5f, 6.5f, 2, 1.3f, RMossStone, 0.2f);
                    c.Outline(0.55f);
                    break;
                case "stump":
                    {
                        for (int y = 2 * c.s; y < 8 * c.s; y++)
                            for (int x = 3 * c.s; x < 13 * c.s; x++)
                            {
                                float u = (x - 3f * c.s) / (10f * c.s);
                                c.SetP(x, y, At(RBark, 0.85f - u * 0.7f + (c.Hash(x / 2, y) - 0.5f) * 0.25f));
                            }
                        c.Blob(3.5f, 2.2f, 2, 1.4f, RBark, -0.1f); c.Blob(12.5f, 2.2f, 2, 1.4f, RBark, -0.2f);
                        c.Ellipse(8, 8.3f, 5.1f, 2.5f, H("#C9A27A"));
                        c.Ellipse(8, 8.3f, 3.6f, 1.7f, H("#B58C64"));
                        c.Ellipse(8, 8.3f, 2.2f, 1.0f, H("#D6B48A"));
                        c.Ellipse(8, 8.3f, 0.8f, 0.4f, H("#9C7650"));
                        c.BlobC(4.5f, 9, 1.4f, 0.9f, H("#5E8A44"));
                        c.Outline(0.5f);
                        break;
                    }
                case "boulder":
                    c.Blob(8, 7, 7.3f, 6.2f, RStone, -0.05f);
                    c.Blob(5, 10, 3.5f, 2.2f, RMossStone, 0.15f);
                    c.Blob(11, 9.5f, 2.2f, 1.4f, RMossStone, 0.1f);
                    c.Line(9, 3, 11, 7, RStone[1]);
                    c.Outline(0.5f);
                    break;
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Zaun (rustikaler Holzzaun)

        static Sprite Fence(string kind)
        {
            var c = new Px(16, 20, 820);
            void Post(float x)
            {
                for (int y = 0; y < 16 * c.s; y++)
                    for (int xx = 0; xx < 3 * c.s; xx++)
                        c.SetP(Mathf.RoundToInt(x * c.s) + xx, y, At(RWood, 0.8f - xx / (3f * c.s) * 0.55f + (c.Hash(xx, y / 3) - 0.5f) * 0.2f));
                c.Blob(x + 1.5f, 15.8f, 1.6f, 0.8f, RWood, 0.15f);
            }
            void Rail(float y)
            {
                for (int x = 0; x < c.pw; x++)
                    for (int yy = 0; yy < 2 * c.s; yy++)
                        c.SetP(x, Mathf.RoundToInt(y * c.s) + yy, At(RWood, 0.35f + yy / (2f * c.s) * 0.45f + (c.Hash(x / 4, yy) - 0.5f) * 0.15f));
            }
            if (kind == "h") { Rail(4); Rail(10); Post(0); }
            else if (kind == "v") { for (int y = 0; y < c.ph; y++) for (int x = 6 * c.s; x < 8 * c.s; x++) c.SetP(x, y, At(RWood, 0.35f + (x - 6 * c.s) / (2f * c.s) * 0.3f)); Post(6.5f); }
            else Post(6.5f);
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ Baeume (Laubkronen aus Blattballen)

        static void Trunk(Px c, float cx, float bottom, float top, float width)
        {
            for (int y = Mathf.FloorToInt(bottom * c.s); y < top * c.s; y++)
            {
                float ly = y / (float)c.s;
                float wv = width * (1 + Mathf.Max(0, 3 - (ly - bottom)) * 0.18f);
                for (int x = Mathf.FloorToInt((cx - wv / 2) * c.s); x < (cx + wv / 2) * c.s; x++)
                {
                    float u = ((x + 0.5f) / c.s - (cx - wv / 2)) / wv;
                    float t = 0.85f - u * 0.75f + (c.Hash(x / 2, y / 4) - 0.5f) * 0.3f;
                    c.SetP(x, y, At(RBark, t));
                }
            }
        }

        static void Canopy(Px c, float cx, float cy, float rx, float ry, Color[] ramp, int clumps, System.Random rnd)
        {
            // hintere, dunkle Ballen
            for (int i = 0; i < clumps; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = (float)rnd.NextDouble();
                float bx = cx + Mathf.Cos(a) * rx * 0.7f * d, by = cy + Mathf.Sin(a) * ry * 0.65f * d;
                float r = (rx + ry) * 0.25f * (0.7f + (float)rnd.NextDouble() * 0.45f);
                c.Blob(bx, by, r, r * 0.88f, ramp, -0.22f, 0.14f);
            }
            // vordere Ballen, oben links staerker beleuchtet
            for (int i = 0; i < clumps; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = (float)rnd.NextDouble();
                float bx = cx + Mathf.Cos(a) * rx * 0.55f * d, by = cy + Mathf.Sin(a) * ry * 0.5f * d;
                float r = (rx + ry) * 0.2f * (0.6f + (float)rnd.NextDouble() * 0.45f);
                float lit = ((by - cy) / ry - (bx - cx) / rx) * 0.12f;
                c.Blob(bx, by, r, r * 0.85f, ramp, lit, 0.16f);
            }
            // Sonnenflecken / einzelne Blaetter am Rand
            for (int i = 0; i < clumps * 6; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float bx = cx + Mathf.Cos(a) * rx * (0.85f + (float)rnd.NextDouble() * 0.2f), by = cy + Mathf.Sin(a) * ry * (0.85f + (float)rnd.NextDouble() * 0.2f);
                if (!c.SolidP(Mathf.FloorToInt(bx * c.s), Mathf.FloorToInt(by * c.s))) continue;
                c.Dot(bx, by + 0.5f, Mathf.Sin(a) > 0 ? ramp[ramp.Length - 2] : ramp[1]);
            }
        }

        internal static Sprite Oak(int v)
        {
            var c = new Px(40, 56, 1000 + v);
            var rnd = new System.Random(v * 17 + 3);
            var ramp = v % 3 == 2 ? RLeafWarm : RLeaf;
            Trunk(c, 20, 0.5f, 24, 6);
            // Aeste
            c.Line(20, 18, 13, 26, RBark[2], 2); c.Line(20, 20, 27, 27, RBark[1], 2);
            // Wurzeln
            c.Blob(15, 1.2f, 3, 1.2f, RBark, -0.1f); c.Blob(25, 1.2f, 3, 1.2f, RBark, -0.25f);
            Canopy(c, 20, 36, 17, 16, ramp, 14, rnd);
            if (v % 4 == 1) foreach (var q in new[] { new Vector2(12, 33), new Vector2(26, 39), new Vector2(18, 30), new Vector2(28, 30) }) c.BlobC(q.x, q.y, 1.3f, 1.3f, H("#D8573E"));
            c.Outline(0.55f, 0.85f);
            return c.ToSprite(new Vector2(0.5f, 0.02f));
        }

        internal static Sprite Pine(int v)
        {
            var c = new Px(30, 54, 1100 + v);
            Trunk(c, 15, 0, 10, 3.5f);
            for (int i = 0; i < 6; i++)
            {
                float yb = 7 + i * 7.2f, half = 14 - i * 2.1f, top = yb + 11;
                for (int y = Mathf.FloorToInt(yb * c.s); y < top * c.s; y++)
                {
                    float ly = y / (float)c.s;
                    float k = (ly - yb) / (top - yb);
                    float hw = half * (1 - k);
                    for (int x = Mathf.FloorToInt((15 - hw) * c.s); x < (15 + hw) * c.s; x++)
                    {
                        float u = ((x + 0.5f) / c.s - (15 - hw)) / Mathf.Max(0.1f, 2 * hw);
                        // links hell, rechts dunkel, unten am Saum dunkler; zackiger Rand
                        if ((k < 0.12f) && c.Hash(x, 7) < 0.35f) continue;
                        float t = 0.75f - u * 0.6f + k * 0.15f - (k < 0.15f ? 0.2f : 0) + (c.Hash(x, y) - 0.5f) * 0.2f;
                        c.SetP(x, y, At(RPine, t));
                    }
                }
            }
            c.Outline(0.55f, 0.85f);
            return c.ToSprite(new Vector2(0.5f, 0.02f));
        }

        internal static Sprite Bush(int v)
        {
            var c = new Px(22, 18, 1200 + v);
            var rnd = new System.Random(v * 5 + 9);
            Canopy(c, 11, 7.5f, 9, 6.5f, RLeaf, 7, rnd);
            if (v % 2 == 0) foreach (var q in new[] { new Vector2(6, 9), new Vector2(13, 12), new Vector2(16, 7), new Vector2(9, 5) }) { c.Dot(q.x, q.y, H("#F4EEE0")); c.Dot(q.x + 0.5f, q.y, H("#F4EEE0")); c.Dot(q.x, q.y + 0.5f, H("#F3D36B")); }
            c.Outline(0.55f, 0.85f);
            return c.ToSprite(new Vector2(0.5f, 0.05f));
        }

        static Sprite Stump2() => ObstacleG("stump");

        // ------------------------------------------------------------------ Gebaeude

        /// <summary>Flaeche mit Verlauf aus einer Farbrampe fuellen (links/oben heller).</summary>
        static void ShadedRect(Px c, float x, float y, float w, float h, Color[] ramp, float baseT, float grad = 0.25f, float noise = 0.12f)
        {
            for (int py = Mathf.FloorToInt(y * c.s); py < (y + h) * c.s; py++)
                for (int px = Mathf.FloorToInt(x * c.s); px < (x + w) * c.s; px++)
                {
                    float u = ((px + 0.5f) / c.s - x) / w, vv = ((py + 0.5f) / c.s - y) / h;
                    c.SetP(px, py, At(ramp, baseT + (0.5f - u) * grad * 0.6f + (vv - 0.5f) * grad + (c.Hash(px, py) - 0.5f) * noise));
                }
        }

        static void BrickWall(Px c, float x, float y, float w, float h)
        {
            for (int py = Mathf.FloorToInt(y * c.s); py < (y + h) * c.s; py++)
                for (int px = Mathf.FloorToInt(x * c.s); px < (x + w) * c.s; px++)
                {
                    int row = py / 3, bx = (px + (row % 2) * 4) % 8;
                    bool mortar = py % 3 == 0 || bx == 0;
                    float u = ((px + 0.5f) / c.s - x) / w;
                    float t = mortar ? 0.08f : 0.5f + (c.Hash(px / 8 + row * 13, row) - 0.5f) * 0.35f + (0.5f - u) * 0.15f;
                    c.SetP(px, py, mortar ? RPlaster[1] : At(RBrick, t));
                }
        }

        static void PlankWall(Px c, float x, float y, float w, float h)
        {
            for (int py = Mathf.FloorToInt(y * c.s); py < (y + h) * c.s; py++)
                for (int px = Mathf.FloorToInt(x * c.s); px < (x + w) * c.s; px++)
                {
                    int row = py / 5;
                    bool seam = py % 5 == 0;
                    float u = ((px + 0.5f) / c.s - x) / w;
                    float t = seam ? 0.1f : 0.55f + (c.Hash(row, px / 12) - 0.5f) * 0.25f + (0.5f - u) * 0.2f + (py % 5) * 0.03f;
                    c.SetP(px, py, At(RWood, t));
                }
        }

        /// <summary>Ziegeldach in Frontansicht (Trapez), Ziegelreihen mit Licht oben links.</summary>
        static void TileRoof(Px c, float x0, float x1, float yBase, float yTop, float overhang, Color[] ramp, bool straw = false)
        {
            float h = yTop - yBase;
            for (int py = Mathf.FloorToInt(yBase * c.s); py < yTop * c.s; py++)
            {
                float ly = (py + 0.5f) / c.s, k = (ly - yBase) / h;
                float inset = k * ((x1 - x0) / 2f - 2);
                float l = x0 - overhang + inset, r = x1 + overhang - inset;
                for (int px = Mathf.FloorToInt(l * c.s); px < r * c.s; px++)
                {
                    float u = ((px + 0.5f) / c.s - l) / (r - l);
                    float t;
                    if (straw)
                        t = 0.5f + (c.Hash(px, py / 6) - 0.5f) * 0.5f + (0.5f - u) * 0.3f + k * 0.15f;
                    else
                    {
                        int row = py / 5, col = (px + (row % 2) * 3) / 6;
                        float inRow = (py % 5) / 5f;
                        bool seam = (px + (row % 2) * 3) % 6 == 0;
                        t = 0.25f + inRow * 0.5f + (0.5f - u) * 0.25f + (c.Hash(col, row) - 0.5f) * 0.12f - (seam ? 0.25f : 0);
                    }
                    c.SetP(px, py, At(ramp, t));
                }
            }
            // Traufe mit Schatten
            for (int px = Mathf.FloorToInt((x0 - overhang) * c.s); px < (x1 + overhang) * c.s; px++)
            {
                c.SetP(px, Mathf.FloorToInt(yBase * c.s), ramp[0]);
                c.SetP(px, Mathf.FloorToInt(yBase * c.s) - 1, new Color(0, 0, 0, 0.35f));
            }
        }

        static void Window(Px c, float x, float y, float w, float h, bool lit = true)
        {
            ShadedRect(c, x - 1, y - 1, w + 2, h + 2, RWood, 0.35f);
            var glass = lit ? R("#9C7A3A", "#C9A24E", "#E8C86A", "#F7E2A0", "#FFF2CC") : R("#2C4A5E", "#3E6680", "#5A86A0", "#86AEC2", "#B8D4E0");
            ShadedRect(c, x, y, w, h, glass, 0.55f, 0.6f);
            c.Rect(Mathf.RoundToInt(x + w / 2 - 0.5f), Mathf.RoundToInt(y), 1, Mathf.RoundToInt(h), RWood[1]);
            c.Rect(Mathf.RoundToInt(x), Mathf.RoundToInt(y + h / 2), Mathf.RoundToInt(w), 1, RWood[1]);
            ShadedRect(c, x - 1.5f, y - 2, w + 3, 1, RPlaster, 0.7f, 0.1f);
        }

        static void Door(Px c, float x, float y, float w, float h)
        {
            ShadedRect(c, x - 1, y, w + 2, h + 1, RWood, 0.2f);
            ShadedRect(c, x, y, w, h, RWood, 0.5f, 0.3f);
            for (float xx = x + 1.5f; xx < x + w - 0.5f; xx += 2) c.Line(xx, y, xx, y + h - 1, RWood[2]);
            c.Dot(x + w - 1.5f, y + h * 0.45f, H("#E8C86A"));
        }

        static Sprite HouseV2(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    {
                        c = new Px(46, 34, 11);
                        // Leinenzelt
                        for (int py = 0; py < 30 * c.s; py++)
                        {
                            float ly = (py + 0.5f) / c.s, k = ly / 30f, half = 21 * (1 - k);
                            for (int px = Mathf.FloorToInt((23 - half) * c.s); px < (23 + half) * c.s; px++)
                            {
                                float u = ((px + 0.5f) / c.s - (23 - half)) / Mathf.Max(0.1f, 2 * half);
                                float t = 0.75f - u * 0.6f + k * 0.15f + (Mathf.Sin(u * 40) > 0.8f ? -0.12f : 0);
                                c.SetP(px, py, At(RCloth, t));
                            }
                        }
                        c.Tri(new Vector2(17, 0), new Vector2(29, 0), new Vector2(23, 14), H("#2A1E18"));
                        c.Line(17, 0, 23, 14, RCloth[4]); c.Line(29, 0, 23, 14, RCloth[1]);
                        c.Rect(22, 29, 2, 5, RWood[2]);
                        c.Line(2, 1, 0, 0, RWood[2]); c.Line(44, 1, 46, 0, RWood[2]);
                        break;
                    }
                case 1:
                    {
                        c = new Px(54, 48, 12);
                        PlankWall(c, 6, 2, 42, 22);
                        ShadedRect(c, 5, 2, 3, 22, RWood, 0.3f); ShadedRect(c, 46, 2, 3, 22, RWood, 0.2f);
                        TileRoof(c, 6, 48, 23, 45, 4, RStraw, true);
                        Door(c, 23, 2, 8, 14);
                        Window(c, 10, 10, 7, 6); Window(c, 37, 10, 7, 6);
                        ShadedRect(c, 20, 0, 14, 2, RStone, 0.6f);
                        break;
                    }
                case 2:
                    {
                        c = new Px(66, 60, 13);
                        BrickWall(c, 6, 3, 54, 28);
                        // Fachwerk-Balken
                        foreach (var bx in new[] { 6f, 25f, 41f, 58f }) ShadedRect(c, bx, 3, 2, 28, RWood, 0.35f);
                        ShadedRect(c, 6, 30, 54, 2, RWood, 0.3f);
                        TileRoof(c, 6, 60, 31, 56, 4, RRoof);
                        ShadedRect(c, 46, 42, 6, 14, RBrick, 0.4f); ShadedRect(c, 45, 55, 8, 2, RStone, 0.5f);
                        Door(c, 29, 3, 9, 16);
                        Window(c, 11, 13, 9, 9); Window(c, 46, 13, 9, 9);
                        // Blumenkaesten
                        foreach (var bx in new[] { 11f, 46f })
                        {
                            ShadedRect(c, bx - 1, 11, 11, 2, RWood, 0.45f);
                            for (int i = 0; i < 5; i++) c.BlobC(bx + i * 2.2f, 13.6f, 1.1f, 1.1f, i % 2 == 0 ? H("#E86A6A") : H("#F3D36B"));
                        }
                        ShadedRect(c, 20, 0, 26, 3, RStone, 0.55f);
                        break;
                    }
                default:
                    {
                        c = new Px(80, 72, 14);
                        BrickWall(c, 4, 3, 72, 40);
                        ShadedRect(c, 4, 21, 72, 2, RPlaster, 0.6f);
                        foreach (var bx in new[] { 4f, 38f, 74f }) ShadedRect(c, bx, 3, 2, 40, RPlaster, 0.55f);
                        TileRoof(c, 4, 76, 42, 68, 4, RRoof);
                        ShadedRect(c, 58, 54, 7, 14, RBrick, 0.4f); ShadedRect(c, 57, 67, 9, 2, RStone, 0.5f);
                        ShadedRect(c, 14, 52, 6, 12, RBrick, 0.35f); ShadedRect(c, 13, 63, 8, 2, RStone, 0.5f);
                        Door(c, 35, 3, 10, 17);
                        Window(c, 10, 8, 9, 9); Window(c, 60, 8, 9, 9);
                        Window(c, 10, 27, 9, 9); Window(c, 24, 27, 9, 9); Window(c, 46, 27, 9, 9); Window(c, 60, 27, 9, 9);
                        ShadedRect(c, 28, 0, 24, 3, RStone, 0.55f);
                        for (int i = 0; i < 12; i++) { float bx = 6 + i * 6 + (i >= 5 ? 14 : 0); if (bx > 74) break; c.BlobC(bx, 3.5f, 2, 1.6f, H("#4E8A44")); c.BlobC(bx + 0.5f, 4.5f, 0.8f, 0.8f, i % 2 == 0 ? H("#E86A6A") : H("#F4EEE0")); }
                        break;
                    }
            }
            c.Outline(0.5f, 0.9f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite BinV2(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    c = new Px(24, 20, 21);
                    PlankWall(c, 2, 1, 20, 11);
                    ShadedRect(c, 1, 12, 22, 5, RWood, 0.35f, 0.4f);
                    ShadedRect(c, 1, 16, 22, 1, RWood, 0.75f, 0.1f);
                    ShadedRect(c, 4, 1, 2, 16, RStone, 0.35f); ShadedRect(c, 18, 1, 2, 16, RStone, 0.3f);
                    ShadedRect(c, 10, 11, 4, 3, R("#7A5A1A", "#A8822E", "#D3AE4A", "#EBCF78"), 0.6f);
                    break;
                case 1:
                    c = new Px(30, 26, 22);
                    PlankWall(c, 2, 6, 26, 10);
                    c.Blob(8, 4, 3.6f, 3.6f, RWood, -0.1f); c.Blob(8, 4, 1.4f, 1.4f, RStone, 0.2f);
                    c.Blob(22, 4, 3.6f, 3.6f, RWood, -0.2f); c.Blob(22, 4, 1.4f, 1.4f, RStone, 0.1f);
                    c.BlobC(9, 17.5f, 3, 2, H("#E07A2E")); c.BlobC(15, 18.5f, 3, 2.2f, H("#6EAE44")); c.BlobC(21, 17.5f, 2.6f, 2, H("#E8C04A"));
                    c.Line(27, 13, 30, 16, RWood[2]);
                    break;
                default:
                    c = new Px(36, 36, 23);
                    PlankWall(c, 3, 1, 30, 12);
                    ShadedRect(c, 4, 2, 3, 26, RWood, 0.4f); ShadedRect(c, 29, 2, 3, 26, RWood, 0.3f);
                    TileRoof(c, 2, 34, 24, 34, 1, RRoof);
                    c.BlobC(10, 15, 3, 2, H("#D8573E")); c.BlobC(18, 15, 3, 2, H("#E8C04A")); c.BlobC(26, 15, 3, 2, H("#6EAE44"));
                    break;
            }
            c.Outline(0.5f, 0.9f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }
    }
}
