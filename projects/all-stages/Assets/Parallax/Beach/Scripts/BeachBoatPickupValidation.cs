using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Beach
{
    // Independent progression regression. Only initial stage-entry/source poses
    // are fixtures: after the actual reveal no reset, stage jump, source toggle,
    // held setter or direct boat pose/scale assignment is allowed.
    public sealed class BeachBoatPickupValidation : MonoBehaviour
    {
        const float Dt=1f/60;
        static bool claimed;
        static readonly FieldInfo JumpHeld=typeof(BeachGame).GetField("gameJumpHeld",BindingFlags.Instance|BindingFlags.NonPublic);
        BeachGame game;
        BeachBoat boat;
        BeachWater[] waters;
        string output,phase="boot";
        int frame,initialRespawns;
        SimulationMode previousSimulation;
        bool simulationChanged;
        int manualWaterCalls;
        readonly Dictionary<BeachWater,bool> waterEnabled=new Dictionary<BeachWater,bool>();
        readonly List<string> checks=new List<string>(),errors=new List<string>(),details=new List<string>();
        readonly List<string> trace=new List<string>();
        readonly List<string> captures=new List<string>(),skippedCaptures=new List<string>();
        readonly List<Probe> probes=new List<Probe>();
        [Serializable] sealed class Probe
        {
            public string phase,prop,belowCollider,belowProp;
            public bool active,assembly,hit,blocksBoat;
            public Vector3 rayOrigin;
            public float hitDistance;
        }
        [Serializable] sealed class Report
        {
            public bool passed,noCapture,captureUsed;
            public int captureCount;
            public string[] captures,skippedCaptures;
            public string scope,scene,unity,failure;
            public string[] checks,details,errors;
            public Probe[] legacyCargoProbes;
            public int inactiveLegacyBlockers,propRegistryCount,frames,respawns;
        }
        int inactiveLegacyBlockers;
        static bool Requested()=>Array.IndexOf(Environment.GetCommandLineArgs(),"-beach-boat-pickup-validate")>=0;
        static bool NoCapture=>Array.IndexOf(Environment.GetCommandLineArgs(),"-validation-no-capture")>=0;
        static string Argument(string key)
        {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!Requested())return;
            var node=new GameObject("Beach boat pickup progression validation");DontDestroyOnLoad(node);
            node.AddComponent<BeachBoatPickupValidation>();
        }
        void Require(bool condition,string text)
        {
            if(!condition)throw new InvalidOperationException(text+"; phase="+phase+"; held="+(game==null||game.held==null?"none":game.held.title)+"; notice="+(game==null?"":game.notice));
            checks.Add(text);Debug.Log("BOAT_PICKUP_PASS "+text);
        }
        void Ensure(bool condition,string text){if(!condition)throw new InvalidOperationException(text);}
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            if(!Requested()||claimed){Destroy(gameObject);yield break;}claimed=true;
            output=Path.GetFullPath(Argument("-validation-output")??Argument("-shade-output")??Path.Combine(Application.persistentDataPath,"BeachBoatPickupValidation"));
            Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
            string failure=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object item=null;
                try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}
                catch(Exception ex){failure=ex.ToString();break;}
                if(!more){stack.Pop();continue;}
                if(item is IEnumerator nested){stack.Push(nested);continue;}yield return item;
            }
            foreach(var state in waterEnabled)if(state.Key!=null)state.Key.enabled=state.Value;
            details.Add("Manual physics clock isolation: water components="+waterEnabled.Count+" native force calls="+manualWaterCalls+" manual frames="+frame
                +"; original enabled states restored; FixedUpdate was disabled only while this test explicitly called Simulate once per water per Step.");
            if(simulationChanged)Physics.simulationMode=previousSimulation;Time.timeScale=1;
            Application.logMessageReceived-=Log;
            var report=new Report{passed=failure==null&&errors.Count==0&&checks.Count>0,scene=SceneManager.GetActiveScene().name,unity=Application.unityVersion,failure=failure,
                scope="Focused fresh-scene progression regression. Stage3 entry/player checkpoint and the two shadow solution source poses are explicit initial fixtures. The real shadow solver, .85s confirmation, .65s visible reveal, gravity and settling create the miniature. From reveal onward there is no ResetAll/ResetZone, stage/player teleport, source activation change, held setter or direct boat pose/scale fixture. Camera-ray Interact, native movement/perspective/drop, actual water buoyancy and native boarding must succeed continuously. During manual Script-mode physics, BeachWater components temporarily disable their automatic FixedUpdate while the harness invokes their unchanged real Simulate exactly once per water per Step; original enabled states are restored in cleanup. This isolates the clock without changing buoyancy, boat behavior, collision, shade or boarding criteria. An already active stage3 parasol is separately placed on the real boat roof as a cargo-negative fixture and only that cargo is restored afterward. Not a full unassisted four-puzzle playthrough.",
                noCapture=NoCapture,captureUsed=captures.Count>0,captureCount=captures.Count,captures=captures.ToArray(),skippedCaptures=skippedCaptures.ToArray(),
                checks=checks.ToArray(),details=details.ToArray(),errors=errors.ToArray(),legacyCargoProbes=probes.ToArray(),inactiveLegacyBlockers=inactiveLegacyBlockers,
                propRegistryCount=game==null?0:game.props.Length,frames=frame,respawns=game==null?0:game.respawns-initialRespawns};
            File.WriteAllText(Path.Combine(output,"boat-pickup-results.json"),JsonUtility.ToJson(report,true));
            File.WriteAllLines(Path.Combine(output,"boat-pickup-trace.csv"),trace);
            Debug.Log("BOAT_PICKUP_VALIDATION "+(report.passed?"PASSED":"FAILED")+" checks="+checks.Count+" output="+output);
            Application.Quit(report.passed?0:1);
        }
        IEnumerator Run()
        {
            if(SceneManager.GetActiveScene().name!="Beach")
            {
                Require(Application.CanStreamedLevelBeLoaded("Beach"),"Actual Beach scene is included in the Tutorial entry build");
                yield return SceneManager.LoadSceneAsync("Beach",LoadSceneMode.Single);
            }
            yield return null;yield return null;
            game=FindFirstObjectByType<BeachGame>();Ensure(game!=null,"BeachGame missing");
            boat=game.boat;Ensure(boat!=null&&boat.Prop!=null&&game.shadowHarbor!=null,"Actual boat/harbor missing");
            game.automated=true;Application.runInBackground=true;game.Resume();
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationChanged=true;
            waters=FindObjectsByType<BeachWater>(FindObjectsSortMode.None);
            // Physics.simulationMode=Script does not stop MonoBehaviour.FixedUpdate.
            // Prevent uncounted extra buoyancy forces between coroutine yields;
            // every manual Step still invokes the real public Simulate exactly once.
            foreach(var water in waters){waterEnabled.Add(water,water.enabled);water.enabled=false;}
            trace.Add("frame,phase,playerX,playerY,playerZ,boatX,boatY,boatZ,held,ready,materializing,activeProps,respawns");
            // The sole entry fixture happens before the production solver reveals anything.
            phase="stage3 entry fixture";game.checkpoint=2;game.PlacePlayer(BeachWorld.Checkpoints[2]);initialRespawns=game.respawns;
            Require(!game.shadowHarbor.Solved&&!boat.gameObject.activeInHierarchy,"Fresh scene starts with unsolved silhouettes and hidden reward boat");
            int[] registry=game.props.Select(p=>p.GetInstanceID()).ToArray();
            for(int i=0;i<game.shadowHarbor.Pieces.Length;i++)
            {
                var p=game.shadowHarbor.Pieces[i];p.Restore();
                p.transform.SetPositionAndRotation(game.shadowHarbor.SolutionPositions[i],game.shadowHarbor.SolutionRotations[i]);
                p.transform.localScale=Vector3.one*game.shadowHarbor.SolutionScales[i];
                p.body.position=p.transform.position;p.body.rotation=p.transform.rotation;Physics.SyncTransforms();p.Release();
            }
            game.lighting.Synchronize();phase="production shadow solve and reveal";
            for(int i=0;i<900&&(!game.shadowHarbor.Solved||game.shadowHarbor.Materializing||!boat.Prop.placement.Ready);i++){Step();if(i%20==19)yield return null;}
            Require(game.shadowHarbor.Solved&&game.shadowHarbor.RevealCount==1&&!game.shadowHarbor.Materializing&&boat.Prop.placement.Ready,
                "Real shadow solver creates and settles the miniature without injecting completion state");
            Require(game.shadowHarbor.Pieces.All(p=>!p.gameObject.activeInHierarchy),"Production reveal naturally hides both consumed source pieces");
            Require(registry.SequenceEqual(game.props.Select(p=>p.GetInstanceID()))&&game.props.Length==9&&game.props.Contains(boat.Prop)
                &&(game.counterweightFinale!=null?game.props.Contains(game.counterweightFinale.Rock):game.harborFinale!=null&&game.props.Contains(game.harborFinale.Island))&&game.shadowHarbor.Pieces.All(p=>game.props.Contains(p)),
                "Reset registry retains all nine current props, both consumed source pieces, the boat and the current finale prop");
            ProbeCargo("newly revealed");
            inactiveLegacyBlockers=probes.Count(p=>p.blocksBoat&&!p.active&&p.assembly);
            Require(inactiveLegacyBlockers>0,"Legacy raw registry raycasts reproduce inactive assembly pieces falsely counted as boat cargo");
            var active=((IObjectInteractionHost<BeachProp>)game).Props;
            Require(active.Length==7&&active.All(p=>p.gameObject.activeInHierarchy)&&active.Count(p=>p==boat.Prop)==1
                &&active.Count(p=>p==(game.counterweightFinale!=null?game.counterweightFinale.Rock:game.harborFinale.Island))==1&&game.shadowHarbor.Pieces.All(p=>!active.Contains(p)),
                "Interaction host exposes the seven active prop roots including the boat and current finale prop; consumed sources and decorations are absent");
            phase="continuous walk to receiving tray";Vector3 home=boat.Prop.homePosition;
            yield return Travel(new Vector3(0,0,49.3f));yield return Travel(new Vector3(home.x,.08f,49.3f));yield return Travel(new Vector3(home.x,0,home.z-.95f));
            Vector3 settled=boat.transform.position,settledScale=boat.transform.localScale;
            phase="public empty boat grab";AimAndInteract(boat.Prop);
            Require(game.held==boat.Prop,"Newly materialized empty boat is picked up through public camera Interact without reset or source toggles");
            Capture("01-natural-reveal-public-pickup");
            game.Cancel();yield return Frames(12);
            Require(game.held==null&&Vector3.Distance(boat.transform.position,settled)<.10f&&Vector3.Distance(boat.transform.localScale,settledScale)<.001f,
                "Ordinary cancel restores the same miniature while consumed pieces remain inactive");
            phase="actual active cargo negative";
            var cargo=game.props.First(p=>p.title=="03 · 제작대 파라솔");
            var roof=boat.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Boat canopy physical rim");
            cargo.Restore();cargo.transform.localScale=Vector3.one*.55f;cargo.transform.rotation=Quaternion.identity;
            cargo.transform.position=new Vector3(roof.bounds.center.x,3,roof.bounds.center.z-roof.bounds.extents.z*.4f);Physics.SyncTransforms();
            cargo.transform.position+=Vector3.up*(roof.bounds.max.y+.25f-cargo.WorldBounds.min.y);cargo.body.position=cargo.transform.position;cargo.body.rotation=cargo.transform.rotation;
            Physics.SyncTransforms();cargo.Release();cargo.body.linearVelocity=Vector3.down;
            yield return Frames(180);
            ProbeCargo("real active parasol on boat");
            Require(probes.Any(p=>p.phase=="real active parasol on boat"&&p.prop==cargo.title&&p.active&&p.blocksBoat),
                "Actual active stage3 cargo rests on the boat and remains a valid cargo candidate");
            AimAndInteract(boat.Prop);
            Require(game.held==null&&game.notice!=null&&game.notice.Contains("위에 놓인 사물"),
                "Real cargo still blocks public boat pickup with the existing remove-cargo message");
            // Negative-fixture cleanup: only the deliberately moved active cargo.
            // Hidden source transforms/active state and the revealed boat are untouched.
            cargo.Restore();game.lighting.Synchronize();yield return Frames(10);
            Require(game.shadowHarbor.Pieces.All(p=>!p.gameObject.activeInHierarchy)&&registry.SequenceEqual(game.props.Select(p=>p.GetInstanceID())),
                "Cargo fixture cleanup does not reactivate consumed sources or change the prop registry");
            phase="public boat carry and perspective";AimAndInteract(boat.Prop);Require(game.held==boat.Prop,"Removing real cargo permits the same public boat pickup");
            float miniature=boat.transform.localScale.x;Vector3 carryStart=game.player.feet;
            Look(game.view.transform.position+new Vector3(-1,-.8f,0));
            yield return Travel(new Vector3(home.x,.08f,49.3f));yield return Travel(new Vector3(0,.08f,49.3f));
            Require(game.held==boat.Prop&&Vector3.Distance(carryStart,game.player.feet)>5,"Native player carries the same completed miniature along the workshop route");
            Look(new Vector3(0,-.2f,63));yield return Frames(60);
            yield return Travel(new Vector3(0,.24f,55.6f));yield return Frames(12);
            bool dropped=false;
            foreach(var target in new[]{new Vector3(0,-1.7f,64),new Vector3(0,-1f,64),new Vector3(3,-.1f,59),new Vector3(-3,-.1f,59),new Vector3(0,-.5f,64)})
            {
                Look(target);yield return Frames(60);
                details.Add("Perspective release attempt target="+target+" boat="+boat.transform.position+" scale="+boat.transform.localScale.x+" reason="+game.invalidReason+" obstacle="+game.PlacementObstacle);
                if(boat.transform.localScale.x>1.2f&&boat.HullBounds.min.z>56.05f&&boat.HullBounds.center.z<=60&&game.Drop()){dropped=true;break;}
            }
            Require(dropped&&boat.transform.localScale.x>miniature*2.8f,"Native perspective enlargement and public Drop place the same boat on actual launch water");
            phase="water settling";yield return Frames(300);
            Require(boat.Prop.placement.Ready&&BeachWater.Supports(boat.Prop)&&game.held==null,"Released boat becomes physically buoyant and Ready without direct water-pose fixture");
            Capture("02-camera-placed-floating-boat");
            phase="native boarding";
            float runX=Mathf.Clamp(boat.HullBounds.center.x,-1.9f,1.9f);yield return Travel(new Vector3(runX,.24f,54.7f));yield return Frames(3);
            Require(game.player.grounded,"Native boarding run-up starts on the real departure pier");
            Vector3 landing=new Vector3(boat.HullBounds.center.x,boat.HullBounds.max.y,boat.HullBounds.min.z+.30f);
            Look(new Vector3(landing.x,game.view.transform.position.y,landing.z));
            for(int i=0;i<120&&game.player.feet.z<55.72f;i++){Step(Vector2.up);if(i%20==19)yield return null;}
            Require(game.player.grounded&&game.player.feet.z<56.05f,"Native running jump leaves the actual pier edge");
            yield return Travel(landing,true);yield return Travel(boat.transform.TransformPoint(new Vector3(0,.12f,.40f)));yield return Frames(12);
            Require(boat.IsAboard&&game.FootSafe(game.player.feet)&&game.respawns==initialRespawns,"Same naturally created, grabbed and water-placed boat supports a real shaded aboard player");
            Capture("03-native-boarded-same-boat");
            Require(game.shadowHarbor.Pieces.All(p=>!p.gameObject.activeInHierarchy)&&registry.SequenceEqual(game.props.Select(p=>p.GetInstanceID())),
                "End-to-end pickup/carry/drop/boarding never resets the reveal or reactivates hidden source pieces");
        }
        void Step(Vector2 move=default,bool jump=false,bool held=false)
        {
            JumpHeld?.SetValue(game,held);game.Tick(Dt,move,jump);
            foreach(var water in waters){water.Simulate(Dt);manualWaterCalls++;}Physics.Simulate(Dt);Physics.SyncTransforms();
            foreach(var prop in game.props)if(prop.gameObject.activeInHierarchy){prop.placement.Tick(Dt);prop.GetComponent<BeachPropAudio>()?.SampleWater();}
            game.lighting.Synchronize();frame++;
            Ensure(game.respawns==initialRespawns,"Unexpected heat/water recovery broke continuous progression at "+phase+" feet="+game.player.feet);
            if(frame%30==0)trace.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8},{9},{10},{11},{12}",
                frame,phase,game.player.feet.x,game.player.feet.y,game.player.feet.z,boat.transform.position.x,boat.transform.position.y,boat.transform.position.z,
                game.held!=null,boat.Prop.placement.Ready,game.shadowHarbor.Materializing,((IObjectInteractionHost<BeachProp>)game).Props.Length,game.respawns));
        }
        IEnumerator Frames(int count){for(int i=0;i<count;i++){Step();if(i%20==19)yield return null;}}
        IEnumerator Travel(Vector3 target,bool jump=false,int limit=360)
        {
            Vector3 start=game.player.feet;bool launched=false;int hold=0;
            for(int i=0;i<limit;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.16f&&Mathf.Abs(target.y-game.player.feet.y)<.20f&&game.player.grounded&&(!jump||launched&&i>15))yield break;
                if(delta.magnitude>.04f)game.yaw=Mathf.Atan2(delta.x,delta.y);
                bool pressed=jump&&!launched;if(pressed){Ensure(game.player.grounded,"Boarding jump must begin grounded");launched=true;}
                Vector2 input=delta.magnitude<.04f?Vector2.zero:delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.6f));
                Step(input,pressed,launched&&hold++<7);if(i%20==19)yield return null;
            }
            throw new InvalidOperationException("Native movement could not reach "+target+" from "+start+"; actual="+game.player.feet+" safe="+game.safe);
        }
        void Look(Vector3 point)
        {
            Vector3 d=point-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();
        }
        void AimAndInteract(BeachProp target)
        {
            foreach(var part in target.parts.OrderBy(p=>Vector3.Distance(p.bounds.center,game.view.transform.position)))
            {
                if(!part.enabled)continue;Look(part.bounds.center);game.UpdateAim();
                if(game.aimed==target){game.Interact();return;}
            }
            throw new InvalidOperationException("Actual camera cannot aim at "+target.title+" from "+game.view.transform.position);
        }
        void ProbeCargo(string label)
        {
            foreach(var prop in game.props)
            {
                if(prop==boat.Prop)continue;var bounds=prop.WorldBounds;Vector3 origin=new Vector3(bounds.center.x,bounds.min.y+.04f,bounds.center.z);
                bool hit=Physics.Raycast(origin,Vector3.down,out var below,.14f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
                var support=hit?below.collider.GetComponentInParent<BeachProp>():null;
                probes.Add(new Probe{phase=label,prop=prop.title,active=prop.gameObject.activeInHierarchy,assembly=game.shadowHarbor.Pieces.Contains(prop),rayOrigin=origin,
                    hit=hit,hitDistance=hit?below.distance:0,belowCollider=hit?below.collider.name:"none",belowProp=support==null?"none":support.title,blocksBoat=support==boat.Prop});
            }
        }
        void Capture(string name)
        {
            if(NoCapture){skippedCaptures.Add(name);Debug.Log("BOAT_PICKUP_CAPTURE_SKIP "+name+" (-validation-no-capture)");return;}
            var camera=game.view;var previousTarget=camera.targetTexture;var previous=RenderTexture.active;
            var texture=new RenderTexture(1280,720,24);Texture2D pixels=null;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());captures.Add(name+".png");
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previous;texture.Release();Destroy(texture);if(pixels!=null)Destroy(pixels);}
        }
    }
}
