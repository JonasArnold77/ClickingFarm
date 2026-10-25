using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Berg hinter dem Haus: Am Bergfuss steigt eine Felswand (Skyden-Klippentextur) auf,
    /// darueber eine grasige Hochebene mit Baeumen, dahinter zwei dunstige Bergketten mit
    /// Schneegipfeln und der Himmel mit Wolken. Jede Ebene bewegt sich mit eigener
    /// Geschwindigkeit (Parallaxe) -> Tiefe wie in einem gemalten Hintergrund.
    /// </summary>
    public partial class WorldBuilder
    {
        // Bergfuss (Grenze zwischen begehbarer Wiese und Berg), x aufsteigend
        static readonly Vector2[] Ridge =
        {
            new Vector2(-15f, 3.0f), new Vector2(-9f, 4.6f), new Vector2(-5f, 6.0f), new Vector2(-2f, 6.9f),
            new Vector2(0f, 7.2f), new Vector2(3f, 7.0f), new Vector2(6f, 6.2f), new Vector2(9.5f, 5.0f), new Vector2(15f, 3.6f)
        };
        const float FaceH = 3.0f;   // Hoehe der Felswand in Welteinheiten

        Texture2D plateauTex;
        Vector3 camStart;
        readonly List<(Transform t, float factor)> parallax = new List<(Transform, float)>();
        readonly List<(Transform t, float speed, float phase)> clouds = new List<(Transform, float, float)>();

        /// <summary>Hoehe des Bergfusses an Stelle x (leicht wellig).</summary>
        public float EdgeY(float x)
        {
            float y = Ridge[Ridge.Length - 1].y;
            for (int i = 0; i < Ridge.Length - 1; i++)
                if (x <= Ridge[i + 1].x)
                {
                    float t = Mathf.InverseLerp(Ridge[i].x, Ridge[i + 1].x, x);
                    t = t * t * (3 - 2 * t);
                    y = Mathf.Lerp(Ridge[i].y, Ridge[i + 1].y, t);
                    break;
                }
            return y + (Fbm(x * 0.5f, 3.3f, 41) - 0.5f) * 0.35f;
        }

        /// <summary>Wie weit liegt p vor dem Bergfuss (positiv = auf der Wiese).</summary>
        public float Inside(Vector2 p) => EdgeY(p.x) - p.y;

        void BuildPlateauMask()
        {
            var px = new Color[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    float v = Mathf.InverseLerp(-0.1f, 0.1f, Inside(MaskToWorld(x, y)));
                    px[y * mw + x] = new Color(v, v, v, 1);
                }
            plateauTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "PlateauMask" };
            plateauTex.SetPixels(px);
            plateauTex.Apply();
            GroundMaterials.Terrain.SetTexture("_Plateau", plateauTex);
        }

        // ------------------------------------------------------------------ Hilfen

        static Material spriteMat;

        /// <summary>Standard-2D-Material (beleuchtet) mit eigener Textur.</summary>
        static Material LitMaterial(Texture tex)
        {
            if (spriteMat == null)
            {
                var tmp = new GameObject("tmp").AddComponent<SpriteRenderer>();
                spriteMat = tmp.sharedMaterial;
                Destroy(tmp.gameObject);
            }
            return new Material(spriteMat) { mainTexture = tex };
        }

        /// <summary>Kopiert einen Ausschnitt aus einem Atlas in eine eigene, kachelbare Textur (auf der GPU).</summary>
        static Texture2D CopyRegion(Texture2D atlas, int x, int yFromBottom, int w, int h, TextureWrapMode wrapV)
        {
            var t = new Texture2D(w, h, atlas.format, false) { filterMode = FilterMode.Bilinear, wrapModeU = TextureWrapMode.Repeat, wrapModeV = wrapV, name = "Region" };
            try { Graphics.CopyTexture(atlas, 0, 0, x, yFromBottom, w, h, t, 0, 0, 0, 0); }
            catch (System.Exception e) { Debug.LogWarning("CopyTexture nicht moeglich: " + e.Message); }
            return t;
        }

        static Sprite mistSprite;

        /// <summary>Weicher Kreis mit bilinearer Filterung (Wolken, Dunst, Lichtstrahlen).</summary>
        static Sprite MistSprite
        {
            get
            {
                if (mistSprite != null) return mistSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Mist" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = Mathf.Clamp01(1 - d);
                        px[y * n + x] = new Color(1, 1, 1, a * a * (3 - 2 * a));
                    }
                tex.SetPixels(px); tex.Apply();
                mistSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return mistSprite;
            }
        }

        Transform Layer(string name, float factor)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            if (factor < 0.999f) parallax.Add((t, factor));
            return t;
        }

        static MeshRenderer MeshObj(Transform parent, string name, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tri, Material mat, int order)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(c); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            return mr;
        }

        /// <summary>Band zwischen Unter- und Oberkante (Funktionen von x), UV in Weltkoordinaten / texWorld.</summary>
        static void Band(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t, float x0, float x1, float step,
            System.Func<float, float> bottom, System.Func<float, float> top, float texW, float texH, bool vFromBand,
            System.Func<float, float, Color> colBottom, System.Func<float, float, Color> colTop)
        {
            for (float x = x0; x < x1; x += step)
            {
                float xb = x + step;
                float b0 = bottom(x), b1 = bottom(xb), t0 = top(x), t1 = top(xb);
                int i = v.Count;
                v.Add(new Vector3(x, b0)); v.Add(new Vector3(xb, b1)); v.Add(new Vector3(xb, t1)); v.Add(new Vector3(x, t0));
                if (vFromBand) { uv.Add(new Vector2(x / texW, 0)); uv.Add(new Vector2(xb / texW, 0)); uv.Add(new Vector2(xb / texW, 1)); uv.Add(new Vector2(x / texW, 1)); }
                else { uv.Add(new Vector2(x / texW, b0 / texH)); uv.Add(new Vector2(xb / texW, b1 / texH)); uv.Add(new Vector2(xb / texW, t1 / texH)); uv.Add(new Vector2(x / texW, t0 / texH)); }
                c.Add(colBottom(x, b0)); c.Add(colBottom(xb, b1)); c.Add(colTop(xb, t1)); c.Add(colTop(x, t0));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
        }

        // ------------------------------------------------------------------ Berg

        void BuildMountain()
        {
            var cam = Camera.main;
            camStart = cam != null ? cam.transform.position : Vector3.zero;
            float X0 = -MapW / 2f - 14f, X1 = MapW / 2f + 14f;

            // Texturen aus den Atlanten: Felswand (Skyden Mountain Tiles), reiner Fels, Gras
            var mAtlas = art.mountainAtlas;
            var gAtlas = art.groundAtlas;
            Texture2D faceTex = null, rockTex = null, grassTex = null;
            if (mAtlas != null)
            {
                faceTex = CopyRegion(mAtlas, 256, 860, 256, 628, TextureWrapMode.Clamp);   // Grasfuss unten -> Fels -> Graskante oben
                rockTex = CopyRegion(mAtlas, 256, 1024, 256, 256, TextureWrapMode.Repeat); // reiner Fels
            }
            if (gAtlas != null) grassTex = CopyRegion(gAtlas, 1024, 1792, 256, 256, TextureWrapMode.Repeat);

            // 1) Himmel
            var sky = Layer("Sky", 0.2f);
            var skyTex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Sky" };
            var sp = new Color[64];
            for (int i = 0; i < 64; i++) sp[i] = Color.Lerp(new Color(1f, 0.93f, 0.8f), new Color(0.55f, 0.76f, 0.93f), Mathf.Pow(i / 63f, 0.7f));
            skyTex.SetPixels(sp); skyTex.Apply();
            var skySr = new GameObject("SkyGradient").AddComponent<SpriteRenderer>();
            skySr.transform.SetParent(sky, false);
            skySr.sprite = Sprite.Create(skyTex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0f), 1f);
            skySr.transform.localPosition = new Vector3(0, 8f, 0);
            skySr.transform.localScale = new Vector3(X1 - X0 + 20f, 24f / 64f, 1);
            skySr.sortingOrder = -32760;
            for (int i = 0; i < 14; i++)
            {
                var cl = new GameObject("Cloud").AddComponent<SpriteRenderer>();
                cl.transform.SetParent(sky, false);
                cl.sprite = MistSprite;
                float s = 2.5f + R() * 4f;
                cl.transform.localScale = new Vector3(s * 2.2f, s * 0.7f, 1);
                cl.transform.localPosition = new Vector3(X0 + R() * (X1 - X0), 16.5f + R() * 7f, 0);
                cl.color = new Color(1f, 0.99f, 0.96f, 0.55f + R() * 0.3f);
                cl.sortingOrder = -32755;
                clouds.Add((cl.transform, 0.08f + R() * 0.1f, R() * 10f));
            }

            // 2) Ferne Bergkette mit Schneegipfeln
            var far = Layer("FarRange", 0.45f);
            System.Func<float, float> farTop = x => 14.2f + 4.6f * Mathf.Exp(-(x + 3f) * (x + 3f) / 30f) + 3.2f * Mathf.Exp(-(x - 10f) * (x - 10f) / 16f)
                                                      + 2.5f * Mathf.Exp(-(x + 15f) * (x + 15f) / 20f) + (Fbm(x * 0.35f, 1.1f, 61) - 0.5f) * 2.2f;
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
                Band(v, uv, c, t, X0, X1, 0.25f, x => 9f, farTop, 3.2f, 3.2f, false,
                    (x, y) => new Color(0.68f, 0.76f, 0.84f),
                    (x, y) => y > 17.2f ? new Color(0.97f, 0.98f, 1f) : Color.Lerp(new Color(0.68f, 0.76f, 0.84f), new Color(0.82f, 0.87f, 0.93f), Mathf.InverseLerp(13.5f, 17f, y)));
                MeshObj(far, "Far", v, uv, c, t, LitMaterial(rockTex), -32740);
            }
            Haze(far, 13.2f, 16, -32735, 0.3f);

            // 3) Mittlere Bergkette, gruenblau, naeher
            var mid = Layer("MidRange", 0.7f);
            System.Func<float, float> midTop = x => 12.4f + 2.2f * Mathf.Exp(-(x - 5f) * (x - 5f) / 14f) + 1.8f * Mathf.Exp(-(x + 8f) * (x + 8f) / 12f)
                                                     + (Fbm(x * 0.5f, 2.2f, 71) - 0.5f) * 1.6f;
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
                Band(v, uv, c, t, X0, X1, 0.25f, x => 9f, midTop, 2.6f, 2.6f, false,
                    (x, y) => new Color(0.48f, 0.62f, 0.6f),
                    (x, y) => new Color(0.62f, 0.74f, 0.7f));
                MeshObj(mid, "Mid", v, uv, c, t, LitMaterial(rockTex), -32720);
                // bewaldete Kuppe vorne an der mittleren Kette
                var v2 = new List<Vector3>(); var uv2 = new List<Vector2>(); var c2 = new List<Color>(); var t2 = new List<int>();
                Band(v2, uv2, c2, t2, X0, X1, 0.25f, x => 9f, x => midTop(x) - 1.3f + Mathf.Sin(x * 0.9f) * 0.25f, 1.6f, 1.6f, false,
                    (x, y) => new Color(0.45f, 0.62f, 0.5f), (x, y) => new Color(0.56f, 0.72f, 0.56f));
                MeshObj(mid, "MidForest", v2, uv2, c2, t2, LitMaterial(grassTex), -32718);
            }
            Haze(mid, 11.4f, 18, -32712, 0.22f);

            // 4) Hochebene ueber der Felswand (weltfest) + Baeume darauf
            var near = Layer("Mountain", 1f);
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
                Band(v, uv, c, t, X0, X1, 0.25f, x => EdgeY(x) + FaceH - 0.15f, x => EdgeY(x) + FaceH + 1.6f, 1f, 1f, false,
                    (x, y) => new Color(0.9f, 0.96f, 0.9f), (x, y) => new Color(0.86f, 0.94f, 0.92f, 0f));
                MeshObj(near, "Highland", v, uv, c, t, LitMaterial(grassTex), -32705);
            }
            for (float x = X0 + 1f; x < X1 - 1f; x += 0.7f + R() * 0.9f)
            {
                if (R() < 0.25f) continue;
                var foot = new Vector2(x, EdgeY(x) + FaceH + 0.2f + R() * 0.6f);
                var sr = Place(R() < 0.6f ? "sk_Tree_2" : "sk_Tree_3", foot, 1.1f + R() * 0.4f, R() < 0.5f, 0.06f);
                if (sr == null) continue;
                sr.transform.SetParent(near, true);
                sr.color = new Color(0.86f, 0.93f, 0.92f);
                sr.sortingOrder = -32700 + Mathf.RoundToInt((20f - foot.y) * 0.5f);
            }

            // 5) Felswand am Bergfuss
            if (faceTex != null)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
                Band(v, uv, c, t, X0, X1, 0.2f, x => EdgeY(x) - 0.2f, x => EdgeY(x) + FaceH, 1.25f, 1f, true,
                    (x, y) => Color.white, (x, y) => new Color(0.93f, 0.97f, 0.95f));
                MeshObj(near, "RockFace", v, uv, c, t, LitMaterial(faceTex), -32680);
            }
        }

        /// <summary>Dunstschleier am Fuss einer Bergkette (Luftperspektive).</summary>
        void Haze(Transform parent, float y, int count, int order, float alpha)
        {
            float X0 = -MapW / 2f - 14f, X1 = MapW / 2f + 14f;
            for (int i = 0; i < count; i++)
            {
                var m = new GameObject("Haze").AddComponent<SpriteRenderer>();
                m.transform.SetParent(parent, false);
                m.sprite = MistSprite;
                float s = 3f + R() * 3f;
                m.transform.localScale = new Vector3(s * 2.4f, s * 0.3f, 1);
                m.transform.localPosition = new Vector3(X0 + (X1 - X0) * (i + R()) / count, y + R() * 0.8f, 0);
                m.color = new Color(0.95f, 0.96f, 0.95f, alpha * (0.6f + R() * 0.4f));
                m.sortingOrder = order;
                clouds.Add((m.transform, 0.03f + R() * 0.05f, R() * 10f));
            }
        }

        /// <summary>Parallaxe der Bergebenen und treibende Wolken.</summary>
        void UpdateMountain(float dt)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var d = cam.transform.position - camStart;
            foreach (var (t, f) in parallax) t.position = new Vector3(d.x * (1f - f), d.y * (1f - f), 0);
            float lim = MapW / 2f + 16f;
            foreach (var (t, speed, phase) in clouds)
            {
                var p = t.localPosition;
                p.x += speed * dt;
                if (p.x > lim) p.x = -lim;
                t.localPosition = p;
            }
        }
    }
}
