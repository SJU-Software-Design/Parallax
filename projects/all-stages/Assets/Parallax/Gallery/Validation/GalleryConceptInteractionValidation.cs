using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // A movement/pickup/cancel smoke test of the actual concept-museum player.
    // The pickup fixture moves only the player. Prop poses/scales are never set.
    public sealed class GalleryConceptInteractionValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        const int MaximumTicks = 2400;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly Report report = new Report();
        SimulationMode previousSimulation;
        double deadline;
        int ticks;

        [Serializable] sealed class Report
        {
            public bool passed, movementPassed, pickupPassed, cancelPassed;
            public string scope = "Four-gallery walk through three actual partition doorways, with a physical route beside the preassembled portrait sculpture in gallery 3; four spawn capsule checks and an original zone-0 replica camera pickup/rotate/cancel fixture. CharacterController and public shared interaction methods are used. Only the player is relocated for the labelled pickup fixture; prop transforms/scales are not written except original Restore() cleanup. This is design-preview physical QA, not puzzle completion.";
            public string failure, unity, prop;
            public int ticks, doorwaysCrossed, finalCheckpoint, respawnsBefore, respawnsAfter;
            public float simulatedSeconds, minimumWalkFeetY, maximumWalkFeetY;
            public float pickupReferenceScale, heldScaleAfterAdvance, requestedRotationDegrees, appliedRotationDegrees;
            public Vector3 finalWalkFeet, pickupOriginalPosition, pickupOriginalScale;
            public float cancelPositionError, cancelScaleError, cancelRotationError;
            public bool[] spawnClear;
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            string output = null, failure = null;
            previousSimulation = Physics.simulationMode;
            var sequence = Sequence();
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-validationDir");
                output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                    Path.Combine(Application.persistentDataPath, "GalleryConceptInteractionValidation");
                Directory.CreateDirectory(output);
            }
            catch (Exception exception) { failure = exception.ToString(); }
            if (failure == null)
            {
                deadline = Time.realtimeSinceStartupAsDouble + 15;
                Physics.simulationMode = SimulationMode.Script;
                while (true)
                {
                    object next = null;
                    bool more;
                    try { Guard(); more = sequence.MoveNext(); if (more) next = sequence.Current; }
                    catch (Exception exception) { failure = exception.ToString(); break; }
                    if (!more) break;
                    yield return next;
                }
            }
            try
            {
                if (game != null)
                {
                    if (game.held != null) game.Cancel(true);
                    if (game.props != null) foreach (var prop in game.props) if (prop != null) prop.Restore();
                    Physics.SyncTransforms();
                }
            }
            catch (Exception exception) { if (failure == null) failure = "Cleanup: " + exception; }
            Physics.simulationMode = previousSimulation;
            report.failure = failure;
            report.unity = Application.unityVersion;
            report.ticks = ticks;
            report.simulatedSeconds = ticks * Dt;
            report.checks = checks.ToArray();
            report.errors = errors.ToArray();
            report.passed = failure == null && errors.Count == 0 && report.movementPassed && report.pickupPassed && report.cancelPassed;
            try
            {
                if (output != null) File.WriteAllText(Path.Combine(output, "concept-interaction-results.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception) { report.passed = false; failure = "Cannot write validation report: " + exception; }
            Application.logMessageReceived -= Log;
            if (report.passed) Debug.Log("CONCEPT_INTERACTION_OK " + ticks + " ticks; walk/pickup/cancel passed");
            else Debug.LogError("CONCEPT_INTERACTION_FAIL " + failure + "; errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        void Guard()
        {
            Require(Time.realtimeSinceStartupAsDouble <= deadline, "Validation exceeded its 15-second wall-clock budget");
            Require(ticks < MaximumTicks, "Validation exceeded 2400 simulated motor ticks");
        }

        void Step(Vector2 movement)
        {
            Guard();
            game.Tick(Dt, movement, false);
            Physics.Simulate(Dt);
            Physics.SyncTransforms();
            game.lighting.Synchronize();
            ticks++;
        }

        void Look(Vector3 target)
        {
            Vector3 delta = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(delta.x, delta.z);
            game.pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude);
            game.SyncView();
        }

        IEnumerator Sequence()
        {
            Require(game != null && game.controller != null && game.view != null, "Actual GalleryGame/controller/camera are required");
            Require(game.automated && game.conceptLayout, "The concept-interaction hook must enable automated concept layout");
            yield return null;
            game.Resume();
            game.PlacePlayer(new Vector3(0, 0, 3));
            game.yaw = 0; game.pitch = 0; game.SyncView();
            report.respawnsBefore = game.respawns;
            for (int i = 0; i < 120; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(game.player.grounded && Mathf.Abs(game.player.feet.y) < .2f, "Entrance does not settle on the actual floor");

            float[] doors = { 14, 29, 44 };
            report.minimumWalkFeetY = report.maximumWalkFeetY = game.player.feet.y;
            Vector2[] waypoints = { new Vector2(0, 30.5f), new Vector2(3.3f, 33), new Vector2(3.3f, 42), new Vector2(0, 43), new Vector2(0, 46) };
            foreach (Vector2 target in waypoints)
            {
                while (true)
                {
                    Vector2 delta = target - new Vector2(game.player.feet.x, game.player.feet.z);
                    if (delta.magnitude < .065f) break;
                    // Tick accepts world input; fixed yaw 0 keeps this identical
                    // to ordinary forward/strafe controls. Brake before turns.
                    float approach = Mathf.Sqrt(Mathf.Clamp01(delta.magnitude / 1.2f));
                    Step(delta.normalized * approach);
                    Vector3 feet = game.player.feet;
                    report.finalWalkFeet = feet;
                    report.finalCheckpoint = game.checkpoint;
                    Require(!float.IsNaN(feet.y) && !float.IsInfinity(feet.y), "Motor produced an invalid feet position");
                    report.minimumWalkFeetY = Mathf.Min(report.minimumWalkFeetY, feet.y);
                    report.maximumWalkFeetY = Mathf.Max(report.maximumWalkFeetY, feet.y);
                    Require(Mathf.Abs(feet.x) < 3.7f && Mathf.Abs(feet.y) < .2f, "Museum walking route leaves its clear ground corridor at " + feet);
                    Require(game.respawns == report.respawnsBefore, "Museum walking route respawned");
                    while (report.doorwaysCrossed < doors.Length && feet.z > doors[report.doorwaysCrossed] + .4f)
                        report.doorwaysCrossed++;
                    if (ticks % 12 == 0) yield return null;
                }
            }
            for (int i = 0; i < 36; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            report.finalWalkFeet = game.player.feet;
            report.finalCheckpoint = game.checkpoint;
            report.respawnsAfter = game.respawns;
            Require(report.doorwaysCrossed == 3 && game.checkpoint == 3 && game.player.grounded && Mathf.Abs(game.player.feet.y) < .2f,
                "Walking through all four galleries did not reach grounded checkpoint 3");
            Require(game.respawns == report.respawnsBefore, "Walk or braking caused a respawn");
            report.spawnClear = new bool[game.SpawnPoints.Length];
            Require(report.spawnClear.Length == 4, "Concept museum must expose four spawn points");
            for (int i = 0; i < report.spawnClear.Length; i++)
            {
                report.spawnClear[i] = game.CanStand(game.SpawnPoints[i]);
                Require(report.spawnClear[i], "Spawn point " + i + " intersects museum geometry");
            }
            report.movementPassed = true;
            Check("Actual CharacterController crosses three partition doorways and walks beside the preassembled gallery-3 portrait, reaches checkpoint 3 with no respawn, and all four spawn capsules are clear");

            GalleryProp replica = Array.Find(game.props, prop => prop != null && prop.zone == 0);
            Require(replica != null && replica.parts != null && replica.parts.Length > 0, "An original zone-0 replica is required");
            report.prop = replica.title;
            report.pickupOriginalPosition = replica.transform.position;
            report.pickupOriginalScale = replica.transform.localScale;
            Quaternion originalRotation = replica.transform.rotation;
            bool originalKinematic = replica.body.isKinematic, originalGravity = replica.body.useGravity;
            Vector3 fixtureFeet = replica.transform.position + new Vector3(0, 0, -2.8f);
            Require(game.CanStand(fixtureFeet), "The labelled player-only pickup fixture is obstructed");
            game.PlacePlayer(fixtureFeet);
            for (int i = 0; i < 24; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            foreach (var part in replica.parts)
            {
                Look(part.bounds.center);
                game.UpdateAim();
                if (game.aimed != replica) continue;
                game.Interact();
                if (game.held == replica) break;
            }
            Require(game.held == replica, "Actual camera ray cannot select and grab the original replica");
            Require(replica.body.isKinematic, "Pickup does not retain shared held-body ownership");
            foreach (var part in replica.parts) Require(part.enabled, "Held collision protection was disabled");
            report.pickupReferenceScale = game.referenceScale;
            Require(Mathf.Abs(game.referenceScale - report.pickupOriginalScale.x) < .0001f, "Pickup lost the object's actual pre-grab scale");
            Quaternion requestedBefore = game.heldRotation, appliedBefore = replica.transform.rotation;
            game.RotateHeld(12, 0);
            report.requestedRotationDegrees = Quaternion.Angle(requestedBefore, game.heldRotation);
            Require(Mathf.Abs(report.requestedRotationDegrees - 12) < .02f, "Public held rotation did not accept a 12-degree request");
            game.AdvanceHeld(.1f);
            Physics.SyncTransforms();
            report.appliedRotationDegrees = Quaternion.Angle(appliedBefore, replica.transform.rotation);
            report.heldScaleAfterAdvance = replica.transform.localScale.x;
            Require(((IObjectInteractionHost<GalleryProp>)game).ValidatePreview(out var reason), "Public held advance produces an unsafe preview: " + reason);
            report.pickupPassed = true;
            Check("Real camera Interact grabs the original replica at its actual scale; RotateHeld and AdvanceHeld retain a collision-safe preview");

            game.Cancel(true);
            Require(game.held == null, "Forced public cancellation did not clear held state");
            report.cancelPositionError = Vector3.Distance(replica.transform.position, report.pickupOriginalPosition);
            report.cancelScaleError = Vector3.Distance(replica.transform.localScale, report.pickupOriginalScale);
            report.cancelRotationError = Quaternion.Angle(replica.transform.rotation, originalRotation);
            Require(report.cancelPositionError < .0001f && report.cancelScaleError < .0001f && report.cancelRotationError < .02f,
                "Cancellation does not exactly restore the actual pickup pose and scale");
            Require(replica.body.isKinematic == originalKinematic && replica.body.useGravity == originalGravity,
                "Cancellation does not restore original rigidbody ownership");
            report.cancelPassed = true;
            Check("Cancel(true) restores the actual pickup position, rotation, scale and body state; all props are restored during cleanup");
        }

        void Log(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CONCEPT_INTERACTION: " + message); }
        void Check(string message)
        { checks.Add(message); Debug.Log("CONCEPT_INTERACTION_PASS " + message); }
    }
}
