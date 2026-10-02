using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryLighting : MonoBehaviour
    {
        public const int MaxCasters = 128;
        public const float Roof = 10f, SurfaceBias = .012f;
        public static readonly Vector3 ToSun = new Vector3(0, 1, -.75f).normalized;
        public static readonly Vector4[] Windows = {
            new Vector4(-14.1f, 14.1f, 1.5f, 4.8f),
            new Vector4(-14.1f, 14.1f, 13.5f, 18f),
            new Vector4(-14.1f, 14.1f, 25.5f, 30.5f),
            new Vector4(-14.1f, 14.1f, 36.5f, 47f)
        };
        public GalleryCaster[] casters;
        // Runtime discovery must not change the shipped scene's serialized layout.
        [NonSerialized] public GalleryCurvedCaster curved;
        readonly Matrix4x4[] inverse = new Matrix4x4[MaxCasters];
        public int Count { get; private set; }
        public void Rebuild()
        {
            casters = FindObjectsByType<GalleryCaster>(FindObjectsSortMode.None);
            var curves=FindObjectsByType<GalleryCurvedCaster>(FindObjectsSortMode.None);
            if(curves.Length>1)throw new InvalidOperationException("This gallery supports one procedural ceramic shadow caster");
            curved=curves.Length==0?null:curves[0];
            Synchronize();
        }
        public void Synchronize()
        {
            Count = 0;
            inverse[GalleryCurvedCaster.HeaderSlot]=new Matrix4x4();
            if (casters != null) foreach (var c in casters)
                if (c != null && c.gameObject.activeInHierarchy && c.shape != null && c.GetComponentInParent<GalleryCurvedCaster>()==null)
                {
                    if(Count>=GalleryCurvedCaster.HeaderSlot)throw new InvalidOperationException("Gallery shadow caster budget exceeded");
                    inverse[Count++] = c.Inverse;
                }
            if(curved!=null&&curved.gameObject.activeInHierarchy)curved.Pack(inverse,Count);
            Shader.SetGlobalInt("_GalleryCasterCount", Count);
            Shader.SetGlobalMatrixArray("_GalleryCasterInverse", inverse);
            Shader.SetGlobalVector("_GalleryToSun", ToSun);
            Shader.SetGlobalFloat("_GalleryRoof", Roof);
            Shader.SetGlobalVectorArray("_GalleryWindows", Windows);
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
            for (int i = 0; i < Count; i++) if (Intersects(inverse[i], origin, ToSun)) return false;
            if(curved!=null&&curved.gameObject.activeInHierarchy&&curved.Intersects(origin,ToSun))return false;
            return true;
        }
        void LateUpdate() { Synchronize(); }
    }
}
