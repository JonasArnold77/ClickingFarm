using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Angriffspflanzen auf den Feldern: einpflanzen, durch Tippen gross ziehen (Klick-Wachstum),
    /// ausgraben. Ausgewachsene Pflanzen leuchten in der Farbe ihrer Wirkung und verteidigen nachts.
    /// Ausserdem die Werkbank zum Veredeln und Kreuzen.
    /// </summary>
    public partial class WorldBuilder
    {
        class PlantView { public Transform root; public SpriteRenderer sr, glow; public Light2D light; public float squash, pop; public int stage = -1; }

        readonly Dictionary<int, PlantView> plantViews = new Dictionary<int, PlantView>();
        SpriteRenderer workbench;
        Vector2 workbenchPos;
        float workbenchSquash;

        public event Action<Cell> EmptyFieldTapped;
        public event Action<PlantGenome> PlantTapped;
        public event Action WorkbenchTapped;
        public event Action<PlantGenome> PlantGrown;
        public event Action InventoryChanged;

        public IEnumerable<PlantGenome> Plants => save.plants;
        public bool HasField(Cell c) => fields.ContainsKey(c);
        public static Vector2 CellCenterOf(Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        public PlantGenome PlantAt(Cell c)
        {
            foreach (var p in save.plants) if (p.planted && p.cellX == c.x && p.cellY == c.y) return p;
            return null;
        }

        public void NotifyInventory() { save.Save(); InventoryChanged?.Invoke(); }

        // ------------------------------------------------------------------ Werkbank

        void PlaceWorkbench()
        {
            workbenchPos = HousePos + new Vector2(-3.5f, -0.9f);
            Shadow(workbenchPos, 1.6f, 0.4f, 0.3f);
            workbench = Place("sk_Tree_4", workbenchPos, 1.35f, false, 0.02f);
            Reserve(workbenchPos, 0.75f);
            // Werkzeug und Saatgut auf dem Stumpf
            float top = workbench != null ? workbench.bounds.max.y - 0.12f : workbenchPos.y + 0.4f;
            var bag1 = PlaceSized("hh_corn_seedbag", new Vector2(workbenchPos.x - 0.3f, top - 0.05f), 0.42f);
            var bag2 = PlaceSized("hh_carrot_seedbag", new Vector2(workbenchPos.x + 0.12f, top - 0.1f), 0.36f);
            var hoe = PlaceSized("hh_hoe_icon", new Vector2(workbenchPos.x + 0.55f, workbenchPos.y - 0.05f), 0.75f);
            foreach (var sr in new[] { bag1, bag2, hoe }) if (sr != null) { sr.sortingOrder = Order(workbenchPos.y) + 1; sr.transform.SetParent(workbench.transform, true); }
            if (hoe != null) hoe.transform.rotation = Quaternion.Euler(0, 0, -12f);
        }

        public bool HitWorkbench(Vector2 w)
        {
            if (workbench == null) return false;
            var b = workbench.bounds; b.Expand(new Vector3(0.4f, 0.8f, 0));
            b.center += new Vector3(0, 0.3f, 0);
            return b.Contains(new Vector3(w.x, w.y, b.center.z));
        }

        public void TapWorkbench()
        {
            workbenchSquash = 1f;
            Sfx.Play("pop", 0.9f);
            Haptics.Tap(10);
            WorkbenchTapped?.Invoke();
        }

        // ------------------------------------------------------------------ Pflanzen setzen / ausgraben

        public void PlantInto(Cell c, PlantGenome g)
        {
            if (!HasField(c) || PlantAt(c) != null || g.planted) return;
            g.planted = true; g.cellX = c.x; g.cellY = c.y; g.growth = 0;
            CreatePlantView(g, true);
            Particles.I?.Burst(CellCenterOf(c), "square", new Color(0.45f, 0.3f, 0.18f), 8, 2.2f, 0.45f, 0.9f, 6f, 1f);
            Sfx.Play("sow");
            Haptics.Tap(12);
            NotifyInventory();
        }

        public void Unplant(PlantGenome g)
        {
            if (!g.planted) return;
            var pos = CellCenterOf(new Cell(g.cellX, g.cellY));
            RemovePlantView(g.uid);
            g.planted = false; g.growth = 0;
            Particles.I?.Burst(pos, "leaf", new Color(0.5f, 0.8f, 0.35f), 8, 2.5f, 0.6f, 0.6f, 4f, 1.5f);
            Sfx.Play("dig");
            NotifyInventory();
        }

        /// <summary>Pflanze aus dem Spielstand entfernen (z. B. beim Kreuzen als Spender verbraucht).</summary>
        public void RemovePlant(PlantGenome g)
        {
            RemovePlantView(g.uid);
            save.plants.Remove(g);
            NotifyInventory();
        }

        /// <summary>Pflanze ersetzen (Ergebnis einer Kreuzung), Feldposition bleibt.</summary>
        public void ReplacePlant(PlantGenome old, PlantGenome neu)
        {
            int i = save.plants.IndexOf(old);
            RemovePlantView(old.uid);
            if (i >= 0) save.plants[i] = neu; else save.plants.Add(neu);
            if (neu.planted) CreatePlantView(neu, true);
            NotifyInventory();
        }

        public void RefreshPlant(PlantGenome g) { if (g.planted) { RemovePlantView(g.uid); CreatePlantView(g, true); } }

        public PlantGenome AddPlant(PlantGenome g) { g.uid = save.nextUid++; save.plants.Add(g); NotifyInventory(); return g; }
        public int NextUid() => save.nextUid++;

        void RemovePlantView(int uid)
        {
            if (plantViews.TryGetValue(uid, out var v)) { if (v.root != null) Destroy(v.root.gameObject); plantViews.Remove(uid); }
        }

        void CreatePlantView(PlantGenome g, bool fx)
        {
            var c = new Cell(g.cellX, g.cellY);
            var root = new GameObject("Plant_" + g.uid).transform;
            root.SetParent(objects, false);
            root.position = CellCenterOf(c) + new Vector2(0, -0.32f);
            var glowGo = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glowGo.transform.SetParent(root, false);
            glowGo.sprite = MistSprite;
            glowGo.transform.localPosition = new Vector3(0, 0.25f, 0);
            glowGo.transform.localScale = new Vector3(1.3f, 0.75f, 1);
            glowGo.sortingOrder = -31400;
            var sr = new GameObject("Body").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.sortingOrder = Order(root.position.y);
            var light = root.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 1.6f; light.pointLightInnerRadius = 0.1f;
            light.color = g.ElementColor; light.intensity = 0f;
            var v = new PlantView { root = root, sr = sr, glow = glowGo, light = light, pop = fx ? 0f : 1f };
            plantViews[g.uid] = v;
            UpdatePlantSprite(g, v);
        }

        void UpdatePlantSprite(PlantGenome g, PlantView v)
        {
            var sp = PlantBalance.I.Species(g.species);
            float f = g.growth / (float)Mathf.Max(1, PlantBalance.I.growTaps);
            int stage = g.Grown ? 2 : f >= 0.5f ? 1 : 0;
            if (stage == v.stage) return;
            v.stage = stage;
            var sprite = art.Get(sp.art + sp.stages[Mathf.Clamp(stage, 0, sp.stages.Length - 1)]);
            v.sr.sprite = sprite;
            if (sprite != null)
            {
                float width = (stage == 2 ? 0.95f : stage == 1 ? 0.75f : 0.5f) * sp.scale;
                float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
                v.sr.transform.localScale = new Vector3(s, s, 1);
                v.sr.transform.localPosition = new Vector3(0, -sprite.bounds.min.y * s, 0);
            }
            var col = g.ElementColor;
            v.glow.color = new Color(col.r, col.g, col.b, g.Grown ? 0.35f : 0f);
        }

        /// <summary>Tipp auf eine Pflanze: waechst ein Stueck (Klick-Wachstum) oder zeigt Infos, wenn sie fertig ist.</summary>
        public bool TapPlant(Cell c)
        {
            var g = PlantAt(c);
            if (g == null) return false;
            if (!plantViews.TryGetValue(g.uid, out var v)) return false;
            if (g.Grown) { v.squash = 0.6f; PlantTapped?.Invoke(g); return true; }
            g.growth++;
            v.squash = 1f;
            var pos = CellCenterOf(c);
            Particles.I?.Burst(pos + new Vector2(0, 0.1f), "leaf", new Color(0.55f, 0.85f, 0.35f), 3, 1.8f, 0.5f, 0.5f, 3f, 1.2f);
            float progress = g.growth / (float)PlantBalance.I.growTaps;
            Sfx.Play("grow", 0.9f + progress * 0.8f, 0.8f);
            Haptics.Tap(6);
            UpdatePlantSprite(g, v);
            if (g.Grown)
            {
                v.pop = 0f;
                Particles.I?.Burst(pos + new Vector2(0, 0.4f), "star", g.ElementColor, 14, 3.5f, 0.7f, 0.8f, 1f, 1f);
                Particles.I?.Ring(pos, Color.white, 1.4f);
                Sfx.Play("levelup", 1.1f, 0.7f);
                Haptics.Tap(35);
                Floater?.Invoke(pos + new Vector2(0, 1f), g.name + " ist bereit!", g.ElementColor);
                PlantGrown?.Invoke(g);
            }
            save.Save();
            return true;
        }

        public void TapEmptyField(Cell c) { if (PlantAt(c) == null) EmptyFieldTapped?.Invoke(c); }

        public bool IsPlantGrowing(Cell c) { var g = PlantAt(c); return g != null && !g.Grown; }

        void LoadPlants()
        {
            foreach (var g in save.plants)
                if (g.planted)
                {
                    if (HasField(new Cell(g.cellX, g.cellY))) CreatePlantView(g, false);
                    else g.planted = false;
                }
        }

        void UpdatePlants(float dt)
        {
            float t = Time.time;
            bool night = NightWave.I != null && NightWave.I.NightAmount > 0.01f;
            float nightAmt = NightWave.I != null ? NightWave.I.NightAmount : 0f;
            foreach (var g in save.plants)
            {
                if (!g.planted || !plantViews.TryGetValue(g.uid, out var v)) continue;
                v.squash = Mathf.MoveTowards(v.squash, 0, dt * 4f);
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 3f);
                float sq = Mathf.Sin(v.squash * Mathf.PI * 1.5f) * v.squash * 0.22f;
                float pop = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float sway = Mathf.Sin(t * 1.6f + g.uid) * 0.025f;
                v.root.localScale = new Vector3((1 + sq * 0.8f) * pop, (1 - sq) * pop, 1);
                v.root.localRotation = Quaternion.Euler(0, 0, sway * 40f);
                if (g.Grown)
                {
                    var col = g.ElementColor;
                    v.glow.color = new Color(col.r, col.g, col.b, 0.22f + 0.1f * Mathf.Sin(t * 2f + g.uid) + nightAmt * 0.2f);
                    v.light.intensity = nightAmt * (0.9f + 0.2f * Mathf.Sin(t * 3f + g.uid));
                }
                else v.light.intensity = 0f;
            }

            // Werkbank wippt beim Antippen
            if (workbench != null)
            {
                workbenchSquash = Mathf.MoveTowards(workbenchSquash, 0, dt * 4f);
                float sq = Mathf.Sin(workbenchSquash * Mathf.PI * 1.5f) * workbenchSquash * 0.12f;
                workbench.transform.localScale = new Vector3(1.35f * (1 + sq), 1.35f * (1 - sq), 1);
            }
        }

        /// <summary>Weltposition einer gepflanzten Pflanze (Schussursprung).</summary>
        public Vector2 PlantMuzzle(PlantGenome g) => CellCenterOf(new Cell(g.cellX, g.cellY)) + new Vector2(0, 0.35f);

        /// <summary>Kleiner Rueckstoss-Wackler beim Schiessen.</summary>
        public void KickPlant(PlantGenome g) { if (plantViews.TryGetValue(g.uid, out var v)) v.squash = Mathf.Max(v.squash, 0.5f); }
    }
}
