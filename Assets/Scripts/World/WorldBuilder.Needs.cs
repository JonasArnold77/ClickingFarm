using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Bedingungen fuer Freischaltungen ("needs" in plants.json bei Pflanzen, Verbesserungen und Bauwerken):
    /// Dinge, die man auf dem Hof erreicht haben muss, z. B. "walls:6" (6 Mauerstuecke), "plant:tauschilf:2"
    /// (2 reife Tauschilfe), "medal:3" (eine Ernte-Pflanze auf Silber). So baut der Fortschritt auf dem auf,
    /// was man schon gemacht hat. Arten: fields, walls, stonewalls, gates, towers, pedestals, knollis, huts,
    /// totems, river, waves, medal, plant:ID, earned, delivered, kills, own:ID (Verbesserung bzw. Bauwerk gekauft).
    /// </summary>
    public partial class WorldBuilder
    {
        struct Need { public string kind, arg; public double n; }   // double: spaete Grossprojekte verlangen astronomische Mengen

        static Need ParseNeed(string s)
        {
            var p = s.Split(':');
            var n = new Need { kind = p[0].Trim() };
            if (n.kind == "own") { n.arg = p.Length > 1 ? p[1].Trim() : ""; n.n = 1; return n; }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (p.Length >= 3) { n.arg = p[1].Trim(); double.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out n.n); }
            else if (p.Length == 2) double.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out n.n);
            return n;
        }

        /// <summary>Aktueller Stand einer Bedingung.</summary>
        double NeedValue(Need n)
        {
            switch (n.kind)
            {
                case "fields": return FieldCount;
                case "earned": return save.earned;
                case "delivered": return save.deliveredTotal > 0 ? save.deliveredTotal : save.deliveredCount;
                case "kills": return save.killsTotal;
                case "walls": { int c = 0; foreach (var w in save.walls) if (!w.gate) c++; return c; }
                case "stonewalls": { int c = 0; foreach (var w in save.walls) if (w.stone) c++; return c; }
                case "gates": { int c = 0; foreach (var w in save.walls) if (w.gate) c++; return c; }
                case "pedestals": return save.pedestals.Count;
                case "knollis": return KnolliCount;
                case "river": return save.rivers.Count;
                case "waves": return save.wave;
                case "towers": return PlacedCount(s => s.id == "wachturm");
                case "huts": return PlacedCount(s => s.id == "knollihuette");
                case "totems": return PlacedCount(s => IsTotem(s));
                case "medal": { int best = 0; foreach (var p in save.planted) if (HasMedals(p) && p.Grown) best = Mathf.Max(best, p.Lv); return best; }
                case "plant": { int c = 0; foreach (var p in save.planted) if (p.type == n.arg && p.Grown) c++; return c; }
                case "own":
                    {
                        // "own:korb1" (ohne Zahl) heisst: einmal kaufen
                        if (HasUpgrade(n.arg)) return 1;
                        int c = 0; foreach (var st in save.structures) if (st.id == n.arg) c++;
                        return c;
                    }
            }
            return 0;
        }

        /// <summary>Muenzpreis einer Verbesserung bzw. eines Bauwerks (fuer "Spar auf ...").</summary>
        static double OwnPrice(string id)
        {
            var B = PlantBalance.I;
            var st = B.Structure(id); if (st != null) return st.cost;
            if (B.upgrades != null) foreach (var u in B.upgrades) if (u.id == id) return u.cost;
            return 0;
        }

        int PlacedCount(System.Predicate<StructureState> f)
        {
            int c = 0;
            foreach (var s in save.structures) if (s.placed && f(s)) c++;
            return c;
        }

        /// <summary>Lesbarer Name einer Bedingung ("6 Mauerstücke", "Eine Pflanze auf Silber", "2 reife Tauschilf").</summary>
        static string NeedName(Need n)
        {
            switch (n.kind)
            {
                case "fields": return "Felder";
                case "earned": return "verdiente Münzen";
                case "delivered": return "gelieferte Ernte";
                case "kills": return "besiegte Zombies";
                case "walls": return "Mauerstücke";
                case "stonewalls": return "Steinmauern";
                case "gates": return "Tore";
                case "pedestals": return "Steinpulte";
                case "knollis": return "Knollis";
                case "river": return "Bachstücke";
                case "waves": return "Nächte überstanden";
                case "towers": return "Wachtürme";
                case "huts": return "Knolli-Hütten";
                case "totems": return "Totems";
                case "medal": return "Ernte-Pflanze auf " + MedalName((int)n.n);
                case "plant": return "reife " + (PlantBalance.I.Plant(n.arg)?.name ?? n.arg);
                case "own": return UnlockName(n.arg) + " gekauft";
            }
            return n.kind;
        }

        /// <summary>Zaehler, die bei Ziel-Aufgaben erst ab Zielbeginn zaehlen (verdient, geliefert, besiegt, Naechte).
        /// Alles andere ist ein Zustand auf dem Hof (Felder, Mauern, reife Pflanzen, Medaille) und zaehlt, wie es gerade ist.</summary>
        static bool IsCounter(string kind) => kind == "earned" || kind == "delivered" || kind == "kills" || kind == "waves";

        public bool NeedsMet(string[] needs)
        {
            if (needs == null) return true;
            foreach (var s in needs) { if (string.IsNullOrEmpty(s)) continue; var n = ParseNeed(s); if (NeedValue(n) < n.n) return false; }
            return true;
        }

        /// <summary>Alle Bedingungen mit Stand, z. B. "Mauerstücke 3 / 6"; erfuellte gruen mit Haken (rich text) bzw. mit Haken.</summary>
        public List<(string text, bool ok)> NeedsList(string[] needs)
        {
            var l = new List<(string, bool)>();
            if (needs == null) return l;
            foreach (var s in needs)
            {
                if (string.IsNullOrEmpty(s)) continue;
                var n = ParseNeed(s);
                double v = NeedValue(n);
                bool ok = v >= n.n;
                l.Add((n.kind == "medal" ? NeedName(n) : NeedName(n) + " " + Num.Short(System.Math.Min(v, n.n)) + " / " + Num.Short(n.n), ok));
            }
            return l;
        }

        /// <summary>Jede Bedingung als Aufgabe ("Baue 6 Mauerstücke") mit Stand und Ziel.</summary>
        public List<(string task, double v, double n)> NeedsProgress(string[] needs)
        {
            var l = new List<(string, double, double)>();
            if (needs == null) return l;
            foreach (var s in needs)
            {
                if (string.IsNullOrEmpty(s)) continue;
                var n = ParseNeed(s);
                l.Add((NeedTask(n), n.kind == "medal" ? System.Math.Min(NeedValue(n), n.n) : NeedValue(n), n.n));
            }
            return l;
        }

        static string NeedTask(Need n)
        {
            string k = Num.Short(n.n); bool one = n.n == 1;
            switch (n.kind)
            {
                case "fields": return "Baue " + k + " Felder";
                case "earned": return "Verdiene " + k + " Münzen";
                case "delivered": return "Liefere " + k + " Ernte in die Kiste";
                case "kills": return "Besiege " + k + " Zombies";
                case "walls": return "Baue " + k + " Mauerstücke";
                case "stonewalls": return "Mach " + k + " Mauerstücke zu Stein";
                case "gates": return "Baue " + k + (one ? " Tor" : " Tore");
                case "pedestals": return "Stell " + k + (one ? " Steinpult" : " Steinpulte") + " auf";
                case "knollis": return "Heuere " + k + " Knollis an";
                case "river": return "Grab " + k + " Bachstücke";
                case "waves": return "Übersteh " + k + (one ? " Nacht" : " Nächte");
                case "towers": return "Stell " + k + (one ? " Wachturm" : " Wachtürme") + " auf";
                case "huts": return "Bau " + k + (one ? " Knolli-Hütte" : " Knolli-Hütten");
                case "totems": return "Stell " + k + (one ? " Totem" : " Totems") + " auf";
                case "medal": return "Bring eine Ernte-Pflanze auf " + MedalName((int)n.n);
                case "plant": return "Lass " + k + " " + (PlantBalance.I.Plant(n.arg)?.name ?? n.arg) + " reif werden";
                case "own": return "Spar auf " + UnlockName(n.arg) + " und kauf es (" + Num.Short(OwnPrice(n.arg)) + " Münzen)";
            }
            return NeedName(n);
        }

        /// <summary>Kurz fuer Beschreibungen: "6 Mauerstücke, 1 Wachturm".</summary>
        public static string NeedsShort(string[] needs)
        {
            if (needs == null || needs.Length == 0) return "";
            var parts = new List<string>();
            foreach (var s in needs)
            {
                if (string.IsNullOrEmpty(s)) continue;
                var n = ParseNeed(s);
                parts.Add(n.kind == "medal" ? NeedName(n) : Num.Short(n.n) + " " + NeedName(n));
            }
            return string.Join(", ", parts);
        }
    }
}
