using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Bauflaeche: das Startbeet links vor dem Haus und je drei Reihen neben den Strassen (WorldBuilder.Lanes).
    /// Freie Bau-Zellen schimmern ganz leicht als Feld durch; ein Tipp legt das Feld an.
    /// </summary>
    public partial class WorldBuilder
    {

        /// <summary>Innenflaeche des Startbeets (Welteinheiten). Felder gehen anfangs nur hier.</summary>
        public static readonly Rect StartArea = new Rect(-4.5f, -0.9f, 3.6f, 2.7f);


        void LoadDefense()
        {
            RefreshGhosts();
        }

        readonly Dictionary<Cell, SpriteRenderer> ghosts = new Dictionary<Cell, SpriteRenderer>();

        /// <summary>Ueberall, wo ein Feld angelegt werden kann, schimmert es ganz leicht durch.</summary>
        void RefreshGhosts()
        {
            if (fieldSprite == null) fieldSprite = GroundMaterials.TileSprite(4, 3);
            if (fieldSprite == null) return;
            foreach (var c in buildable)
            {
                bool show = CanPlaceField(c);
                ghosts.TryGetValue(c, out var sr);
                if (show && sr == null)
                {
                    sr = new GameObject("FieldGhost").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    sr.transform.position = CellCenter(c);
                    sr.transform.localScale = Vector3.one * CellSize * 0.94f;
                    sr.sprite = fieldSprite;
                    sr.color = new Color(1f, 1f, 1f, 0.2f);
                    sr.sortingOrder = -32305;
                    ghosts[c] = sr;
                }
                if (sr != null) sr.enabled = show;
            }
        }
    }
}
