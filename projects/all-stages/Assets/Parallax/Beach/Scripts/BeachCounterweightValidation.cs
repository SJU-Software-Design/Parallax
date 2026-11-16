using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Beach
{
    // Focused native-player regression. Initial safe-workstation / real-deck
    // placement are declared fixtures. The rock is never assigned a solution
    // transform, scale, held state or completion flag by this test.
    public sealed class BeachCounterweightValidation : MonoBehaviour
    {
        const float Dt = 1f / 60f;
        static bool claimed;
        BeachGame game;
        BeachCounterweightFinale finale;
        BeachSkyShip ship;
        BeachEndingAudio endingAudio;
        readonly Dictionary<string, int> endingCueBaseline = new Dictionary<string, int>();
        readonly HashSet<string> observedMusicPhases = new HashSet<string>();
        readonly List<string> audioTrace = new List<string>();
        int maximumVisibleDrops, grabCueBaseline;
        bool observedWetCanopy, observedDryCanopy, observedMusicAfterPuzzleSilence, observedAudibleDeparture;
        float previousEndingMusicGain = float.NaN, previousPuzzleEnvelope = 1;
        BeachWater[] waters;
        string output, phase = "boot";
        int frames, initialRespawns, routeSamples, publicPlacementTrials;
        float maximumScale, maximumRockDrift, maximumFlightStep;
        SimulationMode previousSimulation;
        bool changedSimulation, allowExpectedRespawns;
        int adversarialRespawns;
        readonly Dictionary<Renderer, Vector3> canopyRelative = new Dictionary<Renderer, Vector3>();
        readonly List<string> checks = new List<string>();
        readonly List<string> skippedChecks = new List<string>();
        int captureCount;
        readonly List<string> details = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> fixtures = new List<string>();
        readonly List<string> trace = new List<string>();
        readonly Dictionary<Transform, Vector3> waterPositions = new Dictionary<Transform, Vector3>();
        Vector3 home, homeScale, acceptedAim, acceptedPlayer;
        Camera overviewCamera;
        Vector3 overviewCradleStart, overviewBridgeStart;
        bool overviewMidCaptured, midLiftObserved;
        Texture2D overviewDepth;
        readonly List<ViewEvidence> readability = new List<ViewEvidence>();
        [Serializable] sealed class ViewEvidence
        {
            public string phase, feature;
            public int samples, inFrame, visible;
            public bool centerVisible;
            public float minViewportX, maxViewportX, minViewportY, maxViewportY, pixelWidth, pixelHeight;
            public int[] visibleIndices;
        }

        [Serializable] sealed class Report
        {
            public bool passed, audioOnly, captureUsed;
            public int captureCount;
            public string scope, scene, unity, failure;
            public string[] checks, details, errors, fixtures, skippedChecks;
            public ViewEvidence[] readability;
            public int frames, routeSamples, publicPlacementTrials, respawns, adversarialRespawns;
            public float maximumRockScale, maximumRockCarrierDrift, maximumFlightStep;
            public int maximumVisibleDrops;
            public string[] observedMusicPhases;
        }
        static bool Requested() => Array.IndexOf(Environment.GetCommandLineArgs(), "-validate-beach-counterweight") >= 0;
        static bool AudioOnly => Array.IndexOf(Environment.GetCommandLineArgs(), "-counterweight-audio-only") >= 0;
        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        static float Number(string key, float fallback)
        {
            return float.TryParse(Argument(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Requested() || FindFirstObjectByType<BeachCounterweightValidation>() != null) return;
            var go = new GameObject("Counterweight harbor native regression");
            DontDestroyOnLoad(go); go.AddComponent<BeachCounterweightValidation>();
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var loaded = FindFirstObjectByType<BeachGame>(); if (loaded != null) loaded.automated = true;
            Application.runInBackground = true;
        }
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }
        void Ensure(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + "; phase=" + phase
                + "; feet=" + (game == null ? Vector3.zero : game.player.feet)
                + "; rock=" + (finale == null || finale.Rock == null ? Vector3.zero : finale.Rock.transform.position)
                + "; notice=" + (game == null ? "" : game.notice));
        }
        void Require(bool condition, string message)
        {
            Ensure(condition, message); checks.Add(message); Debug.Log("COUNTERWEIGHT_PASS " + message);
        }
        IEnumerator Start()
        {
            if (!Requested() || claimed) { Destroy(gameObject); yield break; }
            claimed = true; output = Path.GetFullPath(Argument("-counterweight-validation-output")
                ?? Path.Combine(Application.persistentDataPath, "BeachCounterweightValidation"));
            Directory.CreateDirectory(output);
            string reportPath = Path.Combine(output, "counterweight-results.json");
            if (File.Exists(reportPath)) { Debug.LogError("Refusing to overwrite a counterweight validation report"); Application.Quit(2); yield break; }
            Application.logMessageReceived += Log; SceneManager.sceneLoaded += SceneLoaded;
            string failure = null; var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool more = false; object item = null;
                try { more = stack.Peek().MoveNext(); if (more) item = stack.Peek().Current; }
                catch (Exception ex) { failure = ex.ToString(); break; }
                if (!more) { stack.Pop(); continue; }
                if (item is IEnumerator nested) { stack.Push(nested); continue; }
                yield return item;
            }
            if (failure != null && game != null && !AudioOnly) try { Capture("99-failure"); } catch (Exception ex) { details.Add("Capture failure: " + ex.Message); }
            if (changedSimulation) Physics.simulationMode = previousSimulation;
            SceneManager.sceneLoaded -= SceneLoaded; Application.logMessageReceived -= Log; Time.timeScale = 1;
            var report = new Report {
                passed = failure == null && errors.Count == 0 && checks.Count > 0,
                scope = "Focused native submerged covered-bridge regression. Public fourth-zone demo opens earlier gates. Declared shoreline pickup, checkpoint headroom, near-landing bypass and adversarial real-ship-deck player pose fixtures are setup only. Expected bypass respawns are reported separately and the ordinary-flow baseline resets afterwards. All rock acquisition, perspective growth, release, actual PhysX settling, hoist movement, latch protection, route walking and ship flight thereafter use public game commands. Placement search changes camera aim and uses the real motor; it never assigns stone transform, scale, kinematic state, held state, Ready or puzzle-completion flags. The explicit -counterweight-audio-only flag suppresses all camera rendering, depth reads and PNG capture; its omitted graphics assertions are listed as skipped rather than passed. Without that flag, the original fixed-camera depth and visibility assertions remain enabled. Not a full unassisted three-course playthrough, subjective sound approval or OS key-delivery test.",
                scene = SceneManager.GetActiveScene().name, unity = Application.unityVersion, failure = failure,
                checks = checks.ToArray(), details = details.ToArray(), errors = errors.ToArray(), fixtures = fixtures.ToArray(), readability = readability.ToArray(),
                audioOnly = AudioOnly, captureUsed = captureCount > 0, captureCount = captureCount, skippedChecks = skippedChecks.ToArray(),
                frames = frames, routeSamples = routeSamples, publicPlacementTrials = publicPlacementTrials,
                respawns = game == null ? 0 : game.respawns - initialRespawns, adversarialRespawns = adversarialRespawns,
                maximumRockScale = maximumScale, maximumRockCarrierDrift = maximumRockDrift, maximumFlightStep = maximumFlightStep,
                maximumVisibleDrops = maximumVisibleDrops, observedMusicPhases = observedMusicPhases.ToArray()
            };
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            File.WriteAllLines(Path.Combine(output, "counterweight-trace.csv"), trace);
            if (AudioOnly) File.WriteAllLines(Path.Combine(output, "ending-audio-trace.csv"), audioTrace);
            if (overviewCamera != null) Destroy(overviewCamera.gameObject);
            if (overviewDepth != null) Destroy(overviewDepth);
            Debug.Log("COUNTERWEIGHT_VALIDATION " + (report.passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " output=" + output);
            Application.Quit(report.passed ? 0 : 1);
        }
        IEnumerator Run()
        {
            if (SceneManager.GetActiveScene().name != "Beach")
            {
                Require(Application.CanStreamedLevelBeLoaded("Beach"), "Complete-course player includes the real Beach scene");
                yield return SceneManager.LoadSceneAsync("Beach", LoadSceneMode.Single);
            }
            yield return null; yield return null;
            game = FindFirstObjectByType<BeachGame>(); Ensure(game != null, "BeachGame is missing");
            finale = game.counterweightFinale; ship = game.skyShip;
            Ensure(finale != null && finale.Rock != null && ship != null, "Natural boulder, submerged bridge or real ship is missing");
            game.automated = true; Application.runInBackground = true; game.StartHarborFinaleDemo(); game.Resume();
            previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script; changedSimulation = true;
            waters = FindObjectsByType<BeachWater>(FindObjectsSortMode.None); initialRespawns = game.respawns;
            foreach (var water in waters) waterPositions.Add(water.transform, water.transform.position);
            home = finale.Rock.transform.position; homeScale = finale.Rock.transform.localScale;
            trace.Add("frame,phase,feetX,feetY,feetZ,rockX,rockY,rockZ,scale,held,supported,enoughWeight,lift,ready,flight");
            InitializeOverview(); CheckInitialReadability();
            if (AudioOnly) CheckInitialAudio();
            Require(game.harborFinale == null && game.castleRoute == null, "Submerged covered bridge replaces the retired miniature-island and castle finale");
            Require(game.props.Count(p => p == finale.Rock) == 1, "Exactly one original natural boulder is registered for reset and ordinary interaction");
            Require(!finale.Ready && !finale.LoadLatched && finale.LiftProgress < .001f && !ship.Flying, "Fresh finale starts fully submerged, unlatched and moored");
            Require(finale.cradleFloor != null && finale.cradleFloor.bounds.size.x >= 7.5f && finale.cradleFloor.bounds.size.z >= 5.5f, "Side receiver has a broad real physical floor");
            var natural = finale.Rock.WorldBounds;
            Require(natural.size.x >= 1 && natural.size.y >= 1 && natural.min.y > -.12f && natural.min.y < .12f,
                "Original boulder is already a substantial ground-resting coastal rock");
            Require(!finale.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("돌 전시") || t.name.Contains("돌 받침 철제 상판")),
                "Natural starting rock has no display pedestal");
            float span = Vector3.Distance(finale.BridgeNearPoint, finale.BridgeFarPoint);
            float maximumFlatJump = FirstPersonMotor.Speed * (2 * Mathf.Sqrt(2 * (FirstPersonMotor.JumpHeight + FirstPersonMotor.AdditionalJumpHeight) / FirstPersonMotor.Gravity) + .12f);
            details.Add("Disconnected span=" + span + " conservative flat-jump range=" + maximumFlatJump);
            Require(span > 18 && span > maximumFlatJump * 2, "Open-water gap exceeds eighteen metres and twice the conservative flat-ground jump range");
            Vector3 waterProbe = Vector3.Lerp(finale.BridgeNearPoint, finale.BridgeFarPoint, .5f); waterProbe.y = finale.WaterSurface + .01f;
            Require(waters.Any(w => w.Contains(waterProbe)) && BeachWater.InHazard(waterProbe), "Actual open-water gap is a contact hazard at its real surface");
            yield return BypassTrials();
            yield return CheckCheckpointHeadroom();
            phase = "actual ship launch gate fixture";
            Fixture(new Vector3(.4f, 3.06f, 128.4f), "Adversarial pre-completion pose on the actual ship deck beyond the fixed pier"); yield return Frames(6);
            foreach (var hit in Physics.RaycastAll(game.player.feet + Vector3.up * .18f, Vector3.down, .6f,
                BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore).Where(h => h.collider != game.controller).OrderBy(h => h.distance))
            {
                string owner = hit.collider.name;
                for (var parent = hit.collider.transform.parent; parent != null; parent = parent.parent) owner = parent.name + "/" + owner;
                details.Add("Adversarial actual-deck ground ray: owner=" + owner + " distance=" + hit.distance.ToString("F4", CultureInfo.InvariantCulture));
            }
            Require(ship.IsAboard && !ship.TryLaunch() && !ship.Flying, "Actual empty-handed ship rider cannot depart before raising the covered bridge");
            phase = "natural boulder pickup fixture"; PickupFixture(); yield return Frames(8);
            Require(game.FootSafe(game.player.feet), "Authored shore pickup point is protected by actual shade");
            phase = "ordinary pickup and cancel";
            if (AudioOnly) BeginAudioObservations();
            AimAndInteract(); Require(game.held == finale.Rock, "Ordinary camera-ray Interact grabs the original natural boulder");
            if (AudioOnly) Require(game.Audio.CountFor("prop.grab") == grabCueBaseline,
                "The natural boulder grab suppresses the unselected generic prop-grab sound");
            Require(game.referenceDistance < 3 && game.referenceDistance > .1f, "Natural boulder has a practical ordinary perspective reference distance");
            yield return Frames(30);
            Require(!finale.LoadLatched && finale.LiftProgress < .001f, "Held boulder cannot latch or actuate the submerged bridge");
            game.Cancel(); yield return Frames(8);
            Require(game.held == null && Vector3.Distance(home, finale.Rock.transform.position) < .08f
                && Vector3.Distance(homeScale, finale.Rock.transform.localScale) < .001f, "Ordinary cancel restores the natural boulder without solution injection");
            phase = "public perspective placement"; yield return Solve(false);
            Require(maximumScale >= homeScale.x * 1.8f && game.held == null, "Original natural boulder grows meaningfully through public perspective and release");
            Require(finale.LoadLatched && finale.LiftProgress > .999f, "Actual supported heavy load latches and raises the complete covered bridge");
            Require(Mathf.Abs(finale.cradle.position.y - (finale.cradleRaisedPosition.y - finale.travel)) < .04f
                && Vector3.Distance(finale.bridge.position, finale.bridgeRaisedPosition) < .04f, "Counterweight and complete bridge reach their opposing authored stops");
            Require(Vector3.Angle(finale.bridge.up, Vector3.up) < .05f && CanopyRelativeDrift() < .003f, "Deck, supports and canopy rise together as one rigid level assembly");
            Require(waterPositions.All(p => p.Key != null && Vector3.Distance(p.Key.position, p.Value) < .0001f), "Open-sea surface heights remain unchanged throughout lifting");
            bool actualRoute = finale.RouteShadeContinuous();
            details.Add("Completed physical route: result=" + actualRoute + " failure=" + finale.LastRouteFailure + " point=" + finale.LastShadeFailure.ToString("F4"));
            Require(finale.Ready && actualRoute && finale.Rock.WorldBounds.max.y < finale.WaterSurface,
                "Raised roof supplies the actual continuous safe route while the latched rock remains underwater");
            CheckFinalOverview();
            if (AudioOnly) CheckCompletedLiftAudio();
            phase = "underwater latched-load grab rejection";
            Vector3 rest = finale.Rock.transform.position; float lift = finale.LiftProgress;
            Look(finale.Rock.WorldBounds.center); game.UpdateAim();
            details.Add("Underwater load ordinary click target=" + (game.aimed == null ? "none" : game.aimed.title));
            game.Interact(); yield return Frames(30);
            maximumRockDrift = Vector3.Distance(rest, finale.Rock.transform.position);
            Require(game.held == null && finale.LoadLatched && finale.Ready && maximumRockDrift < .06f,
                "Ordinary click cannot release the latched underwater load or collapse the completed route");
            phase = "pause at completed mechanism"; game.Pause();
            Vector3 cradlePosition = finale.cradle.position, bridgePosition = finale.bridge.position;
            game.Tick(1, Vector2.zero);
            Require(!game.active && Time.timeScale == 0 && finale.cradle.position == cradlePosition
                && finale.bridge.position == bridgePosition && finale.LiftProgress == lift, "Ordinary pause freezes the completed physical mechanism");
            game.Resume();
            if (AudioOnly) Require(endingAudio.Running, "Ordinary resume restores the ending audio transport");
            phase = "real route floor and five-foot shadow samples"; CheckRoute();
            Require(!ship.IsAboard && !ship.TryLaunch(), "Completed bridge does not permit shore-side remote departure");
            phase = "continuous native boarding"; yield return ReturnToRouteStart();
            foreach (var point in finale.routePoints) yield return Travel(point, 1200);
            Vector3 middle = Vector3.Lerp(finale.BridgeNearPoint, finale.BridgeFarPoint, .5f) + Vector3.up * .025f;
            yield return Travel(finale.BridgeFarPoint + Vector3.up * .025f, 600);
            yield return Travel(middle, 1000); CheckOverhead("raised bridge", true); yield return JumpInPlace("raised covered bridge");
            yield return Travel(finale.BridgeFarPoint + Vector3.up * .025f, 1000);
            yield return Travel(new Vector3(.4f, 3.06f, 126.8f), 500);
            game.yaw = 0; game.SyncView();
            for (int i = 0; i < 120 && !ship.IsAboard; i++) { Step(Vector2.up * .2f); if (i % 20 == 19) yield return null; }
            Require(ship.IsAboard && game.held == null && game.respawns == initialRespawns, "Public motor crosses the actual covered route, jumps under its roof and boards without respawn");
            if (AudioOnly) CheckDryCanopyAudio();
            if (!AudioOnly) Capture("03-on-real-ship");
            Require(ship.TryLaunch() && ship.Flying && !game.controller.enabled, "Completed bridge plus actual boarding starts the real ship ending");
            phase = "actual twenty-four-second ending"; bool pausedFlight = false;
            for (int i = 0; i < 1460 && !game.won; i++)
            {
                Vector3 before = ship.transform.position; Step(); maximumFlightStep = Mathf.Max(maximumFlightStep, Vector3.Distance(before, ship.transform.position));
                if (!pausedFlight && ship.FlightTime > 17)
                {
                    float flightTime = ship.FlightTime; Vector3 position = ship.transform.position;
                    float musicGain = AudioOnly ? endingAudio.MusicSource.volume : 0;
                    float puzzleEnvelope = AudioOnly ? endingAudio.PuzzleMusicEnvelope : 0;
                    float environmentEnvelope = AudioOnly ? endingAudio.EndingEnvelope : 0;
                    int drops = AudioOnly ? endingAudio.Drips.TotalEmitted : 0;
                    game.Pause(); game.Tick(1, Vector2.zero);
                    if (AudioOnly) Require(!endingAudio.Running && !endingAudio.MusicSource.isPlaying
                        && endingAudio.MusicSource.volume == musicGain && endingAudio.PuzzleMusicEnvelope == puzzleEnvelope
                        && endingAudio.EndingEnvelope == environmentEnvelope && endingAudio.Drips.TotalEmitted == drops,
                        "Ordinary pause freezes the ending music envelope, source and actual canopy-drip simulation");
                    Require(ship.FlightTime == flightTime && ship.transform.position == position, "Ordinary pause freezes the final flight clock and ship");
                    game.Resume(); pausedFlight = true; if (!AudioOnly) Capture("04-sky-departure");
                }
                if (i % 20 == 19) yield return null;
            }
            Require(game.won && !game.active && ship.FlightTime == BeachSkyShip.FlightDuration
                && ship.EndingFade == 1 && ship.EndingTitleAlpha == 1 && maximumFlightStep < 1, "Actual twenty-four-second flight reaches the paused END screen continuously");
            if (AudioOnly) CheckCompletedFlightAudio();
            phase = "production reset"; game.ResetAll(); initialRespawns = game.respawns; game.Resume(); yield return Frames(2);
            Require(!game.won && !ship.Flying && game.controller.enabled && !finale.Ready && !finale.LoadLatched && finale.LiftProgress < .001f,
                "ResetAll restores controller, ship and fully submerged unlatched mechanism");
            Require(Vector3.Distance(finale.Rock.transform.position, home) < .08f
                && Vector3.Distance(finale.Rock.transform.localScale, homeScale) < .001f && game.checkpoint == 0,
                "ResetAll restores the same natural boulder and normal first beach checkpoint");
            Require(!ship.TryLaunch(), "Reset clears the departure prerequisite");
            if (AudioOnly) CheckResetAudio();
        }
        IEnumerator BypassTrials()
        {
            phase = "declared near-landing bypass attempts"; allowExpectedRespawns = true;
            foreach (var attempt in new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(-1.15f, 1), new Vector2(1.15f, 1) })
            {
                Vector3 start = finale.BridgeNearPoint - Vector3.forward * 1.1f + Vector3.right * attempt.x + Vector3.up * .025f;
                Fixture(start, attempt.y == 0 ? "Unsolved near-landing ordinary run fixture" : "Unsolved near-landing ordinary jump fixture");
                yield return Frames(8); int before = game.respawns; float maximumZ = game.player.feet.z;
                for (int i = 0; i < 330 && game.respawns == before; i++)
                {
                    Step(Vector2.up, attempt.y > 0 && i == 0); maximumZ = Mathf.Max(maximumZ, game.player.feet.z);
                    if (i % 20 == 19) yield return null;
                }
                int count = game.respawns - before; adversarialRespawns += count;
                details.Add("Actual unsolved bypass: edgeX=" + attempt.x + " jump=" + (attempt.y > 0)
                    + " maxZ=" + maximumZ + " respawns=" + count + " returned=" + game.player.feet.ToString("F4"));
                Require(count == 1 && maximumZ < finale.BridgeFarPoint.z - 2 && !ship.IsAboard && !ship.Flying,
                    "Unsolved public " + (attempt.y == 0 ? "run" : "jump") + " at X offset " + attempt.x + " cannot bypass the long open-water gap");
                Require(Vector2.Distance(new Vector2(game.player.feet.x, game.player.feet.z),
                    new Vector2(finale.ObservationPoint.x, finale.ObservationPoint.z)) < .4f,
                    "Failed bypass returns the player to the real observation shore");
            }
            allowExpectedRespawns = false; initialRespawns = game.respawns;
        }
        IEnumerator CheckCheckpointHeadroom()
        {
            phase = "declared checkpoint canopy clearance fixtures";
            for (int i = 0; i < BeachWorld.Checkpoints.Length; i++)
            {
                Fixture(BeachWorld.Checkpoints[i] + Vector3.up * .025f, "Independent checkpoint " + (i + 1) + " physical headroom fixture");
                yield return Frames(8); CheckOverhead("checkpoint " + (i + 1), false);
                yield return JumpInPlace("checkpoint " + (i + 1));
            }
        }
        void CheckOverhead(string label, bool requireBridgeRoof)
        {
            var hits = Physics.RaycastAll(game.player.feet + Vector3.up * .2f, Vector3.up, 10,
                BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider != game.controller && h.collider.enabled).OrderBy(h => h.distance).ToArray();
            float clearance = hits.Length == 0 ? float.PositiveInfinity : hits[0].distance + .2f;
            details.Add("Actual overhead " + label + ": clearance=" + clearance + " firstCollider="
                + (hits.Length == 0 ? "open vertical headroom" : hits[0].collider.name));
            Require(clearance >= (requireBridgeRoof ? 4.1f : 3.9f), label + " has generous genuine physical headroom near the four-metre canopy target");
            if (requireBridgeRoof)
                Require(hits.Length > 0 && hits[0].collider.transform.IsChildOf(finale.bridge),
                    "Actual upward PhysX ray hits the lifted bridge roof rather than an invisible shade exemption");
        }
        IEnumerator JumpInPlace(string label)
        {
            float initialY = game.player.feet.y, apex = initialY;
            for (int i = 0; i < 100; i++)
            {
                Step(Vector2.zero, i == 0); apex = Mathf.Max(apex, game.player.feet.y);
                if (i > 50 && game.player.grounded) break;
                if (i % 20 == 19) yield return null;
            }
            details.Add("Public jump " + label + ": apexAboveFloor=" + (apex - initialY) + " landed=" + game.player.grounded);
            Require(apex - initialY > .6f && game.player.grounded && Mathf.Abs(game.player.feet.y - initialY) < .15f,
                label + " permits an actual full standing jump and safe landing without a low roof collision");
        }
        Bounds FullBridgeBounds()
        {
            var renderers = finale.bridge.GetComponentsInChildren<Renderer>(true);
            Ensure(renderers.Length > 0, "Complete bridge has no rendered physical assembly");
            var result = renderers[0].bounds; foreach (var renderer in renderers) result.Encapsulate(renderer.bounds);
            return result;
        }
        float CanopyRelativeDrift()
        {
            float result = 0;
            foreach (var pair in canopyRelative)
                result = Mathf.Max(result, Vector3.Distance(pair.Value, pair.Key.transform.position - finale.bridge.position));
            return result;
        }
        IEnumerable<Vector3> OpenWaterPoints()
        {
            for (int i = 0; i < 9; i++)
            {
                Vector3 point = Vector3.Lerp(finale.BridgeNearPoint, finale.BridgeFarPoint, .08f + i * .105f);
                point.y = finale.WaterSurface + .01f; yield return point;
            }
        }
        IEnumerable<Vector3> CanopySurfacePoints()
        {
            return MeshSurfacePoints(finale.BridgeCanopyRenderers.Where(r => r != null)
                .Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null), 64);
        }
        IEnumerator Solve(bool repeated)
        {
            var rock = finale.Rock;
            AimAndInteract(); Require(game.held == rock, "Public pickup begins perspective placement");
            float initialLift = finale.LiftProgress;
            // Move only the player through the authored safe work area. Keep the
            // receiver in view while carrying the stone clear of its display
            // stand and then widening the ordinary perspective projection.
            Vector3 carryAim = new Vector3(finale.cradleFloor.bounds.center.x, finale.cradleFloor.bounds.max.y + 2.7f, finale.cradleFloor.bounds.center.z + .3f);
            yield return CarryTo(finale.clearPoint, carryAim, 320);
            Require(Vector2.Distance(new Vector2(game.player.feet.x, game.player.feet.z),
                new Vector2(finale.clearPoint.x, finale.clearPoint.z)) < .18f && game.held == rock
                && Mathf.Abs(finale.LiftProgress - initialLift) < .001f,
                "Ordinary sideways motor movement carries the held rock clear of its display stand without actuating the hoist");
            yield return Frames(12);
            Vector3 adjustment = new Vector3(Number("-counterweight-adjustment-x", finale.adjustmentPoint.x), finale.adjustmentPoint.y,
                Number("-counterweight-adjustment-z", finale.adjustmentPoint.z));
            yield return CarryTo(adjustment, carryAim, 320);
            Require(Mathf.Abs(finale.LiftProgress - initialLift) < .001f, "Native protected work-area movement enlarges held stone without moving the mechanism");
            var floor = finale.cradleFloor.bounds;
            // These parameters affect camera aim only; the production resolver alone chooses actual stone scale.
            float desiredScale = Number("-counterweight-scale", 6.0f);
            float aimX = Number("-counterweight-aim-x", floor.center.x + .5f);
            float aimZ = Number("-counterweight-aim-z", floor.center.z + .3f);
            float aimY = Number("-counterweight-aim-y", floor.max.y + desiredScale * .45f);
            bool released = false;
            foreach (var shift in new[] { Vector3.zero, new Vector3(0, .12f, 0), new Vector3(0, -.12f, 0), new Vector3(-.25f, 0, 0), new Vector3(.25f, 0, 0), new Vector3(0, 0, .35f), new Vector3(0, 0, -.35f) })
            {
                Vector3 target = new Vector3(aimX, aimY, aimZ) + shift;
                Look(target); yield return Frames(70); publicPlacementTrials++;
                maximumScale = Mathf.Max(maximumScale, rock.transform.localScale.x);
                details.Add("Public trial " + publicPlacementTrials + ": eye=" + game.view.transform.position.ToString("F4") + " target=" + target.ToString("F4")
                    + " actual=" + rock.transform.position.ToString("F4") + " scale=" + rock.transform.localScale.x.ToString("F4", CultureInfo.InvariantCulture)
                    + " bounds=" + rock.WorldBounds + " invalid=" + game.invalidReason + " obstacle=" + game.PlacementObstacle
                    + " resolver=" + PlacementDiagnostic());
                var bounds = rock.WorldBounds;
                bool nearTray = bounds.center.x > floor.min.x - .4f && bounds.center.x < floor.max.x + .4f
                    && bounds.center.z > floor.min.z - .4f && bounds.center.z < floor.max.z + .4f && bounds.min.y >= floor.max.y - .3f && bounds.min.y <= floor.max.y + 2.2f;
                if (nearTray && rock.transform.localScale.x >= homeScale.x * 1.8f && game.Drop())
                {
                    released = true; acceptedAim = target; acceptedPlayer = game.player.feet; break;
                }
            }
            Ensure(released, "No public perspective trial found a releasable receiver placement");
            details.Add("Accepted public release: player=" + acceptedPlayer + " aim=" + acceptedAim + " root=" + rock.transform.position + " scale=" + rock.transform.localScale);
            if (!AudioOnly) Capture("00-public-perspective-release");
            bool paused = false, intermediate = false;
            Vector3 previousStone = rock.transform.position;
            for (int i = 0; i < 1500 && !finale.Ready; i++)
            {
                Step();
                Ensure(Vector3.Distance(previousStone, rock.transform.position) < 1.2f, "Stone moved discontinuously during real settling / guided hoist");
                previousStone = rock.transform.position;
                if (finale.LiftProgress > .02f && finale.LiftProgress < .98f)
                {
                    intermediate = true;
                    if (!midLiftObserved && finale.LiftProgress >= .6f && finale.LiftProgress <= .8f) CheckMidLiftReadability();
                    if (!paused)
                    {
                        float progress = finale.LiftProgress; Vector3 stone = rock.transform.position;
                        game.Pause(); game.Tick(.5f, Vector2.zero);
                        Require(finale.LiftProgress == progress && rock.transform.position == stone, "Pause freezes an intermediate guided lift");
                        game.Resume(); paused = true;
                    }
                }
                if (i % 20 == 19) yield return null;
            }
            details.Add("After settle: position=" + rock.transform.position + " scale=" + rock.transform.localScale + " bounds=" + rock.WorldBounds
                + " supported=" + finale.LoadSupported + " weight=" + finale.EnoughWeight + " lift=" + finale.LiftProgress + " ready=" + finale.Ready
                + " routeFailure=" + finale.LastRouteFailure + " shadeFailure=" + finale.LastShadeFailure.ToString("F4")
                + " rockShadeSamples=" + finale.LastRockShadeSamples + " movementBlocked=" + finale.MovementBlocked
                + " movementObstacle=" + finale.LastMovementObstacle
                + " placement=" + (rock.placement == null ? "missing" : rock.placement.Status));
            foreach (var hit in Physics.RaycastAll(new Vector3(rock.WorldBounds.center.x, rock.WorldBounds.min.y + .12f, rock.WorldBounds.center.z),
                Vector3.down, 2.5f, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.collider.attachedRigidbody == rock.body) continue;
                string owner = hit.collider.name;
                for (var parent = hit.collider.transform.parent; parent != null; parent = parent.parent) owner = parent.name + "/" + owner;
                details.Add("After settle support ray: owner=" + owner + " distance=" + hit.distance.ToString("F4", CultureInfo.InvariantCulture)
                    + " point=" + hit.point.ToString("F4") + " normal=" + hit.normal.ToString("F4"));
            }
            Require(intermediate && paused, "Hoist has observable gradual travel and pausing, rather than an instant completion flag");
            Require(finale.LoadLatched && finale.Ready, "A public gravity-settled load latches and automatically completes the genuinely roof-shaded route");
            details.Add("Actual latched load offset from chosen receiver center=" + (rock.transform.position - finale.cradleFloor.bounds.center).ToString("F4"));

        }
        static readonly string[] EndingKeys = {
            "rock_small", "rock_medium", "rock_large", "load_latch", "pulley", "water_rise", "bridge_stop",
            "canopy_drips", "splash_small", "splash_medium", "splash_large", "departure", "takeoff", "flight", "departure_bgm"
        };
        void CheckInitialAudio()
        {
            endingAudio = game.Audio == null ? null : game.Audio.Ending;
            Require(endingAudio != null && endingAudio.Running, "The active beach installs and runs the scene-local selected ending audio");
            Require(EndingKeys.All(key => endingAudio.ClipFor(key) != null),
                "All fifteen selected ending AudioClips are actually loaded from the player Resources bank");
            Require(endingAudio.ClipFor("departure_bgm").length > 150,
                "HB02 uses the full Wanderlust music source rather than a twenty-four-second audition excerpt");
            Require(endingAudio.Drips != null && endingAudio.Drips.RoofEdgeCount > 0
                && endingAudio.Drips.VisibleDropCount == 0 && !endingAudio.Drips.HasEmerged,
                "The genuinely submerged canopy starts with configured eaves and zero visible water drops");
            Require(endingAudio.MusicPhase == "Puzzle" && endingAudio.PuzzleMusicEnvelope > .99f
                && endingAudio.CountFor("departure_bgm") == 0,
                "The unsolved shoreline keeps existing BB01 without starting the departure music");
            Require(BeachEndingAudio.LargeRockGain < .34f && BeachEndingAudio.FlightGain < .10f,
                "Selected large-rock HF06 and flight HF31 gains are reduced from ordinary impact and movement levels");
            audioTrace.Add("frame,lift,ready,flight,phase,puzzleEnvelope,endingEnvelope,musicGain,visibleDrops,totalDrops");
        }
        void BeginAudioObservations()
        {
            endingCueBaseline.Clear();
            foreach (string key in EndingKeys) endingCueBaseline[key] = endingAudio.CountFor(key);
            grabCueBaseline = game.Audio.CountFor("prop.grab");
            maximumVisibleDrops = 0; observedWetCanopy = observedDryCanopy = observedMusicAfterPuzzleSilence = observedAudibleDeparture = false;
            observedMusicPhases.Clear(); previousEndingMusicGain = float.NaN; previousPuzzleEnvelope = endingAudio.PuzzleMusicEnvelope;
            Require(endingAudio.RockCueFor(finale.Rock, false) == "rock_medium",
                "The original substantial natural boulder selects the user's medium-rock HF08 band before perspective growth");
        }
        int CueDelta(string key) => endingAudio.CountFor(key) - (endingCueBaseline.TryGetValue(key, out int n) ? n : 0);
        void ObserveAudioState()
        {
            Ensure(endingAudio.PuzzleMusicEnvelope >= 0 && endingAudio.PuzzleMusicEnvelope <= 1
                && endingAudio.EndingEnvelope >= 0 && endingAudio.EndingEnvelope <= 1,
                "Audio envelopes remain finite and in their valid unit range");
            observedMusicPhases.Add(endingAudio.MusicPhase);
            var drips = endingAudio.Drips;
            if (drips != null)
            {
                maximumVisibleDrops = Mathf.Max(maximumVisibleDrops, drips.VisibleDropCount);
                if (!drips.HasEmerged)
                    Ensure(drips.VisibleDropCount == 0, "No water drops may be rendered under the wholly submerged canopy");
                if (drips.VisibleDropCount > 0)
                {
                    observedWetCanopy = true;
                    Ensure(drips.HasVisibleGeometry && drips.DropRenderer != null && drips.DropRenderer.enabled,
                        "Visible-drop counts must correspond to an enabled real water-drop mesh");
                    Ensure(drips.LastSpawnPosition.y > finale.WaterSurface + .05f,
                        "Actual canopy water drops originate above the unchanged sea surface");
                }
                if (observedWetCanopy && finale.Ready && drips.Wetness <= .001f && drips.VisibleDropCount == 0)
                    observedDryCanopy = true;
            }
            if (ship.Flying)
            {
                Ensure(endingAudio.PuzzleMusicEnvelope <= previousPuzzleEnvelope + .002f,
                    "Existing BB01 cannot grow louder again during the final ship departure");
                previousPuzzleEnvelope = endingAudio.PuzzleMusicEnvelope;
                if (endingAudio.CountFor("departure_bgm") > 0 && endingAudio.MusicSource.volume > .001f)
                {
                    Ensure(endingAudio.PuzzleMusicEnvelope <= .001f,
                        "HB02 may become audible only after the existing puzzle music has fully faded out");
                    observedMusicAfterPuzzleSilence = true; observedAudibleDeparture = true;
                }
                if (ship.FlightTime >= 14.1f)
                {
                    float gain = endingAudio.MusicSource.volume;
                    if (!float.IsNaN(previousEndingMusicGain))
                        Ensure(gain <= previousEndingMusicGain + .0002f, "The selected departure music fades monotonically as the distant ship reaches the ending");
                    previousEndingMusicGain = gain;
                }
            }
            if (frames % 15 == 0)
                audioTrace.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2},{3:F4},{4},{5:F4},{6:F4},{7:F4},{8},{9}",
                    frames, finale.LiftProgress, finale.Ready, ship.FlightTime, endingAudio.MusicPhase,
                    endingAudio.PuzzleMusicEnvelope, endingAudio.EndingEnvelope, endingAudio.MusicSource.volume,
                    drips == null ? 0 : drips.VisibleDropCount, drips == null ? 0 : drips.TotalEmitted));
        }
        void CheckCompletedLiftAudio()
        {
            Require(new[] { "load_latch", "pulley", "water_rise", "bridge_stop", "canopy_drips" }.All(key => CueDelta(key) == 1),
                "The real gravity-settled lift plays HF11, HF14, HF17, HF20 and HF22 exactly once at their own mechanism stages");
            Require(endingAudio.PulleySource != null && !endingAudio.PulleySource.isPlaying,
                "The real hoist stops its pulley loop at the physical upper stop");
            Require(CueDelta("rock_small") + CueDelta("rock_medium") + CueDelta("rock_large") == 0,
                "The supported counterweight tray adds no unselected rock landing or extra seat-impact sound");
            Require(game.Audio.CountFor("prop.grab") == grabCueBaseline,
                "Both public natural-rock grabs remain silent instead of reusing the generic object grab");
            Require(endingAudio.RockCueFor(finale.Rock, false) == "rock_large",
                "The genuinely perspective-enlarged boulder selects the user's quieter large-rock HF06 band");
            Require(observedWetCanopy && maximumVisibleDrops > 0,
                "The actual emerging canopy creates visible above-water drop geometry during its real upward travel");
        }
        void CheckDryCanopyAudio()
        {
            Require(observedDryCanopy && endingAudio.Drips.Wetness <= .001f && endingAudio.Drips.VisibleDropCount == 0,
                "Wet-canopy water drops visibly finish drying instead of raining forever after the player walks the full route");
            Require(endingAudio.CanopyDripsSource != null && !endingAudio.CanopyDripsSource.isPlaying,
                "HF22 fades and stops when the physical wet-canopy effect has dried");
        }
        void CheckCompletedFlightAudio()
        {
            Require(new[] { "departure", "takeoff", "flight", "departure_bgm" }.All(key => CueDelta(key) == 1),
                "The public ship launch starts HF26, HF29, quiet HF31 and full HB02 exactly once");
            Require(observedMusicAfterPuzzleSilence && observedAudibleDeparture,
                "The actual flight enables a nonzero HB02 source envelope only after BB01 reaches silence");
            Require(observedMusicPhases.Contains("FadingPuzzle") && observedMusicPhases.Contains("FadingInDeparture")
                && observedMusicPhases.Contains("FadingEnding"),
                "The real flight clock traverses separate puzzle fade-out, departure fade-in and ending fade-out phases");
            Require(endingAudio.PuzzleMusicEnvelope <= .001f && endingAudio.MusicSource.volume <= .003f
                && endingAudio.EndingEnvelope <= .01f,
                "The real twenty-four-second END screen leaves music silent and the final environment faded away");
            Require(captureCount == 0 && readability.Count == 0 && skippedChecks.Count == 6,
                "The explicit audio-only regression performs zero camera captures and reports all six omitted graphics assertions");
        }
        void CheckResetAudio()
        {
            Require(endingAudio.Running && endingAudio.MusicPhase == "Puzzle" && endingAudio.PuzzleMusicEnvelope > .99f
                && endingAudio.EndingEnvelope > .99f && !endingAudio.MusicSource.isPlaying,
                "ResetAll restores ordinary BB01 transport and clears the departure music instead of leaving the ending mute");
            Require(endingAudio.Drips.VisibleDropCount == 0 && endingAudio.Drips.TotalEmitted == 0
                && !endingAudio.PulleySource.isPlaying && !endingAudio.CanopyDripsSource.isPlaying,
                "ResetAll removes every wet-canopy drop and stops both continuous mechanism sound sources");
        }
        void PickupFixture()
        {
            Fixture(new Vector3(Number("-counterweight-pickup-x", finale.PickupPoint.x), finale.PickupPoint.y, Number("-counterweight-pickup-z", finale.PickupPoint.z)),
                "Initial safe fixed-workpier close-pickup pose");
        }
        void Fixture(Vector3 point, string reason)
        {
            fixtures.Add(reason + ": " + point.ToString("F4")); game.PlacePlayer(point);
        }
        void Step(Vector2 move = default, bool jump = false)
        {
            game.Tick(Dt, move, jump);
            foreach (var water in waters) water.Simulate(Dt);
            Physics.Simulate(Dt); Physics.SyncTransforms();
            foreach (var prop in game.props) if (prop != null && prop.gameObject.activeInHierarchy) prop.placement.Tick(Dt);
            game.lighting.Synchronize(); frames++;
            if (AudioOnly && endingAudio != null) ObserveAudioState();
            Ensure(allowExpectedRespawns || game.respawns == initialRespawns, "Unexpected heat/water respawn interrupts the ordinary focused flow");
            if (frames % 30 == 0)
                trace.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9},{10},{11},{12:F4},{13},{14:F4}",
                    frames, phase, game.player.feet.x, game.player.feet.y, game.player.feet.z, finale.Rock.transform.position.x,
                    finale.Rock.transform.position.y, finale.Rock.transform.position.z, finale.Rock.transform.localScale.x,
                    game.held != null, finale.LoadSupported, finale.EnoughWeight, finale.LiftProgress, finale.Ready, ship.FlightTime));
        }
        IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { Step(); if (i % 20 == 19) yield return null; }
        }
        IEnumerator Travel(Vector3 target, int limit)
        {
            Vector3 start = game.player.feet;
            for (int i = 0; i < limit; i++)
            {
                Vector2 delta = new Vector2(target.x - game.player.feet.x, target.z - game.player.feet.z);
                if (delta.magnitude < .13f && Mathf.Abs(target.y - game.player.feet.y) < .24f && game.player.grounded) yield break;
                if (delta.magnitude > .04f) game.yaw = Mathf.Atan2(delta.x, delta.y);
                Step(delta.magnitude < .04f ? Vector2.zero : delta.normalized * Mathf.Sqrt(Mathf.Clamp01(delta.magnitude / .6f)));
                if (i % 20 == 19) yield return null;
            }
            throw new InvalidOperationException("Native route blocked from " + start + " toward " + target + " actual=" + game.player.feet);
        }
        IEnumerator ReturnToRouteStart()
        {
            Ensure(finale.routePoints != null && finale.routePoints.Length > 3, "Authored return route is missing");
            int nearest = Enumerable.Range(0, finale.routePoints.Length)
                .OrderBy(i => Vector3.SqrMagnitude(game.player.feet - finale.routePoints[i])).First();
            details.Add("Public motor return to full route start via authored waypoint " + nearest
                + " from " + game.player.feet.ToString("F4"));
            // The new viewing deck sits to the side of the open sea. Rejoin its
            // actual connector instead of cutting diagonally toward the beach.
            for (int i = nearest; i >= 0; i--) yield return Travel(finale.routePoints[i], 1000);
        }
        IEnumerator CarryTo(Vector3 target, Vector3 viewTarget, int limit)
        {
            Vector3 start = game.player.feet;
            for (int i = 0; i < limit; i++)
            {
                Vector2 delta = new Vector2(target.x - game.player.feet.x, target.z - game.player.feet.z);
                if (delta.magnitude < .13f && Mathf.Abs(target.y - game.player.feet.y) < .24f && game.player.grounded) yield break;
                Look(viewTarget);
                // BeachGame.Tick takes a world-space move vector, as its normal
                // Update path already converts the player's WASD input to world.
                Vector2 direction = delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.zero;
                Step(direction * (.7f * Mathf.Sqrt(Mathf.Clamp01(delta.magnitude / .6f))));
                if (i % 20 == 19) yield return null;
            }
            throw new InvalidOperationException("Native held-rock work-area route blocked from " + start + " toward " + target + " actual=" + game.player.feet);
        }
        string PlacementDiagnostic()
        {
            try
            {
                var property = typeof(BeachGame).GetProperty("Interaction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var interaction = property == null ? null : property.GetValue(game) as ObjectInteraction<BeachProp>;
                if (interaction == null) return "unavailable";
                var placement = interaction.LastPlacement;
                return "limiter=" + placement.Limiter + " source=" + (placement.Source == null ? "none" : placement.Source.name)
                    + " depth=" + placement.Depth.ToString("F4", CultureInfo.InvariantCulture)
                    + " surfaceDepth=" + placement.SurfaceDepth.ToString("F4", CultureInfo.InvariantCulture)
                    + " boundaryDepth=" + placement.BoundaryDepth.ToString("F4", CultureInfo.InvariantCulture)
                    + " roomMaximum=" + placement.RoomMaximum.ToString("F4", CultureInfo.InvariantCulture)
                    + " feasible=" + placement.Feasible;
            }
            catch (Exception ex) { return "diagnostic unavailable: " + ex.GetType().Name + ": " + ex.Message; }
        }
        void Look(Vector3 point)
        {
            Vector3 direction = point - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude); game.SyncView();
        }
        void AimAndInteract()
        {
            foreach (var part in finale.Rock.parts.OrderBy(p => Vector3.Distance(p.bounds.center, game.view.transform.position)))
            {
                if (!part.enabled) continue; Look(part.bounds.center); game.UpdateAim();
                if (game.aimed == finale.Rock) { game.Interact(); return; }
            }
            throw new InvalidOperationException("Camera cannot target actual rock from " + game.view.transform.position);
        }
        void CheckRoute()
        {
            Ensure(finale.routePoints != null && finale.routePoints.Length > 3, "Physical route points are missing");
            for (int section = 0; section + 1 < finale.routePoints.Length; section++)
            {
                Vector3 start = finale.routePoints[section], end = finale.routePoints[section + 1];
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / .23f));
                Vector3 right = Vector3.Cross(Vector3.up, end - start).normalized;
                for (int j = 0; j <= count; j++) foreach (float side in new[] { -.22f, 0, .22f })
                {
                    Vector3 sample = Vector3.Lerp(start, end, (float)j / count) + right * side;
                    var hits = Physics.RaycastAll(sample + Vector3.up * .3f, Vector3.down, .8f, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider != game.controller && h.normal.y > .5f && PlacementSupport.IsUsableGround(h.collider))
                        .OrderBy(h => h.distance).ToArray();
                    Ensure(hits.Length > 0, "Actual route has no usable floor near " + sample);
                    Vector3 feet = hits[0].point + Vector3.up * .025f;
                    Ensure(game.FootSafe(feet) && !BeachWater.InHazard(feet), "Actual five-foot shadow / water gap near " + feet);
                    routeSamples++;
                }
            }
            Require(routeSamples > 40, "Dense actual support / five-foot shadow samples cover the boarding route and joins: " + routeSamples);
        }

        void InitializeOverview()
        {
            if (AudioOnly)
            {
                Ensure(finale.BridgeCanopyRenderers != null && finale.BridgeCanopyRenderers.Length > 0, "Bridge canopy renderer API is missing");
                foreach (var renderer in finale.BridgeCanopyRenderers)
                    if (renderer != null) canopyRelative[renderer] = renderer.transform.position - finale.bridge.position;
                details.Add("Explicit audio-only mode: no validation camera is created and no camera/depth/PNG capture is performed.");
                return;
            }
            var node = new GameObject("Validation-only fixed initial-view camera");
            overviewCamera = node.AddComponent<Camera>();
            overviewCamera.CopyFrom(game.view);
            overviewCamera.enabled = false;
            overviewCamera.transform.SetPositionAndRotation(game.view.transform.position, game.view.transform.rotation);
            overviewCamera.aspect = 1280f / 720f;
            overviewCradleStart = finale.cradle.position;
            overviewBridgeStart = finale.bridge.position;
            Ensure(finale.BridgeCanopyRenderers != null && finale.BridgeCanopyRenderers.Length > 0, "Bridge canopy renderer API is missing");
            foreach (var renderer in finale.BridgeCanopyRenderers) if (renderer != null) canopyRelative[renderer] = renderer.transform.position - finale.bridge.position;
            details.Add("Actual initial camera: eye=" + overviewCamera.transform.position.ToString("F4")
                + " forward=" + overviewCamera.transform.forward.ToString("F4")
                + " verticalFov=" + overviewCamera.fieldOfView.ToString("F2", CultureInfo.InvariantCulture));
        }
        void ReadOverviewDepth()
        {
            Ensure(!AudioOnly, "Audio-only validation must not render or read a camera buffer");
            Ensure(overviewCamera != null, "Fixed overview camera is missing");
            var shader = Shader.Find("Parallax/PlacementDepth");
            Ensure(shader != null, "Existing linear-depth shader is missing");
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.RFloat);
            texture.filterMode = FilterMode.Point;
            var oldTarget = overviewCamera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldFlags = overviewCamera.clearFlags;
            var oldBackground = overviewCamera.backgroundColor;
            float oldFar = Shader.GetGlobalFloat("_PlacementPreviewFar");
            try
            {
                overviewCamera.targetTexture = texture;
                overviewCamera.clearFlags = CameraClearFlags.SolidColor;
                overviewCamera.backgroundColor = Color.white;
                overviewCamera.allowHDR = false; overviewCamera.allowMSAA = false;
                Shader.SetGlobalFloat("_PlacementPreviewFar", overviewCamera.farClipPlane);
                overviewCamera.RenderWithShader(shader, "");
                RenderTexture.active = texture;
                if (overviewDepth == null)
                    overviewDepth = new Texture2D(1280, 720, TextureFormat.RFloat, false, true);
                overviewDepth.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); overviewDepth.Apply();
            }
            finally
            {
                Shader.SetGlobalFloat("_PlacementPreviewFar", oldFar);
                overviewCamera.targetTexture = oldTarget; overviewCamera.clearFlags = oldFlags;
                overviewCamera.backgroundColor = oldBackground; RenderTexture.active = oldActive;
                texture.Release(); Destroy(texture);
            }
        }
        bool MatchesRenderedDepth(Vector3 viewport)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(viewport.x * 1280), 0, 1279);
            int y = Mathf.Clamp(Mathf.FloorToInt(viewport.y * 720), 0, 719);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                float actual = overviewDepth.GetPixel(Mathf.Clamp(x + dx, 0, 1279),
                    Mathf.Clamp(y + dy, 0, 719)).r * overviewCamera.farClipPlane;
                if (Mathf.Abs(actual - viewport.z) <= .12f) return true;
            }
            return false;
        }
        bool MatchesWaterPlaneDepth(Vector3 viewport)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(viewport.x * 1280), 0, 1279);
            int y = Mathf.Clamp(Mathf.FloorToInt(viewport.y * 720), 0, 719);
            // At a grazing angle one water pixel covers metres along the plane.
            // Compare the depth of that exact pixel's centre ray, rather than
            // the unsnapped world probe or a neighbouring pixel's different ray.
            Ray ray = overviewCamera.ViewportPointToRay(new Vector3((x + .5f) / 1280f, (y + .5f) / 720f, 0));
            if (ray.direction.y >= -.00001f) return false;
            float distance = (finale.WaterSurface - ray.origin.y) / ray.direction.y;
            if (distance <= 0) return false;
            Vector3 point = ray.GetPoint(distance);
            float minZ = Mathf.Min(finale.BridgeNearPoint.z, finale.BridgeFarPoint.z);
            float maxZ = Mathf.Max(finale.BridgeNearPoint.z, finale.BridgeFarPoint.z);
            float halfWidth = BridgeFloor().bounds.extents.x;
            if (point.z < minZ || point.z > maxZ || Mathf.Abs(point.x - finale.BridgeNearPoint.x) > halfWidth) return false;
            if (!waters.Any(w => w != null && w.Contains(point) && Mathf.Abs(w.surface - finale.WaterSurface) < .03f)) return false;
            float expected = overviewCamera.WorldToViewportPoint(point).z;
            float actual = overviewDepth.GetPixel(x, y).r * overviewCamera.farClipPlane;
            bool visible = Mathf.Abs(actual - expected) <= .12f;
            details.Add("Exact water pixel (" + x + "," + y + "): planePoint=" + point.ToString("F4")
                + " expectedViewDepth=" + expected.ToString("F4", CultureInfo.InvariantCulture)
                + " actualViewDepth=" + actual.ToString("F4", CultureInfo.InvariantCulture) + " visible=" + visible);
            return visible;
        }
        ViewEvidence MeasureFeature(string label, IEnumerable<Vector3> source, bool waterPlane = false)
        {
            var points = source.ToArray();
            var indices = new List<int>();
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            var result = new ViewEvidence { phase = phase, feature = label, samples = points.Length };
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 at = overviewCamera.WorldToViewportPoint(points[i]);
                if (at.z <= overviewCamera.nearClipPlane || at.x < .02f || at.x > .98f || at.y < .02f || at.y > .98f) continue;
                result.inFrame++;
                if (!(waterPlane ? MatchesWaterPlaneDepth(at) : MatchesRenderedDepth(at))) continue;
                indices.Add(i); min = Vector2.Min(min, new Vector2(at.x, at.y)); max = Vector2.Max(max, new Vector2(at.x, at.y));
            }
            result.visible = indices.Count; result.visibleIndices = indices.ToArray();
            if (result.visible > 0)
            {
                result.minViewportX = min.x; result.maxViewportX = max.x;
                result.minViewportY = min.y; result.maxViewportY = max.y;
                result.pixelWidth = (max.x - min.x) * 1280; result.pixelHeight = (max.y - min.y) * 720;
            }
            result.centerVisible = indices.Contains(4);
            readability.Add(result);
            details.Add("Rendered first-view visibility " + label + ": visible=" + result.visible + "/" + result.samples
                + " inFrame=" + result.inFrame + " center=" + result.centerVisible + " visiblePixelExtent="
                + result.pixelWidth.ToString("F1", CultureInfo.InvariantCulture) + "x" + result.pixelHeight.ToString("F1", CultureInfo.InvariantCulture)
                + " viewport=(" + result.minViewportX.ToString("F3", CultureInfo.InvariantCulture) + ","
                + result.minViewportY.ToString("F3", CultureInfo.InvariantCulture) + ")..("
                + result.maxViewportX.ToString("F3", CultureInfo.InvariantCulture) + ","
                + result.maxViewportY.ToString("F3", CultureInfo.InvariantCulture) + ")");
            return result;
        }
        IEnumerable<Vector3> DeckTopPoints(BoxCollider floor)
        {
            Ensure(floor != null, "Visible deck's real floor is missing");
            foreach (float z in new[] { -.32f, 0, .32f }) foreach (float x in new[] { -.32f, 0, .32f })
                yield return floor.transform.TransformPoint(floor.center
                    + new Vector3(floor.size.x * x, floor.size.y * .5f, floor.size.z * z)) + floor.transform.up * .015f;
        }
        BoxCollider BridgeFloor() => finale.BridgeFloor;
        IEnumerable<Vector3> MeshSurfacePoints(IEnumerable<MeshFilter> filters, int perMesh)
        {
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                int stride = Mathf.Max(1, triangles.Length / 3 / Mathf.Max(1, perMesh));
                for (int face = 0; face < triangles.Length / 3; face += stride)
                {
                    int i = face * 3;
                    Vector3 point = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3;
                    yield return filter.transform.TransformPoint(point);
                }
            }
        }
        IEnumerable<Vector3> HullSurfacePoints()
        {
            string[] names = { "StylShip_Body", "StylShip_Wall", "StylShip_Prow" };
            return MeshSurfacePoints(ship.GetComponentsInChildren<MeshFilter>().Where(f => names.Contains(f.name)), 96);
        }
        IEnumerable<Vector3> CradleFramePoints()
        {
            var filters = finale.cradle.GetComponentsInChildren<MeshFilter>();
            var frame = filters.Where(f => f.name.Contains("테두리") || f.name.Contains("모서리 철물")
                || f.name.Contains("하부 받침보")).ToArray();
            return MeshSurfacePoints(frame.Length > 0 ? frame : filters, 32);
        }
        void CheckInitialReadability()
        {
            phase = "actual initial open-water readability";
            if (AudioOnly)
            {
                Bounds bounds = FullBridgeBounds();
                details.Add("Initial complete bridge world bounds=" + bounds + " waterSurface=" + finale.WaterSurface);
                Require(bounds.max.y < finale.WaterSurface - 1,
                    "Entire initial deck, supports and canopy are genuinely below the water surface");
                Require(game.FootSafe(game.player.feet), "Actual initial coastal viewpoint has real shade");
                skippedChecks.AddRange(new[] {
                    "Actual initial water occludes the submerged bridge deck in rendered depth",
                    "Actual initial view reveals the broad empty side receiver",
                    "Actual initial view shows empty open water through the future bridge corridor",
                    "Actual initial view includes substantial real ship hull surfaces beyond the open-water gap"
                });
                return;
            }
            Capture("00-first-player-view"); Capture(overviewCamera, "00-first-fixed-view"); ReadOverviewDepth();
            var pan = MeasureFeature("empty side receiving tray", DeckTopPoints(finale.cradleFloor));
            var water = MeasureFeature("open water over submerged bridge", OpenWaterPoints(), true);
            var hull = MeasureFeature("real ship hull surfaces", HullSurfacePoints());
            var submergedDeck = MeasureFeature("submerged bridge deck must be hidden", DeckTopPoints(BridgeFloor()));
            Bounds all = FullBridgeBounds();
            details.Add("Initial complete bridge world bounds=" + all + " waterSurface=" + finale.WaterSurface);
            Require(all.max.y < finale.WaterSurface - 1,
                "Entire initial deck, supports and canopy are genuinely below the water surface");
            Require(submergedDeck.visible == 0, "Actual initial water occludes the submerged bridge deck in rendered depth");
            Require(game.FootSafe(game.player.feet), "Actual initial coastal viewpoint has real shade");
            Require(pan.visible >= 3 && pan.pixelWidth >= 35, "Actual initial view reveals the broad empty side receiver");
            Require(water.visible >= 3, "Actual initial view shows empty open water through the future bridge corridor");
            Require(hull.visible >= 6 && hull.pixelWidth >= 30 && hull.pixelHeight >= 8,
                "Actual initial view includes substantial real ship hull surfaces beyond the open-water gap");
        }
        void CheckMidLiftReadability()
        {
            phase = "same-view complete bridge emergence";
            if (AudioOnly)
            {
                Require(finale.LoadLatched && finale.cradle.position.y < finale.cradleRaisedPosition.y - 1
                    && finale.bridge.position.y > finale.bridgeRaisedPosition.y - finale.travel + 1,
                    "Real supported load latches before the counterweight descends and complete bridge rises");
                Require(CanopyRelativeDrift() < .003f, "Actual canopy and physical deck preserve their relative pose during emergence");
                skippedChecks.Add("Same initial camera sees the genuinely emerging canopy above open water");
                midLiftObserved = true;
                return;
            }
            Capture(overviewCamera, "01-fixed-view-emerging-canopy"); ReadOverviewDepth();
            var canopy = MeasureFeature("emerging actual canopy surfaces", CanopySurfacePoints());
            details.Add("Actual opposing lift: trayDelta=" + (finale.cradle.position - overviewCradleStart).ToString("F4")
                + " completeBridgeDelta=" + (finale.bridge.position - overviewBridgeStart).ToString("F4")
                + " canopyRelativeDrift=" + CanopyRelativeDrift().ToString("F5", CultureInfo.InvariantCulture));
            Require(finale.LoadLatched && finale.cradle.position.y < overviewCradleStart.y - 1
                && finale.bridge.position.y > overviewBridgeStart.y + 1,
                "Real supported load latches before the counterweight descends and complete bridge rises");
            Require(CanopyRelativeDrift() < .003f, "Actual canopy and physical deck preserve their relative pose during emergence");
            Require(canopy.visible >= 3, "Same initial camera sees the genuinely emerging canopy above open water");
            overviewMidCaptured = midLiftObserved = true;
        }
        void CheckFinalOverview()
        {
            phase = "same-view completed covered bridge";
            if (AudioOnly)
            {
                Ensure(midLiftObserved, "Intermediate physical emergence must be observed in audio-only mode");
                skippedChecks.Add("Same initial view preserves empty-water, emergence and complete covered-bridge stages");
                Require(finale.RoofClearance >= 4.1f && BridgeFloor().bounds.size.z > 18,
                    "Final complete bridge provides a long real walking floor and high authored canopy");
                return;
            }
            Capture(overviewCamera, "02-fixed-view-raised-covered-bridge"); ReadOverviewDepth();
            var canopy = MeasureFeature("raised actual canopy surfaces", CanopySurfacePoints());
            Require(overviewMidCaptured && canopy.visible >= 6, "Same initial view preserves empty-water, emergence and complete covered-bridge stages");
            Require(finale.RoofClearance >= 4.1f && BridgeFloor().bounds.size.z > 18,
                "Final complete bridge provides a long real walking floor and high authored canopy");
        }
        void Capture(string name) { Capture(game.view, name); }
        void Capture(Camera camera, string name)
        {
            Ensure(!AudioOnly, "Audio-only validation must not call PNG capture");
            captureCount++;
            foreach (var preview in FindObjectsByType<BeachPlacementPreview>(FindObjectsSortMode.None)) preview.Refresh();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var texture = new RenderTexture(1280, 720, 24); Texture2D pixels = null;
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; texture.Release(); Destroy(texture); if (pixels != null) Destroy(pixels);
            }
        }
    }
}
