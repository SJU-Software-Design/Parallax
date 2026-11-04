using System;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Only tutorial dice opt in. PropReleasePhysics still owns contact audio.
    [DisallowMultipleComponent]
    public sealed class TutorialLandingFeedback : MonoBehaviour
    {
        internal const float MaximumPitch = .45f;
        const float QuietSpeed = .35f;
        const float FullImpactSpeed = 3.5f;
        const float SupportNormalY = .6f;

        Action<float> onImpact;
        bool pending;
        float releasedSide;

        internal void Configure(Action<float> listener) => onImpact = listener;

        internal void RecordPlacementResult(bool placed, float side)
        {
            pending = placed;
            releasedSide = side;
        }

        internal void ClearPending() => pending = false;

        void OnDisable() => ClearPending();

        void OnCollisionEnter(Collision collision) => ObserveCollision(collision);

        // A body can scrape a ledge's side before touching its top in the same contact pair.
        void OnCollisionStay(Collision collision) => ObserveCollision(collision);

        void ObserveCollision(Collision collision)
        {
            if (!pending || !isActiveAndEnabled) return;

            float speed = -1f;
            for (int i = 0; i < collision.contactCount; i++)
                speed = Mathf.Max(speed, SupportSpeed(collision.GetContact(i).normal, collision.relativeVelocity));
            ConsumeSupport(speed);
        }

        internal static float SupportSpeed(Vector3 normal, Vector3 relativeVelocity) =>
            normal.y >= SupportNormalY ? Mathf.Abs(Vector3.Dot(relativeVelocity, normal)) : -1f;

        internal void ConsumeSupport(float speed)
        {
            if (!pending || speed < 0f) return;
            pending = false;
            if (speed <= QuietSpeed) return;

            float sizeWeight = Mathf.Clamp01(Mathf.Log(1f + releasedSide) / Mathf.Log(11f));
            float impactWeight = Mathf.Sqrt(Mathf.InverseLerp(QuietSpeed, FullImpactSpeed, speed));
            onImpact?.Invoke(Mathf.Lerp(.08f, MaximumPitch, sizeWeight) * impactWeight);
        }
    }

    internal sealed class TutorialLandingShake : IDisposable
    {
        const float RiseTime = .024f;
        readonly Camera view;
        float startTime, duration, startingPitch, peakPitch;
        bool viewOffsetApplied;

        internal TutorialLandingShake(Camera view)
        {
            this.view = view;
            Camera.onPreCull += BeginRender;
            Camera.onPostRender += EndRender;
        }

        internal void Play(float pitch, float now)
        {
            if (pitch <= 0f || (now < startTime + duration && pitch <= peakPitch)) return;

            startingPitch = Sample(now);
            peakPitch = Mathf.Min(pitch, TutorialLandingFeedback.MaximumPitch);
            duration = Mathf.Lerp(.10f, .16f, peakPitch / TutorialLandingFeedback.MaximumPitch);
            startTime = now;
        }

        internal float Sample(float now)
        {
            float elapsed = now - startTime;
            if (duration <= 0f || elapsed >= duration) return 0f;
            if (elapsed < RiseTime) return Mathf.SmoothStep(startingPitch, peakPitch, elapsed / RiseTime);
            return Mathf.SmoothStep(peakPitch, 0f, (elapsed - RiseTime) / (duration - RiseTime));
        }

        void BeginRender(Camera camera)
        {
            if (camera == view) ApplyView(Time.unscaledTime);
        }

        void EndRender(Camera camera)
        {
            if (camera == view) RestoreView();
        }

        internal void ApplyView(float now)
        {
            RestoreView();
            float pitch = Sample(now);
            if (pitch <= 0f) return;

            // Unity's view space reverses Z. This pitches the rendered camera down
            // without moving its transform, which the interaction and audio use.
            view.worldToCameraMatrix = Matrix4x4.Rotate(Quaternion.Euler(pitch, 0f, 0f)) * view.worldToCameraMatrix;
            viewOffsetApplied = true;
        }

        internal void RestoreView()
        {
            if (!viewOffsetApplied) return;
            if (view != null) view.ResetWorldToCameraMatrix();
            viewOffsetApplied = false;
        }

        internal void Clear()
        {
            duration = 0f;
            RestoreView();
        }

        public void Dispose()
        {
            Camera.onPreCull -= BeginRender;
            Camera.onPostRender -= EndRender;
            Clear();
        }
    }
}
