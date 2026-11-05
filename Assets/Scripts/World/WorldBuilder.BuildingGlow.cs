using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Gebaeude-Stufen sichtbar machen: Ab einer bestimmten Stufe bekommt ein Gebaeude (Laden und alle Bauwerke ausser
    /// Totems) Metall-Elemente – das Schild wird zur Bronze-, Silber- bzw. Goldplatte mit Nieten, auf dem Dach sitzt
    /// eine Zier (Kugel, Kugel mit Spitze, Stern). Die Bilder dafuer sind fertig gemalt (name_t1/_t2/_t3.png, erzeugt mit
    /// Tools/Buildings/metals.py); hier wird nur das passende Bild gesetzt und der Wechsel kurz gefeiert.
    /// Schwellen in plants.json -> buildingGlow.levels.
    /// </summary>
    public partial class WorldBuilder
    {
        /// <summary>0 = Holz, 1 Bronze, 2 Silber, 3 Gold.</summary>
        public static int BuildingTier(int lv)
        {
            var l = PlantBalance.I.buildingGlow?.levels;
            if (l == null) return 0;
            int t = 0;
            for (int i = 0; i < l.Length; i++) if (lv >= l[i]) t = i + 1;
            return t;
        }

        void UpdateBuildingGlow(float dt)
        {
            if (shopSr != null)
            {
                int tier = BuildingTier(ShopLv);
                var want = BuildingArt.Tiered("laden", tier);
                if (want != null && shopSr.sprite != want) { shopSr.sprite = want; TierUpFx(shopSr, tier); }
            }
            foreach (var v in structViews)
            {
                if (v.sr == null || IsTotem(v.def)) continue;
                var want = StructSprite(v.st);
                if (want == null || v.sr.sprite == want) continue;
                v.sr.sprite = want;
                if (v.st.placed) TierUpFx(v.sr, BuildingTier(v.st.lv));
            }
        }

        /// <summary>Neue Metallstufe: Funkenregen und Lichtring in der Stufenfarbe ueber dem Dach.</summary>
        void TierUpFx(SpriteRenderer sr, int tier)
        {
            if (tier <= 0 || Time.timeSinceLevelLoad < 2f) return;   // beim Laden nicht feiern
            var col = MedalColor(tier + 1);   // 2 Bronze, 3 Silber, 4 Gold
            var top = new Vector2(sr.bounds.center.x, sr.bounds.max.y - sr.bounds.size.y * 0.15f);
            Shockwave(top, 1.8f, 0.5f, new Color(col.r, col.g, col.b, 0.85f));
            Particles.I?.Burst(top, "star", col, 26, 3.2f, 0.9f, 0.3f, -0.6f, 1.2f);
            Sfx.Play("unlock", 1.1f + 0.1f * tier, 0.8f);
        }
    }
}
