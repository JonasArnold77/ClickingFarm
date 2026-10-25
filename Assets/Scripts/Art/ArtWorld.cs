using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Welt-Grafik im gemuetlichen 16-px-Farm-Stil: erdige, warme Palette, dunkelbraune Konturen,
    /// verbundene Beete (Autotiling), Holzzaeune, buschige Baeume, Holzhaus mit Schindeldach.
    /// Eigene Platzhalter-Grafik, nichts kopiert.
    /// </summary>
    public static partial class Art
    {
        // Warme, erdige Palette
        public static readonly Color G0 = H("#3E7A2C"), G1 = H("#4F9233"), G2 = H("#62A83C"), G3 = H("#7EC24A"), G4 = H("#A2D85E");
        public static readonly Color S0 = H("#4A2A16"), S1 = H("#6B3E22"), S2 = H("#87522D"), S3 = H("#A06A3C"), S4 = H("#BD8752");
        public static readonly Color W0 = H("#331B0E"), W1 = H("#4E2D17"), W2 = H("#6A3F21");
        public static readonly Color Plank0 = H("#5C3216"), Plank1 = H("#8A5328"), Plank2 = H("#B07138"), Plank3 = H("#CF9452");
        public static readonly Color Roof0 = H("#5E1E14"), Roof1 = H("#8C2E1E"), Roof2 = H("#B5452A"), Roof3 = H("#D9673C");
        public static readonly Color Outline = H("#2B1608");

        static Color Pick(Px c, Color a, Color b, float p) => c.Rand() < p ? a : b;

        // ------------------------------------------------------------------ Boden

        static Sprite GrassTile(int v)
        {
            var c = new Px(16, 16, 900 + v * 37);
            c.Fill(G2);
            // weiche Flecken
            for (int i = 0; i < 6; i++) c.Ellipse(c.RandI(0, 16), c.RandI(0, 16), 2 + c.Rand() * 2, 1 + c.Rand() * 1.5f, c.Rand() < 0.5f ? G1 : Light(G2, 0.06f));
            // kleine Halme: helle Spitze, dunkler Fuss
            for (int i = 0; i < 14; i++)
            {
                int x = c.RandI(0, 16), y = c.RandI(0, 15);
                c.Set(x, y, G1); c.Set(x, y + 1, G3);
                if (c.Rand() < 0.4f) { c.Set(x + 1, y, G1); c.Set(x + 1, y + 1, G4); }
            }
            for (int i = 0; i < 8; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), G0);
            if (v == 3) { c.Set(4, 5, H("#FFF3A0")); c.Set(3, 5, H("#FFFFFF")); c.Set(11, 11, H("#F7A8D8")); c.Set(12, 11, H("#F7A8D8")); }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>Beet-Kachel, die sich mit Nachbarbeeten verbindet. mask: 1=N, 2=O, 4=S, 8=W.</summary>
        static Sprite SoilTile(int mask, bool wet)
        {
            var c = new Px(16, 16, 300 + mask);
            Color b0 = wet ? H("#3A2112") : S1, b1 = wet ? H("#4A2B17") : S2, b2 = wet ? H("#5A361D") : S3, hi = wet ? H("#6B4428") : S4;
            bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
            int x0 = w ? 0 : 1, x1 = e ? 15 : 14, y0 = s ? 0 : 1, y1 = n ? 15 : 14;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    // runde Ecken, wo zwei Seiten offen sind
                    bool corner = (!w && !s && x == x0 && y == y0) || (!e && !s && x == x1 && y == y0) || (!w && !n && x == x0 && y == y1) || (!e && !n && x == x1 && y == y1);
                    if (corner) continue;
                    c.Set(x, y, b1);
                }
            // Kruemel-Textur
            for (int i = 0; i < 22; i++) { int x = c.RandI(x0, x1 + 1), y = c.RandI(y0, y1 + 1); if (c.Solid(x, y)) c.Set(x, y, c.Rand() < 0.55f ? b0 : b2); }
            for (int i = 0; i < 6; i++)
            {
                int x = c.RandI(x0 + 1, x1), y = c.RandI(y0 + 1, y1);
                if (c.Solid(x, y) && c.Solid(x, y - 1)) { c.Set(x, y, hi); c.Set(x, y - 1, b0); }
            }
            // Raender: oben Schatten (Grasueberhang), unten heller Erdwall
            if (!n) for (int x = x0; x <= x1; x++) { if (c.Solid(x, y1)) c.Set(x, y1, b0); if (c.Solid(x, y1 - 1)) c.Set(x, y1 - 1, Dark(b1, 0.85f)); }
            if (!s) for (int x = x0; x <= x1; x++) { if (c.Solid(x, y0)) c.Set(x, y0, hi); }
            if (!w) for (int y = y0; y <= y1; y++) if (c.Solid(x0, y)) c.Set(x0, y, Dark(b1, 0.8f));
            if (!e) for (int y = y0; y <= y1; y++) if (c.Solid(x1, y)) c.Set(x1, y, Dark(b1, 0.9f));
            if (wet) for (int i = 0; i < 4; i++) { int x = c.RandI(x0 + 1, x1), y = c.RandI(y0 + 1, y1); if (c.Solid(x, y)) c.Set(x, y, H("#7A6A80")); }
            // dunkle Kontur nach aussen
            c.Outline(0.55f, 0.7f);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite PathTile(int v)
        {
            var c = new Px(16, 16, 700 + v);
            c.Fill(H("#C9A370"));
            for (int i = 0; i < 5; i++)
            {
                float x = c.RandI(1, 15), y = c.RandI(1, 15);
                c.Ellipse(x, y, 2 + c.Rand() * 1.5f, 1.4f + c.Rand(), H("#B08A5A"));
                c.Ellipse(x - 0.5f, y + 0.5f, 1.4f, 0.8f, H("#DDBC88"));
            }
            for (int i = 0; i < 12; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), Pick(c, H("#9E7A4D"), H("#E8CFA0"), 0.5f));
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite WaterTile(int v)
        {
            var c = new Px(16, 16, 800);
            c.Fill(H("#3C8FD0"));
            for (int i = 0; i < 18; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), H("#347FBE"));
            for (int y = 2; y < 16; y += 5)
                for (int x = 0; x < 16; x++)
                {
                    int xx = (x + v * 3 + y) % 16;
                    if (xx < 3) c.Set(x, y, H("#8ED2F5"));
                    else if (xx == 3) c.Set(x, y, H("#5FB0E6"));
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite FloorTile()
        {
            var c = new Px(16, 16, 810);
            c.Fill(H("#B79B78"));
            for (int y = 0; y < 16; y += 4) for (int x = 0; x < 16; x++) c.Set(x, y, H("#8E7558"));
            for (int y = 0; y < 16; y++) for (int x = (y / 4 % 2) * 4; x < 16; x += 8) c.Set(x, y, H("#8E7558"));
            for (int i = 0; i < 10; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), H("#CDB394"));
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Zaun

        /// <summary>Holzzaun. h = waagrecht (Pfosten links + Latten), v = senkrecht (Pfosten + Seitenlatte), p = einzelner Pfosten.</summary>
        static Sprite Fence(string kind)
        {
            var c = new Px(16, 20, 820);
            void Post(int x)
            {
                c.Rect(x, 0, 4, 15, Plank2); c.Rect(x + 3, 0, 1, 15, Plank1); c.Rect(x, 14, 4, 2, Plank3);
                c.Set(x + 1, 15, Plank3); c.Set(x + 2, 16, Plank3); c.Set(x + 1, 16, Plank2);
                c.Set(x + 1, 6, Plank1); c.Set(x + 2, 10, Plank1);
            }
            if (kind == "h")
            {
                c.Rect(0, 9, 16, 3, Plank2); c.Rect(0, 11, 16, 1, Plank3); c.Rect(0, 9, 16, 1, Plank1);
                c.Rect(0, 3, 16, 3, Plank2); c.Rect(0, 5, 16, 1, Plank3); c.Rect(0, 3, 16, 1, Plank1);
                Post(0);
            }
            else if (kind == "v")
            {
                c.Rect(6, 0, 4, 20, Plank1); c.Rect(6, 0, 1, 20, Plank2);
                Post(6);
            }
            else Post(6);
            c.Outline(0.4f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ Baeume und Deko

        static Sprite Oak(int v)
        {
            var c = new Px(36, 52, 1000 + v);
            Color l0 = v % 3 == 2 ? H("#2F6B2C") : H("#2C6A30"), l1 = v % 3 == 2 ? H("#3F8A34") : H("#3B8636"), l2 = v % 3 == 2 ? H("#58A63E") : H("#52A443"), l3 = v % 3 == 2 ? H("#7EC24A") : H("#78C25A");
            // Stamm mit Wurzeln
            c.Rect(14, 2, 8, 20, Plank1);
            c.Rect(14, 2, 2, 20, Plank0); c.Rect(19, 2, 2, 20, Plank2);
            c.Tri(new Vector2(9, 1), new Vector2(15, 1), new Vector2(15, 7), Plank1);
            c.Tri(new Vector2(21, 1), new Vector2(27, 1), new Vector2(21, 7), Plank1);
            for (int y = 4; y < 20; y += 4) c.Set(17, y, Plank0);
            // Krone aus vielen Kugeln
            Vector3[] blobs = { new Vector3(18, 34, 13), new Vector3(9, 30, 8), new Vector3(27, 30, 8), new Vector3(12, 41, 8), new Vector3(24, 41, 8), new Vector3(18, 45, 7), new Vector3(18, 26, 9) };
            foreach (var b in blobs) c.Ellipse(b.x, b.y, b.z, b.z * 0.9f, l0);
            foreach (var b in blobs) c.Ellipse(b.x - 1, b.y + 1, b.z - 1.5f, b.z * 0.9f - 1.5f, l1);
            foreach (var b in blobs) c.Ellipse(b.x - 2, b.y + 2.5f, b.z * 0.55f, b.z * 0.45f, l2);
            for (int i = 0; i < 14; i++) { int x = c.RandI(6, 30), y = c.RandI(26, 50); if (c.Solid(x, y) && c.Get(x, y) == l2) { c.Set(x, y, l3); c.Set(x + 1, y, l3); } }
            for (int i = 0; i < 18; i++) { int x = c.RandI(4, 32), y = c.RandI(20, 48); if (c.Solid(x, y) && c.Get(x, y) == l1) c.Set(x, y, l0); }
            if (v % 4 == 1) foreach (var q in new[] { new Vector2(10, 32), new Vector2(24, 38), new Vector2(17, 29), new Vector2(26, 28) }) { c.Circle(q.x, q.y, 1.5f, H("#E8463A")); c.Set((int)q.x, (int)q.y + 1, H("#FFB0A0")); }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0.02f));
        }

        static Sprite Pine(int v)
        {
            var c = new Px(28, 48, 1100 + v);
            Color l0 = H("#1E4F33"), l1 = H("#2A6B40"), l2 = H("#3B8A4C"), l3 = H("#5BAA5E");
            c.Rect(12, 0, 4, 9, Plank1); c.Rect(12, 0, 1, 9, Plank0);
            for (int i = 0; i < 5; i++)
            {
                float y = 6 + i * 7.5f, half = 13 - i * 2.2f;
                c.Tri(new Vector2(14 - half, y), new Vector2(14 + half, y), new Vector2(14, y + 12), l0);
                c.Tri(new Vector2(14 - half + 1.5f, y + 1), new Vector2(14 + half - 2.5f, y + 1), new Vector2(13.5f, y + 11), l1);
                c.Tri(new Vector2(14 - half + 3, y + 2), new Vector2(13, y + 2), new Vector2(13, y + 9), l2);
                c.Set((int)(14 - half + 4), (int)y + 3, l3);
            }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0.02f));
        }

        static Sprite Bush(int v)
        {
            var c = new Px(20, 16, 1200 + v);
            c.Ellipse(10, 6, 9, 6, H("#2C6A30")); c.Ellipse(6, 8, 5, 4.5f, H("#3B8636")); c.Ellipse(14, 8, 5, 4.5f, H("#3B8636")); c.Ellipse(10, 10, 5, 4, H("#52A443"));
            c.Ellipse(8, 11, 2.5f, 1.8f, H("#78C25A"));
            if (v % 2 == 0) foreach (var q in new[] { new Vector2(5, 8), new Vector2(12, 11), new Vector2(15, 6), new Vector2(8, 5) }) { c.Set((int)q.x, (int)q.y, H("#FF8FC8")); c.Set((int)q.x, (int)q.y + 1, H("#FFD2EC")); }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0.05f));
        }

        static Sprite Stump2()
        {
            var c = new Px(16, 16, 1300);
            c.Rect(3, 2, 10, 6, Plank1); c.Rect(3, 2, 2, 6, Plank0); c.Rect(11, 2, 2, 6, Plank2);
            c.Ellipse(8, 8.5f, 5.2f, 2.6f, Plank3); c.Ellipse(8, 8.5f, 3.4f, 1.6f, H("#E2B17A")); c.Ellipse(8, 8.5f, 1.4f, 0.7f, Plank2);
            c.Rect(1, 1, 3, 2, Plank1); c.Rect(12, 1, 3, 2, Plank1);
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Gebaeude

        static void Planks(Px c, int x, int y, int w, int h, Color b, Color dark, Color light)
        {
            c.Rect(x, y, w, h, b);
            for (int yy = y; yy < y + h; yy += 3)
            {
                for (int xx = x; xx < x + w; xx++) c.Set(xx, yy, dark);
                int seam = x + ((yy * 7) % Mathf.Max(1, w - 4)) + 2;
                c.Set(seam, yy + 1, dark); c.Set(seam, yy + 2, dark);
                for (int xx = x; xx < x + w; xx++) if (c.Rand() < 0.08f) c.Set(xx, yy + 2, light);
            }
        }

        static void ShingleRoof(Px c, int x0, int x1, int yBase, int yTop, int overhang, Color b0, Color b1, Color b2, Color b3)
        {
            int h = yTop - yBase;
            for (int y = yBase; y <= yTop; y++)
            {
                float t = (y - yBase) / (float)Mathf.Max(1, h);
                int inset = Mathf.RoundToInt(t * ((x1 - x0) / 2f - 3));
                int left = x0 - overhang + inset, right = x1 + overhang - inset;
                for (int x = left; x <= right; x++)
                {
                    int row = (y - yBase) / 3;
                    bool edge = (y - yBase) % 3 == 0;
                    bool seam = ((x + row * 2) % 5) == 0;
                    c.Set(x, y, edge ? b0 : seam ? b1 : (y - yBase) % 3 == 2 ? b3 : b2);
                }
            }
            // Dachkante
            for (int x = x0 - overhang; x <= x1 + overhang; x++) { c.Set(x, yBase, b0); c.Set(x, yBase - 1, Dark(b0, 0.7f)); }
        }

        static void Window(Px c, int x, int y, int w, int h, bool lit = true)
        {
            c.Rect(x - 1, y - 1, w + 2, h + 2, Plank0);
            c.Rect(x, y, w, h, lit ? H("#FFE7A0") : H("#7FB6D8"));
            c.Rect(x, y + h - 2, w, 2, lit ? H("#FFF6D0") : H("#BFE4F5"));
            c.Rect(x + w / 2, y, 1, h, Plank1); c.Rect(x, y + h / 2, w, 1, Plank1);
            c.Rect(x - 2, y - 2, w + 4, 1, Plank3);
        }

        static void Door(Px c, int x, int y, int w, int h)
        {
            c.Rect(x - 1, y, w + 2, h + 1, Plank0);
            c.Rect(x, y, w, h, Plank1);
            for (int xx = x + 1; xx < x + w; xx += 2) c.Rect(xx, y, 1, h, Plank0);
            c.Set(x + w - 2, y + h / 2, H("#FFD23F"));
        }

        static Sprite HouseV2(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    {
                        c = new Px(44, 32, 11);
                        // Zelt aus Stoffbahnen
                        c.Tri(new Vector2(2, 2), new Vector2(42, 2), new Vector2(22, 28), H("#C9783A"));
                        c.Tri(new Vector2(5, 3), new Vector2(39, 3), new Vector2(22, 26), H("#E39A4E"));
                        for (int i = 0; i < 5; i++) c.Line(22, 26, 8 + i * 7, 3, H("#C9783A"));
                        c.Tri(new Vector2(16, 2), new Vector2(28, 2), new Vector2(22, 15), H("#3B2010"));
                        c.Line(16, 2, 22, 15, H("#F2C27A")); c.Line(28, 2, 22, 15, H("#F2C27A"));
                        c.Rect(21, 26, 2, 5, Plank1);
                        c.Line(2, 2, 0, 0, Plank1); c.Line(42, 2, 44, 0, Plank1);
                        break;
                    }
                case 1:
                    {
                        c = new Px(52, 46, 12);
                        Planks(c, 6, 2, 40, 22, Plank2, Plank0, Plank3);
                        c.Rect(6, 2, 3, 22, Plank1); c.Rect(43, 2, 3, 22, Plank1);
                        ShingleRoof(c, 6, 45, 23, 42, 4, H("#6B4020"), H("#8A5A2A"), H("#B58340"), H("#D4A55C"));
                        Door(c, 22, 2, 8, 13);
                        Window(c, 10, 9, 7, 6); Window(c, 35, 9, 7, 6);
                        c.Rect(20, 0, 12, 2, H("#9A8A78"));
                        break;
                    }
                case 2:
                    {
                        c = new Px(64, 56, 13);
                        Planks(c, 6, 3, 52, 28, H("#E6D2AE"), H("#B39C78"), H("#F6E8CC"));
                        c.Rect(6, 3, 52, 3, H("#9A8266"));
                        ShingleRoof(c, 6, 57, 30, 52, 4, Roof0, Roof1, Roof2, Roof3);
                        c.Rect(44, 40, 6, 14, H("#9A5A44")); c.Rect(43, 52, 8, 2, H("#7A4434"));
                        Door(c, 28, 3, 9, 15);
                        Window(c, 11, 12, 9, 8); Window(c, 45, 12, 9, 8);
                        // Veranda
                        c.Rect(18, 0, 28, 3, Plank2); c.Rect(18, 0, 28, 1, Plank1);
                        c.Rect(19, 3, 2, 14, Plank2); c.Rect(43, 3, 2, 14, Plank2);
                        c.Rect(17, 17, 30, 2, Plank1);
                        foreach (var x in new[] { 10, 13, 16, 46, 49, 52 }) { c.Set(x, 11, H("#FF7AB8")); c.Set(x + 1, 11, H("#5FCB4B")); }
                        break;
                    }
                default:
                    {
                        c = new Px(76, 66, 14);
                        Planks(c, 4, 3, 68, 38, H("#F2E4C8"), H("#BFA888"), H("#FFF6E4"));
                        c.Rect(4, 21, 68, 2, H("#9A8266"));
                        ShingleRoof(c, 4, 71, 40, 62, 4, H("#1E3A6E"), H("#2D5596"), H("#3F6FBF"), H("#5D8FDB"));
                        c.Rect(56, 50, 7, 14, H("#9A5A44"));
                        Door(c, 33, 3, 10, 16);
                        Window(c, 9, 8, 9, 9); Window(c, 58, 8, 9, 9);
                        Window(c, 9, 27, 9, 9); Window(c, 23, 27, 9, 9); Window(c, 44, 27, 9, 9); Window(c, 58, 27, 9, 9);
                        c.Rect(28, 0, 20, 3, H("#C9C3B8")); c.Rect(28, 0, 20, 1, H("#9A948A"));
                        for (int x = 6; x < 70; x += 4) if (x < 30 || x > 46) { c.Set(x, 3, H("#5FCB4B")); c.Set(x, 4, x % 8 == 0 ? H("#FF7AB8") : H("#FFE45C")); }
                        break;
                    }
            }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite BinV2(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    c = new Px(24, 20, 21);
                    c.Rect(2, 1, 20, 11, Plank2);
                    for (int y = 1; y < 12; y += 4) c.Rect(2, y, 20, 1, Plank0);
                    c.Rect(2, 1, 2, 11, Plank1); c.Rect(20, 1, 2, 11, Plank1);
                    c.Rect(1, 12, 22, 5, Plank1); c.Rect(1, 16, 22, 1, Plank3); c.Rect(1, 12, 22, 1, Plank0);
                    c.Rect(10, 11, 4, 3, H("#E0B24A")); c.Set(11, 12, H("#8A6A1A"));
                    c.Rect(4, 12, 2, 5, H("#9A8A78")); c.Rect(18, 12, 2, 5, H("#9A8A78"));
                    break;
                case 1:
                    c = new Px(30, 24, 22);
                    c.Rect(2, 6, 26, 10, H("#B5452A")); c.Rect(2, 6, 26, 2, Roof0); c.Rect(2, 14, 26, 2, Roof3);
                    for (int x = 4; x < 28; x += 5) c.Rect(x, 8, 1, 6, Roof1);
                    c.Circle(8, 4, 3.6f, Plank0); c.Circle(8, 4, 2.2f, Plank2); c.Set(8, 4, Plank0);
                    c.Circle(22, 4, 3.6f, Plank0); c.Circle(22, 4, 2.2f, Plank2); c.Set(22, 4, Plank0);
                    c.Ellipse(9, 17, 3, 2, H("#FF8A1F")); c.Ellipse(15, 18, 3, 2.2f, H("#7EE04A")); c.Ellipse(21, 17, 2.6f, 2, H("#FFD93B"));
                    c.Line(27, 13, 30, 16, Plank1);
                    break;
                default:
                    c = new Px(36, 34, 23);
                    c.Rect(3, 1, 30, 12, Plank2); for (int y = 1; y < 13; y += 3) c.Rect(3, y, 30, 1, Plank0);
                    c.Rect(3, 12, 30, 2, Plank0);
                    c.Rect(4, 2, 3, 24, Plank1); c.Rect(29, 2, 3, 24, Plank1);
                    c.Rect(1, 23, 34, 8, H("#E05A6A"));
                    for (int x = 1; x < 35; x += 8) c.Rect(x, 23, 4, 8, H("#FFF0E0"));
                    for (int x = 1; x < 35; x += 4) c.Set(x + 1, 22, x % 8 < 4 ? H("#FFF0E0") : H("#E05A6A"));
                    c.Ellipse(10, 15.5f, 3, 2, H("#FF4122")); c.Ellipse(18, 15.5f, 3, 2, H("#FFC21A")); c.Ellipse(26, 15.5f, 3, 2, H("#5FD35A"));
                    break;
            }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ Monster

        static void Eyes(Px c, int x, int y, int gap = 3, bool angry = false)
        {
            foreach (var ex in new[] { x, x + gap })
            {
                c.Rect(ex, y, 2, 2, H("#1A0E08"));
                c.Set(ex, y + 1, Color.white);
                if (angry) c.Set(ex + (ex == x ? -1 : 2), y + 2, H("#1A0E08"));
            }
        }

        static Sprite MonsterSprite(string id, int frame)
        {
            var def = Balance.Monster(id);
            var col = def != null ? H(def.color) : Color.magenta;
            var c = new Px(16, 16, id.Length + frame);
            switch (id)
            {
                case "slime":
                    {
                        float w = frame == 0 ? 6.5f : 7.4f, h = frame == 0 ? 6.5f : 5.2f;
                        c.Ellipse(8, h, w, h, Dark(col, 0.75f));
                        c.Ellipse(8, h + 0.6f, w - 1, h - 1, col);
                        c.Ellipse(5.5f, h * 1.45f, 1.8f, 1.2f, Light(col, 0.6f));
                        c.Set(5, (int)(h * 1.5f), Color.white);
                        Eyes(c, 6, (int)(h * 0.8f), 4);
                        c.Rect(7, (int)(h * 0.5f), 3, 1, Dark(col, 0.5f));
                        break;
                    }
                case "ember_beetle":
                    {
                        int up = frame;
                        for (int i = 0; i < 3; i++) { c.Set(3, 2 + i * 2 + up, H("#2B1608")); c.Set(12, 2 + i * 2 + (1 - up), H("#2B1608")); }
                        c.Ellipse(8, 6, 4.5f, 4.8f, Dark(col, 0.8f));
                        c.Ellipse(8, 6.5f, 3.8f, 4.2f, col);
                        c.Line(8, 2, 8, 10, Dark(col, 0.6f));
                        c.Set(6, 7, H("#FFE45C")); c.Set(10, 5, H("#FFE45C")); c.Set(6, 4, H("#FFD23F"));
                        c.Ellipse(8, 11.5f, 3, 2.2f, H("#3A2416"));
                        Eyes(c, 6, 11, 3, true);
                        c.Set(6, 14, H("#2B1608")); c.Set(10, 14, H("#2B1608")); c.Set(5, 15, H("#FF7A2E")); c.Set(11, 15, H("#FF7A2E"));
                        break;
                    }
                case "weed_gnome":
                    {
                        c.Rect(5 + frame, 0, 2, 2, Plank1); c.Rect(9 - frame, 0, 2, 2, Plank1);
                        c.Ellipse(8, 6, 4.5f, 4.5f, H("#C99A6A"));
                        c.Ellipse(8, 6.5f, 3.6f, 3.8f, H("#E2B98A"));
                        Eyes(c, 6, 6, 3, true);
                        c.Rect(7, 4, 3, 1, H("#7A3A20"));
                        c.Line(8, 10, 5, 15 - frame, col); c.Line(8, 10, 11, 15, Dark(col, 0.85f)); c.Line(8, 10, 8, 15, Light(col, 0.15f));
                        c.Ellipse(5, 14 - frame, 2, 1.2f, col); c.Ellipse(11, 14, 2, 1.2f, Dark(col, 0.85f));
                        break;
                    }
                case "crow":
                    {
                        c.Ellipse(8, 7, 4.5f, 3.5f, col);
                        c.Ellipse(11, 9, 2.5f, 2.3f, col);
                        c.Tri(new Vector2(13, 9.5f), new Vector2(13, 8), new Vector2(16, 8.6f), H("#FFC21A"));
                        c.Set(11, 10, Color.white); c.Set(12, 10, H("#1A0E08"));
                        if (frame == 0) c.Tri(new Vector2(4, 8), new Vector2(10, 8), new Vector2(5, 15), Light(col, 0.15f));
                        else c.Tri(new Vector2(4, 7), new Vector2(10, 7), new Vector2(5, 1), Light(col, 0.15f));
                        c.Tri(new Vector2(4, 6), new Vector2(4, 8), new Vector2(0, 5), col);
                        c.Set(7, 3, H("#FFC21A")); c.Set(9, 3, H("#FFC21A"));
                        break;
                    }
            }
            c.Outline(0.45f);
            return c.ToSprite(new Vector2(0.5f, 0.05f));
        }

        static Sprite Projectile(string kind)
        {
            var c = new Px(8, 8, 1);
            switch (kind)
            {
                case "fire": c.Circle(4, 4, 3.5f, H("#FF5A1F")); c.Circle(4, 4.5f, 2.4f, H("#FFB22E")); c.Circle(3.6f, 5, 1.2f, H("#FFF2A0")); break;
                case "water": c.Circle(4, 4, 3.5f, H("#4FB4F0")); c.Circle(4, 4, 2.5f, H("#9AD8FF")); c.Set(2, 5, Color.white); c.Set(3, 6, Color.white); break;
                default: c.Ellipse(4, 4, 3.5f, 2.3f, H("#F2E2B0")); c.Ellipse(4, 4.6f, 2.2f, 1, H("#FFF8E0")); break;
            }
            c.Outline(0.5f);
            return c.ToSprite(new Vector2(0.5f, 0.5f), 12);
        }

        static Sprite RangeRing()
        {
            int n = 128; var c = new Px(n, n);
            float rad = 62f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    if (d > rad) continue;
                    float a = d > rad - 3 ? 0.95f : (d > rad - 10 ? 0.18f : 0.08f);
                    // gestrichelte Kante
                    if (d > rad - 3) { float ang = Mathf.Atan2(y - n / 2f, x - n / 2f); if (Mathf.Sin(ang * 24f) < -0.2f) a = 0.35f; }
                    c.c[y * n + x] = new Color(1, 1, 1, a);
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f), 64);
        }

        // ------------------------------------------------------------------ Beschuetzer-Pflanzen (mit Gesicht)

        static void DefenderShape(Px c, CropDef def, int stage, float cx)
        {
            Color L = H(def.leafColor), F = H(def.fruitColor);
            bool ripe = stage == 3;
            switch (def.shape)
            {
                case "pepper":
                    {
                        c.Ellipse(cx, 5, 6.5f, 4.5f, Dark(L, 0.75f));
                        c.Ellipse(cx - 3, 6, 3.5f, 3, L); c.Ellipse(cx + 3, 6.5f, 3.5f, 3, Light(L, 0.12f));
                        if (!ripe) { c.Ellipse(cx, 9, 2, 3, Dark(F, 0.7f)); break; }
                        // grosse Schote mit Gesicht + zwei kleine
                        c.Ellipse(cx - 5, 9, 1.6f, 3, Dark(F, 0.85f)); c.Ellipse(cx + 5.5f, 8.5f, 1.6f, 3, Dark(F, 0.85f));
                        c.Ellipse(cx, 12, 4.4f, 6.2f, F);
                        c.Ellipse(cx - 1.5f, 14, 1.4f, 3, Light(F, 0.45f));
                        c.Rect((int)cx - 1, 18, 2, 3, Dark(L, 0.9f)); c.Set((int)cx + 1, 20, L); c.Set((int)cx + 2, 21, L);
                        Eyes(c, (int)cx - 2, 12, 3);
                        c.Set((int)cx - 1, 9, Dark(F, 0.5f)); c.Set((int)cx, 9, Dark(F, 0.5f)); c.Set((int)cx + 1, 10, Dark(F, 0.5f));
                        c.Set((int)cx, 22, H("#FFE45C")); c.Set((int)cx - 1, 21, H("#FF9A2E"));
                        break;
                    }
                case "bell":
                    {
                        c.Line(cx, 1, cx, ripe ? 13 : 9, Dark(L, 0.8f));
                        c.Ellipse(cx - 3.5f, 4, 3.2f, 1.6f, L); c.Ellipse(cx + 3.5f, 6, 3.2f, 1.6f, Light(L, 0.12f));
                        if (!ripe) { c.Ellipse(cx, 11, 2.4f, 2.8f, Dark(F, 0.75f)); break; }
                        // Glocke
                        c.Ellipse(cx, 15, 6, 5.5f, Dark(F, 0.8f));
                        c.Ellipse(cx, 15.5f, 5.2f, 5, F);
                        c.Rect((int)cx - 6, 10, 13, 2, Dark(F, 0.8f));
                        c.Ellipse(cx - 2.5f, 18, 1.5f, 1.4f, Light(F, 0.6f));
                        c.Rect((int)cx - 1, 20, 2, 2, Dark(L, 0.8f));
                        Eyes(c, (int)cx - 2, 14, 3);
                        c.Set((int)cx, 12, Dark(F, 0.45f));
                        c.Circle(cx, 8.5f, 1.4f, H("#BFEAFF")); c.Set((int)cx, 9, Color.white);
                        break;
                    }
                case "vine":
                    {
                        float hgt = ripe ? 1f : 0.65f;
                        for (int i = 0; i < 3; i++)
                        {
                            float a0 = i * 2.1f;
                            for (float t = 0; t < 1; t += 0.04f)
                            {
                                float x = cx + Mathf.Sin(t * 9 + a0) * (5 - t * 2.5f), y = 1 + t * 16 * hgt;
                                c.Set(Mathf.RoundToInt(x), Mathf.RoundToInt(y), i == 1 ? Light(L, 0.15f) : L);
                                if (i == 0 && Mathf.RoundToInt(t * 25) % 5 == 0) c.Set(Mathf.RoundToInt(x) + 1, Mathf.RoundToInt(y), H("#F2E2B0"));
                            }
                        }
                        if (!ripe) break;
                        c.Circle(cx, 11, 4.2f, Dark(L, 0.75f));
                        c.Circle(cx, 11.5f, 3.4f, L);
                        Eyes(c, (int)cx - 2, 11, 3, true);
                        for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; c.Tri(new Vector2(cx + Mathf.Cos(a) * 3.8f, 11 + Mathf.Sin(a) * 3.8f), new Vector2(cx + Mathf.Cos(a + 0.4f) * 3.8f, 11 + Mathf.Sin(a + 0.4f) * 3.8f), new Vector2(cx + Mathf.Cos(a + 0.2f) * 6.5f, 11 + Mathf.Sin(a + 0.2f) * 6.5f), F); }
                        c.Set((int)cx - 4, 18, F); c.Set((int)cx + 4, 17, F); c.Set((int)cx + 5, 18, Light(F, 0.4f));
                        break;
                    }
                case "cannon":
                    {
                        c.Line(cx - 7, 3, cx + 7, 4, Dark(L, 0.8f));
                        c.Ellipse(cx - 5, 5, 2.6f, 2f, L); c.Ellipse(cx + 5, 6, 2.6f, 2f, Light(L, 0.1f));
                        if (!ripe) { c.Ellipse(cx, 5, 3, 2.5f, Dark(F, 0.75f)); break; }
                        c.Ellipse(cx, 8, 7, 6, F);
                        for (int i = -2; i <= 2; i++) c.Line(cx + i * 2.4f, 3, cx + i * 2.8f, 13, Dark(F, 0.8f));
                        // Rohr
                        c.Line(cx + 2, 13, cx + 6, 19, Dark(L, 0.7f), 3);
                        c.Ellipse(cx + 7, 19.5f, 2.2f, 1.6f, H("#2B1608"));
                        Eyes(c, (int)cx - 3, 8, 4, true);
                        c.Rect((int)cx - 2, 5, 5, 1, H("#5A2A0A"));
                        break;
                    }
                case "lance":
                    {
                        int hgt = ripe ? 15 : 10;
                        c.Line(cx, 1, cx, hgt, Dark(L, 0.85f), 2);
                        c.Ellipse(cx - 3, 6, 3, 1.5f, L); c.Ellipse(cx + 3.5f, 9, 3, 1.5f, Light(L, 0.1f));
                        if (!ripe) { c.Ellipse(cx, hgt + 1, 2, 2.4f, Dark(L, 0.9f)); break; }
                        for (int i = 0; i < 12; i++)
                        {
                            float a = i / 12f * Mathf.PI * 2;
                            c.Ellipse(cx + Mathf.Cos(a) * 4.6f, hgt + 4 + Mathf.Sin(a) * 4.6f, 1.9f, 1.9f, i % 2 == 0 ? F : Light(F, 0.3f));
                        }
                        c.Circle(cx, hgt + 4, 3.2f, H("#FFF7C8"));
                        Eyes(c, (int)cx - 2, hgt + 4, 3);
                        break;
                    }
            }
        }
    }
}
