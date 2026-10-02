using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryExhibitValidation : MonoBehaviour
    {
        public GalleryGame game;
        string output;
        readonly List<string> checks=new List<string>(), errors=new List<string>();
        readonly List<Result> results=new List<Result>();
        int poses, rays;
        [Serializable] public sealed class Result
        {
            public string title;
            public int solidParts;
            public float minimumScale, maximumScale, fallMetres;
            public bool resetPassed, cancelPassed, releasePassed;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope, failure, unity, gpu;
            public int props, casters, posesChecked, colliderShadowRays;
            public Result[] exhibits;
            public string[] checks, errors;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");
            output=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"GalleryExhibits");
            Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var routine=Run().GetEnumerator();string failure=null;
            while(true)
            {
                object next=null;bool more=false;
                try{more=routine.MoveNext();if(more)next=routine.Current;}
                catch(Exception e){failure=e.ToString();}
                if(!more)break;
                yield return next;
            }
            var report=new Report{passed=failure==null&&errors.Count==0, failure=failure,
                scope="Isolated per-exhibit fixtures: only player position is set near each original exhibit. Pickup uses camera Raycast; resizing and airborne placement use camera aim only. Real PhysX fall, cancel, zone reset, collision penetration and collision-hull ray agreement. Visible ceramic shadow fidelity is asserted separately by GalleryCeramicShadowReview. Not a full puzzle playthrough.",
                unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,props=game.props.Length,casters=game.lighting.Count,
                posesChecked=poses,colliderShadowRays=rays,exhibits=results.ToArray(),checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"exhibit-results.json"),JsonUtility.ToJson(report,true));
            if(failure!=null){Debug.Log("EXHIBIT_FAIL "+failure);Capture("failure");}
            Physics.simulationMode=previous;Application.logMessageReceived-=Log;
            Application.Quit(report.passed?0:1);
        }
        void Log(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        void Check(string message){checks.Add(message);Debug.Log("EXHIBIT_PASS "+message);}
        void PlayerNear(GalleryProp prop)
        {
            Vector3 feet=new Vector3(prop.homePosition.x,.025f,prop.homePosition.z-2.5f);
            Require(game.CanStand(feet),"Player fixture obstructed: "+prop.title);
            game.controller.enabled=false;game.controller.transform.position=feet;game.controller.enabled=true;
            game.player.feet=feet;game.verticalVelocity=game.exposure=0;Physics.SyncTransforms();game.SyncView();
        }
        void Look(Vector3 point)
        {
            var d=point-game.view.transform.position;
            game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();
        }
        void Grab(GalleryProp prop)
        {
            foreach(var part in prop.parts)
            {
                Look(part.bounds.center);game.UpdateAim();
                if(game.aimed==prop){game.Interact();break;}
            }
            Require(game.held==prop,"Cannot pick up "+prop.title);
            Require(prop.body.isKinematic,"Held body not kinematic");
        }
        void Aim(float yaw,float pitch)
        {
            game.yaw=yaw;game.pitch=pitch;game.SyncView();
            for(int i=0;i<4;i++){game.UpdateHeldPose();CheckPose(game.held);}
        }
        void CheckPose(GalleryProp prop)
        {
            Require(!game.invalid,"Invalid committed pose: "+game.invalidReason);
            foreach(var box in prop.parts)
            {
                Require(box!=null&&box.enabled,"Missing or disabled collider");
                foreach(var other in Physics.OverlapBox(box.bounds.center,box.bounds.size*.5f+Vector3.one*.005f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                {
                    if(other==game.controller||other.GetComponentInParent<GalleryProp>()==prop)continue;
                    Vector3 direction;float depth;
                    if(Physics.ComputePenetration(box,box.transform.position,box.transform.rotation,other,other.transform.position,other.transform.rotation,out direction,out depth))
                        Require(depth<=.0041f,"Penetration "+depth+" against "+other.name);
                }
            }
            Require(Mathf.Abs(prop.transform.localScale.x-game.RequestedScale)<.001f,"Perspective ratio mismatch");
            poses++;
        }
        void CheckShadowRays(GalleryProp prop)
        {
            var bounds=prop.WorldBounds;
            for(int x=0;x<21;x++)for(int z=0;z<25;z++)
            {
                Vector3 origin=new Vector3(Mathf.Lerp(bounds.min.x-.3f,bounds.max.x+.3f,x/20f),.03f,
                    Mathf.Lerp(bounds.min.z-.3f,bounds.max.z+bounds.size.y*.75f+.3f,z/24f));
                bool physical=false,analytic=false;
                foreach(var box in prop.parts)
                {
                    RaycastHit hit;
                    physical|=box.Raycast(new Ray(origin,GalleryLighting.ToSun),out hit,100);
                    analytic|=GalleryLighting.Intersects(box.GetComponent<GalleryCaster>().Inverse,origin,GalleryLighting.ToSun);
                }
                // PhysX rays originating inside a solid differ from the analytic exit ray.
                bool inside=false;
                foreach(var box in prop.parts)
                {
                    var point=box.transform.InverseTransformPoint(origin)-box.center;
                    inside|=Mathf.Abs(point.x)<=box.size.x*.5f&&Mathf.Abs(point.y)<=box.size.y*.5f&&Mathf.Abs(point.z)<=box.size.z*.5f;
                }
                if(inside)continue;
                Require(physical==analytic,"Collision hull ray mismatch: "+prop.title);rays++;
            }
        }
        IEnumerable<object> Run()
        {
            game.ResetAll();game.Resume();
            Require(game.props.Length==11,"Expected nine original and two new objects");
            Require(game.lighting.Count<=GalleryLighting.MaxCasters,"Shadow budget exceeded");
            int originalCount=game.props.Length;GalleryPuzzleExhibits.AddTo(game);
            Require(game.props.Length==originalCount,"Exhibit installation is not idempotent");
            var names=new[]{GalleryPuzzleExhibits.VesselTitle,GalleryPuzzleExhibits.WindowTitle};
            for(int exhibit=0;exhibit<names.Length;exhibit++)
            {
                game.ResetAll();game.Resume();
                var prop=Array.Find(game.props,p=>p.title==names[exhibit]);
                Require(prop!=null,"Missing exhibit");
                foreach(var renderer in prop.renderers)Require(renderer.sharedMaterial!=null,"Missing material");
                if(exhibit==0)
                {
                    Renderer body=null;foreach(var r in prop.renderers)if(r.name=="Curved porcelain body")body=r;
                    Require(body.sharedMaterial.mainTexture!=null&&body.sharedMaterial.mainTexture.width>=1024,"Missing generated porcelain texture");
                    var surface=GalleryCeramicMesh.Vase();
                    Require(surface.vertices.Length>10000&&surface.triangles.Length>50000,"Curved ceramic mesh detail missing");
                    Require(body.sharedMaterial.shader.name=="Standard","Glazed ceramic shader is missing");
                    GalleryCeramicMesh.Export(Path.Combine(output,"Model"));
                }
                var result=new Result{title=prop.title,solidParts=prop.parts.Length,minimumScale=100,maximumScale=0};results.Add(result);
                PlayerNear(prop);Look(prop.WorldBounds.center);game.lighting.Synchronize();
                Capture(exhibit==0?"01-porcelain-in-gallery":"02-window-in-gallery");
                if(exhibit==0)
                {
                    // Additional camera-only art review; restore the gameplay view
                    // before any pickup/physics assertions.
                    var artPosition=prop.homePosition+new Vector3(1.9f,1.6f,-2.5f);
                    var artDirection=prop.homePosition+Vector3.up*.05f-artPosition;
                    var artRotation=Quaternion.Euler(-Mathf.Atan2(artDirection.y,new Vector2(artDirection.x,artDirection.z).magnitude)*Mathf.Rad2Deg,
                        Mathf.Atan2(artDirection.x,artDirection.z)*Mathf.Rad2Deg,0);
#if PARALLAX_EXISTING_PLAYER
                    // UnityLinker removed the getter; GalleryWorld sets 72 degrees.
                    const float previousFov=72;
#else
                    var previousFov=game.view.fieldOfView;
#endif
                    game.view.transform.SetPositionAndRotation(artPosition,artRotation);
                    game.view.fieldOfView=48;
                    Capture("05-porcelain-three-quarter");
                    game.view.fieldOfView=previousFov;
                    game.SyncView();Look(prop.WorldBounds.center);
                }
                CheckShadowRays(prop);Grab(prop);
                for(int y=-5;y<=5;y++)
                {
                    for(int p=0;p<13;p++)
                    {
                        Aim(y*.17f,-.9f+((y&1)==0?p:12-p)*.125f);
                        result.minimumScale=Mathf.Min(result.minimumScale,prop.transform.localScale.x);
                        result.maximumScale=Mathf.Max(result.maximumScale,prop.transform.localScale.x);
                    }
                    yield return null;
                }
                Require(result.maximumScale-result.minimumScale>.2f,"Camera aiming did not resize exhibit");
                game.Cancel();result.cancelPassed=Vector3.Distance(prop.transform.position,prop.homePosition)<.0001f&&prop.transform.localScale==prop.homeScale;
                Require(result.cancelPassed,"Cancel failed to restore original transform");
                Grab(prop);bool airborne=false;
                for(int yaw=-4;yaw<=4&&!airborne;yaw++)for(int p=0;p<7&&!airborne;p++)
                {
                    Aim(yaw*.16f,.25f+p*.10f);
                    // Touching a ceiling may stop further motion but the solver's
                    // accepted airborne pose remains valid and can be released.
                    airborne=!game.invalid&&prop.WorldBounds.min.y>.65f&&prop.WorldBounds.max.y<9.98f;
                }
                Require(airborne,"No airborne camera placement found");
                game.RotateHeld(20,12);game.UpdateHeldPose();CheckPose(prop);
                Capture(exhibit==0?"03-porcelain-held":"04-window-held");
                var position=prop.transform.position;var rotation=prop.transform.rotation;var scale=prop.transform.localScale;
                Require(game.Drop(),"Drop refused");
                result.releasePassed=Vector3.Distance(position,prop.transform.position)<.0001f&&Quaternion.Angle(rotation,prop.transform.rotation)<.001f&&scale==prop.transform.localScale&&!prop.body.isKinematic;
                Require(result.releasePassed,"Drop altered pose or failed to enable physics");
                for(int i=0;i<24;i++)Physics.Simulate(1f/120);
                Physics.SyncTransforms();game.lighting.Synchronize();result.fallMetres=position.y-prop.transform.position.y;
                Require(result.fallMetres>.05f,"Gravity did not lower the released exhibit");
                CheckShadowRays(prop);
                game.checkpoint=prop.zone;game.ResetZone();
                result.resetPassed=Vector3.Distance(prop.transform.position,prop.homePosition)<.0001f&&prop.transform.localScale==prop.homeScale&&prop.body.isKinematic;
                Require(result.resetPassed,"Zone reset did not restore exhibit");
                Check(prop.title+": camera pickup/scaling/rotation, cancel, physical release, collision hull rays and zone reset passed");
                yield return null;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-gallery-ceramic-shadow-review")>=0)
                GalleryCeramicShadowReview.Run(game,output);
            game.ResetAll();Check("All original props retained; new exhibits installed exactly once; shadow budget "+game.lighting.Count+"/128");
        }
        void Capture(string name)
        {
            var camera=game.view;var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;game.lighting.Synchronize();camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            camera.targetTexture=previous;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(texture);
        }
    }
}
