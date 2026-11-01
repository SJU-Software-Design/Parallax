using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class GalleryPlatformBuild
    {
        const string ScenePath = "Assets/Parallax/Gallery/Scenes/Gallery.unity";

        [MenuItem("Parallax/OS별 빌드/Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Windows", "Parallax.exe");
        }

        [MenuItem("Parallax/OS별 빌드/macOS")]
        public static void BuildMacOS()
        {
            Build(BuildTarget.StandaloneOSX, "macOS", "Parallax.app");
        }

        static void Build(BuildTarget target, string platform, string executable)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new Exception("Install " + platform + " Build Support (Mono) for Unity " + Application.unityVersion);

            GalleryBuild.Prepare();
            if (ShaderUtil.ShaderHasError(Shader.Find("Parallax/GallerySurface")))
                throw new Exception("Gallery shader has compilation errors; build stopped.");

            string outputDir = Path.GetDirectoryName(Parallax.Editor.BuildPaths.Output(executable));
            string output = Path.Combine(outputDir, executable);
            var args = Environment.GetCommandLineArgs();
            int outputAt = Array.IndexOf(args, "-parallaxBuildOutput");
            if (outputAt >= 0 && outputAt + 1 < args.Length)
            {
                output = Path.GetFullPath(args[outputAt + 1]);
                outputDir = Path.GetDirectoryName(output);
            }
            Directory.CreateDirectory(outputDir);
            string platformName = BuildPipeline.GetBuildTargetName(target);
            string previousArchitecture = EditorUserBuildSettings.GetPlatformSettings(platformName, "Architecture");
            var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            try
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
                // Universal builds support both Apple silicon and Intel Macs.
                if (target == BuildTarget.StandaloneOSX)
                    EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", "x64arm64");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = target,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.None
                });
                File.WriteAllText(Path.Combine(outputDir, "build-result.txt"),
                    "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                    "\nWarnings: " + report.summary.totalWarnings + "\nOutput: " + output +
                    "\nUnity: " + Application.unityVersion);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception(platform + " Gallery build failed");
                Debug.Log("GALLERY_PLATFORM_BUILD_OK " + output);
            }
            finally
            {
                if (target == BuildTarget.StandaloneOSX)
                    EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", previousArchitecture);
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
            }
        }
    }
}
