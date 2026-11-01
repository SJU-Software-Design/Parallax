using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static partial class BuildEntry
    {
        public const string ScenePath = "Assets/Parallax/Legacy/Scenes/Courtyard.unity";
        [MenuItem("그늘 잇기/플레이 씬 준비")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Shade Link").AddComponent<ShadeGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Shade Link Team";
            PlayerSettings.productName = "Parallax";
            PlayerSettings.bundleVersion = "0.6.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SplashScreen.show = false;
            // Runtime-generated materials must remain available in a player build.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var always = settings.FindProperty("m_AlwaysIncludedShaders");
            // The font shader is a runtime-internal resource. Serializing it as an
            // explicit build asset triggers HideFlags.DontSave in Unity 6.3.
            for (int i = always.arraySize - 1; i >= 0; i--)
            {
                var item = always.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue != null && item.objectReferenceValue.name == "GUI/Text Shader")
                { item.objectReferenceValue = null; always.DeleteArrayElementAtIndex(i); }
            }
            settings.FindProperty("m_FogStripping").intValue = 1;
            settings.FindProperty("m_FogKeepLinear").boolValue = true;
            settings.FindProperty("m_FogKeepExp").boolValue = false;
            settings.FindProperty("m_FogKeepExp2").boolValue = false;
            foreach (string name in new[] { "Standard", "Sprites/Default", "ShadeLink/Flat", "ShadeLink/Finish", "ShadeLink/WorldText" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null) throw new Exception("Missing shader: " + name);
                bool found = false;
                for (int i = 0; i < always.arraySize; i++) if (always.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (!found) { always.InsertArrayElementAtIndex(always.arraySize); always.GetArrayElementAtIndex(always.arraySize - 1).objectReferenceValue = shader; }
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("SHADE_PREPARE_OK");
        }
        [MenuItem("그늘 잇기/규칙 검증")]
        public static void Test() { RuleTests.Run(); ShadowPuzzleTests.Run(); CourtyardPuzzleTests.Run(); KeyPuzzleTests.Run(); }

        [MenuItem("그늘 잇기/Windows 실행 파일 만들기")]
        public static void BuildWindows()
        {
            Prepare(); Test();
            string output = Environment.GetEnvironmentVariable("SHADE_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output)) output = Parallax.Editor.BuildPaths.Output("Parallax_Unity_v0.6_Key/Parallax.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/build-result.txt", "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors
                + "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize
                + "\nDuration: " + report.summary.totalTime + "\nOutput: " + output + "\nUnity: " + Application.unityVersion);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("SHADE_BUILD_OK " + output);
        }

        [MenuItem("그늘 잇기/macOS 실행 파일 만들기")]
        public static void BuildMacOS()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new Exception("Install Mac Build Support (Mono) for Unity " + Application.unityVersion);
            Prepare(); Test();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.shadelink.prototype");
            string platformName = BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            EditorUserBuildSettings.SetPlatformSettings(platformName, "Architecture", "x64arm64");
            AssetDatabase.SaveAssets();
            string output = Environment.GetEnvironmentVariable("SHADE_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output)) output = Parallax.Editor.BuildPaths.Output("Parallax_Unity_v0.6_Key_macOS/ShadeLink.app");
            if (!output.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) throw new Exception("Mac output must end in .app");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/build-macos-result.txt", "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors
                + "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize
                + "\nDuration: " + report.summary.totalTime + "\nOutput: " + output + "\nUnity: " + Application.unityVersion
                + "\nGraphics: Metal\nArchitecture: " + EditorUserBuildSettings.GetPlatformSettings(platformName, "Architecture")
                + "\nRuntime tested on macOS: No (cross-built on Windows)");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("macOS build failed: " + report.summary.result);
            Debug.Log("SHADE_MACOS_BUILD_OK " + output);
        }
    }
}
