using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShadeLink.Library;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class LibraryBuild
    {
        public const string ScenePath = "Assets/Parallax/Library/Scenes/Library.unity";
        public const string BlockoutScenePath = "Assets/Parallax/Library/Scenes/Library_Blockout.unity";
        const string Generated = "Assets/Parallax/Library/Generated/";

        [MenuItem("Parallax/도서관 씬 준비")]
        public static void Prepare() { PrepareScene(false); }

        [MenuItem("Parallax/개발 검증/도서관 구조만 준비")]
        public static void PrepareBlockout() { PrepareScene(true); }

        static void RequireAuthenticAsset(LibraryGame game)
        {
            // Structural tests compile before the optional source download is available.
            // A distributable scene must run the real asset importer successfully.
            var importer = typeof(LibraryBuild).Assembly.GetType("ShadeLink.Editor.LibraryAssetImport");
            var integrate = importer?.GetMethod("Integrate", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(LibraryGame) }, null);
            if (integrate == null)
                throw new InvalidOperationException("LibraryAssetImport.Integrate(LibraryGame) is required for the final library scene. Use PrepareBlockout only for structural testing.");
            try { integrate.Invoke(null, new object[] { game }); }
            catch (TargetInvocationException failure)
            { throw new InvalidOperationException("Authentic Medieval Library Kit integration failed. Final scene/build was stopped.", failure.InnerException ?? failure); }
        }

        static void SaveMaterials(LibraryGame game, string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var material in LibraryWorld.Materials.Where(m => m != null).Distinct().ToArray())
            {
                if (AssetDatabase.Contains(material)) continue;
                string name = material.name;
                foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                string path = directory + name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null) { AssetDatabase.CreateAsset(material, path); continue; }
                EditorUtility.CopySerialized(material, existing);
                foreach (var renderer in game.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] == material) { materials[i] = existing; changed = true; }
                    if (changed) renderer.sharedMaterials = materials;
                }
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        static void PrepareScene(bool blockout)
        {
            string scenePath = blockout ? BlockoutScenePath : ScenePath;
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            Directory.CreateDirectory(Generated);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LibraryGame game = LibraryWorld.Build();
            if (!blockout) RequireAuthenticAsset(game);
            foreach (var prop in game.props) prop.Remember();
            SaveMaterials(game, Generated + (blockout ? "Blockout/" : "Stage/"));
            game.lighting.Rebuild();
            if (game.props.Length == 0 || !game.props.Any(p => p.kind == "Book"))
                throw new InvalidOperationException("Library stage has no individually selectable books.");
            if (game.props.Any(p => string.IsNullOrEmpty(p.objectId)) ||
                game.props.Select(p => p.objectId).Distinct().Count() != game.props.Length)
                throw new InvalidOperationException("Library props must have unique non-empty objectIds.");
            if (game.props.Any(p => p.GetComponentsInChildren<LibraryProp>().Length != 1))
                throw new InvalidOperationException("Library books and furniture must have independent roots.");
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Failed to save library scene: " + scenePath);
            if (!blockout)
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != BlockoutScenePath).ToList();
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LIBRARY_PREPARE_OK mode=" + (blockout ? "blockout" : "authentic") +
                " props=" + game.props.Length + " books=" + game.props.Count(p => p.kind == "Book") +
                " casters=" + game.lighting.Count + " scene=" + scenePath);
        }

        [MenuItem("Parallax/도서관 Windows 빌드")]
        public static void Build() { BuildPlayer(false); }

        [MenuItem("Parallax/개발 검증/도서관 구조만 Windows 빌드")]
        public static void BuildBlockout() { BuildPlayer(true); }

        static void BuildPlayer(bool blockout)
        {
            PrepareScene(blockout);
            var shader = Shader.Find("Parallax/LibrarySurface");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Library surface shader compilation failed; build stopped.");
            PlayerSettings.productName = "Parallax - Library v0.12.0" + (blockout ? " Structural Test" : "");
            PlayerSettings.bundleVersion = "0.12.0";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            string output = Parallax.Editor.BuildPaths.Output(blockout
                ? "Parallax_Unity_v0.12.0_Library_Blockout/Parallax_Blockout.exe"
                : "Parallax_Unity_v0.12.0_Library/Parallax.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { blockout ? BlockoutScenePath : ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            Directory.CreateDirectory("Validation/Library");
            File.WriteAllText("Validation/Library/" + (blockout ? "blockout-build-result.txt" : "build-result.txt"),
                "Result: " + report.summary.result + "\nMode: " + (blockout ? "structural-only" : "authentic-asset") +
                "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings +
                "\nOutput: " + output + "\nUnity: " + Application.unityVersion, new System.Text.UTF8Encoding(false));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Library Windows build failed.");
            Debug.Log("LIBRARY_BUILD_OK mode=" + (blockout ? "blockout" : "authentic") + " " + output);
        }
    }
}
