using System;
using System.IO;
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
            TutorialBuild.Prepare();
            GalleryBuild.Prepare();
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
            PlayerSettings.productName = "Parallax - Tutorial to Beach - Selected Audio";
            PlayerSettings.bundleVersion = "0.26.10-bronze-plaques";
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
            Debug.Log("TUTORIAL_ELEVATOR_BUILD_OK " + output);
        }
    }
}
