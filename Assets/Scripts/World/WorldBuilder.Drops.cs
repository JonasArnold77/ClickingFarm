using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Ernte faellt heraus – mit Gefuehl: Das Stueck ploppt aus der Pflanze (Stauchen und Strecken), fliegt im Bogen
    /// und dreht sich dabei, sein Schatten waechst beim Herunterfallen. Beim Aufprall knirscht es: Kruemel spritzen,
    /// ein Staubring, das Stueck quetscht sich platt, springt zweimal kleiner nach und wackelt sich zurecht. Erst dann
    /// liegt es im Haufen. Bei Ernte-Tipps des Spielers etwas lauter (Ton, leichtes Wackeln der Kamera).
    /// </summary>
    public partial class WorldBuilder
    {
        readonly Dictionary<PlantedState, int> dropsInFlight = new Dictionary<PlantedState, int>();
        readonly Dictionary<Sprite, Color> crumbCols = new Dictionary<Sprite, Color>();
        int dropsActive;
        const int MaxDrops = 40;   // mehr gleichzeitig wird unruhig – der Rest erscheint direkt im Haufen

        int InFlight(PlantedState p) => dropsInFlight.TryGetValue(p, out int n) ? n : 0;

        /// <summary>Die neu dazugekommenen Stuecke (die letzten add im Vorrat) aus der Pflanze fallen lassen.</summary>
        void LaunchDrops(PlantedState p, int add, bool loud)
        {
            if (add <= 0 || !plantViews.TryGetValue(new Cell(p.x, p.y), out var v) || v.sr == null || v.sr.sprite == null) return;
            var basePos = PlantBase(p);
            var from = basePos + new Vector2(0, Mathf.Max(0.12f, (v.sr.bounds.max.y - basePos.y) * 0.55f));
            int first = p.stock - add;
            for (int k = 0; k < Mathf.Min(add, 6); k++)
            {
                int idx = first + k;
                if (idx < 0 || idx >= PileShown || dropsActive >= MaxDrops) continue;
                dropsInFlight[p] = InFlight(p) + 1;
                dropsActive++;
                StartCoroutine(DropFly(p, idx, v.sr.sprite, from, k * 0.07f, loud));
            }
        }

        /// <summary>Kruemelfarbe: mittlere Farbe des Bildes (wenn lesbar), sonst ein warmer Erdton.</summary>
        Color CrumbColor(Sprite s)
        {
            if (crumbCols.TryGetValue(s, out var c)) return c;
            c = new Color(0.85f, 0.7f, 0.42f);
            try
            {
                if (s.texture != null && s.texture.isReadable)
                {
                    var r = s.textureRect; Color sum = Color.clear; int n = 0;
                    for (int y = 0; y < 6; y++)
                        for (int x = 0; x < 6; x++)
                        {
                            var px = s.texture.GetPixel((int)(r.x + r.width * (x + 0.5f) / 6f), (int)(r.y + r.height * (y + 0.5f) / 6f));
                            if (px.a > 0.5f) { sum += px; n++; }
                        }
                    if (n > 0) { c = sum / n; c.a = 1f; }
                }
            }
            catch { }
            crumbCols[s] = c;
            return c;
        }

        IEnumerator DropFly(PlantedState p, int idx, Sprite spr, Vector2 from, float delay, bool loud)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var target = ScatterPos(p, idx);
            var sr = new GameObject("Drop").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(objects, false);
            sr.sprite = spr;
            MedalAura.Set(sr, MedalOf(p), 1.4f);
            float baseS = 0.15f / Mathf.Max(0.01f, spr.bounds.size.x);
            float finalRot = (Hash(p, idx, 3) * 2f - 1f) * 35f;
            var shadow = Shadow(target, 0.16f, 0.05f, 0f);
            var crumb = CrumbColor(spr);
            int order = Order(Mathf.Min(from.y, target.y)) + 30;
            sr.sortingOrder = order;
            float spinDir = Random.value < 0.5f ? -1f : 1f;

            // 1) Plopp aus der Pflanze: schnell gross werden, dabei in die Hoehe gestreckt
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.08f)
            {
                float e = 1f - (1f - t) * (1f - t);
                sr.transform.position = from + Vector2.up * 0.06f * e;
                sr.transform.localScale = new Vector3(baseS * 1.1f * e * 0.8f, baseS * 1.1f * e * 1.35f, 1f);
                yield return null;
            }

            // 2) Flug im Bogen: dreht sich, streckt sich in Flugrichtung, Schatten waechst
            float dist = Vector2.Distance(from, target);
            float dur = 0.3f + dist * 0.12f, h = 0.42f + Random.value * 0.22f;
            float spin = spinDir * Random.Range(360f, 620f);
            var start = from + Vector2.up * 0.06f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                var pos = Vector2.Lerp(start, target, t) + Vector2.up * (4f * h * t * (1f - t));
                float speed = Mathf.Abs(1f - 2f * t);                       // schnell am Anfang und am Ende
                float st = 1f + 0.3f * speed;
                sr.transform.position = pos;
                sr.transform.rotation = Quaternion.Euler(0, 0, finalRot + spin * (1f - t));
                sr.transform.localScale = new Vector3(baseS * 1.1f / Mathf.Sqrt(st), baseS * 1.1f * st, 1f);
                if (shadow != null) { shadow.transform.localScale = new Vector3(0.16f * (0.4f + 0.6f * t), 0.05f * (0.4f + 0.6f * t), 1f); shadow.color = new Color(0.08f, 0.14f, 0.1f, 0.3f * t); }
                if (Random.value < 0.25f) Particles.I?.Spawn(pos, "spark", Color.Lerp(crumb, Color.white, 0.6f), Vector3.zero, 0.25f, 0.04f, 0f, 0f);
                yield return null;
            }
            sr.transform.position = target;
            sr.transform.rotation = Quaternion.Euler(0, 0, finalRot);

            // 3) Aufprall: es knirscht
            Crunch(p, target, crumb, loud, 1f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.08f)
            {
                float q = Mathf.Sin(t * Mathf.PI);
                sr.transform.localScale = new Vector3(baseS * (1f + 0.5f * q), baseS * (1f - 0.42f * q), 1f);
                yield return null;
            }

            // 4) zweimal kleiner nachspringen
            float[] hops = { 0.11f, 0.035f }, hopT = { 0.17f, 0.1f };
            for (int b = 0; b < hops.Length; b++)
            {
                float side = spinDir * (b == 0 ? 0.03f : 0.012f);
                var a0 = target;
                for (float t = 0f; t < 1f; t += Time.deltaTime / hopT[b])
                {
                    float y = 4f * hops[b] * t * (1f - t);
                    sr.transform.position = a0 + new Vector2(side * t, y);
                    float st = 1f + 0.18f * (1f - t) * (b == 0 ? 1f : 0.5f);
                    sr.transform.localScale = new Vector3(baseS / st, baseS * st, 1f);
                    sr.transform.rotation = Quaternion.Euler(0, 0, finalRot + spinDir * 18f * Mathf.Sin(t * Mathf.PI) * (b == 0 ? 1f : 0.4f));
                    yield return null;
                }
                target = a0 + new Vector2(side, 0f);
                if (b == 0) Crunch(p, target, crumb, false, 0.45f);
            }

            // 5) zurechtwackeln, dann uebernimmt der Haufen
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                float w = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.12f;
                sr.transform.localScale = new Vector3(baseS * (1f + w), baseS * (1f - w), 1f);
                yield return null;
            }
            Destroy(sr.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
            dropsActive = Mathf.Max(0, dropsActive - 1);
            if (dropsInFlight.TryGetValue(p, out int n)) { if (n <= 1) dropsInFlight.Remove(p); else dropsInFlight[p] = n - 1; }
        }

        /// <summary>Knirschender Aufprall: Kruemel, Staub, kleiner Ring, Ton – staerker bei Spieler-Tipps.</summary>
        void Crunch(PlantedState p, Vector2 at, Color crumb, bool loud, float k)
        {
            var P = Particles.I;
            P?.Burst(at + new Vector2(0, 0.02f), "square", crumb, Mathf.RoundToInt(7 * k), 2.2f * k, 0.45f, 0.045f, 9f, 1.6f * k);
            P?.Burst(at, "square", new Color(0.62f, 0.5f, 0.36f, 0.8f), Mathf.RoundToInt(4 * k), 1.2f * k, 0.35f, 0.035f, 7f, 0.8f);
            for (int i = 0; i < Mathf.RoundToInt(3 * k); i++)
                P?.Spawn(at + new Vector2(Random.Range(-0.08f, 0.08f), 0.02f), "leaf", new Color(0.85f, 0.78f, 0.62f, 0.45f), new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.1f, 0.35f), 0), 0.45f, 0.12f * k, 0.22f * k, 0f);
            if (k >= 1f) P?.Ring(at, new Color(1f, 0.95f, 0.8f, 0.55f), 0.32f, 0.22f);
            Sfx.Play("crunch", Random.Range(1.15f, 1.6f), (loud ? 0.45f : 0.18f) * k);
            if (loud && k >= 1f) { WorldCamera.I?.Shake(0.005f); Haptics.Tap(6); }
            PunchField(new Cell(p.x, p.y), 0.15f * k);
        }
    }
}
