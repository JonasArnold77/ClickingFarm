using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Knollis bekommen ein Feld: Knolli antippen, dann ein Feld antippen – ab dann bearbeitet er nur dieses Feld
    /// (alle zusammenhaengenden Feldzellen, ein "Beet"). Solange er ausgewaehlt ist, leuchtet sein Beet gruen.
    /// Knollis ohne Feld warten an der Lieferkiste und fragen ab und zu, wo sie arbeiten sollen.
    /// </summary>
    public partial class WorldBuilder
    {
        Knolli selectedKnolli;
        float askT;
        readonly Dictionary<int, HashSet<Cell>> bedCache = new Dictionary<int, HashSet<Cell>>();
        readonly List<SpriteRenderer> bedPool = new List<SpriteRenderer>();
        float bedFlashT;
        HashSet<Cell> bedFlash;

        public bool KnolliSelected => selectedKnolli != null;

        KnolliJob Job(Knolli k)
        {
            while (save.knolliJobs.Count <= k.slot) save.knolliJobs.Add(new KnolliJob());
            return save.knolliJobs[k.slot];
        }

        /// <summary>Alle Feldzellen, die mit dieser zusammenhaengen (ohne Diagonalen).</summary>
        HashSet<Cell> Bed(Cell start)
        {
            var set = new HashSet<Cell>();
            if (!HasField(start)) return set;
            var queue = new Queue<Cell>();
            set.Add(start); queue.Enqueue(start);
            while (queue.Count > 0 && set.Count < 600)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { new Cell(c.x + 1, c.y), new Cell(c.x - 1, c.y), new Cell(c.x, c.y + 1), new Cell(c.x, c.y - 1) })
                    if (HasField(d) && set.Add(d)) queue.Enqueue(d);
            }
            return set;
        }

        /// <summary>Beet des Knollis (null = noch keinem Feld zugeordnet oder das Feld gibt es nicht mehr).</summary>
        HashSet<Cell> BedOf(Knolli k)
        {
            var j = Job(k);
            if (!j.set) return null;
            if (bedCache.TryGetValue(k.slot, out var b)) return b;
            b = Bed(new Cell(j.x, j.y));
            if (b.Count == 0) b = null;
            bedCache[k.slot] = b;
            return b;
        }

        bool InBed(Knolli k, PlantedState p) { var b = BedOf(k); return b != null && b.Contains(new Cell(p.x, p.y)); }

        // ------------------------------------------------------------------ Antippen und Zuordnen

        public bool HitKnolli(Vector2 w)
        {
            Knolli best = null; float bd = 0.32f * 0.32f;
            foreach (var k in knollis)
            {
                float d = (k.pos + new Vector2(0, 0.22f) - w).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            hitKnolli = best;
            return best != null;
        }
        Knolli hitKnolli;

        public void TapKnolli()
        {
            if (hitKnolli == null) return;
            selectedKnolli = hitKnolli == selectedKnolli ? null : hitKnolli;
            Sfx.Play("pop", 1.4f, 0.6f);
            Haptics.Tap(10);
            if (selectedKnolli != null)
            {
                selectedKnolli.idleT = 2f;   // stehen bleiben, solange man zuordnet
                Floater?.Invoke(selectedKnolli.pos + new Vector2(0, 0.8f), BedOf(selectedKnolli) != null ? "Mein Feld leuchtet – tippe ein anderes an" : "Tippe ein Feld an, das ich bearbeiten soll", new Color(0.8f, 1f, 0.7f));
            }
        }

        /// <summary>Tipp, waehrend ein Knolli ausgewaehlt ist: auf ein Feld = zuordnen, sonst abbrechen.</summary>
        public void AssignKnolli(Cell c)
        {
            var k = selectedKnolli;
            selectedKnolli = null;
            if (k == null) return;
            if (!HasField(c))
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Das ist kein Feld", new Color(1f, 0.7f, 0.55f));
                Sfx.Play("error", 1f, 0.4f);
                return;
            }
            var j = Job(k);
            j.set = true; j.x = c.x; j.y = c.y;
            bedCache.Remove(k.slot);
            if (k.state == 1) { k.goal = null; k.state = k.carry > 0 ? 2 : 0; k.target = CrateDrop; }
            k.idleT = 0f;
            bedFlash = Bed(c); bedFlashT = 1.2f;
            Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.7f), "Knolli arbeitet jetzt hier (" + bedFlash.Count + " Felder)", new Color(0.8f, 1f, 0.7f));
            Sfx.Play("unlock", 1.4f, 0.6f);
            Haptics.Tap(20);
            save.Save();
        }

        // ------------------------------------------------------------------ Anzeige

        /// <summary>Beet des ausgewaehlten Knollis gruen zeigen; unzugeordnete Knollis fragen ab und zu nach Arbeit.</summary>
        void UpdateKnolliJobs(float dt)
        {
            HashSet<Cell> show = null;
            if (selectedKnolli != null) show = BedOf(selectedKnolli);
            bool gold = false;
            if (show == null && IsTotem(OpenStructure)) { show = TotemBed(OpenStructure); gold = true; }
            if (show == null && bedFlashT > 0f) { bedFlashT -= dt; show = bedFlash; }
            int n = show != null ? show.Count : 0, i = 0;
            while (bedPool.Count < n)
            {
                var sr = new GameObject("KnolliBed").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Art.Get("white");
                sr.sortingOrder = -32287;
                bedPool.Add(sr);
            }
            float pulse = 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f);
            if (show != null)
                foreach (var c in show)
                {
                    var sr = bedPool[i++];
                    sr.enabled = true;
                    sr.transform.position = CellCenter(c);
                    float k = CellSize * 0.96f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
                    sr.transform.localScale = new Vector3(k, k, 1);
                    sr.color = gold ? new Color(1f, 0.85f, 0.35f, pulse) : new Color(0.4f, 1f, 0.45f, pulse);
                }
            for (; i < bedPool.Count; i++) bedPool[i].enabled = false;

            // ausgewaehlter Knolli huepft; wer kein Feld hat, fragt ab und zu
            askT -= dt;
            foreach (var k in knollis)
            {
                if (k == selectedKnolli) { k.body.transform.position += new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.08f, 0); continue; }
                if (askT <= 0f && BedOf(k) == null && k.carry == 0)
                    Floater?.Invoke(k.pos + new Vector2(0, 0.75f), "Wo soll ich arbeiten? Tipp mich an!", new Color(1f, 0.95f, 0.75f));
            }
            if (askT <= 0f) askT = 9f;
        }
    }
}
