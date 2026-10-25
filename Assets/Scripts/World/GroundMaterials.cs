using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Boden-Material der freien Welt: mischt Bodenarten aus dem Skyden-Atlas "Ground Tiles"
    /// (Gras, dunkles Gras, Erdweg, Kopfsteinpflaster, Schlamm) ueber eine Splat-Maske.
    /// Welche Atlas-Kachel welche Bodenart ist, steht in den Rect-Konstanten unten.
    /// </summary>
    public static class GroundMaterials
    {
        // Atlas: 8x8 Kacheln; Rechteck = (x, y, Breite, Hoehe) in UV, Ursprung unten links
        static Vector4 TileRect(int col, int rowFromTop) => new Vector4(col / 8f, (7 - rowFromTop) / 8f, 1 / 8f, 1 / 8f);
        public static readonly Vector4 Grass = TileRect(4, 0);   // einfarbiges Gras
        public static readonly Vector4 DarkGrass = TileRect(3, 0);
        public static readonly Vector4 Dirt = TileRect(1, 5);    // Mitte des weichen Erd-Sets
        public static readonly Vector4 Stone = TileRect(1, 2);   // Mitte des Pflaster-Sets

        static Material terrain;

        public static Material Terrain
        {
            get
            {
                if (terrain != null) return terrain;
                var shader = Resources.Load<Shader>("Shaders/TerrainSplat2D");
                if (shader == null || !shader.isSupported) { Debug.LogWarning("TerrainSplat2D-Shader fehlt oder wird nicht unterstuetzt"); shader = Shader.Find("Sprites/Default"); }
                terrain = new Material(shader) { name = "Terrain (Skyden-Boden)" };
                var art = WorldArtSet.I;
                if (art != null && art.groundAtlas != null) terrain.SetTexture("_Atlas", art.groundAtlas);
                else Debug.LogWarning("WorldArt: Boden-Atlas fehlt");
                terrain.SetVector("_GrassRect", Grass);
                terrain.SetVector("_MeadowRect", DarkGrass);
                terrain.SetVector("_DirtRect", Dirt);
                terrain.SetVector("_StoneRect", Stone);
                terrain.SetTexture("_NoiseTex", NoiseTex());
                terrain.SetFloat("_TileWorld", 1f);
                return terrain;
            }
        }

        /// <summary>Atlas-Kachel als eigenes Sprite (z. B. Gras mit Blumen als Boden-Deko).</summary>
        public static Sprite TileSprite(int col, int rowFromTop)
        {
            var atlas = WorldArtSet.I != null ? WorldArtSet.I.groundAtlas : null;
            if (atlas == null) return null;
            return Sprite.Create(atlas, new Rect(col * 256, (7 - rowFromTop) * 256, 256, 256), new Vector2(0.5f, 0.5f), 256, 0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// Sehr weiches, kachelbares Rauschen fuer Nebel: grobe Wertrauschen-Oktaven, danach
        /// mehrfach (umlaufend) weichgezeichnet, damit beim Verzerren im Shader keine Kanten/Linien entstehen.
        /// </summary>
        public static Texture2D FogNoiseTex(int n = 256)
        {
            var c = new Px(n, n, 606, 1);
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    v[y * n + x] = Art.TileNoise(c, x * 4f / n, y * 4f / n, 4, 31) * 0.6f + Art.TileNoise(c, x * 8f / n, y * 8f / n, 8, 32) * 0.4f;
            int r = n / 24;
            var tmp = new float[n * n];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float sum = 0;
                        for (int k = -r; k <= r; k++) sum += v[y * n + ((x + k) % n + n) % n];
                        tmp[y * n + x] = sum / (2 * r + 1);
                    }
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float sum = 0;
                        for (int k = -r; k <= r; k++) sum += tmp[((y + k) % n + n) % n * n + x];
                        v[y * n + x] = sum / (2 * r + 1);
                    }
            }
            // Kontrast wieder auf 0..1 strecken
            float mn = 1, mx = 0;
            foreach (var f in v) { mn = Mathf.Min(mn, f); mx = Mathf.Max(mx, f); }
            for (int i = 0; i < v.Length; i++) { float f = (v[i] - mn) / Mathf.Max(0.001f, mx - mn); c.c[i] = new Color(f, f, f, 1); }
            var t = c.ToTexture(TextureWrapMode.Repeat, FilterMode.Bilinear);
            t.name = "Fog_Noise";
            return t;
        }

        /// <summary>Weiches, kachelbares Rauschen fuer ausgefranste Kanten und Pfuetzen.</summary>
        public static Texture2D NoiseTex(int n = 64)
        {
            var c = new Px(n, n, 505, 1);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float f = n / 64f;
                    float v = Art.TileNoise(c, x / (8f * f), y / (8f * f), 8, 21) * 0.6f + Art.TileNoise(c, x / (4f * f), y / (4f * f), 16, 22) * 0.4f;
                    c.SetP(x, y, new Color(v, v, v, 1));
                }
            var t = c.ToTexture(TextureWrapMode.Repeat, FilterMode.Bilinear);
            t.name = "Ground_Noise";
            return t;
        }
    }
}
