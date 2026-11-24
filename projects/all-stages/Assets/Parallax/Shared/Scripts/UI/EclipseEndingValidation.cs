using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ShadeLink;
using ShadeLink.Beach;
using ShadeLink.Eclipse;

// Explicit separate-process regression. Never uses the old -eclipse-test driver:
// that older driver captures images. Only -eclipse-ending-test enables its automated fixtures;
// this harness quits just its isolated validation process after real title return.
public sealed class EclipseEndingValidation : MonoBehaviour
{
    const string Flag = "-eclipse-ending-validate";
    const float Dt = 1f / 60;
    static string output, isolated;
    static Dictionary<string, string> productionBefore;
    static bool claimed;
    readonly List<CheckResult> checks = new List<CheckResult>();
    readonly List<string> errors = new List<string>();
    readonly List<Trace> trace = new List<Trace>();
    readonly HashSet<string> teamText = new HashSet<string>();
    bool naturalRoute, endingTitleRoute;
    int naturalSamples, endingTitleSamples;
    string failure, lastTrace, runPhase, publicCreditSha;
    [Serializable] public sealed class CheckResult { public bool passed; public string name, detail; }
    [Serializable] public sealed class Trace { public string scene, section, exitPhase; public bool blocked, transitioning, sceneLoading, shellLoading, overlay, endingReveal; public double time; }
    [Serializable] public sealed class Report
    {
        public bool passed, productionStorageUnchanged, renderedPixelsVerified;
        public int captureCount, processId, naturalRouteSamples, endingTitleRouteSamples;
        public string scope, failure, unity, graphicsDevice, finalScene, phase, publicCreditSha256, completionProfilePath, completionProfileSha256;
        public CheckResult[] checks; public string[] errors, observedTeamText; public Trace[] trace;
    }
    [Serializable] sealed class PptTeamSource
    { public string sourcePath, sourceSha256, sourceKind, hashMode, presentationSha256, presentationEntry, userApprovedChange; public int orderedSlide; public EclipseCreditEntry[] creditTeam; }
    [Serializable] sealed class CompletionExpectation
    { public int processId; public string profileSha256, checkpointSha256, publicCreditSha256; }
    static string Digest(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    const string Scope = "Isolated native D3D11 state/real PhysX regression; no screenshot, camera render/readback, OS input or audible listening. Beach flight is a stated terminal-time fixture, not a full puzzle completion. A separate slightly smaller/visible-rim source-Moon pickup and actual release are observed through protected credits and their return, then explicitly reset; the independent large-Moon route continues through the full ending. A separate maximum-size visual fixture uses declared geometry and production Credits.Begin/Tick only, without puzzle-success or anchor flag injection; it is not a completed gameplay route. Credits input uses the same public production tick with boolean inputs and deterministic dt, not synthesized keyboard events. Scene loads, actual CharacterController traversal, staged exit and natural title return are observed; only this validation process may quit.";
    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
    static string Hash(string path)
    {
        if (!File.Exists(path)) return "<absent>";
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (claimed || Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;
        claimed = true;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-test") >= 0)
            throw new ArgumentException("Do not combine capture-enabled -eclipse-test with the ending validator.");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") < 0)
            throw new ArgumentException("Ending validation requires explicit -eclipse-ending-test for isolated automation.");
        string directory = Arg(Flag);
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory)) throw new ArgumentException(Flag + " requires an absolute output directory.");
        output = Path.GetFullPath(directory); isolated = Path.Combine(output, "SaveData");
        productionBefore = new Dictionary<string, string>();
        foreach (string path in new[] { GameProgress.SavePath, GameProgress.BackupPath,
            Path.Combine(Application.persistentDataPath, "settings.json"), Path.Combine(Application.persistentDataPath, "settings.json.previous"),
            GameCompletionState.ProfilePath, GameCompletionState.BackupPath }) productionBefore[path] = Hash(path);
        if (string.Equals(Path.GetDirectoryName(GameProgress.SavePath), isolated, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Validation may not use the player's real save directory.");
        if (!GameProgressRuntime.SetSaveDirectoryForValidation(isolated)) throw new InvalidOperationException(GameProgressRuntime.LastError);
        Directory.CreateDirectory(output); AudioListener.volume = 0;
        var host = new GameObject("Eclipse ending · isolated native regression");
        DontDestroyOnLoad(host); host.AddComponent<EclipseEndingValidation>();
    }
    void Awake() { Application.logMessageReceived += Log; }
    void OnDestroy() { Application.logMessageReceived -= Log; }
    void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
    void Record(bool passed, string name, string detail = "")
    { checks.Add(new CheckResult { passed = passed, name = name, detail = detail }); Debug.Log("ECLIPSE_ENDING_" + (passed ? "PASS " : "FAIL ") + name + " " + detail); }
    void Check(bool passed, string name, string detail = "")
    { Record(passed, name, detail); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
    static object Read(object owner, string name)
    {
        if (owner == null) throw new InvalidOperationException("Missing owner for " + name);
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property != null) return property.GetValue(owner);
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field != null) return field.GetValue(owner);
        throw new MissingMemberException(owner.GetType().FullName, name);
    }
    static bool Bool(object owner, string name) => (bool)Read(owner, name);
    static float Number(object owner, string name) => Convert.ToSingle(Read(owner, name));
    static EclipseEndingCredits Credits(EclipseFinaleGame game) => game.Credits;
    static string Section(EclipseFinaleGame game) => game.Credits.Section.ToString();
    bool solarPresentationStable = true, readingMoonFramingStable = true, cinematicSunReached;
    float minimumPresentedSunY = float.PositiveInfinity, maximumPresentationRatioError, maximumReadingMoonAngle;
    float minimumCameraSurfaceClearance = float.PositiveInfinity, minimumMoonFloorClearance = float.PositiveInfinity, minimumSunSurfaceGap = float.PositiveInfinity;
    float acceptedReleaseCoverage, measuredMoonOuterRadius;
    int framedDiscSamples;
    void CreditTick(EclipseFinaleGame game, bool space, bool escape)
    {
        game.TickEndingSequence(Dt, space, escape);
        if (!game.Credits.Active) return;
        Vector3 eye = game.view.transform.position, sun = EclipseFinaleGame.SunPosition;
        float moonDistance = Vector3.Distance(eye, game.moon.position), sunDistance = Vector3.Distance(eye, sun);
        float sunAngle = Mathf.Asin(Mathf.Clamp01(EclipseFinaleGame.SunRadius / sunDistance));
        float moonAngle = Mathf.Asin(Mathf.Clamp01(game.MoonRadius * .97f / moonDistance));
        float ratio = moonAngle / sunAngle;
        float error = Mathf.Abs(ratio - game.Credits.PresentedMoonSunAngularRatio);
        maximumPresentationRatioError = Mathf.Max(maximumPresentationRatioError, error);
        minimumPresentedSunY = Mathf.Min(minimumPresentedSunY, sun.y);
        cinematicSunReached |= game.Credits.CameraLiftProgress > .999f && Vector3.Distance(sun, EclipseFinaleGame.CinematicSunPosition) < .001f;
        float cameraClearance = moonDistance - measuredMoonOuterRadius;
        float floorClearance = game.moon.position.y - measuredMoonOuterRadius;
        float solarGap = sunDistance - EclipseFinaleGame.SunRadius - moonDistance - measuredMoonOuterRadius;
        minimumCameraSurfaceClearance = Mathf.Min(minimumCameraSurfaceClearance, cameraClearance);
        minimumMoonFloorClearance = Mathf.Min(minimumMoonFloorClearance, floorClearance);
        minimumSunSurfaceGap = Mathf.Min(minimumSunSurfaceGap, solarGap);
        var light = game.world.GetComponentsInChildren<Light>(true).FirstOrDefault(item => item.type == LightType.Directional);
        solarPresentationStable &= Finite(ratio) && error < .0005f &&
            Vector3.Distance(sun, game.world.SunRenderer.transform.position) < .0001f &&
            game.world.SunSurfaceSuppressed && !game.world.SunRenderer.enabled &&
            light != null && Vector3.Dot(light.transform.forward, -sun.normalized) > .99999f && game.Safe &&
            game.MoonAlignmentConfirmed && game.ReleasedMoonAlignment.Complete &&
            Mathf.Abs(game.ReleasedMoonAlignment.Coverage - acceptedReleaseCoverage) < .000001f &&
            cameraClearance >= .149f && floorClearance >= .019f && solarGap >= .999f;
        if (game.Credits.Section == EndingCreditSection.Team || game.Credits.Section == EndingCreditSection.Assets || game.Credits.Section == EndingCreditSection.Thanks)
        {
            float actualAngle = Mathf.Asin(Mathf.Clamp01(measuredMoonOuterRadius / moonDistance)) * Mathf.Rad2Deg;
            maximumReadingMoonAngle = Mathf.Max(maximumReadingMoonAngle, actualAngle);
            framedDiscSamples++;
            readingMoonFramingStable &= actualAngle <= 11.502f && CompleteMoonDiscInViewport(game.view, game.moon.position, measuredMoonOuterRadius);
        }
    }
    static bool CompleteMoonDiscInViewport(Camera camera, Vector3 centre, float radius)
    {
        Vector3 eye = camera.transform.position, axis = centre - eye;
        float distance = axis.magnitude;
        if (!Finite(distance) || distance <= radius || radius <= 0) return false;
        axis /= distance;
        Vector3 right = Vector3.Cross(Vector3.up, axis).normalized;
        if (right.sqrMagnitude < .001f) right = Vector3.right;
        Vector3 up = Vector3.Cross(axis, right).normalized;
        // Exact apparent sphere limb: the tangent circle is nearer than its
        // object centre, with the reduced physical ring radius below.
        Vector3 limbCentre = centre - axis * (radius * radius / distance);
        float limbRadius = radius * Mathf.Sqrt(Mathf.Max(0, 1 - radius * radius / (distance * distance)));
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            Vector3 point = camera.WorldToViewportPoint(limbCentre + limbRadius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
            if (!Finite(point) || point.z <= camera.nearClipPlane || point.x < .005f || point.x > .995f || point.y < .005f || point.y > .995f) return false;
        }
        return true;
    }
    static void SetAutoProperty(object owner, string name, object value)
    {
        var field = owner.GetType().GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(owner.GetType().FullName, name);
        field.SetValue(owner, value);
    }
    void LateUpdate() { AudioListener.volume = 0; Observe(); }
    void Observe()
    {
        var game = FindFirstObjectByType<EclipseFinaleGame>();
        var creditsProperty = game == null ? null : game.GetType().GetProperty("Credits");
        object credits = creditsProperty == null ? null : creditsProperty.GetValue(game);
        string section = credits == null ? "<none>" : Read(credits, "Section").ToString();
        var item = new Trace { scene = SceneManager.GetActiveScene().name, section = section, time = Time.realtimeSinceStartupAsDouble,
            exitPhase = game == null || game.GetComponent<EclipseExitPresentation>() == null ? "<none>" : game.GetComponent<EclipseExitPresentation>().Phase.ToString(),
            endingReveal = ParallaxGameShell.Instance != null && ParallaxGameShell.Instance.EndingTitleRevealActive,
            blocked = ParallaxGameShell.BlocksGameplay, transitioning = GameProgressRuntime.Transitioning,
            sceneLoading = GameProgressRuntime.SceneLoading, shellLoading = GameProgressRuntime.ShellTransitioning, overlay = ParallaxGameShell.ShouldShowLoadingOverlay };
        string key = item.scene + item.section + item.exitPhase + item.endingReveal + item.blocked + item.transitioning + item.sceneLoading + item.shellLoading + item.overlay;
        if (key != lastTrace) { lastTrace = key; trace.Add(item); }
        if (endingTitleRoute)
        {
            endingTitleSamples++;
            if ((item.overlay || item.shellLoading) && !errors.Contains("Natural Eclipse/title ending displayed a manual loading overlay."))
                errors.Add("Natural Eclipse/title ending displayed a manual loading overlay.");
        }
        if (naturalRoute)
        {
            naturalSamples++;
            if ((item.overlay || item.shellLoading) && !errors.Contains("Natural Beach/Eclipse handoff displayed a manual loading overlay.")) errors.Add("Natural Beach/Eclipse handoff displayed a manual loading overlay.");
        }
    }
    IEnumerator Wait(Func<bool> predicate, float seconds, string label)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(label); Observe(); yield return null; }
        Observe();
    }
    IEnumerator WaitStage(string scene)
    {
        yield return Wait(() => SceneManager.GetActiveScene().name == scene && !GameProgressRuntime.SceneLoading && !GameProgressRuntime.ShellTransitioning &&
            (scene == "MainMenu" || GameProgressRuntime.GameplayActive), 60, "Stage initialization: " + scene);
        yield return null;
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        runPhase = Arg("-eclipse-ending-phase") ?? "exercise";
        var stack = new Stack<IEnumerator>();
        if (runPhase == "reload") stack.Push(ReloadCompletionSequence());
        else if (runPhase == "exercise") stack.Push(Sequence());
        else { failure = "Unsupported ending regression phase"; runPhase = "invalid"; }
        double deadline = Time.realtimeSinceStartupAsDouble + 600;
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Ending regression exceeded 600 seconds.");
                more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current;
            }
            catch (Exception error) { failure = error.ToString(); break; }
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        bool unchanged = productionBefore.All(item => Hash(item.Key) == item.Value);
        Record(unchanged, "Real player progress/settings and backups remain byte-identical");
        var report = new Report { passed = failure == null && errors.Count == 0 && unchanged && checks.All(item => item.passed), productionStorageUnchanged = unchanged,
            renderedPixelsVerified = false, captureCount = 0, processId = System.Diagnostics.Process.GetCurrentProcess().Id, naturalRouteSamples = naturalSamples, endingTitleRouteSamples = endingTitleSamples,
            phase = runPhase, publicCreditSha256 = publicCreditSha, completionProfilePath = GameCompletionState.ProfilePath,
            completionProfileSha256 = Hash(GameCompletionState.ProfilePath),
            scope = Scope, failure = failure, unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
            finalScene = SceneManager.GetActiveScene().name, checks = checks.ToArray(), errors = errors.ToArray(), trace = trace.ToArray(), observedTeamText = teamText.ToArray() };
        string reportName = runPhase == "exercise" ? "eclipse-ending-results.json" : "eclipse-ending-" + runPhase + "-results.json";
        File.WriteAllText(Path.Combine(output, reportName), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        Debug.Log("ECLIPSE_ENDING_VALIDATION " + (report.passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " " + failure);
        Application.Quit(report.passed ? 0 : 1);
    }
    IEnumerator Sequence()
    {
        yield return null; yield return null;
        Check(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, "Ending regression uses the actual D3D11 player", SystemInfo.graphicsDeviceType.ToString());
        Check(SceneManager.GetActiveScene().name == "MainMenu" && ParallaxGameShell.Instance != null, "Five-scene build starts in the actual MainMenu Shell");
        Check(!GameProgress.IsUnisolatedValidation && GameProgress.SavePath.StartsWith(isolated + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Progress storage is isolated before any game entry", GameProgress.SavePath);
        Check(!ParallaxGameShell.Administrator, "Ending regression starts with normal player authority");
        ValidatePublicCreditContent();
        ValidateInitiallyLockedMenu();
        ValidateSandboxCompletionIsolation();
        Check(SceneManager.sceneCountInBuildSettings == 5 && Application.CanStreamedLevelBeLoaded("Eclipse"), "The player includes exactly five intended stages including Eclipse");
        GameProgressRuntime.AdminTravel("Eclipse");
        Check(SceneManager.GetActiveScene().name == "MainMenu" && !GameProgressRuntime.SceneLoading, "Public Eclipse admin travel rejects a regular player");
        var originalSettings = ParallaxGameShell.Instance.appliedConfig.DeepCopy();
        GameShellSettingsRuntime.Apply(originalSettings.DeepCopy()); yield return null;
        Check(GameShellSettingsRuntime.AudioRoutingReady, "Ending regression installs the real nonpersistent settings mixer");
        Check(GameProgress.TrySave(new GameProgressRecord { stage = "Beach", checkpoint = 3, milestones = 15, swordEncountered = true }), "Seed an explicit completed-hoist Beach fixture before natural ending travel", GameProgress.LastError);
        GameProgressRuntime.ContinueGame(); yield return WaitStage("Beach");
        var beach = FindFirstObjectByType<BeachGame>(); var ship = beach == null ? null : beach.skyShip;
        Check(beach != null && ship != null, "Beach continuation reconstructs its actual sky ship");
        Check(!GameProgressRuntime.EnterEclipseFromBeach(), "Natural Eclipse entry rejects a nonflying Beach ship");
        ship.Initialize(beach); SetAutoProperty(ship, "Flying", true); SetAutoProperty(ship, "FlightTime", BeachSkyShip.FlightDuration - .04f);
        beach.controller.enabled = false; beach.active = true; beach.won = false;
        Check(!GameProgressRuntime.EnterEclipseFromBeach(), "Natural Eclipse entry rejects flight before its terminal time");
        int loadingId = ParallaxGameShell.Instance.LoadingSequenceId;
        naturalRoute = true;
        ship.Tick(.08f);
        Check(ship.EclipseTransitionRequested, "The actual terminal ship Tick requests the natural fifth scene", "Terminal-time fixture only; earlier sailing was not re-solved.");
        yield return WaitStage("Eclipse"); naturalRoute = false;
        var game = FindFirstObjectByType<EclipseFinaleGame>();
        Check(game != null && ((IGameProgressStage)game).ProgressReady, "Natural handoff creates a ready actual Eclipse stage");
        Check(ParallaxGameShell.Instance.LoadingSequenceId == loadingId && !ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.BlocksGameplay,
            "Natural arrival preserves the loading sequence and releases gameplay without a Shell overlay", "samples=" + naturalSamples);
        var saved = GameProgressRuntime.PeekSavedRecord();
        Check(saved != null && saved.stage == "Eclipse" && saved.checkpoint == 0 && saved.swordEncountered && !GameProgressRuntime.TestSession,
            "Natural Eclipse arrival saves its opening checkpoint and preserves normal-session encounter history");
        GameProgressRuntime.Pause();
        Check(game.paused && ParallaxGameShell.ShouldShowPauseMenu, "Eclipse puzzle uses the common pause menu without a transition loop");
        var settings = originalSettings.DeepCopy(); settings.fieldOfView = 82; settings.mouseSensitivity = 1.25f;
        GameShellSettingsRuntime.Apply(settings, false); GameProgressRuntime.Resume(); yield return null;
        Check(!game.paused && !ParallaxGameShell.BlocksGameplay && Mathf.Abs(game.view.fieldOfView - 82) < .01f,
            "Nonpersistent common settings and Resume affect the actual Eclipse camera");
        Check(GameProgressRuntime.SaveCurrent(), "Eclipse opening puzzle is manually saveable", GameProgressRuntime.LastError);
        GameProgressRuntime.ReturnToTitle(); yield return WaitStage("MainMenu");
        Check(GameProgressRuntime.HasSave && GameProgressRuntime.PeekSavedRecord().stage == "Eclipse", "The actual title Continue slot recognizes Eclipse");
        GameProgressRuntime.ContinueGame(); yield return WaitStage("Eclipse"); game = FindFirstObjectByType<EclipseFinaleGame>();
        game.automated = true;
        Check(game.phase == FinalePhase.Chamber && !game.totality && game.held == FinaleItem.None, "Eclipse Continue reconstructs the safe initial puzzle rather than mid-credits transforms");
        Check(Bool(game, "ValidationQuitSuppressed"), "The isolated ending regression uses its explicit automation flag");
        EclipseControlFeelValidation.Run(game, Check);
        EclipseAlignmentValidation.Run(game, Check);
        EclipsePickupContinuityValidation.RunMoon(game, Check);
        EclipseMoonFloorContactValidation.Run(game, Check);
        EclipseMovementAudioValidation.Run(game, Check);
        ValidateShelter(game);
        EclipseExitRouteValidation.RunOpening(game, Check);
        ValidateSelectedAssets(game);
        ValidateExitSurfaceFromSourcePixels(game);
        var music = game.GetComponent<EclipseEndingMusic>();
        Check(music != null && !music.Started, "The opening Eclipse puzzle does not start the approved ending music early");
        game.AimAt(game.exitSign.TransformPoint(new Vector3(0, .5f, 0))); game.Interact();
        Check(game.held == FinaleItem.None && !game.ExitUnlocked, "A real EXIT interaction before mandatory credits cannot grab or unlock the sign");
        yield return ValidatePuzzleAmbience(game);
        var presentationSettings = GameShellSettingsRuntime.Current.DeepCopy(); presentationSettings.fieldOfView = 70;
        GameShellSettingsRuntime.Apply(presentationSettings, false); game.Play();
        Check(Mathf.Abs(game.view.fieldOfView - 70) < .001f, "Cinematic readability is checked at the real default 70-degree FOV after separately exercising the common 82-degree setting");
        EclipseCinematicFramingValidation.Run(game, Check);
        yield return ValidateAnnularReleaseStartsCredits(game);
        yield return SolveMoon(game);
        yield return ValidateCredits(game);
        EclipseExitRouteValidation.RunCompletedRoute(game, Check);
        yield return TraverseExit(game);
        GameShellSettingsRuntime.Apply(originalSettings, false);
    }
    void ValidatePptTeamSource(EclipseCreditDocument document)
    {
        string path = Arg("-eclipse-team-role-source");
        Check(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && File.Exists(path),
            "Approved roles combine the inspected presentation with the user's later explicit corrections", path ?? "<missing>");
        var source = JsonUtility.FromJson<PptTeamSource>(File.ReadAllText(path));
        bool transcript = source != null && source.sourceKind == "approved-role-transcript";
        string sourceFile = source == null || string.IsNullOrWhiteSpace(source.sourcePath) ? null :
            Path.GetFullPath(Path.IsPathRooted(source.sourcePath) ? source.sourcePath : Path.Combine(Path.GetDirectoryName(path), source.sourcePath));
        string sourceText = transcript && File.Exists(sourceFile) ? File.ReadAllText(sourceFile).TrimStart('\uFEFF').Replace("\r\n", "\n") : "";
        string sourceHash = transcript ? Digest(Encoding.UTF8.GetBytes(sourceText)) : Hash(sourceFile);
        Check(source != null && source.orderedSlide == 8 && source.creditTeam != null && source.creditTeam.Length == 4 &&
            File.Exists(sourceFile) && sourceHash == source.sourceSha256 &&
            (transcript ? source.hashMode == "utf8-lf" && source.presentationEntry == "ppt/slides/slide8.xml" &&
                !string.IsNullOrWhiteSpace(source.presentationSha256) && source.presentationSha256.Length == 64 &&
                source.presentationSha256.All(Uri.IsHexDigit) : string.IsNullOrEmpty(source.sourceKind)),
            transcript ? "The checked-in approval transcript matches its UTF-8/LF hash; the private presentation is recorded separately as provenance" :
                "The role fixture refers to the unchanged inspected latest presentation and its actual eighth slide");
        Check(!string.IsNullOrWhiteSpace(source.userApprovedChange) &&
            source.userApprovedChange.Contains("엔딩") && source.userApprovedChange.Contains("안건우") &&
            source.userApprovedChange.Contains("이민섭"),
            "The role fixture explicitly records the user's later ending and shared title-planning correction");
        Check(document.team.Single(x => x.id == "team-1").role.Contains("엔딩 기획·구현") &&
            document.team.Single(x => x.id == "team-1").role.Contains("게임 시작화면 기획") &&
            document.team.Single(x => x.id == "team-2").role.Contains("게임 시작화면 기획") &&
            !document.team.Where(x => x.id != "team-1").Any(x => x.role.Contains("엔딩 기획·구현")),
            "Ending planning and implementation belong to leader Ahn; title planning belongs to Ahn and Lee");
        foreach (var expected in source.creditTeam)
            Check(document.team.Any(actual => actual.id == expected.id && actual.title == expected.title && actual.role == expected.role) &&
                (!transcript || sourceText.Contains(expected.title) && sourceText.Contains(expected.role.Replace("\r\n", "\n"))),
                "Packed team caption and complete role match the approved presentation plus user correction: " + expected.title, expected.role);
    }
    void ValidateInitiallyLockedMenu()
    {
        var shell = ParallaxGameShell.Instance;
        Check(GameCompletionState.ProfilePath.StartsWith(isolated + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !GameCompletionState.CreditsUnlocked && !File.Exists(GameCompletionState.ProfilePath),
            "A fresh isolated profile starts with credits locked and never reads the real player's completion file");
        Check(shell.MainMenuItemCount == 4 && Enumerable.Range(0, shell.MainMenuItemCount).All(i => shell.GetMainMenuLabel(i) != "크레딧") &&
            shell.GetMainMenuLabel(3) == "게임 나가기" && !shell.OpenCreditsViewer(),
            "Before completion the title keeps its four existing commands and cannot open the credits viewer");
    }
    void ValidateSandboxCompletionIsolation()
    {
        string original = Path.GetDirectoryName(GameProgress.SavePath);
        string probe = Path.Combine(output, "SandboxCompletionProbe");
        try
        {
            Check(GameProgressRuntime.SetSaveDirectoryForValidation(probe), "Admin completion storage fixture uses its own isolated profile directory");
            Check(!GameCompletionState.CreditsUnlocked, "An independent admin storage fixture starts locked");
            GameCompletionState.MarkEndingSeen(true);
            Check(GameCompletionState.CreditsUnlocked && !File.Exists(GameCompletionState.ProfilePath) && !File.Exists(GameCompletionState.BackupPath),
                "The production admin/sandbox completion branch unlocks only the session and creates no persistent profile",
                "Explicit storage-branch fixture; not an injected gameplay completion.");
        }
        finally { GameProgressRuntime.SetSaveDirectoryForValidation(original); }
        Check(!GameCompletionState.CreditsUnlocked && !File.Exists(GameCompletionState.ProfilePath),
            "Returning to the normal isolated profile clears the admin fixture's session unlock");
    }
    IEnumerator ReloadCompletionSequence()
    {
        yield return null; yield return null;
        Check(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SceneManager.GetActiveScene().name == "MainMenu",
            "Completion reload is a separate actual native title process");
        string path = Path.Combine(output, "completion-profile-expectation.json");
        Check(File.Exists(path), "The previous successful actual ending left a completion-reload expectation");
        var expected = JsonUtility.FromJson<CompletionExpectation>(File.ReadAllText(path));
        Check(expected != null && expected.processId != System.Diagnostics.Process.GetCurrentProcess().Id,
            "Persistent credits unlock is checked in a newly launched process rather than shared static memory");
        ValidatePublicCreditContent();
        Check(GameCompletionState.CreditsUnlocked && Hash(GameCompletionState.ProfilePath) == expected.profileSha256 &&
            Hash(GameProgress.SavePath) == expected.checkpointSha256 && publicCreditSha == expected.publicCreditSha256,
            "A new process restores the earned credits unlock and leaves both the checkpoint and packed roles unchanged");
        var shell = ParallaxGameShell.Instance;
        Check(shell.MainMenuItemCount == 5 && shell.GetMainMenuLabel(3) == "크레딧" && shell.GetMainMenuLabel(4) == "게임 나가기",
            "The earned credits entry and original Quit command remain available after relaunch");
        string original = Path.GetDirectoryName(GameProgress.SavePath);
        string backupProbe = Path.Combine(output, "CompletionBackupProbe");
        string invalidProbe = Path.Combine(output, "CompletionInvalidProbe");
        try
        {
            Directory.CreateDirectory(backupProbe);
            File.WriteAllText(Path.Combine(backupProbe, "ParallaxEndingSeen.json"), "{broken completion fixture");
            File.Copy(GameCompletionState.BackupPath, Path.Combine(backupProbe, "ParallaxEndingSeen.json.bak"), true);
            Check(GameProgressRuntime.SetSaveDirectoryForValidation(backupProbe), "Completion backup fixture is isolated from the normal earned profile");
            Check(GameCompletionState.CreditsUnlocked, "A damaged copied primary profile still unlocks through its valid completion backup");
            Directory.CreateDirectory(invalidProbe);
            File.WriteAllText(Path.Combine(invalidProbe, "ParallaxEndingSeen.json"), "{\"endingSeen\":true}");
            File.WriteAllText(Path.Combine(invalidProbe, "ParallaxEndingSeen.json.bak"), "{\"endingSeen\":true}");
            Check(GameProgressRuntime.SetSaveDirectoryForValidation(invalidProbe), "Missing-header completion fixture is separately isolated");
            Check(!GameCompletionState.CreditsUnlocked, "An endingSeen flag without the required profile magic/version cannot grant the unlock");
        }
        finally { GameProgressRuntime.SetSaveDirectoryForValidation(original); }
        Check(GameCompletionState.CreditsUnlocked && Hash(GameCompletionState.ProfilePath) == expected.profileSha256,
            "Backup and invalid-record probes preserve the actual earned completion profile");
    }
    void ValidatePublicCreditContent()
    {
        var asset = Resources.Load<TextAsset>("Eclipse/EndingCredits");
        var document = asset == null ? null : JsonUtility.FromJson<EclipseCreditDocument>(asset.text);
        Check(document != null && document.team != null && document.team.Length == 4 && document.assets != null && document.assets.Length > 0,
            "The native player loads the cleaned team and actual-use credit document");
        publicCreditSha = Digest(asset.bytes);
        ValidatePptTeamSource(document);
        string[] removed = { "확인 중", "기록 없음", "라이선스 대상", "대상 원음", "candidate02",
            "per local acquisition record", "museum record", "subscription tier", "source license is inferred", "not a third-party game asset pack", "license asserted", "license claimed", "per manifest", "not credited as", "Project generated artwork", "Project generated material" };
        var problems = document.assets.Where(entry => removed.Any(phrase =>
            ((entry.title ?? "") + "\n" + (entry.creator ?? "") + "\n" + (entry.license ?? "")).IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        Check(problems.Length == 0, "Player-facing credits contain no internal review, unconfirmed-author or license-classification commentary",
            string.Join(" | ", problems.Select(entry => entry.id + ": " + entry.title)));
        Check(document.assets.All(entry => !string.IsNullOrWhiteSpace(entry.usage) && !string.IsNullOrWhiteSpace(entry.title)),
            "Every cleaned public credit still identifies its actual use and work title");
        Check(document.assets.Where(entry => (entry.license ?? "").IndexOf("CC BY", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (entry.license ?? "").IndexOf("CC-BY", StringComparison.OrdinalIgnoreCase) >= 0).All(entry =>
                !string.IsNullOrWhiteSpace(entry.creator) && !string.IsNullOrWhiteSpace(entry.url)),
            "Documented CC-BY attributions retain their authors and source links after commentary removal");
        Check(document.assets.Any(entry => entry.title == EclipseMovementAudio.CreditTitle && entry.creator == "3조 Parallax" && entry.usage.Contains("일식")) &&
            !document.assets.Any(entry => entry.title == "Parallax soft movement" && entry.usage.Contains("일식")) &&
            document.assets.Any(entry => entry.title == "Aurora" && entry.usage.Contains("감사")),
            "The actual space-deck movement and approved Aurora closing edit are credited at their real use locations");
        var movementCredit = document.assets.Where(x => x.id == "sfx-eclipse-space-deck-movement").ToArray();
        Check(movementCredit.Length == 1 && movementCredit[0].usage.Contains("걷기 4종") &&
            movementCredit[0].usage.Contains("이륙") && movementCredit[0].usage.Contains("착지") &&
            document.assets.Count(x => x.title == "Aurora") == 1 &&
            document.assets.Where(x => x.id == "sfx-c701dbe1ed1162" || x.id == "sfx-b0056bdddebe56").Count(x => x.usage.Contains("EXIT")) == 2,
            "The six actual space movement cues and both original EXIT wind layers are credited once at their actual use");
        Check(document.team.Any(entry => entry.title == "김태양" && entry.role.StartsWith("UI 총괄\n", StringComparison.Ordinal) &&
            entry.role.Contains("튜토리얼 기획·구현") && entry.role.Contains("맵 QA 및 외부 플레이어 QA 수집") &&
            entry.role.Contains("맵 아이디어 제안") && !entry.role.Contains("진행 중") && !entry.role.Contains("수정중")),
            "Kim Taeyang's approved UI leadership is shown first with tutorial, QA and map contributions retained");
        Check(document.team.Any(entry => entry.title == "이하늘" && entry.role.Contains("사운드 조사 및 구현") &&
            entry.role.Contains("외부 플레이어 QA 수집") && entry.role.Contains("맵 아이디어 제안 및 자료조사")),
            "Lee Haneul's approved sound, external-player QA and map research contributions are explicit");
    }
    void ValidateSelectedAssets(EclipseFinaleGame game)
    {
        object world = game.world;
        foreach (string flag in new[] { "EnvironmentAssetsReady", "SunSurfaceReady", "ExitOriginalAssetReady", "ArrivalShipReady" })
            Record(Bool(world, flag), "Selected Eclipse asset is genuinely ready: " + flag, Read(world, "AssetStatus").ToString());
        var ship = Read(world, "LeftPirateShip") as Transform;
        Check(ship != null && ship.gameObject.activeInHierarchy && ship.position.x < game.body.transform.position.x,
            "The reused Beach pirate ship floats to the player's left");
        var renderers = ship.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        Check(renderers.Length > 0 && ship.GetComponentsInChildren<Collider>(true).Length == 0,
            "Left-side arrival ship has real visible meshes and no gameplay colliders");
        var shipBounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) shipBounds.Encapsulate(renderer.bounds);
        Check(shipBounds.max.x < -13.1f && shipBounds.min.y > .25f,
            "The entire reused ship is above the floor and outside the left edge of the 26m central deck", shipBounds.ToString());
        var originalShip = Resources.Load<GameObject>("Eclipse/Selected/ArrivalPirateShip");
        var originalMeshes = originalShip == null ? new Mesh[0] : originalShip.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null).Select(filter => filter.sharedMesh).ToArray();
        var actualMeshes = ship.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh != null).ToArray();
        var selectedSun = Resources.Load<Texture2D>("Eclipse/Selected/SolarSystemScope/SunSurface");
        var sunRenderer = game.world.SunRenderer;
        Check(selectedSun != null && sunRenderer != null && sunRenderer.sharedMaterial != null &&
            sunRenderer.sharedMaterial.mainTexture == selectedSun && sunRenderer.sharedMaterial.shader != null &&
            sunRenderer.sharedMaterial.shader.isSupported && game.world.SelectedMeshCount > 0,
            "The actual sun renderer uses the selected original texture and a supported shader alongside real environment meshes");
        var exitSource = game.exitSign.GetComponent<EclipseExitSignSource>();
        Check(Resources.Load<GameObject>(EclipseExitSignSource.ModelResource) != null &&
            Resources.Load<Texture2D>(EclipseExitSignSource.TextureResource) != null && exitSource != null && exitSource.Ready &&
            exitSource.OriginalVertices > 0 && exitSource.OriginalTriangles > 0 && exitSource.AdaptedTriangles > 0 &&
            exitSource.OpeningMeshVerified && exitSource.SourceVisual != null &&
            exitSource.SourceVisual.GetComponentsInChildren<Collider>(true).Length == 0,
            "The selected original EXIT source has real derivative meshes with the existing i opening and no replacement colliders",
            exitSource == null ? "<missing source adapter>" : exitSource.Status);
        var marker = ship.GetComponent<EclipseSelectedAssetMarker>();
        Check(originalShip != null && originalMeshes.Length > 0 && actualMeshes.Length > 0 && actualMeshes.All(filter => originalMeshes.Contains(filter.sharedMesh)) &&
            marker != null && !string.IsNullOrWhiteSpace(marker.sourceUrl) && !string.IsNullOrWhiteSpace(marker.license),
            "The left arrival ship reuses the prepared Beach-source meshes and keeps source/license metadata");
        var viewMatrix = Matrix4x4.TRS(game.view.transform.position, Quaternion.Euler(0, -90, 0), Vector3.one).inverse;
        // Unity camera view space reverses Z. This is a mathematical frustum only;
        // the real player camera pose is never moved for the visibility probe.
        viewMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1)) * viewMatrix;
        var planes = GeometryUtility.CalculateFrustumPlanes(game.view.projectionMatrix * viewMatrix);
        Check(renderers.Any(renderer => GeometryUtility.TestPlanesAABB(planes, renderer.bounds)),
            "The floating ship intersects the actual camera's left-looking frustum");
    }
    void ValidateShelter(EclipseFinaleGame game)
    { EclipseCanopyValidation.Run(game, Check); }

    struct ExitSurfaceTriangle
    {
        public Vector3 a, b, c;
        public Vector2 ua, ub, uc;
    }
    static bool HitExitTriangle(Vector3 origin, ExitSurfaceTriangle triangle, out float distance, out Vector2 uv)
    {
        Vector3 edge1 = triangle.b - triangle.a, edge2 = triangle.c - triangle.a;
        Vector3 p = Vector3.Cross(Vector3.forward, edge2);
        float determinant = Vector3.Dot(edge1, p);
        distance = 0; uv = Vector2.zero;
        if (Mathf.Abs(determinant) < .0000001f) return false;
        float inverse = 1 / determinant;
        Vector3 relative = origin - triangle.a;
        float u = Vector3.Dot(relative, p) * inverse;
        if (u < -.000001f || u > 1.000001f) return false;
        Vector3 q = Vector3.Cross(relative, edge1);
        float v = Vector3.Dot(Vector3.forward, q) * inverse;
        if (v < -.000001f || u + v > 1.000001f) return false;
        distance = Vector3.Dot(edge2, q) * inverse;
        if (distance < 0) return false;
        uv = triangle.ua * (1 - u - v) + triangle.ub * u + triangle.uc * v;
        return true;
    }
    void ValidateExitSurfaceFromSourcePixels(EclipseFinaleGame game)
    {
        string imagePath = Arg("-eclipse-exit-texture-source");
        Check(!string.IsNullOrWhiteSpace(imagePath) && Path.IsPathRooted(imagePath) && File.Exists(imagePath),
            "EXIT appearance regression receives the explicit original-image CPU fixture", imagePath ?? "<missing argument>");
        Check(Hash(imagePath) == "423069E5415F4E94C48550B279B3B94D3C0761F9ABD004669AA23AB23327F044",
            "The CPU fixture is the unchanged officially downloaded EXIT image");
        var source = game.exitSign.GetComponent<EclipseExitSignSource>();
        var imported = Resources.Load<Texture2D>(EclipseExitSignSource.TextureResource);
        Check(source != null && source.SourceMaterial != null && source.SourceMaterial.mainTexture == imported &&
            source.SurfaceUvVerified && source.SourceVisual != null && imported != null && imported.width == 700 && imported.height == 352 &&
            source.SourceVisual.GetComponentsInChildren<MeshRenderer>(true).All(renderer => renderer.enabled && renderer.sharedMaterial == source.SourceMaterial),
            "All actual EXIT derivative renderers use the original full-size texture and the repaired surface material");
        var triangles = new List<ExitSurfaceTriangle>();
        foreach (var filter in source.SourceVisual.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var vertices = mesh.vertices; var uv = mesh.uv; var indices = mesh.triangles;
            Check(uv.Length == vertices.Length, "An actual EXIT mesh supplies a UV for every adapted vertex", filter.name);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                triangles.Add(new ExitSurfaceTriangle {
                    a = game.exitSign.InverseTransformPoint(filter.transform.TransformPoint(vertices[a])),
                    b = game.exitSign.InverseTransformPoint(filter.transform.TransformPoint(vertices[b])),
                    c = game.exitSign.InverseTransformPoint(filter.transform.TransformPoint(vertices[c])),
                    ua = uv[a], ub = uv[b], uc = uv[c] });
            }
        }
        var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        int samples = 0, hits = 0, green = 0, white = 0;
        bool finiteUvs = true;
        try
        {
            Check(pixels.LoadImage(File.ReadAllBytes(imagePath), false), "The original PNG decodes for CPU data sampling without rendering or GPU readback");
            for (int iy = 0; iy < 48; iy++) for (int ix = 0; ix < 96; ix++)
            {
                float x = Mathf.Lerp(-1.695f, 1.695f, (ix + .5f) / 96);
                float y = Mathf.Lerp(.005f, EclipseFinaleWorld.SignHeight - .005f, (iy + .5f) / 48);
                if (Mathf.Abs(x - EclipseFinaleWorld.DoorX) < EclipseFinaleWorld.DoorWidth * .5f &&
                    y > EclipseFinaleWorld.DoorBottom && y < EclipseFinaleWorld.DoorBottom + EclipseFinaleWorld.DoorHeight) continue;
                samples++;
                Vector3 origin = new Vector3(x, y, -1);
                float nearest = float.PositiveInfinity; Vector2 mapped = Vector2.zero;
                foreach (var triangle in triangles)
                    if (HitExitTriangle(origin, triangle, out float distance, out Vector2 value) && distance < nearest)
                    { nearest = distance; mapped = value; }
                if (float.IsPositiveInfinity(nearest)) continue;
                hits++; finiteUvs &= Finite(mapped.x) && Finite(mapped.y);
                Color color = pixels.GetPixelBilinear(mapped.x, mapped.y);
                if (color.g > color.r * 1.7f && color.g > color.b * 1.4f && color.g > 60f / 255) green++;
                if (color.r > .90f && color.g > .90f && color.b > .90f) white++;
            }
        }
        finally { Destroy(pixels); }
        float greenFraction = hits == 0 ? 0 : (float)green / hits, whiteFraction = hits == 0 ? 1 : (float)white / hits;
        Check(finiteUvs && hits > samples * .95f && greenFraction > .50f && whiteFraction < .45f,
            "Actual front-hit barycentric mesh UVs map mostly to the green sign rather than the old white image border",
            "grid=" + samples + " meshHits=" + hits + " green=" + greenFraction + " white=" + whiteFraction +
            "; original PNG CPU samples and actual mesh UV data only; no rendered-pixel claim");
    }
    void AimMoonOffset(EclipseFinaleGame game, float degrees)
    {
        Vector3 ray = EclipseFinaleGame.SunPosition - game.view.transform.position;
        game.AimAt(game.view.transform.position + Quaternion.AngleAxis(degrees, Vector3.up) * ray);
    }
    static float Luminance(Color value) => value.r * .2126f + value.g * .7152f + value.b * .0722f;
    IEnumerator ValidateAnnularReleaseStartsCredits(EclipseFinaleGame game)
    {
        Vector3 originalMoon = game.moon.position, originalScale = game.moon.localScale;
        Quaternion originalRotation = game.moon.rotation;
        string saveBefore = Hash(GameProgress.SavePath);
        try
        {
            game.ResetGame(true); game.Play(); game.automated = true;
            game.Teleport(EclipsePickupContinuityValidation.SolvableMoonPickupFeet(game, .93f));
            for (int i = 0; i < 15; i++) game.Tick(Dt, Vector2.zero, false);
            Check(game.InShelter, "The rim-visible release fixture picks the original Moon from the widened actual shelter shadow");
            game.AimAt(game.moon.position); game.Interact();
            Check(game.held == FinaleItem.Moon, "The annular-success route grabs the unchanged source Moon through the actual interaction");
            game.AimAt(EclipseFinaleGame.SunPosition);
            for (int i = 0; i < 240; i++) { game.Tick(Dt, Vector2.zero, false); if (i % 30 == 0) yield return null; }
            var held = game.CurrentMoonAlignment;
            Check(held.Complete && held.Coverage >= MoonAlignmentRules.RequiredCoverage && held.Coverage < .94f &&
                held.RealRadiusRatio > .88f && held.RealRadiusRatio < .99f && !game.totality && !game.EndingSequenceActive,
                "A slightly smaller centered Moon with a visible solar rim is accepted before release while holding alone cannot complete",
                "actualCoverage=" + held.Coverage + " realSolarRadiusRatio=" + held.RealRadiusRatio + "; body/view fixture only, no Moon scale or completion injection");
            Check(game.Drop() && game.MoonAnchored && game.MoonAlignmentConfirmed && game.ReleasedMoonAlignment.Complete &&
                Mathf.Abs(game.ReleasedMoonAlignment.Coverage - held.Coverage) < .000001f,
                "Actual Drop accepts the annular Moon and preserves its measured partial-coverage snapshot without replacing it with a perfect-alignment sample");
            // Exercise the real regrab/cancel lifecycle before protected credits
            // start: totality suppression must never hide the unsolved Sun.
            for (int i = 0; i < 24; i++) game.Tick(Dt, Vector2.zero, false);
            Check(game.totality && game.world.SunSurfaceSuppressed && !game.world.SunRenderer.enabled,
                "An actually released complete Moon suppresses only the bright solar surface during totality");
            game.AimAt(game.moon.position); game.Interact(); game.UpdateEclipse(0);
            Check(game.held == FinaleItem.Moon && !game.totality && !game.world.SunSurfaceSuppressed && game.world.SunRenderer.enabled,
                "An actual pre-credit Moon regrab restores the bright Sun for the active perspective puzzle");
            game.Cancel();
            for (int i = 0; i < 24; i++) game.Tick(Dt, Vector2.zero, false);
            Check(game.held == FinaleItem.None && game.totality && game.world.SunSurfaceSuppressed && !game.world.SunRenderer.enabled &&
                Mathf.Abs(game.ReleasedMoonAlignment.Coverage - held.Coverage) < .000001f,
                "Cancel restores the accepted actual release and its totality presentation after the normal settling gate");
            for (int i = 0; i < 600 && !game.EndingSequenceActive; i++)
            { game.Tick(Dt, Vector2.zero, false); if (i % 15 == 0) yield return null; }
            Check(game.totality && game.EndingSequenceActive && game.Credits.Section == EndingCreditSection.Intro &&
                game.TotalityStableTime >= 1.8f && game.darkness >= .985f && game.ReleasedMoonAlignment.Coverage < .94f &&
                Mathf.Abs(game.ReleasedMoonAlignment.Coverage - held.Coverage) < .000001f,
                "The actual visible-rim release settles into full darkness and starts mandatory credits under the relaxed 70-percent solve policy",
                "savedCoverage=" + game.ReleasedMoonAlignment.Coverage + " stable=" + game.TotalityStableTime + " darkness=" + game.darkness);
            Vector3 acceptedPosition = game.Credits.LockedMoonPosition, acceptedScale = game.Credits.LockedMoonScale;
            Quaternion acceptedRotation = game.Credits.LockedMoonRotation;
            bool annularCinemaCoveredSun = false; int annularFrames = 0;
            while (game.Credits.Section != EndingCreditSection.Finished)
            {
                bool optional = game.Credits.Section == EndingCreditSection.Assets;
                game.TickEndingSequence(Dt, false, optional);
                if (game.Credits.Section == EndingCreditSection.Team)
                    annularCinemaCoveredSun |= game.Credits.PresentedMoonSunAngularRatio >= 1.049f && game.MoonPreviewCoverage > .99f;
                if (++annularFrames > 12000) throw new TimeoutException("Annular protected credits did not complete their return.");
                if (annularFrames % 60 == 0) yield return null;
            }
            Check(annularCinemaCoveredSun && game.Credits.TeamComplete && game.Credits.ThanksComplete && game.ExitUnlocked &&
                !game.Credits.ReturnSkyPoseApplied && Vector3.Distance(game.moon.position, acceptedPosition) < .0001f &&
                Quaternion.Angle(game.moon.rotation, acceptedRotation) < .001f && Vector3.Distance(game.moon.localScale, acceptedScale) < .0001f &&
                Mathf.Abs(game.ReleasedMoonAlignment.Coverage - held.Coverage) < .000001f &&
                game.MoonPreviewCoverage >= MoonAlignmentRules.RequiredCoverage,
                "The annular route completes protected credits, receives full cinematic coverage, then restores its original nonblocking Moon and measured visible-rim solve",
                "protectedTicks=" + annularFrames + " returnedCoverage=" + game.MoonPreviewCoverage);
            ValidateCompletedEclipseViewpoints(game, "Actual annular solve");
            Check(Hash(GameProgress.SavePath) == saveBefore && !GameCompletionState.CreditsUnlocked,
                "Starting credits through the annular path neither rewrites Continue nor unlocks completion before EXIT");
        }
        finally
        {
            game.ResetGame(true); game.Play(); game.automated = true;
            game.GetComponent<EclipseEndingAmbience>()?.TickAmbience(0);
            game.GetComponent<EclipseEndingMusic>()?.TickMusic(0);
        }
        yield return null;
        Check(game.Credits.Section == EndingCreditSection.Inactive && !game.MoonAlignmentConfirmed &&
            Vector3.Distance(game.moon.position, originalMoon) < .0001f && Vector3.Distance(game.moon.localScale, originalScale) < .0001f &&
            Quaternion.Angle(game.moon.rotation, originalRotation) < .001f,
            "The declared annular route resets to the exact unsolved original Moon before the independent complete ending route");
        Check(!game.world.SunSurfaceSuppressed && game.world.SunRenderer.enabled,
            "Reset restores the original bright Sun instead of retaining a stale totality-only render state");
    }
    IEnumerator SolveMoon(EclipseFinaleGame game)
    {
        game.Play(); game.automated = true;
        // Declared body position only: the original lunar scale/pose is never altered.
        // Select an actually reachable large pickup (well above the retired
        // 1.20 size gate). Only the body fixture changes, never the Moon.
        game.Teleport(EclipsePickupContinuityValidation.SolvableMoonPickupFeet(game, 1.75f));
        game.AimAt(game.moon.position); game.Interact();
        Check(game.held == FinaleItem.Moon && game.InShelter, "The unchanged initial Moon is picked from the wide real shadow through the production ray");
        float daylight = game.world.SunLightIntensity, dayAmbient = Luminance(game.world.AmbientColor), dayDeck = Luminance(game.world.DeckTint);
        float low = 0, high = 30, partialAngle = 15;
        for (int search = 0; search < 8; search++)
        {
            partialAngle = (low + high) * .5f; AimMoonOffset(game, partialAngle);
            for (int i = 0; i < 90; i++) game.Tick(Dt, Vector2.zero, false);
            if (game.MoonPreviewCoverage > .45f) low = partialAngle; else high = partialAngle;
            yield return null;
        }
        partialAngle = (low + high) * .5f; AimMoonOffset(game, partialAngle);
        for (int i = 0; i < 180; i++) { game.Tick(Dt, Vector2.zero, false); if (i % 30 == 0) yield return null; }
        float partialCoverage = game.MoonPreviewCoverage, partialDarkness = game.darkness, partialLight = game.world.SunLightIntensity;
        float partialAmbient = Luminance(game.world.AmbientColor), partialDeck = Luminance(game.world.DeckTint);
        Check(partialCoverage > .25f && partialCoverage < .70f && partialDarkness > .10f && partialDarkness < .9f &&
            partialLight < daylight && partialAmbient < dayAmbient && partialDeck < dayDeck && !game.totality && !game.EndingSequenceActive,
            "Actual held-moon partial coverage darkens the live sunlight, ambient and deck before puzzle completion",
            "coverage=" + partialCoverage + " darkness=" + partialDarkness + " sun=" + daylight + "→" + partialLight + " ambient=" + dayAmbient + "→" + partialAmbient);
        game.AimAt(EclipseFinaleGame.SunPosition);
        float beforeFull = game.darkness; bool gradual = true; float previous = game.darkness;
        for (int i = 0; i < 180; i++)
        {
            game.Tick(Dt, Vector2.zero, false);
            gradual &= game.darkness >= previous - .0001f && game.darkness - previous < .02f;
            previous = game.darkness;
            if (i % 30 == 0) yield return null;
        }
        Check(game.CurrentMoonAlignment.Coverage > .99f && game.CurrentMoonAlignment.RealRadiusRatio > 1.5f &&
            game.CurrentMoonAlignment.Complete && !game.totality && !game.EndingSequenceActive,
            "A real large-Moon pickup above the retired size limit can fully cover the Sun but cannot complete before Drop",
            "coverage=" + game.CurrentMoonAlignment.Coverage + " realSolarRadiusRatio=" + game.CurrentMoonAlignment.RealRadiusRatio);
        Check(gradual && game.darkness > beforeFull + .2f && game.world.SunLightIntensity < partialLight &&
            Luminance(game.world.AmbientColor) < partialAmbient && Luminance(game.world.DeckTint) < partialDeck &&
            game.world.SelectedLightGain < .2f && game.world.ReflectionGain < .02f,
            "Partial-to-full coverage progressively dims actual presentation values and settles into a dark credit floor",
            "darkness=" + game.darkness + " sun=" + game.world.SunLightIntensity + " deck=" + game.world.DeckTint + " selected=" + game.world.SelectedLightGain);
        Check(game.Drop() && game.MoonAnchored, "The actual Drop anchors the aligned moon without injecting completion flags");
        for (int i = 0; i < 600 && !game.EndingSequenceActive; i++) { game.Tick(Dt, Vector2.zero, false); if (i % 15 == 0) yield return null; }
        Check(game.totality && game.EndingSequenceActive && Credits(game) != null && game.TotalityStableTime >= 1.8f && game.darkness >= .985f,
            "Credits begin only after actual released-moon totality and the authored dark-floor settling interval",
            "stableSeconds=" + game.TotalityStableTime + " darkness=" + game.darkness);
        var corona = game.world.CoronaRenderer;
        Check(corona != null && corona.enabled && corona.sharedMaterial != null && corona.sharedMaterial.shader != null &&
            corona.sharedMaterial.shader.isSupported && game.world.CoronaTracksMoonRim && game.world.VisibleCoronaMargin > 0 &&
            Vector3.Distance(corona.transform.position, game.world.CoronaReferenceEye) > Vector3.Distance(game.moon.position, game.world.CoronaReferenceEye),
            "The actual solved moon leaves a positive angular corona rim on a supported plane behind it",
            "marginDegrees=" + game.world.VisibleCoronaMargin + " rimRadius=" + game.world.CoronaRimRadius + "; angular geometry, not pixel brightness");
    }
    void ObserveTeam(EclipseFinaleGame game)
    {
        // Public presentation evidence is preferred when supplied by Credits.
        var current = Credits(game).GetType().GetProperty("CurrentText");
        if (current != null && current.GetValue(Credits(game)) is string value && !string.IsNullOrEmpty(value)) teamText.Add(value);
        foreach (var text in game.GetComponentsInChildren<TextMesh>(true))
        {
            var renderer = text.GetComponent<Renderer>();
            if (text.gameObject.activeInHierarchy && text.color.a > .02f && renderer != null && renderer.enabled &&
                GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(game.view), renderer.bounds)) teamText.Add(text.text);
        }
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    void ValidateFloorRows(EclipseFinaleGame game, string phase)
    {
        var rows = game.GetComponentsInChildren<TextMesh>(true).Where(row => row.name.StartsWith("Floor credit row ", StringComparison.Ordinal) &&
            row.gameObject.activeInHierarchy && row.color.a > .08f && !string.IsNullOrWhiteSpace(row.text)).ToArray();
        Check(rows.Length > 0, phase + " has actual active floor text to measure");
        var bodyFont = Resources.Load<Font>("Preview/BodyFont");
        var titleFont = Resources.Load<Font>("Preview/TitleFont");
        var shader = Resources.Load<Shader>("Eclipse/FloorCreditsShader");
        var planes = GeometryUtility.CalculateFrustumPlanes(game.view);
        var detail = new StringBuilder();
        bool sane = true, floorPlane = true, atlas = true, inView = false, fullCardInReadingArea = false;
        float bestProjectedLineHeight = 0;
        foreach (var row in rows)
        {
            var renderer = row.GetComponent<MeshRenderer>();
            if (renderer == null) { sane = atlas = false; detail.Append(row.name + ": missing MeshRenderer; "); continue; }
            Bounds local = renderer.localBounds, world = renderer.bounds;
            float width = row.transform.TransformVector(Vector3.right * local.size.x).magnitude;
            float depth = row.transform.TransformVector(Vector3.up * local.size.y).magnitude;
            bool valid = Finite(local.center) && Finite(local.size) && Finite(world.center) && Finite(world.size) &&
                Finite(row.transform.lossyScale) && Finite(width) && Finite(depth) && local.size.x > .00001f &&
                local.size.y > .00001f && width >= .25f && width <= 8.5f && depth >= .10f && depth <= 8.5f &&
                world.size.y < .015f && world.size.x < 12.1f && world.size.z < 12.1f;
            sane &= valid;
            floorPlane &= Mathf.Abs(Vector3.Dot(row.transform.forward.normalized, Vector3.up)) > .999f &&
                row.transform.position.y >= .025f && row.transform.position.y < .10f;
            var material = renderer.sharedMaterial;
            atlas &= renderer.enabled && bodyFont != null && titleFont != null &&
                (row.font == bodyFont || row.font == titleFont) && material != null && shader != null &&
                material.shader == shader && shader.isSupported && material.HasProperty("_MainTex") &&
                material.mainTexture != null && row.font != null && row.font.material != null &&
                material.mainTexture == row.font.material.mainTexture;
            bool visible = GeometryUtility.TestPlanesAABB(planes, world); inView |= visible;
            var corners = new[] { new Vector3(local.min.x, local.min.y, local.center.z), new Vector3(local.max.x, local.min.y, local.center.z),
                new Vector3(local.min.x, local.max.y, local.center.z), new Vector3(local.max.x, local.max.y, local.center.z) }
                .Select(point => game.view.WorldToViewportPoint(row.transform.TransformPoint(point))).ToArray();
            float projectedLineHeight = 0;
            if (corners.All(point => Finite(point) && point.z > 0))
            {
                projectedLineHeight = (corners.Max(point => point.y) - corners.Min(point => point.y)) / row.text.Split('\n').Length;
                if (visible) bestProjectedLineHeight = Mathf.Max(bestProjectedLineHeight, projectedLineHeight);
                fullCardInReadingArea |= corners.All(point => point.x >= .025f && point.x <= .975f && point.y >= .035f && point.y <= .90f);
            }
            detail.Append(row.name).Append(" measured=").Append(width.ToString("F3")).Append("x").Append(depth.ToString("F3"))
                .Append(" cameraY=").Append(game.view.transform.position.y.ToString("F3"))
                .Append(" projectedLineHeight=").Append(projectedLineHeight.ToString("F5"))
                .Append(" approxPixels=").Append((projectedLineHeight * Screen.height).ToString("F2"))
                .Append(" inFrustum=").Append(visible).Append(" world=").Append(world.ToString("F4")).Append("; ");
        }
        Check(sane, phase + " actual local/world text bounds stay finite, positive and within the designed floor dimensions", detail.ToString());
        Check(floorPlane, phase + " actual glyph geometry stays on the fixed horizontal world floor", detail.ToString());
        Check(atlas, phase + " actual text uses the bundled fonts and the supported depth-tested font-atlas shader", detail.ToString());
        Check(inView && fullCardInReadingArea && bestProjectedLineHeight >= .014f,
            phase + " projects a complete credit card into the reading area with line height at least 1.4 percent of the viewport",
            detail + "; geometric screen projection only, not rendered-pixel or subjective readability approval");
    }
    IEnumerator ValidatePuzzleAmbience(EclipseFinaleGame game)
    {
        var ambience = game.GetComponent<EclipseEndingAmbience>();
        var original = Resources.Load<AudioClip>(EclipseEndingAmbience.ResourceKey);
        Check(ambience != null && original != null && ambience.Clip == original && ambience.AmbienceSource != null &&
            ambience.AmbienceSource.clip == original && original.channels == 1 && original.frequency == 48000 &&
            original.length > 18 && original.length < 19,
            "The puzzle uses the selected seamless low-machine resource with its derived mono 48k format",
            original == null ? "<missing ambience>" : original.name + " seconds=" + original.length + " channels=" + original.channels + " Hz=" + original.frequency);
        var source = ambience.AmbienceSource;
        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("GameShell/SettingsMixer");
        Check(source.loop && source.spatialBlend == 0 && GameShellSettingsRuntime.AudioCategory(original.name, source.name) == "Ambient" &&
            source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Ambient" && source.outputAudioMixerGroup.audioMixer == mixer &&
            ambience.LegacyToneMuted,
            "The actual puzzle loop replaces the legacy tone and routes through the common Ambient group");
        for (int i = 0; i < 14; i++) ambience.TickAmbience(.1f);
        Check(ambience.Started && !ambience.Paused && Mathf.Abs(ambience.FadeGain - 1) < .001f &&
            Mathf.Abs(source.volume - EclipseEndingAmbience.BaseGain) < .001f && !game.GetComponent<EclipseEndingMusic>().Started,
            "Production ambience ticks reach the authored puzzle gain while ending music remains stopped",
            "gain=" + ambience.FadeGain + " source=" + source.volume + "; looping state only, no audible seam claim");
        var previous = GameShellSettingsRuntime.Current.DeepCopy();
        var muted = previous.DeepCopy(); muted.ambientVolume = 0; GameShellSettingsRuntime.Apply(muted, false); yield return null;
        float db;
        Check(mixer != null && mixer.GetFloat("AmbientVolume", out db) && Mathf.Abs(db + 80) < .01f,
            "Ambient volume zero reaches the actual mixer parameter");
        var full = previous.DeepCopy(); full.ambientVolume = 1; GameShellSettingsRuntime.Apply(full, false); yield return null;
        Check(mixer.GetFloat("AmbientVolume", out db) && Mathf.Abs(db) < .01f,
            "Ambient volume one restores its actual mixer parameter to zero decibels");
        GameShellSettingsRuntime.Apply(previous, false); yield return null;
        GameProgressRuntime.Pause(); yield return null;
        Check(game.paused && ambience.Paused, "The actual puzzle ambience observes the common Pause state");
        float pausedGain = ambience.FadeGain; ambience.TickAmbience(.1f);
        Check(Mathf.Abs(pausedGain - ambience.FadeGain) < .0001f, "Pause holds the ambience fade clock");
        GameProgressRuntime.Resume(); yield return null;
        Check(!game.paused && !ambience.Paused && ambience.Started, "Resume restores the same puzzle loop without starting ending music");
    }
    IEnumerator ValidateMusicStarted(EclipseFinaleGame game)
    {
        var music = game.GetComponent<EclipseEndingMusic>();
        var ambience = game.GetComponent<EclipseEndingAmbience>();
        Check(ambience != null && ambience.Started && !ambience.ReadyForCreditsMusic && music != null && !music.Started,
            "Actual early-credit Update fades the puzzle loop first and keeps the ending music gate closed");
        for (int i = 0; i < 27; i++) ambience.TickAmbience(.1f);
        Check(ambience.ReadyForCreditsMusic && ambience.FadeGain < .0001f && ambience.AmbienceSource.volume < .0001f,
            "The actual two-and-a-half-second ambience fade reaches silence before music is permitted");
        music.TickMusic(.1f);
        var original = Resources.Load<AudioClip>(EclipseEndingMusic.ResourceKey);
        Check(music != null && music.Started && !music.Paused && original != null && music.Clip == original &&
            music.MusicSource != null && music.MusicSource.clip == original &&
            original.loadType == AudioClipLoadType.Streaming && original.channels == 2 && original.length > 400,
            "Actual credits start the identical approved streaming stereo music resource",
            original == null ? "<missing clip>" : original.name + " length=" + original.length + " channels=" + original.channels + " load=" + original.loadType);
        var source = music.MusicSource;
        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("GameShell/SettingsMixer");
        Check(source.spatialBlend == 0 && GameShellSettingsRuntime.AudioCategory(original.name, source.name) == "Music" &&
            source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Music" && source.outputAudioMixerGroup.audioMixer == mixer &&
            music.IncomingSource != null && music.IncomingSource.outputAudioMixerGroup == source.outputAudioMixerGroup,
            "Both actual Aurora voices route immediately through the shared Music group");
        Check(music.FadeGain > 0 && music.FadeGain < .4f, "The music production tick begins a gradual three-second fade after the ambience gate opens",
            "gain=" + music.FadeGain + " sourceVolume=" + source.volume + "; -nosound, no audible-listening claim");
        for (int i = 0; i < 31; i++) music.TickMusic(.1f);
        Check(Mathf.Abs(music.FadeGain - 1) < .001f && Mathf.Abs(source.volume - EclipseEndingMusic.BaseGain) < .001f,
            "Production music ticks complete the authored fade at the original voice gain", "gain=" + music.FadeGain + " volume=" + source.volume);
        var previous = GameShellSettingsRuntime.Current.DeepCopy();
        var muted = previous.DeepCopy(); muted.musicVolume = 0; GameShellSettingsRuntime.Apply(muted, false); yield return null;
        float db;
        Check(mixer != null && mixer.GetFloat("MusicVolume", out db) && Mathf.Abs(db + 80) < .01f && Mathf.Abs(source.volume - EclipseEndingMusic.BaseGain) < .001f,
            "Music volume zero reaches the real mixer without destroying the authored source envelope");
        var full = previous.DeepCopy(); full.musicVolume = 1; GameShellSettingsRuntime.Apply(full, false); yield return null;
        Check(mixer.GetFloat("MusicVolume", out db) && Mathf.Abs(db) < .01f,
            "Music volume one restores the actual Music mixer parameter to zero decibels");
        GameShellSettingsRuntime.Apply(previous, false); yield return null;
    }
    [Serializable] sealed class FloorSpacingEvidence
    {
        public int samples, adjacentPairs, maximumLines;
        public float minimumGap = 10000;
        public bool passed = true;
        public string firstOverlap;
    }
    void SampleFloorSpacing(EclipseFinaleGame game, FloorSpacingEvidence evidence)
    {
        var rows = game.GetComponentsInChildren<TextMesh>(true).Where(row => row.name.StartsWith("Floor credit row ", StringComparison.Ordinal) &&
            row.gameObject.activeInHierarchy && row.color.a > .08f && !string.IsNullOrWhiteSpace(row.text) &&
            row.GetComponent<Renderer>() != null && row.GetComponent<Renderer>().enabled)
            .OrderBy(row => row.GetComponent<Renderer>().bounds.center.z).ToArray();
        evidence.samples++;
        foreach (var row in rows) evidence.maximumLines = Mathf.Max(evidence.maximumLines, row.text.Split('\n').Length);
        for (int i = 1; i < rows.Length; i++)
        {
            var near = rows[i - 1].GetComponent<Renderer>().bounds;
            var far = rows[i].GetComponent<Renderer>().bounds;
            float gap = far.min.z - near.max.z;
            evidence.adjacentPairs++; evidence.minimumGap = Mathf.Min(evidence.minimumGap, gap);
            if (!Finite(gap) || gap < -.001f)
            {
                evidence.passed = false;
                if (evidence.firstOverlap == null)
                    evidence.firstOverlap = "section=" + game.Credits.Section + " elapsed=" + game.Credits.SectionElapsed +
                        " gap=" + gap + " near=" + near.ToString("F4") + " far=" + far.ToString("F4") +
                        " textA=" + rows[i - 1].text + " textB=" + rows[i].text;
            }
        }
    }
    IEnumerator ValidateOptionalFloorSpacing(EclipseFinaleGame game)
    {
        var evidence = new FloorSpacingEvidence();
        float target = Mathf.Max(game.Credits.SectionElapsed, game.Credits.AssetsDuration - .5f);
        int guard = 0;
        // Advance the actual optional cards with the approved held-Space input.
        // Leave the final half second intact so the independent Esc test still
        // exercises the real skip path rather than an automatic phase change.
        while (game.Credits.Section == EndingCreditSection.Assets && game.Credits.SectionElapsed < target)
        {
            CreditTick(game, true, false);
            if (guard % 6 == 0) SampleFloorSpacing(game, evidence);
            if (++guard > 60000) throw new TimeoutException("Optional asset spacing sampling exceeded the bounded production ticks.");
            if (guard % 60 == 0) yield return null;
        }
        Check(game.Credits.Section == EndingCreditSection.Assets && evidence.samples > 0 && evidence.adjacentPairs > 0 && evidence.passed,
            "At supported 120-percent text size, actual consecutive optional credit bounds do not overlap on world-floor Z",
            JsonUtility.ToJson(evidence));
    }
    IEnumerator ValidateCredits(EclipseFinaleGame game)
    {
        Vector3 camera = game.Credits.LockedCameraPosition, moon = game.Credits.LockedMoonPosition, scale = game.Credits.LockedMoonScale;
        acceptedReleaseCoverage = game.ReleasedMoonAlignment.Coverage;
        measuredMoonOuterRadius = 0;
        foreach (var filter in game.moon.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null)
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    measuredMoonOuterRadius = Mathf.Max(measuredMoonOuterRadius,
                        Vector3.Distance(game.moon.position, filter.transform.TransformPoint(vertex)));
        Check(Finite(measuredMoonOuterRadius) && measuredMoonOuterRadius >= game.MoonRadius * .95f &&
            Mathf.Abs(measuredMoonOuterRadius - game.Credits.LockedMoonOuterRadius) < .001f,
            "Cinematic safety uses the actual unchanged lunar mesh's outer vertex radius rather than only its smaller gameplay silhouette",
            "outerRadius=" + measuredMoonOuterRadius + " collisionRadius=" + game.MoonRadius);
        solarPresentationStable = readingMoonFramingStable = true; cinematicSunReached = false;
        maximumPresentationRatioError = maximumReadingMoonAngle = 0; framedDiscSamples = 0;
        minimumPresentedSunY = minimumCameraSurfaceClearance = minimumMoonFloorClearance = minimumSunSurfaceGap = float.PositiveInfinity;
        Vector3 originalSun = game.Credits.LockedSunPosition;
        Check(Vector3.Distance(originalSun, EclipseFinaleGame.PuzzleSunPosition) < .0001f, "Credits lock the actual low puzzle Sun before the safe reading composition");
        Quaternion moonRotation = game.Credits.LockedMoonRotation; float fov = game.Credits.LockedFieldOfView;
        var floorRows = game.GetComponentsInChildren<TextMesh>(true).Where(row => row.name.StartsWith("Floor credit row ", StringComparison.Ordinal)).ToArray();
        var floorShader = Resources.Load<Shader>("Eclipse/FloorCreditsShader");
        Check(floorRows.Length > 0 && floorShader != null && floorShader.isSupported && floorRows.All(row =>
            Mathf.Abs(Vector3.Dot(row.transform.forward, Vector3.up)) > .999f),
            "Actual credits use supported world-floor text geometry rather than screen-space overlay text");
        Check(Resources.Load<Font>("Preview/BodyFont") != null && Resources.Load<Font>("Preview/TitleFont") != null,
            "The actual player bundles both Korean body and title font resources without requiring OS-font fallback");
        var largeText = GameShellSettingsRuntime.Current.DeepCopy(); largeText.textSize = 120;
        GameShellSettingsRuntime.Apply(largeText, false);
        Check(Mathf.Abs(GameShellSettingsRuntime.TextScale - 1.2f) < .001f, "Floor-credit geometry uses the real supported large-text setting");
        var teamSpacing = new FloorSpacingEvidence();
        bool introGeometryChecked = false, teamGeometryChecked = false, thanksGeometryChecked = false;
        bool normalTeamRate = true, protectedIntroTeam = true, bodyStable = true, fovStable = true, eclipseStable = true, raisedPoseStable = true, movementGuardChecked = false;
        Vector3 fixedFeet = game.body.transform.position; float maximumObservedLift = 0;
        float realUpdateBefore = Number(Credits(game), "Elapsed");
        game.automated = false;
        yield return new WaitForSecondsRealtime(.15f);
        game.automated = true;
        Check(!game.paused && !game.InputEnabled && Number(Credits(game), "Elapsed") > realUpdateBefore + .04f && !ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.ShouldShowPauseMenu,
            "Actual production Update advances mandatory credits across the Shell transition guards");
        yield return ValidateMusicStarted(game);
        int guard = 0;
        while (Section(game) == "Intro" || Section(game) == "Team")
        {
            string section = Section(game);
            if (section == "Team" && !movementGuardChecked)
            {
                Vector3 feet = game.body.transform.position;
                game.Tick(Dt, Vector2.one, true);
                Check(Vector3.Distance(feet, game.body.transform.position) < .00001f && game.held == FinaleItem.None,
                    "Protected credits ignore movement/jump without shifting the real player");
                movementGuardChecked = true;
            }
            float before = Number(Credits(game), "SectionElapsed");
            CreditTick(game, true, true); ObserveTeam(game);
            if (Section(game) == "Team" && guard % 6 == 0) SampleFloorSpacing(game, teamSpacing);
            if (!introGeometryChecked && Section(game) == "Intro" && Credits(game).SectionElapsed >= 5.6f)
            { ValidateFloorRows(game, "Intro"); introGeometryChecked = true; }
            if (!teamGeometryChecked && Section(game) == "Team" && Credits(game).SectionElapsed >= 3f)
            { ValidateFloorRows(game, "Team"); teamGeometryChecked = true; }
            if (Section(game) == section)
            {
                float expectedSpeed = section == "Team" ? EclipseEndingCredits.NormalRollSpeed : 1;
                normalTeamRate &= Mathf.Abs(Credits(game).AppliedSpeed - expectedSpeed) < .0001f &&
                    Mathf.Abs(Number(Credits(game), "SectionElapsed") - before - Dt * expectedSpeed) < .001f;
            }
            protectedIntroTeam &= Section(game) != "Thanks" && Section(game) != "Finished";
            bodyStable &= Vector3.Distance(fixedFeet, game.body.transform.position) < .0001f;
            fovStable &= Mathf.Abs(fov - game.view.fieldOfView) < .001f;
            maximumObservedLift = Mathf.Max(maximumObservedLift, game.view.transform.position.y - camera.y);
            if (Section(game) == "Team") raisedPoseStable &= Vector3.Distance(game.view.transform.position, Credits(game).RaisedCameraPosition) < .001f && Credits(game).CameraLiftProgress > .999f;
            Vector3 eye = game.view.transform.position;
            float moonDistance = Vector3.Distance(eye, game.moon.position), haloDistance = Vector3.Distance(eye, game.world.CoronaRenderer.transform.position);
            float moonAngle = Mathf.Asin(Mathf.Clamp01(game.MoonRadius / Mathf.Max(.001f, moonDistance)));
            float haloAngle = Mathf.Atan(game.world.CoronaRimRadius / Mathf.Max(.001f, haloDistance));
            eclipseStable &= game.MoonAlignmentConfirmed && game.ReleasedMoonAlignment.Complete && Quaternion.Angle(moonRotation, game.moon.rotation) < .001f && Vector3.Distance(scale, game.moon.localScale) < .0001f &&
                Vector3.Distance(eye, game.world.CoronaReferenceEye) < .001f && game.world.CoronaTracksMoonRim && haloDistance > moonDistance && haloAngle > moonAngle;
            if (++guard > 30000) throw new TimeoutException("Protected team credits did not reach Assets.");
            if (guard % 30 == 0) yield return null;
        }
        Check(normalTeamRate && protectedIntroTeam && Bool(Credits(game), "TeamComplete") && Section(game) == "Assets",
            "Intro retains its original speed and mandatory team rolls at 1.15x; carried Space/Esc cannot accelerate or skip either");
        Check(teamSpacing.samples > 0 && teamSpacing.adjacentPairs > 0 && teamSpacing.passed,
            "At supported 120-percent text size, actual consecutive mandatory team cards have no world-floor Z overlap",
            JsonUtility.ToJson(teamSpacing));
        var credits = (EclipseEndingCredits)Credits(game);
        var documentAsset = Resources.Load<TextAsset>("Eclipse/EndingCredits");
        var document = documentAsset == null ? null : JsonUtility.FromJson<EclipseCreditDocument>(documentAsset.text);
        Check(document != null && document.team != null && document.team.Length == 4 && credits.PresentedTeamLines.Length == 4,
            "Four actual team cards were presented before optional assets");
        Func<string, string> compact = value => System.Text.RegularExpressions.Regex.Replace(value ?? "", @"\s+", "");
        foreach (var member in document.team)
            Check(credits.PresentedTeamLines.Any(line => compact(line).Contains(compact(member.title)) && compact(line).Contains(compact(member.role ?? member.usage))),
                "Mandatory team name and entire assigned role were presented normally: " + member.title, string.Join(" | ", credits.PresentedTeamLines));
        Check(document.assets != null && document.assets.Length == credits.AssetCount && credits.AssetCount > 0 && credits.HasUsageMetadata,
            "The real optional credits document contains usage, work and creator metadata for every card", "cards=" + credits.AssetCount);
        Check(bodyStable && fovStable && maximumObservedLift >= 3f && raisedPoseStable,
            "Credits raise the actual camera smoothly while retaining the player body and configured FOV, then hold the raised reading viewpoint",
            "maximumLift=" + maximumObservedLift + " target=" + credits.RaisedCameraPosition + " pitch=" + credits.CreditPitchDegrees);
        Check(eclipseStable, "The lunar appearance and actual-camera eclipse/corona alignment remain stable throughout the crane movement");
        Check(solarPresentationStable && cinematicSunReached && maximumPresentationRatioError < .0005f,
            "Cinematic reframing preserves the actual accepted release snapshot while keeping the unchanged Moon outside the camera, above the floor and in front of the actual Sun",
            "minimumCameraClearance=" + minimumCameraSurfaceClearance + " minimumFloorClearance=" + minimumMoonFloorClearance +
            " minimumSunSurfaceGap=" + minimumSunSurfaceGap + " presentedRatioError=" + maximumPresentationRatioError +
            "; locked ratio may change for safe framing; actual geometry only, not pixel-render approval");
        Check(readingMoonFramingStable && framedDiscSamples > 0 && maximumReadingMoonAngle <= 11.502f,
            "After the initial crane all actual tangent-limb samples of the large Moon stay within the reading viewport and the 11.5-degree angular budget",
            "readingSamples=" + framedDiscSamples + " maximumMoonAngleDegrees=" + maximumReadingMoonAngle);
        GameProgressRuntime.Pause();
        Check(!game.paused && !ParallaxGameShell.ShouldShowPauseMenu, "Common Pause cannot swallow protected credits input");
        string slotBefore = Hash(GameProgress.SavePath);
        Check(!GameProgressRuntime.SaveCurrent() && Hash(GameProgress.SavePath) == slotBefore, "Credits prevent unsafe mid-sequence progress writes");
        float normalAssetBefore = credits.SectionElapsed; CreditTick(game, false, false);
        Check(Section(game) == "Assets" && Mathf.Abs(credits.AppliedSpeed - 1.15f) < .0001f &&
            Mathf.Abs(credits.SectionElapsed - normalAssetBefore - Dt * 1.15f) < .001f,
            "The optional asset roll also uses the approved 15-percent faster normal speed");
        float assetElapsed = Number(Credits(game), "SectionElapsed"); CreditTick(game, true, false);
        Check(Section(game) == "Assets" && Number(Credits(game), "SectionElapsed") > assetElapsed + Dt * 1.5f && Mathf.Abs(credits.AppliedSpeed - EclipseEndingCredits.NormalRollSpeed * EclipseEndingCredits.FastForwardMultiplier) < .001f, "Held Space accelerates only optional assets to five times their new normal speed (5.75x)");
        yield return ValidateOptionalFloorSpacing(game);
        CreditTick(game, false, true);
        Check(Section(game) == "Thanks" && !Bool(Credits(game), "ThanksComplete") && !Bool(game, "ExitUnlocked"), "Esc skips optional credits to the mandatory final thank-you, never beyond it");
        bool thanksNormal = true, thanksSeen = false; guard = 0;
        while (Section(game) == "Thanks")
        {
            float before = Number(Credits(game), "SectionElapsed"); CreditTick(game, true, true);
            thanksSeen |= credits.VisibleRows.Any(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ").Contains("THANK YOU FOR PLAYING"));
            if (!thanksGeometryChecked && Section(game) == "Thanks" && credits.SectionElapsed >= 1.5f)
            { ValidateFloorRows(game, "Thanks"); thanksGeometryChecked = true; }
            if (Section(game) == "Thanks") thanksNormal &= Mathf.Abs(Number(Credits(game), "SectionElapsed") - before - Dt) < .001f && !Bool(game, "ExitUnlocked") && Mathf.Abs(credits.AppliedSpeed - 1) < .001f;
            if (++guard > 3000) throw new TimeoutException("Mandatory thank-you did not finish.");
            if (guard % 30 == 0) yield return null;
        }
        Check(introGeometryChecked && teamGeometryChecked && thanksGeometryChecked, "All required floor-credit phases were measured using their actual runtime glyph renderers");
        Check(thanksNormal && thanksSeen && Bool(Credits(game), "ThanksComplete") && guard * Dt >= EclipseEndingCredits.ThanksDuration - Dt && guard * Dt < EclipseEndingCredits.ThanksDuration + Dt * 3, "The exact final thank-you plays all eight seconds at normal speed despite carried Space/Esc inputs", "simulatedSeconds=" + guard * Dt);
        guard = 0;
        while (Section(game) != "Finished") { CreditTick(game, true, true); if (++guard > 3000) throw new TimeoutException("Camera did not return to EXIT."); if (guard % 30 == 0) yield return null; }
        Check(Bool(game, "ExitUnlocked") && game.InputEnabled && !Bool(game, "EndingSequenceActive") && !ParallaxGameShell.BlocksGameplay && !Bool(game, "QuitRequested"),
            "Control resumes after camera return; finishing credits alone never quits the game");
        Check(Vector3.Distance(camera, game.view.transform.position) < .0001f && Mathf.Abs(fov - game.view.fieldOfView) < .001f &&
            Vector3.Distance(credits.ReturnMoonPosition, game.moon.position) < .0001f && Quaternion.Angle(moonRotation, game.moon.rotation) < .001f &&
            Vector3.Distance(scale, game.moon.localScale) < .0001f &&
            (credits.ReturnSkyPoseApplied || Vector3.Distance(moon, game.moon.position) < .0001f),
            "The camera and configured FOV return exactly while the unchanged Moon shape restores the original or route-safe gameplay pose");
        Check(credits.ReturnSkyPoseApplied && Vector3.Distance(moon, game.moon.position) > .5f &&
            game.ReleasedMoonAlignment.Complete && Vector3.Distance(game.ReleasedMoonAlignment.MoonPosition, moon) < .0001f &&
            game.MoonPreviewCoverage >= MoonAlignmentRules.RequiredCoverage,
            "The actual large-Moon solve moves its blocking scene sphere into the sky without changing the accepted raw release or losing the eclipse",
            "rawMoon=" + moon + " gameplayMoon=" + game.moon.position + " actualCoverage=" + game.MoonPreviewCoverage);
        ValidateReturnedMoonRoute(game);
        ValidateCompletedEclipseViewpoints(game, "Actual large-Moon solve");
        Check(solarPresentationStable && readingMoonFramingStable && Vector3.Distance(EclipseFinaleGame.SunPosition, originalSun) < .0001f &&
            Vector3.Distance(game.world.SunRenderer.transform.position, originalSun) < .0001f,
            "Return restores the exact original low puzzle Sun after keeping all Team/Assets/Thanks lunar limb samples framed",
            "allReadingSamples=" + framedDiscSamples + " maximumMoonAngleDegrees=" + maximumReadingMoonAngle);
        Check(!GameCompletionState.CreditsUnlocked && !File.Exists(GameCompletionState.ProfilePath),
            "Finishing the protected credit roll alone cannot earn completion before actual EXIT passage succeeds");
        GameProgressRuntime.Pause();
        Check(game.paused && ParallaxGameShell.ShouldShowPauseMenu, "Common pause is available again after all mandatory credits");
        yield return null;
        var music = game.GetComponent<EclipseEndingMusic>();
        Check(music != null && music.Started && music.Paused && music.Clip == music.MusicSource.clip,
            "The continuing Return/EXIT music obeys the common post-credits Pause flag");
        Check(GameProgressRuntime.SaveCurrent(), "The restored post-credits control state is safely saveable", GameProgressRuntime.LastError);
        GameProgressRuntime.Resume();
        yield return null;
        Check(music.Started && !music.Paused && music.MusicSource.clip == music.Clip,
            "Resume keeps the same ending music active for the actual EXIT walk");
        Check(!game.paused && Bool(game, "ExitUnlocked") && !ParallaxGameShell.BlocksGameplay,
            "Resume after credits keeps the unlocked EXIT without restarting the mandatory roll");
    }
    static bool EclipsePresentationMatchesEye(EclipseFinaleGame game)
    {
        var world = game.world;
        Vector3 eye = game.view.transform.position, moonRay = game.moon.position - eye;
        Vector3 haloRay = world.CoronaRenderer.transform.position - eye;
        return game.totality && game.MoonAlignmentConfirmed && world.SunSurfaceSuppressed && !world.SunRenderer.enabled &&
            world.CoronaTracksMoonRim && world.VisibleCoronaMargin > 0 &&
            Vector3.Distance(eye, world.CoronaReferenceEye) < .0001f &&
            haloRay.magnitude > moonRay.magnitude &&
            // Cross residual is stable for nearly collinear float vectors.
            // acos(normalized-dot) can report 0.0198 degrees for the same ray;
            // 1e-4 here is a stricter ~0.0057-degree alignment tolerance.
            Vector3.Cross(moonRay.normalized, haloRay.normalized).magnitude <= .0001f &&
            Vector3.Dot(moonRay.normalized, haloRay.normalized) > .99999f;
    }
    void ValidateCompletedEclipseViewpoints(EclipseFinaleGame game, string route)
    {
        Vector3 eye = game.view.transform.position, feet = game.body.transform.position;
        Quaternion rotation = game.view.transform.rotation, moonRotation = game.moon.rotation;
        Vector3 moonPosition = game.moon.position, moonScale = game.moon.localScale;
        MoonAlignmentSample release = game.ReleasedMoonAlignment;
        bool hidden = true, aligned = true; int samples = 0;
        float maximumCrossResidual = 0, maximumReportedAngle = 0;
        float minimumHaloDepthGap = float.PositiveInfinity, minimumCoronaMargin = float.PositiveInfinity;
        var observerFailures = new List<string>();
        Action observe = () =>
        {
            var world = game.world;
            Vector3 at = game.view.transform.position, moonRay = game.moon.position - at;
            Vector3 haloRay = world.CoronaRenderer.transform.position - at;
            float cross = Vector3.Cross(moonRay.normalized, haloRay.normalized).magnitude;
            float angle = Vector3.Angle(moonRay, haloRay);
            maximumCrossResidual = Mathf.Max(maximumCrossResidual, cross);
            maximumReportedAngle = Mathf.Max(maximumReportedAngle, angle);
            minimumHaloDepthGap = Mathf.Min(minimumHaloDepthGap, haloRay.magnitude - moonRay.magnitude);
            minimumCoronaMargin = Mathf.Min(minimumCoronaMargin, world.VisibleCoronaMargin);
            bool matches = EclipsePresentationMatchesEye(game);
            hidden &= world.SunSurfaceSuppressed && !world.SunRenderer.enabled;
            aligned &= matches; samples++;
            if (!matches && observerFailures.Count < 4)
                observerFailures.Add("eye=" + at.ToString("F5") + " moon=" + game.moon.position.ToString("F5") +
                    " sun=" + EclipseFinaleGame.SunPosition.ToString("F5") + " halo=" + world.CoronaRenderer.transform.position.ToString("F5") +
                    " radius=" + game.MoonRadius + " moonDistance=" + moonRay.magnitude +
                    " sunDistance=" + Vector3.Distance(at, EclipseFinaleGame.SunPosition) + " haloDistance=" + haloRay.magnitude +
                    " referenceEye=" + world.CoronaReferenceEye.ToString("F5") + " tracks=" + world.CoronaTracksMoonRim +
                    " margin=" + world.VisibleCoronaMargin + " cross=" + cross + " reportedAngle=" + angle +
                    " totality=" + game.totality + " confirmed=" + game.MoonAlignmentConfirmed +
                    " suppressed=" + world.SunSurfaceSuppressed + " sunEnabled=" + world.SunRenderer.enabled);
        };
        try
        {
            // Observer-only geometry probes cover side steps and the entire
            // authored approach/corridor. No body teleport or solve flags change.
            for (int column = 0; column < 5; column++) for (int station = 0; station < 14; station++)
            {
                game.view.transform.position = new Vector3(-4 + column * 2,
                    .06f + EclipseFinaleGame.EyeHeight + .12f, Mathf.Lerp(7, 72, station / 13f));
                game.world.SetLight(game.darkness, game.totality);
                observe();
            }
            foreach (Vector3 position in new[] { new Vector3(-10, eye.y, 1), new Vector3(10, eye.y, 1),
                new Vector3(-10, eye.y, 45), new Vector3(10, eye.y, 45) })
            {
                game.view.transform.position = position;
                game.world.SetLight(game.darkness, game.totality);
                observe();
            }
        }
        finally
        {
            game.view.transform.SetPositionAndRotation(eye, rotation);
            game.world.SetLight(game.darkness, game.totality);
        }
        Check(hidden && samples == 74,
            route + ": the bright Sun cannot reappear after credits at lateral and near/far EXIT observer positions",
            "observerSamples=" + samples + "; renderer state, not captured pixels");
        Check(aligned && EclipsePresentationMatchesEye(game),
            route + ": every moved observer retains a positive corona rim centered behind the unchanged opaque Moon",
            "observerSamples=" + samples + " maximumCrossResidual=" + maximumCrossResidual +
            " maximumReportedAcosAngle=" + maximumReportedAngle + " minimumHaloDepthGap=" + minimumHaloDepthGap +
            " minimumCoronaMargin=" + minimumCoronaMargin + " firstFailures=[" + string.Join(" | ", observerFailures) +
            "]; halo/limb angular geometry, not rendered-pixel approval");
        Check(Vector3.Distance(feet, game.body.transform.position) < .000001f &&
            Vector3.Distance(eye, game.view.transform.position) < .000001f && Quaternion.Angle(rotation, game.view.transform.rotation) < .001f &&
            Vector3.Distance(moonPosition, game.moon.position) < .000001f && Quaternion.Angle(moonRotation, game.moon.rotation) < .001f &&
            Vector3.Distance(moonScale, game.moon.localScale) < .000001f && game.ReleasedMoonAlignment.Equals(release),
            route + ": observer probes restore the camera and preserve the actual player, Moon shape/pose and original successful release snapshot");
    }
    void ValidateReturnedMoonRoute(EclipseFinaleGame game)
    {
        var sphere = game.moon.GetComponent<SphereCollider>();
        var renderer = game.moon.GetComponent<Renderer>();
        Check(sphere != null && sphere.enabled && renderer != null && renderer.enabled &&
            renderer.shadowCastingMode == ShadowCastingMode.On && !Physics.GetIgnoreCollision(game.body, sphere),
            "The return fix keeps the actual Moon collider, visible mesh and shadow active rather than bypassing player collision");
        Vector3 centre = game.moon.TransformPoint(sphere.center);
        float radius = sphere.radius * Mathf.Max(Mathf.Abs(game.moon.lossyScale.x), Mathf.Abs(game.moon.lossyScale.y), Mathf.Abs(game.moon.lossyScale.z));
        float minimumCapsuleGap = float.PositiveInfinity, minimumEyeGap = float.PositiveInfinity;
        float halfSegment = Mathf.Max(0, game.body.height * .5f - game.body.radius);
        for (int column = 0; column < 5; column++) for (int station = 0; station < 14; station++)
        {
            Vector3 feet = new Vector3(-4 + column * 2, .06f, Mathf.Lerp(7, 72, station / 13f));
            Vector3 middle = feet + game.body.center;
            Vector3 bottom = middle - Vector3.up * halfSegment, top = middle + Vector3.up * halfSegment;
            Vector3 segment = top - bottom;
            float t = Mathf.Clamp01(Vector3.Dot(centre - bottom, segment) / Mathf.Max(.000001f, segment.sqrMagnitude));
            float gap = Vector3.Distance(centre, bottom + t * segment) - radius - game.body.radius - game.body.skinWidth;
            minimumCapsuleGap = Mathf.Min(minimumCapsuleGap, gap);
            // Include the final entrance camera's small upward drift. This is a
            // geometric route probe, not a body teleport or completed traversal.
            float eyeGap = Vector3.Distance(feet + Vector3.up * (EclipseFinaleGame.EyeHeight + .12f), centre) - measuredMoonOuterRadius;
            minimumEyeGap = Mathf.Min(minimumEyeGap, eyeGap);
        }
        Check(minimumCapsuleGap > .1f && minimumEyeGap > game.view.nearClipPlane + .1f,
            "Independent route probes keep the returned large Moon clear of real capsule dimensions and the final drifting eye along the approach and EXIT corridor",
            "capsuleGap=" + minimumCapsuleGap + " outerMeshEyeGap=" + minimumEyeGap + "; actual sphere/mesh dimensions, not a rendered-pixel claim");
    }
    IEnumerator TraverseExit(EclipseFinaleGame game)
    {
        EclipsePickupContinuityValidation.RunExit(game, Check);
        game.Teleport(game.exitSign.position + new Vector3(0, .04f, -2));
        game.AimAt(game.exitSign.TransformPoint(new Vector3(0, .5f, 0))); game.Interact();
        Check(game.held == FinaleItem.Exit, "The exit sign becomes interactable only after mandatory credits");
        // A slightly elevated real aim keeps the enlarged sign above the floor;
        // the shared volume resolver preserves scale/depth instead of pushing it.
        game.AimAt(new Vector3(0, 2.6f, 65));
        for (int i = 0; i < 130; i++) { game.Tick(Dt, Vector2.zero, false); if (i % 20 == 0) yield return null; }
        Check(game.DoorFits, "Production perspective enlargement creates a capsule-sized real i opening", "scale=" + game.ExitScale);
        Check(game.Drop(), "The enlarged EXIT is released through the production lifecycle");
        for (int i = 0; i < 500 && game.ExitFalling; i++) { game.Tick(Dt, Vector2.zero, false); if (i % 20 == 0) yield return null; }
        string savedBefore = Hash(GameProgress.SavePath);
        Check(GameProgressRuntime.FailNextEndingTitleForValidation(),
            "Only the explicit isolated ending regression may inject one natural-title load failure");
        endingTitleRoute = true;
        yield return CompleteExitRoute(game, "injected-return");
        var presentation = game.GetComponent<EclipseExitPresentation>();
        yield return Wait(() => SceneManager.GetActiveScene().name == "Eclipse" && !GameProgressRuntime.Transitioning &&
            presentation != null && !presentation.Active, 10, "Injected title failure recovery");
        Check(game.body.enabled && game.paused && ParallaxGameShell.ShouldShowPauseMenu &&
            !presentation.TitleRequested && presentation.BlackAlpha < .001f && !ParallaxGameShell.Instance.EndingTitleRevealActive &&
            game.ExitUnlocked && !game.QuitRequested &&
            -game.exitSign.InverseTransformPoint(game.body.transform.position).z * game.ExitScale - .05f * game.ExitScale > game.body.radius + game.body.skinWidth + .1f,
            "A failed title load clears both black curtains, restores the real controller safely before EXIT and opens the common pause menu");
        Check(Hash(GameProgress.SavePath) == savedBefore, "Injected ending recovery preserves the actual continuing-game checkpoint");
        Check(!GameCompletionState.CreditsUnlocked && !File.Exists(GameCompletionState.ProfilePath) && !File.Exists(GameCompletionState.BackupPath),
            "A failed natural title load grants neither a session unlock nor a persistent completion record");
        GameProgressRuntime.Resume(); yield return null;
        Check(!game.paused && game.InputEnabled && game.ExitUnlocked && game.Credits.Section == EndingCreditSection.Finished,
            "Resume after the failed ending load restores control without replaying mandatory credits");
        yield return CompleteExitRoute(game, "retry-return");
        yield return WaitStage("MainMenu");
        endingTitleRoute = false;
        var shell = ParallaxGameShell.Instance;
        Check(shell != null && shell.IsTitle && !GameProgressRuntime.GameplayActive && !ParallaxGameShell.ShouldShowLoadingOverlay,
            "The actual completed ending returns to the real title scene without quitting or displaying a manual Loading overlay");
        Check(GameCompletionState.CreditsUnlocked && File.Exists(GameCompletionState.ProfilePath) &&
            File.Exists(GameCompletionState.BackupPath) && Hash(GameCompletionState.ProfilePath) == Hash(GameCompletionState.BackupPath),
            "Successful normal EXIT-to-title completion unlocks credits and atomically leaves valid matching primary and backup profile files");
        Check(shell.EndingTitleReplayActive && shell.CurrentIntroFrame.logo < .001f && shell.CurrentIntroFrame.background < .001f &&
            shell.CurrentIntroFrame.menu < .001f && !shell.OpenCreditsViewer(),
            "Actual title return begins the original title-only presentation and keeps its menu gated without replaying the school logo");
        yield return ValidateEndingTitleReplay(shell);
        Check(Hash(GameProgress.SavePath) == savedBefore,
            "The completed title replay and separate completion profile never rewrite the existing Continue checkpoint");
        yield return ValidateManualCreditsViewer(shell);
        var expected = new CompletionExpectation { processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            profileSha256 = Hash(GameCompletionState.ProfilePath), checkpointSha256 = Hash(GameProgress.SavePath), publicCreditSha256 = publicCreditSha };
        File.WriteAllText(Path.Combine(output, "completion-profile-expectation.json"), JsonUtility.ToJson(expected, true), new UTF8Encoding(false));
    }
    IEnumerator ValidateEndingTitleReplay(ParallaxGameShell shell)
    {
        int dispatchStart = shell.SoundDispatch.Length;
        bool noSchool = true, titleOnly = false, lightFade = false, menuFade = false, ordered = true;
        float previousTitle = 0, previousBackground = 0, previousMenu = 0;
        int guard = 0;
        while (shell.EndingTitleReplayActive)
        {
            shell.TickEndingTitlePresentation(.05f);
            var frame = shell.CurrentIntroFrame;
            noSchool &= frame.logo < .0001f && frame.phase != "school-logo";
            titleOnly |= frame.title > .8f && frame.background < .001f && frame.menu < .001f;
            lightFade |= frame.title > .999f && frame.background > .05f && frame.background < .95f && frame.menu < .001f;
            menuFade |= frame.background > .999f && frame.menu > .05f && frame.menu < .95f;
            ordered &= frame.title >= previousTitle - .0001f && frame.background >= previousBackground - .0001f && frame.menu >= previousMenu - .0001f;
            previousTitle = frame.title; previousBackground = frame.background; previousMenu = frame.menu;
            if (++guard > 150) throw new TimeoutException("Original title replay timeline did not finish.");
            if (guard % 10 == 0) yield return null;
        }
        var emitted = shell.SoundDispatch.Skip(dispatchStart).ToArray();
        Check(noSchool && titleOnly && lightFade && menuFade && ordered && shell.CurrentIntroFrame.menu > .999f &&
            Mathf.Abs(shell.EndingTitleReplayElapsed - ParallaxGameShell.EndingTitleRevealSeconds) < .0001f,
            "The same production presentation ticks reveal Parallax first, then lighting/background, then the menu over 5.28 seconds without a university-logo replay");
        Check(emitted.Count(item => item == "switch") == 1 && emitted.Count(item => item == "startup") == 1 &&
            emitted.Count(item => item == "hum-start") == 1 && emitted.Count(item => item == "initial-rattle") == 1 &&
            Array.IndexOf(emitted, "switch") < Array.IndexOf(emitted, "startup"),
            "The restored original title sequence dispatches one switch, startup, hum and initial-rattle event in the intended order",
            string.Join(", ", emitted) + "; muted event routing only, not audible listening");
    }
    IEnumerator ValidateManualCreditsViewer(ParallaxGameShell shell)
    {
        string profileBefore = Hash(GameCompletionState.ProfilePath), saveBefore = Hash(GameProgress.SavePath);
        Check(shell.MainMenuItemCount == 5 && shell.GetMainMenuLabel(2) == "환경설정" && shell.GetMainMenuLabel(3) == "크레딧" && shell.GetMainMenuLabel(4) == "게임 나가기",
            "Earned credits appear immediately below Settings while retaining the original Quit command");
        var rects = Enumerable.Range(0, shell.MainMenuItemCount).Select(shell.GetMainMenuRect).ToArray();
        Check(rects.All(rect => rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= 1920 && rect.yMax <= 1080 && rect.height >= 68) &&
            Enumerable.Range(1, rects.Length - 1).All(i => rects[i].yMin >= rects[i - 1].yMax),
            "All five actual title-command rectangles fit the 1920x1080 design canvas without overlap or shrinking the existing text rows");
        Check(shell.OpenCreditsViewer() && shell.CreditsViewerOpen && shell.CreditsViewerScrollY == 0 && shell.CreditsViewerMaxScroll > 650,
            "After the title reveal the actual credits viewer opens at the top with scrollable full content");
        var document = JsonUtility.FromJson<EclipseCreditDocument>(Resources.Load<TextAsset>("Eclipse/EndingCredits").text);
        string text = shell.CreditsViewerContentText;
        Check(shell.CreditsViewerEntryCount == document.team.Length + document.assets.Length &&
            document.team.All(entry => text.Contains(entry.title) && text.Contains(entry.role)) &&
            document.assets.All(entry => text.Contains(entry.title) && text.Contains(string.IsNullOrWhiteSpace(entry.displayUsage) ? entry.usage : entry.displayUsage) &&
                (string.IsNullOrWhiteSpace(entry.creator) || text.Contains(entry.creator)) && (string.IsNullOrWhiteSpace(entry.license) || text.Contains(entry.license)) &&
                (string.IsNullOrWhiteSpace(entry.url) || text.Contains(entry.url))),
            "The manual viewer includes every approved team role including the user's later corrections and every actual-use asset, author, required notice and source link",
            "entries=" + shell.CreditsViewerEntryCount);
        yield return null; yield return null;
        Check(shell.CreditsViewerScrollY == 0 && FindFirstObjectByType<EclipseEndingMusic>() == null &&
            !FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source => source.isPlaying && source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Music"),
            "Manual credits do not auto-scroll or start any ending-music voice");
        shell.ScrollCreditsViewer(416);
        float scrolled = shell.CreditsViewerScrollY;
        Check(scrolled > 0 && scrolled <= 416.001f, "The same public scroll operation used by the mouse-wheel handler moves manual credits");
        shell.ScrollCreditsViewer(float.NaN); shell.ScrollCreditsViewer(float.PositiveInfinity);
        Check(Mathf.Abs(shell.CreditsViewerScrollY - scrolled) < .0001f, "Nonfinite scroll values cannot corrupt the viewer position");
        shell.ScrollCreditsViewer(1000000);
        Check(Mathf.Abs(shell.CreditsViewerScrollY - shell.CreditsViewerMaxScroll) < .001f, "Manual credits scroll clamps to the end of the full source list");
        shell.ScrollCreditsViewer(-1000000);
        Check(shell.CreditsViewerScrollY == 0, "Manual credits scroll clamps to the start");
        shell.CloseCreditsViewer();
        Check(!shell.CreditsViewerOpen && shell.IsTitle && shell.OpenCreditsViewer() && shell.CreditsViewerScrollY == 0,
            "Closing returns to the title and reopening resets the manual credits to the first entry");
        shell.CloseCreditsViewer();
        Check(Hash(GameCompletionState.ProfilePath) == profileBefore && Hash(GameProgress.SavePath) == saveBefore,
            "Manual credits browsing leaves both the earned completion profile and existing Continue save byte-identical");
    }
    IEnumerator CompleteExitRoute(EclipseFinaleGame game, string attempt)
    {
        Vector3 approach = game.exitSign.TransformPoint(new Vector3(EclipseFinaleWorld.DoorX, 0, -.65f));
        yield return Walk(game, approach);
        Vector3 approachFeet = game.body.transform.position;
        Vector3 approachLocal = game.exitSign.InverseTransformPoint(approachFeet);
        float approachError = Vector2.Distance(new Vector2(approachFeet.x, approachFeet.z), new Vector2(approach.x, approach.z));
        game.AimAt(game.exitSign.TransformPoint(new Vector3(EclipseFinaleWorld.DoorX, .45f, 2)));
        bool crossed = false;
        var recentTicks = new Queue<string>();
        int crossingTicks = 0;
        for (int i = 0; i < 360 && !game.EndingSequenceActive; i++)
        {
            game.Tick(Dt, Vector2.up, false); crossingTicks++;
            crossed |= game.exitSign.InverseTransformPoint(game.body.transform.position).z >= -.015f;
            if (i % 10 == 0)
            {
                recentTicks.Enqueue("tick=" + i + " local=" + game.exitSign.InverseTransformPoint(game.body.transform.position).ToString("F5") +
                    " motor=" + game.Locomotion.Velocity.ToString("F5") + " controller=" + game.body.velocity.ToString("F5") +
                    " grounded=" + game.body.isGrounded + " input=" + game.InputEnabled + " phase=" + game.phase);
                while (recentTicks.Count > 6) recentTicks.Dequeue();
                yield return null;
            }
        }
        var presentation = game.GetComponent<EclipseExitPresentation>();
        Check(crossed && presentation != null && presentation.Phase == EclipseExitPhase.Enter && !game.InputEnabled && !game.QuitRequested,
            attempt + ": real CharacterController traversal starts a gradual entry rather than quitting",
            ExitTraversalDiagnostics(game, presentation, crossed, crossingTicks, approach, approachFeet, approachLocal, approachError, recentTicks));
        var original = Resources.Load<AudioClip>(EclipseExitPresentation.WhooshResourceKey);
        var whoosh = presentation.WhooshSource;
        Check(original != null && whoosh != null && whoosh.clip != null && whoosh.clip != original &&
            whoosh.clip.samples == original.samples && whoosh.clip.frequency == original.frequency && whoosh.clip.channels == original.channels &&
            whoosh.outputAudioMixerGroup != null && whoosh.outputAudioMixerGroup.name == "Sfx" && original.name != whoosh.clip.name,
            attempt + ": the selected existing flight recording is reused through an independent SFX-routed whoosh voice");
        var config = GameShellSettingsRuntime.Current.DeepCopy();
        var mutedEffects = config.DeepCopy(); mutedEffects.masterVolume = 1; mutedEffects.effectsVolume = 0;
        GameShellSettingsRuntime.Apply(mutedEffects, false); yield return null;
        float effectsDb; whoosh.outputAudioMixerGroup.audioMixer.GetFloat("SfxVolume", out effectsDb);
        Check(effectsDb <= -79, attempt + ": the actual EXIT voice obeys Effects mute", "dB=" + effectsDb);
        var mutedAmbient = config.DeepCopy(); mutedAmbient.masterVolume = 1; mutedAmbient.effectsVolume = 1; mutedAmbient.ambientVolume = 0;
        GameShellSettingsRuntime.Apply(mutedAmbient, false); yield return null;
        whoosh.outputAudioMixerGroup.audioMixer.GetFloat("SfxVolume", out effectsDb);
        Check(Mathf.Abs(effectsDb) < .01f && whoosh.outputAudioMixerGroup.name == "Sfx",
            attempt + ": Ambient mute leaves the separate EXIT sound-effects bus available", "dB=" + effectsDb);
        GameShellSettingsRuntime.Apply(config, false);
        GameProgressRuntime.Pause();
        Check(!game.paused && !GameProgressRuntime.SaveCurrent(), attempt + ": common pause and checkpoint writes cannot interrupt the exit presentation");
        Vector3 fixedFeet = game.body.transform.position, previousEye = game.view.transform.position;
        float fov = game.view.fieldOfView, previousAlpha = presentation.BlackAlpha, previousDistance = presentation.ForwardDistance;
        bool gradual = true, darkIntermediateSeen = false, clearLeadInSeen = false, bodyStable = true;
        bool entranceTotalityStable = true; int entranceObserverSamples = 0;
        int guard = 0;
        while (presentation.Phase == EclipseExitPhase.Enter)
        {
            game.Tick(Dt, Vector2.one, true);
            gradual &= Vector3.Distance(previousEye, game.view.transform.position) < .12f &&
                presentation.BlackAlpha >= previousAlpha - .0001f && presentation.ForwardDistance >= previousDistance - .0001f;
            bodyStable &= Vector3.Distance(fixedFeet, game.body.transform.position) < .0001f && Mathf.Abs(fov - game.view.fieldOfView) < .001f;
            clearLeadInSeen |= presentation.Elapsed > .5f && presentation.Elapsed < 1.1f && presentation.BlackAlpha < .001f;
            darkIntermediateSeen |= presentation.BlackAlpha > .1f && presentation.BlackAlpha < .9f;
            previousEye = game.view.transform.position; previousAlpha = presentation.BlackAlpha; previousDistance = presentation.ForwardDistance;
            CheckNoPrematureQuit(game, presentation);
            if (++guard > 600) throw new TimeoutException("Exit entrance did not finish.");
            if (guard % 10 == 0)
            {
                // A real frame executes World.LateUpdate after the entrance's
                // camera motion; do not refresh through SetLight in this probe.
                yield return null;
                entranceTotalityStable &= EclipsePresentationMatchesEye(game); entranceObserverSamples++;
            }
        }
        Check(gradual && bodyStable && clearLeadInSeen && darkIntermediateSeen && presentation.Phase == EclipseExitPhase.BlackThanks &&
            presentation.Elapsed >= EclipseExitPresentation.EnterDuration - Dt && presentation.ForwardDistance > 4 && presentation.BlackAlpha > .999f,
            attempt + ": entry advances gradually for 3.8 seconds, then settles to full black without moving the player body",
            "elapsed=" + presentation.Elapsed + " forward=" + presentation.ForwardDistance);
        Check(entranceTotalityStable && entranceObserverSamples > 0,
            attempt + ": actual LateUpdate keeps the solved eclipse aligned while the final entrance camera moves beyond normal gameplay ticks",
            "lateFrameSamples=" + entranceObserverSamples + "; camera/corona geometry and bright-surface visibility only");
        var music = game.GetComponent<EclipseEndingMusic>();
        Check(music != null && music.CodaClip != null && music.CodaSource != null,
            attempt + ": the approved original-song ending coda has its own loaded voice");
        music.TickMusic(0);
        Check(music.Closing && music.ClosingFadeGain > .999f && presentation.MusicFadeGain > .999f &&
            music.CodaSource.volume > EclipseEndingMusic.BaseGain * .99f &&
            music.CodaSource.outputAudioMixerGroup != null && music.CodaSource.outputAudioMixerGroup.name == "Music" &&
            music.MusicSource.volume < .0001f && music.IncomingSource.volume < .0001f,
            attempt + ": Aurora remains at its authored gain on the black thank-you after the original voices crossfade into the natural coda",
            "codaGain=" + music.CodaSource.volume + " crossfade=" + music.CodaCrossfadeProgress);
        Check(EclipseExitPresentation.ClosingThanksText == "감사합니다" && !EclipseExitPresentation.ClosingThanksText.EndsWith("."),
            attempt + ": the actual HUD's shared closing message has no trailing period");
        bool thanksSeen = false, fadeSeen = false, synced = true, musicStayed = true;
        float previousGain = 1; guard = 0;
        while (presentation.Phase == EclipseExitPhase.BlackThanks)
        {
            thanksSeen |= presentation.ThanksAlpha > .85f;
            game.Tick(Dt, Vector2.one, true); music.TickMusic(0);
            float age = presentation.Elapsed - EclipseExitPresentation.EnterDuration;
            if (presentation.Phase == EclipseExitPhase.BlackThanks)
            {
                float gain = presentation.MusicFadeGain;
                if (age < EclipseExitPresentation.ThanksDuration - EclipseExitPresentation.ThanksFadeSeconds)
                    musicStayed &= gain > .999f && music.CodaSource.volume > EclipseEndingMusic.BaseGain * .99f;
                else
                {
                    fadeSeen |= gain > .05f && gain < .95f;
                    synced &= Mathf.Abs(presentation.ThanksAlpha - gain) < .0001f &&
                        Mathf.Abs(music.ClosingFadeGain - gain) < .0001f &&
                        Mathf.Abs(music.CodaSource.volume - EclipseEndingMusic.BaseGain * gain) < .0001f &&
                        gain <= previousGain + .0001f && previousGain - gain < .12f;
                }
                previousGain = gain;
                CheckNoPrematureQuit(game, presentation);
            }
            if (++guard > 300) throw new TimeoutException("Closing black thank-you did not reach its protected hold.");
            if (guard % 10 == 0) yield return null;
        }
        Check(thanksSeen && fadeSeen && synced && musicStayed && presentation.Phase == EclipseExitPhase.BlackHold &&
            !presentation.TitleRequested && presentation.ThanksAlpha < .0001f && presentation.BlackAlpha > .999f &&
            music.CodaSource.volume < .0001f && music.MusicSource.volume < .0001f && music.IncomingSource.volume < .0001f,
            attempt + ": the visible closing text and Aurora coda fade together only during the final thank-you, then reach silence together");
        float holdStart = presentation.Elapsed; bool holdClear = true; guard = 0;
        while (!presentation.TitleRequested)
        {
            holdClear &= presentation.Phase == EclipseExitPhase.BlackHold && presentation.BlackAlpha > .999f &&
                presentation.ThanksAlpha < .0001f && presentation.MusicFadeGain < .0001f && music.CodaSource.volume < .0001f;
            game.Tick(Dt, Vector2.one, true); music.TickMusic(0);
            if (++guard > 180) throw new TimeoutException("Closing silent hold did not request title return.");
            if (guard % 10 == 0) yield return null;
        }
        Check(holdClear && guard * Dt >= EclipseExitPresentation.BlackHoldDuration - Dt * 2 &&
            guard * Dt <= EclipseExitPresentation.BlackHoldDuration + Dt * 2 &&
            presentation.Elapsed >= EclipseExitPresentation.EnterDuration + EclipseExitPresentation.ThanksDuration + EclipseExitPresentation.BlackHoldDuration - Dt &&
            presentation.Phase == EclipseExitPhase.Returning && !game.QuitRequested,
            attempt + ": a separate one-second fully black silent hold follows the 2.2-second thank-you before the natural title replay",
            "holdSeconds=" + guard * Dt + " total=" + presentation.Elapsed);
    }
    string ExitTraversalDiagnostics(EclipseFinaleGame game, EclipseExitPresentation presentation, bool crossed, int ticks,
        Vector3 approach, Vector3 approachFeet, Vector3 approachLocal, float approachError, Queue<string> recent)
    {
        var body = game.body;
        Vector3 centre = body.transform.TransformPoint(body.center);
        float halfSegment = Mathf.Max(0, body.height * .5f - body.radius);
        var overlapping = Physics.OverlapCapsule(centre - Vector3.up * halfSegment, centre + Vector3.up * halfSegment,
            body.radius + body.skinWidth + .015f, ~0, QueryTriggerInteraction.Ignore)
            .Where(collider => collider != body)
            .Select(collider => collider.name + " layer=" + collider.gameObject.layer + " bounds=" + collider.bounds.ToString("F5")).ToArray();
        Vector3 local = game.exitSign.InverseTransformPoint(body.transform.position);
        return "crossed=" + crossed + " ticks=" + ticks + " presentation=" + (presentation == null ? "<missing>" : presentation.Phase.ToString()) +
            " input=" + game.InputEnabled + " totality=" + game.totality + " anchored=" + game.MoonAnchored + " confirmed=" + game.MoonAlignmentConfirmed +
            " exitFalling=" + game.ExitFalling + " doorFits=" + game.DoorFits + " controllerEnabled=" + body.enabled +
            " feet=" + body.transform.position.ToString("F5") + " local=" + local.ToString("F5") + " motor=" + game.Locomotion.Velocity.ToString("F5") +
            " controllerVelocity=" + body.velocity.ToString("F5") + " grounded=" + body.isGrounded + " phase=" + game.phase +
            " approachTarget=" + approach.ToString("F5") + " actualApproachFeet=" + approachFeet.ToString("F5") + " actualApproachLocal=" + approachLocal.ToString("F5") +
            " approachError=" + approachError + " walkTolerance=.14" +
            " approachLateralWorldError=" + Mathf.Abs(approachLocal.x - EclipseFinaleWorld.DoorX) * game.ExitScale +
            " finalLateralWorldError=" + Mathf.Abs(local.x - EclipseFinaleWorld.DoorX) * game.ExitScale +
            " apertureWorldRadiusClearance=" + (EclipseFinaleWorld.DoorWidth * .5f * game.ExitScale - body.radius) +
            " apertureWorldSkinClearance=" + (EclipseFinaleWorld.DoorWidth * .5f * game.ExitScale - body.radius - body.skinWidth) +
            " signPos=" + game.exitSign.position.ToString("F5") + " signScale=" + game.exitSign.localScale.ToString("F5") +
            " signRotation=" + game.exitSign.rotation.eulerAngles.ToString("F5") + " externalCapsuleContacts=[" + string.Join(" | ", overlapping) +
            "] recentTenTickSamples=[" + string.Join(" | ", recent) + "]";
    }
    void CheckNoPrematureQuit(EclipseFinaleGame game, EclipseExitPresentation presentation)
    {
        if (game.QuitRequested || presentation.TitleRequested) throw new InvalidOperationException("The game requested quit/title during the protected entrance phase.");
    }
    IEnumerator Walk(EclipseFinaleGame game, Vector3 target)
    {
        int count = 0;
        while (Vector2.Distance(new Vector2(game.body.transform.position.x, game.body.transform.position.z), new Vector2(target.x, target.z)) > .14f)
        {
            if (++count > 2400) throw new TimeoutException("Actual Eclipse controller failed to reach the exit approach.");
            Vector3 delta = target - game.body.transform.position; delta.y = 0;
            game.AimAt(game.view.transform.position + delta.normalized * 20); game.Tick(Dt, Vector2.up, false);
            if (count % 15 == 0) yield return null;
        }
    }
}
