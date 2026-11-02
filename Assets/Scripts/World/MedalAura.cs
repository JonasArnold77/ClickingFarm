using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Comic-Kontur in Medaillenfarbe (Bronze, Silber, Gold) um ein Bild: um die Ernte-Pflanze selbst und um
    /// jedes einzelne Erntestueck (fliegend, im Haufen, beim Knolli, auf dem Weg in die Kiste).
    /// Eine deckungsgleiche Kopie des Bildes liegt darueber; der Shader "SeedClicker/MedalOutline2D" zeichnet davon nur
    /// eine harte Linie genau auf der Aussenlinie, aussen mit dunklem Tuschestrich – wie in einem Comic.
    /// Haengt als Komponente am Bild und folgt ihm selbst (Bild, sichtbar, Reihenfolge) – Aufrufer setzen nur die Stufe.
    /// </summary>
    public class MedalAura : MonoBehaviour
    {
        const float Grow = 1f;   // deckungsgleich: die Linie liegt genau auf der Aussenlinie

        SpriteRenderer host, outline;
        MaterialPropertyBlock block;
        Sprite lastSprite;
        int level;
        float strength = 1f, seed;
        static Material outlineMat;
        static bool shaderLooked;
        static readonly Dictionary<Sprite, Vector4> uvRects = new Dictionary<Sprite, Vector4>();

        /// <summary>Stufe setzen (unter 2 = keine Kontur). strength: Deckkraft (Pflanze 1, kleine Stuecke etwas weniger).</summary>
        public static void Set(SpriteRenderer host, int level, float strength = 1f)
        {
            if (host == null) return;
            var a = host.GetComponent<MedalAura>();
            if (a == null)
            {
                if (level < 2) return;
                a = host.gameObject.AddComponent<MedalAura>();
                a.host = host;
                a.seed = Random.value * 10f;
            }
            a.level = level; a.strength = strength;
        }

        void LateUpdate()
        {
            bool on = host != null && host.enabled && host.sprite != null && level >= 2 && Mat != null;
            if (!on) { if (outline != null) outline.enabled = false; return; }
            if (outline == null) Build();
            outline.enabled = true;

            var spr = host.sprite;
            if (spr != lastSprite)
            {
                lastSprite = spr;
                outline.sprite = spr;
                block.SetVector("_UVRect", UVRect(spr));
                block.SetFloat("_Grow", Grow);
                block.SetFloat("_Seed", seed);
                outline.SetPropertyBlock(block);
            }
            outline.flipX = host.flipX; outline.flipY = host.flipY;
            // um die Bildmitte vergroessern (nicht um den Drehpunkt), damit die Kontur genau um die Form liegt
            var bc = (Vector2)spr.bounds.center;
            outline.transform.localPosition = bc * (1f - Grow);
            outline.transform.localScale = new Vector3(Grow, Grow, 1f);

            var c = WorldBuilder.MedalColor(level);
            outline.color = new Color(c.r, c.g, c.b, strength);
            outline.sortingLayerID = host.sortingLayerID;
            outline.sortingOrder = host.sortingOrder + 1;   // darueber: der Shader zeichnet nur den Rand, die Mitte bleibt frei
        }

        void OnDisable() { if (outline != null) outline.enabled = false; }

        void Build()
        {
            outline = new GameObject("MedalOutline").AddComponent<SpriteRenderer>();
            outline.transform.SetParent(host.transform, false);
            outline.sharedMaterial = Mat;
            block = new MaterialPropertyBlock();
            lastSprite = null;
        }

        static Material Mat
        {
            get
            {
                if (!shaderLooked)
                {
                    shaderLooked = true;
                    var sh = Shader.Find("SeedClicker/MedalOutline2D");
                    if (sh != null) outlineMat = new Material(sh);
                }
                return outlineMat;   // ohne Shader: keine Kontur
            }
        }

        /// <summary>UV-Bereich des Bildes im (Atlas-)Bild: Proben ausserhalb zaehlen im Shader als leer.</summary>
        static Vector4 UVRect(Sprite s)
        {
            if (uvRects.TryGetValue(s, out var r)) return r;
            float x0 = 1f, y0 = 1f, x1 = 0f, y1 = 0f;
            foreach (var u in s.uv) { x0 = Mathf.Min(x0, u.x); y0 = Mathf.Min(y0, u.y); x1 = Mathf.Max(x1, u.x); y1 = Mathf.Max(y1, u.y); }
            r = new Vector4(x0, y0, x1, y1);
            uvRects[s] = r;
            return r;
        }
    }
}
