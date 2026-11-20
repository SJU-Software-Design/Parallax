using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using ShadeLink;

/// <summary>Settings affect presentation/input only. Authored audio source gains and fades remain intact.</summary>
[DefaultExecutionOrder(31000)]
public sealed class GameShellSettingsRuntime : MonoBehaviour
{
    static GameShellSettingsRuntime instance;
    static readonly GameShellConfig defaults = GameShellConfig.Defaults();
    public static GameShellConfig Current { get; private set; }
    public static bool ShowControlHints { get { return (Current ?? defaults).showHints; } }
    public static float TextScale { get { return (Current ?? defaults).textSize / 100f; } }
    public static bool CameraShake { get { return (Current ?? defaults).cameraShake; } }
    public static bool ReducedFlicker { get { return (Current ?? defaults).reducedFlicker; } }
    public static float Brightness { get { return (Current ?? defaults).brightness; } }
    public static bool AudioRoutingReady { get { return instance != null && instance.mixer != null; } }

    AudioMixer mixer;
    AudioMixerGroup music, effects, ambience;
    readonly List<AudioSource> routedSources = new List<AudioSource>();
    float nextScan;
    bool optionsApplied;
    PlayerControls.Options lastOptions;
    KeyCode[] lastBindings;

    public static void Apply(GameShellConfig config, bool persistControls = false)
    {
        if (config == null) return;
        if (instance == null)
        {
            var node = new GameObject("Parallax settings runtime");
            instance = node.AddComponent<GameShellSettingsRuntime>();
            DontDestroyOnLoad(node);
        }
        Current = config;
        instance.ApplyControls(persistControls);
        // AudioMixer parameters are committed in Start/LateUpdate, not Awake.
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        mixer = Resources.Load<AudioMixer>("GameShell/SettingsMixer");
        if (mixer != null)
        {
            music = Group("Music"); effects = Group("Sfx"); ambience = Group("Ambient");
        }
        else Debug.LogWarning("GAME_SHELL_AUDIO: SettingsMixer resource is missing.");
        SceneManager.sceneLoaded += SceneLoaded;
    }

    AudioMixerGroup Group(string name)
    {
        foreach (var group in mixer.FindMatchingGroups(name))
            if (group.name == name) return group;
        return null;
    }
    void Start() { ApplyMixer(); ScanPresentation(); }
    void SceneLoaded(Scene scene, LoadSceneMode mode) { nextScan = 0; }
    void LateUpdate()
    {
        ApplyMixer();
        if (Time.unscaledTime >= nextScan)
        {
            ScanPresentation(); nextScan = Time.unscaledTime + .25f;
        }
        // Pools may change clips. Routing never alters the source.volume envelope.
        for (int i = routedSources.Count - 1; i >= 0; i--)
        {
            if (routedSources[i] == null) { routedSources.RemoveAt(i); continue; }
            RouteInternal(routedSources[i]);
        }
    }

    void ApplyControls(bool persist)
    {
        var config = Current;
        var next = new PlayerControls.Options { Sensitivity = config.mouseSensitivity * 2f,
            FieldOfView = config.fieldOfView, InvertX = false, InvertY = config.invertMouseY };
        if (!optionsApplied || persist || next.Sensitivity != lastOptions.Sensitivity ||
            next.FieldOfView != lastOptions.FieldOfView || next.InvertY != lastOptions.InvertY)
        {
            PlayerControls.ApplyRuntimeOptions(next, persist);
            lastOptions = next; optionsApplied = true;
        }
        bool same = lastBindings != null && lastBindings.Length == config.bindings.Length;
        if (same) for (int i = 0; i < lastBindings.Length; i++)
            if (lastBindings[i] != config.bindings[i]) { same = false; break; }
        if (!same)
        {
            PlayerControls.ApplyBindings(config.bindings);
            lastBindings = (KeyCode[])config.bindings.Clone();
        }
    }

    public static float VolumeDecibels(float linear)
    { return linear <= 0 ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear), -80f, 0f); }
    void ApplyMixer()
    {
        if (mixer == null || Current == null) return;
        mixer.SetFloat("MasterVolume", VolumeDecibels(Current.masterVolume));
        mixer.SetFloat("MusicVolume", VolumeDecibels(Current.musicVolume));
        mixer.SetFloat("SfxVolume", VolumeDecibels(Current.effectsVolume));
        mixer.SetFloat("AmbientVolume", VolumeDecibels(Current.ambientVolume));
    }

    /// <summary>Optional immediate hook after assigning a clip, before Play. Shell audio keeps its own live gain.</summary>
    public static void Route(AudioSource source)
    {
        if (instance == null || source == null) return;
        if (!instance.routedSources.Contains(source)) instance.routedSources.Add(source);
        instance.RouteInternal(source);
    }
    void RouteInternal(AudioSource source)
    {
        if (mixer == null || source == null || source.GetComponentInParent<ParallaxGameShell>() != null) return;
        // Preserve a pre-existing external mixer. Stage banks currently have no mixer assignment.
        var previous = source.outputAudioMixerGroup;
        if (previous != null && previous.audioMixer != mixer) return;
        string category = AudioCategory(source.clip == null ? "" : source.clip.name, source.gameObject.name);
        AudioMixerGroup target = category == "Music" ? music : category == "Ambient" ? ambience : effects;
        if (target != null && previous != target) source.outputAudioMixerGroup = target;
    }
    public static string AudioCategory(string clipName, string sourceName = "")
    {
        string cue = (clipName ?? "").ToLowerInvariant();
        string node = (sourceName ?? "").ToLowerInvariant();
        if (cue.Contains("bgm") || cue.Contains("music"))
            return "Music";
        if (cue.Contains("ambience") || cue.Contains("ambient") || cue.Contains("ventilation") ||
            cue.Contains("canopy_drips") || cue.Contains("wind") || cue == "flight" || node.Contains("shore 115") || node.Contains("ventilation") || node.Contains("spatial wind"))
            return "Ambient";
        return "Sfx";
    }

    void ScanPresentation()
    {
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Route(source);
        // Stage cameras are intentionally untagged. Only the explicit player input camera is eligible.
        var camera = PlayerControls.ConfiguredCamera;
        if (camera != null && camera.GetComponent<GameShellBrightness>() == null)
            camera.gameObject.AddComponent<GameShellBrightness>();
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance != this) return;
        foreach (var source in routedSources)
            if (source != null && source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.audioMixer == mixer)
                source.outputAudioMixerGroup = null;
        instance = null; Current = null;
    }
}
