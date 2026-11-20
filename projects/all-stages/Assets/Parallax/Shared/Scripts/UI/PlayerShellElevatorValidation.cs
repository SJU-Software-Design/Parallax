using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;

// Explicit isolated native test. Tutorial pose/lesson are disclosed setup fixtures;
// automatic ride timing and the real scene loader perform the natural transfer.
// Gallery uses its production Tick with the real CharacterController after the
// live Shell input gate is checked. No OS key dispatch or GUI pixels are tested.
public static class PlayerShellElevatorValidation
{
    public const string Scope = "Live Shell, real NewGame scene load, public lesson-three restore fixture, sheltered approach and CharacterController boarding, automatic elevator phases, actual natural Gallery scene transfer, loading/pause presentation policy, closed-door collision, production Gallery Tick exit, pause/save/title APIs. Tutorial Update is disabled only to suppress raw input during disclosed fixture movement; Gallery automated mode uses native Tick. No OS-input dispatch, capture, ReadPixels or explicit Camera.Render call.";
    [Serializable] public sealed class Sample
    {
        public double time;
        public string scene, phase;
        public bool sceneLoading, shellTransitioning, transitioning, overlay, pauseMenu, blocked, arrival;
        public float door, z;
    }
    [Serializable] public sealed class Report
    {
        public string scope = Scope;
        public int captureCount, departures, naturalLoadSamples, arrivalSamples, closedSamples, openSamples;
        public bool renderedPixelsVerified;
        public List<string> violations = new List<string>();
        public List<Sample> samples = new List<Sample>();
    }
    public static Report LatestReport { get; private set; }
    sealed class Probe
    {
        public readonly Report report = new Report();
        public TutorialElevatorRide ride;
        public GalleryGame gallery;
        public bool natural;
        string lastKey;
        void Violation(string detail) { if (!report.violations.Contains(detail)) report.violations.Add(detail); }
        public void Observe()
        {
            var flow = TutorialMuseumElevatorFlow.Instance;
            if (gallery == null && flow != null) gallery = flow.CurrentGallery;
            bool arrival = gallery != null && gallery.ElevatorArrival;
            var sample = new Sample { time = Time.realtimeSinceStartupAsDouble, scene = SceneManager.GetActiveScene().name,
                phase = ride == null ? "<none>" : ride.Phase.ToString(), door = ride == null ? -1 : ride.DoorOpenFraction,
                sceneLoading = GameProgressRuntime.SceneLoading, shellTransitioning = GameProgressRuntime.ShellTransitioning,
                transitioning = GameProgressRuntime.Transitioning, overlay = ParallaxGameShell.ShouldShowLoadingOverlay,
                pauseMenu = ParallaxGameShell.ShouldShowPauseMenu, blocked = ParallaxGameShell.BlocksGameplay,
                arrival = arrival, z = gallery == null ? -999 : gallery.controller.transform.position.z };
            string key = sample.scene + sample.phase + sample.sceneLoading + sample.shellTransitioning + sample.transitioning + sample.overlay + sample.pauseMenu + sample.blocked + sample.arrival;
            if (lastKey != key) { report.samples.Add(sample); lastKey = key; }
            if (!natural) return;
            if (sample.overlay) Violation("Natural elevator route displayed the Shell loading overlay.");
            if (sample.pauseMenu) Violation("Natural elevator handoff flashed the pause presentation.");
            if (flow != null && flow.IsLoading)
            {
                report.naturalLoadSamples++;
                if (!sample.sceneLoading || !sample.blocked) Violation("Actual natural scene initialization released its input gate too early.");
            }
            if (arrival && flow != null && !flow.IsLoading && ride != null && ride.DestinationReady)
            {
                report.arrivalSamples++;
                if (ride.DoorOpenFraction < .01f) report.closedSamples++;
                if (ride.Phase == ElevatorPhase.Arrived) report.openSamples++;
                if (sample.sceneLoading || sample.shellTransitioning || sample.blocked || !sample.transitioning)
                    Violation("Ready arrival failed to separate movement permission from save/menu protection.");
            }
        }
    }
    static IEnumerator Wait(Func<bool> ready, float seconds, string description, Probe probe)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!ready())
        {
            probe.Observe();
            if (Time.realtimeSinceStartupAsDouble > deadline)
                throw new TimeoutException(description + "; state=" + JsonUtility.ToJson(probe.report));
            yield return null;
        }
        probe.Observe();
    }
    static void SetFixtureFeet(CharacterController controller, Vector3 feet)
    {
        bool enabled = controller.enabled; controller.enabled = false;
        controller.transform.position = feet; controller.enabled = enabled;
        Physics.SyncTransforms();
    }
    static IEnumerator Walk(CharacterController controller, Vector3 target, Probe probe)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 8;
        while (Vector2.Distance(new Vector2(controller.transform.position.x, controller.transform.position.z), new Vector2(target.x, target.z)) > .08f)
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Real controller did not reach elevator approach/boarding target; feet=" + controller.transform.position + " target=" + target);
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, .001f, .05f);
            Vector3 delta = target - controller.transform.position; delta.y = 0;
            controller.Move(Vector3.ClampMagnitude(delta, 2.4f * dt) + Vector3.down * .04f);
            probe.Observe(); yield return null;
        }
        probe.Observe();
    }
    public static IEnumerator Run(Action<bool, string, string> check)
    {
        if (check == null) throw new ArgumentNullException(nameof(check));
        check(Array.IndexOf(Environment.GetCommandLineArgs(), "-player-shell-validate") >= 0 && !GameProgress.IsUnisolatedValidation,
            "Elevator integration uses the explicit isolated native validation process", Scope);
        var shell = ParallaxGameShell.Instance; var probe = new Probe(); LatestReport = probe.report;
        check(shell != null && SceneManager.GetActiveScene().name == "MainMenu", "Natural elevator regression starts with the live player Shell in MainMenu", "");
        GameProgressRuntime.NewGame();
        check(GameProgressRuntime.SceneLoading && GameProgressRuntime.ShellTransitioning && ParallaxGameShell.ShouldShowLoadingOverlay && ParallaxGameShell.BlocksGameplay,
            "Initial title-to-Tutorial load retains presentation and input protection", "");
        yield return Wait(() => GameProgressRuntime.CurrentStage == "Tutorial" && !GameProgressRuntime.SceneLoading && GameProgressRuntime.GameplayActive, 100, "Initial Tutorial load", probe);
        yield return null; yield return null;
        var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialGame>();
        check(tutorial != null && tutorial.ProgressReady && ParallaxGameShell.Instance == shell && !ParallaxGameShell.BlocksGameplay,
            "Actual Tutorial load releases input while keeping one persistent Shell", "");
        int initialLoadingId = shell.LoadingSequenceId;
        double initialDirectLoad = StageTravelMenu.SceneLoadCalledAt;
        bool tutorialWasEnabled = tutorial.enabled;
        TutorialElevatorRide ride = null;
        Action<TutorialElevatorRide> departed = null;
        Action<ElevatorPhase, ElevatorPhase> phaseChanged = null;
        var phases = new HashSet<ElevatorPhase>(); bool departedWithClosedDoors = false;
        try
        {
            tutorial.RestoreProgress(new GameProgressRecord { stage = "Tutorial", tutorialStage = 3, checkpoint = 2 });
            tutorial.enabled = false; // Fixture movement suppresses raw user input only; ride/flow/Shell remain live.
            ride = tutorial.MuseumElevator; probe.ride = ride;
            check(ride != null && ride.Asset != null && ride.AutomaticTick && !ride.Paused && TutorialMuseumElevatorFlow.Instance != null,
                "Public lesson-three restore supplies the real automatically timed elevator route", "No review/capture driver or phase setters");
            check(tutorial.CaptureProgress().tutorialStage == 3 && !tutorial.ElevatorDeparted, "Lesson fixture has not completed or departed before real boarding", "");
            phases.Add(ride.Phase);
            phaseChanged = (previous, next) => phases.Add(next);
            departed = current => { probe.report.departures++; departedWithClosedDoors = current.DoorOpenFraction <= .001f; probe.natural = true; };
            ride.PhaseChanged += phaseChanged; ride.DepartureReady += departed;
            Vector3 outside = ride.WorldEntry + ride.transform.forward * (ride.Asset.approachBounds.max.z + .7f - 1.4f);
            SetFixtureFeet(tutorial.Controller, outside);
            tutorial.Camera.transform.rotation = Quaternion.LookRotation(-ride.transform.forward);
            check(!ride.PlayerInApproach() && ride.Phase == ElevatorPhase.IdleClosed, "Disclosed exit-shelter fixture starts outside the actual approach trigger", "feet=" + tutorial.Controller.transform.position);
            yield return Walk(tutorial.Controller, ride.WorldEntry, probe);
            check(ride.PlayerInApproach() && !tutorial.ElevatorDeparted, "Real controller approach opens the elevator without completing the lesson", "phase=" + ride.Phase);
            yield return Wait(() => ride.Phase == ElevatorPhase.Boarding && ride.DoorOpenFraction >= .999f, 8, "Ordinary boarding doors", probe);
            yield return Walk(tutorial.Controller, ride.WorldBoardingCenter, probe);
            check(ride.PlayerWhollyInside() && !ride.DoorwayObstructed(), "Actual controller boarding clears the complete capsule and doorway safety region", "localFeet=" + ride.transform.InverseTransformPoint(tutorial.Controller.transform.position));
            yield return Wait(() => TutorialMuseumElevatorFlow.Instance != null && TutorialMuseumElevatorFlow.Instance.CurrentGallery != null && !TutorialMuseumElevatorFlow.Instance.IsLoading && ride.DestinationReady, 100, "Natural Gallery scene transfer", probe);
            var flow = TutorialMuseumElevatorFlow.Instance; var gallery = flow.CurrentGallery; probe.gallery = gallery;
            yield return null; yield return null; probe.Observe();
            check(probe.report.departures == 1 && departedWithClosedDoors && phases.Contains(ElevatorPhase.ClosingForDeparture) && phases.Contains(ElevatorPhase.Descending),
                "Normal dwell and full door closure trigger exactly one real natural scene transfer", "departures=" + probe.report.departures);
            check(GameProgressRuntime.CurrentStage == "Gallery" && gallery != null && gallery.ElevatorArrival && gallery.active && ride == flow.ActiveRide && ParallaxGameShell.Instance == shell,
                "The live persistent cabin hands its actual player to Gallery arrival", "phase=" + ride.Phase);
            check(!GameProgressRuntime.SceneLoading && !GameProgressRuntime.ShellTransitioning && GameProgressRuntime.Transitioning &&
                !ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.ShouldShowPauseMenu && !ParallaxGameShell.BlocksGameplay,
                "Ready arrival keeps save protection but releases movement and both blocking presentations", "This predicate fails the original Shell movement deadlock before any native Tick");
            check(ride.DoorOpenFraction < .01f && ride.Phase != ElevatorPhase.Arrived, "Destination becomes ready while its ordinary stopping/chime/closed-door protection remains active", "phase=" + ride.Phase + " door=" + ride.DoorOpenFraction);
            gallery.Pause();
            check(!gallery.ProgressPaused && !GameProgressRuntime.Paused && Time.timeScale > 0 && !ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.ShouldShowPauseMenu,
                "Direct Gallery pause/focus path cannot stop an active natural arrival", "Public Pause API only; no OS focus or cursor manipulation");
            Vector3 closedStart = gallery.controller.transform.position;
            CollisionFlags closedContact = gallery.controller.Move(Vector3.forward * 3f); gallery.SyncView(); Physics.SyncTransforms();
            check((closedContact & CollisionFlags.Sides) != 0 && gallery.controller.transform.position.z < .65f && gallery.ElevatorArrival,
                "Real closed destination doors stop controller exit before the arrival threshold", "before=" + closedStart + " after=" + gallery.controller.transform.position + " collision=" + closedContact);
            check(!GameProgressRuntime.SaveCurrent(), "Arrival still rejects checkpoint saving before the player exits", GameProgressRuntime.LastError);
            GameProgressRuntime.Pause();
            check(!GameProgressRuntime.Paused && Time.timeScale > 0, "Transition guard prevents a pause command from stopping the natural arrival", "");
            yield return Wait(() => ride.Phase == ElevatorPhase.Arrived && ride.DoorOpenFraction >= .999f, 15, "Normal destination door opening", probe);
            check(!ParallaxGameShell.BlocksGameplay && !ParallaxGameShell.ShouldShowLoadingOverlay && gallery.ElevatorArrival,
                "Fully opened destination doors leave the Shell movement gate open before exit", "");
            check(ride.TransferCamera == null && gallery.view.enabled && UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled) == 1,
                "Natural camera/listener handoff finishes without retaining the transfer camera", "");
            gallery.yaw = gallery.pitch = 0; gallery.SyncView(); // Disclosed native look command towards the open exit.
            Vector3 exitStart = gallery.controller.transform.position;
            double exitDeadline = Time.realtimeSinceStartupAsDouble + 6;
            while (gallery.ElevatorArrival)
            {
                check(!ParallaxGameShell.BlocksGameplay, "Live Shell allows the production Gallery movement step during arrival", "");
                gallery.Tick(Mathf.Clamp(Time.unscaledDeltaTime, .001f, .05f), Vector2.up);
                probe.Observe();
                if (Time.realtimeSinceStartupAsDouble > exitDeadline) throw new TimeoutException("Production movement did not clear Gallery ElevatorArrival; " + JsonUtility.ToJson(probe.report));
                yield return null;
            }
            probe.Observe(); probe.natural = false;
            check(gallery.controller.transform.position.z > .65f && gallery.controller.transform.position.z > exitStart.z + .35f && !GameProgressRuntime.Transitioning,
                "Production Gallery Tick advances the real controller out and clears ElevatorArrival naturally", "start=" + exitStart + " end=" + gallery.controller.transform.position);
            check(shell.LoadingSequenceId == initialLoadingId && StageTravelMenu.SceneLoadCalledAt == initialDirectLoad,
                "Natural elevator route never invokes the manual loading presentation or stage-travel loader", "sequence=" + initialLoadingId);
            check(probe.report.naturalLoadSamples > 0 && probe.report.arrivalSamples > 0 && probe.report.closedSamples > 0 && probe.report.openSamples > 0 && probe.report.violations.Count == 0,
                "Observed natural initialization and arrival keep the intended loading, pause and input policy throughout", JsonUtility.ToJson(probe.report));
            GameProgressRuntime.Pause();
            check(GameProgressRuntime.Paused && ParallaxGameShell.ShouldShowPauseMenu && ParallaxGameShell.BlocksGameplay && !ParallaxGameShell.ShouldShowLoadingOverlay,
                "The production pause API behind Escape becomes available after elevator exit", "No physical key event is synthesized");
            check(GameProgressRuntime.SaveCurrent() && GameProgressRuntime.PeekSavedRecord()?.stage == "Gallery",
                "The ordinary paused Gallery checkpoint can save after actual elevator exit", GameProgressRuntime.LastError);
            GameProgressRuntime.Resume();
            check(!GameProgressRuntime.Paused && !ParallaxGameShell.BlocksGameplay, "Resume restores normal Gallery input after the pause/save check", "");
            GameProgressRuntime.ReturnToTitle();
            check(GameProgressRuntime.ShellTransitioning && ParallaxGameShell.ShouldShowLoadingOverlay && ParallaxGameShell.BlocksGameplay,
                "Explicit title return retains its own Shell loading/input protection", "");
            yield return Wait(() => GameProgressRuntime.CurrentStage == "MainMenu" && !GameProgressRuntime.SceneLoading, 30, "Saved title return", probe);
            yield return null; yield return null;
            check(TutorialMuseumElevatorFlow.Instance == null && ParallaxGameShell.Instance == shell && !ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.ShouldShowPauseMenu,
                "Title return disposes the completed elevator route and releases both transient presentations", "");
            Debug.Log("PLAYER_SHELL_ELEVATOR_VALIDATION " + JsonUtility.ToJson(probe.report));
        }
        finally
        {
            if (ride != null) { if (departed != null) ride.DepartureReady -= departed; if (phaseChanged != null) ride.PhaseChanged -= phaseChanged; }
            if (tutorial != null) tutorial.enabled = tutorialWasEnabled;
        }
    }
}