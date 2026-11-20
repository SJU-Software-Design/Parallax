using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using ShadeLink;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;

// Explicit command-line test only. No capture, window automation or speaker output.
// Run exercise, then a NEW process with phase reload and the SAME output directory.
public sealed partial class PlayerShellValidation : MonoBehaviour
{
    const string Flag = "-player-shell-validate";
    static bool claimed;
    static string rootDirectory, isolatedDirectory, userSavePath, userBackupPath, userSettingsPath;
    static string userSaveBefore, userBackupBefore, userSettingsBefore, userSettingsBackupBefore;
    readonly List<CheckResult> checks = new List<CheckResult>();
    readonly List<string> errors = new List<string>();
    readonly Dictionary<int, LoadingTrace> loadingTraces = new Dictionary<int, LoadingTrace>();
    readonly HashSet<int> verifiedLoading = new HashSet<int>();
    int lastObservedTipIndex = -1;
    string phase, failure;
    double deadline;
    [Serializable] sealed class CheckResult { public string name, detail; public bool passed; }
    [Serializable] sealed class RelaunchExpectation { public int processId; public GameProgressRecord record; public string primarySha256; }
    [Serializable] sealed class Report
    {
        public bool passed, productionStorageUnchanged, batchMode, renderedPixelsVerified;
        public int processId, captureCount;
        public string phase, unity, scene, failure, saveDirectory, graphicsDevice, scope;
        public CheckResult[] checks;
        public string[] errors;
        public LoadingTrace[] loadingTransitions;
        public PlayerShellElevatorValidation.Report elevator;
    }
    public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;
    static string Argument(string key)
    {
        string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!Requested || claimed) return;
        claimed = true;
        string requested = Argument(Flag);
        if (string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(requested))
            throw new ArgumentException(Flag + " requires an absolute test output directory.");
        rootDirectory = Path.GetFullPath(requested);
        isolatedDirectory = Path.Combine(rootDirectory, "SaveData");
        userSavePath = GameProgress.SavePath; userBackupPath = GameProgress.BackupPath;
        userSettingsPath = Path.Combine(Application.persistentDataPath, "settings.json");
        userSaveBefore = HashFile(userSavePath); userBackupBefore = HashFile(userBackupPath);
        userSettingsBefore = HashFile(userSettingsPath); userSettingsBackupBefore = HashFile(userSettingsPath + ".previous");
        if (string.Equals(Path.GetDirectoryName(userSavePath), isolatedDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test progress must not use the real player save directory.");
        if (!GameProgressRuntime.SetSaveDirectoryForValidation(isolatedDirectory))
            throw new InvalidOperationException(GameProgressRuntime.LastError);
        Directory.CreateDirectory(rootDirectory);
        AudioListener.volume = 0;
        var host = new GameObject("Player shell integration validation");
        DontDestroyOnLoad(host); host.AddComponent<PlayerShellValidation>();
    }
    void Awake() { SceneManager.sceneLoaded += Loaded; Application.logMessageReceived += Log; }
    void OnDestroy() { SceneManager.sceneLoaded -= Loaded; Application.logMessageReceived -= Log; }
    void Loaded(Scene scene, LoadSceneMode mode)
    {
        // Scene automation only avoids hidden-window focus pausing the game.
        // No checkpoint, completion flag, reset or Resume is supplied here.
        var gallery = FindFirstObjectByType<GalleryGame>(); if (gallery != null) gallery.automated = true;
        var beach = FindFirstObjectByType<BeachGame>(); if (beach != null) beach.automated = true;
        Application.runInBackground = true; QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60; AudioListener.volume = 0;
        ObserveLoading();
        var shell = ParallaxGameShell.Instance;
        if (shell != null && loadingTraces.TryGetValue(shell.LoadingSequenceId, out var trace) && scene.name == shell.LoadingTarget) trace.sceneLoadedAt = Time.realtimeSinceStartupAsDouble;
    }
    void LateUpdate() { AudioListener.volume = 0; ObserveLoading(); }
    void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    void Check(bool passed, string name, string detail = "")
    {
        checks.Add(new CheckResult { passed = passed, name = name, detail = detail });
        Debug.Log((passed ? "PLAYER_SHELL_PASS " : "PLAYER_SHELL_FAIL ") + name + " " + detail);
        if (!passed) throw new InvalidOperationException(name + ": " + detail);
    }
    IEnumerator Start()
    {
        phase = Argument("-player-shell-phase") ?? "exercise";
        deadline = Time.realtimeSinceStartupAsDouble + 300;
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        var stack = new Stack<IEnumerator>(); stack.Push(Sequence());
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try { more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
            catch (Exception error) { failure = error.ToString(); break; }
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        ObserveLoading();
        bool storageUnchanged = HashFile(userSavePath) == userSaveBefore && HashFile(userBackupPath) == userBackupBefore &&
            HashFile(userSettingsPath) == userSettingsBefore && HashFile(userSettingsPath + ".previous") == userSettingsBackupBefore;
        checks.Add(new CheckResult { name = "Real player progress/settings and backups remain byte-identical", passed = storageUnchanged });
        var report = new Report {
            passed = failure == null && errors.Count == 0 && storageUnchanged && checks.All(c => c.passed),
            productionStorageUnchanged = storageUnchanged, batchMode = Application.isBatchMode,
            processId = System.Diagnostics.Process.GetCurrentProcess().Id, captureCount = 0,
            phase = phase, unity = Application.unityVersion, scene = SceneManager.GetActiveScene().name, graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
            failure = failure, saveDirectory = isolatedDirectory, checks = checks.ToArray(), errors = errors.ToArray(),
            renderedPixelsVerified = false, loadingTransitions = loadingTraces.Values.OrderBy(t => t.id).ToArray(),
            elevator = phase == "elevator" ? PlayerShellElevatorValidation.LatestReport : null,
            scope = "Actual MainMenu/Tutorial/Gallery/Beach scene ownership, production progress/pause APIs, isolated save/title/continue, Gallery room-2 checkpoint reconstruction fixture, pure admin gesture and reflected admin-session policy, backup recovery, and separate-process reload. Sword Tip validation additionally covers actual first room entry before puzzle completion, autosave/respawn/restart persistence, checksum-valid old-field omission, cross-map carry, sandbox separation and Continue no-repeat snapshots. Loading transition telemetry additionally checks fade-state continuity, actual async-call ordering/progress, minimum opaque duration, restore-before-reveal/input blocking and injected-failure cleanup. Validation advances presentation state but skips GUI/input dispatch; renderedPixelsVerified=false. The optional elevator phase exercises the actual Tutorial-to-Gallery elevator scene transfer with the shell alive, stage-owned arrival without shell overlays, physical cabin exit, pause and checkpoint saving. The optional polish phase additionally checks actual tutorial exposure ticks/gauge data, three-map pickup refusal audio, Gallery player contacts and the final right window material/portal isolation. This does not verify physical keyboard/mouse actions, OnGUI pixels/layout, OS cursor lock, continuous puzzle solving or subjective audio. Speaker output is muted; no capture API is used."
        };
        File.WriteAllText(Path.Combine(rootDirectory, "player-shell-" + phase + "-results.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        Debug.Log("PLAYER_SHELL_VALIDATION " + (report.passed ? "PASSED" : "FAILED") + " phase=" + phase + " checks=" + checks.Count + " " + failure);
        Application.Quit(report.passed ? 0 : 1);
    }
    IEnumerator Sequence()
    {
        yield return null; yield return null;
        Check(phase == "exercise" || phase == "reload" || phase == "polish" || phase == "elevator", "Known validation phase", phase);
        Check(SceneManager.GetActiveScene().name == "MainMenu", "Build starts in MainMenu");
        Check(Path.GetFullPath(GameProgress.SavePath).StartsWith(isolatedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Progress path isolated before gameplay", GameProgress.SavePath);
        Check(ParallaxGameShell.Instance != null && !ParallaxGameShell.Administrator, "New process starts in player mode");
        Check(ParallaxGameShell.OwnsPauseMenus && !ParallaxGameShell.AllowStagePauseInput, "Player shell owns pause input policy during validation");
        VerifyOwnership("MainMenu"); VerifyAssetsAndSettings(); VerifyAdminSequence(); VerifyLoadingText(); VerifyLegacySwordEncounterDefault();
        if (phase == "reload") { yield return ReloadSequence(); yield break; }
        if (phase == "polish") { yield return PolishSequence(); yield break; }
        if (phase == "elevator") { yield return PlayerShellElevatorValidation.Run(Check); yield break; }

        string emptyDirectory = Path.Combine(rootDirectory, "NoSaveProbe-" + Guid.NewGuid().ToString("N"));
        Check(GameProgressRuntime.SetSaveDirectoryForValidation(emptyDirectory), "Empty-save probe uses a separate directory");
        Check(!GameProgressRuntime.HasSave, "Continue model is disabled without a valid save");
        GameProgressRuntime.ContinueGame(); yield return null;
        Check(GameProgressRuntime.CurrentStage == "MainMenu" && !GameProgressRuntime.Transitioning, "Missing-save Continue does not leave the title");
        Check(GameProgressRuntime.SetSaveDirectoryForValidation(isolatedDirectory), "Restore dedicated test-save directory");
        var shell = ParallaxGameShell.Instance;
        Check(GameProgress.TrySave(new GameProgressRecord { stage = "Tutorial", swordEncountered = true }), "Seed prior encountered progress before new-game reset test");
        yield return VerifyLoadingFailureCleanup();
        GameProgressRuntime.NewGame(); yield return WaitForStage("Tutorial");
        Check(ParallaxGameShell.Instance == shell, "Shell persists from title into gameplay");
        Check(GameProgressRuntime.HasSave && !GameProgressRuntime.TestSession, "Fresh Tutorial writes its first real checkpoint");
        VerifyOwnership("Tutorial"); yield return VerifyPause("Tutorial");
        Check(GameProgressRuntime.SaveCurrent(), "Save current Tutorial through production API", GameProgressRuntime.LastError);
        GameProgressRecord tutorial = ReadSaved();
        Check(tutorial.stage == "Tutorial" && tutorial.tutorialStage == 1, "Fresh Tutorial save identifies the first lesson");
        Check(!tutorial.swordEncountered, "Successful new game resets the sword-encounter flag from the previous run");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        Check(ParallaxGameShell.Instance == shell && !GameProgressRuntime.GameplayActive, "Return-to-title releases gameplay and keeps one shell");
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Tutorial", true);
        Check(((IGameProgressStage)FindFirstObjectByType<TutorialGame>()).CaptureProgress().CheckpointKey == tutorial.CheckpointKey, "Tutorial checkpoint survives save/title/continue");
        GameProgressRuntime.RestartCheckpoint(); yield return null;
        Check(GameProgressRuntime.GameplayActive && !GameProgressRuntime.Paused, "Current Tutorial checkpoint can restart and resume");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");

        // Explicit restore fixture: completed bronze and Nike rooms, unsolved Dike.
        // This is not a claim that the validator solved the physical puzzles.
        var galleryFixture = new GameProgressRecord { stage = "Gallery", checkpoint = 1, galleryRoom = 2, milestones = 3,
            tutorialStage = 1, tutorialStep = 0, tutorialFirstSize = 0 };
        Check(GameProgress.TrySave(galleryFixture), "Write validated Gallery room-2 restore fixture", GameProgress.LastError);
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Gallery", true);
        VerifyGalleryCheckpoint(); VerifyOwnership("Gallery"); yield return VerifyPause("Gallery");
        yield return VerifySwordEncounterAndContinue(galleryFixture);
        galleryFixture.swordEncountered = true;
        yield return VerifyActualSettings(); yield return VerifyCompletedGalleryRestore();
        GameProgressRuntime.RestartCheckpoint(); yield return null;
        VerifyGalleryRestartAtCheckpoint();
        // Restart at the existing Nike entrance deliberately resets Nike. Restore
        // the declared completed-prefix fixture again for the separate save/load test.
        ((IGameProgressStage)FindFirstObjectByType<GalleryGame>()).RestoreProgress(galleryFixture);
        yield return null; VerifyGalleryCheckpoint();
        Check(GameProgressRuntime.SaveCurrent(), "Save reconstructed live Gallery checkpoint", GameProgressRuntime.LastError);
        var saved = ReadSaved();
        Check(saved.stage == "Gallery" && saved.checkpoint == 1 && (saved.milestones & 3) == 3 && saved.CheckpointKey == ((IGameProgressStage)FindFirstObjectByType<GalleryGame>()).CaptureProgress().CheckpointKey, "Captured Gallery record retains completed-room flags");
        VerifyInvalidSaveDoesNotReplace(saved); VerifyBackupRecovery(saved);
        yield return VerifyCrossMapEncounterCarry();
        string primaryBefore = HashFile(GameProgress.SavePath), backupBefore = HashFile(GameProgress.BackupPath);
        SetAdmin(true);
        Check(ParallaxGameShell.Administrator && PauseCount() == 5, "Admin changes the pause menu by one map-travel item");
        GameProgressRuntime.AdminTravel("Beach"); yield return WaitForStage("Beach");
        Check(GameProgressRuntime.TestSession, "Admin map travel enters a separate test session");
        VerifyOwnership("Beach"); yield return VerifyPause("Beach");
        yield return VerifyCompletedBeachRestore();
        Check(GameProgressRuntime.SaveCurrent(), "Admin save-and-exit can save its sandbox", GameProgressRuntime.LastError);
        Check(File.Exists(GameProgress.AdminSavePath) && !string.Equals(GameProgress.AdminSavePath, GameProgress.SavePath, StringComparison.OrdinalIgnoreCase), "Admin progress uses a separate JSON file");
        Check(!ReadEnvelopeRecord(GameProgress.AdminSavePath).swordEncountered, "Admin sandbox does not inherit the normal-run sword encounter");
        Check(HashFile(GameProgress.SavePath) == primaryBefore && HashFile(GameProgress.BackupPath) == backupBefore, "Admin travel/autosave/manual-save preserve player primary and backup bytes");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Gallery", true, true);
        Check(!GameProgressRuntime.TestSession, "Continue uses the normal save instead of the admin sandbox");
        VerifyGalleryCheckpoint();
        SetAdmin(false); Check(PauseCount() == 4, "Player menu returns to four commands");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        saved = ReadSaved();
        var expectation = new RelaunchExpectation { processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            record = saved, primarySha256 = HashFile(GameProgress.SavePath) };
        File.WriteAllText(Path.Combine(rootDirectory, "relaunch-expectation.json"), JsonUtility.ToJson(expectation, true), new UTF8Encoding(false));
        Check(saved.stage == "Gallery" && saved.checkpoint == 1 && (saved.milestones & 3) == 3 && saved.swordEncountered, "Persisted checkpoint, completion and encounter flags ready for a NEW process");
    }
    IEnumerator ReloadSequence()
    {
        string expectationPath = Path.Combine(rootDirectory, "relaunch-expectation.json");
        Check(File.Exists(expectationPath), "Exercise process produced a reload expectation");
        var expected = JsonUtility.FromJson<RelaunchExpectation>(File.ReadAllText(expectationPath));
        Check(expected != null && expected.record != null, "Reload expectation is readable");
        Check(expected.processId != System.Diagnostics.Process.GetCurrentProcess().Id, "Reload runs in a distinct OS process");
        var saved = ReadSaved();
        Check(saved.saveId == expected.record.saveId && HashFile(GameProgress.SavePath) == expected.primarySha256, "Save identifier and exact file bytes survive process restart");
        Check(saved.CheckpointKey == expected.record.CheckpointKey, "Checkpoint fields survive process restart");
        Check(saved.swordEncountered == expected.record.swordEncountered, "Sword-encounter flag survives a distinct process restart");
        Check(!ParallaxGameShell.Administrator && !GameProgressRuntime.TestSession && PauseCount() == 4, "Admin unlock is not persisted into the new process");
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Gallery", true, expected.record.swordEncountered);
        VerifyGalleryCheckpoint(); VerifyOwnership("Gallery");
        Check(!GameProgressRuntime.TestSession, "Fresh-process Continue restores player progress");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
    }
    IEnumerator WaitForStage(string stage, bool expectedRandomTip = false, bool expectedSwordTipUnlocked = false)
    {
        double until = Time.realtimeSinceStartupAsDouble + 100;
        while (GameProgressRuntime.CurrentStage != stage || GameProgressRuntime.Transitioning ||
            stage != "MainMenu" && !GameProgressRuntime.GameplayActive)
        {
            if (Time.realtimeSinceStartupAsDouble > until || Time.realtimeSinceStartupAsDouble > deadline)
                throw new TimeoutException("Waiting for " + stage + "; scene=" + GameProgressRuntime.CurrentStage + "; error=" + GameProgressRuntime.LastError);
            yield return null;
        }
        yield return null; yield return null;
        Check(string.IsNullOrEmpty(GameProgressRuntime.LastError), "Stage handoff completes without an error: " + stage, GameProgressRuntime.LastError);
        if (stage != "MainMenu") VerifySuccessfulLoading(stage, expectedRandomTip, expectedSwordTipUnlocked);
    }
    IEnumerator VerifyPause(string stage)
    {
        GameProgressRuntime.Resume();
        Check(!GameProgressRuntime.Paused, "Production resume releases " + stage);
        GameProgressRuntime.Pause();
        Check(GameProgressRuntime.Paused && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Production pause releases pointer: " + stage);
        Vector3 position = PlayerPosition(); float elapsed = StageElapsed();
        StepGameplay(.05f);
        yield return new WaitForSecondsRealtime(.12f);
        Check(Vector3.Distance(PlayerPosition(), position) < .015f && Mathf.Abs(StageElapsed() - elapsed) < .001f, "Pause freezes gameplay position/timer: " + stage);
        GameProgressRuntime.Resume();
        Check(!GameProgressRuntime.Paused, "Production resume restores gameplay: " + stage);
        if (stage != "Tutorial") { float before = StageElapsed(); StepGameplay(.05f); Check(StageElapsed() > before + .01f, "Production Tick advances again after resume: " + stage); }
    }
    void VerifyGalleryCheckpoint()
    {
        var game = FindFirstObjectByType<GalleryGame>();
        Check(game != null && game.shadowCourse != null && game.bottleBalance != null, "Gallery restore owns its real puzzle components", GalleryDiagnostic(game));
        Check(game.shadowCourse.TutorialCleared && game.shadowCourse.NikeCleared && game.shadowCourse.NikeUnlocked && game.shadowCourse.NikeGateOpen,
            "Gallery completed bronze/Nike flags reconstruct actual open progression gates", GalleryDiagnostic(game));
        Check(!game.bottleBalance.Solved && game.checkpoint == 1, "Current Dike room remains unsolved at its checkpoint", GalleryDiagnostic(game));
        Check(Vector3.Distance(game.controller.transform.position, game.SpawnPoints[1] + Vector3.up * .025f) < 1.5f, "Gallery resumes at the unchanged production death-respawn point", GalleryDiagnostic(game));
    }
    void VerifyGalleryRestartAtCheckpoint()
    {
        var game = FindFirstObjectByType<GalleryGame>();
        Check(game != null && game.shadowCourse != null && game.bottleBalance != null,
            "Gallery checkpoint restart keeps its actual puzzle components", GalleryDiagnostic(game));
        var course = game.shadowCourse;
        Check(course.TutorialCleared && !course.NikeCleared && !course.NikeUnlocked && course.NikeGateOpening <= .005f,
            "Production Nike-entrance restart resets Nike and preserves completed bronze", GalleryDiagnostic(game));
        Check(game.checkpoint == 1 && !game.bottleBalance.Solved && game.active && !GameProgressRuntime.Paused &&
            Vector3.Distance(game.controller.transform.position, game.SpawnPoints[1] + Vector3.up * .025f) < 1.5f,
            "Production restart resumes at the unchanged Nike death-respawn point", GalleryDiagnostic(game));
    }
    static string GalleryDiagnostic(GalleryGame game)
    {
        if (game == null) return "No GalleryGame; scene=" + SceneManager.GetActiveScene().name;
        var course = game.shadowCourse;
        GameProgressRecord saved = GameProgressRuntime.PeekSavedRecord();
        return "checkpoint=" + game.checkpoint + " active=" + game.active + " paused=" + GameProgressRuntime.Paused +
            " feet=" + (game.player == null ? "<none>" : game.player.feet.ToString("F3")) +
            " controller=" + (game.controller == null ? "<none>" : game.controller.transform.position.ToString("F3")) +
            " respawn=" + (game.SpawnPoints == null || game.SpawnPoints.Length < 2 ? "<none>" : game.SpawnPoints[1].ToString("F3")) +
            " TutorialCleared=" + course?.TutorialCleared + " NikeCleared=" + course?.NikeCleared +
            " NikeUnlocked=" + course?.NikeUnlocked + " gateOpening=" + course?.NikeGateOpening +
            " InDikeRoom=" + game.bottleBalance?.InRoom + " DikeSolved=" + game.bottleBalance?.Solved +
            " live=" + JsonUtility.ToJson(game.CaptureProgress()) + " saved=" + (saved == null ? "<none>" : JsonUtility.ToJson(saved));
    }
    void VerifyOwnership(string stage)
    {
        Check(FindObjectsByType<ParallaxGameShell>(FindObjectsSortMode.None).Length == 1, "One persistent shell: " + stage);
        Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1, "One enabled audio listener: " + stage);
        Check(ParallaxGameShell.Instance.GetComponentsInChildren<Camera>(true).Length == 0, "Menu camera is not parented to persistent shell: " + stage);
    }
    void VerifyAssetsAndSettings()
    {
        foreach (string stage in new[] { "MainMenu", "Tutorial", "Gallery", "Beach" })
            Check(Application.CanStreamedLevelBeLoaded(stage), "Scene belongs to built player: " + stage);
        Texture2D background = Resources.Load<Texture2D>("Preview/Background");
        Check(background != null && background.width == 1672 && background.height == 941, "Original background pixel dimensions survive import");
        Check(Resources.Load<Texture2D>("Preview/SchoolLogo") != null, "Official school logo is bundled");
        foreach (string key in new[] { "BodyFont", "TitleFont" }) Check(Resources.Load<Font>("Preview/" + key) != null, "Full font is bundled: " + key);
        Font body = Resources.Load<Font>("Preview/BodyFont");
        foreach (char c in "세종대학교SW기초설계프로젝트3조게임재개하기설정현재구간저장나맵이동")
            Check(body.HasCharacter(c), "Body font includes glyph U+" + ((int)c).ToString("X4"));
        foreach (string key in new[] { "Lamp/Switch", "Lamp/Startup", "Lamp/Hum", "Lamp/Rattle", "Lamp/Crackle", "UI/Confirm", "UI/Back", "UI/Denied" })
        { var clip = Resources.Load<AudioClip>("Preview/" + key); Check(clip != null && clip.samples > 0 && clip.length > .01f, "Real sound clip is loaded: " + key); }
        AudioMixer mixer = Resources.Load<AudioMixer>("GameShell/SettingsMixer");
        Check(mixer != null, "Native settings AudioMixer is bundled");
        var settingsErrors = new List<string>(); ParallaxGameShell.Instance.ValidateSettings(settingsErrors);
        Check(settingsErrors.Count == 0, "Settings defaults/bindings/cloned draft validate without user I/O", string.Join("; ", settingsErrors));
        Check(PauseCount() == 4, "Default player pause has four commands");
    }
    void VerifyAdminSequence()
    {
        var sequence = new AdminUnlockSequence();
        Action<float> correct = start => { for (int i = 0; i < 5; i++) sequence.Mouse(i < 2 ? 0 : 1, true, start + i * .2f); };
        correct(1); Check(sequence.Backquote(true, 2), "Logo left-left/right-right-right/backquote unlock sequence");
        Check(sequence.Step == 0 && !sequence.Backquote(true, 2.1f), "Successful gesture resets and cannot repeat without clicks");
        sequence.Mouse(0, true, 3); sequence.Mouse(0, false, 3.1f);
        Check(sequence.Step == 0 && !sequence.Backquote(true, 3.2f), "Off-logo click cancels the gesture");
        sequence.Reset(); sequence.Mouse(0, true, 4); sequence.Mouse(1, true, 4.2f); sequence.Mouse(1, true, 4.4f);
        Check(!sequence.Backquote(true, 4.5f), "Wrong click order cannot unlock");
        correct(6); Check(!sequence.Backquote(true, 11.1f), "Four-second idle timeout rejects a late backquote");
        correct(12); Check(!sequence.Backquote(false, 13), "Backquote outside the logo cannot unlock");
        Check(!ParallaxGameShell.Administrator, "Pure gesture tests do not unlock the live session");
    }
    void VerifyInvalidSaveDoesNotReplace(GameProgressRecord saved)
    {
        string before = HashFile(GameProgress.SavePath); var invalid = saved.Copy(); invalid.stage = "InvalidStage";
        Check(!GameProgress.TrySave(invalid), "Invalid checkpoint records are rejected");
        Check(HashFile(GameProgress.SavePath) == before, "Rejected save preserves the valid primary bytes");
        ReadSaved();
    }
    void VerifyBackupRecovery(GameProgressRecord saved)
    {
        Check(GameProgress.TrySave(saved), "Create a valid recovery backup", GameProgress.LastError);
        byte[] primary = File.ReadAllBytes(GameProgress.SavePath), backup = File.ReadAllBytes(GameProgress.BackupPath);
        try
        {
            File.WriteAllText(GameProgress.SavePath, "{deliberately corrupt isolated validation fixture", new UTF8Encoding(false));
            Check(GameProgress.TryLoad(out var recovered) && GameProgress.RecoveredBackup, "Corrupt primary falls back to validated backup", GameProgress.LastError);
            Check(recovered.CheckpointKey == saved.CheckpointKey && recovered.saveId == saved.saveId, "Recovery returns the previous complete checkpoint record");
        }
        finally { File.WriteAllBytes(GameProgress.SavePath, primary); File.WriteAllBytes(GameProgress.BackupPath, backup); }
        ReadSaved();
    }
    [Serializable] sealed class LoadingSample
    {
        public double time;
        public string phase, scene;
        public float alpha, actual, visible, sceneProgress;
        public bool blocked, paused;
    }
    [Serializable] sealed class LoadingTrace
    {
        public int id, observedFrames, tipIndex, previousTipIndex;
        public string target, tip;
        public bool randomizedTip, swordTipUnlocked;
        public double startedAt, opaqueAt, readyAt, endedAt, actualLoadCalledAt = -1, sceneLoadedAt = -1, firstFadeInAt = -1;
        public bool cancelled, opaqueFrameBeforeLoad, fadeOutIntermediate, fadeInIntermediate, destinationPauseObserved, resumeGuardChecked;
        public List<string> violations = new List<string>();
        public List<LoadingSample> samples = new List<LoadingSample>();
        [NonSerialized] public float previousActual = -1, previousVisible = -1, previousAlpha;
        [NonSerialized] public string previousPhase;
    }
    static void Violation(LoadingTrace trace, string message)
    { if (!trace.violations.Contains(message)) trace.violations.Add(message); }
    void ObserveLoading()
    {
        var shell = ParallaxGameShell.Instance;
        if (shell == null || shell.LoadingSequenceId <= 0) return;
        if (!loadingTraces.TryGetValue(shell.LoadingSequenceId, out var trace))
        {
            trace = new LoadingTrace { id = shell.LoadingSequenceId, target = shell.LoadingTarget, tip = shell.CurrentLoadingTip,
                tipIndex = shell.CurrentLoadingTipIndex, randomizedTip = shell.LoadingTipRandomized, swordTipUnlocked = shell.LoadingSwordTipUnlocked, previousTipIndex = lastObservedTipIndex };
            lastObservedTipIndex = trace.tipIndex;
            loadingTraces.Add(trace.id, trace);
        }
        try
        {
            if (!string.Equals(trace.tip, shell.CurrentLoadingTip, StringComparison.Ordinal) || trace.tipIndex != shell.CurrentLoadingTipIndex || trace.randomizedTip != shell.LoadingTipRandomized || trace.swordTipUnlocked != shell.LoadingSwordTipUnlocked)
                Violation(trace, "Selected Tip changed within one loading sequence.");
            trace.observedFrames++; trace.startedAt = shell.LoadingStartedAt; trace.opaqueAt = shell.LoadingOpaqueAt;
            trace.readyAt = shell.LoadingReadyAt; trace.endedAt = shell.LoadingEndedAt; trace.cancelled = shell.LastLoadingCancelled;
            string state = shell.LoadingPhase.ToString(); float alpha = shell.LoadingOverlayAlpha;
            float actual = shell.LoadingActualProgress, visible = shell.LoadingVisibleProgress;
            if (!FiniteUnit(alpha) || !FiniteUnit(actual) || !FiniteUnit(visible)) Violation(trace, "Alpha/progress must be finite in [0,1].");
            if (actual + .0001f < trace.previousActual || visible + .0001f < trace.previousVisible) Violation(trace, "Actual/visible progress moved backwards.");
            if (state == "FadingOut" && trace.previousPhase == state && alpha + .0001f < trace.previousAlpha) Violation(trace, "Fade-out alpha moved backwards.");
            if (state == "FadingIn" && trace.previousPhase == state && alpha - .0001f > trace.previousAlpha) Violation(trace, "Fade-in alpha moved backwards.");
            bool active = shell.LoadingPresentationActive;
            bool blocked = ParallaxGameShell.BlocksGameplay, paused = GameProgressRuntime.Paused;
            if (active && (!GameProgressRuntime.Transitioning || !blocked)) Violation(trace, "Gameplay input was released during a loading presentation.");
            if (state == "FadingOut" && alpha > .001f && alpha < .999f) trace.fadeOutIntermediate = true;
            if (state == "FadingIn")
            {
                if (trace.firstFadeInAt < 0) trace.firstFadeInAt = Time.realtimeSinceStartupAsDouble;
                if (alpha > .001f && alpha < .999f) trace.fadeInIntermediate = true;
                if (!GameProgressRuntime.GameplayActive || !paused) Violation(trace, "Destination gameplay was not held paused throughout reveal.");
            }
            if (state == "Loading" && alpha < .999f) Violation(trace, "Loading phase was not fully opaque.");
            if (trace.sceneLoadedAt >= 0 && SceneManager.GetActiveScene().name == trace.target && (state == "Loading" || state == "FadingIn"))
            {
                IGameProgressStage destination = trace.target == "Tutorial" ? FindFirstObjectByType<TutorialGame>() :
                    trace.target == "Gallery" ? FindFirstObjectByType<GalleryGame>() :
                    trace.target == "Beach" ? FindFirstObjectByType<BeachGame>() : null;
                if (destination == null || !destination.ProgressPaused || Time.timeScale != 0)
                    Violation(trace, "Destination first updates were not held paused with timeScale zero.");
                else trace.destinationPauseObserved = true;
                if (state == "FadingIn" && !trace.resumeGuardChecked)
                {
                    trace.resumeGuardChecked = true;
                    GameProgressRuntime.Resume();
                    if (destination == null || !destination.ProgressPaused || Time.timeScale != 0)
                        Violation(trace, "Resume command bypassed the loading transition guard.");
                }
            }
            double called = StageTravelMenu.SceneLoadCalledAt;
            if (active && called >= trace.startedAt && called >= 0) trace.actualLoadCalledAt = called;
            if (state == "Loading" && alpha >= .999f && (called < trace.startedAt || called < 0)) trace.opaqueFrameBeforeLoad = true;
            if (trace.readyAt <= 0 && visible > actual * .9f + .002f) Violation(trace, "Bar advanced beyond the real async-derived target before restoration.");
            if (trace.actualLoadCalledAt >= 0 && trace.readyAt <= 0 && actual > StageTravelMenu.SceneLoadProgress + .002f)
                Violation(trace, "Reported actual progress exceeded the real AsyncOperation observation.");
            var sample = new LoadingSample { time = Time.realtimeSinceStartupAsDouble, phase = state, alpha = alpha,
                actual = actual, visible = visible, sceneProgress = StageTravelMenu.SceneLoadProgress,
                scene = SceneManager.GetActiveScene().name, blocked = blocked, paused = paused };
            LoadingSample previous = trace.samples.Count == 0 ? null : trace.samples[trace.samples.Count - 1];
            if (previous == null || previous.phase != state || Mathf.Abs(previous.alpha - alpha) >= .025f ||
                Mathf.Abs(previous.actual - actual) >= .01f || Mathf.Abs(previous.visible - visible) >= .025f)
                trace.samples.Add(sample);
            trace.previousActual = actual; trace.previousVisible = visible; trace.previousAlpha = alpha; trace.previousPhase = state;
        }
        catch (Exception error) { Violation(trace, "Telemetry observation failed: " + error.Message); }
    }
    static bool FiniteUnit(float value)
    { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= -.0001f && value <= 1.0001f; }
    void VerifyLoadingText()
    {
        const string expected = "개발 중 가장 오래 걸린 작업은 사운드 수집과 선정이었습니다. “하나만 더 들어볼까요”가 이렇게 무서운 말인 줄 몰랐습니다.";
        Check(string.Equals(ParallaxGameShell.LoadingTip, expected, StringComparison.Ordinal), "Loading Tip matches the exact requested sentence and curly quotes");
        Font font = Resources.Load<Font>("Preview/BodyFont");
        Check(font != null && expected.Where(c => !char.IsWhiteSpace(c)).All(font.HasCharacter), "Bundled body font includes every exact Tip glyph");
        Check(Mathf.Abs(ParallaxGameShell.LoadingFadeSeconds - .65f) < .0001f && Mathf.Abs(ParallaxGameShell.MinimumLoadingSeconds - 2.8f) < .0001f,
            "Configured fade/minimum opaque presentation durations are explicit");
        string[] pool = ParallaxGameShell.ContinueTipsSnapshot();
        Check(pool != null && pool.Length >= 2 && pool.All(t => !string.IsNullOrWhiteSpace(t)) && pool.Distinct(StringComparer.Ordinal).Count() == pool.Length,
            "Continue Tip pool has distinct nonempty choices and supports repeat exclusion");
        Check(ParallaxGameShell.SwordTipIndex == 2, "Sword anecdote retains its approved pool index");
        Check(string.Equals(pool[0], expected, StringComparison.Ordinal), "Pool index zero preserves the requested fixed new-game sound Tip");
        Check(pool.SelectMany(t => t).Where(c => !char.IsWhiteSpace(c)).All(font.HasCharacter), "Bundled body font includes every Continue Tip glyph");
        Check(pool.Length > 2 && pool[1] == "시작화면 주사위는 움직이지 않습니다. 대신 메뉴의 위치가 여러 번 움직였습니다." &&
            pool[2] == "검을 꽂는 시간은 1.5초지만, 마음에 드는 검을 찾는 데에는 1.5시간이 걸렸습니다.", "Approved dice and sword anecdotes use the exact final wording");
        foreach (bool unlocked in new[] { false, true })
        {
            int[] eligible = ParallaxGameShell.EligibleContinueTipIndices(unlocked);
            int[] expectedEligible = Enumerable.Range(0, pool.Length).Where(index => unlocked || index != ParallaxGameShell.SwordTipIndex).ToArray();
            Check(eligible.SequenceEqual(expectedEligible), "Sword Tip eligibility follows first encounter only; unlocked=" + unlocked);
            for (int previous = -1; previous <= pool.Length; previous++)
            {
                int[] allowed = eligible.Where(index => index != previous).ToArray();
                int[] mapped = Enumerable.Range(0, allowed.Length).Select(slot => ParallaxGameShell.SelectContinueTipIndex(previous, slot, unlocked)).ToArray();
                Check(mapped.SequenceEqual(allowed), "Gated Tip picker covers each eligible nonprevious choice; unlocked=" + unlocked + " previous=" + previous);
            }
        }
        string first = pool[0]; pool[0] = "__validator_mutates_snapshot_only__";
        Check(string.Equals(ParallaxGameShell.ContinueTipsSnapshot()[0], first, StringComparison.Ordinal), "Tip pool snapshots cannot mutate production choices");
        pool = ParallaxGameShell.ContinueTipsSnapshot();
        for (int previous = -1; previous <= pool.Length; previous++)
        {
            bool validPrevious = previous >= 0 && previous < pool.Length;
            int count = validPrevious ? pool.Length - 1 : pool.Length;
            int[] mapped = Enumerable.Range(0, count).Select(slot => ParallaxGameShell.SelectContinueTipIndex(previous, slot)).ToArray();
            int[] expectedIndices = Enumerable.Range(0, pool.Length).Where(index => !validPrevious || index != previous).ToArray();
            Check(mapped.SequenceEqual(expectedIndices), "Deterministic Tip slots cover each allowed choice exactly once; previous=" + previous);
        }
    }
    [Serializable] sealed class VerificationEnvelope { public string magic, payload, sha256; }
    static string PayloadHash(string payload)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
    }
    static GameProgressRecord ReadEnvelopeRecord(string path)
    {
        var envelope = JsonUtility.FromJson<VerificationEnvelope>(File.ReadAllText(path, Encoding.UTF8));
        if (envelope == null || envelope.sha256 != PayloadHash(envelope.payload)) throw new InvalidDataException("Validation record envelope is invalid: " + path);
        return JsonUtility.FromJson<GameProgressRecord>(envelope.payload);
    }
    void VerifyLegacySwordEncounterDefault()
    {
        string probe = Path.Combine(rootDirectory, "LegacyEncounterProbe-" + Guid.NewGuid().ToString("N"));
        Check(GameProgressRuntime.SetSaveDirectoryForValidation(probe), "Legacy payload probe uses its own isolated directory");
        try
        {
            foreach (string stage in new[] { "Tutorial", "Gallery", "Beach" })
            {
                var old = new GameProgressRecord { stage = stage, galleryRoom = stage == "Gallery" ? 2 : 0, checkpoint = stage == "Gallery" ? 1 : 0, milestones = stage == "Gallery" ? 3 : 0 };
                Check(GameProgress.TrySave(old), "Prepare checksum-valid legacy probe: " + stage);
                var envelope = JsonUtility.FromJson<VerificationEnvelope>(File.ReadAllText(GameProgress.SavePath, Encoding.UTF8));
                string original = envelope.payload;
                envelope.payload = original.Replace(",\"swordEncountered\":false", "").Replace("\"swordEncountered\":false,", "");
                Check(envelope.payload != original && !envelope.payload.Contains("swordEncountered"), "Legacy payload genuinely omits the new field: " + stage);
                envelope.sha256 = PayloadHash(envelope.payload);
                File.WriteAllText(GameProgress.SavePath, JsonUtility.ToJson(envelope, true), new UTF8Encoding(false));
                Check(GameProgress.TryLoad(out var restored) && !GameProgress.RecoveredBackup && !restored.swordEncountered,
                    "Old version-one save defaults to unencountered without progress inference: " + stage, GameProgress.LastError);
            }
            var initial = new GameProgressRecord(); var encountered = initial.Copy(); encountered.swordEncountered = true;
            Check(initial.CheckpointKey != encountered.CheckpointKey && encountered.Copy().swordEncountered, "Encounter state participates in autosave change detection and record cloning");
        }
        finally { Check(GameProgressRuntime.SetSaveDirectoryForValidation(isolatedDirectory), "Legacy probe restores the dedicated save directory"); }
    }
    IEnumerator VerifySwordEncounterAndContinue(GameProgressRecord galleryFixture)
    {
        var game = FindFirstObjectByType<GalleryGame>();
        Check(!game.CaptureProgress().swordEncountered && !ReadSaved().swordEncountered && !game.bottleBalance.Solved,
            "Nike-entry restore has not encountered the unsolved sword exhibition");
        game.PlacePlayer(game.bottleBalance.DemoPosition);
        double until = Time.realtimeSinceStartupAsDouble + 6;
        while (GameProgressRuntime.PeekSavedRecord()?.swordEncountered != true)
        {
            if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException("Actual first Dike entry did not autosave the encounter flag.");
            yield return null;
        }
        Check(game.bottleBalance.InRoom && !game.bottleBalance.Solved && (game.CaptureProgress().milestones & 4) == 0 && game.CaptureProgress().swordEncountered,
            "Actual first entry unlocks sword Tip while the puzzle is still incomplete");
        Check(ReadSaved().swordEncountered, "First exhibition encounter is written by production autosave");
        game.Respawn(); yield return null;
        Check(game.CaptureProgress().swordEncountered, "Existing death-respawn preserves the encounter flag");
        game.ResetZone(); yield return null;
        Check(game.CaptureProgress().swordEncountered, "Existing ResetZone preserves the encounter flag");
        var completedPrefix = galleryFixture.Copy(); completedPrefix.swordEncountered = true;
        game.RestoreProgress(completedPrefix); yield return null;
        Check(GameProgressRuntime.SaveCurrent() && ReadSaved().swordEncountered, "Restored prefix and explicit encounter remain saveable");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        for (int repeat = 0; repeat < 2; repeat++)
        {
            GameProgressRuntime.ContinueGame(); yield return WaitForStage("Gallery", true, true);
            VerifyGalleryCheckpoint();
            Check(ReadSaved().swordEncountered, "Continue retains the first-encounter save flag; repeat=" + repeat);
            if (repeat == 0) { GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu"); }
        }
    }
    IEnumerator VerifyCrossMapEncounterCarry()
    {
        var gallery = ReadSaved();
        Check(gallery.stage == "Gallery" && gallery.swordEncountered, "Cross-map fixture begins in the encountered normal run");
        Check(StageTravelMenu.LoadStage("Beach"), "Existing stage transport accepts normal-session Beach travel");
        double until = Time.realtimeSinceStartupAsDouble + 90;
        while (StageTravelMenu.Loading || GameProgressRuntime.CurrentStage != "Beach" || !GameProgressRuntime.GameplayActive)
        {
            if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException("Normal cross-map encounter test did not reach Beach.");
            yield return null;
        }
        yield return null; yield return null;
        Check(!GameProgressRuntime.TestSession && GameProgressRuntime.SaveCurrent(), "Normal cross-map destination uses the production save path", GameProgressRuntime.LastError);
        var beach = ReadSaved();
        Check(beach.stage == "Beach" && beach.swordEncountered, "Central runtime carries the encounter into a different map save");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Beach", true, true);
        Check(ReadSaved().swordEncountered, "Other-map Continue retains sword Tip eligibility");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        GameProgressRuntime.NewGame(); yield return WaitForStage("Tutorial");
        Check(!ReadSaved().swordEncountered, "New game clears the genuinely encountered central session history");
        GameProgressRuntime.ReturnToTitle(); yield return WaitForStage("MainMenu");
        Check(GameProgress.TrySave(gallery), "Restore the explicit Gallery fixture after cross-map verification");
        GameProgressRuntime.ContinueGame(); yield return WaitForStage("Gallery", true, true);
        VerifyGalleryCheckpoint();
    }
    IEnumerator VerifyLoadingFailureCleanup()
    {
        var shell = ParallaxGameShell.Instance;
        string primary = HashFile(GameProgress.SavePath), backup = HashFile(GameProgress.BackupPath);
        double previousLoadCall = StageTravelMenu.SceneLoadCalledAt;
        int sequence = shell.LoadingSequenceId;
        Check(GameProgressRuntime.RequestLoadFailureAfterFadeForValidation(), "Validation-only one-shot load failure is armed");
        GameProgressRuntime.NewGame();
        double until = Time.realtimeSinceStartupAsDouble + 20;
        while (GameProgressRuntime.Transitioning || shell.LoadingPresentationActive)
        {
            if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException("Injected loading failure did not clean up.");
            yield return null;
        }
        yield return null; ObserveLoading();
        Check(shell.LoadingSequenceId == sequence + 1 && shell.LastLoadingCancelled && shell.LoadingOpaqueAt > shell.LoadingStartedAt,
            "Injected failure traverses fade-out and reaches the opaque presentation state");
        Check(SceneManager.GetActiveScene().name == "MainMenu" && StageTravelMenu.SceneLoadCalledAt == previousLoadCall && !StageTravelMenu.Loading,
            "After-fade failure never invokes or changes the scene load");
        Check(shell.LoadingPhase == ParallaxGameShell.LoadingTransitionPhase.Idle && !shell.LoadingPresentationActive && shell.LoadingOverlayAlpha == 0 &&
            !GameProgressRuntime.Transitioning && !ParallaxGameShell.BlocksGameplay, "Failed load releases presentation/runtime/input locks");
        Check(GameProgressRuntime.LastError == "검증용 로딩 실패를 재현했습니다.", "Failure result remains available for player feedback");
        Check(HashFile(GameProgress.SavePath) == primary && HashFile(GameProgress.BackupPath) == backup, "Failed new-game handoff preserves the existing isolated save and backup");
        var trace = loadingTraces[shell.LoadingSequenceId];
        Check(trace.violations.Count == 0 && trace.fadeOutIntermediate && trace.opaqueFrameBeforeLoad,
            "Cancelled fade has continuous presentation states and no input/progress violations", string.Join("; ", trace.violations));
        verifiedLoading.Add(trace.id);
    }
    void VerifySuccessfulLoading(string target, bool expectedRandomTip, bool expectedSwordTipUnlocked)
    {
        ObserveLoading(); var shell = ParallaxGameShell.Instance;
        Check(shell.LoadingSequenceId > 0 && loadingTraces.TryGetValue(shell.LoadingSequenceId, out _), "A real loading sequence accompanies " + target);
        var trace = loadingTraces[shell.LoadingSequenceId]; if (!verifiedLoading.Add(trace.id)) return;
        string[] choices = ParallaxGameShell.ContinueTipsSnapshot();
        int[] eligible = ParallaxGameShell.EligibleContinueTipIndices(expectedSwordTipUnlocked);
        Check(trace.swordTipUnlocked == expectedSwordTipUnlocked && eligible.Contains(trace.tipIndex), "Loading snapshot applies the saved sword-encounter eligibility: " + target);
        Check(trace.randomizedTip == expectedRandomTip && trace.tipIndex >= 0 && trace.tipIndex < choices.Length &&
            string.Equals(trace.tip, choices[trace.tipIndex], StringComparison.Ordinal),
            "Loading uses the expected Continue/random or new-game/admin fixed Tip policy: " + target,
            "random=" + trace.randomizedTip + " index=" + trace.tipIndex + " previous=" + trace.previousTipIndex);
        Check(expectedRandomTip ? !eligible.Contains(trace.previousTipIndex) || trace.tipIndex != trace.previousTipIndex :
            trace.tipIndex == 0 && string.Equals(trace.tip, ParallaxGameShell.LoadingTip, StringComparison.Ordinal),
            "Tip selection excludes the preceding shown choice or retains the fixed sound Tip policy: " + target);
        Check(trace.target == target && !trace.cancelled && trace.violations.Count == 0,
            "Loading phase/input/async progress invariants hold: " + target, string.Join("; ", trace.violations));
        Check(trace.actualLoadCalledAt >= trace.opaqueAt && trace.opaqueAt > trace.startedAt && trace.opaqueFrameBeforeLoad,
            "Actual scene-load call follows fully opaque presentation state: " + target,
            "started=" + trace.startedAt + " opaque=" + trace.opaqueAt + " called=" + trace.actualLoadCalledAt);
        Check(trace.readyAt >= trace.sceneLoadedAt && trace.sceneLoadedAt >= trace.actualLoadCalledAt && trace.readyAt > 0,
            "Destination load/restoration completes before reveal readiness: " + target);
        Check(trace.firstFadeInAt + .025 >= trace.opaqueAt + ParallaxGameShell.MinimumLoadingSeconds &&
            trace.endedAt - trace.opaqueAt + .04 >= ParallaxGameShell.MinimumLoadingSeconds + ParallaxGameShell.LoadingFadeSeconds,
            "Opaque loading minimum and subsequent fade-in are respected: " + target);
        Check(trace.fadeOutIntermediate && trace.fadeInIntermediate && trace.destinationPauseObserved && trace.resumeGuardChecked && trace.previousActual >= .9999f && trace.previousVisible >= .9999f,
            "Fade states are gradual and monotonic bar reaches exactly complete: " + target);
        Check(shell.LoadingPhase == ParallaxGameShell.LoadingTransitionPhase.Idle && !shell.LoadingPresentationActive && shell.LoadingOverlayAlpha == 0 &&
            !GameProgressRuntime.Transitioning && !GameProgressRuntime.Paused, "Success releases loading locks only after full reveal: " + target);
    }
    IEnumerator VerifyActualSettings()
    {
        var shell = ParallaxGameShell.Instance;
        GameShellConfig defaults = GameShellConfig.Defaults();
        Check(defaults.masterVolume == 1 && defaults.musicVolume == 1 && defaults.effectsVolume == 1 && defaults.ambientVolume == 1,
            "All four audio defaults preserve the original game mix at unity gain");
        GameShellConfig before = shell.appliedConfig.DeepCopy();
        PlayerControls.Options previousOptions = PlayerControls.CurrentOptions;
        KeyCode[] previousBindings = Enumerable.Range(0, 11).Select(i => PlayerControls.KeyFor((PlayerControls.Action)i)).ToArray();
        string prefsBefore = ControlsPreferenceFingerprint();
        GameProgressRuntime.Pause(); shell.OpenSettings();
        var test = before.DeepCopy(); test.masterVolume = .8f; test.musicVolume = .35f;
        test.effectsVolume = .6f; test.ambientVolume = .25f; test.fieldOfView = 82;
        test.bindings[0] = KeyCode.UpArrow; shell.draftConfig = test.DeepCopy();
        try
        {
            GameShellSettingsRuntime.Apply(test, false);
            yield return null; yield return null;
            Check(GameShellSettingsRuntime.AudioRoutingReady, "Actual native mixer routing is installed");
            AudioMixer mixer = Resources.Load<AudioMixer>("GameShell/SettingsMixer");
            string[] parameters = { "MasterVolume", "MusicVolume", "SfxVolume", "AmbientVolume" };
            float[] levels = { .8f, .35f, .6f, .25f };
            for (int i = 0; i < parameters.Length; i++)
            { bool found = mixer.GetFloat(parameters[i], out float value); Check(found && Mathf.Abs(value - GameShellSettingsRuntime.VolumeDecibels(levels[i])) < .02f, "Actual exposed mixer gain: " + parameters[i]); }
            Check(PlayerControls.ConfiguredCamera != null && Mathf.Abs(PlayerControls.ConfiguredCamera.fieldOfView - 82) < .001f,
                "Live FOV reaches the actual player camera");
            Check(PlayerControls.KeyFor(PlayerControls.Action.Forward) == KeyCode.UpArrow, "Changed key reaches the actual input adapter");
            Check(GameShellSettingsRuntime.AudioCategory("museum_bgm") == "Music" && GameShellSettingsRuntime.AudioCategory("ambient_wind") == "Ambient" &&
                GameShellSettingsRuntime.AudioCategory("select_001") == "Sfx", "BGM/ambience/UI use their intended mixer categories");
            shell.CloseSettings(false);
            Check(shell.appliedConfig.bindings[0] == before.bindings[0] && shell.draftConfig.bindings[0] == before.bindings[0] &&
                Mathf.Abs(shell.appliedConfig.fieldOfView - before.fieldOfView) < .001f, "Cancel discards draft FOV/key edits");
            GameShellSettingsRuntime.Apply(shell.appliedConfig, false);
            yield return null;
            Check(PlayerControls.KeyFor(PlayerControls.Action.Forward) == before.bindings[0], "Explicit validation restore returns live input to applied keys");
            Check(ControlsPreferenceFingerprint() == prefsBefore, "Live settings test performs no PlayerPrefs writes");
        }
        finally
        {
            if (shell.settingsOpen) shell.CloseSettings(false);
            GameShellSettingsRuntime.Apply(before, false);
            PlayerControls.ApplyRuntimeOptions(previousOptions, false); PlayerControls.ApplyBindings(previousBindings);
            GameProgressRuntime.Resume();
        }
    }
    IEnumerator VerifyCompletedGalleryRestore()
    {
        var game = FindFirstObjectByType<GalleryGame>();
        ((IGameProgressStage)game).RestoreProgress(new GameProgressRecord { stage = "Gallery", galleryRoom = 3, checkpoint = 2, milestones = 7, swordEncountered = true });
        yield return null; StepGameplay(.05f); yield return null;
        Check(game.bottleBalance.Solved && game.bottleBalance.Reward.Unlocked && game.bottleBalance.Door.Opening >= .995f,
            "Completed Dike fixture restores the actual sword reward and open door");
        Check(game.bottleBalance.Sounds.Count("dike.door.open") == 0 && game.bottleBalance.Sounds.Count("dike.scale.motion") == 0,
            "Completed Dike restore does not replay opening or scale-motion cues");
        ((IGameProgressStage)game).RestoreProgress(new GameProgressRecord { stage = "Gallery", galleryRoom = 2, checkpoint = 1, milestones = 3, swordEncountered = true });
        yield return null; VerifyGalleryCheckpoint();
    }
    IEnumerator VerifyCompletedBeachRestore()
    {
        var game = FindFirstObjectByType<BeachGame>();
        ((IGameProgressStage)game).RestoreProgress(new GameProgressRecord { stage = "Beach", checkpoint = 3, milestones = 15 });
        GameProgressRuntime.Resume(); yield return null; StepGameplay(.05f); yield return null;
        var finale = game.counterweightFinale;
        Check(finale != null && finale.Ready && finale.LoadLatched && finale.LiftProgress >= .999f,
            "Completed Beach fixture restores the actual latched hoist");
        Physics.SyncTransforms();
        Check(finale.BridgeFloor != null && finale.BridgeFloor.enabled && finale.BridgeFloor.bounds.max.y > finale.WaterSurface + .005f,
            "Restored covered bridge has a real raised walking collider");
        Check(finale.RouteShadeContinuous(), "Restored walking corridor has actual ground, clearance and five-foot shade", finale.LastRouteFailure);
        var ending = game.Audio.Ending;
        Check(ending != null && new[] { "load_latch", "pulley", "water_rise", "bridge_stop" }.All(cue => ending.CountFor(cue) == 0),
            "Completed Beach restore does not replay hoist completion cues");
        Check(ending.Drips != null && ending.Drips.Wetness == 0 && ending.Drips.VisibleDropCount == 0,
            "Restored completed canopy begins dry without a duplicate emergence effect");
    }
    static void StepGameplay(float dt)
    {
        var gallery = FindFirstObjectByType<GalleryGame>(); if (gallery != null) { gallery.Tick(dt, Vector2.zero); return; }
        var beach = FindFirstObjectByType<BeachGame>(); if (beach != null) beach.Tick(dt, Vector2.zero);
    }
    static string ControlsPreferenceFingerprint()
    {
        const string prefix = "Parallax.Controls.";
        return string.Join("|", new[] { "Sensitivity", "FieldOfView", "InvertX", "InvertY" }.Select(key =>
            key + ":" + PlayerPrefs.HasKey(prefix + key) + ":" + (key.StartsWith("Invert", StringComparison.Ordinal) ?
            PlayerPrefs.GetInt(prefix + key, -777).ToString() : PlayerPrefs.GetFloat(prefix + key, -777).ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
    }
    static GameProgressRecord ReadSaved()
    { if (!GameProgress.TryLoad(out var saved)) throw new InvalidOperationException("Expected saved checkpoint: " + GameProgress.LastError); return saved; }
    static int PauseCount()
    { return (int)typeof(ParallaxGameShell).GetProperty("PauseCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ParallaxGameShell.Instance); }
    static void SetAdmin(bool value)
    { typeof(ParallaxGameShell).GetProperty("AdminMode").GetSetMethod(true).Invoke(ParallaxGameShell.Instance, new object[] { value }); }
    static Vector3 PlayerPosition()
    {
        var tutorial = FindFirstObjectByType<TutorialGame>(); if (tutorial != null) return tutorial.Controller.transform.position;
        var gallery = FindFirstObjectByType<GalleryGame>(); if (gallery != null) return gallery.controller.transform.position;
        var beach = FindFirstObjectByType<BeachGame>(); if (beach != null) return beach.controller.transform.position;
        return Vector3.zero;
    }
    static float StageElapsed()
    { var gallery = FindFirstObjectByType<GalleryGame>(); if (gallery != null) return gallery.elapsed; var beach = FindFirstObjectByType<BeachGame>(); return beach != null ? beach.elapsed : 0; }
    static string HashFile(string path)
    {
        if (!File.Exists(path)) return "<missing>";
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
}
