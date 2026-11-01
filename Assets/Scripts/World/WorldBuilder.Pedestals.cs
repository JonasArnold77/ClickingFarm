using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Steinpulte: kleine Steinsockel mit einem Erdloch in der Mitte. Sie stehen auf dem Boden oder oben auf einer Mauer;
    /// ins Loch passen nur Kampf- und Verteidigungspflanzen. Bau-Modus "Steinpult" (Knopf unten), Tipp baut bzw. baut
    /// ein leeres Pult wieder ab (Muenzen zurueck).
    /// Dazu das Ziehen im Bau-Modus: Finger gedrueckt halten und ziehen setzt Mauern (oder Pulte) entlang der Spur;
    /// beginnt der Zug auf einer leeren Mauer, wird stattdessen abgebaut. Werte in plants.json -> pedestal.
    /// </summary>
    public partial class WorldBuilder
    {
        class PedestalView { public PedestalState st; public SpriteRenderer sr, lip, heartSr; public float pop; }
        readonly Dictionary<Cell, PedestalView> pedestals = new Dictionary<Cell, PedestalView>();
        static Sprite pedSprite, pedGhost;
        bool pedestalMode;

        /// <summary>Hoehe der Pult-Vorderseite und wie hoch eine Pflanze im Loch steht (ueber ihrem Bodenpunkt).</summary>
        public const float PedH = 0.2f, PedLift = PedH + CellSize * 0.5f - PedSink - CellSize * 0.12f;   // Fuss etwas unter der Lochmitte
        /// <summary>So tief steckt der Fuss der Pflanze in der Erde; die vordere Kante (PedLipSprite) verdeckt ihn.</summary>
        const float PedSink = 0.1f, PedCut = 0.04f;
        static Sprite pedLip;
        const float PedTop = CellSize;   // Platte ist ein ganzes Feld tief

        public bool PedestalMode => pedestalMode;
        public bool BuildMode => wallMode || pedestalMode || riverMode;
        public event System.Action<bool> PedestalModeChanged;

        public bool HasPedestal(Cell c) => pedestals.ContainsKey(c);

        /// <summary>Wie weit eine Pflanze auf dieser Zelle angehoben steht (Mauer und/oder Pult).</summary>
        public float CellLift(Cell c) => (HasWall(c) ? WallH : 0f) + (HasPedestal(c) ? PedLift : 0f);

        public static bool IsDefensePlant(CatalogDef d) => d != null && (d.IsCombat || d.role == "light");

        public void SetPedestalMode(bool on)
        {
            if (on && wallMode) { wallMode = false; WallModeChanged?.Invoke(false); }
            if (on && riverMode) { riverMode = false; RiverModeChanged?.Invoke(false); }
            CancelWallPlan();
            pedestalMode = on;
            RefreshGhosts();
            PedestalModeChanged?.Invoke(on);
        }

        void LoadPedestals()
        {
            foreach (var p in save.pedestals) CreatePedestalView(p, false);
        }

        public bool CanPlacePedestal(Cell c) => !HasPedestal(c) && PlantAt(c) == null && !HasStructure(c) && !HasGate(c) && (HasWall(c) || CanPlaceWall(c));

        bool BuildPedestal(Cell c, bool quietNoMoney)
        {
            if (!CanPlacePedestal(c)) return false;
            float cost = PlantBalance.I.Pedestal.cost;
            if (save.coins < cost)
            {
                if (!quietNoMoney)
                {
                    Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Zu wenig Münzen", new Color(1f, 0.6f, 0.5f));
                    Sfx.Play("error", 1f, 0.6f);
                }
                return false;
            }
            TrySpend(cost);
            var st = new PedestalState { x = c.x, y = c.y };
            save.pedestals.Add(st);
            CreatePedestalView(st, true);
            RefreshGhosts();
            return true;
        }

        bool RemovePedestal(Cell c, bool refund)
        {
            if (!pedestals.TryGetValue(c, out var v)) return false;
            var p = PlantAt(c);
            if (p != null) { if (refund) return false; Unplant(p); }
            Particles.I?.Burst(CellCenter(c) + new Vector2(0, CellLift(c)), "square", new Color(0.62f, 0.6f, 0.58f), 8, 2.2f, 0.5f, 0.09f, 7f, 1.5f);
            Sfx.Play("break", 1.3f, 0.6f);
            if (v.sr != null) Destroy(v.sr.gameObject);
            pedestals.Remove(c);
            save.pedestals.Remove(v.st);
            if (refund) AddCoins(PlantBalance.I.Pedestal.cost, CellCenter(c) + new Vector2(0, 0.6f));
            RefreshGhosts();
            return true;
        }

        Vector2 PedFoot(Cell c) => new Vector2((c.x + 0.5f) * CellSize, c.y * CellSize + (HasWall(c) ? WallH : 0f));

        void CreatePedestalView(PedestalState st, bool fx)
        {
            var c = new Cell(st.x, st.y);
            var sr = new GameObject("Pedestal").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = PedestalSprite;
            sr.transform.position = PedFoot(c);
            sr.transform.localScale = Vector3.one * CellSize;
            sr.sortingOrder = Order(c.y * CellSize + 0.1f) + 1;   // knapp vor der Mauer, hinter der Pflanze im Loch
            // vordere Haelfte (Erde vorn im Loch, Rand, Vorderseite) vor der Pflanze: so waechst sie aus dem Loch heraus
            var lip = new GameObject("PedestalLip").AddComponent<SpriteRenderer>();
            lip.transform.SetParent(sr.transform, false);
            lip.sprite = PedestalLipSprite;
            lip.sortingOrder = Order(c.y * CellSize) + 3;
            pedestals[c] = new PedestalView { st = st, sr = sr, lip = lip, pop = fx ? 0f : 1f };
            ApplyHeartLook(pedestals[c]);
            ClearDecorAround(CellCenter(c), CellSize * 0.6f);
            if (fx)
            {
                Particles.I?.Burst(PedFoot(c) + new Vector2(0, 0.2f), "square", new Color(0.65f, 0.63f, 0.6f), 8, 2.2f, 0.45f, 0.08f, 7f, 1.4f);
                Sfx.Play("rock", UnityEngine.Random.Range(1.1f, 1.3f), 0.7f);
                Haptics.Tap(10);
            }
        }

        void UpdatePedestals(float dt)
        {
            foreach (var kv in pedestals)
            {
                var v = kv.Value;
                if (v.heartSr != null)
                {
                    // Wurzelherz pulsiert wie ein Herzschlag
                    float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 3.2f + kv.Key.x)), 6f);
                    v.heartSr.color = new Color(1f, 0.35f, 0.5f, 0.45f + 0.35f * beat);
                    if (UnityEngine.Random.value < dt * 1.5f) Particles.I?.Spawn((Vector2)v.heartSr.transform.position, "spark", new Color(1f, 0.5f, 0.6f), Vector3.up * 0.5f, 0.7f, 0.07f, 0f, 0f);
                }
                if (v.pop >= 1f || v.sr == null) continue;
                v.pop = Mathf.MoveTowards(v.pop, 1f, dt * 4f);
                float e = 1 + 2.70158f * Mathf.Pow(v.pop - 1, 3) + 1.70158f * Mathf.Pow(v.pop - 1, 2);
                v.sr.transform.localScale = Vector3.one * CellSize * e;
            }
        }

        /// <summary>Tipp im Pult-Modus: Pult bauen bzw. leeres Pult abbauen.</summary>
        public bool TapPedestalMode(Cell c)
        {
            if (HasPedestal(c)) return RemovePedestal(c, true);
            if (!CanPlacePedestal(c)) return false;   // dann faellt der Tipp auf Baum/Fels durch
            if (BuildPedestal(c, false)) save.Save();
            return true;
        }

        // ------------------------------------------------------------------ Ziehen (Mauern und Pulte malen)

        bool strokeErase, strokeWarned;
        Cell strokeLast;

        /// <summary>Druck im Bau-Modus. true = ab jetzt wird gemalt (Karte verschiebt sich nicht).</summary>
        public bool BeginBuildStroke(Cell c)
        {
            if (!BuildMode) return false;
            strokeWarned = false;
            strokeLast = c;
            if (wallMode)
            {
                strokeErase = HasWall(c) && PlantAt(c) == null && !HasPedestal(c);
                if (!strokeErase && !CanPlaceWall(c)) return false;
            }
            else
            {
                strokeErase = HasPedestal(c) && PlantAt(c) == null;
                if (!strokeErase && !CanPlacePedestal(c)) return false;
            }
            StrokeAt(c);
            return true;
        }

        /// <summary>Finger bewegt sich: alle Zellen zwischen der letzten und dieser bekommen eine Mauer (luckenlos, ohne Diagonalen).</summary>
        public void ContinueBuildStroke(Cell to)
        {
            int guard = 0;
            while (!strokeLast.Equals(to) && guard++ < 64)
            {
                int dx = to.x - strokeLast.x, dy = to.y - strokeLast.y;
                // in die Richtung mit dem groesseren Abstand weiter – so entsteht eine zusammenhaengende Mauer
                strokeLast = Mathf.Abs(dx) >= Mathf.Abs(dy) ? new Cell(strokeLast.x + System.Math.Sign(dx), strokeLast.y) : new Cell(strokeLast.x, strokeLast.y + System.Math.Sign(dy));
                StrokeAt(strokeLast);
            }
        }

        public void EndBuildStroke() => save.Save();

        void StrokeAt(Cell c)
        {
            if (wallMode)
            {
                if (strokeErase)
                {
                    if (walls.TryGetValue(c, out var v) && PlantAt(c) == null && !HasPedestal(c))
                    {
                        RemoveWall(c, v, false);
                        RefundWallWood(CellCenter(c) + new Vector2(0, 0.5f));
                    }
                    return;
                }
                if (!CanPlaceWall(c)) return;
                if (WoodLeft < WallWood - 0.0001f)
                {
                    if (!strokeWarned) { strokeWarned = true; Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.6f), "Zu wenig Holz – fälle Bäume!", new Color(1f, 0.6f, 0.5f)); Sfx.Play("error", 1f, 0.6f); }
                    return;
                }
                BuildWall(c);
                return;
            }
            if (strokeErase) { RemovePedestal(c, true); return; }
            if (!BuildPedestal(c, strokeWarned)) { if (!strokeWarned && save.coins < PlantBalance.I.Pedestal.cost) strokeWarned = true; }
        }

        // ------------------------------------------------------------------ Bild

        /// <summary>Steinpult in Schraegsicht: Vorderseite aus Steinbloecken, oben eine Steinplatte mit Erdloch.</summary>
        public static Sprite PedestalSprite
        {
            get
            {
                if (pedSprite != null) return pedSprite;
                const int w = 96;
                int pw = w, front = Mathf.RoundToInt(PedH / CellSize * w), top = Mathf.RoundToInt(PedTop / CellSize * w), h = front + top;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                var stone = new Color(0.58f, 0.57f, 0.55f); var stoneHi = new Color(0.82f, 0.8f, 0.76f); var dark = new Color(0.3f, 0.29f, 0.28f);
                int x0 = (w - pw) / 2, x1 = x0 + pw;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = Color.clear;
                        if (x >= x0 && x < x1)
                        {
                            float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.25f;
                            if (y < front)
                            {
                                // Vorderseite: zwei Reihen Steinbloecke, versetzt
                                int row = y / (front / 2 + 1);
                                bool joint = y % (front / 2 + 1) < 2 || ((x - x0 + row * 13) % 26) < 2;
                                c = Color.Lerp(stone * 0.8f, stone, n + y / (float)front * 0.4f);
                                if (joint) c = dark;
                            }
                            else
                            {
                                // Platte mit Erdloch in der Mitte
                                float u = (x - w / 2f) / (pw * 0.5f), v = (y - front - top / 2f) / (top * 0.5f);
                                float r = Mathf.Sqrt(u * u * 1.0f + v * v * 1.0f);
                                c = Color.Lerp(stone, stoneHi, n + 0.35f);
                                if (r < 0.62f) c = Color.Lerp(new Color(0.42f, 0.4f, 0.38f), stone, (r - 0.5f) / 0.12f);   // Rand der Mulde
                                if (r < 0.5f) c = Color.Lerp(new Color(0.22f, 0.14f, 0.08f), new Color(0.36f, 0.24f, 0.14f), Mathf.PerlinNoise(x * 0.4f, y * 0.4f) + v * 0.3f);
                            }
                            if (x < x0 + 2 || x >= x1 - 2 || y < 2 || y >= h - 2 || y == front) c = dark;
                            c.a = 1f;
                        }
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                pedSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return pedSprite;
            }
        }

        /// <summary>Vorderer Teil des Pults bis knapp unter die Lochmitte (inklusive Erde) – liegt vor der Pflanze.</summary>
        public static Sprite PedestalLipSprite
        {
            get
            {
                if (pedLip != null) return pedLip;
                var src = PedestalSprite.texture;
                int w = src.width, h = src.height;
                int front = Mathf.RoundToInt(PedH / CellSize * w), top = Mathf.RoundToInt(PedTop / CellSize * w);
                int cut = front + top / 2 - Mathf.RoundToInt(PedCut / CellSize * w);
                var px = src.GetPixels();
                for (int y = cut; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = Color.clear;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels(px); tex.Apply();
                pedLip = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                return pedLip;
            }
        }

        /// <summary>Nur die Platte – Vorschau im Pult-Modus.</summary>
        public static Sprite PedestalGhostSprite
        {
            get
            {
                if (pedGhost != null) return pedGhost;
                var s = PedestalSprite;
                int front = Mathf.RoundToInt(PedH / CellSize * 96), top = Mathf.RoundToInt(PedTop / CellSize * 96);
                pedGhost = Sprite.Create(s.texture, new Rect(0, front, 96, top), new Vector2(0.5f, 0.5f), 96);
                return pedGhost;
            }
        }
    }
}
