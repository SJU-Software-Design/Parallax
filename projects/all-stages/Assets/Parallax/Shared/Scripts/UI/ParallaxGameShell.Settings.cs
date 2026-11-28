using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class GameShellConfig
{
    public float masterVolume = 1f, musicVolume = 1f, effectsVolume = 1f, ambientVolume = 1f;
    public float brightness = 1f, mouseSensitivity = 1f, fieldOfView = 75f;
    public bool invertMouseY, reducedFlicker, showHints = true, cameraShake = true;
    public int screenMode = 2, width = 1280, height = 720, qualityLevel = 2, vSyncCount = 1, frameLimit = 60, textSize = 100;
    public KeyCode[] bindings = DefaultBindings();

    public static KeyCode[] DefaultBindings()
    {
        return new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.E, KeyCode.Backspace, KeyCode.R, KeyCode.H };
    }
    // Keep the upstream J-to-H default migration without taking a custom H key.
    public bool MigrateLegacyHintBinding()
    {
        if (bindings == null || bindings.Length != DefaultBindings().Length) return false;
        int hint = (int)ShadeLink.PlayerControls.Action.Hints;
        if (bindings[hint] != KeyCode.J || Array.IndexOf(bindings, KeyCode.H) >= 0) return false;
        bindings[hint] = KeyCode.H;
        return true;
    }
    // Preserve pre-help settings that legitimately used K. Prefer the action's
    // original key, then an unused default key; never replace another binding.
    // Returns the migrated action index so the shell can explain the new key.
    public int MigrateReservedHelpBinding()
    {
        var defaults = DefaultBindings();
        if (bindings == null || bindings.Length != defaults.Length) return -1;
        int action = Array.IndexOf(bindings, KeyCode.K);
        if (action < 0) return -1;
        var used = new HashSet<KeyCode>(bindings);
        KeyCode replacement = defaults[action];
        if (used.Contains(replacement))
            foreach (KeyCode candidate in defaults)
                if (!used.Contains(candidate)) { replacement = candidate; break; }
        if (used.Contains(replacement)) return -1;
        bindings[action] = replacement;
        return action;
    }
    public static GameShellConfig Defaults() { return new GameShellConfig(); }
    public GameShellConfig DeepCopy()
    {
        var copy = (GameShellConfig)MemberwiseClone();
        copy.bindings = bindings == null ? DefaultBindings() : (KeyCode[])bindings.Clone();
        return copy;
    }
}

public partial class ParallaxGameShell : MonoBehaviour
{
    public GameShellConfig appliedConfig, draftConfig;
    public bool settingsOpen;
    public enum SettingsTab { Display, Sound, Controls, Comfort }
    public SettingsTab settingsTab = SettingsTab.Sound;
    public bool DisplayTrialPending { get { return displayTrialPending; } }
    public bool Rebinding { get { return bindingCapture >= 0 || bindingConflict >= 0; } }
    public float MasterVolume { get { return Mathf.Clamp01(CurrentConfig.masterVolume); } }
    public float SfxVolume { get { return Mathf.Clamp01(CurrentConfig.effectsVolume); } }
    public float AmbientVolume { get { return Mathf.Clamp01(CurrentConfig.ambientVolume); } }
    public bool ReducedFlicker { get { return CurrentConfig.reducedFlicker; } }
    public float PreviewBrightness { get { return CurrentConfig.brightness; } }
    public float PreviewTextScale { get { return CurrentConfig.textSize / 100f; } }
    GameShellConfig CurrentConfig { get { return (settingsOpen ? draftConfig : appliedConfig) ?? GameShellConfig.Defaults(); } }
    static readonly string[] SettingsTabs = { "화면", "소리", "조작", "편의 기능" };
    static readonly string[] BindingLabels = { "앞으로 이동", "뒤로 이동", "왼쪽 이동", "오른쪽 이동", "점프", "잡기 / 놓기", "물체 회전", "상호작용", "잡기 취소", "구역 초기화", "안내 보기" };
    static readonly int[] ResolutionWidths = { 960, 1280, 1600, 1920, 2560 };
    static readonly int[] ResolutionHeights = { 540, 720, 900, 1080, 1440 };
    static readonly string[] ScreenModeNames = { "전체 화면", "테두리 없는 창", "창 모드" };
    static readonly int[] FrameLimits = { 30, 60, 120, 144, 0 };
    static readonly int[] TextSizes = { 90, 100, 110, 120 };
    int settingsRow, bindingCapture = -1, bindingConflict = -1;
    KeyCode requestedBinding;
    float captureArmedAt, displayTrialDeadline;
    bool keysView, sliderMoved, displayTrialPending;
    Vector2 settingsLastPointer;
    DisplaySnapshot displayBeforeTrial;
    string settingsSavePath;
    GUIStyle settingsSliderTrackStyle, settingsSliderThumbStyle;

