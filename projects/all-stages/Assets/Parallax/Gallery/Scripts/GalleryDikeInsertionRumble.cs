using UnityEngine;

namespace ShadeLink.Gallery
{
    // The world remains fixed. The displayed camera receives one short pulse
    // after the blade seats; controller position, aim angles and FOV stay intact.
    [DefaultExecutionOrder(60)]
    public sealed class GalleryDikeInsertionRumble : MonoBehaviour
    {
        public const float Strength = .75f;
        public const float Duration = 1.60f;
        public const float AttackSeconds = .08f;
        public const float HoldSeconds = .24f;
        public const float MaximumPositionOffset = GalleryCathedralRumble.MaximumPositionOffset * Strength;
        public const float MaximumRotationDegrees = GalleryCathedralRumble.MaximumRotationDegrees * Strength;

        public GalleryGame Game { get; private set; }
        public GalleryBottleBalanceKeyReward Reward { get; private set; }
        public float Elapsed { get; private set; }
        public float Intensity { get; private set; }
        public bool Pulsing { get; private set; }
        public int TriggerCount { get; private set; }
        public Vector3 AppliedLocalOffset { get; private set; }
        public Vector3 AppliedEulerOffset { get; private set; }
        Camera sampledCamera;
        Vector3 basePosition, sampledPosition;
        Quaternion baseRotation, sampledRotation;
        bool sampleOwned;

        public static GalleryDikeInsertionRumble Install(GalleryGame game, GalleryBottleBalanceKeyReward reward)
        {
            var effect = game.GetComponent<GalleryDikeInsertionRumble>();
            if (effect == null) effect = game.gameObject.AddComponent<GalleryDikeInsertionRumble>();
            if (effect.Reward != null) effect.Reward.KeyInserted -= effect.Trigger;
            effect.Game = game; effect.Reward = reward;
            if (reward != null) reward.KeyInserted += effect.Trigger;
            effect.ResetPulse();
            return effect;
        }

        void Trigger()
        {
            if (Reward == null || !Reward.Unlocked || Pulsing) return;
            Elapsed = 0; Intensity = 0; Pulsing = true; TriggerCount++;
        }

        // Driven by the same active gameplay clock as insertion and the door.
        public void Tick(float dt)
        {
            if (!Pulsing || dt <= 0 || Game == null || !Game.active || Game.won || Game.Transitioning) return;
            Elapsed = Mathf.Min(Duration, Elapsed + dt);
            if (Elapsed >= Duration) { Pulsing = false; Intensity = 0; ClearSample(); return; }
            float attack = Mathf.SmoothStep(0, 1, Elapsed / AttackSeconds);
            float decay = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(AttackSeconds + HoldSeconds, Duration, Elapsed));
            Intensity = attack * decay * Strength;
        }

        void LateUpdate() { ApplySample(); }

        public void ApplySample()
        {
            ClearSample();
            if (Game == null || Game.view == null || !Game.active || Game.won || Game.Transitioning || Intensity <= 0) return;
            GalleryCathedralRumble.Sample(Elapsed, Intensity, out var position, out var rotation);
            sampledCamera = Game.view;
            basePosition = sampledCamera.transform.position; baseRotation = sampledCamera.transform.rotation;
            sampledPosition = basePosition + baseRotation * position;
            sampledRotation = baseRotation * Quaternion.Euler(rotation);
            sampledCamera.transform.SetPositionAndRotation(sampledPosition, sampledRotation);
            AppliedLocalOffset = position; AppliedEulerOffset = rotation; sampleOwned = true;
        }

        public void ClearSample()
        {
            if (sampleOwned && sampledCamera != null && (Game == null || !Game.Transitioning) &&
                (sampledCamera.transform.position - sampledPosition).sqrMagnitude < .000000001f &&
                Quaternion.Angle(sampledCamera.transform.rotation, sampledRotation) < .001f)
                sampledCamera.transform.SetPositionAndRotation(basePosition, baseRotation);
            sampleOwned = false; sampledCamera = null;
            AppliedLocalOffset = AppliedEulerOffset = Vector3.zero;
        }

        public void ResetPulse()
        {
            ClearSample(); Pulsing = false; Elapsed = Intensity = 0; TriggerCount = 0;
        }
        void OnDisable() { ClearSample(); }
        void OnDestroy() { if (Reward != null) Reward.KeyInserted -= Trigger; }
    }
}
