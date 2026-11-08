using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class PlacementSupportTests
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var objects=new List<GameObject>();var mode=Physics.simulationMode;
            GameObject Box(string name,Vector3 at,Vector3 size)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(go);go.name=name;
                go.transform.position=at;go.transform.localScale=size;return go;
            }
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                var decks=new[]{
                    Box("Left island",new Vector3(-4,.15f,35.2f),new Vector3(3,.3f,2.3f)),
                    Box("Right island",new Vector3(4.5f,.15f,38.5f),new Vector3(3,.3f,2.1f)),
                    Box("Left finger",new Vector3(-3.6f,.15f,33.95f),new Vector3(1.5f,.3f,1.3f)),
                    Box("Right finger",new Vector3(4.1f,.15f,39.9f),new Vector3(1.6f,.3f,1.4f))};
                var root=new GameObject("Diagonal plank");objects.Add(root);
                root.transform.SetPositionAndRotation(new Vector3(-.25f,.302f,36.25f),Quaternion.Euler(0,48,0));
                root.transform.localScale=Vector3.one*2.8f;
                var mesh=Box("Plank surface",Vector3.zero,Vector3.one);mesh.transform.SetParent(root.transform,false);
                mesh.transform.localPosition=Vector3.up*.1f;mesh.transform.localScale=new Vector3(1.25f,.2f,2.35f);
                var shape=mesh.GetComponent<Collider>();var renderer=mesh.GetComponent<Renderer>();var original=renderer.sharedMaterial;
                var body=root.AddComponent<Rigidbody>();body.isKinematic=true;
                var support=root.AddComponent<PlacementSupport>();support.Initialize(body,new[]{renderer});
                var playerObject=new GameObject("Platform test player");objects.Add(playerObject);
                playerObject.transform.position=new Vector3(100,0,100);
                var player=playerObject.AddComponent<CharacterController>();player.height=1.8f;player.center=Vector3.up*.9f;player.radius=.28f;player.skinWidth=.02f;
                support.SetPlayer(player);support.Released();body.isKinematic=false;body.useGravity=true;Physics.SyncTransforms();
                for(int i=0;i<600;i++){Physics.Simulate(.01f);support.Tick(.01f);}
                Require(body.IsSleeping()&&Mathf.Abs(root.transform.position.y-.30f)<.02f,"The diagonal plank must physically rest across both islands.");
                Require(support.Ready,"A resting diagonal plank must activate even when five sampled points miss its supports.");
                Require(renderer.sharedMaterial==original&&PlacementSupport.IsUsableGround(shape)&&!Physics.GetIgnoreCollision(player,shape),"Ready must restore the solid material, usable platform and player collision.");
                player.enabled=false;playerObject.transform.position=new Vector3(root.transform.position.x,shape.bounds.max.y+.6f,root.transform.position.z);
                player.enabled=true;Physics.SyncTransforms();
                for(int i=0;i<40;i++)player.Move(Vector3.down*.06f);
                Require(Mathf.Abs(playerObject.transform.position.y-shape.bounds.max.y)<.05f,"The player must actually stand on the diagonal plank; feet="+playerObject.transform.position+", surface="+shape.bounds.max.y+", ignored="+Physics.GetIgnoreCollision(player,shape));
                support.BeginHold();support.Tick(1);
                Require(!support.Ready&&Physics.GetIgnoreCollision(player,shape),"Regrabbing must immediately disable the platform.");
                support.Released();support.Tick(.1f);Require(!support.Ready,"A release still requires continuous settling.");
                support.Tick(.3f);Require(support.Ready,"A supported release must reactivate after settling.");
                foreach(var deck in decks)deck.SetActive(false);
                support.Tick(.4f);Require(!support.Ready,"Removing both supports must disable the platform.");
                body.isKinematic=true;root.transform.localScale=Vector3.one;
                root.transform.SetPositionAndRotation(new Vector3(100,5,0),Quaternion.identity);body.position=root.transform.position;body.rotation=root.transform.rotation;
                Box("Side wall",new Vector3(101.12f,5.1f,0),new Vector3(1,3,4));
                Box("Ceiling",new Vector3(100,5.7f,0),new Vector3(4,1,4));Physics.SyncTransforms();
                support.Released();support.Tick(1);
                Require(!support.Ready,"Side-wall and ceiling contact must not count as support from below.");
                Debug.Log("PLACEMENT_SUPPORT_TESTS_OK diagonal landing, real player standing, materials/collision, regrab/settling, support removal, wall/ceiling rejection");
            }
            finally
            {
                Physics.simulationMode=mode;
                foreach(var go in objects)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
