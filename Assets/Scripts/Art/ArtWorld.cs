using UnityEngine;

namespace SeedClicker
{
    /// <summary>Monster, Geschosse, Reichweiten-Ring und Beschuetzer-Pflanzen (mit Gesichtern).</summary>
    public static partial class Art
    {
        static Color Pick(Px c, Color a, Color b, float p) => c.Rand() < p ? a : b;

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
                        c.BlobC(8, h, w, h, Dark(col, 0.75f));
                        c.BlobC(8, h + 0.6f, w - 1, h - 1, col);
                        c.BlobC(5.5f, h * 1.45f, 1.8f, 1.2f, Light(col, 0.6f));
                        c.Set(5, (int)(h * 1.5f), Color.white);
                        Eyes(c, 6, (int)(h * 0.8f), 4);
                        c.Rect(7, (int)(h * 0.5f), 3, 1, Dark(col, 0.5f));
                        break;
                    }
                case "ember_beetle":
                    {
                        int up = frame;
                        for (int i = 0; i < 3; i++) { c.Set(3, 2 + i * 2 + up, H("#2B1608")); c.Set(12, 2 + i * 2 + (1 - up), H("#2B1608")); }
                        c.BlobC(8, 6, 4.5f, 4.8f, Dark(col, 0.8f));
                        c.BlobC(8, 6.5f, 3.8f, 4.2f, col);
                        c.Line(8, 2, 8, 10, Dark(col, 0.6f));
                        c.Set(6, 7, H("#FFE45C")); c.Set(10, 5, H("#FFE45C")); c.Set(6, 4, H("#FFD23F"));
                        c.BlobC(8, 11.5f, 3, 2.2f, H("#3A2416"));
                        Eyes(c, 6, 11, 3, true);
                        c.Set(6, 14, H("#2B1608")); c.Set(10, 14, H("#2B1608")); c.Set(5, 15, H("#FF7A2E")); c.Set(11, 15, H("#FF7A2E"));
                        break;
                    }
                case "weed_gnome":
                    {
                        c.Rect(5 + frame, 0, 2, 2, Plank1); c.Rect(9 - frame, 0, 2, 2, Plank1);
                        c.BlobC(8, 6, 4.5f, 4.5f, H("#C99A6A"));
                        c.BlobC(8, 6.5f, 3.6f, 3.8f, H("#E2B98A"));
                        Eyes(c, 6, 6, 3, true);
                        c.Rect(7, 4, 3, 1, H("#7A3A20"));
                        c.Line(8, 10, 5, 15 - frame, col); c.Line(8, 10, 11, 15, Dark(col, 0.85f)); c.Line(8, 10, 8, 15, Light(col, 0.15f));
                        c.BlobC(5, 14 - frame, 2, 1.2f, col); c.BlobC(11, 14, 2, 1.2f, Dark(col, 0.85f));
                        break;
                    }
                case "crow":
                    {
                        c.BlobC(8, 7, 4.5f, 3.5f, col);
                        c.BlobC(11, 9, 2.5f, 2.3f, col);
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
                case "fire": c.BlobC(4, 4, 3.5f, 3.5f, H("#FF5A1F")); c.BlobC(4, 4.5f, 2.4f, 2.4f, H("#FFB22E")); c.BlobC(3.6f, 5, 1.2f, 1.2f, H("#FFF2A0")); break;
                case "water": c.BlobC(4, 4, 3.5f, 3.5f, H("#4FB4F0")); c.BlobC(4, 4, 2.5f, 2.5f, H("#9AD8FF")); c.Set(2, 5, Color.white); c.Set(3, 6, Color.white); break;
                default: c.BlobC(4, 4, 3.5f, 2.3f, H("#F2E2B0")); c.BlobC(4, 4.6f, 2.2f, 1, H("#FFF8E0")); break;
            }
            c.Outline(0.5f);
            return c.ToSprite(new Vector2(0.5f, 0.5f), 12);
        }

        static Sprite RangeRing()
        {
            int n = 128; var c = new Px(n, n, 1, 1);
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
                        c.BlobC(cx, 5, 6.5f, 4.5f, Dark(L, 0.75f));
                        c.BlobC(cx - 3, 6, 3.5f, 3, L); c.BlobC(cx + 3, 6.5f, 3.5f, 3, Light(L, 0.12f));
                        if (!ripe) { c.BlobC(cx, 9, 2, 3, Dark(F, 0.7f)); break; }
                        // grosse Schote mit Gesicht + zwei kleine
                        c.BlobC(cx - 5, 9, 1.6f, 3, Dark(F, 0.85f)); c.BlobC(cx + 5.5f, 8.5f, 1.6f, 3, Dark(F, 0.85f));
                        c.BlobC(cx, 12, 4.4f, 6.2f, F);
                        c.BlobC(cx - 1.5f, 14, 1.4f, 3, Light(F, 0.45f));
                        c.Rect((int)cx - 1, 18, 2, 3, Dark(L, 0.9f)); c.Set((int)cx + 1, 20, L); c.Set((int)cx + 2, 21, L);
                        Eyes(c, (int)cx - 2, 12, 3);
                        c.Set((int)cx - 1, 9, Dark(F, 0.5f)); c.Set((int)cx, 9, Dark(F, 0.5f)); c.Set((int)cx + 1, 10, Dark(F, 0.5f));
                        c.Set((int)cx, 22, H("#FFE45C")); c.Set((int)cx - 1, 21, H("#FF9A2E"));
                        break;
                    }
                case "bell":
                    {
                        c.Line(cx, 1, cx, ripe ? 13 : 9, Dark(L, 0.8f));
                        c.BlobC(cx - 3.5f, 4, 3.2f, 1.6f, L); c.BlobC(cx + 3.5f, 6, 3.2f, 1.6f, Light(L, 0.12f));
                        if (!ripe) { c.BlobC(cx, 11, 2.4f, 2.8f, Dark(F, 0.75f)); break; }
                        // Glocke
                        c.BlobC(cx, 15, 6, 5.5f, Dark(F, 0.8f));
                        c.BlobC(cx, 15.5f, 5.2f, 5, F);
                        c.Rect((int)cx - 6, 10, 13, 2, Dark(F, 0.8f));
                        c.BlobC(cx - 2.5f, 18, 1.5f, 1.4f, Light(F, 0.6f));
                        c.Rect((int)cx - 1, 20, 2, 2, Dark(L, 0.8f));
                        Eyes(c, (int)cx - 2, 14, 3);
                        c.Set((int)cx, 12, Dark(F, 0.45f));
                        c.BlobC(cx, 8.5f, 1.4f, 1.4f, H("#BFEAFF")); c.Set((int)cx, 9, Color.white);
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
                        c.BlobC(cx, 11, 4.2f, 4.2f, Dark(L, 0.75f));
                        c.BlobC(cx, 11.5f, 3.4f, 3.4f, L);
                        Eyes(c, (int)cx - 2, 11, 3, true);
                        for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; c.Tri(new Vector2(cx + Mathf.Cos(a) * 3.8f, 11 + Mathf.Sin(a) * 3.8f), new Vector2(cx + Mathf.Cos(a + 0.4f) * 3.8f, 11 + Mathf.Sin(a + 0.4f) * 3.8f), new Vector2(cx + Mathf.Cos(a + 0.2f) * 6.5f, 11 + Mathf.Sin(a + 0.2f) * 6.5f), F); }
                        c.Set((int)cx - 4, 18, F); c.Set((int)cx + 4, 17, F); c.Set((int)cx + 5, 18, Light(F, 0.4f));
                        break;
                    }
                case "cannon":
                    {
                        c.Line(cx - 7, 3, cx + 7, 4, Dark(L, 0.8f));
                        c.BlobC(cx - 5, 5, 2.6f, 2f, L); c.BlobC(cx + 5, 6, 2.6f, 2f, Light(L, 0.1f));
                        if (!ripe) { c.BlobC(cx, 5, 3, 2.5f, Dark(F, 0.75f)); break; }
                        c.BlobC(cx, 8, 7, 6, F);
                        for (int i = -2; i <= 2; i++) c.Line(cx + i * 2.4f, 3, cx + i * 2.8f, 13, Dark(F, 0.8f));
                        // Rohr
                        c.Line(cx + 2, 13, cx + 6, 19, Dark(L, 0.7f), 3);
                        c.BlobC(cx + 7, 19.5f, 2.2f, 1.6f, H("#2B1608"));
                        Eyes(c, (int)cx - 3, 8, 4, true);
                        c.Rect((int)cx - 2, 5, 5, 1, H("#5A2A0A"));
                        break;
                    }
                case "lance":
                    {
                        int hgt = ripe ? 15 : 10;
                        c.Line(cx, 1, cx, hgt, Dark(L, 0.85f), 2);
                        c.BlobC(cx - 3, 6, 3, 1.5f, L); c.BlobC(cx + 3.5f, 9, 3, 1.5f, Light(L, 0.1f));
                        if (!ripe) { c.BlobC(cx, hgt + 1, 2, 2.4f, Dark(L, 0.9f)); break; }
                        for (int i = 0; i < 12; i++)
                        {
                            float a = i / 12f * Mathf.PI * 2;
                            c.BlobC(cx + Mathf.Cos(a) * 4.6f, hgt + 4 + Mathf.Sin(a) * 4.6f, 1.9f, 1.9f, i % 2 == 0 ? F : Light(F, 0.3f));
                        }
                        c.BlobC(cx, hgt + 4, 3.2f, 3.2f, H("#FFF7C8"));
                        Eyes(c, (int)cx - 2, hgt + 4, 3);
                        break;
                    }
            }
        }
    }
}
