using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Clean material presets for the new concept-world builder. There are no
    // geometry, collider, input, or stage-coordinate assumptions in this class.
    public static class GalleryMuseumMaterials
    {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();

        static Texture2D Texture(string key)
        {
            if(textures.TryGetValue(key,out var texture)&&texture!=null)return texture;
            texture=Resources.Load<Texture2D>("MuseumMaterials/"+key);
            if(texture==null)throw new InvalidOperationException("Museum material texture is missing: "+key);
            textures[key]=texture;return texture;
        }

        static void Surface(Material material,string prefix,float repeats,float normal,float roughness)
        {
            material.SetTexture("_SurfaceAlbedo",Texture(prefix+"Albedo"));
            material.SetTexture("_SurfaceNormal",Texture(prefix+"Normal"));
            material.SetTexture("_SurfaceRoughness",Texture(prefix+"Roughness"));
            material.SetTexture("_SurfaceOcclusion",Texture2D.whiteTexture);
            material.SetFloat("_UseSurfaceMaps",1);
            material.SetFloat("_WorldTiling",repeats);
            material.SetFloat("_NormalStrength",normal);
            material.SetFloat("_Roughness",roughness);
            material.SetFloat("_Grid",0);
            material.color=new Color(.96f,.95f,.92f);
        }

        public static void Apply(Material material,string category)
        {
            if(material==null)throw new ArgumentNullException(nameof(material));
            if(material.shader==null||material.shader.name!="Parallax/MuseumSurface")
                throw new ArgumentException("Museum presets require Parallax/MuseumSurface",nameof(material));
            string kind=(category??"").Trim().ToLowerInvariant();
            material.SetFloat("_UseSurfaceMaps",0);
            material.SetFloat("_Metallic",0);
            material.SetFloat("_NormalStrength",0);
            material.SetFloat("_Roughness",.76f);
            material.SetFloat("_ReflectionStrength",.22f);
            material.SetFloat("_AmbientStrength",1);
            material.SetFloat("_ContactAO",0);
            material.SetFloat("_ContactDistance",.65f);
            material.SetFloat("_ExhibitLight",0);
            material.SetColor("_EmissionColor",Color.black);

            switch(kind)
            {
                case "stone":
                case "marble":
                case "limestone":
                case "travertine":
                case "floor":
                    Surface(material,"Stone",1f/3f,.42f,.65f);
                    material.SetFloat("_ReflectionStrength",.55f);break;
                case "plaster":
                case "wall":
                    Surface(material,"Plaster",1f/3,.35f,.95f);
                    material.SetFloat("_ReflectionStrength",.10f);break;
                case "wood":
                case "walnut":
                    Surface(material,"Wood",1,.55f,.73f);
                    material.SetFloat("_ReflectionStrength",.32f);break;
                case "bronze":
                case "brass":
                case "gold":
                    material.SetFloat("_Metallic",.82f);
                    material.SetFloat("_Roughness",.30f);
                    material.SetFloat("_ReflectionStrength",.80f);break;
                case "artwork":
                case "painting":
                case "print":
                    material.SetFloat("_ExhibitLight",.75f);
                    material.SetFloat("_Roughness",.91f);
                    material.SetFloat("_ContactAO",0);
                    material.SetFloat("_ReflectionStrength",.02f);break;
                case "paper":
                    material.SetFloat("_ExhibitLight",.18f);
                    material.SetFloat("_Roughness",.92f);
                    material.SetFloat("_ReflectionStrength",.03f);break;
                case "ink":
                    material.SetFloat("_Roughness",.86f);
                    material.SetFloat("_ReflectionStrength",.08f);break;
                default:
                    throw new ArgumentException("Unknown museum material category: "+category,nameof(category));
            }
        }
    }
}
