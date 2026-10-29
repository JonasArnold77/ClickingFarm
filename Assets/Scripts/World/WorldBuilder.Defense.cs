using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Das Startbeet: ein Bereich links vor dem Haus, in dem anfangs Felder angelegt werden duerfen.
    /// Es gibt keine Zaeune mehr; die freien Beet-Zellen sind dezent als Raster markiert.
    /// </summary>
    public partial class WorldBuilder
    {
        float noWoodT;
        readonly Dictionary<Cell, SpriteRenderer> plotMarks = new Dictionary<Cell, SpriteRenderer>();

        /// <summary>Innenflaeche des Startbeets (Welteinheiten). Felder gehen anfangs nur hier.</summary>
        public static readonly Rect StartArea = new Rect(-4.5f, -0.9f, 3.6f, 2.7f);

        /// <summary>Darf an dieser Stelle ein Feld liegen? (Spaeter: weitere Bereiche freischalten.)</summary>
        public static bool InFarmArea(Vector2 p) => StartArea.Contains(p);

        void LoadDefense()
        {
            BuildPlotMarks();
        }

        /// <summary>Jede Zelle des Startbeets bekommt ein schwaches Rasterkaestchen, damit man sieht, wo Felder hingehen.</summary>
        void BuildPlotMarks()
        {
            var sprite = art.Get("hh_slot");
            if (sprite == null) return;
            var a = CellAt(StartArea.min + new Vector2(0.01f, 0.01f));
            var b = CellAt(StartArea.max - new Vector2(0.01f, 0.01f));
            for (int x = a.x; x <= b.x; x++)
                for (int y = a.y; y <= b.y; y++)
                {
                    var c = new Cell(x, y);
                    if (!InFarmArea(CellCenter(c))) continue;
                    var sr = new GameObject("Plot").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    sr.transform.position = CellCenter(c);
                    sr.transform.localScale = Vector3.one * (CellSize * 0.92f / sprite.bounds.size.x);
                    sr.sprite = sprite;
                    sr.color = new Color(1f, 0.95f, 0.8f, 0.28f);
                    sr.sortingOrder = -32302;
                    plotMarks[c] = sr;
                }
        }
    }
}
