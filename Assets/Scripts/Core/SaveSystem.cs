using System;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>Speichert den Spielstand als JSON in den PlayerPrefs (funktioniert auf Android und WebGL).</summary>
    public static class SaveSystem
    {
        const string Key = "seedclicker_save_v1";

        public static void Save(GameState s)
        {
            try
            {
                s.lastSaveTicks = DateTime.UtcNow.Ticks;
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Speichern fehlgeschlagen: " + e.Message); }
        }

        public static GameState Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(Key)) return null;
                var s = JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(Key));
                if (s == null) return null;
                // Neue Werkzeuge in der Balancing-Datei: Array auffuellen
                if (s.toolTiers == null || s.toolTiers.Length < Balance.B.tools.Length)
                {
                    var t = new int[Balance.B.tools.Length];
                    if (s.toolTiers != null) Array.Copy(s.toolTiers, t, s.toolTiers.Length);
                    s.toolTiers = t;
                }
                if (s.construction == null) s.construction = new ConstructionState();
                return s;
            }
            catch (Exception e) { Debug.LogWarning("Laden fehlgeschlagen: " + e.Message); return null; }
        }

        public static void Delete() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }
}
