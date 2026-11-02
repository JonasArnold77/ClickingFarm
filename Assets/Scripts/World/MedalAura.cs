using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Comic-Kontur in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Ganz einfach: unter dem Bild liegen zwei etwas groessere Kopien derselben Form, einfarbig gefuellt –
    /// innen die Medaillenfarbe, ganz aussen ein dunkler Tuschestrich. Was ueber das Bild hinaussteht, ist die Kontur.
    /// Einfarbig fuellt der eingebaute Shader "GUI/Text Shader" (Farbe vom SpriteRenderer, Form aus dem Alpha des Bildes).
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        const float ColorGrow = 1.09f, InkGrow = 1.14f;   // so viel groesser als das Bild (um seine Mitte)

        SpriteRenderer host, fill, ink;
        int level;
        float strength = 1f;
        static Material silhouetteMat;
        static bool shaderLooked;

        /// <summary>Stufe setzen (unter 2 = keine Kontur). strength: Deckkraft.</summary>
        public static void Set(SpriteRenderer host, int level, float strength = 1f)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
            }
            a.level = level; a.strength = strength;
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2 && Mat != null;
            if (!on) { Show(false); return; }
            if (fill == null) { ink = Make("MedalInk"); fill = Make("MedalColor"); }
            Show(true);
            var c = WorldBuilder.MedalColor(level);
            Layer(fill, ColorGrow, new Color(c.r, c.g, c.b, strength), 1);
            Layer(ink, InkGrow, new Color(c.r * 0.3f, c.g * 0.25f, c.b * 0.2f, strength), 2);
        }

        /// <summary>Kopie der Form, um die Bildmitte vergroessert, direkt unter dem Bild.</summary>
        void Layer(SpriteRenderer sr, float grow, Color col, int below)
        {
            var spr = host.sprite;
            sr.sprite = spr;
            sr.flipX = host.flipX; sr.flipY = host.flipY;
            var bc = (Vector2)spr.bounds.center;
            sr.transform.localPosition = bc * (1f - grow);
            sr.transform.localScale = new Vector3(grow, grow, 1f);
            sr.color = col;
            sr.sortingLayerID = host.sortingLayerID;
            sr.sortingOrder = host.sortingOrder - below;
        }

        void Show(bool on)
        {
            if (fill != null) fill.enabled = on;
            if (ink != null) ink.enabled = on;
        }

        void OnDisable() => Show(false);

        SpriteRenderer Make(string name)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
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
