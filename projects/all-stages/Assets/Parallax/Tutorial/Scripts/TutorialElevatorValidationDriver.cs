using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Tutorial
{
    // Opt-in integration QA. Production phases/completion are never supplied by
    // the harness: actual controller movement and a real rigidbody obstruction
    // exercise the boarding/door code, followed by the real Gallery scene load.
    public sealed class TutorialElevatorValidationDriver : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class AudioEvidence
        {
            public string id, clip;
            public int frames, initialSample, lastSample, voiceId;
            public float maximumVolume;
            public bool progressed, afterGallery, loop;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string phase, failure, startedUtc, finishedUtc, unity, gpu, finalScene;
            public string scope = "Focused native/Editor integration QA of the actual E01 prefab, automatic ride states and actual asynchronous Tutorial-to-Gallery scene handoff. Review setup jumps to the existing final tutorial approach; actual CharacterController.Move enters the cabin, then a real kinematic rigidbody placed in the production safety bounds exercises reopening. Production pause/reset commands are invoked by method reflection. No completion, door phase, arrival flag or scene state is supplied. Gallery.Tick drives the actual destination motor through the open doorway. Camera.Render captures use disclosed inspection viewpoints and existing scene materials/lighting. This is not a continuous tutorial puzzle playthrough or subjective art/audio approval.";
            public float closedDoorDuration, feetPoseError, eyePoseError, viewPoseErrorDegrees, maximumShaftMotion;
            public float tutorialCaptureDeviation, shaftCaptureDeviation, arrivalCaptureDeviation, actualExitDistance;
            public int departureEvents, arrivalEvents, gallerySceneLoads, maximumActiveListeners, observedFrames;
            public Vector3 departureLocalFeet, arrivalLocalFeet, galleryExitFeet;
            public List<Check> checks = new List<Check>();
            public List<string> phases = new List<string>(), scenes = new List<string>(), captures = new List<string>(), errors = new List<string>(), handoffLogs = new List<string>();
            public List<AudioEvidence> actualAudio = new List<AudioEvidence>();
            public List<TutorialElevatorAudio.Playback> audioHistory = new List<TutorialElevatorAudio.Playback>();
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly Report report = new Report();
        TutorialGame tutorial;
        TutorialElevatorRide ride;
        GalleryGame gallery;
        TutorialElevatorRide.TransferPose departurePose;
        string reportPath, captureDirectory;
        double deadline;
        bool finished, descendingObserved, stoppingObserved, floorOneObserved, floorB1Observed, monitorListeners, completedAtDeparture;
        ElevatorPhase previousPhase;
        Transform shaftBand;
        Vector3 initialShaftPosition;
        double finalClosingBegan;
        GameObject blocker;
        TutorialElevatorAudio selectedAudio;
        int runVoiceAtDeparture;
        static readonly string[] AudioIds = { "EL02", "EL04", "EL07", "EL09", "EL11", "EL25", "EL21", "EL23" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tutorial-elevator-validate") < 0 ||
                UnityEngine.Object.FindFirstObjectByType<TutorialElevatorValidationDriver>() != null) return;
            var root = new GameObject("Tutorial elevator actual integration validation");
            DontDestroyOnLoad(root);
            root.AddComponent<TutorialElevatorValidationDriver>();
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            report.startedUtc = DateTime.UtcNow.ToString("o");
            report.unity = Application.unityVersion;
            report.gpu = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType;
            Application.runInBackground = true;
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-validation-output");
            string requested = at >= 0 && at + 1 < args.Length ? args[at + 1] :
                Path.Combine(Application.persistentDataPath, "TutorialElevatorValidation");
            reportPath = Path.GetFullPath(requested);
            if (!string.Equals(Path.GetExtension(reportPath), ".json", StringComparison.OrdinalIgnoreCase))
                reportPath = Path.Combine(reportPath, "tutorial-elevator-validation-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            captureDirectory = Path.Combine(Path.GetDirectoryName(reportPath), "captures");
            Directory.CreateDirectory(captureDirectory);
            if (File.Exists(reportPath))
            {
                Debug.LogError("Refusing to reuse tutorial elevator validation report: " + reportPath);
                Application.Quit(2);
                yield break;
            }
            Application.logMessageReceived += ObserveLog;
            SceneManager.sceneLoaded += ObserveScene;
            report.scenes.Add(SceneManager.GetActiveScene().name);
            deadline = Time.realtimeSinceStartupAsDouble + 220;

            var stack = new Stack<IEnumerator>();
            stack.Push(Sequence());
            while (stack.Count > 0)
            {
                object yielded = null;
                bool advance = true;
                try
                {
                    if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Elevator validation exceeded 220 real-time seconds.");
                    var current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); advance = false; }
                    else yielded = current.Current;
                }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!advance) continue;
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
            Finish();
        }

        void Update()
        {
            if (finished) return;
            report.observedFrames++;
            if (ride != null)
            {
                ObserveActualAudio();
                if (ride.Phase != previousPhase)
                {
                    previousPhase = ride.Phase;
                    report.phases.Add(ride.Phase + " at " + Time.realtimeSinceStartupAsDouble.ToString("F3"));
                }
                floorOneObserved |= ride.FloorLabel == "1";
                floorB1Observed |= ride.FloorLabel == "B1";
                descendingObserved |= ride.Phase == ElevatorPhase.Descending;
                stoppingObserved |= ride.Phase == ElevatorPhase.Stopping;
                if (shaftBand != null)
                    report.maximumShaftMotion = Mathf.Max(report.maximumShaftMotion,
                        Vector3.Distance(initialShaftPosition, shaftBand.localPosition));
            }
            if (monitorListeners)
            {
                int count = 0;
                foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.enabled && listener.gameObject.activeInHierarchy) count++;
                report.maximumActiveListeners = Mathf.Max(report.maximumActiveListeners, count);
            }
            if (gallery == null) gallery = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if (gallery != null) gallery.automated = true;
        }

        IEnumerator Sequence()
        {
            report.phase = "initialize actual saved tutorial and E01";
            CheckThat(SceneManager.GetActiveScene().name == "Tutorial", "Tutorial is initial scene", SceneManager.GetActiveScene().name);
            while ((tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialGame>()) == null || tutorial.Controller == null || tutorial.Camera == null)
                yield return null;
            report.phase = "actual original tutorial audio and first lesson baseline";
            yield return TutorialAudioBaselineValidation.Run(tutorial, CheckThat);
            report.phase = "actual wall, corner and near-door boarding";
            yield return TutorialElevatorBoardingValidation.Run(tutorial, CheckThat);
            report.phase = "initialize actual saved tutorial and E01";
            // The public review entry point changes only the disclosed setup pose/course.
            Invoke(tutorial, "JumpToElevatorReview");
            yield return null;
            while ((ride = UnityEngine.Object.FindFirstObjectByType<TutorialElevatorRide>()) == null || ride.Asset == null)
                yield return null;
            monitorListeners = true;
            var prefab = Resources.Load<GameObject>("Tutorial/ElevatorE01");
            CheckThat(prefab != null && prefab.GetComponent<ElevatorE01Asset>() != null,
                "Actual prepared E01 resource exists", "Resources/Tutorial/ElevatorE01");
            CheckThat(ride.Asset.doors != null && ride.Asset.doors.Length == 4,
                "Four independently movable door leaves", "Exterior pair and cabin pair retained from original model");
            foreach (var door in ride.Asset.doors)
                CheckThat(door != null && door.GetComponentsInChildren<MeshFilter>(true).Length > 0 &&
                    door.GetComponentsInChildren<Collider>(true).Length > 0,
                    "Door mesh and physical collider", door == null ? "null door" : door.name);
            var glass = ride.Asset.backGlass;
            CheckThat(glass != null && glass.sharedMaterial != null && glass.sharedMaterial.shader != null &&
                glass.sharedMaterial.shader.name == "Parallax/Elevator Frosted Glass" &&
                glass.sharedMaterial.color.a > .1f && glass.sharedMaterial.color.a < .95f,
                "Original rear panel uses translucent glass", glass == null ? "missing rear panel" : glass.sharedMaterial.name);
            selectedAudio = ride.SelectedAudio;
            CheckThat(selectedAudio != null && selectedAudio.AllClipsLoaded && selectedAudio.LoadedClipCount == 8 && selectedAudio.SourceCount == 7,
                "Eight selected clips and one shared door voice are present", "8 clips / 7 owned sources");
            foreach (string id in AudioIds)
                CheckThat(selectedAudio.ClipFor(id) != null && selectedAudio.ClipFor(id).loadState == AudioDataLoadState.Loaded,
                    "Selected resource is actually loaded", id);
            CheckThat(!TutorialComplete(), "Approach does not complete tutorial", "Completion remains false before closed-door departure");
            InspectPhysicalCabin();
            shaftBand = FindDescendant(ride.transform, "Passing shaft landing 0");
            CheckThat(shaftBand != null, "Shaft motion geometry exists", "Passing shaft landing 0");
            initialShaftPosition = shaftBand.localPosition;

            yield return MoveController(tutorial.Controller, ride.WorldEntry, 1.5f, 4);
            yield return WaitFor(() => ride.Phase == ElevatorPhase.OpeningForBoarding && ride.DoorOpenFraction > .12f,
                8, "Approach must open the actual doors");
            report.phase = "production pause and reset";
            Invoke(tutorial, "SetPaused", true);
            yield return null;
            yield return null;
            var pausedDoorSource = selectedAudio.SourceFor("EL02");
            int pausedDoorSample = pausedDoorSource.timeSamples;
            int pausedDoorPlays = selectedAudio.PlayCount("EL02");
            float pausedFraction = ride.DoorOpenFraction;
            Vector3 pausedShaft = shaftBand.localPosition;
            yield return Wait(.3f);
            CheckThat(ride.Paused && Mathf.Abs(ride.DoorOpenFraction - pausedFraction) < .002f &&
                Vector3.Distance(pausedShaft, shaftBand.localPosition) < .001f,
                "Production pause freezes elevator", "Paused through TutorialGame.SetPaused; no ride phase supplied");
            CheckThat(selectedAudio.AudioPaused && !pausedDoorSource.isPlaying && Mathf.Abs(pausedDoorSource.timeSamples - pausedDoorSample) < 2048,
                "Production pause holds actual door audio sample position", "Sample=" + pausedDoorSample + "→" + pausedDoorSource.timeSamples);
            Invoke(tutorial, "SetPaused", false);
            yield return WaitFor(() => ride.DoorOpenFraction > pausedFraction + .08f, 3, "Unpause must resume opening");
            CheckThat(!selectedAudio.AudioPaused && pausedDoorSource.isPlaying && pausedDoorSource.timeSamples > pausedDoorSample && selectedAudio.PlayCount("EL02") == pausedDoorPlays,
                "Door audio resumes without restarting its cue", "Actual samples advance; Play count unchanged");
            Invoke(tutorial, "RestartLesson");
            yield return null;
            CheckThat(ride.Phase == ElevatorPhase.IdleClosed && ride.DoorOpenFraction < .001f && ride.RideElapsed == 0 &&
                ride.FloorLabel == "1" && !ride.DestinationReady,
                "Production lesson reset restores idle elevator", "RestartLesson is the actual R-key command path");
            bool onlyVent = true;
            foreach (var voice in ride.GetComponentsInChildren<AudioSource>(true))
                if (voice.isPlaying && (voice.clip == null || !voice.clip.name.StartsWith("EL23", StringComparison.Ordinal))) onlyVent = false;
            CheckThat(onlyVent && selectedAudio.SourceFor("EL23").isPlaying,
                "Reset stops effects and leaves one ventilation loop", "Only EL23 remains playing");
            CheckThat(!TutorialComplete(), "Reset retains incomplete tutorial", "No departure has occurred");
            Invoke(tutorial, "JumpToElevatorReview");
            yield return null;
            yield return MoveController(tutorial.Controller, ride.WorldEntry, 1.5f, 4);
            yield return WaitFor(() => ride.Phase == ElevatorPhase.Boarding && ride.DoorOpenFraction > .999f, 8, "Doors must fully open for boarding");
            report.tutorialCaptureDeviation = Capture(tutorial.Camera, "01-elevator-open-approach.png",
                ride.transform.TransformPoint(new Vector3(.18f, 1.65f, 4.4f)), ride.transform.TransformPoint(new Vector3(0, 1.5f, -1)));

            report.phase = "partial boarding safety";
            Vector3 partial = ride.transform.TransformPoint(new Vector3(ride.Asset.cabinBounds.center.x,
                ride.Asset.floorY + .03f, ride.Asset.thresholdZ + .03f));
            yield return MoveController(tutorial.Controller, partial, 1.5f, 4);
            CheckThat(!ride.PlayerWhollyInside(), "Threshold standing is partial boarding", tutorial.Controller.transform.position.ToString("F3"));
            yield return Wait(.95f);
            CheckThat(ride.Phase == ElevatorPhase.Boarding && ride.DoorOpenFraction > .999f && ride.BoardingDwell < .05f && !TutorialComplete(),
                "Partial boarding keeps doors open", "Waited longer than the 0.75-second whole-body dwell");

            report.phase = "real obstruction reopens doors";
            yield return MoveController(tutorial.Controller, ride.WorldBoardingCenter, 1.5f, 5);
            CheckThat(ride.PlayerWhollyInside(), "Actual controller fully enters cabin", tutorial.Controller.transform.position.ToString("F3"));
            yield return WaitFor(() => ride.Phase == ElevatorPhase.ClosingForDeparture && ride.DoorOpenFraction < .82f,
                5, "Whole-body dwell must start real closing");
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Validation physical door safety obstruction";
            blocker.transform.position = ride.transform.TransformPoint(ride.Asset.doorSafetyBounds.center);
            blocker.transform.rotation = ride.transform.rotation;
            blocker.transform.localScale = Vector3.one * .23f;
            var rigidbody = blocker.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            Physics.SyncTransforms();
            CheckThat(ride.DoorwayObstructed(), "Actual safety volume detects rigidbody", "A real kinematic collider is placed in the production doorway bounds");
            yield return WaitFor(() => ride.Phase == ElevatorPhase.OpeningForBoarding, 2, "Obstructed doors must reverse to opening");
            yield return WaitFor(() => ride.Phase == ElevatorPhase.Boarding && ride.DoorOpenFraction > .999f, 4, "Reopened doors must fully open");
            yield return Wait(.3f);
            CheckThat(ride.Phase == ElevatorPhase.Boarding && !TutorialComplete(), "Obstruction prevents departure", "Doors remain open while real obstruction remains");
            Destroy(blocker); blocker = null;
            yield return null;
            yield return new WaitForFixedUpdate();

            ride.DepartureReady += ObserveDeparture;
            ride.ArrivedAtDestination += ObserveArrival;
            yield return WaitFor(() => ride.Phase == ElevatorPhase.ClosingForDeparture, 3, "Removing obstruction must allow a new whole-body dwell");
            finalClosingBegan = Time.realtimeSinceStartupAsDouble;
            yield return WaitFor(() => ride.Phase == ElevatorPhase.Descending, 5, "Doors must close before descent");
            CheckThat(ride.DoorOpenFraction < .001f && report.departureEvents == 1 && completedAtDeparture,
                "Closed doors trigger tutorial completion and one departure", "Door fraction=" + ride.DoorOpenFraction + ", events=" + report.departureEvents);
            CheckThat(report.closedDoorDuration >= ride.DoorDuration - .25f && report.closedDoorDuration <= ride.DoorDuration + .65f,
                "Closing uses intended slow duration", report.closedDoorDuration.ToString("F3") + " seconds");
            report.phase = "actual descent and Gallery scene handoff";
            yield return WaitFor(() => ride.RideElapsed > .7f && JourneyCamera() != null, 6, "Journey camera and descent must remain visible");
            Camera journeyCamera = JourneyCamera();
            report.shaftCaptureDeviation = Capture(journeyCamera, "02-frosted-wall-during-descent.png", null,
                ride.Asset.backGlass.bounds.center);
            CheckThat(journeyCamera.isActiveAndEnabled, "Journey camera stays visible through scene handoff", journeyCamera.name);
            CheckThat(selectedAudio.SourceFor("EL11").isPlaying && selectedAudio.SourceFor("EL11").timeSamples > 0 && selectedAudio.SourceFor("EL11").volume > .001f,
                "Actual running clip advances during descent", selectedAudio.SourceFor("EL11").clip.name);
            yield return WaitFor(() => ride.Phase == ElevatorPhase.OpeningAtDestination || ride.Phase == ElevatorPhase.Arrived,
                70, "Actual Gallery load, descent and stop must complete");
            gallery = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            CheckThat(SceneManager.GetActiveScene().name == "Gallery" && gallery != null && gallery.controller != null && gallery.view != null,
                "Actual Gallery scene loaded", SceneManager.GetActiveScene().name);
            gallery.automated = true;
            CheckThat(UnityEngine.Object.FindFirstObjectByType<TutorialGame>() == null,
                "Tutorial scene actually unloaded", "No TutorialGame survives the Single-scene handoff");
            CheckThat(descendingObserved && stoppingObserved && floorOneObserved && floorB1Observed && ride.DestinationReady,
                "Descent, stop and 1-to-B1 floor sequence observed", string.Join(", ", report.phases));
            CheckThat(report.maximumShaftMotion > .5f, "Shaft structures actually move behind glass", report.maximumShaftMotion.ToString("F3"));
            bool hasBasementText = false;
            foreach (var label in ride.GetComponentsInChildren<TextMesh>(true)) hasBasementText |= label.text == "B1";
            CheckThat(hasBasementText, "Actual floor display reads B1", "Production TextMesh, not only a reported floor property");
            yield return WaitFor(() => ride.Phase == ElevatorPhase.Arrived && gallery.active && gallery.started, 6, "Destination doors must fully open and resume Gallery");
            yield return null;
            CheckThat(report.arrivalEvents == 1 && report.gallerySceneLoads == 1,
                "One arrival and one Gallery scene load", "arrivals=" + report.arrivalEvents + ", loads=" + report.gallerySceneLoads);
            CheckThat(ride.TransferCamera == null && gallery.view.isActiveAndEnabled && gallery.controller.enabled,
                "Destination owns the camera and controller", "Temporary transfer camera removed");
            CheckThat(gallery.bottleBalance != null, "Latest Dike museum room remains enabled on arrival", "Tutorial route includes the existing bottle and balance exhibition");
            // Inspect the bound destination controller and its actual camera only
            // after transfer ownership has ended; retained transition data alone
            // would not prove that the new player received the pose.
            report.departureLocalFeet = departurePose.localFeet;
            report.arrivalLocalFeet = ride.transform.InverseTransformPoint(gallery.controller.transform.position);
            report.feetPoseError = Vector3.Distance(departurePose.localFeet, report.arrivalLocalFeet);
            report.eyePoseError = Vector3.Distance(departurePose.localEye, ride.transform.InverseTransformPoint(gallery.view.transform.position));
            report.viewPoseErrorDegrees = Quaternion.Angle(departurePose.localViewRotation,
                Quaternion.Inverse(ride.transform.rotation) * gallery.view.transform.rotation);
            CheckThat(report.feetPoseError < .10f && report.eyePoseError < .10f && report.viewPoseErrorDegrees < .25f,
                "Actual destination feet and view survive real scene load", "feet=" + report.feetPoseError + ", eye=" + report.eyePoseError + ", angle=" + report.viewPoseErrorDegrees);
            report.arrivalCaptureDeviation = Capture(gallery.view, "03-open-door-to-museum.png", null,
                gallery.view.transform.position + ride.transform.forward * 6 + Vector3.up * .15f);

            report.phase = "actual destination motor crosses open elevator doorway";
            Vector3 before = gallery.controller.transform.position;
            Vector3 target = ride.transform.TransformPoint(new Vector3(ride.Asset.cabinBounds.center.x,
                ride.Asset.floorY + .03f, ride.Asset.thresholdZ + 1.65f));
            double walkDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (Vector3.ProjectOnPlane(target - gallery.controller.transform.position, Vector3.up).magnitude > .12f &&
                Time.realtimeSinceStartupAsDouble < walkDeadline)
            {
                Vector3 desired = Vector3.ProjectOnPlane(target - gallery.controller.transform.position, Vector3.up).normalized;
                // Gallery.Tick accepts world X/Z, then converts to motor-local
                // input itself. Supplying camera-local input would rotate twice.
                gallery.Tick(Mathf.Min(Time.deltaTime, .05f), new Vector2(desired.x, desired.z), false);
                yield return null;
            }
            report.galleryExitFeet = gallery.controller.transform.position;
            report.actualExitDistance = Vector3.Distance(before, report.galleryExitFeet);
            Vector3 localExit = ride.transform.InverseTransformPoint(report.galleryExitFeet);
            CheckThat(localExit.z > ride.Asset.thresholdZ + gallery.controller.radius + .15f && report.actualExitDistance > .65f,
                "Actual Gallery motor crosses physical open doorway", "Local exit feet=" + localExit.ToString("F3") + ", travel=" + report.actualExitDistance.ToString("F3"));
            CheckThat(gallery.checkpoint == 0 && !gallery.won && gallery.active && Time.timeScale > 0 &&
                gallery.player.feet.z > 0 && gallery.player.feet.z < 12,
                "Arrival remains in museum first room", gallery.player.feet.ToString("F3"));
            CheckThat(report.maximumActiveListeners == 1, "Only one active listener throughout visible transfer", report.maximumActiveListeners.ToString());
            VerifySelectedAudio();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tutorial-route-beach-validate") >= 0)
            {
                report.phase = "actual museum exit continues to Beach";
                yield return TutorialRouteBeachValidation.Run(gallery, CheckThat, captureDirectory);
            }
            CheckThat(report.tutorialCaptureDeviation > .008f && report.shaftCaptureDeviation > .008f && report.arrivalCaptureDeviation > .008f,
                "Actual camera renders contain visible geometry", "luminance deviations=" + report.tutorialCaptureDeviation + ", " + report.shaftCaptureDeviation + ", " + report.arrivalCaptureDeviation);
            report.phase = "complete";
        }

        void ObserveActualAudio()
        {
            if (selectedAudio == null) selectedAudio = ride.SelectedAudio;
            if (selectedAudio == null) return;
            foreach (var voice in ride.GetComponentsInChildren<AudioSource>(true))
            {
                if (!voice.isPlaying || voice.clip == null || !voice.clip.name.StartsWith("EL", StringComparison.Ordinal)) continue;
                string id = voice.clip.name.Substring(0, 4);
                AudioEvidence evidence = report.actualAudio.Find(x => x.id == id);
                if (evidence == null)
                {
                    evidence = new AudioEvidence { id = id, clip = voice.clip.name, initialSample = voice.timeSamples, voiceId = voice.GetInstanceID(), loop = voice.loop };
                    report.actualAudio.Add(evidence);
                }
                evidence.frames++;
                evidence.maximumVolume = Mathf.Max(evidence.maximumVolume, voice.volume);
                evidence.lastSample = voice.timeSamples;
                evidence.progressed |= voice.timeSamples != evidence.initialSample;
                evidence.afterGallery |= SceneManager.GetActiveScene().name == "Gallery";
            }
            if (ride.Phase == ElevatorPhase.Descending && runVoiceAtDeparture == 0)
                runVoiceAtDeparture = selectedAudio.SourceFor("EL11").GetInstanceID();
        }

        void VerifySelectedAudio()
        {
            foreach (string id in AudioIds)
            {
                AudioEvidence actual = report.actualAudio.Find(x => x.id == id);
                CheckThat(actual != null && actual.frames > 0 && actual.progressed,
                    "Selected sound actually plays and advances samples", id);
            }
            foreach (string id in new[] { "EL07", "EL09", "EL11", "EL25", "EL21" })
                CheckThat(selectedAudio.PlayCount(id) == 1, "Single event does not duplicate a selected cue", id + " count=" + selectedAudio.PlayCount(id));
            CheckThat(selectedAudio.PlayCount("EL02") >= 3 && selectedAudio.PlayCount("EL04") >= 2,
                "Approach, obstruction reopening and destination use door sounds", "open=" + selectedAudio.PlayCount("EL02") + ", close=" + selectedAudio.PlayCount("EL04"));
            CheckThat(runVoiceAtDeparture != 0 && runVoiceAtDeparture == selectedAudio.SourceFor("EL11").GetInstanceID(),
                "Running voice survives the actual scene replacement", runVoiceAtDeparture.ToString());
            CheckThat(selectedAudio.SourceFor("EL23").isPlaying && selectedAudio.SourceFor("EL23").loop && !selectedAudio.SourceFor("EL11").isPlaying,
                "Arrival stops running motor and retains quiet ventilation", "EL11 stopped / EL23 looping");
            TutorialElevatorAudio.Playback stopCue = default, bellCue = default, openCue = default;
            foreach (var cue in selectedAudio.PlaybackHistory)
            {
                report.audioHistory.Add(cue);
                if (cue.id == "EL25") stopCue = cue;
                if (cue.id == "EL21") bellCue = cue;
                if (cue.id == "EL02" && cue.phase == ElevatorPhase.OpeningAtDestination) openCue = cue;
            }
            CheckThat(stopCue.phase == ElevatorPhase.Stopping && bellCue.phase == ElevatorPhase.ArrivalChime && openCue.phase == ElevatorPhase.OpeningAtDestination &&
                bellCue.dspTime - stopCue.dspTime >= ride.StopDuration - .10 && openCue.dspTime - bellCue.dspTime >= .35,
                "Stop, selected bell and opening have real DSP ordering", "stop→bell=" + (bellCue.dspTime - stopCue.dspTime).ToString("F3") + ", bell→open=" + (openCue.dspTime - bellCue.dspTime).ToString("F3"));
            foreach (string id in new[] { "EL02", "EL04" })
            {
                var actual = report.actualAudio.Find(x => x.id == id);
                CheckThat(actual.maximumVolume > .01f && actual.maximumVolume <= .221f,
                    "Door playback gain is reduced by six dB", id + " actual max gain=" + actual.maximumVolume.ToString("F6"));
            }
            CheckThat(selectedAudio.ClipFor("EL15") == null && selectedAudio.ClipFor("EL25") != null,
                "EL25 replaces EL15 in the active cue bank", "Only the selected EL25 stop is mapped");
        }

        void ObserveDeparture(TutorialElevatorRide current)
        {
            report.departureEvents++;
            departurePose = current.CapturePose();
            completedAtDeparture = TutorialComplete();
            report.closedDoorDuration = (float)(Time.realtimeSinceStartupAsDouble - finalClosingBegan);
        }
        void ObserveArrival(TutorialElevatorRide current) { report.arrivalEvents++; }
        Camera JourneyCamera() => ride != null && ride.TransferCamera != null ? ride.TransferCamera : gallery != null ? gallery.view : null;
        void InspectPhysicalCabin()
        {
            var glassCollider = ride.Asset.backGlass.GetComponent<Collider>();
            CheckThat(glassCollider != null, "Translucent panel retains physical collision", ride.Asset.backGlass.name);
            Vector3 origin = ride.Asset.backGlass.bounds.center + ride.transform.forward * .6f;
            Vector3 behind = -ride.transform.forward;
            CheckThat(Physics.Raycast(origin, behind, out var glassHit, 1.5f, ~0, QueryTriggerInteraction.Ignore) &&
                glassHit.collider == glassCollider,
                "Actual rear panel blocks physical passage", glassHit.collider == null ? "No collision" : glassHit.collider.name);
            bool wasEnabled = glassCollider.enabled;
            try
            {
                glassCollider.enabled = false;
                Physics.SyncTransforms();
                bool backedBySolidWall = Physics.Raycast(origin, behind, out var rearHit, 1.5f, ~0, QueryTriggerInteraction.Ignore);
                CheckThat(!backedBySolidWall, "Real aperture exists behind translucent panel",
                    backedBySolidWall ? "Unexpected solid backing: " + rearHit.collider.name : "Source back wall and frame do not cover panel centre");
            }
            finally { glassCollider.enabled = wasEnabled; Physics.SyncTransforms(); }
            Vector3 centre = ride.WorldBoardingCenter + Vector3.up * .5f;
            CheckThat(Physics.Raycast(centre, Vector3.down, out var floor, 1, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(floor.point.y - ride.transform.TransformPoint(Vector3.up * ride.Asset.floorY).y) < .05f,
                "Original cabin floor has actual collision", floor.collider == null ? "Missing floor" : floor.collider.name);
            Vector3 sideOrigin = ride.WorldBoardingCenter + Vector3.up * 1.6f;
            CheckThat(Physics.Raycast(sideOrigin, ride.transform.right, 2.5f, ~0, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(sideOrigin, -ride.transform.right, 2.5f, ~0, QueryTriggerInteraction.Ignore),
                "Cabin side walls have actual collision", "Real source meshes tested in both horizontal directions");
        }
        bool TutorialComplete() => tutorial != null && (bool)typeof(TutorialGame).GetField("sunlightComplete", Flags).GetValue(tutorial);
        static object Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, Flags);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            return info.Invoke(target, args);
        }
        static Transform FindDescendant(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }
        void CheckThat(bool passed, string name, string detail)
        {
            report.checks.Add(new Check { name = name, detail = detail, passed = passed });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        IEnumerator Wait(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }
        IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException(message + "; phase=" + (ride == null ? "null" : ride.Phase.ToString()));
                yield return null;
            }
        }
        IEnumerator MoveController(CharacterController controller, Vector3 target, float speed, float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (true)
            {
                Vector3 delta = Vector3.ProjectOnPlane(target - controller.transform.position, Vector3.up);
                if (delta.magnitude < .065f) yield break;
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new TimeoutException("Actual CharacterController could not reach boarding target " + target + "; feet=" + controller.transform.position);
                float dt = Mathf.Min(Time.unscaledDeltaTime, .04f);
                controller.Move(Vector3.ClampMagnitude(delta, speed * dt) + Vector3.down * dt);
                yield return null;
            }
        }
        float Capture(Camera camera, string name, Vector3? position, Vector3? lookAt)
        {
            if (camera == null) throw new InvalidOperationException("Capture camera is missing: " + name);
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            float oldAspect = camera.aspect;
            var render = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                if (position.HasValue) camera.transform.position = position.Value;
                if (lookAt.HasValue) camera.transform.rotation = Quaternion.LookRotation(lookAt.Value - camera.transform.position, Vector3.up);
                camera.aspect = 1600f / 900f;
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                string path = Path.Combine(captureDirectory, name);
                File.WriteAllBytes(path, texture.EncodeToPNG()); report.captures.Add(path);
                var pixels = texture.GetPixels32(); double mean = 0, squared = 0; int count = 0;
                for (int i = 0; i < pixels.Length; i += 31)
                {
                    double l = (pixels[i].r * .2126 + pixels[i].g * .7152 + pixels[i].b * .0722) / 255;
                    mean += l; squared += l * l; count++;
                }
                mean /= Math.Max(1, count);
                return (float)Math.Sqrt(Math.Max(0, squared / Math.Max(1, count) - mean * mean));
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.targetTexture = oldTarget; camera.aspect = oldAspect; RenderTexture.active = oldActive;
                render.Release(); Destroy(render); Destroy(texture);
            }
        }
        void ObserveScene(Scene scene, LoadSceneMode mode)
        {
            report.scenes.Add(scene.name);
            if (scene.name == "Gallery") report.gallerySceneLoads++;
        }
        void ObserveLog(string message, string stack, LogType type)
        {
            if (message.IndexOf("ELEVATOR", StringComparison.OrdinalIgnoreCase) >= 0 &&
                type == LogType.Log && !report.handoffLogs.Contains(message)) report.handoffLogs.Add(message);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + stack);
        }
        void Finish()
        {
            if (finished) return;
            finished = true;
            if (blocker != null) Destroy(blocker);
            if (ride != null) { ride.DepartureReady -= ObserveDeparture; ride.ArrivedAtDestination -= ObserveArrival; }
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.finalScene = SceneManager.GetActiveScene().name;
            report.passed = report.failure == null && report.errors.Count == 0 && report.phase == "complete" && report.checks.Count >= 30;
            SceneManager.sceneLoaded -= ObserveScene;
            Application.logMessageReceived -= ObserveLog;
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            if (report.passed) Debug.Log("TUTORIAL_ELEVATOR_VALIDATION_OK checks=" + report.checks.Count);
            else Debug.LogError("TUTORIAL_ELEVATOR_VALIDATION_FAIL phase=" + report.phase + " " + report.failure);
            if (!Application.isEditor) Application.Quit(report.passed ? 0 : 1);
        }
    }
}
