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
            float vTop = (2048 - 565) / texW, vMid = (2048 - 700) / texW, vBot = (2048 - 780) / texW;

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            const float inner = 0.12f, wall = 0.32f, fade = 0.28f; // schmale Kante
            for (int i = 0; i < even.Count - 1; i++)
            {
                var a = even[i]; var b = even[i + 1];
                Vector2 na = Normal(even, i), nb = Normal(even, i + 1);
                float ua = Mathf.Lerp(u0, u1, (i % 4) / 4f), ub = Mathf.Lerp(u0, u1, (i % 4 + 1) / 4f);
                // Band 1: Grasrand + Felswand
                Quad(verts, uvs, cols, tris,
                    a - na * inner, b - nb * inner, b + nb * wall, a + na * wall,
                    new Vector2(ua, vTop), new Vector2(ub, vTop), new Vector2(ub, vMid), new Vector2(ua, vMid),
                    Color.white, Color.white, new Color(0.74f, 0.75f, 0.76f), new Color(0.74f, 0.75f, 0.76f));
                // Band 2: Fels verschwindet im Nebel
                Quad(verts, uvs, cols, tris,
                    a + na * wall, b + nb * wall, b + nb * (wall + fade), a + na * (wall + fade),
                    new Vector2(ua, vMid), new Vector2(ub, vMid), new Vector2(ub, vBot), new Vector2(ua, vBot),
                    new Color(0.74f, 0.75f, 0.76f), new Color(0.74f, 0.75f, 0.76f), new Color(0.78f, 0.79f, 0.8f, 0f), new Color(0.78f, 0.79f, 0.8f, 0f));
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

        // ------------------------------------------------------------------ Abgrund: grauer, wabernder Nebel

        static Sprite mistSprite;

        /// <summary>Weicher Kreis mit bilinearer Filterung (Nebel soll weich, nicht pixelig sein).</summary>
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

        /// <summary>Eine Nebelschwade, die treibt und dabei "atmet" (Groesse, Dichte, leichtes Auf und Ab).</summary>
        class Puff { public Transform t; public SpriteRenderer sr; public Vector3 baseScale; public float baseA, phase, speed, drift, bob; public bool world; }
        readonly List<Puff> puffs = new List<Puff>();

        void BuildAbyss()
        {
            abyss = new GameObject("Abyss").transform;
            abyss.SetParent(transform, false);
            var cam = Camera.main;
            camStart = cam != null ? cam.transform.position : Vector3.zero;

            // Grundschicht: grauer, wabernder Nebel (Shader), fuellt den ganzen Abgrund
            var fogShader = Resources.Load<Shader>("Shaders/Fog2D");
            var white = new Texture2D(4, 4) { name = "White" };
            var wp = new Color[16]; for (int i = 0; i < 16; i++) wp[i] = Color.white; white.SetPixels(wp); white.Apply();
            var fog = new GameObject("FogBase").AddComponent<SpriteRenderer>();
            fog.transform.SetParent(abyss, false);
            fog.sprite = Sprite.Create(white, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            fog.transform.localPosition = new Vector3(0, 8, 0);
            fog.transform.localScale = new Vector3(MapW + 34f, 40f, 1);
            if (fogShader != null && fogShader.isSupported)
            {
                var mat = new Material(fogShader) { name = "Fog" };
                mat.SetTexture("_NoiseTex", GroundMaterials.FogNoiseTex());
                fog.sharedMaterial = mat;
            }
            else { Debug.LogWarning("Fog2D-Shader fehlt"); fog.color = new Color(0.6f, 0.62f, 0.64f); }
            fog.sortingOrder = -32700;

            // Schwaden in der Tiefe (bewegen sich mit der Parallaxe)
            for (int i = 0; i < 55; i++)
            {
                var pos = new Vector3((float)(rnd.NextDouble() * (MapW + 26) - MapW / 2f - 13f), (float)(rnd.NextDouble() * 26f - 4f), 0);
                float sz = 3f + (float)rnd.NextDouble() * 5f;
                float g = 0.68f + (float)rnd.NextDouble() * 0.2f;
                AddPuff(abyss, pos, new Vector3(sz * 1.8f, sz * 0.7f, 1), new Color(g, g + 0.01f, g + 0.02f), 0.25f + (float)rnd.NextDouble() * 0.3f, -32580 + i % 3, false);
            }

            // Dichter Nebelsaum direkt hinter der Kante (bewegt sich mit dem Plateau)
            for (float x = -MapW / 2f - 3f; x <= MapW / 2f + 3f; x += 0.9f)
            {
                var pos = new Vector3(x + (float)rnd.NextDouble() * 0.5f, EdgeY(x) + 0.9f + (float)rnd.NextDouble() * 0.5f, 0);
                float g = 0.78f + (float)rnd.NextDouble() * 0.1f;
                AddPuff(transform, pos, new Vector3(3.0f, 1.5f, 1), new Color(g, g, g + 0.01f), 0.5f, -32450, true);
            }
        }

        void AddPuff(Transform parent, Vector3 pos, Vector3 scale, Color col, float alpha, int order, bool world)
        {
            var sr = new GameObject("Mist").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = MistSprite;
            if (world) sr.transform.position = pos; else sr.transform.localPosition = pos;
            sr.transform.localScale = scale;
            col.a = alpha;
            sr.color = col;
            sr.sortingOrder = order;
            puffs.Add(new Puff
            {
                t = sr.transform, sr = sr, baseScale = scale, baseA = alpha, world = world,
                phase = (float)rnd.NextDouble() * 20f, speed = 0.25f + (float)rnd.NextDouble() * 0.35f,
                drift = world ? 0.04f + (float)rnd.NextDouble() * 0.05f : 0.1f + (float)rnd.NextDouble() * 0.2f,
                bob = 0.15f + (float)rnd.NextDouble() * 0.25f
            });
        }

        /// <summary>Parallaxe (Tiefe) + treibende, atmende Nebelschwaden.</summary>
        void UpdateAbyss(float dt)
        {
            if (abyss == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var d = cam.transform.position - camStart;
            abyss.position = new Vector3(d.x * 0.55f, d.y * 0.55f, 0);
            float time = Time.time;
            foreach (var p in puffs)
            {
                float k = time * p.speed + p.phase;
                // atmen: Groesse und Dichte schwanken, leicht versetzt
                float breathe = Mathf.Sin(k);
                p.t.localScale = new Vector3(p.baseScale.x * (1f + breathe * 0.14f), p.baseScale.y * (1f + Mathf.Sin(k * 1.3f + 1f) * 0.18f), 1);
                var c = p.sr.color; c.a = p.baseA * (0.65f + 0.35f * Mathf.Sin(k * 0.7f + 2f)); p.sr.color = c;
                // treiben + leichtes Auf und Ab
                var pos = p.world ? p.t.position : p.t.localPosition;
                pos.x += p.drift * dt;
                pos.y += Mathf.Cos(k * 0.8f) * p.bob * dt;
                float limit = MapW / 2f + (p.world ? 4f : 14f);
                if (pos.x > limit)
                {
                    pos.x = -limit;
                    if (p.world) pos.y = EdgeY(pos.x) + 0.9f;
                }
                if (p.world) p.t.position = pos; else p.t.localPosition = pos;
            }
        }
    }
}
