using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>Einfache Lokalisierung: Resources/Lang/&lt;sprache&gt;.txt mit Zeilen "schluessel=Text".</summary>
    public static class Loc
    {
        static readonly Dictionary<string, string> table = new Dictionary<string, string>();
        public static string Language { get; private set; } = "de";

        public static void Load(string lang = "de")
        {
            Language = lang;
            table.Clear();
            var ta = Resources.Load<TextAsset>("Lang/" + lang);
            if (ta == null) { Debug.LogWarning("Sprachdatei fehlt: " + lang); return; }
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                table[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Replace("\\n", "\n");
            }
        }

        public static string T(string key) => table.TryGetValue(key, out var v) ? v : key;
        public static string T(string key, params object[] args)
        {
            var f = T(key);
            try { return string.Format(f, args); } catch { return f; }
        }
        public static bool Has(string key) => table.ContainsKey(key);
    }
}
