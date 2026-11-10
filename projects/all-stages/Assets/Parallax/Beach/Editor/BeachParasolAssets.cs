using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        static partial void ConfigureParasols(BeachGame game)
        {
            const string sourcePath="Assets/ThirdParty/ToonTasticBeachProps/";
            var importer=AssetImporter.GetAtPath(sourcePath+"beachprops.fbx") as ModelImporter;
            if(importer==null)throw new InvalidOperationException("ToonTastic model is missing.");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath+"Prop_BeachUmbrella.prefab");
            if(source==null)throw new InvalidOperationException("ToonTastic umbrella prefab is missing.");
            var instance=UnityEngine.Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var filter=instance.GetComponentInChildren<MeshFilter>();var renderer=filter.GetComponent<Renderer>();
                var original=filter.sharedMesh;float scale=2.36f/renderer.bounds.size.y;
                var origin=new Vector3(renderer.bounds.center.x,renderer.bounds.min.y,renderer.bounds.center.z);
                var matrix=Matrix4x4.TRS(-origin*scale,Quaternion.identity,Vector3.one*scale)*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                // Rebuild channels rather than retain the FBX's packed vertex layout.
                var mesh=new Mesh{name="ToonTastic Umbrella C",indexFormat=original.indexFormat,
                    vertices=original.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                    normals=original.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray(),
                    uv=original.uv,triangles=original.triangles};
                mesh.RecalculateBounds();mesh=Save(mesh,Generated+"Art_Umbrella.asset");
                var triangles=mesh.triangles;var uv=mesh.uv;
                // The supplied palette UVs separate fabric from the pole.
                var cloth=Enumerable.Range(0,triangles.Length/3).Where(i=>uv[triangles[i*3]].x<.55f&&uv[triangles[i*3+1]].x<.55f&&uv[triangles[i*3+2]].x<.55f)
                    .SelectMany(i=>new[]{triangles[i*3],triangles[i*3+1],triangles[i*3+2]}).ToArray();
                if(cloth.Length==0)throw new InvalidOperationException("ToonTastic canopy is missing.");
                var canopy=new Mesh{name="ToonTastic Umbrella C canopy",indexFormat=mesh.indexFormat,vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,triangles=cloth};
                var points=canopy.vertices;var bounds=new Bounds(points[cloth[0]],Vector3.zero);
                foreach(int i in cloth)bounds.Encapsulate(points[i]);canopy.bounds=bounds;
                canopy=Save(canopy,Generated+"Art_Umbrella_Canopy.asset");
                var shader=Shader.Find("Parallax/BeachSurface");
                var textureImporter=AssetImporter.GetAtPath(sourcePath+"ColorPalette_256.png") as TextureImporter;
                if(shader==null||ShaderUtil.ShaderHasError(shader)||textureImporter==null)throw new InvalidOperationException("ToonTastic shader or palette is missing.");
                if(!textureImporter.sRGBTexture||textureImporter.anisoLevel!=8)
                {textureImporter.sRGBTexture=true;textureImporter.anisoLevel=8;textureImporter.SaveAndReimport();}
                var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath+"ColorPalette_256.png");
                var material=Save(new Material(shader){name="ToonTastic Umbrella palette",color=Color.white,mainTexture=palette},Generated+"Art_Umbrella_0.mat");
                foreach(var f in game.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&f.sharedMesh.name=="umbrella original imported mesh"))
                {f.sharedMesh=mesh;f.GetComponent<Renderer>().sharedMaterials=new[]{material};}
                foreach(var host in game.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Beach resort parasol · ")).ToArray())
                {
                    var old=host.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name.StartsWith("Resort canopy · "));var footprint=old.sharedMesh.bounds;
                    // Inactive authored originals keep independently edited scenes mergeable.
                    foreach(var f in host.GetComponentsInChildren<MeshFilter>().Where(f=>new[]{"Resort canopy · ","Resort valance · ","Resort piping · ","Resort ribs · ","Resort vent · "}.Any(p=>f.sharedMesh.name.StartsWith(p))).ToArray())
                    {f.GetComponent<Renderer>().enabled=false;foreach(var c in f.GetComponents<Collider>())c.enabled=false;f.gameObject.SetActive(false);}
                    var roof=new GameObject("ToonTastic Umbrella C shelter canopy");roof.transform.SetParent(host,false);
                    roof.transform.localScale=new Vector3(footprint.size.x/bounds.size.x,1,footprint.size.z/bounds.size.z);
                    roof.transform.localPosition=new Vector3(0,footprint.max.y-bounds.max.y,0);
                    roof.AddComponent<MeshFilter>().sharedMesh=canopy;var r=roof.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.TwoSided;
                    roof.AddComponent<MeshCollider>().sharedMesh=canopy;
                }
                foreach(var prop in game.props)
                {
                    var roof=prop.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c=>c.name=="파라솔 차양");
                    if(roof!=null){roof.center=bounds.center;roof.size=bounds.size;}
                    prop.Remember();
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
    }
}
