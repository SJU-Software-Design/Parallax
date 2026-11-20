#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Eclipse.Editor
{
    // Generates only the fifth scene in the throwaway StageProject. The runtime
    // owns the world; no existing MainMenu/Tutorial/Gallery/Beach scene is rebuilt.
    public static class EclipseEditorBuild
    {
        public const string ScenePath = "Assets/Parallax/Eclipse/Scenes/Eclipse.unity";
        static void RequireGeneratedProject() => global::PlayerShellBuild.RequireGeneratedProject();

        public static void Prepare()
        {
            RequireGeneratedProject();
            const string floorShaderPath = "Assets/Parallax/Eclipse/Resources/Eclipse/FloorCreditsShader.shader";
            string floorShader = File.ReadAllText(floorShaderPath, Encoding.UTF8);
            if (!System.Text.RegularExpressions.Regex.IsMatch(floorShader, @"\bZTest\s+LEqual\b") ||
                !System.Text.RegularExpressions.Regex.IsMatch(floorShader, @"\bZWrite\s+Off\b") ||
                !System.Text.RegularExpressions.Regex.IsMatch(floorShader, @"tex2D\s*\(\s*_MainTex\s*,\s*i\.uv\s*\)\.a"))
                throw new InvalidDataException("Floor credits must remain depth-tested, non-depth-writing glyph-atlas geometry.");
            Debug.Log("ECLIPSE_FLOOR_SHADER_SOURCE_GUARD ZTest=LEqual ZWrite=Off atlasAlpha=true; native validation separately checks the actual material and geometry.");
            EclipseSelectedAssetPreparation.Prepare();
            const string musicPath = "Assets/Parallax/Eclipse/Resources/Eclipse/Audio/CreditsAurora.mp3";
            var musicImporter = AssetImporter.GetAtPath(musicPath) as AudioImporter;
            if (musicImporter == null) throw new FileNotFoundException("Approved original Aurora music source is missing", musicPath);
            var sample = musicImporter.defaultSampleSettings;
            bool musicChanged = sample.loadType != AudioClipLoadType.Streaming || musicImporter.forceToMono ||
                !sample.preloadAudioData || musicImporter.loadInBackground;
            sample.loadType = AudioClipLoadType.Streaming;
            sample.preloadAudioData = true;
            musicImporter.defaultSampleSettings = sample;
            musicImporter.forceToMono = false;
            musicImporter.loadInBackground = false;
            if (musicChanged) musicImporter.SaveAndReimport();
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Parallax/Eclipse/Scenes"));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("PARALLAX · Eclipse ending").AddComponent<EclipseFinaleGame>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the generated Eclipse scene.");
            Debug.Log("ECLIPSE_ENDING_SCENE_PREPARED " + ScenePath + " originalFourScenesPreserved=true");
        }

        public static void WriteCredits(string output)
        {
            RequireGeneratedProject();
            const string sourceRoot = "Assets/Parallax/Eclipse/ThirdParty";
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException("Selected Eclipse asset sources/licenses are missing: " + sourceRoot);
            const string documentPath = "Assets/Parallax/Eclipse/Resources/Eclipse/EndingCredits.json";
            var document = JsonUtility.FromJson<EclipseCreditDocument>(File.ReadAllText(documentPath, Encoding.UTF8));
            if (document == null || document.team == null || document.team.Length != 4 || document.assets == null || document.assets.Length == 0)
                throw new InvalidDataException("Verified Eclipse team/use credits are missing or incomplete.");
            File.Copy(documentPath, Path.Combine(output, "EclipseEnding-Credits.json"), true);
            var text = new StringBuilder();
            text.AppendLine("PARALLAX — ECLIPSE ENDING CREDITS");
            text.AppendLine("세종대학교 SW기초설계 프로젝트 / 3조 Parallax");
            text.AppendLine(); text.AppendLine("만든 사람들");
            foreach (var member in document.team) { text.AppendLine(member.title); text.AppendLine(member.role ?? member.usage); text.AppendLine(); }
            text.AppendLine("사용 장면 — 자료 · 제작자");
            foreach (var entry in document.assets)
            {
                text.AppendLine(entry.usage + " — " + entry.title);
                string attribution = string.IsNullOrWhiteSpace(entry.creator) ? entry.license :
                    string.IsNullOrWhiteSpace(entry.license) ? entry.creator : entry.creator + " / " + entry.license;
                if (!string.IsNullOrWhiteSpace(attribution)) text.AppendLine(attribution);
                if (!string.IsNullOrWhiteSpace(entry.url)) text.AppendLine(entry.url);
                text.AppendLine();
            }
            text.AppendLine("THANK YOU FOR PLAYING");

            File.WriteAllText(Path.Combine(output, "EclipseEnding-Credits.txt"), text.ToString(), new UTF8Encoding(false));
            string targetRoot = Path.Combine(output, "ThirdPartyNotices", "Eclipse");
            foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = source.Substring(sourceRoot.Length).TrimStart('/', '\\');
                string target = Path.Combine(targetRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
            }
        }
    }
}
#endif
