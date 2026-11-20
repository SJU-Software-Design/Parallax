using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // First-person boot contact on the Eclipse deck. Cadence and cue decisions
    // remain entirely in PlayerMovementAudio.Tick, shared with the other maps.
    public sealed class EclipseMovementAudio : MonoBehaviour
    {
        public const int SampleRate = 48000;
        public const string CreditTitle = "우주 갑판 발걸음";
        const int VoiceCount = 6;
        static EclipseMovementAudio instance;
        readonly List<AudioClip> clips = new List<AudioClip>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly bool[] wasPlaying = new bool[VoiceCount];
        CharacterController controller;
        int nextVoice, nextStep;
        bool suspended;
        public IReadOnlyList<AudioClip> Clips => clips;
        public IReadOnlyList<AudioSource> Voices => voices;
        public bool Ready => controller != null && clips.Count == 6 && voices.Count == VoiceCount;
        public PlayerMovementAudio.Cue LastCue { get; private set; }
        public int PlayedCount { get; private set; }
        public AudioSource LastVoice { get; private set; }

        public static EclipseMovementAudio Attach(CharacterController body)
        {
            if (body == null) return null;
            var value = body.GetComponent<EclipseMovementAudio>();
            if (value == null) value = body.gameObject.AddComponent<EclipseMovementAudio>();
            value.Initialize(body); return value;
        }
        void Initialize(CharacterController body)
        {
            if (controller != null) return;
            controller = body; instance = this;
            for (int i = 0; i < 4; i++) clips.Add(BootClip("Eclipse deck boot step " + (i + 1), .26f, i, false, false));
            clips.Add(BootClip("Eclipse deck boot takeoff", .18f, 4, true, false));
            clips.Add(BootClip("Eclipse deck boot landing", .34f, 5, false, true));
            for (int i = 0; i < VoiceCount; i++)
            {
                var node = new GameObject("Eclipse deck contact " + (i + 1)); node.transform.SetParent(transform, false);
                var source = node.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
                source.dopplerLevel = 0; source.priority = 150; source.volume = 0;
                voices.Add(source);
            }
        }
        public static void ResetFor(CharacterController body)
        { if (body != null) body.GetComponent<EclipseMovementAudio>()?.ResetVoices(); }
        public static bool TryPlayMovement(PlayerMovementAudio.Cue cue, float impact, Vector3 position)
        {
            var value = instance;
            var game = EclipseFinaleGame.Instance;
            if (!Application.isPlaying || value == null || !value.isActiveAndEnabled || !value.Ready || game == null ||
                value.gameObject.scene.name != "Eclipse" || game.body != value.controller ||
                cue == PlayerMovementAudio.Cue.None) return false;
            value.Play(cue, Mathf.Clamp01(impact)); return true;
        }
        void Play(PlayerMovementAudio.Cue cue, float impact)
        {
            int index = cue == PlayerMovementAudio.Cue.Footstep ? nextStep++ % 4 : cue == PlayerMovementAudio.Cue.Jump ? 4 : 5;
            // Each cue owns a separate voice, so a landing cannot retune the
            // still-decaying tail of the previous boot contact.
            var source = voices[nextVoice++ % VoiceCount];
            source.Stop(); source.clip = clips[index];
            source.pitch = cue == PlayerMovementAudio.Cue.Jump ? 1.12f : cue == PlayerMovementAudio.Cue.Land ? .92f : 1;
            source.volume = cue == PlayerMovementAudio.Cue.Land ? .07f + .14f * impact : cue == PlayerMovementAudio.Cue.Jump ? .045f : .075f;
            GameShellSettingsRuntime.Route(source); source.Play();
            LastCue = cue; LastVoice = source; PlayedCount++;
        }
        void OnEnable() { if (controller != null) instance = this; }
        void Update()
        {
            var game = EclipseFinaleGame.Instance;
            bool pause = game != null && (game.paused || game.PresentationSuspended);
            if (pause == suspended) return;
            suspended = pause;
            for (int i = 0; i < voices.Count; i++)
            {
                if (voices[i] == null) continue;
                if (pause) { wasPlaying[i] = voices[i].isPlaying; if (wasPlaying[i]) voices[i].Pause(); }
                else if (wasPlaying[i]) { GameShellSettingsRuntime.Route(voices[i]); voices[i].UnPause(); }
            }
        }
        void ResetVoices()
        {
            foreach (var voice in voices) if (voice != null) { voice.Stop(); voice.volume = 0; }
            for (int i = 0; i < wasPlaying.Length; i++) wasPlaying[i] = false;
            nextStep = nextVoice = PlayedCount = 0; suspended = false;
            LastCue = PlayerMovementAudio.Cue.None; LastVoice = null;
        }
        static AudioClip BootClip(string name, float duration, int variant, bool jump, bool land)
        {
            int count = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[count]; uint noise = 31031997u + (uint)variant * 747796405u;
            float filtered = 0, peak = 0;
            float fundamental = (land ? 53 : jump ? 84 : 64) * (1 + (variant - 1.5f) * .012f);
            for (int i = 0; i < count; i++)
            {
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float white = (noise & 65535) / 32767.5f - 1;
                filtered += .23f * (white - filtered);
                float t = (float)i / SampleRate, p = (float)i / (count - 1);
                float attack = 1 - Mathf.Exp(-t * 1400);
                float decay = land ? 20 : jump ? 38 : 26;
                float body = .72f * Mathf.Sin(2 * Mathf.PI * (fundamental * t + .32f * (1 - Mathf.Exp(-28 * t)))) * Mathf.Exp(-t * decay);
                float sole = .28f * filtered * Mathf.Exp(-t * 78);
                float deck = (.095f * Mathf.Sin(2 * Mathf.PI * (193 + variant * 3) * t) * Mathf.Exp(-t * 23) +
                    .052f * Mathf.Sin(2 * Mathf.PI * (327 + variant * 5) * t) * Mathf.Exp(-t * 34));
                float value = attack * (body + sole + deck) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .74f) / .26f)));
                data[i] = value; peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float gain = (land ? .70f : jump ? .50f : .62f) / Mathf.Max(.001f, peak);
            for (int i = 0; i < count; i++) data[i] *= gain;
            data[0] = data[count - 1] = 0;
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave; clip.SetData(data, 0); return clip;
        }
        void OnDisable() { ResetVoices(); if (instance == this) instance = null; }
        void OnDestroy() { foreach (var clip in clips) if (clip != null) Destroy(clip); if (instance == this) instance = null; }
    }
}