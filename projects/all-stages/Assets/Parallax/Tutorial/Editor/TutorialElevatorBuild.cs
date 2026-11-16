using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class TutorialElevatorBuild
    {
        public static void Prepare()
        {
            ShadeLink.Tutorial.Editor.ElevatorAssetPreparation.Prepare();
            TutorialElevatorAudioAssets.Prepare();
            BeachAudioAssets.Prepare();
            PrepareEndingAudio();
            BeachBuild.Prepare();
            TutorialBuild.Prepare();
            GalleryBuild.Prepare();
            GalleryBottleBalanceReadableArchitecture.PrepareSavedScene();
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/Parallax/Tutorial/Scenes/Tutorial.unity", true),
                new EditorBuildSettingsScene("Assets/Parallax/Gallery/Scenes/Gallery.unity", true),
                new EditorBuildSettingsScene("Assets/Parallax/Beach/Scenes/Beach.unity", true) };
            AssetDatabase.SaveAssets();
        }

        public static void BuildWindows()
        {
            Prepare();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-elevator-output");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -elevator-output");
            string output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            PlayerSettings.productName = "Parallax - Complete Course - Harbor Sound";
            PlayerSettings.bundleVersion = "0.29.2-complete-harbor";
            string[] scenes = {
                "Assets/Parallax/Tutorial/Scenes/Tutorial.unity",
                "Assets/Parallax/Gallery/Scenes/Gallery.unity",
                "Assets/Parallax/Beach/Scenes/Beach.unity"
            };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = Path.Combine(output, "Parallax.exe"),
                target = BuildTarget.StandaloneWindows64, subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None
            });
            File.WriteAllText(Path.Combine(output, "build-result.json"),
                "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + "}");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Elevator build failed");
            GalleryBuild.WriteCredits(output);
            File.Copy("Assets/Parallax/Tutorial/ThirdParty/ElevatorE01/CREDITS.txt", Path.Combine(output, "ElevatorE01-CREDITS.txt"), true);
            TutorialElevatorAudioAssets.WriteCredits(output);
            BeachAudioAssets.WriteCredits(output);
            BeachHarborAssetPreparation.WriteCredits(output);
            File.Copy("Documentation/Beach/CounterweightHarbor.md", Path.Combine(output, "CounterweightHarbor-Credits-and-Controls.md"), true);
            const string ending = "Assets/Parallax/Beach/Resources/Beach/EndingAudio/";
            File.Copy(ending + "CREDITS.txt", Path.Combine(output, "HarborEndingAudio-CREDITS.txt"), true);
            File.Copy(ending + "EndingAudioManifest.json", Path.Combine(output, "HarborEndingAudio-selection.json"), true);
            File.Copy("Documentation/Beach/HarborEndingSound-Credits-and-Controls.txt", Path.Combine(output, "HarborEndingSound-Credits-and-Controls.txt"), true);
            Debug.Log("TUTORIAL_ELEVATOR_BUILD_OK " + output);
        }
        [Serializable] sealed class EndingManifest { public int recordCount; public EndingClip[] records; }
        [Serializable] sealed class EndingClip
        {
            public string fileName, resourceKey, outputSha256;
            public int channels;
            public float durationSeconds;
            public bool streaming;
        }

        // Fail a clean checkout build before shipping a silent or incomplete ending.
        static void PrepareEndingAudio()
        {
            const string folder = "Assets/Parallax/Beach/Resources/Beach/EndingAudio/";
            var manifest = JsonUtility.FromJson<EndingManifest>(File.ReadAllText(folder + "EndingAudioManifest.json"));
            if (manifest == null || manifest.records == null || manifest.recordCount != 15 || manifest.records.Length != 15)
                throw new InvalidOperationException("The ending needs all 15 selected audio clips.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in manifest.records)
            {
                if (record == null || string.IsNullOrEmpty(record.fileName) || Path.GetFileName(record.fileName) != record.fileName || !names.Add(record.fileName))
                    throw new InvalidOperationException("Invalid or duplicate ending audio record.");
                string path = folder + record.fileName;
                string expectedKey = "Beach/EndingAudio/" + Path.GetFileNameWithoutExtension(record.fileName);
                if (record.resourceKey != expectedKey) throw new InvalidOperationException("Ending resource key mismatch: " + path);
                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    string hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
                    if (!string.Equals(hash, record.outputSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Selected ending audio hash mismatch: " + path);
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.samples <= 0 || clip.channels != record.channels ||
                    Mathf.Abs(clip.length - record.durationSeconds) > .15f ||
                    clip.loadType != (record.streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad))
                    throw new InvalidOperationException("Selected ending audio import mismatch: " + path);
            }
            Debug.Log("HARBOR_ENDING_AUDIO_VERIFIED clips=15 sourceHashes=verified");
        }
    }
}
