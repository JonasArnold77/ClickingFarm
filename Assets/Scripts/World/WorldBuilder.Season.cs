using System;
using UnityEngine;

namespace SeedClicker
{
    public enum Season { Sommer, Herbst, Winter }

    /// <summary>
    /// Jahreszeiten: Sommer, Herbst, Winter – frei umschaltbar (Knopf oben). Boden und Natur werden im Shader
    /// umgefaerbt (Resources/Shaders/SeasonCommon.hlsl): im Herbst goldenes bis rostrotes Laub (Nadelbaeume bleiben
    /// gruen), im Winter Schnee und Reif. Dazu fallen Blaetter bzw. Schneeflocken, das Licht wird waermer/kuehler.
    /// Pflanzen, Zombies und Zaeune bleiben unveraendert.
    /// </summary>
    public partial class WorldBuilder
    {
        Material seasonMat, evergreenMat, defaultSpriteMat;
        float autumnAmt, winterAmt, seasonScanT, seasonFxT;
        static readonly int AutumnId = Shader.PropertyToID("_SeasonAutumn"), WinterId = Shader.PropertyToID("_SeasonWinter");

        public Season Season => (Season)Mathf.Clamp(save.season, 0, 2);
        public float AutumnAmount => autumnAmt;
        public float WinterAmount => winterAmt;
        public event Action<Season> SeasonChanged;

        void InitSeason()
        {
            var sh = Shader.Find("SeedClicker/SeasonLit2D");
            if (sh != null && sh.isSupported)
            {
                seasonMat = new Material(sh) { name = "SeasonLit" };
                evergreenMat = new Material(sh) { name = "SeasonLitEvergreen" };
                evergreenMat.SetFloat("_Evergreen", 1f);
            }
            var probe = new GameObject("Probe").AddComponent<SpriteRenderer>();
            defaultSpriteMat = probe.sharedMaterial;
            Destroy(probe.gameObject);
            autumnAmt = Season == Season.Herbst ? 1f : 0f;
            winterAmt = Season == Season.Winter ? 1f : 0f;
            Shader.SetGlobalFloat(AutumnId, autumnAmt);
            Shader.SetGlobalFloat(WinterId, winterAmt);
            ApplySeasonMaterials();
        }

        public void SetSeason(Season s)
        {
            save.season = (int)s;
            save.Save();
            seasonFxT = 1.2f;   // kurzer Schwall Blaetter / Flocken beim Wechsel
            Sfx.Play(s == Season.Winter ? "bell" : "unlock", s == Season.Winter ? 1.5f : 0.9f, 0.6f);
            Haptics.Tap(20);
            SeasonChanged?.Invoke(s);
        }

        public void NextSeason() => SetSeason((Season)(((int)Season + 1) % 3));

        public static string SeasonName(Season s) => s == Season.Herbst ? "Herbst" : s == Season.Winter ? "Winter" : "Sommer";

        /// <summary>Natur-Sprites und Berg-Ebenen bekommen das Jahreszeiten-Material (neue Objekte werden laufend erfasst).</summary>
        void ApplySeasonMaterials()
        {
            if (seasonMat == null) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                bool isDefault = m == defaultSpriteMat || m.shader.name.Contains("Sprite-Lit-Default");
                if (!isDefault) continue;
                string n = r.gameObject.name;
                if (n.StartsWith("Pot") || n == "Sprite" || n == "Plant" || n == "FieldFlash" || n == "Footprint" || n == "BarBack" || n == "BarFill" || n == "CellCursor") continue;
                if (r is SpriteRenderer)
                    r.sharedMaterial = n.Contains("sk_Tree_1") || n.Contains("sk_Tree_2") || n.ToLower().Contains("pine") ? evergreenMat : seasonMat;
                else if (r is MeshRenderer)
                {
                    // Berg-Baender: eigenes Material mit gleicher Textur
                    var mm = new Material(seasonMat) { mainTexture = m.mainTexture };
                    r.sharedMaterial = mm;
                }
            }
        }

        void UpdateSeason(float dt)
        {
            var s = Season;
            autumnAmt = Mathf.MoveTowards(autumnAmt, s == Season.Herbst ? 1f : 0f, dt * 0.9f);
            winterAmt = Mathf.MoveTowards(winterAmt, s == Season.Winter ? 1f : 0f, dt * 0.9f);
            Shader.SetGlobalFloat(AutumnId, autumnAmt);
            Shader.SetGlobalFloat(WinterId, winterAmt);
            seasonScanT -= dt;
            if (seasonScanT <= 0f) { seasonScanT = 1.5f; ApplySeasonMaterials(); }

            // fallende Blaetter / Schneeflocken rund um die Kamera
            var P = Particles.I;
            var cam = Camera.main;
            if (P == null || cam == null) return;
            seasonFxT = Mathf.Max(0f, seasonFxT - dt);
            float boost = 1f + seasonFxT * 6f;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            if (autumnAmt > 0.3f && UnityEngine.Random.value < dt * 5f * boost)
            {
                var p = c + new Vector2(UnityEngine.Random.Range(-w, w), h + 0.3f);
                var col = Color.Lerp(new Color(1f, 0.7f, 0.2f), new Color(0.85f, 0.3f, 0.12f), UnityEngine.Random.value);
                P.Spawn(p, "leaf", col, new Vector3(UnityEngine.Random.Range(-0.5f, 0.3f), -0.7f, 0), 9f, 0.15f, 0.15f, 0.05f, UnityEngine.Random.Range(-160f, 160f), 0.1f, 3100);
            }
            if (winterAmt > 0.3f)
            {
                int n = Mathf.FloorToInt(dt * 28f * boost + UnityEngine.Random.value);
                for (int i = 0; i < n; i++)
                {
                    var p = c + new Vector2(UnityEngine.Random.Range(-w * 1.2f, w), h + 0.2f);
                    float size = UnityEngine.Random.Range(0.03f, 0.07f);
                    P.Spawn(p, "spark", new Color(1f, 1f, 1f, 0.85f), new Vector3(UnityEngine.Random.Range(0.05f, 0.35f), UnityEngine.Random.Range(-0.9f, -0.5f), 0), 10f, size, size, 0f, 0f, 0f, 3100);
                }
            }
        }
    }
}
