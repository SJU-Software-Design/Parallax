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
        [Serializable] sealed class Report{public bool passed;public string scope,failure;public string[] checks,errors;public int props,shadowMeshes;public float carriedDistance,carriedJump,mirrorGrowth;}
        float carriedDistance,carriedJump,mirrorGrowth;
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
        bool CastleWalkingSurface(Vector3 surface)
        {
            return Physics.Raycast(surface+Vector3.up*.08f,Vector3.down,out var hit,.30f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                &&PlacementSupport.IsUsableGround(hit.collider)&&Mathf.Abs(hit.point.y-surface.y)<.16f;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");output=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"BeachValidation");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;yield return null;
            var sequence=Sequence();string failure=null;
            while(true){bool more=false;object item=null;try{more=sequence.MoveNext();if(more)item=sequence.Current;}catch(Exception ex){failure=ex.ToString();}if(!more||failure!=null)break;yield return item;}
            if(failure!=null)Capture("failure");
            var result=new Report{passed=failure==null&&errors.Count==0,scope="Real camera grab, held movement, jumping, perspective and cancellation. Optical and shaded plank crossings use explicitly prepared fixtures. Optional mats, chairs and balls are fixed decoration and are not water-test props. Castle coverage is limited to two separate fragment sources and the upper parasol, empty front cuts, physical summit support, prepared checkpoint recovery without replacing source objects, and scoped resets. Actual castle fragment placement, manual upper shade and victory are covered separately by BeachCastleValidation; shadow assembly, boat travel and water recovery have separate native validators. Images are camera renders without the IMGUI overlay. This is not a claimed unassisted full-stage solve or UI visual validation.",failure=failure,checks=checks.ToArray(),errors=errors.ToArray(),props=game.props.Length,shadowMeshes=game.lighting.Count,carriedDistance=carriedDistance,carriedJump=carriedJump,mirrorGrowth=mirrorGrowth};
            File.WriteAllText(Path.Combine(output,"beach-results.json"),JsonUtility.ToJson(result,true));Physics.simulationMode=previous;Application.logMessageReceived-=Log;Application.Quit(result.passed?0:1);
        }
        IEnumerator Sequence()
        {
            game.Resume();for(int i=0;i<12;i++)Step(Vector2.zero);yield return null;
            Require(game.props.Length==11,"11 essential interactable objects instantiated");Require(game.lighting.Count>40,"Imported visual meshes have exact non-solid shadow queries");Require(game.safe,"Initial checkpoint is shaded");
            Require(!game.props.Any(p=>p.title=="01 · 차광 돗자리"||p.title=="02 · 접이식 의자"||p.title=="02 · 비치볼"),"Optional mat, folding chair and beach ball are scenery rather than interactable puzzle props");
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
            game.ResetAll();game.Resume();game.checkpoint=3;game.PlacePlayer(BeachWorld.Checkpoints[3]);Look(new Vector3(0,5,96));Capture("beach-castle");
            var castle=game.castleRoute;
            Require(castle!=null&&castle.LowerFragment!=null&&castle.UpperFragment!=null&&castle.Parasol!=null&&castle.CastleRoot!=null,
                "Saved compact castle retains its source art, two separate wall fragments and movable upper parasol");
            var castleProps=new[]{castle.LowerFragment,castle.UpperFragment,castle.Parasol};
            int[] castleIds=castleProps.Select(p=>p.GetInstanceID()).ToArray();
            Require(game.props.Count(p=>p.zone==3)==3&&castleProps.Distinct().Count()==3&&castleProps.All(p=>game.props.Count(q=>q==p)==1),
                "The final zone contains exactly two independently registered fragments and one upper parasol");
            Require(new[]{castle.LowerFragment,castle.UpperFragment}.All(p=>p.platform&&p.upright&&!p.buoyant),
                "Both front-wall fragments are upright, non-floating physical stair pieces");
            Require(BeachCastleRestoration.UpperPose.y>BeachCastleRestoration.LowerPose.y+2
                &&Vector3.Distance(BeachCastleRestoration.Summit,BeachWorld.Exit)<.01f
                &&Vector3.Distance(BeachCastleRestoration.Midpoint,BeachWorld.CastleRest)<.01f,
                "The two front cuts rise through the middle landing to the original summit goal");
            Require(!CastleWalkingSurface(BeachCastleRestoration.LowerPose)&&!CastleWalkingSurface(BeachCastleRestoration.UpperPose),
                "Neither unrestored front cut supplies a walking surface at its missing stair midpoint");
            Require(Physics.Raycast(BeachWorld.Exit+Vector3.up,Vector3.down,out var top,1.2f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&Mathf.Abs(top.point.y-BeachWorld.Exit.y)<.08f,"Exit has physical support on the high castle crown");
            game.PlacePlayer(BeachWorld.CastleRest);for(int i=0;i<15;i++)Step(Vector2.zero);
            Require(game.safe&&!game.castleRest&&!castle.AtMidpoint&&!castle.FirstRestored,
                "Standing at the front middle landing alone does not claim a restored first stair");
            // This checkpoint-state fixture isolates recovery. It does not claim
            // the first crossing was solved; BeachCastleValidation tests that flow.
            castle.FirstRestored=castle.AtMidpoint=true;game.castleRest=true;
            game.Respawn();
            Require(Vector3.Distance(game.player.feet,BeachWorld.CastleRest)<.15f
                &&castleProps.Select(p=>p.GetInstanceID()).SequenceEqual(castleIds)&&castleProps.All(p=>game.props.Count(q=>q==p)==1),
                "Prepared middle-checkpoint recovery returns the player without replacing either fragment or the upper parasol");
            game.ResetZone();
            Require(!game.castleRest&&!castle.FirstRestored&&!castle.SecondRestored&&!castle.AtMidpoint
                &&castleProps.All(p=>Vector3.Distance(p.transform.position,p.homePosition)<.01f&&Vector3.Distance(p.transform.localScale,p.homeScale)<.001f),
                "Resetting the final zone clears restoration progress and restores all three puzzle sources home");
            game.ResetAll();game.Resume();game.Pause();float elapsed=game.elapsed;game.Tick(.05f,Vector2.up);Require(game.elapsed==elapsed&&Time.timeScale==0,"Pause freezes simulation and input");
            game.Resume();
            // Seed progress flags explicitly so the reset assertion is not vacuous.
            game.lightGate.unlocked=game.waterGate.unlocked=game.castleRest=true;
            castle.FirstRestored=castle.SecondRestored=castle.AtMidpoint=true;
            game.ResetAll();Require(!game.lightGate.unlocked&&!game.waterGate.unlocked&&!game.castleRest
                &&!castle.FirstRestored&&!castle.SecondRestored&&!castle.AtMidpoint&&castleProps.Select(p=>p.GetInstanceID()).SequenceEqual(castleIds),
                "Full reset clears mirror, boat-gate and castle progress without replacing the two fragments or upper parasol");
            game.view.transform.position=new Vector3(45,47,17);game.view.transform.LookAt(new Vector3(0,0,53));Capture("beach-overview");yield return null;
        }
    }
}
