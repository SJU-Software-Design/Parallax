using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink;

// Shared title, pause and settings presentation. Stage gameplay remains owned by its existing controllers.
public partial class ParallaxGameShell : MonoBehaviour
{
    public const string PreviewVersion = "0.39.0-admin-sections";
    const float ReferenceWidth = 1920f, ReferenceHeight = 1080f, IntroLength = 9.28f;
    public readonly Color Foreground = new Color(.935f, .936f, .925f, 1);
    public readonly Color Muted = new Color(.62f, .62f, .65f, 1);
    readonly Color PanelColor = new Color(.035f, .036f, .055f, .975f);
    readonly string[] menuLabels = { "게임 시작하기", "이어하기", "환경설정", "게임 나가기" };
    // Shared by the title-only intro and the menu after returning from settings.
    // Reference draft: title at the upper left, four compact rows at the lower left.
    static readonly Rect MainTitleRect = new Rect(114, 48, 760, 184);
    const int MainTitleFontSize = 164, MainMenuFontSize = 40;
    readonly Rect[] menuRects = {
        new Rect(128, 710, 435, 68), new Rect(128, 790, 330, 68),
        new Rect(128, 870, 330, 68), new Rect(128, 950, 410, 68)
    };
    readonly Dictionary<string, GUIStyle> textStyles = new Dictionary<string, GUIStyle>();
    readonly List<string> soundTrace = new List<string>();
    Texture2D background, schoolLogo, brushShort, brushPanel, circleTexture;
    Font bodyFont, titleFont;
    AudioClip switchClip, startupClip, humClip, rattleClip, crackleClip, confirmClip, backClip, deniedClip;
    AudioSource humSource, lampSource, uiSource;
    float startAt, renderAlpha = 1, lastUiAt = -10, humFadeAt, nextFlickerAt, flickerAt = -100;
    float pendingRattleAt = -1, pausedAt, notificationUntil;
    float guiScale = 1, guiOffsetX, guiOffsetY;
    bool switchPlayed, startupPlayed, firstRattlePlayed, humStarted, hadFocus = true;
    bool previousSettingsOpen, pointerMode = true, exitDialog, quitting, validationMode;
    int selectedMenu, dialogSelection;
    int successfulUiRepaints; bool menuRepaintLogged;
    Vector2 lastRawPointer = new Vector2(-1000, -1000);
    string notification = "", validationPath;
    IntroFrame intro;

