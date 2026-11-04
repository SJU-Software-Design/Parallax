using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Read-only asset selection for a review checkout. Invoke after the final
    // Gallery scene has been prepared and saved. It never changes source assets.
    public static class GalleryReviewManifest
    {
        const string GalleryRoot = "Assets/Parallax/Gallery";
        const string ScenePath = GalleryRoot + "/Scenes/Gallery.unity";
        const long FileLimit = 100000000;

        [Serializable] sealed class FileEntry
        {
            public string path, sha256;
            public long bytes;
        }

        [Serializable] sealed class Manifest
        {
            public string schema = "parallax-museum-review-v1";
            public string version, unity, savedUtc, scene;
            public bool valid;
            public string[] sceneDependencies, generatedAssets, externalDependencies, enabledBuildScenes, oversizedFiles;
            public FileEntry[] reviewFiles;
        }

        [MenuItem("Parallax/미술관 PR 검토 목록 저장")]
        public static void Save()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Prepare and save the museum scene first.", ScenePath);

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] dependencies = AssetDatabase.GetDependencies(new[] { ScenePath }, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (string dependency in dependencies)
                if (dependency.StartsWith(GalleryRoot + "/", StringComparison.Ordinal))
                    AddAsset(project, dependency, selected);

            // Resources are loaded by name as well as referenced by the scene.
            // Keep the complete museum source and Resources directories, while
            // selecting Generated only through the saved scene's dependency graph.
            foreach (string folder in new[] { "Scripts", "Editor", "Validation", "Resources" })
            {
                string relativeFolder = GalleryRoot + "/" + folder;
                string absoluteFolder = Path.Combine(project, relativeFolder);
                if (!Directory.Exists(absoluteFolder)) continue;
                foreach (string file in Directory.GetFiles(absoluteFolder, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(project.Length + 1).Replace('\\', '/');
                    AddAsset(project, relative, selected);
                }
            }

            var entries = new List<FileEntry>(selected.Count);
            foreach (string relative in selected.OrderBy(path => path, StringComparer.Ordinal))
            {
                string absolute = Path.Combine(project, relative);
                if (!File.Exists(absolute)) throw new FileNotFoundException("Required review asset is missing.", relative);
                string hash;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(absolute))
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                entries.Add(new FileEntry { path = relative, bytes = new FileInfo(absolute).Length, sha256 = hash });
            }

            string[] oversized = entries.Where(entry => entry.bytes >= FileLimit).Select(entry => entry.path).ToArray();
            var manifest = new Manifest
            {
                version = GalleryGame.Version,
                unity = Application.unityVersion,
                savedUtc = DateTime.UtcNow.ToString("O"),
                scene = ScenePath,
                valid = oversized.Length == 0,
                sceneDependencies = dependencies,
                generatedAssets = dependencies.Where(path => path.StartsWith(GalleryRoot + "/Generated/", StringComparison.Ordinal)).ToArray(),
                externalDependencies = dependencies.Where(path => !path.StartsWith(GalleryRoot + "/", StringComparison.Ordinal)).ToArray(),
                enabledBuildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                oversizedFiles = oversized,
                reviewFiles = entries.ToArray()
            };
            string output = Argument("-gallery-review-output") ?? Path.Combine(project, "Validation/Gallery/review-manifest.json");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            Debug.Log("MUSEUM_REVIEW_MANIFEST_OK files=" + entries.Count + " generated=" + manifest.generatedAssets.Length +
                " oversized=" + oversized.Length + " output=" + output);
            if (!manifest.valid) throw new InvalidOperationException("Museum review contains a file of at least 100 MB; inspect the manifest.");
        }

        static void AddAsset(string project, string relative, HashSet<string> selected)
        {
            relative = relative.Replace('\\', '/');
            if (File.Exists(Path.Combine(project, relative))) selected.Add(relative);
            if (!relative.EndsWith(".meta", StringComparison.Ordinal) && File.Exists(Path.Combine(project, relative + ".meta")))
                selected.Add(relative + ".meta");
            string parent = Path.GetDirectoryName(relative).Replace('\\', '/');
            while (parent == GalleryRoot || parent.StartsWith(GalleryRoot + "/", StringComparison.Ordinal))
            {
                if (File.Exists(Path.Combine(project, parent + ".meta"))) selected.Add(parent + ".meta");
                parent = Path.GetDirectoryName(parent).Replace('\\', '/');
            }
        }

        static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(arguments, name);
            return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
        }
    }
}
