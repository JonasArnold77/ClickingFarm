using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Baut eine frei gezeichnete Welt ohne Tile-Raster mit den Grafiken aus
    /// Skyden "Cozy Farm" (Boden, Baeume, Haus, Natur) und Happy Harvest (Props):
    /// ein Boden-Material (Gras, dunkles Gras, Erdweg, Pflaster, Schlamm) ueber eine Splat-Maske,
    /// darauf frei platzierte Objekte, sortiert nach Y.
    /// 1 Welteinheit = 1 Skyden-Kachel (256 px).
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public static WorldBuilder I { get; private set; }

        public const float MapW = 22f, MapH = 30f;
        public static readonly Vector2 HousePos = new Vector2(0f, 4.5f);   // Mitte der Hausunterkante
        const int MaskPerUnit = 8;
        const float HouseScale = 1.6f;

        Texture2D mask;
        Color[] maskPixels;
        int mw, mh;
        System.Random rnd;
        readonly List<Vector2> path = new List<Vector2>();
        readonly List<Vector2> branch = new List<Vector2>();
        readonly List<Vector2> occupied = new List<Vector2>();
        readonly List<float> occupiedR = new List<float>();
        Transform objects;
        float ambientT;
        WorldArtSet art;

        public static int Order(float y) => Mathf.RoundToInt(-y * 100f);

        void Awake()
        {
            I = this;
            art = WorldArtSet.I;
            rnd = new System.Random(1234);
            objects = new GameObject("Objects").transform;
            objects.SetParent(transform, false);
            BuildPaths();
            BuildMask();
            BuildTerrain();
            if (art == null) { Debug.LogError("Resources/WorldArt.asset fehlt"); return; }
            PlaceHouse();
            PlaceNature();
            SetupLight();
        }

        // ------------------------------------------------------------------ Wege

        void BuildPaths()
        {
            var p = HousePos + new Vector2(0f, -0.2f);
            path.Add(p);
            float x = p.x;
            for (float y = p.y - 0.8f; y > -MapH / 2f - 1; y -= 0.8f)
            {
                x += Mathf.Sin(y * 0.35f) * 0.28f + Mathf.Sin(y * 0.13f + 1.3f) * 0.18f;
                path.Add(new Vector2(x, y));
            }
            var b = path[7];
            for (int i = 1; i < 16; i++) branch.Add(b + new Vector2(i * 0.8f, Mathf.Sin(i * 0.5f) * 0.35f - i * 0.12f));
        }

        static float DistToPolyline(Vector2 p, List<Vector2> pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        public float DistToPath(Vector2 p) => Mathf.Min(DistToPolyline(p, path), DistToPolyline(p, branch));

        // ------------------------------------------------------------------ Splat-Maske

        static float SmoothNoise(float x, float y, int salt)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int a, int b) { unchecked { int n = a * 374761393 + b * 668265263 + salt * 982451653; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xffff) / 65535f; } }
            return Mathf.Lerp(Mathf.Lerp(H(xi, yi), H(xi + 1, yi), fx), Mathf.Lerp(H(xi, yi + 1), H(xi + 1, yi + 1), fx), fy);
        }

        static float Fbm(float x, float y, int salt) => SmoothNoise(x, y, salt) * 0.55f + SmoothNoise(x * 2.1f, y * 2.1f, salt + 1) * 0.3f + SmoothNoise(x * 4.3f, y * 4.3f, salt + 2) * 0.15f;

        void BuildMask()
        {
            mw = Mathf.RoundToInt(MapW * MaskPerUnit); mh = Mathf.RoundToInt(MapH * MaskPerUnit);
            maskPixels = new Color[mw * mh];
            var mudSpots = new[] { (HousePos + new Vector2(-4.2f, -3.2f), 1.1f), (HousePos + new Vector2(3.8f, -6.5f), 0.8f), (new Vector2(-5f, -9f), 1.4f), (new Vector2(6f, 9f), 1.0f) };
            var yardC = HousePos + new Vector2(0f, -0.65f);
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    var w = MaskToWorld(x, y);
                    float edge = (Fbm(w.x * 1.2f, w.y * 1.2f, 3) - 0.5f) * 0.35f;
                    float dirt = 1f - Mathf.InverseLerp(0.32f, 0.62f, DistToPath(w) + edge);
                    // Vorplatz aus Pflaster vor dem Haus, von Erde umrandet
                    var yd = (w - yardC) / new Vector2(1.9f, 0.85f);
                    float stone = 1f - Mathf.InverseLerp(0.85f, 1.0f, yd.magnitude + edge * 0.4f);
                    dirt = Mathf.Max(dirt, 1f - Mathf.InverseLerp(1.0f, 1.35f, yd.magnitude + edge));
                    float mud = 0f;
                    foreach (var (c, r) in mudSpots)
                    {
                        float d = (w - c).magnitude / r + (Fbm(w.x * 1.5f, w.y * 1.5f, 9) - 0.5f) * 0.8f;
                        mud = Mathf.Max(mud, 1f - Mathf.InverseLerp(0.75f, 1.05f, d));
                    }
                    float dark = Mathf.InverseLerp(0.42f, 0.62f, Fbm(w.x * 0.3f, w.y * 0.3f, 21));
                    maskPixels[y * mw + x] = new Color(mud, dirt, dark, stone);
                }
            mask = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "SplatMask" };
            mask.SetPixels(maskPixels);
            mask.Apply();
        }

        Vector2 MaskToWorld(int x, int y) => new Vector2((x + 0.5f) / MaskPerUnit - MapW / 2f, (y + 0.5f) / MaskPerUnit - MapH / 2f);

        public Color MaskAt(Vector2 w)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((w.x + MapW / 2f) * MaskPerUnit), 0, mw - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((w.y + MapH / 2f) * MaskPerUnit), 0, mh - 1);
            return maskPixels[y * mw + x];
        }

        void BuildTerrain()
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // Rand etwas groesser als die Karte, damit man nie ins Leere schaut
            sr.sprite = Sprite.Create(mask, new Rect(0, 0, mw, mh), new Vector2(0.5f, 0.5f), MaskPerUnit, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = GroundMaterials.Terrain;
            sr.sortingOrder = -32000;
        }

        // ------------------------------------------------------------------ Objekte setzen

        /// <summary>Setzt ein Sprite so, dass seine Unterkante auf "foot" steht (Sortierung nach Y).</summary>
        SpriteRenderer Place(string spriteName, Vector2 foot, float scale = 1f, bool flip = false, float footInset = 0f)
        {
            var sprite = art.Get(spriteName);
            if (sprite == null) return null;
            var go = new GameObject(spriteName);
            go.transform.SetParent(objects, false);
            go.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            float bottom = sprite.bounds.min.y * scale;
            go.transform.position = new Vector3(foot.x, foot.y - bottom - footInset * scale, 0);
            sr.sortingOrder = Order(foot.y);
            return sr;
        }

        /// <summary>Wie Place, aber auf eine Zielhoehe (Welteinheiten) normiert – fuer Props mit anderer Pixeldichte.</summary>
        SpriteRenderer PlaceSized(string spriteName, Vector2 foot, float height, bool flip = false)
        {
            var sprite = art.Get(spriteName);
            if (sprite == null) return null;
            return Place(spriteName, foot, height / sprite.bounds.size.y, flip);
        }

        void Shadow(Vector2 foot, float w, float h, float alpha = 0.28f)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(objects, false);
            go.transform.position = foot + new Vector2(w * 0.1f, 0.02f);
            go.transform.localScale = new Vector3(w, h, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get("soft");
            sr.color = new Color(0.08f, 0.14f, 0.1f, alpha);
            sr.sortingOrder = -31000;
        }

        void Reserve(Vector2 p, float r) { occupied.Add(p); occupiedR.Add(r); }

        bool Free(Vector2 p, float r)
        {
            for (int i = 0; i < occupied.Count; i++)
            {
                float d = r + occupiedR[i];
                if ((occupied[i] - p).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        bool OnBareGrass(Vector2 p, float pathClear)
        {
            if (DistToPath(p) < pathClear) return false;
            var m = MaskAt(p);
            return m.r < 0.3f && m.g < 0.25f && m.a < 0.2f;
        }

        Vector2 RandomPoint(float margin = 0f) =>
            new Vector2((float)(rnd.NextDouble() * (MapW - 2 * margin) - MapW / 2f + margin), (float)(rnd.NextDouble() * (MapH - 2 * margin) - MapH / 2f + margin));

        float R() => (float)rnd.NextDouble();

        // ------------------------------------------------------------------ Haus

        void PlaceHouse()
        {
            Shadow(HousePos + new Vector2(0.15f, 0.05f), 3.6f, 0.55f, 0.35f);
            Place("sk_House_0", HousePos, HouseScale, false, 0.02f);
            Reserve(HousePos + new Vector2(0, 0.9f), 2.0f);
            Reserve(HousePos + new Vector2(0, -0.6f), 1.2f); // Vorplatz frei halten
            // ein paar Happy-Harvest-Props direkt am Haus
            var barrel = HousePos + new Vector2(1.85f, -0.25f);
            PlaceSized("hh_barrel", barrel, 0.5f); Shadow(barrel, 0.55f, 0.18f); Reserve(barrel, 0.3f);
            var sign = path[4] + new Vector2(0.75f, 0f);
            PlaceSized("hh_sign", sign, 0.7f); Shadow(sign, 0.35f, 0.12f); Reserve(sign, 0.25f);

            // warmes Fensterlicht fuer die Nacht vorbereiten
            var lgo = new GameObject("WindowLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.position = HousePos + new Vector2(0, 0.6f);
            var l = lgo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = 3f; l.pointLightInnerRadius = 0.3f;
            l.color = new Color(1f, 0.8f, 0.5f); l.intensity = 0f;
        }

        // ------------------------------------------------------------------ Natur

        void PlaceNature()
        {
            float hw = MapW / 2f, hh = MapH / 2f;

            // Baeume: dichter Waldrand, lockere Gruppen auf der Lichtung
            var treePos = new List<Vector2>();
            for (int i = 0; i < 9000 && treePos.Count < 150; i++)
            {
                var p = RandomPoint(-1.5f);
                float edge = Mathf.Min(hw - Mathf.Abs(p.x), hh - Mathf.Abs(p.y));
                float grove = Fbm(p.x * 0.2f, p.y * 0.2f, 31);
                float density = edge < 2.5f ? 1f : grove > 0.62f ? 0.5f : 0.02f;
                if (R() > density) continue;
                if ((p - HousePos).magnitude < 4f || !OnBareGrass(p, 0.9f)) continue;
                bool big = R() < 0.6f;
                float r = big ? 1.05f : 0.7f;
                if (!Free(p, r)) continue;
                Reserve(p, r);
                treePos.Add(p);
                bool pine = grove < 0.5f ? R() < 0.55f : R() < 0.3f;
                string name = pine ? (big ? "sk_Tree_2" : "sk_Tree_1") : (big ? "sk_Tree_3" : "sk_Tree_0");
                float s = (big ? 1.55f : 1.3f) * (0.88f + R() * 0.3f);
                Place(name, p, s, R() < 0.5f, 0.06f);
            }

            // Buesche und Pilze an Baeumen
            foreach (var t in treePos)
            {
                if (R() < 0.45f)
                {
                    var p = t + new Vector2(R() * 1.6f - 0.8f, -0.3f - R() * 0.5f);
                    if (OnBareGrass(p, 0.6f) && Free(p, 0.35f)) { Reserve(p, 0.35f); Place("sk_Bush", p, 0.7f + R() * 0.3f, R() < 0.5f, 0.05f); }
                }
                if (R() < 0.18f)
                {
                    var p = t + new Vector2(R() * 1.2f - 0.6f, -0.2f - R() * 0.4f);
                    if (OnBareGrass(p, 0.5f) && Free(p, 0.2f)) { Reserve(p, 0.2f); Place("sk_Mushroom_" + rnd.Next(0, 5), p, 0.6f + R() * 0.3f, R() < 0.5f, 0.03f); }
                }
            }

            // Felsen, Steine, Baumstamm
            for (int i = 0; i < 40; i++)
            {
                var p = RandomPoint(1f);
                if ((p - HousePos).magnitude < 3f || !OnBareGrass(p, 0.7f) || !Free(p, 0.4f)) continue;
                Reserve(p, 0.4f);
                float roll = R();
                if (roll < 0.35f) Place("sk_Stones", p, 0.6f + R() * 0.3f, R() < 0.5f, 0.04f);
                else if (roll < 0.55f) { PlaceSized("hh_rock_big", p, 0.6f + R() * 0.2f, R() < 0.5f); Shadow(p, 0.9f, 0.2f); }
                else if (roll < 0.75f) { PlaceSized("hh_rock_medium", p, 0.4f, R() < 0.5f); Shadow(p, 0.6f, 0.15f); }
                else if (roll < 0.88f) { PlaceSized("hh_log", p, 0.3f, R() < 0.5f); Shadow(p, 0.9f, 0.15f); }
                else { PlaceSized("hh_rock_small", p, 0.25f, R() < 0.5f); Shadow(p, 0.4f, 0.1f); }
            }

            // Blumen, Gras, kleine Pflanzen
            string[] smalls = { "sk_Flowers_0", "sk_Flowers_1", "sk_Grass", "sk_Grass", "hh_grass1", "hh_grass2", "hh_flowers_red", "hh_flowers_white", "hh_flowers_yellow" };
            for (int i = 0; i < 260; i++)
            {
                var p = RandomPoint(0.5f);
                if ((p - HousePos).magnitude < 2.2f || !OnBareGrass(p, 0.45f) || !Free(p, 0.12f)) continue;
                string n = smalls[rnd.Next(smalls.Length)];
                if (n.StartsWith("hh_")) PlaceSized(n, p, n.Contains("grass") ? 0.3f + R() * 0.1f : 0.22f, R() < 0.5f);
                else Place(n, p, 0.55f + R() * 0.3f, R() < 0.5f, 0.02f);
            }

            // Blumenbeete am Haus
            foreach (var dx in new[] { -1.55f, -1.2f, 1.25f })
                PlaceSized(dx < 0 ? "hh_flowers_red" : "hh_flowers_yellow", HousePos + new Vector2(dx, -0.1f), 0.24f);

            // Dunkle Waldkante ausserhalb der Karte, damit der Rand nie leer wirkt
            for (float x = -hw - 1.5f; x < hw + 1.5f; x += 0.9f)
                foreach (var y in new[] { hh + 0.4f, -hh - 0.6f })
                    Place(R() < 0.5f ? "sk_Tree_2" : "sk_Tree_3", new Vector2(x + R() * 0.4f, y + R() * 0.6f), 1.6f, R() < 0.5f, 0.06f).color = new Color(0.78f, 0.84f, 0.82f);
            for (float y = -hh - 1f; y < hh + 1f; y += 0.9f)
                foreach (var x in new[] { -hw - 0.6f, hw + 0.6f })
                    Place(R() < 0.5f ? "sk_Tree_2" : "sk_Tree_3", new Vector2(x + R() * 0.6f - 0.3f, y), 1.6f, R() < 0.5f, 0.06f).color = new Color(0.78f, 0.84f, 0.82f);
        }

        void SetupLight()
        {
            Light2D global = null;
            foreach (var l in FindObjectsOfType<Light2D>()) if (l.lightType == Light2D.LightType.Global) { global = l; break; }
            if (global == null) { global = new GameObject("Global Light 2D").AddComponent<Light2D>(); global.lightType = Light2D.LightType.Global; }
            global.color = new Color(1f, 0.97f, 0.9f);
            global.intensity = 1f;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.2f, 0.32f, 0.2f);
        }

        // ------------------------------------------------------------------ Atmosphaere: schwebende Pollen

        void Update()
        {
            ambientT -= Time.deltaTime;
            if (ambientT > 0 || Particles.I == null) return;
            ambientT = 0.2f;
            var cam = Camera.main;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var p = cam.transform.position + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0);
            p.z = 0;
            Particles.I.Spawn(p, "square", new Color(1f, 0.98f, 0.85f, 0.7f), new Vector3(Random.Range(0.04f, 0.12f), Random.Range(-0.02f, 0.05f), 0), Random.Range(3f, 5f), 0.18f, 0.04f, -0.01f, 20f, 0f, 30000);
        }
    }
}
