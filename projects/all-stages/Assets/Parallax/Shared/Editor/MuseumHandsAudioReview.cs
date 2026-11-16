using System;
using System.IO;
using System.Security.Cryptography;
using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Review exports only: no scene, audio bank or interaction setting is changed.
    public static class MuseumHandsAudioReview
    {
        [Serializable]
        sealed class Candidate
        {
            public string id, labelKo, role, status, eventId, absolutePath, originalAbsolutePath;
            public string sourceTitle, creator, sourcePage, license, licenseURL, sha256, originalSha256;
            public string description, processing;
            public int number;
            public double durationSeconds, pitch;
        }

        [Serializable]
        sealed class RuntimeEvidence
        {
            public string assetRelativeFile, assetAbsolutePath, sourceAbsolutePath, sourceSha256, gameAssetSha256;
            public bool hashesMatch;
            public string unityCodeFile, unityCodeSha256, lengthRule, notes;
            public double animationDurationSeconds, estimatedRuntimePitch;
        }

        [Serializable]
        sealed class ExportMetadata
        {
            public string status, exportedUtc, sourceClipName, sourceResourceKey;
            public string sourcePcmAbsolutePath, pitchPcmAbsolutePath, sourcePcmSha256, pitchPcmSha256;
            public string algorithm, limits;
            public int sampleRate, channels, sourceFrames, pitchFrames;
            public double sourceDurationSeconds, clipLengthSeconds, pitch, pitchDurationSeconds;
        }

        [Serializable]
        sealed class Review
        {
            public string schemaVersion, date, status, scope, request, duplicateNote;
            public string[] unchangedFiles;
            public RuntimeEvidence runtimeEvidence;
            public Candidate[] candidates;
            public ExportMetadata unityExport;
        }

        // Root invokes this entry point once, so exports precede its final build.
        public static void ExportAndBuild()
        {
            ExportHandsReview();
            MuseumSelectedAudioBuild.ValidateAndBuild();
        }

        public static void ExportHandsReview()
        {
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", ".."));
            string reviewFile = Path.Combine(workspace, "artifacts", "museum-audio-revision-20261112", "hands-review.json");
            Require(File.Exists(reviewFile), "Hands review manifest must exist before exporting.");
            var review = JsonUtility.FromJson<Review>(File.ReadAllText(reviewFile));
            Require(review != null && review.candidates != null, "Hands review manifest is invalid.");
            const string resource = "MuseumAudio/Selected/HandsOpen58";
            var clip = Resources.Load<AudioClip>(resource);
            Require(clip != null, "Selected hands 58 audio is missing.");
            Require(clip.LoadAudioData() && clip.loadState == AudioDataLoadState.Loaded, "58 audio data did not load synchronously.");
            var samples = new float[clip.samples * clip.channels];
            Require(clip.GetData(samples, 0), "Unity could not read selected hands 58 PCM.");
            float pitch = Mathf.Clamp(clip.length / Mathf.Max(.1f, GalleryCathedralActivation.OpeningSeconds), .5f, 1.5f);
            int outputFrames = Mathf.CeilToInt(clip.samples / pitch);
            var pitched = new float[outputFrames * clip.channels];
            for (int frame = 0; frame < outputFrames; frame++)
            {
                double cursor = frame * (double)pitch;
                int from = Math.Min((int)cursor, clip.samples - 1);
                int to = Math.Min(from + 1, clip.samples - 1);
                float fraction = (float)(cursor - from);
                for (int channel = 0; channel < clip.channels; channel++)
                    pitched[frame * clip.channels + channel] = Mathf.Lerp(samples[from * clip.channels + channel], samples[to * clip.channels + channel], fraction);
            }
            string outputFolder = Path.Combine(Path.GetDirectoryName(reviewFile), "hands");
            Directory.CreateDirectory(outputFolder);
            string rawFile = Path.Combine(outputFolder, "58-original-unity-pcm.wav");
            string pitchFile = Path.Combine(outputFolder, "H0-current-game-pitch-58.wav");
            WritePcm16(rawFile, samples, clip.frequency, clip.channels);
            WritePcm16(pitchFile, pitched, clip.frequency, clip.channels);
            foreach (var candidate in review.candidates)
            {
                if (candidate.id != "H0" && candidate.id != "58") continue;
                bool isPitch = candidate.id == "H0";
                candidate.absolutePath = isPitch ? pitchFile : rawFile;
                candidate.sha256 = Hash(candidate.absolutePath);
                candidate.durationSeconds = (isPitch ? outputFrames : clip.samples) / (double)clip.frequency;
                candidate.pitch = isPitch ? pitch : 1;
                candidate.status = "comparison_ready_not_reselected";
            }
            review.runtimeEvidence.estimatedRuntimePitch = pitch;
            review.unityExport = new ExportMetadata
            {
                status = "exported_for_reaudition_only", exportedUtc = DateTime.UtcNow.ToString("o"),
                sourceClipName = clip.name, sourceResourceKey = resource,
                sourcePcmAbsolutePath = rawFile, pitchPcmAbsolutePath = pitchFile,
                sourcePcmSha256 = Hash(rawFile), pitchPcmSha256 = Hash(pitchFile),
                sampleRate = clip.frequency, channels = clip.channels, sourceFrames = clip.samples, pitchFrames = outputFrames,
                sourceDurationSeconds = clip.samples / (double)clip.frequency, clipLengthSeconds = clip.length,
                pitch = pitch, pitchDurationSeconds = outputFrames / (double)clip.frequency,
                algorithm = "Unity AudioClip.LoadAudioData/GetData; retain imported PCM channels and amplitude; evaluate source at outputFrame*pitch with per-channel linear interpolation; keep original sample rate; encode PCM16 WAV. No stretch with preserved pitch, EQ, normalization, added sound, or mixing.",
                limits = "Pitch-only comparison. Does not capture Unity playback resampler, spatial attenuation, runtime gain, room reverb, BGM, final 166 thud, or hardware audio output. Original source OGG and Unity sound selection are untouched."
            };
            File.WriteAllText(reviewFile, JsonUtility.ToJson(review, true));
            Require(Math.Abs(review.unityExport.pitchDurationSeconds - GalleryCathedralActivation.OpeningSeconds) < .002,
                "Current-pitch comparison must match the unchanged 2.2-second opening.");
            Debug.Log("MUSEUM_HANDS_REVIEW_EXPORTED pitch=" + pitch.ToString("R") + " manifest=" + reviewFile);
        }

        static void WritePcm16(string path, float[] values, int rate, int channels)
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = values.Length * 2;
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); writer.Write(36 + dataBytes);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
                writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); writer.Write(dataBytes);
                foreach (float sample in values)
                {
                    Require(!float.IsNaN(sample) && !float.IsInfinity(sample), "Invalid PCM sample.");
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
                }
            }
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("MUSEUM_HANDS_REVIEW: " + message); }
    }
}
