using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Ziele: Es gibt immer genau vier Aufgaben (plants.json -> milestones[i].tasks, Bedingungen wie bei needs).
    /// Sind alle vier geschafft, wird das Ziel erreicht und sein Paket frei (Pflanzen, Verbesserungen, Bauwerke),
    /// das das Spiel erweitert – dann kommen die naechsten vier Aufgaben. Was in einem Ziel steht, wird nur noch
    /// darueber frei (Muenz-Schwellen und needs gelten dafuer nicht mehr). Kampfpflanzen kommen weiter ueber die Naechte.
    /// </summary>
    public partial class WorldBuilder
    {
        public event Action<MilestoneDef, double, int> MilestoneReached;   // Ziel, Muenzen, Runensteine
        /// <summary>Gerade wird ein Ziel ausgezahlt (die einzelnen Freischalt-Banner bleiben dann still).</summary>
        public bool GrantingMilestone { get; private set; }

        Dictionary<string, int> milestoneOf;

        static MilestoneDef[] Milestones => PlantBalance.I.milestones ?? new MilestoneDef[0];
        public int MilestoneIndex => save.milestone;
        public int MilestoneTotal => Milestones.Length;
        /// <summary>Aktuelles Ziel – nach dem letzten festen Ziel ein Grossprojekt (endlos, immer gewaltigere Mengen).</summary>
        public MilestoneDef CurrentMilestone => save.milestone < Milestones.Length ? Milestones[save.milestone] : Project(save.milestone - Milestones.Length);
        /// <summary>Geschaffte Grossprojekte (jedes gibt dauerhaft mehr Ertrag).</summary>
        public int ProjectsDone => Mathf.Max(0, save.milestone - Milestones.Length);
        public float ProjectMult => 1f + PlantBalance.I.projects.bonusPerProject * ProjectsDone;

        MilestoneDef cachedProject; int cachedProjectIdx = -1;

        /// <summary>Grossprojekt j: astronomische Mengen an Ernte, besiegten Zombies und Muenzen, dazu eine riesige Anlage.</summary>
        MilestoneDef Project(int j)
        {
            if (cachedProjectIdx == j && cachedProject != null) return cachedProject;
            var P = PlantBalance.I.projects;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string R(double v) => Math.Round(v).ToString("0", inv);
            string[] build = { "fields:" + (60 + 12 * j), "towers:" + (3 + j), "stonewalls:" + (15 + 6 * j), "knollis:" + (8 + 2 * j) };
            var m = new MilestoneDef
            {
                project = true,
                name = "Großprojekt " + (j + 1) + ": " + P.names[j % P.names.Length],
                desc = "Dauerhaft +" + Mathf.RoundToInt(P.bonusPerProject * 100) + " % auf alle Ernte – für immer, Projekt für Projekt mehr.",
                tasks = new[] { "delivered:" + R(P.delivered * Math.Pow(P.deliveredGrowth, j)), "kills:" + R(P.kills * Math.Pow(P.killsGrowth, j)),
                                "earned:" + R(P.earned * Math.Pow(P.earnedGrowth, j)), build[j % build.Length] },
                unlocks = new string[0],
                coinShare = P.coinShare,
                runes = P.runes + j
            };
            cachedProject = m; cachedProjectIdx = j;
            return m;
        }

        /// <summary>In welchem Ziel steckt dieses Ding (-1 = in keinem, dann gelten die alten Schwellen)?</summary>
        public int MilestoneOf(string id)
        {
            if (milestoneOf == null)
            {
                milestoneOf = new Dictionary<string, int>();
                var ms = Milestones;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i].unlocks != null) foreach (var u in ms[i].unlocks) milestoneOf[u] = i;
            }
            return id != null && milestoneOf.TryGetValue(id, out int k) ? k : -1;
        }

        bool MilestoneDone(string id) { int k = MilestoneOf(id); return k >= 0 && k < save.milestone; }

        /// <summary>Verbesserung kaufbar? Im Ziel: sobald es erreicht ist; sonst Muenz-Schwelle und needs.</summary>
        public bool UpgradeAvailable(ShopUpgradeDef u) => u != null && (MilestoneOf(u.id) >= 0 ? MilestoneDone(u.id) : save.earned >= u.unlockEarned && NeedsMet(u.needs));
        public bool StructureAvailable(StructureDef d) => d != null && (MilestoneOf(d.id) >= 0 ? MilestoneDone(d.id) : save.earned >= d.unlockEarned && NeedsMet(d.needs));

        /// <summary>Lesbarer Name eines freigeschalteten Dings (Pflanze, Verbesserung oder Bauwerk).</summary>
        public static string UnlockName(string id)
        {
            var B = PlantBalance.I;
            var c = B.Plant(id); if (c != null) return c.name;
            var s = B.Structure(id); if (s != null) return s.name;
            if (B.upgrades != null) foreach (var u in B.upgrades) if (u.id == id) return u.name;
            return id;
        }

        /// <summary>Alle vier Aufgaben geschafft? Dann Ziel erreichen (auch mehrere hintereinander bei alten Spielstaenden).</summary>
        void CheckMilestones()
        {
            int guard = 0;
            while (guard++ < 50)
            {
                var m = CurrentMilestone;
                if (m == null || !NeedsMet(m.tasks)) return;
                save.milestone++;
                double coins = Math.Round(save.earned * m.coinShare);   // Belohnung: ein guter Batzen vom bisher Verdienten
                GrantingMilestone = true;
                if (m.unlocks != null)
                    foreach (var id in m.unlocks)
                    {
                        var c = PlantBalance.I.Plant(id);
                        if (c != null && !save.unlocked.Contains(id)) { save.unlocked.Add(id); PlantUnlocked?.Invoke(id); }
                    }
                GrantingMilestone = false;
                Sfx.Play("levelup", 1f, 1f); Sfx.Play("unlock", 1.2f, 0.9f);
                WorldPostFX.Punch(1f);
                Haptics.Tap(60);
                WorldFx.Play("firework", HousePos + new Vector2(0, 1.5f), 0.22f);
                if (coins > 0) AddCoins(coins, HousePos + new Vector2(0, 1.4f));
                if (m.runes > 0) AddRunes(m.runes, HousePos + new Vector2(0, 1.8f));
                MilestoneReached?.Invoke(m, coins, m.runes);
                save.Save();
            }
        }
    }
}
