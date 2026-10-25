using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Startet das Spiel automatisch in jeder Szene (z. B. SampleScene): einfach Play druecken.
    /// Alles Weitere (Farm, UI, Licht, Sound) wird per Code aufgebaut.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindObjectOfType<Game>() != null) return;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var root = new GameObject("SeedClicker");
            Object.DontDestroyOnLoad(root);
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
