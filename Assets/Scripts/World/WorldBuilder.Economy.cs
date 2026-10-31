using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Wirtschaft wie bei Cookie Clicker: Muenzen durch Ernte-Tipps und von selbst (Muenzen pro Sekunde),
    /// Verbesserungen und Gebaeude im Laden, Hof-Ausbau, Offline-Ertrag und goldene Schmetterlinge.
    /// Hier sitzen auch die Rollen der Nutzpflanzen: Wasser, Ernte, Duenger, Verstaerker, Automatik, Honig, Licht.
    /// Alle Zahlen stehen in plants.json (upgrades, buildings, farmLevels, catalog.role/value/cost).
    /// </summary>
    public partial class WorldBuilder
    {
        float cps, cpsTimer, saveTimer, butterflyTimer = 60f, goldenBoostT, goldenBoost = 1f, fieldTintT, prodFxT;
        double coinFrac;
        readonly Dictionary<PlantedState, float> autoTimers = new Dictionary<PlantedState, float>();
        Transform butterfly; SpriteRenderer butterflySr; Vector2 bfFrom, bfTo; float bfT, bfDur;
        static Sprite butterflySprite;

        public double Coins => save.coins;
        public double Earned => save.earned;
        public float Cps => cps;
        public float GoldenBoostLeft => goldenBoostT;
        public event Action CoinsChanged;
        public event Action<double, double> OfflineEarned;   // Muenzen, Sekunden
        public event Action<string> GoldenCaught;

        // ------------------------------------------------------------------ Muenzen

        public void AddCoins(double n, Vector2? at = null, bool floater = true)
        {
            if (n <= 0) return;
            save.coins += n;
            save.earned += n;
            if (at.HasValue && floater) Floater?.Invoke(at.Value, "+" + Num.Short(n), new Color(1f, 0.88f, 0.35f));
            CoinsChanged?.Invoke();
            CheckEarnedUnlocks();
        }

        public bool TrySpend(double n, Vector2? at = null)
        {
            if (save.coins + 1e-6 < n)
            {
                if (at.HasValue) Floater?.Invoke(at.Value, "Zu wenig Münzen", new Color(1f, 0.6f, 0.5f));
                Sfx.Play("error", 1f, 0.5f);
                return false;
            }
            save.coins -= n;
            CoinsChanged?.Invoke();
            return true;
        }

        void CheckEarnedUnlocks()
        {
            foreach (var c in PlantBalance.I.catalog)
            {
                if (c.IsCombat || IsUnlocked(c.id) || c.unlockEarned > save.earned) continue;
                save.unlocked.Add(c.id);
                PlantUnlocked?.Invoke(c.id);
            }
        }

        // ------------------------------------------------------------------ Verbesserungen

        public bool HasUpgrade(string id) => save.upgrades.Contains(id);

        /// <summary>Produkt aller gekauften Verbesserungen mit diesem Effekt (1 = keine).</summary>
        public float UpgradeMult(string effect)
        {
            float m = 1f;
            var ups = PlantBalance.I.upgrades;
            if (ups != null) foreach (var u in ups) if (u.effect == effect && HasUpgrade(u.id)) m *= u.value;
            return m;
        }

        /// <summary>Summe aller gekauften Verbesserungen mit diesem Effekt (0 = keine).</summary>
        public float UpgradeAdd(string effect)
        {
            float a = 0f;
            var ups = PlantBalance.I.upgrades;
            if (ups != null) foreach (var u in ups) if (u.effect == effect && HasUpgrade(u.id)) a += u.value;
            return a;
        }

        public bool BuyUpgrade(ShopUpgradeDef u)
        {
            if (u == null || HasUpgrade(u.id) || !TrySpend(u.cost)) return false;
            save.upgrades.Add(u.id);
            Sfx.Play("unlock", 1.1f, 0.9f);
            Haptics.Tap(30);
            WorldPostFX.Punch(0.5f);
            save.Save();
            return true;
        }

        // ------------------------------------------------------------------ Gebaeude

        public int BuildingCount(string id) { foreach (var b in save.buildings) if (b.id == id) return b.n; return 0; }
        public double BuildingCost(BuildingDef d) => Math.Ceiling(d.baseCost * Math.Pow(1.15, BuildingCount(d.id)));

        public bool BuyBuilding(BuildingDef d)
        {
            if (d == null || !TrySpend(BuildingCost(d))) return false;
            var e = save.buildings.Find(b => b.id == d.id);
            if (e == null) save.buildings.Add(e = new BuildingCount { id = d.id });
            e.n++;
            RefreshBuildingViews(d.id);
            Sfx.Play("levelup", 1.2f, 0.7f);
            Haptics.Tap(25);
            save.Save();
            return true;
        }

        /// <summary>Feste Plaetze fuer die Gebaeude rund um den Hof (werden beim Aufbau frei gehalten).</summary>
        static readonly Vector2[] BuildingSpots =
        {
            new Vector2(-5.9f, 3.3f), new Vector2(5.9f, 3.3f), new Vector2(-9.9f, -1.2f),
            new Vector2(9.9f, -1.2f), new Vector2(-9.9f, -7.5f), new Vector2(9.9f, -7.5f),
        };
        readonly Dictionary<string, SpriteRenderer> buildingViews = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, float> buildingPop = new Dictionary<string, float>();

        /// <summary>Gebaeude-Plaetze blockieren erst, wenn dort ein Gebaeude steht (siehe RefreshBuildingViews).</summary>
        void ReserveBuildingSpots() { }

        void RefreshBuildingViews(string bumpId = null)
        {
            var defs = PlantBalance.I.buildings;
            if (defs == null) return;
            for (int i = 0; i < defs.Length && i < BuildingSpots.Length; i++)
            {
                var d = defs[i];
                if (BuildingCount(d.id) <= 0 || buildingViews.ContainsKey(d.id)) continue;
                var spr = art.Get(d.art);
                if (spr == null) continue;
                var sr = Place(d.art, BuildingSpots[i], 1f, false, 0.02f);
                float s = 1.6f * d.scale / Mathf.Max(0.01f, spr.bounds.size.x);
                sr.transform.localScale = Vector3.one * s;
                sr.transform.position = new Vector3(BuildingSpots[i].x, BuildingSpots[i].y - spr.bounds.min.y * s, 0);
                Shadow(BuildingSpots[i], 1.6f * d.scale, 0.35f, 0.3f);
                buildingViews[d.id] = sr;
                Reserve(BuildingSpots[i], 1.1f);
                buildingBase[d.id] = sr.transform.localScale;
                buildingPop[d.id] = 0f;
                Particles.I?.Burst(BuildingSpots[i] + new Vector2(0, 0.5f), "star", new Color(1f, 0.9f, 0.5f), 18, 3f, 0.8f, 0.3f, 1f, 1.5f);
            }
            if (bumpId != null && buildingViews.ContainsKey(bumpId)) buildingPop[bumpId] = 0f;
        }

        // ------------------------------------------------------------------ Hof-Ausbau

        public FarmLevelDef NextFarmLevel()
        {
            var l = PlantBalance.I.farmLevels;
            return l != null && FarmLevel < l.Length && FarmLevel < MaxFarmLevel ? l[FarmLevel] : null;
        }

        public bool BuyFarm()
        {
            var next = NextFarmLevel();
            if (next == null) return false;
            if (save.wood < next.wood) { Sfx.Play("error", 1f, 0.5f); return false; }
            if (!TrySpend(next.cost)) return false;
            save.wood -= next.wood; WoodChanged?.Invoke(save.wood);
            ExpandFarm();
            return true;
        }

        // ------------------------------------------------------------------ Rollen der Pflanzen

        static int Cheb(PlantedState a, PlantedState b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        int GrownNear(PlantedState p, string role, int radius)
        {
            int n = 0;
            foreach (var q in save.planted) if (q != p && q.Grown && q.Role == role && Cheb(p, q) <= radius) n++;
            return n;
        }

        /// <summary>Gegossen: ein Tauschilf erreicht das Feld (Saftrohre verlaengern) oder ein Brunnen steht in der Naehe.</summary>
        public bool IsWatered(PlantedState p)
        {
            foreach (var q in save.planted) if (q != p && q.Grown && q.Role == "water" && InCross(new Cell(q.x, q.y), new Cell(p.x, p.y), WaterRadiusOf(q))) return true;
            if (NearRiver(p)) return true;   // am Bach
            return StructEffect("water", CellCenter(new Cell(p.x, p.y))) > 0f;
        }
        float FertMult(PlantedState p) => 1f + 0.5f * GrownNear(p, "fert", 1) * UpgradeMult("fert");
        float BoostMult(PlantedState p) => GrownNear(p, "boost", 2) > 0 ? 2f : 1f;
        float WaterMult(PlantedState p) => IsWatered(p) ? 1.5f : 1f;

        /// <summary>So viel bringt ein Tipp: Wachstum (bzw. Stufen-Fortschritt) und bei reifer Ernte Muenzen.</summary>
        public int TapPower(PlantedState p, bool growing)
        {
            float m = UpgradeMult("click") * BoostMult(p) * (1f + StructEffect("scare", CellCenter(new Cell(p.x, p.y))));
            if (growing) m *= UpgradeMult("growth") * (IsWatered(p) ? 2f : 1f) * (1f + 0.5f * GrownNear(p, "fert", 1)) * TotemMult(p, "growth") * RiverMult(p, "growth");
            return Mathf.Max(1, Mathf.RoundToInt(m));
        }

        public double HarvestPerTap(PlantedState p)
        {
            var c = PlantBalance.I.Plant(p.type);
            if (c == null || p.Role != "harvest" || !p.Grown) return 0;
            return c.value * p.Lv * UpgradeMult("harvest") * UpgradeMult("click") * BoostMult(p) * FertMult(p) * WaterMult(p) * TotemMult(p, "harvest");
        }

        /// <summary>Muenzen pro Sekunde dieser Pflanze (Ernte und Honig verdienen auch ohne Tippen).</summary>
        public float PlantCps(PlantedState p)
        {
            if (!p.Grown) return 0f;
            var c = PlantBalance.I.Plant(p.type);
            if (c == null) return 0f;
            if (p.Role == "harvest") return c.value * p.Lv * 0.15f * UpgradeMult("harvest") * FertMult(p) * WaterMult(p) * TotemMult(p, "produce") * RiverMult(p, "produce");
            if (p.Role == "bee")
            {
                int flowers = 0;
                var pos = CellCenter(new Cell(p.x, p.y));
                foreach (var d in decor) if (d.go != null && d.go.name.ToLower().Contains("flower") && (d.p - pos).sqrMagnitude < 2.2f * 2.2f) flowers++;
                flowers += GrownNear(p, "bee", 2);
                return c.value * p.Lv * (1f + 0.25f * flowers) * UpgradeMult("bee") * FertMult(p) * (1f + StructGlobal("bee")) * TotemMult(p, "produce") * RiverMult(p, "produce");
            }
            return 0f;
        }

        /// <summary>Licht: Schadens-Bonus fuer Zombies in der Naehe einer reifen Lichtpflanze.</summary>
        public float LightBonus(Vector2 pos)
        {
            float best = 1f;
            foreach (var p in save.planted)
            {
                if (!p.Grown || p.Role != "light") continue;
                float r = 2.2f + 0.3f * (p.Lv - 1);
                if ((CellCenter(new Cell(p.x, p.y)) - pos).sqrMagnitude < r * r) best = Mathf.Max(best, 1.5f + UpgradeAdd("light") + 0.05f * (p.Lv - 1));
            }
            return Mathf.Max(best, 1f + StructEffect("light", pos));   // Laternen
        }

        float ComputeCps()
        {
            float sum = 0f;
            foreach (var p in save.planted) if (!MakesProduct(p)) sum += PlantCps(p);   // Ernte-Pflanzen machen Produkte statt Muenzen
            var defs = PlantBalance.I.buildings;
            if (defs != null) for (int i = 0; i < defs.Length; i++) sum += defs[i].cps * BuildingCount(defs[i].id) * BuildingMult(i);
            return sum * UpgradeMult("cps");
        }

        /// <summary>Ein "Tipp" von einer Automatik-Pflanze: wie ein Klick, aber ohne grosses Spektakel.</summary>
        void SoftTap(PlantedState p, PlantedState from)
        {
            if (!plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) return;
            var basePos = (Vector2)v.root.position;
            var top = basePos + new Vector2(0, (v.sr.bounds.max.y - basePos.y) * 0.75f);
            v.sy = Mathf.Min(v.sy, -0.05f); v.flash = Mathf.Max(v.flash, 0.3f);
            PunchField(new Cell(p.x, p.y), 0.4f);
            if (from != null && plantViews.TryGetValue(new Cell(from.x, from.y), out var fv)) fv.sy = Mathf.Min(fv.sy, -0.04f);
            Particles.I?.Spawn(basePos + new Vector2(0, 0.15f), "spark", new Color(0.8f, 1f, 0.6f), Vector3.up * 0.6f, 0.3f, 0.1f, 0f, 0f);
            Sfx.Play("tick", UnityEngine.Random.Range(1.6f, 1.9f), 0.15f);
            if (!p.Grown)
            {
                p.growth = Mathf.Min(p.GrowTaps, p.growth + TapPower(p, true));
                UpdatePlantSprite(p, v);
                if (p.Grown) Ripe(p, v, basePos, top);
                return;
            }
            if (Time.unscaledTime < v.readyAt) return;
            if (p.Role == "harvest") AddProduct(p, 1, ItemValue(p) * 0.5, null);   // Klopfwurz erntet halb so gut
            p.xp += 1;
            if (p.xp >= Mathf.Max(1, PlantBalance.I.levelTaps)) LevelUp(p, v, basePos, top);
        }

        void UpdateAuto(float dt)
        {
            foreach (var p in save.planted)
            {
                if (!p.Grown || p.Role != "auto") continue;
                autoTimers.TryGetValue(p, out var t);
                t -= dt;
                if (t <= 0f)
                {
                    t = 1.4f / (p.Lv * UpgradeMult("auto"));
                    // klickt nur das Feld in Blickrichtung
                    var target = PlantAt(Step(new Cell(p.x, p.y), p.dir, 1));
                    if (target != null && target != p && target.Role != "auto") SoftTap(target, p);
                }
                autoTimers[p] = t;
            }
        }

        // ------------------------------------------------------------------ Laufender Betrieb

        void InitEconomy()
        {
            if (!save.economyInit) { save.coins = Math.Max(save.coins, PlantBalance.I.startCoins); save.economyInit = true; }
            CheckEarnedUnlocks();
            RefreshBuildingViews();
            InitStructures();
            InitKnollis();
            cps = ComputeCps();
            // Offline-Ertrag (gedeckelt, halbe Rate)
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (save.lastSeen > 0)
            {
                double secs = Math.Min(now - save.lastSeen, (2 + UpgradeAdd("offline") + StructGlobal("offline")) * 3600.0);
                double earned = secs > 60 ? cps * secs * 0.5 : 0;
                if (earned >= 1) { AddCoins(earned); StartCoroutine(AnnounceOffline(earned, secs)); }
            }
            save.lastSeen = now;
            save.Save();
        }

        System.Collections.IEnumerator AnnounceOffline(double coins, double secs)
        {
            yield return null; yield return null;   // UI ist dann aufgebaut
            OfflineEarned?.Invoke(coins, secs);
        }

        void OnApplicationPause(bool paused) { if (paused) { save.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); save.Save(); } }
        void OnApplicationQuit() { save.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); save.Save(); }

        void UpdateEconomy(float dt)
        {
            cpsTimer -= dt;
            if (cpsTimer <= 0f) { cpsTimer = 0.5f; cps = ComputeCps(); }
            if (goldenBoostT > 0f) { goldenBoostT -= dt; if (goldenBoostT <= 0f) goldenBoost = 1f; }
            // laufend gutschreiben
            coinFrac += cps * goldenBoost * dt;
            if (coinFrac >= 1) { double whole = Math.Floor(coinFrac); coinFrac -= whole; AddCoins(whole); }
            UpdateAuto(dt);
            // sichtbar machen, wer verdient
            prodFxT -= dt;
            if (prodFxT <= 0f)
            {
                prodFxT = 1.2f;
                foreach (var p in save.planted)
                {
                    float pc = PlantCps(p);
                    if (pc <= 0f || UnityEngine.Random.value > 0.5f) continue;
                    var pos = CellCenter(new Cell(p.x, p.y)) + new Vector2(0, 0.4f);
                    Particles.I?.Spawn(pos, "square", p.Role == "bee" ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.88f, 0.35f), Vector3.up * 0.8f, 0.7f, 0.06f, 0.03f, 0f);
                }
            }
            // gegossene Felder sind dunkler
            fieldTintT -= dt;
            if (fieldTintT <= 0f)
            {
                fieldTintT = 0.5f;
                foreach (var kv in fields)
                {
                    if (kv.Value == null || wetFlash.ContainsKey(kv.Key)) continue;
                    var p = PlantAt(kv.Key);
                    bool wet = p != null ? IsWatered(p) : IsWatered(new PlantedState { x = kv.Key.x, y = kv.Key.y });
                    kv.Value.color = wet ? new Color(0.7f, 0.68f, 0.8f) : Color.white;
                }
            }
            // Gebaeude ploppen beim Kauf
            foreach (var id in new List<string>(buildingPop.Keys))
            {
                float k = Mathf.MoveTowards(buildingPop[id], 1f, dt * 3f);
                buildingPop[id] = k;
                if (buildingViews.TryGetValue(id, out var sr) && sr != null)
                {
                    float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                    if (buildingBase.TryGetValue(id, out var bs)) sr.transform.localScale = bs * e;
                }
            }
            UpdateButterfly(dt);
            saveTimer -= dt;
            if (saveTimer <= 0f) { saveTimer = 5f; save.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); save.Save(); }
        }

        readonly Dictionary<string, Vector3> buildingBase = new Dictionary<string, Vector3>();

        // ------------------------------------------------------------------ Goldener Schmetterling

        void UpdateButterfly(float dt)
        {
            if (butterfly == null)
            {
                butterflyTimer -= dt * UpgradeMult("golden");
                if (butterflyTimer > 0f || Camera.main == null || !save.starterChosen) return;
                butterflyTimer = UnityEngine.Random.Range(70f, 150f);
                var cam = Camera.main;
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                bool ltr = UnityEngine.Random.value < 0.5f;
                bfFrom = c + new Vector2(ltr ? -w - 0.5f : w + 0.5f, UnityEngine.Random.Range(-h * 0.5f, h * 0.6f));
                bfTo = c + new Vector2(ltr ? w + 0.5f : -w - 0.5f, UnityEngine.Random.Range(-h * 0.5f, h * 0.6f));
                bfT = 0f; bfDur = 11f;
                butterflySr = new GameObject("GoldenButterfly").AddComponent<SpriteRenderer>();
                butterflySr.sprite = ButterflySprite;
                butterflySr.color = new Color(1.6f, 1.3f, 0.45f, 1f);
                butterflySr.sortingOrder = 31600;
                butterfly = butterflySr.transform;
                butterfly.SetParent(transform, false);
                return;
            }
            bfT += dt;
            float k = bfT / bfDur;
            if (k >= 1f) { Destroy(butterfly.gameObject); butterfly = null; return; }
            var pos = Vector2.Lerp(bfFrom, bfTo, k) + new Vector2(0, Mathf.Sin(bfT * 2.2f) * 0.6f);
            butterfly.position = pos;
            float flap = Mathf.Abs(Mathf.Sin(bfT * 14f));
            butterfly.localScale = new Vector3((0.35f + 0.35f * flap) * (bfTo.x > bfFrom.x ? 1 : -1), 0.6f, 1);
            if (UnityEngine.Random.value < dt * 12f) Particles.I?.Spawn(pos, "star", new Color(1f, 0.9f, 0.4f), Vector3.down * 0.3f, 0.8f, 0.08f, 0f, 0f);
        }

        /// <summary>Tipp auf den goldenen Schmetterling: Erntefieber (x7 fuer 30 s) oder ein Muenzregen.</summary>
        public bool TryCatchButterfly(Vector2 w)
        {
            if (butterfly == null || ((Vector2)butterfly.position - w).sqrMagnitude > 0.6f * 0.6f) return false;
            var pos = (Vector2)butterfly.position;
            Destroy(butterfly.gameObject); butterfly = null;
            WorldFx.Play("firework", pos, 0.2f);
            Particles.I?.Burst(pos, "star", new Color(1f, 0.9f, 0.4f), 30, 4f, 0.9f, 0.3f, 1f, 1.5f);
            Sfx.Play("levelup", 1.3f, 1f); Sfx.Play("unlock", 1.5f, 0.8f);
            Haptics.Tap(60);
            WorldPostFX.Punch(1f);
            if (UnityEngine.Random.value < 0.5f && cps > 0.5f)
            {
                goldenBoost = 7f; goldenBoostT = 30f;
                GoldenCaught?.Invoke("Erntefieber! Alle Einnahmen ×7 für 30 Sekunden");
            }
            else
            {
                double bonus = Math.Max(25, Math.Min(save.coins * 0.15, Math.Max(cps, 1) * 900));
                AddCoins(bonus, pos);
                GoldenCaught?.Invoke("Münzregen! +" + Num.Short(bonus));
            }
            return true;
        }

        static Sprite ButterflySprite
        {
            get
            {
                if (butterflySprite != null) return butterflySprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float au = Mathf.Abs(u);
                        float upper = ((au - 0.45f) * (au - 0.45f)) / 0.2f + ((v - 0.25f) * (v - 0.25f)) / 0.3f;
                        float lower = ((au - 0.35f) * (au - 0.35f)) / 0.09f + ((v + 0.35f) * (v + 0.35f)) / 0.12f;
                        float a = Mathf.Max(Mathf.Clamp01((1f - upper) * 6f), Mathf.Clamp01((1f - lower) * 6f));
                        var c = Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.95f, 0.6f), Mathf.Clamp01(1f - au));
                        if (au < 0.06f && Mathf.Abs(v) < 0.6f) { c = new Color(0.35f, 0.22f, 0.1f); a = 1f; }
                        c.a = a;
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                butterflySprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return butterflySprite;
            }
        }
    }

    /// <summary>Grosse Zahlen lesbar: 950, 1,2K, 3,4M, 5,6Mrd …</summary>
    public static class Num
    {
        static readonly string[] Suffix = { "", "K", "M", "Mrd", "Bio", "Brd", "Trd" };
        public static string Short(double v)
        {
            if (v < 0) return "-" + Short(-v);
            if (v < 1000) return v < 10 && v % 1 > 0.05 ? v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) : Math.Floor(v).ToString("0");
            int i = 0;
            while (v >= 1000 && i < Suffix.Length - 1) { v /= 1000; i++; }
            return v.ToString(v < 100 ? "0.#" : "0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + Suffix[i];
        }
    }
}
