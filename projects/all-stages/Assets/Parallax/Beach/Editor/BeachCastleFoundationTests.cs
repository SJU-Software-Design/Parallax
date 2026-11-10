using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static class BeachCastleFoundationTests
    {
        public static void Run()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Parallax/Beach/Generated/sandcastle.prefab");
            var castle=UnityEngine.Object.Instantiate(prefab);
            try
            {
                castle.transform.position=new Vector3(0,0,96);castle.transform.localScale=Vector3.one*9;
                var filter=castle.GetComponent<MeshFilter>();var source=filter.sharedMesh;
                var collider=castle.AddComponent<MeshCollider>();collider.sharedMesh=source;
                var towers=TowerVertices(source);
                float feetRadius=source.vertices.Where(v=>Mathf.Abs(v.y*9-.899694f)<.001f).Max(Radius);
                BeachCastleCut.Apply(castle.transform);
                var mesh=filter.sharedMesh;
                float topRadius=mesh.vertices.Where(v=>Mathf.Abs(v.y*9-1.080261f)<.001f).Max(Radius);
                float bottomRadius=mesh.vertices.Where(v=>Mathf.Abs(v.y)<.00001f).Max(Radius);
                if(topRadius<feetRadius+.1f||topRadius>feetRadius+.5f)
                    throw new Exception("Round foundation does not closely support the tower feet: top="+topRadius+" towers="+feetRadius);
                if(bottomRadius-topRadius<.4f||bottomRadius-topRadius>1)
                    throw new Exception("Round foundation slope extends too far from its upper platform.");
                if(!towers.SetEquals(TowerVertices(mesh)))throw new Exception("The tower geometry or shading changed with the foundation.");
                if(collider.sharedMesh!=mesh)throw new Exception("Round foundation collision mesh is stale.");
                Debug.Log("BEACH_ROUND_FOUNDATION_TESTS_OK top="+topRadius+" bottom="+bottomRadius+" towerFeet="+feetRadius+" towersUnchanged=true colliderMatches=true");
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            finally{UnityEngine.Object.DestroyImmediate(castle);}
        }
        static float Radius(Vector3 v)=>new Vector2(v.x,v.z).magnitude*9;
        static HashSet<string> TowerVertices(Mesh mesh)
        {
            var points=mesh.vertices;var normals=mesh.normals;var result=new HashSet<string>();
            for(int i=0;i<points.Length;i++)
                if(points[i].y>.13f&&Mathf.Abs(points[i].x)>.34f)
                    result.Add(points[i].ToString("F5")+normals[i].ToString("F5"));
            return result;
        }
    }
}
