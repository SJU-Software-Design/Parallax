using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;
namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureWood(BeachGame game)
        {
            var shader=Shader.Find("Parallax/BeachTexturedSurface");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Beach wood shader failed.");
            var color=BeachTexture("PlankTexturesPBR/Planks4a.tif");var normal=BeachTexture("PlankTexturesPBR/Planks4_Normal.tga",true);
            var ao=BeachTexture("PlankTexturesPBR/Planks4_Ambient_Occlusion.tga",linear:true);var metal=BeachTexture("PlankTexturesPBR/Planks4_Metallic.tga",linear:true);
            foreach(var material in BeachWorld.Materials.Concat(game.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)).Where(m=>m!=null).Distinct())
            {
                if(material.name!="Weathered timber"&&material.name!="Shadow boat timber deck")continue;
                material.shader=shader;material.color=Color.white;material.SetTexture("_MainTex",color);material.SetTexture("_BumpMap",normal);
                material.SetTexture("_OcclusionMap",ao);material.SetTexture("_MetallicGlossMap",metal);
                material.SetFloat("_BumpScale",.7f);material.SetFloat("_Tiling",.65f);material.SetFloat("_Glossiness",.32f);material.SetFloat("_Metallic",1);
                EditorUtility.SetDirty(material);
            }
        }

    }
}
