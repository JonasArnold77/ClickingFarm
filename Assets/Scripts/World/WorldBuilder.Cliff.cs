using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Grasige Klippe hinter dem Haus: Der Boden endet an einer Kammlinie (Spitze direkt hinter dem Haus,
    /// links steil, rechts flacher mit Knick). Dahinter faellt das Land in einen Abgrund mit
    /// nebligem Tal weit unten (Parallaxe fuer Tiefe). Der Felsrand nutzt die Skyden-Klippentextur.
    /// </summary>
    public partial class WorldBuilder
    {
        // Kammlinie wie in der Skizze (x aufsteigend)
        static readonly Vector2[] Ridge =
        {
            new Vector2(-13f, -8.5f), new Vector2(-9f, -2.4f), new Vector2(-6.5f, 1.6f), new Vector2(-3f, 6.2f),
            new Vector2(0f, 9.1f),
            new Vector2(2.6f, 8.3f), new Vector2(5.4f, 7.0f), new Vector2(6.0f, 6.3f), new Vector2(8.6f, 4.3f), new Vector2(13f, 1.2f)
        };

        Texture2D plateauTex;
        Transform abyss;
        Vector3 camStart;
        readonly List<Transform> mists = new List<Transform>();

        /// <summary>Hoehe der Klippenkante an Stelle x (mit leicht welliger, natuerlicher Kante).</summary>
        public float EdgeY(float x)
        {
            float y = Ridge[Ridge.Length - 1].y;
            for (int i = 0; i < Ridge.Length - 1; i++)
                if (x <= Ridge[i + 1].x)
                {
                    float t = Mathf.InverseLerp(Ridge[i].x, Ridge[i + 1].x, x);
                    y = Mathf.Lerp(Ridge[i].y, Ridge[i + 1].y, t);
                    break;
                }
            return y + (Fbm(x * 0.55f, 3.3f, 41) - 0.5f) * 0.7f + (SmoothNoise(x * 2.3f, 7.7f, 43) - 0.5f) * 0.18f;
        }

        /// <summary>Wie weit liegt p innerhalb des Plateaus (positiv = auf festem Boden).</summary>
        public float Inside(Vector2 p) => EdgeY(p.x) - p.y;

        void BuildPlateauMask()
        {
            var px = new Color[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    var w = MaskToWorld(x, y);
                    float v = Mathf.InverseLerp(-0.06f, 0.06f, Inside(w));
                    px[y * mw + x] = new Color(v, v, v, 1);
                }
            plateauTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "PlateauMask" };
            plateauTex.SetPixels(px);
            plateauTex.Apply();
            GroundMaterials.Terrain.SetTexture("_Plateau", plateauTex);
        }

        // ------------------------------------------------------------------ Felsrand entlang der Kante

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

        void BuildCliffEdge()
        {
            if (art.mountainAtlas == null) { Debug.LogWarning("WorldArt: Klippen-Atlas fehlt"); return; }

            // Kante in gleichen Bogenstuecken abtasten (4 Stuecke pro Texturkachel)
            const float step = 0.25f;
            var pts = new List<Vector2>();
            for (float x = -MapW / 2f - 3f; x <= MapW / 2f + 3f; x += 0.05f) pts.Add(new Vector2(x, EdgeY(x)));
            var even = new List<Vector2> { pts[0] };
            float acc = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += (pts[i] - pts[i - 1]).magnitude;
                if (acc >= step) { even.Add(pts[i]); acc = 0; }
            }

            // Textur-Ausschnitt: Klippenwand (mittlere Spalte), oben Grasrand -> unten Fels
            const float texW = 2048f;
            float u0 = (256 + 6) / texW, u1 = (512 - 6) / texW;
            float vTop = (2048 - 560) / texW, vMid = (2048 - 1000) / texW, vBot = (2048 - 1150) / texW;

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            const float inner = 0.18f, wall = 1.35f, fade = 0.9f;
            for (int i = 0; i < even.Count - 1; i++)
            {
                var a = even[i]; var b = even[i + 1];
                Vector2 na = Normal(even, i), nb = Normal(even, i + 1);
                float ua = Mathf.Lerp(u0, u1, (i % 4) / 4f), ub = Mathf.Lerp(u0, u1, (i % 4 + 1) / 4f);
                // Band 1: Grasrand + Felswand
                Quad(verts, uvs, cols, tris,
                    a - na * inner, b - nb * inner, b + nb * wall, a + na * wall,
                    new Vector2(ua, vTop), new Vector2(ub, vTop), new Vector2(ub, vMid), new Vector2(ua, vMid),
                    Color.white, Color.white, new Color(0.7f, 0.78f, 0.8f), new Color(0.7f, 0.78f, 0.8f));
                // Band 2: Fels verschwindet im Nebel
                Quad(verts, uvs, cols, tris,
                    a + na * wall, b + nb * wall, b + nb * (wall + fade), a + na * (wall + fade),
                    new Vector2(ua, vMid), new Vector2(ub, vMid), new Vector2(ub, vBot), new Vector2(ua, vBot),
                    new Color(0.7f, 0.78f, 0.8f), new Color(0.7f, 0.78f, 0.8f), new Color(0.55f, 0.68f, 0.74f, 0f), new Color(0.55f, 0.68f, 0.74f, 0f));
            }
            var mesh = new Mesh { name = "CliffEdge" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("CliffEdge");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LitMaterial(art.mountainAtlas);
            mr.sortingOrder = -32400;
        }

        static Vector2 Normal(List<Vector2> p, int i)
        {
            var d = p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(i - 1, 0)];
            return new Vector2(-d.y, d.x).normalized; // zeigt nach "oben" = in den Abgrund
        }

        static void Quad(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t,
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 t0, Vector2 t1, Vector2 t2, Vector2 t3, Color c0, Color c1, Color c2, Color c3)
        {
            int b = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            uv.Add(t0); uv.Add(t1); uv.Add(t2); uv.Add(t3);
            c.Add(c0); c.Add(c1); c.Add(c2); c.Add(c3);
            t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }

        // ------------------------------------------------------------------ Abgrund: nebliges Tal weit unten

        void BuildAbyss()
        {
            abyss = new GameObject("Abyss").transform;
            abyss.SetParent(transform, false);
            var cam = Camera.main;
            camStart = cam != null ? cam.transform.position : Vector3.zero;

            // Talboden: weicher, dunstiger Verlauf
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "AbyssFloor" };
            var px = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float n = Fbm(x * 0.25f, y * 0.25f, 77);
                    var col = Color.Lerp(new Color(0.24f, 0.36f, 0.33f), new Color(0.42f, 0.55f, 0.5f), n);
                    px[y * 32 + x] = Color.Lerp(col, new Color(0.62f, 0.72f, 0.74f), y / 31f * 0.45f);
                }
            tex.SetPixels(px); tex.Apply();
            var floor = new GameObject("ValleyFloor").AddComponent<SpriteRenderer>();
            floor.transform.SetParent(abyss, false);
            floor.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 1f);
            floor.transform.localPosition = new Vector3(0, 8, 0);
            floor.transform.localScale = new Vector3((MapW + 30f) / 32f, 34f / 32f, 1);
            floor.sortingOrder = -32700;

            // Fluss im Tal
            float rx = -14f;
            for (float y = -6f; y < 26f; y += 0.15f)
            {
                rx = Mathf.Sin(y * 0.22f) * 4.5f + Mathf.Sin(y * 0.07f) * 3f;
                var w = new GameObject("River").AddComponent<SpriteRenderer>();
                w.transform.SetParent(abyss, false);
                w.sprite = Art.Get("soft");
                w.color = new Color(0.6f, 0.78f, 0.86f, 0.35f);
                w.transform.localPosition = new Vector3(rx, y, 0);
                w.transform.localScale = new Vector3(1.3f, 1.3f, 1);
                w.sortingOrder = -32695;
            }

            // Wald weit unten: kleine, blaeulich verblasste Baeume (Luftperspektive)
            for (int i = 0; i < 900; i++)
            {
                var p = new Vector2((float)(rnd.NextDouble() * (MapW + 26) - MapW / 2f - 13f), (float)(rnd.NextDouble() * 30f - 6f));
                if (Mathf.Abs(p.x - (Mathf.Sin(p.y * 0.22f) * 4.5f + Mathf.Sin(p.y * 0.07f) * 3f)) < 1.2f) continue;
                var sr = new GameObject("FarTree").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(abyss, false);
                sr.sprite = art.Get(rnd.NextDouble() < 0.5 ? "sk_Tree_2" : "sk_Tree_3");
                float s = 0.2f + (float)rnd.NextDouble() * 0.1f;
                sr.transform.localScale = new Vector3(rnd.NextDouble() < 0.5 ? s : -s, s, 1);
                sr.transform.localPosition = p;
                float haze = Mathf.Clamp01((p.y + 6f) / 30f);
                sr.color = Color.Lerp(new Color(0.5f, 0.64f, 0.66f, 0.9f), new Color(0.68f, 0.78f, 0.82f, 0.7f), haze);
                sr.sortingOrder = -32640 - Mathf.RoundToInt(p.y * 2f);
            }

            // Nebelschwaden, die langsam treiben
            for (int i = 0; i < 26; i++)
            {
                var m = new GameObject("Mist").AddComponent<SpriteRenderer>();
                m.transform.SetParent(abyss, false);
                m.sprite = Art.Get("soft");
                m.color = new Color(0.92f, 0.95f, 0.96f, 0.22f + (float)rnd.NextDouble() * 0.15f);
                m.transform.localPosition = new Vector3((float)(rnd.NextDouble() * (MapW + 20) - MapW / 2f - 10f), (float)(rnd.NextDouble() * 24f - 2f), 0);
                float s = 3f + (float)rnd.NextDouble() * 4f;
                m.transform.localScale = new Vector3(s * 1.8f, s * 0.6f, 1);
                m.sortingOrder = -32580;
                mists.Add(m.transform);
            }
        }

        /// <summary>Parallaxe: das Tal bewegt sich langsamer als das Plateau -> wirkt tief unten.</summary>
        void UpdateAbyss(float dt)
        {
            if (abyss == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var d = cam.transform.position - camStart;
            abyss.position = new Vector3(d.x * 0.55f, d.y * 0.55f, 0);
            foreach (var m in mists)
            {
                var p = m.localPosition;
                p.x += dt * 0.12f;
                if (p.x > MapW / 2f + 12f) p.x = -MapW / 2f - 12f;
                m.localPosition = p;
            }
        }
    }
}
