using UnityEngine;

namespace SeedClicker
{
    /// <summary>Ab und zu flattert ein Glueckskaefer ueber die Farm. Antippen gibt einen Bonus.</summary>
    public class LuckyBug : MonoBehaviour
    {
        public static LuckyBug I { get; private set; }

        SpriteRenderer sr;
        float timer;
        float life;
        Vector3 from, to;
        bool active;
        Game g;

        void Awake()
        {
            I = this;
            g = Game.I;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get("bug");
            sr.sortingOrder = 2500;
            sr.enabled = false;
            timer = Random.Range(35f, 60f);
        }

        void Update()
        {
            if (!active)
            {
                if (g.TutorialActive || g.Sleeping) return;
                timer -= Time.deltaTime;
                if (timer <= 0) Launch();
                return;
            }
            life += Time.deltaTime;
            float dur = 14f;
            float t = life / dur;
            if (t >= 1f) { Hide(); return; }
            var p = Vector3.Lerp(from, to, t);
            p += new Vector3(Mathf.Sin(life * 2.3f) * 1.2f, Mathf.Sin(life * 3.7f) * 0.8f, 0);
            transform.position = p;
            float flap = Mathf.Abs(Mathf.Sin(life * 22f));
            transform.localScale = new Vector3(0.6f + flap * 0.25f, 0.85f, 1) * 1.1f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(life * 5f) * 15f);
            if (Random.value < Time.deltaTime * 8f)
                Particles.I.Spawn(p, "spark", new Color(1f, 0.95f, 0.5f), Vector3.zero, 0.5f, 0.6f, 0f, 0f);
        }

        void Launch()
        {
            var cam = Camera.main;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = cam.transform.position;
            bool left = Random.value < 0.5f;
            from = new Vector3(c.x + (left ? -w - 1 : w + 1), c.y + Random.Range(-h * 0.5f, h * 0.4f), 0);
            to = new Vector3(c.x + (left ? w + 1 : -w - 1), c.y + Random.Range(-h * 0.5f, h * 0.4f), 0);
            life = 0; active = true; sr.enabled = true;
            Sfx.Play("bug");
        }

        void Hide()
        {
            active = false; sr.enabled = false;
            timer = Random.Range(45f, 100f);
        }

        public bool TryCatch(Vector3 world)
        {
            if (!active) return false;
            var d = world - transform.position; d.z = 0;
            if (d.magnitude > 0.9f) return false;
            Particles.I.Burst(transform.position, "star", new Color(1f, 0.85f, 0.3f), 14, 5f, 0.7f, 1.1f, 2f);
            Particles.I.Ring(transform.position, Color.white, 1.6f);
            Sfx.Play("unlock", 1.3f);
            Haptics.Tap(30);
            g.CatchLuckyBug();
            Hide();
            return true;
        }
    }
}
