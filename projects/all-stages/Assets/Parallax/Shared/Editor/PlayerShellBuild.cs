using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Run this builder only in an explicitly identified generated project; source scenes stay untouched.
public static class PlayerShellBuild
{
    private const string ExpectedVersion = "6000.3.24f1";
    private const string ScenePath = "Assets/Parallax/Shared/Scenes/MainMenu.unity";
    private static string OutputPath => AbsoluteArgument("-parallaxBuildOutput",
        Parallax.Editor.BuildPaths.Output("Parallax_PlayerShell/Parallax.exe"));
    private static string ResultPath => AbsoluteArgument("-player-shell-result",
        Path.Combine(Path.GetDirectoryName(OutputPath), "build-result.json"));

    private static string AbsoluteArgument(string flag, string fallback = null)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, flag);
        string value = fallback;
        if (index >= 0)
        {
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException(flag + " requires an absolute path.");
            value = arguments[index + 1];
        }
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
            throw new ArgumentException(flag + " requires an absolute path.");
        return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool Within(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // The launcher supplies both paths and mirrors only into a separate disposable
    // project. Reject direct source-project calls before any scene or asset writes.
    public static void RequireGeneratedProject()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string generated = AbsoluteArgument("-generated-project");
        string source = AbsoluteArgument("-source-project");
        if (!string.Equals(project, generated, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project, source, StringComparison.OrdinalIgnoreCase) ||
            Within(project, source) || Within(source, project))
            throw new InvalidOperationException("Player shell preparation requires a separate generated project matching -generated-project.");
        if (!Directory.Exists(Path.Combine(source, "Assets")) ||
            !Directory.Exists(Path.Combine(source, "ProjectSettings")))
            throw new DirectoryNotFoundException("The -source-project Unity source is missing: " + source);
        for (var parent = new DirectoryInfo(project); parent != null; parent = parent.Parent)
            if (Directory.Exists(Path.Combine(parent.FullName, "projects")) &&
                Directory.Exists(Path.Combine(parent.FullName, "scripts")) &&
                Within(project, Path.Combine(parent.FullName, "projects")))
                throw new InvalidOperationException("Scene preparation cannot run inside repository source projects.");
        foreach (string target in new[] { OutputPath, ResultPath })
            if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase) || Within(target, source))
                throw new InvalidOperationException("Build outputs cannot be written into the source project: " + target);
    }

    private static readonly string[] Scenes = {
        ScenePath,
        "Assets/Parallax/Tutorial/Scenes/Tutorial.unity",
        "Assets/Parallax/Gallery/Scenes/Gallery.unity",
        "Assets/Parallax/Beach/Scenes/Beach.unity",
        ShadeLink.Eclipse.Editor.EclipseEditorBuild.ScenePath
    };
    [Serializable]
    private sealed class BuildResultRecord
    {
        public string result;
        public string unityVersion;
        public string project;
        public string output;
        public string preparedBuildProof;
        public string scene;
        public string[] scenes;
        public string target;
        public string backend;
        public string startedUtc;
        public string completedUtc;
        public string exception;
        public int errors;
        public int warnings;
        public long totalBytes;
        public double durationSeconds;
    }

    [MenuItem("Parallax/Build Player Shell Windows")]
    public static void BuildWindows()
    {
        RequireGeneratedProject();
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var result = new BuildResultRecord
        {
            result = "NotStarted",
            unityVersion = Application.unityVersion,
            project = project,
            output = OutputPath,
            scene = ScenePath,
            scenes = (string[])Scenes.Clone(),
            target = BuildTarget.StandaloneWindows64.ToString(),
            backend = ScriptingImplementation.Mono2x.ToString(),
            startedUtc = DateTime.UtcNow.ToString("o"),
            exception = ""
        };
        var started = DateTime.UtcNow;
        try
        {
            if (Application.unityVersion != ExpectedVersion)
                throw new InvalidOperationException("Expected Unity " + ExpectedVersion + ", got " + Application.unityVersion);

            // Existing complete-course preparation already prepares Beach, Tutorial,
            // its selected elevator audio and the current generated museum once.
            RunRequiredRegressions();
            bool reusePrepared = Array.IndexOf(Environment.GetCommandLineArgs(), "-polish-reuse-prepared-scenes") >= 0;
            if (!reusePrepared)
            {
                ShadeLink.Editor.TutorialElevatorBuild.Prepare();
                GameShellSettingsAssets.Ensure();
            }
            else
            {
                // Explicit diagnostic rebuilds reuse this run's already prepared
                // scenes/assets. Required code regressions still run above.
                foreach (string prepared in Scenes)
                {
                    // The four previous stages are already prepared. Eclipse is
                    // newly authored below and is never required from an old proof.
                    if (prepared == ShadeLink.Eclipse.Editor.EclipseEditorBuild.ScenePath) continue;
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(prepared) == null)
                        throw new FileNotFoundException("Prepared diagnostic scene is missing", prepared);
                }
                // A new output can explicitly cite an earlier successful build
                // of this same generated project without overwriting that build.
                string[] arguments = Environment.GetCommandLineArgs();
                int proofIndex = Array.IndexOf(arguments, "-prepared-build-proof");
                bool externalProof = proofIndex >= 0;
                string proofPath = ResultPath;
                if (externalProof)
                {
                    if (proofIndex + 1 >= arguments.Length || arguments[proofIndex + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-prepared-build-proof requires an absolute JSON result path.");
                    proofPath = arguments[proofIndex + 1];
                }
                if (!Path.IsPathRooted(proofPath) || !File.Exists(proofPath))
                    throw new FileNotFoundException("A successful prepared-build proof is required before reusing scenes.", proofPath);
                proofPath = Path.GetFullPath(proofPath);
                var previous = JsonUtility.FromJson<BuildResultRecord>(File.ReadAllText(proofPath));
                if (previous == null || previous.result != "Succeeded" || previous.errors != 0 ||
                    string.IsNullOrWhiteSpace(previous.project) || !Path.IsPathRooted(previous.project) ||
                    !string.Equals(Path.GetFullPath(previous.project).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        project, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Prepared build provenance does not identify a successful build of this generated project.");
                if (string.IsNullOrWhiteSpace(previous.output) || !Path.IsPathRooted(previous.output) || !File.Exists(previous.output))
                    throw new FileNotFoundException("The prepared proof's successful player output is missing.", previous.output);
                if (!externalProof && !string.Equals(Path.GetFullPath(previous.output), Path.GetFullPath(OutputPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The current output must match its previous result when no external prepared proof is supplied.");
                result.preparedBuildProof = proofPath;
                Debug.Log("POLISH_REBUILD_REUSING_PREPARED_ASSETS proof=" + proofPath + " previousOutput=" + previous.output);
            }
            ShadeLink.Eclipse.Editor.EclipseEditorBuild.Prepare();
            ConfigurePlayer();
            ConfigurePreviewImports();
            ConfigureMutableFinalWindow();
            if (!reusePrepared) CreateMainMenuScene();
            else EditorBuildSettings.scenes = Array.ConvertAll(Scenes, path => new EditorBuildSettingsScene(path, true));
            foreach (string scenePath in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new FileNotFoundException("Player shell scene is missing", scenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            AssetDatabase.SaveAssets();

            Debug.Log("PLAYER_SHELL_BUILD_START " + OutputPath);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = (string[])Scenes.Clone(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None
            });
            if (report == null)
                throw new InvalidOperationException("Unity returned no build report.");

            result.result = report.summary.result.ToString();
            result.errors = (int)report.summary.totalErrors;
            result.warnings = (int)report.summary.totalWarnings;
            result.totalBytes = checked((long)report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Preview build failed: " + report.summary.result);
            if (!File.Exists(OutputPath))
                throw new FileNotFoundException("Build succeeded but preview executable is missing.", OutputPath);
            WriteDistributionCredits(Path.GetDirectoryName(OutputPath));
            Debug.Log("PLAYER_SHELL_BUILD_OK " + OutputPath);
        }
        catch (Exception exception)
        {
            if (result.result == "NotStarted" || result.result == BuildResult.Succeeded.ToString())
                result.result = BuildResult.Failed.ToString();
            result.errors = Math.Max(1, result.errors);
            result.exception = exception.ToString();
            throw;
        }
        finally
        {
            result.completedUtc = DateTime.UtcNow.ToString("o");
            result.durationSeconds = (DateTime.UtcNow - started).TotalSeconds;
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true) + "\n", new UTF8Encoding(false));
        }
    }

    [Serializable]
    private sealed class RegressionCase
    { public string suite, error; public bool passed; public double seconds; }
    [Serializable]
    private sealed class RegressionReport
    { public bool passed; public string unityVersion, scope; public RegressionCase[] suites; }

    private static void RunRequiredRegressions()
    {
        string directory = Path.Combine(Path.GetDirectoryName(ResultPath), "EditorRegression");
        Directory.CreateDirectory(directory);
        var results = new System.Collections.Generic.List<RegressionCase>();
        string[] keys = { "Parallax.Controls.Sensitivity", "Parallax.Controls.FieldOfView", "Parallax.Controls.InvertX", "Parallax.Controls.InvertY" };
        bool[] existed = Array.ConvertAll(keys, PlayerPrefs.HasKey);
        float sensitivity = PlayerPrefs.GetFloat(keys[0]), fov = PlayerPrefs.GetFloat(keys[1]);
        int invertX = PlayerPrefs.GetInt(keys[2]), invertY = PlayerPrefs.GetInt(keys[3]);
        string previousTemp = Environment.GetEnvironmentVariable("TEMP"), previousTmp = Environment.GetEnvironmentVariable("TMP");
        try
        {
            // The established suites default to Path.GetTempPath(). Keep their
            // reports in this D-drive run, without changing user environment settings.
            Environment.SetEnvironmentVariable("TEMP", directory, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("TMP", directory, EnvironmentVariableTarget.Process);
            string[] names = { "PlayerControlsTests.Run", "ObjectInteractionTests.Run", "ReleasePhysicsTests.Run" };
            Action[] run = { ShadeLink.Editor.PlayerControlsTests.Run, ShadeLink.Editor.ObjectInteractionTests.Run, ShadeLink.Editor.ReleasePhysicsTests.Run };
            for (int i = 0; i < run.Length; i++)
            {
                var result = new RegressionCase { suite = names[i], error = "" };
                var started = DateTime.UtcNow;
                try { run[i](); result.passed = true; }
                catch (Exception error) { result.error = error.ToString(); }
                result.seconds = (DateTime.UtcNow - started).TotalSeconds;
                results.Add(result);
            }
        }
        finally
        {
            // PlayerControlsTests intentionally exercises persistence. Restore both
            // original values AND original key absence so build QA changes no preference.
            if (existed[0]) PlayerPrefs.SetFloat(keys[0], sensitivity); else PlayerPrefs.DeleteKey(keys[0]);
            if (existed[1]) PlayerPrefs.SetFloat(keys[1], fov); else PlayerPrefs.DeleteKey(keys[1]);
            if (existed[2]) PlayerPrefs.SetInt(keys[2], invertX); else PlayerPrefs.DeleteKey(keys[2]);
            if (existed[3]) PlayerPrefs.SetInt(keys[3], invertY); else PlayerPrefs.DeleteKey(keys[3]);
            PlayerPrefs.Save();
            Environment.SetEnvironmentVariable("TEMP", previousTemp, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("TMP", previousTmp, EnvironmentVariableTarget.Process);
        }
        bool passed = results.Count == 3 && results.TrueForAll(result => result.passed);
        var report = new RegressionReport { passed = passed, unityVersion = Application.unityVersion,
            scope = "Unity Editor batch-mode production input, object interaction and release-physics regression suites. These are not native-player or OS-input checks. Preference values/key absence are restored and temporary reports stay in the run directory.", suites = results.ToArray() };
        File.WriteAllText(Path.Combine(directory, "editor-regression-results.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        if (!passed) throw new InvalidOperationException("Required player shell regression suite failed. See " + directory);
    }

    private static void ConfigureMutableFinalWindow()
    {
        Scene scene = EditorSceneManager.OpenScene(Scenes[2], OpenSceneMode.Single);
        if (ConfigureFinalWindowBatching(scene) > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save generated window batching settings.");
        }
    }

    // Also called on the in-memory build scene by GalleryFinalWindowBuildProcessor,
    // so ordinary Unity builds retain the fix without rewriting canonical scenes.
    internal static int ConfigureFinalWindowBatching(Scene scene)
    {
        ShadeLink.Gallery.GalleryGame game = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            game = root.GetComponentInChildren<ShadeLink.Gallery.GalleryGame>(true);
            if (game != null) break;
        }
        if (game == null || !game.conceptLayout) return 0;
        MeshRenderer last = null; float furthest = float.NegativeInfinity;
        foreach (var renderer in game.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name != ShadeLink.Gallery.GalleryFinalWindowAppearance.PaneName) continue;
            float z = game.transform.InverseTransformPoint(renderer.bounds.center).z;
            if (z > furthest) { furthest = z; last = renderer; }
        }
        if (last == null) throw new InvalidOperationException("Authored final window is missing before player batching.");
        Transform group = last.transform.parent;
        while (group != null && group != game.transform && group.name != "Classical arched exterior window") group = group.parent;
        if (group == null || group == game.transform) throw new InvalidOperationException("Final window ownership is ambiguous.");
        int changed = 0;
        foreach (var node in group.GetComponentsInChildren<Transform>(true))
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(node.gameObject);
            if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
            GameObjectUtility.SetStaticEditorFlags(node.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            changed++;
        }
        Debug.Log("POLISH_MUTABLE_FINAL_WINDOW_BATCHING authoredZ=" + furthest + " changed=" + changed + " originalSceneUntouched=true");
        return changed;
    }
    private static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Shade Link Team";
        PlayerSettings.productName = "Parallax";
        PlayerSettings.bundleVersion = global::ParallaxGameShell.PreviewVersion;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets.Length == 0)
            throw new InvalidOperationException("Preview PlayerSettings could not be loaded.");
        var settings = new SerializedObject(assets[0]);
        SerializedProperty input = settings.FindProperty("activeInputHandler");
        if (input == null)
            throw new InvalidOperationException("Unity 6000.3 PlayerSettings activeInputHandler is missing.");
        input.intValue = 0; // Input Manager: the preview uses the classic Input API.
        SerializedProperty nativeResolution = settings.FindProperty("defaultIsNativeResolution");
        if (nativeResolution != null) nativeResolution.boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();

        QualitySettings.vSyncCount = 1;
        // targetFrameRate is a runtime property. ParallaxGameShell must also set
        // these defaults in Awake/Start so they take effect in the Windows player.
        Application.targetFrameRate = 60;
        Application.runInBackground = false;
    }

    private static void ConfigurePreviewImports()
    {
        const string folder = "Assets/Parallax/Shared/Resources/Preview";
        if (!AssetDatabase.IsValidFolder(folder))
            throw new DirectoryNotFoundException("Preview resources are missing: " + folder);

        int textures = 0, audioClips = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Texture importer is missing: " + path);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 4096;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            standalone.format = TextureImporterFormat.RGBA32;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
            textures++;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder + "/Lamp", folder + "/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Audio importer is missing: " + path);
            AudioImporterSampleSettings samples = importer.defaultSampleSettings;
            samples.loadType = AudioClipLoadType.DecompressOnLoad;
            samples.preloadAudioData = true;
            samples.compressionFormat = AudioCompressionFormat.PCM;
            samples.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = samples;
            importer.SetOverrideSampleSettings("Standalone", samples);
            importer.loadInBackground = false;
            // Preserve source channel counts and the existing forceToMono setting.
            // Unity exposes Normalize through the importer SerializedObject only.
            var serialized = new SerializedObject(importer);
            SerializedProperty normalize = serialized.FindProperty("m_Normalize")
                ?? serialized.FindProperty("normalize");
            if (normalize == null)
                throw new InvalidOperationException("Audio importer Normalize property is missing: " + path);
            normalize.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            audioClips++;
        }

        foreach (string key in new[] { "BodyFont", "TitleFont" })
        {
            string path = folder + "/" + key + ".ttf";
            var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (importer == null)
                throw new FileNotFoundException("Full preview font is missing: " + path, path);
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
            importer.SaveAndReimport();
        }

        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Background.png");
        if (background == null || background.width != 1672 || background.height != 941)
            throw new InvalidOperationException("Background import must retain the original 1672 x 941 pixels.");
        Debug.Log("PLAYER_SHELL_IMPORTS_OK textures=" + textures + " audio=" + audioClips
            + " fonts=2 background=" + background.width + "x" + background.height);
    }

    private static void WriteDistributionCredits(string output)
    {
        ShadeLink.Editor.GalleryBuild.WriteCredits(output);
        File.Copy("Assets/Parallax/Gallery/ThirdParty/PassageAudio/CREDITS.txt", Path.Combine(output, "GalleryFallAudio-CREDITS.txt"), true);
        File.Copy("Assets/Parallax/Tutorial/ThirdParty/ElevatorE01/CREDITS.txt", Path.Combine(output, "ElevatorE01-CREDITS.txt"), true);
        ShadeLink.Editor.TutorialElevatorAudioAssets.WriteCredits(output);
        ShadeLink.Editor.BeachAudioAssets.WriteCredits(output);
        ShadeLink.Editor.BeachHarborAssetPreparation.WriteCredits(output);
        File.Copy("Documentation/Beach/CounterweightHarbor.md", Path.Combine(output, "CounterweightHarbor-Credits-and-Controls.md"), true);
        // These fifteen live bridge/flight clips were previously missing from
        // the readable sidecars. Preserve their complete source/edit/license text.
        const string endingAudio = "Assets/Parallax/Beach/Resources/Beach/EndingAudio/";
        File.Copy(endingAudio + "CREDITS.txt", Path.Combine(output, "HarborEndingAudio-CREDITS.txt"), true);
        File.Copy(endingAudio + "EndingAudioManifest.json", Path.Combine(output, "HarborEndingAudio-selection.json"), true);
        File.Copy("Documentation/Beach/HarborEndingSound-Credits-and-Controls.txt", Path.Combine(output, "HarborEndingSound-Credits-and-Controls.txt"), true);
        ShadeLink.Eclipse.Editor.EclipseEditorBuild.WriteCredits(output);
        const string shellSources = "Assets/Parallax/Shared/ThirdParty/PlayerShellSources";
        if (!Directory.Exists(shellSources))
            throw new DirectoryNotFoundException("Player shell source notices are missing: " + shellSources);
        string notices = Path.Combine(output, "ThirdPartyNotices", "PlayerShell");
        foreach (string source in Directory.GetFiles(shellSources, "*", SearchOption.AllDirectories))
        {
            if (source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string relative = source.Substring(shellSources.Length).TrimStart('/', '\\');
            string destination = Path.Combine(notices, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
    }
    private static void CreateMainMenuScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Parallax/Shared/Scenes"));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Parallax").AddComponent<ParallaxGameShell>();
        var cameraObject = new GameObject("Preview Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        camera.orthographic = true;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 10f;
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Could not save preview scene: " + ScenePath);
        EditorBuildSettings.scenes = Array.ConvertAll(Scenes, path => new EditorBuildSettingsScene(path, true));
    }
}
