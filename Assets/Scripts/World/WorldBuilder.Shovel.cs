using System;

namespace SeedClicker
{
    /// <summary>
    /// Schaufel: nur mit diesem Werkzeug legt ein Tipp auf freien Boden Felder an (Anfang setzen, Vorschau ziehen,
    /// zweiter Tipp baut). Mit der Hand passiert dort nichts – so entstehen keine Felder aus Versehen.
    /// Schaufel, Mauer, Steinpult und Bach schliessen sich gegenseitig aus.
    /// </summary>
    public partial class WorldBuilder
    {
        bool shovelMode;
        public bool ShovelMode => shovelMode;
        public event Action<bool> ShovelModeChanged;

        public void SetShovelMode(bool on)
        {
            if (on && wallMode) SetWallMode(false);
            if (on && pedestalMode) SetPedestalMode(false);
            if (on && riverMode) SetRiverMode(false);
            CancelWallPlan();
            shovelMode = on;
            RefreshGhosts();
            ShovelModeChanged?.Invoke(on);
        }

        /// <summary>Anderer Bau-Modus wird eingeschaltet: Schaufel weglegen.</summary>
        void DropShovel()
        {
            if (!shovelMode) return;
            shovelMode = false;
            ShovelModeChanged?.Invoke(false);
        }
    }
}
