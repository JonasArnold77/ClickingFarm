using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Baut eine frei gezeichnete Welt ohne Tile-Raster:
    /// ein grosser Boden mit Material-Mischung (Gras, Wiese, Schlamm, Erdweg) ueber eine Splat-Maske,
    /// darauf frei platzierte Baeume, Buesche, Steine, Blumen und ein Haus.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public static WorldBuilder I { get; private set; }

        // Kartengroesse in Welteinheiten (1 Einheit = 32 Pixel), Mittelpunkt bei (0, 0)
        public const float MapW = 48f, MapH = 64f;
        public static readonly Vector2 HousePos = new Vector2(0f, 6f);
        const int MaskPerUnit = 4;

        Texture2D mask;
        Color[] maskPixels;
        int mw, mh;
        System.Random rnd;
        readonly List<Vector2> pathPoints = new List<Vector2>();
        readonly List<Vector2> trees = new List<Vector2>();
        Transform objects;
        float ambientT;

        public static int Order(float y) => Mathf.RoundToInt(-y * 20f);

        void Awake()
        {
            I = this;
            rnd = new System.Random(1234);
            objects = new GameObject("Objects").transform;
            objects.SetParent(transform, false);
            BuildPath();
            BuildMask();
            BuildTerrain();
            PlaceHouse();
            PlaceNature();
            SetupLight();
        }

        // ------------------------------------------------------------------ Weg vom Haus nach Sueden

        void BuildPath()
        {
            pathPoints.Clear();
            var p = HousePos + new Vector2(0.1f, -0.3f);
            pathPoints.Add(p);
            float x = p.x;
            for (float y = p.y - 1.5f; y > -MapH / 2f - 2; y -= 1.5f)
            {
                x += Mathf.Sin(y * 0.18f) * 0.55f + Mathf.Sin(y * 0.07f + 1.3f) * 0.35f;
                pathPoints.Add(new Vector2(x, y));
            }
            // Abzweig nach Osten zum Waldrand
            var branchStart = pathPoints[6];
            for (int i = 1; i < 14; i++) pathPoints2.Add(branchStart + new Vector2(i * 1.6f, Mathf.Sin(i * 0.6f) * 0.8f - i * 0.25f));
        }

        readonly List<Vector2> pathPoints2 = new List<Vector2>();

        static float DistToPolyline(Vector2 p, List<Vector2> pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        public float DistToPath(Vector2 p) => Mathf.Min(DistToPolyline(p, pathPoints), DistToPolyline(p, pathPoints2));

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
            var mudSpots = new[] { (HousePos + new Vector2(-7f, -4f), 2.6f), (HousePos + new Vector2(5.5f, -9f), 1.8f), (new Vector2(-9f, -18f), 3.2f), (new Vector2(10f, 12f), 2.2f) };
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    var w = MaskToWorld(x, y);
                    // Erdweg und Vorplatz
                    float path = 1f - Mathf.InverseLerp(0.9f, 1.6f, DistToPath(w) + (Fbm(w.x * 0.6f, w.y * 0.6f, 3) - 0.5f) * 0.8f);
                    var yard = (w - (HousePos + new Vector2(0, -1.2f))) / new Vector2(4.2f, 2.2f);
                    path = Mathf.Max(path, 1f - Mathf.InverseLerp(0.8f, 1.15f, yard.magnitude + (Fbm(w.x, w.y, 4) - 0.5f) * 0.4f));
                    // Schlammloecher (und matschige Stellen am Weg)
                    float mud = 0f;
                    foreach (var (c, r) in mudSpots)
                    {
                        float d = (w - c).magnitude / r + (Fbm(w.x * 0.8f, w.y * 0.8f, 9) - 0.5f) * 0.9f;
                        mud = Mathf.Max(mud, 1f - Mathf.InverseLerp(0.75f, 1.1f, d));
                    }
                    if ((w - HousePos).magnitude > 6f) mud = Mathf.Max(mud, path * Mathf.InverseLerp(0.62f, 0.8f, Fbm(w.x * 0.35f, w.y * 0.35f, 11)));
                    // Blumenwiesen-Flecken
                    float meadow = Mathf.InverseLerp(0.48f, 0.66f, Fbm(w.x * 0.12f, w.y * 0.12f, 21));
                    maskPixels[y * mw + x] = new Color(mud, path, meadow, 1);
                }
            mask = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "SplatMask" };
            mask.SetPixels(maskPixels);
            mask.Apply();
        }

        Vector2 MaskToWorld(int x, int y) => new Vector2((x + 0.5f) / MaskPerUnit - MapW / 2f, (y + 0.5f) / MaskPerUnit - MapH / 2f);

        /// <summary>Maskenwert an einer Weltposition (R Schlamm, G Weg, B Wiese).</summary>
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
            sr.sprite = Sprite.Create(mask, new Rect(0, 0, mw, mh), new Vector2(0.5f, 0.5f), MaskPerUnit, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = GroundMaterials.Terrain;
            sr.sortingOrder = -30000;
        }

        // ------------------------------------------------------------------ Objekte

        SpriteRenderer Place(string name, Sprite sprite, Vector2 pos, float scale = 1f, bool flip = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(objects, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = Order(pos.y);
            return sr;
        }

        void Shadow(Vector2 pos, float w, float h, float alpha = 0.3f)
        {
            var sr = Place("Shadow", Art.Get("soft"), pos + new Vector2(w * 0.12f, -h * 0.1f));
            sr.transform.localScale = new Vector3(w, h, 1);
            sr.color = new Color(0.04f, 0.09f, 0.1f, alpha);
            sr.sortingOrder = -20000;
        }

        void PlaceHouse()
        {
            const float scale = 1.25f;
            var sr = Place("House", Art.Get("house:2"), HousePos, scale);
            Shadow(HousePos + new Vector2(0.4f, 0.2f), 7.5f, 1.6f, 0.35f);
            // warmes Fensterlicht fuer die Nacht vorbereiten
            var lgo = new GameObject("WindowLight");
            lgo.transform.SetParent(sr.transform, false);
            lgo.transform.position = HousePos + new Vector2(0, 1.4f);
            var l = lgo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = 6f; l.pointLightInnerRadius = 0.5f;
            l.color = new Color(1f, 0.8f, 0.5f); l.intensity = 0f;
        }

        bool Free(Vector2 p, float houseClear, float pathClear)
        {
            if ((p - (HousePos + new Vector2(0, 1.5f))).magnitude < houseClear) return false;
            if (DistToPath(p) < pathClear) return false;
            var m = MaskAt(p);
            return m.r < 0.4f && m.g < 0.3f;
        }

        void PlaceNature()
        {
            float hw = MapW / 2f, hh = MapH / 2f;

            // Baeume: dichter Waldrand, lockere Gruppen auf der Lichtung
            for (int i = 0; i < 6000 && trees.Count < 230; i++)
            {
                var p = new Vector2((float)(rnd.NextDouble() * (MapW + 6) - hw - 3), (float)(rnd.NextDouble() * (MapH + 6) - hh - 3));
                float edge = Mathf.Min(hw - Mathf.Abs(p.x), hh - Mathf.Abs(p.y));
                float forest = Fbm(p.x * 0.09f, p.y * 0.09f, 31);
                float density = edge < 6f ? 1f : forest > 0.6f ? 0.55f : 0.03f;
                if (rnd.NextDouble() > density) continue;
                if (!Free(p, 8.5f, 2.4f)) continue;
                float minD = edge < 6f ? 1.9f : 2.6f;
                bool ok = true;
                foreach (var t in trees) if ((t - p).sqrMagnitude < minD * minD) { ok = false; break; }
                if (!ok) continue;
                trees.Add(p);
                bool pine = forest < 0.45f ? rnd.NextDouble() < 0.6 : rnd.NextDouble() < 0.25;
                float s = 0.95f + (float)rnd.NextDouble() * 0.45f;
                Place(pine ? "Pine" : "Oak", pine ? Art.Pine(rnd.Next(0, 3)) : Art.Oak(rnd.Next(0, 6)), p, s, rnd.NextDouble() < 0.5);
                Shadow(p + new Vector2(0.4f, 0.1f), (pine ? 2.2f : 3.2f) * s, 0.9f * s, 0.3f);
            }

            // Buesche am Waldrand und an Baeumen
            for (int i = 0; i < 400; i++)
            {
                if (trees.Count == 0) break;
                var t = trees[rnd.Next(trees.Count)];
                var p = t + new Vector2((float)(rnd.NextDouble() * 4 - 2), (float)(rnd.NextDouble() * 2 - 1.5f));
                if (!Free(p, 7f, 1.8f)) continue;
                if (rnd.NextDouble() < 0.7) continue;
                float s = 0.9f + (float)rnd.NextDouble() * 0.5f;
                Place("Bush", Art.Bush(rnd.Next(0, 4)), p, s, rnd.NextDouble() < 0.5);
                Shadow(p, 1.6f * s, 0.5f * s, 0.25f);
            }

            // Steine, Felsen, Baumstuempfe, hohes Gras, Blumen
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector2((float)(rnd.NextDouble() * MapW - hw), (float)(rnd.NextDouble() * MapH - hh));
                if (!Free(p, 6f, 1.6f)) continue;
                double roll = rnd.NextDouble();
                var m = MaskAt(p);
                if (roll < 0.12) { Place("Stone", Art.Get("ob:stone"), p, 0.8f + (float)rnd.NextDouble() * 0.4f); Shadow(p, 0.9f, 0.35f, 0.25f); }
                else if (roll < 0.17) { Place("Boulder", Art.Get("ob:boulder"), p, 1.1f + (float)rnd.NextDouble() * 0.4f); Shadow(p, 1.5f, 0.5f, 0.3f); }
                else if (roll < 0.21) { Place("Stump", Art.Get("ob:stump"), p, 1f); Shadow(p, 1.2f, 0.4f, 0.25f); }
                else if (roll < 0.62) Place("TallGrass", Art.Get("ob:grass"), p, 0.8f + (float)rnd.NextDouble() * 0.5f, rnd.NextDouble() < 0.5);
                else if (m.b > 0.4f || roll < 0.75) Place("Flowers", Art.Get("decor:flowers:" + rnd.Next(0, 4)), p, 1f);
            }

            // Nicht begehbarer Rand: noch eine dunkle Waldkante ausserhalb der Karte
            for (float x = -hw - 4; x < hw + 4; x += 1.7f)
            {
                foreach (var y in new[] { hh + 1.5f, -hh - 2.5f })
                {
                    var p = new Vector2(x + (float)rnd.NextDouble(), y + (float)rnd.NextDouble() * 1.5f);
                    Place("EdgePine", Art.Pine(rnd.Next(0, 3)), p, 1.2f, rnd.NextDouble() < 0.5).color = new Color(0.8f, 0.85f, 0.85f);
                }
            }
            for (float y = -hh - 3; y < hh + 3; y += 1.6f)
                foreach (var x in new[] { -hw - 2f, hw + 2f })
                {
                    var p = new Vector2(x + (float)rnd.NextDouble() * 1.5f - 0.75f, y);
                    Place("EdgeOak", Art.Oak(rnd.Next(0, 6)), p, 1.2f, rnd.NextDouble() < 0.5).color = new Color(0.8f, 0.85f, 0.85f);
                }
        }

        void SetupLight()
        {
            Light2D global = null;
            foreach (var l in FindObjectsOfType<Light2D>()) if (l.lightType == Light2D.LightType.Global) { global = l; break; }
            if (global == null) { global = new GameObject("Global Light 2D").AddComponent<Light2D>(); global.lightType = Light2D.LightType.Global; }
            global.color = new Color(1f, 0.96f, 0.86f);
            global.intensity = 1f;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.12f, 0.24f, 0.16f);
        }

        // ------------------------------------------------------------------ Atmosphaere

        void Update()
        {
            ambientT -= Time.deltaTime;
            if (ambientT > 0 || Particles.I == null) return;
            ambientT = 0.15f;
            var cam = Camera.main;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var p = cam.transform.position + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0);
            p.z = 0;
            Particles.I.Spawn(p, "square", new Color(1f, 0.98f, 0.85f, 0.75f), new Vector3(Random.Range(0.1f, 0.35f), Random.Range(-0.05f, 0.15f), 0), Random.Range(3f, 5f), 0.45f, 0.1f, -0.02f, 20f);
        }
    }
}
