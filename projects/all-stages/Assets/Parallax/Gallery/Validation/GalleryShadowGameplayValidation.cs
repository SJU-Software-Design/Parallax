using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Player-only pickup fixtures are disclosed in the report. Positive props
    // are never teleported, resized or declared placed by this validation.
    public sealed class GalleryShadowGameplayValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90, Grid = .25f, MinX = -5.75f;
        const int Width = 47, MaximumTicks = 16000;
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<CarryReport> carries = new List<CarryReport>();
        readonly List<RouteReport> routes = new List<RouteReport>();
        GalleryMuseumShadowCourse course;
        string output;
        double deadline;
        int ticks;
        SimulationMode previousSimulation;
        bool ownsSimulation, subscribed;

        [Serializable] sealed class CarryReport
        {
            public string tool;
            public Vector3 pickupFixture, observerFixture, originalPosition, releasedPosition, settledPosition;
            public float originalScale, requestedScale, maximumInputScale, releasedScale, settledScale, actualCarryMetres;
            public int carryTicks, settleTicks;
        }
        [Serializable] sealed class RouteReport
        {
            public string room;
            public Vector3[] path;
            public bool[] plannedFootSafe;
            public int gridNodes, walkTicks;
            public float length, longestUnshadedRun, maximumExposure;
            public Vector3 longestUnsafeStart, longestUnsafeEnd, closestStartSafePoint, closestGoalSafePoint;
            public int safeComponents, startSafeComponent, goalSafeComponent;
            public float safeComponentGap;
            public string gridDiagnosticsFile;
        }
        [Serializable] sealed class Report
        {
            public bool passed, initialSixTools, homeStatueRejected, closedDoorSolid, heldStatueRejected, unsettledStatueRejected;
            public bool bronzeInputReleased, bronzePhysicallySettled, bronzeCourseWalk;
            public bool statueInputReleased, statuePhysicallySettled, fixedLightStillIlluminates, originalScanShadowMesh, physicalDoorOpened, nikeCourseWalk;
            public bool shadeOnlyRejected, wrongKeySizeRejected, keyDocked, keyReset;
            public bool physicsRestored;
            public string phase, failure, unity, gpu;
            public string scope = "Actual production museum with one original bronze and one original movable Nike and a fixed spotlight. Player-only angular-size pickup and observer fixtures are disclosed. Positive tools are moved and resized only through camera Interact, actual CharacterController carry, the common perspective resolver, public Drop and PhysX; their transforms, scales, rigidbody poses and placement readiness are never forced. Shadow routes are selected from actual floor, capsule and FootSafe queries, then walked by the actual CharacterController without player warps during a crossing. The closed-door negative uses an explicitly positioned player while Nike remains at its unsolved home pose. Held and freshly dropped Nike negatives are sampled during the same actual input sequence. CPU shadow/goal probes and screenshots are reported separately from carrying and walking. This is focused integration QA, not an autonomous unassisted puzzle solution or a GPU/CPU pixel-parity claim.";
            public int simulatedTicks, initialPropCount, respawns, checkpoint;
            public float simulatedSeconds, shadeReadiness;
            public CarryReport[] carries;
            public RouteReport[] routes;
            public string[] checks, errors;
        }
        sealed class Navigation
        {
            public Vector3[] points;
            public bool[] clear, safe;
            public int rows;
            public float minZ;
            public List<Vector3> path;
            public float length, longestUnsafe;
        }
        struct OpenNode : IComparable<OpenNode>
        {
            public int id;
            public float cost;
            public OpenNode(int index, float value) { id = index; cost = value; }
            public int CompareTo(OpenNode other)
            { int order = cost.CompareTo(other.cost); return order != 0 ? order : id.CompareTo(other.id); }
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log; subscribed = true;
            previousSimulation = Physics.simulationMode;
            deadline = Time.realtimeSinceStartupAsDouble + 90;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            var sequence = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Guard(); more = sequence.MoveNext(); if (more) next = sequence.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break;
                yield return next;
            }
            RestoreSimulation(); report.physicsRestored = Physics.simulationMode == previousSimulation;
            report.simulatedTicks = ticks; report.simulatedSeconds = ticks * Dt;
            report.carries = carries.ToArray(); report.routes = routes.ToArray();
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            if (game != null && !report.nikeCourseWalk) { report.respawns = game.respawns; report.checkpoint = game.checkpoint; }
            report.passed = report.failure == null && errors.Count == 0 && report.initialSixTools &&
                report.homeStatueRejected && report.closedDoorSolid && report.heldStatueRejected && report.unsettledStatueRejected &&
                report.bronzeInputReleased && report.bronzePhysicallySettled && report.bronzeCourseWalk &&
                report.statueInputReleased && report.statuePhysicallySettled && report.fixedLightStillIlluminates && report.originalScanShadowMesh &&
                report.shadeOnlyRejected && report.wrongKeySizeRejected && report.keyDocked && report.physicalDoorOpened &&
                report.nikeCourseWalk && report.keyReset && report.physicsRestored;
            try
            {
                if (output != null)
                {
                    if (!report.passed && game != null) Capture("shadow-gameplay-failure");
                    File.WriteAllText(Path.Combine(output, "shadow-gameplay-results.json"), JsonUtility.ToJson(report, true));
                }
            }
            catch (Exception exception) { report.passed = false; report.failure = "Cannot write validation artifact: " + exception; }
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_SHADOW_GAMEPLAY_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_SHADOW_GAMEPLAY_FAIL phase=" + report.phase + " " + report.failure + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "GalleryShadowGameplayValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.conceptLayout && !game.exhibitionPreview,
                "Actual automated playable concept museum is required");
            course = game.shadowCourse;
            Require(course != null && course.bronze != null && course.statue != null && game.nikeLight != null,
                "Production bronze, movable Nike and fixed spotlight are required");
            Require(game.controller != null && game.view != null && game.player != null, "Actual first-person rig is required");
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            yield return null;
            game.ResetAll(); game.Resume();
            for (int i = 0; i < 90; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            report.initialPropCount = game.props.Length;
            Require(game.props.Length == 6 && Array.FindAll(game.props, p => p != null && p.zone == 0).Length == 1 &&
                Array.FindAll(game.props, p => p != null && p.zone == 1).Length == 1,
                "Fresh layout must have six tools: bronze, Nike, portrait three and exit sculpture");
            Require(Vector3.Distance(course.bronze.transform.position, course.bronze.homePosition) < .02f &&
                Vector3.Distance(course.statue.transform.position, course.statue.homePosition) < .02f,
                "Input-positive tools did not begin at their original home poses");
            report.initialSixTools = true;
            Check("Fresh playable museum contains one original bronze and one original movable Nike, six tools total");

            report.phase = "initial unsolved Nike and actual closed-door controller collision";
            game.nikeLight.ResetLight();
            Require(!game.nikeLight.SolutionReady && !course.NikeUnlocked && !course.NikeGateOpen,
                "Nike home pose already unlocks the exit");
            report.homeStatueRejected = true;
            Vector3 gate = course.NikeGatePosition;
            game.checkpoint = 1;
            game.PlacePlayer(new Vector3(gate.x, 0, gate.z - .8f));
            Require(game.CanStand(game.player.feet), "Closed-door approach player fixture is obstructed");
            game.yaw = game.pitch = 0; game.SyncView();
            int beforeRespawns = game.respawns;
            for (int i = 0; i < 30; i++) { Step(Vector2.up); if (i % 6 == 0) yield return null; }
            bool enabledBarrier = false;
            foreach (var box in course.NikeGateColliders) if (box != null && box.enabled) enabledBarrier = true;
            Require(enabledBarrier && !course.NikeUnlocked && !course.NikeGateOpen &&
                game.player.feet.z < gate.z - .12f && game.respawns == beforeRespawns,
                "Actual controller bypassed the closed walnut door or the collision sample respawned");
            report.closedDoorSolid = true;
            Check("The home statue leaves the physical walnut door closed and the CharacterController cannot cross it");

            // End the isolated negative, restoring only original authored poses.
            // Positive sequences below contain no prop transform/scale writes.
            game.ResetAll(); game.Resume();
            for (int i = 0; i < 30; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            report.phase = "bronze actual pickup/carry/perspective enlargement/Drop/PhysX";
            var bronzeInput = CarryOriginal(course.bronze, new Vector3(0, .006f, 7.15f), 2.1f, new Vector3(0, 0, 2.8f), false);
            while (bronzeInput.MoveNext()) yield return bronzeInput.Current;
            CaptureView("shadow-gameplay-01-bronze-input", new Vector3(5.9f, 4.4f, 12), new Vector3(0, 1.6f, 8.3f));
            var firstWalk = PlanAndWalk(new Vector2(0, 14.8f), "bronze room");
            while (firstWalk.MoveNext()) yield return firstWalk.Current;
            Require(course.TutorialCleared && game.checkpoint == 1, "Input-created bronze shade did not permit the first real crossing");
            report.bronzeCourseWalk = true;
            Check("Original bronze is enlarged and released through actual input, settles in PhysX, and its real shade supports a controller crossing without respawn");

            report.phase = "spotlight actual pickup/carry/perspective placement/Drop/PhysX";
            var emitter = game.nikeLight.emitter;
            Vector3 sourcePosition = emitter.position;
            Quaternion sourceRotation = emitter.rotation;
            Require(game.nikeLight.fixedLamp != null && emitter.IsChildOf(game.nikeLight.fixedLamp) &&
                game.nikeLight.fixedLamp.GetComponentInParent<GalleryProp>() == null, "Light fixture can be picked up");
            var canvas = game.nikeLight.canvas;
            Require(canvas != null && !canvas.Complete && !canvas.portablePainting.gameObject.activeSelf,
                "The initial exhibit must show its silhouette before ejecting the painting");
            CaptureView("shadow-gameplay-02-nike-silhouette", GalleryNikeCanvas.DemoPosition + Vector3.up * 1.65f, new Vector3(0, 1.65f, 24));
            var nikeInput = CarryOriginal(course.statue, GalleryNikeCanvas.SolutionPosition, GalleryNikeCanvas.SolutionScale,
                new Vector3(-2.30f, 0, 20.40f), true);
            while (nikeInput.MoveNext()) yield return nikeInput.Current;
            Require(Vector3.Distance(sourcePosition, emitter.position) < .001f &&
                Quaternion.Angle(sourceRotation, emitter.rotation) < .01f, "The spotlight moved while carrying Nike");
            game.nikeLight.SyncPose();
            report.shadeReadiness = game.nikeLight.ShadeReadiness;
            Mesh original = Resources.Load<Mesh>("MuseumScans/NikeMesh");
            Require(Array.Exists(course.statue.GetComponentsInChildren<MeshFilter>(), filter => filter.sharedMesh == original),
                "The movable statue must keep the original Nike render mesh");
            Require(canvas.Fusing && report.shadeReadiness >= canvas.RequiredMatch,
                "A settled, contained shadow must start the fusion");
            report.originalScanShadowMesh = true;
            bool sawBurst = false;
            int burstTicks = 0;
            for (int i = 0; i < 240 && !canvas.Complete; i++)
            {
                Step(Vector2.zero);
                if (!sawBurst && canvas.portablePainting.gameObject.activeSelf)
                {
                    Require(canvas.Fusing && canvas.fusionGlow.gameObject.activeSelf && !course.statue.gameObject.activeSelf,
                        "Burst must consume the statue and eject the canvas");
                    sawBurst = true;
                }
                if (sawBurst && ++burstTicks == 14)
                    CaptureView("shadow-gameplay-03-nike-fusion", new Vector3(-.82f, 1.7f, 17.7f), new Vector3(-4.9f, 2.15f, 26));
                if (i % 8 == 0) yield return null;
            }
            Require(sawBurst && canvas.Complete && !course.NikeUnlocked && !canvas.shadeMarks.activeSelf &&
                canvas.portablePainting.placement.Ready && Array.IndexOf(game.props, canvas.portablePainting) >= 0,
                "The emerged painting must be interactable while the door stays closed");
            report.phase = "Nike shade alone does not unlock the door";
            var key = canvas.portablePainting;
            var socket = course.nikeSocket;
            Require(socket != null && Array.TrueForAll(socket.splitPainting, half => !half.activeSelf), "The door key socket must be empty after fusion");
            Vector3 shadePoint = new Vector3(0, .025f, 28.2f);
            Require(game.nikeLight.IsIlluminated(shadePoint, Vector3.up), "The painting's arrival must leave the exit lit");
            report.scope += " The shade-only negative uses a temporary painting pose, restored before the real key pickup.";
            Vector3 shadeFixture = new Vector3(-3.90f, key.homePosition.y, 22.25f);
            key.transform.position = shadeFixture;
            key.body.position = shadeFixture;
            Physics.SyncTransforms();
            key.placement.ResetPlaced();
            for (int i = 0; i < 24; i++) Step(Vector2.zero);
            Require(game.nikeLight.PaintingCoversExit() && !course.NikeUnlocked && !course.NikeGateOpen,
                "A settled painting casting real shade over the exit must leave the key door closed");
            report.shadeOnlyRejected = true;
            key.Restore();

            report.phase = "Nike key actual pickup, scale adjustment and docking";
            game.PlacePlayer(new Vector3(-4.48f, 0, 22.55f));
            Grab(key);
            var walk = WalkKeyToward(new Vector2(0, 26.1f), socket);
            while (walk.MoveNext()) yield return walk.Current;
            Require(socket.Targeted(game.view) && key.transform.localScale.x < GalleryNikeDoorSocket.TargetScale &&
                socket.FitHint(key) != null && !game.Drop() && game.held == key && !course.NikeUnlocked,
                "An undersized painting should remain held when clicking the door socket");
            report.wrongKeySizeRejected = true;
            walk = WalkKeyToward(new Vector2(0, 24.05f), socket);
            while (walk.MoveNext()) yield return walk.Current;
            string pose = " at " + key.transform.position.ToString("F3") + " scale=" + key.transform.localScale.x +
                " obstacle=" + game.PlacementObstacle;
            Require(socket.FitHint(key) == null, "The resized painting did not fit the door socket" + pose);
            CaptureView("shadow-gameplay-04-nike-key-fit", game.view.transform.position, socket.Center);
            Require(game.Drop() && game.held == null, "Public Drop refused the fitted painting key" + pose);
            for (int i = 0; i < 240 && !course.NikeGateOpen; i++) { Step(Vector2.zero); if (i % 8 == 0) yield return null; }
            Require(course.NikeUnlocked && course.NikeGateOpen && socket.Seated && !key.gameObject.activeSelf &&
                Array.IndexOf(game.props, key) < 0, "The fitted painting should be consumed by the opened door");
            for (int i = 0; i < socket.splitPainting.Length; i++)
            {
                var half = socket.splitPainting[i];
                Require(half.activeInHierarchy && half.transform.IsChildOf(course.NikeGateLeaves[i]),
                    "Each visible frame half should move with its wooden door leaf");
            }
            report.keyDocked = true;
            report.physicalDoorOpened = true;
            CaptureView("shadow-gameplay-05-nike-split-door", new Vector3(2.6f, 2.2f, 23.8f), socket.Center);
            Check("Real shade and an undersized key leave the door closed; camera input fits the resized painting and both halves open with the door");
            var secondWalk = PlanAndWalk(new Vector2(0, 29.5f), "Nike spotlight room");
            while (secondWalk.MoveNext()) yield return secondWalk.Current;
            Require(course.NikeCleared && game.checkpoint == 2 && game.player.feet.z > 29.3f,
                "Actual controller did not cross the opened door into the portrait room");
            report.nikeCourseWalk = true;
            Check("Actual controller walks through the opened walnut door to the portrait checkpoint without respawn");
            report.checkpoint = game.checkpoint; report.respawns = game.respawns;
            game.ResetAll();
            Require(!canvas.Complete && course.statue.gameObject.activeSelf && !key.gameObject.activeSelf &&
                !socket.Seated && !course.NikeUnlocked && Array.TrueForAll(socket.splitPainting, half => !half.activeSelf) &&
                Array.IndexOf(game.props, course.statue) >= 0 && Array.IndexOf(game.props, key) < 0,
                "Reset must restore the statue and close the door with an empty socket");
            report.keyReset = true;
            Check("Restart restores the movable statue and an empty door socket");
        }

        IEnumerator WalkKeyToward(Vector2 destination, GalleryNikeDoorSocket socket)
        {
            int start = ticks;
            while (true)
            {
                Vector2 delta = destination - Flat(game.player.feet);
                if (delta.magnitude < .055f) break;
                Look(socket.Center);
                Step(delta.normalized * Mathf.Sqrt(Mathf.Clamp01(delta.magnitude / 1.2f)));
                Require(ticks - start < 1000, "Actual key carry could not reach " + destination);
                if (ticks % 8 == 0) yield return null;
            }
            for (int i = 0; i < 48; i++)
            {
                Look(socket.Center);
                Step(Vector2.zero);
                if (i % 8 == 0) yield return null;
            }
            Require(game.held == socket.canvas.portablePainting, "Actual carry lost the painting key");
        }

        IEnumerator CarryOriginal(GalleryProp prop, Vector3 goalBase, float goalScale, Vector3 observer, bool nike)
        {
            Require(game.held == null && prop.placement.Ready, "Original input tool must begin released and ready: " + prop.title);
            var observation = new CarryReport { tool = prop.title, originalPosition = prop.transform.position,
                originalScale = prop.transform.localScale.x, requestedScale = goalScale, observerFixture = observer };
            carries.Add(observation);
            Require(Vector3.Distance(observation.originalPosition, prop.homePosition) < .02f &&
                Mathf.Abs(observation.originalScale - prop.homeScale.x) < .01f,
                "Input-positive tool is not at its original authored pose: " + prop.title);
            const float eyeAboveFixture = 1.575f; // PlacePlayer adds .025m; SyncView adds 1.55m.
            Vector3 observerEye = observer + Vector3.up * eyeAboveFixture;
            float reference = observation.originalScale / goalScale * Vector3.Distance(goalBase, observerEye);
            float vertical = prop.transform.position.y - (observer.y + eyeAboveFixture);
            float radicand = reference * reference - vertical * vertical;
            Require(radicand > .20f, "Requested angular size has no reachable floor pickup fixture: " + prop.title);
            Vector3 pickup = new Vector3(prop.transform.position.x, observer.y, prop.transform.position.z - Mathf.Sqrt(radicand));
            observation.pickupFixture = pickup;
            Require(game.CanStand(pickup) && game.CanStand(observer), "Player-only pickup/observer fixture is obstructed: " +
                prop.title + " pickup=" + pickup + " observer=" + observer);
            game.PlacePlayer(pickup);
            int respawns = game.respawns;
            for (int i = 0; i < 24; i++) { Step(Vector2.zero); if (i % 8 == 0) yield return null; }
            Require(Vector3.Distance(prop.transform.position, observation.originalPosition) < .02f, "Original tool moved before camera pickup");
            Grab(prop);
            Require(Mathf.Abs(game.referenceScale - observation.originalScale) < .001f, "Public pickup did not freeze original angular scale");
            Debug.Log("SHADOW_INPUT_PICKUP tool=" + prop.title + " playerFixture=" + pickup.ToString("F4") +
                " observerFixture=" + observer.ToString("F4") + " reference=" + game.referenceDistance + " targetBase=" + goalBase);
            int startTicks = ticks;
            Vector3 previousFeet = game.player.feet;
            while (true)
            {
                Vector2 delta = Flat(observer) - Flat(game.player.feet);
                if (delta.magnitude < .055f) break;
                Look(goalBase); Step(delta.normalized * Mathf.Sqrt(Mathf.Clamp01(delta.magnitude / 1.2f)));
                observation.actualCarryMetres += Vector3.Distance(previousFeet, game.player.feet); previousFeet = game.player.feet;
                observation.maximumInputScale = Mathf.Max(observation.maximumInputScale, prop.transform.localScale.x);
                Require(game.held == prop && prop.body.isKinematic && game.respawns == respawns,
                    "Actual carry lost its tool or respawned: " + prop.title);
                if (nike)
                {
                    Require(!game.nikeLight.Ready && !game.nikeLight.SolutionReady && !course.NikeUnlocked,
                        "Carried Nike counted as a settled goal solution");
                    report.heldStatueRejected = true;
                    Require(prop.renderers[0].sharedMaterial.GetTag("RenderType", false) == "Opaque",
                        "The carried Nike needs an opaque depth-pass silhouette");
                }
                Require(ticks - startTicks < 1500, "Input carry did not reach its observer position: " + prop.title);
                if (ticks % 8 == 0) yield return null;
            }
            for (int i = 0; i < 48; i++)
            {
                Look(goalBase); Step(Vector2.zero);
                observation.maximumInputScale = Mathf.Max(observation.maximumInputScale, prop.transform.localScale.x);
                Require(game.held == prop && game.respawns == respawns, "Braking lost the original tool");
                if (ticks % 8 == 0) yield return null;
            }
            observation.carryTicks = ticks - startTicks;
            Require(observation.actualCarryMetres > .5f, "Positive carry did not include real CharacterController movement");
            if (nike)
            {
                for (int z = 23; z <= 28; z++) for (int x = -3; x <= 3; x++)
                    if (game.nikeLight.IsIlluminated(new Vector3(x, .025f, z), Vector3.up)) report.fixedLightStillIlluminates = true;
                Require(report.fixedLightStillIlluminates && !game.nikeLight.SolutionReady,
                    "Fixed spotlight must illuminate the scene while Nike is held without unlocking the door");
            }
            Require(game.Drop() && game.held == null, "Public Drop refused input tool: " + prop.title + " " + game.invalidReason + " " + game.PlacementObstacle);
            observation.releasedPosition = prop.transform.position; observation.releasedScale = prop.transform.localScale.x;
            Require(!prop.placement.Ready, "Freshly dropped input prop was incorrectly treated as already settled");
            if (nike)
            {
                report.statueInputReleased = true; game.nikeLight.SyncPose();
                Require(!game.nikeLight.Ready && !game.nikeLight.SolutionReady && !course.NikeUnlocked,
                    "Freshly dropped Nike opened the door before settling");
                report.unsettledStatueRejected = true;
            }
            else if (prop == course.bronze)
            {
                report.bronzeInputReleased = true;
                Require(observation.releasedScale > observation.originalScale * 1.5f,
                    "Bronze did not actually enlarge through the shared perspective input resolver");
            }
            for (int i = 0; i < 600 && !prop.placement.Ready; i++)
            {
                Look(goalBase); Step(Vector2.zero); observation.settleTicks++;
                Require(game.respawns == respawns && game.held == null, "Released tool settling respawned or reacquired the tool");
                if (i % 8 == 0) yield return null;
            }
            Require(prop.placement.Ready && game.ValidPlacement(prop, out _),
                "Input-released tool did not physically settle: " + prop.title + " " + prop.placement.Status);
            observation.settledPosition = prop.transform.position; observation.settledScale = prop.transform.localScale.x;
            if (nike) report.statuePhysicallySettled = true; else if (prop == course.bronze) report.bronzePhysicallySettled = true;
            Debug.Log("SHADOW_INPUT_SETTLED tool=" + prop.title + " position=" + prop.transform.position.ToString("F4") +
                " scale=" + prop.transform.localScale.x + " carryMetres=" + observation.actualCarryMetres + " status=" + prop.placement.Status);
        }

        IEnumerator PlanAndWalk(Vector2 goal, string room)
        {
            report.phase = room + " real shadow route planning and CharacterController walking";
            var navigation = Plan(Flat(game.player.feet), goal);
            var observation = new RouteReport { room = room, path = navigation.path == null ? Array.Empty<Vector3>() : navigation.path.ToArray(), gridNodes = navigation.points.Length,
                length = navigation.length, longestUnshadedRun = navigation.longestUnsafe };
            routes.Add(observation);
            // Persist topology before eligibility assertions, including a failed
            // route. Otherwise a genuine disconnected shadow cannot be located.
            WriteRouteDiagnostics(navigation, observation, Flat(game.player.feet), goal);
            Require(navigation.path != null && navigation.path.Count > 1, "No capsule-clear physical route in " + room);
            Require(navigation.longestUnsafe <= 1.05f, "Input-created shade has an unsafe run of " + navigation.longestUnsafe + "m in " + room +
                " from " + observation.longestUnsafeStart.ToString("F3") + " to " + observation.longestUnsafeEnd.ToString("F3"));
            int startRespawns = game.respawns, startTicks = ticks, segment = 0, progressTick = ticks;
            Vector2 progressAnchor = Flat(game.player.feet);
            bool requireShade = navigation.longestUnsafe < .001f;
            while (Vector2.Distance(Flat(game.player.feet), goal) > .085f)
            {
                Vector2 feet = Flat(game.player.feet);
                segment = NearestSegment(navigation.path, segment, feet);
                Vector2 target = Flat(navigation.path[segment + 1]);
                for (float lookAhead = .85f; lookAhead >= .10f; lookAhead *= .5f)
                {
                    Vector2 candidate = Pursuit(navigation.path, segment, feet, lookAhead);
                    if (ClearSteering(feet, candidate, requireShade)) { target = candidate; break; }
                }
                Vector2 delta = target - feet;
                float approach = Mathf.Sqrt(Mathf.Clamp01(Vector2.Distance(feet, goal) / .8f));
                if (!game.FootSafe(game.player.feet)) approach = 1;
                Look(new Vector3(target.x, game.view.transform.position.y, target.y)); Step(delta.normalized * approach);
                observation.maximumExposure = Mathf.Max(observation.maximumExposure, game.exposure);
                Require(game.respawns == startRespawns, "Actual shadow walk respawned in " + room + " at " + feet + " toward " + target);
                if (Vector2.Distance(Flat(game.player.feet), progressAnchor) > .08f)
                { progressAnchor = Flat(game.player.feet); progressTick = ticks; }
                Require(ticks - progressTick < 180, "Actual controller is stuck in " + room + " at " + game.player.feet + " toward " + target);
                Require(ticks - startTicks < 2500, "Actual walking route exceeded its input budget in " + room);
                if (ticks % 8 == 0) yield return null;
            }
            for (int i = 0; i < 24; i++) { Step(Vector2.zero); if (i % 8 == 0) yield return null; }
            Require(game.respawns == startRespawns, "Braking after the physical shadow crossing respawned");
            observation.walkTicks = ticks - startTicks;
        }

        void WriteRouteDiagnostics(Navigation nav, RouteReport observation, Vector2 start, Vector2 goal)
        {
            string stem = observation.room.StartsWith("Nike", StringComparison.Ordinal) ? "nike" : "bronze";
            observation.gridDiagnosticsFile = "shadow-route-" + stem + ".csv";
            observation.plannedFootSafe = new bool[observation.path.Length];
            float unsafeRun = 0, longest = 0; int spanStart = 0;
            for (int i = 0; i < observation.path.Length; i++)
            {
                observation.plannedFootSafe[i] = game.FootSafe(observation.path[i]);
                if (i == 0) continue;
                if (observation.plannedFootSafe[i - 1] && observation.plannedFootSafe[i]) { unsafeRun = 0; spanStart = i; }
                else
                {
                    if (unsafeRun == 0) spanStart = i - 1;
                    unsafeRun += Vector3.Distance(observation.path[i - 1], observation.path[i]);
                    if (unsafeRun > longest)
                    {
                        longest = unsafeRun;
                        observation.longestUnsafeStart = observation.path[spanStart];
                        observation.longestUnsafeEnd = observation.path[i];
                    }
                }
            }
            int[] components = SafeComponents(nav, out int componentCount);
            observation.safeComponents = componentCount;
            int from = Nearest(nav, start), to = Nearest(nav, goal);
            observation.startSafeComponent = from >= 0 ? components[from] : -1;
            observation.goalSafeComponent = to >= 0 ? components[to] : -1;
            float gapSquared = float.PositiveInfinity;
            if (observation.startSafeComponent >= 0 && observation.goalSafeComponent >= 0)
            {
                if (observation.startSafeComponent == observation.goalSafeComponent) gapSquared = 0;
                else for (int a = 0; a < nav.points.Length; a++) if (components[a] == observation.startSafeComponent)
                    for (int b = 0; b < nav.points.Length; b++) if (components[b] == observation.goalSafeComponent)
                    {
                        float distance = (nav.points[a] - nav.points[b]).sqrMagnitude;
                        if (distance < gapSquared)
                        {
                            gapSquared = distance; observation.closestStartSafePoint = nav.points[a]; observation.closestGoalSafePoint = nav.points[b];
                        }
                    }
            }
            observation.safeComponentGap = float.IsPositiveInfinity(gapSquared) ? -1 : Mathf.Sqrt(gapSquared);
            using (var writer = new StreamWriter(Path.Combine(output, observation.gridDiagnosticsFile), false, new UTF8Encoding(false)))
            {
                writer.WriteLine("x,z,feetY,capsuleClear,footSafe,safeComponent,nikeShadow,lampIlluminated,blockingColliders");
                for (int id = 0; id < nav.points.Length; id++)
                {
                    Vector3 point = new Vector3(MinX + id % Width * Grid, nav.clear[id] ? nav.points[id].y : .025f, nav.minZ + id / Width * Grid);
                    string blockers = nav.clear[id] ? "" : BlockingNames(point);
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3},{3},{4},{5},{6},{7},\"{8}\"",
                        point.x, point.z, point.y, nav.clear[id] ? 1 : 0, nav.safe[id] ? 1 : 0, components[id],
                        game.nikeLight.IsNikeShadow(point) ? 1 : 0, game.nikeLight.IsIlluminated(point, Vector3.up) ? 1 : 0,
                        blockers.Replace("\"", "\"\"")));
                }
            }
            File.WriteAllText(Path.Combine(output, "shadow-route-" + stem + ".json"), JsonUtility.ToJson(observation, true));
            Debug.Log("SHADOW_ROUTE_TOPOLOGY room=" + observation.room + " unsafe=" + observation.longestUnshadedRun +
                " from=" + observation.longestUnsafeStart.ToString("F3") + " to=" + observation.longestUnsafeEnd.ToString("F3") +
                " startComponent=" + observation.startSafeComponent + " goalComponent=" + observation.goalSafeComponent +
                " closestGap=" + observation.safeComponentGap + " closestStart=" + observation.closestStartSafePoint.ToString("F3") +
                " closestGoal=" + observation.closestGoalSafePoint.ToString("F3"));
        }

        int[] SafeComponents(Navigation nav, out int count)
        {
            var components = new int[nav.points.Length];
            for (int i = 0; i < components.Length; i++) components[i] = -1;
            var queue = new Queue<int>(); count = 0;
            for (int seed = 0; seed < nav.points.Length; seed++)
            {
                if (!nav.clear[seed] || !nav.safe[seed] || components[seed] >= 0) continue;
                components[seed] = count; queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(), x = current % Width, z = current / Width;
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || xx >= Width || zz < 0 || zz >= nav.rows) continue;
                        int id = zz * Width + xx;
                        if (!nav.clear[id] || !nav.safe[id] || components[id] >= 0 || Mathf.Abs(nav.points[id].y - nav.points[current].y) > .22f) continue;
                        if (dx != 0 && dz != 0 && (!nav.clear[z * Width + xx] || !nav.safe[z * Width + xx] || !nav.clear[zz * Width + x] || !nav.safe[zz * Width + x])) continue;
                        components[id] = count; queue.Enqueue(id);
                    }
                }
                count++;
            }
            return components;
        }

        string BlockingNames(Vector3 feet)
        {
            var names = new List<string>();
            Vector3 center = feet + game.controller.center;
            float stem = game.controller.height * .5f - game.controller.radius;
            foreach (var collider in Physics.OverlapCapsule(center - Vector3.up * stem, center + Vector3.up * stem,
                game.controller.radius - .012f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == game.controller || !PlacementSupport.IsUsableGround(collider)) continue;
                string label = collider.name;
                for (Transform parent = collider.transform.parent; parent != null && parent != game.transform; parent = parent.parent)
                    label = parent.name + "/" + label;
                if (!names.Contains(label)) names.Add(label);
            }
            return string.Join(" | ", names);
        }

        bool Ground(Vector2 point, out Vector3 feet)
        {
            float top = 0;
            foreach (var offset in new[]{Vector3.zero,Vector3.right*.23f,Vector3.left*.23f,Vector3.forward*.23f,Vector3.back*.23f})
                foreach (var hit in Physics.RaycastAll(new Vector3(point.x, 1, point.y) + offset, Vector3.down, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == game.controller || hit.normal.y < .5f || !PlacementSupport.IsUsableGround(hit.collider)) continue;
                    if (hit.point.y > .235f) { feet = default; return false; }
                    top = Mathf.Max(top, hit.point.y);
                }
            feet = new Vector3(point.x, top + .025f, point.y);
            return game.CanStand(feet);
        }

        Navigation Plan(Vector2 start, Vector2 goal)
        {
            var nav = new Navigation { minZ = Mathf.Floor((start.y - .5f) / Grid) * Grid };
            nav.rows = Mathf.CeilToInt((goal.y - nav.minZ) / Grid) + 2;
            int count = nav.rows * Width;
            nav.points = new Vector3[count]; nav.clear = new bool[count]; nav.safe = new bool[count];
            for (int z = 0; z < nav.rows; z++) for (int x = 0; x < Width; x++)
            {
                Guard(); int id = z * Width + x;
                nav.clear[id] = Ground(new Vector2(MinX + x * Grid, nav.minZ + z * Grid), out nav.points[id]);
                nav.safe[id] = nav.clear[id] && game.FootSafe(nav.points[id]);
            }
            int from = Nearest(nav, start), to = Nearest(nav, goal);
            if (from < 0 || to < 0) return nav;
            var costs = new float[count]; var previous = new int[count];
            for (int i = 0; i < count; i++) { costs[i] = float.PositiveInfinity; previous[i] = -1; }
            var open = new SortedSet<OpenNode>(); costs[from] = 0; open.Add(new OpenNode(from, 0));
            while (open.Count > 0)
            {
                OpenNode current = open.Min; open.Remove(current);
                if (current.cost > costs[current.id] + .0001f) continue;
                if (current.id == to) break;
                int x = current.id % Width, z = current.id / Width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || xx >= Width || zz < 0 || zz >= nav.rows) continue;
                    int id = zz * Width + xx;
                    if (!nav.clear[id] || Mathf.Abs(nav.points[id].y - nav.points[current.id].y) > .22f) continue;
                    if (dx != 0 && dz != 0 && (!nav.clear[z * Width + xx] || !nav.clear[zz * Width + x])) continue;
                    float distance = Grid * (dx != 0 && dz != 0 ? 1.41421356f : 1);
                    float next = current.cost + distance * (nav.safe[id] && nav.safe[current.id] ? 1 : 32);
                    if (next >= costs[id]) continue;
                    costs[id] = next; previous[id] = current.id; open.Add(new OpenNode(id, next));
                }
            }
            if (float.IsInfinity(costs[to])) return nav;
            var ids = new List<int>();
            for (int id = to; id >= 0; id = previous[id]) { ids.Add(id); if (id == from) break; }
            ids.Reverse(); nav.path = new List<Vector3>(); float unsafeRun = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                nav.path.Add(nav.points[ids[i]]);
                if (i == 0) continue;
                float length = Vector3.Distance(nav.points[ids[i - 1]], nav.points[ids[i]]); nav.length += length;
                unsafeRun = nav.safe[ids[i - 1]] && nav.safe[ids[i]] ? 0 : unsafeRun + length;
                nav.longestUnsafe = Mathf.Max(nav.longestUnsafe, unsafeRun);
            }
            // End at the actual requested room threshold rather than a nearby
            // grid point; final steering still uses capsule and foot queries.
            if (Ground(goal, out var end) && Vector2.Distance(Flat(nav.path[nav.path.Count - 1]), goal) > .001f) nav.path.Add(end);
            return nav;
        }

        static int Nearest(Navigation nav, Vector2 point)
        {
            int best = -1; float distance = .65f * .65f;
            for (int i = 0; i < nav.points.Length; i++) if (nav.clear[i])
            { float candidate = (Flat(nav.points[i]) - point).sqrMagnitude; if (candidate < distance) { distance = candidate; best = i; } }
            return best;
        }
        static Vector2 Flat(Vector3 point) { return new Vector2(point.x, point.z); }
        static int NearestSegment(List<Vector3> path, int current, Vector2 feet)
        {
            int nearest = current; float best = float.PositiveInfinity;
            for (int i = current; i <= Mathf.Min(path.Count - 2, current + 8); i++)
            {
                Vector2 a = Flat(path[i]), d = Flat(path[i + 1]) - a;
                float t = d.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(feet - a, d) / d.sqrMagnitude);
                float distance = (feet - (a + d * t)).sqrMagnitude;
                if (distance <= best + .000001f) { best = distance; nearest = i; }
            }
            return nearest;
        }
        static Vector2 Pursuit(List<Vector3> path, int segment, Vector2 feet, float lookAhead)
        {
            Vector2 a = Flat(path[segment]), b = Flat(path[segment + 1]), d = b - a;
            float t = d.sqrMagnitude < .000001f ? 1 : Mathf.Clamp01(Vector2.Dot(feet - a, d) / d.sqrMagnitude);
            Vector2 cursor = Vector2.Lerp(a, b, t);
            while (true)
            {
                float remaining = Vector2.Distance(cursor, b);
                if (remaining >= lookAhead && remaining > .00001f) return Vector2.Lerp(cursor, b, lookAhead / remaining);
                lookAhead -= remaining;
                if (segment + 2 >= path.Count) return b;
                cursor = b; segment++; b = Flat(path[segment + 1]);
            }
        }
        bool ClearSteering(Vector2 from, Vector2 to, bool requireShade)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .10f)); float previousHeight = game.player.feet.y;
            for (int i = 1; i <= samples; i++)
            {
                if (!Ground(Vector2.Lerp(from, to, i / (float)samples), out var feet) || Mathf.Abs(feet.y - previousHeight) > .22f) return false;
                if (requireShade && !game.FootSafe(feet)) return false;
                previousHeight = feet.y;
            }
            return true;
        }

        void Grab(GalleryProp prop)
        {
            foreach (var part in prop.parts)
            {
                if (part == null || !part.enabled) continue;
                Look(part.bounds.center); game.UpdateAim();
                if (game.aimed == prop) { game.Interact(); if (game.held == prop) break; }
            }
            Require(game.held == prop, "Actual camera ray could not grab original tool " + prop.title);
        }
        void Look(Vector3 point)
        {
            Vector3 direction = point - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude); game.SyncView();
        }
        void Step(Vector2 movement)
        {
            Guard(); game.Tick(Dt, movement, false); Physics.Simulate(Dt); Physics.SyncTransforms();
            game.nikeLight.SyncPose(); game.lighting.Synchronize(); ticks++;
        }
        void Guard()
        {
            Require(Time.realtimeSinceStartupAsDouble <= deadline, "Exceeded 90-second wall-clock budget");
            Require(ticks < MaximumTicks, "Exceeded 16000 simulated ticks");
        }
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("SHADOW_GAMEPLAY: " + message); }
        void Check(string message) { checks.Add(message); Debug.Log("SHADOW_GAMEPLAY_PASS " + message); }
        void Log(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
        void RestoreSimulation() { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Unsubscribe() { if (subscribed) { Application.logMessageReceived -= Log; subscribed = false; } }
        void OnDestroy() { RestoreSimulation(); Unsubscribe(); }
        void CaptureView(string name, Vector3 position, Vector3 target)
        {
            var camera = game.view; Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            try { Capture(name); } finally { camera.transform.SetPositionAndRotation(oldPosition, oldRotation); }
        }
        void Capture(string name)
        {
            var camera = game.view; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; game.nikeLight.SyncPose(); game.lighting.Synchronize(); camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); if (texture != null) Destroy(texture);
            }
        }
    }
}
