using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Knolli-Berufe: Knolli antippen, Beruf waehlen.
    ///  - Traeger (carrier): bringt die Ernte eines Felds zur Lieferkiste (wie bisher, Feld antippen).
    ///  - Giesser (water): holt Wasser am Bach (oder am Haus) und giesst die Pflanzen eines Felds – gegossen fuer eine Weile.
    ///  - Holzfaeller (wood): faellt Baeume und graebt Stuempfe aus, das Holz kommt wie beim Spieler.
    ///  - Maurer (mason): flickt die am staerksten beschaedigte Mauer.
    ///  - Wache (guard): nachts wirft sie Steine auf Zombies in der Naehe des Hauses, tagsueber schlaeft sie.
    /// Wie viele Knollis es geben kann, bestimmen Wohnplaetze: das Haus hat ein paar, jede Knolli-Huette mehr.
    /// Jeder Knolli traegt das Werkzeug seines Berufs. Werte in plants.json -> delivery (work...).
    /// </summary>
    public partial class WorldBuilder
    {
        public static readonly (string id, string name, string desc, bool needsField)[] KnolliRoles =
        {
            ("carrier", "Träger", "Bringt die Ernte eines Felds zur Lieferkiste", true),
            ("water", "Gießer", "Holt Wasser am Bach und gießt ein Feld", true),
            ("wood", "Holzfäller", "Fällt Bäume und bringt Holz", false),
            ("mason", "Maurer", "Flickt beschädigte Mauern", false),
            ("guard", "Wache", "Wirft nachts Steine auf Zombies", false),
        };

        public static string RoleName(string id) { foreach (var r in KnolliRoles) if (r.id == id) return r.name; return "Träger"; }
        static bool RoleNeedsField(string id) { foreach (var r in KnolliRoles) if (r.id == id) return r.needsField; return true; }

        string RoleOf(Knolli k) { var r = Job(k).role; return string.IsNullOrEmpty(r) ? "carrier" : r; }
        public string SelectedRole => selectedKnolli != null ? RoleOf(selectedKnolli) : null;
        string pendingRole;   // gewaehlter Feld-Beruf, wartet auf den Tipp aufs Feld

        readonly Dictionary<PlantedState, float> wetUntil = new Dictionary<PlantedState, float>();
        bool WetByKnolli(PlantedState p) => wetUntil.TryGetValue(p, out var t) && t > Time.time;

        // ------------------------------------------------------------------ Beruf waehlen

        /// <summary>Beruf fuer den ausgewaehlten Knolli. Feld-Berufe warten auf den Tipp aufs Feld.</summary>
        public void ChooseRole(string role)
        {
            var k = selectedKnolli;
            if (k == null) return;
            if (RoleNeedsField(role))
            {
                pendingRole = role;
                Floater?.Invoke(k.pos + new Vector2(0, 0.85f), RoleName(role) + ": tippe jetzt ein Feld an", new Color(0.8f, 1f, 0.7f));
                Sfx.Play("tick", 1.3f, 0.5f);
                return;
            }
            var j = Job(k);
            j.role = role;
            ResetWork(k);
            selectedKnolli = null;
            pendingRole = null;
            Floater?.Invoke(k.pos + new Vector2(0, 0.85f), "Ich bin jetzt " + RoleName(role) + "!", new Color(1f, 0.92f, 0.6f));
            Particles.I?.Burst(k.pos + new Vector2(0, 0.3f), "star", new Color(1f, 0.9f, 0.5f), 10, 2f, 0.5f, 0.15f, 1f, 1f);
            Sfx.Play("unlock", 1.4f, 0.6f);
            Haptics.Tap(20);
            save.Save();
        }

        /// <summary>Beim Zuordnen eines Felds: den gewaehlten Feld-Beruf (sonst den bisherigen bzw. Traeger) uebernehmen.</summary>
        void ApplyFieldRole(Knolli k)
        {
            var j = Job(k);
            string role = pendingRole ?? (RoleNeedsField(RoleOf(k)) ? RoleOf(k) : "carrier");
            if (j.role != role) { j.role = role; ResetWork(k); }
            pendingRole = null;
        }

        void ResetWork(Knolli k)
        {
            if (k.carry > 0 && RoleOf(k) != "carrier") { k.state = 2; k.target = CrateDrop; }   // Ladung noch abliefern
            else { k.state = k.carry > 0 ? 2 : 0; }
            k.goal = null; k.workTree = null; k.workT = 0f; k.water = 0; k.idleT = 0f;
        }

        // ------------------------------------------------------------------ Wohnplaetze

        public int Housing
        {
            get
            {
                int n = PlantBalance.I.Delivery.baseHousing;
                foreach (var s in save.structures)
                {
                    var d = PlantBalance.I.Structure(s.id);
                    if (s.placed && d != null && d.effect == "hut") n += Mathf.RoundToInt(StructValue(s));
                }
                return n;
            }
        }
        public bool HousingFull => save.knollis >= Housing;

        // ------------------------------------------------------------------ Arbeit

        /// <summary>Laeuft zum Ziel (um Mauern herum). true = angekommen.</summary>
        bool WalkTo(Knolli k, Vector2 target, float speedMul, float dt, out bool moving)
        {
            k.target = target;
            var aim = RouteAim(k, k.pos, target, dt, out bool walled);
            moving = false;
            if (walled) { KnolliWalledIn(k, dt); return false; }
            var d = aim - k.pos;
            float dist = d.magnitude;
            if ((target - k.pos).magnitude <= 0.05f) return true;
            float speed = PlantBalance.I.Delivery.knolliSpeed * speedMul * (k.gold ? 1.6f : 1f) * (HasPerk("stiefel") ? 1.3f : 1f);
            if (dist > 0.0001f) k.pos += d / dist * Mathf.Min(dist, speed * dt);
            k.walkT += dt * 10f;
            if (Mathf.Abs(d.x) > 0.02f) k.face = Mathf.Sign(d.x);
            moving = true;
            return false;
        }

        /// <summary>Ein Arbeitsschritt fuer Knollis mit anderem Beruf als Traeger. Liefert, ob er laeuft.</summary>
        bool UpdateWorker(Knolli k, string role, float dt)
        {
            bool moving = false;
            switch (role)
            {
                case "water": moving = WorkWater(k, dt); break;
                case "wood": moving = WorkWood(k, dt); break;
                case "mason": moving = WorkMason(k, dt); break;
                case "guard": moving = WorkGuard(k, dt); break;
            }
            return moving;
        }

        Vector2 WaterSource(Vector2 from)
        {
            Vector2 best = HousePos + new Vector2(-1.2f, -1.3f); float bd = float.MaxValue;
            foreach (var c in rivers.Keys)
            {
                var p = CellCenter(c);
                float d = (p - from).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (bd < float.MaxValue)
            {
                // ans Ufer stellen, nicht ins Wasser
                var c = CellAt(best);
                foreach (var n in Neighbors4(c)) if (!rivers.ContainsKey(n) && !Blocks(n)) return CellCenter(n) + (best - CellCenter(n)) * 0.35f;
            }
            return best;
        }

        bool WorkWater(Knolli k, float dt)
        {
            var bed = BedOf(k);
            bool moving;
            if (bed == null) { WalkTo(k, CrateDrop + new Vector2(-0.6f, 0f), 0.4f, dt, out moving); return moving; }
            if (k.water <= 0)
            {
                // zum Wasser
                if (WalkTo(k, WaterSource(k.pos), 1f, dt, out moving))
                {
                    k.workT += dt;
                    if (k.workT > 0.6f)
                    {
                        k.workT = 0f; k.water = PlantBalance.I.Delivery.workWaterPerTrip;
                        Particles.I?.Burst(k.pos + new Vector2(0.15f * k.face, 0.15f), "drop", new Color(0.45f, 0.75f, 1f), 8, 1.8f, 0.45f, 0.08f, 6f, 1.2f);
                        Sfx.Play("water", 1.3f, 0.35f);
                    }
                }
                return moving;
            }
            // naechste durstige Pflanze im Feld
            PlantedState pick = null; float bd = float.MaxValue;
            foreach (var p in save.planted)
            {
                if (!bed.Contains(new Cell(p.x, p.y)) || (wetUntil.TryGetValue(p, out var t) && t > Time.time + 8f)) continue;
                float d = (PlantBase(p) - k.pos).sqrMagnitude;
                if (d < bd) { bd = d; pick = p; }
            }
            if (pick == null) { WalkTo(k, BedCenter(bed) + new Vector2(0, -0.5f), 0.4f, dt, out moving); return moving; }
            if (WalkTo(k, PlantBase(pick) + new Vector2(-0.25f * k.face, -0.08f), 1f, dt, out moving))
            {
                k.workT += dt;
                if (k.workT > 0.45f)
                {
                    k.workT = 0f; k.water--;
                    wetUntil[pick] = Time.time + PlantBalance.I.Delivery.workWetSeconds;
                    StartCoroutine(Droplet(k.pos + new Vector2(0.15f * k.face, 0.45f), new Cell(pick.x, pick.y), 0f, false));
                    PunchField(new Cell(pick.x, pick.y), 0.3f);
                    k.swing = 1f;
                }
            }
            return moving;
        }

        static Vector2 BedCenter(HashSet<Cell> bed)
        {
            var s = Vector2.zero;
            foreach (var c in bed) s += CellCenter(c);
            return bed.Count > 0 ? s / bed.Count : Vector2.zero;
        }

        bool WorkWood(Knolli k, float dt)
        {
            bool moving;
            var t = k.workTree;
            if (t == null || t.gone || (t.sr == null && t.stump == null) || t.fallT >= 0f)
            {
                k.workTree = t = null;
                float bd = float.MaxValue;
                foreach (var o in trees)
                {
                    if (o.gone || o.fallT >= 0f || (o.sr == null && o.stump == null)) continue;
                    bool taken = false;
                    foreach (var other in knollis) if (other != k && other.workTree == o) taken = true;
                    if (taken) continue;
                    float d = (o.foot - k.pos).sqrMagnitude;
                    if (d < bd) { bd = d; t = o; }
                }
                k.workTree = t;
                if (t == null) { WalkTo(k, HousePos + new Vector2(1.5f, -1.4f), 0.4f, dt, out moving); return moving; }
            }
            float side = k.pos.x < t.foot.x ? -1f : 1f;
            if (WalkTo(k, t.foot + new Vector2(side * 0.38f, -0.05f), 1f, dt, out moving))
            {
                k.face = -side;
                k.workT += dt;
                if (k.workT > PlantBalance.I.Delivery.workChopInterval)
                {
                    k.workT = 0f;
                    k.swing = 1f;
                    Chop(t, k.pos, true);
                }
            }
            return moving;
        }

        bool WorkMason(Knolli k, float dt)
        {
            bool moving;
            WallView worst = null; Cell wc = default; float wr = 0.999f;
            foreach (var kv in walls)
            {
                float r = kv.Value.st.hp / Mathf.Max(1f, MaxHp(kv.Value.st));
                if (r < wr) { wr = r; worst = kv.Value; wc = kv.Key; }
            }
            if (worst == null) { WalkTo(k, HousePos + new Vector2(-1.6f, -1.6f), 0.4f, dt, out moving); return moving; }
            if (WalkTo(k, CellCenter(wc) + new Vector2(0.1f, -CellSize * 0.75f), 1f, dt, out moving))
            {
                k.workT += dt;
                if (k.workT > 0.55f)
                {
                    k.workT = 0f; k.swing = 1f;
                    float max = MaxHp(worst.st);
                    worst.st.hp = Mathf.Min(max, worst.st.hp + max * PlantBalance.I.Delivery.workRepair);
                    worst.pop = Mathf.Min(worst.pop, 0.85f);
                    Particles.I?.Burst(CellCenter(wc) + new Vector2(0, 0.2f), "square", new Color(0.8f, 0.7f, 0.55f), 5, 1.6f, 0.4f, 0.06f, 6f, 1f);
                    Sfx.Play("chop", Random.Range(1.4f, 1.6f), 0.3f);
                    if (worst.st.hp >= max) Floater?.Invoke(CellCenter(wc) + new Vector2(0, 0.9f), "wie neu!", new Color(0.8f, 1f, 0.7f));
                }
            }
            return moving;
        }

        bool WorkGuard(Knolli k, float dt)
        {
            bool moving;
            var nwI = NightWave.I;
            bool night = nwI != null && (nwI.Active || nwI.Evening);
            var post = HousePos + new Vector2(((k.slot % 3) - 1) * 1.1f, -2.2f);
            if (!night)
            {
                // tagsueber schlafen
                if (WalkTo(k, post + new Vector2(0, 0.6f), 0.4f, dt, out moving))
                {
                    k.workT += dt;
                    if (k.workT > 1.6f) { k.workT = 0f; Floater?.Invoke(k.pos + new Vector2(0.15f, 0.6f), "z", new Color(0.85f, 0.9f, 1f)); }
                }
                return moving;
            }
            WalkTo(k, post, 1f, dt, out moving);
            k.workT += dt;
            if (!moving && k.workT > PlantBalance.I.Delivery.guardInterval && nwI.Active)
            {
                k.workT = 0f;
                if (nwI.TowerShot(k.pos + new Vector2(0, 0.45f), PlantBalance.I.Delivery.guardRange, PlantBalance.I.Delivery.guardDamage)) k.swing = 1f;
            }
            return moving;
        }

        // ------------------------------------------------------------------ Werkzeug in der Hand

        static readonly Dictionary<string, Sprite> toolSprites = new Dictionary<string, Sprite>();

        void DrawTool(Knolli k, string role, float hop)
        {
            if (k.tool == null)
            {
                k.tool = new GameObject("KnolliTool").AddComponent<SpriteRenderer>();
                k.tool.transform.SetParent(objects, false);
            }
            var spr = role == "carrier" ? null : ToolSprite(role);
            k.tool.enabled = spr != null;
            if (spr == null) return;
            k.tool.sprite = spr;
            k.swing = Mathf.MoveTowards(k.swing, 0f, Time.deltaTime * 4f);
            float s = 0.3f / Mathf.Max(0.01f, spr.bounds.size.x);
            k.tool.transform.localScale = new Vector3(s * k.face, s, 1f);
            var hand = role == "guard" ? new Vector2(0f, 0.44f) : new Vector2(0.22f * k.face, 0.16f);   // Helm auf dem Kopf, sonst in der Hand
            k.tool.transform.position = k.pos + hand + new Vector2(0, hop);
            float sw = role == "guard" ? 0f : Mathf.Sin(k.swing * Mathf.PI) * 70f * -k.face;
            k.tool.transform.rotation = Quaternion.Euler(0, 0, sw);
            k.tool.sortingOrder = k.body.sortingOrder + 1;
        }

        /// <summary>Kleine Werkzeuge, im Code gemalt: Giesskanne, Axt, Hammer, Helm.</summary>
        static Sprite ToolSprite(string role)
        {
            if (toolSprites.TryGetValue(role, out var s)) return s;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            var wood = new Color(0.55f, 0.36f, 0.2f); var iron = new Color(0.62f, 0.64f, 0.7f); var ink = new Color(0.15f, 0.12f, 0.1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color c = Color.clear;
                    switch (role)
                    {
                        case "water":
                            if (x >= 7 && x <= 21 && y >= 4 && y <= 17) c = new Color(0.35f, 0.62f, 0.9f);                       // Kanne
                            if (Mathf.Abs(y - 11) <= 1 && x >= 7 && x <= 21) c = new Color(0.55f, 0.78f, 1f);
                            if (x > 21 && x < 29 && Mathf.Abs(y - (10 + (x - 21))) <= 1) c = new Color(0.35f, 0.62f, 0.9f);        // Tuelle
                            if (y >= 18 && y <= 22 && (x == 9 || x == 19 || (y == 22 && x > 9 && x < 19))) c = ink;                // Henkel
                            break;
                        case "wood":
                            if (x >= 14 && x <= 16 && y >= 2 && y <= 26) c = wood;                                                 // Stiel
                            if (x >= 6 && x <= 15 && y >= 19 && y <= 28 && (x - 6) * 0.8f + (28 - y) * 0.3f < 9) c = iron;          // Klinge
                            break;
                        case "mason":
                            if (x >= 14 && x <= 16 && y >= 2 && y <= 22) c = wood;
                            if (x >= 7 && x <= 24 && y >= 21 && y <= 27) c = iron;                                                 // Hammerkopf
                            break;
                        case "guard":
                            {
                                float dx = (x - 16f) / 12f, dy = (y - 10f) / 11f;
                                if (dy >= 0 && dx * dx + dy * dy < 1f) c = Color.Lerp(iron, Color.white, dy * 0.4f);              // Helm
                                if (y >= 9 && y <= 11 && x >= 3 && x <= 29) c = new Color(0.5f, 0.52f, 0.58f);                    // Rand
                                if (x >= 15 && x <= 17 && y >= 21 && y <= 25) c = new Color(0.85f, 0.3f, 0.25f);                  // Federbusch
                                break;
                            }
                    }
                    // Kontur
                    px[y * n + x] = c;
                }
            var outl = (Color[])px.Clone();
            for (int y = 1; y < n - 1; y++)
                for (int x = 1; x < n - 1; x++)
                    if (px[y * n + x].a == 0f && (px[y * n + x + 1].a > 0 || px[y * n + x - 1].a > 0 || px[(y + 1) * n + x].a > 0 || px[(y - 1) * n + x].a > 0)) outl[y * n + x] = ink;
            tex.SetPixels(outl); tex.Apply();
            return toolSprites[role] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.2f), n);
        }
    }
}
