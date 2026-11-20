using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink;

// Session-only sequence. No game-start button click is delayed or intercepted.
public sealed class AdminUnlockSequence
{
    static readonly int[] Pattern = { 0, 0, 1, 1, 1 };
    int step; float lastInput;
    public int Step { get { return step; } }
    public void Reset() { step = 0; lastInput = -100; }
    public void Mouse(int button, bool overLogo, float time)
    {
        if (!overLogo) { Reset(); return; }
        if (time - lastInput > 4) Reset();
        if (step < Pattern.Length && button == Pattern[step]) step++;
        else step = button == 0 ? 1 : 0;
        lastInput = time;
    }
    public bool Backquote(bool overLogo, float time)
    {
        bool accepted = overLogo && step == Pattern.Length && time - lastInput <= 4;
        Reset(); return accepted;
    }
}

public partial class ParallaxGameShell : MonoBehaviour
{
    public static ParallaxGameShell Instance { get; private set; }
    public static bool OwnsPauseMenus { get { return Instance != null; } }
    public static bool Administrator { get { return Instance != null && Instance.AdminMode; } }
    public static bool IsUiFeedbackSource(AudioSource source) { return Instance != null && source == Instance.uiSource; }
    public static bool ShouldShowLoadingOverlay { get { return Instance != null && (Instance.LoadingPresentationActive || GameProgressRuntime.ShellTransitioning); } }
    public static bool ShouldShowPauseMenu { get { return Instance != null && GameProgressRuntime.Paused && !GameProgressRuntime.Transitioning && !Instance.LoadingPresentationActive; } }
    public static bool BlocksGameplay { get { return Instance != null && (Instance.settingsOpen || Instance.LoadingPresentationActive || GameProgressRuntime.Paused || GameProgressRuntime.SceneLoading); } }
    public static bool AllowStagePauseInput { get { return !OwnsPauseMenus; } }
    public bool AdminMode { get; private set; }
    public bool IsTitle { get { return !InGameplayScene; } }
    bool InGameplayScene { get { return SceneManager.GetActiveScene().name != "MainMenu"; } }
    bool enteredGameplay, titleDialogIsNewGame;
    readonly AdminUnlockSequence adminGesture = new AdminUnlockSequence();
    int pauseSelection, pausePromptSelection;
    string pausePrompt;
    Vector2 gamePointer;
    bool wasPaused;

