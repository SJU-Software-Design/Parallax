using UnityEngine;

namespace ShadeLink.Eclipse
{
    // User-selected internally synthesized candidate 02. It replaces the old
    // generated three-tone room sound; it is not an external sound recording.
    [DefaultExecutionOrder(990)]
    public sealed class EclipseEndingAmbience : MonoBehaviour
    {
        public const string ResourceKey = "Eclipse/Audio/LowMachineAmbienceLoop";
        public const float BaseGain = .18f, InitialFadeSeconds = 1.25f, CreditsFadeSeconds = 2.5f;
        public AudioSource AmbienceSource { get; private set; }
        public AudioClip Clip { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public float FadeGain { get; private set; }
        public bool ReadyForCreditsMusic => Clip == null || (creditsLatched && creditsAge >= CreditsFadeSeconds);
        public bool LegacyToneMuted => game != null && game.world != null && !game.world.LegacyAmbienceEnabled;
        EclipseFinaleGame game;
        float initialAge, creditsAge, creditsStartGain;
        bool creditsLatched, wasPlaying;

        public void Initialize(EclipseFinaleGame owner)
        {
            if (game != null || owner == null) return;
            game = owner; Clip = Resources.Load<AudioClip>(ResourceKey);
            if (Clip == null) { Debug.LogError("ECLIPSE_AMBIENCE: Selected synthetic candidate02 loop is missing."); return; }
            var node = new GameObject("Eclipse selected low machine ambience");
            node.transform.SetParent(transform, false);
            AmbienceSource = node.AddComponent<AudioSource>();
            AmbienceSource.playOnAwake = false; AmbienceSource.loop = true;
            AmbienceSource.spatialBlend = 0; AmbienceSource.pitch = 1;
            AmbienceSource.dopplerLevel = 0; AmbienceSource.priority = 150;
            AmbienceSource.clip = Clip; AmbienceSource.volume = 0;
            GameShellSettingsRuntime.Route(AmbienceSource);
            if (game.world != null) game.world.SetLegacyAmbienceEnabled(false);
        }
        void Update() { TickAmbience(Time.deltaTime); }
        public void TickAmbience(float dt)
        {
            if (game == null || game.Credits == null || Clip == null || AmbienceSource == null) return;
            bool inCredits = game.Credits.Section != EndingCreditSection.Inactive;
            if (!inCredits && creditsLatched) ResetSound();
            if (inCredits && !creditsLatched)
            { creditsLatched = true; creditsAge = 0; creditsStartGain = FadeGain; }
            if (game.paused || game.PresentationSuspended)
            {
                if (Started && !Paused)
                { wasPlaying = AmbienceSource.isPlaying; if (wasPlaying) AmbienceSource.Pause(); Paused = true; }
                return;
            }
            if (Paused)
            {
                if (wasPlaying) { GameShellSettingsRuntime.Route(AmbienceSource); AmbienceSource.UnPause(); }
                Paused = false;
            }
            if (float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0;
            dt = Mathf.Clamp(dt, 0, .1f);
            if (creditsLatched)
            {
                creditsAge = Mathf.Min(CreditsFadeSeconds, creditsAge + dt);
                FadeGain = creditsStartGain * (1 - Mathf.SmoothStep(0, 1, creditsAge / CreditsFadeSeconds));
                AmbienceSource.volume = BaseGain * FadeGain;
                if (ReadyForCreditsMusic) { AmbienceSource.Stop(); AmbienceSource.volume = 0; FadeGain = 0; }
                return;
            }
            if (!game.started) return;
            if (!Started)
            {
                AmbienceSource.time = 0; AmbienceSource.volume = 0;
                GameShellSettingsRuntime.Route(AmbienceSource); AmbienceSource.Play(); Started = true;
            }
            initialAge += dt;
            FadeGain = Mathf.SmoothStep(0, 1, initialAge / InitialFadeSeconds);
            AmbienceSource.volume = BaseGain * FadeGain;
        }
        void ResetSound()
        {
            if (AmbienceSource != null) { AmbienceSource.Stop(); AmbienceSource.volume = 0; }
            Started = Paused = creditsLatched = wasPlaying = false;
            FadeGain = initialAge = creditsAge = creditsStartGain = 0;
        }
        void OnDisable() { ResetSound(); }
        void OnDestroy() { ResetSound(); }
    }
}
