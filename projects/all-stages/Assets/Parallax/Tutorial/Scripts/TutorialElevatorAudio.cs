using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Owns only this cabin's selected voices. The cabin survives the actual
    // Tutorial -> Gallery load, so loops and pause positions survive with it.
    public sealed class TutorialElevatorAudio : MonoBehaviour
    {
        [Serializable]
        public struct Playback
        {
            public string id, clip;
            public double dspTime;
            public ElevatorPhase phase;
            public int sample;
            public float volume;
        }

        sealed class ClipSlot
        {
            public string id;
            public AudioClip clip;
            public AudioSource source;
            public int count;
        }

        const string ResourceFolder = "Tutorial/ElevatorAudio/";
        public const float DoorGainReductionDb = -6;
        public static float DoorPlaybackGain => .44f * Mathf.Pow(10, DoorGainReductionDb / 20);
        readonly Dictionary<string, ClipSlot> slots = new Dictionary<string, ClipSlot>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly HashSet<AudioSource> pausedSources = new HashSet<AudioSource>();
        readonly List<Playback> history = new List<Playback>();
        TutorialElevatorRide ride;
        AudioSource door, latch, start, run, stop, bell, vent;
        bool initialized, audioPaused, doorFading;
        float runTarget, runAge, doorGain;

        public bool AudioPaused => audioPaused;
        public int PauseTransitions { get; private set; }
        public int ResumeTransitions { get; private set; }
        public int ResetCount { get; private set; }
        public int SourceCount => sources.Count;
        public int LoadedClipCount { get { int count = 0; foreach (var slot in slots.Values) if (slot.clip != null) count++; return count; } }
        public bool AllClipsLoaded => LoadedClipCount == 8;
        public IReadOnlyList<Playback> PlaybackHistory => history;
        public int PlayCount(string id) => slots.TryGetValue(id, out var slot) ? slot.count : 0;
        public AudioSource SourceFor(string id) => slots.TryGetValue(id, out var slot) ? slot.source : null;
        public AudioClip ClipFor(string id) => slots.TryGetValue(id, out var slot) ? slot.clip : null;

        public void Initialize(TutorialElevatorRide owner)
        {
            if (ride != null && ride != owner) throw new InvalidOperationException("Elevator audio is already bound");
            if (initialized) return;
            ride = owner;
            Vector3 middle = owner.Asset != null ? owner.Asset.cabinBounds.center : new Vector3(0, 1.4f, -1.2f);
            Vector3 doorway = new Vector3(middle.x, 1.25f, 0);
            door = Voice("Elevator sliding door", doorway, 10, false, 48);
            latch = Voice("Elevator closed latch", doorway, 9, false, 52);
            start = Voice("Elevator motor startup", middle, 9, false, 58);
            run = Voice("Elevator running motor", middle, 8, true, 64);
            stop = Voice("Elevator selected EL25 stop", middle, 9, false, 56);
            bell = Voice("Elevator arrival bell", new Vector3(middle.x, 2.35f, -.15f), 11, false, 48);
            vent = Voice("Elevator ventilation", new Vector3(middle.x, 2.45f, middle.z), 7, true, 150);
            Load("EL02", "EL02_DoorOpen", door);
            Load("EL04", "EL04_DoorClose", door);
            Load("EL07", "EL07_DoorLatch", latch);
            Load("EL09", "EL09_MotorStart", start);
            Load("EL11", "EL11_MotorRun", run);
            Load("EL25", "EL25_MotorStop", stop);
            Load("EL21", "EL21_ArrivalBell", bell);
            Load("EL23", "EL23_Ventilation", vent);
            ride.PhaseChanged += OnPhaseChanged;
            ride.RideReset += OnRideReset;
            initialized = true;
        }

        AudioSource Voice(string label, Vector3 local, float range, bool loop, int priority)
        {
            var node = new GameObject(label);
            node.transform.SetParent(transform, false);
            node.transform.localPosition = local;
            var source = node.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.25f; source.maxDistance = range;
            source.dopplerLevel = 0; source.spread = loop ? 100 : 45;
            source.volume = 0; source.priority = priority;
            sources.Add(source);
            return source;
        }

        void Load(string id, string resource, AudioSource voice)
        {
            var clip = Resources.Load<AudioClip>(ResourceFolder + resource);
            slots.Add(id, new ClipSlot { id = id, clip = clip, source = voice });
            if (clip == null) Debug.LogWarning("ELEVATOR_AUDIO_MISSING " + ResourceFolder + resource);
        }

        void OnRideReset()
        {
            StopEverything();
            ResetCount++;
            Play("EL23", .075f);
            SynchronizePause();
        }

        void OnPhaseChanged(ElevatorPhase previous, ElevatorPhase next)
        {
            switch (next)
            {
                case ElevatorPhase.OpeningForBoarding:
                case ElevatorPhase.OpeningAtDestination:
                    PlayDoor("EL02", ride.DoorOpenFraction);
                    break;
                case ElevatorPhase.Boarding:
                    doorFading = true;
                    break;
                case ElevatorPhase.ClosingForDeparture:
                    PlayDoor("EL04", 1 - ride.DoorOpenFraction);
                    break;
                case ElevatorPhase.Descending:
                    StopVoice(door);
                    doorFading = false;
                    Play("EL07", .32f);
                    Play("EL09", .38f);
                    runAge = 0;
                    runTarget = .25f;
                    Play("EL11", 0);
                    break;
                case ElevatorPhase.Stopping:
                    runTarget = 0;
                    Play("EL25", .34f);
                    break;
                case ElevatorPhase.ArrivalChime:
                    Play("EL21", .29f);
                    break;
                case ElevatorPhase.Arrived:
                    doorFading = true;
                    runTarget = 0;
                    break;
            }
        }

        void PlayDoor(string id, float progress)
        {
            StopVoice(door);
            doorFading = false;
            doorGain = DoorPlaybackGain;
            Play(id, doorGain, progress);
        }

        void Play(string id, float gain, float progress = 0)
        {
            if (!slots.TryGetValue(id, out var slot) || slot.clip == null) return;
            StopVoice(slot.source);
            slot.source.clip = slot.clip;
            slot.source.volume = gain;
            slot.source.pitch = 1;
            if (!slot.source.loop)
                slot.source.time = Mathf.Clamp(progress, 0, .995f) * slot.clip.length;
            slot.source.Play();
            slot.count++;
            history.Add(new Playback { id = id, clip = slot.clip.name, dspTime = AudioSettings.dspTime,
                phase = ride.Phase, sample = slot.source.timeSamples, volume = gain });
            SynchronizePause();
        }

        void LateUpdate()
        {
            if (!initialized) return;
            SynchronizePause();
            if (audioPaused) return;
            float dt = Time.deltaTime;
            if (run != null && run.isPlaying)
            {
                runAge += dt;
                float startupLength = ClipFor("EL09") != null ? ClipFor("EL09").length : 1;
                float target = runAge < startupLength * .55f ? 0 : runTarget;
                run.volume = Mathf.MoveTowards(run.volume, target, dt * .25f / (runTarget > 0 ? .4f : .18f));
                if (runTarget <= 0 && run.volume <= .0001f) StopVoice(run);
            }
            if (doorFading && door != null)
            {
                door.volume = Mathf.MoveTowards(door.volume, 0, dt * doorGain / .04f);
                if (door.volume <= .0001f) { StopVoice(door); doorFading = false; }
            }
        }

        void SynchronizePause()
        {
            if (!initialized || ride == null) return;
            bool pause = ride.Paused || Time.timeScale <= 0;
            if (pause)
            {
                if (!audioPaused) PauseTransitions++;
                audioPaused = true;
                foreach (var source in sources)
                    if (source != null && source.isPlaying && pausedSources.Add(source)) source.Pause();
            }
            else if (audioPaused)
            {
                audioPaused = false;
                ResumeTransitions++;
                foreach (var source in pausedSources) if (source != null) source.UnPause();
                pausedSources.Clear();
            }
        }

        void StopVoice(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            pausedSources.Remove(source);
        }

        void StopEverything()
        {
            foreach (var source in sources) if (source != null) source.Stop();
            pausedSources.Clear(); audioPaused = false;
            runTarget = runAge = 0; doorFading = false;
        }

        void OnDisable() { StopEverything(); }
        void OnDestroy()
        {
            if (ride != null) { ride.PhaseChanged -= OnPhaseChanged; ride.RideReset -= OnRideReset; }
            StopEverything();
        }
    }
}
