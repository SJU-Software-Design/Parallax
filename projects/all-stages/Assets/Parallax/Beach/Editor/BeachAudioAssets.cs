using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Prepare only the explicitly selected Beach bank. Source recordings and
    // the Tutorial/Gallery audio banks are never changed by this importer.
    public static class BeachAudioAssets
    {
        public static readonly string[] Names = {
            "SandFootstep097", "SandLand085", "PropGrab128", "PropRelease156",
            "SandImpactSmall091", "SandImpactMedium083", "SandImpactLargeTeamBreak3", "WoodRodSandBE06",
            "WoodFootstepBE13_01", "WoodFootstepBE13_02", "WoodFootstepBE13_03", "WoodFootstepBE13_04", "WoodLand102",
            "WaterImpactSmall124", "WaterImpactMediumTeamEnter1", "WaterImpactLargeBE10",
            "WoodImpactSmall005", "WoodImpactMedium008", "WoodImpactLarge132", "GateMove027", "Completion036", "BoatCompleteBC02",
            "ShoreAmbience115", "BoatStartBE12", "BoatStopBE12", "BoatRunBE11", "BgmBB01_BossaAntigua"
        };
        const string Folder = "Assets/Parallax/Beach/Resources/BeachAudio/Selected";
        const string ManifestPath = Folder + "/BeachAudioManifest.json";

        [Serializable] sealed class Manifest { public Entry[] records; }
        [Serializable] sealed class Entry
        {
            public string fileName, outputSha256;
            public int frames, sampleRate, channels;
            public float durationSeconds;
            public bool loop;
        }

        public static void Prepare()
        {
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("Beach audio provenance missing", ManifestPath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.records == null || manifest.records.Length != Names.Length)
                throw new InvalidOperationException("Beach bank must have exactly 27 declared selected clips");
            if (Directory.GetFiles(Folder, "*.wav", SearchOption.TopDirectoryOnly).Length +
                Directory.GetFiles(Folder, "*.mp3", SearchOption.TopDirectoryOnly).Length != Names.Length)
                throw new InvalidOperationException("Unexpected audio file in the selected Beach bank");
            var byFile = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in manifest.records)
            {
                if (entry == null || string.IsNullOrEmpty(entry.fileName) || byFile.ContainsKey(entry.fileName))
                    throw new InvalidOperationException("Duplicate or invalid Beach audio manifest entry");
                byFile.Add(entry.fileName, entry);
            }
            int streamingCount = 0;
            foreach (string name in Names)
            {
                bool music = name == "BgmBB01_BossaAntigua";
                string file = name + (music ? ".mp3" : ".wav");
                string path = Folder + "/" + file;
                if (!File.Exists(path) || !byFile.TryGetValue(file, out var entry))
                    throw new FileNotFoundException("Selected Beach audio missing or undeclared", path);
                if (!string.Equals(Sha256(path), entry.outputSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Prepared Beach audio hash changed: " + path);
                bool streaming = name == "ShoreAmbience115" || name == "BoatRunBE11" || music;
                if (entry.loop != streaming) throw new InvalidOperationException("Unexpected Beach loop assignment: " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Beach AudioImporter missing: " + path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.compressionFormat = streaming ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = .9f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                if (!importer.SetOverrideSampleSettings(BuildTargetGroup.Standalone, settings))
                    throw new InvalidOperationException("Beach Standalone audio settings failed: " + path);
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.samples <= 0 || clip.length <= 0 ||
                    clip.channels != entry.channels || clip.frequency != entry.sampleRate ||
                    Mathf.Abs(clip.length - entry.durationSeconds) > (music ? .10f : .03f) ||
                    clip.loadType != settings.loadType)
                    throw new InvalidOperationException("Selected Beach clip format/load type differs: " + path);
                if (!streaming && clip.samples != entry.frames)
                    throw new InvalidOperationException("Beach PCM one-shot sample count changed: " + path);
                if (streaming) streamingCount++;
            }
            if (streamingCount != 3) throw new InvalidOperationException("Beach needs exactly shore, engine-run and BB01 music streaming clips");
            Debug.Log("BEACH_SELECTED_AUDIO_PREPARED clips=27 streamingClips=3 pcmOneShots=24 sourceHashes=verified");
        }

        public static void WriteCredits(string output)
        {
            string credits = Folder + "/CREDITS.txt";
            if (!File.Exists(credits)) throw new FileNotFoundException("Beach sound attribution missing", credits);
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("Beach sound provenance missing", ManifestPath);
            Directory.CreateDirectory(output);
            File.Copy(credits, Path.Combine(output, "BeachAudio-CREDITS.txt"), true);
            File.Copy(ManifestPath, Path.Combine(output, "BeachAudio-Integration.json"), true);
        }

        static string Sha256(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
    }
}