using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ShadeLink.Beach
{
    // Fixtures put real scene props in water. Recovery then uses the same camera,
    // perspective resolver, colliders and release physics as normal play.
    public sealed class BeachWaterValidation : MonoBehaviour
    {
        public BeachGame game;
        const float Dt=1f/60;
        readonly List<string> checks=new List<string>(),failures=new List<string>(),errors=new List<string>();
        string output;
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope;
            public string[] checks,failures,errors;
        }
        void Require(bool value,string text)
        {
            if(!value)throw new Exception(text+"; held="+(game.held==null?"none":game.held.title)+
                "; invalid="+game.invalidReason+"; obstacle="+game.PlacementObstacle);
            checks.Add(text);Debug.Log("BEACH_WATER_PASS "+text);
        }
        void Log(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack);}
        void PhysicsSteps(int count)
        {
            for(int i=0;i<count;i++)
            {
                foreach(var water in FindObjectsByType<BeachWater>(FindObjectsSortMode.None))water.Simulate(Dt);
                Physics.Simulate(Dt);
                foreach(var prop in game.props)prop.placement.Tick(Dt);
            }
            game.lighting.Synchronize();
        }
        void Fixture(BeachProp p,Vector3 position,float scale,float yaw=0)
        {
            p.Restore();p.transform.SetPositionAndRotation(position,Quaternion.identity);p.transform.localScale=Vector3.one*scale;
            p.transform.rotation=Quaternion.Euler(0,yaw,0);
            p.body.position=position;p.body.rotation=Quaternion.identity;Physics.SyncTransforms();p.Release();
        }
        void Look(Vector3 target,bool gradual=false)
        {
            Vector3 d=target-game.view.transform.position;
            float yaw=Mathf.Atan2(d.x,d.z),pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);
            game.yaw=gradual?Mathf.MoveTowardsAngle(game.yaw*Mathf.Rad2Deg,yaw*Mathf.Rad2Deg,1.5f)*Mathf.Deg2Rad:yaw;
            game.pitch=gradual?Mathf.MoveTowards(game.pitch,pitch,.012f):pitch;
            game.SyncView();
        }
        void Grab(BeachProp p)
        {
            foreach(var shape in p.GetComponentsInChildren<Collider>())
            {
                if(!shape.enabled)continue;
                Look(shape.bounds.center);game.UpdateAim();
                if(game.aimed==p){game.Interact();break;}
            }
            Require(game.held==p,"Camera can select and grab "+p.title+" through the water surface");
            Require(!p.placement.Ready&&!p.floating&&p.driftDistance==0,"Recovery grab disables shade, floating state and hand-carried transport credit");
        }
        void PullOntoBank(BeachProp p,Vector3 bank)
        {
            Vector3 start=p.transform.position;
            Vector3 above=p.WorldBounds.center+Vector3.up*4;
            for(int i=0;i<240;i++){Look(above,true);game.UpdateHeldPose();}
            Require(p.WorldBounds.min.y>.10f,"Aiming upward lifts the submerged object clear of water; bottom="+p.WorldBounds.min.y);
            Require(Vector3.Distance(start,p.transform.position)>.35f,"Submerged pickup no longer locks its position");
            for(int i=0;i<360;i++){Look(bank,true);game.UpdateHeldPose();}
            Require(game.ValidPlacement(p,out var reason),"Recovered object reaches a collision-free bank placement: "+reason);
            Require(game.Drop(),"Recovered object can be released without resetting the zone");
            PhysicsSteps(180);
            Require(p.WorldBounds.min.y>-.025f&&p.placement.Ready,"Recovered object settles on dry land and reactivates its placed shadow");
        }
        void SunkenParasol()
        {
            game.ResetAll();game.Resume();game.checkpoint=1;
            var p=game.props.First(x=>x.title=="02 · 햇빛 가리개");
            Fixture(p,new Vector3(0,.2f,36.5f),1.15f);PhysicsSteps(240);
            Require(p.WorldBounds.min.y> -2.1f&&p.WorldBounds.min.y<-.8f,"A non-floating parasol lands on a finite underwater floor instead of falling out of reach");
            game.PlacePlayer(new Vector3(0,0,31.3f));Grab(p);PullOntoBank(p,new Vector3(-4.5f,0,31));
        }
        void SubmergedPlank()
        {
            game.ResetAll();game.Resume();game.checkpoint=1;
            var p=game.props.First(x=>x.title=="02 · 짧은 판자");
            Fixture(p,new Vector3(0,-.09f,36.5f),1.3f,35);PhysicsSteps(180);
            Require(p.floating,"Released plank retains real water buoyancy");
            Vector3 settled=p.body.position;
            PhysicsSteps(600);
            float drift=Vector2.Distance(new Vector2(settled.x,settled.z),new Vector2(p.body.position.x,p.body.position.z));
            Require(BeachWater.Supports(p)&&p.placement.Ready,"The first crossing keeps a settled plank buoyant and usable after ten seconds");
            Require(drift<.05f&&new Vector2(p.body.linearVelocity.x,p.body.linearVelocity.z).magnitude<.025f,
                "The first crossing has no directional drift after settling; displacement="+drift);
            game.PlacePlayer(new Vector3(0,0,31));Grab(p);PullOntoBank(p,new Vector3(0,0,29));
        }
        void SolidBoundary()
        {
            game.ResetAll();game.Resume();game.checkpoint=1;
            var p=game.props.First(x=>x.title=="02 · 짧은 판자");
            Fixture(p,new Vector3(0,-.09f,36.5f),1.3f);PhysicsSteps(120);
            game.PlacePlayer(new Vector3(0,0,31));Grab(p);
            for(int i=0;i<240;i++){Look(new Vector3(0,-3,30),true);game.UpdateHeldPose();}
            Require(p.WorldBounds.min.y>=-2.05f,"Aiming underwater still respects the recoverable world depth");
            for(int i=0;i<300;i++){Look(new Vector3(16,1,36),true);game.UpdateHeldPose();}
            Require(p.WorldBounds.max.x<13.81f&&game.ValidPlacement(p,out _),"Water recovery does not permit crossing the beach boundary or solid ground");
            game.Cancel();Require(game.held==null,"Cancel works after underwater recovery and blocked movement");
        }
        void WaterRemainsHazardous()
        {
            game.ResetAll();game.Resume();game.checkpoint=1;
            game.PlacePlayer(new Vector3(0,-1.7f,36.5f));int respawns=game.respawns;
            game.Tick(Dt,Vector2.zero);
            Require(game.respawns==respawns+1&&Vector3.Distance(game.player.feet,BeachWorld.Checkpoints[1])<.15f,
                "The underwater floor does not allow the player to bypass water failure and checkpoint recovery");
        }
        void Scenario(string name,Action run)
        {
            try{run();}
            catch(Exception ex){failures.Add(name+": "+ex);Debug.Log("BEACH_WATER_FAIL "+name+": "+ex.Message);}
            game.Cancel(true);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-shade-output");
            output=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"BeachWaterValidation");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;yield return null;
            Scenario("sunken parasol",SunkenParasol);yield return null;
            Scenario("submerged plank",SubmergedPlank);yield return null;
            Scenario("solid boundary",SolidBoundary);yield return null;
            Scenario("water danger",WaterRemainsHazardous);yield return null;
            var report=new Report{passed=failures.Count==0&&errors.Count==0,
                scope="Essential plank and parasol water fixtures followed by camera-ray pickup, gradual aiming, perspective collision sweeps, drop, real buoyancy/gravity and settling. Decorative beach balls are no longer selectable and are outside this water-recovery scope. Boat travel and arrival use their separate native validator. Player positions are prepared on shore and exposure is excluded while isolating object recovery. Not a full-stage solve or a manual playtest.",
                checks=checks.ToArray(),failures=failures.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"water-results.json"),JsonUtility.ToJson(report,true));
            Physics.simulationMode=previous;Application.logMessageReceived-=Log;Application.Quit(report.passed?0:1);
        }
    }
}
