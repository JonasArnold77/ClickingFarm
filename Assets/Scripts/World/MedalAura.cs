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
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host;
        readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
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
