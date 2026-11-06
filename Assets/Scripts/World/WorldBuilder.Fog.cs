using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Nebel-Areale: Rund um den Hof ist das Land wie ein Schachbrett in quadratische Areale (10×10 Felder) geteilt, jedes
    /// unter eigenem, dichtem, weissem, wabernden Nebel (weiche Wolkenballen, die treiben, atmen und sich drehen; benachbarte
    /// Areale leicht unterschiedlich hell, damit man die Felder des Schachbretts erkennt). Der Platz um Haus und Laden ist frei.
    /// Nachts kommen die Zombies aus den Nebel-Arealen am Hof (FogSpawnPoint).
    /// Den Hof vergroessert man Areal fuer Areal mit einer Sonnenlilie (Rolle "fogclear"): sehr teuer, nur eine zur Zeit.
    /// Ist sie reif, zieht man sie auf ein Nebel-Areal, das an den Hof grenzt (beim Tragen leuchten diese gruen), oder
    /// drueckt im Pflanzen-Fenster "Nebel lichten" (dann das naechste Areal an der Lilie). Ihr Licht lichtet dort den Nebel,
    /// das Areal gehoert zum Hof, die Lilie ist verbraucht. Preise in plants.json -> farmLevels und fog.
    /// </summary>
    public partial class WorldBuilder
    {
        class FogPuff { public SpriteRenderer sr; public Vector2Int area; public Vector2 home; public float size, phase, alpha, fade; public bool dying; public Vector2 outDir; }
        readonly List<FogPuff> fogPuffs = new List<FogPuff>();
        readonly HashSet<Vector2Int> fogDrawn = new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> openAreas = new HashSet<Vector2Int>();
        static Sprite fogSprite;
        float fogCheckT;
        const int FogOrder = 30500;   // ueber Baeumen, Steinen und Zombies (die kommen aus dem Nebel), unter der Oberflaeche

        // Raster der Areale: 10×10 Felder, Ursprung so, dass der Start-Hof genau 2×1 Areale ist (20×10 Felder, mittig unter dem Haus)
        const int FogAreaCells = 10, AreaOX = -10, AreaOY = -6;
        const int AreaMinX = -2, AreaMaxX = 3, AreaMinY = -3, AreaMaxY = 0;   // Areale, die man lichten kann (darueber: Haus)

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
        public static Vector2Int AreaOf(Cell c) => new Vector2Int(FloorDiv(c.x - AreaOX, FogAreaCells), FloorDiv(c.y - AreaOY, FogAreaCells));
        public static Vector2Int AreaAt(Vector2 w) => AreaOf(CellAt(w));

        /// <summary>Welt-Rechteck eines Areals.</summary>
        public static Rect AreaRect(Vector2Int a)
        {
            float x0 = (AreaOX + a.x * FogAreaCells) * CellSize, y0 = (AreaOY + a.y * FogAreaCells) * CellSize, s = FogAreaCells * CellSize;
            return new Rect(x0, y0, s, s);
        }

        static bool InAreaRange(Vector2Int a) => a.x >= AreaMinX && a.x <= AreaMaxX && a.y >= AreaMinY && a.y <= AreaMaxY;
        static bool StartArea_(Vector2Int a) => a.x >= 0 && a.x <= 1 && a.y == 0;

        /// <summary>Gehoert das Areal zum Hof (oder liegt es oben beim Haus, wo nie Nebel ist)?</summary>
        public bool IsAreaOpen(Vector2Int a)
        {
            EnsureAreas();
            return a.y > AreaMaxY || StartArea_(a) || openAreas.Contains(a);
        }

        /// <summary>Kann dieses Areal jetzt gelichtet werden? Im Nebel, im Raster und neben einem Hof-Areal.</summary>
        public bool AreaClearable(Vector2Int a)
        {
            if (!InAreaRange(a) || IsAreaOpen(a)) return false;
            foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                var n = a + d;
                if (n.y <= AreaMaxY && IsAreaOpen(n)) return true;
            }
            return false;
        }

        /// <summary>Alte Spielstaende: alles, was der Hof schon war oder wo schon etwas steht, wird offen.</summary>
        void EnsureAreas()
        {
            if (save.areasInit && save.areaSize != FogAreaCells)
            {
                // Spielstand mit anderer Areal-Groesse (frueher 5×5 bzw. 2×2): jedes offene Areal in die neuen Areale umrechnen
                // (ein neues Areal ist offen, sobald ein altes offenes darin liegt); fruehere Raster begannen bei Feld (-8, -6)
                int oldSize = save.areaSize > 0 ? save.areaSize : 5;
                const int oldOX = -8, oldOY = -6;
                var olds = new List<Vector2Int>(save.openAreas);
                save.openAreas.Clear(); openAreas.Clear();
                save.areaSize = FogAreaCells;
                foreach (var oa in olds)
                    for (int x = oldOX + oa.x * oldSize; x < oldOX + (oa.x + 1) * oldSize; x++)
                        for (int y = oldOY + oa.y * oldSize; y < oldOY + (oa.y + 1) * oldSize; y++)
                        {
                            var na = AreaOf(new Cell(x, y));
                            if (na.y > AreaMaxY || StartArea_(na) || openAreas.Contains(na)) continue;
                            openAreas.Add(na); save.openAreas.Add(na);
                        }
                save.Save();
            }
            if (save.areasInit) { if (openAreas.Count == 0) foreach (var a in save.openAreas) openAreas.Add(a); return; }
            save.areaSize = FogAreaCells;
            save.areasInit = true;
            var old = save.farmSides ? UnityEngine.Rect.MinMaxRect(save.farmX0, save.farmY0, save.farmX1, FarmRect(0).yMax) : FarmRect(FarmLevel);
            for (int ax = AreaMinX; ax <= AreaMaxX; ax++)
                for (int ay = AreaMinY; ay <= AreaMaxY; ay++)
                {
                    var a = new Vector2Int(ax, ay);
                    var r = AreaRect(a);
                    if (r.xMax > old.xMin + 0.05f && r.xMin < old.xMax - 0.05f && r.yMax > old.yMin + 0.05f && r.yMin < old.yMax - 0.05f) Open(a);
                }
            foreach (var c in save.fields) Open(AreaOf(c));
            foreach (var p in save.planted) Open(AreaOf(new Cell(p.x, p.y)));
            foreach (var w in save.walls) Open(AreaOf(new Cell(w.x, w.y)));
            foreach (var st in save.structures) if (st.placed) Open(AreaOf(new Cell(st.x, st.y)));
            save.areasOpened = Mathf.Max(save.areasOpened, FarmLevel);   // bisherige Ausbauten zaehlen fuer den Preis
            void Open(Vector2Int a)
            {
                if (a.y > AreaMaxY || StartArea_(a) || openAreas.Contains(a)) return;
                openAreas.Add(a); save.openAreas.Add(a);
            }
        }

        /// <summary>Areal zum Hof nehmen (ohne Kosten) – der Nebel dort loest sich auf.</summary>
        void OpenArea(Vector2Int a)
        {
            EnsureAreas();
            if (IsAreaOpen(a)) return;
            openAreas.Add(a); save.openAreas.Add(a);
            save.areasOpened++;
            if (save.farmLevel < MaxFarmLevel) save.farmLevel++;
            var c = AreaRect(a).center;
            foreach (var f in fogPuffs)
                if (!f.dying && f.area == a) { f.dying = true; f.outDir = (f.home - c).normalized + (c - (Vector2)HousePos).normalized * 0.6f; }
            RefreshGhosts();
            save.Save();
        }

        /// <summary>Wo ein Zombie aus dem Nebel tritt: irgendwo in einem Nebel-Areal, das an den Hof grenzt.</summary>
        public Vector2 FogSpawnPoint(System.Random rnd)
        {
            var edge = new List<Vector2Int>();
            for (int ax = AreaMinX; ax <= AreaMaxX; ax++)
                for (int ay = AreaMinY; ay <= AreaMaxY; ay++) { var a = new Vector2Int(ax, ay); if (AreaClearable(a)) edge.Add(a); }
            float hw = MapW / 2f - 0.3f, hh = MapH / 2f - 0.3f;
            if (edge.Count == 0) return new Vector2((float)(rnd.NextDouble() * 2 - 1) * hw, -hh);
            var r = AreaRect(edge[rnd.Next(edge.Count)]);
            var p = new Vector2(Mathf.Lerp(r.xMin + 0.4f, r.xMax - 0.4f, (float)rnd.NextDouble()), Mathf.Lerp(r.yMin + 0.4f, r.yMax - 0.4f, (float)rnd.NextDouble()));
            return new Vector2(Mathf.Clamp(p.x, -hw, hw), Mathf.Clamp(p.y, -hh, hh));
        }

        // ------------------------------------------------------------------ Aufbau

        /// <summary>Nebel fuer jedes Areal ausserhalb des Hofs (auch ein Ring ueber das Raster hinaus bis zum Kartenrand).</summary>
        void RebuildFog(bool fadeIn)
        {
            EnsureAreas();
            for (int ax = AreaMinX - 2; ax <= AreaMaxX + 2; ax++)
                for (int ay = AreaMinY - 2; ay <= AreaMaxY; ay++)
                {
                    var a = new Vector2Int(ax, ay);
                    if (IsAreaOpen(a) || fogDrawn.Contains(a)) continue;
                    fogDrawn.Add(a);
                    AddAreaFog(a, fadeIn);
                }
        }

        void AddAreaFog(Vector2Int a, bool fadeIn)
        {
            var rnd = new System.Random(4711 + a.x * 7919 + a.y * 104729 + save.mapSeed);
            var r = AreaRect(a);
            bool light = ((a.x + a.y) & 1) == 0;   // Schachbrett: jedes zweite Areal etwas heller
            float alpha = light ? 0.86f : 0.95f;
            // 3×3 Ballen je Areal (10×10 Felder), dicht – an den Raendern zeigen leichte Naehte das Schachbrett
            var spots = new List<Vector2>();
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) spots.Add(new Vector2((i + 0.5f) / 3f, (j + 0.5f) / 3f));
            foreach (var s in spots)
            {
                var home = new Vector2(r.x + r.width * (s.x + (float)(rnd.NextDouble() - 0.5) * 0.08f), r.y + r.height * (s.y + (float)(rnd.NextDouble() - 0.5) * 0.08f));
                var sr = new GameObject("Fog").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(objects, false);
                sr.sprite = FogSprite;
                sr.sortingOrder = FogOrder;
                var f = new FogPuff
                {
                    sr = sr, area = a, home = home, size = r.width * (0.56f + (float)rnd.NextDouble() * 0.06f),
                    phase = (float)rnd.NextDouble() * 50f, alpha = alpha, fade = fadeIn ? 0f : 1f
                };
                // gleich an seinen Platz – sonst haengt er bis zum ersten Blick in der Kartenmitte
                sr.transform.position = home;
                float sc = f.size / FogSprite.bounds.size.x;
                sr.transform.localScale = new Vector3(sc, sc, 1f);
                sr.color = new Color(0.96f, 0.97f, 1f, alpha * f.fade);
                fogPuffs.Add(f);
            }
        }

        // ------------------------------------------------------------------ Bewegung, Hervorheben und Sonnenlilie

        void UpdateFog(float dt)
        {
            if (fogPuffs.Count == 0) RebuildFog(false);
            float t = Time.time;
            var view = ViewRect(3f);
            // traegt man eine reife Sonnenlilie, leuchten die Areale golden, die man jetzt lichten kann
            bool lily = carrying != null && carrying.Grown && carrying.Role == "fogclear";
            Vector2Int under = lily ? AreaAt(dragPos) : new Vector2Int(int.MinValue, 0);
            for (int i = fogPuffs.Count - 1; i >= 0; i--)
            {
                var f = fogPuffs[i];
                if (f.dying)
                {
                    f.fade -= dt * 0.45f;
                    f.home += f.outDir * dt * 1.4f;
                    if (f.fade <= 0f) { Destroy(f.sr.gameObject); fogPuffs.RemoveAt(i); continue; }
                }
                else if (f.fade < 1f) f.fade = Mathf.Min(1f, f.fade + dt * 0.5f);
                if (!view.Contains(f.home)) continue;   // nur was man sieht bewegt sich
                // langsam treiben, atmen, drehen (im eigenen Areal)
                var off = new Vector2(Mathf.Sin(t * 0.23f + f.phase) * 0.25f, Mathf.Cos(t * 0.19f + f.phase * 1.3f) * 0.18f);
                f.sr.transform.position = f.home + off;
                float s = f.size * (1f + 0.06f * Mathf.Sin(t * 0.37f + f.phase * 0.7f)) / FogSprite.bounds.size.x;
                f.sr.transform.localScale = new Vector3(s, s, 1f);
                f.sr.transform.rotation = Quaternion.Euler(0, 0, f.phase * 30f + t * (3f + f.phase % 4f));
                var col = new Color(0.96f, 0.97f, 1f);
                if (lily && !f.dying && AreaClearable(f.area))
                {
                    float glow = f.area == under ? 0.75f : 0.35f + 0.15f * Mathf.Sin(t * 4f);
                    col = Color.Lerp(col, new Color(0.35f, 1f, 0.45f), glow);   // gruen wie alle Ablageziele
                }
                col.a = f.alpha * f.fade * (0.9f + 0.1f * Mathf.Sin(t * 0.5f + f.phase));
                f.sr.color = col;
            }

            // reife Sonnenlilie: einmal Bescheid sagen, wie man sie anwendet
            fogCheckT -= dt;
            if (fogCheckT > 0f) return;
            fogCheckT = 0.5f;
            foreach (var p in save.planted)
                if (p.Grown && p.Role == "fogclear" && fogLilyTold != p)
                {
                    fogLilyTold = p;
                    Floater?.Invoke(PlantBase(p) + new Vector2(0, 1.1f), "Sonnenlilie bereit! Auf ein Nebel-Feld am Hof ziehen – oder antippen", new Color(1f, 0.95f, 0.7f));
                    Sfx.Play("unlock", 1.3f, 0.7f);
                }
        }
        PlantedState fogLilyTold;

        /// <summary>Reife Sonnenlilie auf ein Areal anwenden: Nebel lichten, das Areal gehoert zum Hof.</summary>
        public bool UseFogLily(PlantedState p, Vector2Int area)
        {
            if (p == null || !p.Grown || p.Role != "fogclear") return false;
            var at = PlantBase(p) + new Vector2(0, 0.4f);
            if (!AreaClearable(area))
            {
                Floater?.Invoke(at + new Vector2(0, 0.6f), IsAreaOpen(area) ? "Dort ist kein Nebel" : "Nur Nebel direkt am Hof lässt sich lichten", new Color(1f, 0.75f, 0.55f));
                Sfx.Play("error", 1f, 0.5f);
                return false;
            }
            RemovePlantView(new Cell(p.x, p.y));
            save.planted.Remove(p); DirtyPlants();
            OpenArea(area);
            var c = AreaRect(area).center;
            Shockwave(c, 5f, 1.2f, new Color(1f, 0.95f, 0.7f, 0.9f));
            Particles.I?.Burst(c, "star", new Color(1f, 0.92f, 0.55f), 40, 4f, 1.2f, 0.35f, -0.4f, 1.4f);
            Floater?.Invoke(c + new Vector2(0, 0.8f), "Der Nebel lichtet sich!", new Color(1f, 0.95f, 0.7f));
            Sfx.Play("levelup", 0.9f, 0.8f);
            Haptics.Tap(40);
            WorldPostFX.Punch(0.6f);
            NotifyInventory();
            save.Save();
            return true;
        }

        /// <summary>Knopf im Pflanzen-Fenster: das Nebel-Areal am Hof, das der Lilie am naechsten liegt.</summary>
        public bool UseFogLilyNearest(PlantedState p)
        {
            if (p == null) return false;
            var at = PlantBase(p);
            Vector2Int best = default; float bd = float.MaxValue; bool found = false;
            for (int ax = AreaMinX; ax <= AreaMaxX; ax++)
                for (int ay = AreaMinY; ay <= AreaMaxY; ay++)
                {
                    var a = new Vector2Int(ax, ay);
                    if (!AreaClearable(a)) continue;
                    float d = (AreaRect(a).center - at).sqrMagnitude;
                    if (d < bd) { bd = d; best = a; found = true; }
                }
            if (!found) { Floater?.Invoke(at + new Vector2(0, 1f), "Kein Nebel mehr zu lichten", new Color(1f, 0.75f, 0.55f)); return false; }
            return UseFogLily(p, best);
        }

        /// <summary>Hof um ein Areal vergroessern (ohne Lilie, z. B. alter Laden-Weg): das naechste am Start-Hof.</summary>
        public void ExpandFarm()
        {
            Vector2Int best = default; float bd = float.MaxValue; bool found = false;
            var home = AreaRect(new Vector2Int(1, 0)).center;
            for (int ax = AreaMinX; ax <= AreaMaxX; ax++)
                for (int ay = AreaMinY; ay <= AreaMaxY; ay++)
                {
                    var a = new Vector2Int(ax, ay);
                    if (!AreaClearable(a)) continue;
                    float d = (AreaRect(a).center - home).sqrMagnitude;
                    if (d < bd) { bd = d; best = a; found = true; }
                }
            if (found) OpenArea(best);
        }

        // ------------------------------------------------------------------ Preis

        /// <summary>Preis des naechsten Areals (10×10 Felder): fog.areaBase × fog.growth^(gelichtete Areale), dazu etwas Holz.</summary>
        public FarmLevelDef NextAreaCost()
        {
            var F = PlantBalance.I.Fog;
            int n = save.areasOpened;
            float g = Mathf.Pow(F.growth, n);
            return new FarmLevelDef { cost = Mathf.Round(F.areaBase * g), wood = Mathf.CeilToInt(F.areaWood * Mathf.Sqrt(g)), crops = new string[0] };
        }

        bool AnyAreaClearable()
        {
            for (int ax = AreaMinX; ax <= AreaMaxX; ax++)
                for (int ay = AreaMinY; ay <= AreaMaxY; ay++) if (AreaClearable(new Vector2Int(ax, ay))) return true;
            return false;
        }

        /// <summary>Preis der Sonnenlilie: ein Vielfaches des naechsten Areals.</summary>
        float FogPlantCost() { if (FreeBuild) return 0f; var n = NextAreaCost(); return n == null ? float.MaxValue : n.cost * PlantBalance.I.Fog.costMult; }   // Testmodus "Alle Gebaeude gratis": gratis

        /// <summary>Darf jetzt eine Sonnenlilie gepflanzt werden? Prueft Nebel, Holz und Ernte (Muenzen prueft das Pflanzen).</summary>
        bool CanPlantFog(Cell c)
        {
            var at = CellCenter(c) + new Vector2(0, 0.7f);
            var next = NextAreaCost();
            string why = next == null || !AnyAreaClearable() ? "Kein Nebel mehr zu lichten"
                : save.planted.Exists(q => q.Role == "fogclear") ? "Es wächst schon eine Sonnenlilie"
                : !FreeBuild && save.wood < next.wood ? "Die Sonnenlilie braucht " + next.wood + " Holz"
                : !FreeBuild && !HasCrops(CropCost(next.crops)) ? "Die Sonnenlilie braucht " + CropCostText(CropCost(next.crops))
                : null;
            if (why == null) return true;
            Floater?.Invoke(at, why, new Color(1f, 0.75f, 0.55f));
            Sfx.Play("error", 1f, 0.5f);
            return false;
        }

        /// <summary>Holz und Ernte fuer die Sonnenlilie abziehen (nach dem Bezahlen).</summary>
        void PayFogExtras()
        {
            var next = NextAreaCost();
            if (next == null || FreeBuild) return;
            TakeCrops(CropCost(next.crops));
            save.wood -= next.wood; WoodChanged?.Invoke(save.wood);
        }

        /// <summary>Weicher Wolkenballen: mehrere ueberlagerte weiche Flecken, zum Rand hin auslaufend.</summary>
        static Sprite FogSprite
        {
            get
            {
                if (fogSprite != null) return fogSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                var rnd = new System.Random(99);
                var blobs = new List<Vector3>();
                for (int i = 0; i < 7; i++)
                    blobs.Add(new Vector3((float)(rnd.NextDouble() - 0.5) * 0.5f, (float)(rnd.NextDouble() - 0.5) * 0.4f, 0.18f + (float)rnd.NextDouble() * 0.14f));
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = (x + 0.5f) / n - 0.5f, fy = (y + 0.5f) / n - 0.5f;
                        float a = 0f;
                        foreach (var bl in blobs)
                        {
                            float dx = fx - bl.x, dy = fy - bl.y;
                            a = Mathf.Max(a, Mathf.Exp(-(dx * dx + dy * dy) / (2f * bl.z * bl.z)));
                        }
                        float edge = Mathf.Clamp01((0.5f - Mathf.Sqrt(fx * fx + fy * fy)) * 5f);   // nie ein harter Rand
                        px[y * n + x] = new Color(1f, 1f, 1f, a * edge);
                    }
                tex.SetPixels(px); tex.Apply();
                return fogSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }
    }
}
