using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    public class Monster
    {
        public int id; public MonsterDef def;
        public Vector2 pos; public float hp, maxHp;
        public float slowT, slowAmt, burnT, burnDps;
        public Vector2 targetPos; public int targetTile = -1; public bool targetHouse;
        public float attackT, retargetT, hitFlash, bob;
        public bool dead, fled;
        public float Speed => def.speed * (slowT > 0 ? 1f - slowAmt : 1f);
    }

    public class Shot
    {
        public Vector2 pos; public int targetId; public Vector2 lastTarget;
        public float speed; public float damage; public CropDef src; public bool power;
        public bool done;
    }

    public class BattleResult
    {
        public int kills; public double coins; public double xp; public int eaten; public int wilted; public double stolen;
        public bool victory; public int night;
    }

    /// <summary>
    /// Nachtkampf: Monster kommen aus dem Wald und wollen an die Pflanzen und ans Haus.
    /// Reife Abwehrpflanzen schiessen automatisch; Antippen "pusht" sie (Power-Schuss + schneller).
    /// Die Platzierung zaehlt: Reichweite und Flaechenschaden haengen vom Feld ab.
    /// </summary>
    public partial class Game
    {
        public bool BattleActive { get; private set; }
        public int Wave { get; private set; }
        public int TotalWaves { get; private set; }
        public readonly List<Monster> Monsters = new List<Monster>();
        public readonly List<Shot> Shots = new List<Shot>();
        public BattleResult LastBattle { get; private set; }
        public BattleResult CurrentBattle { get; private set; }

        public event Action BattleStarted;
        public event Action<BattleResult> BattleEnded;
        public event Action<int, Vector2, bool> ShotFired;   // Feld, Zielpunkt, Power-Schuss?
        public event Action<int> PulseFired;                 // Feld (Dornen-Puls)

        readonly Dictionary<int, float> cooldowns = new Dictionary<int, float>();
        readonly Dictionary<int, float> pushStacks = new Dictionary<int, float>();
        readonly Dictionary<int, float> lastPush = new Dictionary<int, float>();
        int nextMonsterId = 1;
        int toSpawn;
        float spawnT, battleTime, waveBreakT;

        NightDef N => B.night;
        int NightNumber => S.nightsSurvived + 1;

        public bool IsCropUnlocked(CropDef c)
        {
            if (c == null) return false;
            if (c.starter) return c.id == S.starter || S.level >= B.otherStartersLevel;
            return c.unlockLevel <= S.level;
        }

        public bool IsDefenderReady(TileData t) { var c = Balance.Crop(t.crop); return c != null && c.IsDefender && IsRipe(t); }

        public float DefenderMaxHp(CropDef c) => c.defense.hp * (1 + N.defenderLevelScale * (S.level - 1));
        public float DefenderDamage(CropDef c) => c.defense.damage * (1 + N.defenderLevelScale * (S.level - 1)) * UpgradeValue("battle_spirit");
        public float PushStacks(int tileIdx) => pushStacks.TryGetValue(tileIdx, out var v) ? v : 0f;

        // ---------------------------------------------------------------- Start / Ende

        void CheckBattleStart()
        {
            if (BattleActive || Sleeping || S.starter == "" || S.battleDay == S.day) return;
            if (Hour < N.startHour) return;
            StartBattle();
        }

        public void StartBattle()
        {
            S.battleDay = S.day;
            BattleActive = true;
            Monsters.Clear(); Shots.Clear(); cooldowns.Clear(); pushStacks.Clear();
            int n = NightNumber;
            TotalWaves = Mathf.Clamp(Mathf.FloorToInt(N.wavesBase + N.wavesPerNight * (n - 1)), 1, N.maxWaves);
            Wave = 0;
            battleTime = 0;
            CurrentBattle = new BattleResult { night = n };
            CancelPlacing();
            NextWave();
            BattleStarted?.Invoke();
            MarkDirty();
        }

        void NextWave()
        {
            Wave++;
            int n = NightNumber;
            toSpawn = Mathf.Max(1, Mathf.RoundToInt((N.monstersBase + N.monstersPerNight * (n - 1)) * (0.8f + 0.2f * Wave)));
            spawnT = 0.5f;
            waveBreakT = 0;
        }

        void EndBattle(bool victory)
        {
            if (!BattleActive) return;
            BattleActive = false;
            var r = CurrentBattle;
            r.victory = victory;
            foreach (var m in Monsters) m.fled = true;
            if (victory)
            {
                double bonus = Math.Round(N.victoryBonus * LevelScale * (1 + 0.25 * r.night));
                r.coins += bonus;
                AddCoins(bonus);
                S.nightsSurvived++;
            }
            Monsters.Clear(); Shots.Clear();
            LastBattle = r;
            BattleEnded?.Invoke(r);
            MarkDirty();
        }

        // ---------------------------------------------------------------- Ablauf pro Frame

        void TickBattle(float dt)
        {
            if (!BattleActive) return;
            battleTime += dt;

            // Spawnen
            if (toSpawn > 0)
            {
                spawnT -= dt;
                if (spawnT <= 0) { SpawnMonster(); toSpawn--; spawnT = N.spawnInterval * UnityEngine.Random.Range(0.7f, 1.3f); }
            }
            else if (Monsters.Count == 0)
            {
                if (Wave >= TotalWaves) { EndBattle(true); return; }
                waveBreakT += dt;
                if (waveBreakT >= N.waveBreak) NextWave();
            }
            if (battleTime > N.maxSeconds) { EndBattle(false); return; } // Morgengrauen: Monster fliehen

            TickMonsters(dt);
            TickDefenders(dt);
            TickShots(dt);
            Monsters.RemoveAll(m => m.dead);
            Shots.RemoveAll(s => s.done);

            // Push-Stapel klingen ab
            if (pushStacks.Count > 0)
            {
                var keys = new List<int>(pushStacks.Keys);
                foreach (var k in keys) { float v = pushStacks[k] - dt * 1.5f; if (v <= 0) pushStacks.Remove(k); else pushStacks[k] = v; }
            }
        }

        void SpawnMonster()
        {
            int n = NightNumber;
            // Monsterart nach Gewicht, nur die schon "erwachten"
            float total = 0;
            foreach (var d in B.monsters) if (d.firstNight <= n) total += d.weight;
            float roll = UnityEngine.Random.value * total;
            MonsterDef def = B.monsters[0];
            foreach (var d in B.monsters) { if (d.firstNight > n) continue; roll -= d.weight; if (roll <= 0) { def = d; break; } }

            GetFarmBounds(out var min, out var max);
            Vector2 p;
            int side = UnityEngine.Random.Range(0, 3); // links, rechts, unten (oben ist das Haus)
            if (side == 0) p = new Vector2(min.x - 1.5f, UnityEngine.Random.Range(min.y, max.y));
            else if (side == 1) p = new Vector2(max.x + 1.5f, UnityEngine.Random.Range(min.y, max.y));
            else p = new Vector2(UnityEngine.Random.Range(min.x, max.x), min.y - 1.5f);

            float hp = def.hp * (1 + N.hpGrowthPerNight * (n - 1)) * (float)Math.Pow(1.08, S.level - 1);
            var m = new Monster { id = nextMonsterId++, def = def, pos = p, hp = hp, maxHp = hp, bob = UnityEngine.Random.value * 10 };
            Retarget(m);
            Monsters.Add(m);
            FeedbackRaised?.Invoke(new Feedback { type = FxType.MonsterSpawn, x = -1, y = -1, world = p, hasWorld = true, id = def.id });
        }

        /// <summary>Begrenzung der freigeschalteten Flaeche (fuer Spawnpunkte).</summary>
        void GetFarmBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue); max = new Vector2(float.MinValue, float.MinValue);
            foreach (var id in S.unlockedAreas)
            {
                var a = Balance.Area(id); if (a == null) continue;
                foreach (var r in a.rects)
                {
                    min = Vector2.Min(min, new Vector2(r.x, r.y));
                    max = Vector2.Max(max, new Vector2(r.x + r.w, r.y + r.h));
                }
            }
            if (min.x > max.x) { min = Vector2.zero; max = new Vector2(W, H); }
        }

        public Vector2 HouseWorld => new Vector2(W / 2f - 2.5f, H + 0.6f);

        void Retarget(Monster m)
        {
            m.retargetT = 1f;
            float best = float.MaxValue; int bestIdx = -1;
            for (int i = 0; i < S.tiles.Count; i++)
            {
                var t = S.tiles[i];
                if (!t.HasCrop) continue;
                var c = Balance.Crop(t.crop);
                if (c == null) continue;
                var tp = new Vector2(i % W + 0.5f, i / W + 0.5f);
                float d = (tp - m.pos).sqrMagnitude;
                // Normale Pflanzen sind leckerer, Abwehrpflanzen werden aber auch angegriffen
                if (c.IsDefender) d *= 1.2f;
                if (d < best) { best = d; bestIdx = i; }
            }
            if (bestIdx >= 0) { m.targetTile = bestIdx; m.targetHouse = false; m.targetPos = new Vector2(bestIdx % W + 0.5f, bestIdx / W + 0.5f); }
            else { m.targetTile = -1; m.targetHouse = true; m.targetPos = HouseWorld; }
        }

        void TickMonsters(float dt)
        {
            foreach (var m in Monsters)
            {
                if (m.dead) continue;
                m.hitFlash = Mathf.Max(0, m.hitFlash - dt * 5f);
                m.bob += dt * (2f + m.Speed * 3f);
                if (m.slowT > 0) m.slowT -= dt;
                if (m.burnT > 0)
                {
                    m.burnT -= dt;
                    DamageMonster(m, m.burnDps * dt, null, false, silent: true);
                    if (m.dead) continue;
                }
                m.retargetT -= dt;
                if (m.retargetT <= 0 || (m.targetTile >= 0 && !S.tiles[m.targetTile].HasCrop)) Retarget(m);

                var to = m.targetPos - m.pos;
                float dist = to.magnitude;
                if (dist > 0.45f)
                {
                    m.pos += to / dist * Mathf.Min(dist, m.Speed * dt);
                    m.attackT = 0.4f;
                    continue;
                }
                m.attackT -= dt;
                if (m.attackT > 0) continue;
                m.attackT = 1.1f;
                MonsterAttack(m);
            }
        }

        void MonsterAttack(Monster m)
        {
            var r = CurrentBattle;
            if (m.targetHouse)
            {
                double steal = Math.Floor(S.coins * N.houseSteal);
                if (steal > 0) { S.coins -= steal; r.stolen += steal; }
                FeedbackRaised?.Invoke(new Feedback { type = FxType.Steal, x = -1, y = -1, world = m.pos, hasWorld = true, text = steal > 0 ? "-" + Fmt.Num(steal) : null });
                MarkDirty();
                return;
            }
            int i = m.targetTile;
            var t = S.tiles[i];
            var c = Balance.Crop(t.crop);
            if (c == null) return;
            int x = i % W, y = i / W;
            if (c.IsDefender && IsRipe(t))
            {
                t.dmg += m.def.damage * (1 + N.hpGrowthPerNight * 0.5f * (NightNumber - 1));
                if (t.dmg >= DefenderMaxHp(c))
                {
                    // Nicht zerstoert, nur welk: muss ein Stueck nachwachsen
                    t.dmg = 0; t.growth = c.growth * N.wiltRegrow;
                    r.wilted++;
                    Raise(FxType.Wilt, x, y, Loc.T("ui.wilted"), 1f, c.id);
                }
                else Raise(FxType.Eaten, x, y, null, 0.5f, c.id);
            }
            else
            {
                t.growth -= c.growth * N.cropBite * m.def.damage;
                if (t.growth <= 0)
                {
                    t.crop = ""; t.growth = 0; t.harvests = 0;
                    r.eaten++;
                    Raise(FxType.Eaten, x, y, Loc.T("ui.eaten"), 1f, c.id);
                    m.retargetT = 0;
                }
                else Raise(FxType.Eaten, x, y, null, 0.5f, c.id);
            }
            Changed(x, y);
        }

        void TickDefenders(float dt)
        {
            for (int i = 0; i < S.tiles.Count; i++)
            {
                var t = S.tiles[i];
                if (!t.HasCrop) continue;
                var c = Balance.Crop(t.crop);
                if (c == null || !c.IsDefender || !IsRipe(t)) continue;
                float cd = cooldowns.TryGetValue(i, out var v) ? v : UnityEngine.Random.Range(0f, c.defense.cooldown);
                cd -= dt * (1f + PushStacks(i) * N.pushSpeedPerStack);
                if (cd <= 0)
                {
                    if (FireDefender(i, c, false)) cd = c.defense.cooldown;
                    else cd = 0.1f;
                }
                cooldowns[i] = cd;
            }
        }

        Monster NearestMonster(Vector2 p, float range)
        {
            Monster best = null; float bd = range * range;
            foreach (var m in Monsters)
            {
                if (m.dead) continue;
                float d = (m.pos - p).sqrMagnitude;
                if (d <= bd) { bd = d; best = m; }
            }
            return best;
        }

        /// <summary>Eine Abwehrpflanze greift an. Gibt false zurueck, wenn kein Ziel in Reichweite ist.</summary>
        bool FireDefender(int i, CropDef c, bool power)
        {
            var p = new Vector2(i % W + 0.5f, i / W + 0.5f);
            var d = c.defense;
            float dmg = DefenderDamage(c) * (power ? N.pushDamageMult : 1f);
            if (d.pulse)
            {
                bool any = false;
                foreach (var m in Monsters)
                {
                    if (m.dead || (m.pos - p).magnitude > d.range) continue;
                    DamageMonster(m, dmg * Balance.ElementMult(d.element, m.def.element), c, power);
                    any = true;
                }
                if (any || power) PulseFired?.Invoke(i);
                return any;
            }
            var target = NearestMonster(p, d.range);
            if (target == null) return false;
            if (d.projectile == "beam")
            {
                // Strahl trifft sofort
                DamageMonster(target, dmg * Balance.ElementMult(d.element, target.def.element), c, power);
                ShotFired?.Invoke(i, target.pos, power);
                return true;
            }
            Shots.Add(new Shot { pos = p + new Vector2(0, 0.3f), targetId = target.id, lastTarget = target.pos, speed = d.projectile == "seed" ? 6f : 9f, damage = dmg, src = c, power = power });
            ShotFired?.Invoke(i, target.pos, power);
            return true;
        }

        void TickShots(float dt)
        {
            foreach (var s in Shots)
            {
                if (s.done) continue;
                var target = Monsters.Find(m => m.id == s.targetId && !m.dead);
                if (target != null) s.lastTarget = target.pos;
                var to = s.lastTarget - s.pos;
                float step = s.speed * dt;
                if (to.magnitude > step) { s.pos += to.normalized * step; continue; }
                s.pos = s.lastTarget;
                s.done = true;
                var d = s.src.defense;
                if (d.area > 0)
                {
                    foreach (var m in Monsters)
                        if (!m.dead && (m.pos - s.pos).magnitude <= d.area) Hit(m, s);
                    FeedbackRaised?.Invoke(new Feedback { type = FxType.Splash, x = -1, y = -1, world = s.pos, hasWorld = true, id = s.src.id, strength = d.area });
                }
                else if (target != null) Hit(target, s);
            }
        }

        void Hit(Monster m, Shot s)
        {
            var d = s.src.defense;
            DamageMonster(m, s.damage * Balance.ElementMult(d.element, m.def.element), s.src, s.power);
            if (d.slow > 0) { m.slowT = d.slowTime; m.slowAmt = d.slow; }
            if (d.burn > 0) { m.burnT = d.burnTime; m.burnDps = d.burn * (1 + N.defenderLevelScale * (S.level - 1)); }
        }

        void DamageMonster(Monster m, float dmg, CropDef src, bool power, bool silent = false)
        {
            if (m.dead) return;
            m.hp -= dmg;
            if (!silent)
            {
                m.hitFlash = 1f;
                float mult = src != null ? Balance.ElementMult(src.defense.element, m.def.element) : 1f;
                FeedbackRaised?.Invoke(new Feedback
                {
                    type = FxType.MonsterHit, x = -1, y = -1, world = m.pos, hasWorld = true, id = src != null ? src.defense.element : "tap",
                    text = Fmt.Num(Math.Max(1, Math.Round(dmg))), strength = mult > 1.01f ? 2f : power ? 1.5f : 1f
                });
            }
            if (m.hp <= 0) KillMonster(m);
        }

        void KillMonster(Monster m)
        {
            m.dead = true;
            var r = CurrentBattle;
            double coins = Math.Round(m.def.reward * (1 + N.rewardGrowthPerNight * (NightNumber - 1)) * LevelScale);
            double xp = Math.Max(1, Math.Round(m.def.xp * (1 + 0.1 * (S.level - 1))));
            r.kills++; r.coins += coins; r.xp += xp;
            S.monstersDefeated++;
            AddCoins(coins);
            AddXp(xp);
            FeedbackRaised?.Invoke(new Feedback { type = FxType.MonsterDie, x = -1, y = -1, world = m.pos, hasWorld = true, id = m.def.id, text = "+" + Fmt.Num(coins) });
        }

        // ---------------------------------------------------------------- Spieler-Eingriffe

        /// <summary>Antippen einer Abwehrpflanze: Power-Schuss und schnelleres Feuern.</summary>
        bool DoPush(int x, int y)
        {
            int i = y * W + x;
            var t = S.tiles[i];
            var c = Balance.Crop(t.crop);
            if (c == null || !c.IsDefender) return false;
            if (lastPush.TryGetValue(i, out var lt) && Time.time - lt < N.pushMinInterval) return false;
            lastPush[i] = Time.time;
            pushStacks[i] = Mathf.Min(N.pushMaxStacks, PushStacks(i) + 1f);
            bool fired = FireDefender(i, c, true);
            Raise(FxType.Push, x, y, null, fired ? 1.5f : 1f, c.id);
            if (!S.battleTutorialDone && fired) { S.battleTutorialDone = true; MarkDirty(); }
            return true;
        }

        /// <summary>Direktes Antippen eines Monsters (Weltposition). Gibt true zurueck, wenn eines getroffen wurde.</summary>
        public bool TapMonster(Vector2 world)
        {
            if (!BattleActive) return false;
            Monster best = null; float bd = 0.7f * 0.7f;
            foreach (var m in Monsters)
            {
                if (m.dead) continue;
                float d = (m.pos + new Vector2(0, 0.3f) - world).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            if (best == null) return false;
            float dmg = N.tapDamage * (1 + 0.15f * (S.level - 1)) * UpgradeValue("green_thumb");
            DamageMonster(best, dmg, null, false);
            return true;
        }
    }
}
