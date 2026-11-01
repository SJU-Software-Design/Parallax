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
    // Run only in an isolated Editor, with GPU and without -quit:
    // -executeMethod ShadeLink.Editor.BeachBoatValidation.Run -shade-output <new folder>
    [InitializeOnLoad]
    public static class BeachBoatValidation
    {
        const string ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity",Key="Parallax.BeachBoatValidation.";
        const float Dt=1f/60;
        static Beach.BeachGame game;
        static Beach.BeachBoat boat;
        static Beach.BeachWater[] waters;
        static Report report;
        static Check current;
        static IEnumerator sequence;
        static string output;
        static bool started,finished,simulationConfigured,outputWritable;
        static SimulationMode previousSimulation;
        static readonly List<string> bootErrors=new List<string>(),trace=new List<string>();
        static readonly System.Reflection.FieldInfo JumpHeld=typeof(Beach.BeachGame).GetField("gameJumpHeld",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        static int frame;

        [Serializable] public sealed class Check
        {public string name,failure;public bool passed;public List<string> details=new List<string>();}
        [Serializable] public sealed class Report
        {
            public bool passed,sceneUnchanged;
            public string scope,scene,unity,gpu,failure,sceneHashBefore,sceneHashAfter;
            public List<Check> checks=new List<Check>();
            public List<string> images=new List<string>(),errors=new List<string>();
        }
        static BeachBoatValidation(){EditorApplication.update+=Update;Application.logMessageReceived+=CaptureLog;}
        public static void Run()
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start from EditMode in an isolated project.");
                output=Path.GetFullPath(Argument("-shade-output")??"Validation/BeachBoat");
                string log=Argument("-logFile"),fullLog=string.IsNullOrEmpty(log)?null:Path.GetFullPath(log);
                if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any(p=>fullLog==null||Directory.Exists(p)||!string.Equals(Path.GetFullPath(p),fullLog,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Use a fresh validation output folder: "+output);
                Directory.CreateDirectory(output);outputWritable=true;SessionState.SetBool(Key+"Writable",true);
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("The isolated project contains an unsaved scene.");
                SessionState.SetString(Key+"Output",output);SessionState.SetString(Key+"Hash",Hash(SceneFile()));
                SessionState.SetString(Key+"Start",EditorApplication.timeSinceStartup.ToString("R",CultureInfo.InvariantCulture));SessionState.SetString(Key+"Phase","pending");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                if(!UnityEditor.Search.SearchService.EnumerateDatabases().Any())throw new InvalidOperationException("Default Unity Search database was not created before PlayMode.");
                EditorApplication.EnterPlaymode();
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void Update()
        {
            string phase=SessionState.GetString(Key+"Phase","");if(finished||(phase!="pending"&&phase!="running"))return;
            if(double.TryParse(SessionState.GetString(Key+"Start","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out var began)&&EditorApplication.timeSinceStartup-began>360)
            {Finish("Validation exceeded the 360-second watchdog.");return;}
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!started)
                {
                    started=true;output=SessionState.GetString(Key+"Output","");
                    report=new Report{scene=ScenePath,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,sceneHashBefore=SessionState.GetString(Key+"Hash",""),
                        scope="Saved Beach scene in real GPU PlayMode and native physics. Earlier optical completion and revealing the generated miniature are declared fixtures; shadow-silhouette matching has separate validation. Tests retain actual camera pickup/perspective placement, buoyancy, mooring, native controller boarding and passive riding, real canopy-shadow queries, exposure/water failure, recovery and landing. A negative experiment temporarily disables the actual canopy renderer to verify shade dependency; no heat rule or support state is overridden. This is not an unassisted full-stage solve; camera renders omit IMGUI."};
                    report.errors.AddRange(bootErrors);SessionState.SetString(Key+"Phase","running");sequence=Sequence();
                }
                if(!sequence.MoveNext())Finish(null);
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static IEnumerator Sequence()
        {
            yield return null;yield return null;yield return null;
            game=UnityEngine.Object.FindFirstObjectByType<Beach.BeachGame>();Require(game!=null,"Saved BeachGame missing.");
            boat=game.boat;Require(boat!=null&&boat.Prop!=null,"Saved integrated-canopy boat missing.");Require(JumpHeld!=null,"Native held-jump input unavailable.");
            game.automated=true;waters=UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None);
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationConfigured=true;
            trace.Add("frame,check,playerX,playerY,playerZ,boatX,boatY,boatZ,ready,aboard,safe,exposure,respawns,departed,arrived,berth,gate");
            bool all=Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-all")>=0;
            if(all||Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-arrival-only")>=0)
            {
                foreach(var pose in new[]{new Vector2(1.2f,0),new Vector2(1.2f,90),new Vector2(1.2f,180),new Vector2(1.6f,0),new Vector2(1.6f,90),new Vector2(1.6f,180)})
                {
                    Vector2 fixture=pose;
                    BeginCheck("Arrival with enlarged rotated boat: scale="+pose.x+" yaw="+pose.y);yield return null;yield return null;
                    RunCheck(()=>CheckRotatedArrival(fixture.x,fixture.y));yield return null;
                }
                BeginCheck("Native early disembark onto the real arrival pier");yield return null;yield return null;RunCheck(CheckEarlyDisembark);yield return null;
                BeginCheck("A boat at the arrival pier without departure cannot unlock the gate");yield return null;yield return null;RunCheck(CheckUnlaunchedArrival);yield return null;
                BeginCheck("A launched empty boat cannot complete a remote shore-side player's crossing");yield return null;yield return null;RunCheck(CheckEmptyArrival);yield return null;
                BeginCheck("Native diagonal landing from a side-lane boat opens the arrival gate");yield return null;yield return null;RunCheck(CheckSideLanding);yield return null;
                if(!all)yield break;
            }
            BeginCheck("Actual miniature camera pickup and perspective enlargement");yield return null;yield return null;RunCheck(CheckMiniature);yield return null;
            if(!all&&Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-mini-only")>=0)yield break;
            BeginCheck("Hull-only buoyancy, launch mooring and real integrated shade");yield return null;yield return null;RunCheck(CheckBuoyancyAndShade);yield return null;
            BeginCheck("Native boarding, passive shaded current ride and actual landing");yield return null;yield return null;RunCheck(CheckRide);yield return null;
            BeginCheck("Native water failure recovers an enlarged uncompleted boat");yield return null;yield return null;RunCheck(CheckRecovery);yield return null;
            BeginCheck("Cargo cannot unlock arrival and no dry-side bypass remains");yield return null;yield return null;RunCheck(CheckNoCargoBypass);yield return null;
        }
        static void BeginCheck(string name)
        {current=new Check{name=name};report.checks.Add(current);ResetFixture();}
        static void RunCheck(Action action)
        {
            string name=current.name;
            try{action();current.passed=true;Debug.Log("BEACH_BOAT_PASS "+name);}
            catch(Exception error)
            {
                current.failure=error.ToString();Debug.Log("BEACH_BOAT_FAIL "+name+": "+error.Message);
                try{Capture("Failure-"+report.checks.Count,game.view.transform.position,game.view.transform.position+game.view.transform.forward);}catch(Exception capture){current.failure+="\nCapture: "+capture;}
            }
            finally{File.WriteAllLines(Path.Combine(output,"boat-trace.csv"),trace);current=null;}
        }
        static void ResetFixture()
        {
            game.ResetAll();game.automated=true;game.Resume();game.lightGate.unlocked=true;game.checkpoint=2;
            // Explicit post-puzzle availability fixture; it does not pretend to
            // solve the silhouette or modify how the real boat works afterward.
            boat.gameObject.SetActive(true);boat.ResetBoat();game.lighting.Rebuild();game.PlacePlayer(Beach.BeachWorld.Checkpoints[2]);
            JumpHeld.SetValue(game,false);frame=0;Physics.SyncTransforms();game.lighting.Synchronize();Idle(3);
            Detail("Declared earlier optical-completion and boat-reveal fixture; original miniature home="+boat.Prop.homePosition+" scale="+boat.Prop.homeScale);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Detail(string message){current.details.Add(message);}
        static void Step(Vector2 move=default,bool jump=false,bool held=false)
        {
            JumpHeld.SetValue(game,held);game.Tick(Dt,move,jump);
            foreach(var water in waters)water.Simulate(Dt);Physics.Simulate(Dt);Physics.SyncTransforms();game.lighting.Synchronize();frame++;
            if(frame%10==0||jump)trace.Add(string.Format(CultureInfo.InvariantCulture,"{0},\"{1}\",{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8},{9},{10},{11:F4},{12},{13},{14},{15},{16}",
                frame,current.name,game.player.feet.x,game.player.feet.y,game.player.feet.z,boat.transform.position.x,boat.transform.position.y,boat.transform.position.z,boat.Prop.placement.Ready,boat.IsAboard,game.safe,game.exposure,game.respawns,boat.Departed,boat.Arrived,boat.AtBerth,game.waterGate.unlocked));
        }
        static void Idle(int count){for(int i=0;i<count;i++)Step();}
        static void Look(Vector3 target)
        {
            Vector3 delta=target-game.view.transform.position;game.yaw=Mathf.Atan2(delta.x,delta.z);game.pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude);game.SyncView();
        }
        static void CheckMiniature()
        {
            Require(boat.Prop.maxScale<=1.6f,"Boat enlargement must stay within the actually navigable canopy clearance.");
            Require(boat.transform.localScale.x<.5f,"The materialized boat must begin as a miniature.");
            Vector3 home=boat.Prop.homePosition;
            Capture("MiniatureBoat",home+new Vector3(2,1.3f,-2.5f),boat.transform.position+Vector3.up*.25f);
            Travel(new Vector3(0,0,49.3f));Travel(new Vector3(home.x,.08f,49.3f));
            Travel(new Vector3(home.x,0,home.z-.95f));
            Look(boat.HullBounds.center);game.UpdateAim();Require(game.aimed==boat.Prop,"Actual camera ray did not select the miniature: "+(game.aimed==null?"none":game.aimed.title));
            game.Interact();Require(game.held==boat.Prop,"Actual grab lifecycle did not hold the miniature.");float initial=boat.transform.localScale.x;
            Detail("Native board-front tray pickup at "+game.player.feet+"; actual camera reference distance="+game.referenceDistance+" miniature scale="+initial);
            // Carry the same miniature back along the established shaded strip.
            // Looking down keeps the perspective preview near the operator while
            // walking, before aiming it out onto the actual launch water.
            Look(game.view.transform.position+new Vector3(-1,-.8f,0));
            Travel(new Vector3(home.x,.08f,49.3f));Travel(new Vector3(0,.08f,49.3f));
            Look(new Vector3(0,-.2f,63));Idle(60);
            Travel(new Vector3(0,.24f,55.6f));Idle(12);
            Detail("Native held carry to departure edge ends at "+game.player.feet);
            bool dropped=false;
            foreach(var target in new[]{new Vector3(0,-1.7f,64),new Vector3(0,-1f,64),new Vector3(3,-.1f,59),new Vector3(-3,-.1f,59),new Vector3(0,-.5f,64)})
            {
                Look(target);Idle(60);
                var interaction=(ObjectInteraction<Beach.BeachProp>)typeof(Beach.BeachGame).GetProperty("Interaction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                var limit=interaction.LastPlacement;
                Detail("Native perspective aim="+target+" scale="+boat.transform.localScale.x+" pose="+boat.transform.position+" blocked="+game.PlacementBlocked+" reason="+game.invalidReason+" limiter="+limit.Limiter+" source="+(limit.Source==null?"none":limit.Source.name)+" point="+limit.Point);
                if(boat.transform.localScale.x>1.2f&&boat.HullBounds.min.z>56.05f&&boat.HullBounds.center.z<=60&&game.Drop()){dropped=true;break;}
            }
            Require(dropped&&boat.transform.localScale.x>initial*2.8f,"Camera-driven enlargement must produce a released larger boat without a transform-size fixture.");
            Idle(300);Require(boat.Prop.placement.Ready&&Beach.BeachWater.Supports(boat.Prop),"The real camera-released boat did not float and become Ready.");
            Require(boat.HullBounds.max.y>waters.Single(w=>w.name=="그늘 배 항로").surface+.035f,"The enlarged walkable deck must stay above the water hazard.");
            Capture("PerspectiveEnlargedBoat",new Vector3(7,7,54),boat.transform.position+Vector3.up);
            Board(false);RideAndLand("CameraDerived");
        }
        static void FloatFixture(float scale=1.6f)
        {
            boat.Prop.Restore();boat.transform.SetPositionAndRotation(new Vector3(0,.08f,58.2f),Quaternion.identity);boat.transform.localScale=Vector3.one*scale;
            boat.Prop.body.position=boat.transform.position;boat.Prop.body.rotation=boat.transform.rotation;Physics.SyncTransforms();boat.Prop.Release();Idle(240);
            Require(boat.Prop.placement.Ready&&Beach.BeachWater.Supports(boat.Prop),"Prepared released boat must settle natively into real buoyant Ready support.");
            Require(game.ValidPlacement(boat.Prop,out var reason),"Prepared boat overlaps real structures or map limits: "+reason);
            Detail("Declared enlarged/released launch fixture scale="+scale+"; buoyancy, gravity, support settling and controller remain native. settled="+boat.transform.position+" hull="+boat.HullBounds);
        }
        static Vector3 BoardingMark()=>boat.transform.TransformPoint(new Vector3(0,.12f,.40f));
        static void Board(bool prepareStart=true)
        {
            float runX=Mathf.Clamp(boat.HullBounds.center.x,-1.9f,1.9f);
            Vector3 runStart=new Vector3(runX,.24f,54.7f);
            // Physical launch fixtures declare one grounded start; the camera-
            // derived boat instead keeps the same player and walks back for a run-up.
            if(prepareStart)game.PlacePlayer(runStart);else Travel(runStart);
            Idle(3);Require(game.player.grounded,"Departure run-up must begin on the actual pier.");
            Vector3 landing=new Vector3(boat.HullBounds.center.x,boat.HullBounds.max.y,boat.HullBounds.min.z+.30f);
            Look(new Vector3(landing.x,game.view.transform.position.y,landing.z));
            int deaths=game.respawns;
            for(int i=0;i<120&&game.player.feet.z<55.72f;i++)
            {Step(Vector2.up);Require(game.respawns==deaths,"Native departure run-up caused a recovery.");}
            Require(game.player.grounded&&game.player.feet.z<56.05f,"Running boarding jump must leave the actual pier edge.");
            Travel(landing,true);Travel(BoardingMark());Idle(12);
            Require(boat.IsAboard&&game.FootSafe(game.player.feet)&&game.respawns==deaths,"Native boarding must reach the genuinely shaded boat deck without recovery.");
            Detail("Native running jump from departure pier and walk to shaded boarding mark="+game.player.feet+" boat="+boat.transform.position+" scale="+boat.transform.localScale);
        }
        static void CheckBuoyancyAndShade()
        {
            FloatFixture();Vector3 position=boat.transform.position;Idle(180);
            Require(new Vector2(position.x-boat.transform.position.x,position.z-boat.transform.position.z).magnitude<.025f&&!boat.Departed,"Visible launch mooring must prevent drift during preparation.");
            Require(!boat.TryLaunch(),"An unoccupied boat must not depart on a shore-side launch request.");
            Board();var cloth=boat.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Integrated cream shade canopy");
            Require(game.FootSafe(game.player.feet),"The boat deck must be protected by actual geometry.");
            cloth.enabled=false;game.lighting.Synchronize();
            try{Require(!game.FootSafe(game.player.feet),"Without the visible canopy fabric this deck must lose actual sun protection.");}
            finally{cloth.enabled=true;game.lighting.Synchronize();}
            Require(game.FootSafe(game.player.feet),"Restored visible fabric must restore the same real protective shadow.");
            Capture("BoatBoardingAndRealShade",new Vector3(-4,4.8f,55.5f),boat.transform.position+Vector3.up*1.5f);
        }
        static void CheckRide()
        {
            FloatFixture();Board();RideAndLand("");
        }
        static void CheckRotatedArrival(float scale,float yaw)
        {
            ReleasedAboardFixture(scale,yaw);
            int deaths=game.respawns;Require(boat.TryLaunch(),"The actual shaded aboard rider must be allowed to launch.");
            for(int i=0;i<1800&&!boat.Arrived;i++)
            {
                Step();Require(game.respawns==deaths,"Rotated actual ride unexpectedly caused a hazard recovery.");
            }
            Detail("Actual ride ends at boat="+boat.transform.position+" hullBow="+boat.HullBounds.max.z+" berth="+boat.AtBerth+" aboard="+boat.IsAboard+" safe="+game.FootSafe(game.player.feet)+" gate="+game.waterGate.unlocked+" travel="+boat.Travelled);
            Capture("RotatedArrival-"+scale+"-"+yaw,new Vector3(-7,7,65),new Vector3(0,2,68));
            Require(boat.Arrived&&boat.AtBerth&&game.waterGate.unlocked,"An occupied physically arrived rotated boat must open the next-zone gate; boat="+boat.transform.position+" bow="+boat.HullBounds.max.z);
            Travel(new Vector3(0,.24f,68.8f));Travel(new Vector3(0,.24f,69.8f));
            Require(!boat.IsAboard&&game.player.grounded&&game.safe&&game.respawns==deaths,"The rider must natively step from the actual rotated boat onto the real safe pier.");
        }
        static void ReleasedAboardFixture(float scale,float yaw,float laneX=0)
        {
            // This regression isolates arrival after a legal launch. It declares
            // a rotated/enlarged released boat and initial rider placement; the
            // subsequent current, contacts, actual shade and landing are native.
            Require(boat.Prop.maxScale<=1.6f&&scale<=boat.Prop.maxScale,"Rotated fixtures must exercise the real navigable scale range up to the production maximum.");
            boat.Prop.Restore();boat.transform.SetPositionAndRotation(new Vector3(laneX,.08f,59.8f),Quaternion.Euler(0,yaw,0));boat.transform.localScale=Vector3.one*scale;
            boat.Prop.body.position=boat.transform.position;boat.Prop.body.rotation=boat.transform.rotation;Physics.SyncTransforms();boat.Prop.Release();Idle(240);
            Require(boat.Prop.placement.Ready&&Beach.BeachWater.Supports(boat.Prop),"Rotated launch fixture must genuinely settle afloat.");
            Require(game.ValidPlacement(boat.Prop,out var reason),"Rotated launch fixture overlaps physical structures: "+reason);
            bool shaded=false;Vector3 rider=default;
            for(int z=0;z<16&&!shaded;z++)for(int x=0;x<11&&!shaded;x++)
            {
                Vector3 feet=boat.transform.TransformPoint(new Vector3(-.5f+x*.1f,.12f,-.9f+z*.1f));
                if(!Physics.Raycast(feet+Vector3.up*.10f,Vector3.down,out var support,.20f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                    ||support.collider.GetComponentInParent<Beach.BeachProp>()!=boat.Prop||!game.FootSafe(feet+Vector3.up*.025f))continue;
                rider=feet;shaded=true;
            }
            Require(shaded,"Rotated launch fixture must have an actually shaded walkable deck position.");
            game.PlacePlayer(rider);Idle(12);
            Require(boat.IsAboard&&game.FootSafe(game.player.feet),"Declared initial rider must settle grounded in the real canopy shadow.");
            Detail("Declared rotated/enlarged release and initial aboard fixture: scale="+scale+" yaw="+yaw+" rider="+game.player.feet+" boat="+boat.transform.position);
        }
        static void CheckEarlyDisembark()
        {
            FloatFixture();Board();int deaths=game.respawns;Require(boat.TryLaunch(),"Early-disembark fixture could not launch.");
            for(int i=0;i<1800&&boat.HullBounds.max.z<67.6f;i++)
            {Step();Require(game.respawns==deaths,"The early-disembark approach unexpectedly caused a recovery.");}
            Require(boat.HullBounds.max.z>=67.6f,"Actual current must bring the boat within a native jump of the real arrival pier.");
            Detail("Native disembark starts while boat bow="+boat.HullBounds.max.z+" arrived="+boat.Arrived+" player="+game.player.feet);
            Travel(new Vector3(0,.24f,68.3f),true);Travel(new Vector3(0,.24f,69.8f));Idle(90);
            Require(!boat.IsAboard&&game.player.grounded&&game.safe&&game.respawns==deaths,"Native early jump must land safely on the real arrival pier without a recovery.");
            Require(boat.Arrived&&game.waterGate.unlocked,"A native rider who disembarks onto the arrival pier must not leave the gate permanently locked.");
            Capture("EarlyDisembarkArrival",new Vector3(-7,6,65),new Vector3(0,1,70));
            Detail("Actual native early-disembark landing="+game.player.feet+" gate="+game.waterGate.unlocked+" boatBow="+boat.HullBounds.max.z);
        }
        static void CheckUnlaunchedArrival()
        {
            // A deliberately placed arrival-side boat is not evidence that the
            // player sailed it. Retain native buoyancy and actual pier support.
            boat.Prop.Restore();boat.transform.SetPositionAndRotation(new Vector3(0,.08f,66.05f),Quaternion.identity);boat.transform.localScale=Vector3.one*1.6f;
            boat.Prop.body.position=boat.transform.position;boat.Prop.body.rotation=boat.transform.rotation;Physics.SyncTransforms();boat.Prop.Release();
            game.PlacePlayer(new Vector3(0,.24f,69.5f));Idle(300);
            Require(game.player.grounded&&game.safe&&!boat.IsAboard,"Negative arrival fixture must have a real safe grounded pier-side player.");
            Require(!boat.Departed&&!boat.Arrived&&!game.waterGate.unlocked,"An unlaunched boat and a pier-side player must not unlock the crossing.");
            Detail("Declared unlaunched arrival-side placement; native float="+Beach.BeachWater.Supports(boat.Prop)+" player="+game.player.feet+" boatBow="+boat.HullBounds.max.z);
        }
        static void CheckEmptyArrival()
        {
            FloatFixture();Board();int deaths=game.respawns;Require(boat.TryLaunch(),"Empty arrival fixture must actually launch with a shaded rider first.");
            // The player actually walks to the aft deck and jumps back to the
            // departure pier; the departed boat then travels without a rider.
            Travel(boat.transform.TransformPoint(new Vector3(0,.12f,-.40f)));
            Travel(new Vector3(0,.24f,55.7f),true);Idle(30);
            Require(!boat.IsAboard&&game.player.grounded&&game.safe&&game.respawns==deaths,"Player must natively return to the actual departure pier before the empty ride.");
            for(int i=0;i<1800&&!boat.AtBerth;i++)
            {Step();Require(game.respawns==deaths,"The remote departure-pier player unexpectedly failed while the boat travelled.");}
            Require(boat.AtBerth&&boat.Departed&&boat.Travelled>4,"The empty negative probe must genuinely sail to the arrival berth.");
            Require(game.player.feet.z<56.1f&&!boat.IsAboard&&!boat.Arrived&&!game.waterGate.unlocked,"A boat arriving without its remote departure-pier player must not unlock the gate.");
            Detail("Real rider launched, natively jumped back to departure pier, and let the boat sail empty; player="+game.player.feet+" boat="+boat.transform.position+" travelled="+boat.Travelled);
        }
        static void CheckSideLanding()
        {
            ReleasedAboardFixture(1.6f,0,4.2f);
            int deaths=game.respawns;Require(boat.TryLaunch(),"A shaded rider in the permitted side launch lane must actually launch.");
            bool stopped=false;
            for(int i=0;i<1800;i++)
            {
                Step();Require(game.respawns==deaths,"Actual side-lane ride unexpectedly caused a hazard recovery.");
                if(boat.HullBounds.max.z>68.8f&&Mathf.Abs(boat.Prop.body.linearVelocity.z)<.1f){stopped=true;break;}
            }
            Require(stopped&&boat.Travelled>4&&Mathf.Abs(boat.transform.position.x-4.2f)<.1f,"The actual current must carry the side-lane boat into native contact with the solid arrival bank.");
            Require(!boat.Arrived&&!game.waterGate.unlocked,"A side-lane boat that misses the pier must still wait for a real player landing.");
            Detail("Actual side-lane bank contact: boat="+boat.transform.position+" bow="+boat.HullBounds.max.z+" player="+game.player.feet+" travel="+boat.Travelled);
            Travel(boat.transform.TransformPoint(new Vector3(-.48f,.12f,.55f)));
            Require(boat.IsAboard&&game.FootSafe(game.player.feet),"Actual port-bow run-up must remain aboard and protected by the visible canopy.");
            Travel(new Vector3(1.8f,.24f,68.8f),true);Idle(6);
            Require(!boat.IsAboard&&boat.IsOnArrivalLanding&&game.player.grounded&&game.safe&&game.respawns==deaths,"Actual diagonal jump must ground the rider safely on the real arrival pier.");
            Require(boat.AtBerth&&boat.Arrived&&game.waterGate.unlocked,"A real pier landing from the nearby side-lane boat must unlock the gate.");
            Capture("SideLaneNativeLanding",new Vector3(10,6,66),new Vector3(2,1,69));
            Detail("Native diagonal landing="+game.player.feet+" gate="+game.waterGate.unlocked+" sideBoat="+boat.transform.position);
        }
        static void RideAndLand(string prefix)
        {
            Vector3 start=game.player.feet,local=boat.transform.InverseTransformPoint(start);int deaths=game.respawns;
            Require(boat.TryLaunch()&&boat.Departed,"Actual aboard launch request did not release the mooring.");
            bool midImage=false;
            for(int i=0;i<1800&&!boat.Arrived;i++)
            {
                Step();Require(game.respawns==deaths,"Passive ride caused an exposure, water or fall respawn.");Require(boat.IsAboard,"Passive rider lost native deck support.");
                if(!midImage&&boat.Travelled>3)
                {
                    Capture(prefix+"NativeShadedBoatRide",new Vector3(-5,5,62),boat.transform.position+Vector3.up*1.2f);
                    Capture(prefix+"BoatRidePlayerEye",game.view.transform.position,new Vector3(0,2,70));
                    Capture(prefix+"BoatRideDeckPlayerEye",game.view.transform.position,boat.transform.TransformPoint(new Vector3(0,.13f,.95f)));
                    Detail("Actual aboard observer camera="+game.view.transform.position+" nativeAboard="+boat.IsAboard+" nativeSafe="+game.safe);
                    midImage=true;
                }
            }
            Require(boat.Arrived&&boat.AtBerth&&game.waterGate.unlocked,"Actual shaded player arrival must open the next-zone gate.");
            Require(Vector3.Distance(local,boat.transform.InverseTransformPoint(game.player.feet))<.25f,"Rider must retain their deck position through real support velocity.");
            Require(game.player.feet.z-start.z>2&&boat.Travelled>2,"Arrival must include real current-driven travel, not camera placement or cargo delivery.");
            Travel(new Vector3(0,.24f,68.8f));Travel(new Vector3(0,.24f,69.8f));
            Require(!boat.IsAboard&&game.player.grounded&&game.safe&&game.respawns==deaths,"Player must natively disembark onto the actual safe arrival pier.");
            Capture(prefix+"BoatArrivalAndLanding",new Vector3(-7,6,65),new Vector3(0,1,70));
            Travel(new Vector3(0,0,71));Travel(new Vector3(0,0,74.3f));
            Require(game.checkpoint==3&&game.player.grounded&&game.safe&&game.respawns==deaths,"Native landing and gate passage must save the actual castle-zone entrance checkpoint.");
            Detail("Passive current ride travelled="+boat.Travelled+"; native landing and castle entrance="+game.player.feet+"; no hazard recovery.");
        }
        static void CheckRecovery()
        {
            FloatFixture();Board();Require(boat.TryLaunch(),"Recovery fixture could not launch.");Idle(180);
            int deaths=game.respawns;float size=boat.transform.localScale.x;
            for(int i=0;i<180&&game.respawns==deaths;i++)Step(Vector2.right);
            Require(game.respawns==deaths+1,"Walking off the moving boat must preserve the native water/heat failure.");Idle(240);
            Require(!boat.Departed&&!boat.Arrived&&Mathf.Abs(boat.transform.position.z-58.2f)<.05f&&Mathf.Abs(boat.transform.localScale.x-size)<.001f,
                "Uncompleted boat must return to its launch mooring at the same enlarged scale after a real failure.");
            Require(boat.Prop.placement.Ready&&Beach.BeachWater.Supports(boat.Prop)&&!game.waterGate.unlocked,"Recovered launch boat must settle for retry without granting arrival.");
            Detail("Native failure recovered enlarged boat at "+boat.transform.position+" scale="+size);
            Board();Require(boat.TryLaunch(),"Recovered boat must permit a second launch.");
            for(int i=0;i<1800&&!boat.Arrived;i++){Step();Require(game.respawns==deaths+1,"Second native ride unexpectedly failed.");}
            Require(boat.Arrived&&game.waterGate.unlocked&&game.checkpoint==2,"Arrival-before-disembark recovery fixture must genuinely arrive before the next checkpoint is saved.");
            int arrivedDeaths=game.respawns;
            for(int i=0;i<180&&game.respawns==arrivedDeaths;i++)Step(Vector2.right);
            Require(game.respawns==arrivedDeaths+1,"Walking off the arrived boat must trigger the native water danger.");Idle(240);
            Require(!boat.Departed&&!boat.Arrived&&!game.waterGate.unlocked&&Mathf.Abs(boat.transform.position.z-58.2f)<.05f&&Mathf.Abs(boat.transform.localScale.x-size)<.001f,
                "A player who falls before saving the castle entrance must recover the same enlarged boat and close the premature arrival gate.");
            Require(boat.Prop.placement.Ready&&Beach.BeachWater.Supports(boat.Prop),"Arrival-recovered boat must be genuinely afloat and ready for another attempt.");
            Detail("Native post-arrival/pre-checkpoint failure also returns the enlarged boat and rolls back the gate.");
        }
        static void CheckNoCargoBypass()
        {
            var calm=waters.Single(w=>w.name=="판자 횡단 수로");Require(calm.flow==Vector3.zero,"Earlier diagonal bridge water must remain calm.");
            foreach(float z in new[]{56.5f,60,64,68})
                Require(!Physics.Raycast(new Vector3(8.8f,.5f,z),Vector3.down,1,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore),"The old right-hand dry bypass still exists at z="+z);
            var ball=game.props.First(p=>p.isBall);
            ball.Restore();ball.transform.SetPositionAndRotation(new Vector3(0,.10f,56.8f),Quaternion.identity);ball.transform.localScale=Vector3.one*1.2f;
            ball.body.position=ball.transform.position;ball.body.rotation=ball.transform.rotation;Physics.SyncTransforms();ball.Release();
            float start=ball.transform.position.z;Idle(900);
            Require(ball.transform.position.z-start>6.5f,"Ordinary cargo probe must actually drift far enough to test the removed arrival shortcut.");
            Require(!game.waterGate.unlocked&&!boat.Arrived,"Floating ordinary cargo must not substitute for a player boat crossing.");
            Detail("Declared physical ordinary-ball probe (not a legal cross-zone placement claim), drift="+(ball.transform.position.z-start));
        }
        static void Travel(Vector3 target,bool jump=false,int limit=360)
        {
            Vector3 start=game.player.feet;int deaths=game.respawns;bool launched=false;int hold=0;
            for(int i=0;i<limit;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.16f&&Mathf.Abs(target.y-game.player.feet.y)<.20f&&game.player.grounded&&(!jump||launched&&i>15))return;
                if(delta.magnitude>.04f)game.yaw=Mathf.Atan2(delta.x,delta.y);
                bool pressed=jump&&!launched;if(pressed){Require(game.player.grounded,"Native boarding jump must begin grounded.");launched=true;}
                Vector2 input=delta.magnitude<.04f?Vector2.zero:delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.6f));
                Step(input,pressed,launched&&hold++<7);Require(game.respawns==deaths,"Native movement from "+start+" to "+target+" caused a hazard recovery; actual="+game.player.feet);
            }
            throw new InvalidOperationException("Native input did not reach "+target+" from "+start+"; actual="+game.player.feet+" grounded="+game.player.grounded+" safe="+game.safe);
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var host=new GameObject("Boat validation camera "+name);var camera=host.AddComponent<Camera>();camera.CopyFrom(game.view);camera.enabled=false;
            var texture=new RenderTexture(1600,900,24);Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                string path=Path.Combine(output,name+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());report.images.Add(path);
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(host);}
        }
        static void CaptureLog(string message,string stack,LogType kind)
        {
            string phase=SessionState.GetString(Key+"Phase","");if(phase!="pending"&&phase!="running")return;
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){string text=message+"\n"+stack;if(report==null)bootErrors.Add(text);else report.errors.Add(text);}
        }
        static void Finish(string failure)
        {
            if(finished)return;finished=true;output=output??SessionState.GetString(Key+"Output",Path.GetFullPath("Validation/BeachBoat"));
            if(report==null)report=new Report{scene=ScenePath,unity=Application.unityVersion,sceneHashBefore=SessionState.GetString(Key+"Hash","")};report.failure=failure;
            if(simulationConfigured)Physics.simulationMode=previousSimulation;
            try
            {
                report.sceneHashAfter=Hash(SceneFile());report.sceneUnchanged=report.sceneHashBefore==report.sceneHashAfter;
                int expected=Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-all")>=0?15:Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-arrival-only")>=0?10:Array.IndexOf(Environment.GetCommandLineArgs(),"-boat-mini-only")>=0?1:5;
                report.passed=failure==null&&report.checks.Count==expected&&report.checks.All(c=>c.passed)&&report.errors.Count==0&&report.sceneUnchanged;
                if(!outputWritable&&!SessionState.GetBool(Key+"Writable",false))throw new InvalidOperationException("Fresh validation output was not established.");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"boat-results.json"),JsonUtility.ToJson(report,true));
                Debug.Log("BEACH_BOAT_FINISHED passed="+report.passed+" checks="+report.checks.Count+" errors="+report.errors.Count+" sceneUnchanged="+report.sceneUnchanged);
            }
            catch(Exception error){report.passed=false;Debug.LogError("BEACH_BOAT_REPORT_FAILED "+error);}
            SessionState.SetString(Key+"Phase","");SessionState.SetBool(Key+"Writable",false);EditorApplication.delayCall+=()=>EditorApplication.Exit(report.passed?0:1);
        }
        static string SceneFile()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",ScenePath));
        static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static string Argument(string name){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:null;}
    }
}
