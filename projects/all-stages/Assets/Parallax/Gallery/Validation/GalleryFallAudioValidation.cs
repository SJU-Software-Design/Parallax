using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Called only by the opt-in, silent, isolated native Gallery verifier.
    // PCM inspection and actual mixer/source state; no audio render/readback.
    public static class GalleryFallAudioValidation
    {
        static void Focus(GalleryFallAudio audio, bool value)
        {
            typeof(GalleryFallAudio).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(audio, new object[] { value });
        }

        public static IEnumerator Run(Action<bool, string, string> check)
        {
            var saved = (GameShellSettingsRuntime.Current ?? GameShellConfig.Defaults()).DeepCopy();
            float timeScale = Time.timeScale, listenerGain = AudioListener.volume;
            bool listenerPaused = AudioListener.pause;
            var host = new GameObject("Gallery fall audio validation fixture");
            AudioSource source = null;
            try
            {
                Time.timeScale = 1;
                GameShellSettingsRuntime.Apply(saved.DeepCopy(), false);
                var audio = GalleryFallAudio.Install(host); source = audio.Source;
                var clip = audio.Clip;
                check(clip != null && clip.channels == 1 && clip.frequency == 48000 &&
                    Mathf.Abs(clip.length - 16) < .001f && clip.samples >= 768000,
                    "Fall rush loads the actual 16-second pure wind PCM resource", clip == null ? "missing" : clip.name + " / " + clip.samples);
                var pcm = clip != null ? new float[clip.samples * clip.channels] : new float[0];
                bool readable = clip != null && clip.GetData(pcm, 0), finite = readable;
                double sum = 0; float peak = 0;
                foreach (float sample in pcm)
                {
                    finite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                    sum += sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                double rms = pcm.Length > 0 ? Math.Sqrt(sum / pcm.Length) : 0;
                check(finite && rms > .12 && rms < .15 && peak > .74f && peak < .76f &&
                    pcm.Length > 0 && Mathf.Abs(pcm[0]) < .000001f && Mathf.Abs(pcm[pcm.Length - 1]) < .000001f,
                    "Fall cue contains finite non-silent recorded PCM with silent anti-click edges",
                    "rms=" + rms + " peak=" + peak + "; no speaker output or subjective listening claim");
                check(audio.BeginFall(true) && audio.BeginCount == 1 && !source.loop && source.clip == clip &&
                    source.spatialBlend == 0 && source.dopplerLevel == 0 && source.playOnAwake == false,
                    "One non-looping two-dimensional voice starts for the fall", "begin=" + audio.BeginCount);
                yield return null;
                check(source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Sfx",
                    "The real falling cue routes through the shared Effects mixer", source.outputAudioMixerGroup == null ? "none" : source.outputAudioMixerGroup.name);
                audio.TickProgress(.2f); float low = audio.Gain, lowPitch = audio.Pitch, lowCutoff = audio.LowPass.cutoffFrequency;
                audio.TickProgress(.6f); float middle = audio.Gain;
                audio.TickProgress(.9f); float high = audio.Gain;
                check(low > 0 && middle > low * 5 && high > middle * 2 && high <= GalleryFallAudio.PeakGain &&
                    audio.Pitch > lowPitch && audio.Pitch < 1.05f && audio.LowPass.cutoffFrequency > lowCutoff * 3,
                    "Actual fall progress increases rush energy and brightness with only mild pitch change",
                    "gains=" + low + "," + middle + "," + high + " pitch=" + audio.Pitch);
                audio.SetPaused(true); float pausedProgress = audio.FallProgress, pausedGain = audio.Gain;
                audio.TickProgress(1); yield return null;
                check(audio.AudioPaused && audio.PlaybackSuspended && audio.FallProgress == pausedProgress && audio.Gain == pausedGain,
                    "Explicit pause freezes the actual fall cue envelope", "progress=" + audio.FallProgress);
                audio.SetPaused(false);
                check(!audio.AudioPaused && audio.PauseTransitions == 1 && audio.ResumeTransitions == 1 && audio.BeginCount == 1,
                    "Resume restores the same recorded voice without replay", "begin=" + audio.BeginCount);

                var config = saved.DeepCopy(); config.masterVolume = 1; config.effectsVolume = 1; config.musicVolume = 0; config.ambientVolume = 0;
                GameShellSettingsRuntime.Apply(config, false); yield return null; yield return null;
                var mixer = source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.audioMixer : null;
                float effectsDb = 99, masterDb = 99;
                if (mixer != null) mixer.GetFloat("SfxVolume", out effectsDb);
                check(mixer != null && Mathf.Abs(effectsDb) < .01f && audio.Gain == high,
                    "Music and Ambient mute preserve the independent Effects fall cue", "Sfx dB=" + effectsDb);
                config = config.DeepCopy(); config.effectsVolume = 0;
                GameShellSettingsRuntime.Apply(config, false); yield return null; yield return null;
                if (mixer != null) mixer.GetFloat("SfxVolume", out effectsDb);
                check(mixer != null && effectsDb <= -79 && audio.Gain == high,
                    "Effects mute silences the real cue bus while preserving its authored envelope", "Sfx dB=" + effectsDb);
                config = config.DeepCopy(); config.effectsVolume = 1; config.masterVolume = 0;
                GameShellSettingsRuntime.Apply(config, false); yield return null; yield return null;
                if (mixer != null) mixer.GetFloat("MasterVolume", out masterDb);
                check(mixer != null && masterDb <= -79 && audio.Gain == high,
                    "Master mute includes the real falling cue", "Master dB=" + masterDb);
                GameShellSettingsRuntime.Apply(saved.DeepCopy(), false);

                audio.BeginArrival(); Time.timeScale = 0;
                check(audio.IsReleasing && !audio.PlaybackSuspended,
                    "Beach's intentional reveal input lock does not freeze landing release", "timeScale=" + Time.timeScale);
                audio.SetPaused(true); float arrival = audio.ArrivalProgress;
                audio.TickArrival(.5f); yield return null;
                check(audio.AudioPaused && audio.ArrivalProgress == arrival && audio.Gain == high,
                    "Pause also holds the landing release envelope", "arrival=" + audio.ArrivalProgress);
                audio.SetPaused(false); audio.TickArrival(.2f); float releaseA = audio.Gain;
                audio.TickArrival(.6f); float releaseB = audio.Gain;
                audio.TickArrival(1);
                check(releaseA < high && releaseB < releaseA && releaseB > 0 && audio.Gain == 0 &&
                    !audio.IsActive && audio.Finished && !source.isPlaying && !source.loop && audio.BeginCount == 1,
                    "Landing releases smoothly to zero and stops the sole voice", "gains=" + high + "," + releaseA + "," + releaseB + "," + audio.Gain);
                Time.timeScale = 1;

                check(audio.BeginFall(false), "A fresh passage can restart the stopped cue", "begin=" + audio.BeginCount);
                Focus(audio, true); audio.TickProgress(.4f); Focus(audio, false);
                float focusedProgress = audio.FallProgress; audio.TickProgress(.8f);
                check(audio.PlaybackSuspended && audio.AudioPaused && audio.FallProgress == focusedProgress,
                    "Focus-loss callback freezes the same falling cue without OS input", "callback fixture only");
                Focus(audio, true);
                check(!audio.AudioPaused && !audio.PlaybackSuspended,
                    "Focus return resumes the existing cue", "callback fixture only");
                audio.BeginArrival(); Focus(audio, false);
                float releaseAgeBeforeFocus = audio.ReleaseAge, releaseGainBeforeFocus = audio.Gain;
                yield return null;
                check(audio.AudioPaused && audio.ReleaseAge == releaseAgeBeforeFocus && audio.Gain == releaseGainBeforeFocus,
                    "Managed focus loss also holds the actual automatic landing release", "callback fixture; no OS focus input");
                Focus(audio, true); yield return null;
                check(!audio.AudioPaused && audio.ReleaseAge == releaseAgeBeforeFocus && audio.LastReleaseDelta == 0,
                    "First focused release frame discards the potentially stale unscaled delta", "delta=" + audio.LastReleaseDelta);
                yield return null;
                check(audio.ReleaseAge > releaseAgeBeforeFocus && audio.LastReleaseDelta > 0 &&
                    audio.LastReleaseDelta <= GalleryFallAudio.MaximumClockStep && audio.Gain <= releaseGainBeforeFocus && audio.Gain > 0,
                    "Following release frames resume gradually with bounded presentation steps",
                    "delta=" + audio.LastReleaseDelta + " age=" + audio.ReleaseAge + " gain=" + audio.Gain);
                audio.Cancel();
                check(audio.Cancelled && !audio.IsActive && !source.isPlaying && source.volume == 0 && !source.loop,
                    "Cancellation removes all active fall audio immediately", "no remaining loop");
                check(audio.BeginFall(true), "A new transition may play after cancellation", "begin=" + audio.BeginCount);
                host.SetActive(false);
                check(!audio.IsActive && !source.isPlaying && source.volume == 0,
                    "Disabling the passage stops its voice", "no active source");
                host.SetActive(true); audio.BeginFall(true); audio.TickProgress(.7f);
            }
            finally
            {
                Time.timeScale = timeScale;
                GameShellSettingsRuntime.Apply(saved, false);
                if (host != null) UnityEngine.Object.Destroy(host);
            }
            yield return null;
            check(source == null && host == null && AudioListener.volume == listenerGain && AudioListener.pause == listenerPaused,
                "Destroy removes the child source and never changes listener state", "no orphan source or listener mutation");
        }
    }
}
