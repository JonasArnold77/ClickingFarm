using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>Leichtgewichtiges Partikelsystem mit gepoolten SpriteRenderern.</summary>
    public class Particles : MonoBehaviour
    {
        public static Particles I { get; private set; }

        class P
        {
            public SpriteRenderer sr; public Vector3 vel; public float gravity, life, max, s0, s1, spin, drag;
            public Color col; public bool fade;
        }

        readonly List<P> active = new List<P>();
        readonly Stack<P> pool = new Stack<P>();

        void Awake() { I = this; }

        P Take()
        {
            P p;
            if (pool.Count > 0) { p = pool.Pop(); p.sr.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                p = new P { sr = go.AddComponent<SpriteRenderer>() };
                p.sr.sortingOrder = 3000;
            }
            active.Add(p);
            return p;
        }

        public void Spawn(Vector3 pos, string sprite, Color col, Vector3 vel, float life, float size, float sizeEnd = -1, float gravity = 9f, float spin = 0f, float drag = 0f, int order = 3000)
        {
            var p = Take();
            p.sr.sprite = Art.Get("px:" + sprite);
            p.sr.color = col;
            p.sr.sortingOrder = order;
            p.sr.transform.position = pos;
            p.sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
            p.vel = vel; p.life = 0; p.max = life; p.s0 = size; p.s1 = sizeEnd < 0 ? size : sizeEnd;
            p.gravity = gravity; p.spin = spin; p.col = col; p.fade = true; p.drag = drag;
            p.sr.transform.localScale = Vector3.one * size;
        }

        public void Burst(Vector3 pos, string sprite, Color col, int n, float speed, float life = 0.6f, float size = 1f, float gravity = 9f, float up = 2f)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1f) * speed;
                var c = col; c.r *= Random.Range(0.85f, 1.1f); c.g *= Random.Range(0.85f, 1.1f);
                Spawn(pos, sprite, c, new Vector3(dir.x, dir.y + up, 0), life * Random.Range(0.7f, 1.2f), size * Random.Range(0.7f, 1.2f), 0f, gravity, Random.Range(-360f, 360f));
            }
        }

        public void Ring(Vector3 pos, Color col, float size = 1f, float life = 0.35f)
        {
            Spawn(pos, "ring", col, Vector3.zero, life, size * 0.3f, size * 1.6f, 0f, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.life += dt;
                if (p.life >= p.max)
                {
                    p.sr.gameObject.SetActive(false);
                    active.RemoveAt(i); pool.Push(p);
                    continue;
                }
                float t = p.life / p.max;
                p.vel.y -= p.gravity * dt;
                if (p.drag > 0) p.vel *= 1f - Mathf.Clamp01(p.drag * dt);
                var tr = p.sr.transform;
                tr.position += p.vel * dt;
                tr.Rotate(0, 0, p.spin * dt);
                tr.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, t);
                var c = p.col; c.a = p.col.a * (t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
                p.sr.color = c;
            }
        }
    }
}
