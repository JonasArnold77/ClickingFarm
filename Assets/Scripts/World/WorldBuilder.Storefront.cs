using System;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Das Haus als Hofladen: gestreifte Markise ueber der ganzen Front, ein Holzschild "Laden" mit Muenzen darauf,
    /// vor den Fenstern Kisten voller Gemuese und ein Staender mit Samentueten. Alles im Code gemalt bzw. aus den
    /// vorhandenen Happy-Harvest-Bildern, liegt vor der Hausfront (eigene Sortierung knapp ueber dem Haus).
    /// Lage relativ zu HousePos, ausgemessen am Haus-Bild (Front unten +1,03, Dachkante +2,32, Fenster x ±1,24).
    /// </summary>
    public partial class WorldBuilder
    {
        const float FrontY = 1.03f, EaveY = 2.32f;

        /// <summary>Laden angetippt (die Oberflaeche oeffnet das Laden-Fenster).</summary>
        public event Action ShopTapped;
        SpriteRenderer shopAwning;

        /// <summary>Liegt der Tipp auf der Ladenfront (Markise, Schild, Kisten)?</summary>
        public bool HitShop(Vector2 w)
        {
            var d = w - HousePos;
            return Mathf.Abs(d.x) < 2.1f && d.y > FrontY - 0.25f && d.y < EaveY + 0.5f;
        }

        public void TapShop()
        {
            Sfx.Play("bell", 1.3f, 0.6f);
            Haptics.Tap(12);
            if (shopAwning != null) StartCoroutine(AwningBounce());
            ShopTapped?.Invoke();
        }

        System.Collections.IEnumerator AwningBounce()
        {
            var t = shopAwning.transform;
            var baseScale = t.localScale;
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime * 4f)
            {
                float e = 1f + Mathf.Sin(k * Mathf.PI) * 0.06f * (1f - k);
                t.localScale = new Vector3(baseScale.x * e, baseScale.y * (2f - e), 1f);
                yield return null;
            }
            t.localScale = baseScale;
        }

        void PlaceStorefront(SpriteRenderer house)
        {
            int baseOrder = house != null ? house.sortingOrder : Order(HousePos.y);

            // Markise: von der Dachkante bis knapp ueber die Fenstermitte
            shopAwning = Deco("ShopAwning", AwningSprite, HousePos + new Vector2(0f, EaveY + 0.04f), 4.15f, baseOrder + 2, pivotTop: true);

            // Schild auf der Markise
            var signPos = HousePos + new Vector2(0f, EaveY + 0.22f);
            Deco("ShopSign", SignSprite, signPos, 1.7f, baseOrder + 3, pivotTop: false, centered: true);
            var label = MakeLabel("ShopLabel", null);
            label.text = "LADEN";
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.05f;
            label.color = new Color(1f, 0.93f, 0.7f);
            label.transform.position = signPos + new Vector2(0f, -0.01f);
            label.GetComponent<MeshRenderer>().sortingOrder = baseOrder + 5;
            var shade = MakeLabel("ShopLabelShadow", null);
            shade.text = "LADEN"; shade.anchor = TextAnchor.MiddleCenter; shade.characterSize = 0.05f;
            shade.color = new Color(0.2f, 0.11f, 0.05f, 0.9f);
            shade.transform.position = signPos + new Vector2(0.025f, -0.035f);
            shade.GetComponent<MeshRenderer>().sortingOrder = baseOrder + 4;
            foreach (float dx in new[] { -0.68f, 0.68f })
            {
                var coin = PlaceSized("hh_coin", signPos + new Vector2(dx, -0.16f), 0.3f);
                if (coin != null) coin.sortingOrder = baseOrder + 5;
            }

            // Gemuesekisten vor den Fenstern, Samentueten daneben
            string[][] fill = { new[] { "hh_carrot_icon", "hh_carrot_icon", "hh_carrot_icon", "hh_carrot_icon", "hh_carrot_icon" }, new[] { "hh_corn_icon", "hh_wheat_icon", "hh_corn_icon", "hh_wheat_icon", "hh_corn_icon" } };
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -1.3f : 1.3f;
                var foot = HousePos + new Vector2(x, FrontY - 0.12f);
                Deco("ShopCrate", ShopCrateSprite, foot, 1.05f, baseOrder + 7, pivotTop: false);
                // zwei Reihen: hinten drei hoeher, vorn zwei dazwischen – sieht nach vollem Haufen aus
                for (int i = 0; i < 5; i++)
                {
                    bool back = i < 3;
                    var at = back ? new Vector2(-0.3f + i * 0.3f, 0.4f + (i == 1 ? 0.06f : 0f)) : new Vector2(-0.15f + (i - 3) * 0.3f, 0.33f);
                    var veg = PlaceSized(fill[side][i], foot + at, back ? 0.34f : 0.36f, i % 2 == 0);
                    if (veg != null) veg.sortingOrder = baseOrder + (back ? 5 : 6);
                }
                var bag = PlaceSized(side == 0 ? "hh_carrot_seedbag" : "hh_wheat_seedbag", foot + new Vector2(side == 0 ? -0.72f : 0.72f, -0.04f), 0.42f);
                if (bag != null) bag.sortingOrder = baseOrder + 8;
            }
            Shadow(HousePos + new Vector2(-1.3f, FrontY - 0.12f), 1.2f, 0.2f);
            Shadow(HousePos + new Vector2(1.3f, FrontY - 0.12f), 1.2f, 0.2f);
        }

        /// <summary>Gemaltes Bild vor das Haus legen. width in Welt-Einheiten; pivotTop: Position ist die Oberkante.</summary>
        SpriteRenderer Deco(string name, Sprite spr, Vector2 pos, float width, int order, bool pivotTop, bool centered = false)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = spr;
            float s = width / Mathf.Max(0.01f, spr.bounds.size.x);
            sr.transform.localScale = new Vector3(s, s, 1f);
            float h = spr.bounds.size.y * s;
            // alle drei Bilder haben den Drehpunkt unten in der Mitte
            sr.transform.position = pivotTop ? pos - new Vector2(0, h) : centered ? pos - new Vector2(0, h * 0.5f) : pos;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ Bilder

        static Sprite awningSprite, signSprite, shopCrateSprite;
        static readonly Color ShopInk = new Color(0.16f, 0.1f, 0.07f);

        /// <summary>Markise: rot-cremefarbene Streifen, oben im Schatten, unten Bogenkante mit dunkler Linie.</summary>
        static Sprite AwningSprite
        {
            get
            {
                if (awningSprite != null) return awningSprite;
                const int w = 320, h = 64, stripes = 10;
                const float body = 44f, scallop = 14f;   // Stoffbahn und Boegen darunter (in Pixeln)
                var red = new Color(0.8f, 0.24f, 0.2f); var cream = new Color(0.98f, 0.92f, 0.78f);
                var px = new Color[w * h];
                float sw = w / (float)stripes;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fy = h - 1 - y;   // von oben gezaehlt
                        int si = Mathf.Min(stripes - 1, (int)(x / sw));
                        float cx = (si + 0.5f) * sw, dx = (x - cx) / (sw * 0.5f);
                        // Unterkante: je Streifen ein Bogen
                        float edge = body + scallop * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx));
                        if (fy > edge + 2.5f) continue;
                        var c = si % 2 == 0 ? red : cream;
                        // gewoelbt: oben dunkler (unter dem Dach), Mitte hell, an den Streifenraendern leichte Falte
                        float shade = Mathf.Lerp(0.62f, 1.05f, Mathf.Clamp01(fy / (body * 0.7f))) * (1f - 0.12f * dx * dx);
                        c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                        if (fy > edge) c = ShopInk;                                  // Linie an der Bogenkante
                        if (fy < 3f) c = ShopInk * 1.3f;                             // obere Kante (Stange)
                        if (fy > body - 1.5f && fy < body + 1f && fy <= edge) c = Color.Lerp(c, ShopInk, 0.35f);   // Saum
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                return awningSprite = ShopSprite(px, w, h);
            }
        }

        /// <summary>Holzschild mit Rahmen und Maserung.</summary>
        static Sprite SignSprite
        {
            get
            {
                if (signSprite != null) return signSprite;
                const int w = 180, h = 50;
                var wood = new Color(0.5f, 0.31f, 0.17f); var light = new Color(0.66f, 0.44f, 0.24f);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        // abgerundete Ecken
                        float ex = Mathf.Max(0, Mathf.Max(8 - x, x - (w - 9))), ey = Mathf.Max(0, Mathf.Max(8 - y, y - (h - 9)));
                        if (ex * ex + ey * ey > 64f) continue;
                        bool frame = x < 5 || x >= w - 5 || y < 5 || y >= h - 5 || ex * ex + ey * ey > 30f;
                        float grain = Mathf.PerlinNoise(x * 0.04f, y * 0.35f);
                        var c = frame ? ShopInk * 1.6f : Color.Lerp(wood, light, grain * 0.8f);
                        if (!frame && Mathf.Abs(y - h / 2) < 1) c *= 0.85f;   // Brettfuge
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                return signSprite = ShopSprite(px, w, h);
            }
        }

        /// <summary>Offene Holzkiste von vorn: zwei Latten, Eckpfosten, dunkler Innenrand oben.</summary>
        static Sprite ShopCrateSprite
        {
            get
            {
                if (shopCrateSprite != null) return shopCrateSprite;
                const int w = 120, h = 56;
                var wood = new Color(0.72f, 0.5f, 0.28f); var dark = new Color(0.45f, 0.29f, 0.15f);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c;
                        bool post = x < 9 || x >= w - 9;
                        bool gap = !post && (y == 26 || y == 27);
                        float grain = Mathf.PerlinNoise(x * 0.05f, y * 0.3f);
                        c = post ? Color.Lerp(dark, wood, 0.55f) : Color.Lerp(dark, wood, 0.7f + grain * 0.3f);
                        if (gap) c = dark * 0.6f;
                        if (y >= h - 6) c = dark * 0.8f;                               // Innenrand
                        if (x < 2 || x >= w - 2 || y < 2 || y >= h - 2) c = ShopInk;        // Umriss
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                return shopCrateSprite = ShopSprite(px, w, h);
            }
        }

        static Sprite ShopSprite(Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        }
    }
}
