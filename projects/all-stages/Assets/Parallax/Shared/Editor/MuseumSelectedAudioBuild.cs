using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    // Builds the saved museum scene. Do not call GalleryBuild.Prepare here:
    // rebuilding its geometry would replace the saved art and baked lighting.
    public static class MuseumSelectedAudioBuild
    {
        const string GalleryScene = "Assets/Parallax/Gallery/Scenes/Gallery.unity";
        const string SelectedFolder = "Assets/Parallax/Gallery/Resources/MuseumAudio/Selected";
        const string MusicResource = "MuseumAudio/Selected/MuseumBgmC01";
        const string AudioCredits = "Documentation/Gallery/MuseumAudio-Credits.txt";
        static readonly string[] RequiredCues =
        {
            "movement.footstep", "movement.land", "prop.grab", "prop.release",
            "impact.wood.light", "impact.wood.medium", "impact.wood.heavy",
            "impact.metal.light", "impact.metal.medium", "impact.metal.heavy",
            "impact.stone.heavy", "nike.shadow_fit", "nike.paint", "nike.door_open",
            "face.smile", "hands.open", "hands.settle", "ui.confirm", "ui.cancel", "ui.error"
        };

        [Serializable]
        sealed class Result
        {
            public string buildId, startedUtc, finishedUtc, unityVersion, phase, result, output;
            public string sceneSha256Before, sceneSha256After, exception;
            public string[] scenes;
            public int importedClips, validatedSfx, errors, warnings;
            public ulong totalBytes;
            public double buildSeconds;
            public List<string> checks = new List<string>();
            public List<string> errorMessages = new List<string>();
            public List<string> warningMessages = new List<string>();
        }

        // Unity -batchmode -projectPath ... -executeMethod
        // ShadeLink.Editor.MuseumSelectedAudioBuild.ValidateAndBuild
        // -museumAudioOutput <new directory or new .exe path>
        public static void ValidateAndBuild()
        {
            string buildId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string artifactDirectory = Path.Combine(projectRoot, "Builds", "MuseumSelectedAudio", buildId);
            string output = Path.Combine(artifactDirectory, "Parallax.exe");
            var result = new Result
            {
                buildId = buildId, startedUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion, phase = "preflight", result = "Failed", output = output
            };
            string oldProductName = PlayerSettings.productName;
            bool oldDefaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            GraphicsDeviceType[] oldGraphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            bool settingsChanged = false;
            bool outputClaimed = false;
            try
            {
                string requested = Argument("-museumAudioOutput");
                if (!string.IsNullOrEmpty(requested))
                {
                    string full = Path.GetFullPath(requested);
                    output = string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase)
                        ? full : Path.Combine(full, "Parallax.exe");
                }
                result.output = output;
                Require(!File.Exists(output), "Build output already exists; provide a fresh -museumAudioOutput: " + output);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                outputClaimed = true;
                if (!Application.isBatchMode)
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        Require(!SceneManager.GetSceneAt(i).isDirty, "Save your current editor work before running the museum batch entry.");
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(GalleryScene) != null, "Saved Gallery scene is missing.");
                Require(File.Exists(AudioCredits), "Museum audio credits must exist before distributing the selected sounds: " + AudioCredits);
                result.sceneSha256Before = Hash(GalleryScene);
                EditorSceneManager.OpenScene(GalleryScene, OpenSceneMode.Single);

                result.phase = "import-audio";
                result.importedClips = ConfigureImporters();
                result.checks.Add("Selected audio default and Standalone import settings: DecompressOnLoad, preload, Vorbis 0.8");
                result.phase = "validate-bank";
                ValidateBank();
                result.validatedSfx = RequiredCues.Length;
                result.checks.Add("All 20 required SFX and C01 BGM resolve from the runtime Resources bank");

                result.phase = "regression-tests";
                PlayerMovementAudioTests.Run();
                result.checks.Add("PlayerMovementAudioTests.Run");
                ReleasePhysicsTests.Run();
                result.checks.Add("ReleasePhysicsTests.Run");
                PlayerControlsTests.Run();
                result.checks.Add("PlayerControlsTests.Run");
                MuseumSelectedAudioMovementTests.Run();
                result.checks.Add("MuseumSelectedAudioMovementTests.Run (9 timing + 11 routing assertions)");
                GalleryGimmickAudioTests.Run();
                result.checks.Add("GalleryGimmickAudioTests.Run");

                // Fixtures can dirty the editor's in-memory scene. Discard them;
                // the build must consume the existing scene, never a saved fixture.
                EditorSceneManager.OpenScene(GalleryScene, OpenSceneMode.Single);
                var scenes = new List<string> { GalleryScene };
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LibraryBuild.ScenePath) != null)
                    scenes.Add(LibraryBuild.ScenePath);
                else
                    throw new FileNotFoundException("The saved museum's existing Library portal destination is missing.", LibraryBuild.ScenePath);
                result.scenes = scenes.ToArray();
                GalleryBuild.WriteCredits(Path.GetDirectoryName(output));
                File.Copy(AudioCredits, Path.Combine(Path.GetDirectoryName(output), Path.GetFileName(AudioCredits)), true);

                result.phase = "build";
                settingsChanged = true;
                PlayerSettings.productName = "Parallax - Museum Selected Audio";
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = result.scenes, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                Require(report != null, "BuildPipeline returned no report.");
                result.errors = (int)report.summary.totalErrors;
                result.warnings = (int)report.summary.totalWarnings;
                result.totalBytes = report.summary.totalSize;
                result.buildSeconds = report.summary.totalTime.TotalSeconds;
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Assert)
                            result.errorMessages.Add(message.content);
                        else if (message.type == LogType.Warning)
                            result.warningMessages.Add(message.content);
                    }
                Require(report.summary.result == BuildResult.Succeeded && result.errors == 0,
                    "Museum build did not succeed: " + report.summary.result + ", errors=" + result.errors);
                Require(File.Exists(output), "Successful report did not produce the requested executable.");
                result.phase = "verify-saved-scene";
                result.sceneSha256After = Hash(GalleryScene);
                Require(result.sceneSha256Before == result.sceneSha256After, "Saved Gallery scene changed during audio-only validation/build.");
                result.result = "Succeeded";
                result.phase = "complete";
                Debug.Log("MUSEUM_SELECTED_AUDIO_BUILD_OK " + buildId + " " + output);
            }
            catch (Exception error)
            {
                result.errors = Math.Max(1, result.errors);
                result.exception = error.ToString();
                result.errorMessages.Add(error.Message);
                Debug.LogError("MUSEUM_SELECTED_AUDIO_BUILD_FAILED " + buildId + " phase=" + result.phase + " " + error.Message);
                throw;
            }
            finally
            {
                try
                {
                    if (settingsChanged)
                    {
                        PlayerSettings.productName = oldProductName;
                        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, oldGraphics);
                        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, oldDefaultGraphics);
                    }
                }
                finally
                {
                    result.finishedUtc = DateTime.UtcNow.ToString("o");
                    if (File.Exists(GalleryScene)) result.sceneSha256After = Hash(GalleryScene);
                    // A separate unique audit path also covers early failures,
                    // e.g. accidentally passing an existing output executable.
                    Directory.CreateDirectory(artifactDirectory);
                    string json = JsonUtility.ToJson(result, true);
                    File.WriteAllText(Path.Combine(artifactDirectory, "build-result.json"), json);
                    string outputDirectory = Path.GetDirectoryName(result.output);
                    if (outputClaimed && Directory.Exists(outputDirectory) && !File.Exists(Path.Combine(outputDirectory, "build-result.json")))
                        File.WriteAllText(Path.Combine(outputDirectory, "build-result.json"), json);
                }
            }
        }

        // Read-only validation can also be called by a batch check without
        // building. It uses the same Resources loading path as GalleryGame.
        public static void ValidateBank()
        {
            var fixture = new GameObject("Selected museum audio bank validation");
            try
            {
                var listener = new GameObject("Bank validation listener");
                listener.transform.SetParent(fixture.transform, false);
                MuseumAudio bank = MuseumAudio.Install(fixture, listener.transform);
                foreach (string cue in RequiredCues)
                {
                    Require(bank.HasClip(cue), "Missing required museum audio cue: " + cue);
                    AudioClip clip = bank.Clip(cue);
                    Require(clip.length > 0 && clip.samples > 0 && clip.channels > 0, "Invalid audio data for " + cue);
                    ValidateImporter(AssetDatabase.GetAssetPath(clip));
                }
                AudioClip music = Resources.Load<AudioClip>(MusicResource);
                Require(music != null && music.samples > 0 && music.length > 3,
                    "C01 BGM is missing or too short for the two-source loop crossfade.");
                ValidateImporter(AssetDatabase.GetAssetPath(music));
                Require(!bank.HasClip("movement.jump"), "Unselected jump cue should remain silent.");
                Require(!bank.HasClip("face.curtain"), "Unselected curtain cue should remain silent.");
                Require(!bank.HasClip("impact.stone.light"), "Unselected small-stone cue should remain silent.");
                Debug.Log("MUSEUM_SELECTED_AUDIO_BANK_OK 20 SFX + C01 BGM; nike.unlock optional");
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }

        static int ConfigureImporters()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(AssetDatabase.IsValidFolder(SelectedFolder), "Selected museum audio folder is missing.");
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SelectedFolder });
            Require(guids.Length >= RequiredCues.Length + 1, "Selected bank contains fewer than 20 SFX plus C01 BGM.");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Require(importer != null, "AudioImporter missing: " + path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .8f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                Require(importer.SetOverrideSampleSettings(BuildTargetGroup.Standalone, settings), "Cannot set Standalone audio settings: " + path);
                importer.loadInBackground = false;
                importer.SaveAndReimport();
                ValidateImporter(path);
            }
            return guids.Length;
        }

        static void ValidateImporter(string path)
        {
            Require(path.StartsWith(SelectedFolder + "/", StringComparison.Ordinal), "Selected cue resolved outside the approved bank: " + path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Require(importer != null && !importer.loadInBackground, "Selected clip must load before its timing event: " + path);
            ValidateSettings(importer.defaultSampleSettings, path + " (default)");
            Require(importer.ContainsSampleSettingsOverride(BuildTargetGroup.Standalone), "Standalone audio override missing: " + path);
            ValidateSettings(importer.GetOverrideSampleSettings(BuildTargetGroup.Standalone), path + " (Standalone)");
        }

        static void ValidateSettings(AudioImporterSampleSettings settings, string label)
        {
            Require(settings.loadType == AudioClipLoadType.DecompressOnLoad && settings.preloadAudioData,
                "Selected clip must preload decompressed audio: " + label);
            Require(settings.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Abs(settings.quality - .8f) < .001f,
                "Expected Vorbis quality 0.8: " + label);
            Require(settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate, "Source sample rate was changed: " + label);
        }

        static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            if (index < 0) return null;
            Require(index + 1 < arguments.Length && !arguments[index + 1].StartsWith("-"), "Missing value for " + name);
            return arguments[index + 1];
        }

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("MUSEUM_SELECTED_AUDIO: " + message);
        }
    }
}
