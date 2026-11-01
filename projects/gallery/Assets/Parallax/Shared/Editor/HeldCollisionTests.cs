using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class HeldCollisionTests
    {
        sealed class Fixture:IDisposable
        {
            public readonly GameObject root=new GameObject("Held collision fixture");
            public readonly Rigidbody body;
            public readonly BoxCollider box;
            public readonly Component state;
            public Fixture()
            {
                root.layer=8;root.transform.position=new Vector3(0,100,4);
                box=root.AddComponent<BoxCollider>();body=root.AddComponent<Rigidbody>();
                body.isKinematic=true;body.useGravity=false;
                var type=typeof(FirstPersonMotor).Assembly.GetType("ShadeLink.HeldCollisionState");
                Require(type!=null,"HeldCollisionState is missing");
                state=(Component)type.GetMethod("Attach").Invoke(null,new object[]{body});
            }
            public void Call(string method)
            {
                var member=state.GetType().GetMethod(method,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                Require(member!=null,"Missing held collision method "+method);
                try{member.Invoke(state,null);}catch(TargetInvocationException error){throw error.InnerException;}
                Physics.SyncTransforms();
            }
            public GameObject Child(string name,int layer,bool active=true)
            {
                var value=new GameObject(name);value.transform.SetParent(root.transform,false);value.layer=layer;
                value.SetActive(active);return value;
            }
            public void Dispose(){UnityEngine.Object.DestroyImmediate(root);Physics.SyncTransforms();}
        }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var errors=new List<string>();
            int checks=6;
            Check(errors,"owned collider and inactive hierarchy layers restore exactly",()=>
            {
                using(var f=new Fixture())
                {
                    var child=f.Child("Owned shape",12);var part=child.AddComponent<BoxCollider>();
                    var inactive=f.Child("Inactive visual",17,false);
                    f.Call("Begin");
                    Require(f.root.layer==29&&child.layer==29&&inactive.layer==29,"Owned hierarchy did not use the held layer");
                    Require(f.box.enabled&&part.enabled,"Held colliders were disabled instead of solver-isolated");
                    f.Call("Restore");Require(f.root.layer==8&&child.layer==12&&inactive.layer==17,"Original layers were lost");
                }
            });
            Check(errors,"nested independent rigidbody keeps its layers",()=>
            {
                using(var f=new Fixture())
                {
                    var child=f.Child("Independent body",15);child.AddComponent<Rigidbody>().useGravity=false;
                    child.AddComponent<BoxCollider>();var detail=new GameObject("Nested detail");detail.transform.SetParent(child.transform,false);detail.layer=16;
                    f.Call("Begin");Require(child.layer==15&&detail.layer==16,"Held state changed an independent rigidbody owner");
                    f.Call("Restore");
                }
            });
            Check(errors,"holding does not push a dynamic prop and release restores contact",()=>
            {
                using(var f=new Fixture())
                {
                    var loose=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var previous=Physics.simulationMode;
                    try
                    {
                        loose.transform.localScale=Vector3.one*.5f;loose.transform.position=f.root.transform.position+Vector3.right*.7f;
                        var body=loose.AddComponent<Rigidbody>();body.useGravity=false;
                        Physics.simulationMode=SimulationMode.Script;
                        f.Call("Begin");Vector3 start=body.position;
                        for(int i=0;i<10;i++)Physics.Simulate(.02f);
                        Require(Vector3.Distance(start,body.position)<.001f,"Kinematic held prop pushed a loose body");
                        f.Call("Restore");body.WakeUp();
                        for(int i=0;i<10;i++)Physics.Simulate(.02f);
                        Require(body.position.x>start.x+.02f,"Returning to world did not restore physical contacts");
                    }
                    finally{Physics.simulationMode=previous;UnityEngine.Object.DestroyImmediate(loose);}
                }
            });
            Check(errors,"solver isolation leaves collider queries available",()=>
            {
                using(var f=new Fixture())
                {
                    f.Call("Begin");
                    Require(Physics.GetIgnoreLayerCollision(29,0),"Held layer still solver-collides with ordinary world");
                    Require(Physics.Raycast(new Vector3(0,100,0),Vector3.forward,out var hit,10,1<<29,QueryTriggerInteraction.Ignore)&&hit.collider==f.box,
                        "Held collider is no longer queryable");
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    try
                    {
                        wall.transform.position=new Vector3(0,100,9);Physics.SyncTransforms();var hits=new RaycastHit[8];
                        Require(InteractionRaycast.Nearest(new Vector3(0,100,0),Vector3.forward,null,f.root.transform,~0,ref hits,out hit,out _)&&hit.collider.gameObject==wall,
                            "Solver isolation hid an environment wall from projection queries");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(wall);}
                }
            });
            Check(errors,"regrab captures current world layers",()=>
            {
                using(var f=new Fixture())
                {
                    f.Call("Begin");f.Call("Restore");f.root.layer=9;
                    f.Call("Begin");f.Call("Restore");Require(f.root.layer==9,"Regrab restored stale layers");
                }
            });
            Check(errors,"disable restores an isolated body",()=>
            {
                using(var f=new Fixture())
                {
                    f.Call("Begin");f.Call("OnDisable");Require(f.root.layer==8,"Disable stranded the body on the held layer");
                }
            });
            checks++;
            Check(errors,"reflection-only visual keeps its render exclusion layer",()=>
            {
                using(var f=new Fixture())
                {
                    var face=f.Child("Reflection face",31);face.AddComponent<MeshRenderer>();
                    f.Call("Begin");Require(face.layer==31,"Held state removed reflection-camera exclusion from a visual-only surface");
                    Require(f.root.layer==29,"Preserving the visual layer disabled physical isolation");
                    f.Call("Restore");Require(face.layer==31,"Visual layer did not restore");
                }
            });
            foreach(var name in new[]{"ShadeLink.Beach.BeachPlacementPreview","ShadeLink.Starship.StarshipPlacementPreview"})
            {
                var type=typeof(FirstPersonMotor).Assembly.GetType(name);if(type==null)continue;checks++;
                Check(errors,name+" uses a dedicated preview render layer",()=>
                {
                    var preview=(int)type.GetField("PreviewLayer",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
                    Require(preview==30&&preview!=LayerMask.NameToLayer("HeldProp"),"Shadow-preview culling includes unrelated held renderers");
                });
            }
            if(errors.Count>0)throw new Exception("HELD_COLLISION_TESTS_FAILED\n"+string.Join("\n",errors));
            Debug.Log("HELD_COLLISION_TESTS_OK "+checks);
        }
        static void Check(List<string> errors,string name,Action test)
        {
            try{test();Debug.Log("HELD_PASS "+name);}
            catch(Exception error){errors.Add(name+": "+error.Message);Debug.Log("HELD_FAIL "+name+": "+error.Message);}
        }
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
    }
}
