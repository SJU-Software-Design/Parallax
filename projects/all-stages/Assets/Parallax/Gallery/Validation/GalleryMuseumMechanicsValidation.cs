using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Library;

namespace ShadeLink.Gallery
{
    // Explicit solution fixtures plus actual motor/interaction/scene handoff.
    // This does not claim an autonomous solution of the four museum puzzles.
    public sealed class GalleryMuseumMechanicsValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90, Grid = .25f, MinimumX = -5.75f;
        const int MaximumTicks = 10000, GridWidth = 47;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly List<RouteReport> routes = new List<RouteReport>();
        readonly Report report = new Report();
        string output;
        double deadline;
        SimulationMode previousSimulation;
        bool ownsSimulation, subscribed;
        int ticks, libraryLoads;
        string requestedPhase="all";

        [Serializable] sealed class RouteReport
        {
            public string room;
            public int candidate, gridNodes, routeNodes, walkTicks;
            public float length, longestUnshadedRun, totalUnshadedLength, maximumExposure, minimumWalkFeetY, maximumWalkFeetY;
            public Vector3[] plannedPath;
            public Vector3[] fixturePositions;
            public float[] fixtureScales;
        }
        [Serializable] sealed class Report
        {
            public bool passed, exposureRespawn, firstCourseWalk, nikeCourseWalk;
            public bool wrongViewRejected, partialPortraitRejected, heldPortraitRejected, inputOnlyPortrait, portraitOpened, curtainWalk;
            public bool smallExitRejected, heldExitRejected, unsettledExitRejected, reverseExitRejected, sideExitRejected;
            public bool inputOnlyOrcusEnlarged, inputOnlyOrcusReleased, inputOnlyOrcusSettled, physicalMouthCrossing;
            public bool libraryLoaded, libraryActive, simulationRestored;
            public string phase, failure, unity, gpu, orcusLastRestriction;
            public string scope = "Explicit enlarged bronze and repositioned Nike solution fixtures are searched and checked with real placement geometry, capsule queries and the game's FootSafe predicate, then traversed with the actual CharacterController and exposure rules. The separate GalleryShadowGameplayValidation covers camera pickup/carry/perspective resize/Drop from the original bronze and Nike without positive-case prop pose writes. Portrait negatives use labelled mathematical pose fixtures. The positive portrait sequence restores all three original pieces and uses camera pickup, CharacterController carry, shared perspective resize, Drop and PhysX only, followed by actual one-second observation. Cathedral pickup, carry, resize and Drop use public input to reach the helpful near-size threshold; the game then performs its disclosed safe size assistance. Player-only pickup and ground-approach entry fixtures are disclosed; the final controller walk begins on clear floor with a disclosed off-centre offset, climbs the actual sculpture staircase and crosses the aperture before the real Library scene transition. This is focused integration QA, not an autonomous full puzzle playthrough.";
            public int simulatedTicks, exposureRespawns, totalRespawns, finalCheckpoint, enlargementUpdates, mouthWalkFrames, librarySceneLoads;
            public float simulatedSeconds, maximumNegativeExposure, orcusRequiredScale, orcusMaximumScale, orcusReleasedScale, fallDistance, entryLateralOffset, entryLateralOffsetMetres;
            public Vector3 orcusReleasedPosition;
            public RouteReport[] routes;
            public string[] checks, errors;
        }
        struct Pose
        {
            public Vector3 position;
            public float scale;
            public Pose(float x, float z, float size) { position = new Vector3(x, .006f, z); scale = size; }
        }
        sealed class Navigation
        {
            public Vector3[] points;
            public bool[] clear, safe;
            public int rows;
            public float minZ;
            public List<Vector3> path;
            public float length, longestUnsafe, totalUnsafe;
        }
        struct OpenNode : IComparable<OpenNode>
        {
            public int id;
            public float cost;
            public OpenNode(int index, float value) { id = index; cost = value; }
            public int CompareTo(OpenNode other)
            { int result = cost.CompareTo(other.cost); return result != 0 ? result : id.CompareTo(other.id); }
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += SceneLoaded;
            subscribed = true;
            previousSimulation = Physics.simulationMode;
            deadline = Time.realtimeSinceStartupAsDouble + 120;
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
            RestoreSimulation();
            report.simulationRestored = Physics.simulationMode == previousSimulation;
            report.simulatedTicks = ticks; report.simulatedSeconds = ticks * Dt;
            report.librarySceneLoads = libraryLoads;
            if (game != null) { report.totalRespawns = game.respawns; report.finalCheckpoint = game.checkpoint; }
            report.routes = routes.ToArray(); report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.exposureRespawn && report.firstCourseWalk &&
                report.nikeCourseWalk && report.inputOnlyPortrait && report.portraitOpened && report.curtainWalk && report.inputOnlyOrcusSettled &&
                report.physicalMouthCrossing && report.libraryActive && report.simulationRestored;
            if(requestedPhase=="portrait")report.passed=report.failure==null&&errors.Count==0&&report.inputOnlyPortrait&&report.portraitOpened&&report.curtainWalk&&report.simulationRestored;
            if(requestedPhase=="exit")report.passed=report.failure==null&&errors.Count==0&&report.inputOnlyOrcusSettled&&report.physicalMouthCrossing&&report.libraryActive&&report.simulationRestored;
            try
            {
                if (output != null) File.WriteAllText(Path.Combine(output, "museum-mechanics-results.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception) { report.passed = false; report.failure = "Cannot write report: " + exception; }
            if (!report.passed)
            {
                var camera = game != null ? game.view : FindFirstObjectByType<LibraryGame>()?.view;
                if (camera != null && output != null)
                    try { Capture(camera, "mechanics-failure"); } catch (Exception) { }
            }
            Unsubscribe();
            if (report.passed) Debug.Log("MUSEUM_MECHANICS_OK phase="+requestedPhase+" " + checks.Count + " checks");
            else Debug.LogError("MUSEUM_MECHANICS_FAIL phase=" + report.phase + " " + report.failure + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "GalleryMuseumMechanicsValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.conceptLayout, "Actual automated concept museum is required");
            Require(Application.CanStreamedLevelBeLoaded("Library"), "Build does not contain the actual Library scene");
            var course = game.GetComponentInChildren<GalleryMuseumShadowCourse>(true);
            var portrait = game.GetComponentInChildren<GalleryConceptPerspective>(true);
            var gate = game.GetComponentInChildren<GalleryPortraitGate>(true);
            Require(course != null && course.bronze != null && course.statue != null && game.nikeLight != null,
                "Actual bronze and movable Nike shadow-course tools are required");
            Require(portrait != null && gate != null && game.orcusExit != null, "Portrait gate or sculpture mouth is missing");
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            yield return null;
            game.exhibitionPreview = false;
            game.ResetAll(); game.Resume();
            for (int i = 0; i < 90; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }

            int phaseAt=Array.IndexOf(args,"-museum-test-phase");
            if(phaseAt>=0&&phaseAt+1<args.Length)requestedPhase=args[phaseAt+1];
            if(requestedPhase=="portrait"||requestedPhase=="exit")
            {
                report.scope="Isolated "+requestedPhase+" phase. "+report.scope;
                game.checkpoint=requestedPhase=="portrait"?2:3;
                game.PlacePlayer(requestedPhase=="portrait"?portrait.viewingPoint.position:new Vector3(0,0,49.1f));
                var isolated=requestedPhase=="portrait"?PerspectiveFixtures(portrait,gate):OrcusFixtures(game.orcusExit);
                while(isolated.MoveNext())yield return isolated.Current;
                yield break;
            }

            report.phase = "negative exposure: ordinary straight walking without arranged shade";
            int before = game.respawns;
            game.yaw = game.pitch = 0; game.SyncView();
            for (int i = 0; i < 480 && game.respawns == before; i++)
            {
                Step(game.player.feet.z < 9.5f ? Vector2.up : Vector2.zero);
                report.maximumNegativeExposure = Mathf.Max(report.maximumNegativeExposure, game.exposure);
                if (i % 12 == 0) yield return null;
            }
            report.exposureRespawns = game.respawns - before;
            Require(report.maximumNegativeExposure > .5f && report.exposureRespawns == 1,
                "Unprotected straight walking failed to accumulate sunlight exposure and respawn exactly once");
            report.exposureRespawn = true;
            Check("Negative: actual unprotected CharacterController walk accumulates sunlight exposure and returns to the checkpoint");

            game.ResetAll(); game.Resume();
            for (int i = 0; i < 30; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(Array.FindAll(game.props, prop => prop != null && prop.zone == 0).Length == 1,
                "First room must contain one movable bronze instead of the retired painting tools");
            report.phase = "room 1 explicit enlarged-bronze fixtures and physical shadow route";
            Pose[][] bronzeCandidates = {
                new[]{new Pose(0,7.15f,2.1f)}, new[]{new Pose(0,7.2f,2.2f)},
                new[]{new Pose(0,7.1f,2.3f)}, new[]{new Pose(-.4f,7.2f,2.2f)},
                new[]{new Pose(.4f,7.2f,2.2f)}, new[]{new Pose(0,7.3f,2.4f)} };
            var first = ChooseFixtureRoute(new[]{course.bronze}, bronzeCandidates, new Vector2(0, 14.8f), "bronze room");
            CaptureView("mechanics-01-bronze-fixture", new Vector3(0, 1.8f, 4.8f), new Vector3(0, 2.3f, 9));
            var walking = WalkRoute(first.path, first.report);
            while (walking.MoveNext()) yield return walking.Current;
            Require(game.checkpoint >= 1 && course.TutorialCleared, "First actual shadow crossing did not advance its course/checkpoint");
            report.firstCourseWalk = true;
            Check("Explicit bronze fixture passes real placement checks; a physical camera-controller walk crosses its shade without respawn and reaches checkpoint 1");

            report.phase = "room 2 explicit Nike fixture, fixed spotlight and physical shadow route";
            var cover=game.nikeLight.cover;
            Require(cover!=null,"Nike light-cover bust is missing");
            PoseFixture(cover.Prop,GalleryNikeLightCover.RevealedPosition,Quaternion.identity,1);
            cover.Observe(game.nikeLight.emitter,GalleryNikeCanvas.Center);game.nikeLight.SyncPose();
            report.scope+=" The light-cover bust is explicitly moved aside as part of the disclosed Nike solution fixture. Actual cover pickup/slide/Drop is covered separately by Nike discovery validation.";
            Pose[][] nikeCandidates = {
                new[]{new Pose(GalleryNikeCanvas.SolutionPosition.x,GalleryNikeCanvas.SolutionPosition.z,GalleryNikeCanvas.SolutionScale)} };
            var second = ChooseFixtureRoute(new[]{course.statue}, nikeCandidates, new Vector2(0, 29.5f), "Nike spotlight room", true);
            CaptureView("mechanics-02-nike-spotlight-fixture", new Vector3(0, 1.8f, 19.8f), new Vector3(0, 1.4f, 26));
            walking = WalkRoute(second.path, second.report);
            while (walking.MoveNext()) yield return walking.Current;
            Require(game.checkpoint >= 2 && course.NikeCleared, "Second actual shadow crossing did not advance its course/checkpoint");
            report.nikeCourseWalk = true;
            Check("The settled Nike fixture matches its framed shadow; the seated painting opens the walnut doors and the controller reaches checkpoint 2 without respawn");

            var perspective = PerspectiveFixtures(portrait, gate);
            while (perspective.MoveNext()) yield return perspective.Current;
            var mouth = OrcusFixtures(game.orcusExit);
            while (mouth.MoveNext()) yield return mouth.Current;
        }

        void PoseFixture(GalleryProp prop, Vector3 position, Quaternion rotation, float scale)
        {
            prop.Restore();
            prop.transform.SetPositionAndRotation(position, rotation); prop.transform.localScale = Vector3.one * scale;
            prop.body.position = position; prop.body.rotation = rotation;
            Physics.SyncTransforms(); prop.placement.ResetPlaced();
        }

        (List<Vector3> path, RouteReport report) ChooseFixtureRoute(GalleryProp[] props, Pose[][] candidates, Vector2 goal, string room, bool requireSpotlightSolution = false)
        {
            string last = "no candidates";
            int selected = -1;
            Navigation best = null;
            for (int candidate = 0; candidate < candidates.Length; candidate++)
            {
                Guard();
                for (int i = 0; i < props.Length; i++) PoseFixture(props[i], candidates[candidate][i].position, Quaternion.identity, candidates[candidate][i].scale);
                bool valid = true;
                foreach (var prop in props)
                {
                    if (!prop.placement.Ready) { last = prop.title + ": " + prop.placement.Status; valid = false; break; }
                    if (!game.ValidPlacement(prop, out var reason)) { last = prop.title + ": " + reason; valid = false; break; }
                }
                if (!valid)
                { Debug.Log("MUSEUM_MECHANICS_CANDIDATE_REJECT " + room + " candidate=" + candidate + " placement=" + last); continue; }
                if (requireSpotlightSolution)
                {
                    game.nikeLight.ResetLight();
                    var canvas = game.nikeLight.canvas;
                    int animationTicks = Mathf.CeilToInt((canvas.BrushSeconds + 4f) / Dt);
                    for (int i = 0; i < animationTicks && !canvas.Complete; i++) Step(Vector2.zero);
                    if (!canvas.Complete)
                    {
                        last = "Settled Nike did not fuse into the wall frame: " + game.nikeLight.Status;
                        Debug.Log("MUSEUM_MECHANICS_CANDIDATE_REJECT " + room + " candidate=" + candidate + " " + last);
                        continue;
                    }
                    var socket = game.shadowCourse.nikeSocket;
                    PoseFixture(canvas.portablePainting, socket.Center - Vector3.forward * .30f, Quaternion.identity, GalleryNikeDoorSocket.TargetScale);
                    Require(socket.FitHint(canvas.portablePainting) == null, "Painting key fixture does not fit the door");
                    socket.BeginDock(canvas.portablePainting);
                    for (int i = 0; i < 140; i++) Step(Vector2.zero);
                    if (!game.shadowCourse.NikeGateOpen)
                    {
                        last = "Seated painting did not open the doorway";
                        Debug.Log("MUSEUM_MECHANICS_CANDIDATE_REJECT " + room + " candidate=" + candidate + " " + last);
                        continue;
                    }
                }
                game.lighting.Synchronize();
                Navigation nav = Plan(new Vector2(game.player.feet.x, game.player.feet.z), goal);
                if (nav.path == null)
                {
                    last = "No capsule-clear route in actual geometry";
                    Debug.Log("MUSEUM_MECHANICS_CANDIDATE_REJECT " + room + " candidate=" + candidate + " " + last); continue;
                }
                Debug.Log("MUSEUM_MECHANICS_CANDIDATE_ROUTE " + room + " candidate=" + candidate + " nodes=" + nav.path.Count +
                    " length=" + nav.length + " longestUnshaded=" + nav.longestUnsafe + " totalUnshaded=" + nav.totalUnsafe);
                if (nav.longestUnsafe > 1.05f)
                {
                    last = "Shadow route has an unsafe run of " + nav.longestUnsafe + "m";
                    Debug.Log("MUSEUM_MECHANICS_CANDIDATE_REJECT " + room + " candidate=" + candidate + " " + last); continue;
                }
                // Evaluate every candidate before choosing: fully shaded paths
                // beat shorter paths that depend on exposure grace.
                if (best == null || nav.longestUnsafe < best.longestUnsafe - .001f ||
                    (Mathf.Abs(nav.longestUnsafe - best.longestUnsafe) < .001f && (nav.totalUnsafe < best.totalUnsafe - .001f ||
                    (Mathf.Abs(nav.totalUnsafe - best.totalUnsafe) < .001f && nav.length < best.length))))
                { selected = candidate; best = nav; }
            }
            Require(best != null, "No valid " + room + " fixture/path: " + last);
            for (int i = 0; i < props.Length; i++) PoseFixture(props[i], candidates[selected][i].position, Quaternion.identity, candidates[selected][i].scale);
            game.lighting.Synchronize();
            var observation = new RouteReport { room = room, candidate = selected, gridNodes = best.points.Length,
                routeNodes = best.path.Count, length = best.length, longestUnshadedRun = best.longestUnsafe, totalUnshadedLength = best.totalUnsafe,
                plannedPath = best.path.ToArray(), minimumWalkFeetY = game.player.feet.y, maximumWalkFeetY = game.player.feet.y,
                fixturePositions = new Vector3[props.Length], fixtureScales = new float[props.Length] };
            for (int i = 0; i < props.Length; i++) { observation.fixturePositions[i] = props[i].transform.position; observation.fixtureScales[i] = props[i].transform.localScale.x; }
            routes.Add(observation);
            Debug.Log("MUSEUM_MECHANICS_FIXTURE " + room + " candidate=" + selected + " route=" + best.path.Count + " longestUnshaded=" + best.longestUnsafe);
            return (best.path, observation);
        }

        bool Ground(Vector2 point, out Vector3 feet)
        {
            float top = 0;
            Vector3[] offsets = { Vector3.zero, Vector3.right * .23f, Vector3.left * .23f, Vector3.forward * .23f, Vector3.back * .23f };
            foreach (var offset in offsets)
            {
                foreach (var hit in Physics.RaycastAll(new Vector3(point.x, 1, point.y) + offset, Vector3.down, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == game.controller || hit.normal.y < .5f || !PlacementSupport.IsUsableGround(hit.collider)) continue;
                    if (hit.point.y > .235f) { feet = default; return false; }
                    top = Mathf.Max(top, hit.point.y);
                }
            }
            feet = new Vector3(point.x, top + .025f, point.y);
            return game.CanStand(feet);
        }

        Navigation Plan(Vector2 start, Vector2 goal)
        {
            var nav = new Navigation { minZ = Mathf.Floor(start.y / Grid) * Grid };
            nav.rows = Mathf.CeilToInt((goal.y - nav.minZ) / Grid) + 2;
            int count = nav.rows * GridWidth;
            nav.points = new Vector3[count]; nav.clear = new bool[count]; nav.safe = new bool[count];
            for (int z = 0; z < nav.rows; z++)
            {
                Guard();
                for (int x = 0; x < GridWidth; x++)
                {
                    int id = z * GridWidth + x;
                    nav.clear[id] = Ground(new Vector2(MinimumX + x * Grid, nav.minZ + z * Grid), out nav.points[id]);
                    nav.safe[id] = nav.clear[id] && game.FootSafe(nav.points[id]);
                }
            }
            int from = Nearest(nav, start), to = Nearest(nav, goal);
            if (from < 0 || to < 0) return nav;
            var cost = new float[count]; var previous = new int[count];
            for (int i = 0; i < count; i++) { cost[i] = float.PositiveInfinity; previous[i] = -1; }
            var open = new SortedSet<OpenNode>(); cost[from] = 0; open.Add(new OpenNode(from, 0));
            while (open.Count > 0)
            {
                OpenNode current = open.Min; open.Remove(current);
                if (current.cost > cost[current.id] + .0001f) continue;
                if (current.id == to) break;
                int x = current.id % GridWidth, z = current.id / GridWidth;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || xx >= GridWidth || zz < 0 || zz >= nav.rows) continue;
                    int id = zz * GridWidth + xx;
                    if (!nav.clear[id] || Mathf.Abs(nav.points[id].y - nav.points[current.id].y) > .22f) continue;
                    if (dx != 0 && dz != 0 && (!nav.clear[z * GridWidth + xx] || !nav.clear[zz * GridWidth + x])) continue;
                    float distance = Grid * (dx != 0 && dz != 0 ? 1.41421356f : 1);
                    float next = current.cost + distance * (nav.safe[id] && nav.safe[current.id] ? 1 : 32);
                    if (next >= cost[id]) continue;
                    cost[id] = next; previous[id] = current.id; open.Add(new OpenNode(id, next));
                }
            }
            if (float.IsInfinity(cost[to])) return nav;
            var ids = new List<int>();
            for (int id = to; id >= 0; id = previous[id]) { ids.Add(id); if (id == from) break; }
            ids.Reverse(); nav.path = new List<Vector3>(ids.Count);
            float unsafeRun = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                nav.path.Add(nav.points[ids[i]]);
                if (i == 0) continue;
                float length = Vector3.Distance(nav.points[ids[i - 1]], nav.points[ids[i]]);
                nav.length += length;
                unsafeRun = nav.safe[ids[i - 1]] && nav.safe[ids[i]] ? 0 : unsafeRun + length;
                if (!nav.safe[ids[i - 1]] || !nav.safe[ids[i]]) nav.totalUnsafe += length;
                nav.longestUnsafe = Mathf.Max(nav.longestUnsafe, unsafeRun);
            }
            return nav;
        }

        static int Nearest(Navigation nav, Vector2 point)
        {
            int closest = -1; float best = .65f * .65f;
            for (int i = 0; i < nav.points.Length; i++) if (nav.clear[i])
            {
                float distance = (new Vector2(nav.points[i].x, nav.points[i].z) - point).sqrMagnitude;
                if (distance < best) { best = distance; closest = i; }
            }
            return closest;
        }
        static Vector2 Flat(Vector3 point) { return new Vector2(point.x, point.z); }

        static int NearestForwardSegment(List<Vector3> path, int segment, Vector2 feet)
        {
            int nearest = segment;
            float best = float.PositiveInfinity;
            int last = Mathf.Min(path.Count - 2, segment + 8);
            for (int i = segment; i <= last; i++)
            {
                Vector2 a = Flat(path[i]), direction = Flat(path[i + 1]) - a;
                float t = direction.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(feet - a, direction) / direction.sqrMagnitude);
                float distance = (feet - (a + direction * t)).sqrMagnitude;
                // Equal-distance endpoints belong to the later segment. Never
                // move backwards when pursuit has already rounded a corner.
                if (distance <= best + .000001f) { best = distance; nearest = i; }
            }
            return nearest;
        }

