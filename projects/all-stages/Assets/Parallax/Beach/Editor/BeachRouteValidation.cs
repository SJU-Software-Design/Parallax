using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    // Run in an isolated Editor without -quit or -nographics:
    // -executeMethod ShadeLink.Editor.BeachRouteValidation.Run -shade-output <new folder>
    // SessionState carries the request through the PlayMode domain reload. No scene is saved.
    [InitializeOnLoad]
    public static class BeachRouteValidation
    {
        const string ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity";
        const string Key="Parallax.BeachRouteValidation.";
        const float Dt=1f/60;
        static readonly List<string> bootErrors=new List<string>();
        static Beach.BeachGame game;
        static IEnumerator sequence;
        static Report report;
        static Check current;
        static string output;
        static bool started,finished,outputWritable,simulationConfigured;
        static SimulationMode previousSimulation;

        [Serializable] public sealed class Check
        {
            public string name,failure;
            public bool passed;
            public List<string> details=new List<string>();
        }
        [Serializable] public sealed class Visibility
        {
            public string target,occluder;
            public Vector3 position,viewport;
            public bool inViewport,clear,targetSurface;
            public float occluderDistance;
        }
        [Serializable] public sealed class Report
        {
            public bool passed,sceneUnchanged;
            public string scope,scene,unity,gpu,failure,sceneHashBefore,sceneHashAfter;
            public List<Check> checks=new List<Check>();
            public List<Visibility> visibility=new List<Visibility>();
            public List<string> images=new List<string>(),errors=new List<string>();
        }

        static BeachRouteValidation()
        {
            EditorApplication.update+=Update;
            Application.logMessageReceived+=CaptureLog;
        }

        public static void Run()
        {
            try
            {
                SessionState.SetBool(Key+"OutputWritable",false);
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Start the validation from EditMode in an isolated Editor.");
                output=Argument("-shade-output")??Path.GetFullPath("Validation/BeachRoute");
                output=Path.GetFullPath(output);
                string log=Argument("-logFile");
                string currentLog=string.IsNullOrEmpty(log)?null:Path.GetFullPath(log);
                if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any(entry=>
                    currentLog==null||Directory.Exists(entry)||!string.Equals(Path.GetFullPath(entry),currentLog,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Use a new validation output folder, containing at most this Editor's -logFile: "+output);
                Directory.CreateDirectory(output);outputWritable=true;SessionState.SetBool(Key+"OutputWritable",true);
                string sceneFile=SceneFile();
                if(!File.Exists(sceneFile))throw new FileNotFoundException("Saved Beach scene is missing",sceneFile);
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                    throw new InvalidOperationException("The isolated Editor has unsaved scene changes; validation will not discard or save them.");
                SessionState.SetString(Key+"Output",output);
                SessionState.SetString(Key+"SceneHash",Hash(sceneFile));
                SessionState.SetString(Key+"Start",EditorApplication.timeSinceStartup.ToString("R",CultureInfo.InvariantCulture));
                SessionState.SetString(Key+"Phase","pending");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                PrepareSearchIndex();
                EditorApplication.EnterPlaymode();
            }
            catch(Exception error) { Finish(error.ToString()); }
        }

        static void PrepareSearchIndex()
        {
            // A fresh copied project's default search database is created lazily.
            // Unity cannot create it after EnterPlaymode starts, so force the public
            // discovery path now in EditMode instead of hiding a later Editor error.
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Search bootstrap must run before entering PlayMode.");
            var databases=UnityEditor.Search.SearchService.EnumerateDatabases().ToArray();
            Require(databases.Length>0,"Unity Search did not create/load the copied project's default search database.");
            string index=Path.GetFullPath(Path.Combine(Application.dataPath,"..","UserSettings","Search.index"));
            Debug.Log("BEACH_ROUTE_SEARCH_READY databases="+databases.Length+" projectIndexExists="+File.Exists(index)+" path="+index);
        }

        static void Update()
        {
            string phase=SessionState.GetString(Key+"Phase","");
            if(finished||(phase!="pending"&&phase!="running"))return;
            double began;
            if(double.TryParse(SessionState.GetString(Key+"Start","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out began)&&
                EditorApplication.timeSinceStartup-began>360)
            { Finish("Validation exceeded the 360-second Editor watchdog.");return; }
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!started)
                {
                    started=true;output=SessionState.GetString(Key+"Output","");
                    report=new Report {
                        scope="Saved Beach scene in real PlayMode and native Physics. Exact bridge/garden structures, layout and player-eye parasol views, checkpoint shade and headroom, checkpoint-1 goal sight, prepared diagonal bridge and parasol crossing, continuous S-garden input, and the compact beach-to-front-castle approach. Separate native validation covers placement of the two castle fragments and upper parasol, shadow materialization and boat travel. Prior optical completion, prop placements and segment ground starts are explicit fixtures. Heat, water hazards, controller collisions and respawn remain active. This is not an unassisted complete-stage solve; renders omit IMGUI.",
                        scene=ScenePath,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                        sceneHashBefore=SessionState.GetString(Key+"SceneHash","")
                    };
                    report.errors.AddRange(bootErrors);SessionState.SetString(Key+"Phase","running");
                    sequence=Sequence();
                }
                if(!sequence.MoveNext())Finish(null);
            }
            catch(Exception error) { Finish(error.ToString()); }
        }

        static Beach.BeachWater[] waters;
        static Beach.BeachWaterGateLink[] links;
        static readonly System.Reflection.FieldInfo JumpHeld=typeof(Beach.BeachGame).GetField("gameJumpHeld",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        static readonly List<string> trace=new List<string>();
        static int frame,lastJump=-100;
        static IEnumerator Sequence()
        {
            yield return null;yield return null;yield return null;
            var games=UnityEngine.Object.FindObjectsByType<Beach.BeachGame>(FindObjectsSortMode.None);
            Require(games.Length==1,"Expected one saved BeachGame; found "+games.Length);
            game=games[0];game.automated=true;
            Require(game.lighting.Count>40,"PlayMode shadow-query meshes were not initialized.");
            Require(JumpHeld!=null,"Actual held-jump input field was not found.");
            waters=UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None);
            links=UnityEngine.Object.FindObjectsByType<Beach.BeachWaterGateLink>(FindObjectsSortMode.None);
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationConfigured=true;
            trace.Add("frame,check,x,y,z,grounded,safe,exposure,respawns,jump");
            RunCheck("Saved route solids, checkpoint shade, goal sight and parasol views",CheckStructure);
            yield return null;
            RunCheck("Prepared diagonal bridge, two islets and continuous S-garden route",CheckBridge);
            yield return null;
            RunCheck("Continuous central beach approach to the compact front castle",CheckCastleApproach);
            yield return null;
            File.WriteAllLines(Path.Combine(output,"route-trace.csv"),trace);
        }
        static void RunCheck(string name,Action action)
        {
            current=new Check{name=name};report.checks.Add(current);
            try { Reset();action();current.passed=true;Debug.Log("BEACH_ROUTE_PASS "+name); }
            catch(Exception error)
            {
                current.failure=error.ToString();Debug.Log("BEACH_ROUTE_FAIL "+name+": "+error.Message);
                try { Capture("Failure-"+report.checks.Count,game.view.transform.position,game.view.transform.rotation); }
                catch(Exception capture) { current.failure+="\nFailure capture: "+capture; }
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"route-trace.csv"),trace);
                try { Reset(); }
                catch(Exception error) { current.passed=false;current.failure+="\nCleanup: "+error; }
                current=null;
            }
        }
        static void Reset()
        {
            if(game==null)return;
            game.automated=true;game.ResetAll();game.Resume();
            JumpHeld.SetValue(game,false);frame=0;lastJump=-100;
            foreach(var link in links)link.SendMessage("Update",SendMessageOptions.RequireReceiver);
            Physics.SyncTransforms();game.lighting.Synchronize();
        }
        static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
        static void Detail(string text) { current.details.Add(text); }
        static void Step(Vector2 movement=default,bool jump=false,bool held=false)
        {
            JumpHeld.SetValue(game,held);game.Tick(Dt,movement,jump);
            // This synchronous fixture must also run the real linked gate's normal Update.
            foreach(var link in links)link.SendMessage("Update",SendMessageOptions.RequireReceiver);
            foreach(var water in waters)water.Simulate(Dt);
            Physics.Simulate(Dt);game.lighting.Synchronize();
            frame++;if(jump)lastJump=frame;
            if((game.controller.collisionFlags&CollisionFlags.Above)!=0)Detail("Native ceiling contact feet="+game.player.feet+" head-near="+string.Join("; ",Physics.OverlapSphere(game.player.feet+Vector3.up*1.6f,.45f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).Where(c=>c!=game.controller).Select(c=>Hierarchy(c.transform)+" @ "+c.transform.position)));
            if(frame%10==0||jump)trace.Add(string.Format(CultureInfo.InvariantCulture,"{0},\"{1}\",{2:F4},{3:F4},{4:F4},{5},{6},{7:F4},{8},{9}",frame,current.name,game.player.feet.x,game.player.feet.y,game.player.feet.z,game.player.grounded,game.safe,game.exposure,game.respawns,jump));
        }
        static void Idle(int frames) { for(int i=0;i<frames;i++)Step(); }
        static void SegmentStart(int checkpoint,Vector3 groundedStart)
        {
            // Earlier optical completion is a declared segment precondition, not a puzzle result.
            game.lightGate.unlocked=true;game.checkpoint=checkpoint;
            Require(game.CanStand(groundedStart+Vector3.up*.025f),"Prepared ground start overlaps a solid: "+groundedStart);
            game.PlacePlayer(groundedStart);Idle(3);
            Require(game.player.grounded,"Prepared start did not settle onto native ground: "+game.player.feet);
            Detail("Declared prior optical-completion fixture; ground start="+groundedStart+"; heat and hazards active.");
        }
        static void Travel(Vector3 target,bool jump=false,int limit=300)
        {
            int respawns=game.respawns;Vector3 start=game.player.feet;float peak=start.y;
            if(jump)
            {
                while(frame-lastJump<48) { Step();Require(game.respawns==respawns,"Respawn while waiting for normal jump cooldown at "+game.player.feet); }
                Require(game.player.grounded,"Jump must begin grounded: "+game.player.feet+" -> "+target);
            }
            bool launched=false;int heldFrames=0;
            for(int i=0;i<limit;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                bool reached=delta.magnitude<.20f&&Mathf.Abs(game.player.feet.y-target.y)<.22f&&game.player.grounded;
                if(reached&&(!jump||launched&&i>15))
                {
                    Detail("Native input "+(jump?"jump":"walk")+" "+start+" -> "+target+" actual="+game.player.feet+" peak="+peak.ToString("F3",CultureInfo.InvariantCulture)+" safe="+game.safe+" exposure="+game.exposure.ToString("F3",CultureInfo.InvariantCulture));
                    return;
                }
                if(delta.magnitude>.05f)game.yaw=Mathf.Atan2(delta.x,delta.y);
                bool pressed=jump&&!launched;
                if(pressed)launched=true;
                bool held=launched&&heldFrames++<7;
                // Real keyboard ramp and squared response are retained; ease input near a landing.
                Vector2 input=delta.magnitude<.06f?Vector2.zero:delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.65f));
                Step(input,pressed,held);peak=Mathf.Max(peak,game.player.feet.y);
                Require(game.respawns==respawns,"Route respawn (heat/water/fall) from "+start+" toward "+target+" actual="+game.player.feet+" frame="+frame);
                if(game.won&&Vector3.Distance(game.player.feet,target)<1.35f)return;
            }
            throw new InvalidOperationException("Native route did not reach "+target+" from "+start+"; actual="+game.player.feet+" grounded="+game.player.grounded+" safe="+game.safe+" exposure="+game.exposure+" peak="+peak+" nearby="+string.Join("; ",Physics.OverlapSphere(game.player.feet+Vector3.up*.8f,.9f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).Where(c=>c!=game.controller).Select(c=>Hierarchy(c.transform)+" @ "+c.transform.position)));
        }
        static Beach.BeachProp Prop(string title) { return game.props.Single(p=>p.title==title); }
        static void Fixture(Beach.BeachProp prop,Vector3 position,float scale,float yaw=0)
        {
            prop.Restore();prop.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));prop.transform.localScale=Vector3.one*scale;
            prop.body.position=position;prop.body.rotation=prop.transform.rotation;Physics.SyncTransforms();prop.Release();game.lighting.Synchronize();
            Detail("Prepared released prop "+prop.title+" root="+position+" scale="+scale+" yaw="+yaw+"; gravity and support settling remain native.");
        }
        static void Solid(string name,Vector3 top,Vector2 size,bool walkable=true)
        {
            var obj=GameObject.Find(name);Require(obj!=null,"Saved route solid missing: "+name);
            var box=obj.GetComponent<BoxCollider>();Require(box!=null&&box.enabled,"Route solid lacks native BoxCollider: "+name);
            Bounds bounds=box.bounds;
            Require(Vector3.Distance(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z),top)<.015f&&Mathf.Abs(bounds.size.x-size.x)<.015f&&Mathf.Abs(bounds.size.z-size.y)<.015f&&Mathf.Abs(bounds.min.y)<.015f,"Route bounds changed: "+name+" actual="+bounds);
            Require(Physics.Raycast(top+Vector3.up*.5f,Vector3.down,out var hit,.6f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&hit.collider==box,"Native top support missing/occluded: "+name);
            if(walkable)Require(game.CanStand(top+Vector3.up*.06f),"Route center lacks controller headroom: "+name);
            Detail(name+" native solid top="+top+" xz="+size+" supported to y=0.");
        }
        static void Water(string name,Vector2 minimum,Vector2 maximum,Vector3 flow,bool transport)
        {
            var water=waters.SingleOrDefault(w=>w.name==name);
            Require(water!=null&&Vector2.Distance(water.min,minimum)<.001f&&Vector2.Distance(water.max,maximum)<.001f&&Vector3.Distance(water.flow,flow)<.001f&&water.transportChannel==transport,"Original water contract changed: "+name);
        }
        static void View(string name,Vector3 position,Vector3 target) { Capture(name,position,Quaternion.LookRotation(target-position)); }
        static void CheckCheckpointViews()
        {
            // These are gameplay requirements, independent of the roof model or its hierarchy.
            for(int i=0;i<Beach.BeachWorld.Checkpoints.Length;i++)
            {
                var feet=Beach.BeachWorld.Checkpoints[i];
                Require(game.FootSafe(feet),"Checkpoint "+i+" lacks real protective shade at "+feet);
                Require(game.CanStand(feet+Vector3.up*.06f),"Checkpoint "+i+" lacks native controller headroom at "+feet);
                Detail("Checkpoint "+i+" retains actual foot-safe shade and controller clearance at "+feet);
            }
            Vector3 eye=Beach.BeachWorld.Checkpoints[1]+Vector3.up*1.55f;
            View("CrossingPlayerEye",eye,new Vector3(0,1.55f,40));
            View("CheckpointParasolUnderside",eye,new Vector3(.68f,3.05f,25.75f));
            View("CrossingParasolClose",new Vector3(-8.4f,2.0f,30),new Vector3(-3.1f,3.5f,33.6f));

            var flag=GameObject.Find("Beach goal flag");
            Require(flag!=null,"The saved scene has no Beach goal flag.");
            var renderer=flag.GetComponent<Renderer>()??flag.GetComponentInChildren<Renderer>();
            Require(renderer!=null&&renderer.enabled,"The goal flag needs an enabled renderer.");
            Vector3 target=renderer.bounds.center,delta=target-eye;
            Quaternion rotation=Quaternion.LookRotation(new Vector3(0,2.5f,70)-eye);
            Capture("CheckpointGoalPlayerEye",eye,rotation);

            // Use the same FOV as the player. This camera is only an observer and
            // does not move the controller or alter a prior-completion fixture.
            var host=new GameObject("Validation checkpoint sight");var camera=host.AddComponent<Camera>();
            camera.CopyFrom(game.view);camera.enabled=false;camera.transform.SetPositionAndRotation(eye,rotation);
            var sight=new Visibility {target="Checkpoint 1 / Beach goal flag",position=target,clear=true,occluder="none"};
            try
            {
                sight.viewport=camera.WorldToViewportPoint(target);
                sight.inViewport=sight.viewport.z>0&&sight.viewport.x>0&&sight.viewport.x<1&&sight.viewport.y>0&&sight.viewport.y<1;
                // Include the real shadow-query meshes: non-solid fabric can
                // obscure a goal just as a physical roof can. No roof is skipped.
                foreach(var hit in Physics.RaycastAll(eye,delta.normalized,delta.magnitude-.03f,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                {
                    if(hit.collider==game.controller)continue;
                    bool targetHit=hit.collider.transform==flag.transform||hit.collider.transform.IsChildOf(flag.transform);
                    sight.targetSurface=targetHit;sight.clear=targetHit;
                    sight.occluder=Hierarchy(hit.collider.transform);sight.occluderDistance=hit.distance;break;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            report.visibility.Add(sight);
            Detail(sight.target+" viewport="+sight.viewport+" clear="+sight.clear+" firstHit="+sight.occluder);
            Require(sight.inViewport&&sight.clear,"The goal flag must remain visible from checkpoint 1 at player eye height; firstHit="+sight.occluder);
        }
        static void CheckStructure()
        {
            // Capture first so a later failing gameplay fixture still leaves the six layout views.
            View("BridgeApproach",new Vector3(0,3,28),new Vector3(-1,.8f,36.7f));
            View("BridgeIsletsOverview",new Vector3(15,14,30),new Vector3(0,.5f,36.7f));
            View("SGarden",new Vector3(13,7,40),new Vector3(0,.5f,44));
            View("BoatWaterGap",new Vector3(18,8,52),new Vector3(0,.7f,61));
            View("CastleApproach",new Vector3(9,7,78),new Vector3(0,2,90));
            View("CastleFrontCuts",new Vector3(9,10,84),new Vector3(0,3.8f,93));
            CheckCheckpointViews();
            Require(game.props.Length==11,"Eleven essential props including two separate castle fragments and one upper parasol must be registered.");
            Water("판자 횡단 수로",new Vector2(-14,33),new Vector2(14,40),Vector3.zero,false);
            Water("그늘 배 항로",new Vector2(-14,55),new Vector2(14,69),new Vector3(0,0,.9f),false);
            Solid("02 · 왼쪽 쉼터 섬",new Vector3(-4,.3f,35.2f),new Vector2(3,2.3f));
            Solid("02 · 오른쪽 연결 섬",new Vector3(4.5f,.3f,38.5f),new Vector2(3,2.1f));
            Solid("02 · 왼쪽 섬 접안길",new Vector3(-3.6f,.3f,33.95f),new Vector2(1.5f,1.3f));
            Solid("02 · 오른쪽 섬 접안길",new Vector3(4.1f,.3f,39.9f),new Vector2(1.6f,1.4f));
            Solid("02 · 왼쪽 모래 화단",new Vector3(-4,1,42.5f),new Vector2(8,1.5f),false);
            Solid("02 · 오른쪽 모래 화단",new Vector3(4,1.1f,46.2f),new Vector2(8,1.5f),false);
            Solid("03 · 그늘 배 출발 선착장",new Vector3(0,.24f,54.5f),new Vector2(4.8f,3));
            Solid("03 · 모래성 도착 선착장",new Vector3(0,.24f,69.3f),new Vector2(5.4f,2.8f));
            foreach(string removed in new[]{"04 · 관람 테라스 1","04 · 관람 테라스 2","04 · 관람 테라스 3","04 · 성벽 앞 테라스",
                "탑을 두르는 상부 성벽","정원에서 성벽으로 · 모래계단","중간 쉼터에서 위층으로 · 모래계단","위층 성벽 파라솔","정상 복구 성벽의 파라솔"})
                Require(GameObject.Find(removed)==null,"The retired external castle route or giant canopy is still present: "+removed);
            foreach(var refuge in new[]{new Vector3(-4,.3f,35.2f),new Vector3(3,0,42.6f),new Vector3(-2.6f,0,46.5f),new Vector3(0,.24f,54.2f),new Vector3(0,.24f,69.7f),new Vector3(0,0,80.5f),Beach.BeachCastleRestoration.LowerStart})
                Require(game.FootSafe(refuge),"Real geometric shade missing at route refuge "+refuge);
            Detail("Native shade at five bridge/boat refuges and two compact castle-approach stops; retired side scaffolding and giant canopies are absent. Images omit HUD labels.");
        }
        static void CheckBridge()
        {
            SegmentStart(1,Beach.BeachWorld.Checkpoints[1]);
            var plank=Prop("02 · 짧은 판자");var parasol=Prop("02 · 햇빛 가리개");
            foreach(float scale in new[]{3.3f,3.8f,4.2f})
            {
                Fixture(plank,new Vector3(.25f,.30f,36.85f),scale,68.8f);Idle(180);
                bool legal=game.ValidPlacement(plank,out var reason);
                Require(plank.placement.Ready&&legal,"Diagonal plank scale "+scale+" must natively settle to legal Ready support; ready="+plank.placement.Ready+" supported="+plank.placement.Supported+" reason="+reason+" bounds="+plank.WorldBounds);
                Detail("Scale "+scale+" diagonal plank: native Ready=true, ValidPlacement=true; settled="+plank.transform.position+" bounds="+plank.WorldBounds);
            }
            Fixture(plank,new Vector3(.25f,.30f,36.85f),3.3f,68.8f);
            Fixture(parasol,new Vector3(3,0,32.5f),4.5f);
            Idle(180);
            Require(plank.placement.Ready,"Released diagonal plank did not become Ready; root="+plank.transform.position+" bounds="+plank.WorldBounds);
            Require(game.ValidPlacement(plank,out var plankReason),"Settled diagonal plank has an illegal overlap/bounds: "+plankReason);
            Require(parasol.placement.Ready&&game.ValidPlacement(parasol,out var parasolReason),"Prepared parasol must have legal native Ready support.");
            Detail("Settled legal Ready diagonal plank bounds="+plank.WorldBounds+" parasol root="+parasol.transform.position);
            View("BridgePrepared",new Vector3(15,14,30),new Vector3(0,.5f,36.7f));
            // One declared ground start; all following waypoints use uninterrupted native input.
            game.PlacePlayer(new Vector3(-4,0,32.35f));Idle(3);
            Travel(new Vector3(-4,.3f,34.65f),true);
            float top=plank.WorldBounds.max.y;
            Travel(new Vector3(-3.2f,top,35.45f),true);
            Travel(new Vector3(.25f,top,36.85f));
            Travel(new Vector3(3.8f,top,38.2f));
            Travel(new Vector3(4.5f,.3f,39.0f));
            Travel(new Vector3(4.5f,0,41.0f));
            Travel(new Vector3(3,0,42.6f));Idle(35);
            Travel(new Vector3(2,0,43.8f));Travel(new Vector3(-1.2f,0,43.8f));
            Travel(new Vector3(-2.6f,0,46.5f));Idle(35);
            Travel(Beach.BeachWorld.Checkpoints[2]);
            Require(game.respawns==0&&game.checkpoint==2,"Prepared bridge/S-garden route must reach checkpoint 2 without hazard recovery.");
        }
        static void CheckCastleApproach()
        {
            SegmentStart(3,Beach.BeachWorld.Checkpoints[3]);
            Require(game.castleRoute!=null&&game.castleRoute.LowerFragment!=null&&game.castleRoute.UpperFragment!=null&&game.castleRoute.Parasol!=null,
                "The compact castle must register both miniature sources and the upper parasol.");
            Travel(new Vector3(0,0,80.5f));Idle(60);
            Travel(new Vector3(0,0,85.8f));
            // The seven shallow original-front approach steps are traversed by
            // continuous walking input; no jumps or castle-puzzle fixtures occur.
            Travel(new Vector3(0,1.1f,88.7f));Travel(Beach.BeachCastleRestoration.LowerStart);
            Require(game.respawns==0,"The central beach and shallow front steps must reach the lower miniature worktop without recovery.");
            Capture("CastleFrontApproachPlayerEye",game.view.transform.position,game.view.transform.rotation);
        }
        static void Capture(string name,Vector3 position,Quaternion rotation)
        {
            var host=new GameObject("Validation camera "+name);var camera=host.AddComponent<Camera>();camera.CopyFrom(game.view);camera.enabled=false;
            var texture=new RenderTexture(1600,900,24);Texture2D pixels=null;var active=RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(position,rotation);camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                string path=Path.Combine(output,name+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());report.images.Add(path);
            }
            finally
            {
                RenderTexture.active=active;camera.targetTexture=null;texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(host);
            }
        }
        static void CaptureLog(string message,string stack,LogType kind)
        {
            string phase=SessionState.GetString(Key+"Phase","");
            if(phase!="pending"&&phase!="running")return;
            if(kind!=LogType.Error&&kind!=LogType.Exception&&kind!=LogType.Assert)return;
            string text=message+"\n"+stack;if(report==null)bootErrors.Add(text);else report.errors.Add(text);
        }
        static void Finish(string failure)
        {
            if(finished)return;finished=true;
            output=output??SessionState.GetString(Key+"Output",Path.GetFullPath("Validation/BeachRoute"));
            if(report==null)report=new Report{scene=ScenePath,unity=Application.unityVersion,sceneHashBefore=SessionState.GetString(Key+"SceneHash","")};
            report.failure=failure;
            if(simulationConfigured)Physics.simulationMode=previousSimulation;
            try
            {
                report.sceneHashAfter=File.Exists(SceneFile())?Hash(SceneFile()):"missing";
                report.sceneUnchanged=report.sceneHashBefore==report.sceneHashAfter;
                report.passed=failure==null&&report.checks.Count==3&&report.checks.All(c=>c.passed)&&report.errors.Count==0&&report.sceneUnchanged;
                if(!outputWritable&&!SessionState.GetBool(Key+"OutputWritable",false))
                    throw new InvalidOperationException("No new output folder was approved; existing validation files were left untouched. "+failure);
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"route-results.json"),JsonUtility.ToJson(report,true));
                Debug.Log("BEACH_ROUTE_FINISHED passed="+report.passed+" checks="+report.checks.Count+" errors="+report.errors.Count+" sceneUnchanged="+report.sceneUnchanged+" output="+output);
            }
            catch(Exception error) { report.passed=false;Debug.LogError("BEACH_ROUTE_REPORT_FAILED "+error); }
            SessionState.SetString(Key+"Phase","");SessionState.SetBool(Key+"OutputWritable",false);int code=report.passed?0:1;
            EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
        static string SceneFile() { return Path.GetFullPath(Path.Combine(Application.dataPath,"..",ScenePath)); }
        static string Hash(string path)
        { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(); }
        static string Argument(string name)
        { var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:null; }
        static string Hierarchy(Transform value)
        { string name=value.name;while(value.parent!=null){value=value.parent;name=value.name+"/"+name;}return name; }
    }
}
