using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>
    /// Runen-Altar links vor dem Haus: Hier opfert man Runensteine. Jeder geopferte Stein macht die naechste Welle
    /// gefaehrlicher (mehr, groessere und staerkere Zombies) – aber auch viel ertragreicher (mehr Muenzen pro Zombie,
    /// Bonus beim Ueberstehen, mehr Runensteine). Runensteine fallen von Zombies und manchmal aus Felsbrocken.
    /// Werte in plants.json -> altar.
    /// </summary>
    public partial class WorldBuilder
    {
        SpriteRenderer altar;
        Light2D altarLight;
        Vector2 altarPos;
        float altarSquash, altarScale;
        readonly List<SpriteRenderer> altarRunes = new List<SpriteRenderer>();
        static Sprite altarSprite, runeSprite;

        public event Action AltarTapped;
        public event Action<int> RunesChanged;
        /// <summary>Solange das Altar-Fenster offen ist.</summary>
        public bool AltarOpen { get; set; }

        public int Runes => save.runes;
        /// <summary>Wie viele Runen fuer die naechste Welle geopfert sind (Fluch-Stufe).</summary>
        public int AltarLevel => save.altarLevel;

        void PlaceAltar()
        {
            altarPos = HousePos + new Vector2(-1.95f, -1.6f);
            Shadow(altarPos, 1.4f, 0.32f, 0.32f);
            altar = new GameObject("Altar").AddComponent<SpriteRenderer>();
            altar.transform.SetParent(objects, false);
            altar.sprite = AltarSprite;
            altarScale = 1.25f / Mathf.Max(0.01f, altar.sprite.bounds.size.x);
            altar.transform.position = altarPos;
            altar.transform.localScale = Vector3.one * altarScale;
            altar.sortingOrder = Order(altarPos.y);
            Reserve(altarPos + new Vector2(0, 0.3f), 0.8f);

            var lgo = new GameObject("AltarLight");
            lgo.transform.SetParent(altar.transform, false);
            lgo.transform.position = altarPos + new Vector2(0, 0.8f);
            altarLight = lgo.AddComponent<Light2D>();
            altarLight.lightType = Light2D.LightType.Point;
            altarLight.pointLightInnerRadius = 0.2f; altarLight.pointLightOuterRadius = 2.2f;
            altarLight.color = new Color(0.75f, 0.35f, 1f);
            altarLight.intensity = 0f;

            if (!save.runesInit) { save.runes = Mathf.Max(save.runes, PlantBalance.I.Altar.startRunes); save.runesInit = true; }
        }

        Vector2 AltarTop => altar != null ? new Vector2(altarPos.x, altar.bounds.max.y - 0.15f) : altarPos + new Vector2(0, 0.6f);

        public bool HitAltar(Vector2 w)
        {
            if (altar == null) return false;
            var b = altar.bounds; b.Expand(new Vector3(0.2f, 0.4f, 0));
            return b.Contains(new Vector3(w.x, w.y, b.center.z));
        }

        public void TapAltar()
        {
            altarSquash = 0.5f;
            Sfx.Play("pop", 0.75f);
            Haptics.Tap(10);
            AltarTapped?.Invoke();
        }

        // ------------------------------------------------------------------ Runen

        public void AddRunes(int n, Vector2? at = null)
        {
            if (n <= 0) return;
            save.runes += n;
            if (at.HasValue)
            {
                Floater?.Invoke(at.Value, "+" + n + " Runenstein" + (n > 1 ? "e" : ""), new Color(0.85f, 0.6f, 1f));
                Particles.I?.Burst(at.Value, "star", new Color(0.8f, 0.5f, 1f), 10, 2.5f, 0.7f, 0.2f, 0f, 1f);
                Sfx.Play("bell", 1.5f, 0.5f);
            }
            RunesChanged?.Invoke(save.runes);
        }

        /// <summary>Eine Rune auf den Altar legen. Nur tagsueber und hoechstens bis zur Hoechststufe.</summary>
        public bool OfferRune()
        {
            var nw = NightWave.I;
            if (nw != null && nw.Active) return false;
            if (save.runes <= 0 || save.altarLevel >= PlantBalance.I.Altar.maxLevel) { Sfx.Play("error", 1f, 0.5f); return false; }
            save.runes--;
            save.altarLevel++;
            altarSquash = 1f;
            var top = AltarTop;
            StartCoroutine(Leaf(top + new Vector2(0, 1.4f), top, () =>
            {
                Particles.I?.Burst(top, "spark", new Color(0.8f, 0.4f, 1f), 20, 3f, 0.6f, 0.2f, -1f, 1.2f);
                Shockwave(top, 1.5f, 0.5f, new Color(0.75f, 0.4f, 1f, 0.85f));
                WorldFx.Play("magicPoof", top + new Vector2(0, 0.1f), 0.14f);
                WorldCamera.I?.Shake(0.04f);
            }, RuneSprite, null, 0.28f));
            Sfx.Play("groan", 0.6f, 0.5f);
            Sfx.Play("bell", 0.7f, 0.6f);
            Haptics.Tap(30);
            RunesChanged?.Invoke(save.runes);
            save.Save();
            return true;
        }

        /// <summary>Eine geopferte Rune zuruecknehmen (vor der Welle).</summary>
        public bool TakeBackRune()
        {
            var nw = NightWave.I;
            if (save.altarLevel <= 0 || (nw != null && nw.Active)) return false;
            save.altarLevel--;
            save.runes++;
            Sfx.Play("pop", 1.2f, 0.6f);
            RunesChanged?.Invoke(save.runes);
            save.Save();
            return true;
        }

        /// <summary>Beim Beginn der Nacht: Fluch-Stufe uebernehmen, der Altar ist danach wieder leer.</summary>
        public int ConsumeAltar()
        {
            int lv = save.altarLevel;
            save.altarLevel = 0;
            save.Save();
            return lv;
        }

        void UpdateAltar(float dt)
        {
            if (altar == null) return;
            altarSquash = Mathf.MoveTowards(altarSquash, 0f, dt * 4f);
            float sq = Mathf.Sin(altarSquash * Mathf.PI * 1.5f) * altarSquash * 0.12f;
            altar.transform.localScale = new Vector3(altarScale * (1 + sq), altarScale * (1 - sq), 1);
            int lv = save.altarLevel;
            var nw = NightWave.I;
            int shown = nw != null && nw.Active ? nw.Curse : lv;
            // geopferte Runen schweben ueber dem Altar und kreisen langsam
            while (altarRunes.Count < shown)
            {
                var r = new GameObject("AltarRune").AddComponent<SpriteRenderer>();
                r.transform.SetParent(objects, false);
                r.sprite = RuneSprite;
                r.sortingOrder = Order(altarPos.y) + 2;
                altarRunes.Add(r);
            }
            for (int i = 0; i < altarRunes.Count; i++)
            {
                var r = altarRunes[i];
                r.enabled = i < shown;
                if (!r.enabled) continue;
                float a = Time.time * 0.8f + i * Mathf.PI * 2f / Mathf.Max(1, shown);
                r.transform.position = AltarTop + new Vector2(Mathf.Cos(a) * 0.45f, 0.35f + Mathf.Sin(a) * 0.12f + Mathf.Sin(Time.time * 2f + i) * 0.04f);
                float s = 0.16f / Mathf.Max(0.01f, RuneSprite.bounds.size.x);
                r.transform.localScale = Vector3.one * s;
                r.sortingOrder = Order(altarPos.y) + (Mathf.Sin(a) < 0 ? 3 : -1);   // vorn / hinter dem Altar
            }
            float want = shown > 0 ? 0.6f + 0.35f * shown : 0f;
            altarLight.intensity = Mathf.MoveTowards(altarLight.intensity, want * (0.85f + 0.15f * Mathf.Sin(Time.time * 4f)), dt * 2f);
            if (shown > 0 && UnityEngine.Random.value < dt * (2f + shown * 2f))
                Particles.I?.Spawn(AltarTop + new Vector2(UnityEngine.Random.Range(-0.4f, 0.4f), 0.05f), "spark", new Color(0.8f, 0.45f, 1f), Vector3.up * 0.7f, 0.9f, 0.07f, 0f, 0f);
        }

        // ------------------------------------------------------------------ Bilder

        /// <summary>Steinaltar: breiter Sockel, Platte mit Rand, eingeritzte Runen in der Vorderseite.</summary>
        public static Sprite AltarSprite
        {
            get
            {
                if (altarSprite != null) return altarSprite;
                const int w = 128, h = 112;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var stone = new Color(0.46f, 0.44f, 0.5f); var hi = new Color(0.72f, 0.7f, 0.76f); var dark = new Color(0.22f, 0.2f, 0.26f);
                var glyph = new Color(0.8f, 0.5f, 1f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = Color.clear;
                        float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.25f;
                        bool foot = y < 18 && x >= 10 && x < w - 10;
                        bool body = y >= 18 && y < 70 && x >= 22 && x < w - 22;
                        bool slabFront = y >= 70 && y < 82 && x >= 6 && x < w - 6;
                        bool slabTop = y >= 82 && y < 104 && x >= 10 + (y - 82) / 3 && x < w - 10 - (y - 82) / 3;
                        if (foot) c = Color.Lerp(stone * 0.75f, stone, n + y / 18f * 0.3f);
                        else if (body)
                        {
                            c = Color.Lerp(stone * 0.85f, stone, n + 0.2f);
                            // Runen: einfache Striche im Mittelfeld
                            int gx = (x - 34) % 20, gy = y - 30;
                            bool rune = x >= 34 && x < w - 34 && gy >= 0 && gy < 28 &&
                                        (gx == 4 || (gx > 4 && gx < 13 && Mathf.Abs(gy - (gx - 4) * 1.6f) < 1.4f) || (gx > 4 && gx < 13 && gy == 14 + (x / 20) % 3 * 3));
                            if (rune) c = glyph;
                        }
                        else if (slabFront) c = Color.Lerp(stone * 0.8f, stone, n + 0.3f);
                        else if (slabTop) c = Color.Lerp(stone, hi, n + 0.3f);
                        if (c.a > 0f)
                        {
                            bool edge = (foot && (y < 2 || y == 17 || x == 10 || x == w - 11)) || (body && (x == 22 || x == w - 23)) ||
                                        (slabFront && (y == 70 || x == 6 || x == w - 7)) || (slabTop && (y == 103 || y == 82));
                            if (edge) c = dark;
                            c.a = 1f;
                        }
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                altarSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return altarSprite;
            }
        }

        /// <summary>Runenstein: kleiner violetter Stein mit leuchtendem Zeichen.</summary>
        public static Sprite RuneSprite
        {
            get
            {
                if (runeSprite != null) return runeSprite;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x - n / 2f) / (n * 0.42f), dy = (y - n / 2f) / (n * 0.48f);
                        float d = dx * dx + dy * dy;
                        if (d > 1f) { px[y * n + x] = Color.clear; continue; }
                        var c = Color.Lerp(new Color(0.3f, 0.2f, 0.45f), new Color(0.55f, 0.42f, 0.7f), 1f - d + (y - x) * 0.006f);
                        bool glyph = (Mathf.Abs(x - n / 2) < 2 && y > 12 && y < 36) || (Mathf.Abs(y - 30 + (x - n / 2)) < 2 && x > n / 2 && x < n / 2 + 9) || (Mathf.Abs(y - 20 - (x - n / 2)) < 2 && x < n / 2 && x > n / 2 - 9);
                        if (glyph) c = new Color(1f, 0.8f, 1f);
                        if (d > 0.85f) c *= 0.6f;
                        c.a = 1f;
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                runeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return runeSprite;
            }
        }
    }
}
