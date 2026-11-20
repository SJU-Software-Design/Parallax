using System;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Invoked only by the isolated, muted native ending regression. Frames here
    // are declared fixtures, not synthesized OS mouse/keyboard input.
    public static class EclipseControlFeelValidation
    {
        sealed class ReferenceFloor : IDisposable
        {
            readonly GameObject root;
            public readonly CharacterController Body;
            public readonly FirstPersonMotor Motor = new FirstPersonMotor
            {
                GroundFilter = PlacementSupport.IsUsableGround
            };
            public ReferenceFloor()
            {
                root = new GameObject("Validation fixture / common-map reference motor");
                var floor = new GameObject("Isolated reference floor");
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = new Vector3(1000, -.5f, 0);
                floor.AddComponent<BoxCollider>().size = new Vector3(40, 1, 40);
                var player = new GameObject("Reference capsule");
                player.transform.SetParent(root.transform, false);
                player.transform.position = new Vector3(1000, .06f, .3f);
                Body = player.AddComponent<CharacterController>();
                FirstPersonMotor.Configure(Body, null);
                Physics.SyncTransforms();
                for (int i = 0; i < 8; i++) Step(Vector2.zero, .01f);
            }
            public void Step(Vector2 input, float dt, bool jumpPressed = false, bool jumpHeld = false)
            {
                Motor.Step(Body, input, 0, jumpPressed, jumpHeld, dt);
                Physics.SyncTransforms();
            }
            public void Dispose()
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                Physics.SyncTransforms();
            }
        }

        public static void Run(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            bool explicitTest = Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0;
            check(explicitTest && Application.isBatchMode,
                "Control-feel fixtures run only in the explicit isolated batch player", "No captures or OS input");
            check(game != null && game.Credits.Section == EndingCreditSection.Inactive && game.held == FinaleItem.None,
                "Control-feel fixtures begin in the actual unsolved Eclipse opening", "");
            bool automated = game.automated, started = game.started, paused = game.paused;
            game.automated = true;
            float configuredFov = game.view.fieldOfView;
            try
            {
                Reset(game);
                check(Near(game.body.height, 1.6f) && Near(game.body.radius, .3f) &&
                    Near(game.body.center.y, .8f) && Near(game.body.stepOffset, .2f) &&
                    Near(game.body.skinWidth, .05f) && Near(game.body.slopeLimit, 60),
                    "Eclipse uses the actual common-map feet-relative capsule geometry",
                    "height=" + game.body.height + " radius=" + game.body.radius + " step=" + game.body.stepOffset);
                check(Near(game.view.transform.position.y - game.body.transform.position.y, 1.55f) &&
                    Near(game.view.nearClipPlane, .05f) && Near(game.view.fieldOfView, configuredFov),
                    "The common 1.55m eye and near plane preserve the player's configured FOV", "FOV=" + configuredFov);
                check(game.Locomotion != null && game.MovementAudio != null &&
                    game.MovementAudio.gameObject == game.body.gameObject,
                    "The actual Eclipse owns the shared locomotion and movement-audio components", "");
                foreach (int fps in new[] { 30, 60, 120 }) CompareKeyboardTrajectory(game, fps, check);

                Reset(game);
                game.Locomotion.SmoothKeyboardInput = false;
                for (int i = 0; i < 30; i++) Step(game, new Vector2(.5f, 0), .01f, false);
                check(Mathf.Abs(game.Locomotion.Velocity.x - 1) < .08f,
                    "Actual Eclipse analog half-input uses the common squared response",
                    "realizedVelocity=" + game.Locomotion.Velocity);
                for (int i = 0; i < 30; i++) Step(game, Vector2.one, .01f, false);
                check(Mathf.Abs(new Vector2(game.Locomotion.Velocity.x, game.Locomotion.Velocity.z).magnitude - 4) < .08f,
                    "Actual Eclipse diagonal movement is capped at the same 4m/s", game.Locomotion.Velocity.ToString("F4"));

                foreach (int fps in new[] { 30, 60, 120 })
                {
                    JumpObservation shortJump = Jump(game, fps, false), heldJump = Jump(game, fps, true);
                    check(shortJump.MaximumPositionError < .025f && heldJump.MaximumPositionError < .025f &&
                        Mathf.Abs(shortJump.Actual - shortJump.Reference) < .025f &&
                        Mathf.Abs(heldJump.Actual - heldJump.Reference) < .025f &&
                        Mathf.Abs(heldJump.Actual - 1.4f) < .15f && heldJump.Actual - shortJump.Actual > .2f,
                        "Actual Eclipse short/held jump tracks identical common-map input frames at " + fps + " fps",
                        "actual short/held=" + shortJump.Actual.ToString("F4") + "/" + heldJump.Actual.ToString("F4") +
                        " reference=" + shortJump.Reference.ToString("F4") + "/" + heldJump.Reference.ToString("F4") +
                        " maxTrajectoryError=" + Mathf.Max(shortJump.MaximumPositionError, heldJump.MaximumPositionError).ToString("F5"));
                }

                Reset(game);
                game.SetEndingRotation(Quaternion.identity);
                game.ApplySharedControlFrame(new PlayerControls.Frame { Look = new Vector2(.2f, 3f) });
                check(Mathf.Abs(game.view.transform.forward.y - 1) < .001f &&
                    Mathf.Abs(game.ControlYawDegrees - .2f * Mathf.Rad2Deg) < .001f,
                    "The degree-based cinematic bridges the common radian look and 90-degree upward limit", "");
                game.ApplySharedControlFrame(new PlayerControls.Frame { Look = new Vector2(0, -4f) });
                check(Mathf.Abs(game.ControlPitchDegrees - 70) < .001f,
                    "Eclipse uses the common 70-degree downward look limit", "pitch=" + game.ControlPitchDegrees);

                Reset(game);
                game.AimAt(game.moon.position); game.Interact();
                check(game.held == FinaleItem.Moon, "Control fixtures grab the original moon through its production interaction", "");
                Quaternion before = game.moon.rotation;
                Quaternion cameraBefore = game.view.transform.rotation;
                game.ApplySharedControlFrame(new PlayerControls.Frame { RotateHeld = true, Look = new Vector2(.1f, 0) });
                game.Tick(1f / 60, Vector2.zero, false);
                check(Mathf.Abs(Quaternion.Angle(before, game.moon.rotation) - .1f * Mathf.Rad2Deg) < .02f &&
                    Quaternion.Angle(cameraBefore, game.view.transform.rotation) < .01f,
                    "A held moon rotates at the common one-times response while the view stays fixed",
                    "appliedDegrees=" + Quaternion.Angle(before, game.moon.rotation));
                game.Cancel();

                Reset(game);
                game.AimAt(game.moon.position); game.Interact();
                Vector3 oldPose = game.moon.position;
                game.AimAt(EclipseFinaleGame.SunPosition);
                game.ApplySharedControlFrame(new PlayerControls.Frame { GrabPressed = true, Move = Vector2.right });
                check(game.ControlDropQueued && game.held == FinaleItem.Moon,
                    "A release click waits for this frame's moved camera and held pose", "");
                game.Tick(1f / 30, Vector2.right, false);
                check(!game.ControlDropQueued && game.held == FinaleItem.None && Vector3.Distance(oldPose, game.moon.position) > 1,
                    "The production tick releases the current pose rather than the previous-frame moon",
                    "old=" + oldPose + " released=" + game.moon.position);

                Reset(game);
                for (int i = 0; i < 30; i++) StepFrame(game, Vector2.right, .01f);
                game.Pause(); game.Play();
                Vector3 stopped = game.body.transform.position;
                StepFrame(game, Vector2.zero, .02f);
                check(Vector2.Distance(new Vector2(stopped.x, stopped.z), new Vector2(game.body.transform.position.x, game.body.transform.position.z)) < .001f,
                    "Pause/resume clears old keyboard ramp and planar momentum before input resumes", "");
                for (int i = 0; i < 30; i++) StepFrame(game, Vector2.right, .01f);
                game.Teleport(game.Spawn);
                check(game.Locomotion.Velocity.sqrMagnitude < .00001f && !game.ControlDropQueued,
                    "Teleport resets locomotion impulse and pending placement instead of carrying old movement", "");
                check(Near(game.view.fieldOfView, configuredFov),
                    "Movement, jump, rotation, placement, pause and teleport retain the configured FOV", "FOV=" + configuredFov);
            }
            finally
            {
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.automated = automated;
            }
        }

        static void CompareKeyboardTrajectory(EclipseFinaleGame game, int fps, Action<bool, string, string> check)
        {
            Reset(game);
            using (var reference = new ReferenceFloor())
            {
                Vector3 actualStart = game.body.transform.position, referenceStart = reference.Body.transform.position;
                float largestError = 0;
                int forward = fps, stop = Mathf.RoundToInt(fps * .4f), reverse = Mathf.RoundToInt(fps * .2f);
                for (int i = 0; i < forward + stop + reverse; i++)
                {
                    Vector2 input = i < forward ? Vector2.up : i < forward + stop ? Vector2.zero : Vector2.down;
                    StepFrame(game, input, 1f / fps);
                    reference.Step(input, 1f / fps);
                    Vector3 a = game.body.transform.position - actualStart;
                    Vector3 b = reference.Body.transform.position - referenceStart;
                    largestError = Mathf.Max(largestError, Vector3.Distance(a, b));
                }
                check(largestError < .025f && Vector3.Distance(game.Locomotion.Velocity, reference.Motor.Velocity) < .025f,
                    "Actual Eclipse acceleration, stopping and reversal track the other-map motor at " + fps + " fps",
                    "maxPositionError=" + largestError.ToString("F5") + " actualVelocity=" + game.Locomotion.Velocity.ToString("F4") +
                    " referenceVelocity=" + reference.Motor.Velocity.ToString("F4"));
            }
        }
        struct JumpObservation
        {
            public float Actual, Reference, MaximumPositionError;
        }
        static JumpObservation Jump(EclipseFinaleGame game, int fps, bool held)
        {
            Reset(game);
            using (var reference = new ReferenceFloor())
            {
                float floor = game.body.transform.position.y, referenceFloor = reference.Body.transform.position.y;
                float dt = 1f / fps;
                var observation = new JumpObservation();
                for (int i = 0; i < fps * 2; i++)
                {
                    game.ApplySharedControlFrame(new PlayerControls.Frame { JumpPressed = i == 0, JumpHeld = held });
                    game.Tick(dt, Vector2.zero, i == 0);
                    // Gallery/Beach's direct Tick contract treats a pressed frame
                    // as held for that frame. Compare that identical input here.
                    reference.Step(Vector2.zero, dt, i == 0, held || i == 0);
                    Physics.SyncTransforms();
                    float actualHeight = game.body.transform.position.y - floor;
                    float referenceHeight = reference.Body.transform.position.y - referenceFloor;
                    observation.Actual = Mathf.Max(observation.Actual, actualHeight);
                    observation.Reference = Mathf.Max(observation.Reference, referenceHeight);
                    observation.MaximumPositionError = Mathf.Max(observation.MaximumPositionError,
                        Mathf.Abs(actualHeight - referenceHeight));
                }
                return observation;
            }
        }
        static void Reset(EclipseFinaleGame game)
        {
            game.ResetGame(true);
            game.automated = true;
            game.SetEndingRotation(Quaternion.identity);
            for (int i = 0; i < 8; i++) StepFrame(game, Vector2.zero, .01f);
        }
        static void StepFrame(EclipseFinaleGame game, Vector2 input, float dt)
        {
            game.ApplySharedControlFrame(new PlayerControls.Frame { Move = input });
            Step(game, input, dt, false);
        }
        static void Step(EclipseFinaleGame game, Vector2 input, float dt, bool jump)
        {
            game.Tick(dt, input, jump);
            Physics.SyncTransforms();
        }
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    }
}
