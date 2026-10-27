using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Verteidigung bauen: Zaeune aus Holz (im Feldraster, verbinden sich automatisch, Zombies muessen sie
    /// erst kaputtschlagen) und Blumentoepfe: Eine ausgewachsene Pflanze wird vom Feld in einen Topf gesetzt
    /// und kann dann frei auf der Karte aufgestellt werden. Nur Topfpflanzen verteidigen den Hof.
    /// </summary>
    public partial class WorldBuilder
    {
        class FenceView { public FenceState st; public SpriteRenderer sr; public float shake, pop = 1f; }
        class PotView
        {
            public PotState st; public Transform root; public SpriteRenderer pot, plant, glow; public Light2D light;
            public float squash, pop = 1f;
        }

        readonly Dictionary<Cell, FenceView> fenceViews = new Dictionary<Cell, FenceView>();
        readonly List<PotView> potViews = new List<PotView>();
        static Sprite potSprite;
        float noWoodT;

        public event Action<PotState> PotTapped;
        public event Action PotStockChanged;

        BuildDef Build => PlantBalance.I.build ?? new BuildDef { fenceWood = 1, fenceHp = 10, potWood = 2 };
        public int FenceCost => Build.fenceWood;
        public int PotCost => Build.potWood;

        void LoadDefense()
        {
            foreach (var f in save.fences) PlaceFence(new Cell(f.x, f.y), false, f);
            foreach (var p in save.pots) CreatePotView(p, false);
        }

        bool PayWood(int n, Vector2 at)
        {
            if (save.wood >= n) { save.wood -= n; WoodChanged?.Invoke(save.wood); return true; }
            if (noWoodT <= 0f)
            {
                Floater?.Invoke(at, "Zu wenig Holz – fälle Bäume!", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                noWoodT = 1.2f;
            }
            return false;
        }

        // ------------------------------------------------------------------ Zaeune

        /// <summary>Zaeune haben ein eigenes, groberes Raster: die Skyden-Zaunkacheln sind fuer 1 Einheit gemalt,
        /// so reicht der Zaun einem Zombie etwa bis zur Huefte.</summary>
        public const float FenceSize = 0.9f;
        public static Cell FenceCellAt(Vector2 world) => new Cell(Mathf.FloorToInt(world.x / FenceSize), Mathf.FloorToInt(world.y / FenceSize));
        public static Vector2 FenceCenter(Cell c) => new Vector2((c.x + 0.5f) * FenceSize, (c.y + 0.5f) * FenceSize);

        public bool HasFence(Cell c) => fenceViews.ContainsKey(c);

        public bool CanPlaceFence(Cell c)
        {
            if (fenceViews.ContainsKey(c)) return false;
            var center = FenceCenter(c);
            float h = FenceSize * 0.4f;
            foreach (var o in new[] { Vector2.zero, new Vector2(-h, -h), new Vector2(h, -h), new Vector2(-h, h), new Vector2(h, h) })
                if (fields.ContainsKey(CellAt(center + o))) return false;   // nicht auf Felder
            if (Inside(center) < 0.3f) return false;
            if (Mathf.Abs(center.x) > MapW / 2f - 0.5f || Mathf.Abs(center.y) > MapH / 2f - 0.5f) return false;
            foreach (var p in save.pots) if ((p.Pos - center).sqrMagnitude < 0.6f * 0.6f) return false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (occupiedR[i] <= 0f || IsDecorOcc(i)) continue;
                float d = FenceSize * 0.3f + occupiedR[i] * 0.7f;
                if ((occupied[i] - center).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        /// <summary>Zaunstueck setzen (kostet Holz). loaded = Stand aus dem Spielstand, dann gratis.</summary>
        public bool PlaceFence(Cell c, bool fx, FenceState loaded = null)
        {
            if (loaded == null && !CanPlaceFence(c)) return false;
            var center = FenceCenter(c);
            if (loaded == null && !PayWood(FenceCost, center + new Vector2(0, 0.6f))) return false;
            ClearDecorAround(center, FenceSize * 0.55f);
            var st = loaded ?? new FenceState { x = c.x, y = c.y, hp = Build.fenceHp };
            if (loaded == null) save.fences.Add(st);
            var sr = new GameObject("Fence").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.transform.position = center;
            sr.sortingOrder = Order(center.y - FenceSize * 0.2f);
            var v = new FenceView { st = st, sr = sr, pop = fx ? 0f : 1f };
            fenceViews[c] = v;
            RetileAround(c);
            if (fx)
            {
                Particles.I?.Burst(center, "square", new Color(0.62f, 0.44f, 0.26f), 7, 2.2f, 0.45f, 0.25f, 6f, 1.4f);
                Sfx.Play("chop", UnityEngine.Random.Range(1.1f, 1.25f), 0.8f);
                Haptics.Tap(10);
                save.Save();
            }
            return true;
        }

        void ClearDecorAround(Vector2 center, float r)
        {
            for (int i = decor.Count - 1; i >= 0; i--)
            {
                var d = decor[i];
                if (Mathf.Abs(d.p.x - center.x) > r || Mathf.Abs(d.p.y - center.y) > r) continue;
                if (d.go != null) Destroy(d.go);
                if (d.occ >= 0 && d.occ < occupiedR.Count) occupiedR[d.occ] = 0f;
                decor.RemoveAt(i);
            }
        }

        void RetileAround(Cell c)
        {
            Retile(c);
            Retile(new Cell(c.x + 1, c.y)); Retile(new Cell(c.x - 1, c.y));
            Retile(new Cell(c.x, c.y + 1)); Retile(new Cell(c.x, c.y - 1));
        }

        /// <summary>Waehlt das passende Stueck aus den Skyden-Zaunkacheln (Ecken, gerade, einzeln).</summary>
        void Retile(Cell c)
        {
            if (!fenceViews.TryGetValue(c, out var v)) return;
            bool u = HasFence(new Cell(c.x, c.y + 1)), d = HasFence(new Cell(c.x, c.y - 1));
            bool l = HasFence(new Cell(c.x - 1, c.y)), r = HasFence(new Cell(c.x + 1, c.y));
            int idx;
            if (r && d && !l && !u) idx = 0;
            else if (l && d && !r && !u) idx = 2;
            else if (r && u && !l && !d) idx = 5;
            else if (l && u && !r && !d) idx = 7;
            else if (l || r) idx = 1;
            else if (u || d) idx = 3;
            else idx = 11;
            v.sr.sprite = art.Get("sk_Fence_" + idx);
        }

        /// <summary>Axt auf einen Zaun: wieder abbauen, Holz zurueck.</summary>
        public bool TryRemoveFence(Vector2 world)
        {
            var c = FenceCellAt(world);
            if (!fenceViews.TryGetValue(c, out var v)) return false;
            RemoveFence(c, v, false);
            AddWood(FenceCost, FenceCenter(c) + new Vector2(0, 0.5f));
            return true;
        }

        void RemoveFence(Cell c, FenceView v, bool broken)
        {
            var center = FenceCenter(c);
            Particles.I?.Burst(center + new Vector2(0, 0.15f), "square", new Color(0.6f, 0.42f, 0.25f), broken ? 14 : 8, broken ? 3.5f : 2.5f, 0.6f, 0.3f, 7f, 1.5f);
            Sfx.Play("break", broken ? 0.9f : 1.2f, broken ? 0.9f : 0.6f);
            if (broken) WorldCamera.I?.Shake(0.06f);
            if (v.sr != null) Destroy(v.sr.gameObject);
            fenceViews.Remove(c);
            save.fences.Remove(v.st);
            RetileAround(c);
            save.Save();
        }

        /// <summary>Zaun, der an dieser Stelle im Weg steht (fuer die Zombies), sonst null.</summary>
        public FenceState FenceAt(Vector2 pos)
        {
            return fenceViews.TryGetValue(FenceCellAt(pos), out var v) ? v.st : null;
        }

        /// <summary>Zombie schlaegt auf einen Zaun ein.</summary>
        public void HitFence(FenceState st, float dmg)
        {
            var c = new Cell(st.x, st.y);
            if (!fenceViews.TryGetValue(c, out var v)) return;
            st.hp -= dmg;
            v.shake = 1f;
            if (UnityEngine.Random.value < 0.25f)
                Particles.I?.Spawn(FenceCenter(c) + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.2f), "square", new Color(0.62f, 0.44f, 0.26f),
                    new Vector3(UnityEngine.Random.Range(-1f, 1f), 1.5f, 0), 0.5f, 0.12f, 0f, 6f);
            if (st.hp <= 0f) RemoveFence(c, v, true);
        }

        void UpdateFences(float dt)
        {
            float t = Time.time;
            foreach (var v in fenceViews.Values)
            {
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 4f);
                v.shake = Mathf.MoveTowards(v.shake, 0f, dt * 4f);
                float e = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                v.sr.transform.localScale = Vector3.one * FenceSize * e;
                v.sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 50f) * v.shake * 4f);
                // beschaedigte Zaeune werden dunkler
                float hp = Mathf.Clamp01(v.st.hp / Mathf.Max(1f, Build.fenceHp));
                v.sr.color = Color.Lerp(new Color(0.55f, 0.45f, 0.4f), Color.white, hp);
            }
        }

        // ------------------------------------------------------------------ Blumentoepfe

        public IReadOnlyList<PotState> Pots => save.pots;

        /// <summary>Ausgewachsene Pflanze vom Feld in einen Topf setzen (kostet Holz). Danach ist das Feld frei.</summary>
        public bool PotPlant(PlantedState p)
        {
            if (p == null || !p.Grown) return false;
            var c = new Cell(p.x, p.y);
            var pos = CellCenter(c);
            if (!PayWood(PotCost, pos + new Vector2(0, 0.8f))) return false;
            RemovePlantView(c);
            save.planted.Remove(p);
            save.potStock.Add(p.type);
            Particles.I?.Burst(pos + new Vector2(0, 0.2f), "leaf", new Color(0.55f, 0.85f, 0.35f), 10, 3f, 0.6f, 0.3f, 3f, 1.5f);
            Particles.I?.Burst(pos, "square", new Color(0.45f, 0.3f, 0.18f), 8, 2.4f, 0.5f, 0.25f, 7f, 1.5f);
            Particles.I?.Ring(pos, Color.white, 0.8f);
            Sfx.Play("sow", 0.85f);
            Sfx.Play("unlock", 1.2f, 0.6f);
            Haptics.Tap(25);
            save.Save();
            PotStockChanged?.Invoke();
            NotifyInventory();
            return true;
        }

        public bool CanPlacePot(Vector2 w)
        {
            if (Inside(w) < 0.3f) return false;
            if (Mathf.Abs(w.x) > MapW / 2f - 0.5f || Mathf.Abs(w.y) > MapH / 2f - 0.5f) return false;
            var c = CellAt(w);
            if (fields.ContainsKey(c) || fenceViews.ContainsKey(FenceCellAt(w))) return false;
            foreach (var p in save.pots) if ((p.Pos - w).sqrMagnitude < 0.5f * 0.5f) return false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (occupiedR[i] <= 0f || IsDecorOcc(i)) continue;
                float d = 0.2f + occupiedR[i] * 0.7f;
                if ((occupied[i] - w).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        /// <summary>Naechste Topfpflanze aus dem Lager an dieser Stelle aufstellen.</summary>
        public bool PlacePotFromStock(Vector2 w)
        {
            if (save.potStock.Count == 0)
            {
                if (noWoodT <= 0f)
                {
                    Floater?.Invoke(w + new Vector2(0, 0.4f), "Keine Topfpflanze – erst eine reife Pflanze eintopfen", new Color(1f, 0.85f, 0.6f));
                    noWoodT = 1.2f;
                }
                return false;
            }
            if (!CanPlacePot(w))
            {
                Floater?.Invoke(w + new Vector2(0, 0.4f), "Hier ist kein Platz", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1.1f, 0.5f);
                return false;
            }
            var p = new PotState { x = w.x, y = w.y, type = save.potStock[0] };
            save.potStock.RemoveAt(0);
            save.pots.Add(p);
            ClearDecorAround(w, 0.3f);
            CreatePotView(p, true);
            Particles.I?.Burst(w, "square", new Color(0.5f, 0.36f, 0.22f), 8, 2.2f, 0.45f, 0.25f, 6f, 1.2f);
            Particles.I?.Burst(w + new Vector2(0, 0.5f), "star", p.Genome.ElementColor, 8, 2.5f, 0.6f, 0.3f, 1f, 1f);
            Sfx.Play("sow", 1.1f);
            Sfx.Play("pop", 0.9f, 0.7f);
            Haptics.Tap(18);
            save.Save();
            PotStockChanged?.Invoke();
            return true;
        }

        public PotState PotAt(Vector2 w)
        {
            PotState best = null; float bd = 0.42f * 0.42f;
            foreach (var p in save.pots)
            {
                float d = (p.Pos + new Vector2(0, 0.3f) - w).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public void TapPot(PotState p)
        {
            var v = potViews.Find(x => x.st == p);
            if (v != null) v.squash = 0.6f;
            Sfx.Play("pop", 1.3f, 0.6f);
            PotTapped?.Invoke(p);
        }

        /// <summary>Topfpflanze wieder einsammeln (zurueck ins Lager).</summary>
        public void PickUpPot(PotState p)
        {
            var v = potViews.Find(x => x.st == p);
            if (v != null) { Destroy(v.root.gameObject); potViews.Remove(v); }
            save.pots.Remove(p);
            save.potStock.Add(p.type);
            Particles.I?.Burst(p.Pos + new Vector2(0, 0.3f), "leaf", new Color(0.55f, 0.85f, 0.35f), 8, 2.5f, 0.5f, 0.25f, 3f, 1.5f);
            Sfx.Play("dig", 1.1f);
            save.Save();
            PotStockChanged?.Invoke();
        }

        public Vector2 PotMuzzle(PotState p) => p.Pos + new Vector2(0, 0.6f);
        public void KickPot(PotState p) { var v = potViews.Find(x => x.st == p); if (v != null) v.squash = Mathf.Max(v.squash, 0.5f); }

        void CreatePotView(PotState p, bool fx)
        {
            var g = p.Genome;
            var sp = PlantBalance.I.Species(g.species);
            var root = new GameObject("Pot_" + p.type).transform;
            root.SetParent(objects, false);
            root.position = p.Pos;
            int order = Order(p.Pos.y);

            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root, false);
            shadow.sprite = MistSprite; shadow.color = new Color(0, 0, 0, 0.35f);
            shadow.transform.localScale = new Vector3(0.55f, 0.16f, 1);
            shadow.sortingOrder = -31300;

            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(root, false);
            glow.sprite = MistSprite; glow.transform.localPosition = new Vector3(0, 0.45f, 0);
            glow.transform.localScale = new Vector3(0.9f, 0.9f, 1);
            glow.sortingOrder = order - 2;

            var plant = new GameObject("Plant").AddComponent<SpriteRenderer>();
            plant.transform.SetParent(root, false);
            var ps = art.Get(sp.art + sp.stages[sp.stages.Length - 1]);
            plant.sprite = ps;
            if (ps != null)
            {
                float s = 0.62f * sp.scale / Mathf.Max(0.01f, ps.bounds.size.x);
                plant.transform.localScale = new Vector3(s, s, 1);
                plant.transform.localPosition = new Vector3(0, 0.26f - ps.bounds.min.y * s, 0);
            }
            plant.color = Color.Lerp(Color.white, g.ElementColor, 0.18f);
            plant.sortingOrder = order - 1;   // Stiel verschwindet hinter dem Topfrand

            var pot = new GameObject("Pot").AddComponent<SpriteRenderer>();
            pot.transform.SetParent(root, false);
            pot.sprite = PotSprite;
            pot.transform.localScale = Vector3.one * 0.42f;
            pot.sortingOrder = order;

            var light = root.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 1.3f; light.pointLightInnerRadius = 0.05f;
            light.color = g.ElementColor; light.intensity = 0f;

            potViews.Add(new PotView { st = p, root = root, pot = pot, plant = plant, glow = glow, light = light, pop = fx ? 0f : 1f });
        }

        void UpdatePots(float dt)
        {
            float t = Time.time;
            float night = NightWave.I != null ? Mathf.Max(NightWave.I.NightAmount, NightWave.I.DuskAmount * 0.5f) : 0f;
            noWoodT -= dt;
            foreach (var v in potViews)
            {
                v.squash = Mathf.MoveTowards(v.squash, 0, dt * 5f);
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 3f);
                float sq = Mathf.Sin(v.squash * Mathf.PI * 2f) * v.squash * 0.25f;
                float pop = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                v.root.localScale = new Vector3((1 + sq) * pop, (1 - sq) * pop, 1);
                v.plant.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f + v.st.x * 2f) * 2f);
                var col = v.st.Genome.ElementColor;
                v.glow.color = new Color(col.r, col.g, col.b, 0.18f + 0.08f * Mathf.Sin(t * 2f + v.st.y) + night * 0.25f);
                v.light.intensity = night * (0.9f + 0.2f * Mathf.Sin(t * 3f + v.st.x));
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
