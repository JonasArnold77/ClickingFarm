using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Metall-Ueberzug in Medaillenfarbe (Bronze, Silber, Gold) ueber einem Bild: ueber der Ernte-Pflanze selbst und ueber
    /// jedem einzelnen Erntestueck (fliegend, beim Knolli, auf dem Weg in die Kiste).
    /// Eine Kopie des Bildes liegt direkt darueber und wird vom Shader "SeedClicker/MetalCoat" eingefaerbt: Metallton mal
    /// Helligkeit des Bildes – die Schattierungen der Pflanze bleiben darunter erkennbar, helle Stellen glaenzen.
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        SpriteRenderer host, coat;
        int level;
        (Sprite, int, int, bool, bool) lastKey;
        static Material coatMat;
        static bool shaderLooked;
        /// <summary>Deckkraft des Ueberzugs: Gold dezent, damit die echte Farbe der Pflanze durchschimmert.</summary>
        static float Cover(int level) => level >= 4 ? 0.5f : 0.9f;

        /// <summary>Stufe setzen (unter 2 = kein Ueberzug). width/dirs stammen von der frueheren Kontur und werden ignoriert.</summary>
        public static void Set(SpriteRenderer host, int level, float width = 0f, int dirs = 0)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
            }
            a.level = level;
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2 && Mat != null;
            if (!on) { if (coat != null && coat.enabled) coat.enabled = false; return; }
            if (coat == null)
            {
                coat = new GameObject("MetalCoat").AddComponent<SpriteRenderer>();
                coat.transform.SetParent(host.transform, false);
                coat.sharedMaterial = Mat;
                lastKey = default;
            }
            if (!coat.enabled) coat.enabled = true;

            // nur neu setzen, wenn sich Bild, Stufe, Reihenfolge oder Spiegelung geaendert haben
            var key = (host.sprite, level, host.sortingOrder, host.flipX, host.flipY);
            if (key.Equals(lastKey)) return;
            lastKey = key;
            var c = WorldBuilder.MedalColor(level);
            coat.sprite = host.sprite;
            coat.flipX = host.flipX; coat.flipY = host.flipY;
            coat.color = new Color(c.r, c.g, c.b, Cover(level));
            coat.sortingLayerID = host.sortingLayerID;
            coat.sortingOrder = host.sortingOrder + 1;   // ueber dem Bild
        }

        void OnDisable() { if (coat != null) coat.enabled = false; }

        static Material Mat
        {
            get
            {
                if (!shaderLooked)
                {
                    shaderLooked = true;
                    var sh = Shader.Find("SeedClicker/MetalCoat");
                    if (sh != null) coatMat = new Material(sh);
                }
                return coatMat;
            }
        }
    }
}
