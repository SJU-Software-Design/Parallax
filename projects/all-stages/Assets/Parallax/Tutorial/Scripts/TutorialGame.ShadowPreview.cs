using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    public sealed partial class TutorialGame
    {
        const int ShadowPreviewLayer = 30;
        Camera shadowPreviewCamera;
        RenderTexture shadowPreviewMap;
        Shader shadowPreviewDepth;
        readonly List<Renderer> shadowPreviewRenderers = new List<Renderer>();
        readonly Dictionary<GameObject, int> shadowPreviewLayers = new Dictionary<GameObject, int>();

        public bool ShadowPreviewVisible { get; private set; }
        public Bounds ShadowPreviewCasterBounds { get; private set; }

        // The ordinary held pose is already resolved by Update. This is a visual
        // projection only: neither its render target nor its camera has colliders.
        void LateUpdate() => RefreshShadowPreview();

        public void RefreshShadowPreview()
        {
            ClearShadowPreview();
            if (!isActiveAndEnabled || paused || !inSunlightRoom || sunlight == null || !sunlight.enabled ||
                Held == null || Held.placement == null || !Held.placement.Held ||
                sunlightDice == null || System.Array.IndexOf(sunlightDice, Held) < 0) return;

            shadowPreviewRenderers.Clear();
            Bounds bounds = new Bounds();
            foreach (var renderer in Held.placement.Visuals)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !Held.placement.OriginalCastsShadow(renderer)) continue;
                if (shadowPreviewRenderers.Count == 0) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                shadowPreviewRenderers.Add(renderer);
            }
            if (shadowPreviewRenderers.Count == 0 || !PrepareShadowPreview()) return;

            shadowPreviewCamera.transform.SetPositionAndRotation(bounds.center + SunlightToSun * 65f,
                Quaternion.LookRotation(-SunlightToSun));
            shadowPreviewCamera.orthographicSize = Mathf.Max(2f, bounds.extents.magnitude + .5f);
            Shader.SetGlobalFloat("_TutorialShadowPreviewFar", shadowPreviewCamera.farClipPlane);
            shadowPreviewLayers.Clear();
            try
            {
                // Isolate the original meshes for this synchronous render only;
                // restore every layer before ordinary rendering or physics runs.
                foreach (var renderer in shadowPreviewRenderers)
                {
                    var target = renderer.gameObject;
                    if (shadowPreviewLayers.ContainsKey(target)) continue;
                    shadowPreviewLayers.Add(target, target.layer);
                    target.layer = ShadowPreviewLayer;
                }
                shadowPreviewCamera.RenderWithShader(shadowPreviewDepth, "");
            }
            finally
            {
                foreach (var entry in shadowPreviewLayers)
                    if (entry.Key != null) entry.Key.layer = entry.Value;
            }

            Shader.SetGlobalTexture("_TutorialShadowPreviewMap", shadowPreviewMap);
            Shader.SetGlobalMatrix("_TutorialShadowPreviewVP",
                GL.GetGPUProjectionMatrix(shadowPreviewCamera.projectionMatrix, true) * shadowPreviewCamera.worldToCameraMatrix);
            Shader.SetGlobalMatrix("_TutorialShadowPreviewView", shadowPreviewCamera.worldToCameraMatrix);
            Shader.SetGlobalFloat("_TutorialShadowPreviewActive", 1);
            ShadowPreviewCasterBounds = bounds;
            ShadowPreviewVisible = true;
        }

        bool PrepareShadowPreview()
        {
            if (shadowPreviewCamera != null) return true;
            shadowPreviewDepth = Resources.Load<Shader>("Tutorial/ShadowPreviewDepth");
            if (shadowPreviewDepth == null || !shadowPreviewDepth.isSupported) return false;
            shadowPreviewMap = new RenderTexture(1024, 1024, 24, RenderTextureFormat.RFloat)
            {
                name = "Tutorial held die expected shade",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            shadowPreviewMap.Create();
            shadowPreviewCamera = new GameObject("Tutorial shadow preview camera").AddComponent<Camera>();
            shadowPreviewCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
            shadowPreviewCamera.enabled = false;
            shadowPreviewCamera.orthographic = true;
            shadowPreviewCamera.aspect = 1;
            shadowPreviewCamera.nearClipPlane = .1f;
            shadowPreviewCamera.farClipPlane = 150f;
            shadowPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            shadowPreviewCamera.backgroundColor = Color.white;
            shadowPreviewCamera.cullingMask = 1 << ShadowPreviewLayer;
            shadowPreviewCamera.targetTexture = shadowPreviewMap;
            shadowPreviewCamera.allowHDR = false;
            shadowPreviewCamera.allowMSAA = false;
            shadowPreviewCamera.useOcclusionCulling = false;
            return true;
        }

        void ClearShadowPreview()
        {
            ShadowPreviewVisible = false;
            Shader.SetGlobalFloat("_TutorialShadowPreviewActive", 0);
        }

        void DisposeShadowPreview()
        {
            ClearShadowPreview();
            if (shadowPreviewCamera != null) Destroy(shadowPreviewCamera.gameObject);
            if (shadowPreviewMap != null)
            {
                shadowPreviewMap.Release();
                Destroy(shadowPreviewMap);
            }
        }
    }
}
