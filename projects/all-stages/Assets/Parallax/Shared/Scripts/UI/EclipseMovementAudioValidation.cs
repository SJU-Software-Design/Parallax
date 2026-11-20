using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Explicit isolated native fixtures. This observes decoded samples, routing
    // and actual shared cue decisions; it never synthesizes OS input or captures.
    public static class EclipseMovementAudioValidation
    {
        [Serializable] sealed class SourceGuard
        {
            public bool passed, sharedMovementCadenceAndOtherRoutesBytePreserved, sharedMovementCadenceAndOtherRoutesTextPreserved;
            public string hashMode, sharedMovementSourcePath, sharedMovementOriginalPath;
            public string sharedMovementSourceSha256, sharedMovementOriginalSha256;
        }
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
        static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .00001f;
        public static void Run(EclipseFinaleGame game, Action<bool, string, string> check)
        {
            check(Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-test") >= 0 && Application.isBatchMode &&
                !GameProgress.IsUnisolatedValidation,
                "Eclipse movement/audio fixtures run only in the explicit isolated batch player", "Muted source data and real cue routing; no listening claim");
            check(game != null && game.Credits != null && game.Credits.Section == EndingCreditSection.Inactive && game.held == FinaleItem.None,
                "Movement/audio fixtures begin in the actual unsolved Eclipse opening", "");
            bool automated = game.automated, started = game.started, paused = game.paused;
            string configBefore = JsonUtility.ToJson(GameShellSettingsRuntime.Current);
            try
            {
                game.automated = true; game.ResetGame(true); game.Play();
                var common = game.MovementAudio;
                var audio = game.body.GetComponent<EclipseMovementAudio>();
                check(common != null && audio != null && audio.Ready && audio.Clips.Count == 6 && audio.Voices.Count == 6 &&
                    audio.Voices.Distinct().Count() == 6 && audio.Voices.All(voice => voice != null && !voice.playOnAwake && !voice.loop &&
                        voice.spatialBlend == 0 && voice.dopplerLevel == 0),
                    "The actual Eclipse controller owns six independent deck-contact voices and six actual boot clips", "4 steps, takeoff and landing; common cadence unchanged");
                foreach (var clip in audio.Clips)
                {
                    SampleClip(clip, false, check);
                    check(GameShellSettingsRuntime.AudioCategory(clip.name) == "Sfx" && clip.name != "Parallax soft movement",
                        "A real Eclipse boot waveform belongs to SFX rather than the old soft fallback: " + clip.name, "");
                }
                check(audio.Clips.Take(4).All(clip => Near(clip.length, .26f)) && Near(audio.Clips[4].length, .18f) && Near(audio.Clips[5].length, .34f),
                    "The deck cues have authored short boot attacks and restrained 0.18-to-0.34-second tails", "");
                foreach (var voice in audio.Voices) GameShellSettingsRuntime.Route(voice);
                check(audio.Voices.All(voice => voice.outputAudioMixerGroup != null && voice.outputAudioMixerGroup.name == "Sfx" &&
                    GameShellSettingsRuntime.AudioCategory(voice.clip == null ? "" : voice.clip.name, voice.name) == "Sfx"),
                    "All six independent contact sources route through the shared Effects mixer bus", "No settings mutation or Ambient/Music routing");
                common.Reset(); EclipseMovementAudio.ResetFor(game.body);
                check(common.Tick(Vector3.zero, true, false, .8f) == PlayerMovementAudio.Cue.None && audio.PlayedCount == 0,
                    "Idle standing produces neither a common cue nor an Eclipse sound", "");
                check(common.Tick(Vector3.forward * 4, true, true, .42f) == PlayerMovementAudio.Cue.None && audio.PlayedCount == 0,
                    "Actual shared movement does not play a deck step before its existing 0.43-second interval", "");
                check(common.Tick(Vector3.forward * 4, true, true, .02f) == PlayerMovementAudio.Cue.Footstep && audio.PlayedCount == 1 &&
                    audio.LastCue == PlayerMovementAudio.Cue.Footstep && audio.Clips.Take(4).Contains(audio.LastVoice.clip) && Near(audio.LastVoice.volume, .075f),
                    "Crossing the common interval plays the real Eclipse step through the new scene-specific route", "");
                var stepVoice = audio.LastVoice;
                int count = audio.PlayedCount;
                check(common.Tick(Vector3.forward * 4, true, false, 1f) == PlayerMovementAudio.Cue.None && audio.PlayedCount == count,
                    "Support drift without movement input cannot add an Eclipse step", "");
                check(common.Tick(Vector3.up * 6, false, false, .01f) == PlayerMovementAudio.Cue.Jump && audio.PlayedCount == 2 &&
                    audio.LastCue == PlayerMovementAudio.Cue.Jump && audio.LastVoice.clip == audio.Clips[4] && audio.LastVoice != stepVoice,
                    "The real shared takeoff cue uses its own deck boot voice without retuning the previous step", "");
                var jumpVoice = audio.LastVoice;
                check(common.Tick(Vector3.down * 8, false, false, .1f) == PlayerMovementAudio.Cue.None && audio.PlayedCount == 2,
                    "Airborne fall adds no phantom deck contacts", "");
                check(common.Tick(Vector3.zero, true, false, .01f) == PlayerMovementAudio.Cue.Land && audio.PlayedCount == 3 &&
                    audio.LastCue == PlayerMovementAudio.Cue.Land && audio.LastVoice.clip == audio.Clips[5] &&
                    audio.LastVoice != stepVoice && audio.LastVoice != jumpVoice && Near(audio.LastVoice.volume, .126f),
                    "Actual shared landing drives the independent weighted deck impact with the existing impact gain", "last vertical=-8, impact=.4");
                count = audio.PlayedCount;
                check(common.Tick(Vector3.forward * 4, true, true, 0) == PlayerMovementAudio.Cue.None &&
                    common.Tick(Vector3.zero, true, false, 1f) == PlayerMovementAudio.Cue.None && audio.PlayedCount == count,
                    "Paused/idle common ticks add no Eclipse audio playback", "");
                check(new[] { stepVoice, jumpVoice, audio.LastVoice }.All(voice => voice.outputAudioMixerGroup != null &&
                    voice.outputAudioMixerGroup.name == "Sfx" && voice.clip != null && voice.clip.name.StartsWith("Eclipse deck boot ", StringComparison.Ordinal)),
                    "All three actually triggered cue types use new deck recordings and the Effects bus", "");
                EclipseMovementAudio.ResetFor(game.body);
                check(audio.PlayedCount == 0 && audio.LastCue == PlayerMovementAudio.Cue.None &&
                    audio.Voices.All(voice => !voice.isPlaying && voice.volume == 0),
                    "Eclipse reset stops the independent boot tails and clears cue state", "");
                var music = game.GetComponent<EclipseEndingMusic>();
                var coda = Resources.Load<AudioClip>(EclipseEndingMusic.CodaResourceKey);
                check(music != null && coda != null && music.CodaClip == coda && Near(coda.length, 6) && coda.channels == 2 &&
                    coda.frequency == 48000 && coda.loadType == AudioClipLoadType.DecompressOnLoad &&
                    music.CodaSource != null && music.CodaSource.clip == coda && music.CodaSource.outputAudioMixerGroup != null &&
                    music.CodaSource.outputAudioMixerGroup.name == "Music",
                    "The actual six-second stereo Aurora closing edit is losslessly decoded and immediately routed as Music", "");
                SampleClip(coda, true, check);
                ValidateSharedSourceGuard(check);
                check(JsonUtility.ToJson(GameShellSettingsRuntime.Current) == configBefore,
                    "Movement/audio waveform and routing probes preserve all applied user settings", "");
            }
            finally
            {
                game.ResetGame(started);
                if (paused) game.Pause(); else if (started) game.Play();
                game.automated = automated;
            }
        }
        static void SampleClip(AudioClip clip, bool coda, Action<bool, string, string> check)
        {
            check(clip != null && clip.samples > 0 && clip.channels == (coda ? 2 : 1) && clip.frequency == 48000,
                "Actual audio sample metadata is complete: " + (clip == null ? "<missing>" : clip.name), "");
            var samples = new float[clip.samples * clip.channels];
            check(clip.GetData(samples, 0), "Actual decoded sample data is readable: " + clip.name, "No camera or audio-device capture");
            double square = 0; float peak = 0; int clipped = 0; bool finite = true;
            foreach (float value in samples)
            {
                finite &= !float.IsNaN(value) && !float.IsInfinity(value);
                peak = Mathf.Max(peak, Mathf.Abs(value)); square += value * value;
                if (Mathf.Abs(value) >= .9999f) clipped++;
            }
            float rms = (float)Math.Sqrt(square / samples.Length);
            bool endpointsZero = Enumerable.Range(0, clip.channels).All(channel =>
                Mathf.Abs(samples[channel]) <= .000001f && Mathf.Abs(samples[samples.Length - clip.channels + channel]) <= .000001f);
            check(finite && clipped == 0 && endpointsZero && peak > .1f && peak <= (coda ? .51f : .71f) && rms > .01f && rms < .4f,
                "Actual decoded waveform is non-silent, unclipped and reaches zero at both edges: " + clip.name,
                "seconds=" + clip.length + " peak=" + peak + " RMS=" + rms + " clipped=" + clipped + " endpointsZero=" + endpointsZero);
        }
        static void ValidateSharedSourceGuard(Action<bool, string, string> check)
        {
            string path = Arg("-eclipse-audio-source-guard");
            check(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && File.Exists(path),
                "The native audio regression receives its explicit inspected shared-source guard", path ?? "<missing>");
            var guard = JsonUtility.FromJson<SourceGuard>(File.ReadAllText(path));
            bool normalized = guard != null && guard.hashMode == "utf8-lf";
            string currentPath = ResolveSourcePath(path, guard == null ? null : guard.sharedMovementSourcePath);
            string originalPath = ResolveSourcePath(path, guard == null ? null : guard.sharedMovementOriginalPath);
            check(guard != null && guard.passed &&
                (normalized ? guard.sharedMovementCadenceAndOtherRoutesTextPreserved :
                    string.IsNullOrEmpty(guard.hashMode) && guard.sharedMovementCadenceAndOtherRoutesBytePreserved) &&
                File.Exists(currentPath) && File.Exists(originalPath),
                normalized ? "The portable audio guard refers to the current shared source and captured pre-Eclipse source transcript" :
                    "The audio source guard refers to preserved actual current and pre-change shared movement sources", "");
            byte[] now = File.ReadAllBytes(currentPath), original = File.ReadAllBytes(originalPath);
            if (normalized)
            {
                now = NormalizeSourceText(now); original = NormalizeSourceText(original);
            }
            const string insertion = "            if(ShadeLink.Eclipse.EclipseMovementAudio.TryPlayMovement(cue,impact,transform.position))return;";
            string text = Encoding.UTF8.GetString(now);
            string stripped = text.Replace(insertion + "\r\n", "").Replace(insertion + "\n", "");
            check(Hash(now) == guard.sharedMovementSourceSha256 && Hash(original) == guard.sharedMovementOriginalSha256 &&
                text.Split(new[] { insertion }, StringSplitOptions.None).Length == 2 && Hash(Encoding.UTF8.GetBytes(stripped)) == Hash(original),
                normalized ? "Removing only the Eclipse audio-routing line preserves all existing shared movement source text, cadence and other map routes" :
                    "Removing only the Eclipse audio-routing branch leaves all existing map routes, default sound and shared cue/cadence bytes unchanged",
                normalized ? "UTF-8 BOM and CRLF/LF normalization only; captured source transcript comparison" : "Actual source SHA and preserved backup comparison");
        }
        static string ResolveSourcePath(string fixturePath, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;
            return Path.GetFullPath(Path.IsPathRooted(sourcePath) ? sourcePath : Path.Combine(Path.GetDirectoryName(fixturePath), sourcePath));
        }
        static byte[] NormalizeSourceText(byte[] bytes)
        {
            return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n"));

        }
    }
}