using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Geboostete Felder: Jedes Feld, das von irgendetwas verstaerkt wird (Totem, Bach, Tauschilf, Duenger, Verstaerker,
    /// Vogelscheuche, Brunnen, Treibhaus), schimmert leicht in der Farbe seiner staerksten Quelle. Faehrt man mit der Maus
    /// darueber (oder ist die Pflanze darauf geoeffnet), laufen leuchtende Linien von jeder Quelle zum Feld, und eine
    /// kleine Liste neben dem Zeiger sagt, wer was beitraegt.
    /// </summary>
    public partial class WorldBuilder
    {
        public struct BoostSource { public string label; public Vector2 from; public Color col; public bool noLink; }   // noLink: keine Linie (Totem – seine Umrandung zeigt es schon)

        /// <summary>Feld unter dem Mauszeiger (null: keine Maus, ueber der Oberflaeche oder im Bau-Modus).</summary>
        public Cell? HoverCell { get; set; }
        /// <summary>Quellen fuer das Feld unter dem Mauszeiger (fuer die Liste in der Oberflaeche).</summary>
        public readonly List<BoostSource> HoverSources = new List<BoostSource>();

        readonly Dictionary<Cell, Color> boosted = new Dictionary<Cell, Color>();
        readonly List<SpriteRenderer> shimmerPool = new List<SpriteRenderer>(), linkPool = new List<SpriteRenderer>();
        float boostScanT, shimmerSparkT;
        int linkUsed;

        static readonly Color RiverCol = new Color(0.35f, 0.75f, 1f), SchilfCol = new Color(0.45f, 0.9f, 1f),
                              FertCol = new Color(0.75f, 0.55f, 0.3f), BoostCol = new Color(1f, 0.55f, 0.85f), StructCol = new Color(1f, 0.85f, 0.45f);

        static int Cheb(Cell a, int x, int y) => Mathf.Max(Mathf.Abs(a.x - x), Mathf.Abs(a.y - y));

        /// <summary>Alles, was dieses Feld gerade verstaerkt – mit Text, Herkunft und Farbe.</summary>
        public List<BoostSource> BoostSources(Cell c)
        {
            var list = new List<BoostSource>();
            var at = CellCenter(c);
            RefreshTotems();
            if (totemMap.TryGetValue(c, out var t) && TotemActive(t))
            {
                var d = PlantBalance.I.Structure(t.id);
                list.Add(new BoostSource { label = d.name + ": " + BoostName(d.boost) + " ×" + (1f + StructValue(t) * TotemShare(t)).ToString("0.##"), from = TotemCenter(t), col = OutlineColor(TotemStrength(t)), noLink = true });
            }
            for (int dx = -1; dx <= 1 && rivers.Count > 0; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var rc = new Cell(c.x + dx, c.y + dy);
                    if ((dx == 0 && dy == 0) || !rivers.ContainsKey(rc)) continue;
                    var r = PlantBalance.I.River;
                    list.Add(new BoostSource { label = "Bach: gegossen, Wachstum ×" + r.growth.ToString("0.#") + ", Ernte ×" + r.produce.ToString("0.#"), from = CellCenter(rc), col = RiverCol });
                    dx = 2; break;   // ein Bach reicht
                }
            foreach (var q in save.planted)
            {
                if (!q.Grown || (q.x == c.x && q.y == c.y)) continue;
                string name = PlantBalance.I.Plant(q.type)?.name ?? q.type;
                var from = CellCenter(new Cell(q.x, q.y));
                if (q.Role == "water" && InCross(new Cell(q.x, q.y), c, WaterRadiusOf(q))) list.Add(new BoostSource { label = name + ": gegossen", from = from, col = SchilfCol });
                else if (q.Role == "fert" && Cheb(c, q.x, q.y) <= 1) list.Add(new BoostSource { label = name + ": Dünger, Ernte ×1,5", from = from, col = FertCol });
                else if (q.Role == "boost" && Cheb(c, q.x, q.y) <= 2) list.Add(new BoostSource { label = name + ": Tipps ×2", from = from, col = BoostCol });
            }
            foreach (var s in save.structures)
            {
                if (!s.placed || IsTotem(s)) continue;
                var d = PlantBalance.I.Structure(s.id);
                if (d == null || (d.effect != "scare" && d.effect != "water" && d.effect != "greenhouse")) continue;
                float r = StructRadius(s);
                if (r <= 0f || (StructPos(s) - at).sqrMagnitude > r * r) continue;
                list.Add(new BoostSource { label = d.name + ": " + StructSummary(s).Split('·')[0].Trim(), from = StructPos(s), col = StructCol });
            }
            return list;
        }

        // ------------------------------------------------------------------ Schimmern

        void UpdateBoosts(float dt)
        {
            // alle paar Augenblicke neu bestimmen, welche Felder geboostet sind
            boostScanT -= dt;
            if (boostScanT <= 0f)
            {
                boostScanT = 0.4f;
                boosted.Clear();
                foreach (var c in fields.Keys)
                {
                    var src = BoostSources(c);
                    if (src.Count > 0) boosted[c] = src[0].col;
                }
            }
            int i = 0;
            float t = Time.time;
            foreach (var kv in boosted)
            {
                if (i >= shimmerPool.Count)
                {
                    var nsr = new GameObject("BoostShimmer").AddComponent<SpriteRenderer>();
                    nsr.transform.SetParent(objects, false);
                    nsr.sprite = Art.Get("white");
                    nsr.sharedMaterial = UnlitMat;
                    nsr.sortingOrder = -32298;
                    shimmerPool.Add(nsr);
                }
                var sr = shimmerPool[i++];
                sr.enabled = true;
                sr.transform.position = CellCenter(kv.Key);
                float k = CellSize * 0.92f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
                sr.transform.localScale = new Vector3(k, k, 1f);
                // ein heller Schimmer wandert schraeg ueber die Felder
                float wave = Mathf.Sin(t * 2.2f - (kv.Key.x + kv.Key.y) * 0.7f);
                var col = Color.Lerp(kv.Value, Color.white, 0.4f);
                col.a = 0.1f + 0.1f * Mathf.Max(0f, wave) + 0.03f * Mathf.Sin(t * 5f + kv.Key.x);
                sr.color = col;
            }
            for (; i < shimmerPool.Count; i++) shimmerPool[i].enabled = false;
            // vereinzeltes Glitzern
            shimmerSparkT -= dt;
            if (boosted.Count > 0 && shimmerSparkT <= 0f)
            {
                shimmerSparkT = Mathf.Max(0.04f, 0.5f / boosted.Count);
                int pick = Random.Range(0, boosted.Count), n = 0;
                foreach (var kv in boosted)
                    if (n++ == pick)
                    {
                        Particles.I?.Spawn(CellCenter(kv.Key) + Random.insideUnitCircle * CellSize * 0.4f, "spark", Color.Lerp(kv.Value, Color.white, 0.5f), Vector3.up * 0.25f, 0.6f, 0.06f, 0f, 0f);
                        break;
                    }
            }
            UpdateBoostLinks();
        }

        // ------------------------------------------------------------------ Woher kommt der Boost?

        SpriteRenderer LinkSr()
        {
            if (linkUsed >= linkPool.Count)
            {
                var sr = new GameObject("BoostLink").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = Art.Get("white");
                sr.sharedMaterial = UnlitMat;
                sr.sortingOrder = 30002;
                linkPool.Add(sr);
            }
            var r = linkPool[linkUsed++];
            r.enabled = true;
            return r;
        }

        void LinkDash(Vector2 p, Vector2 dir, float len, float thick, Color col, float angle = -1f)
        {
            var sr = LinkSr();
            float w = Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            sr.transform.position = p;
            sr.transform.rotation = Quaternion.Euler(0, 0, angle >= 0f ? angle : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(len / w, thick / w, 1f);
            sr.color = col;
        }

        /// <summary>Linien von jeder Quelle zum Feld unter der Maus (bzw. zur geoeffneten Pflanze); Striche laufen zum Feld hin.</summary>
        void UpdateBoostLinks()
        {
            linkUsed = 0;
            HoverSources.Clear();
            Cell? target = HoverCell;
            if (target.HasValue && !boosted.ContainsKey(target.Value)) target = null;
            bool fromMouse = target.HasValue;
            if (!target.HasValue && Inspected != null && save.planted.Contains(Inspected))
            {
                var ic = new Cell(Inspected.x, Inspected.y);
                if (boosted.ContainsKey(ic)) target = ic;
            }
            if (target.HasValue)
            {
                var c = target.Value;
                var to = CellCenter(c);
                var src = BoostSources(c);
                if (fromMouse) HoverSources.AddRange(src);
                float t = Time.time;
                // Zielfeld: pulsierender Rahmen
                float pulse = 0.6f + 0.3f * Mathf.Sin(t * 6f);
                for (int d = 0; d < 4; d++)
                {
                    var n = (CellCenter(Step(c, d, 1)) - to).normalized;
                    LinkDash(to + n * CellSize * 0.47f, new Vector2(-n.y, n.x), CellSize * 0.98f, 0.03f, new Color(1f, 1f, 1f, pulse));
                }
                foreach (var s in src)
                {
                    if (s.noLink) continue;
                    var dir = to - s.from;
                    float len = dir.magnitude;
                    // Quelle: leuchtende Raute
                    LinkDash(s.from, Vector2.right, 0.16f + 0.03f * Mathf.Sin(t * 5f), 0.16f + 0.03f * Mathf.Sin(t * 5f), new Color(s.col.r, s.col.g, s.col.b, 0.9f), 45f);
                    if (len < 0.05f) continue;
                    dir /= len;
                    // Linie aus wandernden Strichen
                    int dashes = Mathf.Max(2, Mathf.RoundToInt(len / 0.16f));
                    for (int i = 0; i < dashes; i++)
                    {
                        float u = Mathf.Repeat(i / (float)dashes + t * 0.8f / Mathf.Max(0.5f, len), 1f);
                        LinkDash(s.from + dir * len * u, dir, 0.08f, 0.035f, new Color(s.col.r, s.col.g, s.col.b, 0.95f));
                    }
                }
            }
            for (int i = linkUsed; i < linkPool.Count; i++) linkPool[i].enabled = false;
        }
    }
}
