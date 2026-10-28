using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Blumentoepfe: In der Welt stehen fertige, leere Toepfe an festen Stellen. Eine reife Pflanze wird
    /// (nach 2 Sekunden Sperre) angetippt und "aufgenommen", dann leuchten alle freien Toepfe – ein Tipp
    /// auf einen Topf und die Pflanze fliegt hinein. Nur Topfpflanzen verteidigen den Hof.
    /// Spaeter sollen sich weitere Toepfe bauen lassen.
    /// </summary>
    public partial class WorldBuilder
    {
        class PotView
        {
            public PotState st; public Transform root; public SpriteRenderer pot, plant, glow, marker; public Light2D light;
            public float squash, pop = 1f, hidePlant;
        }

        class Flyer
        {
            public SpriteRenderer sr; public SpriteRenderer shadow; public Vector2 from, to; public float t, dur, s0, s1; public PotView target; public PlantGenome g;
            public PlantedState mergeTarget; public Cell oldAnchor; public int newLevel;   // Zusammenfuehren
        }

        readonly List<PotView> potViews = new List<PotView>();
        readonly List<Flyer> flyers = new List<Flyer>();
        static Sprite potSprite, arrowSprite;
        PlantedState carrying;

        public event Action<PotState> PotTapped;
        /// <summary>Hinweis waehrend des Platzierens (null = vorbei).</summary>
        public event Action<string> PlacingChanged;

        public IReadOnlyList<PotState> Pots => save.pots;
        public bool Placing => carrying != null;
        public PlantedState Carrying => carrying;

        /// <summary>Feste Topf-Stellen rund um Haus und Startbeet (werden auf freien Boden geschoben).</summary>
        static readonly Vector2[] PotSpots =
        {
            new Vector2(-2.9f, 3.0f), new Vector2(2.9f, 3.1f),     // links und rechts vor dem Haus
            new Vector2(0.4f, 1.0f), new Vector2(2.6f, 0.4f),      // am Weg
            new Vector2(-5.6f, 0.4f), new Vector2(-2.6f, -2.4f),   // am Startbeet
            new Vector2(1.4f, -2.2f), new Vector2(-5.2f, -2.4f),
        };

        void LoadPots()
        {
            if (!save.potSlots) BuildPotSlots();
            foreach (var p in save.pots) CreatePotView(p, false);
        }

        /// <summary>Beim ersten Start (oder alten Spielstaenden) die festen Toepfe anlegen.</summary>
        void BuildPotSlots()
        {
            var keep = new List<string>();
            foreach (var p in save.pots) if (!string.IsNullOrEmpty(p.type)) keep.Add(p.type);
            keep.AddRange(save.potStock);
            save.pots.Clear();
            save.potStock.Clear();
            foreach (var spot in PotSpots)
            {
                var pos = FindPotSpot(spot);
                if (pos == null) continue;
                string type = "";
                if (keep.Count > 0) { type = keep[0]; keep.RemoveAt(0); }
                save.pots.Add(new PotState { x = pos.Value.x, y = pos.Value.y, type = type });
            }
            save.potSlots = true;
            save.Save();
        }

        Vector2? FindPotSpot(Vector2 want)
        {
            for (float r = 0f; r <= 1.8f; r += 0.3f)
                for (int k = 0; k < (r == 0 ? 1 : 12); k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    var p = want + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (PotSpotFree(p)) return p;
                }
            return null;
        }

        bool PotSpotFree(Vector2 w)
        {
            if (Inside(w) < 0.4f) return false;
            if (Mathf.Abs(w.x) > MapW / 2f - 0.6f || Mathf.Abs(w.y) > MapH / 2f - 0.6f) return false;
            var area = StartArea; area.xMin -= 1f; area.yMin -= 1f; area.xMax += 1f; area.yMax += 1f;
            if (area.Contains(w)) return false;                                    // nicht im/am Zaun des Beets
            if (fields.ContainsKey(CellAt(w)) || fenceViews.ContainsKey(FenceCellAt(w))) return false;
            foreach (var p in save.pots) if ((p.Pos - w).sqrMagnitude < 0.9f * 0.9f) return false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (occupiedR[i] <= 0f || IsDecorOcc(i)) continue;
                float d = 0.3f + occupiedR[i] * 0.8f;
                if ((occupied[i] - w).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        public PotState PotAt(Vector2 w)
        {
            PotState best = null; float bd = 0.5f * 0.5f;
            foreach (var p in save.pots)
            {
                float d = (p.Pos + new Vector2(0, 0.3f) - w).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        bool AnyFreePot() { foreach (var p in save.pots) if (string.IsNullOrEmpty(p.type)) return true; return false; }

        public void TapPot(PotState p)
        {
            var v = potViews.Find(x => x.st == p);
            if (v != null) v.squash = 0.6f;
            Sfx.Play("pop", 1.3f, 0.6f);
            if (!string.IsNullOrEmpty(p.type)) PotTapped?.Invoke(p);
            else Floater?.Invoke(p.Pos + new Vector2(0, 0.8f), "Leerer Topf – zieh eine Pflanze groß!", new Color(1f, 0.9f, 0.7f));
        }

        /// <summary>Pflanze aus dem Topf entfernen (Topf wird wieder frei).</summary>
        public void ClearPot(PotState p)
        {
            p.type = "";
            p.level = 0;
            var v = potViews.Find(x => x.st == p);
            if (v != null) RefreshPotView(v);
            Particles.I?.Burst(p.Pos + new Vector2(0, 0.4f), "leaf", new Color(0.55f, 0.85f, 0.35f), 10, 2.5f, 0.6f, 0.25f, 3f, 1.5f);
            Sfx.Play("dig", 1.1f);
            save.Save();
        }

        public Vector2 PotMuzzle(PotState p) => p.Pos + new Vector2(0, 0.6f);
        public void KickPot(PotState p) { var v = potViews.Find(x => x.st == p); if (v != null) v.squash = Mathf.Max(v.squash, 0.5f); }

        // ------------------------------------------------------------------ Platzieren

        /// <summary>Reife Pflanze aufnehmen: freie Toepfe leuchten auf.</summary>
        public void StartPlacing(PlantedState p)
        {
            if (!AnyFreePot() && !AnyMergeTargetFor(p))
            {
                Floater?.Invoke(PlantBase(p) + new Vector2(0, 1.2f), "Alle Blumentöpfe sind belegt", new Color(1f, 0.7f, 0.6f));
                Sfx.Play("error", 1f, 0.5f);
                return;
            }
            carrying = p;
            Sfx.Play("pop", 0.8f, 0.9f);
            Sfx.Play("grow", 1.4f, 0.6f);
            Haptics.Tap(20);
            if (plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) { v.sy = 0.3f; v.flash = 0.6f; }
            PlacingChanged?.Invoke(AnyMergeTargetFor(p)
                ? "In einen Blumentopf setzen – oder auf eine gleiche Pflanze ziehen, dann wird sie stärker"
                : "Tippe auf einen leuchtenden Blumentopf, um " + p.Genome.name + " dorthin zu setzen");
        }

        public void CancelPlacing()
        {
            if (carrying == null) return;
            carrying = null;
            dragging = false;
            hoverTarget = null;
            Sfx.Play("pop", 0.6f, 0.5f);
            PlacingChanged?.Invoke(null);
        }

        /// <summary>Tipp waehrend des Platzierens: freier Topf -> Pflanze fliegt hinein, sonst abbrechen.</summary>
        public bool TryPlaceAt(Vector2 w)
        {
            if (carrying == null) return false;
            // gleiche Pflanze auf dem Feld -> zusammenfuehren
            var tp = PlantAt(CellAt(w));
            if (IsMergeTarget(tp)) return MergeInto(tp);
            var pot = PotAt(w);
            if (pot != null && !string.IsNullOrEmpty(pot.type) && pot.type == carrying.type) { MergeIntoPot(pot); return true; }
            if (pot == null || !string.IsNullOrEmpty(pot.type))
            {
                if (pot != null) Floater?.Invoke(pot.Pos + new Vector2(0, 0.9f), "Dieser Topf ist schon belegt", new Color(1f, 0.7f, 0.6f));
                CancelPlacing();
                return false;
            }
            PlaceInPot(pot);
            return true;
        }

        void PlaceInPot(PotState pot)
        {
            var p = carrying;
            carrying = null;
            PlacingChanged?.Invoke(null);
            var c = new Cell(p.x, p.y);
            var g = p.Genome;
            Vector2 from = CellCenter(c);
            Sprite sprite = null; float sx = 0.5f, sy = 0.5f;
            if (plantViews.TryGetValue(c, out var pv) && pv.sr != null)
            {
                sprite = pv.sr.sprite; from = pv.sr.bounds.center;
                sx = pv.sr.transform.lossyScale.x; sy = pv.sr.transform.lossyScale.y;
            }
            RemovePlantView(c);
            save.planted.Remove(p);
            pot.type = p.type;
            pot.level = p.Lv;
            var v = potViews.Find(x => x.st == pot);
            if (v != null) { RefreshPotView(v); v.hidePlant = 1f; }
            save.Save();

            // Flugbahn: im Bogen vom Feld in den Topf, dreht sich, wird auf Topfgroesse kleiner
            var fsr = new GameObject("FlyingPlant").AddComponent<SpriteRenderer>();
            fsr.transform.SetParent(transform, false);
            fsr.sprite = sprite;
            if (PlantMaterialShared != null) fsr.sharedMaterial = PlantMaterialShared;
            fsr.color = Color.Lerp(Color.white, g.ElementColor, 0.15f);
            fsr.sortingOrder = 31200;
            var sh = new GameObject("FlyShadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(transform, false);
            sh.sprite = MistSprite; sh.color = new Color(0, 0, 0, 0.3f); sh.sortingOrder = -31300;
            var to = pot.Pos + new Vector2(0, 0.55f);
            float dist = (to - from).magnitude;
            flyers.Add(new Flyer { sr = fsr, shadow = sh, from = from, to = to, dur = Mathf.Clamp(0.35f + dist * 0.06f, 0.4f, 0.8f), s0 = sx, s1 = sx * 0.85f, target = v, g = g });
            Particles.I?.Burst(from, "square", new Color(0.45f, 0.3f, 0.18f), 10, 2.6f, 0.5f, 0.1f, 9f, 2f);
            Shockwave(CellCenter(c) + new Vector2(0, -CellSize * 0.38f), CellSize * 1.6f, 0.3f, Color.white);
            Sfx.Play("dig", 1f, 0.9f);
            Sfx.Play("launch", 1.5f, 0.6f);
            Haptics.Tap(20);
        }

        void UpdateFlyers(float dt)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.t += dt;
                float k = Mathf.Clamp01(f.t / f.dur);
                var ground = Vector2.Lerp(f.from, f.to, k);
                float h = 4f * (0.9f + (f.to - f.from).magnitude * 0.12f) * k * (1 - k);
                f.sr.transform.position = ground + new Vector2(0, h);
                f.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * 25f * Mathf.Sign(f.to.x - f.from.x + 0.01f));
                float s = Mathf.Lerp(f.s0, f.s1, k) * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
                f.sr.transform.localScale = new Vector3(s, s, 1);
                f.shadow.transform.position = ground;
                f.shadow.transform.localScale = new Vector3(0.6f - h * 0.1f, 0.18f, 1);
                if (UnityEngine.Random.value < dt * 40) Particles.I?.Spawn(f.sr.transform.position, "spark", Color.Lerp(Color.white, f.g.ElementColor, 0.5f), Vector3.zero, 0.4f, 0.18f, 0f, 0f);
                if (k < 1f) continue;
                // Landung im Topf: Wucht!
                Destroy(f.sr.gameObject); Destroy(f.shadow.gameObject);
                flyers.RemoveAt(i);
                if (f.mergeTarget != null) { LandMerge(f); continue; }
                var at = f.to + new Vector2(0, -0.5f);
                if (f.target != null) { f.target.hidePlant = 0f; f.target.squash = 1f; f.target.pop = 0.3f; RefreshPotView(f.target); }
                if (f.newLevel > 1) Floater?.Invoke(at + new Vector2(0, 1.7f), f.g.name + " Stufe " + f.newLevel + "!", new Color(1f, 0.9f, 0.4f));
                Shockwave(at, 2.2f, 0.4f, Color.white);
                Shockwave(at, 1.3f, 0.3f, f.g.ElementColor);
                Particles.I?.Burst(at + new Vector2(0, 0.5f), "star", f.g.ElementColor, 16, 3f, 0.7f, 0.3f, 1f, 1f);
                Particles.I?.Burst(at, "square", new Color(0.45f, 0.3f, 0.18f), 10, 2.5f, 0.5f, 0.1f, 9f, 2.5f);
                Sfx.Play("thump", 0.85f, 1f);
                Sfx.Play("unlock", 1.1f, 0.8f);
                Haptics.Tap(40);
                WorldCamera.I?.Shake(0.07f);
                WorldCamera.I?.Punch(0.03f);
                WorldPostFX.Punch(0.5f);
                Floater?.Invoke(at + new Vector2(0, 1.3f), f.g.name + " verteidigt jetzt!", f.g.ElementColor);
            }
        }

        // ------------------------------------------------------------------ Darstellung

        void CreatePotView(PotState p, bool fx)
        {
            var root = new GameObject("Pot").transform;
            root.SetParent(objects, false);
            root.position = p.Pos;
            int order = Order(p.Pos.y);

            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root, false);
            shadow.sprite = MistSprite; shadow.color = new Color(0, 0, 0, 0.35f);
            shadow.transform.localScale = new Vector3(0.6f, 0.17f, 1);
            shadow.sortingOrder = -31300;

            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(root, false);
            glow.sprite = MistSprite; glow.transform.localPosition = new Vector3(0, 0.45f, 0);
            glow.transform.localScale = new Vector3(1f, 1f, 1);
            glow.sortingOrder = order - 2;

            var plant = new GameObject("Plant").AddComponent<SpriteRenderer>();
            plant.transform.SetParent(root, false);
            plant.sortingOrder = order - 1;   // Stiel verschwindet hinter dem Topfrand
            if (PlantMaterialShared != null) plant.sharedMaterial = PlantMaterialShared;

            var pot = new GameObject("Pot").AddComponent<SpriteRenderer>();
            pot.transform.SetParent(root, false);
            pot.sprite = PotSprite;
            pot.transform.localScale = Vector3.one * 0.48f;
            pot.sortingOrder = order;

            var marker = new GameObject("Marker").AddComponent<SpriteRenderer>();
            marker.transform.SetParent(root, false);
            marker.sprite = ArrowSprite;
            marker.transform.localScale = Vector3.one * 0.32f;
            marker.sortingOrder = 31100;
            marker.enabled = false;

            var light = root.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 1.3f; light.pointLightInnerRadius = 0.05f;
            light.intensity = 0f;

            var v = new PotView { st = p, root = root, pot = pot, plant = plant, glow = glow, marker = marker, light = light, pop = fx ? 0f : 1f };
            potViews.Add(v);
            RefreshPotView(v);
        }

        void RefreshPotView(PotView v)
        {
            bool has = !string.IsNullOrEmpty(v.st.type) && PlantBalance.I.Plant(v.st.type) != null;
            v.plant.enabled = has;
            if (!has) { v.light.intensity = 0f; return; }
            var g = v.st.Genome;
            var sp = PlantBalance.I.Species(g.species);
            var ps = art.Get(sp.art + sp.stages[sp.stages.Length - 1]);
            v.plant.sprite = ps;
            if (ps != null)
            {
                float s = 0.6f / Mathf.Max(0.01f, ps.bounds.size.x) * PlantBalance.I.LevelSize(v.st.Lv);
                float sy = s * 1.3f;
                v.plant.transform.localScale = new Vector3(s, sy, 1);
                v.plant.transform.localPosition = new Vector3(0, 0.3f - ps.bounds.min.y * sy, 0);
            }
            v.plant.color = Color.Lerp(Color.white, g.ElementColor, 0.15f);
            v.light.color = g.ElementColor;
        }

        void UpdatePots(float dt)
        {
            float t = Time.time;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float night = NightWave.I != null ? Mathf.Max(NightWave.I.NightAmount, NightWave.I.DuskAmount * 0.5f) : 0f;
            noWoodT -= dt;
            UpdateFlyers(udt);
            foreach (var v in potViews)
            {
                bool has = !string.IsNullOrEmpty(v.st.type);
                bool target = carrying != null && (!has || v.st.type == carrying.type);   // freier oder gleicher Topf: leuchtet und huepft
                v.squash = Mathf.MoveTowards(v.squash, 0, udt * 5f);
                v.pop = Mathf.MoveTowards(v.pop, 1f, udt * 3f);
                float sq = Mathf.Sin(v.squash * Mathf.PI * 2f) * v.squash * 0.25f;
                float pop = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float hop = target ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f + v.st.x)) * 0.08f : 0f;
                v.root.localScale = new Vector3((1 + sq) * pop * (1 - hop * 0.5f), (1 - sq) * pop * (1 + hop), 1);
                v.plant.enabled = has && v.hidePlant <= 0f;
                if (has) v.plant.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f + v.st.x * 2f) * 2f);
                v.marker.enabled = target;
                if (target) v.marker.transform.localPosition = new Vector3(0, 1.05f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 0.18f, 0);
                if (target)
                {
                    float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                    v.glow.color = new Color(1f, 0.95f, 0.7f, pulse);
                    v.light.color = new Color(1f, 0.9f, 0.6f);
                    v.light.intensity = 0.6f + pulse;
                }
                else if (has)
                {
                    var col = v.st.Genome.ElementColor;
                    v.glow.color = new Color(col.r, col.g, col.b, 0.18f + 0.08f * Mathf.Sin(t * 2f + v.st.y) + night * 0.25f);
                    v.light.color = col;
                    v.light.intensity = night * (0.9f + 0.2f * Mathf.Sin(t * 3f + v.st.x));
                }
                else { v.glow.color = Color.clear; v.light.intensity = 0f; }
            }
        }

        /// <summary>Pfeil ueber freien Toepfen beim Platzieren.</summary>
        static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null) return arrowSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x + 0.5f - n / 2f, fy = y + 0.5f;
                        // Dreieck nach unten (Spitze unten) + Schaft
                        float halfW = (fy - 10f) * 0.75f;
                        bool tri = fy > 10f && fy < 52f && Mathf.Abs(fx) < halfW;
                        bool shaft = fy >= 50f && fy < 88f && Mathf.Abs(fx) < 12f;
                        float halfW2 = (fy - 4f) * 0.75f + 4f;
                        bool triO = fy > 4f && fy < 56f && Mathf.Abs(fx) < halfW2;
                        bool shaftO = fy >= 50f && fy < 92f && Mathf.Abs(fx) < 16f;
                        Color c = Color.clear;
                        if (triO || shaftO) c = new Color(0.35f, 0.2f, 0.05f, 1f);
                        if (tri || shaft) c = Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.95f, 0.6f), fy / n);
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), n);
                return arrowSprite;
            }
        }

        /// <summary>Terrakotta-Topf, im Code gemalt (in den Grafikpaketen gibt es keinen).</summary>
        public static Sprite PotSprite
        {
            get
            {
                if (potSprite != null) return potSprite;
                const int W = 128, H = 128;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[W * H];
                var clay = new Color(0.80f, 0.42f, 0.25f);
                var clayDark = new Color(0.45f, 0.2f, 0.12f);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float fx = x + 0.5f, fy = y + 0.5f;
                        Color c = Color.clear; float a = 0f;
                        // Koerper: Trapez von y 8 bis 84, oben breiter
                        if (fy >= 6 && fy <= 86)
                        {
                            float k = (fy - 6) / 80f;
                            float half = Mathf.Lerp(30f, 42f, k);
                            float u = (fx - 64f) / half;                       // -1..1 quer
                            float edge = 1f - Mathf.Abs(u);
                            if (edge > 0)
                            {
                                float shade = 0.62f + 0.5f * Mathf.Clamp01(1f - Mathf.Abs(u + 0.35f) * 1.1f);   // Licht von links
                                shade *= Mathf.Lerp(0.8f, 1f, k);              // unten etwas dunkler
                                c = Color.Lerp(clayDark, clay, shade);
                                if (fy < 14) c *= 0.85f;                       // Standring
                                a = Mathf.Clamp01(edge * half * 0.9f) * Mathf.Clamp01((fy - 6) * 0.8f);
                            }
                        }
                        // Rand oben: breiteres Band
                        if (fy > 80 && fy <= 100)
                        {
                            float u = (fx - 64f) / 49f;
                            float edge = 1f - Mathf.Abs(u);
                            if (edge > 0)
                            {
                                float shade = 0.72f + 0.45f * Mathf.Clamp01(1f - Mathf.Abs(u + 0.35f) * 1.1f);
                                if (fy > 96) shade += 0.12f;                   // Lichtkante
                                if (fy < 83) shade -= 0.25f;                   // Schatten unter dem Rand
                                c = Color.Lerp(clayDark, clay * 1.08f, shade);
                                a = Mathf.Clamp01(edge * 49f * 0.9f) * Mathf.Clamp01((100.5f - fy) * 0.9f);
                            }
                        }
                        // Erde im Topf (Ellipse oben)
                        float ex = (fx - 64f) / 42f, ey = (fy - 98f) / 7f;
                        if (ex * ex + ey * ey < 1f && fy > 95)
                        {
                            float d = ex * ex + ey * ey;
                            c = Color.Lerp(new Color(0.22f, 0.14f, 0.09f), new Color(0.33f, 0.22f, 0.14f), d);
                            a = Mathf.Clamp01((1f - d) * 12f);
                        }
                        c.a = a;
                        px[y * W + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                potSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.06f), W);
                return potSprite;
            }
        }
    }
}
