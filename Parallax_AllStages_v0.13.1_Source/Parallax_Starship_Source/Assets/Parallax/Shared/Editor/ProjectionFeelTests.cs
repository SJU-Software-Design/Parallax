using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class ProjectionFeelTests
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var objects=new List<GameObject>();
            try
            {
                var cameraObject=new GameObject("Projection camera");objects.Add(cameraObject);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=60;camera.aspect=1;
                var prop=new GameObject("Projection shape");objects.Add(prop);prop.transform.position=Vector3.forward*4;
                var box=prop.AddComponent<BoxCollider>();
                var wall=new GameObject("Projection wall");objects.Add(wall);wall.transform.position=Vector3.forward*9.1f;
                wall.AddComponent<BoxCollider>().size=new Vector3(100,100,.2f);
                Physics.SyncTransforms();
                var solver=new PerspectivePlacement(prop.transform,new[]{box},Vector3.zero,1,4);
                var result=solver.Resolve(camera,Quaternion.identity,null,~0,.001f,1000,Vector3.one*-2000,Vector3.one*2000);
                float expected=(9-.002f)*.99f/1.125f;
                Require(result.Feasible&&Mathf.Abs(result.Depth-expected)<.003f,"Cube thickness/clearance did not produce analytic centre depth");
                UnityEngine.Object.DestroyImmediate(wall);objects.Remove(wall);
                result=solver.Resolve(camera,Quaternion.identity,null,~0,.001f,1000,Vector3.one*-2000,Vector3.one*2000);
                Require(!result.HasSurface&&Mathf.Abs(result.Depth-400)<.001f,"Empty space did not use the 400-unit projection fallback");
                box.size=new Vector3(12,1,1);
                solver=new PerspectivePlacement(prop.transform,new[]{box},Vector3.zero,1,4);
                var post=new GameObject("Offscreen obstruction");objects.Add(post);post.transform.position=new Vector3(6,0,8);
                post.AddComponent<BoxCollider>().size=new Vector3(1,1,.2f);Physics.SyncTransforms();
                Vector3 screen=camera.WorldToViewportPoint(post.transform.position);
                Require(screen.x>1,"Fixture obstruction is not outside the screen");
                result=solver.Resolve(camera,Quaternion.identity,null,~0,.001f,1000,Vector3.one*-2000,Vector3.one*2000);
                Require(result.HasSurface&&result.Depth<8,"Offscreen part of the held silhouette ignored an obstruction");
                Debug.Log("PROJECTION_FEEL_TESTS_OK 3");
            }
            finally
            {
                foreach(var value in objects)if(value!=null)UnityEngine.Object.DestroyImmediate(value);
                Physics.SyncTransforms();
            }
        }
        static void Require(bool value,string message){if(!value)throw new Exception("PROJECTION_FEEL: "+message);}
    }
}
