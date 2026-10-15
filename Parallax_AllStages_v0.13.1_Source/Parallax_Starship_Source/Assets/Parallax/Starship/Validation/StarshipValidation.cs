using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipValidation:MonoBehaviour
    {
        public StarshipGame game;
        const float Dt=1f/60;
        string output;
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        [Serializable] sealed class Report
        {
            public bool passed;public string version,scope,failure;public string[] checks,errors;
            public int props,shadowMeshes,maxHoldQueries,maxRecoveryQueries;
            public float growth,driftDistance;
        }
        int maxQueries,maxRecoveryQueries;float growth,drift;
        void Require(bool value,string message)
        {if(!value)throw new Exception(message);checks.Add(message);Debug.Log("STARSHIP_PASS "+message);}
        void Log(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Look(Vector3 target)
        {
            Vector3 d=target-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();
        }
        void Step(Vector2 move,bool jump=false)
        {
            game.Tick(Dt,move,jump);
            foreach(var w in FindObjectsByType<StarshipWater>(FindObjectsSortMode.None))w.Simulate(Dt);
            Physics.Simulate(Dt);RecordQueries();
        }
        void RecordQueries()
        {
            maxQueries=Mathf.Max(maxQueries,StarshipHold.QueryCount(game));
            maxRecoveryQueries=Mathf.Max(maxRecoveryQueries,StarshipHold.RecoveryQueryCount(game));
        }
        void Settle(int count=45){for(int i=0;i<count;i++)Step(Vector2.zero);}
        IEnumerator WalkTo(Vector3 target,bool jump=false)
        {
            int before=game.respawns,stationary=0;
            var trace=new List<string>{"frame,feetX,feetY,feetZ,exposure,safe,grounded,velocityY"};
            Look(target+Vector3.up*1.4f);
            for(int i=0;i<2400;i++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(target-game.player.feet,Vector3.up);
                if(delta.magnitude<.16f){Settle(12);yield break;}
                Vector3 prior=game.player.feet;
                trace.Add(i+","+prior.x+","+prior.y+","+prior.z+","+game.exposure+","+game.safe+","+game.player.grounded+","+game.verticalVelocity);
                Step(new Vector2(delta.x,delta.z).normalized,jump&&i==0);
                if(game.respawns!=before){File.WriteAllLines(Path.Combine(output,"failed-walk.csv"),trace);throw new Exception("Traversal returned to refuge on the way to "+target+" at "+game.player.feet);}
                stationary=Vector3.ProjectOnPlane(game.player.feet-prior,Vector3.up).magnitude<.001f?stationary+1:0;
                if(stationary>120)
                {
                    var hits=Physics.CapsuleCastAll(game.player.feet+Vector3.up*.25f,game.player.feet+Vector3.up*1.5f,.26f,delta.normalized,.6f,StarshipWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
                    File.WriteAllLines(Path.Combine(output,"blocked-route.txt"),new[]{"feet="+game.player.feet+" target="+target+" velocityY="+game.verticalVelocity+" active="+game.active}.Concat(hits.Select(h=>h.collider.name+" bounds="+h.collider.bounds+" at="+h.point+" normal="+h.normal)));
                    File.WriteAllLines(Path.Combine(output,"failed-walk.csv"),trace);
                    yield return Capture("blocked-route");
                    throw new Exception("Traversal blocked on the way to "+target+" at "+game.player.feet);
                }
                if(i%120==0)yield return null;
            }
            throw new Exception("Traversal blocked on the way to "+target+" at "+game.player.feet);
        }
        void Fixture(StarshipProp p,Vector3 at,float scale,Quaternion rotation,bool release=false)
        {
            p.Restore();p.transform.localScale=Vector3.one*scale;p.transform.SetPositionAndRotation(at,rotation);
            p.body.position=at;p.body.rotation=rotation;Physics.SyncTransforms();p.placement.ResetPlaced();
            if(release)p.Release();game.lighting.Synchronize();
        }
        void Grab(StarshipProp p)
        {
            foreach(var part in p.parts)
            {Look(part.bounds.center);game.UpdateAim();if(game.aimed!=p)continue;game.Interact();if(game.held==p)return;}
            throw new Exception("Could not camera-grab "+p.title+" / aimed="+(game.aimed==null?"none":game.aimed.title));
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var target=new RenderTexture(1600,900,24);var previous=game.view.targetTexture;var active=RenderTexture.active;
            game.view.targetTexture=target;game.view.Render();RenderTexture.active=target;
            var picture=new Texture2D(1600,900,TextureFormat.RGB24,false);picture.ReadPixels(new Rect(0,0,1600,900),0,0);picture.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),picture.EncodeToPNG());
            game.view.targetTexture=previous;RenderTexture.active=active;target.Release();Destroy(target);Destroy(picture);yield return null;
        }
        IEnumerator Start()
        {
            output=Argument("-shade-output")??Path.Combine(Application.persistentDataPath,"starship-validation");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            Exception failure=null;var sequences=new Stack<IEnumerator>();sequences.Push(Sequence());
            while(sequences.Count>0)
            {
                object next=null;bool more=false;
                try{more=sequences.Peek().MoveNext();if(more)next=sequences.Peek().Current;}catch(Exception e){failure=e;Debug.Log("STARSHIP_FAILURE "+e);break;}
                if(!more){sequences.Pop();continue;}
                if(next is IEnumerator nested)sequences.Push(nested);else yield return next;
            }
            var report=new Report{passed=failure==null&&errors.Count==0,version=StarshipGame.Version,scope="Runtime Unity/PhysX camera interaction and HoldFix port regressions. Explicit prepared fixtures isolate optical, rack, bridge, current, high-deck and enlarged EXIT mechanisms. Corridor traversal and ending run via normal Tick movement. Images are explicit camera renders; hidden-window IMGUI capture is unavailable. This is not an unassisted full-map solve or a human difficulty/FPS benchmark.",failure=failure?.ToString(),checks=checks.ToArray(),errors=errors.ToArray(),props=game.props.Length,shadowMeshes=game.lighting.Count,maxHoldQueries=maxQueries,maxRecoveryQueries=maxRecoveryQueries,growth=growth,driftDistance=drift};
            File.WriteAllText(Path.Combine(output,"starship-results.json"),JsonUtility.ToJson(report,true));Physics.simulationMode=previous;Application.logMessageReceived-=Log;
            Application.Quit(report.passed?0:1);
        }
        static string Argument(string key)
        {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
        IEnumerator Sequence()
        {
            yield return null;
            yield return Capture("00-menu");
            game.Resume();Settle();
            Require(game.props.Length==14,"Fourteen separate interactable equipment/sign objects exist");
            Require(game.lighting.Count>100,"Imported station meshes have exact shadow-query geometry");
            Require(game.safe,"Initial saved refuge blocks the fixed external star");
            Require(!game.showHints&&game.hintLevel==0,"Final-map hints begin hidden");
            for(int i=0;i<5;i++)
            {
                Require(StarshipWorld.RoomAt(StarshipWorld.Checkpoints[i])==i,"Room "+i+" checkpoint matches requested keypad layout");
                Require(game.FootSafe(StarshipWorld.Checkpoints[i]),"Room "+i+" checkpoint has real protective shade");
            }
            for(int i=0;i<4;i++)Require(Mathf.Abs(Mathf.DeltaAngle(StarshipWorld.Yaws[i],StarshipWorld.Yaws[i+1]))==90,"Room "+i+" to "+(i+1)+" changes local travel direction by 90 degrees");
            yield return Capture("01-cargo-start");
            var cargo=game.props.First(p=>p.title.Contains("확대용"));
            Vector3 home=cargo.transform.position,homeScale=cargo.transform.localScale;
            Grab(cargo);Require(game.held==cargo,"Original equipment can be grabbed through the camera ray");
            Require(!cargo.placement.Ready&&cargo.placement.Held,"Carried equipment immediately loses shade and platform protection");
            Vector3 walking=game.player.feet;
            for(int i=0;i<15;i++)Step(Vector2.right);
            Require(Vector3.Distance(walking,game.player.feet)>.65f&&game.held==cargo,"Player can move while holding equipment");
            float bottom=game.player.feet.y,peak=bottom;
            for(int i=0;i<52;i++){Step(Vector2.zero,i==0);peak=Mathf.Max(peak,game.player.feet.y);}
            Require(peak-bottom>.8f&&game.held==cargo,"Player can jump while holding equipment");
            Look(StarshipWorld.Point(0,new Vector3(4,.1f,7)));
            for(int i=0;i<100;i++)game.UpdateHeldPose();
            growth=cargo.transform.localScale.x/homeScale.x;
            Require(growth>1.6f,"Aiming at a farther surface enlarges equipment");
            float intent=game.holdDistance;
            var overhead=StarshipWorld.Box("Regression / peripheral canopy",game.view.transform.position+Vector3.up*.8f+game.view.transform.forward*1.7f,new Vector3(2,.12f,.6f),game.GetComponentsInChildren<Renderer>().First().sharedMaterial);
            for(int i=0;i<25;i++)game.UpdateHeldPose();
            Require(Mathf.Abs(game.holdDistance-intent)<.01f,"Peripheral overhead geometry does not replace the center-ray scaling intent");
            UnityEngine.Object.DestroyImmediate(overhead);Physics.SyncTransforms();
            var b=cargo.WorldBounds;
            var blocker=StarshipWorld.Box("Regression / side contact",b.center+Vector3.right*(b.extents.x+.13f),new Vector3(.24f,3,2),cargo.renderers[0].sharedMaterial);
            Look(StarshipWorld.Point(0,new Vector3(-5,.1f,3)));
            for(int i=0;i<180;i++){game.UpdateHeldPose();RecordQueries();}
            Vector3 anchor=cargo.transform.TransformPoint(game.GrabLocalPoint)-game.view.transform.position;
            Require(Vector3.Angle(anchor,game.view.transform.forward)<1.2f&&!game.invalid,"Contacted equipment recovers to a newly aimed free location");
            Require(maxRecoveryQueries<=256,"Contact recovery stays inside its inherited 256-query budget");
            Require(maxQueries>maxRecoveryQueries,"Held query telemetry includes dense perspective rays and endpoint refinement");
            UnityEngine.Object.DestroyImmediate(blocker);game.Cancel();
            Require(game.held==null&&Vector3.Distance(cargo.transform.position,home)<.01f&&Vector3.Distance(cargo.transform.localScale,homeScale)<.01f,"Cancel restores pickup pose/scale after obstruction recovery");
            game.ResetAll();game.Resume();Grab(cargo);
            StarshipHold.InteractInput(game);Step(Vector2.right);
            Require(game.held==null&&game.drops==1,"Walking release is queued until the post-movement solve");
            Require(!cargo.placement.Ready,"Release waits for the 0.35 second placement activation");
            Settle(80);Require(cargo.placement.Ready,"Supported released equipment settles and reactivates");
            game.ResetAll();game.Resume();
            game.checkpoint=1;game.PlacePlayer(StarshipWorld.Checkpoints[1]);
            var mirror=game.mirror.prop;
            Fixture(mirror,StarshipWorld.Point(1,new Vector3(0,.025f,0)),2.7f,Quaternion.identity);
            Vector3 sensors=(game.lightGate.sensors[0].position+game.lightGate.sensors[1].position)*.5f;
            game.mirror.transform.rotation=Quaternion.LookRotation(((sensors-game.mirror.transform.position).normalized+StarshipLighting.ToSun).normalized);
            game.lighting.Synchronize();Settle(75);
            Require(game.lightGate.unlocked,"An enlarged correctly aligned reflector simultaneously charges both receivers through optical glass");
            Look(StarshipWorld.Point(1,new Vector3(0,1,8)));yield return Capture("02-optical-room");
            game.lightGate.Restore();Fixture(mirror,StarshipWorld.Point(1,new Vector3(0,.025f,0)),2,Quaternion.identity);
            game.mirror.transform.rotation=Quaternion.LookRotation(((sensors-game.mirror.transform.position).normalized+StarshipLighting.ToSun).normalized);
            game.lighting.Synchronize();Settle(20);
            Require(game.mirror.WorldWidth<3.5f&&game.lightGate.LitSensors==2,"Both real receiver hits below the old 3.5m size condition drive the same illuminated gate state");
            Require(game.lightGate.charge>0&&game.lightGate.charge<1&&!game.lightGate.unlocked,"Two lit indicators show partial charge before the 0.8 second unlock");
            Require(!game.CanStand(StarshipWorld.Point(1,new Vector3(0,0,13.65f))),"The optical doorway remains physically blocked while charging");
            float partialCharge=game.lightGate.charge;
            game.mirror.transform.rotation=Quaternion.LookRotation(((game.lightGate.sensors[0].position-game.mirror.transform.position).normalized+StarshipLighting.ToSun).normalized);
            Settle(20);
            Require(game.lightGate.LitSensors==1&&game.lightGate.charge<partialCharge&&!game.lightGate.unlocked,"One lit receiver cannot unlock the door and loses partial charge");
            game.mirror.transform.rotation=Quaternion.LookRotation(((sensors-game.mirror.transform.position).normalized+StarshipLighting.ToSun).normalized);
            Settle(50);
            Require(game.lightGate.unlocked&&game.lightGate.LitSensors==2,"Two sustained receiver hits unlock the door without a hidden size condition");
            game.mirror.transform.rotation=Quaternion.LookRotation(-StarshipLighting.ToSun);
            Settle(95);
            Require(game.lightGate.unlocked&&game.lightGate.charge==1,"An unlocked optical gate stays charged after the reflected beam is moved away");
            Require(game.CanStand(StarshipWorld.Point(1,new Vector3(0,0,13.65f))),"The opened optical doorway has real player clearance");
            game.lightGate.Restore();Fixture(mirror,StarshipWorld.Point(1,new Vector3(0,.025f,0)),.55f,Quaternion.identity);
            game.mirror.transform.rotation=Quaternion.LookRotation(((sensors-game.mirror.transform.position).normalized+StarshipLighting.ToSun).normalized);
            Settle(80);Require(game.lightGate.LitSensors<2&&!game.lightGate.unlocked,"The small reflector fixture misses at least one real receiver and remains locked");
            game.ResetAll();game.Resume();game.checkpoint=2;game.PlacePlayer(StarshipWorld.Point(2,new Vector3(-5,0,-10)));
            var rack=game.props.First(p=>p.title.Contains("보관 랙"));
            Look(rack.parts.First(p=>p.name=="Rack upright").bounds.center);game.UpdateAim();game.Interact();
            Require(game.held==null,"Loaded rack cannot be moved before its separate modules are removed");
            var modules=game.props.Where(p=>p.title.Contains("랙 왼쪽")||p.title.Contains("랙 오른쪽")).ToArray();
            for(int i=0;i<modules.Length;i++){Grab(modules[i]);Require(game.held==modules[i],"Rack module "+i+" is separately selectable");game.Cancel();Fixture(modules[i],StarshipWorld.Point(2,new Vector3(-8-i*2,.025f,-10)),1,Quaternion.identity);}
            Grab(rack);Require(game.held==rack,"An emptied rack can be grabbed and reused");game.Cancel();
            var table=game.props.First(p=>p.title.Contains("정비 탁자"));
            Fixture(table,StarshipWorld.Point(2,new Vector3(0,.025f,0)),3,Quaternion.identity);
            game.PlacePlayer(StarshipWorld.Point(2,new Vector3(0,0,-1)));
            Require(game.CanStand(game.player.feet)&&game.FootSafe(game.player.feet),"A physically open enlarged table lets the player stand in its real underside shade");
            Look(StarshipWorld.Point(2,new Vector3(0,2,5)));yield return Capture("03-storage-room");
            game.ResetAll();game.Resume();game.checkpoint=3;game.PlacePlayer(StarshipWorld.Checkpoints[3]);
            var plank=game.props.First(p=>p.title.Contains("再使用")||p.title.Contains("재사용"));
            var canopy=game.props.First(p=>p.title.Contains("상층 차광판"));
            Fixture(plank,StarshipWorld.Point(3,new Vector3(0,.015f,-7)),2.6f,Quaternion.Euler(0,StarshipWorld.Yaws[3],0),true);
            Fixture(canopy,StarshipWorld.Point(3,new Vector3(-3.5f,.025f,-10.8f)),3.8f,Quaternion.Euler(0,StarshipWorld.Yaws[3],0),true);
            Settle(100);
            Require(plank.placement.Ready&&canopy.placement.Ready,"Bridge and shade equipment physically settle on the banks");
            var shadowTrace=new List<string>{"CANOPY "+canopy.transform.position+" scale="+canopy.transform.localScale};
            foreach(var renderer in canopy.renderers)shadowTrace.Add("VISUAL "+renderer.name+" "+renderer.bounds+" shadow="+renderer.shadowCastingMode);
            for(float z=-10;z<-2;z+=.5f)
            {
                Vector3 q=StarshipWorld.Point(3,new Vector3(0,.45f,z));
                var sunHits=Physics.RaycastAll(q+Vector3.up*.035f,StarshipLighting.ToSun,100,1<<StarshipLighting.ShadowLayer,QueryTriggerInteraction.Ignore);
                shadowTrace.Add("SUN localZ="+z+" direct="+game.lighting.IsDirectLit(q,Vector3.up)+" reflected="+game.lighting.IsReflectedLit(q,Vector3.up)+" hits="+string.Join("|",sunHits.Select(h=>h.collider.name+" at "+h.point+" distance="+h.distance)));
            }
            File.WriteAllLines(Path.Combine(output,"bridge-shadow-trace.txt"),shadowTrace);
            int before=game.respawns;Look(StarshipWorld.Point(3,new Vector3(0,1,5)));yield return Capture("04-coolant-bridge");
            var trace=new List<string>{"frame,localX,y,localZ,safe,exposure,respawns,bridgeReady"};
            for(int i=0;i<140;i++)
            {
                Step(new Vector2(StarshipWorld.Direction(3,Vector3.forward).x,StarshipWorld.Direction(3,Vector3.forward).z),i==0);
                Vector3 q=Quaternion.Inverse(Quaternion.Euler(0,StarshipWorld.Yaws[3],0))*(game.player.feet-StarshipWorld.Centers[3]);
                trace.Add(i+","+q.x+","+q.y+","+q.z+","+game.safe+","+game.exposure+","+game.respawns+","+plank.placement.Ready);
            }
            File.WriteAllLines(Path.Combine(output,"bridge-trace.csv"),trace);
            Vector3 local=Quaternion.Inverse(Quaternion.Euler(0,StarshipWorld.Yaws[3],0))*(game.player.feet-StarshipWorld.Centers[3]);
            Require(local.z>-5&&game.respawns==before,"The prepared enlarged panel/shield supports a physical first-trench crossing");
            game.PlacePlayer(StarshipWorld.Checkpoints[3]);
            var capsule=game.props.First(p=>p.title.Contains("부력"));
            Fixture(capsule,StarshipWorld.Point(3,new Vector3(-3,.6f,-1.3f)),1.05f,Quaternion.identity,true);
            bool floating=false;
            for(int i=0;i<1000&&!game.waterGate.unlocked;i++){Step(Vector2.zero);floating|=capsule.floating;if(i%90==0)yield return null;}
            drift=capsule.driftDistance;
            Require(floating&&drift>6.5f&&game.waterGate.unlocked,"A released buoyant capsule drifts more than 6.5m and unlocks the separate walking route");
            Fixture(canopy,StarshipWorld.Point(3,new Vector3(2,.025f,7)),3.8f,Quaternion.Euler(0,StarshipWorld.Yaws[3],0),true);
            Fixture(capsule,StarshipWorld.Point(3,new Vector3(7,.025f,7.7f)),1.2f,Quaternion.Euler(0,StarshipWorld.Yaws[3],0),true);
            Settle(100);
            Vector3 lowStep=StarshipWorld.Point(3,new Vector3(7,0,6.7f));
            Require(game.FootSafe(lowStep),"The lower-step approach lies in the newly placed shield's real shadow");
            game.PlacePlayer(lowStep);Settle(15);
            Require(capsule.placement.Ready&&game.safe,"The recovered transport capsule settles as a shaded lower step");
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(7,.816f,7.7f)),true);
            Require(game.player.feet.y>.78f,"Normal movement jumps onto the recovered capsule");
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(7,1.6f,8.8f)),true);
            Require(game.player.feet.y>1.55f,"The capsule makes the first high ledge reachable");
            yield return WalkTo(StarshipWorld.CastleRest,true);
            Require(game.castleRest&&game.safe,"The high maintenance refuge saves a protected intermediate checkpoint");
            game.Respawn();Require(Vector3.Distance(game.player.feet,StarshipWorld.CastleRest)<.1f,"High-deck failure returns to the saved intermediate refuge");
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(6,2.55f,8.45f)));
            Quaternion ramp=Quaternion.Euler(0,StarshipWorld.Yaws[3]-90,0);
            Fixture(plank,StarshipWorld.Point(3,new Vector3(4.3f,2.57f,10.5f)),2.8f,ramp,true);
            Fixture(canopy,StarshipWorld.Point(3,new Vector3(-1.5f,.025f,8)),3.8f,Quaternion.Euler(0,StarshipWorld.Yaws[3],0),true);
            Settle(150);
            File.WriteAllText(Path.Combine(output,"ramp-trace.txt"),"root="+plank.transform.position+" rotation="+plank.transform.eulerAngles+" bounds="+plank.WorldBounds+" velocity="+plank.body.linearVelocity+" angular="+plank.body.angularVelocity+" supported="+plank.placement.Supported+" progress="+plank.placement.Progress+" player="+game.player.feet);
            Look(StarshipWorld.Point(3,new Vector3(0,3.6f,10.5f)));yield return Capture("05a-reused-panel");
            Require(plank.placement.Ready,"The retrieved panel physically settles between the separated high ledges");
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(5,3.0f,10.5f)),true);
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(1.3f,3.0f,10.5f)));
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(-1,3.55f,10.5f)),true);
            Require(game.player.feet.y>3.5f,"Normal jumping and walking cross the retrieved panel to the upper ledge");
            yield return WalkTo(StarshipWorld.Point(3,new Vector3(-.5f,4.55f,12.8f)),true);
            Require(game.player.feet.y>4.5f,"The upper ledge allows a physical final jump onto the exit deck");
            Look(StarshipWorld.Point(3,new Vector3(0,4.8f,13)));yield return Capture("05-upper-deck");
            // Start each route at its prepared exit deck. The covered connecting
            // halls and checkpoint entry are traversed with ordinary movement.
            game.lightGate.unlocked=game.waterGate.unlocked=true;Settle(100);
            Vector3[][] routes={
                new[]{new Vector3(0,2.6f,-13.2f),new Vector3(0,0,-23),new Vector3(-20,0,-23),new Vector3(-20,0,-42),StarshipWorld.Checkpoints[1]},
                new[]{StarshipWorld.Point(1,new Vector3(0,0,13.2f)),new Vector3(42,0,-42),StarshipWorld.Checkpoints[2]},
                new[]{StarshipWorld.Point(2,new Vector3(0,1.6f,13.2f)),new Vector3(42,0,42),StarshipWorld.Checkpoints[3]},
                new[]{StarshipWorld.Point(3,new Vector3(0,4.55f,13.2f)),new Vector3(-42,0,42),StarshipWorld.Checkpoints[4]}
            };
            for(int room=0;room<routes.Length;room++)
            {
                game.checkpoint=room;game.PlacePlayer(routes[room][0]);game.exposure=0;
                for(int node=1;node<routes[room].Length;node++)yield return WalkTo(routes[room][node]);
                Require(game.checkpoint==room+1&&game.safe,"Covered connecting route "+room+" reaches and saves the next room through normal movement");
            }
            game.ResetAll();game.Resume();game.checkpoint=4;game.PlacePlayer(StarshipWorld.Checkpoints[4]);
            Require(game.props.Count(p=>p.zone==4)==1,"The final room contains exactly one interactable EXIT sign");
            Look(game.ending.exitSign.WorldBounds.center);yield return Capture("06-empty-exit-room");
            game.ending.CheckEntrance();Require(game.ending.phase==StarshipEnding.Phase.None,"Normal-size EXIT does not trigger the hidden corridor");
            var sign=game.ending.exitSign;
            Fixture(sign,StarshipWorld.Point(4,new Vector3(0,.02f,0)),8,Quaternion.Euler(0,180,0));
            Vector3 door=sign.transform.TransformPoint(new Vector3(0,.02f,-.3f));
            game.PlacePlayer(door);game.yaw=Mathf.PI;game.pitch=0;game.SyncView();
            Require(game.CanStand(game.player.feet),"The enlarged EXIT has a real capsule-sized hollow doorway");
            yield return Capture("07-enlarged-exit");
            for(int i=0;i<140&&game.ending.phase==StarshipEnding.Phase.None;i++)Step(Vector2.down);
            Require(game.ending.phase==StarshipEnding.Phase.Corridor,"Walking inside the enlarged placed EXIT enters the hidden black corridor");
            yield return Capture("08-black-corridor");
            for(int i=0;i<650&&game.ending.phase<StarshipEnding.Phase.Drifting;i++){Step(Vector2.down);if(i%90==0)yield return null;}
            Require(game.ending.phase==StarshipEnding.Phase.Drifting,"Normal corridor movement opens the airlock and begins weightless departure");
            Vector3 start=game.view.transform.position;
            for(int i=0;i<480;i++)Step(Vector2.zero);
            Require(game.view.transform.position.y>start.y+15&&!game.controller.enabled,"Departure visibly rises into space and hands off character gravity");
            yield return Capture("09-space-departure");
            for(int i=0;i<420&&!game.won;i++)Step(Vector2.zero);
            Require(game.won&&game.ending.phase==StarshipEnding.Phase.Complete&&game.ending.ShowEnd,"The ending retains the starfield/ship view and displays THE END");
            yield return Capture("10-the-end");
            game.ResetAll();game.Resume();Settle();
            Require(game.controller.enabled&&!game.won&&game.ending.phase==StarshipEnding.Phase.None&&game.safe,"Restart restores gravity, doors, equipment and the initial safe checkpoint");
        }
    }
}
