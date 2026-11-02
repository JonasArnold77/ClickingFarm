using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Medaillen der Ernte-Pflanzen: Statt Zahlen-Stufen werden sie normal -> Bronze -> Silber -> Gold.
    /// Jeder Aufstieg braucht deutlich mehr Klicks als der vorige (plants.json -> harvestLevelTaps) und bringt
    /// einen grossen Ertragssprung (harvestLevelMult). Pflanze und jedes Erntestueck bekommen dann eine
    /// leuchtende Kontur in Medaillenfarbe (MedalAura).
    /// Ein reifes Tauschilf in Reichweite verdoppelt den Stufen-Fortschritt (helpers.waterLevelMult).
    /// </summary>
    public partial class WorldBuilder
    {
        static readonly Color BronzeCol = new Color(0.86f, 0.5f, 0.24f), SilverCol = new Color(0.84f, 0.88f, 0.95f), GoldCol = new Color(1f, 0.8f, 0.24f);

        public static bool HasMedals(PlantedState p) => p != null && p.Role == "harvest";

        public bool LevelMaxed(PlantedState p) => HasMedals(p) && p.Lv >= PlantBalance.I.HarvestMaxLevel;

        /// <summary>Klicks bis zur naechsten Stufe (Medaille) – bei Gold praktisch unendlich.</summary>
        public int LevelTaps(PlantedState p)
        {
            var B = PlantBalance.I;
            if (!HasMedals(p)) return B.LevelTapsFor(p.Lv);
            if (LevelMaxed(p)) return int.MaxValue / 4;
            return Mathf.Max(1, B.harvestLevelTaps[Mathf.Clamp(p.Lv - 1, 0, B.harvestLevelTaps.Length - 1)]);
        }

        /// <summary>Ertrags-Faktor der Stufe: Medaillen springen gross, alle anderen wachsen mit der Stufe.</summary>
        public float LevelMult(PlantedState p)
        {
            var m = PlantBalance.I.harvestLevelMult;
            if (!HasMedals(p) || m == null || m.Length == 0) return p.Lv;
            return m[Mathf.Clamp(p.Lv - 1, 0, m.Length - 1)];
        }

        public static string MedalName(int lv) => lv >= 4 ? "Gold" : lv == 3 ? "Silber" : lv == 2 ? "Bronze" : "Normal";
        public static Color MedalColor(int lv) => lv >= 4 ? GoldCol : lv == 3 ? SilverCol : BronzeCol;

        /// <summary>Steht ein reifes Tauschilf so nah, dass es dieses Feld betropft?</summary>
        bool InTauschilfReach(PlantedState p)
        {
            foreach (var q in save.planted)
                if (q != p && q.Grown && q.Role == "water" && InCross(new Cell(q.x, q.y), new Cell(p.x, p.y), WaterRadiusOf(q))) return true;
            return false;
        }

        /// <summary>Fortschritt fuer Ernte-Pflanzen am Tauschilf vervielfachen.</summary>
        int WaterLevelGain(PlantedState p, int gain) => HasMedals(p) && InTauschilfReach(p) ? Mathf.RoundToInt(gain * PlantBalance.I.Helpers.waterLevelMult) : gain;

        /// <summary>Stufen-Fortschritt gutschreiben (Klopfwurz, Tropfen); true, wenn die Pflanze dabei aufsteigt.</summary>
        bool GainLevelXp(PlantedState p, PlantView v, int gain, Vector2 basePos, Vector2 top)
        {
            if (LevelMaxed(p) || gain <= 0) return false;
            p.xp += WaterLevelGain(p, gain);
            if (p.xp < LevelTaps(p)) return false;
            LevelUp(p, v, basePos, top);
            return true;
        }

        /// <summary>Medaille fuer die Kontur (unter 2 = keine): reife Ernte-Pflanze und ihre Erntestuecke.</summary>
        static int MedalOf(PlantedState p) => HasMedals(p) && p.Grown ? p.Lv : 0;
    }
}
