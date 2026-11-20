using UnityEngine;

namespace ShadeLink.Eclipse
{
    public sealed partial class EclipseFinaleGame
    {
        readonly FirstPersonMotor sharedLocomotion = new FirstPersonMotor
        {
            GroundFilter = PlacementSupport.IsUsableGround
        };
        PlayerMovementAudio sharedMovementAudio;
        bool sharedJumpHeld, sharedPendingDrop;

        public FirstPersonMotor Locomotion => sharedLocomotion;
        public PlayerMovementAudio MovementAudio => sharedMovementAudio;
        public bool ControlDropQueued => sharedPendingDrop;
        public float ControlYawDegrees => yaw;
        public float ControlPitchDegrees => pitch;

        // The capsule and eye use the same contract as Tutorial/Gallery/Beach.
        // The owning scene still configures its far plane, background, and FOV.
        void ConfigureSharedControls()
        {
            FirstPersonMotor.Configure(body, view);
            sharedMovementAudio = PlayerMovementAudio.Attach(body);
            EclipseMovementAudio.Attach(body);
            ResetSharedMotion();
        }

        void ResetSharedMotion()
        {
            sharedLocomotion.Reset();
            sharedMovementAudio?.Reset();
            EclipseMovementAudio.ResetFor(body);
            controls.Reset();
            sharedJumpHeld = sharedPendingDrop = false;
            vertical = 0;
        }

        void ClearSharedControlInput()
        {
            sharedLocomotion.ClearInput();
            controls.Reset();
            sharedJumpHeld = sharedPendingDrop = false;
            vertical = sharedLocomotion.VerticalVelocity;
        }

        // Called after PlayerControls.Read, so these are the same post-sensitivity
        // look values and remapped actions that the other maps consume.
        public void ApplySharedControlFrame(PlayerControls.Frame frame)
        {
            if (!started || paused || EndingSequenceActive) return;
            sharedLocomotion.SmoothKeyboardInput = !controls.UsingGamepad;
            if (frame.RotateHeld && held == FinaleItem.Moon)
            {
                Vector2 turn = PlayerControls.ObjectRotationDegrees(frame.Look);
                carryRotation = Quaternion.AngleAxis(turn.x, Vector3.up) * carryRotation *
                    Quaternion.AngleAxis(turn.y, Vector3.right);
            }
            else
            {
                // Eclipse retains degree-based camera state for its cinematic.
                // Shared controls use radians and a positive upward pitch.
                float yawRadians = yaw * Mathf.Deg2Rad;
                float pitchRadians = -pitch * Mathf.Deg2Rad;
                PlayerControls.ApplyLook(ref yawRadians, ref pitchRadians, frame.Look);
                yaw = yawRadians * Mathf.Rad2Deg;
                pitch = -pitchRadians * Mathf.Rad2Deg;
            }
            SyncCamera();
            UpdateAim();
            if (frame.CancelPressed)
            {
                sharedPendingDrop = false;
                Cancel();
            }
            if (frame.GrabPressed)
            {
                if (held != FinaleItem.None) sharedPendingDrop = true;
                else Interact();
            }
            sharedJumpHeld = frame.JumpHeld;
        }

        // Tick supplies local input, exactly as the Tutorial motor path does.
        void StepSharedMovement(float dt, Vector2 localInput, bool jumpPressed)
        {
            sharedLocomotion.VerticalVelocity = vertical;
            sharedLocomotion.Step(body, localInput, yaw * Mathf.Deg2Rad,
                jumpPressed, sharedJumpHeld || jumpPressed, dt);
            vertical = sharedLocomotion.VerticalVelocity;
            sharedMovementAudio?.Tick(sharedLocomotion.Velocity, sharedLocomotion.Grounded,
                localInput.sqrMagnitude > .0001f, dt);
        }

        // Run only after movement, SyncCamera, and UpdateHeld for this frame.
        // A click releases the pose the player is currently seeing.
        void FlushSharedControlDrop()
        {
            if (!sharedPendingDrop) return;
            sharedPendingDrop = false;
            if (held != FinaleItem.None) Drop();
        }
    }
}
