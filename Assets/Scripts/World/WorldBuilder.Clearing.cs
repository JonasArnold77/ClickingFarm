using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Freier bauen: Im ruhigen Umfeld um den Hof (Hofflaeche der zweiten Ausbaustufe plus Rand) steht nichts im Weg –
    /// keine Baeume, Felsbrocken, Steine oder Baumstaemme; dort waechst auch nichts nach. Ausserhalb laesst sich jedes
    /// Hindernis durch oft Antippen entfernen: Baeume und Stuempfe (Axt), Felsbrocken und jetzt auch die losen Steine,
    /// Steinhaufen und Baumstaemme ("Kram"). Gebaeude-Plaetze sind erst belegt, wenn dort ein Gebaeude steht.
    /// </summary>
    public partial class WorldBuilder
    {
        class JunkObj { public int id, occ, hp; public Vector2 p; public SpriteRenderer sr, shadow; public float shake; public bool wood; }
        readonly List<JunkObj> junk = new List<JunkObj>();

        /// <summary>Ruhiges Umfeld um den Hof: hier wird nichts Blockierendes erzeugt.</summary>
        static int calmLevel = 2;   // Vorfuehr-Hof: der ganze Hof (Stufe 6) ist frei

        public static bool InCalmZone(Vector2 p)
        {
            var r = FarmRect(calmLevel);
            return p.x > r.xMin - 1f && p.x < r.xMax + 1f && p.y > r.yMin - 1f && p.y < r.yMax + 3f;
        }

        void AddJunk(SpriteRenderer sr, SpriteRenderer shadow, Vector2 p, int occ, int hp, bool wood)
        {
            junk.Add(new JunkObj { id = junk.Count, occ = occ, hp = hp, p = p, sr = sr, shadow = shadow, wood = wood });
        }

        /// <summary>Nach dem Laden: Umfeld des Hofs frei raeumen und bereits weggeklickten Kram entfernen.</summary>
        void ClearCalmZone()
        {
            foreach (var id in save.junkGone) if (id >= 0 && id < junk.Count) RemoveJunk(junk[id], false);
            foreach (var j in junk) if (j.sr != null && InCalmZone(j.p)) RemoveJunk(j, false);
            foreach (var r in rocks) if (r.sr != null && InCalmZone(r.p)) RemoveRock(r, false);
            foreach (var t in trees)
            {
                if (t.gone || !InCalmZone(t.foot)) continue;
                if (t.sr != null) Destroy(t.sr.gameObject);
                t.sr = null; t.hp = 0;
                RemoveStump(t, false);
            }
            save.fellNights.RemoveAll(f => f.id >= 0 && f.id < trees.Count && InCalmZone(trees[f.id].foot));   // nichts waechst nach
        }

        /// <summary>Tipp auf losen Kram (Steine, Steinhaufen, Baumstamm): nach ein paar Tipps ist er weg.</summary>
        public bool TryClearJunk(Vector2 w)
        {
            JunkObj best = null; int order = int.MinValue;
            foreach (var j in junk)
            {
                if (j.sr == null) continue;
                var b = j.sr.bounds; b.Expand(0.2f);
                if (!b.Contains(new Vector3(w.x, w.y, b.center.z))) continue;
                if (j.sr.sortingOrder > order) { order = j.sr.sortingOrder; best = j; }
            }
            if (best == null) return false;
            best.hp--;
            best.shake = 1f;
            var col = best.wood ? new Color(0.6f, 0.42f, 0.25f) : new Color(0.6f, 0.58f, 0.55f);
            Particles.I?.Burst(best.p + new Vector2(0, 0.2f), "square", col, 6, 2.4f, 0.45f, 0.08f, 9f, 2f);
            Sfx.Play("chop", UnityEngine.Random.Range(best.wood ? 0.9f : 1.3f, best.wood ? 1.05f : 1.5f), 0.8f);
            Haptics.Tap(12);
            Floater?.Invoke(best.p + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.6f), best.hp > 0 ? "noch " + best.hp : "weg!", new Color(1f, 0.95f, 0.8f));
            if (best.hp <= 0)
            {
                RemoveJunk(best, true);
                save.junkGone.Add(best.id);
                if (best.wood) AddWood(1, best.p + new Vector2(0, 0.5f));
                else AddCoins(1 + rnd.Next(3), best.p + new Vector2(0, 0.5f));
                save.Save();
                RefreshGhosts();
            }
            return true;
        }

        void RemoveJunk(JunkObj j, bool fx)
        {
            if (fx && j.sr != null)
            {
                Particles.I?.Burst(j.p + new Vector2(0, 0.2f), "square", j.wood ? new Color(0.55f, 0.38f, 0.22f) : new Color(0.55f, 0.52f, 0.5f), 14, 3.4f, 0.6f, 0.12f, 8f, 2f);
                WorldFx.Play("poof", j.p + new Vector2(0, 0.2f), 0.14f);
                Sfx.Play("break", j.wood ? 0.9f : 1.2f, 0.9f);
                Haptics.Tap(25);
            }
            if (j.sr != null) Destroy(j.sr.gameObject);
            if (j.shadow != null) Destroy(j.shadow.gameObject);
            j.sr = null; j.shadow = null;
            if (j.occ >= 0 && j.occ < occupiedR.Count) occupiedR[j.occ] = 0f;
        }

        void UpdateJunk(float dt)
        {
            foreach (var j in junk)
            {
                if (j.sr == null || j.shake <= 0f) continue;
                j.shake = Mathf.MoveTowards(j.shake, 0f, dt * 5f);
                j.sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 50f) * j.shake * 5f);
            }
        }
    }
}
