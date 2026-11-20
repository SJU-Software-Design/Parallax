using UnityEngine;

namespace ShadeLink.Eclipse
{
    // The approved full recording remains intact. Only an actual EXIT traversal
    // crossfades it into an edit of the same recording's final chord and decay.
    [DefaultExecutionOrder(1000)]
    public sealed class EclipseEndingMusic : MonoBehaviour
    {
        public const string ResourceKey = "Eclipse/Audio/CreditsAurora";
        public const string CodaResourceKey = "Eclipse/Audio/AuroraClosingCoda";
        public const float BaseGain = .24f, FadeInSeconds = 3f, LoopCrossfadeSeconds = 1.5f;
        public const float ClosingCrossfadeSeconds = 1.2f;
        public AudioSource MusicSource => current;
        public AudioSource IncomingSource => incoming;
        public AudioSource CodaSource { get; private set; }
        public AudioClip Clip { get; private set; }
        public AudioClip CodaClip { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public float FadeGain { get; private set; }
        public bool LoopCrossfading { get; private set; }
        public int LoopCount { get; private set; }
        public bool Closing { get; private set; }
        public float ClosingFadeGain { get; private set; } = 1;
        public float CodaCrossfadeProgress { get; private set; }
        public float ClosingElapsed => game != null && game.ExitPresentation != null ? game.ExitPresentation.Elapsed : 0;
        EclipseFinaleGame game;
        AudioSource first, second, current, incoming;
        float fadeAge, overlapAge, closingFirstVolume, closingSecondVolume, closingResumeTime, lastClosingElapsed;
        bool firstWasPlaying, secondWasPlaying, codaWasPlaying;

        public void Initialize(EclipseFinaleGame owner)
        {
            if (game != null || owner == null) return;
            game = owner;
            Clip = Resources.Load<AudioClip>(ResourceKey);
            CodaClip = Resources.Load<AudioClip>(CodaResourceKey);
            if (Clip == null || CodaClip == null)
            { Debug.LogError("ECLIPSE_MUSIC: Approved Aurora recording or its closing edit is missing."); return; }
            Clip.name = "Aurora · Eclipse Credits BGM";
            CodaClip.name = "Aurora · Eclipse Closing Coda BGM";
            first = Voice("Eclipse credits music A", Clip);
            second = Voice("Eclipse credits music B", Clip);
            CodaSource = Voice("Eclipse closing coda music", CodaClip);
            current = first; incoming = second;
            StopMusic();
        }
        AudioSource Voice(string title, AudioClip clip)
        {
            var node = new GameObject(title); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.pitch = 1; source.dopplerLevel = 0; source.priority = 80;
            source.volume = 0; source.clip = clip;
            GameShellSettingsRuntime.Route(source);
            return source;
        }
        void PlayFromStart(AudioSource source)
        {
            source.Stop(); source.time = 0; source.pitch = 1; source.volume = 0;
            GameShellSettingsRuntime.Route(source); source.Play();
        }
        void Update() { TickMusic(Time.deltaTime); }
        public void TickMusic(float dt)
        {
            if (game == null || Clip == null || first == null || second == null || game.Credits == null) return;
            if (game.Credits.Section == EndingCreditSection.Inactive)
            { if (Started) StopMusic(); return; }
            if (Closing && (game.ExitPresentation == null || !game.ExitPresentation.Active)) RecoverAfterAbortedReturn();
            if (game.paused || game.PresentationSuspended)
            {
                if (Started && !Paused)
                {
                    firstWasPlaying = first.isPlaying; secondWasPlaying = second.isPlaying;
                    codaWasPlaying = CodaSource != null && CodaSource.isPlaying;
                    if (firstWasPlaying) first.Pause();
                    if (secondWasPlaying) second.Pause();
                    if (codaWasPlaying) CodaSource.Pause();
                    Paused = true;
                }
                return;
            }
            if (Paused)
            {
                if (firstWasPlaying) { GameShellSettingsRuntime.Route(first); first.UnPause(); }
                if (secondWasPlaying) { GameShellSettingsRuntime.Route(second); second.UnPause(); }
                if (codaWasPlaying) { GameShellSettingsRuntime.Route(CodaSource); CodaSource.UnPause(); }
                Paused = false;
            }
            var ambience = GetComponent<EclipseEndingAmbience>();
            if (!Started && ambience != null && !ambience.ReadyForCreditsMusic) return;
            if (!Started)
            {
                current = first; incoming = second; fadeAge = overlapAge = 0;
                LoopCrossfading = false; Started = true;
                PlayFromStart(current);
            }
            if (float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0;
            dt = Mathf.Clamp(dt, 0, .1f);
            fadeAge += dt;
            FadeGain = Mathf.SmoothStep(0, 1, fadeAge / FadeInSeconds);
            if (!Closing && game.ExitPresentation != null && game.ExitPresentation.Active) BeginClosing();
            if (Closing) { TickClosing(); return; }

            float overlap = Mathf.Min(LoopCrossfadeSeconds, Clip.length * .2f);
            if (!LoopCrossfading && current.time >= Clip.length - overlap)
            { PlayFromStart(incoming); LoopCrossfading = true; overlapAge = 0; }
            if (LoopCrossfading)
            {
                overlapAge += dt;
                float t = Mathf.Clamp01(overlapAge / Mathf.Max(.01f, overlap));
                current.volume = BaseGain * FadeGain * Mathf.Cos(t * Mathf.PI * .5f);
                incoming.volume = BaseGain * FadeGain * Mathf.Sin(t * Mathf.PI * .5f);
                if (t >= 1)
                {
                    current.Stop(); current.volume = 0;
                    var previous = current; current = incoming; incoming = previous;
                    LoopCrossfading = false; LoopCount++;
                }
            }
            else
            {
                if (!current.isPlaying && !game.QuitRequested) PlayFromStart(current);
                current.volume = BaseGain * FadeGain; incoming.volume = 0;
            }
        }
        void BeginClosing()
        {
            if (CodaSource == null || CodaClip == null) return;
            Closing = true; CodaCrossfadeProgress = 0; ClosingFadeGain = 1;
            closingFirstVolume = first.volume; closingSecondVolume = second.volume;
            closingResumeTime = current.time; lastClosingElapsed = 0;
            CodaSource.Stop(); CodaSource.pitch = 1; CodaSource.volume = 0;
            // Only the silent independent voice seeks; the playing full song is
            // neither stopped nor jumped and retains its current loop overlap.
            CodaSource.time = Mathf.Clamp(ClosingElapsed, 0, Mathf.Max(0, CodaClip.length - .02f));
            GameShellSettingsRuntime.Route(CodaSource); CodaSource.Play();
        }
        void TickClosing()
        {
            var exit = game.ExitPresentation;
            lastClosingElapsed = ClosingElapsed;
            ClosingFadeGain = exit == null ? 1 : exit.MusicFadeGain;
            FadeGain = ClosingFadeGain;
            CodaCrossfadeProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(ClosingElapsed / ClosingCrossfadeSeconds));
            float angle = CodaCrossfadeProgress * Mathf.PI * .5f;
            first.volume = closingFirstVolume * Mathf.Cos(angle) * ClosingFadeGain;
            second.volume = closingSecondVolume * Mathf.Cos(angle) * ClosingFadeGain;
            CodaSource.volume = BaseGain * Mathf.Sin(angle) * ClosingFadeGain;
            if (CodaCrossfadeProgress >= 1)
            { first.Stop(); second.Stop(); first.volume = second.volume = 0; }
            if (ClosingFadeGain <= 0)
            { CodaSource.Stop(); CodaSource.volume = 0; }
        }
        void RecoverAfterAbortedReturn()
        {
            float resume = Mathf.Repeat(closingResumeTime + lastClosingElapsed, Mathf.Max(.01f, Clip.length));
            CodaSource.Stop(); CodaSource.volume = 0;
            first.Stop(); second.Stop(); first.volume = second.volume = 0;
            current = first; incoming = second;
            Paused = firstWasPlaying = secondWasPlaying = codaWasPlaying = false;
            current.time = resume; GameShellSettingsRuntime.Route(current); current.Play();
            Closing = LoopCrossfading = false; CodaCrossfadeProgress = 0; ClosingFadeGain = 1;
            fadeAge = overlapAge = FadeGain = 0;
        }
        void StopMusic()
        {
            if (first != null) { first.Stop(); first.volume = 0; }
            if (second != null) { second.Stop(); second.volume = 0; }
            if (CodaSource != null) { CodaSource.Stop(); CodaSource.volume = 0; }
            Started = Paused = LoopCrossfading = Closing = false;
            FadeGain = fadeAge = overlapAge = CodaCrossfadeProgress = 0; ClosingFadeGain = 1; LoopCount = 0;
            firstWasPlaying = secondWasPlaying = codaWasPlaying = false;
        }
        void OnDisable() { StopMusic(); }
        void OnDestroy() { StopMusic(); }
    }
}