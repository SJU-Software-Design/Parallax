using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Regression of the three concrete usability reports. Initial exhibit poses
    // are disclosed fixtures; picking, normal projection and receiver release
    // must run through the game's public production interaction methods.
    public sealed class GalleryBottleBalanceReviewValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        GalleryBottleBalanceCourse course;
        SimulationMode previousSimulation;
        readonly Report report = new Report();
        readonly List<string> errors = new List<string>(), checks = new List<string>();
        string output;
        bool subscribed, ownsSimulation;
        int ticks, completions;
        double deadline;
        Renderer glassRenderer;
        Renderer[] sculptureRenderers;

        [Serializable] sealed class VisualScaleSample
        {
            public float rootScale, glassViewportHeight, contentsViewportHeight;
            public Vector3 glassWorldSize, contentsWorldSize;
        }

        [Serializable] sealed class Report
        {
            public bool passed, floorPickupAtThreeViews, floorVisualCentrePickup, floorContentsCentrePickup, floorRimPickup;
            public bool ordinaryBottleCarry, perspectiveRatioPreserved, glassMeshScalePreserved, contentsMeshSizePreserved;
            public bool freeBottleTranslation, freeBottleResizing, releasedPosePreserved, releasedBottleUsesGravity;
            public bool internalSizePreserved, detachedExtraction, noHomeCushionSnap;
            public bool eyeHeightEdgePreview, publicEdgeDrop, receiverPreservesCurrentScale, receiverPreservesCurrentRotation, ordinaryReleasedSculpture;
            public bool bottlePanRejected, bottleRejectedWithExactMessage, bottleRejectedWithErrorCue;
            public bool shortBalanceRejected, balancedDoorOpens, woodenDoorHinges, doorRootDoesNotRise, actualPassageWalk;
            public bool balanceLowInsideAccepted, balanceHighInsideAccepted, balanceLowOutsideRejected, balanceHighOutsideRejected;
            public bool balanceHysteresisPreserved, briefAirbornePausedAndResumed, sustainedAirborneClears;
            public bool resetRestores, physicsRestored;
            public int floorPickups, floorPickupViews, ticks, completionEvents, perspectiveSamples;
            public float bottleMovementMetres, minimumHeldBottleScale, maximumHeldBottleScale, maximumRatioError;
            public float maximumGlassWorldScaleError, maximumContentsWorldSizeError;
            public Vector3 baselineGlassWorldSize, baselineContentsWorldSize;
            public VisualScaleSample[] visualScaleSamples;
            public float edgeEyeHeight, edgePanHeight, edgeViewDegrees, edgeScaleBeforeDrop, edgeScaleAfterDrop, edgeExpectedPerspectiveScale;
            public float edgeRotationChangeDegrees;
            public float extractionDistanceFromHome, extractionDistanceFromCushion, leftDoorAngle, rightDoorAngle;
            public float balanceLowInsideStableSeconds, balanceHighInsideStableSeconds;
            public float hysteresisStableBefore, hysteresisStableAfter, briefAirborneStableBefore, briefAirborneStableAfter, resumedStableSeconds;
            public float sustainedAirborneStableBefore, sustainedAirborneStableAfter;
            public Vector3 failureBottlePosition, failureMouthPosition, failureMouthDirection, extractionExitPosition;
            public bool extractionBlocked;
            public float failureBottleScale, failureHeldDepth;
            public bool failureProjectionApplied, failurePlacementBlocked;
            public string extractionBlocker, failurePlacementObstacle, failureInvalidReason;
            public string phase, failure, gpu, unity;
            public string scope = "Focused production-path regression for floor pickup, free perspective bottle motion, eye-height dish-edge placement and a hinged wooden door. Floor pickup aims at the visible glass centre, contained sculpture centre and actual mouth rim from three viewpoints through public Interact. Ordinary projection verifies actual glass renderer world bounds scale with the bottle while contained renderer world bounds stay fixed; projected viewport heights are explanatory measurements, not exact-size assertions across changing views. The edge-placement test uses real Interact/Drop and checks that receiver placement preserves the current sculpture size. Explicit initial pose/scale fixtures separately isolate detached extraction, balance size-entry/keep boundaries, unsupported contact intervals and the final two-second dwell. This is not an uninterrupted user playthrough or subjective art/audio approval.";
            public string[] checks, errors;
        }
        readonly List<VisualScaleSample> visualScaleSamples = new List<VisualScaleSample>();

        IEnumerator Start()
        {
            Application.logMessageReceived += Log; subscribed = true;
            previousSimulation = Physics.simulationMode;
            deadline = Time.realtimeSinceStartupAsDouble + 240;
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
            RestorePhysics(); report.physicsRestored = Physics.simulationMode == previousSimulation;
            report.ticks = ticks; report.completionEvents = completions;
            RecordExtractionDiagnostics();
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.visualScaleSamples = visualScaleSamples.ToArray();
            report.passed = string.IsNullOrEmpty(report.failure) && errors.Count == 0 && report.floorPickupAtThreeViews &&
                report.floorVisualCentrePickup && report.floorContentsCentrePickup && report.floorRimPickup &&
                report.ordinaryBottleCarry && report.perspectiveRatioPreserved && report.freeBottleTranslation && report.freeBottleResizing &&
                report.glassMeshScalePreserved && report.contentsMeshSizePreserved &&
                report.releasedPosePreserved && report.releasedBottleUsesGravity && report.internalSizePreserved &&
                report.detachedExtraction && report.noHomeCushionSnap && report.eyeHeightEdgePreview && report.publicEdgeDrop &&
                report.receiverPreservesCurrentScale && report.receiverPreservesCurrentRotation && report.ordinaryReleasedSculpture && report.bottlePanRejected &&
                report.bottleRejectedWithExactMessage && report.bottleRejectedWithErrorCue && report.shortBalanceRejected &&
                report.balanceLowInsideAccepted && report.balanceHighInsideAccepted && report.balanceLowOutsideRejected && report.balanceHighOutsideRejected &&
                report.balanceHysteresisPreserved && report.briefAirbornePausedAndResumed && report.sustainedAirborneClears &&
                report.balancedDoorOpens && report.woodenDoorHinges && report.doorRootDoesNotRise && report.actualPassageWalk &&
                report.resetRestores && report.physicsRestored;
            if (output != null)
            {
                try
                {
                    if (!report.passed && game != null) Capture("review-failure");
                    File.WriteAllText(Path.Combine(output, "bottle-balance-review-results.json"), JsonUtility.ToJson(report, true));
                }
                catch (Exception exception) { report.passed = false; report.failure = exception.ToString(); }
            }
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_BOTTLE_BALANCE_REVIEW_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_BOTTLE_BALANCE_REVIEW_FAIL phase=" + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-validationDir");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) :
                Path.Combine(Application.persistentDataPath, "MuseumBottleBalanceReviewValidation");
            Directory.CreateDirectory(output);
            yield return null; yield return null;
            Require(game != null && game.automated && game.conceptLayout, "Actual automated saved museum is required.");
            course = game.bottleBalance;
            Require(course != null && course.Bottle != null && course.Sculpture != null && course.Placement != null && course.Door != null,
                "Revised free bottle, placement receiver or wooden door is missing.");
            Transform glass = course.Art.bottleVisual.Find("Open hand-blown vessel");
            glassRenderer = glass != null ? glass.GetComponent<Renderer>() : null;
            sculptureRenderers = course.Sculpture.GetComponentsInChildren<Renderer>();
            Require(glassRenderer != null && glassRenderer.enabled && glassRenderer.gameObject.activeInHierarchy &&
                glassRenderer.bounds.size.sqrMagnitude > .01f && SculptureVisualBounds().size.sqrMagnitude > .01f,
                "Actual visible glass and contained sculpture mesh renderers are required for visual-scale regression.");
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            course.BalanceCompleted += Complete;
            game.StartBottleBalanceDemo();
            game.PlacePlayer(new Vector3(0, 0, 30.1f)); Look(new Vector3(0, 1.7f, 35.6f));
            Capture("00-revised-room-and-wooden-door");

            report.phase = "three floor-level bottle pickup views";
            Vector3 bottleHome = course.Bottle.homePosition;
            foreach (float side in new[] { -1.8f, 0f, 1.8f })
            {
                Vector3 feet = new Vector3(bottleHome.x + side, 0, bottleHome.z - 2.65f);
                for (int target = 0; target < 3; target++)
                {
                    course.ResetCourse(); game.PlacePlayer(feet);
                    Require(game.CanStand(feet) && game.controller.transform.position.y < .04f,
                        "Floor pickup fixture intersects the pedestal or raises the visitor above the floor.");
                    string aimName = target == 0 ? "visible glass centre" : target == 1 ? "contained sculpture centre through glass" : "visible mouth rim";
                    Vector3 aim = target == 0 ? glassRenderer.bounds.center : target == 1 ? SculptureVisualBounds().center :
                        course.Art.bottleVisual.TransformPoint(new Vector3(1.045f, .22f, 0));
                    Look(aim); game.UpdateAim(); game.Interact();
                    Require(game.held == course.Bottle && course.Bottle.placement.Held,
                        "Floor public Interact failed at " + aimName + ", side=" + side + "; it must pick the bottle without climbing.");
                    Require(!course.Extracted && !AnySculptureColliderEnabled(), "Contained sculpture became independently pickable.");
                    report.floorPickups++;
                    if (target == 2) Capture("01-floor-pickup-" + (report.floorPickupViews + 1));
                    game.Cancel(true);
                }
                report.floorPickupViews++;
            }
            report.floorPickupAtThreeViews = report.floorPickupViews == 3 && report.floorPickups == 9;
            report.floorVisualCentrePickup = report.floorContentsCentrePickup = report.floorRimPickup = true;
            report.ordinaryBottleCarry = true;
            Check("Three floor viewpoints each pick the visible glass centre, contained sculpture centre through glass and actual rim through public Interact");

            report.phase = "ordinary free bottle projection and release";
            course.ResetCourse();
            Vector3 closeFloor=new Vector3(bottleHome.x,0,bottleHome.z-1.45f);
            game.PlacePlayer(closeFloor);
            Require(game.CanStand(closeFloor)&&game.controller.transform.position.y<.04f,
                "Close bottle growth fixture requires climbing the display rather than standing on the floor.");
            float visualReferenceScale = course.Bottle.transform.lossyScale.x;
            report.baselineGlassWorldSize = glassRenderer.bounds.size;
            report.baselineContentsWorldSize = SculptureVisualBounds().size;
            RecordVisualScale();
            Look(BottleAim()); game.Interact(); Require(game.held == course.Bottle, "Free carry pickup failed.");
            Vector3[] targets =
            {
                new Vector3(-.8f, .02f, 34.4f), new Vector3(-.8f,3.25f,39.6f),
                new Vector3(-1.5f,3.2f,40.4f),new Vector3(-.8f, .02f, 38.7f),
                new Vector3(-2.0f, .02f, 33.7f),new Vector3(-7.6f,2.7f,39.3f),
                new Vector3(.1f, .02f, 36.8f)
            };
            report.minimumHeldBottleScale = float.PositiveInfinity;
            report.maximumHeldBottleScale = 0;
            bool canDrop = false;
            foreach (Vector3 target in targets)
            {
                Look(target);
                for (int i = 0; i < 30; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                float actual = course.Bottle.transform.localScale.x;
                report.minimumHeldBottleScale = Mathf.Min(report.minimumHeldBottleScale, actual);
                report.maximumHeldBottleScale = Mathf.Max(report.maximumHeldBottleScale, actual);
                if(game.ProjectionApplied)
                {
                    float expected = game.referenceScale * game.holdDistance / game.referenceDistance * game.PlayerOverlapReduction;
                    report.maximumRatioError = Mathf.Max(report.maximumRatioError, Mathf.Abs(actual - expected));
                    report.perspectiveSamples++;
                }
                report.bottleMovementMetres = Mathf.Max(report.bottleMovementMetres,
                    Vector3.Distance(course.Bottle.transform.position, bottleHome));
                Require((course.Sculpture.transform.localScale - Vector3.one).sqrMagnitude < .000001f,
                    "Contained sculpture inherited the bottle's changing scale.");
                // Check the actual rendered geometry as well as its owning root:
                // a correct root scale alone can conceal an incorrectly parented mesh.
                Vector3 expectedGlassSize = report.baselineGlassWorldSize * (course.Bottle.transform.lossyScale.x / visualReferenceScale);
                report.maximumGlassWorldScaleError = Mathf.Max(report.maximumGlassWorldScaleError,
                    RelativeSizeError(glassRenderer.bounds.size, expectedGlassSize));
                report.maximumContentsWorldSizeError = Mathf.Max(report.maximumContentsWorldSizeError,
                    RelativeSizeError(SculptureVisualBounds().size, report.baselineContentsWorldSize));
                RecordVisualScale();
                Require(report.maximumGlassWorldScaleError < .015f,
                    "Actual glass renderer world bounds failed to grow with the ordinary bottle scale.");
                Require(report.maximumContentsWorldSizeError < .015f,
                    "Actual contained sculpture mesh world bounds changed while only the bottle was resized.");
                canDrop = game.ValidPlacement(course.Bottle, out _);
                if (canDrop && report.bottleMovementMetres > 1.0f && report.maximumHeldBottleScale - report.minimumHeldBottleScale > .15f &&
                    report.maximumHeldBottleScale>=1.75f && course.BottleSize>=1.75f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "02-size-guide-hud.png"));
                    yield return null;
                    break;
                }
            }
            Require(report.perspectiveSamples>0&&report.maximumRatioError < .035f, "Bottle no longer obeys ordinary perspective distance/scale math on an applied projection.");
            Require(report.bottleMovementMetres > 1.0f && report.maximumHeldBottleScale - report.minimumHeldBottleScale > .15f,
                "Bottle remains fixed to its original station or cannot be resized by normal surface aiming.");
            Require(report.maximumHeldBottleScale>=1.75f,
                "Actual close floor pickup and normal distant-surface projection did not enlarge the bottle enough for extraction.");
            report.perspectiveRatioPreserved = report.freeBottleTranslation = report.freeBottleResizing = report.internalSizePreserved = true;
            report.glassMeshScalePreserved = report.contentsMeshSizePreserved = true;
            Require(canDrop, "No real free-floor release pose is available.");
            Vector3 releasePosition = course.Bottle.transform.position, releaseScale = course.Bottle.transform.localScale;
            Quaternion releaseRotation = course.Bottle.transform.rotation;
            Require(game.Drop() && game.held == null, "The ordinary free bottle release was rejected.");
            Require(Vector3.Distance(course.Bottle.transform.position, releasePosition) < .015f &&
                Vector3.Distance(course.Bottle.transform.localScale, releaseScale) < .002f &&
                Quaternion.Angle(course.Bottle.transform.rotation, releaseRotation) < .1f,
                "Free bottle release snapped its pose back to a fixed stand.");
            report.releasedPosePreserved = true;
            Require(!course.Bottle.body.isKinematic && course.Bottle.body.useGravity, "Free bottle did not restore ordinary gravity.");
            report.releasedBottleUsesGravity = true; Capture("02-free-bottle-release");
            Check("Ordinary carry enlarges the actual glass mesh while contained mesh world size stays fixed; projected screen heights are recorded separately because perspective keeps a held object's apparent size similar");
            Check("Close floor pickup and normal distant-surface aiming enlarge the bottle beyond extraction size with the shared ratio; free release preserves its real pose and gravity");

            report.phase = "detached enlarged bottle extraction";
            game.Cancel(true); course.ResetCourse();
            // Explicit initial size/pose fixture isolates extraction away from
            // the original display; subsequent rotation/release is production.
            Pose(course.Bottle, new Vector3(0, 1.70f, 33.8f), Quaternion.identity, 1.95f);
            game.PlacePlayer(new Vector3(0, 0, 30.5f)); Look(BottleAim()); game.UpdateAim(); game.Interact();
            Require(game.held == course.Bottle, "Detached enlarged bottle cannot be picked normally.");
            // Aim the neck into the open middle/left of this room rather than
            // into the scale's fixed left pan on the room's right-hand side.
            game.RotateHeld(-90, 0);
            for(int i=0;i<6;i++)Step(Vector2.zero);
            // A mouse turns over successive frames. Stop once the actual mouth
            // is lowered enough; an unnecessary instant 48-degree turn can put
            // an enlarged lip into the floor despite a usable 27-degree exit.
            for(int i=0;i<40&&course.Art.MouthDirection.y>-.50f;i++)
            {
                game.RotateHeld(0,2);
                Step(Vector2.zero);
                if(i%12==0)yield return null;
            }
            Require(course.Art.MouthDirection.y <= -.45f,
                "Normal upper/lower rotation did not reach the real downward extraction threshold. Obstacle="+game.PlacementObstacle+" / "+game.invalidReason);
            Require(course.BottleSize >= 1.75f, "Detached extraction fixture lost its enlarged bottle size.");
            Capture("03-free-bottle-mouth-down");
            int limit = ticks + 800;
            while (!course.Extracted && ticks < limit)
            { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Extracted && !course.Extracting && AnySculptureColliderEnabled(), "Detached bottle failed to release its single independent sculpture. " + course.Status);
            if(game.held==course.Bottle)Require(game.Drop(),"Detached tilted bottle cannot be released after extraction.");
            report.extractionDistanceFromHome = Vector3.Distance(course.Sculpture.transform.position, bottleHome);
            report.extractionDistanceFromCushion = Vector3.Distance(course.Sculpture.transform.position, course.Art.CatchSurfaceWorld);
            Require(report.extractionDistanceFromCushion > .75f, "Sculpture teleported to the old fixed home cushion instead of exiting the moved bottle.");
            report.detachedExtraction = report.noHomeCushionSnap = true;
            Check("An enlarged bottle moved away from its station releases the unchanged sculpture through its real downward mouth without a home-cushion snap");

            report.phase = "eye-height thin dish edge public placement";
            bool placed = false;
            var panShape = course.Art.rightPan.GetComponent<BoxCollider>();
            Require(panShape != null, "Actual dish support collider is missing.");
            for (int attempt = 0; attempt < 6 && !placed; attempt++)
            {
                if (game.held != null) game.Cancel(true);
                float sourceScale = attempt < 3 ? 1.30f : .85f;
                Vector3 top = PanTop(panShape);
                Vector3 feet = top + new Vector3(attempt % 3 == 0 ? 0 : attempt % 3 == 1 ? -.7f : .7f, -top.y, -3.25f);
                game.PlacePlayer(feet);
                Vector3 initial = top + new Vector3(-1.15f, .48f, -.25f);
                Pose(course.Sculpture, initial, Quaternion.Euler(0, 45, 0), sourceScale);
                Look(course.Sculpture.WorldBounds.center); game.UpdateAim(); game.Interact();
                Require(game.held == course.Sculpture, "Sculpture pickup did not use ordinary production interaction.");
                top = PanTop(panShape);
                Look(top + Vector3.up * .14f);
                for (int i = 0; i < 20; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                if (!course.Placement.HasPreview) continue;
                report.edgeEyeHeight = game.view.transform.position.y;
                report.edgePanHeight = top.y;
                Vector3 direction = top - game.view.transform.position;
                report.edgeViewDegrees = Mathf.Abs(Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg);
                Require(report.edgeViewDegrees < 10 && game.controller.transform.position.y < .04f,
                    "Dish placement fixture relies on climbing up to see its top surface.");
                report.edgeScaleBeforeDrop = course.Sculpture.transform.localScale.x;
                Quaternion edgeRotationBeforeDrop = course.Sculpture.transform.rotation;
                report.edgeExpectedPerspectiveScale=game.referenceScale*game.holdDistance/game.referenceDistance*game.PlayerOverlapReduction;
                course.Placement.TryPreview(course.Sculpture,out _,out _);
                Require(Mathf.Abs(report.edgeScaleBeforeDrop-course.Sculpture.transform.localScale.x)<.002f,
                    "Receiver preview changed the current sculpture scale before release.");
                Require(Quaternion.Angle(edgeRotationBeforeDrop, course.Sculpture.transform.rotation) < .1f,
                    "Receiver preview changed the current sculpture rotation.");
                Capture("04-eye-height-dish-edge-preview");
                placed = game.Drop();
                if (placed)
                {
                    report.edgeScaleAfterDrop = course.Sculpture.transform.localScale.x;
                    Require(Mathf.Abs(report.edgeScaleBeforeDrop - report.edgeScaleAfterDrop) < .002f,
                        "Dish receiver resized the sculpture while placing it.");
                    report.edgeRotationChangeDegrees = Quaternion.Angle(edgeRotationBeforeDrop, course.Sculpture.transform.rotation);
                    Require(report.edgeRotationChangeDegrees < .1f,
                        "Dish receiver rotated the sculpture while placing it.");
                    Require(game.held == null && !course.Sculpture.body.isKinematic && course.Sculpture.body.useGravity,
                        "Dish placement did not finish through normal dynamic release.");
                    for (int i = 0; i < 65; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                }
            }
            Require(placed, "Eye-height edge aiming never offered and completed a real dish placement.");
            report.eyeHeightEdgePreview = report.publicEdgeDrop = report.receiverPreservesCurrentScale = report.receiverPreservesCurrentRotation = report.ordinaryReleasedSculpture = true;
            Check("Floor eye-height edge aiming offers an actual receiver preview and public Drop preserves the sculpture's current size and 45-degree yaw");

            report.phase = "whole bottle is explicitly rejected by the dish";
            if (game.held != null) game.Cancel(true);
            Vector3 dishTop = PanTop(panShape);
            game.PlacePlayer(new Vector3(dishTop.x, 0, dishTop.z - 3.0f));
            Pose(course.Bottle, dishTop + new Vector3(-.65f, .42f, -.45f), Quaternion.identity, .65f);
            Look(BottleAim()); game.UpdateAim(); game.Interact();
            Require(game.held == course.Bottle, "Could not pick up a free bottle for the explicit dish rejection check.");
            Look(dishTop + Vector3.up * .15f);
            for (int i = 0; i < 20; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(course.Placement.TargetedPan, "Whole-bottle attempt did not address the real dish receiver.");
            bool droppedBottle = game.Drop();
            Require(!droppedBottle && game.held == course.Bottle, "Whole bottle was accepted on the weighing dish.");
            report.bottlePanRejected = true;
            Require(game.notice == GalleryBottleBalancePlacement.BottleForbiddenMessage, "Whole-bottle rejection did not show the requested exact explanation.");
            report.bottleRejectedWithExactMessage = true;
            Require(MuseumAudio.Instance != null && MuseumAudio.Instance.LastRequestedCue == "ui.error", "Whole-bottle rejection did not use the existing error cue.");
            report.bottleRejectedWithErrorCue = true; Capture("05-whole-bottle-rejected");
            game.Cancel(true);
            Check("Whole-bottle dish release is rejected without losing the held bottle and emits the exact explanation plus existing error cue");

            report.phase = "forgiving size boundary and real contact pauses";
            // These are disclosed load/contact fixtures, separate from the
            // public edge-placement check above. Each entry case begins with
            // the previous capture expired, so keep tolerance cannot mask a
            // too-wide entry band.
            foreach (float sign in new[] { -1f, 1f })
            {
                PutSculptureAbovePan(panShape);
                int clearTicks = Mathf.CeilToInt((GalleryBottleBalanceCourse.BalanceJitterGrace + .08f) / Dt);
                for (int i = 0; i < clearTicks; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                Require(!course.BalanceCaptured && course.StableSeconds < .001f && !course.Solved,
                    "An expired, unsupported load retained its balance capture.");

                float outside = 1 + sign * (GalleryBottleBalanceCourse.BalanceEnterSizeTolerance + .005f);
                PoseOnPan(course.Sculpture, panShape, GalleryBottleBalanceCourse.TargetSculptureScale * outside);
                for (int i = 0; i < 100; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
                Require(!course.BalanceCaptured && course.StableSeconds < .001f && !course.Solved,
                    "A fresh load just outside the +/-10% size entry tolerance earned balance time. Scale ratio=" + outside);
                if (sign < 0) report.balanceLowOutsideRejected = true; else report.balanceHighOutsideRejected = true;

                float inside = 1 + sign * (GalleryBottleBalanceCourse.BalanceEnterSizeTolerance - .005f);
                PoseOnPan(course.Sculpture, panShape, GalleryBottleBalanceCourse.TargetSculptureScale * inside);
                int acceptLimit = ticks + 180;
                while (course.StableSeconds < .30f && ticks < acceptLimit)
                { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
                Require(course.BalanceCaptured && course.StableSeconds >= .30f && !course.Solved,
                    "A supported resting load just inside +/-10% size tolerance could not begin a balance dwell. Scale ratio=" + inside);
                if (sign < 0)
                { report.balanceLowInsideAccepted = true; report.balanceLowInsideStableSeconds = course.StableSeconds; }
                else
                { report.balanceHighInsideAccepted = true; report.balanceHighInsideStableSeconds = course.StableSeconds; }
            }

            float keepRatio = 1 + GalleryBottleBalanceCourse.BalanceKeepSizeTolerance - .01f;
            Require(keepRatio > 1 + GalleryBottleBalanceCourse.BalanceEnterSizeTolerance,
                "The keep-band fixture must sit outside the fresh-entry tolerance.");
            PoseOnPan(course.Sculpture, panShape, GalleryBottleBalanceCourse.TargetSculptureScale * keepRatio);
            report.hysteresisStableBefore = course.StableSeconds;
            for (int i = 0; i < 18; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            report.hysteresisStableAfter = course.StableSeconds;
            Require(course.BalanceCaptured && report.hysteresisStableAfter > report.hysteresisStableBefore + .15f && !course.Solved,
                "A captured +12% size load lost progress inside the +/-13% keep band.");
            report.balanceHysteresisPreserved = true;

            report.briefAirborneStableBefore = course.StableSeconds;
            PutSculptureAbovePan(panShape);
            for (int i = 0; i < 9; i++) Step(Vector2.zero);
            report.briefAirborneStableAfter = course.StableSeconds;
            Require(course.BalanceCaptured && Mathf.Abs(report.briefAirborneStableAfter - report.briefAirborneStableBefore) < Dt * .5f,
                "A 0.1-second unsupported interval reset the dwell or earned airborne balance time.");
            PoseOnPan(course.Sculpture, panShape, GalleryBottleBalanceCourse.TargetSculptureScale * keepRatio);
            for (int i = 0; i < 9; i++) Step(Vector2.zero);
            report.resumedStableSeconds = course.StableSeconds;
            Require(course.BalanceCaptured && report.resumedStableSeconds > report.briefAirborneStableAfter + .05f && !course.Solved,
                "The supported sculpture did not resume its paused capture after brief contact noise.");
            report.briefAirbornePausedAndResumed = true;

            report.sustainedAirborneStableBefore = course.StableSeconds;
            PutSculptureAbovePan(panShape);
            int unsupportedTicks = Mathf.CeilToInt((GalleryBottleBalanceCourse.BalanceJitterGrace + .10f) / Dt);
            for (int i = 0; i < unsupportedTicks; i++)
            {
                Step(Vector2.zero);
                Require(course.StableSeconds <= report.sustainedAirborneStableBefore + .001f,
                    "An unsupported load earned balance dwell time during contact grace.");
                if (i % 12 == 0) yield return null;
            }
            report.sustainedAirborneStableAfter = course.StableSeconds;
            Require(!course.BalanceCaptured && report.sustainedAirborneStableAfter < .001f && !course.Solved,
                "A 0.3-second unsupported load did not clear capture and dwell.");
            report.sustainedAirborneClears = true;
            Check("Fresh +/-9.5% size loads accrue balance time; +/-10.5% fresh loads are rejected, while a captured +12% load stays inside the keep band");
            Check("A 0.1-second loss of contact pauses and resumes dwell without earning airborne time; a 0.3-second loss clears it");

            report.phase = "two-second balance and hinged wooden door";
            Pose(course.Bottle, new Vector3(-3.2f, .9f, 32.0f), Quaternion.identity, .65f);
            PoseOnPan(course.Sculpture, panShape, GalleryBottleBalanceCourse.TargetSculptureScale);
            Vector3 doorStart = course.Door.Root.position;
            for (int i = 0; i < 70; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(!course.Solved && course.Door.Barrier.enabled, "Wooden exit opened before the level dwell completed.");
            report.shortBalanceRejected = true;
            limit = ticks + 900;
            while ((!course.Solved || course.Door.Opening < .999f) && ticks < limit)
            { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Solved && course.StableSeconds >= 2 && completions == 1 && !course.Door.Barrier.enabled,
                "Real balance dwell did not open the wooden exit exactly once.");
            report.balancedDoorOpens = true;
            report.leftDoorAngle = Quaternion.Angle(Quaternion.identity, course.Door.LeftLeaf.localRotation);
            report.rightDoorAngle = Quaternion.Angle(Quaternion.identity, course.Door.RightLeaf.localRotation);
            Require(report.leftDoorAngle >= 95 && report.rightDoorAngle >= 95, "Exit did not swing both wooden leaves on their hinges.");
            report.woodenDoorHinges = true;
            Require(Vector3.Distance(doorStart, course.Door.Root.position) < .001f && Mathf.Abs(course.Door.Root.localPosition.y) < .001f,
                "Wooden door root rose like the removed portcullis.");
            report.doorRootDoesNotRise = true;
            game.PlacePlayer(new Vector3(0, 0, 38.9f)); Look(new Vector3(0, 1.55f, 45)); Capture("06-open-hinged-walnut-door");
            limit = ticks + 400;
            while (game.player.feet.z < 41.4f && ticks < limit)
            { Step(Vector2.up); if (ticks % 12 == 0) yield return null; }
            Require(game.player.feet.z > 41.2f, "Actual visitor capsule cannot walk through the opened wooden doorway.");
            report.actualPassageWalk = true; Capture("07-walk-through-wooden-door");
            Check("Two-second level dwell swings the walnut leaves, keeps the doorway fixed, clears physical collision and permits real passage");

            report.phase = "reset";
            game.ResetAll();
            Require(!course.Extracted && !course.Extracting && !course.Solved && course.Door.Opening < .001f && course.Door.Barrier.enabled &&
                !AnySculptureColliderEnabled() && game.held == null,
                "Reset failed to restore the free bottle, contained artwork and closed wooden door.");
            report.resetRestores = true; Check("Normal reset restores the revised exhibit and closed hinged door");
            report.phase = "complete";
        }

        Vector3 BottleAim()
        { Bounds b = course.Bottle.WorldBounds; return b.center + Vector3.up * b.extents.y * .40f; }
        Bounds SculptureVisualBounds()
        {
            Bounds result = default; bool found = false;
            if (sculptureRenderers != null)
                foreach (Renderer renderer in sculptureRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!found) { result = renderer.bounds; found = true; }
                    else result.Encapsulate(renderer.bounds);
                }
            return result;
        }
        static float RelativeSizeError(Vector3 actual, Vector3 expected)
        { return Vector3.Distance(actual, expected) / Mathf.Max(.001f, expected.magnitude); }
        void RecordVisualScale()
        {
            Bounds glass = glassRenderer.bounds, contents = SculptureVisualBounds();
            visualScaleSamples.Add(new VisualScaleSample
            {
                rootScale = course.Bottle.transform.lossyScale.x,
                glassWorldSize = glass.size, contentsWorldSize = contents.size,
                glassViewportHeight = ViewportHeight(glass), contentsViewportHeight = ViewportHeight(contents)
            });
        }
        float ViewportHeight(Bounds bounds)
        {
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = game.view.WorldToViewportPoint(bounds.center +
                            Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        if (point.z <= 0) return 0;
                        minimum = Mathf.Min(minimum, point.y); maximum = Mathf.Max(maximum, point.y);
                    }
            // Explanatory measurement only: view rotation and position change
            // between targets, so a fixed apparent extent is not asserted.
            return maximum - minimum;
        }
        static Vector3 PanTop(BoxCollider shape)
        { Bounds b = shape.bounds; return new Vector3(b.center.x, b.max.y, b.center.z); }
        bool AnySculptureColliderEnabled()
        { foreach (var shape in course.Sculpture.parts) if (shape.enabled) return true; return false; }
        void Pose(GalleryProp prop, Vector3 at, Quaternion rotation, float scale)
        {
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease(); HeldCollisionState.Attach(prop.body).Restore();
            if (!prop.body.isKinematic) { prop.body.linearVelocity = Vector3.zero; prop.body.angularVelocity = Vector3.zero; }
            prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.SetPositionAndRotation(at, rotation); prop.transform.localScale = Vector3.one * scale;
            prop.body.position = at; prop.body.rotation = rotation; prop.RefreshMass(); Physics.SyncTransforms();
            prop.placement.ResetPlaced();
        }
        void PoseOnPan(GalleryProp prop, BoxCollider shape, float scale)
        {
            Vector3 top = PanTop(shape);
            Pose(prop, top + Vector3.up, Quaternion.identity, scale);
            Bounds b = prop.WorldBounds;
            prop.transform.position += top - new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * .008f;
            prop.body.position = prop.transform.position; Physics.SyncTransforms(); prop.placement.ResetPlaced();
        }
        void PutSculptureAbovePan(BoxCollider shape)
        {
            Pose(course.Sculpture, PanTop(shape) + Vector3.up * 2,
                Quaternion.identity, course.Sculpture.transform.lossyScale.x);
        }
        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude); game.SyncView();
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
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Destroy(target); if (texture != null) Destroy(texture); }
        }
        void Complete() { completions++; }
        void RecordExtractionDiagnostics()
        {
            if(course==null||course.Bottle==null)return;
            report.failureBottlePosition=course.Bottle.transform.position;
            report.failureBottleScale=course.BottleSize;
            report.failureHeldDepth=game.holdDistance;
            report.failureProjectionApplied=game.ProjectionApplied;
            report.failurePlacementBlocked=game.PlacementBlocked;
            report.failurePlacementObstacle=game.PlacementObstacle;
            report.failureInvalidReason=game.invalidReason;
            report.failureMouthPosition=GalleryBottleBalanceVessel.MouthWorld(course.Bottle);
            report.failureMouthDirection=GalleryBottleBalanceVessel.MouthDirection(course.Bottle);
            report.extractionExitPosition=report.failureMouthPosition+report.failureMouthDirection*.56f;
            var type=course.GetType();
            var exit=type.GetProperty("ExtractionExitPosition");
            if(exit!=null&&exit.GetValue(course) is Vector3 point)report.extractionExitPosition=point;
            var blocked=type.GetProperty("ExtractionBlocked");
            if(blocked!=null&&blocked.GetValue(course) is bool value)report.extractionBlocked=value;
            var blocker=type.GetProperty("ExtractionBlocker");
            if(blocker!=null)report.extractionBlocker=Convert.ToString(blocker.GetValue(course));
        }
        void Check(string message) { checks.Add(message); Debug.Log("BOTTLE_BALANCE_REVIEW_PASS " + message); }
        void Guard() { Require(Time.realtimeSinceStartupAsDouble <= deadline, "Review validation exceeded 240 seconds."); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("BOTTLE_BALANCE_REVIEW: " + message); }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void RestorePhysics() { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Unsubscribe()
        {
            if (subscribed) Application.logMessageReceived -= Log;
            if (course != null) course.BalanceCompleted -= Complete;
            subscribed = false;
        }
        void OnDestroy() { RestorePhysics(); Unsubscribe(); }
    }
}
