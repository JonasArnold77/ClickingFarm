using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Lichtstrahlen ueber dem Dach: Ab einer bestimmten Stufe schimmert ein Gebaeude (Laden und alle Bauwerke ausser
    /// Totems) – weiche Strahlen steigen ueber dem Dach auf (vor dem Gebaeude gerendert) und wabern, auf dem Dach blitzen
    /// Glitzerpunkte auf (je hoeher die Stufe, desto mehr), dazu ab und zu ein Funke. Strahl und Glitzer sind Texturen aus
    /// Cartoon FX (cfxr stretch ray blur, cfxr flare – als weiss mit Alpha in Resources/Buildings/fx_*). Farbe nach Stufe:
    /// Bronze, Silber, Gold (Schwellen in plants.json -> buildingGlow.levels).
    /// </summary>
    public partial class WorldBuilder
    {
        class Glow { public SpriteRenderer[] rays; public Light2D light; public int tier = -1; public float sparkT; public Glint[] glints; }
        /// <summary>Ein Glitzerpunkt: zwei gekreuzte Lichtreflexe, die aufblitzen, sich drehen und verglimmen.</summary>
        class Glint { public Transform root; public SpriteRenderer a, b; public float t, life = 1f; public Vector2 off; }
        const int GlintMax = 6;
        static Sprite flareSprite; static bool flareLooked;
        readonly Dictionary<object, Glow> glows = new Dictionary<object, Glow>();
        const int GlowRays = 7;
        static Sprite raySprite;
        static Material glowMat; static bool glowLooked;
        static Material GlowMat
        {
            get
            {
                if (!glowLooked) { glowLooked = true; var sh = Shader.Find("SeedClicker/GlowAdditive"); if (sh != null) glowMat = new Material(sh); }
                return glowMat;
            }
        }
        static readonly object ShopKey = new object();

        /// <summary>0 = kein Schimmer, 1 Bronze, 2 Silber, 3 Gold.</summary>
        public static int BuildingTier(int lv)
        {
            var l = PlantBalance.I.buildingGlow?.levels;
            if (l == null) return 0;
            int t = 0;
            for (int i = 0; i < l.Length; i++) if (lv >= l[i]) t = i + 1;
            return t;
        }

        void UpdateBuildingGlow(float dt)
        {
            var seen = HashSetPool;
            seen.Clear();
            if (shopSr != null) { GlowFor(ShopKey, shopSr, ShopLv, dt); seen.Add(ShopKey); }
            foreach (var v in structViews)
            {
                if (v.sr == null || !v.st.placed || IsTotem(v.def)) continue;
                GlowFor(v.st, v.sr, v.st.lv, dt);
                seen.Add(v.st);
            }
            // abgerissene oder versetzte Bauwerke: Strahlen weg
            if (glows.Count > seen.Count)
            {
                var gone = new List<object>();
                foreach (var k in glows.Keys) if (!seen.Contains(k)) gone.Add(k);
                foreach (var k in gone)
                {
                    foreach (var r in glows[k].rays) if (r != null) Destroy(r.gameObject);
                    if (glows[k].light != null) Destroy(glows[k].light.gameObject);
                    foreach (var gl in glows[k].glints) if (gl.root != null) Destroy(gl.root.gameObject);
                    glows.Remove(k);
                }
            }
        }

        readonly HashSet<object> HashSetPool = new HashSet<object>();

        void GlowFor(object key, SpriteRenderer host, int lv, float dt)
        {
            int tier = BuildingTier(lv);
            glows.TryGetValue(key, out var g);
            if (tier <= 0)
            {
                if (g != null) { foreach (var r in g.rays) r.enabled = false; if (g.light != null) g.light.enabled = false; foreach (var gl in g.glints) gl.root.gameObject.SetActive(false); }
                return;
            }
            if (g == null)
            {
                g = new Glow { rays = new SpriteRenderer[GlowRays] };
                for (int i = 0; i < GlowRays; i++)
                {
                    var sr = new GameObject("GlowRay").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    sr.sprite = RaySprite;
                    if (GlowMat != null) sr.sharedMaterial = GlowMat;   // additiv: das Licht hellt auf
                    g.rays[i] = sr;
                }
                g.glints = new Glint[GlintMax];
                for (int i = 0; i < GlintMax; i++)
                {
                    var gr = new GameObject("Glint").transform;
                    gr.SetParent(objects, false);
                    var gl = new Glint { root = gr, t = 1f, life = 1f };
                    for (int c = 0; c < 2; c++)
                    {
                        var fs = new GameObject(c == 0 ? "FlareH" : "FlareV").AddComponent<SpriteRenderer>();
                        fs.transform.SetParent(gr, false);
                        fs.sprite = FlareSprite;
                        if (GlowMat != null) fs.sharedMaterial = GlowMat;
                        if (c == 1) fs.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                        if (c == 0) gl.a = fs; else gl.b = fs;
                    }
                    g.glints[i] = gl;
                }
                // weiches Licht in der Stufenfarbe ueber dem Dach
                var lgo = new GameObject("GlowLight");
                lgo.transform.SetParent(objects, false);
                g.light = lgo.AddComponent<Light2D>();
                g.light.lightType = Light2D.LightType.Point;
                glows[key] = g;
            }
            var col = MedalColor(tier + 1);   // 2 Bronze, 3 Silber, 4 Gold
            if (g.tier != tier && g.tier >= 0)
            {
                // neue Glanz-Stufe: kurzer Lichtstoss
                Shockwave(new Vector2(host.bounds.center.x, host.bounds.max.y), 1.6f, 0.5f, new Color(col.r, col.g, col.b, 0.8f));
                Particles.I?.Burst(new Vector2(host.bounds.center.x, host.bounds.max.y), "star", col, 20, 3f, 0.9f, 0.3f, -0.6f, 1.2f);
            }
            g.tier = tier;

            var b = host.bounds;
            float w = b.size.x, h = b.size.y;
            float t = Time.time, seed = (key.GetHashCode() & 255) * 0.13f;
            float strength = 0.75f;                           // additiv – die Farbe zeigt die Stufe
            for (int i = 0; i < GlowRays; i++)
            {
                var sr = g.rays[i];
                sr.enabled = true;
                float k = i / (GlowRays - 1f) - 0.5f;          // -0,5 .. 0,5 ueber die Dachbreite
                float sway = Mathf.Sin(t * 0.8f + i * 1.9f + seed) * w * 0.035f;
                float len = h * (0.95f + 0.2f * tier) * (0.8f + 0.25f * Mathf.Sin(t * 1.3f + i * 1.7f + seed));
                float rw = w * (0.11f + 0.035f * Mathf.Sin(t * 0.9f + i * 2.3f + seed));   // schmale Strahlen
                // Fuss der Strahlen im Dach: sie blenden weich ein und steigen ueber dem Gebaeude auf
                var foot = new Vector2(b.center.x + k * w * 0.7f + sway, b.max.y - h * 0.12f);
                sr.transform.position = foot;
                sr.transform.localScale = new Vector3(rw / RaySprite.bounds.size.x, len / RaySprite.bounds.size.y, 1f);
                sr.transform.rotation = Quaternion.Euler(0, 0, k * -14f + Mathf.Sin(t * 0.6f + i + seed) * 3f);
                float a = strength * (0.6f + 0.4f * Mathf.Sin(t * 1.1f + i * 2.9f + seed));
                var lit = Color.Lerp(col, Color.white, 0.15f);   // Licht: etwas heller als die Medaille, Farbe bleibt erkennbar
                sr.color = new Color(lit.r, lit.g, lit.b, Mathf.Min(1f, a));
                sr.sortingOrder = host.sortingOrder + 3;   // vor dem Gebaeude
            }
            if (g.light != null)
            {
                g.light.enabled = true;
                g.light.color = col;
                g.light.transform.position = new Vector3(b.center.x, b.max.y - h * 0.1f, 0f);
                g.light.pointLightOuterRadius = w * (0.9f + 0.15f * tier);
                g.light.pointLightInnerRadius = w * 0.15f;
                g.light.intensity = (0.35f + 0.2f * tier) * (0.8f + 0.2f * Mathf.Sin(t * 1.6f + seed));
            }
            // Glitzerpunkte auf dem Dach: blitzen auf, drehen sich, verglimmen – dann an anderer Stelle neu
            var glintCol = Color.Lerp(col, Color.white, 0.35f);
            int activeGlints = Mathf.Min(GlintMax, 1 + tier + (tier >= 3 ? 2 : 0));
            for (int i = 0; i < GlintMax; i++)
            {
                var gl = g.glints[i];
                bool on = i < activeGlints && FlareSprite != null;
                if (gl.root.gameObject.activeSelf != on) gl.root.gameObject.SetActive(on);
                if (!on) continue;
                gl.t += dt;
                if (gl.t >= gl.life)
                {
                    gl.life = Random.Range(0.45f, 0.85f);
                    gl.t = -Random.Range(0f, 0.9f);   // kurze Pause bis zum naechsten Aufblitzen
                    gl.off = new Vector2(Random.Range(-0.42f, 0.42f), Random.Range(-0.34f, -0.04f));
                }
                float k = Mathf.Clamp01(gl.t / gl.life);
                float pop = gl.t < 0f ? 0f : Mathf.Sin(k * Mathf.PI);
                float size = w * 0.5f * pop;
                gl.root.position = new Vector3(b.center.x + gl.off.x * w, b.max.y + gl.off.y * h, 0f);
                gl.root.localRotation = Quaternion.Euler(0, 0, 15f + k * 50f + i * 23f);
                float fw = Mathf.Max(0.0001f, FlareSprite.bounds.size.x);
                gl.a.transform.localScale = new Vector3(size / fw, size / fw, 1f);
                gl.b.transform.localScale = new Vector3(size * 0.7f / fw, size * 0.7f / fw, 1f);
                var c2 = new Color(glintCol.r, glintCol.g, glintCol.b, pop);
                gl.a.color = c2; gl.b.color = c2;
                gl.a.sortingOrder = gl.b.sortingOrder = host.sortingOrder + 4;
            }

            // ab und zu ein Funke, der ueber dem Dach aufsteigt
            g.sparkT -= dt;
            if (g.sparkT <= 0f)
            {
                g.sparkT = Random.Range(0.5f, 1.1f) / tier;
                var p = new Vector2(b.center.x + Random.Range(-0.35f, 0.35f) * w, b.max.y - h * 0.05f);
                Particles.I?.Spawn(p, "spark", col, new Vector3(Random.Range(-0.1f, 0.1f), 0.55f, 0), 1.6f, 0.07f, 0.02f, 0f);
            }
        }

        /// <summary>Lichtreflex (cfxr flare): waagerechter Glanzstrich mit hellem Kern.</summary>
        static Sprite FlareSprite
        {
            get
            {
                if (!flareLooked)
                {
                    flareLooked = true;
                    var tex = Resources.Load<Texture2D>("Buildings/fx_flare");
                    if (tex != null) flareSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                }
                return flareSprite;
            }
        }

        /// <summary>Weicher senkrechter Lichtstrahl (cfxr stretch ray blur, breites Ende unten); sonst selbst gemalt.</summary>
        static Sprite RaySprite
        {
            get
            {
                if (raySprite != null) return raySprite;
                var rt = Resources.Load<Texture2D>("Buildings/fx_ray");
                if (rt != null) return raySprite = Sprite.Create(rt, new Rect(0, 0, rt.width, rt.height), new Vector2(0.5f, 0.04f), rt.width);
                const int w = 32, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fx = (x + 0.5f) / w * 2f - 1f, fy = (y + 0.5f) / h;
                        float side = Mathf.Exp(-fx * fx * 4.5f);
                        float up = Mathf.Pow(1f - fy, 1.6f) * Mathf.SmoothStep(0f, 1f, fy * 8f);   // unten weich einblenden, oben auslaufen
                        px[y * w + x] = new Color(1f, 1f, 1f, side * up);
                    }
                tex.SetPixels(px); tex.Apply();
                return raySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
            }
        }
    }
}
