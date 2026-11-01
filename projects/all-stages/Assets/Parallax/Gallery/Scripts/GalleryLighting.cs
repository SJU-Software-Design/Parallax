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
        public GalleryMirror mirror;
        readonly Matrix4x4[] inverse = new Matrix4x4[MaxCasters];
        readonly float[] mirrorCaster = new float[MaxCasters];
        readonly float[] previewCaster = new float[MaxCasters];
        public int Count { get; private set; }
        public void Rebuild()
        {
            casters = FindObjectsByType<GalleryCaster>(FindObjectsSortMode.None);
            mirror = FindFirstObjectByType<GalleryMirror>();
            if (casters.Length > MaxCasters) throw new InvalidOperationException("Gallery shadow caster budget exceeded: " + casters.Length);
            Synchronize();
        }
        public void Synchronize()
        {
            Count = 0;
            if (casters != null) foreach (var c in casters)
                if (c != null && c.gameObject.activeInHierarchy && c.shape != null && c.shape.enabled)
                {
                    inverse[Count] = c.Inverse;
                    var prop=c.GetComponentInParent<GalleryProp>();previewCaster[Count]=prop!=null&&prop.placement!=null&&!prop.placement.Ready?1:0;
                    mirrorCaster[Count] = mirror != null && c.GetComponentInParent<GalleryMirror>() == mirror ? 1 : 0;
                    Count++;
                }
            Shader.SetGlobalInt("_GalleryCasterCount", Count);
            Shader.SetGlobalMatrixArray("_GalleryCasterInverse", inverse);
            Shader.SetGlobalFloatArray("_GalleryCasterMirror", mirrorCaster);
            Shader.SetGlobalFloatArray("_GalleryCasterPreview",previewCaster);
            Shader.SetGlobalVector("_GalleryToSun", ToSun);
            Shader.SetGlobalFloat("_GalleryRoof", Roof);
            Shader.SetGlobalVectorArray("_GalleryWindows", Windows);
            bool enabled = mirror != null && mirror.isActiveAndEnabled;
            Shader.SetGlobalFloat("_GalleryMirrorActive", enabled ? 1 : 0);
            if (!enabled) return;
            Shader.SetGlobalMatrix("_GalleryMirrorInverse", mirror.transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_GalleryMirrorPosition", mirror.transform.position);
            Shader.SetGlobalVector("_GalleryMirrorNormal", mirror.transform.forward);
            Shader.SetGlobalVector("_GalleryReflectedDirection", mirror.ReflectedDirection);
            Shader.SetGlobalVector("_GalleryMirrorHalf", new Vector4(mirror.halfSize.x, mirror.halfSize.y, 0, 0));
        }
        public static bool WindowLit(Vector3 point)
        {
            Vector3 aperture = point + ToSun * ((Roof - point.y) / ToSun.y);
            foreach (var w in Windows) if (aperture.x >= w.x && aperture.x <= w.y && aperture.z >= w.z && aperture.z <= w.w) return true;
            return false;
        }
        public static bool Intersects(Matrix4x4 worldToBox, Vector3 origin, Vector3 direction, float maximum = 1e20f)
        {
            Vector3 o = worldToBox.MultiplyPoint3x4(origin), d = worldToBox.MultiplyVector(direction);
            float enter = -1e20f, leave = 1e20f;
            for (int a = 0; a < 3; a++)
            {
                float v = Mathf.Abs(d[a]) < .000001f ? (d[a] < 0 ? -.000001f : .000001f) : d[a];
                float p = (-.5f - o[a]) / v, q = (.5f - o[a]) / v;
                enter = Mathf.Max(enter, Mathf.Min(p, q)); leave = Mathf.Min(leave, Mathf.Max(p, q));
            }
            return leave >= Mathf.Max(enter, .001f) && enter < maximum;
        }
        public bool IsDirectLit(Vector3 surface, Vector3 normal)
        {
            if (!WindowLit(surface)) return false;
            Vector3 origin = surface + normal * SurfaceBias;
            for (int i = 0; i < Count; i++) if (previewCaster[i]<.5f&&Intersects(inverse[i], origin, ToSun)) return false;
            return true;
        }
        public bool MirrorReceivesSun(Vector3 point)
        {
            if (mirror == null || Vector3.Dot(mirror.transform.forward, ToSun) <= .015f || !WindowLit(point)) return false;
            Vector3 origin = point + mirror.transform.forward * SurfaceBias;
            for (int i = 0; i < Count; i++)
                if (previewCaster[i]<.5f&&mirrorCaster[i] < .5f && Intersects(inverse[i], origin, ToSun)) return false;
            return true;
        }
        public bool IsReflectedLit(Vector3 surface, Vector3 normal)
        {
            if (mirror == null || !mirror.isActiveAndEnabled) return false;
            Vector3 n = mirror.transform.forward, direction = mirror.ReflectedDirection;
            float denominator = Vector3.Dot(direction, n);
            if (Vector3.Dot(n, ToSun) <= .015f || denominator <= .015f) return false;
            float distance = Vector3.Dot(surface - mirror.transform.position, n) / denominator;
            if (distance <= .03f || distance > 32) return false;
            Vector3 reflection = surface - direction * distance;
            Vector3 local = mirror.transform.InverseTransformPoint(reflection);
            if (Mathf.Abs(local.x) > mirror.halfSize.x || Mathf.Abs(local.y) > mirror.halfSize.y || !MirrorReceivesSun(reflection)) return false;
            Vector3 origin = surface + normal * SurfaceBias;
            for (int i = 0; i < Count; i++)
                if (previewCaster[i]<.5f&&mirrorCaster[i] < .5f && Intersects(inverse[i], origin, -direction, distance - .025f)) return false;
            return true;
        }
        public bool IsSunlit(Vector3 surface, Vector3 normal) => IsDirectLit(surface, normal) || IsReflectedLit(surface, normal);
        void LateUpdate() { Synchronize(); }
    }
}
