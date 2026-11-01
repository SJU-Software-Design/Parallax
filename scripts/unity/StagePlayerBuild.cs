using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Parallax.StageLauncher.Editor
{
    // Invoked only in the generated Builds/StageProject copy.
    public static class StagePlayerBuild
    {
        [Serializable]
        private sealed class Result
        {
            public string stage, scene, result, buildId, unityVersion, output;
            public int errors, warnings;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Missing build argument: " + name);
            return args[index + 1];
        }

        public static void Build()
        {
            string expectedVersion = Argument("-parallaxUnityVersion");
            if (Application.unityVersion != expectedVersion)
                throw new InvalidOperationException("Expected Unity " + expectedVersion +
                    ", got " + Application.unityVersion);
            string outputRoot = Path.GetFullPath(Argument("-parallaxOutput"));
            string buildId = Argument("-parallaxBuildId");
            string oldProductName = PlayerSettings.productName;
            try
            {
                foreach (string stage in Argument("-parallaxStages").Split(','))
                {
                    if (stage != "Gallery" && stage != "Library" &&
                        stage != "Beach" && stage != "Starship")
                        throw new ArgumentException("Unknown stage: " + stage);

                    string scene = "Assets/Parallax/" + stage + "/Scenes/" + stage + ".unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                        throw new FileNotFoundException("Saved stage scene is missing", scene);
                    string outputDir = Path.Combine(outputRoot, stage);
                    Directory.CreateDirectory(outputDir);
                    string outputExe = Path.Combine(outputDir, "Parallax.exe");
                    PlayerSettings.productName = "Parallax - " + stage;
                    Debug.Log("STAGE_BUILD_START " + stage + " scene=" + scene);
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { scene },
                        locationPathName = outputExe,
                        target = BuildTarget.StandaloneWindows64,
                        subtarget = (int)StandaloneBuildSubtarget.Player,
                        options = BuildOptions.None
                    });
                    var result = new Result
                    {
                        stage = stage, scene = scene, result = report.summary.result.ToString(),
                        buildId = buildId, unityVersion = Application.unityVersion, output = outputExe,
                        errors = (int)report.summary.totalErrors, warnings = (int)report.summary.totalWarnings
                    };
                    File.WriteAllText(Path.Combine(outputDir, "build-result.json"), JsonUtility.ToJson(result, true));
                    if (report.summary.result != BuildResult.Succeeded)
                        throw new InvalidOperationException(stage + " build failed: " + report.summary.result);
                    Debug.Log("STAGE_BUILD_OK " + stage + " output=" + outputExe);
                }
            }
            finally { PlayerSettings.productName = oldProductName; }
        }
    }
}
