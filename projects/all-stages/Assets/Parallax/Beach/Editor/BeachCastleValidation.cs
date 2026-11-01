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
    // Isolated GPU Editor, without -quit or -nographics:
    // -executeMethod ShadeLink.Editor.BeachCastleValidation.Run -shade-output <fresh folder>
    [InitializeOnLoad]
    public static class BeachCastleValidation
    {
        const string ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity",Key="Parallax.BeachCastleValidation.";
        const float Dt=1f/60,ParasolGripHeight=2.15f;
        static readonly Vector3 Start=new Vector3(0,1.1f,89),Mid=new Vector3(0,3.44f,91.7f),Summit=new Vector3(0,6.7f,95.4f);
        static readonly Vector3 LowerPose=new Vector3(0,2.27f,90.5f),UpperPose=new Vector3(0,5.07f,93.55f);
        static readonly System.Reflection.FieldInfo JumpHeld=typeof(Beach.BeachGame).GetField("gameJumpHeld",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        static Beach.BeachGame game;
        static Beach.BeachCastleRestoration route;
        static Beach.BeachProp fragment;
        static Beach.BeachWater[] waters;
        static PropReleasePhysics[] releasedBodies;
        static Report report;
        static Check current;
        static IEnumerator sequence;
        static string output;
        static bool started,finished,simulationConfigured,outputWritable;
        static SimulationMode previousSimulation;
        static int frame;
        static readonly List<string> bootErrors=new List<string>(),trace=new List<string>();
        static readonly List<string> contacts=new List<string>();
        static Physics.ContactEventDelegate contactHandler;
        static readonly Dictionary<BoxCollider,bool> originalContactFlags=new Dictionary<BoxCollider,bool>();

        [Serializable] public sealed class Check
        { public string name,failure;public bool passed;public List<string> details=new List<string>(); }
        [Serializable] public sealed class Report
        {
            public bool passed,sceneUnchanged;
            public string scope,scene,unity,gpu,failure,sceneHashBefore,sceneHashAfter;
            public List<Check> checks=new List<Check>();
            public List<string> images=new List<string>(),errors=new List<string>();
        }
        static BeachCastleValidation(){EditorApplication.update+=Update;Application.logMessageReceived+=CaptureLog;}
        public static void Run()
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start from EditMode in an isolated project.");
                output=Path.GetFullPath(Argument("-shade-output")??"Validation/BeachCastle");
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
            if(double.TryParse(SessionState.GetString(Key+"Start","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out var began)&&EditorApplication.timeSinceStartup-began>480)
            {Finish("Validation exceeded the 480-second watchdog.");return;}
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!started)
                {
                    started=true;output=SessionState.GetString(Key+"Output","");
                    report=new Report{scene=ScenePath,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,sceneHashBefore=SessionState.GetString(Key+"Hash",""),
                        scope="Saved Beach scene in real GPU PlayMode with native controller, Script physics, release-CCD clock and actual mesh shadows. Earlier stages and a local starting landing are declared fixtures. The canonical continuous solve uses actual home-camera pickup, perspective resizing and native release for both separate stairs and the movable parasol, without held/player transform or size fixtures after its start. Practical traversal variants declare physical prop pose fixtures, release and settle them natively, then use actual motor inputs and legal jumps. Isolated summit-region/contact-rejection probes explicitly declare their physical-source and standing setup fixtures. No route proof flags, Ready state, safety or heat values are assigned. Flat-deck, real shade, missing/incorrect stairs, checkpoint recovery and reset remain separately checked. Camera renders omit IMGUI."};
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
            route=game.castleRoute;Require(route!=null,"Saved front castle restoration route missing.");fragment=route.LowerFragment;
            Require(route.LowerFragment!=null&&route.UpperFragment!=null&&route.Parasol!=null,"Both separate front fragments and the movable parasol are required.");
            Require(JumpHeld!=null,"Native held-jump input unavailable.");
            waters=UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None);
            releasedBodies=UnityEngine.Object.FindObjectsByType<PropReleasePhysics>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationConfigured=true;ConfigureContacts();
            trace.Add("frame,check,inputX,inputZ,jump,jumpHeld,playerX,playerY,playerZ,activeProp,propX,propY,propZ,scale,held,ready,safe,exposure,respawns,lowerConnected,first,mid,upperConnected,second,won,yaw,pitch");
            bool quick=Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-solve-only")>=0;
            bool practical=Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-practical-only")>=0;
            bool variants=Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-variants-only")>=0;
            bool summit=Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-summit-only")>=0;
            if(practical||variants||summit)
            {
                if(practical||variants){BeginCheck("Native shorter, offset and rotated released stair placements reach the summit");yield return null;yield return null;RunCheck(CheckPracticalStairs);yield return null;}
                if(practical||summit){BeginCheck("Actual cap corners and original crown beneath the flag all accept valid arrival");yield return null;yield return null;RunCheck(CheckSummitFootprint);yield return null;}
                if(practical){BeginCheck("Bare summit, held source and home-only or kinematic contact cannot grant completion");yield return null;yield return null;RunCheck(CheckContactEligibility);yield return null;}
                yield break;
            }
            if(!quick){BeginCheck("Native front approach reaches the fixed lower parasol landing");yield return null;yield return null;RunCheck(CheckApproach);yield return null;}
            BeginCheck("Continuous native first stair, movable parasol, separate upper stair and summit victory");yield return null;yield return null;RunCheck(CheckNativeSolve);yield return null;
            if(quick)yield break;
            BeginCheck("Each missing front stair physically blocks native jump access");yield return null;yield return null;RunCheck(CheckMissingStairs);yield return null;
            BeginCheck("Wrong fragment size and connection height cannot complete the route");yield return null;yield return null;RunCheck(CheckWrongPlacement);yield return null;
            BeginCheck("Upper work and ascent require the placed parasol actual mesh shadow");yield return null;yield return null;RunCheck(CheckShadeDependency);yield return null;
            BeginCheck("Manual parasol settles on a wide physically flat placement deck");yield return null;yield return null;RunCheck(CheckFlatDeck);yield return null;
            BeginCheck("Checkpoint failure recovers reachable separate sources and zone reset restores all three");yield return null;yield return null;RunCheck(CheckRecoveryAndReset);yield return null;
            BeginCheck("Native shorter, offset and rotated released stair placements reach the summit");yield return null;yield return null;RunCheck(CheckPracticalStairs);yield return null;
            BeginCheck("Actual cap corners and original crown beneath the flag all accept valid arrival");yield return null;yield return null;RunCheck(CheckSummitFootprint);yield return null;
            BeginCheck("Bare summit, held source and home-only or kinematic contact cannot grant completion");yield return null;yield return null;RunCheck(CheckContactEligibility);yield return null;
        }
        static void BeginCheck(string name){current=new Check{name=name};report.checks.Add(current);ResetFixture();}
        static void RunCheck(Action action)
        {
            try{action();current.passed=true;Debug.Log("BEACH_CASTLE_PASS "+current.name);}
            catch(Exception error)
            {
                current.failure=error.ToString();Debug.Log("BEACH_CASTLE_FAIL "+current.name+": "+error.Message);
                try{Capture("Failure-"+report.checks.Count,game.view.transform.position,game.view.transform.position+game.view.transform.forward);}catch(Exception capture){current.failure+="\nCapture: "+capture;}
            }
            finally{File.WriteAllLines(Path.Combine(output,"castle-trace.csv"),trace);current=null;}
        }
        static void ResetFixture()
        {
            game.ResetAll();game.automated=true;game.Resume();game.lightGate.unlocked=true;game.waterGate.unlocked=true;game.checkpoint=3;fragment=route.LowerFragment;
            game.PlacePlayer(Start);JumpHeld.SetValue(game,false);frame=0;contacts.Clear();Physics.SyncTransforms();game.lighting.Synchronize();Idle(4);
            Detail("Declared earlier-stage completion and lower front landing fixture. Native sources keep their homes: A="+route.LowerFragment.homePosition+" scale="+route.LowerFragment.homeScale+" B="+route.UpperFragment.homePosition+" scale="+route.UpperFragment.homeScale+" parasol="+route.Parasol.homePosition+" scale="+route.Parasol.homeScale);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Detail(string message){current.details.Add(message);}
        static void Step(Vector2 move=default,bool jump=false,bool held=false)
        {
            JumpHeld.SetValue(game,held);game.Tick(Dt,move,jump);
            foreach(var release in releasedBodies)if(release!=null&&release.isActiveAndEnabled)release.Tick(Dt);
            foreach(var water in waters)water.Simulate(Dt);Physics.Simulate(Dt);Physics.SyncTransforms();game.lighting.Synchronize();frame++;
            if(frame%10==0||jump)trace.Add(string.Format(CultureInfo.InvariantCulture,"{0},\"{1}\",{2:F4},{3:F4},{4},{5},{6:F4},{7:F4},{8:F4},\"{9}\",{10:F4},{11:F4},{12:F4},{13:F4},{14},{15},{16},{17:F4},{18},{19},{20},{21},{22},{23},{24},{25:F4},{26:F4}",
                frame,current.name,move.x,move.y,jump,held,game.player.feet.x,game.player.feet.y,game.player.feet.z,fragment.title,fragment.transform.position.x,fragment.transform.position.y,fragment.transform.position.z,fragment.transform.localScale.x,game.held==fragment,fragment.placement.Ready,game.safe,game.exposure,game.respawns,route.LowerConnected,route.FirstRestored,route.AtMidpoint,route.UpperConnected,route.SecondRestored,game.won,game.yaw,game.pitch));
        }
        static void Idle(int count){for(int i=0;i<count;i++)Step();}
        static void Look(Vector3 target)
        {
            Vector3 delta=target-game.view.transform.position;game.yaw=Mathf.Atan2(delta.x,delta.z);game.pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude);game.SyncView();
        }
        static Beach.BeachProp[] RouteProps=>new[]{route.LowerFragment,route.UpperFragment,route.Parasol};
        static void ConfigureContacts()
        {
            foreach(var prop in RouteProps)foreach(var part in prop.parts){originalContactFlags[part]=part.providesContacts;part.providesContacts=true;}
            contactHandler=(scene,headers)=>
            {
                if(fragment==null||current==null)return;
                for(int h=0;h<headers.Length;h++)
                {
                    var header=headers[h];
                    for(int p=0;p<header.pairCount;p++)
                    {
                        var pair=header.GetContactPair(p);var a=pair.collider;var b=pair.otherCollider;
                        if(a==null||b==null||(a.attachedRigidbody!=fragment.body&&b.attachedRigidbody!=fragment.body))continue;
                        for(int c=0;c<pair.contactCount;c++)
                        {
                            var point=pair.GetContactPoint(c);contacts.Add("frame="+frame+" a="+a.name+" b="+b.name+" point="+point.position+" normal="+point.normal+" separation="+point.separation);
                            if(contacts.Count>24)contacts.RemoveAt(0);
                        }
                    }
                }
            };
            Physics.ContactEvent+=contactHandler;
        }
        static void Snapshot(string label)
        {Detail(label+": prop="+fragment.title+" feet="+game.player.feet+" grounded="+game.player.grounded+" safe="+game.safe+" root="+fragment.transform.position+" scale="+fragment.transform.localScale+" euler="+fragment.transform.eulerAngles+" bounds="+fragment.WorldBounds+" Ready="+fragment.placement.Ready+" Supported="+fragment.placement.Supported+" held="+(game.held==fragment)+" refDistance="+game.referenceDistance+" refScale="+game.referenceScale+" blocked="+game.PlacementBlocked+" reason="+game.invalidReason+" velocity="+fragment.body.linearVelocity+" sleeping="+fragment.body.IsSleeping());}
        static void SupportDiagnostics()
        {
            foreach(var contact in contacts)Detail("Actual PhysX contact: "+contact);
            foreach(var shape in fragment.parts)
            {
                if(!shape.enabled||shape.isTrigger)continue;var bounds=shape.bounds;
                for(int i=0;i<5;i++)
                {
                    var below=new Vector3(bounds.center.x,bounds.min.y-.08f,bounds.center.z);
                    if(i>0){below.x+=(i==1||i==3?-1:1)*bounds.extents.x*.9f;below.z+=(i<3?-1:1)*bounds.extents.z*.9f;}
                    var point=shape.ClosestPoint(below);
                    var hits=Physics.RaycastAll(point+Vector3.up*.025f,Vector3.down,4,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                        .Where(hit=>hit.collider!=game.controller&&hit.collider.attachedRigidbody!=fragment.body).OrderBy(hit=>hit.distance).ToArray();
                    Detail("Support sample part="+shape.name+" sample="+i+" closest="+point+" below="+(hits.Length==0?"none":string.Join(" | ",hits.Take(2).Select(hit=>hit.collider.name+" distance="+hit.distance.ToString("F5",CultureInfo.InvariantCulture)+" point="+hit.point+" normal="+hit.normal))));
                }
            }
        }
        static bool OwnStairBelow(Beach.BeachProp prop)
        {
            var floors=Physics.RaycastAll(game.player.feet+Vector3.up*.15f,Vector3.down,.65f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider!=game.controller&&hit.normal.y>.5f&&PlacementSupport.IsUsableGround(hit.collider)).OrderBy(hit=>hit.distance).ToArray();
            return floors.Length>0&&floors[0].collider.GetComponentInParent<Beach.BeachProp>()==prop;
        }
        static void Travel(Vector3 target,bool jump=false,int limit=420,bool allowWin=false,float tolerance=.13f,float heightTolerance=.23f,Beach.BeachProp requiredStair=null)
        {
            Require(heightTolerance<=.23f||requiredStair!=null,"Relaxed native arrival height requires actual owned-stair support.");
            Vector3 start=game.player.feet;int deaths=game.respawns;bool launched=false;int hold=0;
            for(int i=0;i<limit;i++)
            {
                if(allowWin&&game.won)return;
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<tolerance&&Mathf.Abs(target.y-game.player.feet.y)<heightTolerance&&game.player.grounded&&
                    (requiredStair==null||OwnStairBelow(requiredStair))&&(!jump||launched&&i>15))
                {
                    if(requiredStair!=null)Detail("Native actual stair arrival target="+target+" feet="+game.player.feet+" grounded="+game.player.grounded+" actualOwnedSupport=True");
                    return;
                }
                if(delta.magnitude>.008f)game.yaw=Mathf.Atan2(delta.x,delta.y);
                bool pressed=jump&&!launched;if(pressed){Require(game.player.grounded,"Native jump must begin grounded.");launched=true;}
                Vector2 input=delta.magnitude<.008f?Vector2.zero:delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.6f));
                Step(input,pressed,launched&&hold++<7);Require(game.respawns==deaths,"Native movement "+start+" -> "+target+" caused recovery at "+game.player.feet);
            }
            throw new InvalidOperationException("Native inputs did not reach "+target+" from "+start+"; actual="+game.player.feet+" grounded="+game.player.grounded+" safe="+game.safe);
        }
        static void GrabActual(Beach.BeachProp prop,string label)
        {
            fragment=prop;Look(prop.transform.position);game.UpdateAim();
            Require(game.aimed==prop,label+" camera selected "+(game.aimed==null?"none":game.aimed.title));
            game.Interact();Require(game.held==prop,label+" actual pickup failed.");Snapshot(label);
        }
        static void RotateForward(){game.RotateHeld(Vector3.SignedAngle(fragment.transform.forward,Vector3.forward,Vector3.up),0);Idle(2);}
        static void CheckApproach()
        {
            Require(route.ApproachRoute!=null&&route.ApproachRoute.Length>1,"ApproachRoute must include the beach-side start and native stairs.");
            game.PlacePlayer(route.ApproachRoute[0]);Idle(4);Detail("Declared local beach-side approach starting fixture="+route.ApproachRoute[0]);
            foreach(var point in route.ApproachRoute.Skip(1)){Detail("Native approach segment from="+game.player.feet+" target="+point+" safe="+game.safe+" exposure="+game.exposure);Travel(point);}
            Travel(Start);Require(game.player.grounded&&game.safe,"Front lower parasol landing is not actually shaded and grounded.");
            Capture("FrontCastleApproachPlayerEye",game.view.transform.position,LowerPose);Snapshot("Actual front approach");
        }
        static void CheckNativeSolve()
        {
            Require(route.CastleRoot!=null&&game.props.Count(p=>p.zone==3)==3,"Final chapter must retain the full castle and exactly two separate fragments plus one parasol.");
            Require(route.LowerFragment!=route.UpperFragment&&route.UpperFragment!=route.Parasol,"Front sources must be separate instances.");
            Require(route.LowerFragment.homeScale.x<1.3f&&route.UpperFragment.homeScale.x<1,"Front stair sources must begin below their useful enlarged size.");
            Require(!route.FirstRestored&&!route.SecondRestored&&!route.AtMidpoint,"Route starts completed.");
            int lowerId=route.LowerFragment.GetInstanceID(),upperId=route.UpperFragment.GetInstanceID(),deaths=game.respawns;
            Capture("FrontCastleRestorationOverview",new Vector3(13,12,81),new Vector3(0,4,93));
            Capture("FrontMiniaturesAndMissingWall",game.view.transform.position,new Vector3(0,3,92));
            Travel(new Vector3(-.85f,1.1f,89.2f),tolerance:.02f);GrabActual(route.LowerFragment,"Native A source pickup");
            Look(game.view.transform.position+new Vector3(0,-.8f,1));PlaceNativeStair(false);ValidateStair(false);NativeClimb(false);
            Travel(Mid);Idle(6);Require(route.FirstRestored&&route.AtMidpoint,"Actual first stair and landing did not register.");
            var lowerPosition=route.LowerFragment.transform.position;var lowerScale=route.LowerFragment.transform.localScale;
            Capture("FirstFrontStairLandingPlayerEye",game.view.transform.position,route.Parasol.transform.position);
            Travel(new Vector3(.9f,3.44f,91.9f),tolerance:.02f);GrabActual(route.Parasol,"Native movable parasol pickup");
            Look(game.view.transform.position+new Vector3(0,.5f,1));Travel(new Vector3(.2f,3.44f,91.7f),tolerance:.02f);PlaceNativeParasol();ValidateParasolShade(false);
            Capture("ManualParasolOnFlatDeckPlayerEye",game.view.transform.position,route.Parasol.transform.position);
            Travel(new Vector3(-1,3.44f,92.3f),tolerance:.02f);GrabActual(route.UpperFragment,"Native separate B source pickup");
            Look(game.view.transform.position+new Vector3(0,-.8f,-1));PlaceNativeStair(true);ValidateStair(true);ValidateParasolShade(true);
            Require(route.LowerFragment.GetInstanceID()==lowerId&&route.UpperFragment.GetInstanceID()==upperId,"A source was replaced.");
            Require(route.LowerFragment.placement.Ready&&Vector3.Distance(route.LowerFragment.transform.position,lowerPosition)<.03f&&Vector3.Distance(route.LowerFragment.transform.localScale,lowerScale)<.001f,"First stair was removed or reused instead of staying in its opening.");
            Capture("BothFrontSectionsRestoredPlayerEye",game.view.transform.position,Summit);
            NativeClimb(true);Travel(Summit,false,420,true);Require(game.won&&route.SecondRestored,"Native front ascent did not reach actual summit victory.");
            Require(game.respawns==deaths&&game.grabs>=3&&game.drops>=3,"Continuous solve must keep native three-source pickup/release lifecycle without recovery.");
            Snapshot("Actual front castle victory");Capture("FrontCastleSummitPlayerEye",game.view.transform.position,new Vector3(1.9f,10,97));
        }
        static void PlaceNativeStair(bool upper)
        {
            var pose=upper?UpperPose:LowerPose;fragment=upper?route.UpperFragment:route.LowerFragment;RotateForward();
            Vector3[] stands=upper?new[]{new Vector3(0,3.44f,91.7f),new Vector3(0,3.44f,91.6f),new Vector3(.1f,3.44f,91.65f),new Vector3(0,3.44f,91.5f)}:
                new[]{new Vector3(.8f,1.1f,89),new Vector3(.8f,1.1f,88.9f),new Vector3(.65f,1.1f,88.85f),new Vector3(.9f,1.1f,89.15f)};
            bool dropped=false;int deaths=game.respawns;
            foreach(var stand in stands)
            {
                Look(game.view.transform.position+new Vector3(0,-.8f,1));Travel(stand,tolerance:.02f);
                foreach(float dy in new[]{0f,.08f,-.08f,.18f,.35f})
                {
                    foreach(float dz in new[]{0f,.08f,-.08f,.16f,-.16f})
                    {
                        Vector3 target=pose+new Vector3(0,dy,dz);Look(target);Idle(3);var delta=fragment.transform.position-pose;
                        float scale=fragment.transform.localScale.x;bool balanced=Mathf.Abs(delta.x)<.10f&&Mathf.Abs(delta.z)<.10f&&delta.y>=-.12f&&delta.y<.40f&&scale>=1.94f&&scale<=2.151f&&Vector3.Dot(fragment.transform.forward,Vector3.forward)>.985f;
                        bool valid=game.ValidPlacement(fragment,out var reason);
                        Detail((upper?"B":"A")+" actual aim="+target+" stand="+game.player.feet+" preview="+fragment.transform.position+" scale="+scale+" balanced="+balanced+" valid="+valid+" reason="+reason+" blocked="+game.PlacementBlocked);
                        if(balanced&&valid&&game.Drop()){dropped=true;break;}
                        Require(game.respawns==deaths,"Native stair placement left its safe working position.");
                    }
                    if(dropped)break;
                }
                if(dropped)break;
            }
            Require(dropped,"Native camera inputs did not yield a valid enlarged "+(upper?"B":"A")+" stair release.");
            Settle("Native "+(upper?"B":"A")+" stair");
            Require(upper?route.UpperConnected:route.LowerConnected,"Released actual stair does not connect both intended elevations.");
            for(int i=0;i<11;i++)
            {
                float fraction=Mathf.Lerp(.025f,.975f,i/10f);var floor=StairFloor(upper,fraction);
                float expected=Mathf.Lerp(upper?3.44f:1.1f,upper?6.7f:3.44f,fraction);
                Detail("Settled "+(upper?"B":"A")+" actual floor profile fraction="+fraction+" point="+floor.point+" expectedY="+expected);
                Require(Mathf.Abs(floor.point.y-expected)<.17f,"Released stair actual floor does not match connection profile at "+floor.point);
            }
        }
        static void Settle(string label)
        {
            contacts.Clear();Idle(100);Snapshot(label+" released and settled");
            if(!fragment.placement.Ready){SupportDiagnostics();Idle(120);Snapshot(label+" extra native settling");}
            Require(fragment.placement.Ready,label+" failed actual Ready support.");
            Require(game.ValidPlacement(fragment,out var reason),label+" overlaps physical structures: "+reason);
        }
        static RaycastHit StairFloor(bool upper,float fraction)
        {
            var prop=upper?route.UpperFragment:route.LowerFragment;float z=Mathf.Lerp(upper?92.1f:89.5f,upper?95f:91.5f,fraction);
            float expected=Mathf.Lerp(upper?3.44f:1.1f,upper?6.7f:3.44f,fraction);
            var own=Physics.RaycastAll(new Vector3(0,expected+.45f,z),Vector3.down,.9f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider.GetComponentInParent<Beach.BeachProp>()==prop&&hit.normal.y>.8f).OrderBy(hit=>hit.distance).ToArray();
            Require(own.Length>0,"Actual front stair floor missing at fraction="+fraction+" upper="+upper+" expectedY="+expected);return own[0];
        }
        static void ValidateStair(bool upper)
        {
            var prop=upper?route.UpperFragment:route.LowerFragment;Require(prop.placement.Ready&&game.held!=prop,"Stair must be released Ready.");
            for(int i=1;i<=5;i++){var floor=StairFloor(upper,i/6f);Require(game.FootSafe(floor.point+Vector3.up*.025f),"Actual stair is not protected by rendered mesh shade at "+floor.point);}
        }
        static void NativeClimb(bool upper)
        {
            for(int i=1;i<=6;i++){if(upper&&game.won)return;var floor=StairFloor(upper,Mathf.Min(.98f,i/6f));Travel(floor.point,false,420,upper);}
        }
        static float ParasolFootY=>route.Parasol.transform.position.y-ParasolGripHeight*route.Parasol.transform.localScale.x;
        static void PlaceNativeParasol()
        {
            fragment=route.Parasol;bool dropped=false;
            foreach(float requested in new[]{2.9f,3.05f,3.2f,3.5f,4f})
            {
                foreach(float dz in new[]{0f,.25f,-.25f,.5f})
                {
                    Vector3 target=new Vector3(2,3.44f+ParasolGripHeight*requested,92.5f+dz);Look(target);Idle(4);
                    float scale=fragment.transform.localScale.x;var at=fragment.transform.position;
                    bool onDeck=scale>=2.75f&&scale<=3.201f&&Mathf.Abs(at.x-2)<.5f&&Mathf.Abs(at.z-92.5f)<.65f&&ParasolFootY>=3.40f&&ParasolFootY<5.3f;
                    bool valid=game.ValidPlacement(fragment,out var reason);
                    Detail("Manual parasol actual aim="+target+" preview="+at+" scale="+scale+" footY="+ParasolFootY+" onDeck="+onDeck+" valid="+valid+" reason="+reason);
                    if(onDeck&&valid&&game.Drop()){dropped=true;break;}
                }
                if(dropped)break;
            }
            Require(dropped,"Native canopy-centre perspective inputs did not release the enlarged manual parasol over the flat deck.");
            Settle("Manual flat-deck parasol");Require(Mathf.Abs(ParasolFootY-3.44f)<.15f,"Manual parasol did not actually settle on the flat deck.");
        }
        static void ValidateParasolShade(bool onStair)
        {
            Vector3 feet=onStair?StairFloor(true,.5f).point+Vector3.up*.025f:new Vector3(0,3.465f,92.8f);
            Require(game.FootSafe(feet),"Actual manual umbrella does not shade upper working/ascent point "+feet);
            Require(game.lighting.TryGetDirectShadowCaster(feet,Vector3.up,out var owner)&&owner==route.Parasol,"The critical upper shade is not cast by the actual placed movable umbrella.");
            Detail("Actual critical mesh shade owner="+owner.title+" feet="+feet);
        }
        static void FixtureProp(Beach.BeachProp prop,Vector3 at,float scale,float yaw=0)
        {
            fragment=prop;prop.Restore();prop.transform.localScale=Vector3.one*scale;prop.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
            prop.body.position=at;prop.body.rotation=prop.transform.rotation;Physics.SyncTransforms();prop.Release();Idle(110);
            Detail("Declared isolated physical released-pose fixture: "+prop.title+" at="+at+" scale="+scale+" yaw="+yaw+" actual="+prop.transform.position+" Ready="+prop.placement.Ready);
        }
        static void FixtureFirstLanding()
        {
            FixtureProp(route.LowerFragment,LowerPose,2);Require(route.LowerFragment.placement.Ready&&route.LowerConnected,"Prepared lower negative fixture did not connect.");
            game.PlacePlayer(StairFloor(false,.55f).point);Idle(5);game.PlacePlayer(Mid);Idle(6);
            Require(route.FirstRestored&&route.AtMidpoint,"Declared first stair/landing standing fixture did not pass native route detection.");
        }
        static void FixtureParasol(bool useful=true)
        {FixtureProp(route.Parasol,new Vector3(2,3.44f+ParasolGripHeight*(useful?3:.65f),useful?92.5f:91.9f),useful?3:.65f);}
        static void JumpForwardAttempt(int ticks,ref float highest)
        {
            int deaths=game.respawns;
            for(int i=0;i<ticks&&game.respawns==deaths&&!game.won;i++){bool jump=i%45==0&&game.player.grounded;Step(Vector2.up,jump,i%45<7);highest=Mathf.Max(highest,game.player.feet.y);}
        }
        static void CheckMissingStairs()
        {
            route.LowerFragment.gameObject.SetActive(false);float high=game.player.feet.y;
            try{JumpForwardAttempt(160,ref high);Require(!route.FirstRestored&&!game.won&&high<3.35f,"Missing first stair was bypassed by native jumping.");}
            finally{route.LowerFragment.gameObject.SetActive(true);}
            Detail("Missing A native jump highest foot="+high);ResetFixture();FixtureFirstLanding();FixtureParasol();game.PlacePlayer(Mid);Idle(4);
            route.UpperFragment.gameObject.SetActive(false);high=game.player.feet.y;
            try{JumpForwardAttempt(160,ref high);Require(!route.SecondRestored&&!game.won&&high<6.5f,"Missing second stair was bypassed by native jumping.");}
            finally{route.UpperFragment.gameObject.SetActive(true);}
            Detail("Missing B native jump highest foot="+high);
        }
        static void CheckWrongPlacement()
        {
            FixtureProp(route.LowerFragment,LowerPose,.75f);Require(!route.LowerConnected,"A small source falsely connects the first rise.");
            game.PlacePlayer(Mid);Idle(5);Require(!route.FirstRestored,"Small wrong-height A granted restoration.");game.PlacePlayer(Summit);Idle(6);Require(!game.won,"A bare summit bypassed missing stair restoration.");
            ResetFixture();FixtureFirstLanding();FixtureParasol();FixtureProp(route.UpperFragment,UpperPose+Vector3.right*2,2);
            Require(!route.UpperConnected,"A lateral wrong-height B falsely connected the ascent.");game.PlacePlayer(Summit);Idle(6);Require(!route.SecondRestored&&!game.won,"Wrongly connected upper source granted victory.");
        }
        static void CheckShadeDependency()
        {
            FixtureFirstLanding();FixtureParasol();FixtureProp(route.UpperFragment,UpperPose,2);ValidateStair(true);ValidateParasolShade(true);
            var renderers=route.Parasol.renderers;var enabled=renderers.Select(r=>r.enabled).ToArray();Vector3 feet=StairFloor(true,.5f).point+Vector3.up*.025f;
            try
            {
                foreach(var renderer in renderers)renderer.enabled=false;game.lighting.Synchronize();
                Require(!game.FootSafe(feet),"Invisible safety remains after disabling actual manual-parasol meshes.");
                game.PlacePlayer(feet);int deaths=game.respawns;Idle(130);Require(game.respawns>deaths&&!game.won,"No actual umbrella shade did not produce native heat failure.");
            }
            finally{for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];game.lighting.Synchronize();}
            ResetFixture();FixtureFirstLanding();FixtureParasol(false);FixtureProp(route.UpperFragment,UpperPose,2);
            feet=StairFloor(true,.5f).point+Vector3.up*.025f;Require(!game.FootSafe(feet),"Unenlarged wrong manual umbrella already covers the upper task.");
            game.PlacePlayer(feet);int before=game.respawns;Idle(130);Require(game.respawns>before&&!game.won,"Wrong/unenlarged umbrella did not cause native heat failure.");
        }
        static void CheckFlatDeck()
        {
            FixtureFirstLanding();FixtureParasol();Require(route.Parasol.placement.Ready,"Manual umbrella has no physical deck support.");
            for(float x=1.5f;x<=2.81f;x+=.325f)for(float z=91.6f;z<=93.21f;z+=.4f)
            {
                var floors=Physics.RaycastAll(new Vector3(x,3.7f,z),Vector3.down,.5f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                    .Where(hit=>hit.collider.GetComponentInParent<Beach.BeachProp>()==null&&hit.collider!=game.controller).OrderBy(hit=>hit.distance).ToArray();
                Require(floors.Length>0&&Mathf.Abs(floors[0].point.y-3.44f)<.025f&&floors[0].normal.y>.99f,"Manual parasol deck is not truly flat/solid at "+new Vector2(x,z));
            }
            fragment=route.Parasol;Snapshot("Actual right-side flat placement deck");Capture("FlatParasolPlacementDeck",new Vector3(6,8,89),new Vector3(1.8f,3.44f,92.4f));
        }
        static void CheckRecoveryAndReset()
        {
            FixtureFirstLanding();FixtureParasol();game.PlacePlayer(Mid);Idle(4);
            Travel(new Vector3(-1,3.44f,92.3f));GrabActual(route.UpperFragment,"Actual upper-source recovery probe pickup");
            int deaths=game.respawns;Look(game.view.transform.position+new Vector3(-1,-.8f,0));float low=game.player.feet.y;
            for(int i=0;i<350&&game.respawns==deaths;i++){Step(Vector2.left);low=Mathf.Min(low,game.player.feet.y);}
            Require(game.respawns>deaths,"Actual departure from flat deck did not exercise checkpoint heat/fall recovery.");
            Require(route.AtMidpoint&&game.checkpoint==3&&Vector3.Distance(game.player.feet,Mid)<2,"Failure discarded the reached front landing checkpoint.");
            Require(route.LowerFragment.placement.Ready&&route.LowerConnected,"Checkpoint failure removed the distinct already-installed first stair.");
            Require(Vector3.Distance(route.UpperFragment.transform.position,game.player.feet)<4,"Upper source is stranded after checkpoint recovery.");
            GrabActual(route.UpperFragment,"Actual reachable B source after failure");game.Cancel(true);
            int[] ids=RouteProps.Select(p=>p.GetInstanceID()).ToArray();game.ResetZone();game.automated=true;game.Resume();Idle(4);
            Require(!route.FirstRestored&&!route.SecondRestored&&!route.AtMidpoint&&!game.won,"Zone reset retained completion.");
            for(int i=0;i<RouteProps.Length;i++){var p=RouteProps[i];Require(p.GetInstanceID()==ids[i]&&p.gameObject.activeInHierarchy&&Vector3.Distance(p.transform.position,p.homePosition)<.035f&&Vector3.Distance(p.transform.localScale,p.homeScale)<.001f,"Zone reset did not restore original separate source "+p.title);}
            Detail("Actual recovery lowestFeetY="+low+"; same three source identities retained by reset.");
        }
        static RaycastHit ActualStairFloor(Beach.BeachProp prop,bool upper,float fraction)
        {
            float rise=upper?1.63f:1.17f,run=upper?1.45f:1;
            float tread=Mathf.Clamp(Mathf.Ceil(fraction*24),1,24)/24;
            Vector3 expected=prop.transform.TransformPoint(new Vector3(0,-rise*.5f+rise*tread,run*(fraction-.5f)));
            var floors=Physics.RaycastAll(expected+Vector3.up*.25f,Vector3.down,.55f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider.GetComponentInParent<Beach.BeachProp>()==prop&&hit.normal.y>.8f).OrderBy(hit=>hit.distance).ToArray();
            Require(floors.Length>0,"Actual transformed stair tread missing: "+prop.title+" fraction="+fraction+" expected="+expected);
            return floors[0];
        }
        static void FixturePracticalStair(bool upper,float scale,float offsetX,float offsetZ,float yaw)
        {
            float floor=upper?3.44f:1.1f,rise=upper?1.63f:1.17f;
            Vector3 pose=new Vector3(offsetX,floor+rise*.5f*scale,(upper?UpperPose:LowerPose).z+offsetZ);
            FixtureProp(upper?route.UpperFragment:route.LowerFragment,pose,scale,yaw);
            Require(fragment.placement.Ready,"Practical variant did not settle into actual support.");
            Require(game.ValidPlacement(fragment,out var reason),"Practical variant physically overlaps structures: "+reason);
            Snapshot("Practical variant settled pose; idealConnected is diagnostic only");
        }
        static void ClimbActualVariant(bool upper,string label)
        {
            var prop=upper?route.UpperFragment:route.LowerFragment;fragment=prop;
            // Follow the actual transformed centre of each physical stair, not
            // the old nine ideal x=0 samples used by the diagnostic flag.
            foreach(float fraction in new[]{.15f,.30f,.45f,.60f,.74f,.85f})
            {
                var floor=ActualStairFloor(prop,upper,fraction);
                Detail(label+" native actual tread fraction="+fraction+" target="+floor.point+" idealLower="+route.LowerConnected+" idealUpper="+route.UpperConnected);
                Travel(floor.point,false,420,upper,tolerance:.05f,heightTolerance:.45f,requiredStair:prop);if(game.won)return;
            }
            var landing=upper?Summit:Mid;
            // A shorter useful stair may leave a modest last rise. A real
            // held jump is a legal player input and must still count as use.
            Require(game.player.grounded,"Practical final jump did not start on actual released stair support.");
            Travel(landing,true,420,upper,tolerance:.06f);Idle(3);
        }
        static void FixtureLargeGoalParasol()
        {FixtureProp(route.Parasol,new Vector3(2,3.44f+ParasolGripHeight*3.2f,93.35f),3.2f);Require(route.Parasol.placement.Ready,"Large goal fixture parasol did not settle on the actual flat deck.");}
        static void CheckPracticalStairs()
        {
            bool nonIdeal=false;
            var cases=new[]{
                new[]{1.85f,0f,0f,0f,1.75f,0f,0f,0f},
                new[]{1.90f,.15f,-.03f,5f,1.80f,.20f,-.04f,7f},
                new[]{1.92f,-.15f,-.025f,-5f,1.85f,-.20f,-.06f,-7f}};
            for(int i=0;i<cases.Length;i++)
            {
                if(i>0)ResetFixture();var pose=cases[i];string label="Practical-"+(i+1);int deaths=game.respawns;
                Detail(label+" declares only released physical prop poses before traversal; no controller teleports or progress assignments after the initial lower landing.");
                FixtureLargeGoalParasol();FixturePracticalStair(false,pose[0],pose[1],pose[2],pose[3]);bool lowerIdeal=route.LowerConnected;
                ClimbActualVariant(false,label+" lower");Require(route.FirstRestored&&route.AtMidpoint,"A physically traversed practical lower stair did not record its actual landing.");
                FixturePracticalStair(true,pose[4],pose[5],pose[6],pose[7]);bool upperIdeal=route.UpperConnected;nonIdeal|=!lowerIdeal||!upperIdeal;
                ClimbActualVariant(true,label+" upper");
                Require(game.won&&route.SecondRestored&&route.IsAtSummit(game.player.feet),"A physically traversable practical placement reached the summit without completion: "+label);
                Require(game.respawns==deaths,"Practical native traversal was replaced by checkpoint recovery.");
                Detail(label+" actual victory feet="+game.player.feet+" idealLower="+lowerIdeal+" idealUpper="+upperIdeal);
                Capture(label+"ActualSummitPlayerEye",game.view.transform.position,new Vector3(1.7f,8,97));
            }
            Require(nonIdeal,"Practical regression failed to cover a physically traversable placement rejected by the ideal nine-ray diagnostic.");
        }
        static bool FindActualCrownFloor(Vector2 xz,out RaycastHit floor)
        {
            var floors=Physics.RaycastAll(new Vector3(xz.x,8.2f,xz.y),Vector3.down,2.55f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider!=game.controller&&hit.collider.GetComponentInParent<Beach.BeachProp>()==null&&hit.normal.y>.8f&&hit.point.y>=5.8f).OrderBy(hit=>hit.distance).ToArray();
            floor=floors.Length>0?floors[0]:default;return floors.Length>0;
        }
        static void FixtureUpperContact()
        {
            FixtureFirstLanding();FixtureLargeGoalParasol();FixtureProp(route.UpperFragment,UpperPose,2);
            Require(route.UpperFragment.placement.Ready,"Upper-contact goal fixture has no actual Ready support.");
            game.PlacePlayer(ActualStairFloor(route.UpperFragment,true,.65f).point);Idle(5);
            Require(!game.won&&!route.SecondRestored,"Standing on the upper prop falsely counted as the actual fixed summit.");
            Detail("Declared actual grounded upper-source standing fixture before isolated summit footprint probe; route flags are never assigned.");
        }
        static void CheckSummitFootprint()
        {
            Require(route.SummitFloor!=null,"Actual summit floor collider API is missing.");
            var mandatory=new[]{new Vector2(-.55f,95.35f),new Vector2(.55f,95.35f),new Vector2(-.55f,96.05f),new Vector2(.55f,96.05f),new Vector2(1.7f,97)};
            var optional=new[]{new Vector2(-.8f,96.7f),new Vector2(.8f,96.7f),new Vector2(0,97.3f)};
            var points=new List<Vector2>(mandatory);
            foreach(var point in optional)if(FindActualCrownFloor(point,out var ignored))points.Add(point);else Detail("No high walkable original crown at optional probe="+point);
            for(int i=0;i<points.Count;i++)
            {
                if(i>0)ResetFixture();FixtureUpperContact();Vector2 xz=points[i];
                Require(FindActualCrownFloor(xz,out var floor),"Requested cap/crown probe has no actual walkable high floor: "+xz);
                Require(game.FootSafe(floor.point+Vector3.up*.025f),"Declared enlarged umbrella does not actually shade requested crown footprint: "+floor.point);
                bool virtualSummit=route.IsAtSummit(floor.point+Vector3.up*.05f),initialCanStand=game.CanStand(floor.point+Vector3.up*.025f);
                game.PlacePlayer(floor.point);Idle(8);
                Detail("Crown footprint native settling requested="+floor.point+" sourceCollider="+floor.collider.name+" virtualSummit="+virtualSummit+" initialCanStand="+initialCanStand+" actualFeet="+game.player.feet+" actualCanStand="+game.CanStand(game.player.feet)+" grounded="+game.player.grounded+" safe="+game.safe+" actualSummit="+route.IsAtSummit(game.player.feet));
                if(!game.won||!route.IsAtSummit(game.player.feet))SummitSupportDiagnostics();
                Require(game.player.grounded&&game.safe,"Requested raw crown did not settle to an actually grounded shaded player position: "+game.player.feet);
                Require(game.won&&route.SecondRestored&&route.IsAtSummit(game.player.feet),"Actual grounded shaded crown/flag arrival did not complete: requested="+floor.point+" actual="+game.player.feet);
                Detail("Actual summit footprint accepted="+floor.point+" collider="+floor.collider.name+" settledFeet="+game.player.feet+" oldGoalDistance="+Vector3.Distance(game.player.feet,Summit));
                if(i==4)Capture("ActualFlagAreaVictoryPlayerEye",game.view.transform.position,new Vector3(1.9f,10,97));
            }
        }
        static void SummitSupportDiagnostics()
        {
            var controller=game.controller;float radius=Mathf.Max(.02f,controller.radius-.025f);
            var bottom=game.player.feet+controller.center-Vector3.up*(controller.height*.5f-controller.radius);
            foreach(var hit in Physics.SphereCastAll(bottom+Vector3.up*.05f,radius,Vector3.down,.15f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).OrderBy(hit=>hit.distance))
            {
                var prop=hit.collider.GetComponentInParent<Beach.BeachProp>();
                Detail("Actual summit support sphere radius="+radius+" origin="+(bottom+Vector3.up*.05f)+" collider="+hit.collider.name+" owner="+(prop==null?"static":prop.title)+" distance="+hit.distance+" point="+hit.point+" normal="+hit.normal+" usable="+PlacementSupport.IsUsableGround(hit.collider));
            }
            foreach(var hit in Physics.RaycastAll(game.player.feet+Vector3.up*.18f,Vector3.down,.8f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).OrderBy(hit=>hit.distance))
                Detail("Actual summit centre downray collider="+hit.collider.name+" distance="+hit.distance+" point="+hit.point+" normal="+hit.normal);
        }
        static void CheckContactEligibility()
        {
            game.PlacePlayer(Summit);Idle(6);Require(!game.won&&!route.SecondRestored,"Bare summit arrival created missing source-use proof.");
            ResetFixture();
            game.PlacePlayer(ActualStairFloor(route.LowerFragment,false,.85f).point);Idle(6);game.PlacePlayer(Mid);Idle(6);
            Require(!route.FirstRestored,"Unreleased home A contact restored the route.");
            ResetFixture();FixtureFirstLanding();FixtureLargeGoalParasol();
            game.PlacePlayer(ActualStairFloor(route.UpperFragment,true,.85f).point);Idle(6);game.PlacePlayer(Summit);Idle(6);
            Require(!route.SecondRestored&&!game.won,"Unreleased home B contact granted summit completion.");
            ResetFixture();FixtureFirstLanding();FixtureLargeGoalParasol();
            game.PlacePlayer(new Vector3(-1,3.44f,92.3f));Idle(4);GrabActual(route.UpperFragment,"Actual held-source eligibility probe");
            game.PlacePlayer(Summit);Idle(6);Require(game.held==route.UpperFragment&&!route.SecondRestored&&!game.won,"Held preview supplied actual source-use proof.");game.Cancel(true);
            ResetFixture();FixtureUpperContact();game.PlacePlayer(Mid);Idle(4);
            GrabActual(route.UpperFragment,"Actual off-stair B pickup after valid upper source contact");game.Cancel(true);Idle(4);
            Require(game.held==null&&route.UpperFragment.placement.Ready,"Native cancel did not restore the released physical B fixture.");
            game.PlacePlayer(Summit);Idle(6);
            Require(game.player.grounded&&game.safe&&route.IsAtSummit(game.player.feet),"Stale-proof rejection did not reach a real shaded summit fixture.");
            Require(!route.SecondRestored&&!game.won,"Repicking B retained stale upper-use proof after cancel without new B contact.");
            Detail("Valid physical B contact followed by actual off-piece camera repick and native cancel cleared its old use proof; shaded summit alone did not complete.");
            ResetFixture();FixtureFirstLanding();FixtureLargeGoalParasol();
            var upper=route.UpperFragment;upper.Restore();upper.transform.localScale=Vector3.one*2;upper.transform.SetPositionAndRotation(UpperPose,Quaternion.identity);
            upper.body.position=UpperPose;upper.body.rotation=Quaternion.identity;Physics.SyncTransforms();upper.placement.ResetPlaced();Idle(4);
            Require(upper.body.isKinematic&&upper.placement.Ready,"Declared kinematic/never-released course fixture did not derive actual support.");
            game.PlacePlayer(ActualStairFloor(upper,true,.65f).point);Idle(6);game.PlacePlayer(Summit);Idle(6);
            Require(!route.SecondRestored&&!game.won,"A Ready but never-released kinematic source granted actual upper-use proof.");
            Detail("Bare goal, actual home A/B contact, held B and physically supported kinematic B all rejected without route flag assignments.");
            ResetFixture();FixtureFirstLanding();FixtureProp(route.UpperFragment,UpperPose,2);game.PlacePlayer(Mid);Idle(4);
            int deaths=game.respawns;bool reachedTop=false;
            for(int i=0;i<260&&game.respawns==deaths&&!game.won;i++)
            {
                // Keep the real motor on the central cap instead of continuing
                // into a different, genuinely shaded castle surface behind it.
                Step(game.player.feet.z<95.30f?Vector2.up:Vector2.zero);
                reachedTop|=route.IsAtSummit(game.player.feet)&&game.player.grounded;
            }
            Require(!game.won&&game.respawns>deaths,"A fast actual ascent without upper shade bypassed the native heat/goal-safety rule.");
            Detail("No-manual-shade native forward ascent rejected; reached actual top="+reachedTop+" then native recovery="+(game.respawns>deaths));
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var host=new GameObject("Castle validation camera "+name);var camera=host.AddComponent<Camera>();camera.CopyFrom(game.view);camera.enabled=false;
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
            if(finished)return;finished=true;output=output??SessionState.GetString(Key+"Output",Path.GetFullPath("Validation/BeachCastle"));
            if(report==null)report=new Report{scene=ScenePath,unity=Application.unityVersion,sceneHashBefore=SessionState.GetString(Key+"Hash","")};report.failure=failure;
            if(simulationConfigured)Physics.simulationMode=previousSimulation;
            if(contactHandler!=null)Physics.ContactEvent-=contactHandler;
            foreach(var entry in originalContactFlags)if(entry.Key!=null)entry.Key.providesContacts=entry.Value;
            try
            {
                report.sceneHashAfter=Hash(SceneFile());report.sceneUnchanged=report.sceneHashBefore==report.sceneHashAfter;
                int expected=Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-solve-only")>=0?1:
                    Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-practical-only")>=0?3:
                    Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-variants-only")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-castle-summit-only")>=0?1:10;
                report.passed=failure==null&&report.checks.Count==expected&&report.checks.All(c=>c.passed)&&report.errors.Count==0&&report.sceneUnchanged;
                if(!outputWritable&&!SessionState.GetBool(Key+"Writable",false))throw new InvalidOperationException("Fresh validation output was not established.");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"castle-results.json"),JsonUtility.ToJson(report,true));
                Debug.Log("BEACH_CASTLE_FINISHED passed="+report.passed+" checks="+report.checks.Count+" errors="+report.errors.Count+" sceneUnchanged="+report.sceneUnchanged);
            }
            catch(Exception error){report.passed=false;Debug.LogError("BEACH_CASTLE_REPORT_FAILED "+error);}
            SessionState.SetString(Key+"Phase","");SessionState.SetBool(Key+"Writable",false);EditorApplication.delayCall+=()=>EditorApplication.Exit(report.passed?0:1);
        }
        static string SceneFile()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",ScenePath));
        static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static string Argument(string name){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:null;}
    }
}
