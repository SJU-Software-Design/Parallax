using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ShadeLink.Beach;

namespace ShadeLink.Gallery
{
    public enum GalleryPassagePhase { Inactive, EntryFade, Falling, LoadingDestination, ArrivalFade, Complete, Failed }

    // The actual enlarged-hand crossing owns entry. Only the falling camera and
    // a seamless, render-only space survive until the real Beach is ready.
    public sealed class GalleryStageTransition : MonoBehaviour
    {
        public const float EntryFadeDuration = .28f, FallDuration = 2.35f, FallMetres = 14, ArrivalFadeDuration = .55f;
        public const int FallLayer = 27;
        public const float MaximumClockStep = .05f;
        public const float FallFarClip = 80, HazeStart = 12, HazeEnd = 30;
        public static readonly Color HazeColor = new Color(.24f, .21f, .17f, 1);
        public bool IsTransitioning { get; private set; }
        public float FallDistance { get; private set; }
        public float FallElapsed { get; private set; }
        public float FallSpeed { get; private set; }
        public bool DestinationReady { get; private set; }
        public GalleryPassagePhase Phase { get; private set; }
        public float FadeOpacity => fade;
        public Vector3 EntryFramePosition { get; private set; }
        public Quaternion EntryFrameRotation { get; private set; }
        public float EntryFrameFieldOfView { get; private set; }
        public Vector3 FallOrigin { get; private set; }
        public Renderer FallSpaceRenderer => spaceRenderer;
        public GalleryFallAudio FallAudio { get; private set; }
        float fade;
        bool automatedHandoff, cameraIsolated, focused, applicationPaused;
        int resumeFrame = -1;
        Camera sourceCamera;
        int sourceMask;
        CameraClearFlags sourceClear;
        Color sourceBackground;
        float sourceFar;
        bool sourceOcclusion, sourcePostFx;
        GalleryConceptPostFX postFx;
        GameObject fallSpace;
        Mesh spaceMesh;
        Material spaceMaterial;
        MeshRenderer spaceRenderer;

        void Awake() { focused = Application.isFocused; DontDestroyOnLoad(gameObject); }

        public bool Begin(GalleryGame game, GalleryOrcusExit entrance)
        {
            if (IsTransitioning || game == null || game.view == null || entrance == null ||
                !Application.CanStreamedLevelBeLoaded("Beach")) return false;
            IsTransitioning = true;
            automatedHandoff = game.automated;
            if (automatedHandoff) SceneManager.sceneLoaded += KeepAutomatedHandoffRunning;
            StartCoroutine(Passage(game, entrance));
            return true;
        }

        void KeepAutomatedHandoffRunning(Scene scene, LoadSceneMode mode)
        {
            if (!automatedHandoff || scene.name != "Beach") return;
            var beach = Object.FindFirstObjectByType<BeachGame>();
            if (beach != null) beach.automated = true;
            Application.runInBackground = true;
        }
        bool FocusSuspended => !automatedHandoff && (!focused || applicationPaused);
        void OnApplicationFocus(bool value)
        {
            if (value && !focused) resumeFrame = Time.frameCount;
            focused = value;
        }
        void OnApplicationPause(bool value)
        {
            if (!value && applicationPaused) resumeFrame = Time.frameCount;
            applicationPaused = value;
        }
        float ClockDelta(float rawDelta)
        {
            if (FocusSuspended || !automatedHandoff && Time.frameCount == resumeFrame) return 0;
            return Mathf.Clamp(rawDelta, 0, MaximumClockStep);
        }

