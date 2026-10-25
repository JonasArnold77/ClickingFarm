using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Erzeugt die gesamte Platzhalter-Grafik prozedural als Pixel-Art (16 px = 1 Feld).
    /// Stil: Stardew-artig, aber kraeftiger und bunter. Sprites werden per Schluessel gecacht.
    /// Spaeter koennen hier einfach echte Sprites aus Resources geladen werden (siehe Get).
    /// </summary>
    public static partial class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Color Dark(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
        public static Color Light(Color c, float f) => new Color(c.r + (1 - c.r) * f, c.g + (1 - c.g) * f, c.b + (1 - c.b) * f, c.a);
        public static Color H(string hex) => Balance.Hex(hex);
        static Color A(Color c, float a) { c.a = a; return c; }

        // Palette
        public static readonly Color Grass = H("#5FCB4B"), GrassDark = H("#3FA43A"), GrassLight = H("#9BEA5E");
        public static readonly Color Soil = H("#A8693F"), SoilDark = H("#7E4A2A"), SoilWet = H("#6A3E26"), SoilWetDark = H("#4E2C1A");
        public static readonly Color Wood = H("#B5793F"), WoodDark = H("#7A4A26"), WoodLight = H("#E0A866");
        public static readonly Color StoneC = H("#A3AEC0"), StoneDark = H("#6E7891"), StoneLight = H("#D7E0EE");
        public static readonly Color Water = H("#3FB4F0"), WaterLight = H("#A8E8FF"), WaterDark = H("#2479C9");
        public static readonly Color Gold = H("#FFD23F"), GoldDark = H("#D99A1E");
        public static readonly Color Cream = H("#FFF4D6"), Brown = H("#7A4A26");

        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            // Echte Grafik hat Vorrang: Resources/Sprites/<schluessel mit _ statt :>
            s = Resources.Load<Sprite>("Sprites/" + key.Replace(':', '_'));
            if (s == null) s = Make(key);
            cache[key] = s;
            return s;
        }

        static Sprite Make(string key)
        {
            var p = key.Split(':');
            switch (p[0])
            {
                case "white": { var c = new Px(4, 4, 1, 1); c.Fill(Color.white); return c.ToSprite(new Vector2(0.5f, 0.5f), 4); }
                case "dot": { var c = new Px(8, 8, 1, 1); c.Circle(4, 4, 4, Color.white); return c.ToSprite(new Vector2(0.5f, 0.5f), 8); }
                case "soft": return SoftCircle();
                case "ground": return Ground(p[1], p.Length > 2 ? int.Parse(p[2]) : 0);
                case "fert": return Fert(int.Parse(p[1]));
                case "wet": return WetOverlay();
                case "ob": return ObstacleG(p[1]);
                case "crop": return p.Length > 2 ? Crop(p[1], int.Parse(p[2])) : Crop(p[1], 3);
                case "item": return Item(p[1]);
                case "house": return HouseV2(p.Length > 1 ? int.Parse(p[1]) : (Game.I != null ? Game.I.S.houseLevel : 0));
                case "bin": return BinV2(p.Length > 1 ? int.Parse(p[1]) : (Game.I != null ? Game.I.S.binLevel : 0));
                case "soil": return SoilTile(int.Parse(p[1]), p.Length > 2 && p[2] == "1");
                case "fence": return Fence(p[1]);
                case "mon": return MonsterSprite(p[1], p.Length > 2 ? int.Parse(p[2]) : 0);
                case "proj": return Projectile(p[1]);
                case "rangering": return RangeRing();
                case "scaffold": return Scaffold();
                case "sign": return Sign();
                case "decor": return Decor(p[1], p.Length > 2 ? int.Parse(p[2]) : 0);
                case "tool": return ToolIcon(p[1], p.Length > 2 ? p[2] : "wood");
                case "upgrade": return UpgradeIcon(p[1]);
                case "panel": return Panel(p.Length > 1 ? p[1] : "cream");
                case "btn": return Button(H(p.Length > 1 ? "#" + p[1] : "#7BD148"));
                case "bar": return Bar();
                case "px": return Particle(p[1]);
                default: return Icon(p[0]);
            }
        }

        // ------------------------------------------------------------------ Boden

        static Sprite Ground(string kind, int v)
        {
            switch (kind)
            {
                case "grass": return GrassTile(v);
                case "path": return PathTile(v);
                case "water": return WaterTile(v);
                case "floor": return FloorTile();
            }
            var c = new Px(16, 16, 100 + v * 13 + kind.GetHashCode() % 1000);
            switch (kind)
            {
                case "grass":
                    c.Fill(Grass);
                    for (int i = 0; i < 26; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), c.Rand() < 0.5f ? GrassDark : Light(Grass, 0.12f));
                    for (int i = 0; i < 3 + v; i++)
                    {
                        int x = c.RandI(1, 15), y = c.RandI(1, 14);
                        c.Set(x, y, GrassLight); c.Set(x, y + 1, GrassLight); c.Set(x - 1, y, GrassDark); c.Set(x + 1, y, Light(Grass, 0.25f));
                    }
                    if (v == 3) { c.Set(4, 5, H("#FFE45C")); c.Set(11, 10, H("#FF7AC8")); c.Set(12, 10, H("#FF7AC8")); }
                    break;
                case "soil":
                case "soilwet":
                    {
                        bool wet = kind == "soilwet";
                        var b = wet ? SoilWet : Soil; var d = wet ? SoilWetDark : SoilDark;
                        c.Fill(b);
                        for (int y = 1; y < 16; y += 4) for (int x = 0; x < 16; x++) { c.Set(x, y, d); c.Set(x, y + 1, Light(b, 0.1f)); }
                        for (int i = 0; i < 18; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), c.Rand() < 0.5f ? d : Light(b, 0.18f));
                        for (int x = 0; x < 16; x++) { c.Set(x, 0, Dark(d, 0.85f)); c.Set(x, 15, Light(b, 0.2f)); }
                        for (int y = 0; y < 16; y++) { c.Set(0, y, Dark(d, 0.9f)); c.Set(15, y, Dark(d, 0.9f)); }
                        if (wet) for (int i = 0; i < 5; i++) c.Set(c.RandI(2, 14), c.RandI(2, 14), H("#8FA7C8"));
                        break;
                    }
                case "water":
                    c.Fill(Water);
                    for (int y = 0; y < 16; y += 4)
                        for (int x = 0; x < 16; x++)
                            if (Mathf.Sin((x + v * 4 + y * 2) * 0.8f) > 0.6f) c.Set(x, y + (v % 2), WaterLight);
                    for (int i = 0; i < 10; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), Dark(Water, 0.85f));
                    break;
                case "floor":
                    c.Fill(H("#C9B79C"));
                    for (int y = 0; y < 16; y += 8) for (int x = 0; x < 16; x++) c.Set(x, y, H("#9C8A70"));
                    for (int x = 0; x < 16; x += 8) for (int y = 0; y < 16; y++) c.Set(x + (y / 8 % 2) * 4, y, H("#9C8A70"));
                    break;
                case "path":
                    c.Fill(H("#E2C796"));
                    for (int i = 0; i < 20; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), c.Rand() < 0.5f ? H("#C7A874") : H("#F2DDB2"));
                    break;
                case "forest":
                    c.Fill(H("#2F8A3A"));
                    for (int i = 0; i < 30; i++) c.Set(c.RandI(0, 16), c.RandI(0, 16), c.Rand() < 0.5f ? H("#256F2F") : H("#3C9E45"));
                    break;
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite WetOverlay()
        {
            var c = new Px(16, 16, 5);
            for (int i = 0; i < 7; i++) c.Set(c.RandI(1, 15), c.RandI(1, 15), A(WaterLight, 0.8f));
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite Fert(int level)
        {
            var c = new Px(16, 16, 9 + level);
            var col = level >= 2 ? H("#C58BFF") : H("#3E2716");
            var col2 = level >= 2 ? H("#FFF27A") : H("#7DBF4A");
            for (int i = 0; i < 14; i++) c.Set(c.RandI(1, 15), c.RandI(1, 15), col);
            for (int i = 0; i < 4; i++) c.Set(c.RandI(1, 15), c.RandI(1, 15), col2);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Hindernisse

        static Sprite Obstacle(string id)
        {
            var c = new Px(16, 16, id.Length * 31);
            switch (id)
            {
                case "grass":
                    for (int i = 0; i < 9; i++)
                    {
                        float bx = 3 + i * 1.2f + c.Rand(), top = 7 + c.Rand() * 6;
                        var col = i % 3 == 0 ? H("#B7F25C") : i % 3 == 1 ? H("#6EDB4A") : H("#4CB83D");
                        c.Line(bx, 2, bx + (c.Rand() - 0.5f) * 3, top, col);
                    }
                    c.Set(6, 11, H("#FFF27A")); c.Set(11, 10, H("#FF9BD6"));
                    c.Outline(0.5f);
                    break;
                case "twig":
                    c.Line(3, 4, 13, 8, Wood, 2);
                    c.Line(8, 6, 10, 11, Wood, 1);
                    c.Ellipse(11, 12, 2, 1.3f, H("#7EE04A"));
                    c.Set(5, 5, WoodLight); c.Set(9, 7, WoodLight);
                    c.Shade(); c.Outline();
                    break;
                case "stone":
                    c.Ellipse(8, 6, 5.5f, 4.2f, StoneC);
                    c.Ellipse(6.5f, 7.5f, 2.5f, 1.6f, StoneLight);
                    c.Line(9, 4, 11, 7, StoneDark);
                    c.Shade(0.1f, 0.25f); c.Outline();
                    break;
                case "stump":
                    return Stump2();
                case "oldstump":
                    c.Rect(3, 2, 10, 7, Wood);
                    c.Ellipse(8, 9, 5, 2.6f, WoodLight);
                    c.Ellipse(8, 9, 3, 1.4f, H("#C98D52"));
                    c.Ellipse(8, 9, 1.2f, 0.6f, WoodLight);
                    for (int y = 2; y < 8; y += 2) c.Set(5, y, WoodDark);
                    c.Set(10, 4, WoodDark); c.Set(10, 5, WoodDark);
                    c.Rect(1, 2, 2, 2, Wood); c.Rect(13, 2, 2, 2, Wood);
                    c.Outline();
                    break;
                case "boulder":
                    c.Ellipse(8, 7, 7.4f, 6.2f, StoneDark);
                    c.Ellipse(7, 8, 6, 5, StoneC);
                    c.Ellipse(5, 10, 2.6f, 1.6f, StoneLight);
                    c.Line(9, 3, 12, 8, StoneDark); c.Line(10, 6, 13, 6, StoneDark);
                    c.Speckle(2, 9, 6, 4, H("#6EC24A"), 0.25f);
                    c.Outline();
                    break;
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ Pflanzen

        static Sprite Crop(string id, int stage)
        {
            var def = Balance.Crop(id);
            if (def == null) return Icon("unknown");
            bool tree = def.shape == "tree";
            int w = tree ? 24 : 16, h = tree ? 32 : def.IsDefender ? 28 : 24;
            var c = new Px(w, h, id.GetHashCode() + stage);
            Color L = H(def.leafColor), F = H(def.fruitColor);
            float cx = w / 2f;

            if (stage == 0)
            {
                c.BlobC(cx, 3, 4, 2, SoilDark);
                c.Set((int)cx - 2, 3, H("#F4D6A0")); c.Set((int)cx + 1, 4, H("#F4D6A0")); c.Set((int)cx, 2, H("#F4D6A0"));
                c.Outline(0.6f);
                return c.ToSprite(new Vector2(0.5f, 0f));
            }
            if (stage == 1)
            {
                c.Line(cx, 1, cx, 6, Dark(L, 0.8f));
                c.BlobC(cx - 2.2f, 6.5f, 2.4f, 1.4f, L);
                c.BlobC(cx + 2.2f, 7.2f, 2.4f, 1.4f, Light(L, 0.15f));
                c.Shade(); c.Outline();
                return c.ToSprite(new Vector2(0.5f, 0f));
            }

            bool ripe = stage == 3;
            if (def.IsDefender) { DefenderShape(c, def, stage, cx); c.Shade(); c.Outline(); return c.ToSprite(new Vector2(0.5f, 0f)); }
            switch (def.shape)
            {
                case "leafy":
                    {
                        float s = ripe ? 1f : 0.7f;
                        c.BlobC(cx, 5 * s + 1, 6.5f * s, 4.5f * s, Dark(L, 0.8f));
                        c.BlobC(cx - 2.5f * s, 6 * s + 1, 3.2f * s, 3.5f * s, L);
                        c.BlobC(cx + 2.5f * s, 6 * s + 1, 3.2f * s, 3.5f * s, L);
                        c.BlobC(cx, 8 * s + 1, 3 * s, 3.5f * s, Light(L, 0.15f));
                        if (ripe) { c.Line(cx, 3, cx, 10, F); c.Line(cx - 3, 5, cx - 1, 9, F); c.Line(cx + 3, 5, cx + 1, 9, F); }
                        break;
                    }
                case "root":
                    {
                        int top = ripe ? 13 : 10;
                        for (int i = -2; i <= 2; i++) c.Line(cx + i * 0.6f, 3, cx + i * 1.8f, top - Mathf.Abs(i), i % 2 == 0 ? L : Light(L, 0.2f));
                        if (ripe) { c.BlobC(cx, 2.5f, 3.6f, 2.6f, F); c.Set((int)cx - 1, 3, Light(F, 0.5f)); }
                        else c.BlobC(cx, 1.6f, 2f, 1.3f, Dark(F, 0.8f));
                        break;
                    }
                case "bush":
                    {
                        float r = ripe ? 6.2f : 4.6f;
                        c.BlobC(cx, r + 0.5f, r + 0.6f, r, Dark(L, 0.8f));
                        c.BlobC(cx - r * 0.35f, r + 1.5f, r * 0.6f, r * 0.6f, L);
                        c.BlobC(cx + r * 0.4f, r + 1.2f, r * 0.55f, r * 0.55f, Light(L, 0.12f));
                        if (ripe)
                        {
                            Vector2[] pos = { new Vector2(-3.5f, 4), new Vector2(2.5f, 3), new Vector2(0, 8), new Vector2(4, 8), new Vector2(-4, 9), new Vector2(-1, 4.5f) };
                            foreach (var q in pos) { c.BlobC(cx + q.x, q.y, 1.6f, 1.6f, F); c.Set((int)(cx + q.x) - 1, (int)q.y + 1, Light(F, 0.6f)); }
                        }
                        break;
                    }
                case "stalk":
                    {
                        int hgt = ripe ? 21 : 14;
                        c.Line(cx, 1, cx, hgt, Dark(L, 0.85f), 2);
                        for (int y = 4; y < hgt - 2; y += 4)
                        {
                            c.Line(cx, y, cx - 5, y + 3, L); c.Line(cx + 1, y + 2, cx + 6, y + 5, Light(L, 0.15f));
                        }
                        if (ripe)
                        {
                            c.BlobC(cx + 3.5f, hgt - 7, 2.2f, 4.2f, F);
                            for (int y = hgt - 10; y < hgt - 4; y += 2) c.Set((int)cx + 3, y, Dark(F, 0.8f));
                            c.Line(cx + 2, hgt - 11, cx + 5, hgt - 9, L);
                            c.Line(cx, hgt, cx - 2, hgt + 2, H("#F2D57A")); c.Line(cx + 1, hgt, cx + 3, hgt + 2, H("#F2D57A"));
                        }
                        break;
                    }
                case "flower":
                    {
                        int hgt = ripe ? 16 : 11;
                        c.Line(cx, 1, cx, hgt, Dark(L, 0.85f), 1);
                        c.BlobC(cx - 2.5f, 6, 2.6f, 1.4f, L); c.BlobC(cx + 2.8f, 9, 2.6f, 1.4f, Light(L, 0.1f));
                        if (ripe)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                float a = i / 10f * Mathf.PI * 2;
                                c.BlobC(cx + Mathf.Cos(a) * 4f, hgt + 2 + Mathf.Sin(a) * 4f, 2.1f, 2.1f, i % 2 == 0 ? F : Light(F, 0.2f));
                            }
                            c.BlobC(cx, hgt + 2, 2.8f, 2.8f, H("#7A4A1E"));
                            c.Set((int)cx - 1, hgt + 3, H("#A86A30"));
                        }
                        else c.BlobC(cx, hgt + 1, 2, 2.4f, Dark(L, 0.9f));
                        break;
                    }
                case "round":
                    {
                        c.Line(cx - 7, 3, cx + 7, 4, Dark(L, 0.8f));
                        c.BlobC(cx - 5, 5, 2.6f, 2f, L); c.BlobC(cx + 5, 6, 2.6f, 2f, Light(L, 0.1f));
                        if (ripe)
                        {
                            c.BlobC(cx, 6, 6.5f, 5.2f, F);
                            bool melon = id == "melon";
                            for (int i = -2; i <= 2; i++) c.Line(cx + i * 2.2f, 2, cx + i * 2.6f, 10, melon ? Dark(F, 0.6f) : Dark(F, 0.82f));
                            c.BlobC(cx - 2.5f, 8.5f, 2, 1.2f, Light(F, 0.4f));
                            c.Rect((int)cx - 1, 11, 2, 2, H("#5B8A2E"));
                        }
                        else c.BlobC(cx, 4, 3, 2.5f, Dark(F, 0.7f));
                        break;
                    }
                case "tree":
                    {
                        int trunkTop = ripe ? 14 : 10;
                        c.Rect((int)cx - 2, 1, 4, trunkTop, Wood);
                        c.Set((int)cx - 1, 5, WoodDark); c.Set((int)cx, 9, WoodDark);
                        float r = ripe ? 9.5f : 6.5f;
                        float cy = trunkTop + r * 0.7f;
                        c.BlobC(cx, cy, r, r * 0.85f, Dark(L, 0.78f));
                        c.BlobC(cx - r * 0.35f, cy + 1.5f, r * 0.6f, r * 0.55f, L);
                        c.BlobC(cx + r * 0.35f, cy + 0.5f, r * 0.55f, r * 0.5f, Light(L, 0.12f));
                        c.BlobC(cx, cy + r * 0.45f, r * 0.45f, r * 0.35f, Light(L, 0.25f));
                        if (ripe)
                        {
                            Vector2[] pos = { new Vector2(-5, -2), new Vector2(4, -3), new Vector2(0, 2), new Vector2(-3, 4), new Vector2(5, 3), new Vector2(1, -5) };
                            foreach (var q in pos) { c.BlobC(cx + q.x, cy + q.y, 1.7f, 1.7f, F); c.Set((int)(cx + q.x), (int)(cy + q.y) + 1, Light(F, 0.6f)); }
                        }
                        break;
                    }
                case "crystal":
                    {
                        int hgt = ripe ? 15 : 9;
                        c.Line(cx, 1, cx, hgt - 3, Dark(L, 0.8f));
                        float s = ripe ? 1f : 0.6f;
                        c.Tri(new Vector2(cx - 1, hgt - 4), new Vector2(cx + 1, hgt - 4), new Vector2(cx, hgt + 6 * s), F);
                        c.Tri(new Vector2(cx - 4 * s, hgt - 5), new Vector2(cx - 2, hgt - 4), new Vector2(cx - 5 * s, hgt + 3 * s), Light(F, 0.2f));
                        c.Tri(new Vector2(cx + 2, hgt - 4), new Vector2(cx + 4 * s, hgt - 5), new Vector2(cx + 5 * s, hgt + 3 * s), Dark(F, 0.85f));
                        c.BlobC(cx - 3, 4, 2.5f, 1.3f, L); c.BlobC(cx + 3, 5, 2.5f, 1.3f, Light(L, 0.2f));
                        if (ripe) { c.Set((int)cx, hgt + 2, Color.white); c.Set((int)cx - 4, hgt, Color.white); }
                        break;
                    }
            }
            c.Shade(); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ Gegenstaende

        static Sprite Item(string id)
        {
            var def = Balance.Item(id);
            if (def == null) return Icon(id);
            var col = H(def.color);
            Px c;
            switch (def.kind)
            {
                case "sprinkler":
                    c = new Px(16, 16, 3);
                    c.BlobC(8, 5, 5, 3, StoneDark);
                    c.BlobC(8, 6, 4, 2.4f, col);
                    c.Rect(7, 6, 2, 5, Dark(col, 0.8f));
                    c.BlobC(8, 11, 2.4f, 1.6f, col);
                    if (def.range >= 2) { c.Rect(3, 6, 2, 2, col); c.Rect(11, 6, 2, 2, col); }
                    if (def.range >= 3) { c.Set(8, 13, H("#FF5FA2")); c.Set(6, 12, H("#FFE45C")); c.Set(10, 12, H("#5FE3FF")); }
                    c.Set(3, 13, WaterLight); c.Set(13, 13, WaterLight); c.Set(2, 10, WaterLight); c.Set(14, 10, WaterLight);
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0.5f));
                case "fertilizer":
                    c = new Px(16, 16, 4);
                    c.Rect(4, 2, 8, 9, def.power >= 2 ? H("#7B4FC9") : H("#C9A26B"));
                    c.Rect(4, 10, 8, 2, Dark(col, 0.9f));
                    c.BlobC(8, 6, 2.5f, 2.5f, def.power >= 2 ? H("#FFE45C") : H("#6FCB4A"));
                    c.Set(5, 12, H("#7DBF4A")); c.Set(8, 13, H("#7DBF4A")); c.Set(10, 12, H("#3E2716"));
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0.5f));
                case "scarecrow":
                    c = new Px(16, 24, 5);
                    c.Rect(7, 1, 2, 18, Wood);
                    c.Rect(2, 11, 12, 2, Wood);
                    c.Rect(5, 7, 6, 7, H("#5B8FD9"));
                    c.Rect(5, 7, 6, 1, H("#3E6BB0"));
                    c.BlobC(8, 17, 3, 3, H("#F2D57A"));
                    c.Set(7, 17, Brown); c.Set(9, 17, Brown); c.Line(7, 15.5f, 9, 15.5f, H("#D9534F"));
                    c.Rect(4, 19, 8, 1, H("#C9532E")); c.Rect(6, 20, 4, 2, H("#E06A3C"));
                    c.Set(2, 12, H("#F2D57A")); c.Set(13, 12, H("#F2D57A")); c.Set(1, 11, H("#F2D57A")); c.Set(14, 11, H("#F2D57A"));
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0.06f));
                case "beehive":
                    c = new Px(16, 20, 6);
                    c.Rect(3, 1, 10, 2, WoodDark);
                    c.Rect(3, 3, 10, 4, col); c.Rect(3, 7, 10, 1, Cream);
                    c.Rect(3, 8, 10, 4, col); c.Rect(3, 12, 10, 1, Cream);
                    c.Rect(2, 13, 12, 3, H("#E0913A"));
                    c.Rect(7, 4, 2, 1, H("#3A2410"));
                    c.Set(1, 16, H("#2B2B2B")); c.Set(14, 14, H("#2B2B2B")); c.Set(13, 17, H("#FFE45C"));
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0.06f));
                case "harvester":
                    c = new Px(16, 20, 7);
                    c.Rect(3, 2, 10, 9, col);
                    c.Rect(4, 11, 8, 5, Light(col, 0.25f));
                    c.Rect(5, 13, 2, 2, Color.white); c.Rect(9, 13, 2, 2, Color.white);
                    c.Set(6, 13, H("#2B2B2B")); c.Set(10, 13, H("#2B2B2B"));
                    c.Rect(7, 16, 2, 2, StoneDark); c.BlobC(8, 18.5f, 1.2f, 1.2f, H("#FF5F5F"));
                    c.Rect(1, 5, 2, 4, StoneDark); c.Rect(13, 5, 2, 4, StoneDark);
                    c.Rect(4, 0, 3, 2, H("#3A3A3A")); c.Rect(9, 0, 3, 2, H("#3A3A3A"));
                    c.Rect(5, 4, 6, 4, H("#C99A5B")); c.Set(6, 7, H("#FF5050")); c.Set(9, 6, H("#FFD93B"));
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0.04f));
            }
            return Icon(id);
        }

        // ------------------------------------------------------------------ Gebaeude

        static Sprite House(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    c = new Px(40, 30, 11);
                    c.Tri(new Vector2(3, 2), new Vector2(37, 2), new Vector2(20, 26), H("#FF8A3D"));
                    for (int i = 0; i < 6; i++) c.Line(20, 26, 6 + i * 6, 2, H("#FFC15A"));
                    c.Tri(new Vector2(15, 2), new Vector2(25, 2), new Vector2(20, 14), H("#5A2E14"));
                    c.Rect(19, 25, 2, 4, WoodDark);
                    c.Set(21, 28, H("#FF5F8F")); c.Set(22, 28, H("#FF5F8F")); c.Set(22, 27, H("#FF5F8F"));
                    break;
                case 1:
                    c = new Px(48, 40, 12);
                    c.Rect(6, 2, 36, 20, Wood);
                    for (int y = 4; y < 22; y += 4) c.Line(6, y, 41, y, WoodDark);
                    c.Tri(new Vector2(1, 20), new Vector2(47, 20), new Vector2(24, 38), H("#F2C14E"));
                    for (int i = 0; i < 8; i++) c.Line(24, 38, 3 + i * 6, 20, H("#D9A23A"));
                    c.Rect(20, 2, 8, 12, WoodDark); c.Set(26, 8, Gold);
                    c.Rect(32, 9, 6, 6, H("#FFE9A0")); c.Rect(34, 9, 1, 6, WoodDark); c.Rect(32, 11, 6, 1, WoodDark);
                    c.Rect(9, 9, 6, 6, H("#FFE9A0")); c.Rect(11, 9, 1, 6, WoodDark);
                    break;
                case 2:
                    c = new Px(56, 48, 13);
                    c.Rect(6, 2, 44, 26, H("#FFF0D0"));
                    c.Rect(6, 2, 44, 3, H("#C9A26B"));
                    c.Tri(new Vector2(0, 26), new Vector2(56, 26), new Vector2(28, 46), H("#E8473C"));
                    for (int y = 28; y < 44; y += 3) c.Line(4, y, 52, y, H("#C2352C"));
                    c.Rect(40, 34, 5, 10, H("#B5503A"));
                    c.Rect(23, 2, 10, 15, H("#8A4E2A")); c.Set(31, 9, Gold);
                    c.Rect(10, 11, 8, 8, H("#8FE0FF")); c.Rect(13, 11, 2, 8, Cream); c.Rect(10, 14, 8, 2, Cream);
                    c.Rect(38, 11, 8, 8, H("#8FE0FF")); c.Rect(41, 11, 2, 8, Cream); c.Rect(38, 14, 8, 2, Cream);
                    c.Rect(9, 10, 10, 1, H("#5FCB4B")); c.Rect(37, 10, 10, 1, H("#5FCB4B"));
                    c.Set(11, 10, H("#FF5FA2")); c.Set(15, 10, H("#FFE45C")); c.Set(40, 10, H("#FF5FA2")); c.Set(44, 10, H("#FFE45C"));
                    break;
                default:
                    c = new Px(64, 56, 14);
                    c.Rect(4, 2, 56, 34, H("#FFF6E0"));
                    c.Rect(4, 18, 56, 2, H("#C9A26B"));
                    c.Tri(new Vector2(-2, 34), new Vector2(66, 34), new Vector2(32, 54), H("#4A7FE0"));
                    for (int y = 36; y < 52; y += 3) c.Line(2, y, 62, y, H("#3762B8"));
                    c.Rect(48, 42, 6, 12, H("#B5503A"));
                    c.Rect(27, 2, 10, 14, H("#8A4E2A")); c.Set(35, 8, Gold);
                    for (int i = 0; i < 4; i++)
                    {
                        int x = 8 + i * 13 + (i >= 2 ? 3 : 0);
                        c.Rect(x, 23, 7, 8, H("#8FE0FF")); c.Rect(x + 3, 23, 1, 8, Cream);
                        if (i != 1 && i != 2) { c.Rect(x, 7, 7, 7, H("#8FE0FF")); c.Rect(x + 3, 7, 1, 7, Cream); }
                    }
                    c.Rect(2, 0, 60, 2, H("#7BD148"));
                    for (int x = 4; x < 60; x += 5) c.Set(x, 2, x % 2 == 0 ? H("#FF5FA2") : H("#FFE45C"));
                    break;
            }
            c.Shade(0.12f, 0.15f); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite Bin(int level)
        {
            Px c;
            switch (level)
            {
                case 0:
                    c = new Px(20, 18, 21);
                    c.Rect(2, 1, 16, 11, Wood);
                    c.Rect(2, 4, 16, 1, WoodDark); c.Rect(2, 8, 16, 1, WoodDark);
                    c.Rect(1, 12, 18, 3, H("#C98D52"));
                    c.Rect(8, 12, 4, 2, Gold);
                    c.Line(3, 15, 6, 17, H("#FF5050")); c.Set(12, 15, H("#FFD93B")); c.Set(13, 16, H("#7EE04A"));
                    break;
                case 1:
                    c = new Px(28, 22, 22);
                    c.Rect(2, 5, 24, 10, H("#D9534F"));
                    c.Rect(2, 9, 24, 1, H("#A93A36"));
                    c.Circle(7, 4, 3.5f, WoodDark); c.Circle(7, 4, 1.5f, WoodLight);
                    c.Circle(21, 4, 3.5f, WoodDark); c.Circle(21, 4, 1.5f, WoodLight);
                    c.Ellipse(9, 16, 3, 2, H("#FF8A1F")); c.Ellipse(15, 17, 3, 2.2f, H("#7EE04A")); c.Ellipse(20, 16, 2.5f, 2, H("#FFD93B"));
                    c.Line(25, 12, 28, 14, WoodDark);
                    break;
                default:
                    c = new Px(34, 32, 23);
                    c.Rect(3, 1, 28, 12, Wood);
                    c.Rect(3, 12, 28, 2, WoodDark);
                    c.Rect(4, 2, 2, 24, WoodDark); c.Rect(28, 2, 2, 24, WoodDark);
                    for (int i = 0; i < 7; i++) c.Tri(new Vector2(1 + i * 4.6f, 22), new Vector2(5.6f + i * 4.6f, 22), new Vector2(3.3f + i * 4.6f, 18), i % 2 == 0 ? H("#FF5F8F") : Cream);
                    c.Rect(1, 22, 32, 8, H("#FF5F8F"));
                    for (int x = 1; x < 33; x += 8) c.Rect(x, 22, 4, 8, Cream);
                    c.Ellipse(9, 15, 3, 2, H("#FF4122")); c.Ellipse(17, 15, 3, 2, H("#FFC21A")); c.Ellipse(25, 15, 3, 2, H("#5FD35A"));
                    break;
            }
            c.Shade(0.15f, 0.18f); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite Scaffold()
        {
            var c = new Px(48, 40, 30);
            for (int x = 4; x < 48; x += 14) c.Rect(x, 0, 2, 38, WoodLight);
            for (int y = 8; y < 40; y += 12) c.Rect(2, y, 44, 2, WoodLight);
            c.Line(4, 2, 18, 18, Wood); c.Line(32, 2, 18, 18, Wood); c.Line(32, 22, 46, 36, Wood);
            c.Rect(18, 30, 10, 6, H("#FFD23F")); c.Rect(20, 32, 6, 2, H("#2B2B2B"));
            c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite Sign()
        {
            var c = new Px(16, 18, 31);
            c.Rect(7, 0, 2, 9, WoodDark);
            c.Rect(1, 7, 14, 9, Wood);
            c.Rect(1, 15, 14, 1, WoodLight);
            c.Rect(6, 9, 4, 3, Gold); c.Rect(6, 12, 1, 2, H("#C9C9C9")); c.Rect(9, 12, 1, 2, H("#C9C9C9")); c.Rect(7, 13, 2, 1, H("#C9C9C9"));
            c.Shade(); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0f));
        }

        static Sprite Decor(string kind, int v)
        {
            Px c;
            switch (kind)
            {
                case "tree": return Oak(v);
                case "pine": return Pine(v);
                case "bush": return Bush(v);
                case "oldtree":
                    {
                        c = new Px(24, 32, 40 + v);
                        var leaf = v % 3 == 0 ? H("#3FA43A") : v % 3 == 1 ? H("#2E9150") : H("#55B84A");
                        c.Rect(10, 0, 4, 10, Wood);
                        c.Ellipse(12, 16, 10, 9, Dark(leaf, 0.8f));
                        c.Ellipse(9, 19, 6, 6, leaf);
                        c.Ellipse(15, 17, 5.5f, 5.5f, Light(leaf, 0.1f));
                        c.Ellipse(12, 24, 5, 4, Light(leaf, 0.22f));
                        if (v % 4 == 1) { c.Circle(7, 15, 1.4f, H("#FF5F8F")); c.Circle(16, 20, 1.4f, H("#FF5F8F")); c.Circle(12, 13, 1.4f, H("#FF5F8F")); }
                        c.Shade(0.1f, 0.2f); c.Outline();
                        return c.ToSprite(new Vector2(0.5f, 0f));
                    }
                case "oldpine":
                    {
                        c = new Px(20, 32, 50 + v);
                        var leaf = H("#2E8C4E");
                        c.Rect(8, 0, 4, 6, WoodDark);
                        for (int i = 0; i < 4; i++) c.Tri(new Vector2(1 + i * 1.5f, 4 + i * 6), new Vector2(19 - i * 1.5f, 4 + i * 6), new Vector2(10, 14 + i * 6), i % 2 == 0 ? leaf : Light(leaf, 0.12f));
                        c.Shade(0.1f, 0.2f); c.Outline();
                        return c.ToSprite(new Vector2(0.5f, 0f));
                    }
                case "oldbush":
                    c = new Px(16, 12, 60 + v);
                    c.Ellipse(8, 5, 7, 5, H("#3FA43A")); c.Ellipse(5, 6, 3.5f, 3.5f, H("#5FCB4B")); c.Ellipse(11, 6, 3.5f, 3, H("#55B84A"));
                    if (v % 2 == 0) { c.Set(5, 8, H("#FF5F8F")); c.Set(10, 7, H("#FFE45C")); c.Set(8, 4, H("#FF5F8F")); }
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0f));
                case "flowers":
                    {
                        c = new Px(16, 16, 70 + v);
                        Color[] cols = { H("#FF5F8F"), H("#FFE45C"), H("#8FB8FF"), H("#FF8A3D"), Color.white, H("#C58BFF") };
                        for (int i = 0; i < 5; i++)
                        {
                            int x = c.RandI(2, 14), y = c.RandI(2, 12);
                            c.Line(x, y - 2, x, y, GrassDark);
                            var fc = cols[(i + v) % cols.Length];
                            c.Set(x, y + 1, fc); c.Set(x - 1, y, fc); c.Set(x + 1, y, fc); c.Set(x, y - 0, H("#FFF27A"));
                        }
                        return c.ToSprite(new Vector2(0.5f, 0.5f));
                    }
                case "fence":
                    c = new Px(16, 12, 80);
                    c.Rect(2, 0, 2, 11, WoodLight); c.Rect(12, 0, 2, 11, WoodLight);
                    c.Rect(0, 4, 16, 2, Wood); c.Rect(0, 8, 16, 2, Wood);
                    c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0f));
                case "well":
                    c = new Px(20, 26, 90);
                    c.Ellipse(10, 5, 8, 4, StoneC); c.Rect(2, 2, 16, 4, StoneC); c.Ellipse(10, 6, 6, 2.5f, WaterDark);
                    c.Rect(3, 6, 2, 12, Wood); c.Rect(15, 6, 2, 12, Wood);
                    c.Tri(new Vector2(0, 17), new Vector2(20, 17), new Vector2(10, 25), H("#E8473C"));
                    c.Shade(); c.Outline();
                    return c.ToSprite(new Vector2(0.5f, 0f));
            }
            return Icon(kind);
        }

        // ------------------------------------------------------------------ Partikel

        static Sprite Particle(string kind)
        {
            Px c;
            switch (kind)
            {
                case "leaf": c = new Px(5, 4, 1, 1); c.Ellipse(2.5f, 2, 2.5f, 1.6f, Color.white); c.Set(0, 0, Color.white); break;
                case "drop": c = new Px(4, 6, 1, 1); c.Circle(2, 2, 2, Color.white); c.Set(1, 4, Color.white); c.Set(2, 4, Color.white); c.Set(2, 5, Color.white); break;
                case "star":
                    c = new Px(7, 7, 1, 1);
                    c.Rect(3, 0, 1, 7, Color.white); c.Rect(0, 3, 7, 1, Color.white); c.Rect(2, 2, 3, 3, Color.white);
                    break;
                case "spark": c = new Px(3, 3, 1, 1); c.Set(1, 0, Color.white); c.Set(0, 1, Color.white); c.Set(1, 1, Color.white); c.Set(2, 1, Color.white); c.Set(1, 2, Color.white); break;
                case "ring":
                    c = new Px(16, 16, 1, 1);
                    for (int i = 0; i < 64; i++) { float a = i / 64f * Mathf.PI * 2; c.Set(8 + Mathf.RoundToInt(Mathf.Cos(a) * 7), 8 + Mathf.RoundToInt(Mathf.Sin(a) * 7), Color.white); }
                    break;
                default: c = new Px(2, 2, 1, 1); c.Fill(Color.white); break;
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f), 16);
        }

        static Sprite SoftCircle()
        {
            int n = 32; var c = new Px(n, n, 1, 1);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    c.c[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d));
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f), 32);
        }

        // ------------------------------------------------------------------ UI

        static Sprite Panel(string style)
        {
            Color fill = style == "dark" ? H("#4A2C17") : style == "paper" ? H("#FCE3B4") : H("#F6CB8C");
            Color border = style == "dark" ? H("#1F1008") : H("#5B2A0E");
            Color inner = style == "dark" ? H("#7A4A26") : H("#C8742E");
            var c = new Px(16, 16, 1, 1);
            c.Fill(fill);
            for (int i = 0; i < 16; i++) { c.Set(i, 0, border); c.Set(i, 15, border); c.Set(0, i, border); c.Set(15, i, border); }
            for (int i = 1; i < 15; i++) { c.Set(i, 1, inner); c.Set(1, i, inner); c.Set(14, i, inner); c.Set(i, 14, Light(fill, 0.5f)); }
            for (int i = 1; i < 15; i++) c.Set(i, 2, Light(inner, 0.4f));
            // abgerundete Ecken
            foreach (var p in new[] { new Vector2Int(0, 0), new Vector2Int(15, 0), new Vector2Int(0, 15), new Vector2Int(15, 15) }) c.Set(p.x, p.y, Color.clear);
            c.Set(1, 1, border); c.Set(14, 1, border); c.Set(1, 14, border); c.Set(14, 14, border);
            return c.ToSprite(new Vector2(0.5f, 0.5f), 16, new Vector4(4, 4, 4, 4));
        }

        static Sprite Button(Color col)
        {
            var c = new Px(16, 16, 1, 1);
            var dark = Dark(col, 0.62f);
            c.Fill(col);
            for (int x = 0; x < 16; x++) { c.Set(x, 0, Dark(dark, 0.8f)); c.Set(x, 1, dark); c.Set(x, 2, dark); c.Set(x, 15, Dark(dark, 0.8f)); c.Set(x, 14, Light(col, 0.35f)); }
            for (int y = 0; y < 16; y++) { c.Set(0, y, Dark(dark, 0.8f)); c.Set(15, y, Dark(dark, 0.8f)); }
            foreach (var p in new[] { new Vector2Int(0, 0), new Vector2Int(15, 0), new Vector2Int(0, 15), new Vector2Int(15, 15) }) c.Set(p.x, p.y, Color.clear);
            return c.ToSprite(new Vector2(0.5f, 0.5f), 16, new Vector4(4, 5, 4, 4));
        }

        static Sprite Bar()
        {
            var c = new Px(8, 8, 1, 1);
            c.Fill(Color.white);
            c.Set(0, 0, Color.clear); c.Set(7, 0, Color.clear); c.Set(0, 7, Color.clear); c.Set(7, 7, Color.clear);
            return c.ToSprite(new Vector2(0.5f, 0.5f), 8, new Vector4(2, 2, 2, 2));
        }

        static Sprite ToolIcon(string tool, string tierId)
        {
            Color head = Color.gray;
            foreach (var t in Balance.B.toolTiers) if (t.id == tierId) head = H(t.color);
            var c = new Px(16, 16, tool.Length);
            var hd = Dark(head, 0.7f);
            switch (tool)
            {
                case "hoe":
                    c.Line(3, 2, 11, 12, Wood, 2);
                    c.Rect(8, 11, 6, 3, head); c.Rect(12, 9, 2, 3, hd);
                    break;
                case "can":
                    c.Rect(3, 3, 8, 7, head); c.Rect(3, 3, 8, 2, hd);
                    c.Line(11, 7, 14, 11, head, 2); c.Rect(13, 11, 3, 2, hd);
                    c.Line(4, 10, 6, 13, hd); c.Line(6, 13, 9, 13, hd); c.Line(9, 13, 10, 10, hd);
                    break;
                case "axe":
                    c.Line(4, 2, 10, 13, Wood, 2);
                    c.BlobC(11, 12, 3.5f, 3, head); c.Rect(7, 11, 3, 2, hd);
                    break;
                case "pickaxe":
                    c.Line(4, 2, 10, 12, Wood, 2);
                    c.Line(4, 11, 9, 14, head, 2); c.Line(9, 14, 15, 10, head, 2);
                    break;
                case "scythe":
                    c.Line(4, 2, 7, 14, Wood, 2);
                    c.Line(7, 14, 13, 13, head, 2); c.Line(13, 13, 15, 10, head, 1);
                    break;
            }
            c.Shade(); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite UpgradeIcon(string id)
        {
            var c = new Px(16, 16, 3);
            switch (id)
            {
                case "green_thumb":
                    c.BlobC(8, 6, 4.5f, 5, H("#F2C08A")); c.Rect(6, 9, 4, 5, H("#F2C08A")); c.BlobC(8, 13, 2, 1.6f, H("#FFD9B0"));
                    c.BlobC(11, 4, 3.5f, 2, H("#5FCB4B")); c.Line(9, 3, 13, 5, H("#3FA43A"));
                    break;
                case "lucky_star":
                    StarShape(c, 8, 8, 7, 3, Gold); c.Set(6, 10, Color.white); c.Set(7, 10, Color.white);
                    break;
                default:
                    c.Tri(new Vector2(9, 15), new Vector2(3, 7), new Vector2(8, 7), H("#FF8A3D"));
                    c.Tri(new Vector2(8, 8), new Vector2(13, 8), new Vector2(6, 0), H("#FFD23F"));
                    break;
            }
            c.Shade(); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static void StarShape(Px c, float cx, float cy, float ro, float ri, Color col)
        {
            for (int i = 0; i < 5; i++)
            {
                float a0 = Mathf.PI / 2 + i * Mathf.PI * 2 / 5, a1 = a0 + Mathf.PI / 5, am = a0 - Mathf.PI / 5;
                var tip = new Vector2(cx + Mathf.Cos(a0) * ro, cy + Mathf.Sin(a0) * ro);
                c.Tri(new Vector2(cx, cy), tip, new Vector2(cx + Mathf.Cos(a1) * ri, cy + Mathf.Sin(a1) * ri), col);
                c.Tri(new Vector2(cx, cy), new Vector2(cx + Mathf.Cos(am) * ri, cy + Mathf.Sin(am) * ri), tip, col);
            }
        }

        static Sprite Icon(string id)
        {
            var c = new Px(16, 16, id.Length * 7);
            switch (id)
            {
                case "coin":
                    c.BlobC(8, 8, 6.5f, 6.5f, GoldDark); c.BlobC(8, 8.5f, 5.5f, 5.5f, Gold); c.Rect(7, 5, 2, 7, GoldDark); c.Set(6, 11, Color.white); c.Set(5, 10, Color.white);
                    break;
                case "wood":
                    c.Rect(2, 5, 12, 6, Wood); c.BlobC(13, 8, 2, 3, WoodLight); c.BlobC(13, 8, 0.9f, 1.3f, Wood); c.Line(3, 7, 10, 7, WoodDark);
                    break;
                case "stone":
                    c.BlobC(8, 7, 6, 4.5f, StoneC); c.BlobC(6, 9, 2.5f, 1.5f, StoneLight); c.Line(9, 4, 11, 7, StoneDark);
                    break;
                case "fiber":
                    for (int i = 0; i < 5; i++) c.Line(5 + i * 1.5f, 2, 3 + i * 2.5f, 14, i % 2 == 0 ? H("#7EE04A") : H("#5FB83A"));
                    c.Rect(4, 6, 8, 2, H("#C9A26B"));
                    break;
                case "energy":
                    c.Tri(new Vector2(10, 15), new Vector2(3, 7), new Vector2(8, 7), H("#FFE45C"));
                    c.Tri(new Vector2(7, 9), new Vector2(13, 9), new Vector2(6, 0), H("#FFC21A"));
                    break;
                case "star": StarShape(c, 8, 8, 7.5f, 3.2f, H("#9B6BFF")); c.Set(7, 10, Color.white); break;
                case "gift":
                    c.Rect(2, 2, 12, 8, H("#FF5F8F")); c.Rect(1, 9, 14, 3, H("#FF8FB0")); c.Rect(7, 2, 2, 10, Gold);
                    c.BlobC(5.5f, 13, 2.5f, 1.5f, Gold); c.BlobC(10.5f, 13, 2.5f, 1.5f, Gold);
                    break;
                case "map":
                    c.Rect(2, 3, 12, 10, H("#F2DDB2")); c.Line(5, 3, 5, 12, H("#C9A26B")); c.Line(10, 3, 10, 12, H("#C9A26B"));
                    c.BlobC(7.5f, 8, 2, 2, H("#5FCB4B")); c.Set(11, 6, H("#E8473C")); c.Set(12, 7, H("#E8473C")); c.Set(11, 8, H("#E8473C")); c.Set(12, 5, H("#E8473C"));
                    break;
                case "hammer":
                    c.Line(4, 2, 10, 10, Wood, 2); c.Rect(7, 9, 7, 4, StoneC); c.Rect(7, 9, 2, 4, StoneDark);
                    break;
                case "scroll":
                    c.Rect(3, 3, 10, 11, H("#FFF0C8")); c.Rect(2, 12, 12, 2, H("#D9A23A")); c.Rect(2, 2, 12, 2, H("#D9A23A"));
                    c.Line(5, 6, 11, 6, H("#B58A5A")); c.Line(5, 8, 11, 8, H("#B58A5A")); c.Line(5, 10, 9, 10, H("#B58A5A"));
                    break;
                case "book":
                    c.Rect(3, 2, 10, 12, H("#4A7FE0")); c.Rect(4, 3, 8, 10, H("#5B8FF0")); c.Rect(3, 2, 2, 12, H("#2E5BB0"));
                    c.Rect(7, 7, 4, 3, Gold);
                    break;
                case "bag":
                    c.BlobC(8, 6, 6, 5.5f, H("#E0913A")); c.Rect(5, 10, 6, 3, H("#C97A2A")); c.Rect(4, 12, 8, 2, H("#FF5F8F"));
                    c.BlobC(8, 6, 2, 2, Gold);
                    break;
                case "gear":
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Rect(8 + Mathf.RoundToInt(Mathf.Cos(a) * 5.5f) - 1, 8 + Mathf.RoundToInt(Mathf.Sin(a) * 5.5f) - 1, 3, 3, StoneC); }
                    c.BlobC(8, 8, 5, 5, StoneC); c.BlobC(8, 8, 2, 2, StoneDark);
                    break;
                case "lock":
                    c.Rect(3, 2, 10, 8, Gold); c.Rect(5, 10, 2, 4, StoneC); c.Rect(9, 10, 2, 4, StoneC); c.Rect(5, 13, 6, 2, StoneC);
                    c.Rect(7, 4, 2, 3, GoldDark);
                    break;
                case "video":
                    c.Rect(1, 3, 14, 10, H("#FF5F5F")); c.Tri(new Vector2(6, 5), new Vector2(6, 11), new Vector2(11, 8), Color.white);
                    break;
                case "bug":
                    c.BlobC(8, 7, 5, 5.5f, H("#FF3B3B")); c.Line(8, 2, 8, 12, H("#2B2B2B")); c.BlobC(8, 12.5f, 3, 2, H("#2B2B2B"));
                    c.Set(5, 8, H("#2B2B2B")); c.Set(11, 6, H("#2B2B2B")); c.Set(6, 4, H("#2B2B2B")); c.Set(10, 9, H("#2B2B2B"));
                    break;
                case "honey":
                    c.Rect(4, 2, 8, 9, H("#FFB627")); c.Rect(3, 11, 10, 2, WoodLight); c.Rect(5, 13, 6, 2, H("#C9532E")); c.Rect(5, 4, 2, 5, H("#FFE08A"));
                    break;
                case "house":
                    c.Rect(3, 2, 10, 7, H("#FFF0D0")); c.Tri(new Vector2(1, 8), new Vector2(15, 8), new Vector2(8, 15), H("#E8473C")); c.Rect(7, 2, 3, 4, H("#8A4E2A"));
                    break;
                case "bin":
                    c.Rect(2, 2, 12, 9, Wood); c.Rect(2, 5, 12, 1, WoodDark); c.Rect(1, 10, 14, 3, H("#C98D52")); c.Rect(7, 10, 2, 2, Gold);
                    break;
                case "moon":
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                        {
                            var q = new Vector2(x + 0.5f, y + 0.5f);
                            if (Vector2.Distance(q, new Vector2(8, 8)) <= 6 && Vector2.Distance(q, new Vector2(10.5f, 9.5f)) > 5) c.Set(x, y, H("#FFF2B0"));
                        }
                    break;
                case "sun":
                    c.BlobC(8, 8, 4.5f, 4.5f, H("#FFD23F"));
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Set(8 + Mathf.RoundToInt(Mathf.Cos(a) * 6.5f), 8 + Mathf.RoundToInt(Mathf.Sin(a) * 6.5f), H("#FFB627")); }
                    break;
                case "bell":
                    c.BlobC(8, 9, 5, 5, Gold); c.Rect(3, 4, 10, 5, Gold); c.Rect(2, 3, 12, 2, GoldDark); c.BlobC(8, 2, 1.5f, 1.5f, GoldDark); c.Rect(7, 14, 2, 2, GoldDark);
                    break;
                case "error":
                    c.BlobC(8, 8, 6, 6, H("#FF5F5F")); c.Rect(7, 4, 2, 6, Color.white); c.Rect(7, 11, 2, 2, Color.white);
                    break;
                case "unknown":
                    c.BlobC(8, 8, 6, 6, H("#6B6B6B")); c.Rect(7, 4, 2, 2, Color.white); c.Rect(7, 7, 2, 2, Color.white); c.Rect(8, 9, 2, 1, Color.white); c.Rect(9, 10, 1, 2, Color.white); c.Rect(6, 11, 4, 1, Color.white);
                    break;
                case "seedbag":
                    c.Rect(4, 2, 8, 10, H("#F2DDB2")); c.Rect(3, 11, 10, 2, H("#C9A26B")); c.BlobC(8, 7, 2.5f, 2.5f, H("#5FCB4B"));
                    break;
                case "arrow":
                    c.Tri(new Vector2(2, 9), new Vector2(14, 9), new Vector2(8, 1), Color.white); c.Rect(5, 9, 6, 6, Color.white);
                    break;
                case "hand":
                    c.Rect(5, 1, 7, 7, H("#FFE0BD")); c.Rect(6, 8, 2, 7, H("#FFE0BD")); c.Rect(8, 6, 2, 3, H("#FFE0BD")); c.Rect(10, 6, 2, 3, H("#FFE0BD"));
                    break;
                default:
                    c.BlobC(8, 8, 6, 6, H("#C9A26B"));
                    break;
            }
            c.Shade(); c.Outline();
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        /// <summary>Icon-Sprite fuer einen Icon-Schluessel aus der Logik ("crop:x", "item:x", "tool:x:tier", "upgrade:x" ...).</summary>
        public static Sprite IconFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return Get("gift");
            if (key.StartsWith("crop:")) return Get("crop:" + key.Substring(5) + ":3");
            return Get(key);
        }
    }
}
