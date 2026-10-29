using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    public class WaveResult
    {
        public int wave; public bool victory; public int kills; public int woodLost;
        public List<CatalogDef> unlocked = new List<CatalogDef>();
        public List<WeaponDef> weapons = new List<WeaponDef>();
    }

    /// <summary>
    /// Tag -> Abend (Warnung) -> Nacht: Zombies schlurfen aus dem Wald heran und wollen zum Haus.
    /// Reife Pflanzen auf den Feldern schiessen je nach Eigenschaften (Flaeche, Distanz, Projektil, Wirkung, Zusatzeffekt),
    /// der Spieler schiesst mit seiner Waffe (Tipp auf einen Zombie), Zaeune halten Zombies auf.
    /// Jede ueberstandene Welle schaltet neue Pflanzenarten frei (siehe plants.json -> catalog.unlockWave).
    /// Zum Testen startet automatisch eine Welle (siehe plants.json -> waves.autoWave).
    /// </summary>
    public partial class NightWave : MonoBehaviour
    {
        public static NightWave I { get; private set; }

        class Monster
        {
            public MonsterDefW def;
            public Transform root; public SpriteRenderer sr; public Transform eyes;
            // Zombie-Figur (geriggt, animiert); sr ist dann null
            public Animator anim; public SpriteRenderer[] parts; public Color[] partCols; public SortingGroup group;
            // Pixel-Zombie mit Laufanimation in 8 Richtungen (Assets/Characters)
            public Sprite[] walk; public float animT; public Color tint = Color.white;
            public Vector2 pos; public float hp, maxHp, hop, hitFlash;
            public float slowT, slowAmt, burnT, burnDps, poisonT, poisonDps; public int poisonStacks;
            public Vector2 knock;
            public bool dead;
            public float bashT;   // schlaegt gerade auf einen Zaun ein
            public float Speed => def.speed * (slowT > 0 ? 1f - slowAmt : 1f);
        }

        class Shot
        {
            public SpriteRenderer sr; public Vector2 pos, vel; public Monster target; public PlantGenome src;
            public float speed, damage, homing, life; public int pierce; public HashSet<Monster> hit = new HashSet<Monster>();
            public bool fireball; public float power = 1f; public SpriteRenderer core;
            public SpriteRenderer hi; public List<SpriteRenderer> flames;   // 3D-Feuerball: Glanzpunkt, lodernde Flammen
            public bool gas;   // Giftspucke: zerplatzt zu einer Giftwolke
            public GameObject fxBall, fxTrail;   // Cartoon-FX (JMO)
            public SpriteRenderer kern, kernHot;  // rot leuchtender, wabernder Kern des Feuerballs
            // Bogenwurf (Feuerweizen): nach oben gespuckt, faellt wie eine Granate aufs Ziel
            public bool lob; public Vector2 from, to; public float t, flight, arc; public SpriteRenderer shadow;
            public bool done;
        }

        public bool Active { get; private set; }
        public int WaveNumber => (wb != null ? wb.Save.wave : 0) + 1;
        public float Countdown { get; private set; }      // Zeit bis zum Abend
        public bool Evening { get; private set; }
        public float EveningLeft { get; private set; }    // Zeit bis die Zombies kommen
        public float NightLeft { get; private set; }      // Zeit bis zum Morgen
        public float NightAmount { get; private set; }
        public float DuskAmount { get; private set; }
        public int MonstersLeft => monsters.Count + toSpawn;
        public float HouseHp { get; private set; }
        public float HouseHpMax => B.waves.houseHp;

        public event Action<float> EveningStarted;
        public event Action<int> WaveStarted;
        public event Action<WaveResult> WaveEnded;

        WorldBuilder wb;
        PlantBalance B => PlantBalance.I;
        readonly List<Monster> monsters = new List<Monster>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<(SpriteRenderer sr, float t)> beams = new List<(SpriteRenderer, float)>();
        readonly Dictionary<PlantedState, float> cooldowns = new Dictionary<PlantedState, float>();
        int toSpawn; float spawnT;
        WaveResult result;
        Light2D global; Light2D window;
        System.Random rnd = new System.Random();
        static Sprite eyeSprite, white;

        void Awake() { I = this; }

        void Start()
        {
            wb = WorldBuilder.I;
            foreach (var l in FindObjectsOfType<Light2D>())
            {
                if (l.lightType == Light2D.LightType.Global) global = l;
                else if (l.name == "WindowLight") window = l;
            }
            Countdown = B.waves.firstWaveDelay;
            HouseHp = B.waves.houseHp;
            BuildWeaponFx();
        }

        // ------------------------------------------------------------------ Ablauf

        /// <summary>Test-Knopf: Abend sofort (kurz) beginnen lassen, danach kommt die Welle.</summary>
        public void StartWave()
        {
            if (Active || Evening || wb == null) return;
            BeginEvening(B.waves.testEveningDuration > 0 ? B.waves.testEveningDuration : 5f);
        }

        void BeginEvening(float duration)
        {
            Evening = true;
            EveningLeft = duration;
            Sfx.Play("bell", 1f, 0.8f);
            EveningStarted?.Invoke(duration);
        }

        void BeginNight()
        {
            Evening = false;
            Active = true;
            toSpawn = B.waves.baseCount + B.waves.countPerWave * (WaveNumber - 1);
            spawnT = 1.5f;
            HouseHp = B.waves.houseHp;
            cooldowns.Clear();
            NightLeft = B.waves.nightDuration > 0 ? B.waves.nightDuration : 60f;
            result = new WaveResult { wave = WaveNumber };
            Sfx.Play("sleep", 0.8f);
            WaveStarted?.Invoke(WaveNumber);
        }

        void EndWave(bool victory)
        {
            Active = false;
            result.victory = victory;
            foreach (var m in monsters) Poof(m, false);
            monsters.Clear();
            foreach (var s in shots) { if (s.sr != null) Destroy(s.sr.gameObject); if (s.shadow != null) Destroy(s.shadow.gameObject); }
            shots.Clear();
            ClearWeaponShots();
            foreach (var c in gasClouds) { foreach (var p in c.puffs) if (p != null) Destroy(p.gameObject); WorldFx.Release(c.fx, 2f); }
            gasClouds.Clear();
            toSpawn = 0;
            if (victory)
            {
                wb.Save.wave++;
                result.unlocked = wb.UnlockForWave(wb.Save.wave);
                if (B.weapons != null) foreach (var w in B.weapons) if (w.unlockWave == wb.Save.wave) result.weapons.Add(w);
            }
            else
            {
                result.woodLost = Mathf.Min(wb.Save.wood, B.waves.woodLossOnDefeat);
                wb.Save.wood -= result.woodLost;
            }
            wb.NotifyInventory();
            Countdown = B.waves.dayDuration;
            WaveEnded?.Invoke(result);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            NightAmount = Mathf.MoveTowards(NightAmount, Active ? 1f : 0f, dt * 0.5f);
            DuskAmount = Mathf.MoveTowards(DuskAmount, Active || Evening ? 1f : 0f, dt * 0.35f);
            UpdateLight();
            UpdateBeams(dt);
            UpdateGas(dt);
            if (wb == null) return;
            UpdateWeapons(dt);

            if (!Active)
            {
                if (Evening)
                {
                    EveningLeft -= dt;
                    if (EveningLeft <= 0f) BeginNight();
                }
                else if (B.waves.autoWave && wb.Save.starterChosen)
                {
                    Countdown -= dt;
                    if (Countdown <= 0f) BeginEvening(B.waves.eveningDuration > 0 ? B.waves.eveningDuration : 12f);
                }
                return;
            }

            if (toSpawn > 0)
            {
                spawnT -= dt;
                if (spawnT <= 0f) { Spawn(); toSpawn--; spawnT = B.waves.spawnInterval * (0.6f + (float)rnd.NextDouble() * 0.8f); }
            }
            UpdateMonsters(dt);
            UpdatePlantsFire(dt);
            UpdateShots(dt);
            monsters.RemoveAll(m => m.dead);
            shots.RemoveAll(s => s.done);
            if (HouseHp <= 0f) { EndWave(false); return; }
            if (toSpawn == 0 && monsters.Count == 0) { EndWave(true); return; }
            // Morgengrauen: die Nacht ist vorbei, uebrige Zombies zerfallen im ersten Licht
            NightLeft -= dt;
            if (NightLeft <= 0f)
            {
                foreach (var m in monsters) if (!m.dead) { Particles.I?.Burst(m.pos + new Vector2(0, 0.4f), "square", new Color(0.55f, 0.5f, 0.45f, 0.8f), 10, 1.5f, 1f, 0.2f, -0.5f, 1.5f); Poof(m, true); }
                toSpawn = 0;
                EndWave(true);
            }
        }

        void UpdateLight()
        {
            // Tag -> warmer, tief stehender Abend (orange-rosa) -> kuehle blaue Nacht
            float n = NightAmount, d = DuskAmount;
            if (global != null)
            {
                var day = new Color(1f, 0.97f, 0.9f);
                var dusk = new Color(1f, 0.55f, 0.38f);
                var night = new Color(0.48f, 0.55f, 0.85f);
                global.color = Color.Lerp(Color.Lerp(day, dusk, d), night, n);
                global.intensity = Mathf.Lerp(Mathf.Lerp(1f, 0.74f, d), 0.55f, n);
            }
            if (window != null) window.intensity = Mathf.Max(n * 1.3f, d * 0.6f);
        }

        // ------------------------------------------------------------------ Monster

        static Sprite EyeSprite
        {
            get
            {
                if (eyeSprite != null) return eyeSprite;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16));
                        float dp = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(17.5f, 14.5f));
                        Color c = Color.clear;
                        if (d < 15) c = d > 13.5f ? new Color(0.15f, 0.08f, 0.05f) : Color.white;
                        if (dp < 7.5f) c = new Color(0.08f, 0.05f, 0.04f);
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(15f, 18f)) < 2.5f) c = Color.white;
                        c.a *= Mathf.Clamp01(15.5f - d);
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                eyeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return eyeSprite;
            }
        }

        static Sprite White
        {
            get
            {
                if (white != null) return white;
                var t = new Texture2D(4, 4); var p = new Color[16]; for (int i = 0; i < 16; i++) p[i] = Color.white; t.SetPixels(p); t.Apply();
                white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return white;
            }
        }

        void Spawn()
        {
            int wave = WaveNumber;
            float total = 0;
            foreach (var d in B.monsters) if (d.firstWave <= wave) total += d.weight;
            float roll = (float)rnd.NextDouble() * total;
            MonsterDefW def = B.monsters[0];
            foreach (var d in B.monsters) { if (d.firstWave > wave) continue; roll -= d.weight; if (roll <= 0) { def = d; break; } }

            // aus dem Waldrand: links, rechts oder unten
            float hw = WorldBuilder.MapW / 2f, hh = WorldBuilder.MapH / 2f;
            Vector2 p;
            int side = rnd.Next(3);
            if (side == 0) p = new Vector2(-hw + 0.5f, -hh + 2 + (float)rnd.NextDouble() * (hh + 1));
            else if (side == 1) p = new Vector2(hw - 0.5f, -hh + 2 + (float)rnd.NextDouble() * (hh + 1));
            else p = new Vector2(-hw + 2 + (float)rnd.NextDouble() * (hw * 2 - 4), -hh + 0.8f);

            var root = new GameObject("Monster_" + def.id).transform;
            root.SetParent(transform, false);
            float hp0 = def.hp * (1 + B.waves.hpGrowthPerWave * (wave - 1));
            var zart = WorldArtSet.I;
            if (zart != null && zart.HasZombieWalk)
            {
                var zm = BuildPixelZombie(def, root, zart.zombieWalk);
                zm.pos = p; zm.hp = zm.maxHp = hp0; zm.hop = (float)rnd.NextDouble() * 6; zm.animT = (float)rnd.NextDouble() * 8f;
                root.position = p;
                monsters.Add(zm);
                Particles.I?.Burst(p, "square", new Color(0.35f, 0.26f, 0.18f), 10, 2.2f, 0.5f, 0.3f, 6f, 2f);
                if (rnd.NextDouble() < 0.5) Sfx.Play("groan", 0.8f + (float)rnd.NextDouble() * 0.4f, 0.45f);
                return;
            }
            var zprefab = zart != null ? zart.zombiePrefab : null;
            if (zprefab != null)
            {
                var zm = BuildZombie(def, root, zprefab);
                zm.pos = p; zm.hp = zm.maxHp = hp0; zm.hop = (float)rnd.NextDouble() * 6;
                root.position = p;
                monsters.Add(zm);
                Particles.I?.Burst(p, "square", new Color(0.35f, 0.26f, 0.18f), 10, 2.2f, 0.5f, 0.3f, 6f, 2f);
                if (rnd.NextDouble() < 0.5) Sfx.Play("groan", 0.8f + (float)rnd.NextDouble() * 0.4f, 0.45f);
                return;
            }
            var sr = new GameObject("Body").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            var art = WorldArtSet.I;
            sr.sprite = art != null ? art.Get(def.art) : null;
            float s = def.scale;
            sr.transform.localScale = new Vector3(s, s, 1);
            if (sr.sprite != null) sr.transform.localPosition = new Vector3(0, -sr.sprite.bounds.min.y * s, 0);
            // Augen machen aus Pilzen und Pflanzen Monster
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            float h = sr.sprite != null ? sr.sprite.bounds.size.y * s : 0.6f;
            float w = sr.sprite != null ? sr.sprite.bounds.size.x * s : 0.6f;
            eyes.localPosition = new Vector3(0, h * 0.42f, 0);
            for (int i = 0; i < 2; i++)
            {
                var e = new GameObject("Eye").AddComponent<SpriteRenderer>();
                e.transform.SetParent(eyes, false);
                e.sprite = EyeSprite;
                float es = Mathf.Clamp(w * 0.22f, 0.12f, 0.3f);
                e.transform.localScale = new Vector3(es, es, 1);
                e.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * w * 0.14f, 0, 0);
            }
            float hp = def.hp * (1 + B.waves.hpGrowthPerWave * (wave - 1));
            var m = new Monster { def = def, root = root, sr = sr, eyes = eyes, pos = p, hp = hp, maxHp = hp, hop = (float)rnd.NextDouble() * 6 };
            root.position = p;
            monsters.Add(m);
            Particles.I?.Burst(p, "leaf", new Color(0.4f, 0.6f, 0.3f), 8, 2.5f, 0.5f, 0.6f, 4f, 1.5f);
        }

        const float PixelZombieHeight = 1.25f;   // Figurhoehe der Pixel-Frames (160 px bei 128 px/Einheit)

        /// <summary>Zombie aus den Pixel-Frames: Laufanimation in 8 Richtungen, Schatten, leichte Faerbung je Art.</summary>
        Monster BuildPixelZombie(MonsterDefW def, Transform root, Sprite[] walk)
        {
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root, false);
            shadow.sprite = WorldBuilder.Mist; shadow.color = new Color(0, 0, 0, 0.35f);
            shadow.transform.localScale = new Vector3(0.55f * def.scale, 0.17f * def.scale, 1);
            shadow.sortingOrder = -31300;
            var sr = new GameObject("Body").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.sprite = walk[0];
            sr.transform.localScale = Vector3.one * (def.scale / PixelZombieHeight);
            var tint = Color.white;
            if (!string.IsNullOrEmpty(def.tint) && ColorUtility.TryParseHtmlString(def.tint, out var tc)) tint = Color.Lerp(Color.white, tc, 0.3f);
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            return new Monster { def = def, root = root, sr = sr, eyes = eyes, walk = walk, tint = tint };
        }

        /// <summary>Richtung -> Index in der Frame-Liste (S, SO, O, NO, N, NW, W, SW).</summary>
        static int DirIndex(Vector2 d)
        {
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // Osten = 0, Norden = 90
            int k = Mathf.RoundToInt(ang / 45f);                   // -4..4
            return ((2 + k) % 8 + 8) % 8;
        }

        const float ZombieNativeHeight = 3.5f;   // Hoehe der Happy-Harvest-Figur in Welteinheiten (unskaliert)

        /// <summary>Zombie aus der geriggten Happy-Harvest-Figur: fahle Haut, langsamer Schlurf-Gang.</summary>
        Monster BuildZombie(MonsterDefW def, Transform root, GameObject prefab)
        {
            // eigener Zwischenknoten: der Animator der Figur setzt die Skalierung ihres Wurzelobjekts selbst
            var holder = new GameObject("Rig").transform;
            holder.SetParent(root, false);
            holder.localScale = Vector3.one * (def.scale / ZombieNativeHeight);
            var z = Instantiate(prefab, holder, false);
            z.name = "Zombie";
            var parts = z.GetComponentsInChildren<SpriteRenderer>(true);
            var skin = new Color(0.6f, 0.75f, 0.5f);
            if (!string.IsNullOrEmpty(def.tint)) ColorUtility.TryParseHtmlString(def.tint, out skin);
            var cols = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) cols[i] = ZombiePartColor(parts[i].name, skin);
            var anim = z.GetComponentInChildren<Animator>();
            if (anim != null) { anim.speed = 0.5f + (float)rnd.NextDouble() * 0.2f; anim.gameObject.AddComponent<ZombieAnimEvents>(); }
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            return new Monster { def = def, root = root, eyes = eyes, anim = anim, parts = parts, partCols = cols, group = root.gameObject.AddComponent<SortingGroup>() };
        }

        /// <summary>Faerbung je Koerperteil: faulig-gruene Haut, graues Haar, verblasste Kleidung, gluehende Augen.</summary>
        static Color ZombiePartColor(string part, Color skin)
        {
            if (part.StartsWith("pupil")) return new Color(2.4f, 0.35f, 0.25f);          // HDR -> leuchtet mit Bloom
            if (part.StartsWith("eye_")) return new Color(0.92f, 0.95f, 0.7f);
            if (part.StartsWith("eyebrow") || part.StartsWith("hair")) return new Color(0.45f, 0.47f, 0.42f);
            if (part.StartsWith("mouth")) return new Color(0.35f, 0.2f, 0.2f);
            if (part.StartsWith("head") || part.StartsWith("ear") || part.StartsWith("nose") || part.StartsWith("hand") || part.StartsWith("eyelid") || part.StartsWith("arm"))
                return skin;
            return new Color(0.62f, 0.64f, 0.56f);                                       // Kleidung: verblasst, schmutzig
        }

        Vector2 HouseTarget => WorldBuilder.HousePos + new Vector2(0, -0.8f);

        void UpdateMonsters(float dt)
        {
            foreach (var m in monsters)
            {
                if (m.dead) continue;
                m.hitFlash = Mathf.Max(0, m.hitFlash - dt * 5f);
                if (m.slowT > 0) m.slowT -= dt;
                if (m.burnT > 0) { m.burnT -= dt; Damage(m, m.burnDps * dt, null, silent: true); }
                if (m.poisonT > 0) { m.poisonT -= dt; Damage(m, m.poisonDps * m.poisonStacks * dt, null, silent: true); } else m.poisonStacks = 0;
                if (m.dead) continue;

                var to = HouseTarget - m.pos;
                float dist = to.magnitude;
                if (dist < 0.7f)
                {
                    HouseHp -= m.def.damage;
                    WorldCamera.I?.Shake(0.12f);
                    Haptics.Tap(40);
                    Sfx.Play("error", 0.9f);
                    wb.RaiseFloater(HouseTarget + new Vector2(0, 1.5f), "-" + Mathf.RoundToInt(m.def.damage), new Color(1f, 0.45f, 0.4f));
                    Poof(m, true);
                    continue;
                }
                // leicht schlaengelnd auf das Haus zu, Rueckstoss klingt ab; ein Zaun im Weg wird zerschlagen
                var dir = to / dist;
                var side = new Vector2(-dir.y, dir.x) * Mathf.Sin(m.hop * 0.7f) * 0.35f;
                var step = (dir + side).normalized * m.Speed * dt;
                var fence = wb.FenceAt(m.pos + (dir + side).normalized * 0.18f);
                if (fence != null)
                {
                    m.bashT += dt;
                    wb.HitFence(fence, m.def.damage * 0.4f * dt);
                    m.pos += m.knock * dt;
                    if (m.bashT > 0.55f) { m.bashT = 0f; Sfx.Play("chop", 0.6f + (float)rnd.NextDouble() * 0.2f, 0.35f); }
                }
                else { m.bashT = 0f; m.pos += step + m.knock * dt; }
                m.knock = Vector2.MoveTowards(m.knock, Vector2.zero, dt * 6f);
                Color c = Color.white;
                if (m.slowT > 0) c = new Color(0.7f, 0.88f, 1f);
                if (m.burnT > 0) c = new Color(1f, 0.75f, 0.55f);
                if (m.poisonT > 0) c = Color.Lerp(c, new Color(0.8f, 0.65f, 1f), 0.6f);
                if (m.walk != null)
                {
                    // Pixel-Zombie: Bild je Richtung, Tempo der Animation passt zur Laufgeschwindigkeit
                    var face = (dir + side * 0.5f).normalized;
                    // feste, zuegige Schrittfolge; nur Eis (verlangsamt) bremst sie mit
                    float fps = 12f * (m.Speed / Mathf.Max(0.01f, m.def.speed)) * (fence != null ? 0.6f : 1f);
                    m.animT += dt * fps;
                    m.sr.sprite = m.walk[DirIndex(face) * 8 + ((int)m.animT % 8)];
                    float bash = fence != null ? Mathf.Max(0, Mathf.Sin(m.bashT / 0.55f * Mathf.PI * 2f)) : 0f;
                    m.root.position = m.pos + dir * bash * 0.1f;
                    m.root.localScale = new Vector3(1 + m.hitFlash * 0.15f, 1 - m.hitFlash * 0.1f, 1);
                    m.sr.sortingOrder = WorldBuilder.Order(m.pos.y) + 1;
                    m.sr.color = Color.Lerp(Color.Lerp(m.tint, m.tint * c, 0.6f), new Color(1f, 0.35f, 0.3f), m.hitFlash);
                    if (rnd.NextDouble() < dt * 0.08) Sfx.Play("groan", 0.75f + (float)rnd.NextDouble() * 0.4f, 0.35f);
                }
                else if (m.group != null)
                {
                    // Zombie: schwerfaelliges Schwanken statt Huepfen, Animation zeigt in Laufrichtung
                    m.hop += dt * (2.2f + m.Speed * 1.5f);
                    float bash = fence != null ? Mathf.Max(0, Mathf.Sin(m.bashT / 0.55f * Mathf.PI * 2f)) : 0f;   // Ausholen und Zuschlagen
                    m.root.position = m.pos + new Vector2(0, Mathf.Abs(Mathf.Sin(m.hop)) * 0.025f) + dir * bash * 0.08f;
                    m.root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(m.hop * 0.5f) * 7f - dir.x * (7f + bash * 10f));
                    m.root.localScale = new Vector3(1 + m.hitFlash * 0.12f, 1 - m.hitFlash * 0.08f, 1);
                    if (m.anim != null) { m.anim.SetFloat("DirX", dir.x); m.anim.SetFloat("DirY", dir.y); m.anim.SetFloat("Speed", fence != null ? 0f : 1f); }
                    m.group.sortingOrder = WorldBuilder.Order(m.pos.y) + 1;
                    for (int i = 0; i < m.parts.Length; i++)
                        if (m.parts[i] != null) m.parts[i].color = Color.Lerp(Color.Lerp(m.partCols[i], m.partCols[i] * c, 0.5f), new Color(1f, 0.35f, 0.3f), m.hitFlash);
                    if (rnd.NextDouble() < dt * 0.08) Sfx.Play("groan", 0.75f + (float)rnd.NextDouble() * 0.4f, 0.35f);
                }
                else
                {
                    m.hop += dt * (4f + m.Speed * 3f);
                    float hopY = Mathf.Abs(Mathf.Sin(m.hop)) * 0.12f;
                    float sq = (1 - Mathf.Abs(Mathf.Sin(m.hop))) * 0.15f;
                    m.root.position = m.pos + new Vector2(0, hopY);
                    float flip = dir.x < 0 ? -1 : 1;
                    m.root.localScale = new Vector3((1 + sq + m.hitFlash * 0.15f) * flip, 1 - sq + m.hitFlash * 0.1f, 1);
                    m.sr.sortingOrder = WorldBuilder.Order(m.pos.y) + 1;
                    foreach (Transform e in m.eyes) e.GetComponent<SpriteRenderer>().sortingOrder = m.sr.sortingOrder + 1;
                    m.sr.color = Color.Lerp(c, new Color(1f, 0.35f, 0.3f), m.hitFlash);
                }
                if (m.burnT > 0 && rnd.NextDouble() < dt * 10) Particles.I?.Spawn(m.pos + new Vector2(0, 0.4f), "square", new Color(1f, 0.6f, 0.2f), new Vector3(0, 1.2f, 0), 0.5f, 0.6f, 0f, 0f);
            }
        }

        /// <summary>Position des naechsten Zombies (fuer die Blicke der Pflanzen).</summary>
        public bool NearestMonsterPos(Vector2 p, float range, out Vector2 pos)
        {
            var m = Nearest(p, range);
            pos = m != null ? m.pos + new Vector2(0, 0.45f) : p;
            return m != null;
        }

        Monster Nearest(Vector2 p, float range, HashSet<Monster> exclude = null)
        {
            Monster best = null; float bd = range * range;
            foreach (var m in monsters)
            {
                if (m.dead || (exclude != null && exclude.Contains(m))) continue;
                float d = (m.pos - p).sqrMagnitude;
                if (d <= bd) { bd = d; best = m; }
            }
            return best;
        }

        // ------------------------------------------------------------------ Pflanzen schiessen

        void UpdatePlantsFire(float dt)
        {
            foreach (var p in wb.Planted)
            {
                if (!p.Grown) continue;   // nur reife Pflanzen verteidigen
                var g = p.Genome;
                float gcd = g.Cooldown * B.LevelCooldown(p.Lv), grange = g.Range + B.LevelRange(p.Lv);
                float cd = cooldowns.TryGetValue(p, out var v) ? v : (float)rnd.NextDouble() * gcd;
                cd -= dt;
                if (cd <= 0f)
                {
                    var muzzle = wb.PlantMuzzle(p);
                    var target = Nearest(muzzle, grange);
                    if (target != null) { Fire(p, muzzle, target); cd = gcd; }
                    else cd = 0.15f;
                }
                cooldowns[p] = cd;
            }
        }

        void Fire(PlantedState plant, Vector2 muzzle, Monster target)
        {
            var g = plant.Genome;
            wb.KickPlant(plant);
            var proj = B.Option(PlantAttr.Projectile, g.projectile.id);
            // Zielauswahl nach Flaechenmuster
            var targets = new List<Monster> { target };
            int extra = 0; float fan = 0f;
            switch (g.pattern.id)
            {
                case "double":
                    {
                        var ex = new HashSet<Monster> { target };
                        var t2 = Nearest(muzzle, g.Range, ex);
                        targets.Add(t2 ?? target);
                        if (g.pattern.level >= 4) { ex.Add(t2); var t3 = Nearest(muzzle, g.Range, ex); if (t3 != null) targets.Add(t3); }
                        break;
                    }
                case "spread": extra = 2 + (g.pattern.level >= 3 ? 1 : 0) + (g.pattern.level >= 5 ? 1 : 0); fan = 13f; break;
            }
            if (g.effect.id == "multishot") { extra += g.effect.level >= 4 ? 2 : 1; if (fan == 0) fan = 10f; }

            float dmg = g.Damage * B.LevelDamage(plant.Lv);   // zusammengefuehrte Pflanzen treffen haerter
            var col = g.ElementColor;
            if (proj != null && proj.id == "bolt")
            {
                int tierB = B.Tier(plant.Lv);
                wb.KickPlant(plant);
                foreach (var t in targets) LightningStrike(g, muzzle, t, dmg, tierB);
                for (int i = 0; i < extra; i++) { var t = Nearest(muzzle, g.Range, new HashSet<Monster>(targets)); if (t == null) break; targets.Add(t); LightningStrike(g, muzzle, t, dmg * 0.8f, tierB); }
                return;
            }
            if (proj != null && proj.id == "beam")
            {
                foreach (var t in targets) { Beam(muzzle, t.pos + new Vector2(0, 0.35f), col); Hit(t, dmg, g, null); }
                for (int i = 0; i < extra; i++) { var t = Nearest(muzzle, g.Range, new HashSet<Monster>(targets)); if (t == null) break; targets.Add(t); Beam(muzzle, t.pos + new Vector2(0, 0.35f), col); Hit(t, dmg * 0.8f, g, null); }
                Sfx.Play("crit", 1.4f, 0.35f);
                return;
            }
            int tier = B.Tier(plant.Lv);
            if (proj != null && proj.id == "gas") wb.SpitPlant(plant);
            if (proj != null && proj.id == "fireball") { wb.SpitPlant(plant); Sfx.Play("pop", 0.7f, 0.8f); Sfx.Play("launch", 0.75f + rnd.Next(0, 10) * 0.01f, 0.55f + tier * 0.08f); Particles.I?.Burst(muzzle, "spark", new Color(1f, 0.6f, 0.2f), 6 + tier * 2, 2.5f, 0.3f, 0.12f, 0f, 1f); }
            foreach (var t in targets) LaunchShot(g, proj, muzzle, t, 0f, dmg, tier);
            for (int i = 0; i < extra; i++)
            {
                float ang = ((i / 2) + 1) * fan * (i % 2 == 0 ? 1 : -1);
                LaunchShot(g, proj, muzzle, target, ang, dmg * 0.8f, tier);
            }
            Sfx.Play("pop", 1.2f + (float)rnd.NextDouble() * 0.3f, 0.45f);
        }

        void LaunchShot(PlantGenome g, OptionDef proj, Vector2 from, Monster target, float angle, float dmg, int tier = 1)
        {
            if (proj.id == "fireball") { LaunchFireball(g, proj, from, target, angle, dmg, tier); return; }
            if (proj.id == "gas") { LaunchGas(g, proj, from, target, angle, dmg, tier); return; }
            var sr = new GameObject("Shot").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = WorldBuilder.Mist;
            var col = g.ElementColor;
            sr.color = new Color(Mathf.Min(1, col.r * 1.3f), Mathf.Min(1, col.g * 1.3f), Mathf.Min(1, col.b * 1.3f), 1f);
            float size = proj.id == "spore" ? 0.42f : proj.id == "thorn" ? 0.22f : 0.3f;
            sr.transform.localScale = proj.id == "thorn" ? new Vector3(size * 2.2f, size * 0.7f, 1) : new Vector3(size, size, 1);
            sr.sortingOrder = 30000;
            var dir = ((target.pos + new Vector2(0, 0.35f)) - from).normalized;
            dir = Quaternion.Euler(0, 0, angle) * dir;
            if (proj.id == "petal") dir = Quaternion.Euler(0, 0, (rnd.NextDouble() < 0.5 ? 1 : -1) * 40f) * dir;
            int pierce = (g.pattern.id == "pierce" ? 1 + g.pattern.level : 0) + (proj.id == "thorn" ? 1 : 0);
            shots.Add(new Shot { sr = sr, pos = from, vel = dir * proj.speed, target = target, src = g, speed = proj.speed, damage = dmg, homing = proj.homing, pierce = pierce, life = 3f });
            sr.transform.position = from;
        }

        void UpdateShots(float dt)
        {
            foreach (var s in shots)
            {
                if (s.done) continue;
                if (s.lob) { UpdateLob(s, dt); continue; }
                s.life -= dt;
                if (s.life <= 0) { s.done = true; Destroy(s.sr.gameObject); continue; }
                if (s.target != null && !s.target.dead && s.homing > 0)
                {
                    var want = ((s.target.pos + new Vector2(0, 0.35f)) - s.pos).normalized * s.speed;
                    s.vel = Vector2.Lerp(s.vel, want, Mathf.Clamp01(s.homing * dt * 6f));
                }
                s.pos += s.vel * dt;
                s.sr.transform.position = s.pos;
                if (s.vel.sqrMagnitude > 0.01f) s.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg);
                if (s.fireball) FireballTrail(s, dt);
                else if (s.gas) { s.sr.transform.localScale = new Vector3(0.3f + 0.04f * s.power, 0.26f + 0.04f * s.power, 1) * (1f + Mathf.Sin(Time.time * 25f) * 0.08f); if (rnd.NextDouble() < dt * 25) Particles.I?.Spawn(s.pos, "square", new Color(0.55f, 0.95f, 0.3f), Vector3.down * 0.3f, 0.5f, 0.05f, 0.02f, 3f); }
                else if (rnd.NextDouble() < dt * 30) Particles.I?.Spawn(s.pos, "spark", s.src.ElementColor, Vector3.zero, 0.3f, 0.5f, 0f, 0f);
                // Treffer
                foreach (var m in monsters)
                {
                    if (m.dead || s.hit.Contains(m)) continue;
                    if (((m.pos + new Vector2(0, 0.3f)) - s.pos).sqrMagnitude > 0.35f * 0.35f) continue;
                    s.hit.Add(m);
                    Hit(m, s.damage, s.src, s.pos);
                    if (s.fireball) FireballBlast(s, m);
                    if (s.gas) SpawnGas(s.pos, s.power, s.damage, s.src);
                    if (s.pierce > 0) { s.pierce--; s.damage *= 0.85f; continue; }
                    s.done = true; Destroy(s.sr.gameObject);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Treffer, Wirkungen, Zusatzeffekte

        void Hit(Monster m, float dmg, PlantGenome g, Vector2? impact)
        {
            if (m.dead) return;
            var pos = impact ?? m.pos + new Vector2(0, 0.3f);
            bool crit = false;
            if (g.effect.id == "crit" && rnd.NextDouble() < 0.08 * g.effect.level) { dmg *= 2f; crit = true; }
            if (m.def.weakTo == g.element.id) dmg *= 1.5f;
            Damage(m, dmg, g, crit: crit);
            ApplyElement(m, g, dmg);

            switch (g.effect.id)
            {
                case "explosion":
                    {
                        float r = 0.55f + 0.15f * g.effect.level;
                        Particles.I?.Ring(pos, new Color(1f, 0.75f, 0.4f, 0.9f), r * 1.6f, 0.35f);
                        Particles.I?.Burst(pos, "square", new Color(1f, 0.6f, 0.25f), 10, 3.5f, 0.4f, 0.8f, 2f, 0.5f);
                        WorldCamera.I?.Shake(0.04f);
                        Sfx.Play("break", 1.3f, 0.5f);
                        foreach (var o in monsters) if (o != m && !o.dead && (o.pos - m.pos).magnitude < r) Damage(o, dmg * (0.3f + 0.1f * g.effect.level), g);
                        break;
                    }
                case "toxin": AddPoison(m, dmg * 0.12f * g.effect.level, g.effect.level); break;
                case "knockback":
                    m.knock += (m.pos - HouseTarget).normalized * (0.6f + 0.35f * g.effect.level);
                    break;
            }

            if (g.pattern.id == "splash")
            {
                float r = 0.5f + 0.15f * g.pattern.level;
                Particles.I?.Ring(pos, g.ElementColor, r * 1.5f, 0.3f);
                foreach (var o in monsters) if (o != m && !o.dead && (o.pos - m.pos).magnitude < r) { Damage(o, dmg * 0.6f, g); ApplyElement(o, g, dmg * 0.6f); }
            }
            if (g.pattern.id == "chain") Chain(m, g, dmg * 0.7f, 1 + g.pattern.level / 2);
            if (g.element.id == "lightning" && g.projectile.id != "bolt") Chain(m, g, dmg * 0.5f, 1 + g.element.level / 2);
        }

        // ------------------------------------------------------------------ Feuerball (Feuerweizen)

        void LaunchFireball(PlantGenome g, OptionDef proj, Vector2 from, Monster target, float angle, float dmg, int tier)
        {
            float size = 0.42f + 0.13f * (tier - 1);
            // aussen: gluehender Hof
            var sr = new GameObject("Fireball").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = WorldBuilder.Mist;
            sr.color = new Color(2.2f, 0.75f, 0.18f, 0.6f);
            sr.transform.localScale = Vector3.one * size;
            sr.sortingOrder = 30000;
            // innen: schattierte, glutfluessige Kugel (dreht sich), dazu ein fester Glanzpunkt
            var core = new GameObject("Orb").AddComponent<SpriteRenderer>();
            core.transform.SetParent(sr.transform, false);
            core.sprite = FireOrb;
            core.color = new Color(1.7f, 1.35f, 1.1f, 1f);
            core.transform.localScale = Vector3.one * 0.7f;
            core.sortingOrder = 30003;
            var hi = new GameObject("Highlight").AddComponent<SpriteRenderer>();
            hi.transform.SetParent(sr.transform, false);
            hi.sprite = WorldBuilder.Mist;
            hi.color = new Color(3f, 2.8f, 2.2f, 0.9f);
            hi.transform.localPosition = new Vector3(-0.11f, 0.12f, 0);
            hi.transform.localScale = new Vector3(0.24f, 0.16f, 1);
            hi.transform.localRotation = Quaternion.Euler(0, 0, 35f);
            hi.sortingOrder = 30004;
            // lodernde Flammenzungen rundherum (hinten und vorne)
            var flames = new List<SpriteRenderer>();
            int nf = 7 + tier;
            for (int i = 0; i < nf; i++)
            {
                var f = new GameObject("Flame").AddComponent<SpriteRenderer>();
                f.transform.SetParent(sr.transform, false);
                f.sprite = PlantCreature.Get("flame");
                f.color = i % 3 == 0 ? new Color(2.6f, 1.6f, 0.5f, 0.95f) : new Color(2.4f, 0.8f, 0.2f, 0.9f);
                f.sortingOrder = i % 3 == 0 ? 30005 : 30002;
                flames.Add(f);
            }
            // Ziel vorhalten: dort, wo der Zombie bei der Landung ungefaehr sein wird
            float dist0 = (target.pos - from).magnitude;
            float flight = 0.75f + dist0 * 0.09f;
            var lead = target.pos + (HouseTarget - target.pos).normalized * target.Speed * flight * 0.8f;
            if (angle != 0f) lead += (Vector2)(Quaternion.Euler(0, 0, 90f) * (lead - from).normalized) * Mathf.Sign(angle) * (0.5f + Mathf.Abs(angle) / 30f);
            var shadow = new GameObject("FireballShadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(transform, false);
            shadow.sprite = WorldBuilder.Mist;
            shadow.color = new Color(0, 0, 0, 0f);
            shadow.sortingOrder = 29990;
            shots.Add(new Shot { sr = sr, core = core, hi = hi, flames = flames, pos = from, vel = Vector2.up, target = target, src = g, damage = dmg * (1f + 0.15f * (tier - 1)),
                life = flight + 0.5f, fireball = true, power = tier, lob = true, from = from, to = lead, flight = flight, arc = 1.4f + dist0 * 0.22f, shadow = shadow });
            sr.transform.position = from;
            sr.transform.localScale = Vector3.one * size * 0.3f;   // waechst aus dem Maul heraus
            // Cartoon FX: lodernder Feuerball mit Feuerschweif statt der eigenen Kugel
            var shot = shots[shots.Count - 1];
            shot.fxBall = WorldFx.Attach("fireball", sr.transform, 0.3f + 0.06f * (tier - 1), 30004);
            if (shot.fxBall != null)
            {
                shot.fxTrail = WorldFx.Attach("fireTrail", sr.transform, 0.22f + 0.04f * (tier - 1), 30002);
                // Rauch des Schweifs ueber Feuerball und Kern legen
                if (shot.fxTrail != null)
                    foreach (var pr in shot.fxTrail.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        if (pr.gameObject.name.Contains("Smoke")) pr.sortingOrder = 30040;
                core.enabled = false; hi.enabled = false;
                foreach (var f in flames) f.enabled = false;
                sr.color = new Color(2.2f, 0.75f, 0.18f, 0.3f);
            }
            // Kern: rot gluehende Kugel (HDR -> Bloom) mit hellerem Inneren, wabert im Flug
            shot.kern = new GameObject("Kern").AddComponent<SpriteRenderer>();
            shot.kern.transform.SetParent(sr.transform, false);
            shot.kern.sprite = KernSprite;
            shot.kern.color = new Color(1.9f, 0.16f, 0.08f, 1f);
            shot.kern.transform.localScale = Vector3.one * 0.6f;
            shot.kern.sortingOrder = 30030;   // vor den Flammen-Partikeln
            shot.kernHot = new GameObject("KernHot").AddComponent<SpriteRenderer>();
            shot.kernHot.transform.SetParent(shot.kern.transform, false);
            shot.kernHot.sprite = KernSprite;
            shot.kernHot.color = new Color(2.4f, 0.55f, 0.25f, 0.85f);
            shot.kernHot.transform.localScale = Vector3.one * 0.45f;
            shot.kernHot.sortingOrder = 30031;
        }

        /// <summary>Bogenflug: steil aus dem Maul nach oben, dann im hohen Bogen herab; Schatten zeigt die Landestelle.</summary>
        void UpdateLob(Shot s, float dt)
        {
            s.t += dt;
            float k = Mathf.Clamp01(s.t / s.flight);
            // Hoehe: schnell hoch, laenger oben, dann faellt er beschleunigt (wie geworfen)
            float h = 4f * s.arc * k * (1f - k);
            var ground = Vector2.Lerp(s.from, s.to, k * k * (3f - 2f * k) * 0.35f + k * 0.65f);
            var prev = s.pos;
            s.pos = ground + new Vector2(0, h);
            s.vel = (s.pos - prev) / Mathf.Max(0.0001f, dt);
            s.sr.transform.position = s.pos;
            if (s.shadow != null)
            {
                s.shadow.transform.position = Vector2.Lerp(s.from, s.to, k);
                float ss = 0.2f + k * 0.35f + s.power * 0.04f;
                s.shadow.transform.localScale = new Vector3(ss, ss * 0.4f, 1);
                s.shadow.color = new Color(0.1f, 0.02f, 0f, 0.15f + k * 0.35f);
            }
            FireballTrail(s, dt);
            if (s.t < 0.12f) s.sr.transform.localScale = Vector3.one * (0.42f + 0.13f * (s.power - 1)) * Mathf.Lerp(0.3f, 1f, s.t / 0.12f);
            if (k < 1f) return;
            // Einschlag: naechster Zombie bekommt den vollen Treffer, die Explosion den Rest
            Monster hit = null; float bd = 0.6f * 0.6f;
            foreach (var m in monsters) { if (m.dead) continue; float d = (m.pos - s.to).sqrMagnitude; if (d < bd) { bd = d; hit = m; } }
            s.pos = s.to;
            if (hit != null) Hit(hit, s.damage, s.src, s.to);
            FireballBlast(s, hit);
            Particles.I?.Burst(s.to, "square", new Color(0.35f, 0.22f, 0.12f), 10, 3f, 0.6f, 0.1f, 9f, 2f);   // Erdbrocken
            s.done = true;
            WorldFx.Release(s.fxTrail);
            Destroy(s.sr.gameObject);
            if (s.shadow != null) Destroy(s.shadow.gameObject);
        }

        // ------------------------------------------------------------------ Giftspucke (Giftruebe)

        class GasCloud { public Vector2 pos; public float r, t, dur, dps, tick; public PlantGenome src; public List<SpriteRenderer> puffs = new List<SpriteRenderer>(); public GameObject fx; }
        readonly List<GasCloud> gasClouds = new List<GasCloud>();

        void LaunchGas(PlantGenome g, OptionDef proj, Vector2 from, Monster target, float angle, float dmg, int tier)
        {
            var sr = new GameObject("GasGlob").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = WorldBuilder.Mist;
            sr.color = new Color(0.8f, 1.6f, 0.45f, 1f);
            sr.sortingOrder = 30000;
            var dir = ((target.pos + new Vector2(0, 0.35f)) - from).normalized;
            dir = Quaternion.Euler(0, 0, angle) * dir;
            float speed = proj.speed;
            shots.Add(new Shot { sr = sr, pos = from, vel = dir * speed, target = target, src = g, speed = speed, damage = dmg, homing = proj.homing, life = 3f, gas = true, power = tier });
            sr.transform.position = from;
            Sfx.Play("pop", 0.55f, 0.7f);
        }

        /// <summary>Giftwolke: bleibt eine Weile stehen, vergiftet und bremst alle darin.</summary>
        void SpawnGas(Vector2 at, float power, float dmg, PlantGenome g)
        {
            var c = new GasCloud { pos = at, r = 0.55f + 0.15f * power, dur = 2f + 0.6f * power, dps = dmg * (0.45f + 0.1f * power), src = g };
            for (int i = 0; i < 4 + (int)power; i++)
            {
                var p = new GameObject("GasPuff").AddComponent<SpriteRenderer>();
                p.transform.SetParent(transform, false);
                p.sprite = WorldBuilder.Mist;
                p.color = i % 2 == 0 ? new Color(0.55f, 1.25f, 0.3f, 0f) : new Color(0.7f, 0.35f, 0.95f, 0f);
                p.sortingOrder = 29995;
                c.puffs.Add(p);
            }
            gasClouds.Add(c);
            WorldFx.Play("splash", at, 0.12f + 0.02f * power, 29996, new Color(0.55f, 1f, 0.3f, 1f));
            c.fx = WorldFx.Play("smoke", at, 0.16f + 0.03f * power, 29996, new Color(0.6f, 1f, 0.35f, 0.85f), null, c.dur + 3f);
            Particles.I?.Burst(at, "square", new Color(0.55f, 0.95f, 0.3f), 12, 2.8f, 0.5f, 0.06f, 4f, 1.2f);
            Particles.I?.Ring(at, new Color(0.6f, 1f, 0.4f, 0.8f), c.r * 1.6f, 0.3f);
            Sfx.Play("dig", 0.6f, 0.6f);
        }

        void UpdateGas(float dt)
        {
            float tt = Time.time;
            for (int i = gasClouds.Count - 1; i >= 0; i--)
            {
                var c = gasClouds[i];
                c.t += dt;
                float k = c.t / c.dur;
                float a = k < 0.15f ? k / 0.15f : k > 0.75f ? (1f - k) / 0.25f : 1f;
                for (int j = 0; j < c.puffs.Count; j++)
                {
                    var p = c.puffs[j];
                    float ph = tt * 0.9f + j * 1.7f;
                    p.transform.position = c.pos + new Vector2(Mathf.Cos(ph) * c.r * 0.45f, Mathf.Sin(ph * 1.3f) * c.r * 0.22f + 0.15f);
                    float s = c.r * (1.4f + 0.2f * Mathf.Sin(ph * 2f));
                    p.transform.localScale = new Vector3(s, s * 0.65f, 1);
                    var col = p.color; col.a = a * 0.7f; p.color = col;
                }
                if (Particles.I != null && rnd.NextDouble() < dt * 6) Particles.I.Spawn(c.pos + UnityEngine.Random.insideUnitCircle * c.r * 0.7f, "ring", new Color(0.7f, 1f, 0.45f, 0.8f), new Vector3(0, 0.2f, 0), 0.6f, 0.03f, 0.07f, -0.1f);
                c.tick -= dt;
                if (c.tick <= 0f)
                {
                    c.tick = 0.25f;
                    foreach (var m in monsters)
                    {
                        if (m.dead || (m.pos - c.pos).magnitude > c.r) continue;
                        Damage(m, c.dps * 0.25f, c.src, silent: true);
                        AddPoison(m, c.dps * 0.15f, 1);
                        m.slowT = Mathf.Max(m.slowT, 0.4f); m.slowAmt = Mathf.Max(m.slowAmt, 0.3f);
                        m.hitFlash = Mathf.Max(m.hitFlash, 0.25f);
                    }
                }
                if (k >= 1f) { foreach (var p in c.puffs) if (p != null) Destroy(p.gameObject); WorldFx.Release(c.fx, 2f); gasClouds.RemoveAt(i); }
            }
        }

        static Sprite kernSprite;
        /// <summary>Leuchtkugel: voller Kern, weicher Rand (fuer den Feuerball-Kern).</summary>
        static Sprite KernSprite
        {
            get
            {
                if (kernSprite != null) return kernSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = d < 0.55f ? 1f : Mathf.Clamp01(1f - (d - 0.55f) / 0.45f);
                        a = a * a * (3f - 2f * a);
                        float bright = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(d / 0.55f));   // innen heller
                        px[y * n + x] = new Color(bright, bright, bright, a);
                    }
                tex.SetPixels(px); tex.Apply();
                kernSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return kernSprite;
            }
        }

        static Sprite fireOrb;
        /// <summary>Kugel mit Licht von oben links, glutfluessiger Oberflaeche (Rauschen) und dunklerem Rand.</summary>
        static Sprite FireOrb
        {
            get
            {
                if (fireOrb != null) return fireOrb;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                var L = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float r2 = u * u + v * v;
                        if (r2 >= 1f) { px[y * n + x] = Color.clear; continue; }
                        var nrm = new Vector3(u, v, Mathf.Sqrt(1f - r2));
                        // Oberflaeche: Rauschen auf der Kugel -> Glutflecken und dunklere Krusten
                        float nz = Mathf.PerlinNoise(nrm.x * 2.6f + 10f, nrm.y * 2.6f + nrm.z * 1.3f) * 0.65f
                                 + Mathf.PerlinNoise(nrm.x * 6f + 3f, nrm.y * 6f + 7f) * 0.35f;
                        float lit = Mathf.Clamp01(Vector3.Dot(nrm, L) * 0.65f + 0.45f);
                        float rim = Mathf.Pow(1f - nrm.z, 2f);
                        var hot = new Color(1f, 0.92f, 0.55f);
                        var mid = new Color(1f, 0.55f, 0.12f);
                        var crust = new Color(0.55f, 0.13f, 0.04f);
                        var c = nz > 0.55f ? Color.Lerp(mid, hot, (nz - 0.55f) / 0.45f) : Color.Lerp(crust, mid, nz / 0.55f);
                        c *= 0.35f + lit * 0.9f;
                        c = Color.Lerp(c, new Color(1f, 0.45f, 0.1f), rim * 0.5f);   // gluehender Rand (Rueckstreuung)
                        c.a = Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 40f);
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                fireOrb = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return fireOrb;
            }
        }

        void FireballTrail(Shot s, float dt)
        {
            // pulsierender Hof, rollende Kugel, lodernde Flammenzungen nach hinten, dazu Schweif und Rauch
            float tt = Time.time;
            float fl = 1f + Mathf.Sin(tt * 30f) * 0.06f;
            s.sr.transform.localScale = Vector3.one * (0.42f + 0.13f * (s.power - 1)) * fl;
            var back = -s.vel.normalized;
            if (s.core != null) s.core.transform.localRotation = Quaternion.Euler(0, 0, -tt * 400f);
            if (s.kern != null)
            {
                // wabern: ungleichmaessig verformen, drehen, pulsieren
                float ph = s.power * 1.7f;
                float kx = 1f + 0.16f * Mathf.Sin(tt * 11f + ph) + 0.07f * Mathf.Sin(tt * 23f + ph * 2f);
                float ky = 1f + 0.16f * Mathf.Sin(tt * 9f + ph + 1.3f) + 0.07f * Mathf.Sin(tt * 19f + 0.7f);
                s.kern.transform.localScale = new Vector3(0.6f * kx, 0.6f * ky, 1f);
                s.kern.transform.localRotation = Quaternion.Euler(0, 0, tt * 90f + Mathf.Sin(tt * 5f) * 25f);
                s.kern.transform.localPosition = new Vector3(Mathf.Sin(tt * 13f) * 0.025f, Mathf.Cos(tt * 17f) * 0.025f, 0);
                float glow = 0.85f + 0.15f * Mathf.Sin(tt * 15f + ph);
                s.kern.color = new Color(1.9f * glow, 0.16f * glow, 0.08f, 1f);
                s.kernHot.transform.localScale = Vector3.one * (0.45f + 0.08f * Mathf.Sin(tt * 21f));
            }
            if (s.flames != null)
            {
                float backAng = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
                for (int i = 0; i < s.flames.Count; i++)
                {
                    var f = s.flames[i];
                    if (f == null) continue;
                    // hintere Flammen lang (Fahrtwind), vordere kurz und zuengelnd
                    float spread = (i / (float)(s.flames.Count - 1) - 0.5f) * 2f;          // -1..1
                    bool front = i % 3 == 0;
                    float ang = front ? backAng + 180f + spread * 140f : backAng + spread * 75f;
                    float lick = Mathf.Sin(tt * (17f + i * 3.1f) + i * 1.7f);
                    float len = front ? 0.45f + 0.12f * lick : 0.75f + 0.25f * lick + (1f - Mathf.Abs(spread)) * 0.45f;
                    var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                    f.transform.localPosition = dir * 0.26f;
                    f.transform.localRotation = Quaternion.Euler(0, 0, ang - 90f + lick * 8f);
                    f.transform.localScale = new Vector3(0.42f * (1.1f - Mathf.Abs(lick) * 0.2f), len, 1);
                    f.flipX = lick > 0.3f;
                }
            }
            int n = Mathf.FloorToInt(dt * (s.fxBall != null ? 10f : 40f + s.power * 12f) + (float)rnd.NextDouble());
            for (int i = 0; i < n; i++)
            {
                var c = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.06f), (float)rnd.NextDouble());
                Particles.I?.Spawn(s.pos + UnityEngine.Random.insideUnitCircle * 0.08f * s.power, "spark", c,
                    (Vector3)(back * (0.6f + (float)rnd.NextDouble()) + UnityEngine.Random.insideUnitCircle * 0.3f), 0.35f, 0.16f + 0.04f * s.power, 0f, -0.4f);
            }
            if (rnd.NextDouble() < dt * 14) Particles.I?.Spawn(s.pos, "square", new Color(0.25f, 0.22f, 0.22f, 0.45f), (Vector3)(back * 0.3f + Vector2.up * 0.3f), 0.9f, 0.08f, 0.22f, -0.15f);
        }

        /// <summary>Einschlag: Explosion mit Flaechenschaden, Feuerring, Licht, Donner – maechtig.</summary>
        void FireballBlast(Shot s, Monster hit)
        {
            float r = 0.6f + 0.18f * s.power;
            var at = s.pos;
            WorldFx.Play("explosion", at, 0.16f + 0.035f * s.power);
            Particles.I?.Ring(at, new Color(1f, 0.6f, 0.2f, 0.95f), r * 1.9f, 0.4f);
            Particles.I?.Ring(at, new Color(1f, 0.95f, 0.7f, 0.9f), r * 1.1f, 0.25f);
            Particles.I?.Burst(at, "spark", new Color(1f, 0.7f, 0.25f), 14 + (int)(s.power * 4), 4.5f, 0.4f, 0.3f, 0.5f, 1.2f);
            Particles.I?.Burst(at, "square", new Color(1f, 0.45f, 0.12f), 10, 3.5f, 0.5f, 0.12f, 4f, 1.5f);
            Particles.I?.Burst(at, "square", new Color(0.25f, 0.22f, 0.22f, 0.7f), 8, 1.4f, 1.1f, 0.25f, -0.6f, 1f);
            if (flashLight != null) { flashLight.transform.position = at; flashLight.intensity = 1.6f + s.power * 0.5f; flashT = 0.12f; }
            Sfx.Play("boom", 1.25f + (float)rnd.NextDouble() * 0.15f, 0.4f + s.power * 0.08f);
            WorldCamera.I?.Shake(0.04f + s.power * 0.015f);
            foreach (var o in monsters)
            {
                if (o == hit || o.dead || (o.pos - at).magnitude > r) continue;
                Damage(o, s.damage * 0.55f, s.src);
                ApplyElement(o, s.src, s.damage * 0.55f);
                o.knock += (o.pos - at).normalized * 0.5f;
            }
            if (hit != null) hit.knock += (hit.pos - at).normalized * (0.4f + s.power * 0.1f) + Vector2.up * 0.2f;
        }

        void ApplyElement(Monster m, PlantGenome g, float dmg)
        {
            switch (g.element.id)
            {
                case "fire": m.burnT = 2f; m.burnDps = Mathf.Max(m.burnDps, dmg * 0.22f * g.element.level); break;
                case "ice": m.slowT = 1.5f; m.slowAmt = Mathf.Min(0.6f, 0.15f * g.element.level); break;
                case "poison": AddPoison(m, dmg * 0.1f * g.element.level, 1); break;
            }
        }

        void AddPoison(Monster m, float dps, int stacks)
        {
            m.poisonDps = Mathf.Max(m.poisonDps, dps);
            m.poisonStacks = Mathf.Min(5, m.poisonStacks + stacks);
            m.poisonT = 3f;
        }

        void Chain(Monster from, PlantGenome g, float dmg, int jumps)
        {
            var ex = new HashSet<Monster> { from };
            var cur = from;
            for (int i = 0; i < jumps; i++)
            {
                var next = Nearest(cur.pos, 2.4f, ex);
                if (next == null) break;
                ex.Add(next);
                Beam(cur.pos + new Vector2(0, 0.35f), next.pos + new Vector2(0, 0.35f), g.ElementColor);
                Damage(next, dmg, g);
                cur = next;
            }
        }

        void Damage(Monster m, float dmg, PlantGenome g, bool silent = false, bool crit = false)
        {
            if (m.dead || dmg <= 0) return;
            m.hp -= dmg;
            if (!silent)
            {
                m.hitFlash = 1f;
                var col = g != null ? g.ElementColor : Color.white;
                Particles.I?.Burst(m.pos + new Vector2(0, 0.35f), "spark", col, crit ? 8 : 3, 2.5f, 0.3f, 0.8f, 2f);
                wb.RaiseFloater(m.pos + new Vector2(0, 0.9f), (crit ? "!" : "") + Mathf.Max(1, Mathf.RoundToInt(dmg)), crit ? new Color(1f, 0.9f, 0.3f) : Color.white);
            }
            if (m.hp <= 0) Kill(m);
        }

        void Kill(Monster m)
        {
            result.kills++;
            Poof(m, true);
        }

        void Poof(Monster m, bool fx)
        {
            m.dead = true;
            if (fx)
            {
                Particles.I?.Burst(m.pos + new Vector2(0, 0.3f), "leaf", new Color(0.6f, 0.45f, 0.65f), 12, 3.5f, 0.6f, 0.6f, 3f, 1.5f);
                Particles.I?.Ring(m.pos + new Vector2(0, 0.3f), Color.white, 1f);
                WorldFx.Play("poof", m.pos + new Vector2(0, 0.35f), 0.18f, WorldBuilder.Order(m.pos.y) + 5);
                Sfx.Play("break", 1.4f, 0.6f);
            }
            if (m.root != null) Destroy(m.root.gameObject);
        }

        // ------------------------------------------------------------------ Kettenblitz (Blitzmais)

        /// <summary>Zickzack-Blitz aus dem Maul (Donnerkoenig: vom Himmel) auf den Zombie, springt dann weiter.</summary>
        void LightningStrike(PlantGenome g, Vector2 muzzle, Monster target, float dmg, int tier)
        {
            var hitP = target.pos + new Vector2(0, 0.4f);
            var from = tier >= 4 ? hitP + new Vector2(UnityEngine.Random.Range(-0.6f, 0.6f), 6f) : muzzle;
            float w = 0.06f + tier * 0.025f;
            ZigBolt(from, hitP, w, tier >= 4 ? 9 : 6);
            Hit(target, dmg, g, hitP);
            // springt auf weitere Zombies ueber, jeder Sprung etwas schwaecher
            var ex = new HashSet<Monster> { target };
            var cur = target;
            int jumps = 1 + tier + (g.pattern.id == "chain" ? g.pattern.level / 2 : 0);
            float jd = dmg * 0.75f;
            for (int j = 0; j < jumps; j++)
            {
                var next = Nearest(cur.pos, 2.2f + tier * 0.3f, ex);
                if (next == null) break;
                ex.Add(next);
                ZigBolt(cur.pos + new Vector2(0, 0.4f), next.pos + new Vector2(0, 0.4f), w * 0.8f, 4);
                Damage(next, jd, g);
                next.slowT = Mathf.Max(next.slowT, 0.25f); next.slowAmt = Mathf.Max(next.slowAmt, 0.6f);   // kurz gelaehmt
                cur = next; jd *= 0.8f;
            }
            WorldFx.Play("lightningImpact", hitP, 0.2f + 0.03f * tier);
            if (tier >= 4) WorldFx.Play("lightningStrike", target.pos, 0.3f);
            Particles.I?.Burst(hitP, "spark", new Color(1f, 0.95f, 0.55f), 10 + tier * 3, 4f, 0.25f, 0.18f, 0f, 1f);
            Particles.I?.Ring(hitP, new Color(1f, 1f, 0.8f, 0.9f), 0.6f + tier * 0.2f, 0.2f);
            if (flashLight != null) { flashLight.transform.position = hitP; flashLight.color = new Color(1f, 0.95f, 0.6f); flashLight.intensity = 1.5f + tier * 0.6f; flashT = 0.09f; }
            Sfx.Play("zap", UnityEngine.Random.Range(0.9f, 1.15f), 0.55f + tier * 0.08f);
            if (tier >= 4) { Sfx.Play("boom", 0.7f, 0.6f); WorldPostFX.Punch(0.4f); }
            WorldCamera.I?.Shake(0.025f + tier * 0.015f);
        }

        /// <summary>Gezackter Blitz aus mehreren hellen Segmenten mit Leuchthof.</summary>
        void ZigBolt(Vector2 a, Vector2 b, float width, int segs)
        {
            var d = b - a;
            var n = new Vector2(-d.y, d.x).normalized;
            var prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float k = i / (float)segs;
                var p = i == segs ? b : a + d * k + n * UnityEngine.Random.Range(-0.5f, 0.5f) * Mathf.Min(1.4f, d.magnitude * 0.35f);
                BeamW(prev, p, new Color(1f, 1f, 0.85f), width, 0.12f, 30003);
                BeamW(prev, p, new Color(1f, 0.9f, 0.35f, 0.4f), width * 2.2f, 0.16f, 30002);
                prev = p;
            }
        }

        void BeamW(Vector2 a, Vector2 b, Color col, float width, float life, int order)
        {
            var sr = new GameObject("Bolt").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = White;
            var d = b - a;
            sr.transform.position = (a + b) / 2f;
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude + width * 0.5f, width, 1);
            sr.color = col;
            sr.sortingOrder = order;
            beams.Add((sr, life));
        }

        void Beam(Vector2 a, Vector2 b, Color col)
        {
            var sr = new GameObject("Beam").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = White;
            var d = b - a;
            sr.transform.position = (a + b) / 2f;
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude, 0.07f, 1);
            sr.color = new Color(Mathf.Min(1, col.r * 1.4f), Mathf.Min(1, col.g * 1.4f), Mathf.Min(1, col.b * 1.4f), 1f);
            sr.sortingOrder = 30001;
            beams.Add((sr, 0.14f));
        }

        void UpdateBeams(float dt)
        {
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var (sr, t) = beams[i];
                t -= dt;
                if (t <= 0) { Destroy(sr.gameObject); beams.RemoveAt(i); continue; }
                var c = sr.color; c.a = t / 0.14f; sr.color = c;
                beams[i] = (sr, t);
            }
        }

    }
}
