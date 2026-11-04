using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // One movable museum lamp lights the fixed Nike. Physics decides shelter;
    // the lamp camera renders the same real exhibits for the visible silhouette.
    [DefaultExecutionOrder(120)]
    public sealed class GalleryNikeLight : MonoBehaviour
    {
        public GalleryGame game;
        public GalleryProp lamp;
        public Transform head, emitter, nikeRoot;
        public Vector3 AimPoint = new Vector3(0, .025f, 28.2f);
        public bool autoAim = true;
        public float fieldOfView = 60, range = 25, intensity = 18;
        public Color lightColor = new Color(1, .87f, .66f, 1);
        public float shadowBias = .025f;
        public const float RequiredShadeFraction = .65f;
        const int ShadowResolution = 2048;
        const float RefreshSeconds = .1f;

        Camera shadowCamera;
        RenderTexture shadowMap;
        Shader depthShader;
        readonly RaycastHit[] rayHits = new RaycastHit[128];
        readonly List<Occluder> occluders = new List<Occluder>();
        readonly List<Renderer> temporarilyHidden = new List<Renderer>();
        static GalleryNikeLight publishedOwner;
        Vector3 observedPosition;
        Quaternion observedRotation;
        float observedRange, observedFov;
        int poseRevision, sampledRevision = -1, renderedRevision = -1;
        float nextSampleTime, nextRenderTime, cachedShade;
        bool havePose, occludersDirty = true;

        sealed class Occluder
        {
            public Renderer renderer;
            public GalleryProp prop;
            public bool glass;
        }

        bool SourceAvailable => lamp != null && emitter != null && lamp.gameObject.activeInHierarchy && emitter.gameObject.activeInHierarchy;
        public bool Ready => isActiveAndEnabled && SourceAvailable &&
            lamp.placement != null && lamp.placement.Ready && !lamp.placement.Held && (game == null || game.held != lamp);
        public float ShadeReadiness { get { UpdateReadiness(); return cachedShade; } }
        public bool SolutionReady => Ready && CanPlace(out _) && ShadeReadiness >= RequiredShadeFraction;
        public string Status
        {
            get
            {
                if (lamp == null || emitter == null || nikeRoot == null) return "전시 조명을 준비하고 있습니다.";
                if (lamp.placement != null && lamp.placement.Held || game != null && game.held == lamp)
                    return ShadeReadiness >= RequiredShadeFraction ? "그림자가 출구와 이어져요 · 조명을 내려놓아 고정하세요." : "조명을 옮겨 니케의 그림자를 출구 발판으로 보내 보세요.";
                if (!CanPlace(out var reason)) return reason;
                if (!Ready) return "조명을 내려놓고 안착할 때까지 잠깐 기다려 주세요.";
                return SolutionReady ? "니케의 그림자가 출구와 이어졌어요 · 그늘을 따라 이동하세요." :
                    "조명을 왼쪽 앞쪽 바닥으로 옮겨 니케의 그림자를 출구 발판과 이어 보세요.";
            }
        }

        void Awake() { if (game == null) game = GetComponent<GalleryGame>(); }
        void OnEnable()
        {
            havePose = false; sampledRevision = renderedRevision = -1;
            nextSampleTime = nextRenderTime = 0; occludersDirty = true;
        }
        void OnTransformChildrenChanged() { occludersDirty = true; }

        // The builder gives the head a collider-free visual branch inside a
        // fixed physical envelope. Aiming never rotates a cached collision part.
        public void SyncPose()
        {
            if (emitter == null || lamp == null) return;
            if (autoAim && head != null)
            {
                Vector3 direction = AimPoint - head.position;
                if (direction.sqrMagnitude > .000001f)
                {
                    Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .98f ? Vector3.forward : Vector3.up;
                    Quaternion rotation = Quaternion.LookRotation(direction, up);
                    if (Quaternion.Angle(head.rotation, rotation) > .001f) head.rotation = rotation;
                }
            }
            fieldOfView = Mathf.Clamp(fieldOfView, 30, 150); range = Mathf.Clamp(range, 3, 40);
            if (!havePose || Vector3.SqrMagnitude(observedPosition - emitter.position) > .00000025f ||
                Quaternion.Angle(observedRotation, emitter.rotation) > .01f ||
                !Mathf.Approximately(observedRange, range) || !Mathf.Approximately(observedFov, fieldOfView))
            {
                havePose = true; observedPosition = emitter.position; observedRotation = emitter.rotation;
                observedRange = range; observedFov = fieldOfView; poseRevision++;
            }
        }

        public bool CanPlace(out string reason)
        {
            if (lamp == null) { reason = "전시 조명을 찾을 수 없습니다."; return false; }
            Vector3 at = lamp.transform.position;
            if (at.x < -5.3f || at.x > 4.8f || at.z < 16 || at.z > 22.2f)
            { reason = "전원선이 닿는 앞쪽 바닥에 조명을 놓아 주세요."; return false; }
            reason = null; return true;
        }

        bool TryLightRay(Vector3 surface, Vector3 normal, out Vector3 direction, out float distance)
        {
            direction = Vector3.zero; distance = 0;
            if (!isActiveAndEnabled || !SourceAvailable) return false;
            SyncPose();
            Vector3 target = surface + normal * GalleryLighting.SurfaceBias;
            Vector3 offset = target - emitter.position;
            distance = offset.magnitude;
            if (distance < .08f || distance >= range) return false;
            direction = offset / distance;
            if (Vector3.Dot(direction, emitter.forward) <= Mathf.Cos(fieldOfView * .5f * Mathf.Deg2Rad)) return false;
            return Vector3.Dot(normal, -direction) > 0;
        }

        bool Ignore(Collider collider)
        {
            if (collider == null || collider is CharacterController || collider.GetComponentInParent<GalleryLightGlass>() != null) return true;
            var prop = collider.GetComponentInParent<GalleryProp>();
            return prop == lamp || prop != null && prop.placement != null && !prop.placement.Ready;
        }

        Collider FirstBlocker(Vector3 direction, float distance)
        {
            float maximum = Mathf.Max(.001f, distance - .015f), nearest = float.PositiveInfinity;
            Collider blocker = null;
            int count = Physics.RaycastNonAlloc(emitter.position, direction, rayHits, maximum, ~0, QueryTriggerInteraction.Ignore);
            if (count == rayHits.Length)
            {
                // A dense exhibit must not silently lose an occluder when the
                // reusable query buffer fills. This uncommon fallback is exact.
                foreach (var hit in Physics.RaycastAll(emitter.position, direction, maximum, ~0, QueryTriggerInteraction.Ignore))
                    if (!Ignore(hit.collider) && hit.distance < nearest) { nearest = hit.distance; blocker = hit.collider; }
            }
            else for (int i = 0; i < count; i++)
                if (!Ignore(rayHits[i].collider) && rayHits[i].distance < nearest)
                { nearest = rayHits[i].distance; blocker = rayHits[i].collider; }
            return blocker;
        }

        public bool IsIlluminated(Vector3 surface, Vector3 normal)
        {
            return TryLightRay(surface, normal, out var direction, out float distance) && FirstBlocker(direction, distance) == null;
        }

        public bool IsNikeShadow(Vector3 surface)
        {
            if (nikeRoot == null || !TryLightRay(surface, Vector3.up, out var direction, out float distance)) return false;
            var blocker = FirstBlocker(direction, distance);
            return blocker != null && blocker.transform.IsChildOf(nikeRoot);
        }

        void UpdateReadiness()
        {
            if (!isActiveAndEnabled || !SourceAvailable) { cachedShade = 0; sampledRevision = -1; return; }
            SyncPose();
            if (sampledRevision == poseRevision && Time.unscaledTime < nextSampleTime) return;
            int covered = 0;
            for (int z = 0; z < 5; z++) for (int x = 0; x < 5; x++)
                if (IsNikeShadow(new Vector3(Mathf.Lerp(-.65f, .65f, x / 4f), .025f, Mathf.Lerp(27.65f, 28.4f, z / 4f)))) covered++;
            cachedShade = covered / 25f; sampledRevision = poseRevision;
            nextSampleTime = Time.unscaledTime + RefreshSeconds;
        }

        public void ResetLight()
        {
            havePose = false; sampledRevision = renderedRevision = -1;
            cachedShade = 0; nextSampleTime = nextRenderTime = 0; occludersDirty = true;
            SyncPose(); UpdateReadiness();
        }

        void EnsureResources()
        {
            if (shadowCamera != null) return;
            depthShader = Shader.Find("Hidden/Parallax/MuseumNikeDepth");
            if (depthShader == null || !depthShader.isSupported)
                throw new System.InvalidOperationException("The museum Nike depth shader is unavailable.");
            var cameraObject = new GameObject("Nike spotlight · real geometry shadow camera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(transform, false);
            shadowCamera = cameraObject.AddComponent<Camera>(); shadowCamera.enabled = false;
            shadowCamera.renderingPath = RenderingPath.Forward;
            shadowCamera.clearFlags = CameraClearFlags.SolidColor; shadowCamera.backgroundColor = Color.white;
            shadowCamera.allowHDR = false; shadowCamera.allowMSAA = false;
            shadowCamera.nearClipPlane = .08f; shadowCamera.aspect = 1;
            shadowMap = new RenderTexture(ShadowResolution, ShadowResolution, 24, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            { name = "Nike lamp · actual mesh depth", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.DontSave };
            shadowMap.Create(); shadowCamera.targetTexture = shadowMap;
            occludersDirty = true;
        }

        void RefreshOccluders()
        {
            occluders.Clear();
            foreach (var renderer in game.GetComponentsInChildren<Renderer>(true))
                occluders.Add(new Occluder { renderer = renderer, prop = renderer.GetComponentInParent<GalleryProp>(),
                    glass = renderer.GetComponentInParent<GalleryLightGlass>() != null });
            occludersDirty = false;
        }

        void Publish()
        {
            Vector3 position = emitter.position, forward = emitter.forward;
            publishedOwner = this;
            Shader.SetGlobalVector("_NikeLightPositionRange", new Vector4(position.x, position.y, position.z, range));
            Shader.SetGlobalVector("_NikeLightDirectionCone", new Vector4(forward.x, forward.y, forward.z, Mathf.Cos(fieldOfView * .5f * Mathf.Deg2Rad)));
            Shader.SetGlobalVector("_NikeLightColorIntensity", new Vector4(lightColor.r, lightColor.g, lightColor.b, intensity));
            // Forward metres/far in RFloat avoids reversed-Z comparisons. The
            // actual DX11 capture verified a Y flip for this RT projection.
            float flipY = SystemInfo.graphicsUVStartsAtTop ? 1 : 0;
            Shader.SetGlobalVector("_NikeShadowParams", new Vector4(range, shadowBias, 1, flipY));
            Shader.SetGlobalMatrix("_NikeWorldToShadowClip", GL.GetGPUProjectionMatrix(shadowCamera.projectionMatrix, true) * shadowCamera.worldToCameraMatrix);
            Shader.SetGlobalTexture("_NikeShadowMap", shadowMap);
            Shader.SetGlobalVector("_NikeShadowMap_TexelSize", new Vector4(1f / ShadowResolution, 1f / ShadowResolution, ShadowResolution, ShadowResolution));
            Shader.SetGlobalFloat("_NikeLightEnabled", 1);
        }

        void RenderShadow()
        {
            shadowCamera.transform.SetPositionAndRotation(emitter.position, emitter.rotation);
            shadowCamera.fieldOfView = fieldOfView; shadowCamera.farClipPlane = range;
            shadowCamera.cullingMask = game.view == null ? ~0 : game.view.cullingMask;
            if (occludersDirty) RefreshOccluders();
            temporarilyHidden.Clear();
            var previous = RenderTexture.active;
            try
            {
                foreach (var entry in occluders)
                {
                    var renderer = entry.renderer;
                    if (renderer == null || !renderer.enabled) continue;
                    if (entry.glass || entry.prop == lamp || entry.prop != null && entry.prop.placement != null && !entry.prop.placement.Ready)
                    { temporarilyHidden.Add(renderer); renderer.enabled = false; }
                }
                Publish();
                shadowCamera.RenderWithShader(depthShader, "RenderType");
            }
            finally
            {
                foreach (var renderer in temporarilyHidden) if (renderer != null) renderer.enabled = true;
                temporarilyHidden.Clear(); RenderTexture.active = previous;
            }
            renderedRevision = poseRevision; nextRenderTime = Time.unscaledTime + RefreshSeconds;
        }

        void LateUpdate()
        {
            if (game == null || !game.conceptLayout) { ClearPublishedLight(); return; }
            if (!SourceAvailable)
            {
                // The roofed room remains independent of sunlight even when
                // its lamp is removed. An empty source must not display the
                // last lamp pose or retain a previously completed shade sample.
                publishedOwner = this;
                Shader.SetGlobalFloat("_NikeLightEnabled", 1);
                Shader.SetGlobalVector("_NikeLightPositionRange", Vector4.zero);
                Shader.SetGlobalVector("_NikeLightDirectionCone", new Vector4(0, -1, 0, .5f));
                Shader.SetGlobalVector("_NikeLightColorIntensity", Vector4.zero);
                Shader.SetGlobalVector("_NikeShadowParams", new Vector4(1, 0, 0, 0));
                Shader.SetGlobalMatrix("_NikeWorldToShadowClip", Matrix4x4.identity);
                Shader.SetGlobalTexture("_NikeShadowMap", Texture2D.whiteTexture);
                Shader.SetGlobalVector("_NikeShadowMap_TexelSize", Vector4.one);
                cachedShade = 0; havePose = false; sampledRevision = renderedRevision = -1;
                nextSampleTime = nextRenderTime = 0; occludersDirty = true;
                return;
            }
            SyncPose(); UpdateReadiness(); EnsureResources();
            if (renderedRevision != poseRevision || Time.unscaledTime >= nextRenderTime) RenderShadow();
            else Publish();
        }

        void ClearPublishedLight()
        {
            if (publishedOwner == this)
            {
                Shader.SetGlobalFloat("_NikeLightEnabled", 0);
                Shader.SetGlobalTexture("_NikeShadowMap", Texture2D.whiteTexture);
                publishedOwner = null;
            }
        }

        void ReleaseResources()
        {
            ClearPublishedLight();
            if (shadowCamera != null)
            {
                shadowCamera.targetTexture = null;
                if (Application.isPlaying) Destroy(shadowCamera.gameObject); else DestroyImmediate(shadowCamera.gameObject);
                shadowCamera = null;
            }
            if (shadowMap != null)
            {
                shadowMap.Release();
                if (Application.isPlaying) Destroy(shadowMap); else DestroyImmediate(shadowMap);
                shadowMap = null;
            }
            renderedRevision = -1; occluders.Clear(); temporarilyHidden.Clear(); occludersDirty = true;
        }
        void OnDisable() { ReleaseResources(); }
        void OnDestroy() { ReleaseResources(); }
    }
}
