using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Mauern aus Holz: Im Mauer-Modus (Knopf unten links) schimmern freie Zellen im Bild als helle Holzplaetze – ueberall
    /// auf der Karte, auch ausserhalb des Hofs und auf leeren Feldern (das Feld liegt darunter weiter). Ein Tipp
    /// baut ein Mauerstueck (kostet Holz), ein Tipp auf eine leere Mauer baut sie wieder ab (Holz zurueck).
    /// Auf jedem Mauerstueck kann eine Pflanze stehen – Tipp auf eine leere Mauer (ausserhalb des Modus) fragt, welche.
    /// Zombies muessen Mauern erst kaputtschlagen; steht eine Pflanze darauf, faellt sie mit.
    /// </summary>
    public partial class WorldBuilder
    {
        class WallView { public WallState st; public SpriteRenderer sr; public float shake, pop = 1f; }
        readonly Dictionary<Cell, WallView> walls = new Dictionary<Cell, WallView>();
        static Sprite wallSprite;
        bool wallMode;

        public bool WallMode => wallMode;
        public event System.Action<bool> WallModeChanged;

        public bool HasWall(Cell c) => walls.ContainsKey(c);
        static Vector2 WallFoot(Cell c) => new Vector2((c.x + 0.5f) * CellSize, c.y * CellSize);
        float WallMaxHp => PlantBalance.I.Wall.hp * UpgradeMult("wall");

        public void SetWallMode(bool on)
        {
            if (on && pedestalMode) { pedestalMode = false; PedestalModeChanged?.Invoke(false); }
            if (!on) CancelWallPlan();
            wallMode = on;
            RefreshGhosts();
            WallModeChanged?.Invoke(on);
        }

        void LoadDefense()
        {
            foreach (var w in save.walls) CreateWallView(w, false);
            LoadPedestals();
            RefreshGhosts();
        }

        /// <summary>Mauer geht ueberall, wo nichts Festes steht: nicht an der Klippe, nicht auf Baeumen, Felsen, Haus,
        /// Bauwerken oder Pflanzen. Auch ausserhalb des Hofs und auf leeren Feldern.</summary>
        public bool CanPlaceWall(Cell c)
        {
            if (HasWall(c) || HasStructure(c) || HasPedestal(c) || PlantAt(c) != null) return false;
            var center = CellCenter(c);
            float hc = CellSize / 2f;
            foreach (var corner in new[] { new Vector2(-hc, -hc), new Vector2(hc, -hc), new Vector2(-hc, hc), new Vector2(hc, hc) })
            {
                var p = center + corner;
                if (Inside(p) < 0.35f) return false;
                if (Mathf.Abs(p.x) > MapW / 2f - 0.5f || Mathf.Abs(p.y) > MapH / 2f - 0.5f) return false;
            }
            for (int i = 0; i < occupied.Count; i++)
            {
                if (occupiedR[i] <= 0f || IsDecorOcc(i)) continue;
                float d = CellSize * 0.42f + occupiedR[i] * 0.75f;
                if ((occupied[i] - center).sqrMagnitude < d * d) return false;
            }
            return true;
        }

        /// <summary>Zelle unter dem Finger. Hohe Mauern (und Pflanzen darauf) ragen in die Zelle darueber –
        /// ein Tipp dort trifft die Mauer.</summary>
        public Cell TapCell(Vector2 w)
        {
            // Mauern und Pulte ragen nach oben: ihre sichtbare Oberseite liegt ueber der eigenen Zelle. Von vorn nach
            // hinten pruefen (die vorderste Zelle wird zuletzt gezeichnet und verdeckt die dahinter).
            var c = CellAt(w);
            for (int dy = 2; dy >= 0; dy--)
            {
                var b = new Cell(c.x, c.y - dy);
                float s = SurfaceH(b);
                if (s <= 0f) continue;
                float yb = b.y * CellSize;
                if (w.y >= yb && w.y <= yb + s + CellSize) return b;
            }
            return c;
        }

        /// <summary>Wie hoch die Oberseite auf dieser Zelle liegt (Mauer und/oder Pult).</summary>
        public float SurfaceH(Cell c) => (HasWall(c) ? WallH : 0f) + (HasPedestal(c) ? PedH : 0f);

        /// <summary>Tipp im Mauer-Modus: Mauer bauen bzw. leere Mauer wieder abbauen.</summary>
        public bool TapWallMode(Cell c)
        {
            if (walls.TryGetValue(c, out var v))
            {
                if (PlantAt(c) != null || HasPedestal(c)) return false;
                RemoveWall(c, v, false);
                RefundWallWood(CellCenter(c) + new Vector2(0, 0.5f));
                return true;
            }
            if (!CanPlaceWall(c)) return false;
            if (WoodLeft < WallWood - 0.0001f)
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Zu wenig Holz – fälle Bäume!", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                return true;
            }
            BuildWall(c);
            save.Save();
            return true;
        }

        void BuildWall(Cell c, bool loud = true)
        {
            PayWallWood();
            var st = new WallState { x = c.x, y = c.y, hp = WallMaxHp };
            save.walls.Add(st);
            CreateWallView(st, true, loud);
            RefreshGhosts();
        }

        void CreateWallView(WallState st, bool fx, bool loud = true)
        {
            var c = new Cell(st.x, st.y);
            var sr = new GameObject("Wall").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.transform.position = WallFoot(c);
            sr.sprite = WallSprite;
            // sortiert knapp hinter der Vorderkante: Zombies davor bleiben vorn, alles in der Zelle dahinter liegt hinten;
            // Pflanzen obendrauf sortieren nach ihrem Fuss auf dem Boden (PlantSortY) und bleiben damit vorn
            sr.sortingOrder = Order(WallFoot(c).y + 0.1f);
            var v = new WallView { st = st, sr = sr, pop = fx ? 0f : 1f };
            walls[c] = v;
            ClearDecorAround(CellCenter(c), CellSize * 0.6f);
            if (fx)
            {
                Particles.I?.Burst(CellCenter(c), "square", new Color(0.8f, 0.62f, 0.4f), 8, 2.2f, 0.45f, 0.08f, 7f, 1.4f);
                if (loud) Sfx.Play("chop", UnityEngine.Random.Range(1.1f, 1.25f), 0.8f);
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
            RemovePedestal(c, false);    // ein Pult obendrauf auch
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
            UpdateWallGhosts();
            UpdateWallPlanPulse();
            foreach (var kv in walls)
            {
                var v = kv.Value;
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 4f);
                v.shake = Mathf.MoveTowards(v.shake, 0f, dt * 4f);
                float e = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                float s = CellSize / Mathf.Max(0.01f, v.sr.sprite.bounds.size.x);
                v.sr.transform.localScale = Vector3.one * s * e;
                v.sr.transform.position = (Vector3)WallFoot(kv.Key) + (Vector3)(UnityEngine.Random.insideUnitCircle * v.shake * 0.03f);
                float hp = Mathf.Clamp01(v.st.hp / Mathf.Max(1f, WallMaxHp));
                v.sr.color = Color.Lerp(new Color(0.6f, 0.5f, 0.45f), Color.white, hp);
            }
        }

        /// <summary>Hoehe der Mauer-Vorderseite in Welteinheiten; Pflanzen auf einer Mauer stehen um so viel hoeher.</summary>
        public const float WallH = 0.6f;

        /// <summary>Hohe Holzmauer in Schraegsicht: unten die Vorderseite aus liegenden Staemmen, oben der Wehrgang
        /// aus Brettern (genau eine Zelle tief). Nebeneinander gebaut ergeben sie eine durchgehende Mauer.</summary>
        public static Sprite WallSprite
        {
            get
            {
                if (wallSprite != null) return wallSprite;
                const int w = 96;
                int front = Mathf.RoundToInt(WallH / CellSize * w), h = front + w;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var light = new Color(0.93f, 0.8f, 0.58f); var mid = new Color(0.84f, 0.68f, 0.46f); var dark = new Color(0.45f, 0.3f, 0.17f);
                var log = new Color(0.62f, 0.42f, 0.24f); var logHi = new Color(0.82f, 0.6f, 0.36f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c;
                        if (y < front)
                        {
                            // Vorderseite: drei liegende Staemme, rund schattiert, unten dunkler (Schatten am Boden)
                            const int n = 4; float lh = front / (float)n;
                            float k = (y % lh) / lh;
                            float round = Mathf.Sin(k * Mathf.PI);
                            float grain = Mathf.PerlinNoise(x * 0.05f + (int)(y / lh) * 5f, y * 0.4f) * 0.25f;
                            c = Color.Lerp(log * 0.75f, logHi, round * 0.85f + grain);
                            c *= Mathf.Lerp(0.7f, 1f, y / (float)front);
                            if (k < 0.08f) c = dark;
                            if (x < 2 || x > w - 3) c = dark * 0.9f;
                        }
                        else
                        {
                            // Wehrgang: helle Bretter, quer
                            int yy = y - front;
                            int plank = yy / (w / 3);
                            float py = (yy % (w / 3)) / (float)(w / 3);
                            float grain = Mathf.PerlinNoise(x * 0.08f + plank * 7f, yy * 0.6f) * 0.5f + Mathf.PerlinNoise(x * 0.3f, yy * 0.1f + plank) * 0.2f;
                            c = Color.Lerp(mid, light, grain + (plank == 1 ? 0.1f : 0f));
                            if (py < 0.08f || py > 0.93f) c = dark;
                            if (x < 3 || x > w - 4 || yy < 3 || y > h - 4) c = dark;
                            if ((x == 12 || x == w - 13) && (int)(py * 10) == 5) c = dark * 0.8f;
                        }
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                wallSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return wallSprite;
            }
        }

        /// <summary>Flacher Holzplatz (nur der Wehrgang) fuer die Bau-Vorschau im Mauer-Modus.</summary>
        public static Sprite WallGhostSprite
        {
            get
            {
                if (wallGhost != null) return wallGhost;
                var s = WallSprite;
                int front = Mathf.RoundToInt(WallH / CellSize * 96);
                wallGhost = Sprite.Create(s.texture, new Rect(0, front, 96, 96), new Vector2(0.5f, 0.5f), 96);
                return wallGhost;
            }
        }
        static Sprite wallGhost;
    }
}
