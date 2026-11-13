using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    // Build the existing saved museum and destination. Runtime installation adds
    // the new exhibition; never regenerate or save the scene or baked lighting.
    public static class MuseumBottleBalanceBuild
    {
        const string GalleryScene = "Assets/Parallax/Gallery/Scenes/Gallery.unity";
        const string BeachScene = "Assets/Parallax/Beach/Scenes/Beach.unity";
        const string AudioCredits = "Documentation/Gallery/MuseumAudio-Credits.txt";
        const string ModelCredits = "Documentation/Gallery/MuseumBottleBalance-Credits.txt";
        const string ProjectSettingsFile = "ProjectSettings/ProjectSettings.asset";

        [Serializable] sealed class ProtectedFile
        {
            public string path, before, after;
            public bool preserved;
        }
        [Serializable] sealed class Result
        {
            public string buildId, startedUtc, finishedUtc, unityVersion, phase, result, output, exception;
            public string productName = "Parallax - Museum Bottle and Balance - Beach Coast";
            public string graphicsApi = "Direct3D11";
            public string[] scenes;
            public int errors, warnings, protectedFileCount;
            public ulong totalBytes;
            public double buildSeconds;
            public bool savedSceneLightingSettingsPreserved, restoredProjectSettingsBytes;
            public List<ProtectedFile> protectedFiles = new List<ProtectedFile>();
            public List<string> checks = new List<string>();
            public List<string> errorMessages = new List<string>();
            public List<string> warningMessages = new List<string>();
        }

        // Unity -batchmode -projectPath ... -executeMethod
        // ShadeLink.Editor.MuseumBottleBalanceBuild.ValidateAndBuild
        // -bottleBalanceOutput <fresh directory or fresh executable path>
        public static void ValidateAndBuild()
        {
            string buildId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string audit = Path.Combine(projectRoot, "Builds", "MuseumBottleBalance", buildId);
            string output = Path.Combine(audit, "Parallax.exe");
            var result = new Result { buildId = buildId, startedUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion, phase = "preflight", result = "Failed", output = output };
            Exception failure = null;
            bool outputClaimed = false, settingsChanged = false;
            string oldProductName = PlayerSettings.productName;
            bool oldDefaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            GraphicsDeviceType[] oldGraphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            byte[] originalProjectSettings = null;
            try
            {
                string requested = Argument("-bottleBalanceOutput");
                if (!string.IsNullOrEmpty(requested))
                {
                    string full = Path.GetFullPath(requested);
                    output = string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase)
                        ? full : Path.Combine(full, "Parallax.exe");
                }
                result.output = output;
                Require(!File.Exists(output), "Output executable already exists; use a fresh -bottleBalanceOutput: " + output);
                Directory.CreateDirectory(Path.GetDirectoryName(output)); outputClaimed = true;
                if (!Application.isBatchMode)
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        Require(!SceneManager.GetSceneAt(i).isDirty, "Save editor work before running this batch build entry point.");
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(GalleryScene) != null, "Saved Gallery scene is missing.");
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(BeachScene) != null, "Latest Beach destination is missing.");
                Require(File.Exists(AudioCredits), "Existing museum audio credits are missing: " + AudioCredits);
                Require(File.Exists(ModelCredits), "External 3D model credits must be included: " + ModelCredits);
                originalProjectSettings = File.ReadAllBytes(ProjectSettingsFile);
                result.protectedFiles = CaptureProtected();
                result.protectedFileCount = result.protectedFiles.Count;
                result.checks.Add("Captured saved Gallery/Beach/Library scenes, lighting assets and all ProjectSettings hashes");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(Resources.Load<GameObject>("BottleBalance/Models/TrefoilKnot") != null,
                    "External CC0 knot model is missing from runtime Resources.");
                Require(Resources.Load<GameObject>("BottleBalance/Models/BronzeTwist") != null,
                    "External CC0 fixed bronze sculpture is missing from runtime Resources.");
                result.checks.Add("Runtime Resources resolve the imported external CC0 knot and fixed bronze artwork");
                GalleryDikePedestalAssets.Prepare();
                Require(Resources.Load<GameObject>("BottleBalance/Dike/VersaillesPedestalP02")!=null,"Actual P02 Versailles pedestal is missing");
                foreach(string name in new[]{"StatueBody","RewardSword","ScaleBeam","ScaleSuspension","PanLeftBowl","PanRightBowl","LawBook","SwordStone"})
                    Require(Resources.Load<GameObject>("BottleBalance/Dike/"+name)!=null,"Dike external model missing: "+name);
                Require(Resources.Load<TextAsset>("BottleBalance/Dike/Rig")!=null,"Verified sword separation manifest missing");
                Require(Resources.Load<GameObject>("BottleBalance/Dike/ConceptSword")!=null,"Concept-matched sword visual missing");
                Require(Resources.Load<TextAsset>("BottleBalance/Dike/ConceptSwordRig")!=null,"Concept sword independent-mesh/hand-grip manifest missing");
                result.checks.Add("Prepared Dike body, independently separated sword, scale, CC0 law-book source and sword stone resolve");
                foreach(string file in new[]{"D02_BookInsideBottle_Tap","D04_BottleFloor_SmallMedium_Short","D09_BottleFloor_Large",
                    "D14_BookFloor_Small","D11_BookFloor_MediumLarge","D21_ScalePivot","D28_SwordFirstLanding_OneHit",
                    "D30_SwordPickup","D33_SwordInsert_Scrape","D36_SwordSeat","D37_SocketRumble_Slow","D42_DoorOpen_Existing161","G01_StatueGlow"})
                    Require(Resources.Load<AudioClip>("MuseumAudio/Selected/Dike/"+file)!=null,"Selected Dike audio missing: "+file);
                Require(Mathf.Abs(Resources.Load<AudioClip>("MuseumAudio/Selected/Dike/D37_SocketRumble_Slow").length-
                    GalleryDikeInsertionRumble.Duration)<.02f,"Slowed D37 audio and camera rumble duration differ");
                result.checks.Add("Thirteen selected external Dike clips resolve, including G01 statue glow and 1.60-second slowed rumble");
                Require(Resources.Load<GameObject>("BottleBalance/Models/RewardAntiqueKey") != null,
                    "Actual external CC0 reward key is missing from runtime Resources.");
                Require(Resources.Load<GameObject>("BottleBalance/Models/original_oval_brass_keyhole_plate") != null,
                    "Original keyhole plate is missing from runtime Resources.");
                foreach (string name in new[] { "DurerFirst", "DurerSixth", "VermeerBalance", "ChristusGoldsmith" })
                    Require(Resources.Load<Texture2D>("MuseumBalanceArt/" + name) != null, "Themed artwork is missing: " + name);
                EditorSceneManager.OpenScene(GalleryScene, OpenSceneMode.Single);
                GalleryBottleBalanceReadableArchitecture.PrepareSavedScene();
                GalleryStaticExhibitCollisionAssets.PrepareSavedScene();
                GalleryPortalBeachAssets.Prepare();
                Require(Resources.Load<GameObject>("MuseumPortal/BeachPreview")!=null,"Live scenic Beach preview is missing");
                result.checks.Add("Prepared separate fixed-exhibit collision meshes and latest Beach render-only live preview");

                // These tests create ordinary museum fixtures. Run before changing
                // productName, so the optional exhibition does not alter fixtures.
                result.phase = "regression-tests";
                PlayerControlsTests.Run(); result.checks.Add("PlayerControlsTests.Run");
                ObjectInteractionTests.Run(); result.checks.Add("ObjectInteractionTests.Run");
                ReleasePhysicsTests.Run(); result.checks.Add("ReleasePhysicsTests.Run");
                MuseumSelectedAudioMovementTests.Run(); result.checks.Add("MuseumSelectedAudioMovementTests.Run");
                EditorSceneManager.OpenScene(GalleryScene, OpenSceneMode.Single);
                result.scenes = new[] { GalleryScene, BeachScene };
                GalleryBuild.WriteCredits(Path.GetDirectoryName(output));
                result.checks.Add("Shared museum credit writer copied existing art/audio, Dike/P02/book/sword and Beach source credits beside the executable");

                result.phase = "build"; settingsChanged = true;
                PlayerSettings.productName = result.productName;
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
                        else if (message.type == LogType.Warning) result.warningMessages.Add(message.content);
                    }
                Require(report.summary.result == BuildResult.Succeeded && result.errors == 0,
                    "Build failed: " + report.summary.result + ", errors=" + result.errors);
                Require(File.Exists(output), "Successful report did not produce the executable.");
                result.result = "Succeeded";
            }
            catch (Exception error)
            {
                failure = error; result.errors = Math.Max(1, result.errors);
                result.exception = error.ToString(); result.errorMessages.Add(error.Message);
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
                        // Restore the exact saved bytes after restoring live settings.
                        // No scene or lighting asset is ever written by this helper.
                        if (originalProjectSettings != null && HashBytes(originalProjectSettings) != Hash(ProjectSettingsFile))
                        {
                            File.WriteAllBytes(ProjectSettingsFile, originalProjectSettings);
                            result.restoredProjectSettingsBytes = true;
                        }
                    }
                    result.savedSceneLightingSettingsPreserved = result.protectedFiles.Count > 0;
                    foreach (ProtectedFile item in result.protectedFiles)
                    {
                        item.after = File.Exists(item.path) ? Hash(item.path) : "missing";
                        item.preserved = item.before == item.after;
                        result.savedSceneLightingSettingsPreserved &= item.preserved;
                        if (!item.preserved) result.errorMessages.Add("Protected file changed: " + item.path);
                    }
                    if (!result.savedSceneLightingSettingsPreserved && result.protectedFiles.Count > 0)
                        throw new InvalidOperationException("Saved scene, lighting or ProjectSettings hashes changed.");
                    if (result.savedSceneLightingSettingsPreserved)
                        result.checks.Add("All " + result.protectedFileCount + " saved scene/lighting/settings SHA256 hashes preserved");
                    if (failure == null) result.phase = "complete";
                }
                catch (Exception error)
                {
                    if (failure == null) failure = error;
                    result.exception = (result.exception ?? "") + "\nPreservation: " + error;
                    result.errorMessages.Add(error.Message); result.errors = Math.Max(1, result.errors); result.result = "Failed";
                }
                result.finishedUtc = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(audit);
                string json = JsonUtility.ToJson(result, true);
                File.WriteAllText(Path.Combine(audit, "build-result.json"), json);
                string outputDirectory = Path.GetDirectoryName(result.output);
                if (outputClaimed && Directory.Exists(outputDirectory) && !File.Exists(Path.Combine(outputDirectory, "build-result.json")))
                    File.WriteAllText(Path.Combine(outputDirectory, "build-result.json"), json);
            }
            if (failure != null)
            {
                Debug.LogError("MUSEUM_BOTTLE_BALANCE_BUILD_FAILED " + buildId + " phase=" + result.phase + " " + failure.Message);
                throw new InvalidOperationException("Bottle and Balance build failed; see " + audit, failure);
            }
            Debug.Log("MUSEUM_BOTTLE_BALANCE_BUILD_OK " + buildId + " " + output);
        }

        static List<ProtectedFile> CaptureProtected()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in new[] { "Assets/Parallax/Gallery/Scenes", "Assets/Parallax/Beach/Scenes", "Assets/Parallax/Library/Scenes", "ProjectSettings" })
                if (Directory.Exists(folder))
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path.Replace('\\', '/'));
            foreach (string path in new[] { "Assets/Parallax/Gallery/Generated/Museum bounce lighting.lighting",
                "Assets/Parallax/Gallery/Generated/Museum bounce lighting.lighting.meta" })
                if (File.Exists(path)) paths.Add(path);
            var sorted = new List<string>(paths); sorted.Sort(StringComparer.Ordinal);
            var result = new List<ProtectedFile>();
            foreach (string path in sorted) result.Add(new ProtectedFile { path = path, before = Hash(path) });
            return result;
        }
        static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs(); int index = Array.IndexOf(arguments, name);
            if (index < 0) return null;
            Require(index + 1 < arguments.Length && !arguments[index + 1].StartsWith("-"), "Missing value for " + name);
            return arguments[index + 1];
        }
        static string Hash(string path) { return HashBytes(File.ReadAllBytes(path)); }
        static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("MUSEUM_BOTTLE_BALANCE: " + message); }
    }
}
