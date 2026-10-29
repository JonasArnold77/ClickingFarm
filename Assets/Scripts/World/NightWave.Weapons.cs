using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Waffen des Spielers. Es gibt keine eigene Spielfigur: Man zielt mit dem Finger, die Schuesse kommen
    /// "von oben" (vom Himmel bzw. vom Bildrand) auf die angetippte Stelle.
    /// Pistole: sofortiger Treffer mit Leuchtspur. Schrotflinte: Streuung um den Zielpunkt.
    /// Pfeil &amp; Bogen: Pfeil faellt vom Himmel und trifft mehrere nah beieinander, bleibt kurz stecken.
    /// Granatwerfer: Granate pfeift herab und explodiert. Werte in plants.json -> weapons.
    /// </summary>
    public partial class NightWave
    {
        class WShot
        {
            public WeaponDef w; public SpriteRenderer sr, shadow;
            public Vector2 from, to; public float dmg, t, flight; public bool done;
        }

        class Fx { public SpriteRenderer sr; public float t, dur, s0, s1; }

        readonly List<WShot> wshots = new List<WShot>();
        readonly List<Fx> fxs = new List<Fx>();
        float weaponCd, flashT;
        Light2D flashLight;
        static Sprite reticle;

        public WeaponDef Weapon => B.Weapon(wb != null ? wb.Save.weapon : "pistole");
        public bool IsWeaponUnlocked(WeaponDef w) => w != null && wb != null && wb.Save.wave >= w.unlockWave;
        /// <summary>0 = bereit, 1 = gerade geschossen (fuer die Anzeige).</summary>
        public float WeaponCooldown01 { get { var w = Weapon; return w == null || w.cooldown <= 0 ? 0 : Mathf.Clamp01(weaponCd / w.cooldown); } }

        public void SelectWeapon(string id)
        {
            var w = B.Weapon(id);
            if (w == null || !IsWeaponUnlocked(w) || wb == null) return;
            wb.Save.weapon = w.id;
            wb.Save.Save();
            Sfx.Play(w.kind == "arrow" ? "bow" : "dry", 1.4f, 0.4f);
        }

        void BuildWeaponFx()
        {
            flashLight = new GameObject("ImpactLight").AddComponent<Light2D>();
            flashLight.transform.SetParent(transform, false);
            flashLight.lightType = Light2D.LightType.Point;
            flashLight.pointLightOuterRadius = 2f; flashLight.pointLightInnerRadius = 0.1f;
            flashLight.color = new Color(1f, 0.8f, 0.45f); flashLight.intensity = 0f;
        }

        /// <summary>Punkt am oberen Bildrand, von dem ein Schuss auf die Stelle herabkommt (leicht perspektivisch).</summary>
        Vector2 SkyPoint(Vector2 impact)
        {
            var cam = Camera.main;
            if (cam == null) return impact + new Vector2(0, 8f);
            Vector2 c = cam.transform.position;
            float top = c.y + cam.orthographicSize + 0.8f;
            return new Vector2(impact.x + (impact.x - c.x) * 0.25f + Random.Range(-0.5f, 0.5f), top);
        }

        /// <summary>Tipp nachts: trifft der Finger einen Zombie, wird geschossen. true = Tipp verbraucht.</summary>
        public bool TryShoot(Vector2 world)
        {
            if (!Active || wb == null) return false;
            Monster best = null; float bd = 0.85f * 0.85f;
            foreach (var m in monsters)
            {
                if (m.dead) continue;
                float d = (m.pos + new Vector2(0, 0.45f) - world).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            if (best == null) return false;
            var w = Weapon;
            if (w == null) return false;
            if (weaponCd > 0f) { Sfx.Play("dry", 1f, 0.35f); return true; }
            Shoot(w, best);
            return true;
        }

        Vector2 Center(Monster m) => m.pos + new Vector2(0, 0.45f);

        void Shoot(WeaponDef w, Monster target)
        {
            float dmg = w.damage * (1f + w.damagePerWave * (WaveNumber - 1));
            weaponCd = w.cooldown;
            var aim = Center(target);
            AddFx(Reticle, aim, 0.16f, 0.75f, 0.45f, new Color(1f, 1f, 1f, 0.9f));
            switch (w.kind)
            {
                case "pellet":
                    {
                        float r = 0.25f + w.spread / 60f;
                        for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
                        {
                            var p = aim + Random.insideUnitCircle * r;
                            Beam(SkyPoint(p), p, new Color(1f, 0.85f, 0.5f));
                            Particles.I?.Burst(p, "square", new Color(0.55f, 0.45f, 0.32f), 2, 1.6f, 0.35f, 0.07f, 8f, 1.5f);
                            var hit = NearestTo(p, 0.36f);
                            if (hit != null) { hit.knock += (hit.pos - aim).normalized * 0.3f + Vector2.up * 0.4f; Damage(hit, dmg, null); }
                        }
                        ImpactFx(aim, 1.3f);
                        Sfx.Play("shotgun", Random.Range(0.92f, 1.05f), 0.9f);
                        WorldCamera.I?.Shake(0.07f);
                        WorldCamera.I?.Punch(0.018f);
                        Haptics.Tap(30);
                        break;
                    }
                case "arrow":
                    Launch(w, aim + Random.insideUnitCircle * 0.05f, dmg, 0.22f, WeaponArt.Arrow, 0.75f);
                    Sfx.Play("bow", Random.Range(0.95f, 1.08f), 0.8f);
                    Haptics.Tap(10);
                    break;
                case "grenade":
                    {
                        var s = Launch(w, target.pos, dmg, 0.55f, WeaponArt.Grenade, 0.34f);
                        s.shadow = new GameObject("GrenadeShadow").AddComponent<SpriteRenderer>();
                        s.shadow.transform.SetParent(transform, false);
                        s.shadow.transform.position = target.pos;
                        s.shadow.sprite = WorldBuilder.Mist; s.shadow.color = new Color(0, 0, 0, 0f);
                        s.shadow.sortingOrder = 29990;
                        AddFx(Reticle, target.pos, s.flight, 0.9f, 0.5f, new Color(1f, 0.5f, 0.35f, 0.9f));
                        Sfx.Play("launch", Random.Range(0.95f, 1.05f), 0.9f);
                        Haptics.Tap(18);
                        break;
                    }
                default:
                    {
                        // Pistole: sofortiger Treffer, Leuchtspur von oben
                        var p = aim + Random.insideUnitCircle * 0.06f;
                        Beam(SkyPoint(p), p, new Color(1f, 0.92f, 0.6f));
                        target.knock += Vector2.up * 0.25f;
                        Damage(target, dmg, null);
                        ImpactFx(p, 1f);
                        Sfx.Play("shot", Random.Range(0.95f, 1.1f), 0.8f);
                        WorldCamera.I?.Shake(0.03f);
                        WorldCamera.I?.Punch(0.008f);
                        Haptics.Tap(15);
                        break;
                    }
            }
        }

        Monster NearestTo(Vector2 p, float r)
        {
            Monster best = null; float bd = r * r;
            foreach (var m in monsters)
            {
                if (m.dead) continue;
                float d = (Center(m) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        WShot Launch(WeaponDef w, Vector2 to, float dmg, float flight, Sprite sprite, float size)
        {
            var sr = new GameObject("WShot_" + w.id).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite;
            sr.transform.localScale = Vector3.one * size;
            sr.sortingOrder = 30005;
            var from = SkyPoint(to);
            sr.transform.position = from;
            var s = new WShot { w = w, sr = sr, from = from, to = to, dmg = dmg, flight = flight };
            wshots.Add(s);
            return s;
        }

        void ImpactFx(Vector2 at, float size)
        {
            WorldFx.Play(size > 1.1f ? "sparks" : "hit", at, 0.13f * size);
            Particles.I?.Burst(at, "spark", new Color(1f, 0.85f, 0.45f), (int)(5 * size), 3f * size, 0.18f, 0.22f * size, 0f, 0.5f);
            Particles.I?.Spawn(at, "spark", new Color(1f, 1f, 0.9f), Vector3.zero, 0.07f, 0.7f * size, 0.1f, 0f, 0f);
            Particles.I?.Burst(at, "square", new Color(0.5f, 0.62f, 0.4f), (int)(3 * size), 2.2f, 0.35f, 0.09f, 7f, 1.5f);
            flashLight.transform.position = at;
            flashLight.intensity = 1.4f * size;
            flashT = 0.07f;
        }

        void AddFx(Sprite sprite, Vector2 at, float dur, float s0, float s1, Color col)
        {
            var sr = new GameObject("Fx").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = at;
            sr.sprite = sprite; sr.color = col;
            sr.sortingOrder = 30010;
            sr.transform.localScale = Vector3.one * s0;
            fxs.Add(new Fx { sr = sr, dur = dur, s0 = s0, s1 = s1 });
        }

        void UpdateWeapons(float dt)
        {
            weaponCd = Mathf.Max(0f, weaponCd - dt);
            if (flashT > 0f) { flashT -= dt; if (flashT <= 0f && flashLight != null) flashLight.intensity = 0f; }

            for (int i = fxs.Count - 1; i >= 0; i--)
            {
                var f = fxs[i];
                f.t += dt;
                float k = Mathf.Clamp01(f.t / f.dur);
                if (k >= 1f || f.sr == null) { if (f.sr != null) Destroy(f.sr.gameObject); fxs.RemoveAt(i); continue; }
                f.sr.transform.localScale = Vector3.one * Mathf.Lerp(f.s0, f.s1, 1f - (1f - k) * (1f - k));
                var c = f.sr.color; c.a = Mathf.Min(c.a, 1f - k * k); f.sr.color = c;
            }

            foreach (var s in wshots)
            {
                if (s.done) continue;
                s.t += dt;
                float k = Mathf.Clamp01(s.t / s.flight);
                float e = k * k;   // beschleunigt nach unten
                var pos = Vector2.Lerp(s.from, s.to, e);
                s.sr.transform.position = pos;
                var dir = s.to - s.from;
                if (s.w.kind == "arrow") s.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                else
                {
                    s.sr.transform.rotation = Quaternion.Euler(0, 0, k * 540f);
                    if (s.shadow != null)
                    {
                        float ss = Mathf.Lerp(0.1f, 0.42f, k);
                        s.shadow.transform.localScale = new Vector3(ss, ss * 0.4f, 1);
                        s.shadow.color = new Color(0, 0, 0, 0.45f * k);
                    }
                    if (Random.value < dt * 30) Particles.I?.Spawn(pos, "square", new Color(0.75f, 0.75f, 0.72f, 0.5f), Vector3.zero, 0.4f, 0.08f, 0.2f, 0f, 0f);
                }
                if (k < 1f) continue;
                if (s.w.kind == "grenade") { Explode(s.to, s.w.radius, s.dmg); Finish(s); continue; }

                // Pfeil: trifft alle nahe beieinander (bis 1 + Durchschlag) und bleibt kurz stecken
                var targets = new List<Monster>();
                foreach (var m in monsters) if (!m.dead && (Center(m) - s.to).sqrMagnitude < 0.5f * 0.5f) targets.Add(m);
                targets.Sort((a, b) => (Center(a) - s.to).sqrMagnitude.CompareTo((Center(b) - s.to).sqrMagnitude));
                for (int i = 0; i < targets.Count && i <= s.w.pierce; i++) { targets[i].knock += dir.normalized * 0.3f; Damage(targets[i], s.dmg, null); }
                ImpactFx(s.to, 0.7f);
                Sfx.Play("chop", 1.3f, 0.6f);
                WorldCamera.I?.Shake(0.02f);
                WorldCamera.I?.Punch(0.006f);
                s.done = true;
                AddFx(s.sr.sprite, s.to, 0.9f, 0.75f, 0.75f, Color.white);
                fxs[fxs.Count - 1].sr.transform.rotation = s.sr.transform.rotation;
                Destroy(s.sr.gameObject);
            }
            wshots.RemoveAll(x => x.done);
        }

        void Finish(WShot s)
        {
            s.done = true;
            if (s.sr != null) Destroy(s.sr.gameObject);
            if (s.shadow != null) Destroy(s.shadow.gameObject);
        }

        void Explode(Vector2 at, float radius, float dmg)
        {
            var P = Particles.I;
            WorldFx.Play("explosionBig", at, 0.26f);
            P?.Ring(at, new Color(1f, 0.8f, 0.45f, 0.95f), radius * 1.8f, 0.4f);
            P?.Burst(at + new Vector2(0, 0.2f), "spark", new Color(1f, 0.75f, 0.35f), 18, 5f, 0.35f, 0.5f, 0f, 1f);
            P?.Burst(at + new Vector2(0, 0.2f), "square", new Color(1f, 0.55f, 0.2f), 14, 4f, 0.5f, 0.3f, 3f, 1.5f);
            P?.Burst(at, "square", new Color(0.35f, 0.32f, 0.3f, 0.8f), 12, 1.8f, 1.1f, 0.35f, -0.6f, 1.2f);   // Rauch steigt auf
            P?.Burst(at, "square", new Color(0.4f, 0.28f, 0.18f), 10, 3.5f, 0.6f, 0.18f, 8f, 2f);           // Erdbrocken
            flashLight.transform.position = at; flashLight.intensity = 3f; flashT = 0.15f;
            Sfx.Play("boom", Random.Range(0.9f, 1.05f), 1f);
            WorldCamera.I?.Shake(0.2f);
            WorldCamera.I?.Punch(0.04f);
            Haptics.Tap(45);
            foreach (var m in monsters)
            {
                if (m.dead) continue;
                float d = (m.pos - at).magnitude;
                if (d > radius) continue;
                m.knock += (m.pos - at).normalized * 1.2f * (1f - d / radius);
                Damage(m, dmg * Mathf.Lerp(1f, 0.5f, d / radius), null);
            }
        }

        void ClearWeaponShots()
        {
            foreach (var s in wshots) Finish(s);
            wshots.Clear();
        }

        /// <summary>Fadenkreuz, das beim Schuss kurz an der Zielstelle aufblitzt.</summary>
        static Sprite Reticle
        {
            get
            {
                if (reticle != null) return reticle;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(d - 30f) / 2.5f);
                        bool tick = (Mathf.Abs(dx) < 2.5f || Mathf.Abs(dy) < 2.5f) && d > 18f && d < 44f;
                        if (tick) a = 1f;
                        px[y * n + x] = new Color(1, 1, 1, a);
                    }
                tex.SetPixels(px); tex.Apply();
                reticle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return reticle;
            }
        }
    }

    /// <summary>Waffen-Icons und Geschosse, im Code gemalt (in den Grafikpaketen gibt es keine Waffen).</summary>
    public static class WeaponArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Arrow => Get("arrow", 128, 32, (x, y) =>
        {
            Color c = Color.clear;
            if (y > 14 && y < 18 && x > 18 && x < 108) c = new Color(0.62f, 0.45f, 0.28f);                       // Schaft
            if (x >= 104 && Mathf.Abs(y - 16) < (126 - x) * 0.45f) c = new Color(0.6f, 0.62f, 0.66f);           // Spitze
            if (x < 34 && Mathf.Abs(y - 16) > 2 && Mathf.Abs(y - 16) < 11 - (x - 6) * 0.15f && x > 6) c = new Color(0.9f, 0.3f, 0.25f);   // Federn
            return c;
        }, new Vector2(0.75f, 0.5f));

        public static Sprite Grenade => Get("grenade", 32, 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(16, 15));
            if (d > 12) return Color.clear;
            float l = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(12, 20)) / 16f);
            return Color.Lerp(new Color(0.2f, 0.26f, 0.18f), new Color(0.5f, 0.6f, 0.42f), l);
        }, new Vector2(0.5f, 0.5f));

        /// <summary>Icon fuer die Waffenleiste.</summary>
        public static Sprite Icon(string id)
        {
            var steel = new Color(0.32f, 0.34f, 0.38f); var steelHi = new Color(0.62f, 0.65f, 0.7f);
            var wood = new Color(0.55f, 0.35f, 0.2f); var woodHi = new Color(0.78f, 0.55f, 0.33f);
            switch (id)
            {
                case "schrotflinte":
                    return Get("ico_" + id, 128, 64, (x, y) =>
                        Box(x, y, 34, 34, 124, 42, steel, steelHi) ?? Box(x, y, 62, 26, 92, 34, wood, woodHi) ??
                        Poly(x, y, new[] { new Vector2(2, 20), new Vector2(40, 30), new Vector2(40, 44), new Vector2(4, 34) }, wood, woodHi) ?? Color.clear);
                case "bogen":
                    return Get("ico_" + id, 128, 64, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(92, 32));
                        if (x < 80 && d > 33 && d < 38) return Color.Lerp(wood, woodHi, (y / 64f));                // Bogen
                        if (Mathf.Abs(x - 74.5f) < 1f && y > 4 && y < 60) return new Color(0.9f, 0.88f, 0.8f);        // Sehne
                        if (Mathf.Abs(y - 32) < 1.6f && x > 30 && x < 112) return new Color(0.75f, 0.55f, 0.32f);    // Pfeil
                        if (x >= 110 && Mathf.Abs(y - 32) < (124 - x) * 0.5f) return steelHi;
                        if (x > 30 && x < 42 && Mathf.Abs(y - 32) > 2 && Mathf.Abs(y - 32) < 7) return new Color(0.9f, 0.3f, 0.25f);
                        return Color.clear;
                    });
                case "granatwerfer":
                    return Get("ico_" + id, 128, 64, (x, y) =>
                    {
                        var olive = new Color(0.3f, 0.36f, 0.24f); var oliveHi = new Color(0.55f, 0.62f, 0.42f);
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(56, 30));
                        if (d < 16) return Color.Lerp(steel, steelHi, Mathf.Clamp01((y - 16) / 30f));               // Trommel
                        return Box(x, y, 44, 32, 124, 50, olive, oliveHi) ?? Box(x, y, 30, 8, 44, 32, wood, woodHi) ??
                               Box(x, y, 4, 30, 40, 44, wood, woodHi) ?? Color.clear;
                    });
                default:   // Pistole
                    return Get("ico_pistole", 128, 64, (x, y) =>
                        Box(x, y, 40, 38, 112, 52, steel, steelHi) ??
                        Poly(x, y, new[] { new Vector2(44, 40), new Vector2(66, 40), new Vector2(58, 8), new Vector2(38, 8) }, wood, woodHi) ??
                        Ring(x, y, new Vector2(70, 32), 7, 9.5f, steel) ?? Color.clear);
            }
        }

        static Color? Box(int x, int y, float x0, float y0, float x1, float y1, Color c, Color hi)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return null;
            float k = (y - y0) / Mathf.Max(1f, y1 - y0);
            var col = Color.Lerp(c, hi, k * 0.8f);
            if (x - x0 < 1.5f || x1 - x < 1.5f || y - y0 < 1.5f || y1 - y < 1.5f) col *= 0.55f;   // Kontur
            col.a = 1f;
            return col;
        }

        static Color? Ring(int x, int y, Vector2 c, float r0, float r1, Color col)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            return d >= r0 && d <= r1 && y < c.y ? col : (Color?)null;
        }

        static Color? Poly(int x, int y, Vector2[] pts, Color c, Color hi)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            if (!inside) return null;
            var col = Color.Lerp(c, hi, Mathf.Clamp01(y / 64f) * 0.7f); col.a = 1f;
            return col;
        }

        static Sprite Get(string key, int w, int h, System.Func<int, int, Color> paint, Vector2? pivot = null)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = paint(x, y);
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
            cache[key] = s;
            return s;
        }
    }
}
