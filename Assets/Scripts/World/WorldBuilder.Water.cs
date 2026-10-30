using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Giess-Effekt des Tauschilfs: Alle paar Sekunden schiesst aus der reifen Wasserpflanze eine kleine Fontaene
    /// (Cartoon FX "Water Fountain"), Wassertropfen fliegen im Bogen auf die Felder im Umkreis und platschen dort auf
    /// (Spritzer + zwei Wasserringe), das Feld glaenzt kurz nass. Ein Tipp auf das reife Tauschilf giesst sofort alle
    /// Felder im Umkreis mit einer grossen Fontaene. Ohne das Effekt-Paket laufen eigene Tropfen-Partikel.
    /// </summary>
    public partial class WorldBuilder
    {
        readonly Dictionary<PlantedState, float> waterTimers = new Dictionary<PlantedState, float>();
        readonly Dictionary<Cell, float> wetFlash = new Dictionary<Cell, float>();
        float waterTapCool;

        static readonly Color WaterCol = new Color(0.55f, 0.82f, 1f, 0.95f);
        static readonly Color DropCol = new Color(0.75f, 1.05f, 1.5f, 1f);   // HDR: leuchtet leicht im Bloom
        static Sprite dropSprite;
        static Material unlitMat;

        /// <summary>Unbeleuchtetes Sprite-Material, damit das Wasser auch im Abendlicht hell bleibt.</summary>
        static Material UnlitMat => unlitMat != null ? unlitMat : (unlitMat = new Material(Shader.Find("Sprites/Default")));

        /// <summary>Wassertropfen im Code gemalt: runder Bauch, Spitze oben, heller Glanzpunkt, dunklerer Rand.</summary>
        static Sprite DropSprite
        {
            get
            {
                if (dropSprite != null) return dropSprite;
                const int w = 32, h = 48;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                        // Bauch unten (Kreis), nach oben spitz zulaufend
                        float r = v < 0.4f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.4f - v) / 0.4f, 2f))) : Mathf.Lerp(1f, 0f, (v - 0.4f) / 0.6f);
                        float d = Mathf.Abs(u) - r * 0.95f;
                        float a = Mathf.Clamp01(-d * 10f);
                        if (a <= 0f) { px[y * w + x] = Color.clear; continue; }
                        var c = Color.Lerp(new Color(0.35f, 0.6f, 0.95f), Color.white, Mathf.Clamp01(1f + d * 4f) * 0.0f + 0.55f);
                        if (d > -0.12f) c = new Color(0.3f, 0.55f, 0.9f);   // Rand
                        float hx = u + 0.35f, hy = v - 0.32f;
                        if (hx * hx + hy * hy * 1.6f < 0.035f) c = Color.white;   // Glanzpunkt
                        c.a = a;
                        px[y * w + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                dropSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.35f), w);
                return dropSprite;
            }
        }

        int WaterRadius => 2 + (int)UpgradeAdd("water");

        void UpdateWatering(float dt)
        {
            waterTapCool -= dt;
            foreach (var p in save.planted)
            {
                if (!p.Grown || p.Role != "water") continue;
                if (!waterTimers.TryGetValue(p, out float t)) t = UnityEngine.Random.Range(0.5f, 3f);
                t -= dt;
                if (t <= 0f)
                {
                    WaterPulse(p, false);
                    t = UnityEngine.Random.Range(1.1f, 1.6f) / Mathf.Max(1f, 0.6f + 0.4f * p.Lv);
                }
                waterTimers[p] = t;
            }
            // nasser Glanz klingt ab (Grundfarbe setzt die Feld-Toenung in UpdateEconomy)
            foreach (var c in new List<Cell>(wetFlash.Keys))
            {
                float k = wetFlash[c] - dt * 1.6f;
                if (!fields.TryGetValue(c, out var sr) || sr == null || k <= 0f) { wetFlash.Remove(c); continue; }
                wetFlash[c] = k;
                sr.color = Color.Lerp(new Color(0.7f, 0.68f, 0.8f), new Color(0.6f, 0.75f, 1f), k);
            }
        }

        /// <summary>Felder (mit oder ohne Pflanze) und Mauer-Pflanzen im Umkreis der Wasserpflanze.</summary>
        List<Cell> WaterTargets(PlantedState w)
        {
            var list = new List<Cell>();
            int r = WaterRadius;
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var c = new Cell(w.x + dx, w.y + dy);
                    // nur dort giessen, wo es etwas bringt: wachsende Saat (gegossen = doppelt so schnell)
                    var t = PlantAt(c);
                    if (t != null && t != w && !t.Grown && t.x == c.x && t.y == c.y) list.Add(c);
                }
            return list;
        }

        /// <summary>Eine Giess-Runde: Fontaene und Tropfen auf ein paar (big: alle) Felder im Umkreis.</summary>
        void WaterPulse(PlantedState p, bool big)
        {
            if (big)
            {
                if (waterTapCool > 0f) return;
                waterTapCool = 0.35f;
            }
            var targets = WaterTargets(p);
            if (targets.Count == 0 && !big) return;   // nichts zu giessen: still bleiben
            var top = PlantBase(p) + new Vector2(0, CellSize * 0.75f);
            StartCoroutine(Fountain(top, big ? 0.22f : 0.14f, big ? 0.6f : 0.3f));
            Shockwave(PlantBase(p), big ? 1.4f : 0.8f, big ? 0.7f : 0.5f, new Color(0.6f, 0.85f, 1f, big ? 0.8f : 0.5f));
            if (big) for (int i = 0; i < 14; i++)
                Particles.I?.Spawn(top, "drop", WaterCol, new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(2.2f, 4f), 0), 0.7f, 0.09f, 0.05f, 8f);
            Sfx.Play("water", UnityEngine.Random.Range(big ? 0.8f : 1.1f, big ? 0.95f : 1.3f), big ? 0.55f : 0.22f);
            for (int i = targets.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (targets[i], targets[j]) = (targets[j], targets[i]); }
            int n = big ? targets.Count : Mathf.Min(targets.Count, 2 + p.Lv / 2);
            for (int i = 0; i < n; i++)
                StartCoroutine(Droplet(top, targets[i], UnityEngine.Random.Range(0f, big ? 0.25f : 0.15f) + i * 0.04f, big));
        }

        IEnumerator Fountain(Vector2 at, float scale, float keep)
        {
            var go = WorldFx.Play("waterFountain", at, scale, 30010, null, objects, 999f);
            if (go == null)
            {
                for (int i = 0; i < 10; i++)
                    Particles.I?.Spawn(at, "drop", WaterCol, new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), UnityEngine.Random.Range(2.5f, 3.5f), 0), 0.6f, 0.07f, 0.04f, 7f);
                yield break;
            }
            yield return new WaitForSeconds(keep);
            WorldFx.Release(go, 1.6f);
        }

        /// <summary>Ein Wassertropfen fliegt im Bogen von der Pflanze aufs Feld und platscht dort auf.</summary>
        IEnumerator Droplet(Vector2 from, Cell target, float delay, bool big)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var to = CellCenter(target) + new Vector2(UnityEngine.Random.Range(-0.08f, 0.08f), UnityEngine.Random.Range(-0.05f, 0.08f));
            var sr = new GameObject("WaterDrop").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = DropSprite;
            sr.sharedMaterial = UnlitMat;
            sr.color = DropCol;
            sr.sortingOrder = 30005;
            float size = (big ? 0.15f : 0.12f) / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            float dur = 0.22f + Vector2.Distance(from, to) * 0.08f;
            float h = 0.6f + Vector2.Distance(from, to) * 0.25f;
            Vector2 prev = from;
            for (float k = 0f; k < 1f; k += Time.deltaTime / dur)
            {
                var pos = Vector2.Lerp(from, to, k) + Vector2.up * (4f * h * k * (1f - k));
                var dir = pos - prev;
                prev = pos;
                sr.transform.position = pos;
                // Tropfen dreht sich in Flugrichtung und streckt sich mit dem Tempo
                // Spitze zeigt gegen die Flugrichtung, leicht gestreckt mit dem Tempo
                if (dir.sqrMagnitude > 1e-6f) sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
                sr.transform.localScale = new Vector3(size, size * (1f + Mathf.Min(0.5f, dir.magnitude * 6f)), 1f);
                if (UnityEngine.Random.value < 0.5f)
                    Particles.I?.Spawn(pos, "drop", new Color(WaterCol.r, WaterCol.g, WaterCol.b, 0.6f), Vector3.zero, 0.25f, 0.04f, 0.01f, 2f);
                yield return null;
            }
            Destroy(sr.gameObject);
            Splash(to, target, big);
        }

        void Splash(Vector2 at, Cell c, bool big)
        {
            // Spritzer: kleine Tropfen springen hoch und fallen zurueck
            for (int i = 0; i < (big ? 8 : 5); i++)
                Particles.I?.Spawn(at, "drop", WaterCol, new Vector3(UnityEngine.Random.Range(-1.1f, 1.1f), UnityEngine.Random.Range(1.2f, 2.4f), 0), 0.4f, 0.06f, 0.03f, 9f);
            // Wasserringe (die Ringe aus dem Effekt-Paket liegen in 3D flach und passen nicht zur Draufsicht)
            Shockwave(at, big ? 0.35f : 0.28f, 0.4f, new Color(0.65f, 0.88f, 1f, 0.7f));
            Shockwave(at, big ? 0.22f : 0.18f, 0.55f, new Color(0.65f, 0.88f, 1f, 0.45f));
            Sfx.Play("pop", UnityEngine.Random.Range(1.4f, 1.8f), big ? 0.25f : 0.12f);
            if (fields.ContainsKey(c)) wetFlash[c] = 1f;
        }
    }
}
