using System;
using System.Globalization;

namespace SeedClicker
{
    /// <summary>Lesbare Abkuerzung grosser Zahlen: 1,2K · 3,4M · 5,6B · 7,8T · 1,1aa …</summary>
    public static class Fmt
    {
        static readonly string[] suffix = { "", "K", "M", "B", "T" };
        // Kein CultureInfo("de-DE"): auf WebGL/IL2CPP fehlen Kulturdaten evtl.
        static readonly NumberFormatInfo de = MakeFormat();
        static NumberFormatInfo MakeFormat()
        {
            var nf = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nf.NumberDecimalSeparator = ",";
            nf.NumberGroupSeparator = ".";
            return nf;
        }

        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            bool neg = v < 0; v = Math.Abs(v);
            string s;
            if (v < 1000) s = v < 10 && v % 1 != 0 ? v.ToString("0.#", de) : Math.Floor(v).ToString("0", de);
            else
            {
                int tier = (int)Math.Floor(Math.Log10(v) / 3);
                double scaled = v / Math.Pow(1000, tier);
                string suf = tier < suffix.Length ? suffix[tier] : Letters(tier - suffix.Length);
                s = (scaled < 100 ? scaled.ToString("0.#", de) : scaled.ToString("0", de)) + suf;
            }
            return neg ? "-" + s : s;
        }

        // aa, ab, ... az, ba, ...
        static string Letters(int n) => ((char)('a' + n / 26 % 26)).ToString() + (char)('a' + n % 26);

        public static string Pct(double fraction) => Math.Round(fraction * 100).ToString("0", de) + " %";

        public static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours} Std. {t.Minutes} Min.";
            if (t.TotalMinutes >= 1) return $"{t.Minutes} Min.";
            return $"{t.Seconds} Sek.";
        }

        public static string Clock(float hour)
        {
            int h = (int)hour % 24, m = (int)((hour - (int)hour) * 60) / 10 * 10;
            return $"{h:00}:{m:00}";
        }
    }
}
