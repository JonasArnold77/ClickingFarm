using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>Darstellung des Nachtkampfs: Monster, Geschosse, Strahlen, Reichweiten-Ringe.</summary>
    public class BattleView : MonoBehaviour
    {
        class MonsterView { public SpriteRenderer body, shadow, hpBack, hpFill; public Vector2 lastPos; public bool used; }

        Game g;
        readonly Dictionary<int, MonsterView> monsters = new Dictionary<int, MonsterView>();
        readonly Stack<MonsterView> monsterPool = new Stack<MonsterView>();
        readonly List<SpriteRenderer> shotViews = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer sr, float t)> beams = new List<(SpriteRenderer, float)>();
        int highlightTile = -1; float highlightT;

        void Awake()
        {
            g = Game.I;
            g.FeedbackRaised += OnFeedback;
            g.ShotFired += OnShot;
            g.PulseFired += OnPulse;
            g.DefenderTapped += (x, y) => { highlightTile = y * g.W + x; highlightT = 3f; };
            g.BattleStarted += () => { Sfx.Play("sleep", 0.7f); CameraController.I?.FocusFarm(); };
        }

        SpriteRenderer SR(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        MonsterView TakeMonster()
        {
            if (monsterPool.Count > 0) { var v = monsterPool.Pop(); SetActive(v, true); return v; }
            var mv = new MonsterView
            {
                shadow = SR("shadow", 0), body = SR("monster", 0), hpBack = SR("hp", 0), hpFill = SR("hpf", 0)
            };
            mv.shadow.sprite = Art.Get("soft"); mv.shadow.color = new Color(0, 0, 0, 0.35f);
            mv.hpBack.sprite = Art.Get("white"); mv.hpBack.color = new Color(0.15f, 0.08f, 0.05f, 0.85f);
            mv.hpFill.sprite = Art.Get("white"); mv.hpFill.color = new Color(0.55f, 0.95f, 0.35f);
            return mv;
        }

        static void SetActive(MonsterView v, bool a)
        {
            v.body.gameObject.SetActive(a); v.shadow.gameObject.SetActive(a); v.hpBack.gameObject.SetActive(a); v.hpFill.gameObject.SetActive(a);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            SyncMonsters();
            SyncShots();
            UpdateBeams(dt);
            UpdateRings(dt);
        }

        void SyncMonsters()
        {
            foreach (var kv in monsters) kv.Value.used = false;
            foreach (var m in g.Monsters)
            {
                if (m.dead) continue;
                if (!monsters.TryGetValue(m.id, out var v)) { v = TakeMonster(); monsters[m.id] = v; v.lastPos = m.pos; }
                v.used = true;
                bool flying = m.def.id == "crow";
                int frame = (int)(m.bob * 1.2f) % 2;
                v.body.sprite = Art.Get("mon:" + m.def.id + ":" + frame);
                float hop = flying ? 0.55f + Mathf.Sin(m.bob * 2f) * 0.1f : Mathf.Abs(Mathf.Sin(m.bob)) * 0.18f;
                float squash = flying ? 0 : (1 - Mathf.Abs(Mathf.Sin(m.bob))) * 0.18f;
                int order = FarmView.Order(m.pos.y) + 2;
                float hitScale = 1f + m.hitFlash * 0.25f;
                v.body.sortingOrder = order;
                v.body.transform.position = new Vector3(m.pos.x, m.pos.y - 0.35f + hop, 0);
                float dir = m.pos.x - v.lastPos.x;
                float sx = Mathf.Abs(dir) > 0.001f ? Mathf.Sign(dir) : Mathf.Sign(v.body.transform.localScale.x == 0 ? 1 : v.body.transform.localScale.x);
                v.body.transform.localScale = new Vector3(sx * (1 + squash), 1 - squash, 1) * hitScale;
                v.lastPos = m.pos;
                Color c = Color.white;
                if (m.slowT > 0) c = new Color(0.6f, 0.82f, 1f);
                if (m.burnT > 0) c = new Color(1f, 0.7f, 0.5f);
                if (m.hitFlash > 0) c = Color.Lerp(c, new Color(1f, 0.3f, 0.3f), m.hitFlash);
                v.body.color = c;
                v.shadow.transform.position = new Vector3(m.pos.x, m.pos.y - 0.38f, 0);
                v.shadow.transform.localScale = new Vector3(0.9f - hop * 0.6f, 0.35f, 1);
                v.shadow.sortingOrder = order - 1;
                float hpf = Mathf.Clamp01(m.hp / m.maxHp);
                bool showHp = hpf < 0.999f;
                v.hpBack.enabled = v.hpFill.enabled = showHp;
                var top = new Vector3(m.pos.x, m.pos.y + 0.55f + hop, 0);
                v.hpBack.transform.position = top; v.hpBack.transform.localScale = new Vector3(0.75f, 0.12f, 1);
                v.hpFill.transform.position = top + new Vector3(-0.35f * (1 - hpf), 0, 0); v.hpFill.transform.localScale = new Vector3(0.7f * hpf, 0.08f, 1);
                v.hpBack.sortingOrder = 2900; v.hpFill.sortingOrder = 2901;
                if (m.burnT > 0 && Random.value < Time.deltaTime * 10f)
                    Particles.I.Spawn(v.body.transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, 0), "square", new Color(1f, 0.6f, 0.2f), new Vector3(0, 1.2f, 0), 0.4f, 0.8f, 0f, 0f);
            }
            var remove = new List<int>();
            foreach (var kv in monsters) if (!kv.Value.used) remove.Add(kv.Key);
            foreach (var id in remove) { SetActive(monsters[id], false); monsterPool.Push(monsters[id]); monsters.Remove(id); }
        }

        void SyncShots()
        {
            int n = 0;
            foreach (var s in g.Shots)
            {
                if (s.done) continue;
                if (n >= shotViews.Count) shotViews.Add(SR("shot", 2800));
                var sr = shotViews[n++];
                sr.gameObject.SetActive(true);
                sr.sprite = Art.Get("proj:" + (string.IsNullOrEmpty(s.src.defense.projectile) ? "seed" : s.src.defense.projectile));
                sr.transform.position = s.pos;
                sr.transform.localScale = Vector3.one * (s.power ? 1.5f : 1f);
                sr.transform.Rotate(0, 0, Time.deltaTime * 400f);
                if (Random.value < Time.deltaTime * 25f)
                {
                    var col = s.src.defense.element == "fire" ? new Color(1f, 0.6f, 0.2f) : s.src.defense.element == "water" ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.85f, 0.4f);
                    Particles.I.Spawn(s.pos, "spark", col, Vector3.zero, 0.3f, 0.7f, 0f, 0f);
                }
            }
            for (int i = n; i < shotViews.Count; i++) shotViews[i].gameObject.SetActive(false);
        }

        void OnShot(int tile, Vector2 target, bool power)
        {
            int x = tile % g.W, y = tile / g.W;
            FarmView.I.Kick(x, y, punch: power ? 0.6f : 0.3f, squash: power ? 1.2f : 0.6f);
            var c = Balance.Crop(g.Tile(x, y).crop);
            if (c == null) return;
            var from = (Vector2)FarmView.TileCenter(x, y) + new Vector2(0, 0.35f);
            if (c.defense.projectile == "beam")
            {
                var sr = SR("beam", 2850);
                sr.sprite = Art.Get("white");
                var d = target - from;
                sr.transform.position = (from + target) / 2f;
                sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(d.magnitude * 4f, power ? 0.9f : 0.5f, 1);
                sr.color = new Color(1f, 0.95f, 0.5f, 0.95f);
                beams.Add((sr, 0.15f));
                Particles.I.Burst(target, "star", new Color(1f, 0.95f, 0.5f), power ? 6 : 3, 2.5f, 0.35f, 0.8f, 0f, 0f);
            }
            Sfx.Play(c.defense.element == "water" ? "water" : c.defense.projectile == "beam" ? "crit" : "pop", power ? 1.3f : 0.9f + Random.Range(0f, 0.2f), power ? 0.8f : 0.45f);
        }

        void OnPulse(int tile)
        {
            int x = tile % g.W, y = tile / g.W;
            var c = Balance.Crop(g.Tile(x, y).crop);
            var p = FarmView.TileCenter(x, y);
            float r = c != null ? c.defense.range : 1.5f;
            FarmView.I.Kick(x, y, squash: 1.3f);
            Particles.I.Spawn(p, "ring", new Color(0.85f, 0.5f, 1f, 0.9f), Vector3.zero, 0.35f, 0.3f, r * 2.1f, 0f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2;
                Particles.I.Spawn(p, "leaf", new Color(0.45f, 0.8f, 0.3f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r * 2.6f, 0.4f, 0.9f, 0.3f, 0f, 400f);
            }
            Sfx.Play("chop", 0.8f, 0.6f);
        }

        void UpdateBeams(float dt)
        {
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                b.t -= dt;
                if (b.t <= 0) { Destroy(b.sr.gameObject); beams.RemoveAt(i); continue; }
                var c = b.sr.color; c.a = b.t / 0.15f; b.sr.color = c;
                beams[i] = b;
            }
        }

        void UpdateRings(float dt)
        {
            highlightT -= dt;
            var sel = Balance.Crop(g.S.selectedSeed);
            bool showAll = g.BattleActive || (sel != null && sel.IsDefender) || (g.Hour >= 18f && !g.Sleeping);
            int n = 0;
            for (int i = 0; i < g.S.tiles.Count; i++)
            {
                var t = g.S.tiles[i];
                if (!t.HasCrop) continue;
                var c = Balance.Crop(t.crop);
                if (c == null || !c.IsDefender) continue;
                bool ripe = g.IsRipe(t);
                if (!showAll && !(highlightT > 0 && highlightTile == i)) continue;
                if (n >= rings.Count) { var r = SR("range", -990); r.sprite = Art.Get("rangering"); rings.Add(r); }
                var sr = rings[n++];
                sr.gameObject.SetActive(true);
                sr.transform.position = FarmView.TileCenter(i % g.W, i / g.W);
                float pulse = 1f + Mathf.Sin(Time.time * 3f + i) * 0.015f;
                sr.transform.localScale = Vector3.one * c.defense.range * pulse;
                var col = ElementColor(c.defense.element);
                col.a = ripe ? (highlightT > 0 && highlightTile == i ? 0.9f : 0.55f) : 0.25f;
                sr.color = col;
            }
            for (int i = n; i < rings.Count; i++) rings[i].gameObject.SetActive(false);
        }

        public static Color ElementColor(string e) => e switch
        {
            "fire" => new Color(1f, 0.5f, 0.3f),
            "water" => new Color(0.45f, 0.8f, 1f),
            "plant" => new Color(0.6f, 0.95f, 0.4f),
            _ => new Color(1f, 0.92f, 0.55f)
        };

        void OnFeedback(Feedback f)
        {
            if (!f.hasWorld) return;
            var p = (Vector3)f.world;
            switch (f.type)
            {
                case FxType.MonsterSpawn:
                    Particles.I.Burst(p, "square", new Color(0.35f, 0.25f, 0.2f), 8, 2f, 0.5f, 1f, 3f);
                    break;
                case FxType.MonsterHit:
                    {
                        var col = f.id == "tap" ? Color.white : ElementColor(f.id);
                        Particles.I.Burst(p + new Vector3(0, 0.2f, 0), "spark", col, f.strength >= 2 ? 7 : 3, 3f, 0.35f, 0.9f, 2f);
                        if (f.id == "tap") { Sfx.Play("chop", 1.3f, 0.6f); Haptics.Tap(8); }
                        break;
                    }
                case FxType.MonsterDie:
                    {
                        var def = Balance.Monster(f.id);
                        var col = def != null ? Balance.Hex(def.color) : Color.white;
                        Particles.I.Burst(p, "square", col, 14, 4f, 0.6f, 1.1f, 5f);
                        Particles.I.Burst(p, "star", new Color(1f, 0.95f, 0.6f), 6, 3f, 0.5f, 1f, 0f);
                        Particles.I.Ring(p, Color.white, 1.2f);
                        Sfx.Play("break", 1.4f, 0.7f); Sfx.Play("coin", 1.1f, 0.5f);
                        CameraController.I?.Shake(0.05f);
                        Haptics.Tap(20);
                        break;
                    }
                case FxType.Splash:
                    {
                        var col = f.id == "dew_bell" ? new Color(0.55f, 0.85f, 1f, 0.9f) : new Color(1f, 0.7f, 0.3f, 0.9f);
                        Particles.I.Spawn(p, "ring", col, Vector3.zero, 0.35f, 0.3f, f.strength * 2.1f, 0f);
                        Particles.I.Burst(p, f.id == "dew_bell" ? "drop" : "square", col, 10, 3.5f, 0.45f, 1f, 4f);
                        if (f.id != "dew_bell") CameraController.I?.Shake(0.08f);
                        Sfx.Play(f.id == "dew_bell" ? "water" : "break", 1.1f, 0.6f);
                        break;
                    }
                case FxType.Steal:
                    Particles.I.Burst(p, "square", new Color(1f, 0.85f, 0.3f), 5, 2f, 0.5f, 0.8f);
                    Sfx.Play("error", 1.2f, 0.4f);
                    break;
            }
        }
    }
}
