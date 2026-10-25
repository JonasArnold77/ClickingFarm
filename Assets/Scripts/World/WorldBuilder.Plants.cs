using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Angriffspflanzen auf den Feldern: Art waehlen, einsetzen, mit ~100 Tipps gross ziehen.
    /// Jeder Tipp soll sich "crunchy" anfuehlen: Stauchen, Kruemel, Funken, Knack-Sound mit steigender
    /// Tonhoehe, Vibration, Mini-Wackler, pulsierender Fortschrittsbalken, Combo bei schnellem Tippen,
    /// Meilensteine bei 25/50/75 % und ein grosses Finale.
    /// Ausserdem das Pflanzenbuch (Stumpf neben dem Haus) mit allen freigeschalteten Arten.
    /// </summary>
    public partial class WorldBuilder
    {
        class PlantView
        {
            public Transform root; public SpriteRenderer sr, glow, barBack, barFill; public Light2D light;
            public float squash, pop = 1f, tilt, barPunch, barShow; public int stage = -1;
        }

        readonly Dictionary<Cell, PlantView> plantViews = new Dictionary<Cell, PlantView>();
        SpriteRenderer book;
        Vector2 bookPos;
        float bookSquash;
        int combo; float lastTapTime = -10f;

        public event Action<Cell> EmptyFieldTapped;
        public event Action<PlantedState> PlantTapped;
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
            root.position = CellCenterOf(c) + new Vector2(0, -CellSize * 0.32f);
            var glow = WorldSprite(root, "Glow", MistSprite, -31400);
            glow.transform.localPosition = new Vector3(0, CellSize * 0.25f, 0);
            glow.transform.localScale = new Vector3(CellSize * 1.4f, CellSize * 0.8f, 1);
            var sr = WorldSprite(root, "Body", null, Order(root.position.y));
            var barBack = WorldSprite(transform, "BarBack", Art.Get("white"), 31000);
            var barFill = WorldSprite(transform, "BarFill", Art.Get("white"), 31001);
            barBack.color = new Color(0.12f, 0.08f, 0.05f, 0.75f);
            var light = root.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 1.2f; light.pointLightInnerRadius = 0.05f;
            light.color = g.ElementColor; light.intensity = 0f;
            var v = new PlantView { root = root, sr = sr, glow = glow, light = light, barBack = barBack, barFill = barFill, pop = fx ? 0f : 1f };
            plantViews[c] = v;
            UpdatePlantSprite(p, v);
        }

        void UpdatePlantSprite(PlantedState p, PlantView v)
        {
            var g = p.Genome;
            var sp = PlantBalance.I.Species(g.species);
            float f = p.growth / (float)Mathf.Max(1, PlantBalance.I.growTaps);
            int stage = p.Grown ? 2 : f >= 0.5f ? 1 : 0;
            // Groesse waechst stufenlos mit jedem Tipp mit
            float width = CellSize * (p.Grown ? 1.1f : Mathf.Lerp(0.8f, 1.0f, f)) * sp.scale;
            var sprite = art.Get(sp.art + sp.stages[Mathf.Clamp(stage, 0, sp.stages.Length - 1)]);
            if (stage != v.stage) { v.stage = stage; v.sr.sprite = sprite; }
            if (sprite != null)
            {
                float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
                v.sr.transform.localScale = new Vector3(s, s, 1);
                v.sr.transform.localPosition = new Vector3(0, -sprite.bounds.min.y * s, 0);
            }
            // leichter Farbstich der Wirkung, damit man die Arten unterscheidet
            var col = g.ElementColor;
            v.sr.color = Color.Lerp(Color.white, col, 0.18f);
            v.glow.color = new Color(col.r, col.g, col.b, p.Grown ? 0.35f : 0f);
        }

        /// <summary>Tipp auf eine Pflanze: waechst ein Stueck (Klick-Wachstum) oder zeigt Infos, wenn sie fertig ist.</summary>
        public bool TapPlant(Cell c)
        {
            var p = PlantAt(c);
            if (p == null || !plantViews.TryGetValue(c, out var v)) return false;
            if (p.Grown) { v.squash = 0.6f; Sfx.Play("pop", 1.3f, 0.6f); PlantTapped?.Invoke(p); return true; }

            // Combo: schnelles Tippen macht es lauter, heller, spritziger
            float now = Time.unscaledTime;
            combo = now - lastTapTime <= PlantBalance.I.comboWindow ? combo + 1 : 1;
            lastTapTime = now;
            float heat = Mathf.Clamp01(combo / 30f);

            p.growth++;
            int taps = PlantBalance.I.growTaps;
            float progress = p.growth / (float)taps;
            var g = p.Genome;
            var pos = CellCenterOf(c);
            var P = Particles.I;

            v.squash = 1f;
            v.tilt = UnityEngine.Random.Range(-1f, 1f);
            v.barPunch = 1f;
            v.barShow = 2.5f;
            // Kruemel und Blaetter
            P?.Burst(pos + new Vector2(0, -0.05f), "square", new Color(0.42f, 0.28f, 0.16f), 3 + (int)(heat * 3), 1.5f + heat * 0.6f, 0.4f, 0.26f, 9f, 1.5f);
            P?.Burst(pos + new Vector2(0, 0.15f), "leaf", new Color(0.55f, 0.85f, 0.35f), 1 + (int)(heat * 2), 1.4f, 0.55f, 0.2f, 3f, 1f);
            P?.Spawn(pos + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.35f), "spark", Color.Lerp(Color.white, g.ElementColor, 0.6f), new Vector3(0, 1.1f, 0), 0.3f, 0.45f + heat * 0.35f, 0f, 0f);
            if (heat > 0.5f && combo % 4 == 0) P?.Ring(pos, new Color(1f, 1f, 1f, 0.15f + heat * 0.2f), 0.3f + heat * 0.2f, 0.2f);
            // Knack + Ton, der mit dem Fortschritt und der Combo steigt
            Sfx.Play("crunch", UnityEngine.Random.Range(0.9f, 1.1f), 0.9f);
            Sfx.Play("grow", 0.8f + progress * 0.7f + heat * 0.3f, 0.45f);
            if (combo % 10 == 0) Sfx.Play("tick", 1f + heat * 0.5f, 0.7f);
            if (combo % 25 == 0) { Floater?.Invoke(pos + new Vector2(0, 0.8f), "Combo ×" + combo, new Color(1f, 0.85f, 0.4f)); }
            Haptics.Tap(heat > 0.5f ? 14 : 8);
            WorldCamera.I?.Shake(0.012f + heat * 0.015f);

            // Meilensteine
            foreach (var m in PlantBalance.I.growMilestones)
                if (p.growth == m)
                {
                    v.pop = 0.4f;
                    P?.Burst(pos + new Vector2(0, 0.3f), "star", new Color(1f, 0.95f, 0.6f), 10, 2.2f, 0.6f, 0.35f, 1f, 1f);
                    P?.Ring(pos, Color.white, 0.6f, 0.35f);
                    Sfx.Play("unlock", 0.9f + m / 100f, 0.7f);
                    Haptics.Tap(30);
                    WorldCamera.I?.Shake(0.05f);
                    Floater?.Invoke(pos + new Vector2(0, 1.1f), m + " %", new Color(0.8f, 1f, 0.6f));
                }

            UpdatePlantSprite(p, v);
            if (p.Grown)
            {
                v.pop = 0f;
                P?.Burst(pos + new Vector2(0, 0.4f), "star", g.ElementColor, 22, 3.2f, 0.8f, 0.45f, 1f, 1f);
                P?.Burst(pos, "leaf", new Color(0.55f, 0.85f, 0.35f), 14, 3f, 0.8f, 0.3f, 3f, 1f);
                P?.Ring(pos, Color.white, 1.4f, 0.45f);
                Sfx.Play("levelup", 1.05f, 0.8f);
                Haptics.Tap(60);
                WorldCamera.I?.Shake(0.12f);
                Floater?.Invoke(pos + new Vector2(0, 1.2f), g.name + " ist bereit!", g.ElementColor);
                PlantGrown?.Invoke(p);
                combo = 0;
            }
            save.Save();
            return true;
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
            float nightAmt = NightWave.I != null ? NightWave.I.NightAmount : 0f;
            if (Time.unscaledTime - lastTapTime > PlantBalance.I.comboWindow * 2f) combo = 0;
            foreach (var p in save.planted)
            {
                var c = new Cell(p.x, p.y);
                if (!plantViews.TryGetValue(c, out var v)) continue;
                v.squash = Mathf.MoveTowards(v.squash, 0, dt * 5f);
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 3f);
                v.barPunch = Mathf.MoveTowards(v.barPunch, 0, dt * 6f);
                v.barShow -= dt;
                float sq = Mathf.Sin(v.squash * Mathf.PI * 2f) * v.squash * 0.3f;
                float pop = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float sway = Mathf.Sin(t * 1.6f + p.x * 0.7f + p.y) * 1.2f;
                v.root.localScale = new Vector3((1 + sq * 0.9f) * pop, (1 - sq) * pop, 1);
                v.root.localRotation = Quaternion.Euler(0, 0, sway + v.tilt * v.squash * 9f);
                var g = p.Genome;
                if (p.Grown)
                {
                    var col = g.ElementColor;
                    v.glow.color = new Color(col.r, col.g, col.b, 0.22f + 0.1f * Mathf.Sin(t * 2f + p.x) + nightAmt * 0.2f);
                    v.light.intensity = nightAmt * (0.9f + 0.2f * Mathf.Sin(t * 3f + p.y));
                }
                else v.light.intensity = 0f;

                // Fortschrittsbalken ueber der Pflanze (waehrend des Wachsens)
                bool showBar = !p.Grown && (v.barShow > 0f || p.growth > 0);
                v.barBack.enabled = v.barFill.enabled = showBar;
                if (showBar)
                {
                    float f = p.growth / (float)PlantBalance.I.growTaps;
                    var top = CellCenterOf(c) + new Vector2(0, CellSize * 1.15f);
                    float w = CellSize * 0.85f, h = 0.06f * (1 + v.barPunch * 0.6f);
                    v.barBack.transform.position = top; v.barBack.transform.localScale = new Vector3(w + 0.04f, h + 0.04f, 1);
                    v.barFill.transform.position = top + new Vector2(-w * 0.5f * (1 - f), 0); v.barFill.transform.localScale = new Vector3(w * f, h, 1);
                    v.barFill.color = Color.Lerp(new Color(0.55f, 0.85f, 0.35f), new Color(1f, 0.85f, 0.35f), f) * (1 + v.barPunch * 0.4f);
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

        public void KickPlant(PlantedState p) { if (plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) v.squash = Mathf.Max(v.squash, 0.5f); }
    }
}
