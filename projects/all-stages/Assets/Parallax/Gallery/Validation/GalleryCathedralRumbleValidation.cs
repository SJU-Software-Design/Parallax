using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Explicit sculpture/viewer fixtures isolate the new authored opening and
    // visual rumble. Full museum mechanics separately walks the original stairs
    // and performs the real Library scene handoff through production input.
    public sealed class GalleryCathedralRumbleValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        GalleryOrcusExit entrance;
        GalleryCathedralActivation activation;
        GalleryCathedralRumble rumble;
        GalleryCathedralPortal portal;
        GalleryProp prop;
        SimulationMode oldSimulation;
        bool ownsSimulation, oldActivationEnabled, oldRumbleEnabled, subscribed;
        string output;
        double deadline;
        int ticks;

        [Serializable] sealed class Report
        {
            public bool passed, timingRetained, widerOpening, synchronizedGeometry, cameraMoves, cameraDoesNotAccumulate;
            public bool playerControlsPreserved, portalFollowsCamera, attackObserved, sustainedRumble, aftershockObserved, aftershockSettles;
            public bool zoneResetRestored, restartRestored, pauseClears, regrabClears, physicsRestored;
            public bool earlyEntryBlocked, barrierContactStable, openedBarrierRemoved, barrierResetAndHoldClear;
            public float observedOpeningSeconds, observedTailSeconds, localOpeningWidth, priorLocalOpeningWidth = .56f, widthIncreasePercent;
            public float maximumRumble, maximumPositionOffset, maximumRotationOffset, startingFov, endingFov;
            public float attackEarly, attackLate, tailStart, tailMiddle, tailEnd;
            public int simulatedTicks, cameraSamples;
            public int earlyWalkTicks, blockedWalkTicks;
            public float closestClosedLocalZ, barrierContactDrift;
            public int earlyApproachCandidates;
            public Vector3 earlyApproachWorld, earlyApproachLocal;
            public string version, unity, gpu, phase, failure;
            public string scope = "Explicit full-size settled sculpture and player viewpoint fixtures. An upper-stair/landing player approach is searched over disclosed local Z/X candidates using a downward usable-ground ray and the real capsule CanStand query; the selected local/world coordinates are recorded. Actual CharacterController forward walking, production Game Tick and PhysX then contact the closed entry barrier while the sculpture is a dynamic sleeping Rigidbody; contact must preserve placement readiness until the actual opening removes the barrier. Production activation Tick, collision/portal interpolation and the display-only camera effect are used without private-state writes. A held-state fixture checks interruption; public current-zone/full resets and Pause check cleanup. The effect is sampled repeatedly on the actual player camera and portal RenderWindow records the same source eye. This focused QA does not claim autonomous carrying or full scene traversal: GalleryMuseumMechanicsValidation separately checks actual pickup/resize/Drop, original ground approach, CharacterController stair climbing, physical aperture crossing and the real Library handoff.";
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log; subscribed = true;
            deadline = Time.realtimeSinceStartupAsDouble + 90;
            var run = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Guard(); more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break;
                yield return next;
            }
            Cleanup();
            report.physicsRestored = Physics.simulationMode == oldSimulation;
            report.version = GalleryGame.Version; report.unity = Application.unityVersion; report.gpu = SystemInfo.graphicsDeviceName;
            report.simulatedTicks = ticks; report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.timingRetained && report.widerOpening &&
                report.synchronizedGeometry && report.cameraMoves && report.cameraDoesNotAccumulate && report.playerControlsPreserved &&
                report.portalFollowsCamera && report.attackObserved && report.sustainedRumble && report.aftershockObserved &&
                report.aftershockSettles && report.zoneResetRestored && report.restartRestored && report.pauseClears && report.regrabClears && report.physicsRestored &&
                report.earlyEntryBlocked && report.barrierContactStable && report.openedBarrierRemoved && report.barrierResetAndHoldClear;
            if (output != null) File.WriteAllText(Path.Combine(output, "cathedral-rumble-results.json"), JsonUtility.ToJson(report, true));
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_CATHEDRAL_RUMBLE_OK " + checks.Count + " opening/envelope/camera/reset checks");
            else Debug.LogError("GALLERY_CATHEDRAL_RUMBLE_FAIL " + report.failure + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(Application.persistentDataPath, "GalleryCathedralRumbleValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.conceptLayout, "The actual automated museum is required");
            game.conceptCapture = true; game.ResetAll(); game.Resume(); game.checkpoint = 3;
            entrance = game.orcusExit; prop = entrance.prop; activation = entrance.cathedralActivation;
            rumble = game.GetComponent<GalleryCathedralRumble>(); portal = activation.portal;
            Require(activation != null && rumble != null && portal != null && rumble.activation == activation,
                "The live Cathedral activation, camera rumble and portal must be connected");
            oldActivationEnabled = activation.enabled; oldRumbleEnabled = rumble.enabled;
            activation.enabled = false; rumble.enabled = false; // Only disable automatic callbacks; public production methods below are unchanged.
            oldSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            SetViewer();
            for (int i = 0; i < 180 && !portal.Ready; i++) yield return null;
            Require(portal.Ready, "The actual live Library preview did not initialize");
            Require(Mathf.Abs(activation.duration - 2.2f) < .001f, "The approved hand-opening duration is not 2.2 seconds");
            var earlyEntry = CheckEarlyEntryBarrier();
            while (earlyEntry.MoveNext()) yield return earlyEntry.Current;

            report.phase = "production opening duration, interpolation and rumble attack/sustain";
            Fixture(); Capture("cathedral-rumble-01-rest");
            float openingTime = 0; bool attackShot = false, middleShot = false;
            for (int i = 0; i < 400 && !activation.Opened; i++)
            {
                Step(); openingTime += Dt;
                CheckGeometry(false); CheckCamera();
                if (openingTime <= .10f) report.attackEarly = Mathf.Max(report.attackEarly, activation.Rumble01);
                if (openingTime >= .26f && openingTime <= .40f) report.attackLate = Mathf.Max(report.attackLate, activation.Rumble01);
                if (openingTime >= .45f && !activation.Opened)
                    Require(activation.Rumble01 > .94f, "Opening rumble faded before the moving stone stopped");
                report.maximumRumble = Mathf.Max(report.maximumRumble, activation.Rumble01);
                if (!attackShot && openingTime >= .28f) { Capture("cathedral-rumble-02-attack"); attackShot = true; }
                if (!middleShot && activation.Progress >= .52f) { Capture("cathedral-rumble-03-opening"); middleShot = true; }
                if (i % 12 == 0) yield return null;
            }
            report.observedOpeningSeconds = openingTime;
            Require(activation.Opened && entrance.IsReadyForEntry && Mathf.Abs(openingTime - 2.2f) < .035f,
                "The actual settled hand opening did not finish in the approved 2.2 seconds");
            report.timingRetained = true;
            report.attackObserved = report.attackEarly > 0 && report.attackEarly < .7f && report.attackLate > .99f;
            Require(report.attackObserved, "The rumble did not grow quickly during the initial quarter-second");
            report.sustainedRumble = report.maximumRumble > .99f;
            CheckGeometry(true); Capture("cathedral-rumble-04-open-aftershock");
            report.tailStart = activation.Rumble01;
            Require(report.tailStart > .94f && activation.AftershockRemaining > .70f,
                "The hand motion stopped without the approved aftershock");
            float tailTime = 0;
            while (activation.Rumble01 > .00001f && tailTime < 1.2f)
            {
                Step(); tailTime += Dt; CheckCamera();
                Require(activation.Opened && entrance.IsReadyForEntry, "Aftershock moved or closed the completed hands");
                if (tailTime >= .38f && tailTime <= .42f) report.tailMiddle = activation.Rumble01;
                if (ticks % 12 == 0) yield return null;
            }
            report.observedTailSeconds = tailTime; report.tailEnd = activation.Rumble01;
            report.aftershockObserved = report.tailMiddle > .35f && report.tailMiddle < .65f;
            report.aftershockSettles = Mathf.Abs(tailTime - .8f) < .05f && report.tailEnd < .00001f;
            Require(report.aftershockObserved && report.aftershockSettles, "The post-stop vibration did not decay smoothly over 0.8 seconds");
            game.SyncView(); rumble.ApplySample();
            Require(rumble.AppliedIntensity < .00001f, "Camera vibration remained after the aftershock finished");
            Capture("cathedral-rumble-05-settled");
            Check("The hands open over 2.2 seconds; rumble grows during 0.25 seconds, persists through motion and decays for 0.8 seconds after stopping");

            report.phase = "public current-zone reset and full restart";
            Fixture(); AdvanceTo(.8f); CheckCamera();
            game.ResetZone(); game.Resume(); RequireRest("Current-zone reset"); report.zoneResetRestored = true;
            Fixture(); AdvanceTo(.8f); CheckCamera();
            game.ResetAll(); game.Resume(); RequireRest("Full restart"); report.restartRestored = true;
            Check("R and full restart restore the original hands, collider centres, portal aperture and unshaken camera");

            report.phase = "pause and carried-state interruptions";
            Fixture(); AdvanceTo(.8f); CheckCamera();
            game.Pause(); rumble.ApplySample();
            Require(activation.Rumble01 < .00001f && rumble.AppliedIntensity < .00001f,
                "Pausing left the camera vibrating");
            report.pauseClears = true; game.Resume();
            Fixture(); AdvanceTo(.8f); CheckCamera();
            game.held = prop; prop.placement.BeginHold(); activation.Tick(Dt); rumble.ApplySample();
            Require(activation.Rumble01 < .00001f && rumble.AppliedIntensity < .00001f,
                "Regrabbing a moving hand sculpture left rumble active");
            Require(!activation.entryBarrier.enabled, "The temporary entry barrier remained active while the sculpture was held");
            report.regrabClears = true; game.held = null;
            game.ResetAll(); game.Resume(); RequireRest("Final reset");
            report.barrierResetAndHoldClear = true;
            Check("Pause and a carried-state interruption clear the camera offset and envelope immediately");
        }

        IEnumerator CheckEarlyEntryBarrier()
        {
            report.phase = "actual early controller walk waits for the slow hand opening";
            Fixture();
            var barrier = activation.entryBarrier;
            Require(barrier != null && !barrier.enabled && !barrier.isTrigger && Array.IndexOf(prop.parts, barrier) < 0,
                "The separate physical entry barrier must begin disabled and stay outside the exhibit's authored placement parts");
            prop.body.isKinematic = false; prop.body.useGravity = true;
            prop.body.linearVelocity = prop.body.angularVelocity = Vector3.zero; prop.body.Sleep();
            Vector3 originalPosition = prop.transform.position;
            // Explicit upper-landing approach: the full mechanics test separately
            // climbs from the ground. Here contact must begin before 2.2 seconds.
            Vector3 approach = FindEarlyApproach();
            game.PlacePlayer(approach); game.yaw = Mathf.Atan2(prop.transform.forward.x, prop.transform.forward.z); game.pitch = 0; game.SyncView();
            Require(game.CanStand(game.player.feet), "The upper-landing early-approach fixture is obstructed");
            activation.Tick(Dt); Physics.SyncTransforms();
            Require(barrier.enabled && !activation.Opened, "The eligible closed hand entrance has no physical wait barrier");
            report.closestClosedLocalZ = float.NegativeInfinity;
            for (int i = 0; i < 140; i++)
            {
                Vector3 before = game.player.feet;
                Vector3 direction = prop.transform.forward;
                game.Tick(Dt, new Vector2(direction.x, direction.z), false); Physics.Simulate(Dt); Physics.SyncTransforms(); activation.Tick(Dt); ticks++;
                float localZ = prop.transform.InverseTransformPoint(game.player.feet).z;
                report.closestClosedLocalZ = Mathf.Max(report.closestClosedLocalZ, localZ); report.earlyWalkTicks++;
                if (Vector3.Distance(before, game.player.feet) < .003f) report.blockedWalkTicks++;
                Require(!activation.Opened && barrier.enabled && localZ < entrance.EntryPlane - .02f,
                    "The actual controller crossed the portal plane before the slower hand opening completed");
                Require(prop.placement.Ready && !activation.OpeningBlocked,
                    "Contact with the entry barrier destabilized the placed sculpture or made its barrier flicker");
                if (i % 12 == 0) yield return null;
            }
            report.barrierContactDrift = Vector3.Distance(originalPosition, prop.transform.position);
            Require(report.blockedWalkTicks > 30 && report.barrierContactDrift < .025f,
                "The controller did not remain physically blocked at a stable sculpture while the hands opened");
            report.earlyEntryBlocked = report.barrierContactStable = true;
            Capture("cathedral-rumble-00-early-entry-blocked");
            for (int i = 0; i < 200 && !activation.Opened; i++)
            {
                game.Tick(Dt, Vector2.zero, false); Physics.Simulate(Dt); Physics.SyncTransforms(); activation.Tick(Dt); ticks++;
                Require(prop.placement.Ready, "A resting contact at the entry barrier invalidated the installed sculpture");
                if (i % 12 == 0) yield return null;
            }
            Require(activation.Opened && !barrier.enabled, "The entry barrier did not disappear on the fully-open frame");
            Vector3 planeFeet = prop.transform.TransformPoint(new Vector3(entrance.EntryCenterX, entrance.EntryBottom + .012f, entrance.EntryPlane));
            Require(game.CanStand(planeFeet), "The fully opened plane is still physically obstructed by the temporary barrier");
            report.openedBarrierRemoved = true;
            Check("Actual forward walking is held before the portal during the 2.2-second motion without destabilizing the dynamic sculpture; the open frame removes the barrier");
        }

        Vector3 FindEarlyApproach()
        {
            // The captured rest palms protrude in front of the portal and can
            // overlap a mathematically centred capsule on the rear landing.
            // Find a real clear upper tread instead of bypassing collision.
            foreach (float z in new[] { -.95f, -.90f, -.85f, -.80f, -.75f, -.70f })
            foreach (float x in new[] { 0f, -.03f, .03f, -.06f, .06f })
            {
                report.earlyApproachCandidates++;
                Vector3 nominal = prop.transform.TransformPoint(new Vector3(entrance.EntryCenterX + x, entrance.EntryBottom + .02f, z));
                if (!Physics.Raycast(nominal + Vector3.up * .25f, Vector3.down, out var ground, .60f, ~0, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < .8f || !PlacementSupport.IsUsableGround(ground.collider)) continue;
                Vector3 candidate = ground.point + Vector3.up * .025f;
                if (!game.CanStand(candidate)) continue;
                report.earlyApproachWorld = candidate; report.earlyApproachLocal = prop.transform.InverseTransformPoint(candidate);
                Debug.Log("CATHEDRAL_RUMBLE_EARLY_APPROACH world=" + candidate.ToString("F4") +
                    " local=" + report.earlyApproachLocal.ToString("F4") + " candidates=" + report.earlyApproachCandidates);
                return candidate;
            }
            throw new InvalidOperationException("CATHEDRAL_RUMBLE: No physically clear supported upper-stair approach was available");
        }

        void Fixture()
        {
            game.held = null; activation.ResetToRest(); prop.Restore(); entrance.ResetEntrance();
            game.checkpoint = 3; game.active = true; Time.timeScale = 1; SetViewer();
            prop.InitializePhysics(); prop.body.isKinematic = true; prop.body.useGravity = false;
            prop.transform.SetPositionAndRotation(new Vector3(0, .006f, 53), Quaternion.identity);
            prop.transform.localScale = Vector3.one * (entrance.RequiredScale + .006f);
            prop.body.position = prop.transform.position; prop.body.rotation = prop.transform.rotation;
            prop.RefreshMass(); Physics.SyncTransforms(); prop.placement.ResetPlaced();
            string reason = prop.placement.Ready ? null : prop.placement.Status;
            Require(prop.placement.Ready && game.ValidPlacement(prop, out reason) && activation.CanOpenAtCurrentPlacement(),
                "The explicit full-size settled sculpture fixture is not valid: " + reason);
            Require(activation.Progress < .00001f && activation.Rumble01 < .00001f, "A new fixture retained animation state");
        }
        void SetViewer()
        {
            game.PlacePlayer(new Vector3(0, .006f, 45.4f));
            Vector3 direction = new Vector3(0, 2.85f, 53) - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z); game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            game.SyncView();
        }
        void Step()
        {
            Guard(); activation.Tick(Dt); game.SyncView(); rumble.ApplySample(); ticks++;
        }
        void AdvanceTo(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds / Dt); i++) Step(); }
        void CheckGeometry(bool fullyOpen)
        {
            float blend = Mathf.SmoothStep(0, 1, activation.Progress);
            foreach (var renderer in activation.morphTargets)
                Require(Mathf.Abs(renderer.GetBlendShapeWeight(0) - blend * activation.openedMorphWeight) < .01f,
                    "The scanned hand or portal mesh disagrees with the actual opening progress");
            for (int i = 0; i < activation.movingParts.Length; i++)
                Require(Vector3.Distance(activation.movingParts[i].center, Vector3.Lerp(activation.closedCenters[i], activation.openedCenters[i], blend)) < .00001f,
                    "A physical hand collider disagrees with the visible opening progress");
            Require(Vector3.Distance(portal.apertureBounds.size, Vector3.Lerp(activation.closedOpening.size, activation.openedOpening.size, blend)) < .00001f,
                "The Library projection bounds disagree with the moving aperture");
            if (!fullyOpen) return;
            report.localOpeningWidth = portal.apertureBounds.size.x;
            report.widthIncreasePercent = (report.localOpeningWidth / report.priorLocalOpeningWidth - 1) * 100;
            report.widerOpening = Mathf.Abs(report.localOpeningWidth - .68f) < .001f && report.widthIncreasePercent >= 20 && report.widthIncreasePercent <= 25;
            Require(report.widerOpening && Mathf.Abs(activation.openedMorphWeight - 225) < .01f,
                "The approved wider hand opening does not use the authored 225-percent morph and 0.68m aperture");
            Require(Mathf.Abs(entrance.EntryHalfWidth - portal.apertureBounds.extents.x) < .0001f &&
                Mathf.Abs(entrance.EntryCenterX - portal.apertureBounds.center.x) < .0001f,
                "The transition window is narrower or offset from the actual opened portal");
            report.synchronizedGeometry = true;
        }
        void CheckCamera()
        {
            Vector3 feet = game.controller.transform.position; float yaw = game.yaw, pitch = game.pitch, fov = game.view.fieldOfView;
            if (report.cameraSamples == 0) report.startingFov = fov;
            game.SyncView(); Vector3 neutral = game.view.transform.position; Quaternion neutralRotation = game.view.transform.rotation;
            rumble.ApplySample(); Vector3 sampled = game.view.transform.position; Quaternion sampledRotation = game.view.transform.rotation;
            float offset = Vector3.Distance(neutral, sampled), angle = Quaternion.Angle(neutralRotation, sampledRotation);
            report.maximumPositionOffset = Mathf.Max(report.maximumPositionOffset, offset);
            report.maximumRotationOffset = Mathf.Max(report.maximumRotationOffset, angle);
            Require(offset <= .0181f && angle <= .281f, "The rumble exceeds its bounded display-only camera range");
            if (offset > .0001f && angle > .001f) report.cameraMoves = true;
            rumble.ApplySample();
            Require(Vector3.Distance(sampled, game.view.transform.position) < .00001f && Quaternion.Angle(sampledRotation, game.view.transform.rotation) < .001f,
                "Repeated camera application accumulates vibration offsets");
            report.cameraDoesNotAccumulate = true;
            Require(Vector3.Distance(feet, game.controller.transform.position) < .000001f && game.yaw == yaw && game.pitch == pitch && game.view.fieldOfView == fov,
                "The display effect altered player motion, aim input or FOV");
            report.playerControlsPreserved = true; report.endingFov = game.view.fieldOfView;
            if (report.cameraSamples % 15 == 0)
            {
                portal.RenderWindow();
                Require(Vector3.Distance(portal.LastSourceEye, sampled) < .00001f, "The Library window rendered from a different eye than the shaken player camera");
                report.portalFollowsCamera = true;
            }
            rumble.ClearSample();
            Require(Vector3.Distance(neutral, game.view.transform.position) < .00001f && Quaternion.Angle(neutralRotation, game.view.transform.rotation) < .001f,
                "Removing the owned camera sample did not restore the neutral pose");
            rumble.ClearSample(); report.cameraSamples++;
        }
        void RequireRest(string context)
        {
            Require(activation.Progress < .00001f && !activation.Opened && activation.Rumble01 < .00001f && activation.AftershockRemaining < .00001f,
                context + ": the opening or vibration state survived reset");
            Require(activation.entryBarrier != null && !activation.entryBarrier.enabled, context + ": the temporary entry barrier survived reset");
            CheckGeometry(false); rumble.ApplySample();
            Require(rumble.AppliedIntensity < .00001f && Vector3.Distance(prop.transform.position, prop.homePosition) < .001f &&
                Vector3.Distance(prop.transform.localScale, prop.homeScale) < .001f, context + ": original sculpture or camera was not restored");
        }
        void Capture(string name)
        {
            var camera = game.view; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                game.SyncView(); rumble.ApplySample(); portal.RenderWindow(); game.lighting.Synchronize(); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target; texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; rumble.ClearSample(); target.Release(); Destroy(target); if (texture != null) Destroy(texture); }
        }
        void Cleanup()
        {
            if (rumble != null) { rumble.ClearSample(); rumble.enabled = oldRumbleEnabled; }
            if (activation != null) { activation.ClearRumble(); activation.enabled = oldActivationEnabled; }
            if (ownsSimulation) { Physics.simulationMode = oldSimulation; ownsSimulation = false; }
        }
        void Guard() { Require(Time.realtimeSinceStartupAsDouble <= deadline && ticks < 5000, "Focused Cathedral QA exceeded its time/tick budget"); }
        void Check(string value) { checks.Add(value); Debug.Log("CATHEDRAL_RUMBLE_PASS " + value); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("CATHEDRAL_RUMBLE: " + message); }
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace); }
        void Unsubscribe() { if (subscribed) { Application.logMessageReceived -= Log; subscribed = false; } }
        void OnDestroy() { Cleanup(); Unsubscribe(); }
    }
}
