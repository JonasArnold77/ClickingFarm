using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Ernte als Produkte: Ernte-Pflanzen (Radieschen, Weissruebe, ...) bringen keine Muenzen mehr direkt, sondern
    /// ihr eigenes Produkt (catalog.product), das sich als kleiner Vorrat neben der Pflanze stapelt – beim Antippen und
    /// langsam von selbst. Knollis (kleine braune, runde Wesen) sammeln den Vorrat ein und tragen ihn zur Lieferkiste
    /// beim Haus. Sobald die Ernte in der Kiste liegt, gibt es das Geld. Werte in plants.json -> delivery.
    /// </summary>
    public partial class WorldBuilder
    {
        // ------------------------------------------------------------------ Vorrat an der Pflanze

        readonly Dictionary<PlantedState, double> prodFrac = new Dictionary<PlantedState, double>();
        readonly Dictionary<PlantedState, List<SpriteRenderer>> pileViews = new Dictionary<PlantedState, List<SpriteRenderer>>();

        public event Action<int, double> DeliveryPaid;   // Stueck, Muenzen

        public static bool MakesProduct(PlantedState p) => p != null && p.Role == "harvest";
        public static string ProductName(CatalogDef d) => d == null ? "" : !string.IsNullOrEmpty(d.product) ? d.product : d.name;
        const int PileShown = 48;   // so viele Stuecke werden gezeigt (der Vorrat selbst ist unbegrenzt)

        /// <summary>Wert eines Produkts dieser Pflanze (wie frueher ein Ernte-Tipp).</summary>
        public double ItemValue(PlantedState p)
        {
            double v = Math.Max(1, HarvestPerTap(p));
            return HasPerk("horn") ? v * 1.25 : v;   // Erntehorn
        }

        /// <summary>Produkte in den Vorrat der Pflanze legen (ohne Grenze – man kann so lange ernten, wie man will).</summary>
        public bool AddProduct(PlantedState p, int n, double valueEach, Vector2? fxFrom = null)
        {
            int add = Mathf.Max(0, n);
            if (fxFrom.HasValue && HasPerk("klee") && UnityEngine.Random.value < 0.15f)
            {
                add *= 2;   // Gluecksklee
                Floater?.Invoke(fxFrom.Value + new Vector2(-0.3f, 0.5f), "Glücksklee!", new Color(0.6f, 1f, 0.5f));
            }
            p.stock += add;
            p.stockValue += add * valueEach;
            // die Pflanze schuettelt sich und blitzt auf, wenn Ernte herausfaellt
            if (add > 0 && plantViews.TryGetValue(new Cell(p.x, p.y), out var dv))
            {
                dv.flash = Mathf.Max(dv.flash, 0.9f);
                dv.tiltVel += (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(45f, 70f);
                dv.syVel -= 4f;
            }
            LaunchDrops(p, add, fxFrom.HasValue);   // crunchy: herausploppen, fliegen, aufprallen, nachspringen
            if (fxFrom.HasValue)
            {
                Floater?.Invoke(fxFrom.Value + new Vector2(0.25f, 0.3f), "+" + add + " " + ProductName(PlantBalance.I.Plant(p.type)), new Color(1f, 0.92f, 0.6f));
            }
            return true;
        }

        Vector2 PilePos(PlantedState p) => PlantBase(p) + new Vector2(CellSize * 0.32f, -CellSize * 0.1f);

        /// <summary>Feste Zufallszahl 0..1 je Pflanze, Stueck und Kanal (bleibt beim Neuzeichnen gleich).</summary>
        static float Hash(PlantedState p, int i, int ch)
        {
            float v = Mathf.Sin((i + 1) * 12.9898f + p.x * 78.233f + p.y * 37.719f + ch * 4.1414f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        /// <summary>Platz i: wild verstreut im Feld und rund ums Feld herum (nicht direkt auf der Pflanze).</summary>
        Vector2 ScatterPos(PlantedState p, int i)
        {
            var center = PlantBase(p) + new Vector2(0, CellSize * 0.38f);   // Feldmitte (auf Mauer/Pult mit angehoben)
            float dx = (Hash(p, i, 0) * 2f - 1f) * CellSize * 0.66f;
            float dy = (Hash(p, i, 1) * 2f - 1f) * CellSize * 0.6f;
            // die Mitte (dort steht die Pflanze) freilassen
            if (Mathf.Abs(dx) < CellSize * 0.18f && Mathf.Abs(dy) < CellSize * 0.2f) dx = Mathf.Sign(dx + 0.001f) * CellSize * (0.2f + Hash(p, i, 2) * 0.3f);
            return center + new Vector2(dx, dy);
        }

        /// <summary>Von selbst nachproduzieren (vorher verdienten diese Pflanzen Muenzen pro Sekunde).</summary>
        void UpdateProducts(float dt)
        {
            foreach (var p in save.planted)
            {
                if (!p.Grown || !MakesProduct(p)) continue;
                prodFrac.TryGetValue(p, out var f);
                f += PlantCps(p) * dt;
                double val = ItemValue(p);
                if (f >= val) { f -= val; AddProduct(p, 1, val, null); }
                prodFrac[p] = f;
            }
            UpdatePiles();
        }

        /// <summary>Die Ernte liegt wild im und ums Feld verstreut: ein Mini-Bild je Stueck im Vorrat.</summary>
        void UpdatePiles()
        {
            foreach (var p in save.planted)
            {
                int show = Mathf.Min(PileShown, Mathf.Max(0, p.stock - InFlight(p)));   // was noch fliegt, liegt noch nicht
                pileViews.TryGetValue(p, out var list);
                if (list == null && show == 0) continue;
                if (list == null) pileViews[p] = list = new List<SpriteRenderer>();
                var spr = plantViews.TryGetValue(new Cell(p.x, p.y), out var v) && v.sr != null ? v.sr.sprite : null;
                while (list.Count < show)
                {
                    var sr = new GameObject("Pile").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    list.Add(sr);
                }
                for (int i = 0; i < list.Count; i++)
                {
                    var sr = list[i];
                    sr.enabled = i < show && spr != null;
                    if (!sr.enabled) continue;
                    sr.sprite = spr;
                    float s = 0.15f / Mathf.Max(0.01f, spr.bounds.size.x);
                    sr.transform.localScale = Vector3.one * s;
                    var pos = ScatterPos(p, i);
                    sr.transform.position = pos;
                    // hinter der Pflanze liegende Stuecke werden von ihr verdeckt, vordere liegen davor
                    int plantOrder = Order(PlantSortY(p, PlantBase(p).y));   // auch auf Mauer/Pult richtig
                    bool behind = pos.y > PlantBase(p).y + CellSize * 0.1f;
                    sr.sortingOrder = plantOrder + (behind ? -3 : 12);
                    sr.transform.rotation = Quaternion.Euler(0, 0, (Hash(p, i, 3) * 2f - 1f) * 35f);
                }
            }
            // Haeufchen von Pflanzen, die es nicht mehr gibt
            foreach (var key in new List<PlantedState>(pileViews.Keys))
                if (!save.planted.Contains(key)) { foreach (var sr in pileViews[key]) if (sr != null) Destroy(sr.gameObject); pileViews.Remove(key); }
        }

        // ------------------------------------------------------------------ Lieferkiste

        SpriteRenderer crate;
        Vector2 cratePos;
        float crateSquash, crateScale;
        static Sprite crateSprite;

        public int DepotCount => save.depotCount;
        public double DepotValue => save.depotValue;

        void PlaceCrate()
        {
            cratePos = HousePos + new Vector2(1.9f, -1.6f);
            Shadow(cratePos, 1f, 0.25f, 0.3f);
            crate = new GameObject("Lieferkiste").AddComponent<SpriteRenderer>();
            crate.transform.SetParent(objects, false);
            crate.sprite = CrateSprite;
            crateScale = 0.85f / Mathf.Max(0.01f, crate.sprite.bounds.size.x);
            crate.transform.position = cratePos;
            crate.transform.localScale = Vector3.one * crateScale;
            crate.sortingOrder = Order(cratePos.y);
            Reserve(cratePos + new Vector2(0, 0.2f), 0.6f);
        }

        Vector2 CrateDrop => cratePos + new Vector2(0, -0.15f);

        public bool HitCrate(Vector2 w)
        {
            if (crate == null) return false;
            var b = crate.bounds; b.Expand(new Vector3(0.2f, 0.3f, 0));
            return b.Contains(new Vector3(w.x, w.y, b.center.z));
        }

        public void TapCrate()
        {
            crateSquash = 0.6f;
            Sfx.Play("pop", 0.9f);
            Floater?.Invoke(cratePos + new Vector2(0, 1f), save.deliveredCount > 0
                ? "Lieferkiste: schon " + save.deliveredCount + " Stück verkauft"
                : "Lieferkiste – Knollis bringen die Ernte her, Geld gibt es sofort", new Color(1f, 0.92f, 0.6f));
        }

        void Deposit(int n, double value, Vector2 at, Dictionary<string, int> types = null)
        {
            CountDelivered(types);
            // sofort bezahlt, sobald die Ernte in der Kiste liegt
            save.deliveredCount += n;
            crateSquash = 0.7f;
            Particles.I?.Burst(cratePos + new Vector2(0, 0.5f), "square", new Color(1f, 0.85f, 0.3f), 8, 2.2f, 0.6f, 0.08f, 3f, 1.2f);
            AddCoins(value, cratePos + new Vector2(0, 0.8f));
            Sfx.Play("coin", UnityEngine.Random.Range(1.1f, 1.3f), 0.6f);
        }

        /// <summary>Alte Spielstaende: was noch in der Kiste wartete, am naechsten Morgen auszahlen.</summary>
        void PayDelivery()
        {
            if (save.depotCount <= 0 && save.depotValue <= 0) return;
            int n = save.depotCount; double v = save.depotValue;
            save.depotCount = 0; save.depotValue = 0;
            AddCoins(v, cratePos + new Vector2(0, 0.9f));
            crateSquash = 1f;
            Particles.I?.Burst(cratePos + new Vector2(0, 0.6f), "square", new Color(1f, 0.85f, 0.3f), 24, 4f, 0.8f, 0.1f, 4f, 1.5f);
            Sfx.Play("coin", 1f, 1f);
            DeliveryPaid?.Invoke(n, v);
        }

        // ------------------------------------------------------------------ Knollis

        class Knolli
        {
            public SpriteRenderer body, shadow, item;
            public Vector2 pos, target; public int state; public PlantedState goal;
            public int carry; public double carryValue; public float walkT, idleT, face = 1f;
            public bool gold; public int slot; public readonly Dictionary<string, int> types = new Dictionary<string, int>();
            public readonly List<Sprite> load = new List<Sprite>();                  // Bilder der Ladung (eins je schwebendem Stueck)
            public readonly List<SpriteRenderer> orbit = new List<SpriteRenderer>(); // schweben um den Knolli herum
            public readonly List<Vector2> lpos = new List<Vector2>(), lvel = new List<Vector2>();
            public TreeObj workTree; public float workT, swing; public int water; public SpriteRenderer tool;   // Berufe   // Ort und Tempo jedes Stuecks (folgt wie an einem Magneten)
        }

        const int OrbitMax = 12;

        readonly List<Knolli> knollis = new List<Knolli>();
        static Sprite knolliSprite;

        public int KnolliCount => save.knollis;
        public double KnolliCost => Math.Ceiling(PlantBalance.I.Delivery.knolliCost * Math.Pow(PlantBalance.I.Delivery.knolliCostGrowth, Mathf.Max(0, save.knollis - PlantBalance.I.Delivery.startKnollis)));

        void InitKnollis()
        {
            if (!save.knolliInit) { save.knollis = Mathf.Max(save.knollis, PlantBalance.I.Delivery.startKnollis); save.knolliInit = true; }
            for (int i = 0; i < save.knollis; i++) SpawnKnolli(false);
            foreach (var pk in save.perks) ApplyPerk(pk, false);
            EnsureChallenge();
        }

        public bool HireKnolli()
        {
            if (HousingFull) { Floater?.Invoke(CrateDrop + new Vector2(0, 1f), "Kein Platz mehr – bau eine Knolli-Hütte!", new Color(1f, 0.75f, 0.5f)); Sfx.Play("error", 1f, 0.5f); return false; }
            if (!TrySpend(KnolliCost)) return false;
            save.knollis++;
            SpawnKnolli(true);
            Sfx.Play("unlock", 1.3f, 0.7f);
            Haptics.Tap(20);
            save.Save();
            return true;
        }

        void SpawnKnolli(bool fx, bool gold = false)
        {
            var k = new Knolli { pos = CrateDrop + UnityEngine.Random.insideUnitCircle * 0.4f, idleT = UnityEngine.Random.Range(0f, 1f), gold = gold, slot = knollis.Count };
            k.shadow = Shadow(k.pos, 0.48f, 0.14f, 0.3f);
            k.body = new GameObject("Knolli").AddComponent<SpriteRenderer>();
            k.body.transform.SetParent(objects, false);
            k.body.sprite = KnolliSprite;
            if (gold) k.body.color = new Color(1f, 0.85f, 0.35f);
            if (fx) Floater?.Invoke(k.pos + new Vector2(0, 0.8f), "Tipp mich an, dann ein Feld!", new Color(0.8f, 1f, 0.7f));
            k.item = new GameObject("KnolliLoad").AddComponent<SpriteRenderer>();
            k.item.transform.SetParent(objects, false);
            k.item.enabled = false;
            knollis.Add(k);
            if (fx) Particles.I?.Burst(k.pos + new Vector2(0, 0.2f), "star", new Color(0.85f, 0.65f, 0.4f), 10, 2f, 0.5f, 0.15f, 1f, 1f);
        }

        /// <summary>Pflanze mit Vorrat, die noch nicht von genug Knollis angesteuert wird – die naechste zuerst.</summary>
        PlantedState FindPickup(Knolli k)
        {
            PlantedState best = null; float bd = float.MaxValue;
            foreach (var p in save.planted)
            {
                if (p.stock <= 0 || !InBed(k, p)) continue;   // nur im eigenen Feld
                bool taken = false;
                foreach (var o in knollis) if (o != k && o.goal == p && o.state == 1) taken = true;
                if (taken) continue;
                float d = (PilePos(p) - k.pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        void UpdateKnollis(float dt)
        {
            bedCache.Clear();   // Felder koennen sich aendern: Beete jeden Frame neu bestimmen
            var D = PlantBalance.I.Delivery;
            foreach (var k in knollis)
            {
                string role = RoleOf(k);
                if (role != "carrier" && k.carry == 0) { DrawKnolli(k, UpdateWorker(k, role, dt), dt, role); continue; }
                // Ziel weg (geerntet, umgetopft)? Mit Ladung zur Kiste, sonst neu suchen
                if (k.state == 1 && (k.goal == null || !save.planted.Contains(k.goal) || k.goal.stock <= 0)) { k.goal = null; k.state = k.carry > 0 ? 2 : 0; k.target = CrateDrop; }
                if (k.state == 0)
                {
                    k.idleT -= dt;
                    if (k.idleT <= 0f)
                    {
                        var p = FindPickup(k);
                        if (p != null) { k.goal = p; k.state = 1; k.target = PilePos(p) + new Vector2(0.12f, -0.05f); }
                        else { k.idleT = UnityEngine.Random.Range(0.8f, 2f); k.target = CrateDrop + UnityEngine.Random.insideUnitCircle * 0.7f; }
                    }
                }
                if (k.state == 1) k.target = PilePos(k.goal) + new Vector2(0.12f, -0.05f);   // Pflanze kann umgetopft werden

                // Weg um Mauern herum, durch Tore
                var aim = RouteAim(k, k.pos, k.target, dt, out bool walled);
                if (walled && k.state != 0) KnolliWalledIn(k, dt);
                var d = aim - k.pos;
                float dist = d.magnitude;
                float speed = D.knolliSpeed * (k.state == 0 ? 0.4f : 1f) * (k.gold ? 1.6f : 1f) * (HasPerk("stiefel") ? 1.3f : 1f);
                bool moving = !walled && (k.target - k.pos).magnitude > 0.04f;
                if (moving && dist > 0.0001f)
                {
                    k.pos += d / dist * Mathf.Min(dist, speed * dt);
                    k.walkT += dt * 10f;
                    if (Mathf.Abs(d.x) > 0.02f) k.face = Mathf.Sign(d.x);
                }
                else if (k.state == 1)
                {
                    // Magnet: alle Ernte im Umkreis fliegt zum Knolli
                    float range = D.knolliRange * (HasPerk("magnet") ? 1.6f : 1f);
                    int flies = 0;
                    foreach (var q in save.planted)
                    {
                        if (q.stock <= 0 || !InBed(k, q) || (PilePos(q) - k.pos).sqrMagnitude > range * range) continue;
                        var qspr = plantViews.TryGetValue(new Cell(q.x, q.y), out var qv) && qv.sr != null ? qv.sr.sprite : null;
                        // jedes Stueck loest sich aus dem Haufen und wird vom Knolli angezogen (Startpunkt = sein Platz im Haufen)
                        for (int i = 0; i < q.stock && k.load.Count < OrbitMax; i++, flies++)
                        {
                            k.load.Add(qspr);
                            k.lpos.Add(i < PileShown ? ScatterPos(q, i) : PilePos(q));
                            k.lvel.Add(new Vector2(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(1.2f, 2.2f)));   // kleiner Hopser beim Losreissen
                        }
                        k.carry += q.stock; k.carryValue += q.stockValue;
                        k.types.TryGetValue(q.type, out int had); k.types[q.type] = had + q.stock;
                        q.stock = 0; q.stockValue = 0;
                        if (k.item.sprite == null || q == k.goal) k.item.sprite = qspr;
                    }
                    if (flies > 0) { Particles.I?.Ring(k.pos + new Vector2(0, 0.3f), new Color(1f, 0.92f, 0.6f, 0.6f), range * 1.2f, 0.4f); Sfx.Play("grow", 1.5f, 0.4f); }
                    k.state = 2; k.goal = null; k.target = CrateDrop + UnityEngine.Random.insideUnitCircle * 0.15f;
                }
                else if (k.state == 2)
                {
                    Unload(k);
                    k.state = 3; k.idleT = 0.9f;
                }
                else if (k.state == 3)
                {
                    // kurz stehen bleiben und zuschauen, wie die Ladung in die Kiste fliegt
                    k.idleT -= dt;
                    if (k.idleT <= 0f) { k.state = 0; k.idleT = 0.2f; }
                }

                DrawKnolli(k, moving, dt, role);
            }

            UpdateKnolliJobs(dt);
            if (crate != null)
            {
                crateSquash = Mathf.MoveTowards(crateSquash, 0f, dt * 4f);
                float csq = Mathf.Sin(crateSquash * Mathf.PI * 1.5f) * crateSquash * 0.12f;
                crate.transform.localScale = new Vector3(crateScale * (1 + csq), crateScale * (1 - csq), 1);
            }
        }

        /// <summary>Zeichnen: huepfen beim Laufen, Schatten, schwebende Ladung, Werkzeug des Berufs.</summary>
        void DrawKnolli(Knolli k, bool moving, float dt, string role)
        {
            float hop = moving ? Mathf.Abs(Mathf.Sin(k.walkT)) * 0.06f : Mathf.Sin(Time.time * 2f + k.pos.x) * 0.01f;
            float sq = moving ? Mathf.Sin(k.walkT * 2f) * 0.06f : Mathf.Sin(k.swing * Mathf.PI) * 0.08f;   // beim Arbeiten staucht er sich
            float s = 0.52f / Mathf.Max(0.01f, KnolliSprite.bounds.size.x);
            k.body.transform.position = k.pos + new Vector2(0, hop);
            k.body.transform.localScale = new Vector3(s * k.face * (1 + sq), s * (1 - sq), 1);
            k.body.sortingOrder = Order(k.pos.y) + 1;
            if (k.shadow != null) { k.shadow.transform.position = k.pos + new Vector2(0.03f, 0.01f); }
            k.item.enabled = false;
            DrawOrbit(k, hop, dt);
            DrawTool(k, role, hop);
        }

        // ------------------------------------------------------------------ Schwebende Ladung und Effektgewitter

        /// <summary>Freies Schweben: jedes Stueck treibt auf eigener, ruhiger Zufallsbahn in einer Wolke um den Knolli.</summary>
        Vector2 OrbitPos(Knolli k, int i, int n, float hop, out bool front)
        {
            float rx = 0.34f + 0.012f * n, ry = 0.24f + 0.008f * n;
            // Grundplatz in der Wolke (gleichmaessig verteilt), dazu eigenes Treiben mit krummen Frequenzen
            float a0 = i * 2.39996f + k.slot * 1.3f, r0 = Mathf.Sqrt((i + 0.5f) / Mathf.Max(1, n));
            float h1 = Mathf.Repeat(i * 0.6180339f + k.slot * 0.37f, 1f), h2 = Mathf.Repeat(i * 0.4142f + k.slot * 0.71f, 1f), t = Time.time;
            float nx = Mathf.Cos(a0) * r0 + Mathf.Sin(t * (0.9f + h1) + h2 * 6.3f) * 0.38f + Mathf.Sin(t * (2.1f + h2) + h1 * 4f) * 0.12f;
            float ny = Mathf.Sin(a0) * r0 + Mathf.Sin(t * (0.7f + h2) + h1 * 6.3f) * 0.38f + Mathf.Cos(t * (1.7f + h1) + h2 * 4f) * 0.12f;
            float m = Mathf.Sqrt(nx * nx + ny * ny);
            if (m > 1f) { nx /= m; ny /= m; }   // in der Wolke bleiben
            front = ny < 0f;   // unten = naeher am Betrachter
            return k.pos + new Vector2(nx * rx, 0.32f + hop + ny * ry);
        }

        /// <summary>Die Ernte schwebt beim Tragen frei um den Knolli herum (vorn davor, hinten dahinter).</summary>
        void DrawOrbit(Knolli k, float hop, float dt)
        {
            int n = k.carry > 0 ? k.load.Count : 0;
            while (k.orbit.Count < n)
            {
                var sr = new GameObject("KnolliOrbit").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                k.orbit.Add(sr);
            }
            for (int i = 0; i < k.orbit.Count; i++)
            {
                var sr = k.orbit[i];
                sr.enabled = i < n && k.load[i] != null;
                if (!sr.enabled) continue;
                sr.sprite = k.load[i];
                var goal = OrbitPos(k, i, n, hop, out bool front);
                // Magnet-Folgen: Feder zum Platz in der Wolke, mit Nachziehen – weit weg zieht es staerker
                while (k.lpos.Count <= i) { k.lpos.Add(goal); k.lvel.Add(Vector2.zero); }
                var p = k.lpos[i]; var vel = k.lvel[i];
                float h = Mathf.Min(dt, 0.05f);
                var d = goal - p;
                float pull = 26f + 40f * Mathf.Clamp01(d.magnitude - 0.4f);
                vel += (d * pull - vel * 6.5f) * h;
                if (vel.sqrMagnitude > 49f) vel = vel.normalized * 7f;
                p += vel * h;
                k.lpos[i] = p; k.lvel[i] = vel;
                float spd = vel.magnitude;
                float stretch = 1f + Mathf.Min(0.45f, spd * 0.08f);
                float s = (front ? 0.19f : 0.15f) / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
                sr.transform.position = p;
                // in Bewegungsrichtung gestreckt und geneigt, in Ruhe sanftes Wiegen
                float ang = spd > 0.3f ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f : 0f;
                sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(Mathf.Sin(Time.time * 4f + i) * 20f, ang, Mathf.Clamp01(spd * 0.4f)));
                sr.transform.localScale = new Vector3(s / Mathf.Sqrt(stretch), s * stretch, 1f);
                if (spd > 2f && UnityEngine.Random.value < 0.3f)
                    Particles.I?.Spawn(p, "spark", new Color(1f, 0.92f, 0.6f, 0.7f), Vector3.zero, 0.3f, 0.045f, 0f, 0f);
                sr.sortingOrder = k.body.sortingOrder + (front ? 2 : -1);
                sr.color = front ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                if (UnityEngine.Random.value < 0.04f)
                    Particles.I?.Spawn(p, "spark", new Color(1f, 0.92f, 0.55f, 0.8f), Vector3.up * 0.2f, 0.4f, 0.05f, 0f, 0f);
            }
        }

        /// <summary>An der Kiste: jedes schwebende Stueck fliegt nacheinander hinein, das letzte loest Bezahlung und Effektgewitter aus.</summary>
        void Unload(Knolli k)
        {
            int n = k.carry; double value = k.carryValue;
            var types = new Dictionary<string, int>(k.types);
            int m = 0;
            for (int i = 0; i < k.load.Count; i++) if (k.load[i] != null) m++;
            if (m == 0) { Deposit(n, value, k.pos, types); CrateStorm(n); }
            else
            {
                int idx = 0;
                for (int i = 0; i < k.load.Count; i++)
                {
                    if (k.load[i] == null) continue;
                    var from = i < k.lpos.Count ? k.lpos[i] : OrbitPos(k, i, k.load.Count, 0f, out _);
                    bool last = ++idx == m;
                    int step = idx;
                    StartCoroutine(IntoCrate(from, k.load[i], (idx - 1) * 0.07f, step, last ? () => { Deposit(n, value, cratePos, types); CrateStorm(n); } : (Action)null));
                }
            }
            k.carry = 0; k.carryValue = 0; k.types.Clear(); k.load.Clear(); k.lpos.Clear(); k.lvel.Clear(); k.item.sprite = null;
            foreach (var sr in k.orbit) sr.enabled = false;
        }

        IEnumerator IntoCrate(Vector2 from, Sprite spr, float delay, int step, Action onHit)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var sr = new GameObject("IntoCrate").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = spr;
            sr.sortingOrder = 30005;
            float size = 0.19f / Mathf.Max(0.01f, spr.bounds.size.x);
            var to = cratePos + new Vector2(0, 0.45f);
            float dur = 0.45f + Vector2.Distance(from, to) * 0.05f;
            float h = 0.7f + Vector2.Distance(from, to) * 0.15f;
            float spin = UnityEngine.Random.Range(-540f, 540f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                float e = t * t * (3f - 2f * t);
                var pos = Vector2.Lerp(from, to, e) + Vector2.up * (4f * h * t * (1f - t));
                sr.transform.position = pos;
                sr.transform.rotation = Quaternion.Euler(0, 0, spin * t);
                sr.transform.localScale = Vector3.one * size * (1f + 0.5f * Mathf.Sin(t * Mathf.PI)) * (t > 0.85f ? Mathf.Lerp(1f, 0.3f, (t - 0.85f) / 0.15f) : 1f);
                // Glitzerspur
                Particles.I?.Spawn(pos, UnityEngine.Random.value < 0.5f ? "spark" : "star", Color.HSVToRGB(Mathf.Repeat(t * 0.6f + step * 0.11f, 1f), 0.45f, 1f),
                                   UnityEngine.Random.insideUnitCircle * 0.3f, 0.4f, 0.11f, 0f, 0f);
                yield return null;
            }
            Destroy(sr.gameObject);
            // jedes Stueck: kleiner Plopp, Kiste wippt, Ton steigt
            crateSquash = Mathf.Max(crateSquash, 0.45f);
            Particles.I?.Burst(to, "spark", new Color(1f, 0.95f, 0.6f), 5, 1.8f, 0.35f, 0.08f, 2f, 1f);
            Sfx.Play("tick", Mathf.Min(2.2f, 1.1f + step * 0.07f), 0.45f);
            onHit?.Invoke();
        }

        /// <summary>Effektgewitter an der Lieferkiste – je groesser die Lieferung, desto wilder.</summary>
        void CrateStorm(int n)
        {
            var top = cratePos + new Vector2(0, 0.5f);
            float k = Mathf.Clamp01(n / 20f);
            crateSquash = 1f;
            Shockwave(cratePos + new Vector2(0, 0.2f), 1.2f + 1.2f * k, 0.45f, new Color(1f, 0.88f, 0.4f, 0.9f));
            Particles.I?.Ring(top, new Color(1f, 0.95f, 0.7f, 0.8f), 1.2f + k, 0.4f);
            Particles.I?.Burst(top, "star", new Color(1f, 0.85f, 0.3f), 12 + Mathf.RoundToInt(20 * k), 3.5f, 0.9f, 0.3f, 2f, 2.5f);
            Particles.I?.Burst(top, "spark", Color.white, 10 + Mathf.RoundToInt(14 * k), 4.5f, 0.5f, 0.12f, 1f, 2f);
            Particles.I?.Burst(top, "square", new Color(1f, 0.75f, 0.2f), 8 + Mathf.RoundToInt(12 * k), 3f, 0.8f, 0.09f, 6f, 3f);
            for (int i = 0; i < 3; i++)
                Particles.I?.Burst(top + UnityEngine.Random.insideUnitCircle * 0.4f, "star", Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 1f), 4, 2.5f, 0.7f, 0.2f, 1f, 1.5f);
            WorldFx.Play("firework", top + new Vector2(0, 0.5f), 0.1f + 0.08f * k);
            WorldPostFX.Punch(0.12f + 0.2f * k);
            Sfx.Play("unlock", 1.5f, 0.35f + 0.3f * k);
            Haptics.Tap(15 + Mathf.RoundToInt(20 * k));
        }

        // ------------------------------------------------------------------ Bilder

        /// <summary>Knolli: kleiner brauner, runder Kerl mit hellem Bauch, grossen Augen und Fuesschen.</summary>
        static Sprite KnolliSprite
        {
            get
            {
                if (knolliSprite != null) return knolliSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                var brown = new Color(0.52f, 0.34f, 0.2f); var hi = new Color(0.72f, 0.5f, 0.3f); var dark = new Color(0.3f, 0.18f, 0.1f);
                var belly = new Color(0.86f, 0.7f, 0.5f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        Color c = Color.clear;
                        float bx = (x - 32f) / 24f, by = (y - 30f) / 22f;
                        float b = bx * bx + by * by;
                        // Fuesschen
                        float f1 = ((x - 23f) * (x - 23f)) / 30f + ((y - 7f) * (y - 7f)) / 12f, f2 = ((x - 41f) * (x - 41f)) / 30f + ((y - 7f) * (y - 7f)) / 12f;
                        if (f1 < 1f || f2 < 1f) c = dark;
                        if (b < 1f)
                        {
                            c = Color.Lerp(brown, hi, Mathf.Clamp01(1f - ((x - 24f) * (x - 24f) + (y - 40f) * (y - 40f)) / 500f) * 0.8f);
                            if (((x - 32f) / 13f) * ((x - 32f) / 13f) + ((y - 22f) / 11f) * ((y - 22f) / 11f) < 1f) c = belly;   // Bauch
                            if (b > 0.86f) c = dark;   // Kontur
                            // Augen
                            float e1 = (x - 25f) * (x - 25f) + (y - 36f) * (y - 36f), e2 = (x - 39f) * (x - 39f) + (y - 36f) * (y - 36f);
                            if (e1 < 30f || e2 < 30f) c = Color.white;
                            if ((x - 26.5f) * (x - 26.5f) + (y - 35f) * (y - 35f) < 9f || (x - 40.5f) * (x - 40.5f) + (y - 35f) * (y - 35f) < 9f) c = new Color(0.08f, 0.06f, 0.05f);
                            if ((x - 27.5f) * (x - 27.5f) + (y - 36.5f) * (y - 36.5f) < 1.5f || (x - 41.5f) * (x - 41.5f) + (y - 36.5f) * (y - 36.5f) < 1.5f) c = Color.white;
                            // Waengchen
                            if ((x - 20f) * (x - 20f) + (y - 29f) * (y - 29f) < 7f || (x - 44f) * (x - 44f) + (y - 29f) * (y - 29f) < 7f) c = Color.Lerp(c, new Color(0.95f, 0.55f, 0.45f), 0.6f);
                        }
                        // kleiner Spross auf dem Kopf
                        if (Mathf.Abs(x - 32f) < 1.6f && y >= 51 && y < 58) c = new Color(0.35f, 0.6f, 0.25f);
                        if (((x - 36f) * (x - 36f)) / 14f + ((y - 58f) * (y - 58f)) / 5f < 1f) c = new Color(0.45f, 0.72f, 0.3f);
                        if (c.a > 0f) c.a = 1f;
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                knolliSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.06f), n);
                return knolliSprite;
            }
        }

        /// <summary>Lieferkiste: Holzkiste mit Latten und einem Schild.</summary>
        static Sprite CrateSprite
        {
            get
            {
                if (crateSprite != null) return crateSprite;
                const int w = 96, h = 84;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var wood = new Color(0.62f, 0.42f, 0.24f); var woodHi = new Color(0.84f, 0.62f, 0.38f); var dark = new Color(0.35f, 0.22f, 0.12f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = Color.clear;
                        bool front = x >= 6 && x < w - 6 && y >= 2 && y < 52;
                        bool lid = x >= 10 && x < w - 10 && y >= 52 && y < 74 - (Mathf.Abs(x - w / 2) < 30 ? 0 : 2);
                        if (front)
                        {
                            float g = Mathf.PerlinNoise(x * 0.08f, y * 0.5f) * 0.3f;
                            c = Color.Lerp(wood, woodHi, g + (y % 16) / 32f);
                            if (y % 16 < 2) c = dark;                                       // Latten
                            if (x < 9 || x >= w - 9) c = dark * 1.2f;                       // Pfosten
                            if (Mathf.Abs((x - 6) - (y - 2) * 1.76f) < 2.2f) c = dark * 1.1f;   // Querstrebe
                        }
                        else if (lid) c = Color.Lerp(wood, woodHi, 0.5f + Mathf.PerlinNoise(x * 0.1f, y * 0.2f) * 0.3f);
                        // Schild mit Muenze
                        if (x >= 36 && x < 60 && y >= 20 && y < 38) c = new Color(0.95f, 0.88f, 0.7f);
                        if ((x - 48f) * (x - 48f) + (y - 29f) * (y - 29f) < 26f) c = new Color(0.95f, 0.75f, 0.2f);
                        if (c.a > 0f) c.a = 1f;
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                crateSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.02f), w);
                return crateSprite;
            }
        }
    }
}
