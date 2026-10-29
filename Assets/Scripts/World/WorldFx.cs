using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Effekte aus "Cartoon FX Remaster" (JMO) in der 2D-Welt abspielen. Die Prefabs stehen in
    /// Resources/WorldArt.asset -> effects (Name -> Prefab). Sie sind fuer 3D gebaut: hier werden sie
    /// skaliert, in die 2D-Sortierung gelegt, optional umgefaerbt und nach dem Abspielen entfernt.
    /// Fehlt ein Effekt, gibt Play null zurueck – dann greifen die eigenen Partikel als Ersatz.
    /// </summary>
    public static class WorldFx
    {
        public static bool Has(string name) => WorldArtSet.I != null && WorldArtSet.I.Fx(name) != null;

        /// <summary>Einmaliger Effekt an einer Weltposition.</summary>
        public static GameObject Play(string name, Vector2 pos, float scale = 1f, int order = 30010, Color? tint = null, Transform parent = null, float lifetime = -1f)
        {
            var prefab = WorldArtSet.I != null ? WorldArtSet.I.Fx(name) : null;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            go.transform.localScale = Vector3.one * scale;
            float maxLife = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (tint.HasValue)
                {
                    var sc = main.startColor;
                    var t = tint.Value;
                    if (sc.mode == ParticleSystemGradientMode.Color) main.startColor = sc.color * t;
                    else if (sc.mode == ParticleSystemGradientMode.TwoColors) main.startColor = new ParticleSystem.MinMaxGradient(sc.colorMin * t, sc.colorMax * t);
                    else main.startColor = t;
                }
                maxLife = Mathf.Max(maxLife, main.duration + main.startLifetime.constantMax);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null) r.sortingOrder = order + r.sortingOrder % 10;
            }
            // 3D-Lichter und Kamerawackeln der Effekte haben in der 2D-Welt keine Funktion
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == "CFXR_Effect") mb.enabled = false;
            ScaleTrails(go, scale);
            Object.Destroy(go, lifetime > 0f ? lifetime : Mathf.Clamp(maxLife + 0.5f, 0.5f, 8f));
            return go;
        }

        /// <summary>Partikel-Schweife (Trails) skalieren ihre Breite nicht mit der Transform-Groesse – hier nachholen.</summary>
        static void ScaleTrails(GameObject go, float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var tr = ps.trails;
                if (tr.enabled) tr.widthOverTrailMultiplier *= factor;
            }
            foreach (var t in go.GetComponentsInChildren<TrailRenderer>(true)) t.widthMultiplier *= factor;
            foreach (var l in go.GetComponentsInChildren<LineRenderer>(true)) l.widthMultiplier *= factor;
        }

        /// <summary>Dauerhafter Effekt (z. B. Feuerball, Rauchwolke), der einem Objekt folgt; wird mit ihm zerstoert.</summary>
        public static GameObject Attach(string name, Transform parent, float scale = 1f, int order = 30010, Color? tint = null)
        {
            var go = Play(name, parent != null ? (Vector2)parent.position : Vector2.zero, 1f, order, tint, parent, 999f);
            if (go == null) return null;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * scale / Mathf.Max(0.0001f, parent != null ? parent.lossyScale.x : 1f);
            ScaleTrails(go, scale);
            return go;
        }

        /// <summary>Lauf-Effekt sanft ausklingen lassen: keine neuen Partikel, dann loeschen.</summary>
        public static void Release(GameObject go, float after = 1.5f)
        {
            if (go == null) return;
            go.transform.SetParent(null, true);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(go, after);
        }
    }
}
