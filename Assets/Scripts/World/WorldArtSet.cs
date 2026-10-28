using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Liste der Grafiken fuer die freie Welt (Skyden "Cozy Farm" + ausgewaehlte Happy-Harvest-Props).
    /// Liegt als Resources/WorldArt.asset vor; Sprites werden per Namen abgefragt.
    /// Neue Grafik: im Inspector einen Eintrag (Name + Sprite) hinzufuegen.
    /// </summary>
    [CreateAssetMenu(menuName = "Seed Clicker/World Art Set")]
    public class WorldArtSet : ScriptableObject
    {
        [Serializable] public class Entry { public string name; public Sprite sprite; }

        [Tooltip("Boden-Atlas (256 px pro Kachel, 8x8 Kacheln)")]
        public Texture2D groundAtlas;
        [Tooltip("Klippen-Atlas (Skyden Mountain Tiles, 256 px pro Kachel)")]
        public Texture2D mountainAtlas;
        public List<Entry> sprites = new List<Entry>();
        [Tooltip("Geriggte Figur (Happy Harvest Prefab_character_base), wird fuer die Zombies benutzt")]
        public GameObject zombiePrefab;
        [Tooltip("Zombie-Laufanimation (Assets/Characters): 8 Richtungen x 8 Bilder, Reihenfolge S, SO, O, NO, N, NW, W, SW")]
        public Sprite[] zombieWalk;
        [Tooltip("Zombie im Stand, je Richtung ein Bild (gleiche Reihenfolge)")]
        public Sprite[] zombieIdle;

        public bool HasZombieWalk
        {
            get
            {
                if (zombieWalk == null || zombieWalk.Length < 64) return false;
                foreach (var s in zombieWalk) if (s == null) return false;
                return true;
            }
        }

        static WorldArtSet inst;
        Dictionary<string, Sprite> lookup;

        public static WorldArtSet I
        {
            get
            {
                if (inst == null) inst = Resources.Load<WorldArtSet>("WorldArt");
                return inst;
            }
        }

        public Sprite Get(string name)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>();
                foreach (var e in sprites) if (e.sprite != null && !string.IsNullOrEmpty(e.name)) lookup[e.name] = e.sprite;
            }
            if (lookup.TryGetValue(name, out var s)) return s;
            Debug.LogWarning("WorldArt: Sprite fehlt: " + name);
            return null;
        }
    }
}
