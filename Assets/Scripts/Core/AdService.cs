using System;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Vorbereitete Werbe-Schnittstelle. Aktuell nur Platzhalter: die UI zeigt ein Fake-Video,
    /// danach gibt es die Belohnung. Spaeter hier das echte Werbe-SDK anbinden.
    /// </summary>
    public static class AdService
    {
        public const string Energy = "energy_refill";
        public const string OfflineDouble = "offline_double";
        public const string GrowAll = "grow_all";
        public const string FreeSeeds = "free_seeds";

        /// <summary>Wird von der UI gesetzt: zeigt das Platzhalter-Video und ruft danach den Callback.</summary>
        public static Action<string, Action> Presenter;

        public static bool RewardedAvailable(string placement) => true;

        public static void ShowRewarded(string placement, Action onReward)
        {
            Debug.Log("[Ads] Belohnungsvideo: " + placement);
            if (Presenter != null) Presenter(placement, onReward);
            else onReward?.Invoke();
        }

        /// <summary>Zwangswerbung: nur beim Tageswechsel, fruehestens nach einigen Minuten, mit Abstand.</summary>
        public static void TryInterstitial()
        {
            var g = Game.I; if (g == null) return;
            var b = g.B; var s = g.S;
            if (s.playTime < b.interstitialMinPlaySeconds) return;
            if (s.playTime - s.lastInterstitial < b.interstitialCooldownSeconds) return;
            s.lastInterstitial = s.playTime;
            Debug.Log("[Ads] Interstitial-Platz (Tageswechsel) – noch nicht angebunden.");
        }
    }
}
