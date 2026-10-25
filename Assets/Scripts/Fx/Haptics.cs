using UnityEngine;

namespace SeedClicker
{
    /// <summary>Leichte Vibration auf dem Handy.</summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static bool init, failed;
#endif

        public static void Tap(long ms = 12)
        {
            if (Game.I != null && !Game.I.S.vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!init)
                {
                    init = true;
                    using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (vibrator != null && !failed) { vibrator.Call("vibrate", ms); return; }
            }
            catch { failed = true; }
            if (ms >= 40) Handheld.Vibrate();
#elif UNITY_IOS && !UNITY_EDITOR
            if (ms >= 40) Handheld.Vibrate();
#endif
        }

        // Sorgt dafuer, dass Unity die VIBRATE-Berechtigung ins Android-Manifest schreibt.
        static void KeepPermission() { Handheld.Vibrate(); }
    }
}