        IEnumerator Passage(GalleryGame game, GalleryOrcusExit entrance)
        {
            bool automated = game.automated;
            sourceCamera = game.view;
            Vector3 localEye = entrance.transform.InverseTransformPoint(sourceCamera.transform.position);
            float frontMargin = .14f / Mathf.Max(.001f, Mathf.Abs(entrance.transform.lossyScale.z));
            localEye.z = Mathf.Min(localEye.z, entrance.EntryPlane - frontMargin);
            EntryFramePosition = entrance.transform.TransformPoint(localEye);
            EntryFrameRotation = sourceCamera.transform.rotation;
            EntryFrameFieldOfView = sourceCamera.fieldOfView;
            Phase = GalleryPassagePhase.EntryFade;
            for (float time = 0; time < EntryFadeDuration;)
            {
                sourceCamera.transform.SetPositionAndRotation(EntryFramePosition, EntryFrameRotation);
                fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / EntryFadeDuration));
                yield return null;
                time += ClockDelta(Time.unscaledDeltaTime);
            }
            fade = 1;
            yield return null;
            BuildFallSpace();
            IsolateCamera();
            FallAudio = GalleryFallAudio.Install(gameObject);
            FallAudio.BeginFall(automated);
            Phase = GalleryPassagePhase.Falling;
            for (float time = 0; time < FallDuration;)
            {
                float t = Mathf.Clamp01(time / FallDuration);
                FallElapsed = time;
                FallDistance = FallMetres * t * t;
                FallSpeed = 2 * FallMetres * t / FallDuration;
                sourceCamera.transform.SetPositionAndRotation(FallOrigin + Vector3.down * FallDistance,
                    Quaternion.Euler(Mathf.Lerp(28, 58, t), EntryFrameRotation.eulerAngles.y, 0));
                // Preserve the configured FOV; no added roll, shake or zoom.
                sourceCamera.fieldOfView = EntryFrameFieldOfView;
                fade = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / .16f));
                if (time > FallDuration - .22f)
                    fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - FallDuration + .22f) / .22f));
                FallAudio.TickProgress(t);
                yield return null;
                if (!FallAudio.PlaybackSuspended) time += ClockDelta(Time.unscaledDeltaTime);
            }
            FallElapsed = FallDuration; FallDistance = FallMetres; FallSpeed = 2 * FallMetres / FallDuration;
            sourceCamera.transform.position = FallOrigin + Vector3.down * FallDistance;
            fade = 1;
            FallAudio.TickProgress(1);
            FallAudio.BeginArrival();
            Phase = GalleryPassagePhase.LoadingDestination;
            RestoreCamera();
            sourceCamera.transform.SetPositionAndRotation(EntryFramePosition, EntryFrameRotation);
            ReleaseFallSpace();
            // A fully opaque frame separates fall motion from the actual swap.
            yield return null;
            AsyncOperation load = SceneManager.LoadSceneAsync("Beach", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            SceneManager.sceneLoaded -= KeepAutomatedHandoffRunning;
            yield return null;
            var beach = Object.FindFirstObjectByType<BeachGame>();
            if (beach == null)
            {
                Phase = GalleryPassagePhase.Failed; fade = 0; IsTransitioning = false;
                FallAudio.Cancel();
                Debug.LogError("MUSEUM_PASSAGE: Beach scene has no BeachGame");
                Destroy(gameObject); yield break;
            }
            beach.automated = automated; Application.runInBackground = automated;
            Application.targetFrameRate = automated ? 90 : -1; QualitySettings.vSyncCount = automated ? 0 : 1;
            beach.checkpoint = 0; beach.yaw = 0; beach.pitch = -.08f;
            beach.PlacePlayer(BeachWorld.Checkpoints[0] + Vector3.up * .35f);
            beach.enabled = false; beach.active = beach.started = true;
            if (!automated) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Phase = GalleryPassagePhase.ArrivalFade;
            for (float time = 0; time < ArrivalFadeDuration;)
            {
                float t = Mathf.Clamp01(time / ArrivalFadeDuration);
                fade = 1 - Mathf.SmoothStep(0, 1, t);
                FallAudio.TickArrival(t);
                yield return null;
                // Beach intentionally holds timeScale=0 until Resume. This is
                // arrival input protection rather than a pause of the passage.
                time += ClockDelta(Time.unscaledDeltaTime);
            }
            fade = 0; FallAudio.Finish();
            beach.enabled = true; beach.Resume(); DestinationReady = true;
            beach.Tell("두 손 사이의 빛을 지나, 바닷바람이 부는 해변에 내려앉았습니다.", 6);
            Phase = GalleryPassagePhase.Complete;
            Debug.Log("MUSEUM_BEACH_HANDOFF_OK");
            IsTransitioning = false; Destroy(gameObject);
        }

        void BuildFallSpace()
        {
            // Separate position and layer both protect against museum scenery.
            // The two open ends lie farther away than any visible camera ray;
            // distance haze also reaches the exact clear color well before far.
            FallOrigin = new Vector3(2048, -1024, 2048);
            fallSpace = new GameObject("Hand passage · seamless falling space");
            fallSpace.transform.SetParent(transform, false); fallSpace.layer = FallLayer;
            fallSpace.transform.position = FallOrigin;
            var filter = fallSpace.AddComponent<MeshFilter>();
            spaceRenderer = fallSpace.AddComponent<MeshRenderer>();
            spaceRenderer.shadowCastingMode = ShadowCastingMode.Off; spaceRenderer.receiveShadows = false;
            const int segments = 128; const float radius = 3.4f;
            float extent = FallFarClip + FallMetres + 20;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length]; var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                vertices[i * 2] = radial + Vector3.up * extent;
                vertices[i * 2 + 1] = radial - Vector3.up * extent;
                uv[i * 2] = new Vector2((float)i / segments, extent);
                uv[i * 2 + 1] = new Vector2((float)i / segments, -extent);
                if (i == segments) continue;
                int at = i * 6, v = i * 2;
                indices[at] = v; indices[at + 1] = v + 1; indices[at + 2] = v + 2;
                indices[at + 3] = v + 2; indices[at + 4] = v + 1; indices[at + 5] = v + 3;
            }
            spaceMesh = new Mesh { name = "Hand fall · continuous open interior, no caps or floor" };
            spaceMesh.vertices = vertices; spaceMesh.uv = uv; spaceMesh.triangles = indices;
            spaceMesh.RecalculateBounds(); spaceMesh.RecalculateNormals(); filter.sharedMesh = spaceMesh;
            spaceMaterial = new Material(Shader.Find("Parallax/GalleryFallSpace")) { name = "Hand fall · warm stone fading into arrival light" };
            spaceMaterial.SetColor("_HazeColor", HazeColor);
            spaceMaterial.SetFloat("_HazeStart", HazeStart); spaceMaterial.SetFloat("_HazeEnd", HazeEnd);
            spaceRenderer.sharedMaterial = spaceMaterial;
        }
        void IsolateCamera()
        {
            sourceMask = sourceCamera.cullingMask; sourceClear = sourceCamera.clearFlags;
            sourceBackground = sourceCamera.backgroundColor; sourceFar = sourceCamera.farClipPlane;
            sourceOcclusion = sourceCamera.useOcclusionCulling;
            postFx = sourceCamera.GetComponent<GalleryConceptPostFX>();
            if (postFx != null) { sourcePostFx = postFx.EffectEnabled; postFx.EffectEnabled = false; }
            sourceCamera.cullingMask = 1 << FallLayer;
            sourceCamera.clearFlags = CameraClearFlags.SolidColor; sourceCamera.backgroundColor = HazeColor;
            sourceCamera.farClipPlane = FallFarClip; sourceCamera.useOcclusionCulling = false;
            cameraIsolated = true;
        }
        void RestoreCamera()
        {
            if (!cameraIsolated || sourceCamera == null) return;
            sourceCamera.cullingMask = sourceMask; sourceCamera.clearFlags = sourceClear;
            sourceCamera.backgroundColor = sourceBackground; sourceCamera.farClipPlane = sourceFar;
            sourceCamera.useOcclusionCulling = sourceOcclusion; sourceCamera.fieldOfView = EntryFrameFieldOfView;
            if (postFx != null) postFx.EffectEnabled = sourcePostFx;
            cameraIsolated = false;
        }
        void ReleaseFallSpace()
        {
            if (fallSpace != null) Destroy(fallSpace);
            if (spaceMesh != null) Destroy(spaceMesh);
            if (spaceMaterial != null) Destroy(spaceMaterial);
            fallSpace = null; spaceMesh = null; spaceMaterial = null; spaceRenderer = null;
        }
        void OnGUI()
        {
            if (!IsTransitioning || fade <= 0) return;
            Color old = GUI.color; int oldDepth = GUI.depth; Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -10000; GUI.matrix = Matrix4x4.identity; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old; GUI.depth = oldDepth; GUI.matrix = oldMatrix;
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= KeepAutomatedHandoffRunning;
            RestoreCamera(); ReleaseFallSpace();
            if (FallAudio != null) FallAudio.Cancel();
        }
    }
}