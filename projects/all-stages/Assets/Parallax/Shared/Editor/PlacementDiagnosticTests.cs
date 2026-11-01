using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ShadeLink.Editor
{
    public static class PlacementDiagnosticTests
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var objects=new List<GameObject>();
            Func<string,Vector3,Vector3,BoxCollider> box=(name,at,size)=>{var go=new GameObject(name);objects.Add(go);go.transform.position=at;var c=go.AddComponent<BoxCollider>();c.size=size;return c;};
            try
            {
                var cameraObject=new GameObject("Diagnostic camera");objects.Add(cameraObject);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=60;camera.aspect=1;
                var held=box("Held",Vector3.forward*4,Vector3.one);
                var wall=box("Far wall",Vector3.forward*9.1f,new Vector3(100,100,.2f));
                var solver=new PerspectivePlacement(held.transform,new[]{held},Vector3.zero,1,4);
                Func<float,Vector3,Vector3,float,PerspectivePlacement.Result> solve=(maximum,min,max,intent)=>solver.Resolve(camera,Quaternion.identity,null,~0,.001f,maximum,min,max,intent);
                var min=Vector3.one*-1000;var max=Vector3.one*1000;
                var r=solve(1000,min,max,float.PositiveInfinity);
                Require(r.Limiter==PerspectivePlacement.LimitKind.Surface&&r.Source==wall,"Surface diagnostic did not identify the limiting collider");
                Require(r.HasPoint&&Mathf.Abs(r.Point.z-9)<.001f,"Surface marker is not the actual winning hit");
                Require(Mathf.Abs(r.Depth-7.91824f)<.003f,"Diagnostic instrumentation changed projection depth");
                r=solve(.5f,min,max,float.PositiveInfinity);
                Require(r.Limiter==PerspectivePlacement.LimitKind.Size&&r.Source==null&&Mathf.Abs(r.Depth-2)<.001f,"Size cap was mislabeled as a surface");
                r=solve(1000,min,new Vector3(1000,1000,5),float.PositiveInfinity);
                Require(r.Limiter==PerspectivePlacement.LimitKind.Boundary&&r.Boundary=="Z 최대"&&r.HasPoint&&Mathf.Abs(r.Point.z-5)<.001f,"Boundary diagnostic did not report the actual constrained plane");
                r=solve(1000,min,max,1);
                Require(r.Limiter==PerspectivePlacement.LimitKind.Intent&&Mathf.Abs(r.Depth-1)<.001f,"Intent cap was mislabeled");
                var near=box("Near obstruction",Vector3.forward*3.1f,new Vector3(100,100,.2f));
                r=solve(1000,min,max,float.PositiveInfinity);Require(r.Source==near,"New nearer obstacle did not become the limiter");
                near.transform.position=Vector3.right*200;
                r=solve(1000,min,max,float.PositiveInfinity);Require(r.Source==wall,"Moving away left a stale limiting object");
                wall.transform.position=Vector3.forward*.103f;
                r=solve(1000,min,max,float.PositiveInfinity);
                Require(!r.Feasible&&r.Limiter==PerspectivePlacement.LimitKind.Minimum&&r.Source==wall&&r.HasPoint,"Minimum conflict erased the actual obstructing wall");
                var enclosing=box("Camera enclosure",Vector3.zero,Vector3.one);
                r=solve(1000,min,max,float.PositiveInfinity);
                Require(!r.Feasible&&r.Limiter==PerspectivePlacement.LimitKind.OriginInside&&r.Source==enclosing,"Origin-inside diagnostic lost the containing object");
                Debug.Log("PLACEMENT_DIAGNOSTICS_TESTS_OK 8");
            }
            finally{for(int i=objects.Count-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(objects[i]);Physics.SyncTransforms();}
        }
        static void Require(bool value,string message){if(!value)throw new Exception("PLACEMENT_DIAGNOSTICS: "+message);}
    }
}
