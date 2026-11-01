using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShadeLink.Beach;
using ShadeLink.Gallery;
using ShadeLink.Library;

namespace ShadeLink
{
    public sealed class PlacementValidation:MonoBehaviour
    {
        const float Dt=1f/60;
        BeachGame beach;GalleryGame gallery;LibraryGame library;
        string stage,output;Camera view;CharacterController controller;PlacementSupport[] props;
        Action resume,reset,interact,aim,cancel,sync;Action<float,Vector2,bool> tick;Action<Vector3> place;
        Func<PlacementSupport> held,aimed;Func<Vector3,bool> sunlit;Func<Vector3> feet;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        [Serializable] class Report{public bool passed;public string stage,scope,failure;public string[] checks,errors;public float carriedDistance,carriedJump;}
        float distance,jumpHeight;
        void Require(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);Debug.Log("PLACEMENT_PASS "+text);}
        void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text+"\n"+stack);}
        void Bind()
        {
            beach=GetComponent<BeachGame>();gallery=GetComponent<GalleryGame>();library=GetComponent<LibraryGame>();
            if(beach!=null){stage="Beach";view=beach.view;controller=beach.controller;props=beach.props.Select(p=>p.placement).ToArray();resume=beach.Resume;reset=beach.ResetAll;interact=beach.Interact;aim=beach.UpdateAim;cancel=()=>beach.Cancel();sync=beach.lighting.Synchronize;tick=beach.Tick;place=beach.PlacePlayer;held=()=>beach.held==null?null:beach.held.placement;aimed=()=>beach.aimed==null?null:beach.aimed.placement;feet=()=>beach.player.feet;sunlit=p=>beach.lighting.IsSunlit(p,Vector3.up);}
            else if(gallery!=null){stage="Gallery";view=gallery.view;controller=gallery.controller;props=gallery.props.Select(p=>p.placement).ToArray();resume=gallery.Resume;reset=gallery.ResetAll;interact=gallery.Interact;aim=gallery.UpdateAim;cancel=()=>gallery.Cancel();sync=gallery.lighting.Synchronize;tick=gallery.Tick;place=gallery.PlacePlayer;held=()=>gallery.held==null?null:gallery.held.placement;aimed=()=>gallery.aimed==null?null:gallery.aimed.placement;feet=()=>gallery.player.feet;sunlit=p=>gallery.lighting.IsSunlit(p,Vector3.up);}
            else{stage="Library";view=library.view;controller=library.controller;props=library.props.Select(p=>p.placement).ToArray();resume=library.Resume;reset=library.ResetAll;interact=library.Interact;aim=library.UpdateAim;cancel=()=>library.Cancel();sync=library.lighting.Synchronize;tick=library.Tick;place=library.PlacePlayer;held=()=>library.held==null?null:library.held.placement;aimed=()=>library.aimed==null?null:library.aimed.placement;feet=()=>library.player.feet;sunlit=p=>library.lighting.IsSunlit(p,Vector3.up);}
        }
        void Look(Vector3 target)
        {
            var d=target-view.transform.position;float yaw=Mathf.Atan2(d.x,d.z),pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
            if(beach!=null){beach.yaw=yaw;beach.pitch=pitch;beach.SyncView();}else if(gallery!=null){gallery.yaw=yaw;gallery.pitch=pitch;gallery.SyncView();}else{library.yaw=yaw;library.pitch=pitch;library.SyncView();}
        }
        void Step(Vector2 move,bool jump=false){tick(Dt,move,jump);if(beach!=null)foreach(var w in FindObjectsByType<BeachWater>(FindObjectsSortMode.None))w.Simulate(Dt);Physics.Simulate(Dt);sync();}
        IEnumerator Start()
        {
            Bind();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");output=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"PlacementValidation",stage);Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;yield return null;
            string failure=null;var steps=Sequence();
            while(true){bool more=false;object current=null;try{more=steps.MoveNext();if(more)current=steps.Current;}catch(Exception ex){failure=ex.ToString();}if(!more||failure!=null)break;yield return current;}
            var report=new Report{passed=failure==null&&errors.Count==0,stage=stage,scope="Actual camera grab, held movement/jump/cancel and scene shadow classification. Separate labelled physics fixtures verify placement activation, release/regrab, unsupported placement, support removal and player collision. Beach additionally verifies mirror charge and preview rendering. Not an unassisted full-stage playthrough or IMGUI screenshot test.",failure=failure,checks=checks.ToArray(),errors=errors.ToArray(),carriedDistance=distance,carriedJump=jumpHeight};
            File.WriteAllText(Path.Combine(output,"placement-results.json"),JsonUtility.ToJson(report,true));Physics.simulationMode=previous;Application.logMessageReceived-=Log;Application.Quit(report.passed?0:1);
        }
        IEnumerator Sequence()
        {
            resume();for(int i=0;i<15;i++)Step(Vector2.zero);
            Require(props.All(p=>p!=null),"Every movable prop has placement state");
            Vector3 pickup=Vector3.zero,scale=Vector3.one;bool ready=false;
            foreach(var prop in props.OrderBy(p=>Vector3.Distance(p.transform.position,feet())))
            {
                foreach(var shape in prop.GetComponentsInChildren<Collider>())
                {if(!shape.enabled||shape.gameObject.layer==30)continue;Look(shape.bounds.center);aim();if(aimed()!=prop)continue;pickup=prop.transform.position;scale=prop.transform.localScale;ready=prop.Ready;interact();if(held()!=null)break;}
                if(held()!=null)break;
            }
            Require(held()!=null,"Camera ray can grab an original scene object");var grabbed=held();
            Require(!grabbed.Ready&&grabbed.Held,"Grabbing disables protection immediately");
            Require(grabbed.Visuals.All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"Carried renderers do not cast physical safe shadows");
            var start=feet();
            // Check carried movement after the keyboard input ramp (up to 1/3 s) has completed.
            for(int i=0;i<30;i++)Step(Vector2.left);distance=Vector3.Distance(start,feet());
            Require(distance>.45f&&held()==grabbed,"Player can walk while carrying an original prop");
            float floor=feet().y,peak=floor;for(int i=0;i<55;i++){Step(Vector2.zero,i==0);peak=Mathf.Max(peak,feet().y);}jumpHeight=peak-floor;
            Require(jumpHeight>.75f&&held()==grabbed,"Player can jump while carrying an original prop");
            cancel();Require(held()==null&&Vector3.Distance(grabbed.transform.position,pickup)<.01f&&Vector3.Distance(grabbed.transform.localScale,scale)<.01f&&grabbed.Ready==ready,"Cancel restores original pose, scale and placement state");
            reset();resume();yield return null;
            VerifySceneShadows();yield return null;
            VerifyPhysics();yield return null;
            if(beach!=null){VerifyBeach();yield return null;}
            reset();resume();Require(props.Any(p=>p.Ready),"Full reset restores usable original placements");
        }
        void VerifySceneShadows()
        {
            var sun=beach!=null?BeachLighting.ToSun:gallery!=null?GalleryLighting.ToSun:LibraryLighting.ToSun;
            if(library!=null)
            {
                // Library's initial props stand in shade. Move a real shelf into
                // a known sun band to isolate its contribution to floor safety.
                var shelf=library.props.First(p=>p.objectId=="shelf-a");shelf.transform.localScale=Vector3.one*2;
                shelf.transform.position=new Vector3(-7,3.02f,7.5f);shelf.transform.rotation=Quaternion.identity;shelf.body.position=shelf.transform.position;shelf.body.rotation=shelf.transform.rotation;
                Physics.SyncTransforms();shelf.placement.ResetPlaced();sync();
                Require(shelf.placement.Ready,"Labelled library sun-band fixture uses a floor-supported original shelf");
            }
            bool changed=false;
            foreach(var prop in props)
            {
                if(!prop.Ready)continue;var saved=prop.Capture();var candidates=new List<Vector3>();
                foreach(var shape in prop.GetComponentsInChildren<Collider>())
                {
                    if(!shape.enabled||shape.gameObject.layer==30)continue;var b=shape.bounds;
                    for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                    {var top=b.center+new Vector3(b.extents.x*x*.65f,0,b.extents.z*z*.65f);var point=top-sun*(top.y/sun.y)+Vector3.up*.02f;if(!sunlit(point))candidates.Add(point);}
                }
                prop.BeginHold();sync();changed=candidates.Any(sunlit);prop.Restore(saved);sync();if(changed)break;
            }
            Require(changed,"Holding a previously installed prop removes its actual scene safety shadow");
            reset();resume();
        }
        void VerifyPhysics()
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Placement validation floor";ground.transform.position=new Vector3(100,-.5f,0);ground.transform.localScale=new Vector3(20,1,20);
            var root=new GameObject("Placement validation object");root.transform.position=new Vector3(100,.01f,0);var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);cube.transform.localPosition=Vector3.up*.5f;
            var body=root.AddComponent<Rigidbody>();body.isKinematic=true;var support=root.AddComponent<PlacementSupport>();support.Initialize(body,new[]{cube.GetComponent<Renderer>()});support.SetPlayer(controller);Physics.SyncTransforms();support.ResetPlaced();
            Require(support.Ready,"A physically supported object can be installed");
            support.BeginHold();Require(Physics.GetIgnoreCollision(controller,cube.GetComponent<Collider>())&&!PlacementSupport.IsUsableGround(cube.GetComponent<Collider>()),"Held object is excluded from player collision and ground probes");
            place(new Vector3(100,1.04f,0));for(int i=0;i<30;i++)controller.Move(Vector3.down*.06f);
            Require(controller.transform.position.y<.1f,"Player physically falls through a held platform onto the real floor");
            place(new Vector3(105,.02f,0));
            for(int cycle=0;cycle<15;cycle++){support.Released();for(int i=0;i<8;i++)support.Tick(Dt);Require(!support.Ready,"Fast release/regrab cycle "+(cycle+1)+" gives no protection");support.BeginHold();}
            support.Released();for(int i=0;i<20;i++)support.Tick(Dt);Require(!support.Ready,"Protection stays off before continuous settling completes");support.Tick(.03f);Require(support.Ready&&!Physics.GetIgnoreCollision(controller,cube.GetComponent<Collider>()),"Stable placement activates both shade eligibility and player collision");
            support.BeginHold();root.transform.localScale=Vector3.one*8;Physics.SyncTransforms();support.Tick(.5f);Require(!support.Ready,"An oversized carried object remains inactive");
            root.transform.localScale=Vector3.one;root.transform.position=new Vector3(100,4,0);body.position=root.transform.position;body.isKinematic=false;body.useGravity=true;support.Released();Physics.SyncTransforms();
            for(int i=0;i<20;i++){Physics.Simulate(Dt);support.Tick(Dt);}Require(!support.Ready,"An airborne released object provides no protection");
            for(int i=0;i<150;i++){Physics.Simulate(Dt);support.Tick(Dt);}Require(support.Ready&&root.transform.position.y<.1f,"Real gravity landing eventually activates a supported object");
            var upper=GameObject.CreatePrimitive(PrimitiveType.Cube);upper.transform.position=new Vector3(100,1.5f,0);var upperBody=upper.AddComponent<Rigidbody>();upperBody.isKinematic=true;var upperSupport=upper.AddComponent<PlacementSupport>();upperSupport.Initialize(upperBody,new[]{upper.GetComponent<Renderer>()});Physics.SyncTransforms();upperSupport.ResetPlaced();Require(upperSupport.Ready,"A placed prop may support another prop");
            support.BeginHold();upperSupport.Tick(Dt);Require(!upperSupport.Ready,"Removing an active support disables the prop above it");
            Destroy(upper);Destroy(root);Destroy(ground);reset();resume();
        }
        void VerifyBeach()
        {
            var mirror=beach.mirror.prop;mirror.Restore();mirror.transform.position=new Vector3(-4,.03f,10);mirror.transform.localScale=Vector3.one*2.5f;
            var target=(beach.lightGate.sensors[0].position+beach.lightGate.sensors[1].position)*.5f;beach.mirror.transform.rotation=Quaternion.LookRotation((target-beach.mirror.transform.position).normalized+BeachLighting.ToSun);Physics.SyncTransforms();
            mirror.placement.BeginHold();for(int i=0;i<90;i++){mirror.placement.Tick(Dt);sync();beach.lightGate.Tick(Dt,beach.lighting,controller);}
            Require(!beach.mirror.Illuminated&&!beach.lightGate.unlocked,"A carried enlarged mirror cannot illuminate or unlock sensors");
            mirror.placement.Released();for(int i=0;i<100;i++){mirror.placement.Tick(Dt);sync();beach.lightGate.Tick(Dt,beach.lighting,controller);}
            Require(mirror.placement.Ready&&beach.lightGate.unlocked,"The same supported mirror placement charges sensors normally");
            var parasol=beach.props.First(p=>p.title.Contains("01")&&p.title.Contains("파라솔"));parasol.Restore();parasol.transform.position=new Vector3(6,.02f,15);parasol.transform.localScale=Vector3.one*2.6f;Physics.SyncTransforms();parasol.placement.ResetPlaced();sync();
            var preview=beach.GetComponent<BeachPlacementPreview>();view.transform.position=new Vector3(13,9,7);view.transform.LookAt(new Vector3(5,0,16));preview.Refresh();Capture("installed");parasol.BeginHold();sync();preview.Refresh();Capture("carried-preview");
            Require(!parasol.placement.Ready&&Shader.GetGlobalFloat("_PlacementPreviewActive")>.5f,"Inactive mesh preview is rendered separately from safety shadows");
            // Use the essential first-crossing plank; beach balls are static decoration.
            var plank=beach.props.First(p=>p.title=="02 · 짧은 판자");plank.Restore();
            plank.floating=true;plank.driftDistance=9;plank.BeginHold();
            Require(plank.driftDistance==0&&!plank.floating,"Picking up the essential plank clears its floating state and carried transport credit");
            plank.transform.SetPositionAndRotation(new Vector3(0,-.15f,36.5f),Quaternion.identity);
            plank.transform.localScale=Vector3.one;plank.transform.position+=Vector3.forward*.3f;
            Physics.SyncTransforms();Vector3 released=plank.transform.position;plank.Release();
            Require(Vector3.Distance(plank.lastDriftPosition,released)<.001f&&plank.driftDistance==0,
                "Releasing the essential plank starts its water tracking at the release pose");
            foreach(var water in FindObjectsByType<BeachWater>(FindObjectsSortMode.None))water.Simulate(Dt);
            Require(plank.floating&&BeachWater.Supports(plank),"The released first-crossing plank remains buoyant in the calm channel");
        }
        void Capture(string name)
        {
            var rt=new RenderTexture(1280,720,24);var old=view.targetTexture;view.targetTexture=rt;view.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var img=new Texture2D(1280,720,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,720),0,0);img.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),img.EncodeToPNG());view.targetTexture=old;RenderTexture.active=prior;Destroy(img);rt.Release();Destroy(rt);
        }
    }
}
