using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Vorfuehr-Hof: ein voll ausgebauter Endlevel-Hof zum Anschauen und Ausprobieren. Der eigene Spielstand wird
    /// vorher gesichert und laesst sich mit einem Knopf zurueckholen.
    /// Aufbau (Zellen, Haus oben): vier Kombo-Beete je Reihe in zwei Reihen (Weizen/Mais im Schachbrett = Freunde,
    /// Tauschilf, Kompostpilz, Trommelkraut in der Mitte, Totem auf jedem Beet, Bach unter jedem Beet), darunter
    /// Knolli-Huetten, dann vier Mauerreihen hintereinander (Stein, Dornen, versetzte Tore), Wachtuerme zwischen den
    /// Reihen, Steinpulte mit Kampfpflanzen auf den Mauern, seitliche Mauern bis zum Hof hoch.
    /// </summary>
    public partial class WorldBuilder
    {
        const string BackupKey = "seedclicker_world_backup";

        public static bool HasShowcaseBackup => PlayerPrefs.HasKey(BackupKey);

        /// <summary>Eigenen Stand sichern, Endlevel-Hof anlegen und die Welt neu aufbauen.</summary>
        public static void LoadShowcase()
        {
            var cur = WorldSave.Load();
            if (!HasShowcaseBackup && !cur.showcase) PlayerPrefs.SetString(BackupKey, JsonUtility.ToJson(cur));   // den echten Stand nur einmal sichern
            var s = BuildShowcaseSave(cur.mapSeed);
            s.Save();
            Bootstrap.Reload();
        }

        /// <summary>Gesicherten eigenen Spielstand zurueckholen.</summary>
        public static void RestoreShowcaseBackup()
        {
            if (!HasShowcaseBackup) return;
            var s = JsonUtility.FromJson<WorldSave>(PlayerPrefs.GetString(BackupKey));
            PlayerPrefs.DeleteKey(BackupKey);
            if (s != null) s.Save();
            Bootstrap.Reload();
        }

        static WorldSave BuildShowcaseSave(int seed)
        {
            var B = PlantBalance.I;
            var s = new WorldSave
            {
                showcase = true, plantsCleared = true, starterChosen = true, economyInit = true, knolliInit = true, runesInit = true,
                mapSeed = seed != 0 ? seed : 12345, farmLevel = MaxFarmLevel,
                coins = 5e8, earned = 2e9, wood = 999, runes = 60, wave = 12, nights = 14,
                milestone = (B.milestones != null ? B.milestones.Length : 0) + 2, knollis = 16,
                weapon = "granatwerfer", selectedPlant = "mais", elementsReset = true,
                deliveredTotal = 250000, killsTotal = 2500, deliveredCount = 250000
            };
            foreach (var c in B.catalog) s.unlocked.Add(c.id);
            foreach (var u in B.upgrades) s.upgrades.Add(u.id);
            foreach (var c in B.catalog) if (c.role == "harvest") s.storage.Add(new CropCount { id = c.id, n = 5000 });
            s.weaponLevels.Add(new WeaponLevel { id = "_bench", lv = 1, elements = new List<WeaponElementLv> { new WeaponElementLv { id = "fire", lv = 3 } } });
            s.weaponLevels.Add(new WeaponLevel { id = "granatwerfer", lv = 15 });

            var fieldSet = new HashSet<Cell>();
            void Plant(Cell c, string type, int level)
            {
                if (!fieldSet.Contains(c)) { fieldSet.Add(c); s.fields.Add(c); }
                var def = B.Plant(type);
                if (def == null) return;
                s.planted.Add(new PlantedState { x = c.x, y = c.y, type = type, growth = B.GrowTapsFor(type), level = level, w = 1, h = 1, dir = 2 });
            }

            // ---------------- Kombo-Beete: 7 breit, 5 hoch, Weizen/Mais im Schachbrett, Helfer innen
            string[,] bed =
            {
                { "W", "M", "W", "M", "W", "M", "W" },
                { "M", "T", "M", "K", "M", "T", "M" },
                { "W", "M", "W", "D", "W", "M", "W" },
                { "M", "T", "M", "K", "M", "T", "M" },
                { "W", "M", "W", "M", "W", "M", "W" },
            };
            int[] bedX = { -15, -7, 1, 9 };
            int[] bedTop = { 2, -5 };   // oberste Reihe der Beete
            string[] totems = { "totem_ernte", "totem_wachstum", "totem_fleiss", "totem_ernte" };
            int ti = 0;
            foreach (int top in bedTop)
                foreach (int x0 in bedX)
                {
                    for (int r = 0; r < 5; r++)
                        for (int q = 0; q < 7; q++)
                        {
                            var c = new Cell(x0 + q, top - r);
                            string k = bed[r, q];
                            string type = k == "W" ? "weizen" : k == "M" ? "mais" : k == "T" ? "tauschilf" : k == "K" ? "kompostpilz" : "trommelkraut";
                            int lv = k == "W" || k == "M" ? ((q + r) % 3 == 0 ? 4 : (q + r) % 3 == 1 ? 3 : 4) : 3;   // meist Gold, ein paar Silber
                            Plant(c, type, lv);
                        }
                    // Totem auf dem Beet, Bach unter dem Beet (fliesst um das Beet herum)
                    s.structures.Add(new StructureState { id = totems[ti++ % totems.Length], x = x0 + 3, y = top - 2, lv = 3, placed = true, anchored = true, bx = x0, by = top });
                    for (int q = -1; q <= 7; q++)
                        if (!s.rivers.Exists(v => v.x == x0 + q && v.y == top - 5)) s.rivers.Add(new RiverState { x = x0 + q, y = top - 5, dir = 1, seq = s.rivers.Count });
                }

            // ---------------- Knolli-Huetten zwischen Beeten und Mauern
            foreach (int x in new[] { -14, -10, -6, -2, 2, 6, 10, 14 })
                s.structures.Add(new StructureState { id = "knollihuette", x = x, y = -12, lv = 2, placed = true });

            // ---------------- vier Mauerreihen hintereinander, versetzte Tore, Steinmauern und Dornen
            int[] wallRows = { -14, -16, -18, -20 };
            for (int i = 0; i < wallRows.Length; i++)
            {
                int y = wallRows[i];
                for (int x = -17; x <= 16; x++)
                {
                    bool gate = (i % 2 == 0 && (x == -9 || x == 8)) || (i % 2 == 1 && (x == -1 || x == 0));
                    s.walls.Add(new WallState { x = x, y = y, hp = 9999, stone = i < 3, thorns = i == 0 || i == 3 ? 3 : 0, gate = gate });
                }
            }
            // seitliche Mauern bis zum Hof hoch
            foreach (int x in new[] { -17, 16 })
                for (int y = -13; y <= 3; y++) s.walls.Add(new WallState { x = x, y = y, hp = 9999, stone = true });

            // Wachtuerme zwischen den Mauerreihen
            foreach (int y in new[] { -15, -17, -19 })
                foreach (int x in new[] { -13, -5, 4, 12 })
                    s.structures.Add(new StructureState { id = "wachturm", x = x, y = y, lv = 4, placed = true, stone = true });

            // Steinpulte mit Kampfpflanzen auf der vordersten und der zweiten Mauerreihe
            string[] combat = { "funkenmais", "glutweizen", "giftruebe", "blitzmais", "sumpfruebe", "aschenweizen" };
            int ci = 0;
            foreach (int y in new[] { -20, -16 })
                for (int x = -15; x <= 14; x += 3)
                {
                    var c = new Cell(x, y);
                    var w = s.walls.Find(v => v.x == x && v.y == y);
                    if (w == null || w.gate) continue;
                    s.pedestals.Add(new PedestalState { x = x, y = y, heart = 2 });
                    s.planted.Add(new PlantedState { x = x, y = y, type = combat[ci % combat.Length], growth = B.GrowTapsFor(combat[ci++ % combat.Length]), level = 6, w = 1, h = 1 });
                }

            // Knollis: Traeger frei, dazu Holzfaeller, Maurer, Wachen, Giesser
            string[] roles = { "carrier", "carrier", "carrier", "carrier", "carrier", "carrier", "carrier", "carrier", "wood", "wood", "mason", "mason", "guard", "guard", "water", "water" };
            for (int i = 0; i < roles.Length; i++)
            {
                var j = new KnolliJob { role = roles[i] };
                if (roles[i] == "water") { j.set = true; j.x = bedX[i % bedX.Length] + 2; j.y = bedTop[0]; }
                s.knolliJobs.Add(j);
            }
            s.msBaseFor = -1;
            return s;
        }
    }
}
