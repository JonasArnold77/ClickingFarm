using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Tagesaufgabe: Jeden Tag (nach jeder Nacht) wird ein Erntegut ausgewaehlt, das man moeglichst oft in der
    /// Lieferkiste abliefern soll. Drei Stufen (plants.json -> challenge.tiers): Bronze = Muenzen, Silber = Runensteine,
    /// Gold = ein einzigartiger Gewinn, den es jeweils nur einmal gibt (Goldener Knolli, Erntehorn, Siebenmeilen-Stiefel,
    /// Magnet-Amulett, Gluecksklee, Erntekrone). Sind alle gewonnen, gibt es bei Gold einen grossen Muenzpreis.
    /// </summary>
    public partial class WorldBuilder
    {
        public event Action ChallengeChanged;
        public event Action<string, string> ChallengeReward;   // Titel, Text

        public static readonly (string id, string name, string desc)[] Perks =
        {
            ("goldknolli", "Goldener Knolli", "Ein zusätzlicher, goldener Knolli – flink wie der Wind"),
            ("horn", "Erntehorn", "Alle Ernte ist 25 % mehr wert"),
            ("stiefel", "Siebenmeilen-Stiefel", "Alle Knollis laufen 30 % schneller"),
            ("magnet", "Magnet-Amulett", "Knollis sammeln in 60 % größerem Umkreis"),
            ("klee", "Glücksklee", "Jeder Ernte-Tipp hat 15 % Chance auf doppelte Ernte"),
            ("krone", "Erntekrone", "Die Lieferkiste glänzt golden – und 3 Runensteine dazu"),
        };

        public bool HasPerk(string id) => save.perks.Contains(id);
        public string ChallengeType => save.chType;
        public int ChallengeCount => save.chCount;
        public int ChallengeTier => save.chTier;
        public int[] ChallengeTiers => PlantBalance.I.Challenge.tiers;

        /// <summary>Beim Laden: gibt es fuer heute schon eine Aufgabe?</summary>
        void EnsureChallenge()
        {
            if (string.IsNullOrEmpty(save.chType) || PlantBalance.I.Plant(save.chType) == null) NewChallenge();
        }

        /// <summary>Neuer Tag: ein Erntegut auswaehlen (am liebsten eins, das man gerade anbaut).</summary>
        void NewChallenge()
        {
            var pool = new List<string>();
            foreach (var p in save.planted) if (MakesProduct(p) && !pool.Contains(p.type)) pool.Add(p.type);
            if (pool.Count == 0)
                foreach (var c in PlantBalance.I.catalog) if (c.role == "harvest" && IsUnlocked(c.id)) pool.Add(c.id);
            if (pool.Count == 0) return;
            if (pool.Count > 1) pool.Remove(save.chType);   // nicht zweimal hintereinander dasselbe
            var r = new System.Random(save.mapSeed + save.nights * 7919);
            save.chType = pool[r.Next(pool.Count)];
            save.chCount = 0; save.chTier = 0;
            ChallengeChanged?.Invoke();
        }

        /// <summary>Abgelieferte Ernte zaehlen; beim Erreichen einer Stufe gibt es den Gewinn sofort.</summary>
        void CountDelivered(Dictionary<string, int> types)
        {
            if (types == null || string.IsNullOrEmpty(save.chType) || !types.TryGetValue(save.chType, out int n) || n <= 0) return;
            save.chCount += n;
            var tiers = ChallengeTiers;
            while (save.chTier < tiers.Length && save.chCount >= tiers[save.chTier]) { GiveReward(save.chTier); save.chTier++; }
            ChallengeChanged?.Invoke();
        }

        void GiveReward(int tier)
        {
            var at = cratePos + new Vector2(0, 1.2f);
            string prod = ProductName(PlantBalance.I.Plant(save.chType));
            switch (tier)
            {
                case 0:
                    {
                        double coins = 30 + 15 * save.wave;
                        AddCoins(coins, at);
                        ChallengeReward?.Invoke("Bronze: " + prod + "!", "+" + Num.Short(coins) + " Münzen");
                        break;
                    }
                case 1:
                    AddRunes(2, at);
                    ChallengeReward?.Invoke("Silber: " + prod + "!", "+2 Runensteine");
                    break;
                default:
                    {
                        // Gold: ein einzigartiger Gewinn, den man noch nicht hat
                        var free = new List<int>();
                        for (int i = 0; i < Perks.Length; i++) if (!HasPerk(Perks[i].id)) free.Add(i);
                        if (free.Count == 0)
                        {
                            double coins = 500 + 200 * save.wave;
                            AddCoins(coins, at);
                            ChallengeReward?.Invoke("Gold: " + prod + "!", "Alle Schätze gesammelt – +" + Num.Short(coins) + " Münzen");
                            break;
                        }
                        var pk = Perks[free[new System.Random(save.mapSeed + save.nights).Next(free.Count)]];
                        save.perks.Add(pk.id);
                        ApplyPerk(pk.id, true);
                        ChallengeReward?.Invoke("Gold! " + pk.name, pk.desc);
                        WorldFx.Play("firework", at, 0.18f);
                        break;
                    }
            }
            Particles.I?.Burst(at, "star", new Color(1f, 0.9f, 0.45f), 24, 3.5f, 0.9f, 0.3f, 1f, 1.5f);
            Sfx.Play("levelup", 1.1f, 0.9f);
            Haptics.Tap(40);
            save.Save();
        }

        /// <summary>Wirkung eines Gewinns einschalten (beim Gewinnen und beim Laden).</summary>
        void ApplyPerk(string id, bool fresh)
        {
            switch (id)
            {
                case "goldknolli": SpawnKnolli(fresh, true); break;
                case "krone":
                    if (crate != null) crate.color = new Color(1f, 0.88f, 0.5f);
                    if (fresh) AddRunes(3, cratePos + new Vector2(0, 1f));
                    break;
            }
        }
    }
}
