using UnityEditor;
using UnityEngine;
using System;

namespace ShadeLink.Editor
{
    public static class PlacementBuild
    {
        public static void BuildAll()
        {
            foreach(var name in new[]{"Parallax/PlacementGhost","Parallax/PlacementDepth","Parallax/BeachSurface","Parallax/GallerySurface","Parallax/LibrarySurface"})
            {var shader=Shader.Find(name);if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Placement shader failed: "+name);}
            BeachBuild.Build();GalleryBuild.Build();LibraryBuild.Build();Debug.Log("PLACEMENT_ALL_BUILD_OK");
        }
    }
}
