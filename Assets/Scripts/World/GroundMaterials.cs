using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Boden-Materialien der frei gezeichneten Welt: Gras, Blumenwiese, Schlamm, Erdweg.
    /// Jede Textur ist nahtlos kachelbar (128 px = 4 Welteinheiten bei 32 px pro Einheit).
    /// Eigene Texturen koennen in Resources/Ground/&lt;name&gt; abgelegt werden und haben Vorrang.
    /// </summary>
    public static class GroundMaterials
    {
        public const int Size = 128;
        public const float WorldSize = 4f;
        public const float PixelsPerUnit = Size / WorldSize;

        static Material terrain;

        public static Material Terrain
        {
            get
            {
                if (terrain != null) return terrain;
                var shader = Resources.Load<Shader>("Shaders/TerrainSplat2D");
                if (shader == null || !shader.isSupported) { Debug.LogWarning("TerrainSplat2D-Shader fehlt oder wird nicht unterstuetzt"); shader = Shader.Find("Sprites/Default"); }
                terrain = new Material(shader) { name = "Terrain (Gras/Schlamm/Weg/Wiese)" };
                terrain.SetTexture("_GrassTex", Load("grass", Grass));
                terrain.SetTexture("_MeadowTex", Load("meadow", Meadow));
                terrain.SetTexture("_MudTex", Load("mud", Mud));
                terrain.SetTexture("_DirtTex", Load("dirt", Dirt));
                terrain.SetTexture("_NoiseTex", NoiseTex());
                terrain.SetFloat("_TexWorldSize", WorldSize);
                terrain.SetFloat("_PixelsPerUnit", PixelsPerUnit);
                return terrain;
            }
        }

        static Texture2D Load(string name, System.Func<Texture2D> make)
        {
            var t = Resources.Load<Texture2D>("Ground/" + name);
            if (t != null) { t.wrapMode = TextureWrapMode.Repeat; return t; }
            var made = make();
            made.name = "Ground_" + name;
            return made;
        }

        static float N(Px c, int x, int y, int cell, int salt) => Art.TileNoise(c, x / (float)cell, y / (float)cell, Size / cell, salt);

        // ------------------------------------------------------------------ Gras

        static void Blades(Px c, System.Random rnd, int count, Color[] ramp, int minLen, int maxLen)
        {
            for (int i = 0; i < count; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size), len = rnd.Next(minLen, maxLen + 1);
                int lean = rnd.Next(-1, 2);
                c.SetWrap(x, y, ramp[1]);
                for (int k = 1; k <= len; k++)
                {
                    int xx = x + (k == len ? lean : 0);
                    c.SetWrap(xx, y + k, k == len ? ramp[ramp.Length - 1] : ramp[Mathf.Min(ramp.Length - 2, 3 + k / 2)]);
                }
            }
        }

        public static Texture2D Grass()
        {
            var c = new Px(Size, Size, 501, 1);
            var ramp = Art.RGrass;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float t = 0.1f + N(c, x, y, 32, 1) * 0.32f + N(c, x, y, 8, 2) * 0.22f + (c.Hash(x, y) - 0.5f) * 0.1f;
                    c.SetP(x, y, Art.At(ramp, t));
                }
            var rnd = new System.Random(7);
            Blades(c, rnd, 900, ramp, 2, 4);
            // dunkle Grasbuesche
            for (int i = 0; i < 40; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size);
                for (int k = 0; k < 7; k++) c.SetWrap(x + rnd.Next(-2, 3), y + rnd.Next(-1, 2), ramp[1]);
                for (int k = 0; k < 4; k++) c.SetWrap(x + rnd.Next(-2, 3), y + 1 + rnd.Next(0, 2), ramp[4]);
            }
            return c.ToTexture();
        }

        public static Texture2D Meadow()
        {
            var c = new Px(Size, Size, 502, 1);
            var ramp = Art.RLeafWarm;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float t = 0.25f + N(c, x, y, 32, 3) * 0.3f + N(c, x, y, 8, 4) * 0.2f + (c.Hash(x, y) - 0.5f) * 0.1f;
                    c.SetP(x, y, Art.At(ramp, t));
                }
            var rnd = new System.Random(11);
            Blades(c, rnd, 1100, ramp, 2, 5);
            // Wildblumen: weiss, gelb, rosa, blau
            Color[] flowers = { Art.H("#F4F1E6"), Art.H("#F3D36B"), Art.H("#E9A6C8"), Art.H("#9DB8E8"), Art.H("#FFFFFF") };
            for (int i = 0; i < 70; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size);
                var f = flowers[rnd.Next(flowers.Length)];
                c.SetWrap(x, y - 1, ramp[2]);
                c.SetWrap(x, y, f); c.SetWrap(x + 1, y, f); c.SetWrap(x, y + 1, f); c.SetWrap(x + 1, y + 1, Art.Dark(f, 0.85f));
                if (rnd.NextDouble() < 0.5) c.SetWrap(x, y, Art.H("#E8A03C"));
            }
            return c.ToTexture();
        }

        // ------------------------------------------------------------------ Schlamm

        public static Texture2D Mud()
        {
            var c = new Px(Size, Size, 503, 1);
            var wet = Art.RSoilWet; var dry = Art.RSoil;
            var puddle = new[] { Art.H("#2B3238"), Art.H("#38434C"), Art.H("#4E5E6A"), Art.H("#7A8E9A"), Art.H("#AFC0C8") };
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float n = N(c, x, y, 32, 5) * 0.6f + N(c, x, y, 16, 6) * 0.4f;
                    float d = N(c, x, y, 8, 7);
                    float t = 0.25f + d * 0.45f + (c.Hash(x, y) - 0.5f) * 0.14f;
                    Color col = n > 0.55f ? Art.At(wet, t) : Art.At(dry, t - 0.05f);
                    if (n > 0.6f)
                    {
                        // Pfuetze: Himmel spiegelt sich, heller Lichtstreifen
                        float pt = 0.3f + (n - 0.6f) * 2.2f + (Mathf.Sin(x * 0.35f + y * 0.15f) > 0.8f ? 0.4f : 0f);
                        col = Art.At(puddle, pt);
                    }
                    else if (n > 0.57f) col = wet[0];
                    else if (n > 0.55f) col = wet[1];
                    c.SetP(x, y, col);
                }
            var rnd = new System.Random(13);
            // Kieselsteine und Erdklumpen
            for (int i = 0; i < 45; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size);
                var ramp = rnd.NextDouble() < 0.5 ? Art.RMossStone : dry;
                c.SetWrap(x, y, ramp[1]); c.SetWrap(x + 1, y, ramp[2]); c.SetWrap(x, y + 1, ramp[4]); c.SetWrap(x + 1, y + 1, ramp[3]);
            }
            // Fussspuren / Rillen
            for (int i = 0; i < 12; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size);
                for (int k = 0; k < 6; k++) { c.SetWrap(x + k, y + (k / 3), wet[0]); c.SetWrap(x + k, y + 1 + (k / 3), dry[4]); }
            }
            return c.ToTexture();
        }

        // ------------------------------------------------------------------ Erdweg

        public static Texture2D Dirt()
        {
            var c = new Px(Size, Size, 504, 1);
            var ramp = Art.RDirt;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float t = 0.28f + N(c, x, y, 32, 8) * 0.3f + N(c, x, y, 8, 9) * 0.25f + (c.Hash(x, y) - 0.5f) * 0.12f;
                    c.SetP(x, y, Art.At(ramp, t));
                }
            var rnd = new System.Random(17);
            for (int i = 0; i < 70; i++)
            {
                int x = rnd.Next(Size), y = rnd.Next(Size);
                var st = Art.RMossStone;
                int r = rnd.Next(1, 3);
                for (int yy = -r; yy <= r; yy++) for (int xx = -r; xx <= r; xx++)
                        if (xx * xx + yy * yy <= r * r) c.SetWrap(x + xx, y + yy, st[Mathf.Clamp(3 + yy - xx / 2, 0, st.Length - 1)]);
                c.SetWrap(x + 1, y - r - 1, Art.Dark(ramp[1], 0.85f));
            }
            return c.ToTexture();
        }

        // ------------------------------------------------------------------ Rauschen fuer ausgefranste Kanten

        public static Texture2D NoiseTex()
        {
            const int n = 64;
            var c = new Px(n, n, 505, 1);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = Art.TileNoise(c, x / 8f, y / 8f, 8, 21) * 0.6f + Art.TileNoise(c, x / 4f, y / 4f, 16, 22) * 0.4f;
                    c.SetP(x, y, new Color(v, v, v, 1));
                }
            var t = c.ToTexture(TextureWrapMode.Repeat, FilterMode.Bilinear);
            t.name = "Ground_Noise";
            return t;
        }
    }
}
