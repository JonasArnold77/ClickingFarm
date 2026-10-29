using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Macht aus einer Pflanze ein Lebewesen: Gesichter auf den "Koepfen" der Pflanze (Maiskolben, Weizenaehren,
    /// Ruebe – Lage in plants.json -> species.heads). Das Gesicht passt zur Angriffsart:
    ///   Samenkorn: runder Schussmund, entschlossene Brauen · Dorn: Zahngrinsen, wuetende Brauen
    ///   Spore: verschlafen, Pustemund · Bluetenblatt: grosse Augen mit Wimpern, Laecheln
    ///   Lichtstrahl: ein grosses, leuchtendes Zyklopenauge
    /// Die Iris hat die Farbe der Wirkung, dazu passende Partikel (Glut, Frost, Gift, Funken, Blaetter).
    /// Die Gesichter blinzeln, schauen zum naechsten Zombie, oeffnen beim Schuss den Mund und kneifen bei Klicks die Augen zu.
    /// Haengt als Kind am Pflanzen-Sprite (dessen Skalierung wird ausgeglichen).
    /// </summary>
    public class PlantCreature : MonoBehaviour
    {
        class Eye { public Transform root, iris; public SpriteRenderer sclera, irisSr, pupil, hi; }
        class Head
        {
            public Transform root, mouth, browL, browR;
            public SpriteRenderer mouthSr, browLSr, browRSr;
            public List<Eye> eyes = new List<Eye>();
            public Vector2 local; public float size;
        }

        SpriteRenderer body;
        PlantGenome g;
        string style;
        readonly List<Head> heads = new List<Head>();
        readonly List<SpriteRenderer> all = new List<SpriteRenderer>();
        readonly HashSet<SpriteRenderer> hidden = new HashSet<SpriteRenderer>();
        float blinkT, blink, shootT, pokeT, wake = 1f, fxT;
        Vector2 look, lookWander;
        float wanderT;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>Gesichter an ein Pflanzen-Sprite haengen.</summary>
        public int Tier { get; private set; } = 1;
        Transform baseRoot; SpriteRenderer aura, bramble;
        readonly List<SpriteRenderer> behind = new List<SpriteRenderer>();

        public static PlantCreature Attach(SpriteRenderer body, PlantGenome g, bool wakeUp, int tier = 1)
        {
            var sp = PlantBalance.I.Species(g.species);
            if (sp == null || sp.heads == null || sp.heads.Length == 0 || body.sprite == null) return null;
            var go = new GameObject("Creature");
            go.transform.SetParent(body.transform, false);
            var c = go.AddComponent<PlantCreature>();
            c.body = body; c.g = g;
            c.style = g.projectile.id;
            c.Tier = Mathf.Clamp(tier, 1, 4);
            foreach (var h in sp.heads) c.BuildHead(h);
            c.BuildMutation();
            c.blinkT = Random.Range(1f, 4f);
            c.wake = wakeUp ? 0f : 1f;
            c.LateUpdate();
            return c;
        }

        void BuildHead(HeadDef h)
        {
            var b = body.sprite.bounds;
            var head = new Head { size = h.s, local = new Vector2(b.min.x + b.size.x * h.x, b.min.y + b.size.y * h.y) };
            head.root = new GameObject("Head").transform;
            head.root.SetParent(transform, false);
            var col = g.ElementColor;
            if (style == "beam")
            {
                head.eyes.Add(MakeEye(head.root, new Vector2(0, 0.05f), 0.62f, col));
            }
            else
            {
                float eyeSize = style == "petal" ? 0.42f : 0.36f;
                head.eyes.Add(MakeEye(head.root, new Vector2(-0.22f, 0.08f), eyeSize, col));
                head.eyes.Add(MakeEye(head.root, new Vector2(0.22f, 0.08f), eyeSize, col));
                string mouth = style == "thorn" ? "grin" : style == "spore" ? "puff" : style == "petal" ? "smile" : "o";
                if (Tier >= 3) mouth = "fangs";                       // ab Mutation 3: Reisszaehne
                else if (Tier == 2 && mouth == "smile") mouth = "o";
                head.mouthSr = Part(head.root, "Mouth", Get(mouth), new Vector2(0, -0.24f), MouthSize(mouth));
                head.mouth = head.mouthSr.transform;
                // Brauen / Wimpern
                var brow = style == "petal" && Tier == 1 ? Get("lash") : Get("brow");
                float bs = style == "petal" ? 0.22f : 0.26f;
                head.browLSr = Part(head.root, "BrowL", brow, new Vector2(-0.22f, 0.36f), bs);
                head.browRSr = Part(head.root, "BrowR", brow, new Vector2(0.22f, 0.36f), bs);
                head.browRSr.flipX = true;
                head.browL = head.browLSr.transform; head.browR = head.browRSr.transform;
                float tilt = style == "thorn" ? 24f : style == "seed" ? 12f : style == "spore" ? -8f : 0f;
                if (Tier >= 2) tilt = Mathf.Max(tilt, 16f + Tier * 6f);   // je Mutation boeser
                head.browL.localRotation = Quaternion.Euler(0, 0, -tilt);
                head.browR.localRotation = Quaternion.Euler(0, 0, tilt);
                if (style == "spore" && Tier == 1) { hidden.Add(head.browLSr); hidden.Add(head.browRSr); }
            }
            // Hoerner ab Mutation 2, groesser und dunkler mit jeder Stufe
            if (Tier >= 2)
            {
                float hs = 0.36f + (Tier - 2) * 0.1f;
                var hornCol = Tier == 2 ? new Color(0.62f, 0.5f, 0.38f) : Tier == 3 ? new Color(0.45f, 0.3f, 0.28f) : new Color(0.22f, 0.1f, 0.14f);
                var hl = Part(head.root, "HornL", Get("horn"), new Vector2(-0.3f, 0.5f), hs); hl.color = hornCol;
                var hr = Part(head.root, "HornR", Get("horn"), new Vector2(0.3f, 0.5f), hs); hr.color = hornCol; hr.flipX = true;
                hl.transform.localRotation = Quaternion.Euler(0, 0, 18f); hr.transform.localRotation = Quaternion.Euler(0, 0, -18f);
                if (Tier >= 4)
                {
                    var hc = Part(head.root, "HornC", Get("horn"), new Vector2(0f, 0.6f), hs * 0.8f); hc.color = hornCol;
                }
                behind.Add(hl); behind.Add(hr);
            }
            heads.Add(head);
        }

        Eye MakeEye(Transform parent, Vector2 pos, float size, Color irisCol)
        {
            var e = new Eye { root = new GameObject("Eye").transform };
            e.root.SetParent(parent, false);
            e.root.localPosition = pos;
            e.root.localScale = Vector3.one * size;
            e.sclera = Part(e.root, "Sclera", Get("sclera"), Vector2.zero, 1f);
            e.iris = new GameObject("Iris").transform; e.iris.SetParent(e.root, false);
            e.irisSr = Part(e.iris, "IrisRing", Get("iris"), Vector2.zero, 0.62f);
            bool beam = style == "beam";
            // Zyklopenauge leuchtet (HDR -> Bloom)
            e.irisSr.color = beam ? new Color(irisCol.r * 2.2f, irisCol.g * 2.2f, irisCol.b * 2.2f) : Color.Lerp(irisCol, Color.white, 0.1f);
            if (Tier >= 3)
            {
                // gluehende Augen (HDR -> Bloom), ab Mutation 4 mit dunkler Lederhaut
                float glow = Tier == 3 ? 2f : 3f;
                var hot = Color.Lerp(irisCol, new Color(1f, 0.25f, 0.15f), Tier == 4 ? 0.45f : 0.2f);
                e.irisSr.color = new Color(hot.r * glow, hot.g * glow, hot.b * glow);
                if (Tier >= 4) e.sclera.color = new Color(0.25f, 0.15f, 0.18f);
            }
            e.pupil = Part(e.iris, "Pupil", Get("pupil"), Vector2.zero, beam ? 0.22f : 0.34f);
            e.hi = Part(e.iris, "Hi", Get("hi"), new Vector2(0.1f, 0.12f), 0.16f);
            return e;
        }

        SpriteRenderer Part(Transform parent, string name, Sprite s, Vector2 pos, float scale)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = s;
            all.Add(sr);
            return sr;
        }

        /// <summary>Schuss: Mund auf, Augen weit.</summary>
        public void Shoot() { shootT = 0.28f; }
        /// <summary>Klick auf die Pflanze: Augen zukneifen.</summary>
        public void Poke() { pokeT = 0.14f; }
        /// <summary>Wacht auf (Reif-Moment): Augen springen auf.</summary>
        public void Wake() { wake = 0f; }

        void LateUpdate()
        {
            if (body == null) { Destroy(gameObject); return; }
            bool vis = body.enabled;   // versteckte Pflanze (z. B. im Flug) -> Gesicht auch weg
            foreach (var sr in all) if (sr != null) sr.enabled = vis && !hidden.Contains(sr);
            if (!vis) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Skalierung des Sprites ausgleichen, damit die Gesichter rund bleiben (das Stauchen des Koerpers bleibt)
            var ps = body.transform.localScale;
            float bw = body.sprite != null ? body.sprite.bounds.size.x * Mathf.Abs(ps.x) : 0.5f;
            transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), 1f);
            int order = body.sortingOrder;
            foreach (var sr in all) if (sr != null) sr.sortingOrder = order + 1;
            foreach (var h in heads)
            {
                foreach (var e in h.eyes) { e.irisSr.sortingOrder = order + 2; e.pupil.sortingOrder = order + 3; e.hi.sortingOrder = order + 4; }
                if (h.browLSr != null) { h.browLSr.sortingOrder = h.browRSr.sortingOrder = order + 2; }
            }

            // Ausdruck
            wake = Mathf.MoveTowards(wake, 1f, dt * 2.5f);
            shootT = Mathf.Max(0f, shootT - dt);
            pokeT = Mathf.Max(0f, pokeT - dt);
            blinkT -= dt;
            if (blinkT <= 0f) { blink = 0.12f; blinkT = Random.Range(2f, 5f); }
            blink = Mathf.Max(0f, blink - dt);
            float open = 1f;
            if (blink > 0f) open = 0.12f;
            if (pokeT > 0f) open = 0.08f;
            if (style == "spore" && Tier == 1) open *= 0.62f;           // verschlafen
            if (shootT > 0f) open = Mathf.Max(open, 1.15f);
            float wakeE = 1 + 2.70158f * Mathf.Pow(wake - 1, 3) + 1.70158f * Mathf.Pow(wake - 1, 2);

            // Blick: naechster Zombie, sonst langsam umherschauen
            Vector2 world = body.transform.position;
            Vector2 target;
            var nw = NightWave.I;
            if (nw != null && nw.Active && nw.NearestMonsterPos(world, 9f, out var mpos)) target = (mpos - world).normalized;
            else
            {
                wanderT -= dt;
                if (wanderT <= 0f) { wanderT = Random.Range(1.2f, 3f); lookWander = Random.insideUnitCircle * 0.8f + Vector2.down * 0.3f; }
                target = lookWander;
            }
            look = Vector2.Lerp(look, Vector2.ClampMagnitude(target, 1f), dt * 6f);

            foreach (var h in heads)
            {
                float hs = h.size * bw;   // Kopfbreite in Welteinheiten
                h.root.localPosition = new Vector3(h.local.x * Mathf.Abs(ps.x), h.local.y * Mathf.Abs(ps.y), 0);
                h.root.localScale = Vector3.one * hs * wakeE;
                foreach (var e in h.eyes)
                {
                    e.root.localScale = new Vector3(e.root.localScale.x, Mathf.MoveTowards(e.root.localScale.y, e.root.localScale.x * open, dt * 12f), 1);
                    e.iris.localPosition = look * 0.18f;
                }
                if (h.mouth != null)
                {
                    float m = shootT > 0f ? 1.6f : pokeT > 0f ? 1.3f : 1f;
                    float baseS = MouthSize(h.mouthSr.sprite == Get("fangs") ? "fangs" : h.mouthSr.sprite == Get("grin") ? "grin" : h.mouthSr.sprite == Get("smile") ? "smile" : "o");
                    h.mouth.localScale = new Vector3(baseS * (shootT > 0f ? 1.2f : 1f), baseS * m, 1);
                }
            }

            // Mutation: Ranken am Fuss, Aura pulsiert
            if (baseRoot != null)
            {
                var b = body.sprite.bounds;
                baseRoot.localPosition = new Vector3(b.center.x * ps.x, b.min.y * Mathf.Abs(ps.y), 0);
                baseRoot.localScale = Vector3.one * bw;
                if (bramble != null) bramble.sortingOrder = order + 1;
                if (aura != null)
                {
                    aura.sortingOrder = order - 1;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                    var ac = g.ElementColor;
                    aura.color = new Color(ac.r * 1.6f, ac.g * 1.6f, ac.b * 1.6f, 0.35f + pulse * 0.25f);
                    aura.transform.localScale = new Vector3(1.6f + pulse * 0.15f, 2.4f + pulse * 0.25f, 1);
                    aura.transform.localPosition = new Vector3(0, b.size.y * Mathf.Abs(ps.y) / bw * 0.5f, 0);
                }
            }
            foreach (var sr in behind) if (sr != null) sr.sortingOrder = order + 1;

            // Wirkung als kleine Partikel am Kopf (je Mutation haeufiger)
            fxT -= dt * Tier;
            if (fxT <= 0f && heads.Count > 0 && Particles.I != null)
            {
                fxT = Random.Range(0.35f, 0.9f);
                var h = heads[Random.Range(0, heads.Count)];
                Vector2 at = h.root.position + new Vector3(0, h.size * bw * 0.4f, 0);
                var col = g.ElementColor;
                switch (g.element.id)
                {
                    case "fire": Particles.I.Spawn(at, "spark", new Color(1f, 0.55f, 0.15f), new Vector3(Random.Range(-0.1f, 0.1f), 0.6f, 0), 0.6f, 0.06f, 0f, -0.2f); break;
                    case "ice": Particles.I.Spawn(at, "spark", new Color(0.75f, 0.95f, 1f), new Vector3(Random.Range(-0.15f, 0.15f), 0.15f, 0), 0.8f, 0.07f, 0f, 0f); break;
                    case "poison": Particles.I.Spawn((Vector2)h.root.position + new Vector2(0, -h.size * bw * 0.3f), "square", new Color(0.65f, 0.35f, 0.85f), new Vector3(0, -0.1f, 0), 0.9f, 0.04f, 0.02f, 2f); break;
                    case "lightning": Particles.I.Spawn(at + Random.insideUnitCircle * 0.08f, "spark", new Color(1f, 0.95f, 0.4f), Vector3.zero, 0.12f, 0.08f, 0f, 0f); break;
                    default: if (Random.value < 0.4f) Particles.I.Spawn(at, "leaf", new Color(0.55f, 0.85f, 0.35f), new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, 0), 1.2f, 0.06f, 0.04f, 0.5f, 120f); break;
                }
            }
        }

        static float MouthSize(string m) => m == "grin" ? 0.42f : m == "fangs" ? 0.46f : m == "smile" ? 0.32f : 0.2f;

        /// <summary>Mutationsteile am Fuss: Dornenranken (ab 2) und lodernde Aura (ab 4).</summary>
        void BuildMutation()
        {
            if (Tier < 2) return;
            baseRoot = new GameObject("Mutation").transform;
            baseRoot.SetParent(transform, false);
            bramble = Part(baseRoot, "Bramble", Get("bramble"), new Vector2(0, 0.12f), Tier == 2 ? 1.0f : Tier == 3 ? 1.15f : 1.35f);
            bramble.color = Tier == 2 ? new Color(0.3f, 0.36f, 0.16f) : Tier == 3 ? new Color(0.32f, 0.2f, 0.2f) : new Color(0.24f, 0.1f, 0.16f);
            if (Tier >= 4)
            {
                aura = Part(baseRoot, "Aura", WorldBuilder.Mist, Vector2.zero, 1f);
            }
        }

        // ------------------------------------------------------------------ Gemalte Gesichtsteile

        static Sprite Get(string key)
        {
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            int n = key == "brow" || key == "lash" ? 64 : key == "bramble" ? 192 : key == "horn" ? 48 : 96;
            int h = key == "brow" || key == "lash" ? 24 : key == "grin" || key == "smile" || key == "fangs" ? 56 : key == "bramble" ? 96 : 96;
            var tex = new Texture2D(n, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * h];
            var ink = new Color(0.16f, 0.1f, 0.08f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;   // -1..1
                    Color c = Color.clear;
                    switch (key)
                    {
                        case "horn":
                            {
                                // gebogenes Horn: Spitze oben, nach aussen gekruemmt
                                float t = (v + 1f) * 0.5f;                         // 0 unten .. 1 oben
                                float cx = -0.15f + t * t * 0.55f;                  // Kruemmung
                                float half = 0.55f * (1f - t);
                                if (Mathf.Abs(u - cx) < half && t < 0.97f)
                                {
                                    c = Color.Lerp(Color.white, new Color(0.75f, 0.75f, 0.75f), Mathf.Clamp01((u - cx) / half * 0.5f + 0.5f));
                                    if (Mathf.Repeat(t * 6f, 1f) < 0.12f && t < 0.8f) c *= 0.8f;   // Rillen
                                    c.a = Mathf.Clamp01((half - Mathf.Abs(u - cx)) * 12f);
                                }
                                break;
                            }
                        case "fangs":
                            {
                                // aufgerissenes Maul mit langen Reisszaehnen oben und unten
                                float top = 0.6f, bottom = 0.6f - 1.45f * (1f - u * u);
                                if (v < top && v > bottom)
                                {
                                    c = new Color(0.2f, 0.02f, 0.04f);
                                    float tooth = Mathf.Abs(Mathf.Repeat(u * 3f + 0.5f, 1f) - 0.5f) * 2f;
                                    if (v > top - 0.75f * (1f - tooth) * (1f - tooth)) c = new Color(1f, 0.97f, 0.88f);
                                    if (v < bottom + 0.55f * (1f - tooth) * (1f - tooth) && u * u < 0.6f) c = new Color(1f, 0.97f, 0.88f);
                                    if (top - v < 0.08f || v - bottom < 0.08f) c = ink;
                                    c.a = 1f;
                                }
                                break;
                            }
                        case "bramble":
                            {
                                // Dornenranken: mehrere geschwungene Ranken mit Dornen nach aussen
                                float best = 9f;
                                for (int k = 0; k < 5; k++)
                                {
                                    float ph = k * 1.3f, amp = 0.25f + k * 0.04f;
                                    float vy = -0.55f + amp * Mathf.Sin(u * (3f + k * 0.7f) + ph) + (k % 2) * 0.25f;
                                    best = Mathf.Min(best, Mathf.Abs(v - vy) * (1f + Mathf.Abs(u) * 0.6f));
                                    // Dornen
                                    float sx = Mathf.Repeat(u * (5f + k) + ph, 1f);
                                    if (sx < 0.3f)
                                    {
                                        float tipH = 0.32f * (1f - sx / 0.3f);
                                        if (v > vy && v < vy + tipH && Mathf.Abs(u) < 0.95f) best = 0f;
                                    }
                                }
                                float edge = 1f - Mathf.Clamp01(Mathf.Abs(u) - 0.85f) * 6f;
                                if (best < 0.07f && edge > 0f)
                                {
                                    c = Color.Lerp(Color.white, new Color(0.65f, 0.65f, 0.65f), Mathf.Clamp01(best * 10f));
                                    c.a = Mathf.Clamp01((0.07f - best) * 40f) * edge;
                                }
                                break;
                            }
                        case "sclera":
                            {
                                float d = Mathf.Sqrt(u * u + v * v);
                                if (d < 0.97f)
                                {
                                    c = Color.Lerp(new Color(0.82f, 0.84f, 0.88f), Color.white, Mathf.Clamp01(v * 0.6f + 0.7f));
                                    if (d > 0.84f) c = Color.Lerp(c, ink, (d - 0.84f) / 0.13f);
                                    c.a = Mathf.Clamp01((0.97f - d) * 30f);
                                }
                                break;
                            }
                        case "iris":
                            {
                                float d = Mathf.Sqrt(u * u + v * v);
                                if (d < 0.97f) { float k = Mathf.Lerp(1.15f, 0.65f, d); c = new Color(k, k, k, Mathf.Clamp01((0.97f - d) * 30f)); }
                                break;
                            }
                        case "pupil":
                            {
                                float d = Mathf.Sqrt(u * u + v * v);
                                if (d < 0.97f) c = new Color(0.05f, 0.04f, 0.05f, Mathf.Clamp01((0.97f - d) * 30f));
                                break;
                            }
                        case "hi":
                            {
                                float d = Mathf.Sqrt(u * u + v * v);
                                if (d < 0.97f) c = new Color(1f, 1f, 1f, Mathf.Clamp01((0.97f - d) * 20f));
                                break;
                            }
                        case "o":
                            {
                                float d = Mathf.Sqrt(u * u + v * v);
                                if (d < 0.97f)
                                {
                                    c = d > 0.72f ? new Color(0.45f, 0.18f, 0.12f) : new Color(0.12f, 0.04f, 0.04f);
                                    if (v < -0.2f && d < 0.6f) c = new Color(0.75f, 0.3f, 0.3f);   // Zunge
                                    c.a = Mathf.Clamp01((0.97f - d) * 30f);
                                }
                                break;
                            }
                        case "puff":
                            {
                                float d = Mathf.Sqrt(u * u * 1.4f + v * v);
                                if (d < 0.9f) c = new Color(0.3f, 0.12f, 0.1f, Mathf.Clamp01((0.9f - d) * 25f));
                                break;
                            }
                        case "grin":
                            {
                                // breiter Halbmond mit Zackenzaehnen
                                float top = 0.55f, bottom = 0.55f - 1.3f * (1f - u * u);
                                if (v < top && v > bottom)
                                {
                                    c = new Color(0.12f, 0.04f, 0.04f);
                                    float tooth = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f) * 2f;   // Zacken
                                    if (v > top - 0.45f * (1f - tooth)) c = new Color(0.98f, 0.96f, 0.88f);
                                    if (v < bottom + 0.4f * (1f - tooth) && u * u < 0.7f) c = new Color(0.98f, 0.96f, 0.88f);
                                    if (top - v < 0.08f || v - bottom < 0.08f) c = ink;
                                    c.a = 1f;
                                }
                                break;
                            }
                        case "smile":
                            {
                                float curve = -0.1f - 0.75f * (1f - u * u);
                                if (Mathf.Abs(v - curve - 0.35f) < 0.22f && Mathf.Abs(u) < 0.92f) c = ink;
                                break;
                            }
                        case "brow":
                            {
                                float thick = 0.55f * (1f - Mathf.Abs(u) * 0.35f);
                                float arc = 0.15f * (1f - u * u);
                                if (Mathf.Abs(v - arc) < thick && Mathf.Abs(u) < 0.95f) c = ink;
                                break;
                            }
                        case "lash":
                            {
                                // drei kleine Wimpern
                                for (int i = 0; i < 3; i++)
                                {
                                    float cx = -0.5f + i * 0.5f;
                                    if (Mathf.Abs((u - cx) - (v + 1f) * 0.25f * (i - 1)) < 0.12f && v > -0.9f) c = ink;
                                }
                                break;
                            }
                    }
                    px[y * n + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, h), new Vector2(0.5f, 0.5f), n);
            sprites[key] = s;
            return s;
        }
    }
}