        static Vector2 PursuitTarget(List<Vector3> path, int segment, Vector2 feet, float lookAhead)
        {
            Vector2 a = Flat(path[segment]), b = Flat(path[segment + 1]), direction = b - a;
            float t = direction.sqrMagnitude < .000001f ? 1 : Mathf.Clamp01(Vector2.Dot(feet - a, direction) / direction.sqrMagnitude);
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
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .10f));
            float previousHeight = game.player.feet.y;
            for (int i = 1; i <= samples; i++)
            {
                if (!Ground(Vector2.Lerp(from, to, (float)i / samples), out var feet) || Mathf.Abs(feet.y - previousHeight) > .22f) return false;
                if (requireShade && !game.FootSafe(feet)) return false;
                previousHeight = feet.y;
            }
            return true;
        }

        IEnumerator WalkRoute(List<Vector3> path, RouteReport observation = null)
        {
            int startRespawns = game.respawns, startTicks = ticks;
            Require(path != null && path.Count > 0, "Walking route is empty");
            if (path.Count == 1) yield break;
            int segment = 0;
            Vector2 goal = Flat(path[path.Count - 1]);
            Vector2 progressAnchor = Flat(game.player.feet);
            int progressTick = ticks;
            bool requireShade = observation != null && observation.longestUnshadedRun < .001f;
            while (Vector2.Distance(Flat(game.player.feet), goal) > .085f)
            {
                Vector2 feet = Flat(game.player.feet);
                segment = NearestForwardSegment(path, segment, feet);
                Vector2 target = Flat(path[segment + 1]);
                // Continuous running with an 85cm lookahead, reduced only when
                // a shortcut would cut through actual collider geometry. Grid
                // nodes describe the route; they are not individual stop points.
                for (float ahead = .85f; ahead >= .10f; ahead *= .5f)
                {
                    Vector2 candidate = PursuitTarget(path, segment, feet, ahead);
                    if (ClearSteering(feet, candidate, requireShade)) { target = candidate; break; }
                }
                Vector2 delta = target - feet;
                float finalDistance = Vector2.Distance(feet, goal);
                float approach = Mathf.Sqrt(Mathf.Clamp01(finalDistance / .8f));
                if (!game.FootSafe(game.player.feet)) approach = 1;
                Vector3 previousFeet = game.player.feet;
                float previousExposure = game.exposure;
                Step(delta.normalized * approach);
                Require(game.respawns == startRespawns, "Physical route respawned from " + previousFeet + " exposure=" + previousExposure + " toward " + target + " segment=" + segment);
                if (Vector2.Distance(Flat(game.player.feet), progressAnchor) > .08f)
                { progressAnchor = Flat(game.player.feet); progressTick = ticks; }
                Require(ticks - progressTick < 90, "Walking follower made no progress for 90 ticks: feet=" + game.player.feet.ToString("F4") +
                    " target=" + target.ToString("F4") + " segment=" + segment + " goal=" + goal.ToString("F3") +
                    " capsuleClear=" + game.CanStand(game.player.feet) + " safe=" + game.safe + " exposure=" + game.exposure);
                if (observation != null)
                {
                    observation.maximumExposure = Mathf.Max(observation.maximumExposure, game.exposure);
                    observation.minimumWalkFeetY = Mathf.Min(observation.minimumWalkFeetY, game.player.feet.y);
                    observation.maximumWalkFeetY = Mathf.Max(observation.maximumWalkFeetY, game.player.feet.y);
                }
                if (ticks % 90 == 0) Debug.Log("MUSEUM_MECHANICS_WALK feet=" + game.player.feet.ToString("F3") + " target=" + target.ToString("F3") +
                    " segment=" + segment + " exposure=" + game.exposure + " safe=" + game.safe + " capsuleClear=" + game.CanStand(game.player.feet));
                if (ticks % 12 == 0) yield return null;
            }
            for (int i = 0; i < 24; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(game.respawns == startRespawns, "Route braking respawned");
            if (observation != null) observation.walkTicks = ticks - startTicks;
        }

        void PortraitSolution(GalleryConceptPerspective portrait)
        {
            for (int i = 0; i < portrait.pieces.Length; i++)
            {
                var prop = portrait.pieces[i];
                PoseFixture(prop, portrait.transform.TransformPoint(portrait.solutionPositions[i]), portrait.transform.rotation, portrait.solutionScales[i]);
                bool valid = game.ValidPlacement(prop, out var reason);
                Require(prop.placement.Ready && valid, "Published portrait fixture is unsupported/invalid: " + i + " " + reason);
            }
            game.lighting.Synchronize();
        }

        IEnumerator PerspectiveFixtures(GalleryConceptPerspective portrait, GalleryPortraitGate gate)
        {
            report.phase = "portrait explicit poses: wrong view, incomplete, held and timed positive";
            Require(portrait.pieces.Length == 3, "Portrait requires three actual pieces");
            gate.ResetGate(); PortraitSolution(portrait);
            game.PlacePlayer(portrait.viewingPoint.position);
            game.yaw = Mathf.PI; game.pitch = 0; game.SyncView();
            for (int i = 0; i < 195; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(!gate.Opened && gate.Progress == 0, "Correct shapes opened the curtain while the camera looked away");
            report.wrongViewRejected = true;

            gate.ResetGate(); PortraitSolution(portrait);
            var moved = portrait.pieces[2];
            PoseFixture(moved, portrait.transform.TransformPoint(portrait.solutionPositions[2]) + Vector3.right * 1.5f, portrait.transform.rotation, portrait.solutionScales[2]);
            Look(portrait.viewingPoint.position + Vector3.forward * 12 + Vector3.up * 1.55f);
            for (int i = 0; i < 195; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(!gate.Opened && portrait.AlignedPieces < 3, "An incomplete portrait opened the curtain");
            report.partialPortraitRejected = true;
            gate.ResetGate(); PortraitSolution(portrait); Grab(portrait.pieces[2]);
            for (int i = 0; i < 195; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(game.held == portrait.pieces[2] && !gate.Opened && portrait.AlignedPieces < 3, "A held portrait piece counted toward the solution");
            report.heldPortraitRejected = true; game.Cancel(true);

            // Positive case begins from the original three pieces. The utility
            // relocates only the player for its documented pickup viewpoints;
            // every piece pose then comes from the normal interaction resolver.
            foreach (var piece in portrait.pieces) piece.Restore();
            gate.ResetGate(); game.checkpoint = 2;
            game.PlacePlayer(portrait.viewingPoint.position);
            game.yaw = 0; game.pitch = 0; game.SyncView();
            report.phase = "portrait positive: original pieces solved through actual pickup/carry/Drop input";
            var solver = GalleryPortraitInputSolver.Solve(game, portrait, Step);
            while (solver.MoveNext()) yield return solver.Current;
            portrait.Evaluate();
            Require(portrait.Complete && portrait.AtViewingPoint && game.held == null, "Input-only portrait solver did not leave three placed aligned pieces");
            report.inputOnlyPortrait = true;
            game.yaw = 0; game.pitch = 0; game.SyncView();
            Capture(game.view, gate.Opened ? "mechanics-03-portrait-recognized" : "mechanics-03-portrait-before-opening");
            // Recognition now starts the slow absorption/red-light/smile
            // performance; the physical gate stays shut until it is traversable.
            int portraitPerformanceTicks = Mathf.CeilToInt(18f / Dt);
            for (int i = 0; i < portraitPerformanceTicks && !gate.Opened; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(gate.Opened && gate.barrier != null && !gate.barrier.enabled, "Recognizing the placed face did not open the physical curtain barrier");
            for (int i = 0; i < 130; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            report.portraitOpened = true;
            Capture(game.view, "mechanics-04-portrait-after-opening");
            Check("The recognizable portrait is assembled through actual pickup/carry/Drop; observation starts the slow curtain-face performance, which opens the real barrier only after its smile and reveal; held or clearly separated pieces remain unfinished");
            var path = new List<Vector3> { new Vector3(3.3f, 0, 33), new Vector3(3.3f, 0, 42), new Vector3(0, 0, 42.8f), new Vector3(0, 0, 45) };
            var walk = WalkRoute(path);
            while (walk.MoveNext()) yield return walk.Current;
            Require(game.checkpoint == 3 && game.player.feet.z > 44.7f, "Actual controller could not cross the opened curtain into checkpoint 3");
            report.curtainWalk = true;
            Check("Actual CharacterController walks beside the aligned sculptures and through the opened curtain into room 4");
        }

        IEnumerator OrcusFixtures(GalleryOrcusExit exit)
        {
            report.phase = "sculpture portal: negative eligibility, input-only enlargement and physical handoff";
            var prop = exit.prop;
            report.orcusRequiredScale = exit.RequiredScale;
            Require(!exit.IsReadyForEntry && !exit.CanEnterFrom(MouthFeet(exit, 0, -.12f), MouthFeet(exit, 0, .12f)), "Small sculpture already accepts entry");
            report.smallExitRejected = true;
            game.PlacePlayer(prop.homePosition + new Vector3(exit.customOpening ? -.85f : 0, 0, -1.25f));
            Require(game.CanStand(game.player.feet), "Close original-mouth pickup fixture is obstructed");
            Grab(prop);
            bool dropped = false;
            for (int i = 0; i < 600 && !dropped; i++)
            {
                Look(new Vector3(0, .045f, 54.3f));
                Step(game.player.feet.z > (exit.customOpening ? 44.2f : 48.3f) ? Vector2.down : Vector2.zero);
                report.enlargementUpdates++;
                Require(game.held == prop && prop.body.isKinematic, "Normal carried walking lost the sculpture");
                float scale = Mathf.Min(prop.transform.lossyScale.x, prop.transform.lossyScale.y, prop.transform.lossyScale.z);
                report.orcusMaximumScale = Mathf.Max(report.orcusMaximumScale, scale);
                report.orcusLastRestriction = game.PlacementDiagnostic.Limiter + ": " + game.invalidReason;
                float releaseScale = exit.cathedralActivation != null ? exit.cathedralActivation.MinimumAssistedScale : exit.RequiredScale;
                if (scale >= releaseScale && game.ValidPlacement(prop, out _))
                {
                    Require(!exit.IsReadyForEntry && !exit.CanEnterFrom(MouthFeet(exit, 0, -.12f), MouthFeet(exit, 0, .12f)), "Held enlarged sculpture accepted entry");
                    report.heldExitRejected = true; report.inputOnlyOrcusEnlarged = true;
                    dropped = game.Drop();
                    if (dropped)
                    {
                        report.inputOnlyOrcusReleased = true;
                        report.orcusReleasedScale = prop.transform.lossyScale.x;
                        report.orcusReleasedPosition = prop.transform.position;
                        Require(!exit.IsReadyForEntry && !exit.CanEnterFrom(MouthFeet(exit, 0, -.12f), MouthFeet(exit, 0, .12f)), "Freshly released, unsettled sculpture accepted entry");
                        report.unsettledExitRejected = true;
                    }
                }
                if (i % 8 == 0) yield return null;
            }
            Require(dropped && report.inputOnlyOrcusEnlarged, "Input-only Orcus enlargement/release failed: max=" + report.orcusMaximumScale + " required=" + exit.RequiredScale + " " + report.orcusLastRestriction);
            for (int i = 0; i < 360 && !exit.IsReadyForEntry; i++) { Step(Vector2.zero); yield return null; }
            Require(exit.IsReadyForEntry, "Input-released sculpture did not settle: " + prop.placement.Status);
            report.inputOnlyOrcusSettled = true;
            Vector3 before = MouthFeet(exit, 0, -.12f), after = MouthFeet(exit, 0, .12f);
            Require(!exit.CanEnterFrom(after, before), "Reverse approach accepted"); report.reverseExitRejected = true;
            Require(!exit.CanEnterFrom(MouthFeet(exit, .95f, -.12f), MouthFeet(exit, .95f, .12f)), "Side approach outside mouth accepted"); report.sideExitRejected = true;
            Require(!exit.CanEnterFrom(before - exit.transform.forward * 3, after), "A discontinuous teleport accepted as a walking entry");
            Require(exit.CanEnterFrom(before, after), "Settled upright mouth rejects a geometrically valid front crossing");
            float lateralOffset = exit.customOpening ? .04f : 0;
            Require(exit.CanEnterFrom(MouthFeet(exit, lateralOffset, -.12f), MouthFeet(exit, lateralOffset, .12f)),
                "The transition rejects an off-centre crossing that must be governed by the physical hand colliders");
            report.entryLateralOffset = lateralOffset;
            report.entryLateralOffsetMetres = exit.transform.TransformVector(Vector3.right * lateralOffset).magnitude;
            Check("Actual camera/carried movement/Drop reaches the helpful near-size threshold, the sculpture completes its size and settles, then permits physical entry");
            CaptureView("mechanics-05-cathedral-ready", game.player.feet + Vector3.up * 1.55f, exit.MouthPosition);

            // Explicit entry fixture: start on clear floor before the first
            // tread, then let the actual controller climb the low sculpture steps.
            // The enlarged prop itself receives no pose/scale writes here.
            Vector3 approach = exit.transform.TransformPoint(new Vector3(exit.EntryCenterX + lateralOffset, 0, exit.approachStart));
            game.PlacePlayer(approach + Vector3.up * .006f);
            Require(game.CanStand(game.player.feet), "Ground approach before the first mouth tread is obstructed");
            game.yaw = Mathf.Atan2(exit.transform.forward.x, exit.transform.forward.z); game.pitch = -.02f; game.SyncView();
            Debug.Log("MUSEUM_MECHANICS_ORCUS_GROUND_APPROACH feet=" + game.player.feet.ToString("F4") +
                " mouth=" + exit.MouthPosition.ToString("F4") + " scale=" + prop.transform.lossyScale.x + " capsuleClear=true");
            Capture(game.view, "mechanics-05b-cathedral-ground-approach");
            exit.ResetEntrance();
            yield return null;
            int beforeRespawns = game.respawns;
            for (int i = 0; i < 600 && !game.Transitioning; i++)
            {
                Vector3 direction = exit.transform.forward;
                Step(new Vector2(direction.x, direction.z)); report.mouthWalkFrames++;
                Require(game.respawns == beforeRespawns, "Physical mouth approach respawned");
                yield return null; // Let the real exit Update sample each CC step.
            }
            Require(game.Transitioning && exit.Entering && report.mouthWalkFrames > 3, "Actual CharacterController did not climb the mouth steps from the ground approach and cross the aperture");
            report.physicalMouthCrossing = true;
            var passage = game.stageTransition;
            Require(passage != null && !game.active && game.won, "Mouth did not begin the actual frozen-input scene passage");
            RestoreSimulation();
            bool capturedFall = false;
            double arrivalDeadline=Time.realtimeSinceStartupAsDouble+15;
            while(Time.realtimeSinceStartupAsDouble<arrivalDeadline)
            {
                Guard();
                if (passage != null)
                {
                    report.fallDistance = Mathf.Max(report.fallDistance, passage.FallDistance);
                    if (!capturedFall && passage.FallDistance > 6 && game != null)
                    { Capture(game.view, "mechanics-06-library-fall"); capturedFall = true; }
                }
                var library = FindFirstObjectByType<LibraryGame>();
                if (library != null && library.enabled && library.active)
                {
                    Require(SceneManager.GetActiveScene().name == "Library" && libraryLoads == 1 && library.started && Time.timeScale == 1,
                        "Actual Library scene did not load/resume exactly once");
                    report.libraryLoaded = report.libraryActive = true;
                    Capture(library.view, "mechanics-07-library-arrival");
                    break;
                }
                yield return null;
            }
            Require(report.libraryActive && report.fallDistance > 10, "Actual Library handoff/descent did not complete");
            Check("Real ground approach with a labelled off-centre offset climbs the sculpture staircase, crosses the aperture and triggers a descent longer than ten metres and one Library scene load with active controls and normal physics");
        }

        Vector3 MouthFeet(GalleryOrcusExit exit, float x, float z)
        { return exit.transform.TransformPoint(new Vector3(exit.EntryCenterX+x, exit.EntryBottom + exit.entryFootOffset, exit.EntryPlane + z)); }
        void Grab(GalleryProp prop)
        {
            foreach (var part in prop.parts)
            {
                if (part == null || !part.enabled) continue;
                Look(part.bounds.center); game.UpdateAim();
                if (game.aimed == prop) { game.Interact(); if (game.held == prop) break; }
            }
            Require(game.held == prop, "Actual camera ray cannot grab " + prop.title);
        }
        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            game.SyncView();
        }
        void Step(Vector2 worldMovement)
        {
            Guard(); game.Tick(Dt, worldMovement, false); Physics.Simulate(Dt); Physics.SyncTransforms();
            game.lighting.Synchronize(); ticks++;
            report.totalRespawns = game.respawns; report.finalCheckpoint = game.checkpoint;
        }
        void Guard()
        {
            Require(Time.realtimeSinceStartupAsDouble <= deadline, "Exceeded 120-second wall-clock budget");
            Require(ticks < MaximumTicks, "Exceeded 10000 simulated ticks");
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { if (scene.name == "Library") libraryLoads++; }
        void RestoreSimulation() { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Unsubscribe()
        {
            if (!subscribed) return;
            Application.logMessageReceived -= Log; SceneManager.sceneLoaded -= SceneLoaded; subscribed = false;
        }
        void OnDestroy() { RestoreSimulation(); Unsubscribe(); }
        void Log(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("MUSEUM_MECHANICS: " + message); }
        void Check(string message) { checks.Add(message); Debug.Log("MUSEUM_MECHANICS_PASS " + message); }
        void CaptureView(string name, Vector3 position, Vector3 target)
        {
            var camera = game.view; Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            try { Capture(camera, name); }
            finally { camera.transform.SetPositionAndRotation(oldPosition, oldRotation); }
        }
        void Capture(Camera camera, string name)
        {
            if (camera == null || output == null) return;
            var old = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; if (game != null) game.lighting.Synchronize(); camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally { camera.targetTexture = old; RenderTexture.active = active; target.Release(); Destroy(target); if (texture != null) Destroy(texture); }
        }
    }
}
