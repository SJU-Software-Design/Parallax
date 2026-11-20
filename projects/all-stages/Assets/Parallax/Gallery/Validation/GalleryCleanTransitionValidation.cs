using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShadeLink;
using ShadeLink.Beach;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Gallery
{
    // Opt-in separate-process QA. Explicit hand/player poses set up the final
    // approach; production placement, opening, motor, entry Update and scene
    // handoff remain authoritative. No completion flags or rendered-pixel APIs.
    [DefaultExecutionOrder(1000)]
    public sealed class GalleryCleanTransitionValidation : MonoBehaviour
    {
        const string Flag = "-gallery-clean-transition-test";
        static bool claimed;
        static string output, isolated;
        static Dictionary<string, string> productionBefore;
        [Serializable] public sealed class Check { public bool passed; public string name, detail; }
        [Serializable] public sealed class Sample
        {
            public string scene, phase;
            public float opacity, fallDistance, fallElapsed, fallSpeed, fieldOfView;
            public int cullingMask;
            public bool loadingOverlay, sourcePresent, destinationPresent, destinationEnabled;
            public Vector3 eye;
        }
        [Serializable] public sealed class Storage { public string path, before, after; public bool unchanged; }
        [Serializable] public sealed class Report
        {
            public bool passed, productionStorageUnchanged, renderedPixelsVerified;
            public int captureCount, processId, fixedSteps, walkSteps, entrySamples, fallSamples, visibleFallSamples, loadingSamples, arrivalSamples, beachLoads, museumRenderersChecked, maskedMuseumSamples;
            public float walkDistance, beachMoveDistance, maximumEntryPoseError, maximumEntryAngleError, maximumEntryFovError;
            public float actualFallDistance, visibleFallDistance, earlyFallSpeed, lateFallSpeed, maximumMeasuredSpeedError, peakFallWindGain;
            public string scope, failure, phase, unity, graphicsDevice, finalScene, dllSha256;
            public Check[] checks;
            public string[] errors, probeFailures;
            public Storage[] storage;
            public Sample[] samples;
        }
        readonly List<Check> checks = new List<Check>();
        readonly List<string> errors = new List<string>(), probeFailures = new List<string>();
        readonly List<Sample> samples = new List<Sample>();
        readonly Report report = new Report();
        GalleryGame game;
        GalleryStageTransition passage;
        Vector2 movement;
        bool drive, watch, handoffLogged;
        Vector3 frozenActor, arrivalActor;
        bool arrivalTracked;
        string phase;
        Renderer[] museumRenderers;
        float previousFallTime = -1, previousFallY, firstFallY, minimumVisibleY = float.MaxValue, maximumVisibleY = float.MinValue;
        float earlySpeedSum, lateSpeedSum, previousArrivalWindGain = float.PositiveInfinity; int earlySpeedCount, lateSpeedCount, originalCameraCount;

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
            var args = Environment.GetCommandLineArgs();
            if (!Application.isBatchMode || Array.IndexOf(args, "-game-progress-validate") < 0 || Array.IndexOf(args, "-preview-validate") < 0)
                throw new ArgumentException("Clean transition QA requires isolated batch mode plus passive -game-progress-validate and -preview-validate flags.");
            if (Array.Exists(args, value => value != Flag && value.EndsWith("-test", StringComparison.OrdinalIgnoreCase)) ||
                Array.IndexOf(args, "-player-shell-validate") >= 0 || Array.IndexOf(args, "-eclipse-ending-validate") >= 0)
                throw new ArgumentException("Do not combine clean transition QA with another active validator or capture driver.");
            string folder = Arg(Flag);
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder)) throw new ArgumentException(Flag + " requires an absolute output directory.");
            output = Path.GetFullPath(folder); isolated = Path.Combine(output, "SaveData");
            string real = Application.persistentDataPath;
            productionBefore = new Dictionary<string, string>();
            foreach (string name in new[] { "ParallaxCheckpoint.json", "ParallaxCheckpoint.json.bak",
                "ParallaxCheckpoint.admin-session.json", "ParallaxCheckpoint.admin-session.json.bak",
                "settings.json", "settings.json.previous", "ParallaxEndingSeen.json", "ParallaxEndingSeen.json.bak" })
            { string path = Path.Combine(real, name); productionBefore[path] = Hash(path); }
            if (string.Equals(Path.GetDirectoryName(GameProgress.SavePath), isolated, StringComparison.OrdinalIgnoreCase) ||
                !GameProgressRuntime.SetSaveDirectoryForValidation(isolated)) throw new InvalidOperationException("QA save isolation failed: " + GameProgressRuntime.LastError);
            Directory.CreateDirectory(output); AudioListener.volume = 0;
            var host = new GameObject("Gallery clean transition · isolated native verification");
            DontDestroyOnLoad(host); host.AddComponent<GalleryCleanTransitionValidation>();
        }
        void Awake()
        {
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            SceneManager.sceneLoaded -= Loaded;
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Gallery")
            {
                var owner = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
                if (owner != null) owner.automated = true;
            }
            if (scene.name == "Beach")
            {
                report.beachLoads++;
                var owner = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
                if (owner != null) owner.automated = true;
            }
            Application.runInBackground = true; Application.targetFrameRate = 90; QualitySettings.vSyncCount = 0;
        }
        void Log(string message, string stack, LogType type)
        {
            if (message.Contains("MUSEUM_BEACH_HANDOFF_OK")) handoffLogged = true;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }
        void Require(bool passed, string name, string detail = "")
        {
            checks.Add(new Check { passed = passed, name = name, detail = detail });
            Debug.Log("GALLERY_CLEAN_TRANSITION_" + (passed ? "PASS " : "FAIL ") + name + " " + detail);
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 90; QualitySettings.vSyncCount = 0;
            var stack = new Stack<IEnumerator>(); stack.Push(Sequence());
            double deadline = Time.realtimeSinceStartupAsDouble + 150;
            while (stack.Count > 0)
            {
                bool more; object next = null;
                try
                {
                    if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Clean transition QA exceeded 150 seconds at " + phase);
                    more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current;
                }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            drive = watch = false;
            report.storage = productionBefore.Select(pair => new Storage { path = pair.Key, before = pair.Value, after = Hash(pair.Key), unchanged = pair.Value == Hash(pair.Key) }).ToArray();
            report.productionStorageUnchanged = report.storage.All(item => item.unchanged);
            checks.Add(new Check { passed = report.productionStorageUnchanged, name = "All eight real player progress, admin, settings and ending files remain byte-identical" });
            report.passed = report.failure == null && errors.Count == 0 && probeFailures.Count == 0 && checks.All(item => item.passed);
            report.captureCount = 0; report.renderedPixelsVerified = false;
            report.processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            report.dllSha256 = Hash(typeof(GalleryStageTransition).Assembly.Location);
            report.phase = phase; report.unity = Application.unityVersion; report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            report.finalScene = SceneManager.GetActiveScene().name;
            report.scope = "Isolated native D3D11, automatic PhysX and production movement/placement/entry/async handoff. Explicit checkpoint-three, three-times hand and clear approach poses are setup fixtures, not puzzle solving. No entrance/opening/success flags are injected and no test scene-load call performs Gallery-to-Beach. Visible downward displacement, acceleration, seamless unlit material/haze, actual frustum and exclusion of every original renderer are inspected from camera/mesh/layer data; no screenshot, explicit camera render, GPU readback, OS input or audible listening. Only this validation process quits itself.";
            report.checks = checks.ToArray(); report.errors = errors.ToArray(); report.probeFailures = probeFailures.ToArray(); report.samples = samples.ToArray();
            File.WriteAllText(Path.Combine(output, "gallery-clean-transition-results.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("GALLERY_CLEAN_TRANSITION_VALIDATION " + (report.passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }
        void FixedUpdate()
        {
            if (!drive || game == null || !game.active || game.won || game.Transitioning) return;
            game.Tick(Time.fixedDeltaTime, movement, false); report.fixedSteps++;
            if (movement.sqrMagnitude > 0) report.walkSteps++;
        }
        static float ColorError(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b), Mathf.Abs(a.a-b.a));
        void Probe(bool passed, string message)
        { if (!passed && !probeFailures.Contains(message)) probeFailures.Add(message); }
        void LateUpdate()
        {
            if (!watch) return;
            if (passage == null && game != null) passage = game.stageTransition;
            if (passage == null) return;
            var beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
            bool overlay = ParallaxGameShell.ShouldShowLoadingOverlay;
            samples.Add(new Sample { scene = SceneManager.GetActiveScene().name, phase = passage.Phase.ToString(), opacity = passage.FadeOpacity,
                loadingOverlay = overlay, sourcePresent = game != null, destinationPresent = beach != null, destinationEnabled = beach != null && beach.enabled,
                fallDistance = passage.FallDistance, fallElapsed = passage.FallElapsed, fallSpeed = passage.FallSpeed,
                fieldOfView = game != null ? game.view.fieldOfView : beach != null ? beach.view.fieldOfView : 0,
                cullingMask = game != null ? game.view.cullingMask : beach != null ? beach.view.cullingMask : 0,
                eye = game != null ? game.view.transform.position : beach != null ? beach.view.transform.position : Vector3.zero });
            Probe(!overlay, "The shared Loading overlay appeared during the natural hand passage.");
            Probe(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(item => item.name == "Museum passage · descending toward the sunlit shore"), "The retired finite cube shaft or false sand/sea floor was instantiated.");
            if (game != null && passage.IsTransitioning)
                Probe(!game.active && game.won && game.held == null && Vector3.Distance(game.controller.transform.position, frozenActor) < .0001f,
                    "Museum gameplay or its actor moved during the protected hand passage.");
            if (passage.Phase == GalleryPassagePhase.EntryFade && game != null)
            {
                report.entrySamples++;
                float position = Vector3.Distance(game.view.transform.position, passage.EntryFramePosition);
                float angle = Quaternion.Angle(game.view.transform.rotation, passage.EntryFrameRotation);
                float fov = Mathf.Abs(game.view.fieldOfView - passage.EntryFrameFieldOfView);
                report.maximumEntryPoseError = Mathf.Max(report.maximumEntryPoseError, position);
                report.maximumEntryAngleError = Mathf.Max(report.maximumEntryAngleError, angle);
                report.maximumEntryFovError = Mathf.Max(report.maximumEntryFovError, fov);
                Probe(position < .0001f && angle < .01f && fov < .001f, "The entry camera moved or changed its lens before the isolated fall.");
                float localZ = game.orcusExit.transform.InverseTransformPoint(game.view.transform.position).z;
                Probe(localZ < game.orcusExit.EntryPlane, "Visible entry framing crossed behind the hand aperture.");
            }
            if (passage.Phase == GalleryPassagePhase.Falling && game != null)
            {
                report.fallSamples++;
                Probe(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == originalCameraCount, "The passage duplicated a camera instead of isolating the existing view.");
                Camera camera = game.view;
                Probe(camera.cullingMask == 1 << GalleryStageTransition.FallLayer && camera.clearFlags == CameraClearFlags.SolidColor &&
                    ColorError(camera.backgroundColor, GalleryStageTransition.HazeColor) < .0001f && !camera.useOcclusionCulling,
                    "The fall camera lost its isolated layer, solid clear color or explicit rendering policy.");
                Probe(Mathf.Abs(camera.fieldOfView - passage.EntryFrameFieldOfView) < .001f &&
                    Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.z, 0)) < .001f,
                    "The fall changed the configured FOV or introduced unwanted roll/shake.");
                var surface = passage.FallSpaceRenderer;
                Probe(surface != null && surface.enabled && surface.gameObject.layer == GalleryStageTransition.FallLayer &&
                    surface.GetComponentsInChildren<Collider>(true).Length == 0 && surface.sharedMaterial != null &&
                    surface.sharedMaterial.shader.name == "Parallax/GalleryFallSpace" && surface.sharedMaterial.shader.isSupported,
                    "The seamless fall surface is missing, unsupported, collidable or on the wrong layer.");
                if (surface != null)
                {
                    var material = surface.sharedMaterial;
                    Probe(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), surface.bounds), "No clean fall surface intersects the actual view frustum.");
                    Probe(surface.bounds.max.y - camera.transform.position.y > camera.farClipPlane &&
                        camera.transform.position.y - surface.bounds.min.y > camera.farClipPlane,
                        "A finite end of the fall surface entered the camera far distance.");
                    Probe(material.GetFloat("_HazeEnd") < camera.farClipPlane && material.GetFloat("_HazeStart") < material.GetFloat("_HazeEnd") &&
                        ColorError(material.GetColor("_HazeColor"), camera.backgroundColor) < .0001f,
                        "Distance haze does not fully match the camera clear color before clipping.");
                    var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                    Probe(mesh != null && mesh.vertexCount == 258 && mesh.triangles.Length == 768 && mesh.vertices.All(vertex => new Vector2(vertex.x, vertex.z).magnitude > 3.39f),
                        "The fall surface acquired caps, floor geometry or discontinuous tube parts.");
                }
                foreach (var renderer in museumRenderers)
                {
                    if (renderer == null) continue;
                    Probe((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0, "An original museum/preview renderer remains in the fall camera mask: " + renderer.name);
                    report.maskedMuseumSamples++;
                }
                var actual = camera.transform.position;
                Probe(Vector3.Distance(actual, passage.FallOrigin + Vector3.down * passage.FallDistance) < .0002f,
                    "The actual camera position differs from the accelerating downward path.");
                if (previousFallTime < 0) firstFallY = actual.y;
                else if (passage.FallElapsed > previousFallTime + .00001f)
                {
                    float measured = (previousFallY - actual.y) / (passage.FallElapsed - previousFallTime);
                    float midpoint = (previousFallTime + passage.FallElapsed) * .5f;
                    float expected = 2 * GalleryStageTransition.FallMetres * midpoint / (GalleryStageTransition.FallDuration * GalleryStageTransition.FallDuration);
                    report.maximumMeasuredSpeedError = Mathf.Max(report.maximumMeasuredSpeedError, Mathf.Abs(measured - expected));
                    Probe(measured >= -.02f && Mathf.Abs(measured - expected) < .05f, "Measured camera displacement does not follow the real accelerating fall.");
                    if (midpoint > .35f && midpoint < .75f) { earlySpeedSum += measured; earlySpeedCount++; }
                    if (midpoint > 1.65f && midpoint < 2.05f) { lateSpeedSum += measured; lateSpeedCount++; }
                }
                previousFallTime = passage.FallElapsed; previousFallY = actual.y;
                report.actualFallDistance = Mathf.Max(report.actualFallDistance, firstFallY - actual.y);
                if (passage.FadeOpacity < .001f)
                {
                    report.visibleFallSamples++;
                    minimumVisibleY = Mathf.Min(minimumVisibleY, actual.y); maximumVisibleY = Mathf.Max(maximumVisibleY, actual.y);
                    report.visibleFallDistance = maximumVisibleY - minimumVisibleY;
                }
                Probe(passage.FallAudio != null && passage.FallAudio.Source != null && passage.FallAudio.Clip != null && passage.FallAudio.BeginCount == 1,
                    "The real falling wind source was not started exactly once by the production passage.");
                if (passage.FallAudio != null && passage.FallAudio.Source != null)
                {
                    report.peakFallWindGain = Mathf.Max(report.peakFallWindGain, passage.FallAudio.Gain);
                    Probe(Mathf.Abs(passage.FallAudio.FallProgress - passage.FallElapsed / GalleryStageTransition.FallDuration) < .002f &&
                        !passage.FallAudio.Source.loop && passage.FallAudio.Source.outputAudioMixerGroup != null &&
                        passage.FallAudio.Source.outputAudioMixerGroup.name == "Sfx",
                        "Actual falling wind timing or Effects routing diverged from the visible production fall.");
                }
            }
            if (passage.Phase == GalleryPassagePhase.LoadingDestination)
            {
                report.loadingSamples++;
                Probe(passage.FadeOpacity >= .99999f, "A world load was exposed before the fade became fully opaque.");
            }
            if (passage.Phase == GalleryPassagePhase.ArrivalFade)
            {
                report.arrivalSamples++;
                if (passage.FallAudio != null)
                {
                    Probe(passage.FallAudio.Gain <= previousArrivalWindGain + .00001f, "The production wind became louder again during landing release.");
                    previousArrivalWindGain = passage.FallAudio.Gain;
                }
                Probe(beach != null && !beach.enabled && beach.active && beach.started && beach.checkpoint == 0 && beach.view.enabled,
                    "The shore reveal lacks the real first-checkpoint camera or enables movement too early.");
                if (beach != null)
                {
                    if (!arrivalTracked) { arrivalActor = beach.controller.transform.position; arrivalTracked = true; }
                    Probe(Vector3.Distance(beach.controller.transform.position, arrivalActor) < .0001f, "Beach actor moved before the shore reveal finished.");
                    Probe(beach.view.cullingMask != 1 << GalleryStageTransition.FallLayer &&
                        passage.FallSpaceRenderer == null && passage.GetComponentsInChildren<Renderer>(true).Length == 0,
                        "The isolated fall layer or its geometry leaked into real Beach arrival.");
                }
            }
        }
        IEnumerator ManagedFocusClockFixture()
        {
            var host = new GameObject("Gallery passage clock · managed callback fixture");
            var clock = host.AddComponent<GalleryStageTransition>();
            try
            {
                // Settle the new object's initial engine focus notification;
                // subsequent calls below are explicit managed fixtures only.
                yield return null;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var focus = typeof(GalleryStageTransition).GetMethod("OnApplicationFocus", flags);
                var pause = typeof(GalleryStageTransition).GetMethod("OnApplicationPause", flags);
                var delta = typeof(GalleryStageTransition).GetMethod("ClockDelta", flags);
                Require(focus != null && pause != null && delta != null, "The passage exposes its actual managed focus/pause callbacks and production clock to reflection fixtures");
                Func<float, float> step = raw => (float)delta.Invoke(clock, new object[] { raw });
                focus.Invoke(clock, new object[] { true }); yield return null;
                Require(Mathf.Abs(step(30) - GalleryStageTransition.MaximumClockStep) < .000001f,
                    "A large resumed-frame clock input is bounded to 50 milliseconds", "Explicit raw-delta fixture; no OS focus event");
                focus.Invoke(clock, new object[] { false }); float focusLost = step(30); yield return null;
                Require(focusLost == 0 && step(30) == 0, "The actual managed focus-loss callback freezes the production passage clock");
                focus.Invoke(clock, new object[] { true });
                Require(step(30) == 0, "The first focus-return frame advances neither falling nor reveal time");
                yield return null;
                Require(Mathf.Abs(step(.02f) - .02f) < .000001f && Mathf.Abs(step(30) - .05f) < .000001f,
                    "The following focused frame resumes with bounded production time");
                pause.Invoke(clock, new object[] { true }); float paused = step(30); yield return null;
                pause.Invoke(clock, new object[] { false });
                Require(paused == 0 && step(30) == 0, "Application pause and its first return frame both freeze the production passage clock");
                yield return null;
                Require(Mathf.Abs(step(30) - .05f) < .000001f, "Application-pause return resumes bounded time on the following frame");
            }
            finally { if (host != null) Destroy(host); }
            yield return null;
        }
        IEnumerator Sequence()
        {
            phase = "isolated entry"; yield return null; yield return null;
            Require(Application.isBatchMode && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, "QA uses the isolated native D3D11 player", SystemInfo.graphicsDeviceType.ToString());
            Require(ParallaxGameShell.Instance != null && SceneManager.GetActiveScene().name == "MainMenu", "The real five-scene Shell is present before the Gallery setup");
            Require(productionBefore.Count == 8 && !GameProgress.IsUnisolatedValidation && GameProgress.SavePath.StartsWith(isolated + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "All eight production paths are guarded and checkpoint storage is isolated", GameProgress.SavePath);
            Require(Physics.simulationMode == SimulationMode.FixedUpdate, "The route retains automatic FixedUpdate PhysX", Physics.simulationMode.ToString());
            Require(Application.CanStreamedLevelBeLoaded("Gallery") && Application.CanStreamedLevelBeLoaded("Beach"), "Actual Gallery and Beach scenes belong to the player build");
            // This setup load is disclosed; the tested Gallery-to-Beach load must
            // only originate from the actual opened-hand crossing below.
            yield return SceneManager.LoadSceneAsync("Gallery", LoadSceneMode.Single); yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            Require(game != null && game.conceptLayout && game.automated, "The canonical Gallery controller is ready in inherited automation");
            game.exhibitionPreview = true; game.ResetAll(); game.Resume(); drive = true;
            var entrance = game.orcusExit;
            var portal = game.GetComponentInChildren<GalleryCathedralPortal>(true);
            Require(entrance != null && entrance.prop != null && entrance.cathedralActivation != null && portal != null && entrance.enabled,
                "The actual final hands, physical entrance, activation and portal remain connected");
            Require(!entrance.IsReadyForEntry && !entrance.Entering && !game.Transitioning, "The original small hand cannot begin the route");
            phase = "fall wind component fixtures";
            yield return GalleryFallAudioValidation.Run((ok, name, detail) => Require(ok, name, detail));
            phase = "managed passage focus clock fixture";
            yield return ManagedFocusClockFixture();
            phase = "declared three-times hand fixture";
            Debug.Log("GALLERY_CLEAN_FIXTURE: checkpoint=3, hands=3x at legacy(0,.006,53), explicit clear approach. Production placement/opening/crossing remains authoritative; no completion flags.");
            var hands = entrance.prop; hands.InitializePhysics(); hands.body.isKinematic = true; hands.body.useGravity = false;
            Vector3 at = game.bottleBalance != null ? GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, .006f, 53)) : new Vector3(0, .006f, 53);
            hands.transform.SetPositionAndRotation(at, Quaternion.identity); hands.transform.localScale = Vector3.one * 3;
            hands.body.position = at; hands.body.rotation = Quaternion.identity; Physics.SyncTransforms(); hands.placement.ResetPlaced();
            game.checkpoint = 3;
            Vector3 waitPoint = game.bottleBalance != null ? GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, .01f, 45.4f)) : new Vector3(0, .01f, 45.4f);
            game.PlacePlayer(waitPoint); game.yaw = game.pitch = 0; game.SyncView();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while ((!portal.Ready || !entrance.IsReadyForEntry) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Require(report.fixedSteps > 0 && hands.placement.Ready && entrance.cathedralActivation.Opened && entrance.IsReadyForEntry,
                "The actual enlarged hands settle and open through production logic", "fixedSteps=" + report.fixedSteps + ", progress=" + entrance.cathedralActivation.Progress);
            Require(portal.Ready && GalleryCathedralPortal.PreviewResource == "MuseumPortal/BeachPreview", "The hands retain the real coastal preview resource");
            Vector3 before = entrance.transform.TransformPoint(new Vector3(entrance.EntryCenterX, entrance.EntryBottom + .025f, entrance.EntryPlane - .12f));
            Vector3 after = entrance.transform.TransformPoint(new Vector3(entrance.EntryCenterX, entrance.EntryBottom + .025f, entrance.EntryPlane + .12f));
            Require(entrance.CanEnterFrom(before, after) && !entrance.CanEnterFrom(after, before), "Only the valid forward physical aperture crossing is eligible");
            Require(!entrance.CanEnterFrom(before - entrance.transform.forward * 3, after), "A discontinuous teleport cannot substitute for crossing the hands");
            Vector3 side = entrance.transform.right * (entrance.EntryHalfWidth * Mathf.Abs(entrance.transform.lossyScale.x) + .5f);
            Require(!entrance.CanEnterFrom(before + side, after + side), "A crossing outside the real hand opening is rejected");
            museumRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            report.museumRenderersChecked = museumRenderers.Length;
            originalCameraCount = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            Require(museumRenderers.Length > 100 && museumRenderers.All(renderer => renderer.gameObject.layer != GalleryStageTransition.FallLayer), "The actual museum and portal preview reserve the isolated fall layer", museumRenderers.Length.ToString());
            phase = "production walking and entry Update";
            Vector3 approach = entrance.transform.TransformPoint(new Vector3(entrance.EntryCenterX + .04f, 0, entrance.approachStart));
            game.PlacePlayer(approach + Vector3.up * .006f); game.yaw = Mathf.Atan2(entrance.transform.forward.x, entrance.transform.forward.z); game.pitch = -.02f; game.SyncView();
            Require(game.CanStand(game.player.feet), "The disclosed approach starts on clear floor before the hand steps", game.player.feet.ToString("F3"));
            entrance.ResetEntrance(); yield return null; yield return null;
            Vector3 walkStart = game.player.feet; int respawns = game.respawns;
            movement = new Vector2(entrance.transform.forward.x, entrance.transform.forward.z);
            until = Time.realtimeSinceStartupAsDouble + 15;
            while (!game.Transitioning && Time.realtimeSinceStartupAsDouble < until) yield return null;
            movement = Vector2.zero; drive = false;
            report.walkDistance = Vector3.Distance(walkStart, game.player.feet);
            Require(game.Transitioning && entrance.Entering && report.walkSteps > 3 && report.walkDistance > .35f && game.respawns == respawns,
                "Production Tick physically climbs the hand approach and normal entrance Update starts the route", "walkSteps=" + report.walkSteps + ", metres=" + report.walkDistance);
            passage = game.stageTransition; frozenActor = game.controller.transform.position; watch = true;
            Require(passage != null && passage.IsTransitioning && passage.Phase == GalleryPassagePhase.EntryFade,
                "The natural crossing begins the entry fade without a test Begin or scene-load call");
            float originalFov = game.view.fieldOfView, originalFar = game.view.farClipPlane;
            game.Tick(.05f, Vector2.one, true); game.Interact(); game.Resume(); game.Pause();
            Require(Vector3.Distance(game.controller.transform.position, frozenActor) < .0001f && !game.active && game.won && game.held == null && Time.timeScale > .99f,
                "Movement, grab, pause and resume cannot disturb the entered passage");
            Require(Mathf.Abs(game.view.fieldOfView - originalFov) < .001f && Mathf.Abs(game.view.farClipPlane - originalFar) < .001f ,
                "Entry preserves the configured source lens before the restored fall");
            phase = "real opaque load and shore reveal"; until = Time.realtimeSinceStartupAsDouble + 45;
            while (!handoffLogged && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Require(handoffLogged && SceneManager.GetActiveScene().name == "Beach" && report.beachLoads == 1, "The production asynchronous handoff loads actual Beach exactly once");
            yield return null;
            Require(report.entrySamples > 1 && report.fallSamples > 30 && report.loadingSamples > 0 && report.arrivalSamples > 1, "Frame sampling observes entry, visible fall, opaque loading and real shore reveal",
                report.entrySamples + "/" + report.fallSamples + "/" + report.loadingSamples + "/" + report.arrivalSamples);
            Require(report.actualFallDistance > 13 && report.visibleFallSamples > 30 && report.visibleFallDistance > 10, "The actual downward camera fall remains visibly open for more than ten metres", "actual=" + report.actualFallDistance + ", fullyVisible=" + report.visibleFallDistance);
            report.earlyFallSpeed = earlySpeedSum / Mathf.Max(1, earlySpeedCount); report.lateFallSpeed = lateSpeedSum / Mathf.Max(1, lateSpeedCount);
            Require(earlySpeedCount > 3 && lateSpeedCount > 3 && report.lateFallSpeed > report.earlyFallSpeed * 2 && report.maximumMeasuredSpeedError < .05f, "Measured native camera motion accelerates throughout the restored fall", "early=" + report.earlyFallSpeed + ", late=" + report.lateFallSpeed);
            Require(report.maskedMuseumSamples > report.museumRenderersChecked * 30, "Every original museum and preview renderer is excluded throughout falling", report.maskedMuseumSamples.ToString());
            Require(report.peakFallWindGain > GalleryFallAudio.PeakGain * .95f, "The actual visible fall drives its real wind crescendo before landing", report.peakFallWindGain.ToString("F4"));
            Require(probeFailures.Count == 0, "Every transition sample preserves isolated visible falling, clean framing, opaque loading, frozen input and zero Loading overlays", string.Join("; ", probeFailures));
            var beach = UnityEngine.Object.FindFirstObjectByType<BeachGame>();
            Require(beach != null && beach.enabled && beach.active && beach.started && !beach.won && beach.controller.enabled && beach.view.enabled && beach.held == null && beach.automated && Time.timeScale > .99f,
                "The actual Beach controller resumes only after its reveal completes");
            Require(Mathf.Abs(beach.view.fieldOfView - originalFov) < .001f, "Real Beach arrival restores the configured player field of view", beach.view.fieldOfView.ToString("F2"));
            Require(beach.checkpoint == 0 && Vector2.Distance(new Vector2(beach.player.feet.x, beach.player.feet.z), new Vector2(BeachWorld.Checkpoints[0].x, BeachWorld.Checkpoints[0].z)) < .35f,
                "The revealed shore is the first real Beach checkpoint", beach.player.feet.ToString("F3"));
            Vector3 start = beach.player.feet;
            for (int i = 0; i < 16; i++) { beach.Tick(Time.fixedDeltaTime, Vector2.up, false); yield return new WaitForFixedUpdate(); }
            report.beachMoveDistance = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(beach.player.feet.x, beach.player.feet.z));
            Require(report.beachMoveDistance > .1f, "The Beach production motor accepts walking after the fade", report.beachMoveDistance.ToString("F3") + " m");
            Require(!SceneManager.GetSceneByName("Gallery").isLoaded && UnityEngine.Object.FindFirstObjectByType<GalleryGame>() == null && UnityEngine.Object.FindFirstObjectByType<GalleryStageTransition>() == null,
                "The museum, camera passage owner and all transition geometry leave the destination");
            Require(!ParallaxGameShell.ShouldShowLoadingOverlay && !ParallaxGameShell.ShouldShowPauseMenu, "Natural arrival leaves no Loading or pause overlay");
            Require(!UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source => source.name == "Gallery falling rush"), "The production fall wind leaves no source or loop after real arrival");
            int listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled && listener.gameObject.activeInHierarchy);
            Require(listeners == 1, "Actual Beach retains one active listener", listeners.ToString());
            Require(errors.Count == 0, "The complete native transition emits zero runtime errors", errors.Count.ToString());
            phase = "complete";
        }
    }
}