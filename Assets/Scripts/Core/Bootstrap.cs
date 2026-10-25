using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Startet das Spiel automatisch in jeder Szene (z. B. SampleScene): einfach Play druecken.
    /// Alles Weitere (Farm, UI, Licht, Sound) wird per Code aufgebaut.
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>
        /// true = bisheriges Raster-Farmspiel (Beete, Nachtkampf, UI). false = neue frei gezeichnete Welt.
        /// Die Spiellogik bleibt erhalten und wird spaeter auf die freie Welt umgezogen.
        /// </summary>
        public static bool LegacyFarm = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindObjectOfType<Game>() != null) return;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var root = new GameObject("SeedClicker");
            Object.DontDestroyOnLoad(root);

            if (!LegacyFarm)
            {
                // Neue, frei gezeichnete Welt (ohne Tile-Raster): Landschaft, Haus, Baeume faellen, Felder anlegen
                root.AddComponent<Sfx>();
                new GameObject("Particles").AddComponent<Particles>().transform.SetParent(root.transform);
                new GameObject("World").AddComponent<WorldBuilder>().transform.SetParent(root.transform);
                root.AddComponent<WorldCamera>();
                root.AddComponent<WorldUI>();
                root.AddComponent<WorldPostFX>();
                return;
            }

            // Reihenfolge wichtig: Game zuerst (laedt Balancing und Spielstand)
            root.AddComponent<Game>();
            root.AddComponent<Sfx>();
            new GameObject("Particles").AddComponent<Particles>().transform.SetParent(root.transform);
            new GameObject("Farm").AddComponent<FarmView>().transform.SetParent(root.transform);
            new GameObject("Battle").AddComponent<BattleView>().transform.SetParent(root.transform);
            new GameObject("LuckyBug").AddComponent<LuckyBug>().transform.SetParent(root.transform);
            root.AddComponent<CameraController>();
            root.AddComponent<GameUI>();
        }
    }
}