    struct DisplaySnapshot
    {
        public int width, height, quality, vSync, fps;
        public FullScreenMode mode;
    }

    public void InitializeSettings()
    {
        settingsSavePath = Path.Combine(Application.persistentDataPath, "settings.json");
        var defaults = GameShellConfig.Defaults();
        defaults.width = Mathf.Max(960, Screen.width);
        defaults.height = Mathf.Max(540, Screen.height);
        defaults.screenMode = Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen ? 0 :
            Screen.fullScreenMode == FullScreenMode.FullScreenWindow ? 1 : 2;
        defaults.qualityLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        defaults.vSyncCount = QualitySettings.vSyncCount > 0 ? 1 : 0;
        defaults.frameLimit = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
        appliedConfig = defaults;
        bool loadedSavedSettings = false;
        try
        {
            if (!validationMode && File.Exists(settingsSavePath))
            {
                var saved = JsonUtility.FromJson<GameShellConfig>(File.ReadAllText(settingsSavePath));
                int migratedHelpAction = saved != null ? saved.MigrateReservedHelpBinding() : -1;
                var failures = new List<string>();
                CheckConfig(saved, failures);
                if (failures.Count == 0)
                {
                    saved.MigrateLegacyHintBinding();
                    appliedConfig = saved.DeepCopy(); loadedSavedSettings = true;
                    if (migratedHelpAction >= 0)
                        Notify(BindingLabels[migratedHelpAction] + " 키를 " + KeyName(saved.bindings[migratedHelpAction]) + "(으)로 옮겼습니다. K는 조작 안내 전용입니다.");
                }
                else Notify("저장된 설정을 읽지 못해 기본값을 사용합니다.");
            }
        }
        catch (Exception error) { Debug.LogWarning("GAME_SHELL_SETTINGS_READ: " + error.Message); }
        if (!validationMode && loadedSavedSettings)
        {
            // The saved file contains only a previously accepted display mode in this player.
            try { ApplyDisplay(appliedConfig); }
            catch (Exception error) { Debug.LogWarning("GAME_SHELL_SAVED_DISPLAY: " + error.Message); }
        }
        draftConfig = appliedConfig.DeepCopy();
        settingsOpen = false;
        displayTrialPending = false;
        ClearBindingCapture();
        SyncSettingsRuntime(true);
    }

    public void OpenSettings()
    {
        if (appliedConfig == null) InitializeSettings();
        if (displayTrialPending) RestoreDisplayTrial(false);
        draftConfig = appliedConfig.DeepCopy();
        settingsOpen = true;
        keysView = false;
        settingsRow = 0;
        settingsLastPointer = Pointer;
        ClearBindingCapture();
        if (Event.current != null) GUIUtility.keyboardControl = 0;
        SyncSettingsRuntime();
    }

    public void CloseSettings(bool keepChanges = false)
    {
        if (!settingsOpen) return;
        if (keepChanges && !ApplyDraft()) return;
        if (displayTrialPending) RestoreDisplayTrial(false);
        draftConfig = appliedConfig.DeepCopy();
        ClearBindingCapture();
        sliderMoved = false;
        settingsOpen = false;
        keysView = false;
        if (Event.current != null) GUIUtility.keyboardControl = 0;
        SyncSettingsRuntime();
        if (!keepChanges) PlayUi("back");
    }

    public void ValidateSettings(List<string> errors)
    {
        if (errors == null) throw new ArgumentNullException("errors");
        CheckConfig(appliedConfig, errors);
        CheckConfig(draftConfig, errors);
        if (ReferenceEquals(appliedConfig, draftConfig) ||
            (appliedConfig != null && draftConfig != null && ReferenceEquals(appliedConfig.bindings, draftConfig.bindings)))
            errors.Add("Applied and draft settings must own separate copies.");
        if (string.IsNullOrEmpty(settingsSavePath) || !Path.IsPathRooted(settingsSavePath))
            errors.Add("Game settings path is not initialized.");
    }

