using System;
using UnityEngine;
using UnityEngine.VFX;

namespace SeedClicker
{
    /// <summary>
    /// 2D-Effekte aus Happy Harvest (VFX Graph) abspielen: Giessen, Saat, Erdwolke, Ernte, Blaetter, Wassertropfen.
    /// Die Effekte stehen in Resources/WorldArt.asset -> vfx (Name -> Effekt). Laeuft VFX Graph auf dem Geraet nicht
    /// (z. B. WebGL, sehr alte Handys) oder fehlt ein Effekt, gibt Play null zurueck – dann greifen eigene Partikel.
    /// </summary>
    public static class WorldVfx
    {
        static readonly bool supported = SystemInfo.supportsComputeShaders;

        /// <summary>Einmaliger Effekt an einer Weltposition; setup darf Eigenschaften setzen (z. B. Farbe).</summary>
        public static VisualEffect Play(string name, Vector2 pos, float scale = 1f, int order = 30010, Action<VisualEffect> setup = null, float lifetime = 3f, bool flipX = false)
        {
            if (!supported || WorldArtSet.I == null) return null;
            var asset = WorldArtSet.I.Vfx(name);
            if (asset == null) return null;
            var go = new GameObject("Vfx_" + name);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            go.transform.localScale = new Vector3(flipX ? -scale : scale, scale, scale);
            var vfx = go.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = asset;
            var r = go.GetComponent<Renderer>();   // VFXRenderer
            if (r != null) r.sortingOrder = order;
            setup?.Invoke(vfx);
            vfx.Reinit();
            vfx.SendEvent("OnPlay");
            UnityEngine.Object.Destroy(go, lifetime);
            return vfx;
        }
    }
}
