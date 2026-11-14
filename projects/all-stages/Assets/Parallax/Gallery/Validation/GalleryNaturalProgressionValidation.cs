using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Gallery
{
    // Starts at the portrait checkpoint as a declared prior-room fixture. The
    // curtain, walk into checkpoint 3, BGM and hand interaction remain live.
    public sealed partial class GalleryNaturalProgressionValidation : MonoBehaviour
    {
        public GalleryGame game;
        const float Dt=1f/60;
        static bool claimed;
        string output;
        MuseumAudio audio;
        GalleryCathedralActivation activation;
        GalleryProp hands;
        SimulationMode previousSimulation;
        readonly List<string> checks=new List<string>(),errors=new List<string>(),handTrials=new List<string>(),routeTrace=new List<string>();
        [Serializable] sealed class Report
        {
            public bool passed,initialDikeSolved,portraitOpened,walkedToHands,musicResumed,publicPickup,publicDrop,handOpened,nikeJumpCrossing,nikeLandingClear,dikeCompleted,staleRecovery;
            public int finalCheckpoint,respawns,physicsSteps;
            public float musicGain,musicTime,handScale,requiredScale;
            public string scope,failure,handState,portraitState,scene;
            public string[] checks,errors,handTrials,routeTrace;
        }
        readonly Report report=new Report();
        double deadline;
        static string Argument(string name)
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
        static bool Requested()=>Array.IndexOf(Environment.GetCommandLineArgs(),"-gallery-natural-progression-test")>=0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!Requested()||SceneManager.GetActiveScene().name=="Gallery")return;
            var go=new GameObject("Museum natural progression scene loader");DontDestroyOnLoad(go);go.AddComponent<GalleryNaturalProgressionValidation>();
        }
        void Require(bool condition,string text)
        {if(!condition)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("MUSEUM_NATURAL_PASS "+text);}
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            if(!Requested()||claimed){Destroy(this);yield break;}claimed=true;
            output=Path.GetFullPath(Argument("-validation-output")??Argument("-shade-output")??Path.Combine(Application.persistentDataPath,"MuseumNaturalProgression"));
            Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
            if(game==null){yield return SceneManager.LoadSceneAsync("Gallery",LoadSceneMode.Single);game=FindFirstObjectByType<GalleryGame>();}
            yield return null;yield return null;
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;deadline=Time.realtimeSinceStartupAsDouble+180;
            var stack=new Stack<IEnumerator>();stack.Push(Sequence());
            while(stack.Count>0)
            {
                bool more=false;object next=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception ex){report.failure=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;
            }
            report.scene=SceneManager.GetActiveScene().name;report.finalCheckpoint=game==null?-1:game.checkpoint;
            report.respawns=game==null?-1:game.respawns;report.handState=HandState();
            if(game!=null&&game.portraitGate!=null)report.portraitState="checkpoint="+game.checkpoint+" stage="+game.portraitGate.Stage+" opened="+game.portraitGate.Opened+" dikeSolved="+(game.bottleBalance!=null&&game.bottleBalance.Solved)+" feet="+game.player.feet;
            if(audio!=null){report.musicGain=audio.MusicGainLevel;report.musicTime=audio.MusicTime;}
            if(hands!=null){report.handScale=hands.transform.lossyScale.x;report.requiredScale=game.orcusExit.RequiredScale;}
            report.checks=checks.ToArray();report.errors=errors.ToArray();report.handTrials=handTrials.ToArray();report.routeTrace=routeTrace.ToArray();report.passed=report.failure==null&&errors.Count==0&&report.handOpened&&report.musicResumed;
            report.scope="Actual Gallery with Dike enabled and normal progression checks. Only the initial bronze gallery is skipped by a declared Nike-room start fixture; checkpoint2/3 are never injected. Published cover/statue/bottle/book/portrait geometry poses are explicit fixtures; actual Nike fusion/painting dock, airborne forward crossing and grounded completion, Dike extraction/balance/sword drop/public socket use, both physical doorway walks, portrait performance, live BGM and public hand enlargement/drop/opening remain production results. A separate declared historical-state fixture restores checkpoint1 and NikeCleared=false after all physical gates were genuinely completed, then checks narrow owner-handoff negatives and actual forward crossing recovery without changing NikeCleared. Script PhysX accelerates gameplay; audio waits use realtime. Not an unassisted full-stage solve or a claim that the user personally jumped the Nike door.";
            Physics.simulationMode=previousSimulation;Time.timeScale=1;Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(output,"museum-natural-progression-results.json"),JsonUtility.ToJson(report,true));
            Debug.Log("MUSEUM_NATURAL_PROGRESSION "+(report.passed?"PASSED":"FAILED")+" checks="+checks.Count+" "+report.failure+" hand="+report.handState);
            Application.Quit(report.passed?0:1);
        }
        void Step(Vector2 move=default,bool jump=false)
        {
            if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Museum progression watchdog: "+HandState());
            game.Tick(Dt,move,jump);Physics.Simulate(Dt);Physics.SyncTransforms();activation?.Tick(Dt);report.physicsSteps++;
        }
        IEnumerator Frames(int count)
        {for(int i=0;i<count;i++){Step();if(i%8==7)yield return null;}}
        Vector3 Shift(Vector3 point)=>game.bottleBalance==null?point:GalleryBottleBalanceCourse.ShiftLegacy(point);
        void Look(Vector3 target)
        {Vector3 d=target-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();}
        IEnumerator Walk(Vector3 target)
        {
            for(int i=0;i<600;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.16f)yield break;
                Step(delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.5f)));
                if(i%8==7)yield return null;
            }
            throw new InvalidOperationException("Actual walking blocked at "+game.player.feet+" toward "+target);
        }
        string HandState()
        {
            if(game==null||game.orcusExit==null||game.orcusExit.prop==null)return "hands unavailable";
            var p=game.orcusExit.prop;var a=game.orcusExit.cathedralActivation;string reason=null;bool valid=game.ValidPlacement(p,out reason);
            return "cp="+game.checkpoint+" feet="+game.player.feet+" scale="+p.transform.lossyScale+" required="+game.orcusExit.RequiredScale+
                " ready="+(p.placement!=null&&p.placement.Ready)+" held="+(game.held==p||(p.placement!=null&&p.placement.Held))+" tilt="+Vector3.Angle(p.transform.up,Vector3.up)+
                " valid="+valid+" reason="+reason+" scaleAndPlacement="+game.orcusExit.ScaleAndPlacementReady+
                " pickupScale="+game.referenceScale+" pickupDistance="+game.referenceDistance+" grabLocal="+game.GrabLocalPoint+" limiter="+game.PlacementDiagnostic.Limiter+" limitSource="+(game.PlacementDiagnostic.Source==null?"none":game.PlacementDiagnostic.Source.name)+" limitBoundary="+game.PlacementDiagnostic.Boundary+" depth="+game.PlacementDiagnostic.Depth+" limitPoint="+game.PlacementDiagnostic.Point+
                (a==null?"":" near="+a.NearEnough+" progress="+a.Progress+" assistBlocked="+a.AssistanceBlocked+" assistReason="+a.AssistanceReason+" openingBlocked="+a.OpeningBlocked+" hint="+a.Status);
        }
        void TraceRoute(string phase){routeTrace.Add(phase+" | "+game.ProgressionState);}
        void PoseNike(GalleryProp p,Vector3 position,Quaternion rotation,float scale)
        {
            p.Restore();p.transform.SetPositionAndRotation(position,rotation);p.transform.localScale=Vector3.one*scale;
            p.body.position=position;p.body.rotation=rotation;p.RefreshMass();Physics.SyncTransforms();p.placement.ResetPlaced();
        }
        IEnumerator CompleteNikeRoute()
        {
            var shadow=game.shadowCourse;var light=game.nikeLight;
            Require(shadow!=null&&light!=null&&light.canvas!=null&&shadow.nikeSocket!=null,"Actual Nike canvas and painting-key socket are installed");
            game.PlacePlayer(new Vector3(0,0,27.3f));yield return Frames(8);
            game.PlacePlayer(new Vector3(0,0,30.1f));yield return Frames(8);
            Require(!shadow.NikeUnlocked&&!shadow.NikeCleared&&game.checkpoint==1,"Locked Nike doorway and a coordinate warp cannot clear the Nike course");
            game.PlacePlayer(GalleryNikeCanvas.DemoPosition);
            PoseNike(light.cover.Prop,GalleryNikeLightCover.RevealedPosition,Quaternion.identity,1);light.SyncPose();
            PoseNike(light.statue,GalleryNikeCanvas.SolutionPosition,light.statue.homeRotation,GalleryNikeCanvas.SolutionScale);
            bool brush=false;
            for(int i=0;i<900&&!light.canvas.Complete;i++){Step();brush|=light.canvas.Drawing;if(i%8==7)yield return null;}
            Require(brush&&light.canvas.Complete&&light.canvas.portablePainting.gameObject.activeInHierarchy,"Actual Nike shadow solution performs fusion, painting and frame ejection");
            var socket=shadow.nikeSocket;
            PoseNike(light.canvas.portablePainting,socket.Center-Vector3.forward*.30f,Quaternion.identity,GalleryNikeDoorSocket.TargetScale);
            Require(socket.FitHint(light.canvas.portablePainting)==null,"The actually produced Nike painting fits its real door socket");
            socket.BeginDock(light.canvas.portablePainting);
            for(int i=0;i<250&&!shadow.NikeGateOpen;i++){Step();if(i%8==7)yield return null;}
            Require(socket.Seated&&shadow.NikeUnlocked&&shadow.NikeGateOpen&&!shadow.NikeCleared,"Real painting docking opens the Nike gate but does not itself clear the crossing");
            TraceRoute("Nike painting dock and physical gate open");
            game.PlacePlayer(new Vector3(0,0,27.3f));yield return Frames(8);
            game.PlacePlayer(new Vector3(0,0,30.1f));yield return Frames(8);
            Require(!shadow.NikeCleared,"Even an unlocked Nike gate does not count a coordinate warp as walking");
            yield return Walk(new Vector3(0,0,27.2f));
            Require(!shadow.NikeCleared,"Reverse walking through the unlocked Nike gate does not clear the course");
            yield return Frames(12);float plane=shadow.NikeGatePosition.z;float before=game.player.feet.z;
            Step(Vector2.up,true);
            for(int i=0;i<120&&game.player.feet.z<30;i++)
            {
                float z=game.player.feet.z;Step(Vector2.up);
                if(z<plane&&game.player.feet.z>=plane)
                {
                    report.nikeJumpCrossing=!game.player.grounded;
                    Require(report.nikeJumpCrossing&&!shadow.NikeCleared,"Actual airborne Nike crossing records intent but waits for a safe landing");
                    TraceRoute("Nike actual airborne gate crossing");
                }
                if(i%8==7)yield return null;
            }
            yield return Frames(50);
            report.nikeLandingClear=shadow.NikeCleared;
            Require(report.nikeJumpCrossing&&report.nikeLandingClear&&game.player.grounded&&game.checkpoint==1,"Safe actual landing completes Nike while the unsolved Dike still keeps checkpoint 1");
            TraceRoute("Nike landing confirmation, Dike still unsolved");
        }
        void RestoreHistoricalPriorState()
        {
            // Explicit historical-state fixture, not evidence of puzzle solving.
            game.checkpoint=1;
            typeof(GalleryMuseumShadowCourse).GetProperty("NikeCleared").GetSetMethod(true).Invoke(game.shadowCourse,new object[]{false});
            typeof(GalleryMuseumShadowCourse).GetField("nikeCrossed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game.shadowCourse,false);
            game.portraitGate.ResetPassage();audio.SetPortraitSilence(true);TraceRoute("DECLARED stale checkpoint1 / missing Nike latch restored");
        }
        IEnumerator StalePriorRecovery()
        {
            var gate=game.portraitGate;var barrier=gate.barrier;
            Vector3 center=barrier.transform.TransformPoint(barrier.center);Vector3 front=new Vector3(center.x,0,center.z-.65f),back=front+Vector3.forward*1.3f;
            yield return Walk(front);RestoreHistoricalPriorState();
            game.PlacePlayer(back);yield return Frames(8);yield return Walk(front);
            Require(game.checkpoint==1&&!gate.PassageConfirmed,"Historical stale state is not repaired by reverse curtain traversal");
            game.PlacePlayer(front);yield return Frames(8);game.PlacePlayer(back);yield return Frames(12);
            Require(game.checkpoint==1&&!gate.PassageConfirmed,"Short PlacePlayer warp across the open curtain cannot repair progress");
            game.PlacePlayer(front);yield return Frames(8);
            Vector3 outside=center+Vector3.right*(barrier.size.x*.5f+.8f);outside.y=0;
            gate.RecordPassage(outside-Vector3.forward*.05f,outside+Vector3.forward*.05f,Dt,true);
            Require(!gate.PassageConfirmed,"Out-of-opening passage query cannot repair progress");gate.ResetPassage();
            foreach(float y in new[]{-1f,barrier.size.y+1f})
            {
                Vector3 wrongHeight=new Vector3(center.x,y,center.z);
                gate.RecordPassage(wrongHeight-Vector3.forward*.05f,wrongHeight+Vector3.forward*.05f,Dt,true);
                Require(!gate.PassageConfirmed,"A passage query below or above the actual doorway cannot repair progress");gate.ResetPassage();
            }
            game.bottleBalance.Door.SetOpening(0);
            yield return Walk(back);
            Require(game.checkpoint==1&&!gate.PassageConfirmed,"A physically closed preceding Dike door blocks historical-state recovery");
            game.bottleBalance.Door.SetOpening(1);game.PlacePlayer(front);yield return Frames(8);
            // Pause/PlacePlayer cancel an actual airborne pending passage.
            Step(Vector2.up,true);
            for(int i=0;i<70&&game.player.feet.z<center.z+.1f;i++){Step(Vector2.up);if(i%8==7)yield return null;}
            game.Pause();game.Resume();yield return Frames(45);
            Require(game.checkpoint==1&&!gate.PassageConfirmed,"Pausing cancels an airborne passage intent before landing");
            game.PlacePlayer(front);yield return Frames(12);
            yield return Walk(back);yield return Frames(8);
            report.staleRecovery=game.checkpoint==3&&gate.PassageConfirmed&&!game.shadowCourse.NikeCleared&&!audio.PortraitSilenced;
            Require(report.staleRecovery,"Actual completed-gate forward traversal repairs stale checkpoint 1 without falsifying NikeCleared");
            TraceRoute("historical stale prior state recovered by actual current gate owner");
            yield return new WaitForSecondsRealtime(.6f);
            Require(audio.MusicAudible&&audio.MusicGainLevel>.01f,"Historical-state recovery also restores live museum BGM");
        }
        IEnumerator Sequence()
        {
            Require(game!=null&&game.conceptLayout&&game.bottleBalance!=null,"Actual concept museum includes the Dike room");
            game.exhibitionPreview=false;game.StartConceptDemo(1);game.automated=true;audio=MuseumAudio.Instance;
            report.initialDikeSolved=game.bottleBalance.Solved;
            Require(game.checkpoint==1&&!game.bottleBalance.Solved&&!game.exhibitionPreview,"Only the first bronze gallery is the declared starting fixture; later checkpoints are never injected");
            yield return new WaitForSecondsRealtime(.15f);
            Require(audio!=null&&audio.GameplayRunning&&audio.MusicAudible,"Museum starts with its live music service");
            yield return CompleteNikeRoute();
            yield return CompleteDikeRoute();
            report.dikeCompleted=game.bottleBalance.Solved;
            Require(game.checkpoint==2&&report.dikeCompleted,"Actual Nike and Dike completion plus doorway walking reach portrait checkpoint 2");
            TraceRoute("full route reaches portrait");
            var gate=game.portraitGate;var portrait=gate.portrait;
            foreach(var p in portrait.pieces)p.Restore();
            for(int i=0;i<portrait.pieces.Length;i++)
            {
                var p=portrait.pieces[i];p.transform.SetPositionAndRotation(portrait.transform.TransformPoint(portrait.solutionPositions[i]),portrait.transform.rotation);
                p.transform.localScale=Vector3.one*portrait.solutionScales[i];p.body.position=p.transform.position;p.body.rotation=p.transform.rotation;p.RefreshMass();Physics.SyncTransforms();p.placement.ResetPlaced();
                Require(p.placement.Ready&&game.ValidPlacement(p,out _),"Published portrait source pose is supported: "+p.title);
            }
            game.PlacePlayer(portrait.viewingPoint.position);Look(portrait.transform.TransformPoint(new Vector3(0,1.9f,39)));portrait.Evaluate();Look(portrait.FaceCenter);portrait.Evaluate();
            Require(portrait.Complete&&portrait.CanObserve,"Actual assembled face is recognized from its moved observation point");
            for(int i=0;i<100&&!gate.Performing;i++){Step();if(i%8==7)yield return null;}
            Require(gate.Performing&&audio.PortraitSilenced,"Actual portrait absorption requests the normal music fade");
            yield return new WaitForSecondsRealtime(2.25f);
            var music=audio.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip!=null&&s.clip.name=="MuseumBgmC01").ToArray();
            Require(audio.PortraitFadeOutComplete&&audio.MusicGainLevel<.0001f&&music.Length==2&&music.All(s=>!s.isPlaying),"Live museum music reaches silence during the portrait performance");
            for(int i=0;i<900&&!gate.Opened;i++){Step();if(i%8==7)yield return null;}
            report.portraitOpened=gate.Opened;
            Require(gate.Opened&&!gate.Performing&&!gate.barrier.enabled,"Complete portrait performance physically opens its curtain");
            Require(game.checkpoint==2&&audio.PortraitSilenced,"Opening the curtain alone does not inject the next checkpoint");
            Look(Shift(new Vector3(0,1.55f,46)));
            yield return Walk(Shift(new Vector3(0,0,45.2f)));
            yield return Frames(8);
            report.walkedToHands=game.checkpoint==3;
            Require(report.walkedToHands&&game.player.grounded&&game.bottleBalance.Solved&&game.shadowCourse.NikeCleared,"Actual full-route curtain traversal reaches checkpoint 3 with Nike and Dike completed");
            Require(!audio.PortraitSilenced&&audio.GameplayRunning,"Natural checkpoint transition clears portrait music silence");
            Require(game.RoomTitle.Contains("두 손")&&game.RoomHint.Contains("충분히 커졌어요"),"Natural room transition exposes the hand-growth objective and readiness instructions");
            yield return new WaitForSecondsRealtime(.65f);
            var liveMusic=music.FirstOrDefault(s=>s.isPlaying);
            Require(liveMusic!=null&&audio.MusicGainLevel>.01f&&audio.MusicAudible,"Natural room entry restores actual music playback and gain");
            int sample=liveMusic.timeSamples;yield return new WaitForSecondsRealtime(.12f);
            report.musicResumed=liveMusic.timeSamples!=sample;Require(report.musicResumed,"Resumed museum AudioSource advances real DSP samples");
            TraceRoute("full normal portrait-to-hand handoff");
            yield return StalePriorRecovery();
            var entrance=game.orcusExit;hands=entrance.prop;activation=entrance.cathedralActivation;
            Require(activation!=null&&!activation.Opened,"Hand exhibit begins unopened on the natural route");
            Vector3 approach=new Vector3(hands.WorldBounds.center.x,0,hands.WorldBounds.min.z-.65f);
            yield return Walk(approach);
            foreach(var part in hands.parts.OrderBy(p=>Vector3.Distance(p.bounds.center,game.view.transform.position)))
            {
                Look(part.bounds.center);game.UpdateAim();if(game.aimed!=hands)continue;game.Interact();if(game.held==hands)break;
            }
            report.publicPickup=game.held==hands;Require(report.publicPickup,"Camera ray and public Interact pick up the real hand sculpture");
            float initialScale=hands.transform.lossyScale.x;
            Vector3 farFloor=Shift(new Vector3(0,.002f,54.5f));
            handTrials.Add("pickup: "+HandState());
            // An open curtain is a real passage, not a wall at the room's
            // checkpoint. Permit the same backward carry into the portrait room.
            for(int i=0;i<640&&!activation.NearEnough;i++)
            {
                Look(farFloor);float minZ=Shift(new Vector3(0,0,40.5f)).z;
                Step(game.player.feet.z>minZ?Vector2.down:Vector2.zero);
                if(i%80==79)handTrials.Add("backward floor aim "+i+": "+HandState());
                if(i%8==7)yield return null;
            }
            // The hit point is the actual grab anchor. A higher wall aim is a
            // valid player action when aiming one fixed floor point limits a
            // tall compound prop against the floor; never inject its scale.
            foreach(float aimY in new[]{1.25f,2.5f,3.5f,4.5f})
            {
                if(activation.NearEnough)break;
                Look(Shift(new Vector3(0,aimY,54.5f)));yield return Frames(60);
                handTrials.Add("wall aim y="+aimY+": "+HandState());
            }
            Require(activation.NearEnough&&hands.transform.lossyScale.x>initialScale*1.2f&&game.held==hands,"Actual backward walking and perspective enlarge held hands to their readiness hint: "+HandState());
            Require(!activation.Opened,"Held hands stay closed before release");
            // First click at the accepted current aim. Re-aiming closer first
            // can correctly shrink the prop below the readiness threshold.
            bool dropped=game.Drop();handTrials.Add("current-aim Drop="+dropped+": "+HandState());
            foreach(float legacyZ in new[]{54.5f,53f,52.5f,54f,51.5f})
            {
                if(dropped)break;
                Look(Shift(new Vector3(0,.002f,legacyZ)));yield return Frames(20);
                if(!activation.NearEnough)continue;
                dropped=game.Drop();handTrials.Add("floor Drop z="+legacyZ+" result="+dropped+": "+HandState());
            }
            report.publicDrop=dropped;
            Require(dropped&&game.held==null&&!hands.body.isKinematic&&hands.body.useGravity,"Public Drop releases enlarged hands to actual gravity: "+HandState());
            for(int i=0;i<900&&!activation.Opened;i++){Step();if(i%8==7)yield return null;}
            report.handOpened=activation.Opened;
            Require(report.handOpened&&hands.placement.Ready&&entrance.IsReadyForEntry,"Released hands physically settle, receive normal assistance and open without a menu jump: "+HandState());
            Require(game.checkpoint==3&&game.respawns==0,"Natural portrait-to-hand route retains checkpoint and player without recovery");
        }
    }
}