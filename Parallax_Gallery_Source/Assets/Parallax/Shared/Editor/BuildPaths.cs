using System.IO;
using UnityEngine;

namespace Parallax.Editor
{
    public static class BuildPaths
    {
        public static string Output(string relativePath)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string delivery = Path.GetFullPath(Path.Combine(project, "../../배포자료"));
            string root = Directory.Exists(delivery) ? delivery : Path.Combine(project, "Builds");
            return Path.Combine(root, relativePath);
        }
    }
}
