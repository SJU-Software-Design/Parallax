using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Tutorial-only audio bank. One-shot voices never interrupt an existing tail.
    public sealed class TutorialAudio : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource source;
            public string cue;
            public double until;
            public double pauseAt;
            public bool paused;
        }

        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "movement.footstep", "T14_Footstep01" }, { "movement.land", "T19_JumpLand" },
            { "prop.grab", "T02_Pickup" }, { "prop.release", "PropRelease156" },
            { "impact.small", "T04_DieSmall" }, { "impact.medium", "T07_DieMedium" }, { "impact.large", "T26_DieLarge" },
            { "door.open", "Door026" }, { "puzzle.complete", "PuzzleComplete036" },
            { "ui.confirm", "UIConfirm42" }, { "ui.cancel", "UICancel142" }, { "ui.error", "UIError145" }
        };
        static readonly Dictionary<string, float> Gains = new Dictionary<string, float>
        {
            { "movement.footstep", .22f }, { "movement.land", .34f },
            { "prop.grab", .24f }, { "prop.release", .30f },
            { "impact.small", .27f }, { "impact.medium", .32f }, { "impact.large", .34f },
            { "door.open", .32f }, { "puzzle.complete", .22f },
            { "ui.confirm", .22f }, { "ui.cancel", .22f }, { "ui.error", .20f }
        };
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<Voice> voices = new List<Voice>();
        AudioSource musicA, musicB, currentMusic, incomingMusic;
        AudioClip[] footstepClips;
        static readonly string[] FootstepFiles = { "T14_Footstep01", "T14_Footstep02", "T14_Footstep03", "T14_Footstep04" };
        bool gameplayRunning, crossfading, musicStarted;
        float crossfadeElapsed;
        int footstepIndex;
        const float MusicGain = .22f;
        const float CrossfadeSeconds = 1.5f;

        [SerializeField, Tooltip("Enable the tutorial's shared movement and prop effects. Music, UI, doors and puzzle cues are independent.")]
        bool commonEffectsEnabled = true;

        public bool CommonEffectsEnabled
        {
            get => commonEffectsEnabled;
            set
            {
                commonEffectsEnabled = value;
                if (value) return;
                foreach (var voice in voices)
                {
                    if (!IsCommonCue(voice.cue)) continue;
                    voice.source.Stop();
                    voice.cue = null;
                    voice.until = 0;
                    voice.pauseAt = 0;
                    voice.paused = false;
                }
            }
        }

        static bool IsCommonCue(string cue) => cue != null &&
            (cue.StartsWith("movement.", StringComparison.Ordinal) ||
             cue.StartsWith("prop.", StringComparison.Ordinal) ||
             cue.StartsWith("impact.", StringComparison.Ordinal));

        public static TutorialAudio Instance { get; private set; }
        public static bool Available => Instance != null && Instance.isActiveAndEnabled;

        public static TutorialAudio Install(GameObject root)
        {
            var audio = root.GetComponent<TutorialAudio>();
            if (audio == null) audio = root.AddComponent<TutorialAudio>();
            Instance = audio;
            audio.LoadBank();
            return audio;
        }

        void Awake()
        {
            Instance = this;
            LoadBank();
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--tutorial-common-sfx-off"))
                CommonEffectsEnabled = false;
        }

        void LoadBank()
        {
            foreach (var item in Files)
                if (!clips.ContainsKey(item.Key))
                    clips[item.Key] = LoadClip(item.Value);
            if (footstepClips == null)
            {
                footstepClips = new AudioClip[FootstepFiles.Length];
                for (int i = 0; i < FootstepFiles.Length; i++) footstepClips[i] = LoadClip(FootstepFiles[i]);
            }
            if (musicA == null)
            {
                // The imported clip contains only the user's authorized 0:00-2:59.
                var clip = LoadClip("BgmTB01_000_179");
                musicA = CreateMusicSource("Tutorial BGM TB01 A", clip);
                musicB = CreateMusicSource("Tutorial BGM TB01 B", clip);
                currentMusic = musicA;
                incomingMusic = musicB;
            }
        }

        static AudioClip LoadClip(string file) =>
            Resources.Load<AudioClip>("TutorialAudio/Refresh/" + file) ??
            Resources.Load<AudioClip>("TutorialAudio/Selected/" + file);

        public static string ImpactCueForSize(float size) =>
            size < 1f ? "impact.small" : size < 2f ? "impact.medium" : "impact.large";

        // These selected recordings already contain a short bounce/tail. Let one
        // contact finish before another solver contact retriggers the same prop.
        public static float ImpactRetriggerDelay(string cue, float pitch)
        {
            if (Available && Instance.clips.TryGetValue(cue, out var clip) && clip != null)
                return Mathf.Max(.12f, clip.length / Mathf.Clamp(pitch, .5f, 1.5f));
            return .18f;
        }

        AudioSource CreateMusicSource(string title, AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = title;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.priority = 80;
            source.volume = 0;
            source.clip = clip;
            return source;
        }

        public void SetGameplayRunning(bool value)
        {
            gameplayRunning = value;
            if (musicA == null || musicA.clip == null || !Application.isPlaying) return;
            if (value)
            {
                double now = AudioSettings.dspTime;
                foreach (var voice in voices)
                {
                    if (!voice.paused) continue;
                    voice.until += now - voice.pauseAt;
                    voice.paused = false;
                    voice.source.UnPause();
                }
                if (!musicStarted)
                {
                    currentMusic.time = 0;
                    currentMusic.volume = 0;
                    currentMusic.Play();
                    musicStarted = true;
                }
                else
                {
                    currentMusic.UnPause();
                    if (crossfading) incomingMusic.UnPause();
                }
            }
            else
            {
                double now = AudioSettings.dspTime;
                foreach (var voice in voices)
                {
                    if (voice.cue == null || voice.cue.StartsWith("ui.", StringComparison.Ordinal) || !voice.source.isPlaying) continue;
                    voice.source.Pause();
                    voice.paused = true;
                    voice.pauseAt = now;
                }
                if (musicA.isPlaying) musicA.Pause();
                if (musicB.isPlaying) musicB.Pause();
            }
        }

        void Update()
        {
            if (!gameplayRunning || currentMusic == null || currentMusic.clip == null || !musicStarted) return;
            if (!crossfading && !currentMusic.isPlaying)
            {
                currentMusic.time = 0;
                currentMusic.volume = 0;
                currentMusic.Play();
            }
            float overlap = Mathf.Min(CrossfadeSeconds, currentMusic.clip.length * .2f);
            if (!crossfading && currentMusic.time >= currentMusic.clip.length - overlap)
            {
                incomingMusic.time = 0;
                incomingMusic.volume = 0;
                incomingMusic.Play();
                crossfading = true;
                crossfadeElapsed = 0;
            }
            if (!crossfading)
            {
                currentMusic.volume = MusicGain;
                return;
            }
            crossfadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(crossfadeElapsed / overlap);
            currentMusic.volume = MusicGain * Mathf.Cos(t * Mathf.PI * .5f);
            incomingMusic.volume = MusicGain * Mathf.Sin(t * Mathf.PI * .5f);
            if (t >= 1)
            {
                currentMusic.Stop();
                var previous = currentMusic;
                currentMusic = incomingMusic;
                incomingMusic = previous;
                crossfading = false;
            }
        }

        public static bool TryPlayMovement(PlayerMovementAudio.Cue cue, float impact, Vector3 position)
        {
            if (!Available) return false;
            if (cue == PlayerMovementAudio.Cue.None || cue == PlayerMovementAudio.Cue.Jump) return true;
            float variation = 1f;
            AudioClip movementClip = null;
            if (cue == PlayerMovementAudio.Cue.Footstep)
            {
                int index = Instance.footstepIndex++;
                variation = 1f + (index % 3 - 1) * .018f;
                if (Instance.footstepClips != null && Instance.footstepClips.Length > 0)
                    movementClip = Instance.footstepClips[index % Instance.footstepClips.Length];
            }
            return Play(cue == PlayerMovementAudio.Cue.Land ? "movement.land" : "movement.footstep",
                Instance, position, cue == PlayerMovementAudio.Cue.Land ? Mathf.Lerp(.72f, 1.15f, impact) : 1f, variation, movementClip);
        }

        public static bool Play(string cue, Component owner, Vector3 position, float volume = 1f, float pitch = 1f, AudioClip clipOverride = null)
        {
            if (!Available) return false;
            var audio = Instance;
            // Consume disabled cues so the shared caller does not fall back to a synthetic sound.
            if (!audio.commonEffectsEnabled && IsCommonCue(cue)) return true;
            if (!audio.clips.TryGetValue(cue, out var clip) || clip == null) return true;
            if (clipOverride != null) clip = clipOverride;
            bool ui = cue.StartsWith("ui.", StringComparison.Ordinal);
            if (!Application.isPlaying || (!audio.gameplayRunning && !ui)) return true;
            double now = AudioSettings.dspTime;
            Voice voice = null;
            foreach (var candidate in audio.voices)
                if (!candidate.paused && now >= candidate.until) { voice = candidate; break; }
            if (voice == null)
            {
                if (audio.voices.Count >= 32) return true;
                var child = new GameObject("Tutorial audio voice " + audio.voices.Count);
                child.transform.SetParent(audio.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 2f; source.maxDistance = 38f;
                source.bypassReverbZones = true;
                voice = new Voice { source = source };
                audio.voices.Add(voice);
            }
            voice.cue = cue;
            voice.source.transform.position = position;
            voice.source.clip = clip;
            voice.source.pitch = Mathf.Clamp(pitch, .5f, 1.5f);
            voice.source.volume = Mathf.Clamp01(Gains[cue] * volume);
            voice.source.spatialBlend = cue.StartsWith("ui.", StringComparison.Ordinal) ||
                cue.StartsWith("movement.", StringComparison.Ordinal) ? 0f : .55f;
            voice.source.priority = ui ? 100 : cue.StartsWith("movement.", StringComparison.Ordinal) ? 160 : 110;
            voice.until = now + clip.length / voice.source.pitch;
            voice.source.Play();
            return true;
        }

        public static void StopAll()
        {
            if (!Available) return;
            foreach (var voice in Instance.voices) { voice.source.Stop(); voice.until = 0; }
            if (Instance.musicA != null) Instance.musicA.Stop();
            if (Instance.musicB != null) Instance.musicB.Stop();
            Instance.crossfading = false;
            Instance.musicStarted = false;
            Instance.gameplayRunning = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
