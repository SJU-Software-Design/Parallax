using UnityEngine;

namespace ShadeLink.Gallery
{
    // The gameplay camera is synchronized at order 0; the portal renders at
    // order 100. Offset this displayed view between them so both agree.
    [DefaultExecutionOrder(50)]
    public sealed class GalleryCathedralRumble : MonoBehaviour
    {
        public GalleryGame game;
        public GalleryCathedralActivation activation;
        public const float MaximumPositionOffset = .018f;
        public const float MaximumRotationDegrees = .28f;
        public Vector3 AppliedLocalOffset { get; private set; }
        public Vector3 AppliedEulerOffset { get; private set; }
        public float AppliedIntensity { get; private set; }
        Camera sampledCamera;
        Vector3 basePosition, sampledPosition;
        Quaternion baseRotation, sampledRotation;
        bool sampleOwned;

        public static GalleryCathedralRumble Install(GalleryGame game, GalleryCathedralActivation activation)
        {
            var effect = game.GetComponent<GalleryCathedralRumble>();
            if (effect == null) effect = game.gameObject.AddComponent<GalleryCathedralRumble>();
            effect.game = game; effect.activation = activation;
            return effect;
        }

        void LateUpdate() { ApplySample(); }

        // No clocks, controller motion, input angles or FOV are changed here.
        // Repeated calls restore our previous sample before applying this one.
        public void ApplySample()
        {
            if (game != null && game.Transitioning)
            {
                // The transition owns the view from this point. Even restoring
                // an old base pose would overwrite its authored camera path.
                ForgetSample(); activation?.ClearRumble(); return;
            }
            ClearSample();
            if (game == null || game.view == null || activation == null) return;
            if (!game.active || game.won) { activation.ClearRumble(); return; }
            float amount = activation.Rumble01;
            if (amount <= 0) return;
            Sample(activation.RumbleTime, amount, out var localOffset, out var eulerOffset);
            sampledCamera = game.view;
            basePosition = sampledCamera.transform.position; baseRotation = sampledCamera.transform.rotation;
            sampledPosition = basePosition + baseRotation * localOffset;
            sampledRotation = baseRotation * Quaternion.Euler(eulerOffset);
            sampledCamera.transform.SetPositionAndRotation(sampledPosition, sampledRotation);
            AppliedLocalOffset = localOffset; AppliedEulerOffset = eulerOffset; AppliedIntensity = amount;
            sampleOwned = true;
        }

        public static void Sample(float time, float intensity, out Vector3 localOffset, out Vector3 eulerOffset)
        {
            intensity = Mathf.Clamp01(intensity);
            float t = time * Mathf.PI * 2;
            // Broad 4-6 Hz movement carries the weight. Smaller incommensurate
            // harmonics roughen it without abrupt random direction changes.
            localOffset = new Vector3(
                (.72f * Mathf.Sin(t * 4.3f) + .28f * Mathf.Sin(t * 9.7f + .8f)) * .009f,
                (.78f * Mathf.Sin(t * 5.2f + .4f) + .22f * Mathf.Sin(t * 11.3f)) * .017f,
                (.80f * Mathf.Sin(t * 4.7f + 1.1f) + .20f * Mathf.Sin(t * 8.9f)) * .004f);
            eulerOffset = new Vector3(
                (.80f * Mathf.Sin(t * 4.8f + .6f) + .20f * Mathf.Sin(t * 10.3f)) * .24f,
                (.78f * Mathf.Sin(t * 4.1f + 1.4f) + .22f * Mathf.Sin(t * 8.7f)) * .10f,
                (.82f * Mathf.Sin(t * 5.6f) + .18f * Mathf.Sin(t * 9.3f + 1.2f)) * .17f);
            localOffset = Vector3.ClampMagnitude(localOffset, MaximumPositionOffset) * intensity;
            eulerOffset = Vector3.ClampMagnitude(eulerOffset, MaximumRotationDegrees) * intensity;
        }

        public void ClearSample()
        {
            // Game.SyncView or a transition may already have replaced our pose.
            // Only undo a pose that still exactly belongs to this effect.
            if (sampleOwned && sampledCamera != null &&
                (game == null || !game.Transitioning) &&
                (sampledCamera.transform.position - sampledPosition).sqrMagnitude < .000000001f &&
                Quaternion.Angle(sampledCamera.transform.rotation, sampledRotation) < .001f)
                sampledCamera.transform.SetPositionAndRotation(basePosition, baseRotation);
            ForgetSample();
        }
        void ForgetSample()
        {
            sampleOwned = false; sampledCamera = null;
            AppliedLocalOffset = AppliedEulerOffset = Vector3.zero; AppliedIntensity = 0;
        }
        void OnDisable() { ClearSample(); }
    }
}
