using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class ControlFeelBuild
    {
        [MenuItem("Parallax/조작감 검증/우주선 macOS 빌드")]
        public static void BuildMacStarship()
        {
            BuildMac("Starship", StarshipBuild.Prepare);
        }

        [MenuItem("Parallax/조작감 검증/해변 macOS 빌드")]
        public static void BuildMacBeach()
        {
            BuildMac("Beach", BeachBuild.Prepare);
        }

        static void BuildMac(string stage, Action prepare)
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneOSX))
                throw new Exception("macOS Build Support is required");
            prepare();
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..","Builds/ControlFeel/"+stage+".app"));
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"-parallaxBuildOutput");
            if(outputAt>=0&&outputAt+1<args.Length)output=Path.GetFullPath(args[outputAt+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string platform=BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            string architecture=EditorUserBuildSettings.GetPlatformSettings(platform,"Architecture");
            var subtarget=EditorUserBuildSettings.standaloneBuildSubtarget;
            try
            {
                EditorUserBuildSettings.standaloneBuildSubtarget=StandaloneBuildSubtarget.Player;
                EditorUserBuildSettings.SetPlatformSettings(platform,"Architecture","x64arm64");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{"Assets/Parallax/"+stage+"/Scenes/"+stage+".unity"},locationPathName=output,
                    target=BuildTarget.StandaloneOSX,subtarget=(int)StandaloneBuildSubtarget.Player,options=BuildOptions.None });
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-result.txt"),
                    "Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nOutput: "+output);
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Control feel "+stage+" build failed");
                Debug.Log("CONTROL_FEEL_BUILD_OK "+output);
            }
            finally
            {
                EditorUserBuildSettings.SetPlatformSettings(platform,"Architecture",architecture);
                EditorUserBuildSettings.standaloneBuildSubtarget=subtarget;
            }
        }
    }
}
