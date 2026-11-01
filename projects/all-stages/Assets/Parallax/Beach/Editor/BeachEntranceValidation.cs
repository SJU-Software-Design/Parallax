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
    // -executeMethod ShadeLink.Editor.BeachEntranceValidation.Run -shade-output <new folder>
    // SessionState carries the request through the PlayMode domain reload. No scene is saved.
    [InitializeOnLoad]
    public static class BeachEntranceValidation
    {
        const string ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity";
        const string Key="Parallax.BeachEntranceValidation.";
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

        static BeachEntranceValidation()
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
                output=Argument("-shade-output")??Path.GetFullPath("Validation/BeachEntrance");
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
            Debug.Log("BEACH_ENTRANCE_SEARCH_READY databases="+databases.Length+" projectIndexExists="+File.Exists(index)+" path="+index);
        }

        static void Update()
        {
            string phase=SessionState.GetString(Key+"Phase","");
            if(finished||(phase!="pending"&&phase!="running"))return;
            double began;
            if(double.TryParse(SessionState.GetString(Key+"Start","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out began)&&
                EditorApplication.timeSinceStartup-began>180)
            { Finish("Validation exceeded the 180-second Editor watchdog.");return; }
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!started)
                {
                    started=true;output=SessionState.GetString(Key+"Output","");
                    report=new Report {
                        scope="Saved Beach scene in real PlayMode and native Physics. Entrance visibility, receiver geometry, prepared closed/open gate collision, actual reflected-light charging and downstream layout regression. Images are explicit camera renders; IMGUI readability and an unassisted full-stage playthrough are outside this check.",
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

        static IEnumerator Sequence()
        {
            // Allow the saved scene's Awake and Start methods to create real shadow proxies and labels.
            yield return null;yield return null;yield return null;
            var games=UnityEngine.Object.FindObjectsByType<Beach.BeachGame>(FindObjectsSortMode.None);
            Require(games.Length==1,"Expected exactly one saved BeachGame; found "+games.Length);
            game=games[0];game.automated=true;
            Require(game.lighting!=null&&game.lighting.Count>40,"Real PlayMode shadow-query meshes were not initialized.");
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationConfigured=true;
            Reset();
            RunCheck("Start shelter and original mirror interaction",CheckStart);
            yield return null;
            RunCheck("Spawn goal visibility and three camera views",CheckVisibility);
            yield return null;
            RunCheck("Readable receivers and closed/open gate collision",CheckReceiversAndGate);
            yield return null;
            RunCheck("Real sensor hits, held/misaligned rejection and charge timing",CheckOptics);
            yield return null;
            RunCheck("Water channels, checkpoints and castle restoration layout",CheckDownstream);
            yield return null;
        }

        static void RunCheck(string name,Action action)
        {
            current=new Check{name=name};report.checks.Add(current);
            try { Reset();action();current.passed=true;Debug.Log("BEACH_ENTRANCE_PASS "+name); }
            catch(Exception error) { current.failure=error.ToString();Debug.Log("BEACH_ENTRANCE_FAIL "+name+": "+error.Message); }
            finally
            {
                try { Reset(); }
                catch(Exception error) { current.passed=false;current.failure+="\nCleanup: "+error; }
                current=null;
            }
        }
        static void Reset()
        {
            if(game==null)return;
            game.automated=true;game.ResetAll();game.Resume();Physics.SyncTransforms();game.lighting.Synchronize();
        }
        static void Require(bool value,string message)
        { if(!value)throw new InvalidOperationException(message); }
        static void Detail(string text) { if(current!=null)current.details.Add(text); }
        static void Step(int frames=1)
        {
            for(int i=0;i<frames;i++)
            {
                game.Tick(Dt,Vector2.zero);
                foreach(var water in UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None))water.Simulate(Dt);
                Physics.Simulate(Dt);game.lighting.Synchronize();
            }
        }
        static void Look(Vector3 target)
        {
            Vector3 delta=target-game.view.transform.position;
            game.yaw=Mathf.Atan2(delta.x,delta.z);game.pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude);game.SyncView();
        }

        static void CheckStart()
        {
            Require(game.props!=null&&game.props.Length==11,"The saved scene must contain 11 essential entrance, bridge, shadow-harbor and castle interactable props.");
            Require(game.props.Count(p=>p.zone==0)==2&&!game.props.Any(p=>p.title=="01 · 차광 돗자리"||p.title=="02 · 접이식 의자"||p.title=="02 · 비치볼"),
                "The optional mat, folding chair and beach ball must remain decoration rather than selectable props.");
            Step(12);
            Require(game.CanStand(game.player.feet),"Entrance scenery overlaps the actual starting controller capsule.");
            Require(game.FootSafe(game.player.feet)&&game.safe&&!Beach.BeachWater.InHazard(game.player.feet),"The initial shelter must provide real protective shade.");
            var prop=game.mirror.prop;Vector3 home=prop.transform.position,scale=prop.transform.localScale;
            bool grabbed=false;
            foreach(var part in prop.parts)
            {
                Look(part.bounds.center);game.UpdateAim();
                if(game.aimed!=prop)continue;
                game.Interact();grabbed=game.held==prop;if(grabbed)break;
            }
            Require(grabbed&&prop.placement.Held&&!game.adjustingMirror,"The original mirror must be selectable by the starting camera ray and enter normal carry.");
            game.Cancel();
            Require(game.held==null&&Vector3.Distance(prop.transform.position,home)<.01f&&Vector3.Distance(prop.transform.localScale,scale)<.001f,"Cancelling must restore the original mirror.");
            Detail("11 essential props including two castle fragments and one movable castle parasol; actual start shade; original camera-ray mirror pickup and cancel passed.");
        }

        static void CheckVisibility()
        {
            Capture("Spawn",game.view.transform.position,game.view.transform.rotation);
            Capture("Overview",new Vector3(23,20,-8),Quaternion.LookRotation(new Vector3(0,1.8f,15)-new Vector3(23,20,-8)));
            Capture("SensorPuzzle",new Vector3(0,2.4f,14),Quaternion.LookRotation(new Vector3(0,1.65f,23.35f)-new Vector3(0,2.4f,14)));
            var flag=GameObject.Find("Beach goal flag");
            Require(flag!=null,"The saved scene has no Beach goal flag.");
            var flagRenderer=flag.GetComponent<Renderer>()??flag.GetComponentInChildren<Renderer>();
            Require(flagRenderer!=null&&flagRenderer.enabled,"The goal flag needs an enabled renderer.");
            var castle=GameObject.Find("3DAssets · sandcastle");
            Require(castle!=null,"The saved scene has no original 3DAssets · sandcastle target root.");
            var exit=Trace("Castle exit",Beach.BeachWorld.Exit,castle.transform);
            var goal=Trace("Beach goal flag",flagRenderer.bounds.center,flag.transform);
            Require(exit.inViewport&&goal.inViewport,"Castle exit/flag must both be inside the unmodified starting camera viewport.");
            Require(exit.clear&&goal.clear,"Starting goal sight is blocked: exit="+exit.occluder+"; flag="+goal.occluder);
        }
        static Visibility Trace(string name,Vector3 target,Transform expectedOwner)
        {
            var value=new Visibility{target=name,position=target,viewport=game.view.WorldToViewportPoint(target),clear=true,occluder="none"};
            value.inViewport=value.viewport.z>0&&value.viewport.x>0&&value.viewport.x<1&&value.viewport.y>0&&value.viewport.y<1;
            Vector3 from=game.view.transform.position,delta=target-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider==game.controller)continue;
                // A first hit on the exact target hierarchy means the castle/flag is visible.
                // Never skip an unrelated object in front of that target.
                bool summitSupport=name=="Castle exit"&&hit.collider.name=="정상 탑 받침"&&
                    game.castleRoute!=null&&hit.collider.GetComponentInParent<Beach.BeachCastleRestoration>()==game.castleRoute&&
                    Vector3.Distance(hit.collider.transform.position,new Vector3(0,6.32f,95.7f))<.01f&&
                    Vector3.Distance(hit.collider.transform.lossyScale,new Vector3(1.8f,.76f,1.4f))<.01f;
                bool targetHit=summitSupport||(expectedOwner!=null&&(hit.collider.transform==expectedOwner||hit.collider.transform.IsChildOf(expectedOwner)));
                value.targetSurface=targetHit;value.clear=targetHit;
                value.occluder=Hierarchy(hit.collider.transform);value.occluderDistance=hit.distance;break;
            }
            report.visibility.Add(value);Detail(name+" viewport="+value.viewport+" clear="+value.clear+" firstHit="+value.occluder);
            return value;
        }

        static void CheckReceiversAndGate()
        {
            var gate=game.lightGate;
            Require(gate!=null&&!gate.waterGate&&gate.sensors!=null&&gate.sensors.Length==2,"The first gate needs two optical targets.");
            Require(gate.receiverVisuals!=null&&gate.receiverVisuals.Length==2,"The first gate needs two visible solar receivers.");
            for(int i=0;i<2;i++)
            {
                var receiver=gate.receiverVisuals[i];
                Require(receiver!=null&&receiver.Target==gate.sensors[i]&&receiver.Face!=null,"Receiver target/face is not connected to the measured sensor "+i);
                Vector3 expected=new Vector3(i==0?-1.4f:1.4f,1.65f,23.35f);
                Require(Vector3.Distance(gate.sensors[i].position,expected)<.03f,"Receiver "+i+" target moved outside the agreed entrance layout.");
                var renderers=receiver.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.GetComponent<TextMesh>()==null).ToArray();
                Require(renderers.Length>0,"Receiver "+i+" has no visible housing.");
                Bounds housing=renderers[0].bounds;foreach(var renderer in renderers)housing.Encapsulate(renderer.bounds);
                Require(housing.size.x>=1.5f&&housing.size.y>=1.5f&&receiver.Face.bounds.size.x>=.4f,"Receiver "+i+" lost its readable housing/aperture dimensions.");
                Require(receiver.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Receiver decoration must not block the measured beam or the walking gap.");
            }
            Require(Vector3.Distance(gate.openOffset,new Vector3(4.5f,0,0))<.01f,"The entrance gate must open sideways by 4.5m.");
            gate.Restore();Physics.SyncTransforms();
            Require(GateHits().Any(h=>h.collider==gate.panel),"The closed gate must physically block the central walking gap.");
            ProbeClosedGateClimbing();
            // This labelled fixture isolates gate motion/collision. Natural optical unlock is tested separately.
            gate.unlocked=true;for(int i=0;i<100;i++)gate.Tick(Dt,game.lighting,game.controller);Physics.SyncTransforms();
            var remaining=GateHits();
            Require(remaining.Length==0,"Opened central gap is still blocked by "+string.Join(", ",remaining.Select(h=>h.collider.name)));
            Require(Vector3.Distance(gate.panel.transform.position,gate.closedPosition+gate.openOffset)<.01f,"The opened gate did not reach its expected physical position.");
            Detail("Prepared closed/open collision fixture; opening was driven through BeachLightGate.Tick, not a transform write.");
        }
        static void ProbeClosedGateClimbing()
        {
            var gate=game.lightGate;
            foreach(float x in new[]{0f,-4f,4f,-8f,8f,-12f,12f,-13.4f,13.4f})
            foreach(int delay in new[]{0,24,48})
            foreach(float firstYaw in Mathf.Abs(x)>13?new[]{0f,Mathf.Sign(x)*.35f}:new[]{0f})
            {
                gate.Restore();game.PlacePlayer(new Vector3(x,0,19.8f));Physics.SyncTransforms();
                Require(game.CanStand(game.controller.transform.position),"Closed-gate approach starts inside scenery at x="+x);
                var motor=new FirstPersonMotor{GroundFilter=PlacementSupport.IsUsableGround};
                for(int i=0;i<6;i++)motor.Step(game.controller,Vector2.zero,0,false,false,Dt,Beach.BeachWorld.PhysicalMask);
                float maximumY=game.controller.transform.position.y,maximumZ=game.controller.transform.position.z;
                bool mantled=false,fell=false;int pressed=-1000,presses=0;
                for(int frame=0;frame<240;frame++)
                {
                    bool jump=frame>=delay&&(frame-delay)%60==0;
                    if(jump){pressed=frame;presses++;}
                    // A seven-frame hold exceeds the motor's full extra-height window.
                    // This is a ground-level controller fixture, with heat deliberately excluded.
                    // Edge trials also steer toward the low side rail, then look
                    // forward again, so its junction cannot silently provide a step.
                    float yaw=frame<100?firstYaw:0;
                    motor.Step(game.controller,Vector2.up,yaw,jump,frame-pressed<7,Dt,Beach.BeachWorld.PhysicalMask);
                    Physics.Simulate(Dt);Physics.SyncTransforms();
                    Vector3 feet=game.controller.transform.position;
                    maximumY=Mathf.Max(maximumY,feet.y);maximumZ=Mathf.Max(maximumZ,feet.z);mantled|=motor.Mantling;
                    Require(feet.z<=gate.closedPosition.z+.45f,"Closed gate bypassed by natural jump/hold/mantle at x="+x+
                        ", delay="+delay+", firstYaw="+firstYaw+", frame="+frame+", feet="+feet+", maximumY="+maximumY+", mantled="+mantled);
                    // Falling outside the dry shoreline is not traversal through
                    // the locked gate; retain it as a labelled diagnostic outcome.
                    if(feet.y<-.1f){fell=true;break;}
                }
                if(firstYaw==0)Require(maximumZ>=gate.closedPosition.z-.9f,"Straight controller fixture never reached the closed gate at x="+x);
                Detail("Closed-gate controller fixture x="+x+", first jump frame="+delay+", firstYaw="+firstYaw+", presses="+presses+
                    ", maxY="+maximumY.ToString("F3")+", maxZ="+maximumZ.ToString("F3")+", mantle="+mantled+", fellOutside="+fell+"; remained on entrance side.");
            }
        }
        static RaycastHit[] GateHits()
        {
            Vector3 center=new Vector3(0,.025f,22.35f)+game.controller.center;
            float offset=game.controller.height*.5f-game.controller.radius;
            return Physics.CapsuleCastAll(center-Vector3.up*offset,center+Vector3.up*offset,game.controller.radius-.012f,Vector3.forward,2.8f,
                Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=game.controller).ToArray();
        }

        static void CheckOptics()
        {
            var gate=game.lightGate;var mirror=game.mirror;var prop=mirror.prop;
            FixtureMirror(2.5f,false);gate.Restore();Step(60);
            Require(ActualHits()==0&&gate.LitSensors==0&&!gate.unlocked&&gate.charge==0,"A misaligned installed mirror must not charge the receiver gate.");
            var idle=gate.receiverVisuals.Select(FaceTint).ToArray();
            AlignMirror();prop.BeginHold();game.lighting.Synchronize();gate.Restore();Step(60);
            Require(!mirror.Illuminated&&gate.LitSensors==0&&gate.charge==0&&!gate.unlocked,"A carried enlarged mirror must not power receivers.");
            FixtureMirror(2.5f,true);ChargeFromRealHits("Existing enlarged-mirror fixture",idle);
            Require(mirror.WorldWidth>2.35f,"The compatibility fixture no longer exercises the original enlarged mirror.");
            FixtureMirror(1.5f,true);
            Require(mirror.WorldWidth<2.35f,"The small-mirror fixture must be below the removed hidden width threshold.");
            ChargeFromRealHits("Both-hit mirror below former 2.35m threshold",idle);
        }
        static void FixtureMirror(float scale,bool aligned)
        {
            var prop=game.mirror.prop;prop.Restore();
            prop.transform.localScale=Vector3.one*scale;prop.transform.SetPositionAndRotation(new Vector3(-4,.03f,10),Quaternion.identity);
            prop.body.position=prop.transform.position;prop.body.rotation=prop.transform.rotation;prop.RefreshMass();
            if(aligned)AlignMirror();else game.mirror.transform.rotation=Quaternion.LookRotation(Beach.BeachLighting.ToSun);
            Physics.SyncTransforms();prop.placement.ResetPlaced();game.lighting.Synchronize();game.lightGate.Restore();
            Require(prop.placement.Ready,"The prepared mirror must have real floor support.");
        }
        static void AlignMirror()
        {
            Vector3 target=(game.lightGate.sensors[0].position+game.lightGate.sensors[1].position)*.5f;
            game.mirror.transform.rotation=Quaternion.LookRotation((target-game.mirror.transform.position).normalized+Beach.BeachLighting.ToSun);
            Physics.SyncTransforms();game.lighting.Synchronize();
        }
        static int ActualHits()
        {
            return game.lightGate.sensors.Count(sensor=>game.lighting.IsReflectedLit(sensor.position,(game.mirror.LightOrigin-sensor.position).normalized));
        }
        static void ChargeFromRealHits(string label,Color[] idle)
        {
            Require(game.mirror.prop.placement.Ready&&game.mirror.Illuminated&&ActualHits()==2,label+": real light does not reach both targets.");
            var gate=game.lightGate;gate.Restore();
            for(int i=0;i<47;i++)
            {
                Step();int hits=ActualHits();
                Require(hits==2&&gate.LitSensors==hits&&gate.illuminated,label+": indicated and actual sensor hits differ.");
            }
            Require(!gate.unlocked&&gate.charge>0&&gate.charge<1,label+": gate must accumulate 0.8s before unlocking.");
            for(int i=0;i<2;i++)
                Require(ColorDistance(FaceTint(gate.receiverVisuals[i]),idle[i])>.2f,label+": receiver face did not visibly change from idle while lit.");
            Step(2);
            Require(gate.unlocked&&gate.charge==1&&gate.LitSensors==2,label+": 49 real 60Hz ticks must unlock the two-hit gate.");
            Detail(label+": width="+game.mirror.WorldWidth.ToString("F3")+"m, actual hits=2, LitSensors=2, unlock after 49 ticks.");
        }
        static Color FaceTint(Beach.BeachSolarReceiver receiver)
        { var block=new MaterialPropertyBlock();receiver.Face.GetPropertyBlock(block);return block.GetColor("_Color"); }
        static float ColorDistance(Color a,Color b)
        { return new Vector3(a.r-b.r,a.g-b.g,a.b-b.b).magnitude; }

        static void CheckDownstream()
        {
            var checkpoints=new[]{new Vector3(-6,0,4),new Vector3(0,0,27),new Vector3(0,0,51),new Vector3(0,0,75)};
            Require(Beach.BeachWorld.Checkpoints.Length==checkpoints.Length,"Checkpoint count changed.");
            for(int i=0;i<checkpoints.Length;i++)Require(Vector3.Distance(Beach.BeachWorld.Checkpoints[i],checkpoints[i])<.001f,"Checkpoint "+i+" moved.");
            Require(Vector3.Distance(Beach.BeachWorld.CastleRest,new Vector3(0,3.44f,91.7f))<.001f&&
                Vector3.Distance(Beach.BeachWorld.Exit,new Vector3(0,6.7f,95.4f))<.001f,"Final castle/refuge coordinates changed.");
            Require(game.waterGate!=null&&game.waterGate.waterGate&&Mathf.Abs(game.waterGate.closedPosition.z-71.8f)<.001f&&
                Vector3.Distance(game.waterGate.openOffset,Vector3.up*5)<.001f,"The downstream water gate layout/motion changed.");
            CheckWater("판자 횡단 수로",new Vector2(-14,33),new Vector2(14,40),Vector3.zero,false);
            CheckWater("그늘 배 항로",new Vector2(-14,55),new Vector2(14,69),new Vector3(0,0,.9f),false);
            var positions=new[]{new Vector3(-3,.04f,29),new Vector3(4,.02f,29),
                new Vector3(5,.03f,51),new Vector3(9.7977f,.522f,51.6791f),
                new Vector3(7.4f,.873097f,50.2f),new Vector3(11.9f,1.5556948f,50.2f),
                new Vector3(-1.8f,2.65f,89.2f),new Vector3(-1.8f,4.99f,92.3f),new Vector3(2,4.8375f,91.7f)};
            var downstream=game.props.Where(p=>p.zone>0).ToArray();
            Require(downstream.Length==9&&downstream.Count(p=>p.zone==1)==2&&downstream.Count(p=>p.zone==2)==4&&downstream.Count(p=>p.zone==3)==3,
                "Expected two bridge props, four shadow-harbor props and two separate castle fragments plus their movable parasol.");
            foreach(var position in positions)Require(downstream.Count(p=>Vector3.Distance(p.homePosition,position)<.001f)==1,"Downstream home prop missing/moved at "+position);
            Require(game.shadowHarbor!=null&&game.shadowHarbor.Pieces.Length==2&&game.boat!=null&&downstream.Contains(game.boat.Prop),"The new shadow puzzle and integrated-canopy boat must be registered interactable props.");
            Require(game.castleRoute!=null&&game.castleRoute.LowerFragment!=null&&game.castleRoute.UpperFragment!=null&&game.castleRoute.Parasol!=null,
                "The compact final puzzle must register two distinct wall fragments and a movable upper parasol.");
            var castleProps=new[]{game.castleRoute.LowerFragment,game.castleRoute.UpperFragment,game.castleRoute.Parasol};
            Require(castleProps.Distinct().Count()==3&&castleProps.All(p=>p.zone==3&&downstream.Contains(p)),
                "The final-zone source props must be separate registered objects.");
            Require(Vector3.Distance(castleProps[0].homePosition,new Vector3(-1.8f,2.65f,89.2f))<.001f&&
                Vector3.Distance(castleProps[0].homeScale,Vector3.one*1.1f)<.001f&&
                Vector3.Distance(castleProps[1].homePosition,new Vector3(-1.8f,4.99f,92.3f))<.001f&&
                Vector3.Distance(castleProps[1].homeScale,Vector3.one*.8f)<.001f&&
                Vector3.Distance(castleProps[2].homePosition,new Vector3(2,4.8375f,91.7f))<.001f&&
                Vector3.Distance(castleProps[2].homeScale,Vector3.one*.65f)<.001f,
                "Both miniature fragment homes and the upper parasol grip home must match the compact front layout.");
            var castle=game.castleRoute.CastleRoot;
            Require(castle!=null&&castle.name=="3DAssets · sandcastle"&&Vector3.Distance(castle.position,new Vector3(0,0,96))<.001f&&
                Vector3.Distance(castle.localScale,Vector3.one*9)<.001f&&castle.GetComponent<Beach.BeachProp>()==null,
                "The large sandcastle art must remain fixed at (0,0,96), scale 9; the two intended front mesh cuts do not change its art transform.");
            Require(castle.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh!=null)&&castle.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),
                "The cut castle must retain its visible source art rather than being replaced by route scaffolding.");
            foreach(float x in new[]{-13f,-9f,-4f,0,4f,9f,13f})
                Require(!Physics.Raycast(new Vector3(x,3,61),Vector3.down,3.02f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore),"The open boat crossing has a walkable bypass at x="+x);
            Require(Physics.Raycast(Beach.BeachWorld.Exit+Vector3.up,Vector3.down,out var top,1.2f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&
                Mathf.Abs(top.point.y-Beach.BeachWorld.Exit.y)<.08f,"The final castle exit lost its real physical support.");
            Detail("Four checkpoints, calm first crossing, full-width boat gap and six essential bridge/harbor prop homes preserved; separate lower/upper miniatures, movable upper parasol, compact front refuge and fixed castle art transform/exit support verified.");
        }
        static void CheckWater(string name,Vector2 minimum,Vector2 maximum,Vector3 flow,bool transport)
        {
            var water=UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None).SingleOrDefault(w=>w.name==name);
            Require(water!=null&&Vector2.Distance(water.min,minimum)<.001f&&Vector2.Distance(water.max,maximum)<.001f&&
                Vector3.Distance(water.flow,flow)<.001f&&water.transportChannel==transport,"Downstream channel changed: "+name);
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
            output=output??SessionState.GetString(Key+"Output",Path.GetFullPath("Validation/BeachEntrance"));
            if(report==null)report=new Report{scene=ScenePath,unity=Application.unityVersion,sceneHashBefore=SessionState.GetString(Key+"SceneHash","")};
            report.failure=failure;
            if(simulationConfigured)Physics.simulationMode=previousSimulation;
            try
            {
                report.sceneHashAfter=File.Exists(SceneFile())?Hash(SceneFile()):"missing";
                report.sceneUnchanged=report.sceneHashBefore==report.sceneHashAfter;
                report.passed=failure==null&&report.checks.Count==5&&report.checks.All(c=>c.passed)&&report.errors.Count==0&&report.sceneUnchanged;
                if(!outputWritable&&!SessionState.GetBool(Key+"OutputWritable",false))
                    throw new InvalidOperationException("No new output folder was approved; existing validation files were left untouched. "+failure);
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"entrance-results.json"),JsonUtility.ToJson(report,true));
                Debug.Log("BEACH_ENTRANCE_FINISHED passed="+report.passed+" checks="+report.checks.Count+" errors="+report.errors.Count+" sceneUnchanged="+report.sceneUnchanged+" output="+output);
            }
            catch(Exception error) { report.passed=false;Debug.LogError("BEACH_ENTRANCE_REPORT_FAILED "+error); }
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
