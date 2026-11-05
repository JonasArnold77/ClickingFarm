using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Ziehen sichtbar machen: Jede Pflanze sagt (Hover-Schild, Pflanzen-Fenster), wohin man sie ziehen kann
    /// (DragHint). Waehrend man eine Pflanze traegt, leuchten alle Orte gruen und durchsichtig, an denen man sie ablegen
    /// kann: freie Felder (umtopfen), gleiche reife Pflanzen (zusammenfuehren), Mauerabschnitte (Steinmoos, Dornenranke),
    /// Steinpulte (Wurzelherz), Wachtuerme (Steinmoos, Mondblume), die Waffenbank (Elemente) und – fuer die reife
    /// Sonnenlilie – die Nebel-Areale am Hof (die faerbt der Nebel selbst gruen, siehe Fog).
    /// </summary>
    public partial class WorldBuilder
    {
        static readonly Color DropTargetCol = new Color(0.35f, 1f, 0.45f, 0.3f);
        readonly List<SpriteRenderer> dropPool = new List<SpriteRenderer>();
        readonly List<Rect> dropRects = new List<Rect>();
        float dropCalcT;
        PlantedState dropFor;

        /// <summary>Wohin laesst sich diese Pflanze ziehen? Kurzer Text fuer Hover-Schild und Pflanzen-Fenster.</summary>
        public string DragHint(PlantedState p)
        {
            if (p == null) return "";
            var t = TransformOf(p);
            if (p.Grown && p.Role == "fogclear") return "Ziehen: auf ein Nebel-Feld am Hof";
            if (t == "stone") return "Ziehen: auf eine Mauer oder einen Wachturm";
            if (t == "thorns") return "Ziehen: auf eine Mauer";
            if (t == "heart") return "Ziehen: in ein Steinpult";
            if (t == "moon") return "Ziehen: auf einen Wachturm";
            if (t != null && IsWeaponElement(t)) return "Ziehen: auf die Waffenbank";
            if (p.Grown && AnyMergeTargetFor(p)) return "Ziehen: auf eine gleiche Pflanze oder ein freies Feld";
            return "Ziehen: auf ein freies Feld";
        }

        void UpdateDropTargets(float dt)
        {
            bool on = carrying != null && dragging;
            if (!on) { if (dropFor != null) { dropFor = null; dropRects.Clear(); } HideDrops(0); return; }
            dropCalcT -= dt;
            if (dropFor != carrying || dropCalcT <= 0f) { dropFor = carrying; dropCalcT = 0.3f; CollectDrops(carrying); }
            while (dropPool.Count < dropRects.Count)
            {
                var sr = new GameObject("DropTarget").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = Art.Get("white");
                sr.sortingOrder = 30900;   // ueber allem in der Welt, unter dem Nebel und der getragenen Pflanze
                dropPool.Add(sr);
            }
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < dropRects.Count; i++)
            {
                var r = dropRects[i]; var sr = dropPool[i];
                sr.enabled = true;
                sr.transform.position = r.center;
                var b = sr.sprite.bounds.size;
                sr.transform.localScale = new Vector3(r.width / Mathf.Max(0.001f, b.x), r.height / Mathf.Max(0.001f, b.y), 1f);
                sr.color = new Color(DropTargetCol.r, DropTargetCol.g, DropTargetCol.b, DropTargetCol.a * pulse);
            }
            HideDrops(dropRects.Count);
        }

        void HideDrops(int from) { for (int i = from; i < dropPool.Count; i++) if (dropPool[i].enabled) dropPool[i].enabled = false; }

        /// <summary>Alle Ablageorte fuer diese Pflanze (nur im Bild), als Welt-Rechtecke.</summary>
        void CollectDrops(PlantedState p)
        {
            dropRects.Clear();
            var t = TransformOf(p);
            float s = CellSize * 0.92f;
            Rect CellRect(Cell c, float lift = 0f) { var ctr = CellCenter(c) + new Vector2(0, lift); return new Rect(ctr.x - s / 2f, ctr.y - s / 2f, s, s); }
            Rect Bounds(SpriteRenderer sr) { var b = sr.bounds; return new Rect(b.min.x, b.min.y, b.size.x, b.size.y); }

            // Verwandlungen: nur ihre Ziele
            if (t != null)
            {
                if ((t == "stone" || t == "thorns"))
                    foreach (var kv in walls)
                    {
                        if (t == "stone" && MossJob(WallSegment(kv.Key)) == null) continue;
                        dropRects.Add(CellRect(kv.Key, WallH * 0.5f));
                    }
                if (t == "heart") foreach (var c in pedestals.Keys) dropRects.Add(CellRect(c, HasWall(c) ? WallH : 0f));
                if (t == "stone" || t == "moon")
                    foreach (var v in structViews) if (v.sr != null && v.st.placed && v.def.effect == "tower" && (t == "moon" ? !v.st.moon : !v.st.stone)) dropRects.Add(Bounds(v.sr));
                if (IsWeaponElement(t) && bench != null) dropRects.Add(Bounds(bench));
                return;
            }
            if (p.Grown && p.Role == "fogclear") return;   // die Nebel-Areale leuchten selbst gruen
            // gleiche reife Pflanzen (zusammenfuehren) und freie Plaetze (umtopfen) im Bild
            foreach (var q in save.planted) if (IsMergeTarget(q)) dropRects.Add(CellRect(new Cell(q.x, q.y)));
            foreach (var c in VisibleCells())
            {
                if (!IsSlot(c) || PlantAt(c) != null || !Fits(c, p.type, false)) continue;
                dropRects.Add(CellRect(c, HasWall(c) ? WallH : 0f));
            }
        }
    }
}
