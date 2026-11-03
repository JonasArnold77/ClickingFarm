using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Steinmoos auf einen Mauerabschnitt (von Hand gezogen oder vom Maurer-Knolli gebracht) tut immer das Naechstbeste:
    ///  1. Holzmauern im Abschnitt werden zu Stein.
    ///  2. Ist der Abschnitt stark beschaedigt, wird er ganz geflickt.
    ///  3. Sonst wird Stein weiter verstaerkt: Stein -> Granit -> Kristall (jede Stufe haelt ein Vielfaches,
    ///     braucht aber mehr Steinmoos). Neue Mauern im Abschnitt werden zuerst nachgezogen.
    /// Der Maurer holt reifes Steinmoos selbst vom Feld, sobald irgendeine Mauer davon etwas haette – sonst flickt er
    /// wie bisher Stueck fuer Stueck. Werte in plants.json -> transforms (wallTier...).
    /// </summary>
    public partial class WorldBuilder
    {
        public int MaxWallTier => PlantBalance.I.Transforms.wallTierNames?.Length ?? 0;

        /// <summary>Haltbarkeits-Faktor der Steinstufe (0 = Stein, 1 = Granit, ...).</summary>
        float StoneMult(WallState st)
        {
            var T = PlantBalance.I.Transforms;
            if (!st.stone) return 1f;
            if (st.tier <= 0 || T.wallTierHpMults == null || T.wallTierHpMults.Length == 0) return T.stoneHpMult;
            return T.wallTierHpMults[Mathf.Min(st.tier, T.wallTierHpMults.Length) - 1];
        }

        public string WallTierName(WallState st)
        {
            if (!st.stone) return "Holz";
            var n = PlantBalance.I.Transforms.wallTierNames;
            return st.tier <= 0 || n == null || n.Length == 0 ? "Stein" : n[Mathf.Min(st.tier, n.Length) - 1];
        }

        /// <summary>Farbe der Stufe: Granit warm-rotgrau, Kristall kuehl-blau leuchtend.</summary>
        static Color WallTint(WallState st) => !st.stone || st.tier <= 0 ? Color.white : st.tier == 1 ? new Color(0.95f, 0.78f, 0.74f) : new Color(0.72f, 0.92f, 1.25f);

        int MossCost(int toTier)
        {
            var c = PlantBalance.I.Transforms.wallTierMoss;
            return c == null || c.Length == 0 ? 1 : c[Mathf.Clamp(toTier - 1, 0, c.Length - 1)];
        }

        /// <summary>Was Steinmoos an diesem Abschnitt taete (null = nichts mehr zu tun).</summary>
        string MossJob(List<Cell> seg)
        {
            bool wood = false, hurt = false; int minTier = int.MaxValue;
            float healBelow = PlantBalance.I.Transforms.mossHealBelow;
            foreach (var c in seg)
            {
                if (!walls.TryGetValue(c, out var v)) continue;
                if (!v.st.stone) wood = true;
                else minTier = Mathf.Min(minTier, v.st.tier);
                if (v.st.hp < MaxHp(v.st) * healBelow) hurt = true;
            }
            if (wood) return "stone";
            if (hurt) return "heal";
            if (minTier < MaxWallTier) return "tier";
            return null;
        }

        /// <summary>Steinmoos wirkt auf den Abschnitt bei c. Liefert den Text fuer die Einblendung (null = nichts passiert).</summary>
        string FeedMoss(Cell c)
        {
            var seg = WallSegment(c);
            switch (MossJob(seg))
            {
                case "stone":
                    {
                        int n = StoneWalls(c);
                        return n + (n == 1 ? " Mauer" : " Mauern") + " aus Stein!";
                    }
                case "heal":
                    {
                        int i = 0;
                        foreach (var x in seg)
                        {
                            var v = walls[x];
                            float max = MaxHp(v.st);
                            if (v.st.hp >= max) continue;
                            v.st.hp = max; v.pop = Mathf.Min(v.pop, 0.7f);
                            StartCoroutine(StoneFx(CellCenter(x) + new Vector2(0, WallH * 0.6f), Mathf.Min(i++, 20) * 0.04f));
                        }
                        return "Abschnitt geflickt (" + seg.Count + " Mauern)!";
                    }
                case "tier":
                    {
                        int minTier = int.MaxValue;
                        foreach (var x in seg) minTier = Mathf.Min(minTier, walls[x].st.tier);
                        int to = minTier + 1, cost = MossCost(to), fed = 0, up = 0, todo = 0;
                        // ein Steinmoos reicht fuer ein paar Mauern: erst die Nachzuegler, davon die fast fertigen zuerst
                        var feed = new List<Cell>();
                        foreach (var x in seg) if (walls[x].st.tier == minTier) feed.Add(x);
                        todo = feed.Count;
                        feed.Sort((a, b) => walls[b].st.moss.CompareTo(walls[a].st.moss));
                        if (feed.Count > PlantBalance.I.Transforms.mossWallsPerFeed) feed.RemoveRange(PlantBalance.I.Transforms.mossWallsPerFeed, feed.Count - PlantBalance.I.Transforms.mossWallsPerFeed);
                        foreach (var x in feed)
                        {
                            var v = walls[x];
                            v.st.moss++;
                            fed = v.st.moss;
                            if (v.st.moss < cost) { v.pop = Mathf.Min(v.pop, 0.85f); continue; }
                            v.st.moss = 0; v.st.tier = to;
                            v.st.hp = MaxHp(v.st);
                            v.pop = 0.5f;
                            StartCoroutine(TierFx(CellCenter(x) + new Vector2(0, WallH * 0.6f), Mathf.Min(up++, 20) * 0.05f, WallTint(v.st)));
                        }
                        var probe = new WallState { stone = true, tier = to };
                        string name = WallTierName(probe);
                        if (up > 0)
                        {
                            WorldCamera.I?.Shake(0.08f);
                            WorldPostFX.Punch(0.35f);
                            Sfx.Play("unlock", 0.9f + 0.15f * to, 0.7f);
                            return up + (up == 1 ? " Mauer" : " Mauern") + " aus " + name + "! ×" + StoneMult(probe).ToString("0.#") + " Haltbarkeit";
                        }
                        return name + ": " + fed + "/" + cost + " Steinmoos (noch " + todo + " Mauern)";
                    }
            }
            return null;
        }

        System.Collections.IEnumerator TierFx(Vector2 at, float delay, Color col)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Particles.I?.Burst(at, "square", col * new Color(0.75f, 0.75f, 0.78f, 1f), 12, 2.8f, 0.55f, 0.1f, 7f, 1.5f);
            Particles.I?.Burst(at + new Vector2(0, 0.2f), "star", col, 10, 2.4f, 0.7f, 0.2f, 0.5f, 1.2f);
            Shockwave(at, 1.1f, 0.4f, new Color(col.r, col.g, col.b, 0.8f));
            Sfx.Play("rock", Random.Range(0.7f, 0.85f), 0.8f);
        }

        // ------------------------------------------------------------------ Maurer-Knolli

        // Abschnitte, an denen Steinmoos etwas taete (alle 1,5 s neu ermittelt, eine Zelle je Abschnitt reicht als Ziel)
        readonly List<(Cell cell, float weight)> mossTargets = new List<(Cell, float)>();
        float mossTargetsAt;

        void RefreshMossTargets()
        {
            if (Time.time < mossTargetsAt) return;
            mossTargetsAt = Time.time + 1.5f;
            mossTargets.Clear();
            var seen = new HashSet<Cell>();
            foreach (var c in walls.Keys)
            {
                if (seen.Contains(c)) continue;
                var seg = WallSegment(c);
                foreach (var x in seg) seen.Add(x);
                var job = MossJob(seg);
                if (job == null) continue;
                // naechste Zelle zum Haus als Ziel (dort steht der Maurer meist)
                Cell best = seg[0]; float bd = float.MaxValue;
                foreach (var x in seg) { float d = (CellCenter(x) - HousePos).sqrMagnitude; if (d < bd) { bd = d; best = x; } }
                mossTargets.Add((best, job == "heal" ? 0f : job == "stone" ? 1f : 2f));   // Rang: flicken, Holz->Stein, verstaerken
            }
        }

        bool AnyMossTarget => mossTargets.Count > 0;

        /// <summary>Bestes Ziel fuer den Maurer: beschaedigte Abschnitte, dann Holz, dann Verstaerken – jeweils der naechste.</summary>
        bool PickMossTarget(Vector2 from, out Cell target)
        {
            target = default; float bd = float.MaxValue; bool found = false;
            foreach (var t in mossTargets)
            {
                if (!walls.ContainsKey(t.cell)) continue;
                float d = t.weight * 1e6f + (CellCenter(t.cell) - from).sqrMagnitude;   // erst der Rang, dann die Naehe
                if (d < bd) { bd = d; target = t.cell; found = true; }
            }
            return found;
        }

        /// <summary>Reifes Steinmoos, das noch kein anderer Maurer holt (nicht das, was der Spieler gerade traegt).</summary>
        PlantedState PickMoss(Knolli k)
        {
            PlantedState best = null; float bd = float.MaxValue;
            foreach (var p in save.planted)
            {
                if (p == carrying || TransformOf(p) != "stone") continue;
                bool taken = false;
                foreach (var o in knollis) if (o != k && o.mossGoal == p) { taken = true; break; }
                if (taken) continue;
                float d = (PlantBase(p) - k.pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        /// <summary>Steinmoos-Arbeit des Maurers. false = nichts zu tun (dann flickt er wie gewohnt).</summary>
        bool WorkMoss(Knolli k, float dt, out bool moving)
        {
            moving = false;
            if (k.moss == null)
            {
                if (!AnyMossTarget) { k.mossGoal = null; return false; }
                var p = k.mossGoal;
                if (p == null || !save.planted.Contains(p) || TransformOf(p) != "stone" || p == carrying) p = k.mossGoal = PickMoss(k);
                if (p == null) return false;
                if (WalkTo(k, PlantBase(p) + new Vector2(-0.25f * k.face, -0.08f), 1f, dt, out moving))
                {
                    k.workT += dt;
                    if (k.workT > 0.5f)
                    {
                        // ausrupfen und mitnehmen
                        k.workT = 0f; k.swing = 1f;
                        k.moss = plantViews.TryGetValue(new Cell(p.x, p.y), out var pv) && pv.sr != null ? pv.sr.sprite : Art.Get("white");
                        k.mossGoal = null;
                        Consume(p, PlantBase(p));
                        Sfx.Play("pop", 1.2f, 0.5f);
                    }
                }
                return true;
            }
            // mit Steinmoos zur Mauer
            if (!PickMossTarget(k.pos, out var wc))
            {
                // gerade nichts mehr zu tun: Moos behalten und warten
                WalkTo(k, HousePos + new Vector2(-1.6f, -1.6f), 0.4f, dt, out moving);
                return true;
            }
            if (WalkTo(k, WallStand(wc), 1f, dt, out moving))
            {
                k.workT += dt;
                if (k.workT > 0.6f)
                {
                    k.workT = 0f; k.swing = 1f;
                    var msg = FeedMoss(wc);
                    k.moss = null;
                    mossTargetsAt = 0f;   // Ziele gleich neu ermitteln
                    if (msg != null) Floater?.Invoke(CellCenter(wc) + new Vector2(0, WallH + 0.7f), msg, new Color(0.85f, 0.85f, 0.9f));
                    save.Save();
                }
            }
            return true;
        }

        /// <summary>Wo der Maurer an einer Mauer arbeitet: auf der Hof-Seite (zum Haus hin), nie draussen vor der Mauer.</summary>
        Vector2 WallStand(Cell wc)
        {
            Vector2 best = CellCenter(wc) + new Vector2(0.1f, -CellSize * 0.75f); float bd = float.MaxValue;
            foreach (var n in Neighbors4(wc))
            {
                if (Blocks(n)) continue;
                float d = (CellCenter(n) - HousePos).sqrMagnitude;
                if (d < bd) { bd = d; best = CellCenter(n); }
            }
            return best;
        }

        /// <summary>Getragenes Steinmoos ueber dem Kopf des Maurers.</summary>
        void DrawMoss(Knolli k, float hop)
        {
            if (k.moss == null) return;
            k.item.enabled = true;
            k.item.sprite = k.moss;
            float s = 0.32f / Mathf.Max(0.01f, k.moss.bounds.size.y);
            k.item.transform.localScale = new Vector3(s, s, 1f);
            k.item.transform.position = k.pos + new Vector2(0, 0.5f + hop + Mathf.Sin(Time.time * 4f) * 0.02f);
            k.item.sortingOrder = k.body.sortingOrder + 2;
        }
    }
}
