using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static partial class BuildEntry
    {
        public const string IndoorScene = "Assets/Parallax/Legacy/Scenes/Indoor.unity";
        [MenuItem("그늘 잇기/실내 실험 씬 준비")]
        public static void PrepareIndoor()
        {
            Prepare();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Indoor prototype").AddComponent<IndoorGame>();
            EditorSceneManager.SaveScene(scene, IndoorScene);
            PlayerSettings.productName = "Parallax - Light Corridor"; PlayerSettings.bundleVersion = "0.7.0";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(IndoorScene, true) };
            AssetDatabase.SaveAssets();
        }
        [MenuItem("그늘 잇기/실내 실험 Windows 빌드")]
        public static void BuildIndoorWindows()
        {
            PrepareIndoor(); Test(); IndoorTests.Run();
            string output = Parallax.Editor.BuildPaths.Output("Parallax_Unity_v0.7_Indoor/Parallax.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { IndoorScene }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Directory.CreateDirectory("Validation/Indoor");
            File.WriteAllText("Validation/Indoor/build-result.txt", "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors
                + "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize + "\nOutput: " + output + "\nUnity: " + Application.unityVersion);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Indoor Windows build failed.");
            Debug.Log("INDOOR_BUILD_OK " + output);
        }
    }
}
