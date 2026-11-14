using UnityEngine;

namespace GlassTrial
{
    [RequireComponent(typeof(Camera))]
    public sealed class GlassCameraCapture : MonoBehaviour
    {
        public Shader blurShader;
        public bool captureEnabled;
        public float blurStrength = 1f;
        public int renderCount;
        public RenderTexture blurred;
        Material material;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (captureEnabled && blurShader != null && blurShader.isSupported)
            {
                if (material == null) material = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
                int width = Mathf.Max(1, source.width), height = Mathf.Max(1, source.height);
                if (blurred == null || blurred.width != width || blurred.height != height)
                {
                    ReleaseBuffer();
                    blurred = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                        { name = "Glass Trial full-resolution", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    blurred.Create();
                }
                var a = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                var b = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                try
                {
                    a.filterMode = b.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(source, a);
                    float blurRadius = 2f * Mathf.Clamp01(blurStrength);
                    material.SetVector("_Direction", new Vector4(blurRadius, 0, 0, 0)); Graphics.Blit(a, b, material);
                    material.SetVector("_Direction", new Vector4(0, blurRadius, 0, 0)); Graphics.Blit(b, blurred, material);
                    renderCount++;
                }
                finally { RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); }
            }
            Graphics.Blit(source, destination);
        }
        void ReleaseBuffer()
        {
            if (blurred != null) { blurred.Release(); Destroy(blurred); blurred = null; }
        }
        void OnDisable() { ReleaseBuffer(); if (material != null) Destroy(material); material = null; }
    }
}
