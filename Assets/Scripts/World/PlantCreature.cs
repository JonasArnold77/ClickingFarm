using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        bool single;   // Weizen: eine breite Aehre mit Zweigen
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
            c.single = PlantForms.IsSingle(sp);
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
            // Hoerner ab Mutation 2, groesser und dunkler mit jeder Stufe (nicht beim Feuerweizen, der hat eigene Mutationen)
            if (Tier >= 2 && !single)
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
            UpdateElement(dt, bw, order);

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

        // ------------------------------------------------------------------ Element am Koerper (ab Mutation 2)

        readonly List<SpriteRenderer> elemParts = new List<SpriteRenderer>();
        Light2D elemLight;
        float elemT, boltT;

        /// <summary>Feuer brennt, Eis vereist, Gift dampft und tropft, Blitz knistert, Natur wuchert –
        /// ab Mutation 2, mit jeder Mutation staerker.</summary>
        void BuildElement()
        {
            if (Tier < 2) return;
            string el = g.element.id;
            var col = g.ElementColor;
            foreach (var h in heads)
            {
                if (el == "fire")
                {
                    // lodernde Flamme hinter dem Kopf (HDR -> Bloom)
                    var f = Part(h.root, "Flame", Get("flame"), new Vector2(0, 0.35f), 1.1f + Tier * 0.25f);
                    f.color = new Color(2.2f, 0.9f, 0.25f, 0.95f);
                    elemParts.Add(f);
                }
                else if (el == "ice")
                {
                    // Eiskristalle wachsen aus dem Kopf
                    for (int i = 0; i < 1 + Tier; i++)
                    {
                        float a = -50f + i * (100f / Mathf.Max(1, Tier));
                        var sh = Part(h.root, "Ice", Get("shard"), Quaternion.Euler(0, 0, a) * new Vector2(0, 0.55f), 0.32f + Tier * 0.06f);
                        sh.transform.localRotation = Quaternion.Euler(0, 0, a);
                        sh.color = new Color(0.8f, 0.95f, 1.25f, 0.9f);
                        elemParts.Add(sh);
                    }
                }
                else if (el == "lightning")
                {
                    var b = Part(h.root, "Bolt", Get("bolt"), new Vector2(0, 0.2f), 0.9f + Tier * 0.15f);
                    b.color = new Color(2f, 1.9f, 0.7f, 1f);
                    elemParts.Add(b);
                }
                else if (el == "poison")
                {
                    // Giftblasen auf dem Kopf
                    var bub = Part(h.root, "Bubbles", Get("bubbles"), new Vector2(0, 0.45f), 0.6f + Tier * 0.12f);
                    bub.color = new Color(0.75f, 0.4f, 1f, 0.9f);
                    elemParts.Add(bub);
                }
                else
                {
                    // Natur: Blaetterkranz
                    var w = Part(h.root, "Leaves", Get("leaves"), new Vector2(0, 0.3f), 1f + Tier * 0.15f);
                    w.color = new Color(0.55f, 0.95f, 0.4f, 1f);
                    elemParts.Add(w);
                }
            }
            foreach (var p in elemParts) behind.Remove(p);
            if (el == "fire" || el == "lightning" || el == "ice")
            {
                var lg = new GameObject("ElementLight");
                lg.transform.SetParent(transform, false);
                elemLight = lg.AddComponent<Light2D>();
                elemLight.lightType = Light2D.LightType.Point;
                elemLight.color = el == "fire" ? new Color(1f, 0.55f, 0.2f) : el == "ice" ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.95f, 0.5f);
                elemLight.pointLightOuterRadius = 0.8f + Tier * 0.35f;
                elemLight.pointLightInnerRadius = 0.05f;
                elemLight.intensity = 0f;
            }
        }

        void UpdateElement(float dt, float bw, int order)
        {
            if (single) { UpdateWheat(dt, bw, order); return; }
            if (Tier < 2 || heads.Count == 0) return;
            if (elemParts.Count == 0 && elemLight == null) BuildElement();
            string el = g.element.id;
            float t = Time.time;
            elemT -= dt;
            boltT -= dt;
            bool flash = false;
            if (el == "lightning" && boltT <= 0f) { boltT = Random.Range(0.08f, 0.6f / Tier); flash = true; }
            for (int i = 0; i < elemParts.Count; i++)
            {
                var p = elemParts[i];
                if (p == null) continue;
                // Flammen/Blaetter hinter dem Gesicht, Eis/Blitz/Blasen davor
                p.sortingOrder = el == "fire" || el == "nature" ? order - 1 : order + 5;
                if (el == "fire")
                {
                    float fl = 1f + Mathf.Sin(t * 17f + i * 2.1f) * 0.08f + Mathf.Sin(t * 29f + i) * 0.05f;
                    p.transform.localScale = new Vector3((1.1f + Tier * 0.25f) * (2f - fl), (1.1f + Tier * 0.25f) * fl, 1);
                    p.flipX = Mathf.Sin(t * 6f + i) > 0f;
                }
                else if (el == "lightning")
                {
                    if (flash) { p.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-40f, 40f)); p.flipX = Random.value < 0.5f; }
                    var c = p.color; c.a = boltT > 0.05f ? Mathf.Clamp01(Random.value * 1.5f) : 1f; p.color = c;
                }
                else if (el == "poison")
                {
                    p.transform.localScale = Vector3.one * (0.6f + Tier * 0.12f) * (1f + Mathf.Sin(t * 3f + i * 1.7f) * 0.08f);
                }
                else if (el == "nature")
                {
                    p.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f + i) * 8f);
                }
                else if (el == "ice")
                {
                    var c = p.color; c.a = 0.75f + 0.2f * Mathf.Sin(t * 2f + i); p.color = c;
                }
            }
            if (elemLight != null)
            {
                elemLight.transform.localPosition = heads[0].root.localPosition;
                float nightK = 0.5f + (NightWave.I != null ? NightWave.I.NightAmount : 0f);
                elemLight.intensity = el == "fire" ? (0.7f + Mathf.PerlinNoise(t * 8f, 0f) * 0.6f) * nightK * (Tier - 1) * 0.6f
                                     : el == "lightning" ? (flash ? 2.5f : 0.15f) * nightK
                                     : 0.4f * nightK;
            }
            // Partikel je Element, mit der Mutation haeufiger
            if (elemT > 0f || Particles.I == null) return;
            elemT = Random.Range(0.06f, 0.16f) / (Tier - 1);
            var h = heads[Random.Range(0, heads.Count)];
            float hs = h.size * bw;
            Vector2 top = h.root.position + new Vector3(Random.Range(-0.3f, 0.3f) * hs, hs * 0.5f, 0);
            Vector2 bottom = (Vector2)body.bounds.center + new Vector2(Random.Range(-0.4f, 0.4f) * body.bounds.extents.x, -body.bounds.extents.y * 0.6f);
            switch (el)
            {
                case "fire":
                    Particles.I.Spawn(top, "spark", Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.08f), Random.value), new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.8f, 1.4f), 0), 0.6f, 0.08f + Tier * 0.02f, 0f, -0.3f);
                    if (Random.value < 0.3f) Particles.I.Spawn(top + new Vector2(0, 0.2f), "square", new Color(0.3f, 0.28f, 0.28f, 0.5f), new Vector3(Random.Range(-0.1f, 0.1f), 0.5f, 0), 1.2f, 0.05f, 0.14f, -0.1f);
                    break;
                case "ice":
                    Particles.I.Spawn(top + Random.insideUnitCircle * hs * 0.4f, "spark", new Color(0.85f, 0.97f, 1f), new Vector3(Random.Range(-0.1f, 0.1f), -0.15f, 0), 1.2f, 0.05f + Tier * 0.01f, 0f, 0.1f);
                    if (Random.value < 0.4f) Particles.I.Spawn(bottom, "square", new Color(0.8f, 0.92f, 1f, 0.35f), new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, 0), 1.5f, 0.08f, 0.22f, 0f);
                    break;
                case "poison":
                    Particles.I.Spawn((Vector2)h.root.position + new Vector2(Random.Range(-0.3f, 0.3f) * hs, -hs * 0.35f), "square", new Color(0.6f, 0.95f, 0.3f), new Vector3(0, -0.2f, 0), 0.9f, 0.05f, 0.03f, 3f);
                    if (Random.value < 0.5f) Particles.I.Spawn(top, "square", new Color(0.65f, 0.3f, 0.85f, 0.45f), new Vector3(Random.Range(-0.2f, 0.2f), 0.35f, 0), 1.4f, 0.06f, 0.2f, -0.05f);
                    break;
                case "lightning":
                    Particles.I.Spawn(top + Random.insideUnitCircle * hs * 0.6f, "spark", new Color(1f, 0.95f, 0.5f), Random.insideUnitCircle * 1.5f, 0.15f, 0.07f + Tier * 0.015f, 0f, 0f);
                    break;
                default:
                    Particles.I.Spawn(top, "leaf", new Color(0.5f, 0.9f, 0.35f), new Vector3(Random.Range(-0.4f, 0.4f), 0.4f, 0), 1.4f, 0.07f, 0.05f, 0.4f, 200f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Feuerweizen
        // Grundform: eine breite Aehre mit Gesicht, zwei Weizenzweige wachsen seitlich heraus.
        // Mutation 1 "Glutkranz": Flammen kreisen um ihn, er waechst.
        // Mutation 2 "Fackelaehre": vier Zweige, deren Spitzen brennen; die Aehre glueht von innen.
        // Mutation 3 "Phoenixweizen": Feuerfluegel schlagen, Flammenkrone, Feuersaeule hinter ihm.

        class Branch { public SpriteRenderer sr, tip; public float ang, h, scale, side; }
        readonly List<Branch> branches = new List<Branch>();
        readonly List<SpriteRenderer> orbit = new List<SpriteRenderer>(), wings = new List<SpriteRenderer>(), crown = new List<SpriteRenderer>();
        Transform wheatRoot;
        SpriteRenderer pillar;
        Color baseTint;

        static readonly Color FlameHdr = new Color(2.3f, 0.95f, 0.28f, 0.95f);

        void BuildWheat()
        {
            var sp = PlantBalance.I.Species(g.species);
            var full = WorldArtSet.I != null ? WorldArtSet.I.Get(sp.art + sp.stages[sp.stages.Length - 1]) : null;
            baseTint = PlantBalance.MutationTint(g, Tier);
            wheatRoot = new GameObject("Wheat").transform;
            wheatRoot.SetParent(transform, false);
            int nb = Tier >= 3 ? 4 : 2;
            for (int i = 0; i < nb; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool upper = i >= 2;
                var br = new Branch
                {
                    side = side, ang = upper ? 52f : 30f, h = upper ? 0.5f : 0.28f, scale = upper ? 0.62f : 0.85f,
                    sr = Part(wheatRoot, "Branch", PlantForms.Third(full, side < 0 ? 0 : 2), Vector2.zero, 1f)
                };
                br.sr.color = baseTint;
                if (Tier >= 3) { br.tip = Part(wheatRoot, "TipFlame", Get("flame"), Vector2.zero, 0.2f); br.tip.color = FlameHdr; }
                branches.Add(br);
            }
            if (Tier >= 2)
            {
                int n = Tier == 2 ? 6 : Tier == 3 ? 8 : 11;
                for (int i = 0; i < n; i++) { var f = Part(wheatRoot, "Orbit", Get("flame"), Vector2.zero, 0.2f); f.color = FlameHdr; orbit.Add(f); }
                var lg = new GameObject("ElementLight");
                lg.transform.SetParent(transform, false);
                elemLight = lg.AddComponent<Light2D>();
                elemLight.lightType = Light2D.LightType.Point;
                elemLight.color = new Color(1f, 0.55f, 0.2f);
                elemLight.pointLightOuterRadius = 1f + Tier * 0.45f;
                elemLight.pointLightInnerRadius = 0.05f;
            }
            if (Tier >= 4)
            {
                for (int i = 0; i < 2; i++) { var w = Part(wheatRoot, "Wing", Get("flame"), Vector2.zero, 1f); w.color = new Color(2.2f, 0.75f, 0.22f, 0.85f); wings.Add(w); }
                pillar = Part(wheatRoot, "Pillar", Get("flame"), Vector2.zero, 1f);
                pillar.color = new Color(2f, 0.6f, 0.2f, 0.5f);
                if (heads.Count > 0)
                    for (int i = 0; i < 3; i++)
                    {
                        var c = Part(heads[0].root, "Crown", Get("flame"), new Vector2((i - 1) * 0.26f, 0.62f + (i == 1 ? 0.08f : 0f)), i == 1 ? 0.5f : 0.38f);
                        c.color = new Color(2.4f, 1.1f, 0.3f, 1f);
                        crown.Add(c);
                    }
            }
        }

        void UpdateWheat(float dt, float bw, int order)
        {
            if (wheatRoot == null || body.sprite == null) return;
            float t = Time.time;
            var sp = PlantBalance.I.Species(g.species);
            var ps = body.transform.localScale;
            var b = body.sprite.bounds;
            float bodyH = b.size.y * Mathf.Abs(ps.y);
            float s = Mathf.Abs(ps.x) / PlantForms.Widen(sp);   // Skalierung ohne Verbreiterung
            Vector2 root = new Vector2(b.center.x * ps.x, b.min.y * Mathf.Abs(ps.y));
            wheatRoot.localPosition = Vector3.zero;

            // Zweige: wachsen seitlich aus der Aehre, wiegen sich leicht
            foreach (var br in branches)
            {
                if (br.sr == null || br.sr.sprite == null) continue;
                var p = root + new Vector2(br.side * bw * 0.1f, bodyH * br.h);
                float rot = -br.side * (br.ang + Mathf.Sin(t * 1.4f + br.h * 5f) * 4f);
                br.sr.transform.localPosition = p;
                br.sr.transform.localRotation = Quaternion.Euler(0, 0, rot);
                br.sr.transform.localScale = new Vector3(s * br.scale, Mathf.Abs(ps.y) * br.scale, 1);
                br.sr.sortingOrder = order - 1;
                if (br.tip != null)
                {
                    float len = br.sr.sprite.bounds.size.y * Mathf.Abs(ps.y) * br.scale;
                    br.tip.transform.localPosition = p + (Vector2)(Quaternion.Euler(0, 0, rot) * Vector2.up) * len * 0.92f;
                    float fl = 1f + Mathf.Sin(t * 19f + br.h * 9f) * 0.12f;
                    br.tip.transform.localScale = new Vector3(bw * 0.32f * (2f - fl), bw * 0.42f * fl, 1);
                    br.tip.sortingOrder = order + 1;
                }
            }

            // Flammenkranz um die Aehre (vorne vor, hinten hinter der Pflanze)
            Vector2 c = root + new Vector2(0, bodyH * 0.45f);
            float rx = bw * 0.85f + 0.08f, ry = bodyH * 0.22f;
            for (int i = 0; i < orbit.Count; i++)
            {
                var f = orbit[i];
                float a = t * (1.2f + Tier * 0.25f) + i * Mathf.PI * 2f / orbit.Count;
                float sn = Mathf.Sin(a);
                f.transform.localPosition = c + new Vector2(Mathf.Cos(a) * rx, sn * ry + Mathf.Sin(t * 3f + i) * bodyH * 0.05f);
                float fl = 1f + Mathf.Sin(t * 17f + i * 2.3f) * 0.12f;
                float sz = bw * (0.22f + Tier * 0.05f) * (0.85f + 0.15f * -sn);
                f.transform.localScale = new Vector3(sz * (2f - fl), sz * 1.3f * fl, 1);
                f.sortingOrder = sn > 0f ? order - 2 : order + 6;
            }

            // Phoenix: Fluegel, Saeule
            for (int i = 0; i < wings.Count; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var w = wings[i];
                // Fluegel: schraeg nach oben aussen, schlagen langsam
                float flap = Mathf.Sin(t * 3.2f) * 10f;
                w.transform.localRotation = Quaternion.Euler(0, 0, -side * (38f + flap));
                float fl = 1f + Mathf.Sin(t * 13f + i) * 0.06f;
                w.transform.localScale = new Vector3(bodyH * 0.75f, bodyH * 1.1f * fl, 1);
                w.transform.localPosition = root + new Vector2(side * bw * 0.3f, bodyH * 0.55f) + (Vector2)(Quaternion.Euler(0, 0, -side * (38f + flap)) * Vector2.up) * bodyH * 0.35f;
                w.sortingOrder = order - 3;
            }
            if (pillar != null)
            {
                pillar.transform.localPosition = root + new Vector2(0, bodyH * 0.75f);
                float fl = 1f + Mathf.Sin(t * 9f) * 0.05f + Mathf.Sin(t * 23f) * 0.03f;
                pillar.transform.localScale = new Vector3(bw * 1.5f * (2f - fl), bodyH * 1.7f * fl, 1);
                pillar.sortingOrder = order - 4;
            }
            foreach (var cr in crown)
            {
                float fl = 1f + Mathf.Sin(t * 21f + cr.transform.localPosition.x * 9f) * 0.12f;
                float baseS = cr.name == "Crown" && Mathf.Abs(cr.transform.localPosition.x) < 0.01f ? 0.5f : 0.38f;
                cr.transform.localScale = new Vector3(baseS * (2f - fl), baseS * 1.25f * fl, 1);
                cr.sortingOrder = order - 1;
            }

            // Aehre glueht von innen (ab Mutation 2), pulsiert
            if (Tier >= 3)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.5f);
                body.color = Color.Lerp(baseTint, new Color(1.5f, 0.85f, 0.4f), pulse * (Tier == 3 ? 0.35f : 0.55f));
            }
            if (elemLight != null)
            {
                elemLight.transform.localPosition = c;
                float nightK = 0.5f + (NightWave.I != null ? NightWave.I.NightAmount : 0f);
                elemLight.intensity = (0.6f + Mathf.PerlinNoise(t * 8f, 0f) * 0.5f) * nightK * (Tier - 1) * 0.55f;
            }

            // Glut und Funken
            elemT -= dt;
            if (Tier < 2 || elemT > 0f || Particles.I == null) return;
            elemT = Random.Range(0.05f, 0.12f) / (Tier - 1);
            Vector2 at = (Vector2)transform.TransformPoint(c) + new Vector2(Random.Range(-rx, rx), Random.Range(-ry, ry));
            Particles.I.Spawn(at, "spark", Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.08f), Random.value),
                new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.6f, 1.3f), 0), 0.7f, 0.06f + Tier * 0.015f, 0f, -0.3f);
        }

        static float MouthSize(string m) => m == "grin" ? 0.42f : m == "fangs" ? 0.46f : m == "smile" ? 0.32f : 0.2f;

        /// <summary>Mutationsteile am Fuss: Dornenranken (ab 2) und lodernde Aura (ab 4).</summary>
        void BuildMutation()
        {
            if (single) { BuildWheat(); return; }
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
            int n = key == "brow" || key == "lash" ? 64 : key == "bramble" ? 192 : key == "horn" || key == "shard" ? 48 : 96;
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
                        case "flame":
                            {
                                // Flammenzunge: unten breit, oben spitz, wellig, heller Kern
                                float t = (v + 1f) * 0.5f;
                                float wob = Mathf.Sin(t * 9f) * 0.08f * t;
                                float half = 0.62f * Mathf.Pow(1f - t, 0.8f) * (0.75f + 0.25f * Mathf.Sin(t * 3.14f));
                                float dx = Mathf.Abs(u - wob);
                                if (dx < half && t > 0.02f)
                                {
                                    float core = 1f - dx / half;
                                    c = Color.Lerp(new Color(1f, 0.35f, 0.08f), new Color(1f, 0.95f, 0.6f), core * core * (1f - t * 0.6f));
                                    c.a = Mathf.Clamp01(core * 3f) * Mathf.Clamp01(t * 10f);
                                }
                                break;
                            }
                        case "shard":
                            {
                                // Eiskristall: schmale Raute mit hellem Grat
                                float t = (v + 1f) * 0.5f;
                                float half = 0.6f * (t < 0.25f ? t / 0.25f : (1f - t) / 0.75f);
                                if (Mathf.Abs(u) < half)
                                {
                                    c = Color.Lerp(new Color(0.55f, 0.75f, 0.95f), Color.white, u < 0 ? 0.8f : 0.3f);
                                    if (Mathf.Abs(u) > half - 0.08f) c = new Color(0.35f, 0.55f, 0.8f);
                                    c.a = 0.95f;
                                }
                                break;
                            }
                        case "bolt":
                            {
                                // Zickzack-Blitz
                                float t = (v + 1f) * 0.5f;
                                float zx = (Mathf.Repeat(t * 3f, 1f) < 0.5f ? Mathf.Repeat(t * 3f, 1f) : 1f - Mathf.Repeat(t * 3f, 1f)) * 1.2f - 0.3f;
                                float d = Mathf.Abs(u - zx);
                                if (d < 0.1f) { c = new Color(1f, 1f, 0.85f); c.a = Mathf.Clamp01((0.1f - d) * 25f); }
                                else if (d < 0.22f) { c = new Color(1f, 0.9f, 0.4f); c.a = Mathf.Clamp01((0.22f - d) * 8f) * 0.6f; }
                                break;
                            }
                        case "bubbles":
                            {
                                // drei Giftblasen
                                float[] bx = { -0.45f, 0.1f, 0.5f }, by = { -0.3f, 0.2f, -0.45f }, br = { 0.32f, 0.42f, 0.25f };
                                for (int k = 0; k < 3; k++)
                                {
                                    float d = Mathf.Sqrt((u - bx[k]) * (u - bx[k]) + (v - by[k]) * (v - by[k]));
                                    if (d < br[k])
                                    {
                                        c = Color.Lerp(new Color(0.85f, 0.55f, 1f), Color.white, d > br[k] * 0.8f ? 0.5f : 0f);
                                        if (Mathf.Sqrt((u - bx[k] + br[k] * 0.35f) * (u - bx[k] + br[k] * 0.35f) + (v - by[k] - br[k] * 0.35f) * (v - by[k] - br[k] * 0.35f)) < br[k] * 0.22f) c = Color.white;
                                        c.a = 0.85f;
                                    }
                                }
                                break;
                            }
                        case "leaves":
                            {
                                // Blaetterkranz: mehrere spitze Blaetter im Faecher
                                for (int k = 0; k < 5; k++)
                                {
                                    float a = (-60f + k * 30f) * Mathf.Deg2Rad;
                                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                                    float lx = u * ca - (v + 0.6f) * sa * -1f, ly = u * sa + (v + 0.6f) * ca;   // in Blattrichtung
                                    lx = u * Mathf.Cos(-a) - (v + 0.6f) * Mathf.Sin(-a); ly = u * Mathf.Sin(-a) + (v + 0.6f) * Mathf.Cos(-a);
                                    if (ly > 0f && ly < 1.4f && Mathf.Abs(lx) < 0.22f * Mathf.Sin(ly / 1.4f * Mathf.PI))
                                    {
                                        c = Mathf.Abs(lx) < 0.025f ? new Color(0.75f, 0.85f, 0.6f) : Color.Lerp(new Color(0.75f, 0.75f, 0.75f), Color.white, ly / 1.4f);
                                        c.a = 1f;
                                    }
                                }
                                break;
                            }
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
