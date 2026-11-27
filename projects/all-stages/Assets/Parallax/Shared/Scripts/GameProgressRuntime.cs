using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;
using ShadeLink.Eclipse;

namespace ShadeLink
{
    public interface IGameProgressStage
    {
        bool ProgressReady { get; }
        bool ProgressPaused { get; }
        bool ProgressTransitioning { get; }
        GameProgressRecord CaptureProgress();
        void RestoreProgress(GameProgressRecord record);
        void SetProgressPaused(bool paused);
        void RestartProgressCheckpoint();
    }

    public static class GameProgressRuntime
    {
        static GameProgressRunner runner;
        static string actionError;
        static bool failNextLoadAfterFade;
        static float summaryUntil = -1;
        static bool cachedHasSave;
        static string cachedSummary = "저장된 진행이 없습니다";
        static void InvalidateSummary() { summaryUntil = -1; }
        static void RefreshSummary()
        {
            if (Time.realtimeSinceStartup < summaryUntil) return;
            summaryUntil = Time.realtimeSinceStartup + .75f;
            cachedHasSave = GameProgress.TryLoad(out var saved);
            if (!cachedHasSave) { cachedSummary = "저장된 진행이 없습니다"; return; }
            string name = saved.stage == "Tutorial" ? "튜토리얼" : saved.stage == "Gallery" ? "미술관" : saved.stage == "Beach" ? "해변" : "일식";
            int section = saved.stage == "Tutorial" ? saved.tutorialStage : saved.stage == "Gallery" ? saved.galleryRoom + 1 : saved.checkpoint + 1;
            cachedSummary = name + " · " + section.ToString("00") + " 구간" + (GameProgress.RecoveredBackup ? " · 백업 복구" : "");
        }
        static GameProgressRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var root = new GameObject("Parallax checkpoint persistence");
                    UnityEngine.Object.DontDestroyOnLoad(root);
                    runner = root.AddComponent<GameProgressRunner>();
                }
                return runner;
            }
        }
        public static bool GameplayActive => Runner.Stage != null && Runner.Stage.ProgressReady;
        public static bool Paused => Runner.Stage != null && Runner.Stage.ProgressPaused;
        // Menu-driven loads own the shell's loading presentation. Natural
        // arrivals keep their own camera/animation and must not create an
        // input lock that prevents the player from completing that arrival.
        public static bool ShellTransitioning => Runner.Loading && !Runner.NaturalLoading;
        public static bool SceneLoading => Runner.Loading || StageTravelMenu.Loading || TutorialMuseumElevatorFlow.ArrivalPending;
        // Saving and menu actions remain protected throughout stage-owned handoffs.
        public static bool Transitioning => SceneLoading || (Runner.Stage != null && Runner.Stage.ProgressTransitioning);
        public static string CurrentStage => SceneManager.GetActiveScene().name;
        public static bool TestSession => Runner.TestSession;
        public static bool HasSave { get { RefreshSummary(); return cachedHasSave; } }
        public static string LastError => actionError ?? GameProgress.LastError;
        public static string SaveSummary
        {
            get
            {
                RefreshSummary(); return cachedSummary;
            }
        }
        public static bool SetSaveDirectoryForValidation(string directory)
        { bool result = GameProgress.SetSaveDirectoryForValidation(directory); InvalidateSummary(); return result; }
        public static GameProgressRecord PeekSavedRecord() => GameProgress.TryLoad(out var saved) ? saved.Copy() : null;
        public static bool RequestLoadFailureAfterFadeForValidation()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-player-shell-validate") < 0 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-game-progress-validate") < 0 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-validate") < 0) return false;
            failNextLoadAfterFade = true;
            return true;
        }
        public static void NewGame() { actionError = null; GlassTrial.HelpHudController.RequestTutorialIntro(); Runner.BeginLoad("Tutorial", null, false, true); }
        public static void ContinueGame()
        {
            actionError = null;
            if (!GameProgress.TryLoad(out var saved)) { Error(GameProgress.LastError, false); return; }
            Runner.BeginLoad(saved.stage, saved, false, false);
        }
        public static void AdminTravel(string scene) => AdminTravel(scene, 0);
        public static bool AdminTravel(string scene, int sectionIndex)
        {
            actionError = null;
            if (!global::ParallaxGameShell.Administrator)
            { Error("관리자 모드에서만 맵을 이동할 수 있습니다.", false); return false; }
            if (!AdminStageSections.IsValid(scene, sectionIndex))
            { Error("선택한 맵의 구간이 없습니다.", false); return false; }
            if (Transitioning || StageTravelMenu.Loading)
            { Error("맵 이동이 끝난 뒤 다시 선택해 주세요.", false); return false; }
            GameProgressRecord record;
            try { record = AdminStageSections.CreateRecord(scene, sectionIndex); }
            catch (Exception error) { Error(error.Message, false); return false; }
            Runner.BeginLoad(scene, record, true, false, sectionIndex);
            return true;
        }
        // The completed ship already owns the black departure fade. Keep its
        // natural handoff separate from the menu's Loading... / Tip presentation.
        public static bool EnterEclipseFromBeach() => Runner.BeginBeachEnding();
        public static bool SaveCurrent()
        {
            actionError = null;
            if (Transitioning || Runner.Stage == null || !Runner.Stage.ProgressReady)
            { Error("맵 이동 중에는 저장할 수 없습니다.", false); return false; }
            var record = Runner.CaptureCurrent();
            bool result = Runner.TestSession ? GameProgress.TrySaveAdminSession(record) : GameProgress.TrySave(record);
            if (result) { Runner.ObserveCurrent(); InvalidateSummary(); }
            else Error(GameProgress.LastError, false);
            return result;
        }
        public static void Pause() { if (!Transitioning) Runner.Stage?.SetProgressPaused(true); }
        public static void Resume() { if (!Transitioning) Runner.Stage?.SetProgressPaused(false); }
        public static void RestartCheckpoint()
        {
            if (Transitioning || Runner.Stage == null) return;
            Runner.Stage.RestartProgressCheckpoint(); Runner.Stage.SetProgressPaused(false);
        }
        public static void ReturnToTitle() { actionError = null; InvalidateSummary(); Runner.BeginTitle(); }
        static bool failNextEndingTitleForValidation;
        public static bool FailNextEndingTitleForValidation()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-validate") < 0 ||
                GameProgress.IsUnisolatedValidation) return false;
            failNextEndingTitleForValidation = true; return true;
        }
        public static bool FinishEclipseEnding()
        {
            var eclipse = Runner.Stage as ShadeLink.Eclipse.EclipseFinaleGame;
            if (eclipse == null || !eclipse.EndingReturnReady) return false;
            actionError = null; InvalidateSummary(); return Runner.BeginEndingTitle();
        }
        internal static void Error(string message, bool log = true)
        {
            actionError = message;
            global::ParallaxGameShell.Instance?.Notify(message);
            if (log) Debug.LogError("GAME_PROGRESS: " + message);
        }

        internal sealed class GameProgressRunner : MonoBehaviour
        {
            public IGameProgressStage Stage { get; private set; }
            public bool Loading { get; private set; }
            public bool NaturalLoading { get; private set; }
            public bool TestSession { get; private set; }
            UnityEngine.Object stageObject;
            string scene, observedKey;
            bool swordEncountered;
            float nextProbe, nextAutoSave;
            void Awake() { SceneManager.sceneLoaded += Loaded; RefreshStage(); }
            void OnDestroy() { SceneManager.sceneLoaded -= Loaded; }
            void Loaded(Scene loaded, LoadSceneMode mode) { scene = null; Stage = null; stageObject = null; observedKey = null; }
            void RefreshStage()
            {
                scene = SceneManager.GetActiveScene().name;
                if (scene == "Tutorial") stageObject = FindFirstObjectByType<TutorialGame>();
                else if (scene == "Gallery") stageObject = FindFirstObjectByType<GalleryGame>();
                else if (scene == "Beach") stageObject = FindFirstObjectByType<BeachGame>();
                else if (scene == "Eclipse") stageObject = FindFirstObjectByType<EclipseFinaleGame>();
                else stageObject = null;
                Stage = stageObject as IGameProgressStage;
                nextProbe = Time.unscaledTime + .1f;
            }
            public GameProgressRecord CaptureCurrent()
            {
                if (Stage == null || !Stage.ProgressReady) return null;
                var record = Stage.CaptureProgress();
                swordEncountered |= record.swordEncountered;
                record.swordEncountered = swordEncountered;
                return record;
            }
            public void ObserveCurrent() { var record = CaptureCurrent(); if (record != null) observedKey = record.CheckpointKey; }
            void LateUpdate()
            {
                if (scene != SceneManager.GetActiveScene().name || stageObject == null && Time.unscaledTime >= nextProbe) RefreshStage();
                if (Loading || Stage == null || !Stage.ProgressReady || Stage.ProgressTransitioning ||
                    StageTravelMenu.Loading || TutorialMuseumElevatorFlow.ArrivalPending) return;
                var record = CaptureCurrent();
                // Sandbox encounters remain local. The existing write guards
                // protect the continuing-game slot from test sessions.
                if (GameProgress.IsUnisolatedValidation || TestSession || !global::ParallaxGameShell.OwnsPauseMenus) return;
                if (record.CheckpointKey == observedKey || Time.unscaledTime < nextAutoSave) return;
                if (GameProgress.TrySave(record)) { observedKey = record.CheckpointKey; InvalidateSummary(); Debug.Log("GAME_PROGRESS_AUTOSAVED " + observedKey); }
                else { nextAutoSave = Time.unscaledTime + 5; Error(GameProgress.LastError, false); }
            }
            public void BeginLoad(string target, GameProgressRecord restore, bool test, bool fresh, int adminSectionIndex = -1)
            {
                if (Loading || StageTravelMenu.Loading || TutorialMuseumElevatorFlow.ArrivalPending ||
                    Stage != null && Stage.ProgressTransitioning) return;
                if (target != "Tutorial" && target != "Gallery" && target != "Beach" && target != "Eclipse")
                { Error("이동할 맵이 올바르지 않습니다."); return; }
                if (!Application.CanStreamedLevelBeLoaded(target)) { Error("맵을 실행본에서 찾을 수 없습니다: " + target); return; }
                StartCoroutine(Load(target, restore, test, fresh, false, adminSectionIndex));
            }
            public bool BeginBeachEnding()
            {
                if (Loading || StageTravelMenu.Loading || TutorialMuseumElevatorFlow.ArrivalPending ||
                    SceneManager.GetActiveScene().name != "Beach") return false;
                var beach = FindFirstObjectByType<BeachGame>();
                if (beach == null || beach.skyShip == null || !beach.skyShip.Flying ||
                    beach.skyShip.FlightTime < BeachSkyShip.FlightDuration) return false;
                if (!Application.CanStreamedLevelBeLoaded("Eclipse"))
                { Error("일식 맵을 실행본에서 찾을 수 없습니다.", false); return false; }
                // Capture the last source checkpoint before its scene is disposed.
                // This updates only session history; the existing slot stays intact
                // until the destination's safe checkpoint has been constructed.
                CaptureCurrent();
                StartCoroutine(Load("Eclipse", null, TestSession, false, true));
                return true;
            }
            // Advance visual iterators here so their exceptions cannot leave a
            // persistent black overlay or a permanent input/loading lock.
            IEnumerator RunLoadingVisual(IEnumerator routine, Action<Exception> failed)
            {
                var stack = new System.Collections.Generic.Stack<IEnumerator>();
                if (routine != null) stack.Push(routine);
                try
                {
                    while (stack.Count > 0)
                    {
                        bool advance = false; object yielded = null; Exception error = null;
                        try
                        {
                            advance = stack.Peek().MoveNext();
                            if (advance) yielded = stack.Peek().Current;
                        }
                        catch (Exception caught) { error = caught; }
                        if (error != null) { failed(error); yield break; }
                        if (!advance)
                        {
                            var finished = stack.Pop() as IDisposable;
                            if (finished != null) finished.Dispose();
                            continue;
                        }
                        if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                        yield return yielded;
                    }
                }
                finally
                {
                    while (stack.Count > 0)
                    {
                        try { (stack.Pop() as IDisposable)?.Dispose(); }
                        catch (Exception error) { Debug.LogWarning("LOADING_VISUAL_DISPOSE: " + error.Message); }
                    }
                }
            }
            IEnumerator Load(string target, GameProgressRecord restore, bool test, bool fresh, bool natural = false, int adminSectionIndex = -1)
            {
                NaturalLoading = natural; Loading = true;
                bool completed = false, recoverTitle = false;
                var shell = global::ParallaxGameShell.Instance;
                try
                {
                    Stage?.SetProgressPaused(true);
                    Exception visualFailure = null;
                    if (shell != null && !natural)
                    {
                        yield return RunLoadingVisual(shell.BeginLoadingTransition(target, restore != null && adminSectionIndex < 0, restore != null && restore.swordEncountered), error => visualFailure = error);
                        if (visualFailure != null) { Error("로딩 화면을 준비하지 못했습니다: " + visualFailure.Message); yield break; }
                    }
                    if (failNextLoadAfterFade)
                    {
                        failNextLoadAfterFade = false;
                        Error("검증용 로딩 실패를 재현했습니다.", false);
                        Debug.Log("LOADING_VALIDATION_FAILURE_INJECTED");
                        yield break;
                    }
                    if (!StageTravelMenu.LoadStage(target, true))
                    { Error(StageTravelMenu.LastFailure ?? "맵 이동을 시작하지 못했습니다."); yield break; }
                    float deadline = Time.realtimeSinceStartup + 90;
                    float reported = 0;
                    while (StageTravelMenu.Loading && Time.realtimeSinceStartup < deadline)
                    {
                        reported = Mathf.Max(reported, StageTravelMenu.SceneLoadProgress);
                        if (shell != null) shell.ReportLoadingProgress(reported);
                        yield return null;
                    }
                    yield return null;
                    RefreshStage();
                    if (StageTravelMenu.Loading || StageTravelMenu.LastFailure != null || Stage == null || !Stage.ProgressReady)
                    { Error(StageTravelMenu.LastFailure ?? "맵의 체크포인트를 준비하지 못했습니다."); yield break; }

                    string failure = null;
                    try
                    {
                        Stage.SetProgressPaused(true);
                        if (restore != null) Stage.RestoreProgress(restore);
                        Stage.SetProgressPaused(true);
                    }
                    catch (Exception error) { failure = "저장된 구간을 복구하지 못했습니다: " + error.Message; }
                    if (failure != null) { recoverTitle = true; Error(failure); yield break; }
                    TestSession = test;
                    if (adminSectionIndex >= 0)
                    {
                        bool selected = false;
                        try { selected = test && AdminStageSections.ApplyAfterLoad(Stage, adminSectionIndex); }
                        catch (Exception error) { Error("선택한 구간을 준비하지 못했습니다: " + error.Message); }
                        if (!selected)
                        { recoverTitle = true; Error("선택한 구간의 시작 상태를 준비하지 못했습니다.", false); yield break; }
                    }
                    // Explicit new/admin loads begin a separate history. Natural
                    // stage routes keep the runner and its encounter history.
                    if (!natural) swordEncountered = restore != null && restore.swordEncountered;
                    ObserveCurrent();
                    if (shell != null && !natural)
                    {
                        shell.ReportLoadingProgress(1);
                        visualFailure = null;
                        yield return RunLoadingVisual(shell.FinishLoadingTransition(), error => visualFailure = error);
                        if (visualFailure != null) { recoverTitle = true; Error("로딩 화면을 마무리하지 못했습니다: " + visualFailure.Message); yield break; }
                        // Finish leaves a transparent Completed frame until the
                        // owner releases the runtime lock in this same turn.
                        shell.EndLoadingTransition();
                    }
                    Stage.SetProgressPaused(false);
                    Loading = false; NaturalLoading = false; completed = true;
                    if ((fresh || target == "Eclipse" && restore == null) && !test) SaveCurrent();
                    Debug.Log("GAME_PROGRESS_STAGE_READY " + target + " test=" + test + " restored=" + (restore != null) + " adminSection=" + adminSectionIndex);
                }
                finally
                {
                    if (!completed)
                    {
                        if (shell != null) shell.CancelLoadingTransition();
                        Loading = false; NaturalLoading = false;
                        if (natural && SceneManager.GetActiveScene().name == "Beach")
                        {
                            var sourceBeach = FindFirstObjectByType<BeachGame>();
                            if (sourceBeach != null && sourceBeach.skyShip != null) sourceBeach.skyShip.FailEclipseTransition();
                        }
                        if (recoverTitle) BeginTitle();
                    }
                }
            }

            public void BeginTitle()
            {
                if (Loading || StageTravelMenu.Loading || Stage != null && Stage.ProgressTransitioning) return;
                if (!Application.CanStreamedLevelBeLoaded("MainMenu")) { Error("시작화면을 실행본에서 찾을 수 없습니다."); return; }
                StartCoroutine(ToTitle());
            }
            public bool BeginEndingTitle()
            {
                if (Loading || StageTravelMenu.Loading || !(Stage is ShadeLink.Eclipse.EclipseFinaleGame eclipse) ||
                    !eclipse.EndingReturnReady || !Application.CanStreamedLevelBeLoaded("MainMenu")) return false;
                global::ParallaxGameShell.Instance?.ArmEndingTitleReveal();
                StartCoroutine(ToTitle(true)); return true;
            }
            IEnumerator ToTitle(bool natural = false)
            {
                NaturalLoading = natural;
                Loading = true; Stage?.SetProgressPaused(true);
                if (global::ParallaxGameShell.Instance != null) yield return new WaitForSecondsRealtime(.10f);
                Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (TutorialMuseumElevatorFlow.Instance != null) Destroy(TutorialMuseumElevatorFlow.Instance.gameObject);
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (!global::ParallaxGameShell.IsUiFeedbackSource(source)) source.Stop();
                AsyncOperation load = null;
                bool injected = natural && failNextEndingTitleForValidation;
                if (injected)
                {
                    failNextEndingTitleForValidation = false;
                    Error("검증용 시작화면 복귀 실패를 재현했습니다.", false);
                    Debug.Log("ENDING_TITLE_VALIDATION_FAILURE_INJECTED");
                }
                else
                {
                    try { load = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single); }
                    catch (Exception error) { Error(error.Message); }
                }
                if (load != null) while (!load.isDone) yield return null;
                yield return null;
                if (SceneManager.GetActiveScene().name != "MainMenu")
                {
                    Loading = false; NaturalLoading = false;
                    RefreshStage();
                    if (natural && Stage is ShadeLink.Eclipse.EclipseFinaleGame failedEnding)
                        failedEnding.ExitPresentation?.AbortTitleReturn();
                    else Stage?.SetProgressPaused(true);
                    yield break;
                }
                if (natural) GameCompletionState.MarkEndingSeen(TestSession);
                Stage = null; stageObject = null; observedKey = null; scene = SceneManager.GetActiveScene().name;
                Loading = false; NaturalLoading = false;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (!string.IsNullOrEmpty(actionError)) global::ParallaxGameShell.Instance?.Notify(actionError);
            }
        }
    }
}
