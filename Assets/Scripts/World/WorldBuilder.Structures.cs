using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Bauwerke: im Laden (Reiter "Bauwerke") einmal kaufen, dann auf einen freien Platz im Hof setzen. Ein Tipp oeffnet
    /// ihr Fenster, weitere Tipps (oder "Verbessern!") bringen sie Stufe um Stufe weiter – wie Pflanzen und Waffenbank.
    /// Jedes wirkt im Umkreis (oder ueberall, radius 0): Wachturm, Saatgutmuehle, dazu die Totems. Werte in plants.json -> structures und structureTaps.
    /// </summary>
    public partial class WorldBuilder
    {
        class StructView { public StructureState st; public StructureDef def; public SpriteRenderer sr; public Light2D light; public float squash, scale, timer; }
        readonly List<StructView> structViews = new List<StructView>();
        StructureState pendingStruct;

        public event Action<StructureState> StructureTapped;
        public event Action<StructureDef, StructureState> StructureLeveled;
        public event Action<StructureDef> StructurePlacing;   // null = fertig gesetzt
        /// <summary>Bauwerk, dessen Fenster gerade offen ist – ein Tipp darauf verbessert es.</summary>
        public StructureState OpenStructure { get; set; }
        public StructureDef PlacingStructure => pendingStruct != null ? PlantBalance.I.Structure(pendingStruct.id) : null;

        public StructureState Struct(string id) => save.structures.Find(s => s.id == id);
        public bool HasStructure(Cell c) { foreach (var s in save.structures) if (s.placed && s.x == c.x && s.y == c.y && !IsTotem(s)) return true; return false; }   // Totems belegen kein Feld

        // ------------------------------------------------------------------ Werte

        public float StructValue(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            if (d == null) return 0f;
            float v = d.value + d.valuePerLevel * (s.lv - 1);
            if (s.stone && d.effect == "tower") v *= PlantBalance.I.Transforms.stoneTowerDamage;   // Steinturm trifft haerter
            return v;
        }

        public float StructRadius(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            if (d == null) return 0f;
            float r = d.radius + d.radiusPerLevel * (s.lv - 1);
            if (s.stone && d.effect == "tower") r += PlantBalance.I.Transforms.stoneTowerRange;
            return r;
        }

        /// <summary>Bild eines Bauwerks (Wachturm aus Holz oder Stein).</summary>
        public static Sprite StructSprite(StructureState s) => StructureArt.Get(s.stone ? s.id + "_stone" : s.id);
        Vector2 StructPos(StructureState s) => s.anchored && IsTotem(s) ? TotemCenter(s) : CellCenter(new Cell(s.x, s.y));

        /// <summary>Summe der Wirkung aller Bauwerke dieser Art, deren Umkreis pos erreicht (radius 0 = ueberall).</summary>
        public float StructEffect(string effect, Vector2 pos)
        {
            float sum = 0f;
            foreach (var s in save.structures)
            {
                if (!s.placed) continue;
                var d = PlantBalance.I.Structure(s.id);
                if (d == null || d.effect != effect) continue;
                float r = StructRadius(s);
                if (r <= 0f || (StructPos(s) - pos).sqrMagnitude <= r * r) sum += StructValue(s);
            }
            return sum;
        }

        public float StructGlobal(string effect)
        {
            float sum = 0f;
            foreach (var s in save.structures) { var d = PlantBalance.I.Structure(s.id); if (s.placed && d != null && d.effect == effect) sum += StructValue(s); }
            return sum;
        }

        public int StructLevel(string effect)
        {
            int lv = 0;
            foreach (var s in save.structures) { var d = PlantBalance.I.Structure(s.id); if (s.placed && d != null && d.effect == effect) lv = Mathf.Max(lv, s.lv); }
            return lv;
        }

        public int StructTapsNeeded(StructureState s)
        {
            var t = PlantBalance.I.StructureTaps;
            return Mathf.Max(1, Mathf.RoundToInt(t.tapsBase * Mathf.Pow(t.tapsGrowth, s.lv - 1)));
        }
        public bool StructMaxed(StructureState s) => s.lv >= PlantBalance.I.StructureTaps.maxLevel;

        /// <summary>Samenpreis nach Rabatt der Saatgutmuehle.</summary>
        public float SeedCost(CatalogDef d) => d == null ? 0f : d.cost * Mathf.Max(0.1f, 1f - StructGlobal("seedcost"));

        /// <summary>Kurzer Text, was das Bauwerk gerade bewirkt (Fenster).</summary>
        public string StructSummary(StructureState s)
        {
            var d = PlantBalance.I.Structure(s.id);
            if (d == null) return "";
            if (IsTotem(d)) return TotemSummary(s);
            float v = StructValue(s), r = StructRadius(s);
            string where = r > 0 ? " · Umkreis " + r.ToString("0.#") : " · überall";
            switch (d.effect)
            {
                case "light": return "Zombies nehmen +" + Mathf.RoundToInt(v * 100) + " % Schaden" + where + (s.moon ? " · Mondlicht" : "");
                case "tower": return (s.moon ? "Schießt nachts doppelt, je " : "Schießt nachts für ") + v.ToString("0.#") + " Schaden" + where + (s.stone ? " · Stein" : "");
                case "hut": return "Wohnraum für " + Mathf.RoundToInt(v) + " Knollis";
                case "seedcost": return "Samen " + Mathf.RoundToInt(Mathf.Min(0.9f, v) * 100) + " % billiger";
            }
            return "";
        }

        // ------------------------------------------------------------------ Kaufen und Aufstellen

        public bool BuyStructure(StructureDef d)
        {
            float cost = StructCost(d);
            if (d == null || (!d.multi && Struct(d.id) != null) || !StructureAvailable(d) || save.wood < d.wood || save.coins < cost) return false;
            if (!TrySpend(cost)) return false;
            save.wood -= d.wood; WoodChanged?.Invoke(save.wood);
            var st = new StructureState { id = d.id, lv = 1 };
            save.structures.Add(st);
            Sfx.Play("levelup", 1.2f, 0.7f);
            Haptics.Tap(25);
            BeginPlacing(st);
            save.Save();
            return true;
        }

        void BeginPlacing(StructureState st)
        {
            pendingStruct = st;
            st.placed = false;
            if (wallMode) SetWallMode(false);
            StructurePlacing?.Invoke(PlantBalance.I.Structure(st.id));
            RefreshGhosts();
        }

        /// <summary>Bauwerk wieder aufheben und neu setzen.</summary>
        public void MoveStructure(StructureState st)
        {
            var v = structViews.Find(x => x.st == st);
            if (v != null) { Particles.I?.Burst(StructPos(st) + new Vector2(0, 0.3f), "square", new Color(0.7f, 0.6f, 0.45f), 8, 2f, 0.5f, 0.1f, 6f, 1.5f); Destroy(v.sr.gameObject); structViews.Remove(v); }
            RemoveTotemPath(st);
            if (OpenStructure == st) OpenStructure = null;
            BeginPlacing(st);
        }

        public bool CanPlaceStructure(Cell c) => PlantAt(c) == null && !HasWall(c) && !HasStructure(c) && (HasField(c) || CanPlaceField(c));

        public bool TryPlaceStructure(Cell c)
        {
            if (pendingStruct == null) return false;
            bool totem = IsTotem(pendingStruct);
            Cell anchor = c;
            if (totem ? !CanPlaceTotem(c, out anchor) : !CanPlaceStructure(c))
            {
                Sfx.Play("error", 1f, 0.5f);
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), totem ? "Tippe auf ein Feld, das noch kein Totem hat" : "Hier ist kein Platz", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            var st = pendingStruct;
            pendingStruct = null;
            st.x = c.x; st.y = c.y; st.placed = true;
            if (totem) { st.anchored = true; st.bx = anchor.x; st.by = anchor.y; }
            if (!totem) ClearDecorAround(CellCenter(c), CellSize * 0.6f);
            CreateStructView(st, true);
            if (totem) TotemPlaced(st);
            StructurePlacing?.Invoke(null);
            RefreshGhosts();
            save.Save();
            return true;
        }

        void InitStructures()
        {
            save.structures.RemoveAll(s => PlantBalance.I.Structure(s.id) == null);   // entfernte Bauwerke (Brunnen, Bienenhaus, ...)
            foreach (var s in save.structures) if (s.placed) CreateStructView(s, false);
            var waiting = save.structures.Find(s => !s.placed);
            if (waiting != null) BeginPlacing(waiting);
        }

        void CreateStructView(StructureState st, bool pop)
        {
            var d = PlantBalance.I.Structure(st.id);
            if (d == null) return;
            if (IsTotem(d))
            {
                // kleiner Totempfahl auf einem schmalen Pfad oben links ueber der Flaeche
                var tsr = new GameObject("Totem_" + st.id).AddComponent<SpriteRenderer>();
                tsr.transform.SetParent(objects, false);
                tsr.sprite = StructSprite(st);
                float tscale = CellSize * 0.75f * d.scale / Mathf.Max(0.01f, tsr.sprite.bounds.size.x);
                tsr.transform.localScale = Vector3.one * tscale;
                CreateTotemPath(st);
                structViews.Add(new StructView { st = st, def = d, sr = tsr, scale = tscale, squash = pop ? 1f : 0f, timer = 3f });
                PlaceTotemPole(structViews[structViews.Count - 1]);
                if (pop)
                {
                    Shockwave(StructPos(st), 1.6f, 0.5f, new Color(1f, 0.95f, 0.7f, 0.8f));
                    Particles.I?.Burst(StructPos(st), "star", new Color(1f, 0.9f, 0.5f), 18, 3f, 0.8f, 0.3f, 1f, 1.5f);
                    Sfx.Play("unlock", 1.1f, 0.7f);
                    Haptics.Tap(30);
                }
                return;
            }
            var foot = StructPos(st) + new Vector2(0, -CellSize * 0.35f);
            var sr = new GameObject("Struct_" + st.id).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = StructSprite(st);
            float scale = CellSize * 1.8f * d.scale / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            sr.transform.position = foot;
            sr.transform.localScale = Vector3.one * scale;
            sr.sortingOrder = Order(foot.y);
            Shadow(foot, CellSize * 1.6f * d.scale, 0.25f, 0.3f);
            var v = new StructView { st = st, def = d, sr = sr, scale = scale, squash = pop ? 1f : 0f, timer = UnityEngine.Random.Range(0.5f, 2f) };
            if (d.effect == "light" || d.effect == "tower")
            {
                var lgo = new GameObject("StructLight");
                lgo.transform.SetParent(sr.transform, false);
                lgo.transform.position = foot + new Vector2(0, sr.bounds.size.y * (d.effect == "light" ? 0.85f : 0.6f));
                v.light = lgo.AddComponent<Light2D>();
                v.light.lightType = Light2D.LightType.Point;
                v.light.pointLightInnerRadius = 0.2f;
                v.light.pointLightOuterRadius = d.effect == "light" ? 2.5f : 1.4f;
                v.light.color = new Color(1f, 0.85f, 0.5f);
                v.light.intensity = 0f;
            }
            structViews.Add(v);
            ApplyStructLook(v);
            if (pop)
            {
                Particles.I?.Burst(foot + new Vector2(0, 0.5f), "star", new Color(1f, 0.9f, 0.5f), 18, 3f, 0.8f, 0.3f, 1f, 1.5f);
                Shockwave(foot, 1.4f, 0.45f, new Color(1f, 0.95f, 0.7f, 0.8f));
                Sfx.Play("unlock", 1.1f, 0.7f);
                Haptics.Tap(30);
            }
        }

        /// <summary>Bild und Licht nach Verwandlungen: Stein-Turm, Mondlicht (kuehles, weites Licht).</summary>
        void ApplyStructLook(StructView v)
        {
            v.sr.sprite = StructSprite(v.st);
            if (v.light == null) return;
            bool lantern = v.def.effect == "light";
            v.light.pointLightOuterRadius = (lantern ? 2.5f : 1.4f) * (v.st.moon ? 2f : 1f);
            if (v.st.moon) v.light.color = new Color(0.65f, 0.78f, 1f);
        }

        // ------------------------------------------------------------------ Antippen und Verbessern

        public StructureState HitStructure(Vector2 w)
        {
            var c = CellAt(w);
            foreach (var v in structViews)
            {
                if (IsTotem(v.def))
                {
                    var tb = v.sr.bounds; tb.Expand(new Vector3(0.1f, 0.05f, 0));
                    if (HitTotemEdge(v.st, w) || tb.Contains(new Vector3(w.x, w.y, tb.center.z))) return v.st;
                    continue;
                }
                var sc = new Cell(v.st.x, v.st.y);
                if (c.Equals(sc)) return v.st;
                // das Bild ragt nach oben ueber das Feld hinaus
                if (PlantAt(c) == null && v.sr.bounds.Contains(new Vector3(w.x, w.y, v.sr.bounds.center.z))) return v.st;
            }
            return null;
        }

        public void TapStructure(StructureState st)
        {
            if (OpenStructure == st) UpgradeStructure(st, 1f, true);
            else
            {
                var v = structViews.Find(x => x.st == st);
                if (v != null) v.squash = Mathf.Max(v.squash, 0.5f);
                Sfx.Play("pop", 0.9f);
                Haptics.Tap(10);
            }
            StructureTapped?.Invoke(st);
        }

        public void UpgradeStructure(StructureState st, float power, bool loud)
        {
            var v = structViews.Find(x => x.st == st);
            if (v == null) return;
            var top = new Vector2(v.sr.bounds.center.x, v.sr.bounds.max.y);
            if (StructMaxed(st))
            {
                if (loud) { v.squash = 0.4f; Sfx.Play("dry", 1.2f, 0.4f); Floater?.Invoke(top + new Vector2(0, 0.3f), "Höchste Stufe!", new Color(1f, 0.9f, 0.6f)); }
                return;
            }
            float gain = Mathf.Max(1f, Mathf.Round(power * (loud ? UpgradeMult("click") : 1f)));
            st.xp += gain;
            v.squash = loud ? 1f : Mathf.Max(v.squash, 0.45f);
            if (loud)
            {
                Particles.I?.Burst(top, "spark", new Color(1f, 0.9f, 0.55f), 6, 2.4f, 0.3f, 0.1f, 4f, 1f);
                Sfx.Play("tick", UnityEngine.Random.Range(1.2f, 1.4f), 0.4f);
                Haptics.Tap(12);
                Floater?.Invoke(top + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.2f), "+" + gain, new Color(1f, 0.85f, 0.45f));
            }
            if (st.xp < StructTapsNeeded(st)) return;
            st.xp = 0;
            st.lv++;
            Shockwave(StructPos(st), 1.8f, 0.5f, new Color(1f, 0.85f, 0.45f, 0.9f));
            Particles.I?.Burst(top, "star", new Color(1f, 0.9f, 0.5f), 22, 3.5f, 0.9f, 0.3f, 1f, 1.5f);
            WorldVfx.Play("eat", top, 1.1f, 30010, null, 2f);
            WorldFx.Play("firework", top + new Vector2(0, 0.4f), 0.15f);
            Sfx.Play("levelup", 1.05f, 0.9f);
            Haptics.Tap(40);
            WorldPostFX.Punch(0.4f);
            StructureLeveled?.Invoke(v.def, st);
            save.Save();
        }

        // ------------------------------------------------------------------ Laufender Betrieb

        void UpdateStructures(float dt)
        {
            var nwI = NightWave.I;
            bool dark = nwI != null && (nwI.Active || nwI.Evening);
            foreach (var v in structViews)
            {
                v.squash = Mathf.MoveTowards(v.squash, 0f, dt * 4f);
                float sq = Mathf.Sin(v.squash * Mathf.PI * 1.5f) * v.squash * 0.14f;
                v.sr.transform.localScale = new Vector3(v.scale * (1 + sq), v.scale * (1 - sq), 1);
                if (IsTotem(v.def)) PlaceTotemPole(v);   // Flaeche kann sich aendern (alte Beet-Totems)
                if (v.light != null)
                {
                    float want = dark ? (v.def.effect == "light" ? 1.3f : 0.7f) : 0f;
                    v.light.intensity = Mathf.MoveTowards(v.light.intensity, want * (0.92f + 0.08f * Mathf.Sin(Time.time * 7f + v.st.x)), dt * 2f);
                }
                v.timer -= dt;
                if (v.timer > 0f) continue;
                v.timer = StructPulse(v, dark);
            }
        }

        /// <summary>Was ein Bauwerk regelmaessig tut. Liefert die Zeit bis zum naechsten Mal.</summary>
        float StructPulse(StructView v, bool dark)
        {
            var pos = StructPos(v.st);
            float r = StructRadius(v.st);
            switch (v.def.effect)
            {
                case "tower":
                    if (NightWave.I != null && NightWave.I.TowerShot(new Vector2(v.sr.bounds.center.x, v.sr.bounds.max.y - 0.1f), r, StructValue(v.st), v.st.moon ? 2 : 1))
                        v.squash = Mathf.Max(v.squash, 0.35f);
                    return Mathf.Max(0.4f, 1.3f - 0.04f * v.st.lv);
            }
            return 3f;
        }
    }

    /// <summary>Platzhalter-Bilder der Bauwerke, im Code gemalt (die Grafikpakete haben dafuer nichts Passendes).</summary>
    public static class StructureArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const int W = 128, H = 160;

        static readonly Color Wood = new Color(0.58f, 0.38f, 0.22f), WoodHi = new Color(0.8f, 0.58f, 0.35f);
        static readonly Color Stone = new Color(0.5f, 0.5f, 0.52f), StoneHi = new Color(0.75f, 0.75f, 0.76f);
        static readonly Color Roof = new Color(0.72f, 0.3f, 0.22f), RoofHi = new Color(0.9f, 0.48f, 0.32f);

        public static Sprite Get(string id)
        {
            if (cache.TryGetValue(id, out var s) && s != null) return s;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) px[y * W + x] = Paint(id, x, y) ?? Color.clear;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.02f), W);
            cache[id] = s;
            return s;
        }

        static Color? Paint(string id, int x, int y)
        {
            switch (id)
            {
                case "wachturm":
                    return Tri(x, y, 22, 118, 106, 118, 64, 158, Roof, RoofHi)
                        ?? Box(x, y, 26, 100, 102, 118, Wood, WoodHi)                                                       // Plattform
                        ?? Box(x, y, 34, 2, 44, 100, Wood, WoodHi) ?? Box(x, y, 84, 2, 94, 100, Wood, WoodHi)
                        ?? Line(x, y, 40, 10, 88, 90, 3, Wood) ?? Line(x, y, 88, 10, 40, 90, 3, Wood);
                case "wachturm_stone":
                    return Tri(x, y, 18, 118, 110, 118, 64, 158, new Color(0.35f, 0.4f, 0.5f), new Color(0.55f, 0.6f, 0.7f))   // Schieferdach
                        ?? Box(x, y, 58, 70, 70, 92, new Color(0.12f, 0.1f, 0.1f), new Color(0.2f, 0.18f, 0.16f))          // Schiessscharte
                        ?? Box(x, y, 24, 100, 104, 118, Stone, StoneHi, true)                                                // Zinnen
                        ?? Box(x, y, 32, 2, 96, 100, Stone, StoneHi, true);                                                  // Turm
                case "saatgutmuehle":
                    return Blades(x, y, 64, 112)
                        ?? Tri(x, y, 32, 96, 96, 96, 64, 120, Roof, RoofHi)
                        ?? Poly(x, y, new[] { new Vector2(28, 2), new Vector2(100, 2), new Vector2(90, 96), new Vector2(38, 96) }, new Color(0.88f, 0.84f, 0.74f), Color.white);
                case "knollihuette":
                    return Circle(x, y, 64, 34, 9, new Color(0.25f, 0.16f, 0.1f), new Color(0.35f, 0.24f, 0.15f))                  // Tuer
                        ?? Circle(x, y, 40, 60, 6, new Color(1f, 0.85f, 0.45f), new Color(1f, 0.95f, 0.7f))                        // Fenster
                        ?? Ellipse(x, y, 64, 96, 52, 30, new Color(0.45f, 0.62f, 0.3f), new Color(0.6f, 0.78f, 0.4f))              // Grasdach
                        ?? Ellipse(x, y, 64, 44, 46, 44, new Color(0.62f, 0.44f, 0.28f), new Color(0.78f, 0.6f, 0.4f));            // runder Erdbau
                case "totem_wachstum": return Totem(x, y, new Color(0.35f, 0.75f, 0.3f), new Color(0.7f, 1f, 0.55f));
                case "totem_ernte": return Totem(x, y, new Color(0.9f, 0.62f, 0.15f), new Color(1f, 0.88f, 0.4f));
                case "totem_fleiss": return Totem(x, y, new Color(0.45f, 0.4f, 0.85f), new Color(0.7f, 0.68f, 1f));
            }
            return Box(x, y, 20, 2, 108, 100, Wood, WoodHi);
        }

        /// <summary>Geschnitzter Pfahl mit zwei Gesichtern, Fluegeln und einem leuchtenden Zeichen in der Farbe seiner Wirkung.</summary>
        static Color? Totem(int x, int y, Color c, Color hi)
        {
            var dark = new Color(0.22f, 0.13f, 0.08f);
            return Circle(x, y, 64, 140, 13, c, hi)                                                                   // Zeichen
                ?? Tri(x, y, 14, 112, 114, 112, 64, 128, c * 0.85f, hi)                                                // Fluegel
                ?? Box(x, y, 52, 92, 60, 100, dark, dark) ?? Box(x, y, 68, 92, 76, 100, dark, dark)                   // Augen oben
                ?? Box(x, y, 54, 74, 74, 80, dark, dark)                                                               // Mund oben
                ?? Box(x, y, 40, 60, 88, 66, c, hi)                                                                    // Band
                ?? Box(x, y, 52, 42, 60, 50, dark, dark) ?? Box(x, y, 68, 42, 76, 50, dark, dark)                     // Augen unten
                ?? Box(x, y, 56, 24, 72, 32, c, hi)                                                                    // Mund unten
                ?? Box(x, y, 44, 2, 84, 112, Wood, WoodHi);                                                            // Pfahl
        }

        static Color Shade(Color c, Color hi, float k) { var col = Color.Lerp(c, hi, Mathf.Clamp01(k) * 0.8f); return col; }

        static Color? Box(int x, int y, float x0, float y0, float x1, float y1, Color c, Color hi, bool bricks = false,
                          Color? glow = null, float gx0 = 0, float gx1 = 0, float gy0 = 0, float gy1 = 0)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return null;
            if (glow.HasValue && x >= gx0 && x <= gx1 && y >= gy0 && y <= gy1) return Color.Lerp(glow.Value, Color.yellow, Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.6f);
            var col = Shade(c, hi, (y - y0) / Mathf.Max(1f, y1 - y0));
            if (bricks && ((y - (int)y0) % 14 < 2 || ((x + ((y - (int)y0) / 14 % 2) * 12) % 24 < 2))) col *= 0.75f;
            if (x - x0 < 2f || x1 - x < 2f || y - y0 < 2f || y1 - y < 2f) col *= 0.6f;
            col.a = c.a;
            return col;
        }

        static Color? Circle(int x, int y, float cx, float cy, float r, Color c, Color hi)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            if (d > r) return null;
            var col = Shade(c, hi, 1f - Vector2.Distance(new Vector2(x, y), new Vector2(cx - r * 0.3f, cy + r * 0.3f)) / (r * 1.5f));
            if (r - d < 1.8f) col *= 0.65f;
            col.a = 1f;
            return col;
        }

        static Color? Ellipse(int x, int y, float cx, float cy, float rx, float ry, Color c, Color hi)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            if (dx * dx + dy * dy > 1f) return null;
            return Shade(c, hi, 0.5f + 0.5f * Mathf.Sin(x * 0.3f + y * 0.5f));
        }

        static Color? Tri(int x, int y, float ax, float ay, float bx, float by, float tx, float ty, Color c, Color hi) =>
            Poly(x, y, new[] { new Vector2(ax, ay), new Vector2(bx, by), new Vector2(tx, ty) }, c, hi);

        static Color? Poly(int x, int y, Vector2[] pts, Color c, Color hi)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            if (!inside) return null;
            var col = Shade(c, hi, y / (float)H + 0.2f);
            col.a = c.a;
            return col;
        }

        static Color? Line(int x, int y, float x0, float y0, float x1, float y1, float w, Color c)
        {
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1); var p = new Vector2(x, y);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            return Vector2.Distance(p, a + (b - a) * t) <= w ? c * 0.85f : (Color?)null;
        }

        static Color? Blades(int x, int y, float cx, float cy)
        {
            var d = new Vector2(x - cx, y - cy);
            float len = d.magnitude;
            if (len > 58 || len < 3) return len < 6 ? new Color(0.35f, 0.25f, 0.15f) : (Color?)null;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 20f;
            float m = Mathf.Repeat(ang, 90f);
            if (m < 3f) return Wood;                               // Fluegelholm
            if (m < 16f && len > 14) return (len % 10 < 1.5f) ? new Color(0.7f, 0.65f, 0.55f) : new Color(0.96f, 0.94f, 0.88f);
            return null;
        }
    }
}
