using System;
using ShadeLink;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GlassTrial
{
    // A non-modal help panel: keyboard input is left available to the stage.
    [DefaultExecutionOrder(-10000)]
    public sealed class HelpHudController : MonoBehaviour
    {
        static HelpHudController instance;
        public static bool PointerInUse => instance != null && instance.pointerInUse;
        public static bool IsPresented => instance != null && instance.state.IsActive;
        public static void EnsureLiveCapture()
        { if (PointerInUse && instance.capture != null) instance.capture.captureEnabled = true; }
        readonly HelpHudState state = new HelpHudState();
        bool pointerInUse, cursorVisible, focused = true;
        CursorLockMode cursorLock;
        int openedFrame;
        Canvas canvas;
        RectTransform panel;
        RawImage background;
        Material glass;
        GlassCameraCapture capture;
        HelpHudFaces pages;
        Font font;
        GUIStyle label, heading, button, prompt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var root = new GameObject("Parallax help HUD");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<HelpHudController>();
            root.AddComponent<HelpHudOcclusion>();
        }

        void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
            var root = new GameObject("Help glass canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var obj = new GameObject("Help glass panel", typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(root.transform, false); panel = obj.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f / 6f); panel.pivot = new Vector2(.5f, .5f);
            background = obj.GetComponent<RawImage>(); background.raycastTarget = false;
            var shader = Resources.Load<Shader>("TutorialIslandGlass");
            if (shader != null && shader.isSupported)
            {
                glass = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                glass.SetFloat("_GlassMode", 1); glass.SetFloat("_CornerRadius", .14f);
                glass.SetFloat("_WhiteTintStrength", .14f); glass.SetFloat("_DarkTintStrength", .26f);
                glass.SetFloat("_Saturation", 1.45f); glass.SetFloat("_ExtraBlurPixels", 9.5f);
                background.material = glass;
            }
            canvas.enabled = false;
            pages = gameObject.AddComponent<HelpHudFaces>();
            pages.Initialize(panel, font);
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void SceneChanged(Scene oldScene, Scene newScene) { Abort(); capture = null; }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Abort(); }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged; Abort();
            if (glass != null) Destroy(glass); if (font != null) Destroy(font);
            if (instance == this) instance = null;
        }
        bool CanOpen()
        {
            return focused && SceneManager.GetActiveScene().name != "MainMenu" &&
                GameProgressRuntime.GameplayActive && !GameProgressRuntime.Paused && !GameProgressRuntime.Transitioning &&
                !ParallaxGameShell.BlocksGameplay && Cursor.lockState == CursorLockMode.Locked;
        }
        void Open()
        {
            if (!CanOpen() || !state.Open()) return;
            cursorVisible = Cursor.visible; cursorLock = Cursor.lockState; openedFrame = Time.frameCount;
            pointerInUse = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Camera cam = PlayerControls.ConfiguredCamera != null ? PlayerControls.ConfiguredCamera : Camera.main;
            capture = cam != null ? cam.GetComponent<GlassCameraCapture>() : null;
            canvas.enabled = true;
        }
        void RestoreCursor()
        {
            if (!pointerInUse) return;
            pointerInUse = false;
            if (focused && !GameProgressRuntime.Paused && !GameProgressRuntime.Transitioning && !ParallaxGameShell.BlocksGameplay)
            { Cursor.lockState = cursorLock; Cursor.visible = cursorVisible; }
        }
        void Abort() { RestoreCursor(); state.Reset(); if (canvas != null) canvas.enabled = false; if (pages != null) pages.Render(state); }
        void Update()
        {
            if (!state.IsActive)
            { if (Input.GetKeyDown(KeyCode.K)) Open(); return; }
            if (!focused || GameProgressRuntime.Paused || GameProgressRuntime.Transitioning || SceneManager.GetActiveScene().name == "MainMenu")
            { Abort(); return; }
            if (Time.frameCount != openedFrame && state.Current != HelpHudState.Phase.Collapsing)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) state.Turn(-1);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) state.Turn(1);
                else if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                { state.Close(); RestoreCursor(); }
            }
            state.Tick(Time.unscaledDeltaTime);
            if (!state.IsActive) { canvas.enabled = false; pages.Render(state); return; }
            float t = state.Expansion;
            panel.sizeDelta = Vector2.Lerp(new Vector2(210, 46), new Vector2(760, 454), t);
            panel.anchoredPosition = new Vector2(0, 200 * t);
            if (glass != null) glass.SetFloat("_PanelAspect", panel.sizeDelta.x / panel.sizeDelta.y);
            background.texture = capture != null && capture.blurred != null ? capture.blurred : Texture2D.whiteTexture;
            if (capture == null || capture.blurred == null) background.color = new Color(.15f, .28f, .30f, .92f);
            else background.color = Color.white;
            if (state.Current == HelpHudState.Phase.Collapsing)
            {
                var color = background.color;
                color.a *= Mathf.Clamp01((t - .12f) / .55f);
                background.color = color;
            }
            pages.Render(state);
        }
        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            heading = new GUIStyle(label) { fontSize = 27, fontStyle = FontStyle.Bold };
            button = new GUIStyle(label) { fontSize = 32 };
            prompt = new GUIStyle(label) { fontSize = 17, alignment = TextAnchor.MiddleRight };
        }
        void OnGUI()
        {
            if (!state.IsActive && CanOpen() && Event.current != null && Event.current.type == EventType.Repaint)
            {
                Styles();
                float s = Mathf.Lerp(Screen.width / 1600f, Screen.height / 900f, .5f);
                var oldMatrix = GUI.matrix;
                var oldColor = GUI.color;
                GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width - 24f * s, Screen.height - 24f * s, 0), Quaternion.identity, Vector3.one * s);
                var rect = new Rect(-178, -32, 178, 28);
                GUI.color = new Color(0, 0, 0, .7f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), "[K] 조작 안내", prompt);
                GUI.color = Color.white;
                GUI.Label(rect, "[K] 조작 안내", prompt);
                GUI.color = oldColor; GUI.matrix = oldMatrix;
            }
            if (!state.IsActive || state.Expansion < .95f || Event.current == null) return;
            Styles();
            float scale = Mathf.Lerp(Screen.width / 1600f, Screen.height / 900f, .5f);
            var old = GUI.matrix; GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width * .5f, Screen.height * (5f / 6f) - 200f * scale, 0), Quaternion.identity, Vector3.one * scale);
            GUI.BeginGroup(new Rect(-380, -227, 760, 454));
            GUI.Label(new Rect(60, 8, 640, 34), "조작 안내", heading);
            if (GUI.Button(new Rect(0, 48, 72, 356), GUIContent.none, GUIStyle.none)) state.Turn(-1);
            if (GUI.Button(new Rect(688, 48, 72, 356), GUIContent.none, GUIStyle.none)) state.Turn(1);
            GUI.Label(new Rect(14, 188, 44, 64), "‹", button);
            GUI.Label(new Rect(702, 188, 44, 64), "›", button);
            GUI.Label(new Rect(150, 411, 460, 32), "아무 키나 누르면 닫기   ·   " + (state.Page + 1) + " / 3", label);
            GUI.EndGroup(); GUI.matrix = old;
        }
    }

    // Temporarily suppress the current map HUD after it has calculated its own
    // visibility, then restore it before the next frame's stage update.
    [DefaultExecutionOrder(10000)]
    public sealed class HelpHudOcclusion : MonoBehaviour
    {
        readonly System.Collections.Generic.Dictionary<Canvas, bool> hidden = new System.Collections.Generic.Dictionary<Canvas, bool>();
        readonly System.Collections.Generic.Dictionary<CanvasGroup, float> faded = new System.Collections.Generic.Dictionary<CanvasGroup, float>();
        bool wasPresented;
        float revealStart = -1f;
        const float RevealSeconds = .25f;
        static bool IsStageHud(string name)
        { return name == "Tutorial glass notice" || name == "Beach glass HUD" || name == "Eclipse glass HUD" || name == "Light exposure glass UI"; }
        void Restore()
        {
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.enabled = pair.Value; hidden.Clear();
            foreach (var pair in faded) if (pair.Key != null) pair.Key.alpha = pair.Value; faded.Clear();
        }
        void Update() { Restore(); }
        void LateUpdate()
        {
            bool presented = HelpHudController.IsPresented;
            if (presented) revealStart = -1f;
            else if (wasPresented) revealStart = Time.unscaledTime;
            wasPresented = presented;
            if (!presented && (revealStart < 0 || Time.unscaledTime - revealStart >= RevealSeconds)) return;
            if (presented) HelpHudController.EnsureLiveCapture();
            foreach (var canvas in FindObjectsOfType<Canvas>())
                if (canvas != null && IsStageHud(canvas.name))
                {
                    if (presented) { hidden[canvas] = canvas.enabled; canvas.enabled = false; }
                    else if (canvas.enabled)
                    {
                        var group = canvas.GetComponent<CanvasGroup>();
                        if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
                        faded[group] = group.alpha;
                        group.alpha *= Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - revealStart) / RevealSeconds));
                    }
                }
        }
        void OnDisable() { Restore(); }
    }
}
