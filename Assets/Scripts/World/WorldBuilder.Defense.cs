using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Mauern aus Holz: Im Mauer-Modus (Knopf unten links) schimmern freie Hof-Zellen als helle Holzplaetze; ein Tipp
    /// baut ein Mauerstueck (kostet Holz), ein Tipp auf eine leere Mauer baut sie wieder ab (Holz zurueck).
    /// Auf jedem Mauerstueck kann eine Pflanze stehen – Tipp auf eine leere Mauer (ausserhalb des Modus) fragt, welche.
    /// Zombies muessen Mauern erst kaputtschlagen; steht eine Pflanze darauf, faellt sie mit.
    /// </summary>
    public partial class WorldBuilder
    {
        class WallView { public WallState st; public SpriteRenderer sr, edge; public float shake, pop = 1f; }
        readonly Dictionary<Cell, WallView> walls = new Dictionary<Cell, WallView>();
        static Sprite wallSprite;
        bool wallMode;

        public bool WallMode => wallMode;
        public event System.Action<bool> WallModeChanged;

        public bool HasWall(Cell c) => walls.ContainsKey(c);
        float WallMaxHp => PlantBalance.I.Wall.hp * UpgradeMult("wall");

        public void SetWallMode(bool on)
        {
            wallMode = on;
            RefreshGhosts();
            WallModeChanged?.Invoke(on);
        }

        void LoadDefense()
        {
            foreach (var w in save.walls) CreateWallView(w, false);
            RefreshGhosts();
        }

        public bool CanPlaceWall(Cell c) => !HasWall(c) && !fields.ContainsKey(c) && CanPlaceField(c);

        /// <summary>Tipp im Mauer-Modus: Mauer bauen bzw. leere Mauer wieder abbauen.</summary>
        public bool TapWallMode(Cell c)
        {
            if (walls.TryGetValue(c, out var v))
            {
                if (PlantAt(c) != null) return false;
                RemoveWall(c, v, false);
                AddWood(PlantBalance.I.Wall.wood, CellCenter(c) + new Vector2(0, 0.5f));
                return true;
            }
            if (!CanPlaceWall(c)) return false;
            int cost = PlantBalance.I.Wall.wood;
            if (save.wood < cost)
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Zu wenig Holz – fälle Bäume!", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                return true;
            }
            save.wood -= cost; WoodChanged?.Invoke(save.wood);
            var st = new WallState { x = c.x, y = c.y, hp = WallMaxHp };
            save.walls.Add(st);
            CreateWallView(st, true);
            RefreshGhosts();
            save.Save();
            return true;
        }

        void CreateWallView(WallState st, bool fx)
        {
            var c = new Cell(st.x, st.y);
            var sr = new GameObject("Wall").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.transform.position = CellCenter(c);
            sr.sprite = WallSprite;
            sr.sortingOrder = -32299;
            // Vorderkante: macht die Mauer etwas erhaben
            var edge = new GameObject("WallEdge").AddComponent<SpriteRenderer>();
            edge.transform.SetParent(sr.transform, false);
            edge.sprite = Art.Get("white");
            edge.color = new Color(0.45f, 0.3f, 0.17f);
            edge.sortingOrder = -32298;
            var v = new WallView { st = st, sr = sr, edge = edge, pop = fx ? 0f : 1f };
            walls[c] = v;
            ClearDecorAround(CellCenter(c), CellSize * 0.6f);
            if (fx)
            {
                Particles.I?.Burst(CellCenter(c), "square", new Color(0.8f, 0.62f, 0.4f), 8, 2.2f, 0.45f, 0.08f, 7f, 1.4f);
                Sfx.Play("chop", UnityEngine.Random.Range(1.1f, 1.25f), 0.8f);
                Haptics.Tap(10);
            }
        }

        void RemoveWall(Cell c, WallView v, bool broken)
        {
            var center = CellCenter(c);
            Particles.I?.Burst(center + new Vector2(0, 0.1f), "square", new Color(0.78f, 0.6f, 0.38f), broken ? 16 : 8, broken ? 3.5f : 2.5f, 0.6f, 0.1f, 7f, 1.5f);
            Sfx.Play("break", broken ? 0.9f : 1.2f, broken ? 0.9f : 0.6f);
            if (broken) WorldCamera.I?.Shake(0.06f);
            var p = PlantAt(c);
            if (p != null) Unplant(p);   // die Pflanze faellt mit
            if (v.sr != null) Destroy(v.sr.gameObject);
            walls.Remove(c);
            save.walls.Remove(v.st);
            RefreshGhosts();
            save.Save();
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

        /// <summary>Mauer an dieser Stelle (fuer die Zombies), sonst null.</summary>
        public WallState WallAt(Vector2 pos) => walls.TryGetValue(CellAt(pos), out var v) ? v.st : null;

        /// <summary>Zombie schlaegt auf eine Mauer ein.</summary>
        public void HitWall(WallState st, float dmg)
        {
            var c = new Cell(st.x, st.y);
            if (!walls.TryGetValue(c, out var v)) return;
            st.hp -= dmg;
            v.shake = 1f;
            if (UnityEngine.Random.value < 0.25f)
                Particles.I?.Spawn(CellCenter(c) + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.1f), "square", new Color(0.78f, 0.6f, 0.38f),
                    new Vector3(UnityEngine.Random.Range(-1f, 1f), 1.5f, 0), 0.5f, 0.07f, 0f, 6f);
            if (st.hp <= 0f) RemoveWall(c, v, true);
        }

        void UpdateWalls(float dt)
        {
            foreach (var kv in walls)
            {
                var v = kv.Value;
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 4f);
                v.shake = Mathf.MoveTowards(v.shake, 0f, dt * 4f);
                float e = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float s = CellSize / Mathf.Max(0.01f, v.sr.sprite.bounds.size.x);
                v.sr.transform.localScale = Vector3.one * s * e;
                v.sr.transform.position = (Vector3)CellCenter(kv.Key) + (Vector3)(UnityEngine.Random.insideUnitCircle * v.shake * 0.03f);
                // Vorderkante als duenner Streifen unter dem Platz
                float w = v.sr.sprite.bounds.size.x;
                v.edge.transform.localPosition = new Vector3(0, -w * 0.5f - w * 0.06f, 0);
                v.edge.transform.localScale = new Vector3(w / Mathf.Max(0.001f, v.edge.sprite.bounds.size.x), w * 0.12f / Mathf.Max(0.001f, v.edge.sprite.bounds.size.y), 1);
                float hp = Mathf.Clamp01(v.st.hp / Mathf.Max(1f, WallMaxHp));
                v.sr.color = Color.Lerp(new Color(0.6f, 0.5f, 0.45f), Color.white, hp);
            }
        }

        /// <summary>Heller Holzplatz (Bretter), im Code gemalt.</summary>
        public static Sprite WallSprite
        {
            get
            {
                if (wallSprite != null) return wallSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                var light = new Color(0.93f, 0.8f, 0.58f); var mid = new Color(0.84f, 0.68f, 0.46f); var dark = new Color(0.55f, 0.38f, 0.22f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int plank = y / (n / 3);
                        float py = (y % (n / 3)) / (float)(n / 3);
                        float grain = Mathf.PerlinNoise(x * 0.08f + plank * 7f, y * 0.6f) * 0.5f + Mathf.PerlinNoise(x * 0.3f, y * 0.1f + plank) * 0.2f;
                        var c = Color.Lerp(mid, light, grain + (plank == 1 ? 0.1f : 0f));
                        if (py < 0.08f || py > 0.93f) c = dark;                       // Fugen zwischen den Brettern
                        if (x < 3 || x > n - 4 || y < 3 || y > n - 4) c = dark;         // Rand
                        if ((x == 12 || x == n - 13) && (int)(py * 10) == 5) c = dark * 0.8f;   // Naegel
                        c.a = 1f;
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                wallSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return wallSprite;
            }
        }
    }
}
