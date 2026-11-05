using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Feste Comic-Linie in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Klassische Sprite-Kontur: Unter dem Bild liegen einfarbige Kopien seiner Form, rundherum um eine feste Strecke
    /// versetzt (Pflanze 16 Richtungen, Erntestueck 8). Zusammen ergeben sie eine gleichmaessig dicke, glatte Linie,
    /// die sich genau an die Aussenlinie legt – egal wie gross das Bild ist.
    /// Einfarbig fuellt der eingebaute Shader "GUI/Text Shader" (Farbe vom SpriteRenderer, Form aus dem Alpha des Bildes).
    /// Dazu bei der Pflanze selbst (nicht bei den kleinen Erntestuecken) ein leichtes Strahlen wie bei verzauberten Waffen:
    /// ein weicher, pulsierender Schein aus vergroesserten, durchsichtigen Silhouetten hinter der Linie und leuchtende
    /// Funken, die langsam aufsteigen. Gold strahlt am staerksten.
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host;
        readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> glow = new List<SpriteRenderer>();   // Schein: vergroesserte Silhouetten
        static readonly float[] GlowScale = { 1.05f, 1.11f, 1.18f };
        static readonly float[] GlowAlpha = { 0.32f, 0.17f, 0.08f };
        float moteT;
        int level, dirs = 16;
        float width = 0.0175f;   // Linienbreite in Welt-Einheiten
        (Sprite, int, int, bool, int, int) lastKey;
        bool shown;
        static Material silhouetteMat;
        static bool shaderLooked;

        /// <summary>Stufe setzen (unter 2 = keine Linie). width: Linienbreite in Welt-Einheiten, dirs: Zahl der Kopien
        /// (mehr = glatter; fuer viele kleine Erntestuecke reichen 8).</summary>
        public static void Set(SpriteRenderer host, int level, float width = 0.0175f, int dirs = 16)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
            }
            a.level = level; a.width = width; a.dirs = Mathf.Clamp(dirs, 4, 24);
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2 && Mat != null;
            if (!on) { if (shown) { Show(0); shown = false; } return; }
            bool big = dirs >= 16;   // die Pflanze selbst strahlt, die kleinen Erntestuecke nicht
            if (big) UpdateGlow();
            while (copies.Count < dirs) copies.Add(Make());
            if (!shown) Show(dirs);

            // nur neu setzen, wenn sich Bild, Stufe, Reihenfolge, Spiegelung oder Groesse geaendert haben
            var ls0 = host.transform.lossyScale;
            var key = (host.sprite, level, host.sortingOrder, host.flipX, Mathf.RoundToInt(ls0.x * 200f), Mathf.RoundToInt(ls0.y * 200f));
            if (shown && key.Equals(lastKey)) return;
            lastKey = key; shown = true;
            var c = WorldBuilder.MedalColor(level);
            var col = new Color(c.r, c.g, c.b, 1f);
            // feste Breite in der Welt, umgerechnet in die (skalierte) Einheit des Bildes
            var ls = host.transform.lossyScale;
            float rx = width / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), ry = width / Mathf.Max(0.0001f, Mathf.Abs(ls.y));
            for (int i = 0; i < dirs; i++)
            {
                var sr = copies[i];
                float ang = i * Mathf.PI * 2f / dirs;
                sr.sprite = host.sprite;
                sr.flipX = host.flipX; sr.flipY = host.flipY;
                sr.transform.localPosition = new Vector3(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry, 0f);
                sr.color = col;
                sr.sortingLayerID = host.sortingLayerID;
                sr.sortingOrder = host.sortingOrder - 1;   // unter dem Bild: nur der Rand ragt heraus
            }
        }

        void Show(int n)
        {
            for (int i = 0; i < copies.Count; i++) if (copies[i] != null) copies[i].enabled = i < n;
            if (n == 0) foreach (var g in glow) if (g != null) g.enabled = false;
        }

        /// <summary>Weicher Schein (pulsiert) und aufsteigende Funken – nur solange die Pflanze im Bild ist.</summary>
        void UpdateGlow()
        {
            while (glow.Count < GlowScale.Length) glow.Add(Make());
            if (!host.isVisible && glow[0].enabled) return;   // ausserhalb des Bilds nichts bewegen (grosse Hoefe)
            var c = WorldBuilder.MedalColor(level);
            float power = level >= 4 ? 1.3f : level == 3 ? 1f : 0.8f;
            float t = Time.time * 2.2f + transform.position.x * 1.7f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t);
            // um die Mitte des Bildes vergroessern (der Drehpunkt liegt unten)
            Vector2 center = host.sprite.bounds.center;
            for (int i = 0; i < glow.Count; i++)
            {
                var g = glow[i];
                g.enabled = true;
                g.sprite = host.sprite;
                g.flipX = host.flipX; g.flipY = host.flipY;
                float s = GlowScale[i] * (1f + 0.02f * Mathf.Sin(t * 0.7f + i));
                g.transform.localScale = new Vector3(s, s, 1f);
                g.transform.localPosition = new Vector3(-center.x * (s - 1f), -center.y * (s - 1f), 0f);
                g.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(GlowAlpha[i] * pulse * power));
                g.sortingLayerID = host.sortingLayerID;
                g.sortingOrder = host.sortingOrder - 2;   // hinter der Linie
            }
            // die Linie selbst strahlt (heller, pulsiert), Funken steigen auf
            var lineCol = Color.Lerp(c, Color.white, 0.12f + 0.28f * (pulse - 0.5f) * power);
            lineCol.a = 1f;
            for (int i = 0; i < copies.Count && i < dirs; i++) if (copies[i] != null) copies[i].color = lineCol;
            moteT -= Time.deltaTime * power;
            if (moteT > 0f) return;
            moteT = Random.Range(0.35f, 0.8f);
            var b = host.bounds;
            var p = new Vector2(Random.Range(b.min.x + b.size.x * 0.2f, b.max.x - b.size.x * 0.2f), Random.Range(b.min.y + b.size.y * 0.3f, b.max.y));
            var mc = Color.Lerp(c, Color.white, 0.35f);
            Particles.I?.Spawn(p, "spark", mc, new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(0.25f, 0.45f), 0), 1.4f, 0.06f, 0.015f, 0f);
        }

        void OnDisable() { Show(0); shown = false; }

        SpriteRenderer Make()
        {
            var sr = new GameObject("MedalLine").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(host.transform, false);
            sr.sharedMaterial = Mat;
            return sr;
        }

        /// <summary>Einfarbige Silhouette: Farbe vom SpriteRenderer, Form aus dem Alpha des Bildes.</summary>
        static Material Mat
        {
            get
            {
                if (!shaderLooked)
                {
                    shaderLooked = true;
                    var sh = Shader.Find("GUI/Text Shader");
                    if (sh != null) silhouetteMat = new Material(sh);
                }
                return silhouetteMat;
            }
        }
    }
}
