using System;
using UnityEngine;

namespace ShadeLink.Library
{
    public sealed class LibraryLighting : MonoBehaviour
    {
        public const int MaxCasters = 128;
        public const float Roof = 10f, SurfaceBias = .012f;
        public static readonly Vector3 ToSun = new Vector3(0, 1, -.75f).normalized;
        public static Vector4[] Windows => LibraryWorld.Skylights;
        public LibraryCaster[] casters;
        readonly Matrix4x4[] inverse = new Matrix4x4[MaxCasters];
        readonly float[] previewCaster = new float[MaxCasters];
        public int Count { get; private set; }
        public void Rebuild()
        {
            casters = FindObjectsByType<LibraryCaster>(FindObjectsSortMode.None);
            if (casters.Length > MaxCasters) throw new InvalidOperationException("Library shadow caster budget exceeded: " + casters.Length);
            Synchronize();
        }
        public void Synchronize()
        {
            Count = 0;
            if (casters != null) foreach (var c in casters)
                if (c != null && c.gameObject.activeInHierarchy && c.shape != null&&c.shape.enabled)
                {var prop=c.GetComponentInParent<LibraryProp>();previewCaster[Count]=prop!=null&&prop.placement!=null&&!prop.placement.Ready?1:0;inverse[Count++] = c.Inverse;}
            Shader.SetGlobalInt("_LibraryCasterCount", Count);
            Shader.SetGlobalMatrixArray("_LibraryCasterInverse", inverse);
            Shader.SetGlobalFloatArray("_LibraryCasterPreview",previewCaster);
            Shader.SetGlobalVector("_LibraryToSun", ToSun);
            Shader.SetGlobalFloat("_LibraryRoof", Roof);
            Shader.SetGlobalInt("_LibraryWindowCount", Windows.Length);
            Shader.SetGlobalVectorArray("_LibraryWindows", Windows);
        }
        public static bool WindowLit(Vector3 point)
        {
            Vector3 aperture = point + ToSun * ((Roof - point.y) / ToSun.y);
            foreach (var w in Windows) if (aperture.x >= w.x && aperture.x <= w.y && aperture.z >= w.z && aperture.z <= w.w) return true;
            return false;
        }
        public static bool Intersects(Matrix4x4 worldToBox, Vector3 origin, Vector3 direction)
        {
            Vector3 o = worldToBox.MultiplyPoint3x4(origin), d = worldToBox.MultiplyVector(direction);
            float enter = -1e20f, leave = 1e20f;
            for (int a = 0; a < 3; a++)
            {
                float v = Mathf.Abs(d[a]) < .000001f ? (d[a] < 0 ? -.000001f : .000001f) : d[a];
                float p = (-.5f - o[a]) / v, q = (.5f - o[a]) / v;
                enter = Mathf.Max(enter, Mathf.Min(p, q)); leave = Mathf.Min(leave, Mathf.Max(p, q));
            }
            return leave >= Mathf.Max(enter, .001f);
        }
        public bool IsSunlit(Vector3 surface, Vector3 normal)
        {
            if (!WindowLit(surface)) return false;
            Vector3 origin = surface + normal * SurfaceBias;
            for (int i = 0; i < Count; i++) if (previewCaster[i]<.5f&&Intersects(inverse[i], origin, ToSun)) return false;
            return true;
        }
        void LateUpdate() { Synchronize(); }
    }
}
