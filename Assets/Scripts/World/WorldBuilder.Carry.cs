using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Reife Pflanzen aufnehmen (antippen oder ziehen), um sie auf eine gleiche Pflanze zu legen (Zusammenfuehren,
    /// siehe WorldBuilder.Merge.cs). Die Pflanzen bleiben sonst auf ihren Feldern – die Felder sind die Verteidigungslinie.
    /// </summary>
    public partial class WorldBuilder
    {
        class Flyer
        {
            public SpriteRenderer sr; public SpriteRenderer shadow; public Vector2 from, to; public float t, dur, s0, s1; public PlantGenome g;
            public PlantedState mergeTarget; public Cell oldAnchor; public int newLevel, oldLevel;
        }

        readonly List<Flyer> flyers = new List<Flyer>();
        static Sprite arrowSprite;
        PlantedState carrying;

        /// <summary>Hinweis waehrend des Tragens (null = vorbei).</summary>
        public event Action<string> PlacingChanged;

        public bool Placing => carrying != null;
        public PlantedState Carrying => carrying;

        /// <summary>Reife Pflanze aufnehmen: gleiche Pflanzen bekommen einen Pfeil.</summary>
        public void StartPlacing(PlantedState p)
        {
            if (!AnyMergeTargetFor(p))
            {
                Floater?.Invoke(PlantBase(p) + new Vector2(0, 1f), "Zieh eine zweite " + p.Genome.name + " groß, um sie zu verstärken", new Color(1f, 0.9f, 0.7f));
                if (plantViews.TryGetValue(new Cell(p.x, p.y), out var pv)) { pv.sy = 0.25f; pv.flash = 0.4f; }
                Sfx.Play("pop", 1.2f, 0.6f);
                return;
            }
            carrying = p;
            Sfx.Play("pop", 0.8f, 0.9f);
            Sfx.Play("grow", 1.4f, 0.6f);
            Haptics.Tap(20);
            if (plantViews.TryGetValue(new Cell(p.x, p.y), out var v)) { v.sy = 0.3f; v.flash = 0.6f; }
            PlacingChanged?.Invoke("Auf eine gleiche Pflanze ziehen – dann wird sie stärker");
        }

        public void CancelPlacing()
        {
            if (carrying == null) return;
            carrying = null;
            dragging = false;
            hoverTarget = null;
            Sfx.Play("pop", 0.6f, 0.5f);
            PlacingChanged?.Invoke(null);
        }

        /// <summary>Ablegen: gleiche Pflanze -> zusammenfuehren, sonst zurueck aufs eigene Feld.</summary>
        public bool TryPlaceAt(Vector2 w)
        {
            if (carrying == null) return false;
            var tp = PlantAt(CellAt(w));
            if (IsMergeTarget(tp)) return MergeInto(tp);
            CancelPlacing();
            return false;
        }

        void UpdateFlyers(float dt)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.t += dt;
                float k = Mathf.Clamp01(f.t / f.dur);
                var ground = Vector2.Lerp(f.from, f.to, k);
                float h = 4f * (0.9f + (f.to - f.from).magnitude * 0.12f) * k * (1 - k);
                f.sr.transform.position = ground + new Vector2(0, h);
                f.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * 25f * Mathf.Sign(f.to.x - f.from.x + 0.01f));
                float s = Mathf.Lerp(f.s0, f.s1, k) * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
                f.sr.transform.localScale = new Vector3(s, s, 1);
                f.shadow.transform.position = ground;
                f.shadow.transform.localScale = new Vector3(0.6f - h * 0.1f, 0.18f, 1);
                if (UnityEngine.Random.value < dt * 40) Particles.I?.Spawn(f.sr.transform.position, "spark", Color.Lerp(Color.white, f.g.ElementColor, 0.5f), Vector3.zero, 0.4f, 0.18f, 0f, 0f);
                if (k < 1f) continue;
                Destroy(f.sr.gameObject); Destroy(f.shadow.gameObject);
                flyers.RemoveAt(i);
                if (f.mergeTarget != null) LandMerge(f);
            }
        }

        void UpdateCarry(float dt) => UpdateFlyers(Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        /// <summary>Pfeil ueber gleichen Pflanzen beim Tragen.</summary>
        static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null) return arrowSprite;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x + 0.5f - n / 2f, fy = y + 0.5f;
                        // Dreieck nach unten (Spitze unten) + Schaft
                        float halfW = (fy - 10f) * 0.75f;
                        bool tri = fy > 10f && fy < 52f && Mathf.Abs(fx) < halfW;
                        bool shaft = fy >= 50f && fy < 88f && Mathf.Abs(fx) < 12f;
                        float halfW2 = (fy - 4f) * 0.75f + 4f;
                        bool triO = fy > 4f && fy < 56f && Mathf.Abs(fx) < halfW2;
                        bool shaftO = fy >= 50f && fy < 92f && Mathf.Abs(fx) < 16f;
                        Color c = Color.clear;
                        if (triO || shaftO) c = new Color(0.35f, 0.2f, 0.05f, 1f);
                        if (tri || shaft) c = Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.95f, 0.6f), fy / n);
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), n);
                return arrowSprite;
            }
        }

    }
}