    [Serializable]
    public struct IntroFrame
    {
        public float logo, title, background, menu;
        public string phase;
    }
    [Serializable]
    sealed class ValidationReport
    {
        public bool passed, gameConnected;
        public string version, unityVersion, activeScene, scope;
        public int captureCount, checks, audioSources;
        public string[] errors, soundDispatch;
        public float[] clipLengths;
    }
    public Vector2 Pointer
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            Vector3 p = Input.mousePosition;
            return new Vector2((p.x - guiOffsetX) / guiScale, (Screen.height - p.y - guiOffsetY) / guiScale);
#else
            return Event.current == null ? Vector2.zero : Event.current.mousePosition;
#endif
        }
    }
    float Now { get { return Time.unscaledTime; } }

    Texture2D menuPointer;

    void ApplyMenuPointer()
    {
        var source = Resources.Load<Texture2D>("ParallaxCursor/cursor_menu");
        if (source == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var previous = RenderTexture.active;
        var buffer = RenderTexture.GetTemporary(48, 48, 0, RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(source, buffer);
            RenderTexture.active = buffer;
            menuPointer = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            menuPointer.ReadPixels(new Rect(0, 0, 48, 48), 0, 0);
            menuPointer.Apply();
            Cursor.SetCursor(menuPointer, new Vector2(16, 7), CursorMode.Auto);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(buffer);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
        Application.runInBackground = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ApplyMenuPointer();
        string[] args = Environment.GetCommandLineArgs();
        int check = Array.IndexOf(args, "-preview-validate");
        validationMode = check >= 0 || Array.IndexOf(args, "-player-shell-validate") >= 0 || Array.IndexOf(args, "-eclipse-ending-validate") >= 0;
        validationPath = check >= 0 && check + 1 < args.Length ? args[check + 1] : null;
        background = Resources.Load<Texture2D>("Preview/Background");
        schoolLogo = Resources.Load<Texture2D>("Preview/SchoolLogo");
        bodyFont = Resources.Load<Font>("Preview/BodyFont");
        titleFont = Resources.Load<Font>("Preview/TitleFont");
        switchClip = Resources.Load<AudioClip>("Preview/Lamp/Switch");
        startupClip = Resources.Load<AudioClip>("Preview/Lamp/Startup");
        humClip = Resources.Load<AudioClip>("Preview/Lamp/Hum");
        rattleClip = Resources.Load<AudioClip>("Preview/Lamp/Rattle");
        crackleClip = Resources.Load<AudioClip>("Preview/Lamp/Crackle");
        confirmClip = Resources.Load<AudioClip>("Preview/UI/Confirm");
        backClip = Resources.Load<AudioClip>("Preview/UI/Back");
        deniedClip = Resources.Load<AudioClip>("Preview/UI/Denied");
        brushShort = MakeBrush(320, 64, 41, false, false);
        brushPanel = MakeBrush(512, 320, 79, true, false);
        circleTexture = MakeCircle();
        humSource = CreateAudio("Electrical ambience", true);
        lampSource = CreateAudio("Lamp transients", false);
        uiSource = CreateAudio("UI feedback", false);
        if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            gameObject.AddComponent<AudioListener>();
        InitializeSettings();
        SceneManager.sceneLoaded += OnShellSceneLoaded;
        UpdateGuiTransform();
        startAt = Now;
        intro = EvaluateIntro(0);
        Debug.Log("PARALLAX_START_PREVIEW_READY version=" + PreviewVersion + " gameConnected=true capture=false");
    }
    void Start()
    {
        // Standalone-preview checks are not dispatched by the connected player.
    }
    AudioSource CreateAudio(string sourceName, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.loop = loop;
        source.volume = 1;
        source.priority = loop ? 150 : 64;
        return source;
    }
    public static IntroFrame EvaluateIntro(float seconds)
    {
        float t = Mathf.Max(0, seconds);
        float logo = t < 1.2f ? Smooth(t / 1.2f) : t < 2.8f ? 1 : t < 3.8f ? 1 - Smooth((t - 2.8f) / 1) : 0;
        return new IntroFrame {
            logo = logo,
            title = Smooth((t - 4) / 1.4f),
            background = Smooth((t - 6.03f) / 2.6f),
            menu = Smooth((t - 8.63f) / .65f),
            phase = t < 3.8f ? "school-logo" : t < 4 ? "black" : t < 5.85f ? "title-only" : t < 6.03f ? "switch" : t < 8.63f ? "lighting" : t < IntroLength ? "menu-fade" : "menu"
        };
    }
    static float Smooth(float v) { v = Mathf.Clamp01(v); return v * v * (3 - 2 * v); }
    void Update()
    {
        UpdateLoadingPresentation();
        if (validationMode || !hadFocus) return;
        UpdateGuiTransform();
        if (InGameplayScene) { UpdateInGameShell(); return; }
        if (GameProgressRuntime.Transitioning || LoadingPresentationActive) return;
        if (EndingTitleReplayActive) TickEndingTitlePresentation(Time.unscaledDeltaTime);
        float elapsed = Now - startAt;
        if (!EndingTitleReplayActive) UpdateIntro(elapsed);
        UpdateHum();
        if (settingsOpen != previousSettingsOpen)
        {
            previousSettingsOpen = settingsOpen;
            flickerAt = -100;
            pendingRattleAt = -1;
            nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
        }
        if (elapsed >= IntroLength) UpdateFlicker();
#if ENABLE_LEGACY_INPUT_MANAGER
        Vector2 rawPointer = Input.mousePosition;
        if ((rawPointer - lastRawPointer).sqrMagnitude > 2) pointerMode = true;
        lastRawPointer = rawPointer;
#endif
        if (!CreditsViewerOpen && !settingsOpen && !exitDialog && elapsed >= IntroLength && pointerMode)
        {
            for (int i = 0; i < MainMenuItemCount; i++)
                if ((i != 1 || GameProgressRuntime.HasSave) && GetMainMenuRect(i).Contains(Pointer)) selectedMenu = i;
        }
    }
    void UpdateIntro(float elapsed)
    {
        intro = EvaluateIntro(elapsed);
        if (!switchPlayed && elapsed >= 5.85f)
        {
            switchPlayed = true;
            PlayLamp(switchClip, .54f, "switch");
        }
        if (!startupPlayed && elapsed >= 6.03f)
        {
            startupPlayed = true;
            PlayLamp(startupClip, .42f, "startup");
            StartHum();
        }
        if (!firstRattlePlayed && elapsed >= 8.63f)
        {
            firstRattlePlayed = true;
            PlayLamp(rattleClip, .072f, "initial-rattle");
            nextFlickerAt = Now + .65f + UnityEngine.Random.Range(10f, 15f);
        }
    }
    void StartHum()
    {
        if (humStarted || humClip == null) return;
        humStarted = true;
        humFadeAt = Now;
        humSource.clip = humClip;
        humSource.volume = 0;
        soundTrace.Add("hum-start");
        if (!validationMode) humSource.Play();
    }
    void UpdateHum()
    {
        if (humSource == null || !humStarted || CreditsViewerOpen) return;
        float fade = Smooth((Now - humFadeAt) / 1.1f);
        float settingsDuck = settingsOpen ? .7f : 1;
        humSource.volume = Mathf.Clamp01(.108f * MasterVolume * AmbientVolume * fade * settingsDuck);
    }
    void PlayLamp(AudioClip clip, float baseGain, string name)
    {
        soundTrace.Add(name);
        if (clip != null && hadFocus && !validationMode)
            lampSource.PlayOneShot(clip, Mathf.Clamp01(baseGain * MasterVolume * ((name == "switch" || name == "startup") ? SfxVolume : AmbientVolume)));
    }
    public void PlayUi(string kind)
    {
        if (!validationMode && Now - lastUiAt < .045f) return;
        lastUiAt = Now;
        AudioClip clip = kind == "back" ? backClip : kind == "denied" ? deniedClip : confirmClip;
        float baseGain = kind == "denied" ? .20f : .22f;
        soundTrace.Add("ui-" + kind);
        if (clip != null && hadFocus && !validationMode)
            uiSource.PlayOneShot(clip, Mathf.Clamp01(baseGain * MasterVolume * SfxVolume));
    }
    void UpdateFlicker()
    {
        if (CreditsViewerOpen || settingsOpen || ReducedFlicker)
        {
            flickerAt = -100;
            pendingRattleAt = -1;
            if (nextFlickerAt < Now + 1) nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
            return;
        }
        if (nextFlickerAt > 0 && Now >= nextFlickerAt)
        {
            flickerAt = Now;
            nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
            PlayLamp(crackleClip, .114f, "periodic-crackle");
            pendingRattleAt = Now + .33f;
        }
        if (pendingRattleAt > 0 && Now >= pendingRattleAt)
        {
            pendingRattleAt = -1;
            PlayLamp(rattleClip, .096f, "periodic-rattle");
        }
    }
    public static float FlickerBrightness(float elapsed)
    {
        if (elapsed < 0 || elapsed >= 1.5f) return 1;
        float first = .12f * Mathf.Exp(-Mathf.Pow((elapsed - .19f) / .09f, 2));
        float second = .16f * Mathf.Exp(-Mathf.Pow((elapsed - .53f) / .13f, 2));
        return Mathf.Clamp(1 - first - second, .78f, 1);
    }
    void SkipIntro()
    {
        EndingTitleReplayActive = false; EndingTitleReplayElapsed = EndingTitleRevealSeconds;
        startAt = Now - IntroLength;
        switchPlayed = startupPlayed = firstRattlePlayed = true;
        intro = EvaluateIntro(IntroLength);
        StartHum();
        nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
    }
    void UpdateGuiTransform()
    {
        guiScale = Mathf.Max(.1f, Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight));
        guiOffsetX = (Screen.width - ReferenceWidth * guiScale) * .5f;
        guiOffsetY = (Screen.height - ReferenceHeight * guiScale) * .5f;
    }
    void OnGUI()
    {
        if (validationMode) return;
        if (InGameplayScene) { DrawInGameShell(); return; }
        ConsumeEndingTitleInput();
        UpdateGuiTransform();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(new Vector3(guiOffsetX, guiOffsetY, 0), Quaternion.identity, new Vector3(guiScale, guiScale, 1));
            renderAlpha = 1;
            if (background != null && intro.background > 0)
            {
                float light = FlickerBrightness(Now - flickerAt) * PreviewBrightness;
                GUI.color = new Color(light, light, light, intro.background);
                GUI.DrawTexture(new Rect(0, 0, ReferenceWidth, ReferenceHeight), background, ScaleMode.ScaleAndCrop, true);
                GUI.color = Color.white;
            }
            if (intro.logo > 0) DrawSchoolLogo(intro.logo);
            if (CreditsViewerOpen)
            {
                renderAlpha = 1; HandleCreditsViewerInput();
                if (CreditsViewerOpen) DrawCreditsViewer();
                else DrawMainMenu();
            }
            else if (intro.menu >= .999f && settingsOpen)
            {
                HandleSettingsInput();
                if (settingsOpen)
                {
                    DrawText(new Rect(110, 12, 420, 58), "Parallax", 42, Foreground, TextAnchor.MiddleLeft, true);
                    DrawSettings();
                }
                else DrawMainMenu();
            }
            else
            {
                HandleAdminLogoInput();
                if (intro.title > 0)
                {
                    renderAlpha = intro.title;
                    DrawText(MainTitleRect, "Parallax", MainTitleFontSize, Foreground, TextAnchor.MiddleLeft, true);
                }
                if (intro.menu > 0)
                {
                    renderAlpha = intro.menu;
                    DrawMainMenu(false);
                }
                renderAlpha = 1;
                HandleMainInput();
            }
            if (exitDialog) DrawExitDialog();
            if (ShouldShowLoadingOverlay) DrawLoadingOverlay();
            if (Now < notificationUntil && !LoadingPresentationActive && !GameProgressRuntime.Transitioning)
            {
                renderAlpha = 1;
                PaintBrush(new Rect(650, 986, 1130, 60), new Color(.035f, .035f, .05f, .94f), 2);
                DrawText(new Rect(675, 993, 1080, 44), notification, 24, Foreground, TextAnchor.MiddleCenter);
            }
            DrawEndingTitleReveal();
            GUI.color = Color.white;
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; renderAlpha = 1; }
        if (Event.current.type == EventType.Repaint)
        {
            successfulUiRepaints++;
            if (!menuRepaintLogged && intro.menu >= .999f)
            {
                menuRepaintLogged = true;
                Debug.Log("PARALLAX_PREVIEW_MENU_DRAW_READY frames=" + successfulUiRepaints + " screen=" + Screen.width + "x" + Screen.height + " captures=0");
            }
        }
    }
    void DrawSchoolLogo(float alpha)
    {
        renderAlpha = alpha;
        if (schoolLogo != null) DrawTextureTint(new Rect(805, 260, 310, 310), schoolLogo, Color.white, ScaleMode.ScaleToFit);
        DrawText(new Rect(400, 620, 1120, 64), "세종대학교 SW기초설계 프로젝트", 38, Foreground, TextAnchor.MiddleCenter);
        DrawText(new Rect(500, 701, 920, 50), "3조 Parallax", 30, new Color(.74f, .74f, .76f), TextAnchor.MiddleCenter);
        renderAlpha = 1;
    }
    void DrawMainMenu(bool includeBackgroundAndTitle = true)
    {
        if (includeBackgroundAndTitle)
        {
            DrawText(MainTitleRect, "Parallax", MainTitleFontSize, Foreground, TextAnchor.MiddleLeft, true);
        }
        bool ready = intro.menu >= .999f && !exitDialog && (!GameProgressRuntime.Transitioning || LoadingPhase == LoadingTransitionPhase.FadingOut);
        selectedMenu = Mathf.Clamp(selectedMenu, 0, MainMenuItemCount - 1);
        for (int i = 0; i < MainMenuItemCount; i++)
        {
            bool enabled = ready && (i != 1 || GameProgressRuntime.HasSave);
            if (Button(GetMainMenuRect(i), GetMainMenuLabel(i), "main-" + i, i == selectedMenu && enabled, enabled, MainMenuFontSize))
                ActivateMenu(i);
        }
        if (ready && GetMainMenuRect(1).Contains(Pointer))
            DrawText(new Rect(590, GetMainMenuRect(1).y + 12, 900, 50), GameProgressRuntime.HasSave ? GameProgressRuntime.SaveSummary : "저장된 진행 기록이 없습니다.", 23, Muted);
        if (ready && Now >= notificationUntil)
            DrawText(new Rect(1370, 1005, 410, 40), "↑ ↓ 선택    Enter 확인", 22, new Color(.7f, .7f, .72f), TextAnchor.MiddleRight);
    }
    void HandleMainInput()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (GameProgressRuntime.Transitioning) return;
        if (intro.menu < .999f)
        {
            if (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
            { SkipIntro(); e.Use(); }
            return;
        }
        if (exitDialog)
        {
            if (e.keyCode == KeyCode.Escape) { exitDialog = false; PlayUi("back"); e.Use(); }
            else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Tab)
            { dialogSelection = 1 - dialogSelection; pointerMode = false; e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
            { if (dialogSelection == 1) ConfirmTitleDialog(); else { exitDialog = false; PlayUi("back"); } e.Use(); }
            return;
        }
        if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.Tab)
        {
            int dir = e.keyCode == KeyCode.UpArrow || e.shift ? -1 : 1;
            do { selectedMenu = (selectedMenu + dir + MainMenuItemCount) % MainMenuItemCount; } while (selectedMenu == 1 && !GameProgressRuntime.HasSave);
            pointerMode = false;
            GUI.FocusControl(null);
            e.Use();
        }
        else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
        { ActivateMenu(selectedMenu); e.Use(); }
        else if (e.keyCode == KeyCode.Escape)
        { titleDialogIsNewGame = false; exitDialog = true; dialogSelection = 0; PlayUi("back"); e.Use(); }
        else if (e.keyCode == KeyCode.F11)
        {
            Screen.fullScreenMode = Screen.fullScreenMode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            e.Use();
        }
    }
    void ActivateMenu(int index)
    {
        if (GameProgressRuntime.Transitioning || index < 0 || index >= MainMenuItemCount) return;
        selectedMenu = index;
        if (index == 1 && !GameProgressRuntime.HasSave) { PlayUi("denied"); return; }
        PlayUi("confirm");
        if (index == 2) OpenSettings();
        else if (index == 3 && GameCompletionState.CreditsUnlocked) OpenCreditsViewer();
        else if (index == MainMenuItemCount - 1) { titleDialogIsNewGame = false; exitDialog = true; dialogSelection = 0; }
        else if (index == 1) GameProgressRuntime.ContinueGame();
        else if (GameProgressRuntime.HasSave) { titleDialogIsNewGame = true; exitDialog = true; dialogSelection = 0; }
        else GameProgressRuntime.NewGame();
    }
    void DrawExitDialog()
    {
        renderAlpha = 1;
        Fill(new Rect(0, 0, ReferenceWidth, ReferenceHeight), new Color(0, 0, 0, .56f));
        PaintBrush(new Rect(430, 330, 1060, 410), PanelColor);
        DrawText(new Rect(500, 398, 920, 90), titleDialogIsNewGame ? "새 게임을 시작할까요?" : "게임을 종료할까요?", 44, Foreground, TextAnchor.MiddleCenter);
        if (titleDialogIsNewGame) DrawText(new Rect(500, 492, 920, 50), "기존 진행 기록을 새 게임으로 바꿉니다.", 27, Muted, TextAnchor.MiddleCenter);
        Rect cancel = new Rect(615, 580, 260, 76), confirm = new Rect(1045, 580, 260, 76);
        if (pointerMode)
        {
            if (cancel.Contains(Pointer)) dialogSelection = 0;
            if (confirm.Contains(Pointer)) dialogSelection = 1;
        }
        if (Button(cancel, "돌아가기", "quit-cancel", dialogSelection == 0, true, 32))
        { exitDialog = false; PlayUi("back"); }
        if (Button(confirm, titleDialogIsNewGame ? "새 게임" : "나가기", "quit-confirm", dialogSelection == 1, !quitting, 32)) ConfirmTitleDialog();
    }
    void QuitPreview()
    {
        if (quitting) return;
        PlayUi("confirm");
        quitting = true;
        if (!validationMode) StartCoroutine(QuitAfterClick());
    }
    IEnumerator QuitAfterClick()
    {
        yield return new WaitForSecondsRealtime(.18f);
        Application.Quit();
    }
    public void Notify(string message)
    {
        notification = message ?? "";
        notificationUntil = Now + 4;
    }
    public void DrawText(Rect rect, string text, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool title = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!title) size = Mathf.RoundToInt(size * PreviewTextScale);
        string key = size + ":" + (int)alignment + ":" + title;
        GUIStyle style;
        if (!textStyles.TryGetValue(key, out style))
        {
            style = new GUIStyle(GUI.skin.label) {
                font = title ? titleFont : bodyFont, fontSize = size,
                fontStyle = FontStyle.Normal, alignment = alignment, wordWrap = true,
                clipping = TextClipping.Clip, richText = false, padding = new RectOffset(0, 0, 0, 0)
            };
            textStyles[key] = style;
        }
        color.a *= renderAlpha;
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
    public bool Button(Rect rect, string text, string id, bool active = false, bool enabled = true, int size = 30)
    {
        Event e = Event.current;
        bool blockedPress = !GameProgressRuntime.Transitioning && !LoadingPresentationActive && !enabled && intro.menu >= .999f && !exitDialog && e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(Pointer);
        if (blockedPress) { PlayUi("denied"); e.Use(); }
        bool hover = enabled && rect.Contains(Pointer);
        bool focused = GUI.GetNameOfFocusedControl() == id;
        bool isMain = id.StartsWith("main-", StringComparison.Ordinal);
        bool highlighted = active || (focused && !isMain);
        if (text == "‹" || text == "›") DrawTextureTint(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, rect.height - 6), circleTexture, new Color(.28f, .28f, .32f));
        if (highlighted && enabled)
            PaintBrush(new Rect(rect.x - 6, rect.y + 9, rect.width + 12, rect.height - 18), new Color(.94f, .94f, .92f));
        else if (hover && enabled)
            Fill(new Rect(rect.x, rect.yMax - 8, rect.width, 2), new Color(.65f, .65f, .68f, .55f));
        Color textColor = !enabled ? new Color(.37f, .37f, .41f) : highlighted ? new Color(.045f, .045f, .06f) : Foreground;
        DrawText(isMain ? new Rect(rect.x + 22, rect.y, rect.width - 44, rect.height) : rect, text, size, textColor, isMain ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && enabled && !GameProgressRuntime.Transitioning && !LoadingPresentationActive;
        GUI.SetNextControlName(id);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.enabled = oldEnabled;
        return clicked;
    }
    public void Fill(Rect rect, Color color)
    {
        DrawTextureTint(rect, Texture2D.whiteTexture, color);
    }
    public void PaintBrush(Rect rect, Color color, int seed = 1)
    {
        Texture2D texture = rect.width > 500 && rect.height > 190 ? brushPanel : brushShort;
        DrawTextureTint(rect, texture, color);
    }
    void DrawTextureTint(Rect rect, Texture texture, Color color, ScaleMode scale = ScaleMode.StretchToFill)
    {
        if (texture == null) return;
        Color previous = GUI.color;
        color.a *= renderAlpha;
        GUI.color = color;
        GUI.DrawTexture(rect, texture, scale, true);
        GUI.color = previous;
    }
    static Texture2D MakeBrush(int width, int height, int seed, bool panel, bool side)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = side ? "Own side paint mask" : panel ? "Own panel paint mask" : "Own selection paint mask";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var random = new System.Random(seed);
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = x / (float)(width - 1), ny = y / (float)(height - 1);
            float noise = (float)random.NextDouble();
            float right = .973f + .012f * Mathf.Sin(ny * 97 + seed) + .007f * Mathf.Sin(ny * 217);
            float left = side ? -.02f : panel ? .009f + .004f * Mathf.Sin(ny * 171) : .012f + .009f * Mathf.Sin(ny * 41);
            float bottom = side ? -.01f : panel ? .012f + .005f * Mathf.Sin(nx * 151) : .09f + .025f * Mathf.Sin(nx * 87 + seed);
            float top = side ? 1.01f : panel ? .988f + .006f * Mathf.Sin(nx * 181 + seed) : .91f + .027f * Mathf.Sin(nx * 97 + seed);
            float dx = Mathf.Min(nx - left, right - nx) * width;
            float dy = Mathf.Min(ny - bottom, top - ny) * height;
            float edge = Mathf.Min(dx, dy);
            float alpha = Mathf.Clamp01(edge / (panel || side ? 1.2f : 1.6f));
            if (edge < (panel ? 5 : 4) && noise > .58f) alpha *= noise * .62f;
            if (!panel && !side && edge > 3 && noise > .994f) alpha *= .45f;
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
    static Texture2D MakeCircle()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.name = "Own UI circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[1024];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16));
            pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(15.8f - distance) * 255));
        }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return texture;
    }
    void OnApplicationFocus(bool focused)
    {
        if (validationMode || focused == hadFocus) return;
        hadFocus = focused;
        if (!focused)
        {
            pausedAt = Now;
            if (humSource != null) humSource.Pause();
            if (lampSource != null) lampSource.Stop();
            if (uiSource != null) uiSource.Stop();
        }
        else
        {
            float delta = Mathf.Max(0, Now - pausedAt);
            startAt += delta;
            nextFlickerAt += delta;
            humFadeAt = Now;
            flickerAt = -100;
            pendingRattleAt = -1;
            if (humStarted && humSource != null && !InGameplayScene) humSource.UnPause();
        }
    }
    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnShellSceneLoaded; Instance = null;
        if (humSource != null) humSource.Stop();
        if (lampSource != null) lampSource.Stop();
        if (uiSource != null) uiSource.Stop();
        if (brushShort != null) Destroy(brushShort);
        if (brushPanel != null) Destroy(brushPanel);
        if (circleTexture != null) Destroy(circleTexture);
    }
    IEnumerator ValidatePreview()
    {
        yield return null;
        var errors = new List<string>();
        int checks = 0;
        Action<string, bool> check = (name, pass) => { checks++; if (!pass) errors.Add(name); };
        check("Original background loaded", background != null && background.width == 1672 && background.height == 941);
        check("Official school logo loaded", schoolLogo != null && schoolLogo.width > 100);
        check("Full fonts loaded", bodyFont != null && titleFont != null);
        string sample = "세종대학교 SW기초설계 프로젝트3조Parallax게임 시작하기이어환경설정나가기전체음량효과환경조작편의기능";
        foreach (char c in sample)
        { if (!char.IsWhiteSpace(c)) check("Body font glyph U+" + ((int)c).ToString("X4"), bodyFont != null && bodyFont.HasCharacter(c)); }
        AudioClip[] clips = { switchClip, startupClip, humClip, rattleClip, crackleClip, confirmClip, backClip, deniedClip };
        float[] lengths = new float[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            check("Audio clip " + i + " loaded", clips[i] != null && clips[i].length > .01f && clips[i].samples > 0);
            if (clips[i] != null) lengths[i] = clips[i].length;
        }
        check("One loop source only", humSource.loop && !lampSource.loop && !uiSource.loop);
        check("3 dedicated audio sources", GetComponentsInChildren<AudioSource>().Length == 3);
        check("No 3D audio", humSource.spatialBlend == 0 && lampSource.spatialBlend == 0 && uiSource.spatialBlend == 0);
        IntroFrame initial = EvaluateIntro(0), logoPeak = EvaluateIntro(2), black = EvaluateIntro(3.9f), titleOnly = EvaluateIntro(5), lighting = EvaluateIntro(7), complete = EvaluateIntro(9.3f);
        check("Logo begins transparent", initial.logo == 0 && initial.background == 0 && initial.menu == 0);
        check("Logo is isolated", logoPeak.logo == 1 && logoPeak.title == 0 && logoPeak.background == 0);
        check("Complete black between screens", black.logo == 0 && black.title == 0 && black.background == 0);
        check("Title only during dark", titleOnly.title > 0 && titleOnly.logo == 0 && titleOnly.background == 0 && titleOnly.menu == 0);
        check("Gradual lamp reveal", lighting.background > 0 && lighting.background < 1 && lighting.menu == 0);
        check("Interactive menu after reveal", complete.title == 1 && complete.background == 1 && complete.menu == 1);
        soundTrace.Clear();
        switchPlayed = startupPlayed = firstRattlePlayed = humStarted = false;
        UpdateIntro(6); UpdateIntro(6.5f); UpdateIntro(9.3f); UpdateIntro(15);
        check("Switch once", soundTrace.FindAll(s => s == "switch").Count == 1);
        check("Startup once", soundTrace.FindAll(s => s == "startup").Count == 1);
        check("Hum once", soundTrace.FindAll(s => s == "hum-start").Count == 1);
        check("Rattle once", soundTrace.FindAll(s => s == "initial-rattle").Count == 1);
        check("Dim only, no strobe blackout", FlickerBrightness(.2f) >= .78f && FlickerBrightness(.53f) >= .78f);
        check("Flicker returns to original", FlickerBrightness(1.5f) == 1 && FlickerBrightness(-1) == 1);
        string scene = SceneManager.GetActiveScene().name;
        ActivateMenu(0);
        check("Start does not load game", SceneManager.GetActiveScene().name == scene && !string.IsNullOrEmpty(notification));
        ValidateSettings(errors); checks++;
        foreach (Vector2 size in new[] { new Vector2(1920,1080), new Vector2(1280,720), new Vector2(1024,768), new Vector2(2560,1080), new Vector2(960,540) })
        {
            float scale = Mathf.Min(size.x / ReferenceWidth, size.y / ReferenceHeight);
            Vector2 offset = (size - new Vector2(ReferenceWidth, ReferenceHeight) * scale) * .5f;
            foreach (Rect rect in menuRects)
                check("Menu geometry " + size + " " + rect.y, rect.x * scale + offset.x >= 0 && rect.xMax * scale + offset.x <= size.x && rect.yMax * scale + offset.y <= size.y);
        }
        var report = new ValidationReport {
            passed = errors.Count == 0, gameConnected = false, version = PreviewVersion,
            unityVersion = Application.unityVersion, activeScene = scene, scope = "Standalone player assets, timeline, audio dispatch, settings and geometry; no capture or subjective listening.",
            captureCount = 0, checks = checks, audioSources = GetComponentsInChildren<AudioSource>().Length,
            errors = errors.ToArray(), soundDispatch = soundTrace.ToArray(), clipLengths = lengths
        };
        if (!string.IsNullOrEmpty(validationPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(validationPath)));
            File.WriteAllText(validationPath, JsonUtility.ToJson(report, true));
        }
        Debug.Log("PARALLAX_PREVIEW_VALIDATION " + report.passed + " checks=" + checks + " captures=0");
        Application.Quit(report.passed ? 0 : 1);
    }
}
