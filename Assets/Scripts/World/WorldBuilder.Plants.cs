using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Angriffspflanzen auf den Feldern: Art waehlen, einsetzen, mit ~100 Tipps gross ziehen.
    /// Jeder Tipp soll Wucht haben: Treffer-Stopp, weisser Blitz, Feder-Stauchen, Bodenwelle, Erdbrocken,
    /// Knack + tiefer Schlag, Zoom-Stoss und Kamera-Ruck, Combo bei schnellem Tippen (Pflanze glueht auf),
    /// Meilensteine bei 25/50/75 % mit kurzem Stillstand und ein grosses Finale.
    /// Pflanzen werden mit eigenem Shader (Normal-Maps) plastisch schattiert und ragen ueber ihr Feld hinaus.
    /// Ausserdem das Pflanzenbuch (Stumpf neben dem Haus) mit allen freigeschalteten Arten.
    /// </summary>
    public partial class WorldBuilder
    {
        class PlantView
        {
            public Transform root, body; public SpriteRenderer sr, glow, shadow, barBack, barFill; public Light2D light;
            public Material mat;
            public float pop = 1f, barPunch, barShow;
            public float sy, syVel, tilt, tiltVel, freeze, flash, size = -1f, heat;   // Feder (Stauchen), Kippen, Treffer-Stopp, Blitz
            public float readyAt, nextHop, lift;   // reif: erst nach readyAt antippbar; Huepfer als Hinweis; angehoben beim Platzieren
            public bool readyShown = true;
            public int stage = -1;
        }

        struct Shock { public SpriteRenderer sr; public float t, dur, w; public int kind; }   // 0 Bodenwelle, 1 Strahlenkranz, 2 Lichtsaeule
        readonly List<Shock> shocks = new List<Shock>();
        static Sprite ringSprite, burstSprite;
        static readonly int[] Penta = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };   // Tonleiter fuer die Klick-Toene

        /// <summary>Jeder Wachstums-Tipp: Weltposition oben an der Pflanze, Combo, Hitze 0..1 (fuer den Combo-Zaehler).</summary>
        public event Action<Vector2, int, float> PlantPumped;
        /// <summary>Wartezeit, bis eine gerade reif gewordene Pflanze angetippt werden kann.</summary>
        public const float ReadyDelay = 2f;
        static Material plantMat;

        /// <summary>Gemeinsames Pflanzen-Material (plastische Schattierung ueber die Normal-Maps).</summary>
        public static Material PlantMaterialShared
        {
            get
            {
                if (plantMat != null) return plantMat;
                var sh = Shader.Find("SeedClicker/PlantLit2D");
                if (sh == null || !sh.isSupported) return null;
                plantMat = new Material(sh) { name = "PlantLit" };
                return plantMat;
            }
        }

        readonly Dictionary<Cell, PlantView> plantViews = new Dictionary<Cell, PlantView>();
        SpriteRenderer book;
        Vector2 bookPos;
        float bookSquash;
        int combo; float lastTapTime = -10f;

        public event Action<Cell> EmptyFieldTapped;
        public event Action BookTapped;
        public event Action<PlantedState> PlantGrown;
        public event Action InventoryChanged;
        public event Action<string> PlantUnlocked;

        public List<PlantedState> Planted => save.planted;
        public bool HasField(Cell c) => fields.ContainsKey(c);
        public static Vector2 CellCenterOf(Cell c) => CellCenter(c);
        public bool IsUnlocked(string id) => save.unlocked.Contains(id);
        public int Combo => combo;

        public PlantedState PlantAt(Cell c)
        {
            foreach (var p in save.planted) if (p.x == c.x && p.y == c.y) return p;
            return null;
        }

        public void NotifyInventory() { save.Save(); InventoryChanged?.Invoke(); }

        // ------------------------------------------------------------------ Freischalten

        /// <summary>Schaltet alle Arten frei, deren Welle erreicht ist. Gibt die neu freigeschalteten zurueck.</summary>
        public List<CatalogDef> UnlockForWave(int wavesSurvived)
        {
            var list = new List<CatalogDef>();
            foreach (var c in PlantBalance.I.catalog)
                if (c.unlockWave <= wavesSurvived && !IsUnlocked(c.id)) { save.unlocked.Add(c.id); list.Add(c); }   // auch nicht gewaehlte Start-Pflanzen (spaeter)
            if (list.Count > 0) { foreach (var c in list) PlantUnlocked?.Invoke(c.id); NotifyInventory(); }
            return list;
        }

        /// <summary>Naechste Art, die noch nicht freigeschaltet ist (fuer die Ziel-Anzeige).</summary>
        public CatalogDef NextUnlock()
        {
            CatalogDef best = null;
            foreach (var c in PlantBalance.I.catalog)
                if (!IsUnlocked(c.id) && (best == null || c.unlockWave < best.unlockWave)) best = c;
            return best;
        }

        public void ChooseStarter(string id)
        {
            if (!save.unlocked.Contains(id)) save.unlocked.Add(id);
            save.selectedPlant = id;
            save.starterChosen = true;
            NotifyInventory();
        }

        public void SelectPlant(string id) { if (IsUnlocked(id)) { save.selectedPlant = id; NotifyInventory(); } }

        // ------------------------------------------------------------------ Pflanzenbuch (Stumpf mit Saatgut)

        void PlaceWorkbench()
        {
            bookPos = HousePos + new Vector2(-3.5f, -0.9f);
            Shadow(bookPos, 1.6f, 0.4f, 0.3f);
            book = Place("sk_Tree_4", bookPos, 1.35f, false, 0.02f);
            Reserve(bookPos, 0.75f);
            float top = book != null ? book.bounds.max.y - 0.12f : bookPos.y + 0.4f;
            var bag1 = PlaceSized("hh_corn_seedbag", new Vector2(bookPos.x - 0.3f, top - 0.05f), 0.42f);
            var bag2 = PlaceSized("hh_carrot_seedbag", new Vector2(bookPos.x + 0.12f, top - 0.1f), 0.36f);
            var bag3 = PlaceSized("hh_wheat_seedbag", new Vector2(bookPos.x + 0.5f, top - 0.12f), 0.32f);
            foreach (var sr in new[] { bag1, bag2, bag3 }) if (sr != null && book != null) { sr.sortingOrder = Order(bookPos.y) + 1; sr.transform.SetParent(book.transform, true); }
        }

        public bool HitWorkbench(Vector2 w)
        {
            if (book == null) return false;
            var b = book.bounds; b.Expand(new Vector3(0.4f, 0.8f, 0));
            b.center += new Vector3(0, 0.3f, 0);
            return b.Contains(new Vector3(w.x, w.y, b.center.z));
        }

        public void TapWorkbench()
        {
            bookSquash = 1f;
            Sfx.Play("pop", 0.9f);
            Haptics.Tap(10);
            BookTapped?.Invoke();
        }

        // ------------------------------------------------------------------ Einsetzen / Ausgraben

        public bool PlantInto(Cell c, string type)
        {
            if (!HasField(c) || PlantAt(c) != null || !IsUnlocked(type)) return false;
            var p = new PlantedState { x = c.x, y = c.y, type = type, growth = 0 };
            save.planted.Add(p);
            CreatePlantView(p, true);
            var pos = CellCenterOf(c);
            Particles.I?.Burst(pos, "square", new Color(0.45f, 0.3f, 0.18f), 8, 2.2f, 0.45f, 0.7f, 6f, 1f);
            Sfx.Play("sow", UnityEngine.Random.Range(0.95f, 1.1f));
            Haptics.Tap(12);
            NotifyInventory();
            return true;
        }

        public void Unplant(PlantedState p)
        {
            var c = new Cell(p.x, p.y);
            RemovePlantView(c);
            save.planted.Remove(p);
            Particles.I?.Burst(CellCenterOf(c), "leaf", new Color(0.5f, 0.8f, 0.35f), 8, 2.5f, 0.6f, 0.5f, 4f, 1.5f);
            Sfx.Play("dig");
            NotifyInventory();
        }

        void RemovePlantView(Cell c)
        {
            if (plantViews.TryGetValue(c, out var v))
            {
                if (v.root != null) Destroy(v.root.gameObject);
                if (v.barBack != null) Destroy(v.barBack.gameObject);
                if (v.barFill != null) Destroy(v.barFill.gameObject);
                plantViews.Remove(c);
            }
        }

        SpriteRenderer WorldSprite(Transform parent, string name, Sprite s, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = s; sr.sortingOrder = order;
            return sr;
        }

        void CreatePlantView(PlantedState p, bool fx)
        {
            var c = new Cell(p.x, p.y);
            var g = p.Genome;
            var root = new GameObject("Plant_" + p.type).transform;
            root.SetParent(objects, false);
            root.position = CellCenterOf(c) + new Vector2(0, -CellSize * 0.38f);
            var shadow = WorldSprite(root, "Shadow", MistSprite, -31350);
            shadow.color = new Color(0.05f, 0.04f, 0.02f, 0.42f);
            var glow = WorldSprite(root, "Glow", MistSprite, -31400);
            glow.transform.localPosition = new Vector3(0, CellSize * 0.45f, 0);
            var body = new GameObject("Body").transform;   // Feder-Knoten: stauchen/kippen um den Fusspunkt
            body.SetParent(root, false);
            var sr = WorldSprite(body, "Sprite", null, Order(root.position.y));
            Material mat = null;
            if (PlantMaterialShared != null) { mat = new Material(PlantMaterialShared); sr.sharedMaterial = mat; }
            var barBack = WorldSprite(transform, "BarBack", Art.Get("white"), 31000);
            var barFill = WorldSprite(transform, "BarFill", Art.Get("white"), 31001);
            barBack.color = new Color(0.12f, 0.08f, 0.05f, 0.75f);
            var light = root.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 1.4f; light.pointLightInnerRadius = 0.05f;
            light.color = g.ElementColor; light.intensity = 0f;
            var v = new PlantView { root = root, body = body, sr = sr, shadow = shadow, glow = glow, light = light, mat = mat, barBack = barBack, barFill = barFill, pop = fx ? 0f : 1f };
            plantViews[c] = v;
            UpdatePlantSprite(p, v, true);
        }

        /// <summary>Breite der Pflanze: waechst mit jedem Tipp, bleibt aber innerhalb des Feldes
        /// (nur nach oben darf sie hinausragen).</summary>
        float PlantWidth(PlantedState p)
        {
            var sp = PlantBalance.I.Species(p.Genome.species);
            float f = p.growth / (float)Mathf.Max(1, PlantBalance.I.growTaps);
            return Mathf.Min(CellSize * 0.95f, CellSize * (p.Grown ? 1f : Mathf.Lerp(0.6f, 0.92f, f)) * sp.scale);
        }

        /// <summary>Streckung nach oben: je reifer, desto hoeher schiesst die Pflanze ueber ihr Feld.</summary>
        float PlantStretch(PlantedState p)
        {
            float f = p.growth / (float)Mathf.Max(1, PlantBalance.I.growTaps);
            return p.Grown ? 1.35f : Mathf.Lerp(1f, 1.25f, f);
        }

        void UpdatePlantSprite(PlantedState p, PlantView v, bool snap = false)
        {
            var g = p.Genome;
            var sp = PlantBalance.I.Species(g.species);
            float f = p.growth / (float)Mathf.Max(1, PlantBalance.I.growTaps);
            int stage = p.Grown ? 2 : f >= 0.5f ? 1 : 0;
            var sprite = art.Get(sp.art + sp.stages[Mathf.Clamp(stage, 0, sp.stages.Length - 1)]);
            if (stage != v.stage) { v.stage = stage; v.sr.sprite = sprite; }
            if (snap || v.size < 0f) v.size = PlantWidth(p);
            if (sprite != null)
            {
                float s = v.size / Mathf.Max(0.01f, sprite.bounds.size.x);
                float sy = s * PlantStretch(p);
                v.sr.transform.localScale = new Vector3(s, sy, 1);
                v.sr.transform.localPosition = new Vector3(0, -sprite.bounds.min.y * sy, 0);
            }
            v.shadow.transform.localScale = new Vector3(v.size * 1.0f, v.size * 0.3f, 1);
            v.glow.transform.localScale = new Vector3(v.size * 1.6f, v.size * 1.2f, 1);
            // leichter Farbstich der Wirkung, damit man die Arten unterscheidet
            v.sr.color = Color.Lerp(Color.white, g.ElementColor, 0.15f);
        }

        /// <summary>Tipp auf eine Pflanze: waechst ein Stueck (Klick-Wachstum) oder zeigt Infos, wenn sie fertig ist.
        /// Das ist die Hauptmechanik – jeder Tipp soll Wucht haben: Treffer-Stopp, Blitz, Federn, Bodenwelle,
        /// Erdbrocken, Zoom-Stoss, tiefer Schlag im Sound.</summary>
        public bool TapPlant(Cell c)
        {
            var p = PlantAt(c);
            if (p == null || !plantViews.TryGetValue(c, out var v)) return false;
            if (p.Grown)
            {
                if (Time.unscaledTime < v.readyAt)
                {
                    // noch gesperrt: nur ein dumpfes Wackeln
                    v.tiltVel += UnityEngine.Random.Range(-1f, 1f) * 25f;
                    Sfx.Play("dry", 0.7f, 0.4f);
                    return true;
                }
                if (carrying == p) CancelPlacing(); else StartPlacing(p);
                return true;
            }

            // Combo: schnelles Tippen macht es lauter, heller, spritziger
            float now = Time.unscaledTime;
            combo = now - lastTapTime <= PlantBalance.I.comboWindow ? combo + 1 : 1;
            lastTapTime = now;
            float heat = Mathf.Clamp01(combo / 30f);
            v.heat = Mathf.Max(v.heat, heat);

            p.growth++;
            int taps = PlantBalance.I.growTaps;
            float progress = p.growth / (float)taps;
            var g = p.Genome;
            var basePos = (Vector2)v.root.position;
            var top = basePos + new Vector2(0, (v.sr.bounds.max.y - basePos.y) * 0.75f);
            var P = Particles.I;

            // Koerper: hart stauchen, kurz einfrieren, weiss aufblitzen, zur Seite kippen
            v.sy = -0.26f - heat * 0.1f;
            v.syVel = 0f;
            v.freeze = 0.045f;
            v.flash = 0.85f;
            v.tiltVel += UnityEngine.Random.Range(-1f, 1f) * (35f + heat * 35f);
            v.barPunch = 1f;
            v.barShow = 2.5f;

            // Das Feld selbst bebt und blinkt auf
            PunchField(c, 0.8f + heat * 0.4f);

            // Bodenwelle und Erdbrocken (dunkel + hell), Blaetter oben, Funke am Treffpunkt
            Shockwave(basePos, v.size * (1.1f + heat * 0.5f), 0.22f, new Color(1f, 0.95f, 0.8f, 0.55f + heat * 0.25f));
            int clods = 5 + (int)(heat * 6);
            P?.Burst(basePos + new Vector2(0, 0.03f), "square", new Color(0.36f, 0.24f, 0.14f), clods, 2.2f + heat * 1.2f, 0.45f, 0.1f, 13f, 3.2f);
            P?.Burst(basePos + new Vector2(0, 0.03f), "square", new Color(0.62f, 0.46f, 0.3f), 2 + (int)(heat * 3), 1.8f + heat, 0.4f, 0.07f, 13f, 2.6f);
            P?.Burst(top, "leaf", new Color(0.55f, 0.85f, 0.35f), 1 + (int)(heat * 3), 1.6f + heat, 0.7f, 0.16f, 2.5f, 1.2f);
            P?.Spawn(top + new Vector2(UnityEngine.Random.Range(-0.1f, 0.1f), -0.1f), "spark", new Color(1f, 1f, 0.9f), Vector3.zero, 0.09f, 0.55f + heat * 0.3f, 0f, 0f);
            P?.Burst(top, "spark", Color.Lerp(Color.white, g.ElementColor, 0.6f), 2 + (int)(heat * 4), 2.6f + heat * 2f, 0.3f, 0.22f, 0f, 0.8f);

            // Strahlenkranz am Treffpunkt und Licht-Stoss im ganzen Bild
            Burst(top, v.size * (1.3f + heat * 0.8f), 0.13f, new Color(1f, 1f, 0.9f, 0.85f));
            WorldPostFX.Punch(0.18f + heat * 0.3f);
            PlantPumped?.Invoke(top, combo, heat);

            // Sound: Knack + tiefer Schlag + steigender Wachs-Ton + Ton aus der Tonleiter (Combo steigt hoch)
            Sfx.Play("note", Mathf.Pow(2f, Penta[(combo - 1) % Penta.Length] / 12f), 0.3f + heat * 0.15f);
            Sfx.Play("crunch", UnityEngine.Random.Range(0.85f, 1.15f), 1f);
            Sfx.Play("thump", UnityEngine.Random.Range(0.9f, 1.1f) + heat * 0.15f, 0.9f);
            Sfx.Play("grow", 0.8f + progress * 0.7f + heat * 0.3f, 0.4f);
            if (combo % 10 == 0) Sfx.Play("tick", 1f + heat * 0.5f, 0.7f);
            if (combo % 25 == 0) { Floater?.Invoke(top + new Vector2(0, 0.5f), "Combo ×" + combo, new Color(1f, 0.85f, 0.4f)); }
            Haptics.Tap(12 + (int)(heat * 12));
            WorldCamera.I?.Punch(0.012f + heat * 0.012f);
            WorldCamera.I?.Kick(Vector2.down * (0.7f + heat * 0.8f));
            WorldCamera.I?.Shake(0.008f + heat * 0.012f);

            // Meilensteine: kurzer Stillstand der ganzen Welt, grosse Welle
            foreach (var m in PlantBalance.I.growMilestones)
                if (p.growth == m)
                {
                    v.pop = 0.5f;
                    v.sy = -0.4f;
                    HitStop(0.07f);
                    Shockwave(basePos, v.size * 2.6f, 0.4f, Color.white);
                    P?.Burst(top, "star", new Color(1f, 0.95f, 0.6f), 14, 3f, 0.7f, 0.3f, 1f, 1f);
                    Sfx.Play("unlock", 0.9f + m / 100f, 0.8f);
                    Haptics.Tap(40);
                    WorldCamera.I?.Shake(0.06f);
                    WorldCamera.I?.Punch(0.035f);
                    Floater?.Invoke(top + new Vector2(0, 0.6f), m + " %", new Color(0.8f, 1f, 0.6f));
                }

            UpdatePlantSprite(p, v);
            if (p.Grown) Ripe(p, v, basePos, top);
            save.Save();
            return true;
        }

        /// <summary>Die Pflanze ist reif: der groesste Moment des Spiels. Stillstand, Zeitlupe, Lichtsaeule,
        /// drei Bodenwellen, Sternenregen, Fanfare. Danach 2 Sekunden Sperre, dann antippen zum Platzieren.</summary>
        void Ripe(PlantedState p, PlantView v, Vector2 basePos, Vector2 top)
        {
            var g = p.Genome;
            var P = Particles.I;
            v.pop = 0f;
            v.flash = 1f;
            v.sy = 0.45f;
            v.readyAt = Time.unscaledTime + ReadyDelay;
            v.readyShown = false;
            StartCoroutine(RipeSlowMo());
            Pillar(basePos, v.size * 1.6f, g.ElementColor);
            Shockwave(basePos, v.size * 4.5f, 0.6f, Color.white);
            Shockwave(basePos, v.size * 3f, 0.45f, g.ElementColor);
            Shockwave(basePos, v.size * 6.5f, 0.85f, new Color(1f, 0.95f, 0.7f, 0.6f));
            Burst(top, v.size * 3.5f, 0.3f, Color.white);
            P?.Burst(top, "star", g.ElementColor, 30, 4.2f, 1f, 0.42f, 1.5f, 2f);
            P?.Burst(top, "star", new Color(1f, 0.95f, 0.6f), 20, 3f, 1.2f, 0.3f, 1f, 2.5f);
            P?.Burst(basePos, "leaf", new Color(0.55f, 0.85f, 0.35f), 18, 3.5f, 1f, 0.22f, 3f, 2f);
            P?.Burst(basePos, "square", new Color(0.4f, 0.28f, 0.16f), 16, 3.5f, 0.6f, 0.11f, 12f, 3.5f);
            Sfx.Play("levelup", 1.05f, 1f);
            Sfx.Play("boom", 1.6f, 0.55f);
            Sfx.Play("thump", 0.6f, 1f);
            Sfx.Play("unlock", 1.3f, 0.8f);
            Haptics.Tap(90);
            WorldCamera.I?.Shake(0.18f);
            WorldCamera.I?.Punch(0.08f);
            WorldCamera.I?.Kick(Vector2.down * 3f);
            WorldPostFX.Punch(1.2f);
            PlantGrown?.Invoke(p);
            combo = 0;
        }

        System.Collections.IEnumerator RipeSlowMo()
        {
            if (hitStopping) yield break;
            hitStopping = true;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(0.16f);
            // Zeitlupe, die langsam wieder auf normale Geschwindigkeit kommt
            for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.7f)
            {
                Time.timeScale = Mathf.Lerp(0.25f, 1f, k * k);
                yield return null;
            }
            Time.timeScale = 1f;
            hitStopping = false;
        }

        /// <summary>Lichtsaeule, die aus dem Himmel auf die Pflanze faellt.</summary>
        void Pillar(Vector2 at, float width, Color col)
        {
            var sr = new GameObject("Pillar").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = at + new Vector2(0, 3.5f);
            sr.sprite = MistSprite;
            sr.color = new Color(Mathf.Lerp(col.r, 1f, 0.6f), Mathf.Lerp(col.g, 1f, 0.6f), Mathf.Lerp(col.b, 1f, 0.6f), 0.9f);
            sr.sortingOrder = 31500;
            shocks.Add(new Shock { sr = sr, t = 0f, dur = 1.3f, w = width, kind = 2 });
        }

        /// <summary>Strahlenkranz (Speedlines) am Treffpunkt.</summary>
        void Burst(Vector2 at, float size, float dur, Color col)
        {
            var sr = new GameObject("Burst").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            sr.sprite = BurstSprite;
            sr.color = col;
            sr.sortingOrder = 31400;
            shocks.Add(new Shock { sr = sr, t = 0f, dur = dur, w = size, kind = 1 });
        }

        static Sprite BurstSprite
        {
            get
            {
                if (burstSprite != null) return burstSprite;
                const int n = 128; const int rays = 12;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
                        float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * rays;
                        float f = Mathf.Abs(ang - Mathf.Round(ang));                  // 0 = Mitte eines Strahls
                        float width = Mathf.Lerp(0.16f, 0.03f, d);                     // spitz nach aussen
                        float a = Mathf.Clamp01((width - f) * 40f) * Mathf.Clamp01((d - 0.35f) * 6f) * Mathf.Clamp01((1f - d) * 8f);
                        px[y * n + x] = new Color(1, 1, 1, a);
                    }
                tex.SetPixels(px); tex.Apply();
                burstSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return burstSprite;
            }
        }

        /// <summary>Kurzer Stillstand der Welt (Treffer-Gefuehl bei grossen Momenten).</summary>
        void HitStop(float seconds)
        {
            if (hitStopping) return;
            StartCoroutine(HitStopRoutine(seconds));
        }
        bool hitStopping;
        System.Collections.IEnumerator HitStopRoutine(float seconds)
        {
            hitStopping = true;
            float old = Time.timeScale;
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = old <= 0.02f ? 1f : old;
            hitStopping = false;
        }

        void UpdateRipe(PlantedState p, PlantView v, float udt)
        {
            float now = Time.unscaledTime;
            var top = new Vector2(v.root.position.x, v.sr.bounds.max.y);
            if (now < v.readyAt)
            {
                // Sperre: es funkelt nach oben, die Pflanze "laedt auf"
                if (UnityEngine.Random.value < udt * 25f)
                    Particles.I?.Spawn((Vector2)v.root.position + new Vector2(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(0f, 0.6f)), "spark",
                        Color.Lerp(Color.white, p.Genome.ElementColor, 0.5f), new Vector3(0, 1.4f, 0), 0.5f, 0.18f, 0f, 0f);
                return;
            }
            if (!v.readyShown)
            {
                v.readyShown = true;
                v.sy = 0.35f; v.flash = 0.7f;
                Burst(top, v.size * 2f, 0.2f, new Color(1f, 0.95f, 0.6f, 0.9f));
                Particles.I?.Burst(top, "star", new Color(1f, 0.95f, 0.6f), 10, 2.5f, 0.6f, 0.25f, 1f, 1.5f);
                Sfx.Play("tick", 1.5f, 0.8f);
                Sfx.Play("note", 2f, 0.4f);
                Haptics.Tap(25);
                Floater?.Invoke(top + new Vector2(0, 0.5f), "Antippen zum Platzieren!", new Color(1f, 0.95f, 0.6f));
                v.nextHop = now + 1.6f;
            }
            // als Hinweis regelmaessig ein kleiner Huepfer mit Glitzer
            if (carrying != p && now >= v.nextHop)
            {
                v.nextHop = now + 1.6f;
                v.sy = 0.18f;
                Particles.I?.Burst(top, "spark", new Color(1f, 0.95f, 0.7f), 3, 1.2f, 0.5f, 0.15f, 0f, 0.8f);
            }
        }

        /// <summary>Flache Bodenwelle (Ellipse in Draufsicht), die schnell auseinanderlaeuft.</summary>
        void Shockwave(Vector2 at, float width, float dur, Color col)
        {
            var sr = new GameObject("Shock").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = at;
            sr.sprite = RingSprite;
            sr.color = col;
            sr.sortingOrder = -31340;
            shocks.Add(new Shock { sr = sr, t = 0f, dur = dur, w = width });
        }

        void UpdateShocks(float dt)
        {
            for (int i = shocks.Count - 1; i >= 0; i--)
            {
                var s = shocks[i];
                s.t += dt;
                float k = Mathf.Clamp01(s.t / s.dur);
                if (k >= 1f || s.sr == null) { if (s.sr != null) Destroy(s.sr.gameObject); shocks.RemoveAt(i); continue; }
                float e = 1f - Mathf.Pow(1f - k, 3f);
                if (s.kind == 2)
                {
                    // Lichtsaeule: schiesst schmal herab, wird breit, verblasst
                    float wp = s.w * Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) * (1f - k * 0.6f);
                    s.sr.transform.localScale = new Vector3(Mathf.Max(0.01f, wp), 9f * Mathf.Min(1f, k * 6f), 1);
                    var cp = s.sr.color; cp.a = (1f - k) * 0.85f; s.sr.color = cp;
                }
                else
                {
                    float w = Mathf.Lerp(s.w * (s.kind == 1 ? 0.5f : 0.25f), s.w, e);
                    s.sr.transform.localScale = new Vector3(w, s.kind == 1 ? w : w * 0.42f, 1);
                    var c = s.sr.color; c.a = Mathf.Min(c.a, (1f - k) * 0.9f); s.sr.color = c;
                }
                shocks[i] = s;
            }
        }

        static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);
                        a = a * a * (3f - 2f * a);
                        a += Mathf.Clamp01(1f - d) * 0.12f;   // leichte Fuellung
                        px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                    }
                tex.SetPixels(px); tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return ringSprite;
            }
        }

        public bool IsPlantGrowing(Cell c) { var p = PlantAt(c); return p != null && !p.Grown; }

        public void TapEmptyField(Cell c) { if (PlantAt(c) == null) EmptyFieldTapped?.Invoke(c); }

        void LoadPlants()
        {
            if (save.starterChosen && !string.IsNullOrEmpty(save.selectedPlant) && !save.unlocked.Contains(save.selectedPlant)) save.unlocked.Add(save.selectedPlant);
            UnlockForWave(save.wave);
            for (int i = save.planted.Count - 1; i >= 0; i--)
            {
                var p = save.planted[i];
                if (HasField(new Cell(p.x, p.y)) && PlantBalance.I.Plant(p.type) != null) CreatePlantView(p, false);
                else save.planted.RemoveAt(i);
            }
        }

        void UpdatePlants(float dt)
        {
            float t = Time.time;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // Klick-Gefuehl laeuft auch im Treffer-Stopp
            float nightAmt = NightWave.I != null ? NightWave.I.NightAmount : 0f;
            if (Time.unscaledTime - lastTapTime > PlantBalance.I.comboWindow * 2f) combo = 0;
            UpdateShocks(udt);
            foreach (var p in save.planted)
            {
                var c = new Cell(p.x, p.y);
                if (!plantViews.TryGetValue(c, out var v)) continue;
                v.pop = Mathf.MoveTowards(v.pop, 1f, udt * 3f);
                v.barPunch = Mathf.MoveTowards(v.barPunch, 0, udt * 6f);
                v.flash = Mathf.MoveTowards(v.flash, 0f, udt * 9f);
                v.heat = Mathf.MoveTowards(v.heat, Time.unscaledTime - lastTapTime < 0.5f ? v.heat : 0f, udt * 1.5f);
                v.barShow -= udt;
                // Groesse pumpt schnell auf den neuen Wert
                float target = PlantWidth(p);
                if (Mathf.Abs(v.size - target) > 0.001f) { v.size = Mathf.Lerp(v.size, target, udt * 16f); UpdatePlantSprite(p, v); }
                // Feder: nach dem Treffer-Stopp elastisch zurueckschwingen
                if (v.freeze > 0f) v.freeze -= udt;
                else
                {
                    v.syVel += (-v.sy * 700f - v.syVel * 15f) * udt;
                    v.sy += v.syVel * udt;
                    v.tiltVel += (-v.tilt * 300f - v.tiltVel * 12f) * udt;
                    v.tilt += v.tiltVel * udt;
                }
                if (p.Grown) UpdateRipe(p, v, udt);
                float pop = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float sway = Mathf.Sin(t * 1.6f + p.x * 0.7f + p.y) * 1f;
                v.lift = Mathf.MoveTowards(v.lift, carrying == p ? 0.18f + Mathf.Sin(Time.unscaledTime * 6f) * 0.04f : 0f, udt * 2f);
                v.body.localPosition = new Vector3(0, v.lift, 0);
                v.body.localScale = new Vector3((1f - v.sy * 0.75f) * pop, (1f + v.sy) * pop, 1);
                v.body.localRotation = Quaternion.Euler(0, 0, sway + v.tilt);
                v.shadow.color = new Color(0.05f, 0.04f, 0.02f, 0.42f * (1f + v.sy * 0.8f));
                if (v.mat != null) v.mat.SetFloat("_Flash", v.flash);
                var g = p.Genome;
                var col = g.ElementColor;
                float glowA = p.Grown ? 0.22f + 0.1f * Mathf.Sin(t * 2f + p.x) + nightAmt * 0.2f : v.heat * 0.45f;
                v.glow.color = new Color(col.r, col.g, col.b, glowA);
                v.light.intensity = p.Grown ? nightAmt * (0.9f + 0.2f * Mathf.Sin(t * 3f + p.y)) : v.heat * 0.8f;

                // Fortschrittsbalken ueber der Pflanze (waehrend des Wachsens)
                bool showBar = !p.Grown && (v.barShow > 0f || p.growth > 0);
                v.barBack.enabled = v.barFill.enabled = showBar;
                if (showBar)
                {
                    float f = p.growth / (float)PlantBalance.I.growTaps;
                    var top = new Vector2(v.root.position.x, v.sr.bounds.max.y + 0.1f);
                    float w = CellSize * 0.85f, h = 0.06f * (1 + v.barPunch * 0.8f);
                    v.barBack.transform.position = top; v.barBack.transform.localScale = new Vector3(w + 0.04f, h + 0.04f, 1);
                    v.barFill.transform.position = top + new Vector2(-w * 0.5f * (1 - f), 0); v.barFill.transform.localScale = new Vector3(w * f, h, 1);
                    v.barFill.color = Color.Lerp(new Color(0.55f, 0.85f, 0.35f), new Color(1f, 0.85f, 0.35f), f) * (1 + v.barPunch * 0.5f);
                }
            }

            if (book != null)
            {
                bookSquash = Mathf.MoveTowards(bookSquash, 0, dt * 4f);
                float sq = Mathf.Sin(bookSquash * Mathf.PI * 1.5f) * bookSquash * 0.12f;
                book.transform.localScale = new Vector3(1.35f * (1 + sq), 1.35f * (1 - sq), 1);
            }
        }

        public Vector2 PlantMuzzle(PlantedState p) => CellCenterOf(new Cell(p.x, p.y)) + new Vector2(0, CellSize * 0.5f);

        public void KickPlant(PlantedState p) { if (plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) v.sy = Mathf.Min(v.sy, -0.12f); }
    }
}
