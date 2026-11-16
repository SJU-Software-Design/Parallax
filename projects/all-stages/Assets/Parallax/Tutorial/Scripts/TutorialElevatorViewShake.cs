using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Apply the small start/stop vibration only while rendering. The owning
    // game's look pose, collision body and transfer pose stay untouched.
    public sealed class TutorialElevatorViewShake : MonoBehaviour
    {
        public TutorialElevatorRide Ride;
        Vector3 originalPosition;
        Quaternion originalRotation;
        bool applied;

        void OnPreCull()
        {
            Restore();
            if (Ride == null || Ride.Paused || !Ride.IsTravelling) return;
            float envelope = Ride.Phase == ElevatorPhase.Stopping
                ? 1 - Mathf.Clamp01(Ride.PhaseElapsed / Mathf.Max(.1f, Ride.StopDuration))
                : Mathf.Sin(Mathf.Clamp01(Ride.RideElapsed / .7f) * Mathf.PI);
            if (envelope <= 0) return;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            float t = Ride.RideElapsed + Ride.PhaseElapsed;
            transform.localPosition += new Vector3(Mathf.Sin(t * 47) * .0025f,
                Mathf.Sin(t * 39) * .005f, 0) * envelope;
            transform.localRotation *= Quaternion.Euler(0, 0, Mathf.Sin(t * 33) * .06f * envelope);
            applied = true;
        }

        void OnPostRender() { Restore(); }
        void OnDisable() { Restore(); }

        void Restore()
        {
            if (!applied) return;
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            applied = false;
        }
    }
}
