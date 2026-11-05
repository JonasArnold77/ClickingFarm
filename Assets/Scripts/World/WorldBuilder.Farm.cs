using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Freie Festungs-Farm: Der Hof ist ein Rechteck vor dem Haus, das man im Laden Stufe fuer Stufe vergroessert
    /// (plants.json -> farmLevels). Ueberall im Hof, wo nichts im Weg ist, schimmert ein moegliches Feld durch; ein Tipp
    /// legt es an. Baeume (Holz) und Felsbrocken (ein paar Muenzen) blockieren und lassen sich wegklicken.
    /// Die Felsgruppen liegen zufaellig ausserhalb des Start-Hofs; alles haengt am Karten-Seed.
    /// </summary>
    public partial class WorldBuilder
    {
        int salt;   // Rausch-Versatz je Karte

        class RockObj { public int id, occ, hp; public Vector2 p; public SpriteRenderer sr, shadow; public float shake; }
        readonly List<RockObj> rocks = new List<RockObj>();
        readonly Dictionary<Cell, SpriteRenderer> ghosts = new Dictionary<Cell, SpriteRenderer>();

        public const int MaxFarmLevel = 6;

        /// <summary>Hof-Flaeche fuer eine Ausbaustufe (Welteinheiten).</summary>
        public static Rect FarmRect(int level)
        {
            level = Mathf.Clamp(level, 0, MaxFarmLevel);
            float xMin = Mathf.Max(-9.6f, -4.8f - 1.2f * level), xMax = -xMin;
            float yMin = Mathf.Max(-13.2f, -3.6f - 1.8f * level), yMax = 2.4f;
            return UnityEngine.Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Start-Hof: dort waechst anfangs nichts im Weg.</summary>
        public static Rect StartArea => FarmRect(0);

        public int FarmLevel => Mathf.Clamp(save.farmLevel, 0, MaxFarmLevel);

        /// <summary>Liegt die Zelle im Hof? Der Hof sind die offenen Nebel-Areale (siehe Fog), nicht die Hausreihe darueber.</summary>
        public bool IsBuildable(Cell c)
        {
            var a = AreaOf(c);
            return a.y <= AreaMaxY && IsAreaOpen(a);
        }

        Vector2Int ghostView = new Vector2Int(int.MinValue, 0);

        /// <summary>Alle Zellen, die gerade im Bild sind (Mauer-Vorschau ueberall auf der Karte).</summary>
        IEnumerable<Cell> VisibleCells()
        {
            var cam = Camera.main;
            if (cam == null) { foreach (var c in FarmCells()) yield return c; yield break; }
            float hh = cam.orthographicSize + CellSize, hw = hh * cam.aspect + CellSize;
            Vector2 ctr = cam.transform.position;
            var a = CellAt(ctr - new Vector2(hw, hh)); var b = CellAt(ctr + new Vector2(hw, hh));
            for (int x = a.x; x <= b.x; x++) for (int y = a.y; y <= b.y; y++) yield return new Cell(x, y);
        }

        /// <summary>Im Mauer-Modus die Vorschau nachziehen, wenn die Kamera ein paar Zellen weiter ist.</summary>
        void UpdateWallGhosts()
        {
            if (!PedestalMode || Camera.main == null) return;
            var c = CellAt(Camera.main.transform.position);
            var key = new Vector2Int(Mathf.FloorToInt(c.x / 3f) * 1000 + Mathf.FloorToInt(c.y / 3f), Mathf.RoundToInt(Camera.main.orthographicSize * 2f));
            if (key == ghostView) return;
            ghostView = key;
            RefreshGhosts();
        }

        /// <summary>Alle Zellen im aktuellen Hof (alle offenen Areale).</summary>
        IEnumerable<Cell> FarmCells()
        {
            for (int ax = AreaMinX - 1; ax <= AreaMaxX + 1; ax++)
                for (int ay = AreaMinY - 1; ay <= AreaMaxY; ay++)
                {
                    var a = new Vector2Int(ax, ay);
                    if (!IsAreaOpen(a)) continue;
                    int x0 = AreaOX + ax * FogAreaCells, y0 = AreaOY + ay * FogAreaCells;
                    for (int x = x0; x < x0 + FogAreaCells; x++)
                        for (int y = y0; y < y0 + FogAreaCells; y++) yield return new Cell(x, y);
                }
        }

        /// <summary>Ueberall, wo ein Feld (oder im Mauer-Modus eine Mauer) angelegt werden kann, schimmert es ganz leicht durch.</summary>
        public void RefreshGhosts()
        {
            if (fieldSprite == null) fieldSprite = GroundMaterials.TileSprite(4, 3);
            if (fieldSprite == null) return;
            var seen = new HashSet<Cell>();
            // Mauer-Modus: kein Raster – dort zeigt die gruene/rote Vorschau, wo es geht
            foreach (var c in PedestalMode ? VisibleCells() : FarmCells())
            {
                seen.Add(c);
                bool show = WallMode || riverMode ? false : PedestalMode ? CanPlacePedestal(c) : CanPlaceField(c) && !HasWall(c);
                ghosts.TryGetValue(c, out var sr);
                if (show && sr == null)
                {
                    sr = new GameObject("FieldGhost").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    sr.transform.position = CellCenter(c);
                    sr.transform.localScale = Vector3.one * CellSize * 0.94f;
                    sr.sortingOrder = -32305;
                    ghosts[c] = sr;
                }
                if (sr != null)
                {
                    sr.enabled = show;
                    // Mauer-Modus: helles Holz statt Erde
                    sr.sprite = WallMode ? WallGhostSprite : PedestalMode ? PedestalGhostSprite : fieldSprite;
                    sr.color = BuildMode ? new Color(1f, 1f, 1f, PedestalMode ? 0.55f : 0.35f) : new Color(1f, 1f, 1f, 0.2f);
                    // Pult-Vorschau oben auf einer Mauer: auf den Wehrgang heben und vor die Mauer sortieren
                    bool onWall = PedestalMode && HasWall(c);
                    sr.transform.position = CellCenter(c) + new Vector2(0, onWall ? WallH : 0f);
                    sr.sortingOrder = onWall ? Order(c.y * CellSize + 0.1f) + 2 : -32305;
                    sr.transform.localScale = Vector3.one * CellSize * 0.94f / (sr.sprite != null ? sr.sprite.bounds.size.x : 1f);
                }
            }
            foreach (var kv in ghosts) if (!seen.Contains(kv.Key) && kv.Value != null) kv.Value.enabled = false;
        }

        // ------------------------------------------------------------------ Felsgruppen

        /// <summary>0..1: wie stark hier eine Felsgruppe liegt (zufaellige Flecken ausserhalb des Start-Hofs).</summary>
        float IslandValue(Vector2 p)
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            if (p.y > -1f || Mathf.Abs(p.x) > hw - 0.8f || p.y < -hh + 0.8f) return 0f;
            var a = StartArea;
            if (p.x > a.xMin - 1.2f && p.x < a.xMax + 1.2f && p.y > a.yMin - 1.4f) return 0f;   // Start-Hof frei
            float v = Fbm(p.x * 0.3f, p.y * 0.3f, 51 + salt);
            return Mathf.InverseLerp(0.66f, 0.74f, v);
        }

        /// <summary>Felsbrocken auf die Felsgruppen setzen; jeder laesst sich wegklicken.</summary>
        void PlaceRockIslands()
        {
            float hw = MapW / 2f, hh = MapH / 2f;
            var pts = new List<Vector2>();
            for (float y = -hh + 0.8f; y < -1f; y += 0.4f)
                for (float x = -hw + 0.8f; x < hw - 0.8f; x += 0.4f)
                {
                    var p = new Vector2(x + (R() - 0.5f) * 0.3f, y + (R() - 0.5f) * 0.3f);
                    if (IslandValue(p) > 0.5f) pts.Add(p);
                }
            for (int i = pts.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (pts[i], pts[j]) = (pts[j], pts[i]); }
            foreach (var p in pts)
            {
                bool big = IslandValue(p) > 0.9f && R() < 0.6f;
                float r = big ? 0.42f : 0.26f;
                if (!Free(p, r)) continue;
                int occ = occupied.Count;
                Reserve(p, r);
                SpriteRenderer sr, sh = null;
                float roll = R();
                if (big)
                {
                    if (roll < 0.6f) { sr = PlaceSized("hh_rock_big", p, 0.95f + R() * 0.4f, R() < 0.5f); sh = Shadow(p, 1.2f, 0.3f, 0.36f); }
                    else sr = Place("sk_Stones", p, 1.15f + R() * 0.4f, R() < 0.5f, 0.04f);
                }
                else
                {
                    if (roll < 0.5f) { sr = PlaceSized("hh_rock_medium", p, 0.45f + R() * 0.15f, R() < 0.5f); sh = Shadow(p, 0.65f, 0.16f); }
                    else sr = Place("sk_Stones", p, 0.65f + R() * 0.3f, R() < 0.5f, 0.04f);
                }
                if (sr != null) rocks.Add(new RockObj { id = rocks.Count, occ = occ, hp = PlantBalance.I.Clicks(big ? 5 : 3, "clear"), p = p, sr = sr, shadow = sh });
            }
        }

        void LoadRocks()
        {
            foreach (var id in save.rocksGone)
                if (id >= 0 && id < rocks.Count) RemoveRock(rocks[id], false);
        }

        /// <summary>Tipp auf einen Felsbrocken: er bricht nach ein paar Schlaegen und gibt ein paar Muenzen.</summary>
        public bool TryMine(Vector2 w)
        {
            RockObj best = null; int order = int.MinValue;
            foreach (var r in rocks)
            {
                if (r.sr == null) continue;
                var b = r.sr.bounds; b.Expand(0.15f);
                if (!b.Contains(new Vector3(w.x, w.y, b.center.z))) continue;
                if (r.sr.sortingOrder > order) { order = r.sr.sortingOrder; best = r; }
            }
            if (best == null) return false;
            best.hp--;
            best.shake = 1f;
            PayClick(best.p + new Vector2(0.3f, 0.2f));
            Particles.I?.Burst(best.p + new Vector2(0, 0.2f), "square", new Color(0.6f, 0.58f, 0.55f), 6, 2.4f, 0.45f, 0.08f, 9f, 2f);
            Sfx.Play("chop", UnityEngine.Random.Range(1.3f, 1.5f), 0.8f);
            Haptics.Tap(12);
            WorldCamera.I?.Shake(0.02f);
            Floater?.Invoke(best.p + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.6f), best.hp > 0 ? "noch " + best.hp : "weg!", new Color(1f, 0.95f, 0.8f));
            if (best.hp <= 0)
            {
                RemoveRock(best, true);
                save.rocksGone.Add(best.id);
                AddCoins(2 + rnd.Next(4), best.p + new Vector2(0, 0.5f));
                if (rnd.NextDouble() < PlantBalance.I.Altar.rockRune) AddRunes(1, best.p + new Vector2(0, 0.9f));
                save.Save();
                RefreshGhosts();
            }
            return true;
        }

        void RemoveRock(RockObj r, bool fx)
        {
            if (fx && r.sr != null)
            {
                Particles.I?.Burst(r.p + new Vector2(0, 0.2f), "square", new Color(0.55f, 0.52f, 0.5f), 14, 3.4f, 0.6f, 0.12f, 8f, 2f);
                WorldFx.Play("poof", r.p + new Vector2(0, 0.2f), 0.14f);
                Sfx.Play("break", 1.2f, 0.9f);
                Haptics.Tap(25);
            }
            if (r.sr != null) Destroy(r.sr.gameObject);
            if (r.shadow != null) Destroy(r.shadow.gameObject);
            r.sr = null; r.shadow = null;
            if (r.occ >= 0 && r.occ < occupiedR.Count) occupiedR[r.occ] = 0f;
        }

        void UpdateRocks(float dt)
        {
            foreach (var r in rocks)
            {
                if (r.sr == null || r.shake <= 0f) continue;
                r.shake = Mathf.MoveTowards(r.shake, 0f, dt * 5f);
                r.sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 50f) * r.shake * 5f);
            }
        }
    }
}
