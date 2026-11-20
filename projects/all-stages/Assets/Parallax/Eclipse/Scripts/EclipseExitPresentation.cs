using UnityEngine;

namespace ShadeLink.Eclipse
{
    public enum EclipseExitPhase { Inactive, Enter, BlackThanks, BlackHold, Returning }

    // An actual i-opening traversal owns this sequence. Credits alone cannot start it.
    public sealed class EclipseExitPresentation : MonoBehaviour
    {
        public const float EnterDuration = 3.8f, ThanksDuration = 2.2f, BlackHoldDuration = 1f, ThanksFadeSeconds = .45f;
        public const string ClosingThanksText = "감사합니다";
        public const string WhooshResourceKey = "Beach/EndingAudio/flight";
        public EclipseExitPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public float BlackAlpha { get; private set; }
        public float ThanksAlpha { get; private set; }
        public float MusicFadeGain { get; private set; } = 1;
        public float ForwardDistance { get; private set; }
        public bool TitleRequested { get; private set; }
        public bool ReturnReady => Phase == EclipseExitPhase.BlackHold && Elapsed >= EnterDuration + ThanksDuration + BlackHoldDuration;
        public bool Active => Phase != EclipseExitPhase.Inactive;
        public AudioSource WhooshSource { get; private set; }
        EclipseFinaleGame game;
        Vector3 eye, forward;
        Quaternion rotation, entryRotation;
        float fov;
        bool soundPaused;
        AudioClip whooshClip;

        public void Initialize(EclipseFinaleGame owner)
        {
            game = owner;
            var node = new GameObject("Exit passage / soft selected wind"); node.transform.SetParent(transform, false);
            WhooshSource = node.AddComponent<AudioSource>();
            WhooshSource.playOnAwake = false; WhooshSource.loop = false; WhooshSource.spatialBlend = 0;
            WhooshSource.dopplerLevel = 0; WhooshSource.priority = 100; WhooshSource.volume = 0;
            var original = Resources.Load<AudioClip>(WhooshResourceKey);
            if (original != null)
            {
                whooshClip = Instantiate(original); whooshClip.name = "Eclipse passage whoosh";
                WhooshSource.clip = whooshClip;
            }
            GameShellSettingsRuntime.Route(WhooshSource);
            ResetPresentation();
        }
        public void ResetPresentation()
        {
            Phase = EclipseExitPhase.Inactive; Elapsed = BlackAlpha = ThanksAlpha = ForwardDistance = 0;
            TitleRequested = soundPaused = false; MusicFadeGain = 1;
            if (WhooshSource != null) { WhooshSource.Stop(); WhooshSource.volume = 0; }
        }
        public void BeginEntrance()
        {
            if (Active || game == null || !game.ExitUnlocked) return;
            eye = game.view.transform.position; rotation = game.view.transform.rotation; fov = game.view.fieldOfView;
            forward = game.exitSign.forward.normalized;
            entryRotation = Quaternion.LookRotation(forward + Vector3.up * .025f);
            Phase = EclipseExitPhase.Enter; Elapsed = BlackAlpha = ThanksAlpha = ForwardDistance = 0; MusicFadeGain = 1;
            if (WhooshSource.clip != null)
            {
                WhooshSource.time = Mathf.Min(2f, WhooshSource.clip.length * .2f);
                WhooshSource.pitch = .95f; WhooshSource.volume = 0;
                GameShellSettingsRuntime.Route(WhooshSource); WhooshSource.Play();
            }
        }
        void Update()
        {
            if (!Active || WhooshSource == null) return;
            bool suspend = game.paused || game.PresentationSuspended;
            if (suspend && !soundPaused) { if (WhooshSource.isPlaying) WhooshSource.Pause(); soundPaused = true; }
            else if (!suspend && soundPaused) { WhooshSource.UnPause(); soundPaused = false; }
        }
        public void TickExit(float dt)
        {
            if (!Active || game == null || game.paused || game.PresentationSuspended || TitleRequested) return;
            if (float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0;
            Elapsed += Mathf.Clamp(dt, 0, .1f);
            if (Phase == EclipseExitPhase.Enter)
            {
                float t = Mathf.Clamp01(Elapsed / EnterDuration);
                float motion = Mathf.SmoothStep(0, 1, t);
                ForwardDistance = 4.2f * motion;
                game.view.transform.position = eye + forward * ForwardDistance + Vector3.up * (.12f * Mathf.Sin(Mathf.PI * t));
                game.SetEndingRotation(Quaternion.Slerp(rotation, entryRotation, Mathf.SmoothStep(0, 1, t * 1.5f)));
                game.view.fieldOfView = fov;
                BlackAlpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Elapsed - 1.25f) / (EnterDuration - 1.25f)));
                float attack = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Elapsed / .9f));
                WhooshSource.volume = .24f * attack * (1 - BlackAlpha);
                WhooshSource.pitch = Mathf.Lerp(.95f, 1.10f, motion);
                if (Elapsed >= EnterDuration) { Phase = EclipseExitPhase.BlackThanks; BlackAlpha = 1; WhooshSource.Stop(); }
            }
            if (Phase == EclipseExitPhase.BlackThanks)
            {
                float age = Elapsed - EnterDuration;
                BlackAlpha = 1;
                MusicFadeGain = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - ThanksDuration + ThanksFadeSeconds) / ThanksFadeSeconds));
                ThanksAlpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / ThanksFadeSeconds)) * MusicFadeGain;
                if (age >= ThanksDuration)
                { Phase = EclipseExitPhase.BlackHold; MusicFadeGain = ThanksAlpha = 0; }
            }
            if (Phase == EclipseExitPhase.BlackHold)
            {
                BlackAlpha = 1; ThanksAlpha = MusicFadeGain = 0;
                if (ReturnReady)
                {
                    if (GameProgressRuntime.FinishEclipseEnding())
                    { TitleRequested = true; Phase = EclipseExitPhase.Returning; ThanksAlpha = 0; }
                    else AbortTitleReturn();
                }
            }
        }
        public void AbortTitleReturn()
        {
            Vector3 recoveryFeet = eye - Vector3.up * EclipseFinaleGame.EyeHeight - forward * .9f;
            Quaternion recoveryRotation = rotation;
            ResetPresentation();
            ParallaxGameShell.Instance?.CancelEndingTitleReveal();
            game.body.enabled = true; game.phase = FinalePhase.Chamber;
            game.Teleport(recoveryFeet); game.SetEndingRotation(recoveryRotation);
            game.Pause(); Time.timeScale = 0;
            ParallaxGameShell.Instance?.Notify("시작화면으로 돌아가지 못했습니다. 다시 나가거나 메뉴를 이용해 주세요.");
        }
        void OnDisable() { if (WhooshSource != null) WhooshSource.Stop(); }
        void OnDestroy() { if (whooshClip != null) Destroy(whooshClip); }
    }
}
