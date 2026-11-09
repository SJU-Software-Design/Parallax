using System;
using System.Linq;
using System.Collections.Generic;
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
            const string sourcePath="Assets/ThirdParty/CartoonUmbrella/";
            var importer=AssetImporter.GetAtPath(sourcePath+"Umbrella.fbx") as ModelImporter;
            if(importer==null)throw new InvalidOperationException("Cartoon Umbrella model is missing.");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath+"Umbrella.prefab");
            if(source==null)throw new InvalidOperationException("Cartoon Umbrella prefab is missing.");
            var instance=UnityEngine.Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var filter=instance.GetComponentInChildren<MeshFilter>();var renderer=filter.GetComponent<Renderer>();
                var original=filter.sharedMesh;float scale=2.36f/renderer.bounds.size.y;
                var origin=new Vector3(renderer.bounds.center.x,renderer.bounds.min.y,renderer.bounds.center.z);
                var matrix=Matrix4x4.TRS(-origin*scale,Quaternion.identity,Vector3.one*scale)*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                var vertices=original.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                var normals=original.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();
                var clothSlots=Enumerable.Range(0,renderer.sharedMaterials.Length).Where(i=>renderer.sharedMaterials[i].name!="Pole").ToArray();
                if(clothSlots.Length!=2||renderer.sharedMaterials.Length!=3)throw new InvalidOperationException("Expected white/red cloth and a separate pole.");
                var clothIds=new HashSet<int>(clothSlots.SelectMany(i=>original.GetTriangles(i)));
                var poleIds=Enumerable.Range(0,original.subMeshCount).Except(clothSlots).SelectMany(i=>original.GetTriangles(i)).Distinct().ToArray();
                if(poleIds.Any(clothIds.Contains))throw new InvalidOperationException("Pole and fabric must have independent vertices.");
                var unchanged=(Vector3[])vertices.Clone();float top=clothIds.Max(i=>vertices[i].y);
                // Keep the dome, but discard the padded underside and render one fabric surface from both sides.
                foreach(int i in clothIds){vertices[i].y=top+(vertices[i].y-top)*.8f;normals[i]=new Vector3(normals[i].x,normals[i].y/.8f,normals[i].z).normalized;}
                int offset=vertices.Length;
                var mesh=new Mesh{name="Fabric parasol B",indexFormat=IndexFormat.UInt32,vertices=vertices.Concat(vertices).ToArray(),normals=normals.Concat(normals.Select(n=>-n)).ToArray(),uv=original.uv.Concat(original.uv).ToArray(),subMeshCount=original.subMeshCount};
                foreach(int slot in Enumerable.Range(0,original.subMeshCount))
                {
                    var indices=original.GetTriangles(slot);
                    if(clothSlots.Contains(slot))
                    {
                        var sheet=new List<int>();
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            int a=indices[i],b=indices[i+1],c=indices[i+2];
                            if(Vector3.Cross(unchanged[b]-unchanged[a],unchanged[c]-unchanged[a]).y<=.0000001f)continue;
                            sheet.AddRange(new[]{a,b,c,a+offset,c+offset,b+offset});
                        }
                        if(sheet.Count==0)throw new InvalidOperationException("Fabric surface is empty.");
                        indices=sheet.ToArray();
                    }
                    mesh.SetTriangles(indices,slot);
                }
                mesh.RecalculateBounds();mesh=Save(mesh,Generated+"Art_FabricParasol.asset");
                var canopy=new Mesh{name="Fabric parasol B canopy",indexFormat=IndexFormat.UInt32,vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,subMeshCount=clothSlots.Length};
                for(int i=0;i<clothSlots.Length;i++)canopy.SetTriangles(mesh.GetTriangles(clothSlots[i]),i);
                var used=canopy.triangles;var points=canopy.vertices;var bounds=new Bounds(points[used[0]],Vector3.zero);
                foreach(int i in used)bounds.Encapsulate(points[i]);canopy.bounds=bounds;
                if(bounds.size.y<.45f||bounds.size.y>.55f||poleIds.Any(i=>points[i]!=unchanged[i]))throw new InvalidOperationException("Fabric dome or pole dimensions changed unexpectedly.");
                canopy=Save(canopy,Generated+"Art_FabricParasol_Canopy.asset");
                var shader=Shader.Find("Parallax/BeachSurface");
                if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Parasol shader failed.");
                var materials=renderer.sharedMaterials.Select((m,i)=>Save(new Material(shader){name="Fabric parasol "+m.name,color=m.color},Generated+"Art_FabricParasol_"+i+".mat")).ToArray();
                foreach(var f in game.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&f.sharedMesh.name=="umbrella original imported mesh"))
                {f.sharedMesh=mesh;f.GetComponent<Renderer>().sharedMaterials=materials;}
                foreach(var host in game.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Beach resort parasol · ")).ToArray())
                {
                    var old=host.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name.StartsWith("Resort canopy · "));var footprint=old.sharedMesh.bounds;
                    // Keep authored originals inactive so independent scene changes merge cleanly.
                    foreach(var f in host.GetComponentsInChildren<MeshFilter>().Where(f=>new[]{"Resort canopy · ","Resort valance · ","Resort piping · ","Resort ribs · ","Resort vent · "}.Any(p=>f.sharedMesh.name.StartsWith(p))).ToArray())
                    {f.GetComponent<Renderer>().enabled=false;foreach(var c in f.GetComponents<Collider>())c.enabled=false;f.gameObject.SetActive(false);}
                    var roof=new GameObject("Fabric parasol B shelter canopy");roof.transform.SetParent(host,false);
                    roof.transform.localScale=new Vector3(footprint.size.x/bounds.size.x,1,footprint.size.z/bounds.size.z);
                    roof.transform.localPosition=new Vector3(0,footprint.max.y-bounds.max.y,0);
                    roof.AddComponent<MeshFilter>().sharedMesh=canopy;var r=roof.AddComponent<MeshRenderer>();r.sharedMaterials=clothSlots.Select(i=>materials[i]).ToArray();r.shadowCastingMode=ShadowCastingMode.TwoSided;
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
