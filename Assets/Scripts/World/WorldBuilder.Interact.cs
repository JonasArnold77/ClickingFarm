using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    public enum WorldTool { Axe, Field }

    [Serializable] public struct Cell { public int x, y; public Cell(int x, int y) { this.x = x; this.y = y; } }

    /// <summary>Gespeicherter Fortschritt der freien Welt.</summary>
    [Serializable]
    public class WorldSave
    {
        public int wood;
        public List<int> felled = new List<int>();       // gefaellte Baeume (Stumpf steht noch)
        public List<int> stumpsGone = new List<int>();   // auch der Stumpf ist weg
        public List<Cell> fields = new List<Cell>();

        const string Key = "seedclicker_world_v1";
        public static WorldSave Load()
        {
            try { if (PlayerPrefs.HasKey(Key)) return JsonUtility.FromJson<WorldSave>(PlayerPrefs.GetString(Key)) ?? new WorldSave(); }
            catch (Exception e) { Debug.LogWarning("Weltstand nicht lesbar: " + e.Message); }
            return new WorldSave();
        }
        public void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static void Delete() { PlayerPrefs.DeleteKey(Key); }
    }

    /// <summary>
    /// Interaktion mit der Welt: Baeume durch viele Tipps faellen (danach bleibt ein Stumpf,
    /// der sich ebenfalls entfernen laesst) und Felder im Rastermuster anlegen.
    /// </summary>
    public partial class WorldBuilder
    {
        public const int TreeHpBig = 8, TreeHpSmall = 5, StumpHp = 3;
        public const int WoodBig = 5, WoodSmall = 3, WoodStump = 1;

        class TreeObj
        {
            public int id, occ; public bool big;
            public Transform pivot; public SpriteRenderer sr, stump;
            public Vector2 foot;
            public int hp, maxHp, stumpHp;
            public float shake, fallT = -1f, fallDir = 1f, pop = 1f, hpShowT;
            public bool gone;
        }

        class Decor { public Vector2 p; public GameObject go; public int occ; }

        readonly List<TreeObj> trees = new List<TreeObj>();
        readonly List<Decor> decor = new List<Decor>();
        readonly Dictionary<Cell, SpriteRenderer> fields = new Dictionary<Cell, SpriteRenderer>();
        readonly List<(Transform t, float k)> popping = new List<(Transform, float)>();
        WorldSave save = new WorldSave();
        SpriteRenderer hpBack, hpFill, cellCursor;
        TreeObj hpTree;
        Sprite fieldSprite;
        float cursorT;

        public int Wood => save.wood;
        public event Action<int> WoodChanged;
        public event Action<Vector2, string, Color> Floater;   // Weltposition, Text, Farbe

        // ------------------------------------------------------------------ Aufbau

        void RegisterTree(SpriteRenderer sr, Vector2 foot, bool big, int occ)
        {
            if (sr == null) return;
            var pivot = new GameObject("Tree").transform;
            pivot.SetParent(objects, false);
            pivot.position = foot;
            sr.transform.SetParent(pivot, true);   // Drehpunkt am Stammfuss: wackeln und umfallen
            int hp = big ? TreeHpBig : TreeHpSmall;
            trees.Add(new TreeObj { id = trees.Count, pivot = pivot, sr = sr, foot = foot, big = big, occ = occ, hp = hp, maxHp = hp });
        }

        void AddDecor(SpriteRenderer sr, Vector2 p, int occ)
        {
            if (sr != null) decor.Add(new Decor { p = p, go = sr.gameObject, occ = occ });
        }

        void LoadWorldSave()
        {
            save = WorldSave.Load();
            foreach (var id in save.felled)
                if (id >= 0 && id < trees.Count) FellImmediate(trees[id], save.stumpsGone.Contains(id));
            foreach (var c in save.fields) PlaceField(c, false, false);
        }

        void FellImmediate(TreeObj t, bool stumpGone)
        {
            if (t.sr != null) Destroy(t.sr.gameObject);
            t.sr = null;
            t.hp = 0;
            if (stumpGone) RemoveStump(t, false);
            else CreateStump(t, false);
        }

        // ------------------------------------------------------------------ Baeume faellen

        /// <summary>Tipp mit der Axt: trifft den vordersten Baum oder Stumpf unter dem Finger.</summary>
        public bool TryChop(Vector2 world)
        {
            TreeObj best = null; int bestOrder = int.MinValue;
            foreach (var t in trees)
            {
                if (t.gone || t.fallT >= 0) continue;
                SpriteRenderer target = t.sr != null ? t.sr : t.stump;
                if (target == null) continue;
                var b = target.bounds;
                // Baumkrone etwas schmaler werten, Stumpf etwas grosszuegiger
                if (t.sr != null) b.Expand(new Vector3(-b.size.x * 0.15f, 0, 0)); else b.Expand(0.25f);
                if (!b.Contains(new Vector3(world.x, world.y, b.center.z))) continue;
                if (target.sortingOrder > bestOrder) { bestOrder = target.sortingOrder; best = t; }
            }
            if (best == null) return false;
            Chop(best, world);
            return true;
        }

        void Chop(TreeObj t, Vector2 hitPoint)
        {
            var P = Particles.I;
            if (t.sr == null)
            {
                // Stumpf ausgraben
                t.stumpHp--;
                t.pop = 0.75f;
                P?.Burst(t.foot + new Vector2(0, 0.15f), "square", new Color(0.55f, 0.38f, 0.22f), 6, 2.2f, 0.45f, 0.9f, 6f, 1.5f);
                Sfx.Play("chop", UnityEngine.Random.Range(0.8f, 0.95f));
                Haptics.Tap(10);
                if (t.stumpHp <= 0)
                {
                    RemoveStump(t, true);
                    AddWood(WoodStump, t.foot + new Vector2(0, 0.4f));
                    save.stumpsGone.Add(t.id);
                    save.Save();
                }
                return;
            }

            t.hp--;
            t.shake = 1f;
            t.hpShowT = 2f;
            hpTree = t;
            var trunk = t.foot + new Vector2(0, 0.35f);
            P?.Burst(trunk, "square", new Color(0.82f, 0.62f, 0.38f), 5, 2.6f, 0.5f, 0.9f, 7f, 1.5f);
            var canopy = (Vector2)t.sr.bounds.center + new Vector2(0, t.sr.bounds.extents.y * 0.3f);
            for (int i = 0; i < 3; i++)
                P?.Spawn(canopy + UnityEngine.Random.insideUnitCircle * 0.6f, "leaf", new Color(0.45f, 0.72f, 0.3f), new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), -0.3f, 0), 1.4f, 0.55f, 0.4f, 0.8f, 240f, 0.5f);
            Sfx.Play("chop", UnityEngine.Random.Range(0.95f, 1.1f));
            Haptics.Tap(12);
            WorldCamera.I?.Shake(0.02f);

            if (t.hp <= 0)
            {
                t.fallT = 0f;
                t.fallDir = hitPoint.x < t.foot.x ? 1f : -1f;   // faellt vom Spieler weg
                Sfx.Play("break", 0.8f);
            }
        }

        void CreateStump(TreeObj t, bool fx)
        {
            var sr = Place("sk_Tree_4", t.foot + new Vector2(0, -0.05f), StumpSize(t), t.id % 2 == 0, 0.02f);
            if (sr == null) return;
            t.stump = sr;
            t.stumpHp = StumpHp;
            if (fx) { sr.transform.localScale *= 0.01f; popping.Add((sr.transform, 0f)); }
        }

        void RemoveStump(TreeObj t, bool fx)
        {
            if (fx && t.stump != null)
            {
                Particles.I?.Burst(t.foot + new Vector2(0, 0.15f), "square", new Color(0.5f, 0.36f, 0.22f), 14, 3.5f, 0.6f, 1f, 7f, 2f);
                Particles.I?.Ring(t.foot, new Color(1, 1, 1, 0.7f), 0.8f);
                Sfx.Play("break", 1.1f);
                Haptics.Tap(25);
            }
            if (t.stump != null) Destroy(t.stump.gameObject);
            t.stump = null;
            t.gone = true;
            if (t.occ >= 0 && t.occ < occupiedR.Count) occupiedR[t.occ] = 0f; // Platz ist jetzt frei
        }

        void AddWood(int n, Vector2 at)
        {
            save.wood += n;
            WoodChanged?.Invoke(save.wood);
            Floater?.Invoke(at, "+" + n + " Holz", new Color(1f, 0.9f, 0.6f));
            Sfx.Play("coin", 1.1f, 0.7f);
        }

        // ------------------------------------------------------------------ Felder im Raster

        public static Cell CellAt(Vector2 world) => new Cell(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        static Vector2 CellCenter(Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        public bool CanPlaceField(Cell c)
        {
            if (fields.ContainsKey(c)) return false;
            var center = CellCenter(c);
            foreach (var corner in new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) })
            {
                var p = center + corner;
                if (Inside(p) < 0.35f) return false;                         // nicht an die Klippe
                if (Mathf.Abs(p.x) > MapW / 2f - 0.5f || Mathf.Abs(p.y) > MapH / 2f - 0.5f) return false;
            }
            if (DistToPath(center) < 0.85f) return false;                    // nicht auf den Weg
            if (MaskAt(center).a > 0.15f) return false;                      // nicht aufs Pflaster
            // Baeume, Stuempfe, Felsen, Haus blockieren; Blumen/Gras/Buesche werden weggeraeumt
            for (int i = 0; i < occupied.Count; i++)
            {
                if (occupiedR[i] <= 0f || IsDecorOcc(i)) continue;
                float d = 0.42f + occupiedR[i] * 0.75f;
                if ((occupied[i] - center).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        bool IsDecorOcc(int occ) { foreach (var d in decor) if (d.occ == occ) return true; return false; }

        /// <summary>Legt ein Feld an (Rasterzelle). Gibt false zurueck, wenn der Platz nicht geht.</summary>
        public bool PlaceField(Cell c, bool fx, bool persist = true)
        {
            if (!CanPlaceField(c)) return false;
            var center = CellCenter(c);
            // Blumen, Gras, Pilze, Buesche in der Zelle wegraeumen
            for (int i = decor.Count - 1; i >= 0; i--)
            {
                var d = decor[i];
                if (Mathf.Abs(d.p.x - center.x) > 0.75f || Mathf.Abs(d.p.y - center.y) > 0.75f) continue;
                if (d.go != null) Destroy(d.go);
                if (d.occ >= 0 && d.occ < occupiedR.Count) occupiedR[d.occ] = 0f;
                decor.RemoveAt(i);
            }
            if (fieldSprite == null) fieldSprite = GroundMaterials.TileSprite(4, 3);
            var go = new GameObject("Field");
            go.transform.SetParent(objects, false);
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fieldSprite;
            sr.sortingOrder = -32300;
            fields[c] = sr;
            if (fx)
            {
                go.transform.localScale = Vector3.one * 0.01f;
                popping.Add((go.transform, 0f));
                Particles.I?.Burst(center, "square", new Color(0.45f, 0.3f, 0.18f), 9, 2.4f, 0.5f, 1f, 6f, 1.2f);
                Sfx.Play("dig", UnityEngine.Random.Range(0.9f, 1.1f));
                Haptics.Tap(10);
            }
            if (persist) { save.fields.Add(c); save.Save(); }
            return true;
        }

        /// <summary>Markiert die Zelle unter dem Finger (gruen = geht, rot = geht nicht).</summary>
        public void ShowCellCursor(Cell c)
        {
            if (cellCursor == null)
            {
                cellCursor = new GameObject("CellCursor").AddComponent<SpriteRenderer>();
                cellCursor.transform.SetParent(transform, false);
                cellCursor.sprite = art.Get("hh_slot_selected");
                cellCursor.sortingOrder = 32000;
            }
            var s = cellCursor.sprite;
            if (s != null) cellCursor.transform.localScale = Vector3.one * (1.0f / s.bounds.size.x);
            cellCursor.transform.position = CellCenter(c);
            bool ok = CanPlaceField(c) || fields.ContainsKey(c);
            cellCursor.color = ok ? new Color(0.75f, 1f, 0.7f, 0.55f) : new Color(1f, 0.5f, 0.45f, 0.55f);
            cellCursor.enabled = true;
            cursorT = 0.6f;
        }

        // ------------------------------------------------------------------ Animation pro Frame

        void UpdateInteract(float dt)
        {
            float time = Time.time;
            foreach (var t in trees)
            {
                if (t.sr != null && t.fallT < 0f && t.shake > 0f)
                {
                    t.shake = Mathf.MoveTowards(t.shake, 0f, dt * 3.5f);
                    t.pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 42f) * t.shake * 4f);
                }
                if (t.fallT >= 0f && t.sr != null)
                {
                    t.fallT += dt / 0.75f;
                    float k = Mathf.Clamp01(t.fallT);
                    t.pivot.localRotation = Quaternion.Euler(0, 0, -t.fallDir * 88f * k * k);
                    var col = t.sr.color; col.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; t.sr.color = col;
                    if (t.fallT >= 1f)
                    {
                        var land = t.foot + new Vector2(t.fallDir * (t.big ? 2.2f : 1.5f), 0.2f);
                        Particles.I?.Burst(land, "leaf", new Color(0.45f, 0.72f, 0.3f), 16, 3.5f, 0.9f, 0.6f, 4f, 1.5f);
                        Particles.I?.Burst(land, "square", new Color(0.75f, 0.68f, 0.5f), 10, 3f, 0.6f, 1f, 5f, 1f);
                        WorldCamera.I?.Shake(0.1f);
                        Haptics.Tap(30);
                        Destroy(t.sr.gameObject);
                        t.sr = null;
                        t.fallT = -1f;   // jetzt ist der Stumpf dran
                        t.pivot.localRotation = Quaternion.identity;
                        CreateStump(t, true);
                        AddWood(t.big ? WoodBig : WoodSmall, t.foot + new Vector2(0, 1f));
                        save.felled.Add(t.id);
                        save.Save();
                    }
                }
                if (t.stump != null && t.pop < 1f)
                {
                    t.pop = Mathf.MoveTowards(t.pop, 1f, dt * 4f);
                    float sq = Mathf.Sin(t.pop * Mathf.PI) * 0.15f;
                    float sx = StumpSize(t);
                    t.stump.transform.localScale = new Vector3(Mathf.Sign(t.stump.transform.localScale.x) * sx * (1 + sq), sx * (1 - sq), 1);
                }
            }

            // Einblenden mit kleinem Ueberschwingen (Felder, neue Stuempfe)
            for (int i = popping.Count - 1; i >= 0; i--)
            {
                var (tr, k) = popping[i];
                if (tr == null) { popping.RemoveAt(i); continue; }
                k = Mathf.Min(1f, k + dt * 4f);
                float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                bool isStump = tr.name == "sk_Tree_4";
                var target = isStump ? StumpScale(tr) : Vector3.one;
                tr.localScale = new Vector3(target.x * e, target.y * e, 1);
                popping[i] = (tr, k);
                if (k >= 1f) { tr.localScale = target; popping.RemoveAt(i); }
            }

            // Lebensbalken ueber dem Baum, der gerade bearbeitet wird
            UpdateHpBar(dt);
            if (cellCursor != null && cursorT > 0f) { cursorT -= dt; if (cursorT <= 0f) cellCursor.enabled = false; }
        }

        static float StumpSize(TreeObj t) => t.big ? 1.0f : 0.8f;

        Vector3 StumpScale(Transform tr)
        {
            foreach (var t in trees) if (t.stump != null && t.stump.transform == tr) { float s = StumpSize(t); return new Vector3(t.id % 2 == 0 ? -s : s, s, 1); }
            return Vector3.one;
        }

        void UpdateHpBar(float dt)
        {
            if (hpBack == null)
            {
                hpBack = new GameObject("HpBack").AddComponent<SpriteRenderer>();
                hpFill = new GameObject("HpFill").AddComponent<SpriteRenderer>();
                hpBack.transform.SetParent(transform, false); hpFill.transform.SetParent(transform, false);
                hpBack.sprite = hpFill.sprite = Art.Get("white");
                hpBack.color = new Color(0.15f, 0.1f, 0.06f, 0.8f);
                hpFill.color = new Color(1f, 0.82f, 0.35f);
                hpBack.sortingOrder = 31000; hpFill.sortingOrder = 31001;
            }
            bool show = hpTree != null && hpTree.sr != null && hpTree.fallT < 0f && hpTree.hpShowT > 0f && hpTree.hp < hpTree.maxHp;
            if (hpTree != null) hpTree.hpShowT -= dt;
            hpBack.enabled = hpFill.enabled = show;
            if (!show) return;
            float f = hpTree.hp / (float)hpTree.maxHp;
            var top = hpTree.foot + new Vector2(0, 1.7f);
            hpBack.transform.position = top; hpBack.transform.localScale = new Vector3(0.9f, 0.13f, 1);
            hpFill.transform.position = top + new Vector2(-0.42f * (1 - f), 0); hpFill.transform.localScale = new Vector3(0.84f * f, 0.08f, 1);
        }

        /// <summary>Spielstand der Welt loeschen (Bäume wachsen nach dem Neustart wieder, Felder sind weg).</summary>
        public static void ResetWorldSave() => WorldSave.Delete();
    }
}
