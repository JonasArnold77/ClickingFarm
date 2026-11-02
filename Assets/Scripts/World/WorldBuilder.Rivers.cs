using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Baeche: Bau-Modus "Bach" (Knopf unten). Wie bei Mauern setzt der erste Klick den Anfang, die blaue Vorschau folgt
    /// dem Finger, der zweite Klick graebt den Bach (Muenzen je Stueck). Zweimal auf dasselbe Bachstueck schuettet es zu.
    /// Baeche fliessen nur um Gaerten herum: nie auf ein Feld, und die Vorschau endet am ersten Feld bzw. Hindernis.
    /// Das Wasser ist comichaft gemalt (dicke dunkle Kontur, kraeftiges Tuerkis, heller Rand) und fliesst sichtbar:
    /// Helligkeitswellen laufen den Bach entlang, weisse Stroemungsstriche ziehen in Fliessrichtung.
    /// Pflanzen auf Feldern direkt am Bach (auch schraeg) wachsen und produzieren schneller.
    /// Werte in plants.json -> river.
    /// </summary>
    public partial class WorldBuilder
    {
        class RiverView { public RiverState st; public SpriteRenderer sr; public SpriteRenderer[] streaks; public float pop; }
        readonly Dictionary<Cell, RiverView> rivers = new Dictionary<Cell, RiverView>();
        static readonly Sprite[] riverSprites = new Sprite[16];
        bool riverMode;
        float riverSparkT;

        public bool RiverMode => riverMode;
        public event System.Action<bool> RiverModeChanged;
        public bool HasRiver(Cell c) => rivers.ContainsKey(c);
        static readonly Color PlanBlue = new Color(0.35f, 0.75f, 1f, 0.55f);

        public void SetRiverMode(bool on)
        {
            if (on && wallMode) { wallMode = false; WallModeChanged?.Invoke(false); }
            if (on && pedestalMode) { pedestalMode = false; PedestalModeChanged?.Invoke(false); }
            CancelWallPlan();
            riverMode = on;
            RefreshGhosts();
            RiverModeChanged?.Invoke(on);
        }

        public bool CanPlaceRiver(Cell c) => !HasRiver(c) && !HasField(c) && CanPlaceWall(c);

        /// <summary>Am Bach: eine Bachzelle liegt direkt oder schraeg daneben.</summary>
        public bool NearRiver(Cell c)
        {
            if (rivers.Count == 0) return false;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if ((dx != 0 || dy != 0) && rivers.ContainsKey(new Cell(c.x + dx, c.y + dy))) return true;
            return false;
        }
        bool NearRiver(PlantedState p) => NearRiver(new Cell(p.x, p.y));

        /// <summary>Faktor fuer Pflanzen am Bach (growth = Wachstum, produce = Ernte von selbst).</summary>
        float RiverMult(PlantedState p, string what)
        {
            if (!NearRiver(p)) return 1f;
            var r = PlantBalance.I.River;
            return what == "growth" ? r.growth : r.produce;
        }

        // ------------------------------------------------------------------ Bauen

        /// <summary>Klick im Bach-Modus. false = hier geht kein Bach (dann Baum faellen / Fels abbauen).</summary>
        public bool RiverClick(Vector2 w)
        {
            var c = TapCell(w);
            if (!wallAnchor.HasValue)
            {
                if (!HasRiver(c) && !CanPlaceRiver(c)) return false;
                wallAnchor = c;
                UpdateWallPlan(w);
                Sfx.Play("tick", 1.2f, 0.5f);
                Haptics.Tap(8);
                return true;
            }
            var a = wallAnchor.Value;
            if (c.Equals(a) && rivers.TryGetValue(a, out var v))
            {
                RemoveRiver(a, v);   // zweimal dasselbe Stueck: zuschuetten
                AddCoins(PlantBalance.I.River.cost * 0.5f, CellCenter(a) + new Vector2(0, 0.5f));
                CancelWallPlan();
                save.Save();
                return true;
            }
            UpdateWallPlan(w);
            var path = new List<Cell>(planOk);
            CancelWallPlan();
            if (path.Count == 0)
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), save.coins < PlantBalance.I.River.cost ? "Zu wenig Münzen" : "Hier geht kein Bach", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.6f);
                return true;
            }
            for (int i = 0; i < path.Count; i++)
            {
                // Fliessrichtung: zum naechsten Stueck, das letzte fliesst weiter wie das vorige
                int dir = i + 1 < path.Count ? DirTo(path[i], path[i + 1]) : i > 0 ? DirTo(path[i - 1], path[i]) : 1;
                if (!TrySpend(PlantBalance.I.River.cost)) break;
                BuildRiver(path[i], dir, i);
            }
            Sfx.Play("water", 1f, 0.8f);
            Haptics.Tap(20);
            save.Save();
            return true;
        }

        static int DirTo(Cell a, Cell b) => b.y > a.y ? 0 : b.x > a.x ? 1 : b.y < a.y ? 2 : 3;

        void BuildRiver(Cell c, int dir, int i)
        {
            var st = new RiverState { x = c.x, y = c.y, dir = dir, seq = save.rivers.Count };
            save.rivers.Add(st);
            ClearDecorAround(CellCenter(c), CellSize * 0.6f);
            CreateRiverView(st, true, i * 0.04f);
            foreach (var d in Neighbors4(c)) if (rivers.TryGetValue(d, out var nv)) nv.sr.sprite = RiverSprite(Mask(d));
            Particles.I?.Burst(CellCenter(c), "square", new Color(0.4f, 0.8f, 1f), 6, 2f, 0.5f, 0.07f, 5f, 1.5f);
        }

        void RemoveRiver(Cell c, RiverView v)
        {
            save.rivers.Remove(v.st);
            Destroy(v.sr.gameObject);
            foreach (var s in v.streaks) Destroy(s.gameObject);
            rivers.Remove(c);
            foreach (var d in Neighbors4(c)) if (rivers.TryGetValue(d, out var nv)) nv.sr.sprite = RiverSprite(Mask(d));
            Particles.I?.Burst(CellCenter(c), "square", new Color(0.55f, 0.4f, 0.25f), 8, 2f, 0.5f, 0.08f, 6f, 1.5f);
            Sfx.Play("dig", 1f, 0.6f);
            RefreshGhosts();
        }

        static Cell[] Neighbors4(Cell c) => new[] { new Cell(c.x, c.y + 1), new Cell(c.x + 1, c.y), new Cell(c.x, c.y - 1), new Cell(c.x - 1, c.y) };

        /// <summary>Welche Nachbarn sind auch Bach (Bit 0 oben, 1 rechts, 2 unten, 3 links).</summary>
        int Mask(Cell c)
        {
            int m = 0; var n = Neighbors4(c);
            for (int i = 0; i < 4; i++) if (rivers.ContainsKey(n[i])) m |= 1 << i;
            return m;
        }

        void LoadRivers()
        {
            foreach (var r in save.rivers) CreateRiverView(r, false, 0f);
            foreach (var kv in rivers) kv.Value.sr.sprite = RiverSprite(Mask(kv.Key));
        }

        void CreateRiverView(RiverState st, bool pop, float delay)
        {
            var c = new Cell(st.x, st.y);
            var sr = new GameObject("Bach").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.transform.position = CellCenter(c);
            sr.sortingOrder = -32296;
            var streaks = new SpriteRenderer[2];
            for (int i = 0; i < streaks.Length; i++)
            {
                var s = new GameObject("BachStrich").AddComponent<SpriteRenderer>();
                s.transform.SetParent(objects, false);
                s.sprite = Art.Get("white");
                s.sortingOrder = -32294;
                streaks[i] = s;
            }
            rivers[c] = new RiverView { st = st, sr = sr, streaks = streaks, pop = pop ? -delay : 1f };
            sr.sprite = RiverSprite(Mask(c));
        }

        // ------------------------------------------------------------------ Fliessen

        void UpdateRivers(float dt)
        {
            if (rivers.Count == 0) return;
            float t = Time.time;
            foreach (var kv in rivers)
            {
                var v = kv.Value; var st = v.st;
                var center = CellCenter(kv.Key);
                // auftauchen: kurz aufploppen
                if (v.pop < 1f) v.pop = Mathf.Min(1f, v.pop + dt * 3f);
                float pk = Mathf.Clamp01(v.pop);
                float ps = pk < 1f ? Mathf.Sin(pk * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(pk * Mathf.PI)) : 1f;
                float k = CellSize / Mathf.Max(0.01f, v.sr.sprite.bounds.size.x) * 1.001f;
                v.sr.transform.localScale = new Vector3(k * ps, k * ps, 1f);
                // Helligkeitswelle laeuft den Bach entlang
                float wave = 0.94f + 0.08f * Mathf.Sin(t * 3f - st.seq * 0.9f);
                v.sr.color = new Color(wave, wave, Mathf.Min(1f, wave + 0.03f), 1f);
                // Stroemungsstriche in Fliessrichtung
                var fwd = (Vector2)(CellCenter(Step(kv.Key, st.dir, 1)) - center).normalized;
                var side = new Vector2(-fwd.y, fwd.x);
                for (int i = 0; i < v.streaks.Length; i++)
                {
                    var s = v.streaks[i];
                    s.enabled = pk >= 1f;
                    float u = Mathf.Repeat(t * 0.55f + i * 0.5f + st.seq * 0.37f, 1f);
                    float lat = (i == 0 ? -0.07f : 0.06f) + Mathf.Sin(t * 4f + i * 2f + st.seq) * 0.02f;
                    var p = center + fwd * (u - 0.5f) * CellSize * 0.9f + side * lat;
                    s.transform.position = p;
                    s.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg);
                    float w = Mathf.Max(0.01f, s.sprite.bounds.size.x);
                    s.transform.localScale = new Vector3(CellSize * 0.24f / w, 0.026f / w, 1f);
                    s.color = new Color(1f, 1f, 1f, 0.75f * Mathf.Sin(u * Mathf.PI));   // ein- und ausblenden
                }
            }
            // ab und zu glitzert das Wasser
            riverSparkT -= dt;
            if (riverSparkT <= 0f)
            {
                riverSparkT = Mathf.Max(0.05f, 0.6f / rivers.Count);
                int pick = UnityEngine.Random.Range(0, rivers.Count), i = 0;
                foreach (var kv in rivers)
                    if (i++ == pick) { Particles.I?.Spawn(CellCenter(kv.Key) + UnityEngine.Random.insideUnitCircle * 0.15f, "star", Color.white, Vector3.up * 0.15f, 0.5f, 0.07f, 0f, 0f); break; }
            }
        }

        // ------------------------------------------------------------------ Bild

        /// <summary>Comic-Wasser: Kreis in der Mitte plus Arme zu den Nachbarn, dicke dunkle Kontur, heller Innenrand,
        /// ein paar helle Glanzflecken. Die Arme reichen bis zum Rand, so verbinden sich Nachbarstuecke nahtlos.</summary>
        static Sprite RiverSprite(int mask)
        {
            if (riverSprites[mask] != null) return riverSprites[mask];
            const int n = 64;
            const float half = 32f, wR = 19f, outline = 4.5f, bank = 3.5f, rim = 3f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            var water = new Color(0.25f, 0.72f, 0.95f); var deep = new Color(0.16f, 0.52f, 0.88f);
            var ink = new Color(0.1f, 0.2f, 0.42f); var foam = new Color(0.85f, 0.97f, 1f); var shore = new Color(0.62f, 0.78f, 0.42f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    // Abstand ausserhalb der Wasserflaeche (<0 = innen)
                    float d = new Vector2(fx, fy).magnitude - wR;
                    if ((mask & 1) != 0 && fy > 0) d = Mathf.Min(d, Mathf.Abs(fx) - wR);
                    if ((mask & 2) != 0 && fx > 0) d = Mathf.Min(d, Mathf.Abs(fy) - wR);
                    if ((mask & 4) != 0 && fy < 0) d = Mathf.Min(d, Mathf.Abs(fx) - wR);
                    if ((mask & 8) != 0 && fx < 0) d = Mathf.Min(d, Mathf.Abs(fy) - wR);
                    Color c;
                    if (d > outline + bank) c = Color.clear;
                    else if (d > outline) c = new Color(shore.r, shore.g, shore.b, 0.85f * (1f - (d - outline) / bank));   // grasiger Uferstreifen
                    else if (d > 0f) c = ink;                                                                            // dicke Comic-Kontur
                    else if (d > -rim) c = foam;                                                                         // heller Rand
                    else
                    {
                        c = Color.Lerp(water, deep, Mathf.Clamp01(-d / wR));                                             // in der Mitte tiefer
                        float g = Mathf.PerlinNoise(x * 0.13f + mask * 3.1f, y * 0.13f);
                        if (g > 0.72f) c = Color.Lerp(c, foam, 0.55f);                                                   // Glanzflecken
                    }
                    px[y * n + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return riverSprites[mask] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
