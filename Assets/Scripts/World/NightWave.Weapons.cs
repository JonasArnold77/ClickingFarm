using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Waffen des Spielers: Der Bauer steht vor dem Haus und schiesst auf den Zombie, den man antippt.
    /// Pistole (schnell), Schrotflinte (Streuschuss), Pfeil &amp; Bogen (durchbohrt), Granatwerfer (Explosion).
    /// Werte in plants.json -> weapons; neue Waffen werden mit den Wellen freigeschaltet.
    /// </summary>
    public partial class NightWave
    {
        class WShot
        {
            public WeaponDef w; public SpriteRenderer sr, shadow;
            public Vector2 pos, vel, from, to; public float dmg, life, t, flight, arc, traveled; public int pierce;
            public HashSet<Monster> hit = new HashSet<Monster>();
            public bool done;
        }

        readonly List<WShot> wshots = new List<WShot>();
        Transform shooter; Animator shooterAnim; SortingGroup shooterGroup;
        Vector2 aimDir = Vector2.down;
        float weaponCd, recoil, flashT;
        Light2D muzzleLight;

        public Vector2 ShooterPos => WorldBuilder.HousePos + new Vector2(0.95f, -1.35f);
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

        void BuildShooter()
        {
            var prefab = WorldArtSet.I != null ? WorldArtSet.I.zombiePrefab : null;
            shooter = new GameObject("Bauer").transform;
            shooter.SetParent(transform, false);
            shooter.position = ShooterPos;
            if (prefab != null)
            {
                var rig = new GameObject("Rig").transform;
                rig.SetParent(shooter, false);
                rig.localScale = Vector3.one * (1.05f / ZombieNativeHeight);
                var z = Instantiate(prefab, rig, false);
                z.name = "Farmer";
                shooterAnim = z.GetComponentInChildren<Animator>();
                if (shooterAnim != null) shooterAnim.gameObject.AddComponent<ZombieAnimEvents>();
            }
            shooterGroup = shooter.gameObject.AddComponent<SortingGroup>();
            shooterGroup.sortingOrder = WorldBuilder.Order(ShooterPos.y);
            var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(shooter, false);
            sh.sprite = WorldBuilder.Mist; sh.color = new Color(0, 0, 0, 0.3f);
            sh.transform.localScale = new Vector3(0.6f, 0.18f, 1);
            sh.sortingOrder = -1;
            muzzleLight = new GameObject("MuzzleLight").AddComponent<Light2D>();
            muzzleLight.transform.SetParent(transform, false);
            muzzleLight.lightType = Light2D.LightType.Point;
            muzzleLight.pointLightOuterRadius = 2.2f; muzzleLight.pointLightInnerRadius = 0.1f;
            muzzleLight.color = new Color(1f, 0.8f, 0.45f); muzzleLight.intensity = 0f;
        }

        Vector2 Muzzle => (Vector2)ShooterPos + new Vector2(0, 0.55f) + aimDir * 0.3f;

        /// <summary>Tipp nachts: trifft der Finger einen Zombie, schiesst der Bauer darauf. true = Tipp verbraucht.</summary>
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

        void Shoot(WeaponDef w, Monster target)
        {
            var aim = target.pos + new Vector2(0, 0.45f);
            aimDir = (aim - ((Vector2)ShooterPos + new Vector2(0, 0.55f))).normalized;
            var from = Muzzle;
            var dir = (aim - from).normalized;
            float dmg = w.damage * (1f + w.damagePerWave * (WaveNumber - 1));
            weaponCd = w.cooldown;
            recoil = 1f;
            float speed = Mathf.Max(1f, w.speed);
            switch (w.kind)
            {
                case "pellet":
                    for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
                    {
                        float ang = Random.Range(-w.spread * 0.5f, w.spread * 0.5f);
                        var d = (Vector2)(Quaternion.Euler(0, 0, ang) * dir);
                        AddShot(w, from, d * speed * Random.Range(0.85f, 1.1f), dmg, new Vector2(0.2f, 0.06f), new Color(1f, 0.9f, 0.55f), WhiteSprite);
                    }
                    Sfx.Play("shotgun", Random.Range(0.92f, 1.05f), 0.9f);
                    WorldCamera.I?.Shake(0.08f);
                    Haptics.Tap(30);
                    MuzzleFlash(from, dir, 1.4f);
                    break;
                case "arrow":
                    AddShot(w, from, dir * speed, dmg, Vector2.one * 0.75f, Color.white, WeaponArt.Arrow);
                    Sfx.Play("bow", Random.Range(0.95f, 1.08f), 0.8f);
                    WorldCamera.I?.Shake(0.015f);
                    Haptics.Tap(12);
                    break;
                case "grenade":
                    {
                        var s = AddShot(w, from, Vector2.zero, dmg, Vector2.one * 0.3f, Color.white, WeaponArt.Grenade);
                        s.from = from; s.to = target.pos + target.pos.normalized * 0f;
                        float dist = (s.to - from).magnitude;
                        s.flight = Mathf.Max(0.35f, dist / speed);
                        s.arc = 0.7f + dist * 0.18f;
                        s.life = s.flight + 0.1f;
                        s.shadow = new GameObject("GrenadeShadow").AddComponent<SpriteRenderer>();
                        s.shadow.transform.SetParent(transform, false);
                        s.shadow.sprite = WorldBuilder.Mist; s.shadow.color = new Color(0, 0, 0, 0.35f);
                        s.shadow.sortingOrder = 29990;
                        Sfx.Play("launch", Random.Range(0.95f, 1.05f), 0.9f);
                        WorldCamera.I?.Shake(0.04f);
                        Haptics.Tap(20);
                        MuzzleFlash(from, dir, 0.9f);
                        break;
                    }
                default:
                    AddShot(w, from, dir * speed, dmg, new Vector2(0.34f, 0.07f), new Color(1f, 0.95f, 0.65f), WhiteSprite);
                    Sfx.Play("shot", Random.Range(0.95f, 1.1f), 0.8f);
                    WorldCamera.I?.Shake(0.03f);
                    Haptics.Tap(15);
                    MuzzleFlash(from, dir, 1f);
                    break;
            }
        }

        static Sprite whiteSprite;
        static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite != null) return whiteSprite;
                var t = new Texture2D(4, 4); var p = new Color[16]; for (int i = 0; i < 16; i++) p[i] = Color.white; t.SetPixels(p); t.Apply();
                whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return whiteSprite;
            }
        }

        WShot AddShot(WeaponDef w, Vector2 from, Vector2 vel, float dmg, Vector2 size, Color col, Sprite sprite)
        {
            var sr = new GameObject("WShot_" + w.id).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite; sr.color = col;
            sr.transform.localScale = new Vector3(size.x, size.y, 1);
            sr.transform.position = from;
            sr.sortingOrder = 30005;
            float speed = Mathf.Max(1f, vel.magnitude);
            var s = new WShot { w = w, sr = sr, pos = from, vel = vel, dmg = dmg, pierce = w.pierce, life = w.range > 0 ? w.range / speed : 1.5f };
            wshots.Add(s);
            return s;
        }

        void MuzzleFlash(Vector2 at, Vector2 dir, float size)
        {
            Particles.I?.Burst(at, "spark", new Color(1f, 0.85f, 0.45f), (int)(4 * size), 3f * size, 0.12f, 0.35f * size, 0f, 0f);
            Particles.I?.Spawn(at + dir * 0.1f, "spark", new Color(1f, 0.95f, 0.7f), (Vector3)dir * 0.5f, 0.08f, 0.6f * size, 0.2f, 0f, 0f);
            Particles.I?.Spawn(at, "square", new Color(0.7f, 0.7f, 0.68f, 0.5f), (Vector3)(dir * 0.6f + Vector2.up * 0.4f), 0.6f, 0.15f * size, 0.35f * size, 0f, 0f);
            muzzleLight.transform.position = at;
            muzzleLight.intensity = 1.6f * size;
            flashT = 0.07f;
        }

        void UpdateWeapons(float dt)
        {
            weaponCd = Mathf.Max(0f, weaponCd - dt);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
            if (flashT > 0f) { flashT -= dt; if (flashT <= 0f && muzzleLight != null) muzzleLight.intensity = 0f; }
            if (shooter != null)
            {
                // tagsueber schaut der Bauer nach vorn, nachts zum Ziel; Rueckstoss beim Schuss
                if (!Active) aimDir = Vector2.Lerp(aimDir, Vector2.down, dt * 2f).normalized;
                shooter.position = ShooterPos - aimDir * recoil * 0.07f;
                if (shooterAnim != null) { shooterAnim.SetFloat("DirX", aimDir.x); shooterAnim.SetFloat("DirY", aimDir.y); shooterAnim.SetFloat("Speed", 0f); }
            }

            foreach (var s in wshots)
            {
                if (s.done) continue;
                s.life -= dt;
                if (s.w.kind == "grenade")
                {
                    s.t += dt;
                    float k = Mathf.Clamp01(s.t / s.flight);
                    var ground = Vector2.Lerp(s.from, s.to, k);
                    float h = 4f * s.arc * k * (1 - k);
                    s.pos = ground + new Vector2(0, h);
                    s.sr.transform.position = s.pos;
                    s.sr.transform.rotation = Quaternion.Euler(0, 0, -k * 720f);
                    s.shadow.transform.position = ground;
                    float ss = 0.32f - h * 0.08f;
                    s.shadow.transform.localScale = new Vector3(ss, ss * 0.4f, 1);
                    if (Random.value < dt * 25) Particles.I?.Spawn(s.pos, "square", new Color(0.75f, 0.75f, 0.72f, 0.5f), Vector3.zero, 0.4f, 0.08f, 0.2f, 0f, 0f);
                    if (k >= 1f) { Explode(s.to, s.w.radius, s.dmg); Finish(s); }
                    continue;
                }
                if (s.life <= 0f) { Finish(s); continue; }
                var step = s.vel * dt;
                s.pos += step;
                s.traveled += step.magnitude;
                s.sr.transform.position = s.pos;
                if (s.vel.sqrMagnitude > 0.01f) s.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg);
                if (s.w.kind == "arrow") s.vel += Vector2.down * dt * 0.6f;   // leichter Bogen
                else if (Random.value < dt * 40) Particles.I?.Spawn(s.pos, "spark", new Color(1f, 0.8f, 0.45f, 0.7f), Vector3.zero, 0.12f, 0.12f, 0f, 0f, 0f);   // Leuchtspur
                foreach (var m in monsters)
                {
                    if (m.dead || s.hit.Contains(m)) continue;
                    if (((m.pos + new Vector2(0, 0.4f)) - s.pos).sqrMagnitude > 0.32f * 0.32f) continue;
                    s.hit.Add(m);
                    float dmg = s.dmg;
                    if (s.w.kind == "pellet") dmg *= Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(s.traveled / Mathf.Max(1f, s.w.range)));   // aus der Naehe am staerksten
                    m.knock += s.vel.normalized * (s.w.kind == "pellet" ? 0.5f : 0.25f);
                    Particles.I?.Burst(s.pos, "square", new Color(0.45f, 0.6f, 0.35f), 4, 2.5f, 0.35f, 0.12f, 6f, 1f);
                    Damage(m, dmg, null);
                    if (s.pierce > 0) { s.pierce--; continue; }
                    Finish(s);
                    break;
                }
            }
            wshots.RemoveAll(s => s.done);
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
            P?.Ring(at, new Color(1f, 0.8f, 0.45f, 0.95f), radius * 1.8f, 0.4f);
            P?.Burst(at + new Vector2(0, 0.2f), "spark", new Color(1f, 0.75f, 0.35f), 18, 5f, 0.35f, 0.5f, 0f, 1f);
            P?.Burst(at + new Vector2(0, 0.2f), "square", new Color(1f, 0.55f, 0.2f), 14, 4f, 0.5f, 0.3f, 3f, 1.5f);
            P?.Burst(at, "square", new Color(0.35f, 0.32f, 0.3f, 0.8f), 12, 1.8f, 1.1f, 0.35f, -0.6f, 1.2f);   // Rauch steigt auf
            P?.Burst(at, "square", new Color(0.4f, 0.28f, 0.18f), 10, 3.5f, 0.6f, 0.18f, 8f, 2f);           // Erdbrocken
            muzzleLight.transform.position = at; muzzleLight.intensity = 3f; flashT = 0.15f;
            Sfx.Play("boom", Random.Range(0.9f, 1.05f), 1f);
            WorldCamera.I?.Shake(0.2f);
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
