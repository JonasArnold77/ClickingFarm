using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Verwandlungs-Pflanzen (Rolle "transform"): Sie wachsen wie alle anderen, werden reif auf ein Ziel gezogen, wirken
    /// dort einmal und sind dann verbraucht.
    /// Steinmoos auf eine Holzmauer: die Mauer und angrenzende Holzmauern werden zu Stein (mehrfach so stabil).
    /// Glutbluete / Frostdistel / Blitzkraut / Giftpilz auf die Waffenbank: die gewaehlte Waffe bekommt Feuer, Eis,
    /// Blitz oder Gift (gleiches Element nochmal = staerker). Werte in plants.json -> transforms und catalog.transform.
    /// </summary>
    public partial class WorldBuilder
    {
        static Sprite stoneWallSprite;

        public static bool IsWeaponElement(string t) => t == "fire" || t == "ice" || t == "lightning" || t == "poison";

        public static Color ElementCol(string e) => e switch
        {
            "fire" => new Color(1f, 0.5f, 0.2f),
            "ice" => new Color(0.55f, 0.85f, 1f),
            "lightning" => new Color(1f, 0.95f, 0.4f),
            "poison" => new Color(0.6f, 1f, 0.35f),
            _ => Color.white
        };

        public static string ElementName(string e) => e switch
        {
            "fire" => "Feuer", "ice" => "Eis", "lightning" => "Blitz", "poison" => "Gift", _ => ""
        };

        /// <summary>Haltbarkeit einer Mauer: Stein haelt ein Vielfaches.</summary>
        float MaxHp(WallState st) => WallMaxHp * (st.stone ? PlantBalance.I.Transforms.stoneHpMult : 1f);

        public string WeaponElement(string id) => WLevel(id).element ?? "";
        public int WeaponElementLv(string id) => WLevel(id).elementLv;

        // ------------------------------------------------------------------ Ziehen und Ablegen

        string TransformOf(PlantedState p) => p != null && p.Grown && p.Role == "transform" ? PlantBalance.I.Plant(p.type)?.transform : null;

        /// <summary>Beim Ziehen: passendes Ziel unter dem Finger hervorheben (Holzmauer bzw. Waffenbank).</summary>
        bool TransformHover(PlantedState p, Vector2 w)
        {
            var t = TransformOf(p);
            if (t == null) return false;
            if (t == "stone")
            {
                var c = TapCell(w);
                if (walls.TryGetValue(c, out var v) && !v.st.stone) { hoverSlot = c; return true; }
                return false;
            }
            if (IsWeaponElement(t) && HitBench(w)) { benchSquash = Mathf.Max(benchSquash, 0.3f); return true; }
            return false;
        }

        /// <summary>Losgelassen: auf dem richtigen Ziel wirkt die Pflanze und ist verbraucht. true = erledigt.</summary>
        bool TryTransform(PlantedState p, Vector2 w)
        {
            var t = TransformOf(p);
            if (t == null) return false;
            var T = PlantBalance.I.Transforms;
            if (t == "stone")
            {
                var c = TapCell(w);
                if (!walls.TryGetValue(c, out var v) || v.st.stone) return false;
                int n = StoneWalls(c, T.stoneCells + T.stoneCellsPerLevel * (p.Lv - 1));
                Consume(p, CellCenter(c) + new Vector2(0, WallH));
                Floater?.Invoke(CellCenter(c) + new Vector2(0, WallH + 0.7f), n + (n == 1 ? " Mauer" : " Mauern") + " aus Stein!", new Color(0.85f, 0.85f, 0.9f));
                save.Save();
                return true;
            }
            if (IsWeaponElement(t) && HitBench(w))
            {
                Infuse(t, p.Lv);
                Consume(p, BenchTop);
                save.Save();
                return true;
            }
            return false;
        }

        /// <summary>Pflanze geht im Ziel auf: kleiner Wirbel aus Blaettern und Funken, dann ist sie weg.</summary>
        void Consume(PlantedState p, Vector2 at)
        {
            var c = new Cell(p.x, p.y);
            RemovePlantView(c);
            save.planted.Remove(p);
            Particles.I?.Burst(at + new Vector2(0, 0.2f), "leaf", new Color(0.5f, 0.85f, 0.4f), 12, 3f, 0.7f, 0.4f, 3f, 1.5f);
            Particles.I?.Burst(at + new Vector2(0, 0.3f), "star", new Color(1f, 0.95f, 0.7f), 14, 3f, 0.8f, 0.25f, 1f, 1.2f);
            WorldVfx.Play("eat", at + new Vector2(0, 0.2f), 1.1f, 30010, null, 2f);
            Haptics.Tap(35);
            NotifyInventory();
        }

        // ------------------------------------------------------------------ Steinmauer

        /// <summary>Holzmauer bei start und zusammenhaengende Holzmauern (bis max Stueck) werden zu Stein.</summary>
        int StoneWalls(Cell start, int max)
        {
            var queue = new Queue<Cell>(); var seen = new HashSet<Cell> { start };
            queue.Enqueue(start);
            int n = 0;
            while (queue.Count > 0 && n < max)
            {
                var c = queue.Dequeue();
                if (!walls.TryGetValue(c, out var v) || v.st.stone) continue;
                v.st.stone = true;
                v.st.hp = MaxHp(v.st);
                v.sr.sprite = StoneWallSprite;
                v.pop = 0.6f;
                StartCoroutine(StoneFx(CellCenter(c) + new Vector2(0, WallH * 0.6f), n * 0.07f));
                n++;
                foreach (var d in new[] { new Cell(c.x + 1, c.y), new Cell(c.x - 1, c.y), new Cell(c.x, c.y + 1), new Cell(c.x, c.y - 1) })
                    if (seen.Add(d) && walls.ContainsKey(d)) queue.Enqueue(d);
            }
            WorldCamera.I?.Shake(0.05f);
            return n;
        }

        System.Collections.IEnumerator StoneFx(Vector2 at, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Particles.I?.Burst(at, "square", new Color(0.62f, 0.62f, 0.64f), 10, 2.6f, 0.5f, 0.1f, 7f, 1.5f);
            Particles.I?.Burst(at, "square", new Color(0.8f, 0.78f, 0.74f, 0.6f), 5, 0.9f, 0.8f, 0.15f, -0.3f, 0.6f);
            Shockwave(at, 0.9f, 0.35f, new Color(0.85f, 0.85f, 0.9f, 0.7f));
            Sfx.Play("rock", Random.Range(0.85f, 1.05f), 0.7f);
        }

        // ------------------------------------------------------------------ Element fuer die Waffe

        void Infuse(string element, int lv)
        {
            var w = PlantBalance.I.Weapon(save.weapon);
            if (w == null) return;
            var e = WLevel(w.id);
            if (e.element == element) e.elementLv = Mathf.Min(PlantBalance.I.Transforms.maxElementLevel, e.elementLv + lv);
            else { e.element = element; e.elementLv = Mathf.Min(PlantBalance.I.Transforms.maxElementLevel, lv); }
            var col = ElementCol(element);
            var top = BenchTop;
            benchSquash = 1f;
            Particles.I?.Burst(top + new Vector2(0, 0.2f), "spark", col, 24, 3.5f, 0.6f, 0.25f, 1f, 1.2f);
            Shockwave(top, 1.4f, 0.45f, new Color(col.r, col.g, col.b, 0.85f));
            WorldFx.Play(element == "lightning" ? "electrified" : element == "fire" ? "flame" : "magicPoof", top + new Vector2(0, 0.2f), 0.12f);
            Sfx.Play(element == "lightning" ? "zap" : "unlock", 1.1f, 0.8f);
            WorldPostFX.Punch(0.4f);
            WeaponInfused?.Invoke(w, element, e.elementLv);
        }

        public event System.Action<WeaponDef, string, int> WeaponInfused;

        /// <summary>Glimmen ueber der Waffe auf der Bank, solange sie ein Element traegt.</summary>
        void UpdateBenchElement()
        {
            if (benchIcon == null) return;
            var el = WeaponElement(save.weapon);
            benchIcon.color = el == "" ? Color.white : Color.Lerp(Color.white, ElementCol(el), 0.45f);
            if (el != "" && Random.value < Time.deltaTime * 6f)
                Particles.I?.Spawn(benchIcon.transform.position + (Vector3)(Random.insideUnitCircle * 0.25f), "spark", ElementCol(el), Vector3.up * 0.4f, 0.6f, 0.08f, 0f, 0f);
        }

        // ------------------------------------------------------------------ Bild

        /// <summary>Steinmauer: Vorderseite aus versetzten Steinbloecken, oben Steinplatten (gleiche Form wie die Holzmauer).</summary>
        public static Sprite StoneWallSprite
        {
            get
            {
                if (stoneWallSprite != null) return stoneWallSprite;
                const int w = 96;
                int front = Mathf.RoundToInt(WallH / CellSize * w), h = front + w;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var stone = new Color(0.56f, 0.56f, 0.58f); var hi = new Color(0.8f, 0.79f, 0.77f); var dark = new Color(0.28f, 0.28f, 0.3f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c;
                        float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.3f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.1f;
                        if (y < front)
                        {
                            const int rows = 4; float rh = front / (float)rows;
                            int row = (int)(y / rh);
                            bool joint = (y % rh) < 2.5f || ((x + row * 16) % 32) < 2.5f;
                            c = Color.Lerp(stone * 0.8f, stone, n + (y % rh) / rh * 0.3f);
                            c *= Mathf.Lerp(0.72f, 1f, y / (float)front);
                            if (joint) c = dark;
                        }
                        else
                        {
                            int yy = y - front;
                            bool joint = (yy % 32) < 2 || ((x + (yy / 32) * 20) % 48) < 2;
                            c = Color.Lerp(stone, hi, n + 0.25f);
                            if (joint) c = Color.Lerp(dark, stone, 0.3f);
                        }
                        if (x < 2 || x > w - 3 || y < 2 || y > h - 3 || y == front) c = dark;
                        c.a = 1f;
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                stoneWallSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return stoneWallSprite;
            }
        }
    }
}
