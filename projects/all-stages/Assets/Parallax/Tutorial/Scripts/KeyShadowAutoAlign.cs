using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Aligns only the held sculpture; it does not compare pixels or create a key.
    public sealed class KeyShadowAutoAlign
    {
        const float ToleranceDegrees = 2f;
        const float HoldSeconds = 1f;
        const float AlignSeconds = .9f;
        // Measured on the movable sculpture root in KeyShadowPreview.
        static readonly Quaternion[] ReferenceRootRotations =
        {
            Quaternion.Euler(17.381f, -78.266f, .65f),
            Quaternion.Euler(-18.534f, -258.966f, 8.73f)
        };
        readonly Transform sculpture;
        readonly Transform lightTransform;
        float holdTime, alignTime, previousAlignTime;
        int nearTarget = -1;
        bool aligning, completed;
        Quaternion startRotation, previousOutput, alignmentTarget, alignmentTwist;
        Quaternion completedBeamAdjustment;

        public KeyShadowAutoAlign(Transform root, Transform light)
        {
            sculpture = root;
            lightTransform = light;
        }

        // The allowed free rotation is about the light's world-space axis, which
        // is tilted in this scene; it is not an Euler X component.
        Quaternion TargetAtCurrentRoll(Quaternion current, int direction, out Quaternion twist)
        {
            Quaternion reference = ReferenceRootRotations[direction];
            Quaternion delta = current * Quaternion.Inverse(reference);
            Vector3 axis = lightTransform.forward.normalized;
            float projected = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis);
            float length = Mathf.Sqrt(projected * projected + delta.w * delta.w);
            twist = length < .00001f ? Quaternion.identity :
                new Quaternion(axis.x * projected / length, axis.y * projected / length,
                    axis.z * projected / length, delta.w / length);
            return twist * reference;
        }

        public float NearestTargetAngle(Quaternion rotation, out int direction)
        {
            direction = 0;
            if (lightTransform == null) return float.PositiveInfinity;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < ReferenceRootRotations.Length; i++)
            {
                float angle = Quaternion.Angle(rotation, TargetAtCurrentRoll(rotation, i, out _));
                if (angle < nearest) { nearest = angle; direction = i; }
            }
            return nearest;
        }

        public float HeldTime => holdTime;
        public bool IsAligning => aligning;
        public bool HasPendingCompletion => completed;
        public static float RequiredHoldTime => HoldSeconds;
        public static float AllowedAngle => ToleranceDegrees;

        public void Reset()
        {
            holdTime = alignTime = 0f;
            nearTarget = -1;
            aligning = completed = false;
        }

        public bool TryConsumeCompletion(out int direction, out Quaternion beamAdjustment)
        {
            direction = nearTarget;
            beamAdjustment = completedBeamAdjustment;
            if (!completed) return false;
            completed = false;
            return true;
        }

        // A blocked preview was never applied. Keep the hold but retry this step
        // from the last accepted pose; in particular, do not report completion.
        public void RejectAlignmentStep(Quaternion appliedRotation)
        {
            alignTime = previousAlignTime;
            previousOutput = appliedRotation;
            aligning = true;
            completed = false;
        }

        public Quaternion Tick(Quaternion current, float dt, bool manualRotation = false)
        {
            if (sculpture == null || lightTransform == null)
            { Reset(); return current; }
            if (manualRotation) { Reset(); return current; }
            float closestAngle = NearestTargetAngle(current, out int closest);

            if (aligning)
            {
                // A fresh rotation input lets the player immediately take control.
                if (Quaternion.Angle(current, previousOutput) > .15f)
                { Reset(); return current; }
                previousAlignTime = alignTime;
                alignTime += Mathf.Max(0f, dt);
                float t = Mathf.Clamp01(alignTime / AlignSeconds);
                t = t * t * (3f - 2f * t);
                previousOutput = Quaternion.Slerp(startRotation, alignmentTarget, t);
                if (t >= 1f) { aligning = false; completed = true; completedBeamAdjustment = alignmentTwist; }
                return previousOutput;
            }
            if (closestAngle > ToleranceDegrees) { Reset(); return current; }
            if (completed) return current;
            if (nearTarget != closest) { nearTarget = closest; holdTime = 0f; }
            holdTime += Mathf.Max(0f, dt);
            if (holdTime >= HoldSeconds)
            {
                aligning = true; alignTime = 0f; startRotation = previousOutput = current;
                alignmentTarget = TargetAtCurrentRoll(current, nearTarget, out alignmentTwist);
            }
            return current;
        }
    }
}
