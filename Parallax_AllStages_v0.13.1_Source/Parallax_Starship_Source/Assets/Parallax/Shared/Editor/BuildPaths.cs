using System.IO;
using UnityEngine;

namespace Parallax.Editor
{
    public static class BuildPaths
    {
        public static string Output(string relativePath)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool mac = relativePath.EndsWith(".app", System.StringComparison.OrdinalIgnoreCase);
            string platform = Path.GetFullPath(Path.Combine(project, "../..", mac ? "맥" : "윈도우"));
            string root = Directory.Exists(platform) ? Path.Combine(platform, "실행파일") : Path.Combine(project, "Builds");
            string first = relativePath.Replace('\\', '/').Split('/')[0];
            string legacy = Path.Combine(platform, "이전버전");
            if (!mac && Directory.Exists(Path.Combine(legacy, first))) root = legacy;
            return Path.Combine(root, relativePath);
        }
    }
}