    static void CheckConfig(GameShellConfig config, List<string> errors)
    {
        if (config == null) { errors.Add("Settings are null."); return; }
        foreach (float value in new[] { config.masterVolume, config.musicVolume, config.effectsVolume, config.ambientVolume })
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1) errors.Add("Invalid volume.");
        if (!FiniteRange(config.brightness, .5f, 1.5f)) errors.Add("Invalid brightness.");
        if (!FiniteRange(config.mouseSensitivity, .2f, 3f)) errors.Add("Invalid mouse sensitivity.");
        if (!FiniteRange(config.fieldOfView, 70f, 90f)) errors.Add("Invalid field of view.");
        if (config.width < 960 || config.width > 7680 || config.height < 540 || config.height > 4320) errors.Add("Invalid resolution.");
        if (config.screenMode < 0 || config.screenMode > 2 || config.vSyncCount < 0 || config.vSyncCount > 1) errors.Add("Invalid display mode.");
        if (config.qualityLevel < 0 || config.qualityLevel >= Mathf.Max(1, QualitySettings.names.Length)) errors.Add("Invalid quality level.");
        if (config.frameLimit != 0 && (config.frameLimit < 30 || config.frameLimit > 540)) errors.Add("Invalid frame limit.");
        if (config.textSize < 90 || config.textSize > 120) errors.Add("Invalid text size.");
        if (config.bindings == null || config.bindings.Length != BindingLabels.Length) { errors.Add("Invalid binding count."); return; }
        var used = new HashSet<KeyCode>();
        foreach (KeyCode key in config.bindings)
            if (!Enum.IsDefined(typeof(KeyCode), key) || key == KeyCode.None || ShadeLink.PlayerControls.IsReservedBindingKey(key) || !used.Add(key)) errors.Add("Invalid or duplicate key binding.");
    }
    static bool FiniteRange(float value, float low, float high)
    { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= low && value <= high; }

    void ClearBindingCapture() { bindingCapture = bindingConflict = -1; requestedBinding = KeyCode.None; }

    public void HandleSettingsInput()
    {
        if (!settingsOpen) return;
        PollDisplayTrial();
        Event e = Event.current;
        if (e == null) return;
        if ((e.type == EventType.MouseUp || (e.type == EventType.KeyUp && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))) && sliderMoved)
        { sliderMoved = false; PlayUi("confirm"); }
        if (e.type != EventType.KeyDown && e.type != EventType.MouseDown) return;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            e.Use();
            if (displayTrialPending) RestoreDisplayTrial(true);
            else if (Rebinding) { ClearBindingCapture(); PlayUi("back"); }
            else if (keysView) { keysView = false; settingsRow = 3; PlayUi("back"); }
            else CloseSettings();
            return;
        }
        if (displayTrialPending)
        {
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            { e.Use(); KeepDisplayTrial(); }
            return;
        }
        if (bindingConflict >= 0)
        {
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            { e.Use(); FinishBindingSwap(); }
            return;
        }
        if (bindingCapture >= 0)
        {
            if (Time.realtimeSinceStartup < captureArmedAt) { e.Use(); return; }
            KeyCode key = e.type == EventType.MouseDown ? (KeyCode)((int)KeyCode.Mouse0 + Mathf.Clamp(e.button, 0, 6)) : e.keyCode;
            e.Use();
            if (key == KeyCode.K)
            { PlayUi("denied"); Notify("K는 조작 안내 전용 키입니다. 다른 키를 선택해 주세요."); return; }
            if (key == KeyCode.None || ShadeLink.PlayerControls.IsReservedBindingKey(key)) { PlayUi("denied"); return; }
            requestedBinding = key;
            for (int i = 0; i < draftConfig.bindings.Length; i++)
                if (i != bindingCapture && draftConfig.bindings[i] == key) { bindingConflict = i; return; }
            draftConfig.bindings[bindingCapture] = key;
            ClearBindingCapture();
            PlayUi("confirm");
            return;
        }
        if (e.type != EventType.KeyDown) return;
        int count = RowCount();
        if (e.keyCode == KeyCode.Tab)
        {
            settingsTab = (SettingsTab)(((int)settingsTab + (e.shift ? 3 : 1)) % 4);
            keysView = false; settingsRow = 0; if (Event.current != null) GUIUtility.keyboardControl = 0; e.Use(); PlayUi("confirm"); return;
        }
        if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
        {
            settingsRow = (settingsRow + (e.keyCode == KeyCode.DownArrow ? 1 : count + 2)) % (count + 3);
            e.Use(); return;
        }
        if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
        { int direction = e.keyCode == KeyCode.RightArrow ? 1 : -1; e.Use(); AdjustRow(direction); return; }
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
        {
            e.Use();
            if (settingsRow == count) ResetCurrentTab();
            else if (settingsRow == count + 1) RevertDraft();
            else if (settingsRow == count + 2) ApplyDraft();
            else AdjustRow(0);
        }
    }

    int RowCount() { return keysView ? BindingLabels.Length : settingsTab == SettingsTab.Display ? 6 : 4; }

    void AdjustRow(int direction)
    {
        if (settingsRow >= RowCount()) return;
        if (keysView)
        {
            if (direction == 0) BeginBinding(settingsRow);
            else if (direction > 0 && settingsRow < 6) settingsRow = Mathf.Min(BindingLabels.Length - 1, settingsRow + 6);
            else if (direction < 0 && settingsRow >= 6) settingsRow -= 6;
            return;
        }
        int step = direction == 0 ? 1 : direction;
        bool continuous = false;
        if (settingsTab == SettingsTab.Display)
        {
            if (settingsRow == 0) draftConfig.screenMode = Cycle(draftConfig.screenMode, step, 3);
            if (settingsRow == 1) ChangeResolution(step);
            if (settingsRow == 2) draftConfig.qualityLevel = Cycle(draftConfig.qualityLevel, step, Mathf.Max(1, QualitySettings.names.Length));
            if (settingsRow == 3) draftConfig.vSyncCount = draftConfig.vSyncCount == 0 ? 1 : 0;
            if (settingsRow == 4) { if (draftConfig.vSyncCount > 0) { PlayUi("denied"); return; } ChangeFrameLimit(step); }
            if (settingsRow == 5) { if (direction == 0) return; draftConfig.brightness = Mathf.Clamp(draftConfig.brightness + direction * .05f, .5f, 1.5f); continuous = true; }
        }
        else if (settingsTab == SettingsTab.Sound)
        {
            if (direction == 0) return;
            if (settingsRow == 0) draftConfig.masterVolume = Mathf.Clamp01(draftConfig.masterVolume + direction * .05f);
            if (settingsRow == 1) draftConfig.musicVolume = Mathf.Clamp01(draftConfig.musicVolume + direction * .05f);
            if (settingsRow == 2) draftConfig.effectsVolume = Mathf.Clamp01(draftConfig.effectsVolume + direction * .05f);
            if (settingsRow == 3) draftConfig.ambientVolume = Mathf.Clamp01(draftConfig.ambientVolume + direction * .05f);
            continuous = true;
        }
        else if (settingsTab == SettingsTab.Controls)
        {
            if (settingsRow == 0) { if (direction == 0) return; draftConfig.mouseSensitivity = Mathf.Clamp(draftConfig.mouseSensitivity + direction * .1f, .2f, 3f); continuous = true; }
            if (settingsRow == 1) draftConfig.invertMouseY = !draftConfig.invertMouseY;
            if (settingsRow == 2) { if (direction == 0) return; draftConfig.fieldOfView = Mathf.Clamp(draftConfig.fieldOfView + direction * 5f, 70f, 90f); continuous = true; }
            if (settingsRow == 3) { keysView = true; settingsRow = 0; }
        }
        else
        {
            if (settingsRow == 0) draftConfig.cameraShake = !draftConfig.cameraShake;
            if (settingsRow == 1) draftConfig.reducedFlicker = !draftConfig.reducedFlicker;
            if (settingsRow == 2) draftConfig.textSize = TextSizes[Cycle(Array.IndexOf(TextSizes, draftConfig.textSize), step, TextSizes.Length)];
            if (settingsRow == 3) draftConfig.showHints = !draftConfig.showHints;
        }
        if (continuous) sliderMoved = true; else PlayUi("confirm");
    }
    static int Cycle(int current, int step, int count) { return ((Mathf.Clamp(current, 0, count - 1) + step) % count + count) % count; }
    void ChangeResolution(int direction)
    {
        int index = 0; for (int i = 0; i < ResolutionWidths.Length; i++) if (draftConfig.width == ResolutionWidths[i] && draftConfig.height == ResolutionHeights[i]) index = i;
        index = Cycle(index, direction, ResolutionWidths.Length);
        draftConfig.width = ResolutionWidths[index]; draftConfig.height = ResolutionHeights[index];
    }
    void ChangeFrameLimit(int direction)
    { draftConfig.frameLimit = FrameLimits[Cycle(Array.IndexOf(FrameLimits, draftConfig.frameLimit), direction, FrameLimits.Length)]; }

    public void DrawSettings()
    {
        if (!settingsOpen) return;
        PollDisplayTrial();
        PaintBrush(new Rect(55, 75, 1810, 940), new Color(0, 0, 0, .96f), 410);
        DrawText(new Rect(155, 105, 1500, 76), keysView ? "환경설정  /  키 설정" : "환경설정", 52, Foreground);
        DrawText(new Rect(160, 179, 1520, 35), "Tab 탭 이동  ·  ↑↓ 항목 선택  ·  ←→ 조절  ·  Enter 선택  ·  Esc 돌아가기", 22, Muted);
        if (displayTrialPending) { DrawDisplayConfirmation(); return; }
        if (Rebinding) { DrawBindingCapture(); return; }
        bool normal = true;
        if (Button(new Rect(1760, 112, 60, 60), "×", "settings-close", false, normal, 40)) CloseSettings();
        for (int tab = 0; tab < 4; tab++)
        {
            if (Button(new Rect(155 + tab * 230, 238, tab == 3 ? 200 : 140, 62), SettingsTabs[tab], "settings-tab-" + tab, (int)settingsTab == tab, normal, 30))
            { settingsTab = (SettingsTab)tab; keysView = false; settingsRow = 0; if (Event.current != null) GUIUtility.keyboardControl = 0; PlayUi("confirm"); }
        }
        if (keysView) DrawBindingRows(normal);
        else if (settingsTab == SettingsTab.Display) DrawDisplayRows(normal);
        else if (settingsTab == SettingsTab.Sound) DrawSoundRows(normal);
        else if (settingsTab == SettingsTab.Controls) DrawControlRows(normal);
        else DrawComfortRows(normal);
        int count = RowCount();
        if (Button(new Rect(155, 921, 430, 58), keysView ? "조작 기본값" : "이 탭 기본값", "settings-default", settingsRow == count, normal, 27)) ResetCurrentTab();
        if (Button(new Rect(1115, 921, 280, 58), "되돌리기", "settings-revert", settingsRow == count + 1, normal, 27)) RevertDraft();
        if (Button(new Rect(1435, 921, 280, 58), "적용", "settings-apply", settingsRow == count + 2, normal, 27)) ApplyDraft();
        if (displayTrialPending) DrawDisplayConfirmation();
        else if (Rebinding) DrawBindingCapture();
        settingsLastPointer = Pointer;
        SyncSettingsRuntime();
    }

    Rect Row(int index, string label)
    {
        var row = new Rect(155, 338 + index * 81, 1560, 65);
        if ((Pointer - settingsLastPointer).sqrMagnitude > 1 && row.Contains(Pointer)) settingsRow = index;
        bool selected = settingsRow == index;
        DrawText(new Rect(row.x + 18, row.y, 610, row.height), label, 29, selected ? Foreground : Muted);
        return row;
    }
    void Value(Rect row, string text) { DrawText(new Rect(925, row.y, 610, row.height), text, 28, Foreground, TextAnchor.MiddleCenter); }
    bool Arrows(Rect row, string text, string id, bool enabled, Action<int> change)
    {
        Value(row, text);
        bool altered = false;
        if (Button(new Rect(870, row.y + 3, 62, 58), "‹", id + "-less", false, enabled, 37)) { change(-1); altered = true; }
        if (Button(new Rect(1580, row.y + 3, 62, 58), "›", id + "-more", false, enabled, 37)) { change(1); altered = true; }
        if (altered) PlayUi("confirm");
        return altered;
    }
    float Slider(Rect row, float value, float low, float high, string readout, bool enabled)
    {
        // Keep Unity range input/keyboard handling, without the native GUI skin visuals.
        if (settingsSliderTrackStyle == null)
        {
            settingsSliderTrackStyle = new GUIStyle { fixedHeight = 32, stretchWidth = true,
                padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset() };
            settingsSliderThumbStyle = new GUIStyle { fixedWidth = 32, fixedHeight = 32,
                padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset() };
        }
        var sliderRect = new Rect(890, row.y + 17, 600, 32);
        bool before = GUI.enabled; Color old = GUI.color;
        GUI.enabled = before && enabled;
        float next = GUI.HorizontalSlider(sliderRect, value, low, high, settingsSliderTrackStyle, settingsSliderThumbStyle);
        GUI.enabled = before;
        float ratio = Mathf.InverseLerp(low, high, enabled ? next : value);
        var track = new Rect(sliderRect.x + 16, sliderRect.y + 13, sliderRect.width - 32, 6);
        Fill(track, new Color(.30f, .30f, .31f, 1));
        Fill(new Rect(track.x, track.y, track.width * ratio, track.height), enabled ? Foreground : Muted);
        GUI.color = enabled ? Foreground : Muted;
        GUI.DrawTexture(new Rect(sliderRect.x + ratio * (sliderRect.width - 32), sliderRect.y, 32, 32),
            circleTexture != null ? circleTexture : Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
        GUI.color = old;
        DrawText(new Rect(1510, row.y, 150, row.height), readout, 26, enabled ? Foreground : Muted, TextAnchor.MiddleRight);
        if (enabled && Mathf.Abs(next - value) > .0001f) sliderMoved = true;
        return enabled ? next : value;
    }
    void DrawDisplayRows(bool enabled)
    {
        Arrows(Row(0, "화면 모드"), ScreenModeNames[draftConfig.screenMode], "screen-mode", enabled, d => draftConfig.screenMode = Cycle(draftConfig.screenMode, d, 3));
        Arrows(Row(1, "해상도"), draftConfig.width + " × " + draftConfig.height, "resolution", enabled, ChangeResolution);
        string[] qualityNames = QualitySettings.names;
        Arrows(Row(2, "그래픽 품질"), qualityNames.Length > 0 ? qualityNames[Mathf.Clamp(draftConfig.qualityLevel, 0, qualityNames.Length - 1)] : "기본", "quality", enabled,
            d => draftConfig.qualityLevel = Cycle(draftConfig.qualityLevel, d, Mathf.Max(1, qualityNames.Length)));
        Arrows(Row(3, "수직 동기화"), draftConfig.vSyncCount > 0 ? "켜짐" : "꺼짐", "vsync", enabled, d => draftConfig.vSyncCount = draftConfig.vSyncCount == 0 ? 1 : 0);
        Arrows(Row(4, "프레임 제한"), draftConfig.vSyncCount > 0 ? "수직 동기화 사용 중" : draftConfig.frameLimit == 0 ? "제한 없음" : draftConfig.frameLimit + " FPS", "fps",
            enabled && draftConfig.vSyncCount == 0, ChangeFrameLimit);
        var row = Row(5, "밝기"); draftConfig.brightness = Slider(row, draftConfig.brightness, .5f, 1.5f, Mathf.RoundToInt(draftConfig.brightness * 100) + "%", enabled);
    }
    void DrawSoundRows(bool enabled)
    {
        var row = Row(0, "전체 음량"); draftConfig.masterVolume = Slider(row, draftConfig.masterVolume, 0, 1, Percent(draftConfig.masterVolume), enabled);
        row = Row(1, "배경음악"); draftConfig.musicVolume = Slider(row, draftConfig.musicVolume, 0, 1, Percent(draftConfig.musicVolume), enabled);
        row = Row(2, "효과음"); draftConfig.effectsVolume = Slider(row, draftConfig.effectsVolume, 0, 1, Percent(draftConfig.effectsVolume), enabled);
        row = Row(3, "환경음"); draftConfig.ambientVolume = Slider(row, draftConfig.ambientVolume, 0, 1, Percent(draftConfig.ambientVolume), enabled);
    }
    static string Percent(float value) { return Mathf.RoundToInt(value * 100) + "%"; }
    void DrawControlRows(bool enabled)
    {
        var row = Row(0, "마우스 감도"); draftConfig.mouseSensitivity = Slider(row, draftConfig.mouseSensitivity, .2f, 3f, draftConfig.mouseSensitivity.ToString("0.0") + " 배", enabled);
        Arrows(Row(1, "마우스 상하 반전"), draftConfig.invertMouseY ? "켜짐" : "꺼짐", "invert", enabled, d => draftConfig.invertMouseY = !draftConfig.invertMouseY);
        row = Row(2, "시야각"); draftConfig.fieldOfView = Slider(row, draftConfig.fieldOfView, 70, 90, Mathf.RoundToInt(draftConfig.fieldOfView) + "°", enabled);
        row = Row(3, "키 설정");
        if (Button(new Rect(890, row.y + 2, 750, 60), "이동 · 상호작용 키 변경", "key-settings", false, enabled, 27))
        { keysView = true; settingsRow = 0; PlayUi("confirm"); }
    }
    void DrawComfortRows(bool enabled)
    {
        Arrows(Row(0, "화면 흔들림"), draftConfig.cameraShake ? "켜짐" : "꺼짐", "shake", enabled, d => draftConfig.cameraShake = !draftConfig.cameraShake);
        Arrows(Row(1, "빛 깜빡임 완화"), draftConfig.reducedFlicker ? "켜짐" : "꺼짐", "flicker", enabled, d => draftConfig.reducedFlicker = !draftConfig.reducedFlicker);
        Arrows(Row(2, "글자 크기"), draftConfig.textSize + "%", "text-size", enabled, d => draftConfig.textSize = TextSizes[Cycle(Array.IndexOf(TextSizes, draftConfig.textSize), d, TextSizes.Length)]);
        Arrows(Row(3, "조작 안내 표시"), draftConfig.showHints ? "켜짐" : "꺼짐", "hints", enabled, d => draftConfig.showHints = !draftConfig.showHints);
    }
    void DrawBindingRows(bool enabled)
    {
        DrawText(new Rect(165, 310, 1510, 32), "키 또는 마우스 버튼을 선택하세요. 중복 키는 교환됩니다. Esc와 K(조작 안내)는 전용 키입니다.", 22, Muted);
        for (int i = 0; i < BindingLabels.Length; i++)
        {
            int index = i; float x = i < 6 ? 155 : 975, y = 350 + (i < 6 ? i : i - 6) * 75;
            var hit = new Rect(x, y, 740, 65);
            if ((Pointer - settingsLastPointer).sqrMagnitude > 1 && hit.Contains(Pointer)) settingsRow = i;
            bool selected = settingsRow == i;
            DrawText(new Rect(x + 15, y, 370, 65), BindingLabels[i], 26, selected ? Foreground : Muted);
            if (Button(new Rect(x + 405, y + 3, 327, 58), KeyName(draftConfig.bindings[i]), "bind-" + i, false, enabled, 24)) BeginBinding(index);
        }
        if (Button(new Rect(1310, 810, 410, 56), "조작 설정으로", "keys-back", false, enabled, 25))
        { keysView = false; settingsRow = 3; PlayUi("back"); }
    }
    void BeginBinding(int index)
    {
        bindingCapture = index; bindingConflict = -1; requestedBinding = KeyCode.None;
        captureArmedAt = Time.realtimeSinceStartup + .15f;
        if (Event.current != null) GUIUtility.keyboardControl = 0; PlayUi("confirm");
    }
    static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "마우스 왼쪽";
            case KeyCode.Mouse1: return "마우스 오른쪽";
            case KeyCode.Mouse2: return "마우스 가운데";
            case KeyCode.Space: return "Space";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.Return: return "Enter";
            case KeyCode.LeftShift: return "왼쪽 Shift";
            case KeyCode.RightShift: return "오른쪽 Shift";
            default: return key.ToString();
        }
    }
    void DrawBindingCapture()
    {
        Fill(new Rect(55, 75, 1810, 940), new Color(0, 0, 0, .70f));
        PaintBrush(new Rect(390, 370, 1140, 360), Color.black, 905);
        if (bindingConflict >= 0)
        {
            DrawText(new Rect(445, 405, 1030, 140), KeyName(requestedBinding) + " 키는 ‘" + BindingLabels[bindingConflict] + "’에서 사용 중입니다.\n‘" + BindingLabels[bindingCapture] + "’의 기존 키와 서로 바꿀까요?", 28, Foreground);
            if (Button(new Rect(485, 590, 430, 68), "서로 교환", "binding-swap", true, true, 29)) FinishBindingSwap();
            if (Button(new Rect(1000, 590, 430, 68), "취소", "binding-cancel", false, true, 29)) { ClearBindingCapture(); PlayUi("back"); }
        }
        else
        {
            DrawText(new Rect(445, 405, 1030, 160), "‘" + BindingLabels[bindingCapture] + "’에 사용할 키를 누르세요.\n마우스 버튼도 지정할 수 있습니다.  /  Esc: 취소", 31, Foreground, TextAnchor.MiddleCenter);
            DrawText(new Rect(445, 605, 1030, 52), "Esc는 메뉴 키로 고정되어 있습니다.", 23, Muted, TextAnchor.MiddleCenter);
        }
    }
    void FinishBindingSwap()
    {
        if (bindingCapture < 0 || bindingConflict < 0) return;
        KeyCode previous = draftConfig.bindings[bindingCapture];
        draftConfig.bindings[bindingCapture] = requestedBinding;
        draftConfig.bindings[bindingConflict] = previous;
        ClearBindingCapture(); PlayUi("confirm");
    }

    void ResetCurrentTab()
    {
        var d = GameShellConfig.Defaults();
        if (keysView || settingsTab == SettingsTab.Controls)
        { draftConfig.mouseSensitivity = d.mouseSensitivity; draftConfig.invertMouseY = d.invertMouseY; draftConfig.fieldOfView = d.fieldOfView; draftConfig.bindings = d.bindings; }
        else if (settingsTab == SettingsTab.Display)
        {
            draftConfig.screenMode = d.screenMode; draftConfig.width = d.width; draftConfig.height = d.height;
            draftConfig.qualityLevel = Mathf.Clamp(d.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            draftConfig.vSyncCount = d.vSyncCount; draftConfig.frameLimit = d.frameLimit; draftConfig.brightness = d.brightness;
        }
        else if (settingsTab == SettingsTab.Sound)
        { draftConfig.masterVolume = d.masterVolume; draftConfig.musicVolume = d.musicVolume; draftConfig.effectsVolume = d.effectsVolume; draftConfig.ambientVolume = d.ambientVolume; }
        else
        { draftConfig.cameraShake = d.cameraShake; draftConfig.reducedFlicker = d.reducedFlicker; draftConfig.textSize = d.textSize; draftConfig.showHints = d.showHints; }
        PlayUi("confirm"); Notify("현재 탭을 기본값으로 바꿨습니다. 적용 전까지 되돌릴 수 있습니다.");
    }
    void RevertDraft()
    {
        if (displayTrialPending) RestoreDisplayTrial(false);
        draftConfig = appliedConfig.DeepCopy(); ClearBindingCapture(); sliderMoved = false;
        SyncSettingsRuntime();
        PlayUi("back"); Notify("마지막으로 적용한 설정으로 되돌렸습니다.");
    }
    bool ApplyDraft()
    {
        if (Rebinding || displayTrialPending) { PlayUi("denied"); return false; }
        var failures = new List<string>(); CheckConfig(draftConfig, failures);
        if (failures.Count > 0) { PlayUi("denied"); Notify("설정 값을 확인해 주세요."); return false; }
        bool displayChanged = draftConfig.screenMode != appliedConfig.screenMode || draftConfig.width != appliedConfig.width ||
            draftConfig.height != appliedConfig.height || draftConfig.qualityLevel != appliedConfig.qualityLevel ||
            draftConfig.vSyncCount != appliedConfig.vSyncCount || draftConfig.frameLimit != appliedConfig.frameLimit;
        if (displayChanged && !validationMode)
        {
            displayBeforeTrial = new DisplaySnapshot { width = Screen.width, height = Screen.height, mode = Screen.fullScreenMode,
                quality = QualitySettings.GetQualityLevel(), vSync = QualitySettings.vSyncCount, fps = Application.targetFrameRate };
            try
            {
                ApplyDisplay(draftConfig); displayTrialPending = true; displayTrialDeadline = Time.realtimeSinceStartup + 15f;
                PlayUi("confirm"); return false;
            }
            catch (Exception error)
            {
                RestoreDisplay(displayBeforeTrial); PlayUi("denied");
                Debug.LogWarning("GAME_SHELL_DISPLAY: " + error.Message); Notify("화면 변경에 실패하여 이전 화면으로 돌아갔습니다."); return false;
            }
        }
        appliedConfig = draftConfig.DeepCopy();
        SyncSettingsRuntime(true);
        bool saved = PersistSettings(); PlayUi("confirm");
        Notify(saved ? "설정을 적용했습니다." : "현재 설정은 적용됐지만 파일 저장에 실패했습니다."); return true;
    }
    static void ApplyDisplay(GameShellConfig config)
    {
        QualitySettings.SetQualityLevel(config.qualityLevel, true);
        // Puzzle shadows remain visible even when a preset disables shadows.
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.vSyncCount = config.vSyncCount;
        Application.targetFrameRate = config.frameLimit == 0 ? -1 : config.frameLimit;
        var mode = config.screenMode == 0 ? FullScreenMode.ExclusiveFullScreen :
            config.screenMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(config.width, config.height, mode);
    }
    static void RestoreDisplay(DisplaySnapshot prior)
    {
        QualitySettings.SetQualityLevel(prior.quality, true);
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.vSyncCount = prior.vSync;
        Application.targetFrameRate = prior.fps;
        Screen.SetResolution(prior.width, prior.height, prior.mode);
    }
    void PollDisplayTrial()
    {
        if (displayTrialPending && Time.realtimeSinceStartup >= displayTrialDeadline)
        { RestoreDisplayTrial(false); Notify("15초 동안 유지 확인이 없어 이전 화면으로 돌아갔습니다."); }
    }
    void KeepDisplayTrial()
    {
        if (!displayTrialPending) return;
        displayTrialPending = false; appliedConfig = draftConfig.DeepCopy();
        SyncSettingsRuntime(true);
        bool saved = PersistSettings(); PlayUi("confirm");
        Notify(saved ? "새 화면 설정을 유지합니다." : "현재 화면은 유지하지만 파일 저장에 실패했습니다.");
    }
    void RestoreDisplayTrial(bool feedback)
    {
        if (!displayTrialPending) return;
        displayTrialPending = false;
        try { RestoreDisplay(displayBeforeTrial); }
        catch (Exception error) { Debug.LogWarning("GAME_SHELL_DISPLAY_RESTORE: " + error.Message); }
        draftConfig = appliedConfig.DeepCopy();
        SyncSettingsRuntime();
        if (feedback) PlayUi("back");
    }
    void DrawDisplayConfirmation()
    {
        Fill(new Rect(55, 75, 1810, 940), new Color(0, 0, 0, .75f));
        PaintBrush(new Rect(390, 370, 1140, 360), Color.black, 906);
        int seconds = Mathf.Max(0, Mathf.CeilToInt(displayTrialDeadline - Time.realtimeSinceStartup));
        DrawText(new Rect(445, 405, 1030, 135), "새 화면 설정을 유지할까요?\n" + seconds + "초 뒤 이전 화면으로 돌아갑니다.", 34, Foreground, TextAnchor.MiddleCenter);
        if (Button(new Rect(485, 590, 430, 68), "유지  ·  Enter", "display-keep", true, true, 29)) KeepDisplayTrial();
        if (Button(new Rect(1000, 590, 430, 68), "되돌리기  ·  Esc", "display-undo", false, true, 29)) RestoreDisplayTrial(true);
    }
    void OnApplicationQuit()
    {
        // An unconfirmed display mode is never treated as a saved preference.
        if (!validationMode && displayTrialPending) RestoreDisplayTrial(false);
    }

    void SyncSettingsRuntime(bool persistControls = false)
    {
        if (validationMode) return;
        GameShellSettingsRuntime.Apply(CurrentConfig, persistControls);
    }

    bool PersistSettings()
    {
        if (validationMode) return true;
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string pending = settingsSavePath + ".tmp";
            File.WriteAllText(pending, JsonUtility.ToJson(appliedConfig, true));
            if (File.Exists(settingsSavePath)) File.Copy(settingsSavePath, settingsSavePath + ".previous", true);
            File.Copy(pending, settingsSavePath, true);
            File.Delete(pending);
            return true;
        }
        catch (Exception error)
        { Debug.LogWarning("GAME_SHELL_SETTINGS_WRITE: " + error.Message); return false; }
    }
}
