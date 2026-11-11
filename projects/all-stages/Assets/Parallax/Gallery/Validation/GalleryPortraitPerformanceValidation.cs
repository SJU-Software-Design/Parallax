using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Published solution poses and player approach fixtures are explicit. All
    // animation, movement, exposure, reset and opening behavior is production code.
    public sealed class GalleryPortraitPerformanceValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        const int MaximumTicks = 12000;
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly Dictionary<Mesh, Vector3[]> originalMeshes = new Dictionary<Mesh, Vector3[]>();
        readonly Dictionary<MeshFilter, Mesh> originalMeshReferences = new Dictionary<MeshFilter, Mesh>();
        readonly Dictionary<Renderer, bool> originalVisibility = new Dictionary<Renderer, bool>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        GalleryConceptPerspective portrait;
        GalleryPortraitGate gate;
        string output;
        double deadline;
        int ticks, startingRespawns, absorptionSignals, smileSignals, resetSignals;
        float absorptionSignalElapsed, smileSignalElapsed;
        GalleryPortraitGate.RevealStage absorptionSignalStage;
        SimulationMode previousSimulation;
        bool ownsSimulation, subscribed, gateSubscribed;

        [Serializable] sealed class Report
        {
            public bool passed, physicsRestored, initialWalkSafe, closedBarrierSolid;
            public bool solutionRecognized, absorptionObserved, absorbedPropsUnselectable;
            public bool intermediateRoomReset, redLightingObserved, smileObserved, openingObserved;
            public bool noDirectFloorLight, noExposureDuringWalk, curtainTraversed, resetAllRestored;
            public bool originalMeshesPreserved, originalMaterialsPreserved;
            public bool audioSignalsTimed, audioSignalsReset, audioSignalLongFrame;
            public int simulatedTicks, floorSamples, actualWalkTicks, respawns, finalCheckpoint;
            public int absorptionCueEvents, smileCueEvents, resetCueEvents;
            public float maximumExposure, maximumMood, maximumSmile, maximumOpening;
            public float smileCueElapsed;
            public float observedHoldSeconds, observedAbsorbSeconds, observedRedSeconds, observedSmileSeconds, observedOpenSeconds;
            public int absorbWalkTicks, redWalkTicks, smileWalkTicks, openingWalkTicks;
            public string phase, failure, version, unity, gpu;
            public string scope = "Explicit published portrait solution fixtures and disclosed player approach positions. Production hold/absorption/red-light/smile/curtain animation and public ResetZone/ResetAll execute normally. Walking uses the actual CharacterController, game Tick, exposure and PhysX; no player teleport occurs during a crossing. A floor grid checks the real direct/reflected sunlight queries in warm and red phases; the shader no-sun global and rendered screenshots are recorded, not a claim of automated GPU/CPU pixel-parity validation. Visual absorption and smile milestone events are checked at the original 4.5-second visual smile boundary, reset/replay, long-frame crossing and one-shot notification; this does not claim direct audible mixing verification. Separate full museum mechanics validation covers input-only portrait assembly and the Library handoff.";
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log; subscribed = true;
            previousSimulation = Physics.simulationMode;
            deadline = Time.realtimeSinceStartupAsDouble + 120;
            report.version = GalleryGame.Version; report.unity = Application.unityVersion;
            report.gpu = SystemInfo.graphicsDeviceName;
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
            report.simulatedTicks = ticks; report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.absorptionCueEvents = absorptionSignals; report.smileCueEvents = smileSignals; report.resetCueEvents = resetSignals;
            if (game != null) { report.respawns = game.respawns - startingRespawns; report.finalCheckpoint = game.checkpoint; }
            report.passed = report.failure == null && errors.Count == 0 && report.physicsRestored &&
                report.initialWalkSafe && report.closedBarrierSolid && report.solutionRecognized && report.absorptionObserved &&
                report.absorbedPropsUnselectable && report.intermediateRoomReset && report.redLightingObserved &&
                report.smileObserved && report.openingObserved && report.noDirectFloorLight && report.noExposureDuringWalk &&
                report.curtainTraversed && report.resetAllRestored && report.originalMeshesPreserved && report.originalMaterialsPreserved &&
                report.audioSignalsTimed && report.audioSignalsReset && report.audioSignalLongFrame;
            try
            {
                if (output != null)
                {
                    if (!report.passed && game != null) Capture("portrait-performance-failure");
                    File.WriteAllText(Path.Combine(output, "portrait-performance-results.json"), JsonUtility.ToJson(report, true));
                }
            }
            catch (Exception exception) { report.passed = false; report.failure = exception.ToString(); }
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_PORTRAIT_PERFORMANCE_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_PORTRAIT_PERFORMANCE_FAIL phase=" + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "GalleryPortraitPerformanceValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.conceptLayout, "The real automated museum is required");
            portrait = game.GetComponentInChildren<GalleryConceptPerspective>(true); gate = game.portraitGate;
            Require(portrait != null && gate != null && portrait.pieces.Length == 3, "The actual portrait pieces and curtain gate are required");
            gate.AbsorptionStarted += OnAbsorptionStarted; gate.SmileStarted += OnSmileStarted; gate.PerformanceReset += OnPerformanceReset;
            gateSubscribed = true;
            game.conceptCapture = true;
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            yield return null; yield return null;
            game.ResetAll(); game.Resume(); game.checkpoint = 2;
            startingRespawns = game.respawns;
            RecordOriginalGeometry();

            report.phase = "warm room, unlit walkways and physical closed curtain";
            CheckFloorLight("warm"); Capture("portrait-performance-01-warm");
            game.PlacePlayer(new Vector3(3.3f, 0, 30.8f));
            var warmWalk = WalkTo(new Vector3(3.3f, 0, 42.3f), 600);
            while (warmWalk.MoveNext()) yield return warmWalk.Current;
            report.initialWalkSafe = true;
            // Disclosed collision-only fixture starts on empty floor in front of
            // the curtain. The unsolved pieces have not been moved.
            game.PlacePlayer(new Vector3(0, 0, 42.55f));
            for (int i = 0; i < 100; i++) { Step(Vector2.up); if (i % 12 == 0) yield return null; }
            Require(game.player.feet.z < 43.3f && !gate.Opened && gate.barrier.enabled,
                "The unsolved physical curtain must block an actual controller crossing");
            report.closedBarrierSolid = true;
            Check("Actual walking through the warm portrait room is safe and its unsolved physical curtain remains solid");

            report.phase = "first absorption and public R/current-room interruption";
            int absorptionBefore = absorptionSignals, smileBefore = smileSignals;
            SolveFixture();
            Capture("portrait-performance-02-complete-face");
            int observationTicks = 0;
            for (int i = 0; i < 240 && !gate.Performing; i++)
            { Step(Vector2.zero); observationTicks++; if (i % 12 == 0) yield return null; }
            Require(gate.Performing && !gate.Opened, "Recognizing the face must start its performance before opening");
            report.observedHoldSeconds = observationTicks * Dt;
            Require(Mathf.Abs(report.observedHoldSeconds - GalleryPortraitGate.HoldSeconds) < .05f,
                "The completed face must be observed for the approved one-second hold before absorption");
            Require(absorptionSignals == absorptionBefore + 1 && smileSignals == smileBefore &&
                absorptionSignalStage == GalleryPortraitGate.RevealStage.Absorbing && absorptionSignalElapsed < .0001f,
                "BGM silence must be signalled once at real absorption start, before any smile cue");
            report.solutionRecognized = true;
            for (int i = 0; i < 70; i++) { Step(Vector2.zero); if (i % 12 == 0) yield return null; }
            Require(gate.Performing && !gate.Opened && gate.barrier.enabled,
                "The curtain barrier opened before the absorption could finish");
            report.absorptionObserved = true;
            Capture("portrait-performance-03-absorbing");
            int resetsBefore = resetSignals;
            game.ResetZone(); game.Resume();
            RequireReset("R during absorption");
            Require(resetSignals == resetsBefore + 1 && smileSignals == smileBefore,
                "Room reset must cancel the pending smile without emitting its cue");
            report.audioSignalsReset = true;
            report.intermediateRoomReset = true;
            Check("Public current-room reset interrupts absorption and restores the original sculpture, neutral lights and closed gate");

            report.phase = "full slow performance with actual walking during every color/expression phase";
            absorptionBefore = absorptionSignals; smileBefore = smileSignals;
            SolveFixture();
            var path = new[] { new Vector3(3.3f, 0, 32), new Vector3(3.3f, 0, 41.6f), new Vector3(0, 0, 42.65f),
                new Vector3(-3.3f, 0, 41.6f), new Vector3(-3.3f, 0, 32), new Vector3(0, 0, 30.8f) };
            int waypoint = 0;
            bool redCaptured = false, smileCaptured = false, openingCaptured = false, absorbedChecked = false;
            for (int i = 0; i < Mathf.CeilToInt(18f / Dt) && !gate.Opened; i++)
            {
                Vector2 movement = Vector2.zero;
                // Wait until recognition starts; looking away beforehand must
                // not accidentally satisfy a face-observation requirement.
                if (gate.Performing)
                {
                    Vector3 delta = path[waypoint] - game.player.feet; delta.y = 0;
                    if (delta.magnitude < .16f) waypoint = (waypoint + 1) % path.Length;
                    else movement = new Vector2(delta.x, delta.z).normalized * (delta.magnitude < .35f ? .35f : 1);
                }
                Step(movement);
                RecordPerformanceStep(movement);
                if (gate.Performing) Require(gate.barrier.enabled, "The physical curtain opened before the slow reveal finished");
                report.maximumMood = Mathf.Max(report.maximumMood, gate.MoodRed01);
                report.maximumSmile = Mathf.Max(report.maximumSmile, gate.Smile01);
                report.maximumOpening = Mathf.Max(report.maximumOpening, gate.Opening01);
                if (gate.Absorbed && !absorbedChecked)
                {
                    foreach (var piece in portrait.pieces)
                        Require(!piece.gameObject.activeInHierarchy || Array.TrueForAll(piece.parts, part => !part.enabled),
                            "Absorbed physical props must not remain selectable colliders in the walkway");
                    Look(gate.transform.position + Vector3.up * 2.7f); game.UpdateAim(); game.Interact();
                    Require(game.held == null, "The curtain face must not be picked up during its performance");
                    absorbedChecked = report.absorbedPropsUnselectable = true;
                    CheckFloorLight("absorbed");
                }
                if (!redCaptured && gate.MoodRed01 >= .48f)
                {
                    CheckFloorLight("red transition"); Capture("portrait-performance-04-red-light");
                    redCaptured = report.redLightingObserved = true;
                }
                if (!smileCaptured && gate.Smile01 >= .60f)
                {
                    Require(gate.Opening01 < .05f && !gate.Opened && gate.barrier.enabled,
                        "The slow smile must be visible while the curtain is still closed");
                    CheckFloorLight("smile"); Capture("portrait-performance-05-smile");
                    smileCaptured = report.smileObserved = true;
                }
                if (!openingCaptured && gate.Opening01 >= .45f)
                {
                    Require(gate.Smile01 >= .95f, "Opening started before the smile finished");
                    CheckFloorLight("opening"); Capture("portrait-performance-06-opening");
                    openingCaptured = report.openingObserved = true;
                }
                if (i % 12 == 0) yield return null;
            }
            Require(gate.Opened && !gate.barrier.enabled && absorbedChecked && redCaptured && smileCaptured && openingCaptured,
                "The complete production absorption/red/smile/open sequence did not finish");
            Require(absorptionSignals == absorptionBefore + 1 && smileSignals == smileBefore + 1 &&
                Mathf.Abs(smileSignalElapsed - GalleryPortraitGate.AbsorbSeconds - GalleryPortraitGate.RedSeconds) < .05f,
                "The smile cue must fire once after the original 4.5-second absorption/red interval");
            report.smileCueElapsed = smileSignalElapsed; report.audioSignalsTimed = true;
            Check("Portrait absorption and smile milestone signals preserve the real animation start and 4.5-second visual smile boundary");
            Require(Mathf.Abs(report.observedAbsorbSeconds - GalleryPortraitGate.AbsorbSeconds) < .05f &&
                Mathf.Abs(report.observedRedSeconds - GalleryPortraitGate.RedSeconds) < .05f &&
                Mathf.Abs(report.observedSmileSeconds - GalleryPortraitGate.SmileSeconds) < .05f &&
                Mathf.Abs(report.observedOpenSeconds - GalleryPortraitGate.OpenSeconds) < .05f,
                "An observed animation phase did not retain its approved slow duration");
            Require(report.absorbWalkTicks > 0 && report.redWalkTicks > 0 && report.smileWalkTicks > 0 && report.openingWalkTicks > 0,
                "The actual controller must walk during absorption, red lighting, smiling and curtain opening");
            CheckFloorLight("fully open");
            var crossing = WalkTo(new Vector3(0, 0, 45.05f), 500);
            while (crossing.MoveNext()) yield return crossing.Current;
            Require(game.player.feet.z > 44.75f && game.checkpoint == 3, "The actual controller cannot traverse the revealed curtain");
            Require(smileSignals == smileBefore + 1, "Walking through the opened curtain repeated the smile cue");
            report.curtainTraversed = true;
            Require(game.respawns == startingRespawns && report.maximumExposure < .001f,
                "Walking through the new portrait illumination or curtain reveal caused sunlight exposure/respawn");
            report.noExposureDuringWalk = report.noDirectFloorLight = true;
            Check("The real slow smile and curtain reveal preserve unlit walkways and permit an exposure-free physical crossing");

            report.phase = "public ResetAll restores props, original geometry/materials and lighting";
            game.ResetAll(); game.Resume();
            RequireReset("ResetAll after the complete reveal");
            report.resetAllRestored = true;
            CheckOriginalGeometry();

            report.phase = "audio milestones across a long frame and a second complete reset";
            absorptionBefore = absorptionSignals; smileBefore = smileSignals;
            SolveFixture(); gate.Tick(GalleryPortraitGate.HoldSeconds + .01f);
            Require(gate.Stage == GalleryPortraitGate.RevealStage.Absorbing && absorptionSignals == absorptionBefore + 1 &&
                smileSignals == smileBefore, "A replay must emit a new absorption signal before its smile");
            // Disclosed long-frame fixture only: the earlier traversal above
            // uses the real controller and normal 90 Hz production stepping.
            gate.Tick(GalleryPortraitGate.TotalPerformanceSeconds + 1);
            Require(gate.Opened && smileSignals == smileBefore + 1,
                "A frame crossing the complete reveal must not miss the single smile milestone");
            gate.Tick(1); gate.Tick(0);
            Require(absorptionSignals == absorptionBefore + 1 && smileSignals == smileBefore + 1,
                "Completed or zero-time ticks repeated a portrait audio milestone");
            resetsBefore = resetSignals; game.ResetAll(); game.Resume(); RequireReset("ResetAll after long frame");
            Require(resetSignals == resetsBefore + 1, "ResetAll did not emit the performance reset signal");
            report.audioSignalLongFrame = true;
            Check("Portrait audio milestones replay after reset and fire only once even when a frame skips the entire smile phase");
            Capture("portrait-performance-07-reset");
            Check("Full restart restores the original selectable props and their unmodified meshes/materials, with warm lights and a closed curtain");
        }

        void SolveFixture()
        {
            game.checkpoint = 2;
            foreach (var piece in portrait.pieces) piece.Restore();
            for (int i = 0; i < portrait.pieces.Length; i++)
            {
                var piece = portrait.pieces[i];
                piece.transform.SetPositionAndRotation(portrait.transform.TransformPoint(portrait.solutionPositions[i]), portrait.transform.rotation);
                piece.transform.localScale = Vector3.one * portrait.solutionScales[i];
                piece.body.position = piece.transform.position; piece.body.rotation = piece.transform.rotation;
                piece.RefreshMass(); Physics.SyncTransforms(); piece.placement.ResetPlaced();
                Require(game.ValidPlacement(piece, out var reason) && piece.placement.Ready,
                    "The explicitly published solution fixture is physically invalid: " + piece.title + " / " + reason);
            }
            game.PlacePlayer(portrait.viewingPoint.position); Look(portrait.transform.TransformPoint(new Vector3(0, 1.9f, 39)));
            portrait.Evaluate(); Look(portrait.FaceCenter); portrait.Evaluate();
            Require(portrait.Complete && portrait.CanObserve && game.held == null, "Published face fixture is not recognizable from its observer");
        }

        void RecordPerformanceStep(Vector2 movement)
        {
            bool walking = movement.sqrMagnitude > .0001f;
            switch (gate.Stage)
            {
                case GalleryPortraitGate.RevealStage.Absorbing:
                    report.observedAbsorbSeconds += Dt; if (walking) report.absorbWalkTicks++; break;
                case GalleryPortraitGate.RevealStage.Reddening:
                    report.observedRedSeconds += Dt; if (walking) report.redWalkTicks++; break;
                case GalleryPortraitGate.RevealStage.Smiling:
                    report.observedSmileSeconds += Dt; if (walking) report.smileWalkTicks++; break;
                case GalleryPortraitGate.RevealStage.Opening:
                    report.observedOpenSeconds += Dt; if (walking) report.openingWalkTicks++; break;
            }
        }

        void RequireReset(string context)
        {
            Require(!gate.Performing && !gate.Absorbed && !gate.Opened && gate.Progress == 0 &&
                gate.MoodRed01 < .001f && gate.Smile01 < .001f && gate.Opening01 < .001f && gate.barrier.enabled,
                context + ": performance/lighting/barrier state was not reset");
            Require(Shader.GetGlobalFloat("_MuseumPortraitMood") < .001f, context + ": red shader lighting survived reset");
            foreach (var piece in portrait.pieces)
            {
                Require(piece.gameObject.activeInHierarchy && Array.IndexOf(game.props, piece) >= 0 &&
                    Vector3.Distance(piece.transform.position, piece.homePosition) < .01f &&
                    Vector3.Distance(piece.transform.localScale, piece.homeScale) < .001f &&
                    Quaternion.Angle(piece.transform.rotation, piece.homeRotation) < .1f &&
                    piece.body.isKinematic && !piece.body.useGravity &&
                    Array.TrueForAll(piece.parts, part => part.enabled), context + ": original prop was not restored: " + piece.title);
                foreach (var renderer in piece.renderers)
                    Require(renderer.enabled == originalVisibility[renderer], context + ": source renderer visibility changed");
            }
            CheckOriginalGeometry(); CheckFloorLight(context);
        }

        void RecordOriginalGeometry()
        {
            foreach (var piece in portrait.pieces)
            {
                foreach (var filter in piece.GetComponentsInChildren<MeshFilter>(true))
                {
                    originalMeshReferences[filter] = filter.sharedMesh;
                    if (filter.sharedMesh != null && !originalMeshes.ContainsKey(filter.sharedMesh)) originalMeshes.Add(filter.sharedMesh, filter.sharedMesh.vertices);
                }
                foreach (var renderer in piece.renderers)
                { originalVisibility[renderer] = renderer.enabled; originalMaterials[renderer] = renderer.sharedMaterials; }
            }
        }

        void CheckOriginalGeometry()
        {
            foreach (var pair in originalMeshReferences)
                Require(pair.Key.sharedMesh == pair.Value, "A reusable source portrait mesh reference changed");
            foreach (var pair in originalMeshes)
            {
                var vertices = pair.Key.vertices;
                Require(vertices.Length == pair.Value.Length, "A source portrait mesh changed its topology");
                for (int i = 0; i < vertices.Length; i++) Require((vertices[i] - pair.Value[i]).sqrMagnitude < .0000000001f,
                    "Smile deformation altered an original reusable portrait mesh");
            }
            foreach (var pair in originalMaterials)
            {
                var slots = pair.Key.sharedMaterials;
                Require(slots.Length == pair.Value.Length, "Source portrait material slots changed");
                for (int i = 0; i < slots.Length; i++) Require(slots[i] == pair.Value[i], "Source portrait materials were not restored");
            }
            report.originalMeshesPreserved = report.originalMaterialsPreserved = true;
        }

        void CheckFloorLight(string phase)
        {
            Require(Shader.GetGlobalFloat("_GalleryPortraitNoSun") > .5f, "The portrait sunlight exclusion shader state is missing");
            game.lighting.Synchronize();
            for (float z = 29.4f; z <= 43.2f; z += .75f)
            for (float x = -6.5f; x <= 6.5f; x += .75f)
            {
                Vector3 floor = new Vector3(x, .025f, z);
                Require(!game.lighting.IsDirectLit(floor, Vector3.up) && !game.lighting.IsReflectedLit(floor, Vector3.up),
                    "Direct/reflected light reaches portrait floor during " + phase + " at " + floor);
                report.floorSamples++;
            }
        }

        IEnumerator WalkTo(Vector3 goal, int budget)
        {
            for (int i = 0; i < budget; i++)
            {
                Vector3 delta = goal - game.player.feet; delta.y = 0;
                if (delta.magnitude < .16f) yield break;
                Step(new Vector2(delta.x, delta.z).normalized * (delta.magnitude < .35f ? .35f : 1));
                if (i % 12 == 0) yield return null;
            }
            throw new InvalidOperationException("Actual controller failed to walk to " + goal + " from " + game.player.feet);
        }

        void Step(Vector2 movement)
        {
            Guard(); game.Tick(Dt, movement, false); Physics.Simulate(Dt); Physics.SyncTransforms(); game.lighting.Synchronize(); ticks++;
            if (movement.sqrMagnitude > .0001f) report.actualWalkTicks++;
            report.maximumExposure = Mathf.Max(report.maximumExposure, game.exposure);
            Require(game.respawns == startingRespawns, "Actual portrait movement caused a respawn");
            Require(game.safe && game.FootSafe(game.player.feet), "The portrait walkway became unsafe");
            if (game.player.feet.z >= 29 && game.player.feet.z < 44)
                Require(!game.lighting.IsSunlit(game.player.feet + Vector3.up * .025f, Vector3.up), "Actual walked floor is illuminated");
        }

        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            game.SyncView();
        }

        void Capture(string name)
        {
            var camera = game.view; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            var target = new RenderTexture(1600, 900, 24); Texture2D texture = null;
            try
            {
                Vector3 position = portrait.viewingPoint.position + Vector3.up * 1.65f;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(gate.transform.position + Vector3.up * 2.65f - position));
                camera.targetTexture = target; game.lighting.Synchronize(); camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive; target.Release(); Destroy(target); if (texture != null) Destroy(texture);
            }
        }

        void Guard()
        {
            Require(Time.realtimeSinceStartupAsDouble <= deadline, "Focused portrait validation exceeded 120 seconds");
            Require(ticks < MaximumTicks, "Focused portrait validation exceeded its simulated tick budget");
        }
        void Check(string message) { checks.Add(message); Debug.Log("PORTRAIT_PERFORMANCE_PASS " + message); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("PORTRAIT_PERFORMANCE: " + message); }
        void RestoreSimulation() { if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; } }
        void Log(string message, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
        void OnAbsorptionStarted()
        { absorptionSignals++; absorptionSignalElapsed = gate.PerformanceElapsed; absorptionSignalStage = gate.Stage; }
        void OnSmileStarted() { smileSignals++; smileSignalElapsed = gate.PerformanceElapsed; }
        void OnPerformanceReset() { resetSignals++; }
        void Unsubscribe()
        {
            if (subscribed) { Application.logMessageReceived -= Log; subscribed = false; }
            if (gateSubscribed && gate != null)
            { gate.AbsorptionStarted -= OnAbsorptionStarted; gate.SmileStarted -= OnSmileStarted; gate.PerformanceReset -= OnPerformanceReset; }
            gateSubscribed = false;
        }
        void OnDestroy() { RestoreSimulation(); Unsubscribe(); }
    }
}
