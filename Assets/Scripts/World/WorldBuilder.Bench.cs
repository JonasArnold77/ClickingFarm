using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Waffenbank rechts vor dem Haus: Ein Tipp oeffnet ihr Fenster, jeder weitere Tipp (oder der grosse Knopf darin)
    /// haemmert an der gewaehlten Waffe. Nach genug Schlaegen steigt die Waffe eine Stufe: mehr Schaden, schnelleres
    /// Nachladen. Stuetzranken (Rolle "support") helfen Gebaeuden in ihrer Naehe: Gebaeude verdienen mehr, die Waffenbank
    /// wird von selbst behaemmert und angeschlagene Mauern werden geflickt. Werte in plants.json -> bench und support.
    /// </summary>
    public partial class WorldBuilder
    {
        SpriteRenderer bench, benchIcon;
        Vector2 benchPos;
        float benchSquash, benchScale, benchIconScale;
        string benchIconFor;
        readonly Dictionary<PlantedState, float> supportTimers = new Dictionary<PlantedState, float>();

        public event Action BenchTapped;
        public event Action<WeaponDef, int> WeaponLeveled;
        /// <summary>Solange das Fenster der Waffenbank offen ist, haemmert ein Tipp auf die Bank.</summary>
        public bool BenchOpen { get; set; }

        void PlaceBench()
        {
            benchPos = HousePos + new Vector2(3.75f, -1.05f);
            Shadow(benchPos, 1.5f, 0.38f, 0.3f);
            bench = Place("hh_log", benchPos, 1f);
            if (bench != null)
            {
                benchScale = 1.45f / Mathf.Max(0.01f, bench.sprite.bounds.size.x);
                bench.transform.localScale = Vector3.one * benchScale;
                bench.transform.position = new Vector3(benchPos.x, benchPos.y - bench.sprite.bounds.min.y * benchScale, 0);
                // die Waffe liegt auf der Bank
                benchIcon = new GameObject("BenchWeapon").AddComponent<SpriteRenderer>();
                benchIcon.transform.SetParent(objects, false);
                benchIcon.sortingOrder = Order(benchPos.y) + 2;
            }
            Reserve(benchPos, 0.75f);
        }

        Vector2 BenchTop => bench != null ? new Vector2(benchPos.x, bench.bounds.max.y - 0.05f) : benchPos + new Vector2(0, 0.4f);

        public bool HitBench(Vector2 w)
        {
            if (bench == null) return false;
            var b = bench.bounds; b.Expand(new Vector3(0.3f, 0.7f, 0));
            b.center += new Vector3(0, 0.25f, 0);
            return b.Contains(new Vector3(w.x, w.y, b.center.z));
        }

        /// <summary>Tipp auf die Bank in der Welt: erst Fenster oeffnen, bei offenem Fenster haemmern.</summary>
        public void TapBench()
        {
            if (BenchOpen) Hammer(1f, true);
            else { benchSquash = 0.5f; Sfx.Play("pop", 0.85f); Haptics.Tap(10); }
            BenchTapped?.Invoke();
        }

        // ------------------------------------------------------------------ Waffenstufen

        WeaponLevel WLevel(string id)
        {
            var e = save.weaponLevels.Find(x => x.id == id);
            if (e == null) save.weaponLevels.Add(e = new WeaponLevel { id = id, lv = 1 });
            if (e.lv < 1) e.lv = 1;
            return e;
        }

        public int WeaponLv(string id) => WLevel(id).lv;
        public float WeaponXp(string id) => WLevel(id).xp;
        public int WeaponTapsNeeded(string id)
        {
            var b = PlantBalance.I.Bench;
            return Mathf.Max(1, Mathf.RoundToInt(b.tapsBase * Mathf.Pow(b.tapsGrowth, WeaponLv(id) - 1)));
        }
        public bool WeaponMaxed(string id) => WeaponLv(id) >= PlantBalance.I.Bench.maxLevel;
        public float WeaponDamageMult(string id) => 1f + PlantBalance.I.Bench.damagePerLevel * (WeaponLv(id) - 1);
        public float WeaponCooldownMult(string id)
        {
            var b = PlantBalance.I.Bench;
            return Mathf.Max(b.minCooldown, Mathf.Pow(1f - b.cooldownPerLevel, WeaponLv(id) - 1));
        }

        /// <summary>Ein Hammerschlag auf die gewaehlte Waffe. loud = vom Spieler (grosser Effekt), sonst von einer Stuetzranke.</summary>
        public void Hammer(float power, bool loud)
        {
            var w = PlantBalance.I.Weapon(save.weapon);
            if (w == null || bench == null) return;
            var e = WLevel(w.id);
            var top = BenchTop;
            if (WeaponMaxed(w.id))
            {
                if (loud) { benchSquash = 0.4f; Sfx.Play("dry", 1.2f, 0.4f); Floater?.Invoke(top + new Vector2(0, 0.5f), "Höchste Stufe!", new Color(1f, 0.9f, 0.6f)); }
                return;
            }
            float gain = Mathf.Max(1f, Mathf.Round(power * (loud ? UpgradeMult("click") : 1f)));
            e.xp += gain;
            benchSquash = loud ? 1f : Mathf.Max(benchSquash, 0.45f);
            var P = Particles.I;
            // Funken spruehen vom Amboss
            P?.Burst(top + new Vector2(0, 0.08f), "spark", new Color(1f, 0.8f, 0.4f), loud ? 9 : 4, loud ? 3.4f : 2f, 0.25f, 0.12f, 6f, 1f);
            if (loud)
            {
                P?.Burst(top, "square", new Color(0.55f, 0.4f, 0.25f), 3, 1.6f, 0.4f, 0.07f, 9f, 1.5f);
                Sfx.Play("chop", UnityEngine.Random.Range(1.25f, 1.45f), 0.6f);
                Haptics.Tap(14);
                WorldCamera.I?.Punch(0.004f);
                Floater?.Invoke(top + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.45f), "+" + gain, new Color(1f, 0.75f, 0.4f));
            }
            else Sfx.Play("tick", UnityEngine.Random.Range(1.3f, 1.5f), 0.2f);

            if (e.xp >= WeaponTapsNeeded(w.id))
            {
                e.xp = 0;
                e.lv++;
                Shockwave(top, 1.6f, 0.5f, new Color(1f, 0.85f, 0.45f, 0.9f));
                P?.Burst(top + new Vector2(0, 0.3f), "star", new Color(1f, 0.9f, 0.5f), 22, 3.5f, 0.9f, 0.3f, 1f, 1.5f);
                WorldVfx.Play("eat", top + new Vector2(0, 0.2f), 1.2f, 30010, null, 2f);
                WorldFx.Play("firework", top + new Vector2(0, 0.6f), 0.15f);
                Sfx.Play("levelup", 1.05f, 0.9f);
                Haptics.Tap(40);
                WorldCamera.I?.Shake(0.06f);
                WorldPostFX.Punch(0.4f);
                WeaponLeveled?.Invoke(w, e.lv);
                save.Save();
            }
        }

        void UpdateBench(float dt)
        {
            if (bench == null) return;
            benchSquash = Mathf.MoveTowards(benchSquash, 0, dt * 4f);
            float sq = Mathf.Sin(benchSquash * Mathf.PI * 1.5f) * benchSquash * 0.14f;
            bench.transform.localScale = new Vector3(benchScale * (1 + sq), benchScale * (1 - sq), 1);
            if (benchIcon == null) return;
            if (benchIconFor != save.weapon)
            {
                benchIconFor = save.weapon;
                benchIcon.sprite = WeaponArt.Icon(save.weapon);
                benchIconScale = 0.85f / Mathf.Max(0.01f, benchIcon.sprite.bounds.size.x);
            }
            var top = BenchTop;
            // die Waffe huepft bei jedem Schlag kurz hoch
            benchIcon.transform.position = top + new Vector2(0, 0.12f + benchSquash * 0.18f);
            benchIcon.transform.rotation = Quaternion.Euler(0, 0, 8f + benchSquash * 14f);
            benchIcon.transform.localScale = Vector3.one * benchIconScale * (1f + benchSquash * 0.1f);
            UpdateBenchElement();
        }

        // ------------------------------------------------------------------ Stuetzranken

        public float SupportRadius(PlantedState p) => PlantBalance.I.Support.radius + PlantBalance.I.Support.radiusPerLevel * (p.Lv - 1);
        bool InSupport(PlantedState p, Vector2 at) { float r = SupportRadius(p); return (CellCenter(new Cell(p.x, p.y)) - at).sqrMagnitude < r * r; }

        /// <summary>Was eine Stuetzranke gerade stuetzt (fuer das Info-Fenster).</summary>
        public string SupportSummary(PlantedState p)
        {
            var parts = new List<string>();
            if (bench != null && InSupport(p, benchPos)) parts.Add("hämmert an der Waffenbank");
            int nw = 0; foreach (var kv in walls) if (InSupport(p, CellCenter(kv.Key))) nw++;
            if (nw > 0) parts.Add("flickt " + nw + (nw == 1 ? " Mauer" : " Mauern"));
            return parts.Count > 0 ? string.Join(" · ", parts) : "Nichts in Reichweite – setz sie an Waffenbank oder Mauern";
        }

        void UpdateSupport(float dt)
        {
            var s = PlantBalance.I.Support;
            foreach (var p in save.planted)
            {
                if (!p.Grown || p.Role != "support") continue;
                if (!supportTimers.TryGetValue(p, out float t)) t = UnityEngine.Random.Range(0.5f, 2f);
                t -= dt;
                if (t <= 0f)
                {
                    t = s.interval / Mathf.Max(1f, 0.6f + 0.4f * p.Lv);
                    SupportPulse(p);
                }
                supportTimers[p] = t;
            }
        }

        /// <summary>Die Ranke greift nach allem in Reichweite: Blaetter fliegen zu Bank und Mauern.</summary>
        void SupportPulse(PlantedState p)
        {
            var s = PlantBalance.I.Support;
            var from = PlantBase(p) + new Vector2(0, CellSize * 0.8f);
            bool any = false;
            if (bench != null && InSupport(p, benchPos))
            {
                any = true;
                StartCoroutine(Leaf(from, BenchTop, () => Hammer(s.hammer * p.Lv, false)));
            }
            foreach (var kv in walls)
            {
                var v = kv.Value;
                float maxHp = MaxHp(v.st);
                if (v.st.hp >= maxHp || !InSupport(p, CellCenter(kv.Key))) continue;
                any = true;
                var at = CellCenter(kv.Key);
                StartCoroutine(Leaf(from, at, () =>
                {
                    if (v.st == null) return;
                    v.st.hp = Mathf.Min(maxHp, v.st.hp + maxHp * (s.repair + 0.02f * (p.Lv - 1)));
                    v.pop = 0.85f;
                    Particles.I?.Burst(at + new Vector2(0, 0.1f), "spark", new Color(0.6f, 1f, 0.5f), 5, 1.4f, 0.4f, 0.1f, -0.5f, 0.6f);
                    Floater?.Invoke(at + new Vector2(0, 0.45f), "geflickt", new Color(0.65f, 1f, 0.55f));
                }));
            }
            if (any && plantViews.TryGetValue(new Cell(p.x, p.y), out var pv)) pv.sy = Mathf.Min(pv.sy, -0.05f);
        }

        static readonly Color LeafCol = new Color(0.45f, 0.78f, 0.3f);

        /// <summary>Ein Blatt (oder eine Muenze, ein Holzscheit, ein Samen) fliegt im Bogen zum Ziel; dort passiert onHit.</summary>
        IEnumerator Leaf(Vector2 from, Vector2 to, Action onHit, Sprite sprite = null, Color? col = null, float width = 0.13f)
        {
            var sr = new GameObject("Leaf").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = sprite != null ? sprite : DropSprite;
            if (sprite == null) sr.sharedMaterial = UnlitMat;
            var tint = col ?? LeafCol;
            sr.color = sprite != null ? Color.white : tint;
            sr.sortingOrder = 30005;
            float size = width / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            float stretch = sprite == null ? 1.4f : 1f;
            float dur = 0.45f + Vector2.Distance(from, to) * 0.05f;
            float h = 0.5f + Vector2.Distance(from, to) * 0.2f;
            float spin = UnityEngine.Random.Range(-720f, 720f);
            for (float k = 0f; k < 1f; k += Time.deltaTime / dur)
            {
                var pos = Vector2.Lerp(from, to, k) + Vector2.up * (4f * h * k * (1f - k));
                sr.transform.position = pos;
                sr.transform.rotation = Quaternion.Euler(0, 0, sprite == null ? spin * k : spin * k * 0.25f);
                sr.transform.localScale = new Vector3(size, size * stretch, 1f);
                if (UnityEngine.Random.value < 0.3f)
                    Particles.I?.Spawn(pos, "square", new Color(tint.r, tint.g, tint.b, 0.6f), Vector3.zero, 0.3f, 0.04f, 0.01f, 0.5f);
                yield return null;
            }
            Destroy(sr.gameObject);
            onHit?.Invoke();
        }
    }
}
