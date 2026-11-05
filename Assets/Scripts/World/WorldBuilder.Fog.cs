using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Nebel als Grenze des Hofs: Rund um den Hof der aktuellen Ausbaustufe liegt dichter, weisser, wabernder Nebel
    /// (viele weiche Wolkenballen, die langsam treiben, atmen und sich drehen). Der Platz um Haus und Laden bleibt frei.
    /// Nachts kommen die Zombies aus dem Nebel (NightWave fragt FogSpawnPoint).
    /// Den Hof vergroessert man mit einer Sonnenlilie (Rolle "fogclear"): sehr teuer, nur eine zur Zeit; ist sie reif,
    /// lichtet ihr Licht den Nebel – der Hof waechst um eine Stufe, der Nebel weicht zurueck, die Lilie ist verbraucht.
    /// Werte in plants.json -> fog.
    /// </summary>
    public partial class WorldBuilder
    {
        class FogPuff { public SpriteRenderer sr; public Vector2 home; public float size, phase, alpha, fade; public bool dying; public Vector2 outDir; }
        readonly List<FogPuff> fogPuffs = new List<FogPuff>();
        readonly HashSet<Vector2Int> fogKeys = new HashSet<Vector2Int>();
        static Sprite fogSprite;
        float fogCheckT;
        const int FogOrder = 30500;   // ueber Baeumen, Steinen und Zombies (die kommen aus dem Nebel), unter der Oberflaeche

        /// <summary>Nebelfreier Hof: die Flaeche der aktuellen Ausbaustufe mit etwas Rand.</summary>
        public Rect FogClearRect
        {
            get
            {
                var r = FarmRect(FarmLevel);
                return UnityEngine.Rect.MinMaxRect(r.xMin - 0.3f, r.yMin - 0.3f, r.xMax + 0.3f, r.yMax + 0.3f);
            }
        }

        /// <summary>Platz um Haus und Laden (oben) – dort ist nie Nebel.</summary>
        static Rect HouseZone => UnityEngine.Rect.MinMaxRect(-7.8f, 2.0f, 7.8f, MapH / 2f + 4f);

        static float RectDist(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Abstand zum nebelfreien Bereich (0 = drin).</summary>
        float FogDist(Vector2 p) => Mathf.Min(RectDist(p, FogClearRect), RectDist(p, HouseZone));

        /// <summary>Wo ein Zombie aus dem Nebel tritt: knapp hinter dem Nebelrand links, rechts oder unten.</summary>
        public Vector2 FogSpawnPoint(System.Random rnd)
        {
            var r = FogClearRect;
            float hw = MapW / 2f - 0.3f, hh = MapH / 2f - 0.3f;
            int side = rnd.Next(4);
            float u = (float)rnd.NextDouble();
            Vector2 p = side == 0 ? new Vector2(r.xMin - 1.1f, Mathf.Lerp(r.yMin, r.yMax, u))
                      : side == 1 ? new Vector2(r.xMax + 1.1f, Mathf.Lerp(r.yMin, r.yMax, u))
                      : new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), r.yMin - 1.1f);
            return new Vector2(Mathf.Clamp(p.x, -hw, hw), Mathf.Clamp(p.y, -hh, hh));
        }

        // ------------------------------------------------------------------ Aufbau

        /// <summary>Nebel an die aktuelle Hofgrenze anpassen: Ballen im neuen Hof loesen sich auf, am neuen Rand kommen neue dazu.</summary>
        void RebuildFog(bool fadeIn)
        {
            // Ballen, die jetzt im freien Hof liegen, treiben nach aussen und loesen sich auf
            var c = FogClearRect.center;
            foreach (var f in fogPuffs)
            {
                if (f.dying || FogDist(f.home) > 0.15f) continue;
                f.dying = true;
                f.outDir = (f.home - c).normalized;
            }
            float hw = MapW / 2f + 2f, hh = MapH / 2f + 2f;
            // Randband: dicht, kleinere Ballen; weiter draussen: grosse Ballen, lockerer verteilt
            AddFogGrid(0.75f, 0f, 2.6f, 1.5f, 2.3f, 0.82f, 0, hw, hh, fadeIn);
            AddFogGrid(2.1f, 2.6f, 99f, 3.4f, 4.4f, 0.93f, 1, hw, hh, fadeIn);
        }

        void AddFogGrid(float step, float dMin, float dMax, float sMin, float sMax, float alpha, int layer, float hw, float hh, bool fadeIn)
        {
            var rnd = new System.Random(4711 + layer * 977 + save.mapSeed);
            int nx = Mathf.CeilToInt(hw * 2f / step), ny = Mathf.CeilToInt(hh * 2f / step);
            for (int ix = 0; ix <= nx; ix++)
                for (int iy = 0; iy <= ny; iy++)
                {
                    // zufaellig verschoben, damit kein Raster zu sehen ist (fest je Karte)
                    var home = new Vector2(-hw + ix * step + (float)(rnd.NextDouble() - 0.5) * step * 0.8f,
                                           -hh + iy * step + (float)(rnd.NextDouble() - 0.5) * step * 0.8f);
                    float size = Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
                    float phase = (float)rnd.NextDouble() * 50f;
                    var key = new Vector2Int(ix * 4 + layer, iy);
                    if (fogKeys.Contains(key)) continue;
                    float d = FogDist(home);
                    if (d <= Mathf.Max(dMin, 0.02f) || d > dMax) continue;   // nie im freien Hof
                    fogKeys.Add(key);
                    var sr = new GameObject("Fog").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(objects, false);
                    sr.sprite = FogSprite;
                    sr.sortingOrder = FogOrder + layer;
                    fogPuffs.Add(new FogPuff { sr = sr, home = home, size = size, phase = phase, alpha = alpha, fade = fadeIn ? 0f : 1f });
                }
        }

        // ------------------------------------------------------------------ Bewegung und Sonnenlilie

        void UpdateFog(float dt)
        {
            if (fogPuffs.Count == 0) RebuildFog(false);
            float t = Time.time;
            var view = ViewRect(3f);
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
                // langsam treiben, atmen, drehen
                var off = new Vector2(Mathf.Sin(t * 0.23f + f.phase) * 0.35f, Mathf.Cos(t * 0.19f + f.phase * 1.3f) * 0.22f);
                f.sr.transform.position = f.home + off;
                float s = f.size * (1f + 0.07f * Mathf.Sin(t * 0.37f + f.phase * 0.7f)) / FogSprite.bounds.size.x;
                f.sr.transform.localScale = new Vector3(s, s, 1f);
                f.sr.transform.rotation = Quaternion.Euler(0, 0, f.phase * 30f + t * (3f + f.phase % 4f));
                f.sr.color = new Color(0.96f, 0.97f, 1f, f.alpha * f.fade * (0.88f + 0.12f * Mathf.Sin(t * 0.5f + f.phase)));
            }

            // reife Sonnenlilie: Nebel lichten
            fogCheckT -= dt;
            if (fogCheckT > 0f) return;
            fogCheckT = 0.4f;
            foreach (var p in save.planted)
                if (p.Grown && p.Role == "fogclear") { ClearFogWith(p); break; }
        }

        void ClearFogWith(PlantedState p)
        {
            var at = PlantBase(p) + new Vector2(0, 0.4f);
            RemovePlantView(new Cell(p.x, p.y));
            save.planted.Remove(p); DirtyPlants();
            ExpandFarm();   // groesserer Hof, der Nebel weicht (RebuildFog in ExpandFarm)
            Shockwave(at, 6f, 1.2f, new Color(1f, 0.95f, 0.7f, 0.9f));
            Particles.I?.Burst(at, "star", new Color(1f, 0.92f, 0.55f), 40, 4f, 1.2f, 0.35f, -0.4f, 1.4f);
            Floater?.Invoke(at + new Vector2(0, 0.8f), "Der Nebel lichtet sich!", new Color(1f, 0.95f, 0.7f));
            NotifyInventory();
            save.Save();
        }

        /// <summary>Preis der Sonnenlilie: ein Vielfaches der naechsten Ausbaustufe (unendlich, wenn der Hof voll ist).</summary>
        float FogPlantCost() { var n = NextFarmLevel(); return n == null ? float.MaxValue : n.cost * PlantBalance.I.Fog.costMult; }

        /// <summary>Darf jetzt eine Sonnenlilie gepflanzt werden? Prueft Hof, Holz und Ernte (Muenzen prueft das Pflanzen).</summary>
        bool CanPlantFog(Cell c)
        {
            var at = CellCenter(c) + new Vector2(0, 0.7f);
            var next = NextFarmLevel();
            string why = next == null ? "Der Hof ist voll ausgebaut – kein Nebel mehr zu lichten"
                : save.planted.Exists(q => q.Role == "fogclear") ? "Es wächst schon eine Sonnenlilie"
                : save.wood < next.wood ? "Die Sonnenlilie braucht " + next.wood + " Holz"
                : !HasCrops(CropCost(next.crops)) ? "Die Sonnenlilie braucht " + CropCostText(CropCost(next.crops))
                : null;
            if (why == null) return true;
            Floater?.Invoke(at, why, new Color(1f, 0.75f, 0.55f));
            Sfx.Play("error", 1f, 0.5f);
            return false;
        }

        /// <summary>Holz und Ernte fuer die Sonnenlilie abziehen (nach dem Bezahlen).</summary>
        void PayFogExtras()
        {
            var next = NextFarmLevel();
            if (next == null) return;
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
