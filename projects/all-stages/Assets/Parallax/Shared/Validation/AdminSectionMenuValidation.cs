using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;
using ShadeLink.Eclipse;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink
{
    // Opt-in separate-process integration QA. All map/section choices invoke
    // the same public actions as the rendered menu. Declared authorization,
    // feet and view fixtures are disclosed; no OS input or rendered pixels.
    [DefaultExecutionOrder(1000)]
    public sealed class AdminSectionMenuValidation : MonoBehaviour
    {
        const string Flag = "-admin-sections-validate";
        static readonly string[] Maps = { "Tutorial", "Gallery", "Beach", "Eclipse" };
        static readonly string[] RealNames = { "ParallaxCheckpoint.json", "ParallaxCheckpoint.json.bak",
            "ParallaxCheckpoint.admin-session.json", "ParallaxCheckpoint.admin-session.json.bak",
            "settings.json", "settings.json.previous", "ParallaxEndingSeen.json", "ParallaxEndingSeen.json.bak" };
        static bool claimed;
        static string output, isolated, requestedPhase;
        static Dictionary<string, string> productionBefore;
        [Serializable] public sealed class Check { public bool passed; public string name, detail; }
        [Serializable] public sealed class Storage { public string path, before, after; public bool unchanged; }
        [Serializable] public sealed class Section { public string scene, label; public int index; }
        [Serializable] public sealed class Report
        {
            public bool passed, productionStorageUnchanged, normalSlotUnchanged, renderedPixelsVerified;
            public int captureCount, osInputCount, processId, expectedSectionLoads, sectionLoads, sceneLoadedEvents, menuMaps, exitWalkTicks;
            public float maximumLabelWidth;
            public string version, phase, failure, finalScene, scope, dllSha256, unity, graphicsDevice;
            public string normalSlotBefore, normalSlotAfter, normalBackupBefore, normalBackupAfter;
            public Check[] checks; public Storage[] storage; public Section[] sections; public string[] errors;
        }
        readonly List<Check> checks = new List<Check>();
        readonly List<string> errors = new List<string>();
        readonly List<Section> sections = new List<Section>();
        readonly Report report = new Report();
        ParallaxGameShell shell;
        string phase, normalBefore, normalBackupBefore;

        static string Arg(string name)
        { var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, name); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        static string Hash(string path)
        {
            if (!File.Exists(path)) return "<absent>";
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            if (claimed || Array.IndexOf(args, Flag) < 0) return;
            claimed = true;
            if (!Application.isBatchMode || Array.IndexOf(args, "-game-progress-validate") < 0 || Array.IndexOf(args, "-preview-validate") < 0)
                throw new ArgumentException("Administrator QA requires isolated batch mode and passive progress/preview validation flags.");
            if (Array.Exists(args, value => value.EndsWith("-test", StringComparison.OrdinalIgnoreCase)) ||
                Array.IndexOf(args, "-player-shell-validate") >= 0 || Array.IndexOf(args, "-eclipse-ending-validate") >= 0)
                throw new ArgumentException("Do not combine administrator QA with another active validator or capture driver.");
            requestedPhase = Arg("-admin-sections-phase") ?? "full";
            if (requestedPhase != "full" && requestedPhase != "startup") throw new ArgumentException("Unknown administrator QA phase.");
            if ((Array.IndexOf(args, "-admin-start-section") >= 0) != (requestedPhase == "startup"))
                throw new ArgumentException("The startup request belongs only to the explicit startup QA phase.");
            string folder = Arg(Flag);
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder)) throw new ArgumentException(Flag + " requires an absolute output folder.");
            output = Path.GetFullPath(folder); isolated = Path.Combine(output, "SaveData");
            string real = Path.GetFullPath(Application.persistentDataPath);
            if (output.StartsWith(real + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output, real, StringComparison.OrdinalIgnoreCase) || Directory.Exists(isolated))
                throw new InvalidOperationException("Administrator QA needs a fresh output folder separate from player storage.");
            productionBefore = RealNames.ToDictionary(name => Path.Combine(real, name), name => Hash(Path.Combine(real, name)));
            if (!GameProgressRuntime.SetSaveDirectoryForValidation(isolated)) throw new InvalidOperationException(GameProgressRuntime.LastError);
            Directory.CreateDirectory(output); AudioListener.volume = 0;
            var host = new GameObject("Administrator sections · isolated native integration");
            DontDestroyOnLoad(host); host.AddComponent<AdminSectionMenuValidation>();
        }
        void Awake() { Application.logMessageReceived += Log; SceneManager.sceneLoaded += Loaded; }
        void OnDestroy() { Application.logMessageReceived -= Log; SceneManager.sceneLoaded -= Loaded; }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            report.sceneLoadedEvents++;
            var gallery = FindFirstObjectByType<GalleryGame>(); if (gallery != null) gallery.automated = true;
            var beach = FindFirstObjectByType<BeachGame>(); if (beach != null) beach.automated = true;
            var eclipse = FindFirstObjectByType<EclipseFinaleGame>(); if (eclipse != null) eclipse.automated = true;
            Application.runInBackground = true; Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
            Debug.Log("ADMIN_SECTIONS_FOCUS_FIXTURE " + scene.name + ": hidden-batch automated Gallery/Beach/Eclipse; Tutorial has no automated field; no OS input");
        }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool passed, string name, string detail = "")
        {
            checks.Add(new Check { passed = passed, name = name, detail = detail });
            Debug.Log("ADMIN_SECTIONS_" + (passed ? "PASS " : "FAIL ") + name + " " + detail);
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
            var stack = new Stack<IEnumerator>(); stack.Push(Sequence());
            double deadline = Time.realtimeSinceStartupAsDouble + 380;
            while (stack.Count > 0)
            {
                bool more; object next = null;
                try
                {
                    if (errors.Count > 0) throw new InvalidOperationException("Runtime error: " + errors[0]);
                    if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Administrator QA deadline at " + phase);
                    more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current;
                }
                catch (Exception error) { report.failure = error.ToString(); break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            Finish();
        }
        void Finish()
        {
            report.storage = productionBefore.Select(pair => new Storage { path = pair.Key, before = pair.Value,
                after = Hash(pair.Key), unchanged = pair.Value == Hash(pair.Key) }).ToArray();
            report.productionStorageUnchanged = report.storage.All(item => item.unchanged);
            report.normalSlotBefore = normalBefore; report.normalBackupBefore = normalBackupBefore;
            report.normalSlotAfter = Hash(GameProgress.SavePath); report.normalBackupAfter = Hash(GameProgress.BackupPath);
            report.normalSlotUnchanged = normalBefore != null && normalBefore == report.normalSlotAfter && normalBackupBefore == report.normalBackupAfter;
            checks.Add(new Check { passed = report.productionStorageUnchanged, name = "All eight real player storage files remain byte-identical" });
            checks.Add(new Check { passed = report.normalSlotUnchanged, name = "The isolated normal V1 Continue slot and backup survive administrator travel unchanged" });
            report.passed = report.failure == null && errors.Count == 0 && checks.All(item => item.passed) && report.sectionLoads == report.expectedSectionLoads;
            report.version = ParallaxGameShell.PreviewVersion; report.phase = requestedPhase + ":" + phase;
            report.captureCount = report.osInputCount = 0; report.renderedPixelsVerified = false;
            report.processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            report.dllSha256 = Hash(typeof(ParallaxGameShell).Assembly.Location);
            report.unity = Application.unityVersion; report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            report.finalScene = SceneManager.GetActiveScene().name;
            report.scope = "Fresh hidden, muted D3D11 native process. Real production two-level menu actions and section loading, authored controller/gate state and physical capsule sweeps; explicit authorization, actor/view and EXIT approach fixtures. Startup phase observes the actual generic startup request. Full phase also traverses the real i-opening and natural ending-to-title path. Font advances and reference-canvas bounds are data checks, not rendered-pixel or subjective appearance approval. No screenshot, explicit rendering/readback, OS input, player settings write, foreign test flag or existing-process termination. Only this process quits itself.";
            report.checks = checks.ToArray(); report.errors = errors.ToArray(); report.sections = sections.ToArray();
            File.WriteAllText(Path.Combine(output, "admin-section-menu-results.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("ADMIN_SECTIONS_VALIDATION " + (report.passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }
        IGameProgressStage CurrentStage()
        {
            switch (SceneManager.GetActiveScene().name)
            {
                case "Tutorial": return FindFirstObjectByType<TutorialGame>();
                case "Gallery": return FindFirstObjectByType<GalleryGame>();
                case "Beach": return FindFirstObjectByType<BeachGame>();
                case "Eclipse": return FindFirstObjectByType<EclipseFinaleGame>();
                default: return null;
            }
        }
        bool Ready(string scene) => SceneManager.GetActiveScene().name == scene && CurrentStage() != null &&
            CurrentStage().ProgressReady && !GameProgressRuntime.Transitioning;
        IEnumerator Wait(Func<bool> condition, float seconds, string reason)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(reason + ": " + GameProgressRuntime.LastError); yield return null; }
        }
        void SeedNormalSlot()
        {
            Require(GameProgress.TrySave(new GameProgressRecord { stage = "Tutorial", tutorialStage = 1, checkpoint = 0 }),
                "Seed a disclosed isolated normal Continue slot after unauthenticated setup", GameProgress.LastError);
            normalBefore = Hash(GameProgress.SavePath); normalBackupBefore = Hash(GameProgress.BackupPath);
            string[] expectedFields = { "version", "stage", "checkpoint", "tutorialStage", "tutorialStep", "galleryRoom",
                "tutorialFirstSize", "milestones", "swordEncountered", "savedUtc", "saveId" };
            string[] fields = typeof(GameProgressRecord).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name).OrderBy(name => name).ToArray();
            Require(GameProgress.Version == 1 && fields.SequenceEqual(expectedFields.OrderBy(name => name)) &&
                GameProgress.TryLoad(out var saved) && saved.version == 1 && saved.stage == "Tutorial" && saved.tutorialStage == 1,
                "Administrator section selection adds no field or schema revision to normal version-one saves", string.Join(",", fields));
        }
        IEnumerator Sequence()
        {
            phase = "shell"; yield return Wait(() => ParallaxGameShell.Instance != null, 20, "Shared shell did not initialize");
            shell = ParallaxGameShell.Instance;
            Require(ParallaxGameShell.PreviewVersion == "0.39.0-admin-sections", "The independent runner belongs to the requested administrator-sections build", ParallaxGameShell.PreviewVersion);
            Require(Path.GetDirectoryName(GameProgress.SavePath) == isolated && !GameProgress.IsUnisolatedValidation,
                "Progress and completion use the fresh isolated SaveData directory", GameProgress.SavePath);
            if (requestedPhase == "startup") { yield return StartupSequence(); yield break; }
            report.expectedSectionLoads = Maps.Sum(map => AdminStageSections.ForStage(map).Count);
            AdminStageSectionsValidation.VerifyCatalog(Require);
            phase = "denied-setup";
            Require(!ParallaxGameShell.Administrator && !GameProgressRuntime.TestSession,
                "The full runner starts without administrator or test-session authority");
            Require(StageTravelMenu.LoadStage("Eclipse", true), "Declare an unauthenticated Eclipse scene fixture for denial checks");
            yield return Wait(() => Ready("Eclipse"), 90, "Unauthenticated Eclipse setup");
            yield return null; yield return null;
            GameProgressRuntime.Pause();
            Require(!shell.OpenAdminTravelMenu() && shell.AdminTravelOptionCount == 0 && !shell.SelectAdminTravelOption(0) &&
                !shell.BackAdminTravelMenu() && !shell.HandleAdminTravelKey(KeyCode.Return),
                "Normal players cannot open, select, confirm or back through administrator menu actions");
            int deniedHandle = SceneManager.GetActiveScene().handle;
            Require(!GameProgressRuntime.AdminTravel("Gallery", 4) && SceneManager.GetActiveScene().handle == deniedHandle && !GameProgressRuntime.Transitioning,
                "The public backend independently denies unauthenticated section travel without loading");
            EclipseAdminSectionValidation.RunDenied(FindFirstObjectByType<EclipseFinaleGame>(), Require);
            // The old normal-session autosave is allowed during declared setup.
            // The preservation baseline begins here, before any admin selection.
            SeedNormalSlot();
            var setter = typeof(ParallaxGameShell).GetProperty("AdminMode", BindingFlags.Public | BindingFlags.Instance).GetSetMethod(true);
            setter.Invoke(shell, new object[] { true });
            Require(ParallaxGameShell.Administrator, "Explicit native authorization fixture enables the existing session-only administrator credential",
                "Private AdminMode setter fixture only in this opt-in batch runner; ordinary unlock gesture unchanged");
            Require(!GameProgressRuntime.AdminTravel("Unknown", 0) && !GameProgressRuntime.AdminTravel("Gallery", -1) &&
                !GameProgressRuntime.AdminTravel("Gallery", AdminStageSections.ForStage("Gallery").Count) && !GameProgressRuntime.Transitioning,
                "The authenticated backend rejects unknown maps and out-of-range sections before starting a load");
            for (int map = 0; map < Maps.Length; map++)
            {
                string scene = Maps[map]; var catalog = AdminStageSections.ForStage(scene);
                for (int index = 0; index < catalog.Count; index++)
                {
                    phase = "section-" + scene + "-" + index;
                    GameProgressRuntime.Pause();
                    Require(shell.OpenAdminTravelMenu() && !shell.AdminTravelSectionsOpen && shell.AdminTravelOptionCount == Maps.Length + 1,
                        "Opening map travel shows only four parent maps and Back", scene + ":" + index);
                    Require(Enumerable.Range(0, Maps.Length).All(i => shell.AdminTravelOptionLabel(i) == AdminStageSections.StageLabel(Maps[i])),
                        "The parent menu contains map names rather than a flattened section list");
                    InspectMenuBounds();
                    int previousHandle = SceneManager.GetActiveScene().handle, previousEvents = report.sceneLoadedEvents;
                    Require(shell.SelectAdminTravelOption(map), "The same action as a map mouse click opens its child section menu", scene);
                    Require(shell.AdminTravelSectionsOpen && shell.AdminTravelStage == scene && shell.AdminTravelOptionCount == catalog.Count + 1 &&
                        SceneManager.GetActiveScene().handle == previousHandle && report.sceneLoadedEvents == previousEvents &&
                        !GameProgressRuntime.Transitioning && GameProgressRuntime.Paused && ParallaxGameShell.BlocksGameplay,
                        "Selecting a parent map performs no scene load and keeps gameplay blocked", scene);
                    Require(Enumerable.Range(0, catalog.Count).All(i => shell.AdminTravelOptionLabel(i) == catalog[i].Label),
                        "The child lists only this map's actual numbered sections", scene);
                    InspectMenuBounds();
                    if (index == 0) ExerciseMenuBackAndKeyboard(map, catalog.Count);
                    if (index % 2 == 0)
                        Require(shell.SelectAdminTravelOption(index), "The same production section action as mouse activation starts travel", catalog[index].Label);
                    else
                    {
                        Require(shell.MoveAdminTravelSelection(index - shell.AdminTravelSelection) || index == shell.AdminTravelSelection,
                            "Keyboard selection moves to the requested actual section", catalog[index].Label);
                        Require(shell.HandleAdminTravelKey(KeyCode.Return), "The production Enter handler starts selected section travel", catalog[index].Label);
                    }
                    yield return Wait(() => report.sceneLoadedEvents > previousEvents && Ready(scene) && !GameProgressRuntime.Paused, 90, "Selected section load " + scene + ":" + index);
                    var stage = CurrentStage();
                    AdminStageSectionsValidation.InspectCurrent(scene, index, stage, Require);
                    AdminStageSectionsValidation.ExerciseController(stage, Require);
                    Require(GameProgressRuntime.SaveCurrent(), "Explicit save in an administrator section uses the isolated admin-session slot", GameProgressRuntime.LastError);
                    Require(Hash(GameProgress.SavePath) == normalBefore && Hash(GameProgress.BackupPath) == normalBackupBefore &&
                        Hash(GameCompletionState.ProfilePath) == "<absent>" && !GameCompletionState.CreditsUnlocked,
                        "Section travel and admin saves preserve the normal Continue slot and earn no ending unlock", scene + ":" + index);
                    sections.Add(new Section { scene = scene, index = index, label = catalog[index].Label }); report.sectionLoads++;
                }
            }
            phase = "eclipse-ready";
            var game = FindFirstObjectByType<EclipseFinaleGame>();
            EclipseAdminSectionValidation.RunReady(game, Require);
            GameProgressRuntime.Resume();
            Require(game.InputEnabled, "The public resume path releases the administrator post-credits section");
            yield return TraverseExit(game);
            phase = "completed";
        }
        IEnumerator StartupSequence()
        {
            phase = "startup-request"; report.expectedSectionLoads = 1;
            SeedNormalSlot();
            yield return Wait(() => AdminStageStartup.Ready || !string.IsNullOrEmpty(AdminStageStartup.Failure), 110, "Generic administrator startup request");
            Require(AdminStageStartup.Requested && AdminStageStartup.Ready && string.IsNullOrEmpty(AdminStageStartup.Failure) &&
                AdminStageStartup.Scene == "Gallery" && AdminStageStartup.Section == 4 &&
                ParallaxGameShell.Administrator && GameProgressRuntime.TestSession && Ready("Gallery") && !GameProgressRuntime.Paused,
                "The actual generic startup request loads Gallery's last authored section with admin input available", AdminStageStartup.Failure);
            var stage = CurrentStage();
            AdminStageSectionsValidation.InspectCurrent("Gallery", 4, stage, Require);
            AdminStageSectionsValidation.ExerciseController(stage, Require);
            Require(Hash(GameProgress.SavePath) == normalBefore && Hash(GameProgress.BackupPath) == normalBackupBefore &&
                !GameCompletionState.CreditsUnlocked && Hash(GameCompletionState.ProfilePath) == "<absent>",
                "Last-section startup preserves the normal slot and creates no completion unlock");
            sections.Add(new Section { scene = "Gallery", index = 4, label = AdminStageSections.ForStage("Gallery")[4].Label }); report.sectionLoads++;
            phase = "startup-completed";
        }
        void ExerciseMenuBackAndKeyboard(int map, int sectionCount)
        {
            Require(shell.HandleAdminTravelKey(KeyCode.UpArrow) && shell.AdminTravelSelection == sectionCount &&
                shell.HandleAdminTravelKey(KeyCode.DownArrow) && shell.AdminTravelSelection == 0,
                "Up and Down wrap within this child list including Back");
            Require(shell.HandleAdminTravelKey(KeyCode.Tab, true) && shell.AdminTravelSelection == sectionCount &&
                shell.HandleAdminTravelKey(KeyCode.Tab) && shell.AdminTravelSelection == 0,
                "Tab and Shift-Tab use the same bounded child selection");
            Require(shell.HandleAdminTravelKey(KeyCode.Escape) && shell.AdminTravelMenuOpen && !shell.AdminTravelSectionsOpen &&
                shell.AdminTravelSelection == map && GameProgressRuntime.Paused,
                "One Escape returns to the parent map and preserves its selection without resuming");
            Require(shell.HandleAdminTravelKey(KeyCode.Space) && shell.AdminTravelSectionsOpen,
                "Space on the preserved parent selection reopens that map's child list");
            Require(shell.SelectAdminTravelOption(sectionCount) && !shell.AdminTravelSectionsOpen && shell.AdminTravelSelection == map,
                "The child Back button returns through the same production action");
            Require(shell.BackAdminTravelMenu() && !shell.AdminTravelMenuOpen && GameProgressRuntime.Paused && ParallaxGameShell.ShouldShowPauseMenu,
                "Parent Back returns to the pause menu and keeps gameplay paused");
            Require(shell.OpenAdminTravelMenu() && shell.SelectAdminTravelOption(map) && shell.AdminTravelSectionsOpen,
                "The administrator can reopen the same child after returning to pause");
            report.menuMaps++;
        }
        void InspectMenuBounds()
        {
            Rect panel = shell.AdminTravelPanelBounds;
            Require(panel.xMin >= 0 && panel.yMin >= 0 && panel.xMax <= 1920 && panel.yMax <= 1080,
                "The actual menu panel fits the 1920-by-1080 reference canvas", panel.ToString());
            var font = Resources.Load<Font>("Preview/BodyFont");
            Require(font != null, "Menu labels use the actual shipped body font");
            float lastBottom = panel.yMin;
            for (int i = 0; i < shell.AdminTravelOptionCount; i++)
            {
                Rect row = shell.AdminTravelOptionBounds(i); string label = shell.AdminTravelOptionLabel(i);
                Require(row.xMin >= panel.xMin && row.xMax <= panel.xMax && row.yMin > lastBottom && row.yMax <= panel.yMax - 69,
                    "Every actual menu hit rectangle remains inside the panel above its footer", label + " " + row);
                lastBottom = row.yMax;
                foreach (int percent in new[] { 90, 100, 110, 120 })
                {
                    int size = Mathf.RoundToInt(32 * percent / 100f); font.RequestCharactersInTexture(label, size, FontStyle.Normal);
                    float width = 0; int top = 0, bottom = 0; bool glyphs = true;
                    foreach (char character in label)
                    {
                        if (!font.GetCharacterInfo(character, out CharacterInfo info, size, FontStyle.Normal)) { glyphs = false; continue; }
                        width += info.advance; top = Mathf.Max(top, info.maxY); bottom = Mathf.Min(bottom, info.minY);
                    }
                    report.maximumLabelWidth = Mathf.Max(report.maximumLabelWidth, width);
                    Require(glyphs && width <= row.width && top - bottom <= row.height,
                        "The real font label metrics fit at each supported text-size preference", percent + "% " + label + " width=" + width + " height=" + (top-bottom));
                }
            }
        }
        IEnumerator TraverseExit(EclipseFinaleGame game)
        {
            const float dt = 1f / 60;
            phase = "post-credits-exit";
            Vector3 sourcePosition = game.exitSign.position, sourceScale = game.exitSign.localScale;
            game.Teleport(game.exitSign.position + new Vector3(0, .04f, -2));
            game.AimAt(game.exitSign.TransformPoint(new Vector3(0, .5f, 0))); game.Interact();
            Require(game.held == FinaleItem.Exit, "The administrator post-credits section uses the actual EXIT grab interaction", "Declared feet/view fixture; no sign or Moon transform setter");
            game.Cancel();
            Require(game.held == FinaleItem.None && game.exitSign.position == sourcePosition && game.exitSign.localScale == sourceScale,
                "The actual EXIT cancel path restores the source sign pose");
            game.AimAt(game.exitSign.TransformPoint(new Vector3(0, .5f, 0))); game.Interact();
            Require(game.held == FinaleItem.Exit, "The original EXIT can be picked up again after cancellation");
            game.AimAt(new Vector3(0, 2.6f, 65));
            for (int i = 0; i < 130; i++) { game.Tick(dt, Vector2.zero, false); if (i % 20 == 0) yield return null; }
            Require(game.DoorFits, "Production perspective enlargement creates a capsule-sized real i opening", "scale=" + game.ExitScale);
            Require(game.Drop(), "The enlarged EXIT is released through the real drop lifecycle");
            for (int i = 0; i < 500 && game.ExitFalling; i++) { game.Tick(dt, Vector2.zero, false); if (i % 20 == 0) yield return null; }
            Require(!game.ExitFalling, "The released EXIT settles before physical traversal");
            Vector3 approach = game.exitSign.TransformPoint(new Vector3(EclipseFinaleWorld.DoorX, 0, -.65f));
            int walk = 0;
            while (Vector2.Distance(new Vector2(game.body.transform.position.x, game.body.transform.position.z), new Vector2(approach.x, approach.z)) > .14f)
            {
                if (++walk > 2400) throw new TimeoutException("The actual capsule could not reach the EXIT approach");
                Vector3 delta = approach - game.body.transform.position; delta.y = 0;
                game.AimAt(game.view.transform.position + delta.normalized * 20); game.Tick(dt, Vector2.up, false); report.exitWalkTicks++;
                if (walk % 15 == 0) yield return null;
            }
            game.AimAt(game.exitSign.TransformPoint(new Vector3(EclipseFinaleWorld.DoorX, .45f, 2)));
            bool crossed = false;
            for (int i = 0; i < 360 && !game.EndingSequenceActive; i++)
            {
                game.Tick(dt, Vector2.up, false); report.exitWalkTicks++;
                crossed |= game.exitSign.InverseTransformPoint(game.body.transform.position).z >= -.015f;
                if (i % 10 == 0) yield return null;
            }
            Require(crossed && game.ExitPresentation.Phase == EclipseExitPhase.Enter && !game.InputEnabled && !game.QuitRequested,
                "The real CharacterController crosses the physical i-opening and starts the gradual EXIT presentation", "ticks=" + report.exitWalkTicks);
            int steps = 0; bool blackThanks = false, blackHold = false;
            while (!game.ExitPresentation.TitleRequested)
            {
                game.Tick(dt, Vector2.one, true);
                blackThanks |= game.ExitPresentation.Phase == EclipseExitPhase.BlackThanks;
                blackHold |= game.ExitPresentation.Phase == EclipseExitPhase.BlackHold;
                if (++steps > 1000) throw new TimeoutException("The real EXIT presentation did not request MainMenu");
                if (game.ExitPresentation.TitleRequested) break;
                if (steps % 15 == 0) yield return null;
            }
            Require(blackThanks && blackHold && !game.QuitRequested,
                "The real post-credits EXIT path preserves the closing thank-you and silent hold before title return");
            yield return Wait(() => SceneManager.GetActiveScene().name == "MainMenu" && !GameProgressRuntime.Transitioning, 30, "Natural administrator EXIT title return");
            Require(GameProgressRuntime.TestSession && GameCompletionState.CreditsUnlocked && Hash(GameCompletionState.ProfilePath) == "<absent>" &&
                Hash(GameCompletionState.BackupPath) == "<absent>" && Hash(GameProgress.SavePath) == normalBefore && Hash(GameProgress.BackupPath) == normalBackupBefore,
                "Natural administrator EXIT returns to MainMenu with only a session unlock and no normal save or profile writes");
        }
    }
}
