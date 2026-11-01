using System.IO;
using UnityEngine;

namespace Parallax.Editor
{
    public static class BuildPaths
    {
        public static string Output(string relativePath)
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var parent=new DirectoryInfo(project).Parent;
            string repository=null;
            while(parent!=null)
            {
                if(Directory.Exists(Path.Combine(parent.FullName,"projects"))&&Directory.Exists(Path.Combine(parent.FullName,"scripts")))
                { repository=parent.FullName;break; }
                parent=parent.Parent;
            }
            string platform=relativePath.EndsWith(".app",System.StringComparison.OrdinalIgnoreCase)?"macos":"windows";
            string output=repository==null?Path.Combine(project,"Builds",platform):
                Path.Combine(repository,"builds",platform,new DirectoryInfo(project).Name);
            return Path.Combine(output,relativePath);
        }
    }
}
