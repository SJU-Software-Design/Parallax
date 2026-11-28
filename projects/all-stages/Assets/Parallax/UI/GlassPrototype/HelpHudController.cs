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
        static bool tutorialIntroPending;
        static int escapeConsumedFrame = -1, clickConsumedFrame = -1;
        public static void RequestTutorialIntro() { tutorialIntroPending = true; }
        public static bool EscapeConsumed => escapeConsumedFrame == Time.frameCount;
        public static bool ClickConsumed => clickConsumedFrame == Time.frameCount;
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
        bool ownsFont;
        GUIStyle label, heading, button, prompt;
        Texture2D dotTexture, dotGlow, navGradient;
        Texture2D shortcutPill, shortcutKey;
        bool ownsShortcutCapture;
        GlassCameraCapture shortcutCapture;
        Material shortcutGlass, shortcutShadow;
        GUIStyle shortcutText, shortcutKeyText;
        GUIStyle hintText, hintTitle;
        Material hintGlass, hintShadow;
        EclipseHintOverlay eclipseHintOverlay;
        public static bool EclipseHintTransitioning => instance != null && instance.eclipseHintOverlay != null && instance.eclipseHintOverlay.Transitioning;
        int hintCaptureFrame = -100;
        readonly float[] shortcutPress = new float[3];
        float leftHover, rightHover, leftPress, rightPress;
        readonly float[] indicator = new float[HelpHudState.PageCount];

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
            font = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Regular");
            if (font == null) { font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24); ownsFont = true; }
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
        void SceneChanged(Scene oldScene, Scene newScene) { Abort(); capture = null; if (newScene.name != "Tutorial") tutorialIntroPending = false; }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Abort(); }
        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged; Abort();
            if (glass != null) Destroy(glass); if (ownsFont && font != null) Destroy(font);
            if (dotTexture != null) Destroy(dotTexture); if (dotGlow != null) Destroy(dotGlow);
            if (navGradient != null) Destroy(navGradient);
            if (shortcutPill != null) Destroy(shortcutPill); if (shortcutKey != null) Destroy(shortcutKey);
            if (shortcutGlass != null) Destroy(shortcutGlass);
            if (shortcutShadow != null) Destroy(shortcutShadow);
            if (hintGlass != null) Destroy(hintGlass); if (hintShadow != null) Destroy(hintShadow);
            if (ownsShortcutCapture && shortcutCapture != null) Destroy(shortcutCapture);
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
            leftHover = rightHover = leftPress = rightPress = 0;
            for (int i = 0; i < indicator.Length; i++) indicator[i] = i == 0 ? 1 : 0;
            cursorVisible = Cursor.visible; cursorLock = Cursor.lockState; openedFrame = Time.frameCount;
            pointerInUse = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Camera cam = PlayerControls.ConfiguredCamera != null ? PlayerControls.ConfiguredCamera : Camera.main;
            capture = cam != null ? cam.GetComponent<GlassCameraCapture>() : null;
            canvas.enabled = true;
            UpdatePresentation();
        }
        void RestoreCursor()
        {
            if (!pointerInUse) return;
            pointerInUse = false;
            if (focused && !GameProgressRuntime.Paused && !GameProgressRuntime.Transitioning && !ParallaxGameShell.BlocksGameplay)
            {
                Cursor.lockState = cursorLock;
                // A locked gameplay cursor must not leave the menu arrow visible.
                Cursor.visible = cursorLock == CursorLockMode.Locked ? false : cursorVisible;
            }
        }
        void Abort() { RestoreCursor(); state.Reset(); if (canvas != null) canvas.enabled = false; if (pages != null) pages.Render(state); }
        void Update()
        {
            KeyCode[] keys = { KeyCode.Escape, KeyCode.K, PlayerControls.KeyFor(PlayerControls.Action.Hints) };
            for (int i = 0; i < keys.Length; i++)
                shortcutPress[i] = Mathf.MoveTowards(shortcutPress[i], Input.GetKey(keys[i]) ? 1 : 0, Time.unscaledDeltaTime / .10f);
            if (!state.IsActive)
            {
                if (tutorialIntroPending && SceneManager.GetActiveScene().name == "Tutorial" && CanOpen())
                { tutorialIntroPending = false; Open(); }
                else if (Input.GetKeyDown(KeyCode.K)) Open();
                return;
            }
            if (!focused || GameProgressRuntime.Paused || GameProgressRuntime.Transitioning || SceneManager.GetActiveScene().name == "MainMenu")
            { Abort(); return; }
            if (Time.frameCount != openedFrame && state.Current != HelpHudState.Phase.Collapsing)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                { escapeConsumedFrame = Time.frameCount; state.Close(); RestoreCursor(); }
                else if (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null))
                { clickConsumedFrame = Time.frameCount; state.Close(); RestoreCursor(); }
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) state.Turn(-1);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) state.Turn(1);
                else if (Input.anyKeyDown && !PointerButtonPressed())
                { state.Close(); RestoreCursor(); }
            }
            else if (state.Current == HelpHudState.Phase.Collapsing && Input.GetKeyDown(KeyCode.Escape)) escapeConsumedFrame = Time.frameCount;
            state.Tick(Time.unscaledDeltaTime);
            int selected = state.Current == HelpHudState.Phase.Turning ? state.NextPage : state.Page;
            for (int i = 0; i < indicator.Length; i++)
                indicator[i] = Mathf.MoveTowards(indicator[i], i == selected ? 1 : 0, Time.unscaledDeltaTime / .32f);
            if (!state.IsActive) { canvas.enabled = false; pages.Render(state); return; }
            UpdatePresentation();
            UpdateNavigationHighlight();
        }
        static bool PointerButtonPressed()
        {
            // Side buttons remain owned by the help HUD just like the main buttons.
            for (int button = 0; button <= 6; button++)
                if (Input.GetMouseButtonDown(button)) return true;
            return false;
        }
        void UpdatePresentation()
        {
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
        void LateUpdate()
        {
            bool shortcutVisible = !state.IsActive && CanOpen() && GameShellSettingsRuntime.ShowControlHints;
            bool hintVisible = !state.IsActive && CanOpen() && Time.frameCount - hintCaptureFrame <= 1;
            if (!shortcutVisible && !hintVisible) return;
            Camera cam = PlayerControls.ConfiguredCamera != null ? PlayerControls.ConfiguredCamera : Camera.main;
            if (cam == null) return;
            if (shortcutCapture == null || shortcutCapture.gameObject != cam.gameObject)
            {
                if (ownsShortcutCapture && shortcutCapture != null) Destroy(shortcutCapture);
                shortcutCapture = cam.GetComponent<GlassCameraCapture>(); ownsShortcutCapture = shortcutCapture == null;
                if (ownsShortcutCapture) shortcutCapture = cam.gameObject.AddComponent<GlassCameraCapture>();
                if (shortcutCapture.blurShader == null) shortcutCapture.blurShader = Resources.Load<Shader>("GlassBlur");
            }
            shortcutCapture.captureEnabled = true;
        }
        public static void DrawHintPanel(string title, string content, float right, float bottom, float width)
        {
            if (instance == null || instance.state.IsActive || string.IsNullOrEmpty(content)) return;
            instance.RenderHintPanel(title, content, right, bottom, width);
        }
        public static void DrawEclipseHintPanel(string title, string content, int page, float right, float bottom, float width)
        {
            if (instance == null || instance.state.IsActive || string.IsNullOrEmpty(content)) return;
            if (instance.eclipseHintOverlay == null) instance.eclipseHintOverlay = instance.gameObject.AddComponent<EclipseHintOverlay>();
            instance.RenderHintPanel(title, content, right, bottom, width, page);
        }
        void RenderHintPanel(string title, string content, float right, float bottom, float width, int eclipsePage = 0)
        {
            hintCaptureFrame = Time.frameCount;
            if (hintText == null)
            {
                hintText = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
                hintTitle = new GUIStyle(hintText) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.40f, 1f, .91f, 1f) } };
            }
            float textScale = GameShellSettingsRuntime.TextScale;
            hintText.fontSize = Mathf.RoundToInt(19 * textScale); hintTitle.fontSize = Mathf.RoundToInt(17 * textScale);
            float bodyHeight = hintText.CalcHeight(new GUIContent(content), width - 48);
            float height = Mathf.Max(82, bodyHeight + (eclipsePage > 0 ? 85 : 62));
            var rect = new Rect(right - width, bottom - height, width, height);
            var previous = GUI.color; GUI.color = Color.white;
            if (Event.current.type == EventType.Repaint)
            {
                if (hintGlass == null)
                {
                    var shader = Resources.Load<Shader>("TutorialIslandGlass");
                    if (shader != null && shader.isSupported)
                    {
                        hintGlass = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                        hintGlass.SetFloat("_GlassMode", 1); hintGlass.SetFloat("_CornerRadius", .23f);
                        hintGlass.SetFloat("_Refraction", 1); hintGlass.SetFloat("_RefractionStrength", 9); hintGlass.SetFloat("_RefractionWidth", .18f);
                        hintGlass.SetFloat("_LensDistortion", 0); hintGlass.SetFloat("_EdgeContrast", 1); hintGlass.SetFloat("_EdgeContrastStrength", .35f);
                        hintGlass.SetFloat("_SpectrumPixels", 3); hintGlass.SetFloat("_WhiteTintStrength", .14f); hintGlass.SetFloat("_DarkTintStrength", .26f);
                        hintGlass.SetFloat("_Saturation", 1.45f); hintGlass.SetFloat("_ExtraBlurPixels", 9.5f); hintGlass.SetFloat("_MilkyTint", 0);
                    }
                    var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
                    if (shadowShader != null && shadowShader.isSupported)
                    {
                        hintShadow = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave };
                        hintShadow.SetFloat("_Padding", 13); hintShadow.SetFloat("_FadeDistance", 11);
                        hintShadow.SetFloat("_CornerRadiusPixels", 18); hintShadow.SetColor("_Color", new Color(0, 0, 0, .38f));
                    }
                }
                bool eclipseOverlayActive = eclipsePage > 0 && eclipseHintOverlay != null && eclipseHintOverlay.enabled;
                if (shortcutCapture != null && shortcutCapture.blurred != null && hintGlass != null)
                {
                    if (hintShadow != null && !eclipseOverlayActive)
                    {
                        hintShadow.SetColor("_Color", new Color(0, 0, 0, .38f));
                        hintShadow.SetVector("_PanelSize", new Vector4(width, height, 0, 0));
                        Graphics.DrawTexture(new Rect(rect.x - 13, rect.y - 10, width + 26, height + 26), Texture2D.whiteTexture, hintShadow);
                    }
                    hintGlass.SetFloat("_PanelAspect", width / height);
                    if (!eclipseOverlayActive) Graphics.DrawTexture(rect, shortcutCapture.blurred, hintGlass);
                }
                else if (!eclipseOverlayActive) { GUI.color = new Color(.035f, .065f, .08f, .93f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
            }
            if (eclipsePage > 0 && eclipseHintOverlay != null && eclipseHintOverlay.enabled)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    float scale;
                    Rect screenRect = HintPanelScreenRect(GUI.matrix, new Vector2(Screen.width, Screen.height),
                        right, bottom, width, height, out scale);
                    if (hintShadow != null) { hintShadow.SetColor("_Color", Color.white); hintShadow.SetVector("_PanelSize", new Vector4(width, height, 0, 0)); }
                    eclipseHintOverlay.Present(title, content, eclipsePage, screenRect, scale,
                        shortcutCapture != null ? shortcutCapture.blurred : null, hintGlass, hintShadow);
                }
            }
            else
            {
                GUI.Label(new Rect(rect.x + 24, rect.y + 13, width - 48, 26), title, hintTitle);
                GUI.Label(new Rect(rect.x + 24, rect.y + 42, width - 48, bodyHeight + 4), content, hintText);
            }
            GUI.color = previous;
        }
        // Align IMGUI navigation with the actual CanvasScaler and animated panel.
        public static Matrix4x4 PanelGuiMatrix(RectTransform target, Canvas owner, float screenHeight)
        {
            var camera = owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            return Matrix4x4.TRS(new Vector3(center.x, screenHeight - center.y, 0), Quaternion.identity,
                new Vector3(owner.scaleFactor, owner.scaleFactor, 1));
        }
        // Legacy map HUDs use different X/Y scales. Preserve their screen-space
        // corner while keeping the TMP panel, glyphs and padding uniformly scaled.
        public static Rect HintPanelScreenRect(Matrix4x4 guiMatrix, Vector2 viewport,
            float right, float bottom, float width, float height, out float scale)
        {
            Vector3 corner = guiMatrix.MultiplyPoint3x4(new Vector3(right, bottom, 0));
            scale = Mathf.Max(.0001f, Mathf.Min(Mathf.Abs(guiMatrix.m00), Mathf.Abs(guiMatrix.m11)));
            float margin = Mathf.Min(12f * scale, Mathf.Min(viewport.x, viewport.y) * .05f);
            scale = Mathf.Min(scale, Mathf.Max(1f, viewport.x - 2 * margin) / width,
                Mathf.Max(1f, viewport.y - 2 * margin) / height);
            float panelWidth = width * scale, panelHeight = height * scale;
            float x = Mathf.Clamp(corner.x - panelWidth, margin, Mathf.Max(margin, viewport.x - margin - panelWidth));
            float y = Mathf.Clamp(corner.y - panelHeight, margin, Mathf.Max(margin, viewport.y - margin - panelHeight));
            return new Rect(x, y, panelWidth, panelHeight);
        }
        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            var boldFont = Resources.Load<Font>("HelpHudFonts/NotoSansKR-Bold");
            heading = new GUIStyle(label) { font = boldFont != null ? boldFont : font, fontSize = 27, fontStyle = boldFont != null ? FontStyle.Normal : FontStyle.Bold };
            button = new GUIStyle(label) { fontSize = 32 };
            prompt = new GUIStyle(label) { fontSize = 17, alignment = TextAnchor.MiddleRight };
        }
        static Texture2D IndicatorTexture(bool glow)
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f - n * .5f) / (n * .5f), dy = (y + .5f - n * .5f) / (n * .5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = glow ? Mathf.Pow(Mathf.Clamp01(1f - d), 2f) : Mathf.Clamp01((1f - d) * 16f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            tex.SetPixels32(pixels); tex.Apply(); return tex;
        }
        void UpdateNavigationHighlight()
        {
            Vector3 guiPoint = PanelGuiMatrix(panel, canvas, Screen.height).inverse.MultiplyPoint3x4(
                new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0));
            Vector2 pos = new Vector2(guiPoint.x + 380, guiPoint.y + 227);
            bool active = pointerInUse && state.Expansion >= .95f && state.Current != HelpHudState.Phase.Collapsing;
            bool left = active && new Rect(0, 48, 110, 356).Contains(pos);
            bool right = active && new Rect(650, 48, 110, 356).Contains(pos);
            float dt = Time.unscaledDeltaTime;
            leftHover = Mathf.MoveTowards(leftHover, left ? 1 : 0, dt / .25f);
            rightHover = Mathf.MoveTowards(rightHover, right ? 1 : 0, dt / .25f);
            leftPress = Mathf.MoveTowards(leftPress, left && Input.GetMouseButton(0) ? 1 : 0, dt / .08f);
            rightPress = Mathf.MoveTowards(rightPress, right && Input.GetMouseButton(0) ? 1 : 0, dt / .08f);
        }
        static Texture2D MakeNavGradient()
        {
            const int w = 110, h = 454; const float radius = 25f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, 0f);
                float dy = Mathf.Max(radius - Mathf.Min(y + .5f, h - y - .5f), 0f);
                float clip = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                float alpha = Mathf.Pow(1f - (float)x / (w - 1), 1.6f) * clip;
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            tex.SetPixels32(pixels); tex.Apply(); return tex;
        }
        void DrawNavigationHighlight()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (navGradient == null) navGradient = MakeNavGradient();
            var oldColor = GUI.color;
            GUI.color = new Color(0f, .66f, .78f, .16f * leftHover + .10f * leftPress);
            GUI.DrawTexture(new Rect(0, 0, 110, 454), navGradient);
            GUI.color = new Color(0f, .66f, .78f, .16f * rightHover + .10f * rightPress);
            GUI.DrawTextureWithTexCoords(new Rect(650, 0, 110, 454), navGradient, new Rect(1, 0, -1, 1));
            GUI.color = oldColor;
        }
        void DrawPageIndicator()
        {
            if (dotTexture == null) dotTexture = IndicatorTexture(false);
            if (dotGlow == null) dotGlow = IndicatorTexture(true);
            var previous = GUI.color;
            float x = 28f;
            for (int i = 0; i < indicator.Length; i++)
            {
                float t = Mathf.SmoothStep(0, 1, indicator[i]);
                float width = Mathf.Lerp(8f, 26f, t);
                var rect = new Rect(x, 426f, width, 8f);
                if (Event.current.type == EventType.Repaint)
                {
                    // Two soft layers keep the active pill luminous without
                    // changing the inactive dots or the panel beneath it.
                    GUI.color = new Color(0f, .75f, .88f, .22f * t);
                    GUI.DrawTexture(new Rect(x - 10, 416f, width + 20, 28), dotGlow);
                    GUI.color = new Color(.02f, .82f, .92f, .55f * t);
                    GUI.DrawTexture(new Rect(x - 5, 421f, width + 10, 18), dotGlow);
                    GUI.color = Color.Lerp(new Color(.72f, .76f, .78f, .75f), new Color(0f, .66f, .78f, 1f), t);
                    GUI.DrawTexture(new Rect(x, rect.y, 8, 8), dotTexture);
                    if (width > 8.01f)
                    {
                        GUI.DrawTexture(new Rect(x + 4, rect.y, width - 8, 8), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(x + width - 8, rect.y, 8, 8), dotTexture);
                    }
                    GUI.color = new Color(.75f, 1f, 1f, .24f * t);
                    GUI.DrawTexture(new Rect(x + 4, rect.y + 1.5f, Mathf.Max(0f, width - 8f), 1f), Texture2D.whiteTexture);
                }
                GUI.color = previous;
                if (GUI.Button(new Rect(x - 3, 416f, width + 6, 28f), GUIContent.none, GUIStyle.none) && state.IsInteractive && i != state.Page)
                    state.Turn((i - state.Page + HelpHudState.PageCount) % HelpHudState.PageCount == 1 ? 1 : -1);
                x += width + 8f;
            }
            GUI.color = previous;
        }
        static Texture2D MakeShortcutShape(int width, int height, float radius, Color32 fill, Color32 border)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - width * .5f) - (width * .5f - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - height * .5f) - (height * .5f - radius), 0);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + .5f - dist);
                Color c = dist > radius - 1.5f ? (Color)border : fill;
                c.a *= alpha; pixels[y * width + x] = c;
            }
            tex.SetPixels32(pixels); tex.Apply(); return tex;
        }
        void DrawShortcuts()
        {
            if (!GameShellSettingsRuntime.ShowControlHints) return;
            if (shortcutKey == null) shortcutKey = MakeShortcutShape(38, 27, 7, new Color32(255, 255, 255, 255), new Color32(150, 220, 231, 255));
            if (shortcutText == null)
            {
                shortcutText = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
                shortcutKeyText = new GUIStyle(shortcutText) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color32(34, 46, 58, 255) } };
            }
            string[] keyLabels = { "ESC", "K", PlayerControls.BindingLabel(PlayerControls.Action.Hints) };
            string[] labels = { "일시정지 메뉴", "조작법", "힌트" };
            float[] keyWidths = new float[3], labelWidths = new float[3];
            float panelWidth = 10 + 14 + 2 * 16;
            for (int i = 0; i < 3; i++)
            {
                keyWidths[i] = Mathf.Max(28, Mathf.Ceil(shortcutKeyText.CalcSize(new GUIContent(keyLabels[i])).x) + 16);
                labelWidths[i] = Mathf.Ceil(shortcutText.CalcSize(new GUIContent(labels[i])).x) + 2;
                panelWidth += keyWidths[i] + 7 + labelWidths[i];
            }
            int textureWidth = Mathf.CeilToInt(panelWidth);
            if (shortcutPill == null || shortcutPill.width != textureWidth)
            {
                if (shortcutPill != null) Destroy(shortcutPill);
                shortcutPill = MakeShortcutShape(textureWidth, 44, 22, new Color32(250, 253, 255, 219), new Color32(255, 255, 255, 245));
            }
            float scale = canvas.scaleFactor;
            var shortcutShader = Resources.Load<Shader>("TutorialIslandGlass");
            bool glassReady = shortcutCapture != null && shortcutCapture.blurred != null && shortcutShader != null && shortcutShader.isSupported;
            shortcutText.normal.textColor = glassReady ? Color.white : new Color32(34, 46, 58, 255);
            var oldMatrix = GUI.matrix; var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(20 * scale, Screen.height - 64 * scale, 0), Quaternion.identity, Vector3.one * scale);
            GUI.color = Color.white;
            if (glassReady)
            {
                if (shortcutGlass == null)
                {
                    var shader = shortcutShader;
                    if (shader != null && shader.isSupported)
                    {
                        shortcutGlass = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                        shortcutGlass.SetFloat("_GlassMode", 1); shortcutGlass.SetFloat("_CornerRadius", 1);
                        // Retain the bright frosted base while sampling the scene
                        // underneath, with a restrained refracted reflective rim.
                        shortcutGlass.SetFloat("_WhiteTintStrength", .57f); shortcutGlass.SetFloat("_DarkTintStrength", .04f);
                        shortcutGlass.SetFloat("_MilkyTint", 0f); shortcutGlass.SetFloat("_Saturation", 1.45f);
                        shortcutGlass.SetFloat("_Refraction", 1f); shortcutGlass.SetFloat("_RefractionStrength", 9f);
                        shortcutGlass.SetFloat("_RefractionWidth", .18f); shortcutGlass.SetFloat("_LensDistortion", 0f);
                        shortcutGlass.SetFloat("_EdgeContrast", 1f); shortcutGlass.SetFloat("_EdgeContrastStrength", .35f);
                        shortcutGlass.SetFloat("_SpectrumPixels", 3f);
                        shortcutGlass.SetFloat("_ExtraBlurPixels", 9.5f);
                    }
                }
                if (shortcutGlass != null)
                {
                    if (shortcutShadow == null)
                    {
                        var shadowShader = Resources.Load<Shader>("TutorialIslandShadow");
                        if (shadowShader != null && shadowShader.isSupported)
                        {
                            shortcutShadow = new Material(shadowShader) { hideFlags = HideFlags.HideAndDontSave };
                            shortcutShadow.SetFloat("_Padding", 13f); shortcutShadow.SetFloat("_FadeDistance", 11f);
                            shortcutShadow.SetColor("_Color", new Color(0, 0, 0, .26f));
                        }
                    }
                    if (shortcutShadow != null)
                    {
                        shortcutShadow.SetVector("_PanelSize", new Vector4(textureWidth, 44, 0, 0));
                        Graphics.DrawTexture(new Rect(-13, -10, textureWidth + 26, 70), Texture2D.whiteTexture, shortcutShadow);
                    }
                    shortcutGlass.SetFloat("_PanelAspect", textureWidth / 44f);
                    Graphics.DrawTexture(new Rect(0, 0, textureWidth, 44), shortcutCapture.blurred, shortcutGlass);
                }
                else GUI.DrawTexture(new Rect(0, 0, textureWidth, 44), shortcutPill);
            }
            else GUI.DrawTexture(new Rect(0, 0, textureWidth, 44), shortcutPill);
            float x = 10;
            for (int i = 0; i < 3; i++)
            {
                float press = Mathf.SmoothStep(0, 1, shortcutPress[i]); float keyWidth = keyWidths[i];
                GUI.color = new Color(.58f, .86f, .91f, 1f); GUI.DrawTexture(new Rect(x, 10, keyWidth, 27), shortcutKey);
                GUI.color = Color.Lerp(Color.white, new Color(.70f, .95f, .97f, 1), press);
                GUI.DrawTexture(new Rect(x, 7 + 2.5f * press, keyWidth, 27), shortcutKey);
                GUI.color = Color.white; GUI.Label(new Rect(x, 7 + 2.5f * press, keyWidth, 27), keyLabels[i], shortcutKeyText);
                GUI.Label(new Rect(x + keyWidth + 7, 7, labelWidths[i], 27), labels[i], shortcutText);
                x += keyWidth + 7 + labelWidths[i] + 16;
            }
            GUI.color = oldColor; GUI.matrix = oldMatrix;
        }
        void OnGUI()
        {
            if (!state.IsActive && CanOpen() && Event.current != null && Event.current.type == EventType.Repaint)
            {
                DrawShortcuts();
            }
            if (!state.IsActive || state.Expansion < .95f || Event.current == null) return;
            Styles();
            var old = GUI.matrix; GUI.matrix = PanelGuiMatrix(panel, canvas, Screen.height);
            GUI.BeginGroup(new Rect(-380, -227, 760, 454));
            DrawNavigationHighlight();
            GUI.Label(new Rect(60, 26, 640, 34), "조작 안내", heading);
            if (GUI.Button(new Rect(0, 48, 110, 356), GUIContent.none, GUIStyle.none)) state.Turn(-1);
            if (GUI.Button(new Rect(650, 48, 110, 356), GUIContent.none, GUIStyle.none)) state.Turn(1);
            var arrowColor = GUI.color;
            GUI.color = Color.Lerp(Color.white, new Color(0f, .66f, .78f, 1f), Mathf.SmoothStep(0, 1, leftHover));
            GUI.Label(new Rect(14 - 4 * leftHover, 188, 44, 64), "‹", button);
            GUI.color = Color.Lerp(Color.white, new Color(0f, .66f, .78f, 1f), Mathf.SmoothStep(0, 1, rightHover));
            GUI.Label(new Rect(702 + 4 * rightHover, 188, 44, 64), "›", button);
            GUI.color = arrowColor;
            GUI.Label(new Rect(150, 411, 460, 32), "아무 키나 누르면 닫기", label);
            DrawPageIndicator();
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
