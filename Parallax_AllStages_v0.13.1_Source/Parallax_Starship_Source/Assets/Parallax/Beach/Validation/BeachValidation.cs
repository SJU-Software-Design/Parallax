using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachValidation:MonoBehaviour
    {
        public BeachGame game;
        const float Dt=1f/60;
        string output;readonly List<string> checks=new List<string>(),errors=new List<string>();
        [Serializable] sealed class Report{public bool passed;public string scope,failure;public string[] checks,errors;public int props,shadowMeshes;public float carriedDistance,carriedJump,mirrorGrowth,driftTravel;}
        float carriedDistance,carriedJump,mirrorGrowth,driftTravel;
        void Require(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);Debug.Log("BEACH_PASS "+text);}
        void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Look(Vector3 target){Vector3 d=target-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();}
        void Step(Vector2 move,bool jump=false)
        {game.Tick(Dt,move,jump);foreach(var w in FindObjectsByType<BeachWater>(FindObjectsSortMode.None))w.Simulate(Dt);Physics.Simulate(Dt);game.lighting.Synchronize();}
        void Fixture(BeachProp p,Vector3 position,float scale,Quaternion rotation,bool release=false)
        {p.Restore();p.transform.SetPositionAndRotation(position,rotation);p.transform.localScale=Vector3.one*scale;p.body.position=position;p.body.rotation=rotation;Physics.SyncTransforms();if(release)p.Release();game.lighting.Synchronize();}
        void Capture(string name)
        {
            var target=new RenderTexture(1600,900,24);var old=game.view.targetTexture;game.view.targetTexture=target;game.view.Render();var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=active;game.view.targetTexture=old;Destroy(image);target.Release();Destroy(target);
        }
        void JumpTo(Vector3 destination)
        {
            for(int i=0;i<65&&!game.won;i++)
            {var delta=new Vector2(destination.x-game.player.feet.x,destination.z-game.player.feet.z);Step(delta.magnitude>.08f?Vector2.ClampMagnitude(delta/(BeachGame.Speed*Dt),1):Vector2.zero,i==0);}
            Require(Vector3.Distance(game.player.feet,destination)<.30f,"Castle jump reaches "+destination+" (actual "+game.player.feet+")");
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");output=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"BeachValidation");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;yield return null;
            var sequence=Sequence();string failure=null;
            while(true){bool more=false;object item=null;try{more=sequence.MoveNext();if(more)item=sequence.Current;}catch(Exception ex){failure=ex.ToString();}if(!more||failure!=null)break;yield return item;}
            if(failure!=null)Capture("failure");
            var result=new Report{passed=failure==null&&errors.Count==0,scope="Real camera grab, held movement, jumping, perspective and cancellation. Optical/bridge/river/castle fixtures are explicitly prepared to isolate mechanics. Float transport and castle jumps use runtime physics. Images are camera renders without the IMGUI overlay. This is not a claimed unassisted full-stage solve or UI visual validation.",failure=failure,checks=checks.ToArray(),errors=errors.ToArray(),props=game.props.Length,shadowMeshes=game.lighting.Count,carriedDistance=carriedDistance,carriedJump=carriedJump,mirrorGrowth=mirrorGrowth,driftTravel=driftTravel};
            File.WriteAllText(Path.Combine(output,"beach-results.json"),JsonUtility.ToJson(result,true));Physics.simulationMode=previous;Application.logMessageReceived-=Log;Application.Quit(result.passed?0:1);
        }
        IEnumerator Sequence()
        {
            game.Resume();for(int i=0;i<12;i++)Step(Vector2.zero);yield return null;
            Require(game.props.Length==19,"19 interactable objects instantiated");Require(game.lighting.Count>40,"Imported visual meshes have exact non-solid shadow queries");Require(game.safe,"Initial checkpoint is shaded");
            Capture("beach-start");var mirror=game.mirror.prop;Vector3 home=mirror.transform.position;float homeScale=mirror.transform.localScale.x;
            foreach(var part in mirror.parts){Look(part.bounds.center);game.UpdateAim();if(game.aimed==mirror){game.Interact();break;}}
            Require(game.held==mirror,"Small mirror can be grabbed by the camera ray");var start=game.player.feet;
            for(int i=0;i<22;i++)Step(Vector2.left);carriedDistance=Vector3.Distance(start,game.player.feet);Require(carriedDistance>.8f&&game.held==mirror,"Player walks while carrying mirror");
            float floor=game.player.feet.y,peak=floor;for(int i=0;i<52;i++){Step(Vector2.zero,i==0);peak=Mathf.Max(peak,game.player.feet.y);}carriedJump=peak-floor;Require(carriedJump>.8f&&game.held==mirror,"Player jumps while carrying mirror");
            float max=mirror.transform.localScale.x;
            foreach(var target in new[]{new Vector3(-4,0,16),new Vector3(-10,0,20),new Vector3(8,0,15),new Vector3(-12,2,22)})
            {for(int i=0;i<90;i++){Vector3 delta=target-game.view.transform.position;float y=Mathf.Atan2(delta.x,delta.z);float p=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude);game.yaw=Mathf.MoveTowardsAngle(game.yaw*Mathf.Rad2Deg,y*Mathf.Rad2Deg,2)*Mathf.Deg2Rad;game.pitch=Mathf.MoveTowards(game.pitch,p,.02f);game.SyncView();game.UpdateHeldPose();max=Mathf.Max(max,mirror.transform.localScale.x);}yield return null;}
            mirrorGrowth=max/homeScale;Require(mirrorGrowth>1.4f,"Camera distance enlarges the held mirror");game.Cancel();Require(game.held==null&&Vector3.Distance(mirror.transform.position,home)<.01f&&Mathf.Abs(mirror.transform.localScale.x-homeScale)<.001f,"Cancel restores pickup pose and size");
            game.ResetAll();game.Resume();Fixture(mirror,new Vector3(-4,.03f,10),2.5f,Quaternion.identity);
            Vector3 targetSensor=(game.lightGate.sensors[0].position+game.lightGate.sensors[1].position)*.5f;
            game.mirror.transform.rotation=Quaternion.LookRotation((targetSensor-game.mirror.transform.position).normalized+BeachLighting.ToSun);game.lighting.Synchronize();
            for(int i=0;i<75;i++)Step(Vector2.zero);Require(game.lightGate.unlocked,"Enlarged aligned mirror illuminates both sensors and opens the gate");
            game.PlacePlayer(new Vector3(-6,0,6));Look(new Vector3(0,1.5f,18));Capture("beach-mirror");yield return null;
            game.ResetAll();game.Resume();game.checkpoint=1;game.PlacePlayer(new Vector3(0,.025f,31));
            var plank=game.props.First(p=>p.title=="02 · 짧은 판자");var parasol=game.props.First(p=>p.title=="02 · 햇빛 가리개");
            Fixture(plank,new Vector3(0,.015f,36.5f),3.8f,Quaternion.identity,true);Fixture(parasol,new Vector3(1,.02f,31),4.5f,Quaternion.identity,true);
            for(int i=0;i<30;i++)Step(Vector2.zero);
            Look(new Vector3(0,1,40));Capture("beach-bridge");int before=game.respawns;
            for(int i=0;i<185;i++){Step(Vector2.up,i==0);if(i%30==0)yield return null;}
            Require(game.player.feet.z>40&&game.respawns==before,"Prepared enlarged plank and parasol allow a physical shaded crossing");
            game.ResetAll();game.Resume();game.checkpoint=2;game.PlacePlayer(BeachWorld.Checkpoints[2]);
            var ball=game.props.First(p=>p.title=="03 · 물살 운반 비치볼");Fixture(ball,new Vector3(0,.7f,55.8f),1.1f,Quaternion.identity,true);
            bool floated=false;for(int i=0;i<1200&&!game.waterGate.unlocked;i++){Step(Vector2.zero);floated|=ball.floating;if(i%30==0)yield return null;}
            driftTravel=ball.driftDistance;Require(floated&&driftTravel>6.5f&&game.waterGate.unlocked,"Released beach ball floats downstream and unlocks the receiving gate");
            game.PlacePlayer(new Vector3(8.8f,.02f,56));Look(new Vector3(0,.4f,65));Capture("beach-current");
            game.ResetAll();game.Resume();game.checkpoint=3;game.PlacePlayer(BeachWorld.Checkpoints[3]);Look(new Vector3(0,5,96));Capture("beach-castle");
            Require(Physics.Raycast(BeachWorld.Exit+Vector3.up,Vector3.down,out var top,1.2f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&Mathf.Abs(top.point.y-BeachWorld.Exit.y)<.08f,"Exit has physical support on the high castle crown");
            game.PlacePlayer(BeachWorld.CastleRest);for(int i=0;i<15;i++)Step(Vector2.zero);Require(game.castleRest&&game.safe,"Castle middle refuge is a safe saved checkpoint");game.Respawn();Require(Vector3.Distance(game.player.feet,BeachWorld.CastleRest)<.15f,"Castle fall recovery returns to the middle refuge");
            game.lightGate.unlocked=game.waterGate.unlocked=true;
            Fixture(game.props.First(p=>p.title=="04 · 성벽 파라솔"),new Vector3(11,.02f,94),4.5f,Quaternion.identity,true);
            Fixture(game.props.First(p=>p.title=="04 · 정상 차광 파라솔"),new Vector3(1.8f,1.1f,89.3f),5.5f,Quaternion.identity,true);
            Fixture(game.props.First(p=>p.title=="04 · 낮은 등반 의자"),new Vector3(9.65f,.02f,96),1.2f,Quaternion.identity,true);
            game.PlacePlayer(new Vector3(10.65f,.02f,96));for(int i=0;i<40;i++)Step(Vector2.zero);
            int castleRespawns=game.respawns;
            foreach(var point in new[]{new Vector3(9.65f,.576f,96),new Vector3(8.2f,1.32f,96),new Vector3(6.65f,2.37f,96),BeachWorld.CastleRest,new Vector3(3.45f,4.35f,96),new Vector3(2.15f,5.28f,94.9f),new Vector3(1.3f,6.1f,94.2f),BeachWorld.Exit})
            {JumpTo(point);yield return null;}
            Require(game.won&&game.respawns==castleRespawns,"Prepared object step and shade allow physical castle ascent and final clear; won="+game.won+", safe="+game.safe+", grounded="+game.player.grounded+", respawns="+(game.respawns-castleRespawns));
            Look(new Vector3(6,4,92));Capture("beach-summit");
            game.ResetAll();game.Resume();game.Pause();float elapsed=game.elapsed;game.Tick(.05f,Vector2.up);Require(game.elapsed==elapsed&&Time.timeScale==0,"Pause freezes simulation and input");
            game.Resume();game.ResetAll();Require(!game.lightGate.unlocked&&!game.waterGate.unlocked&&!game.castleRest,"Full reset clears mirror, current and castle progress");
            game.view.transform.position=new Vector3(45,47,17);game.view.transform.LookAt(new Vector3(0,0,53));Capture("beach-overview");yield return null;
        }
    }
}
