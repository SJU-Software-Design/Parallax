using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Explicit recognisable-but-imperfect compositions and near-size release
    // fixtures. Actual pickup, walking and Library traversal are separate tests.
    public sealed class GalleryComfortValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt = 1f / 90;
        readonly List<string> errors = new List<string>();
        readonly List<string> checks = new List<string>();
        readonly Report report = new Report();
        string output;
        double deadline;
        SimulationMode previousSimulation;
        GalleryCathedralActivation activation;
        bool previousActivationEnabled, ownsSimulation;
        int ticks;

        [Serializable] sealed class Report
        {
            public bool passed, scatteredStaysClosed, smallerFaceAccepted, screenshotLikeFaceAccepted;
            public bool observationJitterForgiven, heldPiecePausesUnlock, nearSizeAssisted, uprightCorrected;
            public bool tinyStaysUnchanged, heldSculptureStaysUnchanged, obstructionRejected, probePreservesPose;
            public bool portraitApronSafe, shadowCourseHazardPreserved;
            public bool simulationRestored;
            public string version, unity, failure;
            public string scope = "Explicit scene/camera/prop and held-state fixtures verify perceptual portrait acceptance, observation grace and collision-safe near-size assistance. The portrait faces deliberately differ from canonical size and viewer position. The Cathedral fixture uses real placement support, Rigidbody simulation, opening morph and the actual safety probe. This is focused integration QA, not autonomous puzzle solving or input-only carrying; GalleryMuseumMechanicsValidation separately checks real interaction, controller stairs and the Library handoff.";
            public int ticks;
            public float viewerDistanceFromMarker, jitterProgressBefore, jitterProgressDuring;
            public float assistedStartScale, assistedFinalScale, tiltBeforeDegrees, tiltAfterDegrees;
            public Vector3 approximateFaceScales, assistedStartPosition, assistedFinalPosition, blockedPosition;
            public Vector3 portraitApronProbe;
            public string portraitStatus;
            public string[] checks, errors;
        }

        void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack);
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            previousSimulation = Physics.simulationMode;
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
            report.version = GalleryGame.Version; report.unity = Application.unityVersion;
            report.ticks = ticks; report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.scatteredStaysClosed && report.smallerFaceAccepted &&
                report.screenshotLikeFaceAccepted && report.observationJitterForgiven && report.heldPiecePausesUnlock &&
                report.nearSizeAssisted && report.uprightCorrected && report.tinyStaysUnchanged && report.heldSculptureStaysUnchanged &&
                report.obstructionRejected && report.probePreservesPose && report.portraitApronSafe &&
                report.shadowCourseHazardPreserved && report.simulationRestored;
            if (output != null) File.WriteAllText(Path.Combine(output, "comfort-results.json"), JsonUtility.ToJson(report, true));
            if (!report.passed && output != null && game != null) Capture("comfort-failure.png");
            Application.logMessageReceived -= Log;
            if (report.passed) Debug.Log("MUSEUM_COMFORT_OK " + checks.Count + " perceptual/assistance checks");
            else Debug.LogError("MUSEUM_COMFORT_FAIL " + report.failure + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-validationDir");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(Application.persistentDataPath, "GalleryComfortValidation");
            Directory.CreateDirectory(output);
            Require(game != null && game.automated && game.conceptLayout, "Actual automated museum required");
            game.conceptCapture = true; game.ResetAll(); game.Resume(); game.active = false; Time.timeScale = 1;
            Physics.simulationMode = SimulationMode.Script; ownsSimulation = true;
            var portrait = game.GetComponentInChildren<GalleryConceptPerspective>(true);
            var gate = game.portraitGate;
            Require(portrait != null && gate != null && game.orcusExit != null, "Actual portrait and sculpture gate required");
            activation = game.orcusExit.cathedralActivation;
            Require(activation != null, "Cathedral comfort controller missing");
            previousActivationEnabled = activation.enabled; activation.enabled = false;
            yield return null;
            for (int i = 0; i < 60 && Shader.GetGlobalFloat("_MuseumProbeReady") < .5f; i++) yield return null;
            CheckPortraitApronSafety();

            SetViewer(new Vector3(0, 0, 28.5f), new Vector3(0, 1.45f, 39));
            report.viewerDistanceFromMarker = Vector2.Distance(new Vector2(game.player.feet.x, game.player.feet.z),
                new Vector2(portrait.viewingPoint.position.x, portrait.viewingPoint.position.z));
            gate.ResetGate();
            for (int i = 0; i < 140; i++) { gate.Tick(Dt); StepPhysics(); if (i % 20 == 0) yield return null; }
            Require(!gate.Opened, "The original scattered exhibits opened the curtain before the player formed a face");
            report.scatteredStaysClosed = true;
            Check("The original separated exhibits remain a puzzle; no accidental initial completion");

            // A small whole face at a comfortable viewing location beyond the
            // former 1.05m observation-point restriction, with individual errors.
            FaceFixture(portrait, new Vector3(.80f, .76f, .86f));
            LookAtFace(portrait); gate.ResetGate();
            for (int i = 0; i < 200 && !gate.Opened; i++) { gate.Tick(Dt); StepPhysics(); if (i % 12 == 0) yield return null; }
            Require(gate.Opened && !gate.barrier.enabled, "A recognisable smaller face at the player's actual viewing position was rejected: " + portrait.Status);
            report.smallerFaceAccepted = true;
            Check("A smaller translated face with mild piece offsets/rotations opens from 1.7m behind the old viewing mark");

            // Reproduce the user's visual intent: paired eyes, a smaller nose
            // and a noticeably smaller lip sofa are still a recognisable face.
            FaceFixture(portrait, new Vector3(.80f, .65f, .49f));
            LookAtFace(portrait); gate.ResetGate();
            for (int i = 0; i < 36; i++) { gate.Tick(Dt); StepPhysics(); }
            report.portraitStatus = portrait.Status;
            report.approximateFaceScales = new Vector3(portrait.pieces[0].transform.lossyScale.x,
                portrait.pieces[1].transform.lossyScale.x, portrait.pieces[2].transform.lossyScale.x);
            report.jitterProgressBefore = gate.Progress;
            Require(gate.Progress > .1f && !gate.Opened, "Recognisable screenshot-like face did not begin completion feedback: " + portrait.Status);
            float savedYaw = game.yaw, savedPitch = game.pitch;
            game.yaw += 1.3f; game.SyncView(); gate.Tick(.08f);
            report.jitterProgressDuring = gate.Progress;
            Require(gate.Progress >= report.jitterProgressBefore * .90f && !gate.Opened, "A brief observation interruption erased the player's progress");
            report.observationJitterForgiven = true;
            game.yaw = savedYaw; game.pitch = savedPitch; game.SyncView();
            Capture("comfort-01-portrait-approximate.png");
            // Also inspect the actual gameplay HUD at the user's old position:
            // checkpoint 2 must show portrait feedback, without the shadow lesson.
            game.checkpoint = 2; game.active = true; game.conceptCapture = false;
            yield return null; yield return new WaitForEndOfFrame();
            var hudPicture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, "comfort-01-portrait-hud.png"), hudPicture.EncodeToPNG());
            Destroy(hudPicture); game.active = false; game.conceptCapture = true;

            var heldPiece = portrait.pieces[1];
            heldPiece.placement.BeginHold(); game.held = heldPiece;
            for (int i = 0; i < 130; i++) { gate.Tick(Dt); StepPhysics(); }
            Require(!gate.Opened, "Holding a portrait piece completed the curtain observation");
            report.heldPiecePausesUnlock = true;
            game.held = null; heldPiece.placement.ResetPlaced();
            for (int i = 0; i < 220 && !gate.Opened; i++) { gate.Tick(Dt); StepPhysics(); if (i % 12 == 0) yield return null; }
            Require(gate.Opened && !gate.barrier.enabled, "The user's recognisable smaller-nose/lips composition remained frustrating after release: " + portrait.Status);
            report.screenshotLikeFaceAccepted = true;
            for (int i = 0; i < 135; i++) gate.Tick(Dt);
            yield return null; yield return new WaitForEndOfFrame(); Capture("comfort-02-portrait-open.png");
            Check("Recognisable unequal face proportions are accepted; a brief look jitter is forgiven, and releasing a held part resumes completion");

            var entrance = game.orcusExit; var statue = entrance.prop;
            SetViewer(new Vector3(2.5f, 0, 46), new Vector3(0, 2.8f, 53));
            float nearScale = entrance.RequiredScale * .92f;
            ExitFixture(statue, entrance.RequiredScale * .80f, Quaternion.identity, false);
            Vector3 tinyPosition = statue.transform.position; float tinyScale = statue.transform.lossyScale.x;
            for (int i = 0; i < 100; i++) { StepPhysics(); activation.Tick(Dt); }
            Require(Mathf.Abs(statue.transform.lossyScale.x - tinyScale) < .0001f && Vector3.Distance(statue.transform.position, tinyPosition) < .0001f,
                "A clearly small statue was completed without the player enlarging it");
            report.tinyStaysUnchanged = true;

            ExitFixture(statue, nearScale, Quaternion.identity, false);
            game.held = statue; statue.placement.BeginHold();
            for (int i = 0; i < 90; i++) { StepPhysics(); activation.Tick(Dt); }
            Require(Mathf.Abs(statue.transform.lossyScale.x - nearScale) < .0001f && !activation.Assisting,
                "Assistance resized the statue while it was still being carried");
            report.heldSculptureStaysUnchanged = true; game.held = null;
            ExitFixture(statue, nearScale, Quaternion.identity, false);
            report.assistedStartScale = statue.transform.lossyScale.x; report.assistedStartPosition = statue.transform.position;
            Require(activation.CanAssistAtCurrentPlacement(), "The central almost-large-enough statue cannot receive help");
            for (int i = 0; i < 300 && !entrance.IsReadyForEntry; i++)
            { StepPhysics(); activation.Tick(Dt); if (i % 9 == 0) yield return null; }
            report.assistedFinalScale = statue.transform.lossyScale.x; report.assistedFinalPosition = statue.transform.position;
            Require(entrance.IsReadyForEntry && statue.transform.lossyScale.x >= entrance.RequiredScale && activation.Opened,
                "The settled 92%-sized statue did not finish resizing and open");
            Require(Vector3.Distance(report.assistedStartPosition, report.assistedFinalPosition) < .002f,
                "Upright size assistance unexpectedly moved the exhibit");
            report.nearSizeAssisted = true;
            yield return null; yield return new WaitForEndOfFrame(); Capture("comfort-03-assisted-cathedral.png");
            Check("A settled 92%-sized statue is completed to the actual traversable size and opens without moving its anchor");

            ExitFixture(statue, nearScale, Quaternion.Euler(0, 8, 6), true);
            report.tiltBeforeDegrees = Vector3.Angle(statue.transform.up, Vector3.up);
            Vector3 tiltedStart = statue.transform.position;
            Require(activation.CanAssistAtCurrentPlacement(), "A mildly tilted, supported near-solution is not eligible for comfort assistance");
            for (int i = 0; i < 600 && !entrance.IsReadyForEntry; i++)
            { StepPhysics(); activation.Tick(Dt); if (i % 9 == 0) yield return null; }
            report.tiltAfterDegrees = Vector3.Angle(statue.transform.up, Vector3.up);
            Require(entrance.IsReadyForEntry && report.tiltBeforeDegrees > 5 && report.tiltAfterDegrees < 1.0f,
                "The gently tilted statue did not become a usable upright doorway");
            Require(!statue.body.isKinematic && statue.body.useGravity &&
                Vector2.Distance(new Vector2(tiltedStart.x, tiltedStart.z), new Vector2(statue.transform.position.x, statue.transform.position.z)) < .035f,
                "Tilt assistance did not restore dynamic gravity or displaced the exhibit horizontally");
            report.uprightCorrected = true;
            Check("A gently tilted near-solution is made upright, then physical gravity settles it before entry is enabled");

            bool foundBlocked = false;
            foreach (float depth in new[] { 52.15f, 52.65f, 51.65f })
            {
                foreach (float margin in new[] { .025f, .05f, .075f, .10f })
                {
                    ExitFixture(statue, nearScale, Quaternion.identity, false, depth);
                    Bounds bounds = statue.WorldBounds;
                    statue.transform.position += Vector3.right * (-GalleryConceptWorld.Width * .5f + .2f + margin - bounds.min.x);
                    statue.body.position = statue.transform.position; Physics.SyncTransforms(); statue.placement.ResetPlaced();
                    if (!game.ValidPlacement(statue, out _)) continue;
                    Vector3 startPosition = statue.transform.position, startScale = statue.transform.localScale;
                    Quaternion startRotation = statue.transform.rotation;
                    Vector3[] centers = ColliderCenters(activation);
                    if (activation.CanAssistAtCurrentPlacement()) continue;
                    Require(Vector3.Distance(startPosition, statue.transform.position) < .00001f &&
                        Vector3.Distance(startScale, statue.transform.localScale) < .00001f && Quaternion.Angle(startRotation, statue.transform.rotation) < .001f &&
                        CentersMatch(activation, centers), "A failed assistance probe mutated the statue or its collision shapes");
                    report.probePreservesPose = true;
                    for (int i = 0; i < 80; i++) { StepPhysics(); activation.Tick(Dt); }
                    Require(activation.AssistanceBlocked && !activation.Assisting &&
                        Vector3.Distance(startPosition, statue.transform.position) < .0001f && Vector3.Distance(startScale, statue.transform.localScale) < .0001f,
                        "Insufficient room caused unsafe growth rather than a clear unchanged blocked state");
                    report.blockedPosition = startPosition; foundBlocked = true; break;
                }
                if (foundBlocked) break;
            }
            Require(foundBlocked, "Could not establish an independently valid near-wall blocked-growth fixture");
            report.obstructionRejected = true;
            Check("A valid near-wall placement remains unchanged when the final doorway would overlap the wall; safety queries restore all authored collider centres");
        }

        void FaceFixture(GalleryConceptPerspective portrait, Vector3 multipliers)
        {
            for (int i = 0; i < portrait.pieces.Length; i++)
            {
                var piece = portrait.pieces[i]; piece.Restore();
                piece.body.isKinematic = true; piece.body.useGravity = false;
                Vector3 offset = i == 0 ? new Vector3(.10f, 0, .10f) : i == 1 ? new Vector3(-.06f, 0, -.10f) : new Vector3(.08f, 0, .08f);
                piece.transform.SetPositionAndRotation(portrait.transform.TransformPoint(portrait.solutionPositions[i] + offset),
                    portrait.transform.rotation * Quaternion.Euler(0, (i - 1) * 6, 0));
                piece.transform.localScale = Vector3.one * portrait.solutionScales[i] * multipliers[i];
                piece.body.position = piece.transform.position; piece.body.rotation = piece.transform.rotation;
                piece.RefreshMass(); Physics.SyncTransforms(); piece.placement.ResetPlaced();
                Require(game.ValidPlacement(piece, out var reason), "Approximate face fixture is physically invalid: " + reason);
            }
        }

        void ExitFixture(GalleryProp statue, float scale, Quaternion rotation, bool dynamicAfterReady, float depth = 53)
        {
            activation.ResetToRest(); game.held = null; statue.Restore();
            statue.body.isKinematic = true; statue.body.useGravity = false;
            statue.transform.SetPositionAndRotation(new Vector3(0, 0, depth), rotation);
            statue.transform.localScale = Vector3.one * scale;
            statue.transform.position += Vector3.up * (.006f - statue.WorldBounds.min.y);
            statue.body.position = statue.transform.position; statue.body.rotation = statue.transform.rotation;
            statue.RefreshMass(); Physics.SyncTransforms(); statue.placement.ResetPlaced();
            if (dynamicAfterReady)
            {
                statue.body.isKinematic = false; statue.body.useGravity = true;
                statue.body.linearVelocity = Vector3.zero; statue.body.angularVelocity = Vector3.zero; statue.body.Sleep();
            }
            Require(game.ValidPlacement(statue, out var reason), "Comfort sculpture fixture is physically invalid: " + reason);
        }

        void CheckPortraitApronSafety()
        {
            int previousCheckpoint = game.checkpoint;
            try
            {
                game.checkpoint = 1; game.lighting.Synchronize();
                bool found = false;
                for (float x = -6.5f; x <= 6.5f; x += .5f)
                {
                    var candidate = new Vector3(x, .025f, 25.8f);
                    if (!game.CanStand(candidate) || !game.lighting.IsSunlit(candidate, Vector3.up) || game.FootSafe(candidate)) continue;
                    report.portraitApronProbe = candidate; found = true; break;
                }
                Require(found, "No physically sunlit, walkable 25.8m apron point was available to test the actual sunlight hazard");
                game.checkpoint = 2;
                report.portraitApronSafe = game.FootSafe(report.portraitApronProbe);
                Require(report.portraitApronSafe, "The portrait observation apron still burns the player after checkpoint 2");
                game.checkpoint = 1;
                report.shadowCourseHazardPreserved = !game.FootSafe(report.portraitApronProbe);
                Require(report.shadowCourseHazardPreserved, "The portrait apron exemption removed the earlier shadow-course hazard");
                Check("The actual sunlit 25.8m observation apron is safe after checkpoint 2 and retains its normal hazard at checkpoint 1");
            }
            finally { game.checkpoint = previousCheckpoint; }
        }

        void SetViewer(Vector3 feet, Vector3 target)
        { game.PlacePlayer(feet); Look(target); }
        void LookAtFace(GalleryConceptPerspective portrait)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < portrait.pieces.Length; i++) center += portrait.pieces[i].transform.TransformPoint(portrait.markerLocalPositions[i]);
            Look(center / portrait.pieces.Length);
        }
        void Look(Vector3 target)
        {
            Vector3 direction = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(direction.x, direction.z);
            game.pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude); game.SyncView();
        }
        void StepPhysics()
        {
            Guard();
            foreach (var prop in game.props) if (prop.placement != null) prop.placement.Tick(Dt);
            Physics.Simulate(Dt); Physics.SyncTransforms(); game.lighting.Synchronize(); ticks++;
        }
        void Capture(string file)
        {
            Camera camera = game.view; var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D picture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); picture.Apply();
                File.WriteAllBytes(Path.Combine(output, file), picture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                if (picture != null) Destroy(picture); target.Release(); Destroy(target);
            }
        }
        static Vector3[] ColliderCenters(GalleryCathedralActivation value)
        { var result = new Vector3[value.movingParts.Length]; for (int i = 0; i < result.Length; i++) result[i] = value.movingParts[i].center; return result; }
        static bool CentersMatch(GalleryCathedralActivation value, Vector3[] centers)
        { for (int i = 0; i < centers.Length; i++) if (Vector3.Distance(centers[i], value.movingParts[i].center) > .00001f) return false; return true; }
        void Check(string value) { checks.Add(value); Debug.Log("MUSEUM_COMFORT_PASS " + value); }
        void Guard() { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Museum comfort validation exceeded 60 seconds"); }
        void RestoreSimulation()
        {
            if (ownsSimulation) { Physics.simulationMode = previousSimulation; ownsSimulation = false; }
            if (activation != null) activation.enabled = previousActivationEnabled;
        }
        void OnDestroy() { RestoreSimulation(); Application.logMessageReceived -= Log; }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
