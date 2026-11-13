using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Focused saved-scene regression. Pose/size fixtures are disclosed below;
    // extraction, ordinary pickup/drop, key pickup/use and walking run through
    // the production course and public game interaction, without setting solved
    // or extracted state through reflection.
    public sealed class GalleryBalanceKeyValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        GalleryBottleBalanceCourse course;
        GalleryBottleBalanceEnvironment environment;
        SimulationMode previousSimulation;
        string output;
        double deadline;
        bool subscribed, ownsSimulation;
        int ticks, completions, glassEvents;
        Renderer glass;
        Renderer[] pieceRenderers;
        Vector3[] pieceMeshReference;
        Vector3 glassMeshReference;

        [Serializable] sealed class Report
        {
            public bool passed, expandedRoomPhysical, widenedPlacementAllowed, originalNikeEntryPreserved, rearOffsetMatchesInsertion;
            public bool fourOriginalArtworks, actualRoomReflectionReady, floorBottlePickup;
            public bool containedShrinkRejected, containedSafeShrinkAllowed;
            public bool contentsMoveIndependently, contentsRotate, glassContacts, contentsMeshSizePreserved, newGlassHooksSilent;
            public bool publicBottleTilt, physicalExtraction, independentDynamicRelease, noHomeCushionSnap;
            public bool edgePreview, publicPiecePickupAndDrop, receiverPreservesScaleAndRotation, bottleRefusedWithExactMessage;
            public bool shortBalanceRejected, levelDwellRevealsKey, hatchAndTrayFullyOpen, keyAvailableDoorClosed;
            public bool inactiveAndZeroDtPauseReward, publicKeyPickup, carriedKeyDoorClosed, publicKeyUse;
            public bool keyUnlocksDoor, woodenDoorSwings, actualWalkThrough, resetRestores, physicsRestored;
            public int ticks, floorPickupViews, impactCount, glassEventCount, completionEvents, capturedArtworks;
            public int audioRequestsBeforeShake, audioRequestsAfterShake;
            public float independentPositionRange, independentRotationDegrees, maximumContentsMeshScaleError;
            public float maximumGlassMeshScaleError, extractionBottleScale, extractionMouthDown, extractionDistanceFromHome, extractionExitHeight;
            public float extractionDistanceFromOldCushion, edgeScaleBefore, edgeScaleAfter, edgeRotationChange;
            public float stableSeconds, hatchProgress, trayProgress, keyViewHeight, lockViewHeight, leftDoorAngle, rightDoorAngle;
            public float walkedToZ, rearArtworkZ, widenedBottleX;
            public Vector3 reflectionSize, failureBottlePosition, failureMouthDirection, failureContentsPosition, failureContentsVelocity, failureContentsLocal;
            public float failureBottleScale;
            public string phase, failure, gpu, unity, rewardStage, extractionBlocker, placementObstacle;
            public string cueBeforeShake, cueAfterShake;
            public string scope = "Saved-museum focused regression. Disclosed fixtures place the visitor, position the bottle at (-4,1.5,35) for shaking, initialize a 1.95-scale detached bottle for extraction, and initialize sculpture source poses and a TargetSculptureScale*1.04 load. Bottle pickup, held rotation, physical contents exit, piece pickup and receiver Drop, key pickup/use, final gravity release, and CharacterController walking use public production paths. No private solved/extracted flags are set. Mesh-size checks use actual renderer mesh bounds and transform scale, independent of rotating world AABBs. This is not an uninterrupted player solution or subjective artwork/audio approval.";
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log; subscribed = true;
            previousSimulation = Physics.simulationMode;
            deadline = Time.realtimeSinceStartupAsDouble + 240;
            report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            IEnumerator sequence = Sequence();
            try
            {
                while (true)
                {
                    bool more; object next = null;
                    try { Guard(); more = sequence.MoveNext(); if (more) next = sequence.Current; }
                    catch (Exception exception) { report.failure = exception.ToString(); break; }
                    if (!more) break;
                    yield return next;
                }
            }
            finally { RestorePhysics(); Unsubscribe(); }
            report.physicsRestored = Physics.simulationMode == previousSimulation;
            report.ticks = ticks; report.completionEvents = completions; report.glassEventCount = glassEvents;
            RecordFailurePose(); report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = string.IsNullOrEmpty(report.failure) && errors.Count == 0 &&
                report.expandedRoomPhysical && report.widenedPlacementAllowed && report.originalNikeEntryPreserved && report.rearOffsetMatchesInsertion &&
                report.fourOriginalArtworks && report.actualRoomReflectionReady && report.floorBottlePickup && report.containedShrinkRejected && report.containedSafeShrinkAllowed &&
                report.contentsMoveIndependently && report.contentsRotate && report.glassContacts && report.contentsMeshSizePreserved && report.newGlassHooksSilent &&
                report.publicBottleTilt && report.physicalExtraction && report.independentDynamicRelease && report.noHomeCushionSnap &&
                report.edgePreview && report.publicPiecePickupAndDrop && report.receiverPreservesScaleAndRotation && report.bottleRefusedWithExactMessage &&
                report.shortBalanceRejected && report.levelDwellRevealsKey && report.hatchAndTrayFullyOpen && report.keyAvailableDoorClosed &&
                report.inactiveAndZeroDtPauseReward && report.publicKeyPickup && report.carriedKeyDoorClosed && report.publicKeyUse &&
                report.keyUnlocksDoor && report.woodenDoorSwings && report.actualWalkThrough && report.resetRestores && report.physicsRestored;
            if (output != null)
            {
                try
                {
                    if (!report.passed && game != null) Capture("balance-key-failure");
                    string json = JsonUtility.ToJson(report, true);
                    File.WriteAllText(Path.Combine(output, "balance-key-validation-results.json"), json);
                    // The existing review launcher reads this filename.
                    File.WriteAllText(Path.Combine(output, "bottle-balance-review-results.json"), json);
                }
                catch (Exception exception) { report.passed = false; report.failure = exception.ToString(); }
            }
            if (report.passed) Debug.Log("GALLERY_BALANCE_KEY_VALIDATION_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_BALANCE_KEY_VALIDATION_FAIL phase=" + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-validationDir");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) :
                Path.Combine(Application.persistentDataPath, "MuseumBalanceKeyValidation");
            Directory.CreateDirectory(output);
            // Environment.Start performs its real room capture after 3 frames.
            for (int i = 0; i < 6; i++) yield return null;
            Require(game != null && game.automated && game.conceptLayout, "Actual automated saved museum is required.");
            course = game.bottleBalance;
            Require(course != null && course.Bottle != null && course.Sculpture != null && course.Contents != null && course.Reward != null,
                "Expanded gallery, independent contents motion or key reward is missing.");
            environment = game.GetComponentInChildren<GalleryBottleBalanceEnvironment>();
            Require(environment != null && environment.RoomProbe != null, "Inserted-room environment component is missing.");
            for (int i = 0; i < 30 && !environment.CaptureReady; i++) yield return null;
            Require(environment.CaptureReady && environment.ArtworkCount == 4, "Four approved works or the real room reflection are not ready.");
            report.fourOriginalArtworks = report.actualRoomReflectionReady = true;
            report.capturedArtworks = environment.ArtworkCount; report.reflectionSize = environment.RoomProbe.size;
            Require(Vector3.Distance(report.reflectionSize, new Vector3(22, 6.6f, 17)) < .01f, "Reflection bounds do not follow the compact room dimensions.");
            Transform glassNode = course.Art.bottleVisual.Find("Open hand-blown vessel");
            glass = glassNode != null ? glassNode.GetComponent<Renderer>() : null;
            pieceRenderers = course.Sculpture.GetComponentsInChildren<Renderer>();
            Require(glass != null && glass.enabled && pieceRenderers.Length > 0, "Actual bottle and sculpture renderers are required.");
            glassMeshReference = IntrinsicMeshSize(glass); pieceMeshReference = new Vector3[pieceRenderers.Length];
            for (int i = 0; i < pieceRenderers.Length; i++) pieceMeshReference[i] = IntrinsicMeshSize(pieceRenderers[i]);
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            course.BalanceCompleted += Complete; course.GlassImpact += GlassImpact;
            game.StartBottleBalanceDemo();

            report.phase = "expanded architecture and original route";
            Require(GalleryBottleBalanceLayout.HalfWidth == 11 && GalleryBottleBalanceLayout.Length == 17 && GalleryBottleBalanceLayout.EndZ == 46,
                "Layout constants do not describe the approved compact 22x17m room.");
            foreach (float x in new[] { -10f, 10f })
            {
                Vector3 feet = new Vector3(x, 0, 35);
                Require(game.CanStand(feet), "New side floor is obstructed at X=" + x);
                Require(Physics.Raycast(feet + Vector3.up * .3f, Vector3.down, out RaycastHit hit, .6f, ~0, QueryTriggerInteraction.Ignore) &&
                    Mathf.Abs(hit.point.y) < .03f, "New side floor has no physical support at X=" + x);
            }
            float wideBottleX = -GalleryBottleBalanceLayout.HalfWidth + 2.5f;
            Pose(course.Bottle, new Vector3(wideBottleX, 1.5f, 35), Quaternion.identity, 1);
            Require(game.ValidPlacement(course.Bottle, out string widthReason), "Side-wing bottle placement hits the old invisible width limit: " + widthReason);
            report.widenedBottleX = course.Bottle.transform.position.x;
            report.expandedRoomPhysical = report.widenedPlacementAllowed = true;
            Pose(course.Bottle, new Vector3(wideBottleX, 1.5f, 35), Quaternion.identity, .62f);
            Require(!game.ValidPlacement(course.Bottle, out string shrinkReason) && !string.IsNullOrEmpty(shrinkReason),
                "An undersized 0.62-scale shell must reject its unchanged-size contents.");
            report.containedShrinkRejected = true;
            Pose(course.Bottle, new Vector3(wideBottleX, 1.5f, 35), Quaternion.identity, .70f);
            Require(game.ValidPlacement(course.Bottle, out string safeShrinkReason), "A fitting 0.70-scale bottle was blocked: " + safeShrinkReason);
            report.containedSafeShrinkAllowed = true;
            Require(game.shadowCourse != null && Mathf.Abs(game.shadowCourse.NikeGatePosition.z - 28.72f) < .01f,
                "Original Nike entry moved when inserting the new room.");
            report.originalNikeEntryPreserved = true;
            var portrait = game.GetComponentInChildren<GalleryPortraitRoom>();
            Require(portrait != null && portrait.originalArtwork != null, "Original portrait collection is missing.");
            report.rearArtworkZ = portrait.originalArtwork.position.z;
            Require(Mathf.Abs(report.rearArtworkZ - (36 + GalleryBottleBalanceLayout.AddedLength)) < .03f, "Original portrait artwork did not follow the inserted room length.");
            report.rearOffsetMatchesInsertion = true;
            course.ResetCourse(); game.PlacePlayer(new Vector3(0, 0, 30.5f)); Look(new Vector3(0, 2.4f, 39));
            Capture("00-expanded-gallery-and-four-original-artworks");
            Check("Compact 22x17m physical room permits X=-8.5m bottle placement, preserves Nike Z28.72 and shifts the original portrait artwork by +17m");

            report.phase = "floor bottle pickup through public interaction";
            Vector3 home = course.Bottle.homePosition;
            foreach (float offset in new[] { -1.6f, 0f, 1.6f })
            {
                course.ResetCourse(); game.PlacePlayer(home + new Vector3(offset, -home.y, -2.65f));
                Require(game.CanStand(game.player.feet) && game.player.feet.y < .05f, "Bottle pickup fixture requires standing on its plinth.");
                Look(glass.bounds.center); game.UpdateAim(); game.Interact();
                Require(game.held == course.Bottle && !course.Extracted && !SculptureCollidersEnabled(), "Visible glass centre cannot be picked from the floor.");
                report.floorPickupViews++; Capture("01-floor-pickup-" + report.floorPickupViews); game.Cancel(true);
            }
            report.floorBottlePickup = report.floorPickupViews == 3;
            Check("Public Interact picks the actual visible bottle centre from three floor viewpoints");

            report.phase = "independent contained motion and silent impact hooks";
            course.ResetCourse(); game.PlacePlayer(new Vector3(0, 0, 31.5f));
            Vector3 shakeOrigin = new Vector3(-4, 1.5f, 35);
            Pose(course.Bottle, shakeOrigin, Quaternion.identity, 1); course.Contents.Reset();
            Vector3 minRelative = Vector3.one * float.PositiveInfinity, maxRelative = Vector3.one * float.NegativeInfinity;
            Quaternion originalPieceRotation = course.Sculpture.transform.rotation;
            report.cueBeforeShake = MuseumAudio.Instance != null ? MuseumAudio.Instance.LastRequestedCue : null;
            report.audioRequestsBeforeShake = MuseumAudio.Instance != null ? MuseumAudio.Instance.RequestCount : 0;
            for (int i = 0; i < 300; i++)
            {
                float t = i * Dt;
                Vector3 at = shakeOrigin + new Vector3(Mathf.Sin(t * 8) * .16f, Mathf.Sin(t * 11) * .09f, Mathf.Sin(t * 6) * .12f);
                Quaternion turn = Quaternion.Euler(Mathf.Sin(t * 7) * 10, Mathf.Sin(t * 5) * 8, Mathf.Sin(t * 9) * 12);
                course.Bottle.transform.SetPositionAndRotation(at, turn); course.Bottle.body.position = at; course.Bottle.body.rotation = turn;
                Physics.SyncTransforms(); course.Tick(Dt); ticks++;
                Vector3 relative = course.Art.bottleVisual.InverseTransformPoint(course.Sculpture.transform.position);
                minRelative = Vector3.Min(minRelative, relative); maxRelative = Vector3.Max(maxRelative, relative);
                report.independentRotationDegrees = Mathf.Max(report.independentRotationDegrees, Quaternion.Angle(originalPieceRotation, course.Sculpture.transform.rotation));
                CheckMeshSizes();
                Require(!course.Extracted && !SculptureCollidersEnabled(), "Small closed-mouth shake released or exposed the contained sculpture.");
                if (i % 12 == 0) yield return null;
            }
            report.independentPositionRange = (maxRelative - minRelative).magnitude;
            report.impactCount = course.Contents.ImpactCount;
            Require(report.independentPositionRange > .03f && report.independentRotationDegrees > .5f && report.impactCount > 0 && glassEvents > 0,
                "Contained sculpture does not translate, rotate and hit the glass independently.");
            report.contentsMoveIndependently = report.contentsRotate = report.glassContacts = true;
            report.cueAfterShake = MuseumAudio.Instance != null ? MuseumAudio.Instance.LastRequestedCue : null;
            report.audioRequestsAfterShake = MuseumAudio.Instance != null ? MuseumAudio.Instance.RequestCount : 0;
            Require(report.cueBeforeShake == report.cueAfterShake && report.audioRequestsBeforeShake == report.audioRequestsAfterShake,
                "New contained-glass hook unexpectedly requested an audio cue before sound approval.");
            report.newGlassHooksSilent = report.contentsMeshSizePreserved = true;
            Look(course.Bottle.transform.position); Capture("02-independent-contents-after-shake");
            Check("Disclosed bottle shake produces independent contents motion and glass contacts at unchanged rendered sculpture size, without audio requests");

            report.phase = "public held tilt and physical contents exit";
            course.ResetCourse();
            // The mouth points to the visitor's left after the public yaw turn.
            // Keep this fixture on the empty central axis, clear of the real
            // bottle plinth at X=-6/Z37, and begin with enough vertical room.
            Pose(course.Bottle, new Vector3(0, 3.2f, 34), Quaternion.identity, 1.95f); course.Contents.Reset();
            game.PlacePlayer(new Vector3(0, 0, 30.2f)); Look(glass.bounds.center); game.UpdateAim(); game.Interact();
            Require(game.held == course.Bottle, "Detached enlarged fixture cannot be picked by public Interact.");
            game.RotateHeld(-90, 0);
            for (int i = 0; i < 6; i++) Step(Vector2.zero);
            for (int i = 0; i < 80 && GalleryBottleBalanceVessel.MouthDirection(course.Bottle).y > -.96f; i++)
            {
                game.RotateHeld(0, 2); Step(Vector2.zero); CheckMeshSizes();
                if (i % 12 == 0) yield return null;
            }
            // Raise the held vessel using only the normal view/placement path.
            // Do not weaken the release-point floor or exhibit collision test.
            for (int lift = 0; lift < 8 && (course.ExtractionExitPosition.y < .70f || course.ExtractionBlocked); lift++)
            {
                Look(game.view.transform.position + game.view.transform.forward * 8 + Vector3.up * .55f);
                for (int i = 0; i < 8; i++) Step(Vector2.zero);
                CheckMeshSizes(); yield return null;
            }
            report.extractionBottleScale = course.BottleSize;
            report.extractionMouthDown = GalleryBottleBalanceVessel.MouthDirection(course.Bottle).y;
            report.extractionExitHeight = course.ExtractionExitPosition.y;
            Require(report.extractionMouthDown < -.45f && course.BottleSize >= 1.75f,
                "Public held rotation could not lower the enlarged mouth into clear floor space: " + game.invalidReason);
            Require(report.extractionExitHeight >= .40f && !course.ExtractionBlocked,
                "Public camera lift did not clear the real exit volume above the floor: " + course.ExtractionBlocker);
            report.publicBottleTilt = true; Capture("03-enlarged-free-bottle-physical-mouth");
            int until = ticks + 1200;
            while (!course.Extracted && ticks < until)
            { Step(Vector2.zero); CheckMeshSizes(); if (ticks % 12 == 0) yield return null; }
            Require(course.Extracted && !course.Extracting && SculptureCollidersEnabled(), "Actual independent contents never fully exited the mouth: " + course.Status);
            Require(!course.Sculpture.body.isKinematic && course.Sculpture.body.useGravity, "Exited sculpture did not hand over to ordinary rigidbody gravity.");
            report.physicalExtraction = report.independentDynamicRelease = true;
            report.extractionDistanceFromHome = Vector3.Distance(course.Sculpture.transform.position, home);
            report.extractionDistanceFromOldCushion = Vector3.Distance(course.Sculpture.transform.position, course.Art.CatchSurfaceWorld);
            Require(report.extractionDistanceFromHome > 1 && report.extractionDistanceFromOldCushion > .75f, "Physical exit snapped back to the home exhibit or its removed cushion.");
            report.noHomeCushionSnap = true;
            if (game.held != null) game.Cancel(true);
            // Move the already emptied bottle away from the receiver approach.
            Pose(course.Bottle, new Vector3(-7, 1, 32.5f), Quaternion.identity, 1);
            Check("Public held rotation lets the independently simulated piece fully leave the real mouth, then ordinary rigidbody gravity takes over");

            report.phase = "eye-height receiver preview and public Drop";
            BoxCollider pan = course.Art.rightPan.GetComponent<BoxCollider>();
            Require(pan != null, "Right physical dish support is missing.");
            bool placed = false;
            for (int attempt = 0; attempt < 6 && !placed; attempt++)
            {
                if (game.held != null) game.Cancel(true);
                Vector3 top = PanTop(pan);
                game.PlacePlayer(new Vector3(top.x + (attempt % 3 - 1) * .65f, 0, top.z - 3.25f));
                Pose(course.Sculpture, top + new Vector3(-1.15f, .48f, -.25f), Quaternion.Euler(0, 45, 0), attempt < 3 ? 1.3f : .85f);
                Look(course.Sculpture.WorldBounds.center); game.UpdateAim(); game.Interact();
                Require(game.held == course.Sculpture, "Extracted sculpture public pickup failed.");
                Look(PanTop(pan) + Vector3.up * .14f);
                for (int i = 0; i < 20; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                if (!course.Placement.HasPreview) continue;
                Require(game.player.feet.y < .05f, "Receiver preview required climbing above the floor.");
                report.edgeScaleBefore = course.Sculpture.transform.localScale.x;
                Quaternion before = course.Sculpture.transform.rotation;
                Capture("04-floor-eye-height-piece-receiver-preview");
                placed = game.Drop();
                if (!placed) continue;
                report.edgeScaleAfter = course.Sculpture.transform.localScale.x;
                report.edgeRotationChange = Quaternion.Angle(before, course.Sculpture.transform.rotation);
                Require(Mathf.Abs(report.edgeScaleBefore - report.edgeScaleAfter) < .002f && report.edgeRotationChange < .1f,
                    "Receiver auto-resized or auto-rotated the current sculpture on release.");
                Require(game.held == null && !course.Sculpture.body.isKinematic && course.Sculpture.body.useGravity, "Public receiver Drop bypassed normal release physics.");
            }
            Require(placed, "No real dish-edge receiver preview could complete a public Drop.");
            report.edgePreview = report.publicPiecePickupAndDrop = report.receiverPreservesScaleAndRotation = true;
            Check("Floor eye-height dish preview and public Drop preserve the user's current piece scale and rotation");

            report.phase = "whole bottle refusal";
            Vector3 dish = PanTop(pan); game.PlacePlayer(new Vector3(dish.x, 0, dish.z - 3));
            Pose(course.Bottle, dish + new Vector3(-.65f, .42f, -.45f), Quaternion.identity, .65f);
            Look(glass.bounds.center); game.UpdateAim(); game.Interact(); Require(game.held == course.Bottle, "Bottle-refusal public pickup failed.");
            Look(dish + Vector3.up * .15f);
            for (int i = 0; i < 20; i++) Step(Vector2.zero);
            Require(course.Placement.TargetedPan && !game.Drop() && game.held == course.Bottle &&
                game.notice == GalleryBottleBalancePlacement.BottleForbiddenMessage, "Whole-bottle pan refusal or its exact explanation regressed.");
            report.bottleRefusedWithExactMessage = true; Capture("05-whole-bottle-refused"); game.Cancel(true);
            Pose(course.Bottle, new Vector3(-7, 1, 32.5f), Quaternion.identity, 1);

            report.phase = "real dynamic load and two-second balance";
            // Clear any dwell accrued during the unrelated receiver test.
            Pose(course.Sculpture, new Vector3(2, 2.5f, 33), Quaternion.identity, 1);
            for (int i = 0; i < 30; i++) Step(Vector2.zero);
            Require(!course.Solved && course.StableSeconds < .01f, "Unsupported load retained earlier dwell.");
            PoseOnPan(course.Sculpture, pan, GalleryBottleBalanceCourse.TargetSculptureScale * 1.04f);
            course.Sculpture.Release();
            for (int i = 0; i < 70; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(!course.Solved && course.Door.Opening < .001f, "Door or reward completed before two seconds of level dwell.");
            report.shortBalanceRejected = true;
            until = ticks + 1200;
            while (!course.Solved && ticks < until) { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Solved && completions == 1 && course.StableSeconds >= 2, "Released +4% size load did not complete a single real balance dwell.");
            report.stableSeconds = course.StableSeconds;
            float hatchBefore = course.Reward.HatchProgress, trayBefore = course.Reward.TrayProgress;
            course.Tick(0); game.active = false; course.Tick(.5f); game.active = true;
            Require(Mathf.Abs(hatchBefore - course.Reward.HatchProgress) < .0001f && Mathf.Abs(trayBefore - course.Reward.TrayProgress) < .0001f,
                "Inactive or zero-dt course advanced the reward mechanism.");
            report.inactiveAndZeroDtPauseReward = true;
            until = ticks + 400;
            while (!course.Reward.KeyAvailable && ticks < until) { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Reward.KeyAvailable && !course.Reward.Unlocked && course.Door.Barrier.enabled && course.Door.Opening < .001f,
                "Balance must reveal a usable key while the door remains locked.");
            report.hatchProgress = course.Reward.HatchProgress; report.trayProgress = course.Reward.TrayProgress;
            Require(report.hatchProgress > .999f && report.trayProgress > .999f, "Key became ready before the hatch and tray completed.");
            report.keyViewHeight = course.Reward.KeyTransform.position.y; report.lockViewHeight = course.Reward.LockTransform.position.y;
            Require(report.keyViewHeight >= 1.45f && report.keyViewHeight <= 1.85f && Mathf.Abs(report.lockViewHeight - 1.25f) < .12f,
                "Key or keyhole is outside the approved floor-reachable height.");
            report.levelDwellRevealsKey = report.hatchAndTrayFullyOpen = report.keyAvailableDoorClosed = true;
            Vector3 keyAt = course.Reward.KeyTransform.position;
            game.PlacePlayer(new Vector3(keyAt.x, 0, keyAt.z - 1.6f)); Look(keyAt); course.Reward.RefreshAim(game.view);
            Capture("06-open-central-hatch-key-on-tray-door-locked");
            Check("A released +4% size load maintains level for two seconds, opens the 80-degree cover and tray, and reveals a reachable key while the door stays locked");

            report.phase = "public key pickup and explicit door use";
            game.UpdateAim(); game.Interact();
            Require(course.Reward.Carrying && !course.Reward.Unlocked && course.Door.Opening < .001f, "Public key pickup did not carry the key with the door still closed.");
            report.publicKeyPickup = report.carriedKeyDoorClosed = true; Capture("07-carried-key-before-door-use");
            Vector3 lockAt = course.Reward.LockTransform.position;
            game.PlacePlayer(new Vector3(lockAt.x, 0, lockAt.z - 1.4f)); Look(lockAt); course.Reward.RefreshAim(game.view);
            Require(course.Reward.TargetedLock, "Floor camera cannot target the door's actual keyhole.");
            game.UpdateAim(); game.Interact();
            Require(course.Reward.Busy && !course.Reward.Unlocked, "Explicit door click did not begin key insertion and turning.");
            report.publicKeyUse = true; Capture("08-key-insertion-at-brass-keyhole");
            until = ticks + 600;
            while ((!course.Reward.Unlocked || course.Door.Opening < .999f) && ticks < until)
            { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Reward.Unlocked && !course.Door.Barrier.enabled && course.Door.Opening > .999f, "Key insertion/turning did not unlock and physically open the door.");
            report.leftDoorAngle = Quaternion.Angle(Quaternion.identity, course.Door.LeftLeaf.localRotation);
            report.rightDoorAngle = Quaternion.Angle(Quaternion.identity, course.Door.RightLeaf.localRotation);
            Require(report.leftDoorAngle > 95 && report.rightDoorAngle > 95 && Mathf.Abs(course.Door.Root.position.y) < .001f, "Exit did not swing its walnut leaves at fixed ground height.");
            report.keyUnlocksDoor = report.woodenDoorSwings = true;
            game.PlacePlayer(new Vector3(0, 0, GalleryBottleBalanceLayout.EndZ - 2));
            Look(new Vector3(0, 1.575f, GalleryBottleBalanceLayout.EndZ + 5)); Capture("09-key-unlocked-hinged-door");
            until = ticks + 500;
            while (game.player.feet.z < GalleryBottleBalanceLayout.EndZ + .6f && ticks < until)
            { Step(Vector2.up); if (ticks % 12 == 0) yield return null; }
            report.walkedToZ = game.player.feet.z;
            Require(report.walkedToZ > GalleryBottleBalanceLayout.EndZ + .4f && game.checkpoint >= 2, "Actual CharacterController cannot pass into the shifted portrait room.");
            report.actualWalkThrough = true; Capture("10-walk-into-shifted-eyes-nose-mouth-room");
            Check("Public key pickup preserves the locked door; explicit keyhole use inserts/turns the key, swings both walnut leaves, and permits real passage beyond the current inserted-room exit");

            report.phase = "reset";
            game.ResetAll();
            Require(!course.Extracted && !course.Extracting && !course.Solved && !course.Reward.Carrying && !course.Reward.KeyAvailable &&
                !course.Reward.Unlocked && course.Reward.HatchProgress < .001f && course.Reward.TrayProgress < .001f &&
                course.Door.Opening < .001f && course.Door.Barrier.enabled && game.held == null && !SculptureCollidersEnabled(),
                "Normal reset did not restore contained sculpture, closed hatch/tray, key and locked wooden door.");
            report.resetRestores = true; Check("Reset restores the entire contained-motion, balance, key-reward and locked-door sequence");
            report.phase = "complete";
        }

        static Vector3 IntrinsicMeshSize(Renderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>();
            Require(mesh != null && mesh.sharedMesh != null, "Visible renderer has no measurable actual mesh.");
            Vector3 scale = renderer.transform.lossyScale;
            return Vector3.Scale(mesh.sharedMesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
        void CheckMeshSizes()
        {
            for (int i = 0; i < pieceRenderers.Length; i++)
                report.maximumContentsMeshScaleError = Mathf.Max(report.maximumContentsMeshScaleError,
                    Vector3.Distance(IntrinsicMeshSize(pieceRenderers[i]), pieceMeshReference[i]) / Mathf.Max(.001f, pieceMeshReference[i].magnitude));
            Vector3 expectedGlass = glassMeshReference * course.BottleSize;
            report.maximumGlassMeshScaleError = Mathf.Max(report.maximumGlassMeshScaleError,
                Vector3.Distance(IntrinsicMeshSize(glass), expectedGlass) / Mathf.Max(.001f, expectedGlass.magnitude));
            Require(report.maximumContentsMeshScaleError < .015f && report.maximumGlassMeshScaleError < .015f,
                "Rendered glass or sculpture intrinsic mesh size violated independent scaling.");
        }
        bool SculptureCollidersEnabled()
        { foreach (var part in course.Sculpture.parts) if (part.enabled) return true; return false; }
        static Vector3 PanTop(BoxCollider pan)
        { Bounds b = pan.bounds; return new Vector3(b.center.x, b.max.y, b.center.z); }
        void Pose(GalleryProp prop, Vector3 at, Quaternion rotation, float scale)
        {
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease(); HeldCollisionState.Attach(prop.body).Restore();
            if (!prop.body.isKinematic) { prop.body.linearVelocity = Vector3.zero; prop.body.angularVelocity = Vector3.zero; }
            prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.SetPositionAndRotation(at, rotation); prop.transform.localScale = Vector3.one * scale;
            prop.body.position = at; prop.body.rotation = rotation; prop.RefreshMass(); Physics.SyncTransforms(); prop.placement.ResetPlaced();
            if (prop == course.Bottle && !course.Extracted) course.Contents.Reset();
        }
        void PoseOnPan(GalleryProp prop, BoxCollider pan, float scale)
        {
            Vector3 top = PanTop(pan); Pose(prop, top + Vector3.up, Quaternion.identity, scale);
            Bounds bounds = prop.WorldBounds;
            prop.transform.position += top - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + Vector3.up * .008f;
            prop.body.position = prop.transform.position; Physics.SyncTransforms(); prop.placement.ResetPlaced();
        }
        void Look(Vector3 target)
        {
            Vector3 d = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(d.x, d.z); game.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); game.SyncView();
        }
        void Step(Vector2 move)
        { Guard(); game.Tick(Dt, move, false); if (game.active) Physics.Simulate(Dt); ticks++; }
        void Capture(string name)
        {
            Camera camera = game.view; RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Destroy(target);
                if (texture != null) Destroy(texture);
            }
        }
        void RecordFailurePose()
        {
            if (course == null) return;
            report.failureBottlePosition = course.Bottle.transform.position;
            report.failureMouthDirection = GalleryBottleBalanceVessel.MouthDirection(course.Bottle);
            report.failureContentsPosition = course.Sculpture.transform.position;
            report.failureBottleScale = course.BottleSize;
            report.failureContentsVelocity = course.Contents != null ? course.Contents.Velocity : Vector3.zero;
            report.failureContentsLocal = course.Art.bottleVisual.InverseTransformPoint(course.Sculpture.transform.position);
            report.extractionBlocker = course.ExtractionBlocker; report.placementObstacle = game.PlacementObstacle;
            report.rewardStage = course.Reward != null ? course.Reward.Stage.ToString() : "missing";
        }
        void Complete() { completions++; }
        void GlassImpact(float speed, Vector3 at) { glassEvents++; }
        void Check(string text) { checks.Add(text); Debug.Log("BALANCE_KEY_PASS " + text); }
        void Guard() { Require(Time.realtimeSinceStartupAsDouble <= deadline, "Validation exceeded 240 seconds."); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("BALANCE_KEY_VALIDATION: " + message); }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void RestorePhysics()
        { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Unsubscribe()
        {
            if (subscribed) Application.logMessageReceived -= Log;
            if (course != null) { course.BalanceCompleted -= Complete; course.GlassImpact -= GlassImpact; }
            subscribed = false;
        }
        void OnDestroy() { RestorePhysics(); Unsubscribe(); }
    }
}
