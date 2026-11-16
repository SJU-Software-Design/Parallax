using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Production interaction/motor checks plus explicitly labelled scale/pan
    // fixtures. Runtime report and GPU captures are written by the sequence.
    public sealed class GalleryBottleBalanceValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly Report report = new Report();
        GalleryBottleBalanceCourse course;
        SimulationMode previousSimulation;
        bool ownsSimulation, subscribed;
        string output;
        double deadline;
        int ticks, contacts, releases, completions, openings;

        [Serializable] sealed class Report
        {
            public bool passed, installedBetweenNikeAndPortrait, bottleCannotBecomeScaleProp;
            public bool containedPieceUnpickable, smallMouthRejects, cameraPickup, actualBackwardWalk;
            public bool anchoredPerspectiveGrowth, internalSizePreserved, pausePreserved, cancelRestores;
            public bool releaseLocksSize, approachPreservesSize, extractionCompletes, ordinaryPiecePickup;
            public bool floatingLoadRejected, smallLoadRejected, largeLoadRejected, shortBalanceRejected, interruptedBalanceReset;
            public bool balanceCompletesAfterDwell, completionOnce, passageWalk, successLatches, resetRestores;
            public bool nikeDoorFlowPreserved, nikeEntranceWalk, shiftedPortraitSolution, shiftedPortraitPerformance;
            public bool shiftedPortraitAudioMilestones, shiftedPortraitCrossing, shiftedCathedralOpening, shiftedCathedralPortal;
            public bool simulationRestored;
            public int simulatedTicks, glassContacts, sculptureReleases, balanceCompletions, passageOpenings;
            public float smallBottleSize, largeBottleSize, approachBottleSize, backwardWalkMetres, stableSecondsAtSuccess;
            public float smallRelativeLoad, largeRelativeLoad, finalRelativeLoad;
            public float portraitRedElapsed, portraitSmileElapsed, portraitClipSeconds, movedCathedralZ;
            public string phase, failure, unity, gpu;
            public string scope = "Actual saved museum plus runtime-only exhibition insertion. Public camera aim, Interact, CharacterController backward walking, Drop, approach and RotateHeld drive the bottle. The internal sculpture is not directly posed during extraction. Its first independent pickup uses ordinary production interaction. Explicitly disclosed sculpture scale/pan placement fixtures isolate underweight, overweight, interrupted/short dwell and the final balance solution; the final target fixture is released through production physics and must settle on the moving pan. Additional explicit original Nike cover/statue/painting solution poses exercise its brush, door and actual walk into the inserted room. Shifted portrait poses use its moved world transform and viewing point; production recognition, red/smile/open timeline and actual curtain crossing run normally. The existing 151 clip length and single milestone dispatch are checked on the game timeline, not DSP wall-clock completion or human listening. The moved cathedral uses an explicit full-size pose to verify opening, capsule-clear ground approach, entry geometry and actual portal rendering; no Library scene handoff is invoked here. This is focused interaction/integration QA, not an autonomous full museum puzzle solution, art approval or audio listening validation. New audio event hooks are counted without supplying unapproved sound clips.";
            public string[] checks, errors;
        }

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
            RestoreSimulation(); report.simulationRestored = Physics.simulationMode == previousSimulation;
            report.simulatedTicks = ticks; report.glassContacts = contacts; report.sculptureReleases = releases;
            report.balanceCompletions = completions; report.passageOpenings = openings;
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.installedBetweenNikeAndPortrait &&
                report.bottleCannotBecomeScaleProp && report.containedPieceUnpickable && report.smallMouthRejects &&
                report.cameraPickup && report.actualBackwardWalk && report.anchoredPerspectiveGrowth && report.internalSizePreserved &&
                report.pausePreserved && report.cancelRestores && report.releaseLocksSize && report.approachPreservesSize &&
                report.extractionCompletes && report.ordinaryPiecePickup && report.floatingLoadRejected && report.smallLoadRejected && report.largeLoadRejected &&
                report.shortBalanceRejected && report.interruptedBalanceReset && report.balanceCompletesAfterDwell &&
                report.completionOnce && report.passageWalk && report.successLatches && report.resetRestores && report.simulationRestored &&
                report.nikeDoorFlowPreserved && report.nikeEntranceWalk && report.shiftedPortraitSolution && report.shiftedPortraitPerformance &&
                report.shiftedPortraitAudioMilestones && report.shiftedPortraitCrossing && report.shiftedCathedralOpening && report.shiftedCathedralPortal;
            try
            {
                if (output != null)
                {
                    if (!report.passed && game != null) Capture("bottle-balance-failure");
                    File.WriteAllText(Path.Combine(output, "bottle-balance-results.json"), JsonUtility.ToJson(report, true));
                }
            }
            catch (Exception exception) { report.passed = false; report.failure = exception.ToString(); }
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_BOTTLE_BALANCE_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_BOTTLE_BALANCE_FAIL phase=" + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "MuseumBottleBalanceValidation");
            Directory.CreateDirectory(output);
            yield return null; yield return null;
            Require(game != null && game.automated && game.conceptLayout, "Actual automated concept museum is required.");
            course = game.bottleBalance;
            Require(course != null && course.Art != null && course.Sculpture != null, "Runtime bottle exhibition was not installed.");
            Require(Resources.Load<GameObject>("BottleBalance/Models/TrefoilKnot") != null &&
                Resources.Load<GameObject>("BottleBalance/Models/BronzeTwist") != null, "External CC0 sculpture meshes are missing.");
            var portrait = game.GetComponentInChildren<GalleryConceptPerspective>(true);
            Require(portrait != null && portrait.viewingPoint != null && portrait.viewingPoint.position.z > 41 && course.Art.bottleStation.position.z > 29 &&
                course.Art.bottleStation.position.z < 41 && course.ExitLeaf.position.z > 40,
                "New exhibition is not between Nike and the shifted portrait room.");
            report.installedBetweenNikeAndPortrait = true;
            Require(course.Art.bottlePickShape.GetComponentInParent<GalleryProp>() == null,
                "Anchored bottle must not be an ordinary carryable scale prop.");
            report.bottleCannotBecomeScaleProp = true;
            course.GlassContact += Contact; course.SculptureReleased += Released;
            course.BalanceCompleted += Completed; course.PassageOpened += Opened;
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            var nikeEntry = NikeEntryIntegration();
            while (nikeEntry.MoveNext()) yield return nikeEntry.Current;
            game.StartBottleBalanceDemo();
            game.PlacePlayer(new Vector3(0, 0, 30.1f)); Look(new Vector3(0, 1.7f, 35.6f));
            Capture("01-room-overview");
            Check("External meshes and runtime insertion are present between Nike and the portrait; anchored bottle is not carryable");

            report.phase = "small bottle pickup and blocked mouth";
            Vector3 station = course.Art.bottleStation.position;
            Vector3 near = new Vector3(station.x, 0, station.z - 2.4f);
            game.PlacePlayer(near); Look(BottleAim());
            Require(course.Sculpture.body.isKinematic && !AnyPieceColliderEnabled(), "Contained sculpture must not have ordinary grab collision.");
            report.containedPieceUnpickable = true;
            game.UpdateAim(); game.Interact();
            Require(course.Manipulating && game.held == null, "Camera pickup did not enter the anchored bottle interaction.");
            report.cameraPickup = true; report.smallBottleSize = course.BottleSize;
            game.RotateHeld(0, 35);
            for (int i = 0; i < 30; i++) { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(!course.Extracting && !course.Extracted && course.BottleTilt >= 27,
                "A tilted small bottle incorrectly released the sculpture.");
            report.smallMouthRejects = true;
            game.Cancel(true);
            Require(!course.Manipulating && Mathf.Abs(course.BottleSize - 1) < .02f && Mathf.Abs(course.BottleTilt) < .02f,
                "Cancel did not restore bottle size and angle.");
            report.cancelRestores = true;
            Check("Public camera pickup selects only the bottle; small mouth blocks extraction and cancel restores its state");

            report.phase = "actual backward walk grows anchored bottle";
            Look(BottleAim()); game.Interact(); Require(course.Manipulating, "Could not re-grab the bottle.");
            Vector3 initialFeet = game.player.feet, fixedStation = course.Art.bottleStation.position;
            int limit = ticks + 700;
            while (course.BottleSize < 2.02f && ticks < limit)
            {
                Look(BottleAim()); Step(Vector2.down);
                Require((course.Sculpture.transform.localScale - Vector3.one).sqrMagnitude < .000001f,
                    "Contained sculpture inherited bottle enlargement.");
                Require(Vector3.Distance(fixedStation, course.Art.bottleStation.position) < .001f,
                    "Fixed bottle station translated while manipulating.");
                if (ticks % 12 == 0) yield return null;
            }
            report.backwardWalkMetres = Vector3.Distance(initialFeet, game.player.feet);
            report.largeBottleSize = course.BottleSize;
            Require(report.backwardWalkMetres > 1 && course.BottleSize >= 1.95f && !course.Extracted,
                "Actual CharacterController backward walking did not enlarge the bottle.");
            report.actualBackwardWalk = report.anchoredPerspectiveGrowth = report.internalSizePreserved = true;
            float pausedSize = course.BottleSize, pausedTilt = course.BottleTilt;
            game.Pause(); game.Tick(.05f, Vector2.down); course.Tick(.05f);
            Require(Mathf.Abs(course.BottleSize - pausedSize) < .0001f && Mathf.Abs(course.BottleTilt - pausedTilt) < .0001f,
                "Pause advanced the bottle manipulation.");
            report.pausePreserved = true; game.Resume();
            Require(game.Drop() && !course.Manipulating, "Drop did not lock the anchored bottle size.");
            float locked = course.BottleSize; report.releaseLocksSize = true;
            limit = ticks + 700;
            while (game.player.feet.z < near.z - .08f && ticks < limit)
            {
                Look(BottleAim()); Step(Vector2.up);
                Require(Mathf.Abs(course.BottleSize - locked) < .001f, "Approaching a released bottle shrank it.");
                if (ticks % 12 == 0) yield return null;
            }
            Require(game.player.feet.z >= near.z - .15f, "Could not walk back toward the expanded bottle.");
            report.approachBottleSize = course.BottleSize; report.approachPreservesSize = true;
            Capture("02-expanded-bottle-and-cradle");
            Check("Actual backward walking grows only the anchored vessel; pause freezes it and drop preserves size during approach");

            report.phase = "large bottle tilt and independent sculpture release";
            Look(BottleAim()); game.Interact(); Require(course.Manipulating, "Could not re-grab expanded bottle for tilt.");
            game.RotateHeld(0, 36);
            limit = ticks + 400;
            while (!course.Extracted && ticks < limit)
            { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            Require(course.Extracted && !course.Extracting && !course.Manipulating && AnyPieceColliderEnabled() &&
                !course.Sculpture.body.isKinematic && contacts == 1 && releases == 1,
                "Wide tilted mouth did not release exactly one independent physics sculpture.");
            for (int i = 0; i < 60; i++) { Step(Vector2.zero); if (ticks % 12 == 0) yield return null; }
            report.extractionCompletes = true;
            Vector3 catchAt = course.Art.catchPad.position;
            game.PlacePlayer(new Vector3(catchAt.x, 0, catchAt.z - 2.1f)); Look(course.Sculpture.WorldBounds.center);
            Capture("03-extracted-cc0-sculpture");
            game.UpdateAim(); game.Interact();
            Require(game.held == course.Sculpture && !course.Manipulating, "Extracted sculpture does not use ordinary pickup.");
            report.ordinaryPiecePickup = true;
            game.Cancel(true);
            Check("Public rotation releases the unchanged sculpture through the mouth and enables ordinary independent pickup");

            report.phase = "disclosed balance fixtures: small and large loads";
            PlaceOnPan(GalleryBottleBalanceCourse.TargetSculptureScale, false);
            course.Sculpture.transform.position+=Vector3.up*.22f;course.Sculpture.body.position=course.Sculpture.transform.position;Physics.SyncTransforms();
            for(int i=0;i<30;i++)Step(Vector2.zero);
            Require(course.RelativeLoad<.01f&&!course.Solved&&course.StableSeconds<.001f,"An unsupported floating sculpture counted as a scale load.");
            report.floatingLoadRejected=true;
            PlaceOnPan(1f, false);
            for (int i = 0; i < 180; i++) { Step(Vector2.zero); if (ticks % 18 == 0) yield return null; }
            report.smallRelativeLoad = course.RelativeLoad;
            Require(course.RelativeLoad < .87f && !course.Solved && course.StableSeconds < .02f && course.ExitBarrier.enabled,
                "Underweight sculpture opened the exit or advanced dwell.");
            report.smallLoadRejected = true;
            PlaceOnPan(2.05f, false);
            for (int i = 0; i < 240; i++) { Step(Vector2.zero); if (ticks % 18 == 0) yield return null; }
            report.largeRelativeLoad = course.RelativeLoad;
            Require(course.RelativeLoad > 1.13f && !course.Solved && course.StableSeconds < .02f && course.ExitBarrier.enabled,
                "Overweight sculpture opened the exit or advanced dwell.");
            report.largeLoadRejected = true;
            Check("Disclosed size/pan fixtures reject underweight and overweight loads without opening the gate");

            report.phase = "short and interrupted balance dwell";
            PlaceOnPan(GalleryBottleBalanceCourse.TargetSculptureScale, false);
            limit = ticks + 500;
            while (course.StableSeconds < .65f && ticks < limit)
            { Step(Vector2.zero); if (ticks % 18 == 0) yield return null; }
            Require(course.StableSeconds >= .6f && course.StableSeconds < 2 && !course.Solved && course.ExitBarrier.enabled,
                "Balance completed before the required dwell.");
            report.shortBalanceRejected = true;
            MovePieceOffPan(); Step(Vector2.zero);
            Require(course.StableSeconds < .001f && !course.Solved, "Interrupted balance did not clear dwell progress.");
            report.interruptedBalanceReset = true;
            Check("A short level interval is insufficient and removing the load resets accumulated balance time");

            report.phase = "target balance fixture with production dynamic release";
            PlaceOnPan(GalleryBottleBalanceCourse.TargetSculptureScale, true);
            limit = ticks + 1000;
            while (!course.Solved && ticks < limit)
            { Step(Vector2.zero); if (ticks % 18 == 0) yield return null; }
            Require(course.Solved && course.StableSeconds >= 2 && completions == 1 && course.ExitBarrier.enabled,
                "Released target sculpture did not settle, complete two-second balance and unblock the passage.");
            report.stableSecondsAtSuccess = course.StableSeconds; report.finalRelativeLoad = course.RelativeLoad;
            report.balanceCompletesAfterDwell = true;
            for (int i = 0; i < 190; i++) { Step(Vector2.zero); if (ticks % 18 == 0) yield return null; }
            Require(completions == 1 && openings == 1 && course.ExitLeaf.position.y > 5.5f && !course.ExitBarrier.enabled,
                "Success hooks repeated or the visual gate failed to rise.");
            report.completionOnce = true;
            game.PlacePlayer(new Vector3(0, 0, 31)); Look(new Vector3(2.0f, 1.9f, 35.8f));
            Capture("04-balanced-exhibition");
            MovePieceOffPan();
            for (int i = 0; i < 30; i++) Step(Vector2.zero);
            Require(course.Solved && !course.ExitBarrier.enabled && completions == 1, "Moving the piece relocked completed progress.");
            report.successLatches = true;
            Check("Dynamically released target fixture settles, completes the real dwell, raises the gate once and latches success");

            report.phase = "actual CharacterController walk through opened route";
            game.PlacePlayer(new Vector3(0, 0, 39.3f)); Look(new Vector3(0, 1.55f, 45));
            limit = ticks + 400;
            while (game.player.feet.z < 41.4f && ticks < limit)
            { Step(Vector2.up); if (ticks % 12 == 0) yield return null; }
            Require(game.player.feet.z > 41.2f, "Actual CharacterController could not cross the opened exhibition gate.");
            report.passageWalk = true; Capture("05-route-to-portrait");
            Check("Actual CharacterController crosses the opened gate into the shifted portrait-room route");

            var shiftedPortrait = ShiftedPortraitIntegration(portrait);
            while (shiftedPortrait.MoveNext()) yield return shiftedPortrait.Current;
            var shiftedCathedral = ShiftedCathedralIntegration();
            while (shiftedCathedral.MoveNext()) yield return shiftedCathedral.Current;

            report.phase = "normal full reset";
            game.ResetAll();
            Require(!course.Extracted && !course.Extracting && !course.Solved && !course.Manipulating &&
                Mathf.Abs(course.BottleSize - 1) < .001f && Mathf.Abs(course.BottleTilt) < .001f &&
                course.ExitBarrier.enabled && !AnyPieceColliderEnabled() && course.Sculpture.body.isKinematic,
                "Normal game reset did not restore the contained sculpture, small bottle and closed route.");
            report.resetRestores = true; report.phase = "complete";
            Check("Normal ResetAll restores the complete new exhibition without rebuilding the saved scene");
        }

        IEnumerator NikeEntryIntegration()
        {
            report.phase = "original Nike solution fixtures and physical entrance to inserted room";
            game.StartConceptDemo(1);
            var light = game.nikeLight; var shadow = game.shadowCourse;
            Require(light != null && light.canvas != null && light.cover != null && shadow != null && shadow.nikeSocket != null,
                "Original Nike light, cover, canvas or socket is missing after insertion.");
            Pose(light.cover.Prop, GalleryNikeLightCover.RevealedPosition, Quaternion.identity, 1);
            light.SyncPose();
            Pose(light.statue, GalleryNikeCanvas.SolutionPosition, light.statue.homeRotation, GalleryNikeCanvas.SolutionScale);
            bool brush = false;
            for (int i = 0; i < 800 && !light.canvas.Complete; i++)
            {
                Step(Vector2.zero); brush |= light.canvas.Drawing;
                if (i % 12 == 0) yield return null;
            }
            Require(brush && light.canvas.Complete && light.canvas.portablePainting.gameObject.activeInHierarchy,
                "The original Nike shadow fixture no longer performs its brush/frame-ejection sequence.");
            var socket = shadow.nikeSocket;
            Pose(light.canvas.portablePainting, socket.Center - Vector3.forward * .30f, Quaternion.identity, GalleryNikeDoorSocket.TargetScale);
            Require(socket.FitHint(light.canvas.portablePainting) == null, "Original Nike painting no longer fits its door socket.");
            socket.BeginDock(light.canvas.portablePainting);
            for (int i = 0; i < 200 && !shadow.NikeGateOpen; i++)
            { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(shadow.NikeGateOpen && socket.Seated, "The original Nike door did not open before the new room.");
            report.nikeDoorFlowPreserved = true;
            game.PlacePlayer(new Vector3(0, 0, 27.6f)); Look(new Vector3(0, 1.55f, 32));
            for (int i = 0; i < 250 && game.player.feet.z < 30.15f; i++)
            { Step(Vector2.up); if (i % 12 == 0) yield return null; }
            Require(game.player.feet.z > 29.9f && shadow.NikeCleared && course.InRoom && course.ExitBarrier.enabled,
                "Actual controller could not pass the preserved Nike doorway into the unsolved inserted room.");
            report.nikeEntranceWalk = true; Capture("00-nike-to-added-room");
            Check("Original Nike brush, ejected painting and socket still open the door; actual walking crosses z29 into the new room");
        }

        IEnumerator ShiftedPortraitIntegration(GalleryConceptPerspective portrait)
        {
            report.phase = "shifted portrait solution and unchanged production performance";
            var gate = game.portraitGate;
            Require(gate != null && gate.transform.position.z > 55 && portrait.viewingPoint.position.z > 42,
                "Portrait curtain or observation point was not shifted with the inserted room.");
            foreach (GalleryProp piece in portrait.pieces) piece.Restore();
            for (int i = 0; i < portrait.pieces.Length; i++)
            {
                GalleryProp piece = portrait.pieces[i];
                Pose(piece, portrait.transform.TransformPoint(portrait.solutionPositions[i]), portrait.transform.rotation, portrait.solutionScales[i]);
                bool valid = game.ValidPlacement(piece, out string reason);
                Require(piece.placement.Ready && valid,
                    "Shifted portrait solution is unsupported or obstructed: " + piece.title + " / " + reason);
            }
            game.PlacePlayer(portrait.viewingPoint.position); Look(portrait.viewingPoint.position - Vector3.forward * 3 + Vector3.up * 1.55f);
            for (int i = 0; i < 35; i++) Step(Vector2.zero);
            Require(!gate.Performing, "Looking away from the shifted face started its performance.");
            Look(portrait.transform.TransformPoint(new Vector3(0, 1.9f, 39))); portrait.Evaluate();
            Look(portrait.FaceCenter); portrait.Evaluate();
            Require(portrait.Complete && portrait.CanObserve && game.held == null,
                "Moved world-space face solution is no longer recognizable from the moved viewing point.");
            report.shiftedPortraitSolution = true; Capture("06-shifted-portrait-solution");
            int absorptions = 0, reds = 0, smiles = 0;
            Action absorbed = () => absorptions++;
            Action red = () => { reds++; report.portraitRedElapsed = gate.PerformanceElapsed; };
            Action smile = () => { smiles++; report.portraitSmileElapsed = gate.PerformanceElapsed; };
            gate.AbsorptionStarted += absorbed; gate.ReddeningStarted += red; gate.SmileStarted += smile;
            var audio = MuseumAudio.Instance;
            AudioClip clip = audio != null ? audio.Clip("face.smile") : null;
            Require(clip != null && Mathf.Abs(clip.length - 9.5f) < .08f, "Existing 151 red-to-open clip is missing or retimed.");
            report.portraitClipSeconds = clip.length;
            int requests = audio.RequestCount;
            bool redShot = false, smileShot = false;
            try
            {
                for (int i = 0; i < 1600 && !gate.Opened; i++)
                {
                    Step(Vector2.zero);
                    if (!redShot && gate.MoodRed01 > .55f) { Capture("07-shifted-portrait-red"); redShot = true; }
                    if (!smileShot && gate.Smile01 > .55f) { Capture("08-shifted-portrait-smile"); smileShot = true; }
                    if (i % 12 == 0) yield return null;
                }
            }
            finally { gate.AbsorptionStarted -= absorbed; gate.ReddeningStarted -= red; gate.SmileStarted -= smile; }
            Require(gate.Opened && !gate.barrier.enabled && redShot && smileShot &&
                Mathf.Abs(gate.PerformanceElapsed - GalleryPortraitGate.TotalPerformanceSeconds) < .001f,
                "The moved portrait's complete production reveal failed.");
            report.shiftedPortraitPerformance = true;
            Require(absorptions == 1 && reds == 1 && smiles == 1 &&
                Mathf.Abs(report.portraitRedElapsed - 2.5f) < .045f && Mathf.Abs(report.portraitSmileElapsed - 4.5f) < .045f &&
                audio.RequestCount == requests + 1,
                "Moved portrait changed the 2.5s red/4.5s smile milestones or dispatched 151 more than once.");
            report.shiftedPortraitAudioMilestones = true;
            Vector3[] route =
            {
                new Vector3(3.3f, 0, 45), new Vector3(3.3f, 0, 54.1f),
                new Vector3(0, 0, 54.8f), new Vector3(0, 0, 57.1f)
            };
            foreach (Vector3 goal in route)
            {
                for (int i = 0; i < 500; i++)
                {
                    Vector3 delta = goal - game.player.feet; delta.y = 0;
                    if (delta.magnitude < .12f) break;
                    Step(new Vector2(delta.x, delta.z).normalized * (delta.magnitude < .35f ? .35f : 1));
                    Require(game.safe && game.exposure < .001f, "The shifted portrait walkway became sunlight-exposed.");
                    if (i % 12 == 0) yield return null;
                }
                Require(Vector3.Distance(new Vector3(game.player.feet.x, 0, game.player.feet.z), goal) < .20f,
                    "Actual controller could not traverse a shifted portrait route waypoint: " + goal);
            }
            Require(game.player.feet.z > 56.8f && game.checkpoint == 3,
                "Crossing the moved curtain did not reach the final room's checkpoint.");
            report.shiftedPortraitCrossing = true; Capture("09-shifted-portrait-to-cathedral");
            Check("Moved portrait world poses remain recognizable; original reveal/151 game milestones run once and actual walking reaches the moved final room");
        }

        IEnumerator ShiftedCathedralIntegration()
        {
            report.phase = "shifted cathedral opening geometry and rendered portal";
            GalleryOrcusExit entrance = game.orcusExit;
            Require(entrance != null && entrance.cathedralActivation != null && entrance.prop.homePosition.z > 64,
                "The original cathedral exhibit or its saved home was not shifted.");
            var activation = entrance.cathedralActivation; var prop = entrance.prop;
            activation.ResetToRest(); prop.Restore(); entrance.ResetEntrance();
            game.PlacePlayer(new Vector3(0, .006f, 57.4f));
            Pose(prop, GalleryBottleBalanceCourse.ShiftLegacy(new Vector3(0, .006f, 53)), Quaternion.identity, entrance.RequiredScale + .006f);
            bool valid = game.ValidPlacement(prop, out string reason);
            Require(prop.placement.Ready && valid && activation.CanOpenAtCurrentPlacement(),
                "Moved cathedral full-size placement is obstructed: " + reason);
            Look(entrance.MouthPosition);
            for (int i = 0; i < 400 && !entrance.IsReadyForEntry; i++)
            {
                Step(Vector2.zero); activation.Tick(Dt);
                if (i % 12 == 0) yield return null;
            }
            Require(activation.Opened && entrance.IsReadyForEntry, "The shifted cathedral hands did not open normally.");
            report.shiftedCathedralOpening = true; report.movedCathedralZ = prop.transform.position.z;
            Vector3 approach = prop.transform.TransformPoint(new Vector3(entrance.EntryCenterX + .04f, 0, entrance.approachStart));
            game.PlacePlayer(approach + Vector3.up * .006f);
            Require(game.CanStand(game.player.feet), "The shifted cathedral's original ground approach is obstructed.");
            Look(entrance.MouthPosition);
            Vector3 before = prop.transform.TransformPoint(new Vector3(entrance.EntryCenterX + .04f,
                entrance.EntryBottom + entrance.entryFootOffset, entrance.EntryPlane - .12f));
            Vector3 after = prop.transform.TransformPoint(new Vector3(entrance.EntryCenterX + .04f,
                entrance.EntryBottom + entrance.entryFootOffset, entrance.EntryPlane + .12f));
            Require(entrance.CanEnterFrom(before, after) && !entrance.CanEnterFrom(after, before),
                "Moved cathedral opening rejects valid front entry or accepts reverse entry.");
            var portal = prop.GetComponent<GalleryCathedralPortal>();
            Require(portal != null, "The original Library portal component is missing.");
            portal.RenderWindow();
            Require(portal.Ready && portal.PortalTexture != null &&
                Vector3.Distance(portal.LastSourceEye, game.view.transform.position) < .001f,
                "The moved Library window no longer renders from the actual visitor camera.");
            report.shiftedCathedralPortal = true; Capture("10-shifted-cathedral-portal");
            Check("Moved cathedral opens at its translated full-size pose, preserves clear entry geometry and renders the Library window from the real camera");
        }

        void Pose(GalleryProp prop, Vector3 position, Quaternion rotation, float scale)
        {
            prop.Restore();
            prop.transform.SetPositionAndRotation(position, rotation); prop.transform.localScale = Vector3.one * scale;
            prop.body.position = position; prop.body.rotation = rotation; prop.RefreshMass();
            Physics.SyncTransforms(); prop.placement.ResetPlaced();
        }

        Vector3 BottleAim()
        { return course.Art.bottlePivot.TransformPoint(new Vector3(-.19f * course.BottleSize, .28f * course.BottleSize, 0)); }
        bool AnyPieceColliderEnabled()
        { foreach (var part in course.Sculpture.parts) if (part.enabled) return true; return false; }
        void PlaceOnPan(float scale, bool release)
        {
            var prop = course.Sculpture;
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease(); HeldCollisionState.Attach(prop.body).Restore();
            if (!prop.body.isKinematic) { prop.body.linearVelocity = Vector3.zero; prop.body.angularVelocity = Vector3.zero; }
            prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.localScale = Vector3.one * scale; prop.transform.rotation = Quaternion.identity;
            prop.transform.position = course.Art.rightPan.position + Vector3.up;
            Physics.SyncTransforms(); Bounds b = prop.WorldBounds;
            prop.transform.position += course.Art.rightPan.position - new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * .008f;
            prop.body.position = prop.transform.position; prop.body.rotation = prop.transform.rotation;
            prop.RefreshMass(); Physics.SyncTransforms();
            if (release) prop.Release();
        }
        void MovePieceOffPan()
        {
            var prop = course.Sculpture;
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease();
            if (!prop.body.isKinematic) { prop.body.linearVelocity = Vector3.zero; prop.body.angularVelocity = Vector3.zero; }
            prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.position = new Vector3(0, 1, 33); prop.body.position = prop.transform.position;
            Physics.SyncTransforms();
        }
        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude); game.SyncView();
        }
        void Step(Vector2 move)
        {
            Guard(); game.Tick(Dt, move, false);
            if (game.active) Physics.Simulate(Dt);
            ticks++;
        }
        void Capture(string name)
        {
            var camera = game.view; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Destroy(target); if (texture != null) Destroy(texture); }
        }
        void Contact() { contacts++; }
        void Released() { releases++; }
        void Completed() { completions++; }
        void Opened() { openings++; }
        void Check(string message) { checks.Add(message); Debug.Log("BOTTLE_BALANCE_PASS " + message); }
        void Guard() { Require(Time.realtimeSinceStartupAsDouble <= deadline, "Validation exceeded 240 seconds."); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("BOTTLE_BALANCE: " + message); }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void RestoreSimulation()
        { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Unsubscribe()
        {
            if (subscribed) Application.logMessageReceived -= Log;
            if (course != null)
            { course.GlassContact -= Contact; course.SculptureReleased -= Released; course.BalanceCompleted -= Completed; course.PassageOpened -= Opened; }
            subscribed = false;
        }
        void OnDestroy() { RestoreSimulation(); Unsubscribe(); }
    }
}
