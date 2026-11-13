using System;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        public static void VerifyCoastalGeometry()
        {
            foreach(var size in new[]{new Vector3(.08f,2.2f,.08f),new Vector3(1.82f,2.1f,.22f),new Vector3(3.2f,.12f,1.4f)})
            {
                var mesh=CoastalRoundedBoxMesh(size,.05f);
                try
                {
                    if((mesh.bounds.size-size).sqrMagnitude>.000001f)throw new Exception("Rounded detail changes the requested outer dimensions.");
                    var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
                    for(int i=0;i<vertices.Length;i++)if(float.IsNaN(vertices[i].sqrMagnitude)||Mathf.Abs(normals[i].magnitude-1)>.001f)throw new Exception("Invalid rounded detail vertex/normal.");
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                        if(Vector3.Dot(normal,normals[a]+normals[b]+normals[c])<=0)throw new Exception("Rounded detail has reversed or degenerate triangles.");
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            CoastalResources();
            foreach(string name in new[]{"Coastal linen weave","Coastal linen normal","Coastal painted timber grain"})
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+name+".asset")==null)throw new Exception("Missing coastal detail texture: "+name);
            AssetDatabase.SaveAssets();Debug.Log("COASTAL_COMMON_CHECK_OK meshes=3 winding=OK textures=3");
        }
    }
}
