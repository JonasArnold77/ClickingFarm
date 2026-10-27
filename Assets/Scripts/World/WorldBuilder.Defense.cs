using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Verteidigung bauen: Zaeune aus Holz (im Feldraster, verbinden sich automatisch, Zombies muessen sie
    /// erst kaputtschlagen) und das eingezaeunte Startbeet. Blumentoepfe: siehe WorldBuilder.Pots.cs.
    /// </summary>
    public partial class WorldBuilder
    {
        class FenceView { public FenceState st; public SpriteRenderer sr; public float shake, pop = 1f; }

        readonly Dictionary<Cell, FenceView> fenceViews = new Dictionary<Cell, FenceView>();
        float noWoodT;


        BuildDef Build => PlantBalance.I.build ?? new BuildDef { fenceWood = 1, fenceHp = 10, potWood = 2 };
        public int FenceCost => Build.fenceWood;

        // ------------------------------------------------------------------ Eingezaeuntes Startbeet

        /// <summary>Innenflaeche des Startbeets (Welteinheiten). Felder gehen anfangs nur hier.</summary>
        public static readonly Rect StartArea = new Rect(-4.5f, -0.9f, 3.6f, 2.7f);

        /// <summary>Darf an dieser Stelle ein Feld liegen? (Spaeter: weitere Bereiche freischalten.)</summary>
        public static bool InFarmArea(Vector2 p) => StartArea.Contains(p);

        /// <summary>Beim ersten Start: Zaun rund um das Startbeet, oben ein Tor Richtung Haus.</summary>
        void BuildEnclosure()
        {
            var a = FenceCellAt(StartArea.min + new Vector2(0.01f, 0.01f));
            var b = FenceCellAt(StartArea.max - new Vector2(0.01f, 0.01f));
            int x0 = a.x - 1, x1 = b.x + 1, y0 = a.y - 1, y1 = b.y + 1;
            int gate = b.x;   // Tor oben rechts, zum Haus hin
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    if (x != x0 && x != x1 && y != y0 && y != y1) continue;
                    if (y == y1 && x == gate) continue;
                    var c = new Cell(x, y);
                    if (fenceViews.ContainsKey(c)) continue;
                    var st = new FenceState { x = x, y = y, hp = Build.fenceHp };
                    save.fences.Add(st);
                    PlaceFence(c, false, st);
                }
            save.enclosureBuilt = true;
            save.Save();
        }

        void LoadDefense()
        {
            foreach (var f in save.fences) PlaceFence(new Cell(f.x, f.y), false, f);
            if (!save.enclosureBuilt) BuildEnclosure();
            LoadPots();
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
    }
}
