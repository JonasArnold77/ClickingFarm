using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Baeume wachsen nach: Ein gefaellter Baum (mit oder ohne Stumpf) steht nach einigen Naechten wieder
    /// (plants.json -> treeRegrowNights, Standard 3). Gezaehlt wird jede Nacht, auch eine verlorene. Steht dort
    /// inzwischen etwas (Feld, Mauer, Pult, Bauwerk, Pflanze), wartet der Baum, bis der Platz frei ist.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly List<TreeObj> growing = new List<TreeObj>();

        int RegrowNights => Mathf.Max(1, PlantBalance.I.treeRegrowNights);

        /// <summary>Gefaellte Baeume aus alten Spielstaenden bekommen ab jetzt ihre Frist.</summary>
        void InitRegrow()
        {
            foreach (var id in save.felled)
                if (save.fellNights.Find(f => f.id == id) == null) save.fellNights.Add(new TreeFell { id = id, night = save.nights });
        }

        void RememberFell(TreeObj t)
        {
            save.fellNights.RemoveAll(f => f.id == t.id);
            save.fellNights.Add(new TreeFell { id = t.id, night = save.nights });
        }

        /// <summary>Wie viele Naechte noch, bis dieser Baum nachwaechst (fuer Hinweise).</summary>
        public int NightsUntilRegrow(int treeId)
        {
            var f = save.fellNights.Find(x => x.id == treeId);
            return f == null ? -1 : Mathf.Max(0, f.night + RegrowNights - save.nights);
        }

        /// <summary>Eine Nacht ist vorbei (gewonnen oder verloren): faellige Baeume wachsen nach.</summary>
        public void NightPassed()
        {
            save.nights++;
            int n = 0;
            for (int i = save.fellNights.Count - 1; i >= 0; i--)
            {
                var f = save.fellNights[i];
                if (save.nights - f.night < RegrowNights || f.id < 0 || f.id >= trees.Count) continue;
                var t = trees[f.id];
                if (TreeBlocked(t)) continue;   // spaeter nochmal versuchen
                Regrow(t, n++ * 0.12f);
                save.fellNights.RemoveAt(i);
            }
            if (n > 0) { RefreshGhosts(); Sfx.Play("grow", 0.9f, 0.8f); }
            save.Save();
        }

        /// <summary>Steht am Stammfuss inzwischen etwas, das der Baum nicht verdraengen soll?</summary>
        bool TreeBlocked(TreeObj t)
        {
            var c = CellAt(t.foot);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var d = new Cell(c.x + dx, c.y + dy);
                    if ((CellCenter(d) - t.foot).sqrMagnitude > 0.55f * 0.55f) continue;
                    if (HasField(d) || HasWall(d) || HasPedestal(d) || HasStructure(d) || PlantAt(d) != null) return true;
                }
            return false;
        }

        void Regrow(TreeObj t, float delay)
        {
            if (t.stump != null) { Destroy(t.stump.gameObject); t.stump = null; }
            if (t.sr != null) return;
            var sr = new GameObject("TreeRegrown").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(t.pivot, false);
            sr.sprite = t.sprite;
            sr.transform.localPosition = t.srLocalPos;
            sr.transform.localScale = Vector3.zero;
            sr.sortingOrder = t.srOrder;
            t.sr = sr;
            t.hp = t.maxHp; t.gone = false; t.fallT = -1f; t.shake = 0f;
            t.pivot.localRotation = Quaternion.identity;
            if (t.occ >= 0 && t.occ < occupiedR.Count) occupiedR[t.occ] = t.occR;   // Platz wieder belegt
            save.felled.Remove(t.id);
            save.stumpsGone.Remove(t.id);
            t.growT = -delay;   // gestaffelt: nicht alle auf einmal
            growing.Add(t);
        }

        /// <summary>Nachwachsen sichtbar machen: der Baum schiesst mit kleinem Ueberschwingen aus dem Boden.</summary>
        void UpdateRegrow(float dt)
        {
            for (int i = growing.Count - 1; i >= 0; i--)
            {
                var t = growing[i];
                if (t.sr == null) { growing.RemoveAt(i); continue; }
                bool started = t.growT >= 0f;
                t.growT += dt / 0.9f;
                if (!started && t.growT >= 0f)
                {
                    Particles.I?.Burst(t.foot + new Vector2(0, 0.2f), "leaf", new Color(0.45f, 0.75f, 0.3f), 14, 3f, 0.8f, 0.5f, 3f, 1.4f);
                    Particles.I?.Burst(t.foot, "square", new Color(0.45f, 0.32f, 0.2f), 8, 2.2f, 0.5f, 0.1f, 7f, 1.4f);
                }
                float k = Mathf.Clamp01(t.growT);
                float e = k <= 0f ? 0f : 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                t.sr.transform.localScale = new Vector3(t.srScale.x * e, t.srScale.y * e, 1);
                if (k >= 1f) { t.sr.transform.localScale = t.srScale; growing.RemoveAt(i); }
            }
        }
    }
}
