using UnityEngine;

namespace ShadeLink.Gallery
{
    // A single recorded, non-looping Effects voice belongs to the persistent
    // hand passage. It follows the passage's fall clock, never BGM or gameplay.
    public sealed class GalleryFallAudio : MonoBehaviour
    {
        public const string ResourceKey = "Gallery/PassageAudio/fall_rush";
        public const float PeakGain = .30f, ReleaseSeconds = .55f, MaximumClockStep = .05f;
        public const float StartPitch = .96f, EndPitch = 1.035f;
        public const float StartCutoff = 1800, EndCutoff = 9500;
        public AudioClip Clip { get; private set; }
        public AudioSource Source { get; private set; }
        public AudioLowPassFilter LowPass { get; private set; }
        public float Gain => Source != null ? Source.volume : 0;
        public float Pitch => Source != null ? Source.pitch : 1;
        public float FallProgress { get; private set; }
        public float ArrivalProgress { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsReleasing { get; private set; }
        public bool Finished { get; private set; }
        public bool Cancelled { get; private set; }
        public bool AudioPaused { get; private set; }
        public int BeginCount { get; private set; }
        public int PauseTransitions { get; private set; }
        public int ResumeTransitions { get; private set; }
        public float ReleaseAge => releaseAge;
        public float LastReleaseDelta { get; private set; }
        // Beach deliberately has timeScale=0 during its reveal input lock.
        // That lock must not freeze an already-started landing release.
        public bool PlaybackSuspended => explicitPaused || applicationPaused ||
            (!automated && !focused) || (!IsReleasing && Time.timeScale <= 0);

        bool initialized, automated, explicitPaused, applicationPaused, focused, skipReleaseDelta;
        float releaseAge, releaseGain, releasePitch, releaseCutoff;

        public static GalleryFallAudio Install(GameObject owner)
        {
            if (owner == null) return null;
            var audio = owner.GetComponent<GalleryFallAudio>();
            if (audio == null) audio = owner.AddComponent<GalleryFallAudio>();
            audio.Initialize();
            return audio;
        }

        void Awake() { Initialize(); }
        void Initialize()
        {
            if (initialized) return;
            initialized = true; focused = Application.isFocused;
            Clip = Resources.Load<AudioClip>(ResourceKey);
            var voice = new GameObject("Gallery falling rush");
            voice.transform.SetParent(transform, false);
            Source = voice.AddComponent<AudioSource>();
            Source.playOnAwake = false; Source.loop = false;
            Source.clip = Clip; Source.spatialBlend = 0; Source.dopplerLevel = 0;
            Source.volume = 0; Source.pitch = StartPitch; Source.priority = 70;
            LowPass = voice.AddComponent<AudioLowPassFilter>();
            LowPass.cutoffFrequency = StartCutoff; LowPass.lowpassResonanceQ = 1;
            // fall_rush intentionally has a neutral resource/source name so the
            // shared router selects Sfx; the selected wind recording is a cue.
            GameShellSettingsRuntime.Route(Source);
        }

        public bool BeginFall(bool automatedPlayback)
        {
            Initialize();
            if (IsActive || Clip == null || Source == null) return false;
            automated = automatedPlayback; focused = Application.isFocused;
            Finished = Cancelled = IsReleasing = false;
            FallProgress = ArrivalProgress = releaseAge = LastReleaseDelta = 0;
            skipReleaseDelta = false;
            Source.Stop(); Source.loop = false; Source.clip = Clip;
            Source.volume = 0; Source.pitch = StartPitch;
            LowPass.cutoffFrequency = StartCutoff;
            IsActive = true; AudioPaused = false;
            GameShellSettingsRuntime.Route(Source); Source.Play(); BeginCount++;
            SynchronizePause();
            return true;
        }

        public void TickProgress(float normalizedFall)
        {
            SynchronizePause();
            if (!IsActive || IsReleasing || AudioPaused) return;
            FallProgress = Mathf.Max(FallProgress, Mathf.Clamp01(normalizedFall));
            // The visual descent is quadratic; its normalized speed is t.
            // Energy and brightness grow with that speed, without a huge pitch shift.
            float energy = FallProgress * FallProgress;
            Source.volume = PeakGain * energy;
            Source.pitch = Mathf.Lerp(StartPitch, EndPitch, FallProgress);
            LowPass.cutoffFrequency = Mathf.Lerp(StartCutoff, EndCutoff, energy);
        }

        public void BeginArrival()
        {
            if (!IsActive || IsReleasing) return;
            IsReleasing = true; releaseAge = ArrivalProgress = 0;
            releaseGain = Gain; releasePitch = Pitch;
            releaseCutoff = LowPass != null ? LowPass.cutoffFrequency : EndCutoff;
            SynchronizePause();
        }

        public void TickArrival(float normalizedArrival)
        {
            SynchronizePause();
            if (!IsActive || !IsReleasing || AudioPaused) return;
            ApplyRelease(Mathf.Max(ArrivalProgress, Mathf.Clamp01(normalizedArrival)));
        }

        void LateUpdate()
        {
            if (!IsActive) return;
            SynchronizePause();
            if (AudioPaused) { LastReleaseDelta = 0; return; }
            // A normal unfocused player can stop ticking. Its first resumed
            // unscaled delta may include the whole focus gap, so discard that
            // delta and bound later presentation steps just like the visual clock.
            float step = skipReleaseDelta ? 0 : Mathf.Clamp(Time.unscaledDeltaTime, 0, MaximumClockStep);
            skipReleaseDelta = false;
            LastReleaseDelta = IsReleasing ? step : 0;
            if (!IsReleasing) return;
            // Advance even while the asynchronous destination loads. An unusually
            // long load cannot leave a rushing loop behind the black frame.
            releaseAge += step;
            ApplyRelease(Mathf.Max(ArrivalProgress, releaseAge / ReleaseSeconds));
        }

        void ApplyRelease(float progress)
        {
            ArrivalProgress = Mathf.Clamp01(progress);
            float remaining = 1 - Mathf.SmoothStep(0, 1, ArrivalProgress);
            Source.volume = releaseGain * remaining;
            Source.pitch = Mathf.Lerp(StartPitch, releasePitch, remaining);
            LowPass.cutoffFrequency = Mathf.Lerp(StartCutoff, releaseCutoff, remaining);
            if (ArrivalProgress >= 1) Finish();
        }

        public void SetPaused(bool paused)
        {
            explicitPaused = paused;
            SynchronizePause();
        }

        void SynchronizePause()
        {
            if (!IsActive || Source == null) return;
            bool pause = PlaybackSuspended;
            if (pause == AudioPaused) return;
            AudioPaused = pause;
            if (pause) { Source.Pause(); PauseTransitions++; }
            else { skipReleaseDelta = true; Source.UnPause(); ResumeTransitions++; }
        }

        public void Finish()
        {
            if (Source != null) { Source.volume = 0; Source.Stop(); }
            IsActive = IsReleasing = AudioPaused = false;
            ArrivalProgress = 1; Finished = true;
        }

        public void Cancel()
        {
            if (Source != null) { Source.volume = 0; Source.Stop(); }
            IsActive = IsReleasing = AudioPaused = false;
            Cancelled = true;
        }

        void OnApplicationFocus(bool value) { focused = value; SynchronizePause(); }
        void OnApplicationPause(bool value) { applicationPaused = value; SynchronizePause(); }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
    }
}
