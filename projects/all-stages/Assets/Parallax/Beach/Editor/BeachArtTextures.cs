using System;
using UnityEditor;
using UnityEngine;
namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static Texture2D BeachTexture(string path,bool normal=false,bool linear=false)
        {
            path="Assets/ThirdParty/"+path;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing beach texture: "+path);
            var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;bool srgb=!normal&&!linear;
            if(importer.textureType!=type||importer.sRGBTexture!=srgb||importer.anisoLevel!=8)
            {importer.textureType=type;importer.sRGBTexture=srgb;importer.anisoLevel=8;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
