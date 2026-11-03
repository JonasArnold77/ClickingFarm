using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Vorrat und Nachbarschaft der Ernte:
    ///  - Jede gelieferte Ernte zaehlt (zusaetzlich zur Bezahlung) in den Vorrat ihrer Sorte. Bauwerke und der Hof-Ausbau
    ///    kosten neben Muenzen und Holz bestimmte Ernte ("crops": z. B. "weizen:30") – so braucht der Hof jede Sorte.
    ///  - Freunde: jede Ernte-Sorte hat Lieblingsnachbarn (catalog -> friends). Je reifer Freund rundherum (auch schraeg)
    ///    bringt sie mehr (helpers.friendBonus) – gemischte Beete lohnen sich mehr als eine einzige Sorte.
    /// </summary>
    public partial class WorldBuilder
    {
        public event Action StorageChanged;

        public double Stored(string id)
        {
            foreach (var c in save.storage) if (c.id == id) return c.n;
            return 0;
        }

        void AddStored(string id, double n)
        {
            if (n <= 0 || string.IsNullOrEmpty(id)) return;
            var e = save.storage.Find(x => x.id == id);
            if (e == null) save.storage.Add(e = new CropCount { id = id });
            e.n += n;
        }

        static (string id, int n) ParseCrop(string s)
        {
            var p = s.Split(':');
            int n = 0;
            if (p.Length > 1) int.TryParse(p[1], out n);
            return (p[0].Trim(), n);
        }

        /// <summary>Erntekosten, mehrfach gekaufte Bauwerke werden teurer (je Stueck +50 %).</summary>
        public List<(string id, int n)> CropCost(string[] crops, int bought = 0)
        {
            var l = new List<(string, int)>();
            if (crops == null) return l;
            foreach (var s in crops)
            {
                if (string.IsNullOrEmpty(s)) continue;
                var c = ParseCrop(s);
                l.Add((c.id, Mathf.CeilToInt(c.n * (1f + 0.5f * bought))));
            }
            return l;
        }

        public bool HasCrops(List<(string id, int n)> cost)
        {
            foreach (var c in cost) if (Stored(c.id) < c.n) return false;
            return true;
        }

        void TakeCrops(List<(string id, int n)> cost)
        {
            foreach (var c in cost) { var e = save.storage.Find(x => x.id == c.id); if (e != null) e.n = Math.Max(0, e.n - c.n); }
            StorageChanged?.Invoke();
        }

        /// <summary>"30 Weizen (12)" – mit aktuellem Vorrat in Klammern.</summary>
        public string CropCostText(List<(string id, int n)> cost, bool withStock = true)
        {
            var parts = new List<string>();
            foreach (var c in cost)
                parts.Add(c.n + " " + (PlantBalance.I.Plant(c.id)?.name ?? c.id) + (withStock ? " (" + Num.Short(Stored(c.id)) + ")" : ""));
            return string.Join(", ", parts);
        }

        /// <summary>Was an Ernte noch fehlt (fuer Hinweise), leer wenn alles da ist.</summary>
        public string CropMissing(List<(string id, int n)> cost)
        {
            var parts = new List<string>();
            foreach (var c in cost)
            {
                double have = Stored(c.id);
                if (have < c.n) parts.Add((c.n - (int)have) + " " + (PlantBalance.I.Plant(c.id)?.name ?? c.id));
            }
            return string.Join(", ", parts);
        }

        // ------------------------------------------------------------------ Freunde (Nachbarschaft)

        /// <summary>Faktor durch reife Freunde rundherum (8 Nachbarn): je Freund + friendBonus.</summary>
        readonly Dictionary<PlantedState, (float t, float m)> friendCache = new Dictionary<PlantedState, (float, float)>();

        public float FriendMult(PlantedState p)
        {
            if (friendCache.TryGetValue(p, out var cached) && Time.time - cached.t < 0.4f) return cached.m;
            float v = FriendMultNow(p);
            friendCache[p] = (Time.time, v);
            return v;
        }

        float FriendMultNow(PlantedState p)
        {
            var c = PlantBalance.I.Plant(p.type);
            if (c == null || c.friends == null || c.friends.Length == 0 || p.Role != "harvest") return 1f;
            return 1f + PlantBalance.I.Helpers.friendBonus * FriendCount(p, c);
        }

        int FriendCount(PlantedState p, CatalogDef c)
        {
            int n = 0;
            foreach (var q in save.planted)
            {
                if (q == p || !q.Grown || q.type == p.type || Cheb(p, q) > 1) continue;
                if (Array.IndexOf(c.friends, q.type) >= 0) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ Kombo: viele verschiedene Quellen

        /// <summary>Welche verschiedenen Quellen verstaerken diese Ernte-Pflanze gerade (Freunde, Tauschilf, Bach, Duenger,
        /// Trommelkraut, Totem, Gruppe)? Je mehr verschiedene, desto groesser der Kombo-Bonus obendrauf.</summary>
        public List<string> ComboSources(PlantedState p)
        {
            var l = new List<string>();
            if (p == null || p.Role != "harvest") return l;
            var c = PlantBalance.I.Plant(p.type);
            if (c != null && c.friends != null && FriendCount(p, c) > 0) l.Add("Freunde");
            if (InTauschilfReach(p)) l.Add("Tauschilf");
            if (NearRiver(p)) l.Add("Bach");
            if (GrownNear(p, "fert", 1) > 0) l.Add("Dünger");
            if (GrownNear(p, "boost", 2) > 0) l.Add("Trommelkraut");
            RefreshTotems();
            if (totemMap.TryGetValue(new Cell(p.x, p.y), out var t) && TotemActive(t)) l.Add("Totem");
            if (c != null && c.group > 0f && GroupMult(p, c) > 1f) l.Add("Gruppe");
            return l;
        }

        // die Kombo wird oft gebraucht (jedes Bild fuer die nachwachsende Ernte): hoechstens alle 0,4 s neu rechnen
        readonly Dictionary<PlantedState, (float t, float m)> comboCache = new Dictionary<PlantedState, (float, float)>();

        public float ComboMult(PlantedState p)
        {
            if (p == null || p.Role != "harvest") return 1f;
            if (comboCache.TryGetValue(p, out var cached) && Time.time - cached.t < 0.4f) return cached.m;
            float v = ComboMultNow(p);
            comboCache[p] = (Time.time, v);
            return v;
        }

        float ComboMultNow(PlantedState p)
        {
            var m = PlantBalance.I.Helpers.comboMults;
            if (m == null || m.Length == 0) return 1f;
            int n = ComboSources(p).Count;
            return m[Mathf.Clamp(n, 0, m.Length - 1)];
        }

        /// <summary>Alles, was die Lage dieser Pflanze bringt (Nachbarn, Boosts, Totem, Gruppe, Kombo) – ohne Stufe und Upgrades.</summary>
        public float PlacementMult(PlantedState p)
        {
            var c = PlantBalance.I.Plant(p.type);
            return BoostMult(p) * FertMult(p) * TotemMult(p, "harvest") * GroupMult(p, c) * FriendMult(p) * ComboMult(p) * (NearRiver(p) ? RiverMult(p, "produce") : 1f);
        }

        // Kombo-Rueckmeldung: steigt die Kombo einer reifen Pflanze spuerbar, gibt es sofort "Kombo ×3,2!"
        readonly Dictionary<PlantedState, float> lastCombo = new Dictionary<PlantedState, float>();
        float comboScanT;

        void UpdateCombos(float dt)
        {
            comboScanT -= dt;
            if (comboScanT > 0f) return;
            comboScanT = 0.5f;
            foreach (var p in save.planted)
            {
                if (!p.Grown || p.Role != "harvest") continue;
                float m = PlacementMult(p);
                bool known = lastCombo.TryGetValue(p, out float old);
                lastCombo[p] = m;
                if (!known || m < old * 1.25f || m < 1.5f) continue;
                var at = PlantBase(p) + new Vector2(0, CellSize * 0.9f);
                var col = ComboColor(m);
                int n = ComboSources(p).Count;
                Floater?.Invoke(at, "Kombo ×" + m.ToString(m < 10f ? "0.#" : "0") + "!" + (n >= 3 ? "  (" + n + " Quellen)" : ""), col);
                Particles.I?.Burst(at, "star", col, 10 + Mathf.Min(20, (int)(m * 2f)), 2.6f, 0.7f, 0.25f, 1f, 1.2f);
                Shockwave(PlantBase(p), 0.8f + Mathf.Min(2f, m * 0.15f), 0.4f, col);
                Sfx.Play("unlock", 1.2f + Mathf.Min(0.6f, m * 0.03f), 0.5f);
            }
            // weggefallene Pflanzen vergessen
            if (comboCache.Count > save.planted.Count * 2) { comboCache.Clear(); friendCache.Clear(); }
            if (lastCombo.Count > save.planted.Count * 2) { var keep = new HashSet<PlantedState>(save.planted); foreach (var k in new List<PlantedState>(lastCombo.Keys)) if (!keep.Contains(k)) lastCombo.Remove(k); }
        }

        /// <summary>Farbe nach Kombo-Staerke: weiss, gruen, gold, violett.</summary>
        public static Color ComboColor(float m) => m < 2f ? new Color(1f, 1f, 0.9f) : m < 4f ? new Color(0.6f, 1f, 0.5f) : m < 8f ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.6f, 1f);

        /// <summary>"Karotte, Salat" – die Lieblingsnachbarn einer Sorte.</summary>
        public static string FriendNames(CatalogDef c)
        {
            if (c?.friends == null) return "";
            var l = new List<string>();
            foreach (var f in c.friends) l.Add(PlantBalance.I.Plant(f)?.name ?? f);
            return string.Join(", ", l);
        }
    }
}
