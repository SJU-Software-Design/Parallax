using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    /// <summary>A view-dependent window, rendered only inside the scanned hands' opening.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GalleryCathedralPortal : MonoBehaviour
    {
        public const string PreviewResource = "MuseumPortal/BeachPreview";
        public const string PreviewSkyResource = "MuseumPortal/BeachPreviewSky";
        public const int PreviewLayer = 28;
        public GalleryGame game;
        public GalleryProp prop;
        public Renderer portalSurface;
        public Transform apertureFrame;
        public Bounds apertureBounds;
        public Renderer[] handRenderers;
        public bool RenderingEnabled = true;
        [Range(256, 1024)] public int textureHeight = 768;
        [Range(.5f, 6)] public float destinationWindowHeight = 2.75f;

        public int RenderCount { get; private set; }
        public Vector3 LastSourceEye { get; private set; }
        public Vector3 LastDestinationEye { get; private set; }
        public Matrix4x4 LastProjection { get; private set; }
        public RenderTexture PortalTexture => target;
        public Camera PreviewCamera => previewCamera;
        public int SourceCullingMaskBeforePortal => originalCullingMask;
        public int PreviewRendererCount => previewRoot == null ? 0 : previewRoot.GetComponentsInChildren<Renderer>(true).Length;
        public bool Ready => previewRoot != null && target != null && RenderCount > 0;

        static readonly Vector3 PreviewOrigin = new Vector3(256, -256, 256);
        static readonly Vector3 WindowCenter = new Vector3(0, 1.85f, 0);
        GameObject previewRoot, cameraObject;
        Camera previewCamera, sourceCamera;
        Material originalSurfaceMaterial, ownedSurfaceMaterial;
        RenderTexture target;
        MaterialPropertyBlock surfaceProperties, handProperties;
        int originalCullingMask;
        bool changedCullingMask, setupFailed;
        float touch;

        public void Configure(GalleryGame owner, GalleryProp exhibit, Renderer surface,
            Transform frame, Bounds opening)
        {
            if (owner == null || exhibit == null || surface == null || frame == null)
                throw new ArgumentNullException("A cathedral portal needs its owner, exhibit, surface and aperture frame.");
            if (opening.size.x <= .001f || opening.size.y <= .001f)
                throw new ArgumentException("The cathedral aperture must have a nonzero width and height.");
            game = owner; prop = exhibit; portalSurface = surface; apertureFrame = frame; apertureBounds = opening;
            if (portalSurface.sharedMaterial == null || portalSurface.sharedMaterial.shader == null ||
                portalSurface.sharedMaterial.shader.name != "Parallax/MuseumLibraryPortal")
            {
                var shader = Shader.Find("Parallax/MuseumLibraryPortal");
                if (shader == null) throw new InvalidOperationException("MuseumLibraryPortal shader is missing.");
                var material = new Material(shader) { name = "Cathedral · coastal window" };
                portalSurface.sharedMaterial = material;
                if (!Application.isPlaying) GalleryWorld.Materials.Add(material);
            }
            portalSurface.shadowCastingMode = ShadowCastingMode.Off;
            portalSurface.receiveShadows = false;
            if (handRenderers == null || handRenderers.Length == 0)
                handRenderers = Array.FindAll(exhibit.GetComponentsInChildren<Renderer>(true), r => r != surface);
            if (Application.isPlaying) EnsureRuntime();
        }

        void Start() { if (Application.isPlaying) EnsureRuntime(); }

        bool EnsureRuntime()
        {
            if (previewRoot != null) return true;
            if (setupFailed || game == null || game.view == null || prop == null || portalSurface == null || apertureFrame == null)
                return false;
            var source = Resources.Load<GameObject>(PreviewResource);
            if (source == null)
            {
                setupFailed = true;
                Debug.LogError("CATHEDRAL_PORTAL: Missing render-only beach preview. Run GalleryPortalBeachAssets.Prepare before building.");
                return false;
            }
            sourceCamera = game.view;
            originalCullingMask = sourceCamera.cullingMask;
            sourceCamera.cullingMask &= ~(1 << PreviewLayer);
            changedCullingMask = true;
            previewRoot = Instantiate(source);
            previewRoot.name = "Beach beyond the hands · render-only coastal stage";
            previewRoot.transform.SetPositionAndRotation(PreviewOrigin, Quaternion.identity);
            previewRoot.transform.localScale = Vector3.one;
            foreach (var child in previewRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;

            cameraObject = new GameObject("Cathedral · off-axis beach camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            var coastalSky = Resources.Load<Material>(PreviewSkyResource);
            previewCamera.clearFlags = coastalSky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.60f, .81f, .90f, 1);
            if (coastalSky != null) cameraObject.AddComponent<Skybox>().material = coastalSky;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.nearClipPlane = .045f;
            previewCamera.farClipPlane = 550;
            previewCamera.allowHDR = true; previewCamera.allowMSAA = false;
            previewCamera.renderingPath = RenderingPath.Forward;
            previewCamera.useOcclusionCulling = false;
            cameraObject.transform.rotation = Quaternion.identity;

            originalSurfaceMaterial = portalSurface.sharedMaterial;
            ownedSurfaceMaterial = new Material(originalSurfaceMaterial) { name = "Cathedral · live beach window (instance)" };
            portalSurface.sharedMaterial = ownedSurfaceMaterial;
            surfaceProperties = new MaterialPropertyBlock(); handProperties = new MaterialPropertyBlock();
            EnsureTarget();
            RenderWindow();
            return true;
        }

        void EnsureTarget()
        {
            int height = Mathf.Clamp(textureHeight, 256, 1024);
            int width = Mathf.Clamp(Mathf.CeilToInt(height * apertureBounds.size.x /
                Mathf.Max(.001f, apertureBounds.size.y) / 64) * 64, 256, 1024);
            if (target != null && target.width == width && target.height == height) return;
            ReleaseTarget();
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            target = new RenderTexture(width, height, 24, format, RenderTextureReadWrite.Linear)
            {
                name = "Cathedral · live beach", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, useMipMap = false, antiAliasing = 1
            };
            target.Create(); previewCamera.targetTexture = target;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !RenderingEnabled || !EnsureRuntime()) return;
            bool touched = game.aimed == prop || game.held == prop;
            touch = Mathf.MoveTowards(touch, touched ? 1 : 0, Time.unscaledDeltaTime * 2.3f);
            // Always make one image on initialization; subsequent renders are useful only in the final room.
            float distance = Vector3.Distance(sourceCamera.transform.position, apertureFrame.TransformPoint(apertureBounds.center));
            if (RenderCount == 0 || distance < 24) RenderWindow();
            PublishGlow(distance);
        }

        public void RenderWindow()
        {
            if (previewCamera == null || sourceCamera == null || portalSurface == null || apertureFrame == null) return;
            EnsureTarget();
            float ratio = destinationWindowHeight / Mathf.Max(.01f, apertureBounds.size.y);
            Matrix4x4 mapping = Matrix4x4.Translate(PreviewOrigin + WindowCenter) * Matrix4x4.Scale(Vector3.one * ratio) *
                Matrix4x4.Translate(-apertureBounds.center) * apertureFrame.worldToLocalMatrix;
            Vector3 eye = mapping.MultiplyPoint3x4(sourceCamera.transform.position);
            float plane = PreviewOrigin.z;
            // Keep a finite projection as the player's eyes reach the entry plane; the stage transition owns traversal.
            eye.z = Mathf.Min(eye.z, plane - .12f);
            previewCamera.transform.SetPositionAndRotation(eye, Quaternion.identity);
            float distance = plane - eye.z, near = previewCamera.nearClipPlane;
            // Keep the real beach's broad ocean mesh and distant castle inside the view.
            previewCamera.farClipPlane = Mathf.Max(550, distance + 510);
            float halfWidth = apertureBounds.size.x * ratio * .5f, halfHeight = destinationWindowHeight * .5f;
            Vector3 center = PreviewOrigin + WindowCenter;
            float left = (center.x - halfWidth - eye.x) * near / distance;
            float right = (center.x + halfWidth - eye.x) * near / distance;
            float bottom = (center.y - halfHeight - eye.y) * near / distance;
            float top = (center.y + halfHeight - eye.y) * near / distance;
            previewCamera.projectionMatrix = Matrix4x4.Frustum(left, right, bottom, top, near, previewCamera.farClipPlane);
            previewCamera.Render();

            Matrix4x4 textureProjection = GL.GetGPUProjectionMatrix(previewCamera.projectionMatrix, true) *
                previewCamera.worldToCameraMatrix * mapping;
            portalSurface.GetPropertyBlock(surfaceProperties);
            surfaceProperties.SetTexture("_PortalTexture", target);
            surfaceProperties.SetMatrix("_PortalViewProjection", textureProjection);
            surfaceProperties.SetFloat("_PortalReady", 1);
            // The GPU projection used for the target texture is vertically flipped on top-origin graphics backends.
            // Correct projective sampling as well, so the beach horizon stays above its shoreline.
            surfaceProperties.SetFloat("_FlipY", SystemInfo.graphicsUVStartsAtTop ? 1 : 0);
            surfaceProperties.SetFloat("_TouchRipple", touch);
            surfaceProperties.SetFloat("_PortalNear", Mathf.Clamp01(1 - Vector3.Distance(sourceCamera.transform.position,
                apertureFrame.TransformPoint(apertureBounds.center)) / 8));
            portalSurface.SetPropertyBlock(surfaceProperties);
            LastSourceEye = sourceCamera.transform.position; LastDestinationEye = eye;
            LastProjection = previewCamera.projectionMatrix; RenderCount++;
        }

        void PublishGlow(float distance)
        {
            if (handRenderers == null) return;
            Vector3 center = apertureFrame.TransformPoint(apertureBounds.center);
            float height = apertureFrame.TransformVector(Vector3.up * apertureBounds.size.y).magnitude;
            float strength = .045f + .035f * Mathf.Clamp01(1 - distance / 8) + touch * .025f;
            foreach (var renderer in handRenderers)
            {
                if (renderer == null || renderer == portalSurface) continue;
                renderer.GetPropertyBlock(handProperties);
                handProperties.SetVector("_PortalGlowPosition", center);
                handProperties.SetColor("_PortalGlowColor", new Color(.24f, .78f, 1, 1) * strength);
                handProperties.SetFloat("_PortalGlowRadius", Mathf.Max(.12f, height * .7f));
                renderer.SetPropertyBlock(handProperties);
            }
        }

        void OnDisable() { ReleaseRuntime(); }
        void OnDestroy() { ReleaseRuntime(); }
        void ReleaseTarget()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (target == null) return;
            target.Release(); DestroyOwned(target); target = null;
        }
        void ReleaseRuntime()
        {
            if (changedCullingMask && sourceCamera != null) sourceCamera.cullingMask = originalCullingMask;
            changedCullingMask = false;
            if (handRenderers != null && handProperties != null)
                foreach (var renderer in handRenderers)
                {
                    if (renderer == null || renderer == portalSurface) continue;
                    renderer.GetPropertyBlock(handProperties);
                    handProperties.SetColor("_PortalGlowColor", Color.black);
                    renderer.SetPropertyBlock(handProperties);
                }
            ReleaseTarget();
            if (portalSurface != null && ownedSurfaceMaterial != null)
            {
                portalSurface.GetPropertyBlock(surfaceProperties ?? (surfaceProperties = new MaterialPropertyBlock()));
                // Unity 6 rejects null in MaterialPropertyBlock.SetTexture. A real black fallback also severs the released RT reference.
                surfaceProperties.SetTexture("_PortalTexture", Texture2D.blackTexture); surfaceProperties.SetFloat("_PortalReady", 0);
                portalSurface.SetPropertyBlock(surfaceProperties);
                if (portalSurface.sharedMaterial == ownedSurfaceMaterial) portalSurface.sharedMaterial = originalSurfaceMaterial;
            }
            DestroyOwned(ownedSurfaceMaterial); ownedSurfaceMaterial = null;
            DestroyOwned(previewRoot); previewRoot = null;
            DestroyOwned(cameraObject); cameraObject = null; previewCamera = null; sourceCamera = null;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
