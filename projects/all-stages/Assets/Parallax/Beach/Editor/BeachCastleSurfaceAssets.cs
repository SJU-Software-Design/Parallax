using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;
namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureCastleSurface(BeachGame game)
        {
            var shader=Shader.Find("Parallax/BeachTexturedSurface");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Castle sand shader failed.");
            var color=BeachTexture("YughuesCastleSand/T_YFSM_11_d.tga");var normal=BeachTexture("YughuesCastleSand/T_YFSM_11_n.tga",true);
            foreach(var material in BeachWorld.Materials.Concat(game.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)).Where(m=>m!=null).Distinct())
            {
                var c=material.color;
                bool sand=material.name.StartsWith("sandcastle material ")&&c.r>c.g&&c.g>.35f&&c.g>c.b||
                    material.name.StartsWith("Castle restoration ")&&!material.name.Contains("shell")||material.name=="Front castle golden sand"||material.name=="Front castle flat stone";
                if(!sand)continue;
                material.shader=shader;material.color=new Color(.65f,.59555f,.48665f);material.SetTexture("_MainTex",color);material.SetTexture("_BumpMap",normal);
                material.SetFloat("_BumpScale",.65f);material.SetFloat("_Tiling",1.4f);material.SetFloat("_Glossiness",.10f);material.SetFloat("_Metallic",0);
                if(material.name.Contains("recessed"))material.color=new Color(.55f,.52f,.48f);
                EditorUtility.SetDirty(material);
            }
        }

    }
}
