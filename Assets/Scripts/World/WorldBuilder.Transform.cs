using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Verwandlungs-Pflanzen (Rolle "transform"): Sie wachsen wie alle anderen, werden reif auf ein Ziel gezogen, wirken
    /// dort einmal und sind dann verbraucht.
    /// Steinmoos auf einen Mauerabschnitt: alle zusammenhaengenden Holzmauern werden zu Stein (mehrfach so stabil);
    /// auf einen hoelzernen Wachturm: Steinturm. Dornenranke: ganzer Abschnitt bekommt Dornen. Beim Ziehen leuchtet
    /// der ganze Abschnitt gruen. Wurzelherz ins Pult: mehr Reichweite. Mondblume: Laterne doppelt weit, Turm doppelt.
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
            var sv = StructTarget(t, w);
            if (sv != null) { sv.squash = Mathf.Max(sv.squash, 0.3f); return true; }
            var c = TapCell(w);
            // Steinmoos und Dornenranke: der ganze Mauerabschnitt leuchtet gruen
            if ((t == "stone" || t == "thorns") && HasWall(c))
            {
                var seg = WallSegment(c);
                if (t == "stone" && seg.TrueForAll(x => walls[x].st.stone)) return false;   // schon alles Stein
                hoverCells = seg; hoverSlot = null; return true;
            }
            if (t == "heart" && HasPedestal(c)) { hoverSlot = c; return true; }
            if (IsWeaponElement(t) && HitBench(w)) { benchSquash = Mathf.Max(benchSquash, 0.3f); return true; }
            return false;
        }

        /// <summary>Losgelassen: auf dem richtigen Ziel wirkt die Pflanze und ist verbraucht. true = erledigt.</summary>
        bool TryTransform(PlantedState p, Vector2 w)
        {
            var t = TransformOf(p);
            if (t == null) return false;
            var T = PlantBalance.I.Transforms;
            // Bauwerke: Steinmoos auf hoelzernen Wachturm, Mondblume auf Laterne/Wachturm
            var sv = StructTarget(t, w);
            if (sv != null)
            {
                var top = new Vector2(sv.sr.bounds.center.x, sv.sr.bounds.max.y - 0.2f);
                if (t == "stone") { sv.st.stone = true; StartCoroutine(StoneFx(top + new Vector2(0, -0.4f), 0f)); StartCoroutine(StoneFx(top, 0.12f)); }
                else
                {
                    sv.st.moon = true;
                    Particles.I?.Burst(top, "star", new Color(0.7f, 0.82f, 1f), 24, 3f, 1f, 0.3f, -0.5f, 1.2f);
                    Shockwave(top, 2f, 0.6f, new Color(0.7f, 0.82f, 1f, 0.8f));
                    Sfx.Play("bell", 1.2f, 0.7f);
                }
                ApplyStructLook(sv);
                sv.squash = 1f;
                Consume(p, top);
                Floater?.Invoke(top + new Vector2(0, 0.6f), t == "stone" ? sv.def.name + " aus Stein!" : sv.def.name + ": Mondlicht!", new Color(0.85f, 0.88f, 1f));
                save.Save();
                return true;
            }
            if (t == "thorns" || t == "heart")
            {
                var c = TapCell(w);
                if (t == "thorns" && HasWall(c))
                {
                    int n = ThornWalls(c, p.Lv);
                    Consume(p, CellCenter(c) + new Vector2(0, WallH));
                    Floater?.Invoke(CellCenter(c) + new Vector2(0, WallH + 0.7f), n + (n == 1 ? " Mauer" : " Mauern") + " mit Dornen!", new Color(0.7f, 1f, 0.6f));
                    save.Save();
                    return true;
                }
                if (t == "heart" && pedestals.TryGetValue(c, out var pv))
                {
                    pv.st.heart = Mathf.Min(T.maxHeart, pv.st.heart + 1);
                    ApplyHeartLook(pv);
                    var at = CellCenter(c) + new Vector2(0, CellLift(c));
                    Particles.I?.Burst(at, "spark", new Color(1f, 0.45f, 0.55f), 18, 2.5f, 0.6f, 0.2f, 0f, 1f);
                    Shockwave(at, 1.2f, 0.45f, new Color(1f, 0.5f, 0.6f, 0.8f));
                    Sfx.Play("grow", 1.1f, 0.8f);
                    Consume(p, at);
                    Floater?.Invoke(at + new Vector2(0, 0.6f), "Wurzelherz " + pv.st.heart + ": +" + (pv.st.heart * T.heartRange).ToString("0.#") + " Reichweite", new Color(1f, 0.7f, 0.75f));
                    save.Save();
                    return true;
                }
                return false;
            }
            if (t == "stone")
            {
                var c = TapCell(w);
                if (!walls.ContainsKey(c)) return false;
                int n = StoneWalls(c);
                if (n == 0) return false;   // schon alles Stein
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

        /// <summary>Alle Mauern, die mit dieser zusammenhaengen (ein Mauerabschnitt, ohne Diagonalen).</summary>
        List<Cell> WallSegment(Cell start)
        {
            var list = new List<Cell>();
            if (!walls.ContainsKey(start)) return list;
            var queue = new Queue<Cell>(); var seen = new HashSet<Cell> { start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                list.Add(c);
                foreach (var d in new[] { new Cell(c.x + 1, c.y), new Cell(c.x - 1, c.y), new Cell(c.x, c.y + 1), new Cell(c.x, c.y - 1) })
                    if (walls.ContainsKey(d) && seen.Add(d)) queue.Enqueue(d);
            }
            return list;
        }

        /// <summary>Bauwerk unter dem Finger, das diese Verwandlung annimmt (sonst null).</summary>
        StructView StructTarget(string t, Vector2 w)
        {
            if (t != "stone" && t != "moon") return null;
            var st = HitStructure(w);
            if (st == null) return null;
            var v = structViews.Find(x => x.st == st);
            if (v == null) return null;
            if (t == "stone") return v.def.effect == "tower" && !st.stone ? v : null;
            return (v.def.effect == "tower" || v.def.effect == "light") && !st.moon ? v : null;
        }

        /// <summary>Reichweiten-Bonus fuer eine Pflanze in einem Pult mit Wurzelherz.</summary>
        public float PotRange(PlantedState p) => pedestals.TryGetValue(new Cell(p.x, p.y), out var v) ? v.st.heart * PlantBalance.I.Transforms.heartRange : 0f;

        void ApplyHeartLook(PedestalView v)
        {
            if (v.st.heart <= 0 || v.sr == null) return;
            if (v.heartSr == null)
            {
                v.heartSr = new GameObject("RootHeart").AddComponent<SpriteRenderer>();
                v.heartSr.transform.SetParent(v.sr.transform, false);
                v.heartSr.sprite = MistSprite;
                v.heartSr.sharedMaterial = UnlitMat;
                v.heartSr.sortingOrder = v.sr.sortingOrder + 1;   // ueber der Erde, unter Pflanze und vorderem Rand
            }
            v.heartSr.transform.position = PedFoot(new Cell(v.st.x, v.st.y)) + new Vector2(0, PedH + CellSize * 0.5f);
            float s = 0.32f + 0.06f * v.st.heart;
            v.heartSr.transform.localScale = new Vector3(s, s * 0.6f, 1f) / Mathf.Max(0.01f, v.sr.transform.lossyScale.x);
        }

        // ------------------------------------------------------------------ Dornen

        /// <summary>Der ganze Mauerabschnitt bei start bekommt Dornen (+lv, hoechstens maxThorns).</summary>
        int ThornWalls(Cell start, int lv)
        {
            int n = 0, cap = PlantBalance.I.Transforms.maxThorns;
            foreach (var c in WallSegment(start))
            {
                if (!walls.TryGetValue(c, out var v)) continue;
                v.st.thorns = Mathf.Min(cap, v.st.thorns + lv);
                ApplyThornLook(v);
                v.pop = 0.75f;
                var at = CellCenter(c) + new Vector2(0, WallH * 0.4f);
                Particles.I?.Burst(at, "leaf", new Color(0.35f, 0.6f, 0.25f), 8, 2.2f, 0.6f, 0.3f, 3f, 1.2f);
                n++;
            }
            Sfx.Play("grow", 0.8f, 0.8f);
            return n;
        }

        void ApplyThornLook(WallView v)
        {
            if (v.st.thorns <= 0 || v.sr == null) return;
            if (v.thornSr == null)
            {
                v.thornSr = new GameObject("Thorns").AddComponent<SpriteRenderer>();
                v.thornSr.transform.SetParent(v.sr.transform, false);
                v.thornSr.sprite = ThornSprite;
                v.thornSr.sortingOrder = v.sr.sortingOrder + 1;
            }
            // mehr Stufen: dichtere, dunklere Ranken
            v.thornSr.color = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), new Color(0.75f, 0.9f, 0.7f, 1f), (v.st.thorns - 1) / 4f);
        }

        static Sprite thornSprite;

        /// <summary>Dornenranken ueber der Mauer-Vorderseite (gleiche Groesse wie das Mauerbild).</summary>
        static Sprite ThornSprite
        {
            get
            {
                if (thornSprite != null) return thornSprite;
                const int w = 96;
                int front = Mathf.RoundToInt(WallH / CellSize * w), h = front + w;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var vine = new Color(0.22f, 0.42f, 0.16f); var thorn = new Color(0.9f, 0.85f, 0.7f);
                for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
                // zwei gewellte Ranken quer ueber die Vorderseite, mit Dornen nach oben und unten
                for (int k = 0; k < 2; k++)
                {
                    float baseY = front * (0.32f + 0.38f * k), amp = front * 0.12f, ph = k * 2.1f;
                    for (int x = 0; x < w; x++)
                    {
                        float y = baseY + Mathf.Sin(x * 0.13f + ph) * amp;
                        for (int dy = -2; dy <= 2; dy++) { int yy = Mathf.RoundToInt(y) + dy; if (yy >= 0 && yy < h) px[yy * w + x] = vine * (1f - Mathf.Abs(dy) * 0.12f); }
                        if ((x + k * 7) % 12 == 0)
                        {
                            float dir = ((x / 12) % 2 == 0) ? 1f : -1f;
                            for (int t = 0; t < 7; t++)
                            {
                                int yy = Mathf.RoundToInt(y + dir * (3 + t)), half = (6 - t) / 3;
                                for (int dx = -half; dx <= half; dx++) { int xx = x + dx; if (xx >= 0 && xx < w && yy >= 0 && yy < h) px[yy * w + xx] = t < 2 ? vine : thorn; }
                            }
                        }
                    }
                }
                tex.SetPixels(px); tex.Apply();
                thornSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return thornSprite;
            }
        }

        // ------------------------------------------------------------------ Steinmauer

        /// <summary>Alle Holzmauern im Mauerabschnitt bei start werden zu Stein.</summary>
        int StoneWalls(Cell start)
        {
            int n = 0;
            foreach (var c in WallSegment(start))
            {
                if (!walls.TryGetValue(c, out var v) || v.st.stone) continue;
                v.st.stone = true;
                v.st.hp = MaxHp(v.st);
                v.sr.sprite = StoneWallSprite;
                v.pop = 0.6f;
                StartCoroutine(StoneFx(CellCenter(c) + new Vector2(0, WallH * 0.6f), Mathf.Min(n, 20) * 0.05f));
                n++;
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