    void OnShellSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetCreditsViewerForScene();
        ObserveEndingTitleScene(scene);
        settingsOpen = false; exitDialog = false; pausePrompt = null; pauseSelection = 0;
        notificationUntil = 0; adminGesture.Reset();
        if (scene.name != "MainMenu")
        {
            enteredGameplay = true;
            if (humSource != null) humSource.Stop();
            if (lampSource != null) lampSource.Stop();
            humStarted = false;
        }
        else if (enteredGameplay && !EndingTitleReplayActive)
        {
            startAt = Now - IntroLength;
            switchPlayed = startupPlayed = firstRattlePlayed = true;
            intro = EvaluateIntro(IntroLength); StartHum();
            nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }
    }
    void HandleAdminLogoInput()
    {
        if (CreditsViewerOpen || intro.menu < .999f || exitDialog || GameProgressRuntime.Transitioning) return;
        Event e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown)
        {
            bool over = MainTitleRect.Contains(Pointer);
            adminGesture.Mouse(e.button, over, Now);
            if (over) e.Use();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.BackQuote)
        {
            if (adminGesture.Backquote(MainTitleRect.Contains(Pointer), Now))
            { AdminMode = true; PlayUi("confirm"); Notify("관리자 모드 활성화"); }
            e.Use();
        }
        else if (e.type == EventType.KeyDown) adminGesture.Reset();
    }
    void ConfirmTitleDialog()
    {
        if (titleDialogIsNewGame)
        { exitDialog = false; titleDialogIsNewGame = false; PlayUi("confirm"); GameProgressRuntime.NewGame(); }
        else QuitPreview();
    }
    void UpdateInGameShell()
    {
        bool paused = GameProgressRuntime.Paused;
        if (paused && !wasPaused) { pauseSelection = 0; pausePrompt = null; }
        wasPaused = paused;
        if (GameProgressRuntime.Transitioning || LoadingPresentationActive) return;
        if (!settingsOpen && PlayerControls.PausePressed)
        {
            if (pausePrompt != null) { pausePrompt = null; PlayUi("back"); }
            else if (paused) { GameProgressRuntime.Resume(); PlayUi("back"); }
            else { GameProgressRuntime.Pause(); PlayUi("back"); }
            return;
        }
        Vector2 pointer = Pointer;
        if ((pointer - gamePointer).sqrMagnitude > 1)
        {
            pointerMode = true;
            if (paused && pausePrompt == null && !settingsOpen)
                for (int i = 0; i < PauseCount; i++) if (PauseRect(i).Contains(pointer)) pauseSelection = i;
        }
        gamePointer = pointer;
    }
    int PauseCount { get { return Administrator ? 5 : 4; } }
    Rect PauseRect(int i)
    {
        float height = Administrator ? 900 : 800;
        return new Rect(625, (1080 - height) * .5f + 235 + i * 100, 670, 78);
    }
    string PauseLabel(int i)
    {
        if (i == 0) return "게임 재개하기";
        if (i == 1) return "설정";
        if (i == 2) return "현재 구간 다시 시작";
        if (Administrator && i == 3) return "맵 이동";
        return "저장하고 나가기";
    }
    void ActivatePause(int i)
    {
        if (GameProgressRuntime.Transitioning || LoadingPresentationActive) return;
        PlayUi("confirm");
        if (i == 0) GameProgressRuntime.Resume();
        else if (i == 1) OpenSettings();
        else if (i == 2) { pausePrompt = "restart"; pausePromptSelection = 0; }
        else if (Administrator && i == 3) { pausePrompt = "travel"; pausePromptSelection = 0; }
        else { pausePrompt = "save"; pausePromptSelection = 0; }
    }
    void DrawInGameShell()
    {
        // Stage-owned transitions render their own view; even the paused
        // construction frame must not flash a shell pause/loading panel.
        if (GameProgressRuntime.Transitioning && !ShouldShowLoadingOverlay) return;
        if (!ShouldShowPauseMenu && !ShouldShowLoadingOverlay && Now >= notificationUntil) return;
        UpdateGuiTransform();
        Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; int oldDepth = GUI.depth;
        try
        {
            GUI.depth = -100;
            GUI.matrix = Matrix4x4.TRS(new Vector3(guiOffsetX, guiOffsetY, 0), Quaternion.identity, new Vector3(guiScale, guiScale, 1));
            renderAlpha = 1;
            if (ShouldShowLoadingOverlay) { DrawLoadingOverlay(); return; }
            if (ShouldShowPauseMenu)
            {
                Fill(new Rect(-guiOffsetX / guiScale, -guiOffsetY / guiScale, Screen.width / guiScale, Screen.height / guiScale), new Color(0, 0, 0, .52f));
                if (settingsOpen)
                {
                    HandleSettingsInput();
                    if (settingsOpen) DrawSettings();
                    // CloseSettings owns the back sound.
                }
                else if (pausePrompt != null) DrawPausePrompt();
                else
                {
                    float height = Administrator ? 900 : 800, y = (1080 - height) * .5f;
                    PaintBrush(new Rect(520, y, 880, height), new Color(.025f, .025f, .035f, .975f));
                    DrawText(new Rect(700, y + 63, 520, 42), Administrator ? "PARALLAX · ADMIN" : "PARALLAX", 24, Muted, TextAnchor.MiddleCenter, true);
                    DrawText(new Rect(630, y + 120, 660, 88), "일시정지", 54, Foreground, TextAnchor.MiddleCenter);
                    for (int i = 0; i < PauseCount; i++)
                        if (Button(PauseRect(i), PauseLabel(i), "pause-" + i, pauseSelection == i, true, 34)) ActivatePause(i);
                    DrawText(new Rect(630, y + height - 75, 660, 44), "Esc   게임으로 돌아가기", 23, Muted, TextAnchor.MiddleCenter);
                    HandlePauseKeyboard();
                }
            }
            if (Now < notificationUntil)
            {
                PaintBrush(new Rect(400, 1010, 1120, 55), new Color(.025f, .025f, .035f, .98f));
                DrawText(new Rect(420, 1015, 1080, 45), notification, 25, Foreground, TextAnchor.MiddleCenter);
            }
            if (ShouldShowLoadingOverlay) DrawLoadingOverlay();
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; renderAlpha = 1; }
    }
    void HandlePauseKeyboard()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.Tab)
        {
            int direction = e.keyCode == KeyCode.UpArrow || e.shift ? -1 : 1;
            pauseSelection = (pauseSelection + direction + PauseCount) % PauseCount;
            pointerMode = false; GUI.FocusControl(null); e.Use();
        }
        else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
        { ActivatePause(pauseSelection); e.Use(); }
    }
    void DrawPausePrompt()
    {
        if (pausePrompt == "travel")
        {
            PaintBrush(new Rect(490, 220, 940, 650), new Color(.025f, .025f, .035f, .99f));
            DrawText(new Rect(590, 275, 740, 75), "맵 이동", 48, Foreground, TextAnchor.MiddleCenter);
            string[] scenes = { "Tutorial", "Gallery", "Beach", "Eclipse" }, labels = { "튜토리얼", "미술관", "해변", "일식", "돌아가기" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rect rect = new Rect(655, 390 + i * 100, 610, 72);
                if (pointerMode && rect.Contains(Pointer)) pausePromptSelection = i;
                if (Button(rect, labels[i], "travel-" + i, pausePromptSelection == i, !GameProgressRuntime.Transitioning, 33))
                { if (i == scenes.Length) pausePrompt = null; else GameProgressRuntime.AdminTravel(scenes[i]); PlayUi("confirm"); }
            }
            Event key = Event.current;
            if (key.type == EventType.KeyDown)
            {
                if (key.keyCode == KeyCode.UpArrow || key.keyCode == KeyCode.DownArrow)
                { pausePromptSelection = (pausePromptSelection + (key.keyCode == KeyCode.UpArrow ? labels.Length - 1 : 1)) % labels.Length; pointerMode = false; key.Use(); }
                else if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.Space)
                { if (pausePromptSelection == scenes.Length) { pausePrompt = null; PlayUi("back"); } else { PlayUi("confirm"); GameProgressRuntime.AdminTravel(scenes[pausePromptSelection]); } key.Use(); }
            }
            return;
        }
        PaintBrush(new Rect(395, 320, 1130, 450), new Color(.025f, .025f, .035f, .99f));
        bool restarting = pausePrompt == "restart";
        DrawText(new Rect(465, 375, 990, 85), restarting ? "현재 구간을 다시 시작할까요?" : "저장하고 나갈까요?", 42, Foreground, TextAnchor.MiddleCenter);
        DrawText(new Rect(465, 470, 990, 70), restarting ? "현재 구간의 퍼즐이 초기화됩니다." : "마지막 리스폰 지점에서 이어할 수 있습니다.", 28, Muted, TextAnchor.MiddleCenter);
        Rect cancel = new Rect(580, 610, 320, 76), confirm = new Rect(1020, 610, 320, 76);
        if (pointerMode) { if (cancel.Contains(Pointer)) pausePromptSelection = 0; if (confirm.Contains(Pointer)) pausePromptSelection = 1; }
        if (Button(cancel, "돌아가기", "pause-cancel", pausePromptSelection == 0, true, 31)) { pausePrompt = null; PlayUi("back"); }
        if (Button(confirm, restarting ? "다시 시작" : "저장하고 나가기", "pause-confirm", pausePromptSelection == 1, true, 31)) ConfirmPausePrompt();
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Tab)
            { pausePromptSelection = 1 - pausePromptSelection; pointerMode = false; e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)
            { if (pausePromptSelection == 1) ConfirmPausePrompt(); else { pausePrompt = null; PlayUi("back"); } e.Use(); }
        }
    }
    void ConfirmPausePrompt()
    {
        if (pausePrompt == "restart") { pausePrompt = null; GameProgressRuntime.RestartCheckpoint(); PlayUi("confirm"); }
        else if (GameProgressRuntime.SaveCurrent()) { pausePrompt = null; PlayUi("confirm"); GameProgressRuntime.ReturnToTitle(); }
        else { PlayUi("denied"); Notify("저장하지 못했습니다. " + GameProgressRuntime.LastError); }
    }
    void DrawLoadingOverlay()
    {
        if (LoadingPresentationActive) { DrawSceneLoadingPresentation(); return; }
        Fill(new Rect(0, 0, ReferenceWidth, ReferenceHeight), new Color(0, 0, 0, .88f));
        DrawText(new Rect(560, 460, 800, 100), "불러오는 중", 42, Foreground, TextAnchor.MiddleCenter);
    }
}