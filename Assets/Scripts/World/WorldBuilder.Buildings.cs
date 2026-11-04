using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Gebaeude aus dem "City Assets Pack - Basic" (Bilder in Resources/Buildings), Deko aus "Pixel Crawler - Free Pack"
    /// (Farm-Requisiten, aus dem Blatt pc_farm.png geschnitten):
    ///  - Der Laden steht rechts neben dem Wohnhaus, davor Gemuesekisten. Klicks leveln ihn (jede Stufe: mehr Muenzen auf alles).
    ///  - Alle Gebaeude: Klick = leveln. Die Funktion (Laden-Fenster, Bauwerk-Fenster) oeffnet ein Knopf oben am Gebaeude,
    ///    der beim Darueberfahren erscheint (am Handy nach dem Antippen) – siehe BuildingUnder und WorldUI.BuildingButton.
    ///  - Knolli-Huetten (zwei Haeuschen im Wechsel) und die Saatgutmuehle bekommen Haus-Bilder statt der Platzhalter.
    ///  - Wachturm: Holzturm aus City-Teilen (Dach, Bretterkanzel, Beine), nach Steinmoos nur Steinbeine (Kanzel bleibt Holz).
    ///  - Gratis-Modus (FreeBuild, zum Ausprobieren): alle Bauwerke sichtbar und kostenlos.
    /// </summary>
    public partial class WorldBuilder
    {
        static Vector2 ShopPos => HousePos + new Vector2(4.4f, 0.75f);   // Fuss des Ladens (Property: HousePos steht in einer anderen Datei, Reihenfolge der statischen Felder ist dort nicht sicher)
        const float ShopWidth = 2.4f;

        public event Action ShopTapped;
        SpriteRenderer shopSr;
        float shopSquash;

        void PlaceShop()
        {
            var spr = BuildingArt.Get("laden");
            if (spr == null) return;
            Shadow(ShopPos + new Vector2(0.1f, 0.03f), ShopWidth * 1.05f, 0.4f, 0.3f);
            shopSr = new GameObject("Laden").AddComponent<SpriteRenderer>();
            shopSr.transform.SetParent(objects, false);
            shopSr.sprite = spr;
            float s = ShopWidth / Mathf.Max(0.01f, spr.bounds.size.x);
            shopSr.transform.localScale = new Vector3(s, s, 1f);
            shopSr.transform.position = ShopPos;
            shopSr.sortingOrder = Order(ShopPos.y);
            Reserve(ShopPos + new Vector2(0, 1.2f), 1.6f);
            Reserve(ShopPos + new Vector2(0, -0.5f), 0.9f);   // Platz vor der Tuer

            // Pixel-Crawler-Gemuesekisten vor dem Laden: Karotten, Kohl, Zwiebeln
            int[] crates = { 0, 2, 6 };
            for (int i = 0; i < crates.Length; i++)
            {
                var c = BuildingArt.FarmCrate(crates[i]);
                if (c == null) continue;
                var foot = ShopPos + new Vector2(-0.75f + i * 0.32f, -0.22f);
                var sr = new GameObject("LadenKiste").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = c;
                sr.transform.position = foot;
                sr.sortingOrder = Order(foot.y);
            }
            var scare = BuildingArt.Scarecrow;
            if (scare != null)
            {
                var foot = ShopPos + new Vector2(1.45f, -0.35f);
                var sr = new GameObject("Vogelscheuche").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = scare;
                sr.transform.position = foot;
                sr.sortingOrder = Order(foot.y);
                Shadow(foot, 0.5f, 0.12f);
                Reserve(foot, 0.35f);
            }
        }

        /// <summary>Liegt der Tipp auf dem Laden?</summary>
        public bool HitShop(Vector2 w)
        {
            if (shopSr == null) return false;
            var b = shopSr.bounds;
            return w.x > b.min.x + 0.1f && w.x < b.max.x - 0.1f && w.y > b.min.y && w.y < b.max.y - 0.2f;
        }

        public int ShopLv => Mathf.Max(1, save.shopLv);
        public float ShopXp => save.shopXp;
        public float ShopCoinMult => 1f + PlantBalance.I.shopLevel.coinBonusPerLevel * (ShopLv - 1);
        public bool ShopMaxed => ShopLv >= PlantBalance.I.StructureTaps.maxLevel;
        public int ShopTapsNeeded
        {
            get { var t = PlantBalance.I.StructureTaps; return PlantBalance.I.Clicks(t.tapsBase * Mathf.Pow(t.tapsGrowth, ShopLv - 1), "build"); }
        }

        /// <summary>Klick auf den Laden: leveln (wie Bauwerke). Das Laden-Fenster oeffnet der Knopf oben am Laden.</summary>
        public void TapShop()
        {
            shopSquash = 1f;
            var top = shopSr != null ? new Vector2(shopSr.bounds.center.x, shopSr.bounds.max.y - 0.3f) : ShopPos;
            if (ShopMaxed)
            {
                Sfx.Play("dry", 1.2f, 0.4f);
                Floater?.Invoke(top, "Höchste Stufe!", new Color(1f, 0.9f, 0.6f));
                ShopTapped?.Invoke();
                return;
            }
            save.shopXp += Mathf.Max(1f, Mathf.Round(UpgradeMult("click")));
            PayClick(top);
            Particles.I?.Burst(top, "spark", new Color(1f, 0.9f, 0.55f), 6, 2.4f, 0.3f, 0.1f, 4f, 1f);
            Sfx.Play("tick", UnityEngine.Random.Range(1.2f, 1.4f), 0.4f);
            Haptics.Tap(10);
            if (save.shopXp >= ShopTapsNeeded)
            {
                save.shopXp = 0f; save.shopLv = ShopLv + 1;
                Sfx.Play("levelup", 1.1f, 0.9f);
                WorldPostFX.Punch(0.5f);
                Shockwave(ShopPos, 2.2f, 0.5f, new Color(1f, 0.9f, 0.5f, 0.8f));
                Particles.I?.Burst(top, "star", new Color(1f, 0.9f, 0.5f), 24, 3f, 0.9f, 0.3f, 1f, 1.2f);
                Floater?.Invoke(top + new Vector2(0, 0.4f), "Laden Stufe " + ShopLv + "! +" + Mathf.RoundToInt((ShopCoinMult - 1f) * 100f) + " % Münzen", new Color(1f, 0.9f, 0.5f));
                save.Save();
            }
            ShopTapped?.Invoke();
        }

        /// <summary>Zum Ausprobieren: alle Bauwerke freigeschaltet und kostenlos (wird nicht gespeichert).</summary>
        public static bool FreeBuild;

        /// <summary>Wo Knopf und Fortschrittsbalken sitzen: im oberen Teil des Gebaeudes (nicht in der Mitte, wo man klickt).</summary>
        public Vector2 BuildingAnchor(bool shop, StructureState st)
        {
            SpriteRenderer sr = shop ? shopSr : structViews.Find(x => x.st == st)?.sr;
            if (sr == null) return BuildingTop(shop, st);
            var b = sr.bounds;
            return new Vector2(b.center.x, b.max.y - b.size.y * 0.24f);
        }

        /// <summary>Gebaeude unter dem Zeiger: Laden (shop = true) oder Bauwerk; top = Oberkante fuer den Knopf.</summary>
        public bool BuildingUnder(Vector2 w, out bool shop, out StructureState st, out Vector2 top)
        {
            shop = false; st = null; top = default;
            if (HitShop(w)) { shop = true; top = BuildingTop(true, null); return true; }
            st = HitStructure(w);
            if (st == null) return false;
            top = BuildingTop(false, st);
            return true;
        }

        public Vector2 BuildingTop(bool shop, StructureState st)
        {
            if (shop) return shopSr != null ? new Vector2(shopSr.bounds.center.x, shopSr.bounds.max.y) : ShopPos;
            var v = structViews.Find(x => x.st == st);
            return v != null ? new Vector2(v.sr.bounds.center.x, v.sr.bounds.max.y) : Vector2.zero;
        }

        /// <summary>Laden wippt nach dem Antippen kurz nach.</summary>
        void UpdateShop(float dt)
        {
            if (shopSr == null || shopSquash <= 0f) return;
            shopSquash = Mathf.Max(0f, shopSquash - dt * 3.5f);
            float s = ShopWidth / Mathf.Max(0.01f, shopSr.sprite.bounds.size.x);
            float k = Mathf.Sin(shopSquash * Mathf.PI * 2f) * 0.04f * shopSquash;
            shopSr.transform.localScale = new Vector3(s * (1f + k), s * (1f - k), 1f);
        }
    }

    /// <summary>Bilder aus Resources/Buildings: ganze Gebaeude (Fuss unten Mitte, 1 Einheit breit) und Pixel-Requisiten.</summary>
    public static class BuildingArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Texture2D farmSheet; static bool farmLooked;

        /// <summary>Gebaeudebild (null, wenn es keins gibt). Drehpunkt knapp ueber der Unterkante (dort liegt der Schatten).</summary>
        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Buildings/" + name);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.03f), tex.width) : null;
            cache[name] = s;
            return s;
        }

        /// <summary>Bild fuer ein Bauwerk aus dem City-Paket (null = gemalter Platzhalter bleibt).
        /// Knolli-Huetten wechseln je nach Platz zwischen zwei Haeuschen.</summary>
        public static Sprite ForStructure(StructureState s)
        {
            switch (s.id)
            {
                case "knollihuette": return Get(((s.x + s.y) & 1) == 0 ? "huette_a" : "huette_b");
                case "saatgutmuehle": return Get("muehle");
                case "wachturm": return Get(s.stone ? "turm_stein" : "turm_holz");   // Holzturm aus City-Teilen zusammengesetzt, nach Steinmoos mit Steinbeinen
            }
            return null;
        }

        // Pixel Crawler Farm.png: 400×400, 16-px-Raster. Kisten in Spalte 160, je 16×32 (Karotte, Rote Bete, Kohl, Salat,
        // Blumenkohl, Brokkoli, Zwiebel); Vogelscheuche bei (240, 32), 32×48. Pixel-Groesse: 48 Pixel = 1 Einheit.
        const float PixelPpu = 48f;

        static Texture2D FarmSheet
        {
            get
            {
                if (!farmLooked)
                {
                    farmLooked = true;
                    farmSheet = Resources.Load<Texture2D>("Buildings/pc_farm");
                    if (farmSheet != null) farmSheet.filterMode = FilterMode.Point;   // Pixel-Kanten scharf
                }
                return farmSheet;
            }
        }

        static Sprite Slice(string key, int x, int y, int w, int h)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var t = FarmSheet;
            float k = t != null ? t.width / 400f : 1f;   // Original 400×400; Unity kann es beim Import auf 512 skalieren
            s = t != null ? Sprite.Create(t, new Rect(x * k, t.height - (y + h) * k, w * k, h * k), new Vector2(0.5f, 0.05f), PixelPpu * k) : null;
            cache[key] = s;
            return s;
        }

        public static Sprite FarmCrate(int i) => Slice("crate" + i, 160, i * 32, 16, 32);
        public static Sprite Scarecrow => Slice("scarecrow", 240, 32, 32, 48);
    }
}
