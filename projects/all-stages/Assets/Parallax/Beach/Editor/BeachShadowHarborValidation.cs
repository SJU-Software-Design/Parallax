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
    // Saved-scene GPU PlayMode validation. Do not pass -quit or -nographics.
    // -executeMethod ShadeLink.Editor.BeachShadowHarborValidation.Run -shade-output <new folder>
    [InitializeOnLoad]
    public static class BeachShadowHarborValidation
    {
        const string ScenePath="Assets/Parallax/Beach/Scenes/Beach.unity",Key="Parallax.BeachShadowHarborValidation.";
        const float Dt=1f/60;
        static readonly List<string> bootErrors=new List<string>();
        static Beach.BeachGame game;
        static Beach.BeachShadowHarbor harbor;
        static Beach.BeachBoat boat;
        static IEnumerator sequence;
        static Report report;
        static Check current;
        static string output;
        static bool started,finished,writable,configured;
        static SimulationMode previousSimulation;
        [Serializable] public sealed class Check{public string name,failure;public bool passed;public List<string> details=new List<string>();}
        [Serializable] public sealed class Trace{public Vector2 point;public bool expectedShadow,blocked;public string caster;}
        [Serializable] public sealed class RevealTrace
        {public string phase;public Vector3 position,scale;public bool kinematic,gravity,ready;public float verticalVelocity;}
        [Serializable] public sealed class Report
        {
            public bool passed,sceneUnchanged;
            public string scope,unity,gpu,failure,sceneHashBefore,sceneHashAfter;
            public List<Check> checks=new List<Check>();
            public List<Trace> matchingRays=new List<Trace>();
            public List<RevealTrace> revealFrames=new List<RevealTrace>();
            public List<string> images=new List<string>(),errors=new List<string>();
        }
        static BeachShadowHarborValidation(){EditorApplication.update+=Update;Application.logMessageReceived+=Log;}
        public static void Run()
        {
            try
            {
                SessionState.SetBool(Key+"Writable",false);
                Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Start from EditMode.");
                output=Path.GetFullPath(Argument("-shade-output")??"Validation/BeachShadowHarbor");
                string log=Argument("-logFile");string currentLog=string.IsNullOrEmpty(log)?null:Path.GetFullPath(log);
                Require(!Directory.Exists(output)||!Directory.EnumerateFileSystemEntries(output).Any(f=>
                    currentLog==null||Directory.Exists(f)||!string.Equals(Path.GetFullPath(f),currentLog,StringComparison.OrdinalIgnoreCase)),"Use a new validation output folder.");
                Directory.CreateDirectory(output);writable=true;SessionState.SetBool(Key+"Writable",true);
                Require(File.Exists(SceneFile()),"Saved Beach scene is missing.");
                Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,"The isolated Editor has unsaved changes.");
                SessionState.SetString(Key+"Output",output);SessionState.SetString(Key+"Hash",Hash(SceneFile()));
                SessionState.SetString(Key+"Start",EditorApplication.timeSinceStartup.ToString("R",CultureInfo.InvariantCulture));SessionState.SetString(Key+"Phase","pending");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                Require(UnityEditor.Search.SearchService.EnumerateDatabases().ToArray().Length>0,"Search database was not initialized before PlayMode.");
                EditorApplication.EnterPlaymode();
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void Update()
        {
            string phase=SessionState.GetString(Key+"Phase","");if(finished||(phase!="pending"&&phase!="running"))return;
            double began;if(double.TryParse(SessionState.GetString(Key+"Start","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out began)&&EditorApplication.timeSinceStartup-began>180){Finish("180-second watchdog expired.");return;}
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(!started)
                {
                    started=true;output=SessionState.GetString(Key+"Output","");
                    report=new Report{scope="Saved Beach scene, real GPU and native Physics. Prior optical completion and matching source-piece poses are explicit fixtures. Both source pieces additionally use untouched home poses for native operator approach, camera pickup at their visible plate centres, real held perspective enlargement/reduction and cancel/shadow restoration. Real upright-board shadow-mesh positive/exterior rays, source Ready/held state, the same boat shrinking from the board, actual Rigidbody gravity and supported landing, ordinary camera pickup, native operator motion and mid-reveal reset are tested. No unassisted full-stage completion is claimed.",unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,sceneHashBefore=SessionState.GetString(Key+"Hash","")};
                    report.errors.AddRange(bootErrors);SessionState.SetString(Key+"Phase","running");sequence=Sequence();
                }
                if(!sequence.MoveNext())Finish(null);
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static IEnumerator Sequence()
        {
            yield return null;yield return null;yield return null;
            game=UnityEngine.Object.FindFirstObjectByType<Beach.BeachGame>();Require(game!=null,"No BeachGame.");
            harbor=game.shadowHarbor;boat=game.boat;Require(harbor!=null&&boat!=null&&harbor.Pieces.Length==2,"Harbor/boat/source references missing.");
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;configured=true;
            RunCheck("Upright board, hidden boat and native shaded operator approach",CheckInitial);yield return null;
            RunCheck("Both source plates stay centred during native pickup and perspective scaling",CheckSourcePivots);yield return null;
            RunCheck("Drop-only assistance supports a native home-to-board shadow solve",CheckNativeAssistedSolve);yield return null;
            RunCheck("Disabled, distant, wrong, undersized and blocked assistance is rejected",CheckAssistRejection);yield return null;
            RunCheck("Held previews, wrong silhouettes and blanket shade are rejected",CheckRejection);yield return null;
            RunCheck("Real board silhouette shrinks and drops the same grabbable boat",CheckMatching);yield return null;
            RunCheck("Mid-reveal and completed-zone reset reuse the same boat",CheckReset);yield return null;
        }
        static void RunCheck(string name,Action action)
        {
            current=new Check{name=name};report.checks.Add(current);
            try{Reset();action();current.passed=true;Debug.Log("BEACH_SHADOW_HARBOR_PASS "+name);}
            catch(Exception error){current.failure=error.ToString();Debug.Log("BEACH_SHADOW_HARBOR_FAIL "+name+": "+error.Message);}
            finally{try{Reset();}catch(Exception error){current.passed=false;current.failure+="\nCleanup: "+error;}current=null;}
        }
        static void Reset()
        {
            if(game==null)return;game.automated=true;game.Cancel(true);game.ResetAll();game.Resume();
            // The first gate is the declared already-completed prior segment.
            game.lightGate.unlocked=true;game.checkpoint=2;game.PlacePlayer(Beach.BeachWorld.Checkpoints[2]);
            Physics.SyncTransforms();game.lighting.Synchronize();Step(3);
        }
        static void Step(int frames=1,Vector2 move=default)
        {
            for(int i=0;i<frames;i++)
            {
                game.Tick(Dt,move);
                foreach(var water in UnityEngine.Object.FindObjectsByType<Beach.BeachWater>(FindObjectsSortMode.None))water.Simulate(Dt);
                Physics.Simulate(Dt);game.lighting.Synchronize();
            }
        }
        static void Travel(Vector3 target)
        {
            int deaths=game.respawns;
            for(int i=0;i<420;i++)
            {
                var delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.16f&&Mathf.Abs(game.player.feet.y-target.y)<.18f&&game.player.grounded){Detail("Native walk reached "+game.player.feet+"; safe="+game.safe);return;}
                Step(1,delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.5f)));
                Require(game.respawns==deaths,"Operator approach caused a heat/water/fall recovery at "+game.player.feet);
            }
            throw new InvalidOperationException("Operator approach blocked at "+game.player.feet+" toward "+target+" nearby="+string.Join("; ",Physics.OverlapSphere(game.player.feet+Vector3.up*.8f,1.1f,Beach.BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore).Where(c=>c!=game.controller).Select(c=>c.name+" @ "+c.transform.position)));
        }
        static void Fixtures(bool held=false,bool wrong=false)
        {
            for(int i=0;i<harbor.Pieces.Length;i++)
            {
                var p=harbor.Pieces[i];p.Restore();
                Quaternion rotation=harbor.SolutionRotations[i]*(wrong?Quaternion.Euler(0,i==0?37:-31,0):Quaternion.identity);
                p.transform.SetPositionAndRotation(harbor.SolutionPositions[i],rotation);p.transform.localScale=Vector3.one*harbor.SolutionScales[i];
                p.body.position=p.transform.position;p.body.rotation=p.transform.rotation;Physics.SyncTransforms();
                if(held)p.BeginHold();else p.Release();
            }
            Physics.SyncTransforms();game.lighting.Synchronize();
            Detail("Prepared source poses; native Ready settling and shadow matching remain active. held="+held+" wrong="+wrong);
        }
        static Vector3 PickupPoint()
        {
            Vector3 home=boat.Prop.homePosition;
            return new Vector3(home.x,0,home.z-.95f);
        }
        static void ApproachBoard()
        {
            Travel(new Vector3(0,0,49.3f));
            Travel(new Vector3(boat.Prop.homePosition.x,.08f,49.3f));
            Travel(PickupPoint());
        }
        static void RecordReveal(string phase)
        {
            var prop=boat.Prop;
            report.revealFrames.Add(new RevealTrace{phase=phase,position=prop.transform.position,scale=prop.transform.localScale,
                kinematic=prop.body.isKinematic,gravity=prop.body.useGravity,ready=prop.placement.Ready,
                verticalVelocity=prop.body.isKinematic?0:prop.body.linearVelocity.y});
        }
        static void WaitForSolve()
        {
            for(int i=0;i<480&&!harbor.Solved;i++)Step();
            Require(harbor.Solved,"Actual projected silhouette did not match: coverage="+harbor.Coverage+" hull="+harbor.HullCoverage+" canopy="+harbor.CanopyCoverage+" clear="+harbor.Clearance+" charge="+harbor.Charge+" poses="+string.Join("; ",harbor.Pieces.Select(p=>p.transform.position+"/"+p.transform.eulerAngles+" Ready="+p.placement.Ready)));
        }
        static void WaitForLanding()
        {
            for(int i=0;i<480&&harbor.Materializing;i++)Step();
            Require(!harbor.Materializing&&boat.Prop.placement.Ready,"The dropped boat must natively settle on its real support before it is ready to pick up.");
            Require(Vector3.Distance(boat.transform.position,boat.Prop.homePosition)<.12f,"The same miniature must land on the board-front catch tray, not at a separate departure display: "+boat.transform.position);
        }
        static void CheckInitial()
        {
            Require(!harbor.Solved&&harbor.Charge==0&&!boat.Prop.gameObject.activeInHierarchy,"Boat must start hidden and puzzle unsolved.");
            Require(game.GetComponentsInChildren<Beach.BeachBoat>(true).Length==1,"There must be one registered boat, including its hidden state.");
            Require(harbor.samples.Count(s=>s.inside)>20&&harbor.samples.Count(s=>!s.inside)>20,"Both silhouette coverage and exterior light need real probes.");
            Require(Mathf.Abs(harbor.target.forward.y)<.05f&&Vector3.Dot(harbor.target.up,Vector3.up)>.99f,"The shadow canvas must stand upright like a notice board.");
            Require(Vector3.Dot(harbor.target.forward,Beach.BeachLighting.ToSun)>.1f,"The upright board front must receive the actual stage sunlight.");
            Require(harbor.boatReleaseAnchor!=null&&harbor.dropAnchor!=null,"The board emergence and native drop anchors must be saved in the scene.");
            Require(Vector3.Distance(new Vector3(boat.Prop.homePosition.x,0,boat.Prop.homePosition.z),new Vector3(harbor.target.position.x,0,harbor.target.position.z))<1.6f,"The miniature catch tray must sit immediately in front of this board.");
            Capture("HarborOverview",new Vector3(17,8,45),new Vector3(8,1.7f,52));
            ApproachBoard();
            foreach(float x in new[]{0f,2f,4f,6f,8f,9.5f})Require(game.FootSafe(new Vector3(x,.08f,49.3f)),"Operator strip lacks real shade at x="+x);
            Require(game.CanStand(PickupPoint()+Vector3.up*.08f),"The board-front pickup point lacks headroom.");
            Require(game.safe,"The actual board-front approach must finish in real shade.");
            Capture("HarborPlayerEye",game.view.transform.position,harbor.target.position);
            Require(!harbor.Solved&&!boat.Prop.gameObject.activeInHierarchy,"Initial home shadows must not accidentally solve.");
        }
        static void CheckRejection()
        {
            Fixtures(true,false);Step(110);Require(!harbor.Solved&&harbor.Charge==0,"Correct geometry held as preview must not charge.");
            foreach(var p in harbor.Pieces)Require(!p.placement.Ready,"A held source cannot be Ready.");
            Reset();Fixtures(false,true);Step(170);Require(!harbor.Solved,"Wrong rotated silhouettes cannot unlock the boat.");
            Reset();var blanket=GameObject.CreatePrimitive(PrimitiveType.Cube);blanket.name="Validation unrelated shade blanket";
            blanket.transform.position=harbor.target.position+new Vector3(.6f,2.5f,-1.05f);blanket.transform.localScale=new Vector3(8,.16f,7);
            try
            {
                game.lighting.Rebuild();Step(110);
                Require(!harbor.Solved&&harbor.Charge==0&&harbor.Clearance<.3f,"A broad unrelated shadow must not pass the shape/exterior-light check.");
                Detail("Blanket rejected with coverage="+harbor.Coverage+" exteriorClear="+harbor.Clearance);
            }
            finally{UnityEngine.Object.DestroyImmediate(blanket);game.lighting.Rebuild();}
        }
        static bool PlateCentred(BoxCollider plate)
        {
            Vector3 point=game.view.WorldToViewportPoint(plate.bounds.center);
            return point.z>0&&Mathf.Abs(point.x-.5f)<.025f&&Mathf.Abs(point.y-.5f)<.025f;
        }
        static void CheckSourcePivots()
        {
            for(int index=0;index<harbor.Pieces.Length;index++)
            {
                Reset();var piece=harbor.Pieces[index];
                string plateName=index==0?"선체 그림자 조각":"차양 그림자 조각";
                var plate=piece.parts.Single(part=>part.name==plateName);
                Vector3 originalPosition=piece.transform.position,originalScale=piece.transform.localScale;
                Quaternion originalRotation=piece.transform.rotation;
                Require(Vector3.Distance(plate.bounds.center,piece.transform.position)<.003f,"The visible "+plateName+" centre must be the actual pickup root, not the stand foot.");
                Travel(new Vector3(0,0,49.3f));Travel(new Vector3(Mathf.Min(piece.homePosition.x,10.4f),.08f,49.3f));
                Require(game.safe&&game.player.grounded,"Native source pickup must take place on the real shaded operator strip.");
                int deaths=game.respawns;
                Vector3 shadow=plate.bounds.center-Beach.BeachLighting.ToSun*(plate.bounds.center.y/Beach.BeachLighting.ToSun.y);
                Require(game.lighting.TryGetDirectShadowCaster(shadow,Vector3.up,out var originalCaster)&&originalCaster==piece,"The original supported source must cast its real plate-centre shadow.");
                Look(plate.bounds.center);game.UpdateAim();Require(game.aimed==piece,"The native camera ray must select "+plateName+" at its visible centre.");
                game.Interact();Require(game.held==piece&&piece.placement.Held,"Native pickup must hold the source piece.");
                Require(game.GrabLocalPoint.sqrMagnitude<.000001f,"The ordinary root pickup anchor must coincide with the rebased visible plate centre.");
                float initialScale=piece.transform.localScale.x;Step(60);
                Require(PlateCentred(plate),"The picked plate must align to the actual screen centre.");
                Require(!game.lighting.TryGetDirectShadowCaster(shadow,Vector3.up,out var heldCaster)||heldCaster!=piece,"A held source preview must lose its actual protective shadow.");
                Detail(plateName+" native pickup: feet="+game.player.feet+" home="+originalPosition+" scale="+initialScale+" referenceDistance="+game.referenceDistance);
                bool enlarged=false;
                foreach(var direction in new[]{new Vector3(0,1.2f,2),new Vector3(0,2,2),new Vector3(-1.5f,2,2),new Vector3(1.5f,2,2),new Vector3(0,3,2),new Vector3(0,2,3.5f)})
                {
                    Look(game.view.transform.position+direction);Step(45);
                    Require(game.held==piece&&game.respawns==deaths,"Native held scaling must preserve the source and ordinary heat rules.");
                    Vector3 centre=game.view.WorldToViewportPoint(plate.bounds.center);
                    Detail(plateName+" enlarge look="+direction+" actualScale="+piece.transform.localScale.x+" viewport="+centre+" blocked="+game.PlacementBlocked+" reason="+game.invalidReason);
                    if(piece.transform.localScale.x>initialScale*1.1f&&!game.PlacementBlocked&&PlateCentred(plate)){enlarged=true;break;}
                }
                Require(enlarged,"Looking into free space must natively enlarge "+plateName+" above its initial scale while keeping the visible plate centred.");
                Capture(index==0?"HullSourceEnlargedAtCentre":"CanopySourceEnlargedAtCentre",game.view.transform.position,game.view.transform.position+game.view.transform.forward*5);
                float larger=piece.transform.localScale.x;bool reduced=false;
                foreach(var direction in new[]{new Vector3(0,-.25f,1.5f),new Vector3(0,-.65f,1.5f),new Vector3(0,-1.2f,1),new Vector3(0,-2,1),new Vector3(-.5f,-.65f,1.5f)})
                {
                    Look(game.view.transform.position+direction);Step(45);
                    Require(game.held==piece&&game.respawns==deaths,"Native held reduction must preserve the source and ordinary heat rules.");
                    Vector3 centre=game.view.WorldToViewportPoint(plate.bounds.center);
                    Detail(plateName+" reduce look="+direction+" actualScale="+piece.transform.localScale.x+" viewport="+centre+" blocked="+game.PlacementBlocked+" reason="+game.invalidReason);
                    if(piece.transform.localScale.x<initialScale*.95f&&piece.transform.localScale.x<larger*.85f&&!game.PlacementBlocked&&PlateCentred(plate)){reduced=true;break;}
                }
                Require(reduced,"Looking toward a closer surface must natively reduce "+plateName+" below its initial scale while keeping the visible plate centred.");
                Capture(index==0?"HullSourceReducedAtCentre":"CanopySourceReducedAtCentre",game.view.transform.position,game.view.transform.position+game.view.transform.forward*5);
                game.Cancel();Step(25);
                Require(game.held==null&&piece.placement.Ready&&game.respawns==deaths,"Cancelling source pickup must restore native Ready without a hazard recovery.");
                Require(Vector3.Distance(piece.transform.position,originalPosition)<.005f&&Quaternion.Angle(piece.transform.rotation,originalRotation)<.1f&&Vector3.Distance(piece.transform.localScale,originalScale)<.005f,"Cancel must restore the original source pose and scale.");
                Require(game.lighting.TryGetDirectShadowCaster(shadow,Vector3.up,out var restoredCaster)&&restoredCaster==piece,"Cancel must restore the original source's actual plate-centre shadow.");
                Detail(plateName+" native cancellation restored pose, Ready and actual shadow; no direct source transform assignment was used.");
            }
        }
        static void TravelPrecise(Vector3 target)
        {
            int deaths=game.respawns;
            for(int i=0;i<360;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.025f&&game.player.grounded)return;
                Step(1,delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.3f)));
                Require(game.respawns==deaths,"Native assist approach caused a hazard recovery at "+game.player.feet);
            }
            throw new InvalidOperationException("Native precise assist approach blocked at "+game.player.feet+" toward "+target);
        }
        static bool AlignmentFor(int index,out Beach.BeachShadowDropAssist.Alignment alignment)
            =>Beach.BeachShadowDropAssist.TryGetAlignment(harbor.Pieces[index],harbor.assistPlates[index],harbor.target,harbor.assistAnchors[index],out alignment);
        static Beach.BeachShadowDropAssist.Alignment NativeNearSource(int index)
        {
            // Authored target data plans camera and walking inputs only. The
            // source is picked from home and all held poses use AdvanceHeld.
            var piece=harbor.Pieces[index];var anchor=harbor.assistAnchors[index];
            Travel(new Vector3(game.player.feet.x,.08f,49.3f));
            Travel(new Vector3(Mathf.Min(piece.homePosition.x,10.4f),.08f,49.3f));
            Require(game.safe&&piece.gameObject.activeInHierarchy&&piece.placement.Ready,"Native assist pickup needs a supported home source and real operator shade.");
            Look(harbor.assistPlates[index].bounds.center);game.UpdateAim();Require(game.aimed==piece,"Actual camera ray failed to select the source for near-match placement.");
            game.Interact();Require(game.held==piece,"Native near-match placement must start with ordinary camera pickup.");
            game.RotateHeld(Mathf.DeltaAngle(game.heldRotation.eulerAngles.y,anchor.rotation.eulerAngles.y+3),0);
            int deaths=game.respawns;
            foreach(float roughScale in new[]{.95f,.97f,1f,1.02f,.94f})
            {
                Vector3 roughRoot=anchor.position+harbor.target.right*.05f;
                float distance=game.referenceDistance/game.referenceScale*roughScale;
                for(int calibration=0;calibration<2;calibration++)
                {
                    float vertical=roughRoot.y-game.view.transform.position.y;
                    Require(distance>Mathf.Abs(vertical)+.03f,"Native pickup reference does not leave room for the authored camera approach.");
                    float forward=Mathf.Sqrt(distance*distance-vertical*vertical);
                    TravelPrecise(new Vector3(roughRoot.x,0,roughRoot.z-forward));
                }
                foreach(Vector3 offset in new[]{Vector3.zero,Vector3.up*.04f,Vector3.down*.04f,harbor.target.right*.06f,-harbor.target.right*.06f})
                {
                    Look(roughRoot+offset);Step(55);
                    Require(game.held==piece&&game.respawns==deaths&&game.safe,"Native near-match aiming must preserve the held source and actual operator safety.");
                    bool near=AlignmentFor(index,out var alignment);
                    var interaction=(ObjectInteraction<Beach.BeachProp>)typeof(Beach.BeachGame).GetProperty("Interaction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                    var limit=interaction.LastPlacement;
                    Detail("Native near-match input source="+index+" intendedScale="+roughScale+" lookOffset="+offset+" feet="+game.player.feet+" actualRoot="+piece.transform.position+" scale="+piece.transform.localScale.x+" angle="+Quaternion.Angle(piece.transform.rotation,anchor.rotation)+" near="+near+" blocked="+game.PlacementBlocked+" reason="+game.invalidReason+" limiter="+limit.Limiter+" hit="+(limit.Source==null?"none":limit.Source.name)+" point="+limit.Point);
                    if(near&&game.ValidPlacement(piece,out var reason))return alignment;
                }
            }
            throw new InvalidOperationException("Actual walking, rotation and aiming did not reach the subtle near-match window for source "+index);
        }
        static void CheckNativeAssistedSolve()
        {
            Require(harbor.placementAssistEnabled&&harbor.assistPlates.Length==2&&harbor.assistAnchors.Length==2,"The scene must provide enabled drop assistance and both source references.");
            var first=harbor.Pieces[0];Vector3 home=first.homePosition,scale=first.homeScale;Quaternion rotation=first.homeRotation;
            NativeNearSource(0);int before=harbor.AssistedDrops;Step(45);
            Require(game.held==first&&harbor.AssistedDrops==before&&Quaternion.Angle(first.transform.rotation,harbor.assistAnchors[0].rotation)>1,"Holding an approximate match must not silently align it or count an assisted drop.");
            game.Cancel();Step(20);
            Require(game.held==null&&harbor.AssistedDrops==before&&first.placement.Ready,"Cancelling a near match must preserve ordinary home support without an assisted drop.");
            Require(Vector3.Distance(first.transform.position,home)<.005f&&Vector3.Distance(first.transform.localScale,scale)<.005f&&Quaternion.Angle(first.transform.rotation,rotation)<.1f,"Cancelling near-match placement must restore the original source pose.");
            Reset();
            for(int index=0;index<harbor.Pieces.Length;index++)
            {
                var piece=harbor.Pieces[index];NativeNearSource(index);int assisted=harbor.AssistedDrops;
                Capture(index==0?"NativeHullNearMatchBeforeDrop":"NativeCanopyNearMatchBeforeDrop",game.view.transform.position,game.view.transform.position+game.view.transform.forward*5);
                Require(game.Drop(),"The ordinary release action must accept a nearly matching native placement.");
                Require(game.held==null&&harbor.AssistedDrops==assisted+1,"Only the successful actual release must increment the assisted-drop count.");
                Require(Vector3.Distance(piece.transform.position,harbor.assistAnchors[index].position)<.005f&&Quaternion.Angle(piece.transform.rotation,harbor.assistAnchors[index].rotation)<.2f,"The small release correction must align the source to its physical authored pose.");
                for(int i=0;i<90&&!piece.placement.Ready&&!harbor.Solved;i++)Step();
                Require(piece.placement.Ready||harbor.Solved,"An assisted source must still settle using the ordinary support rules.");
                Detail("Successful native assisted release source="+index+" actualRoot="+piece.transform.position+" scale="+piece.transform.localScale.x+" counter="+harbor.AssistedDrops);
            }
            WaitForSolve();Require(harbor.AssistedDrops==2,"Both native source releases must be counted exactly once.");
            Require(harbor.HullCoverage>=.84f&&harbor.CanopyCoverage>=.84f&&harbor.Clearance>=.88f,"Assisted placement must still pass actual source-shadow matching.");
            WaitForLanding();Capture("NativeAssistedBoardSolveAndBoat",game.view.transform.position,boat.transform.position+Vector3.up*.25f);
            Detail("Both pieces were picked from untouched homes and placed through real walking, camera aiming, RotateHeld and game.Drop; no held source transform was assigned.");
        }
        static Vector3 FixtureNearRoot(int index,float scale)
        {
            var anchor=harbor.assistAnchors[index];Vector3 normal=harbor.target.forward,sun=Beach.BeachLighting.ToSun;
            Vector3 shadow=anchor.position-sun*(Vector3.Dot(anchor.position-harbor.target.position,normal)/Vector3.Dot(sun,normal));
            var piece=harbor.Pieces[index];float lowest=float.PositiveInfinity;
            foreach(var part in piece.parts)for(int i=0;i<8;i++)
            {
                Vector3 point=part.center+Vector3.Scale(part.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 local=piece.transform.InverseTransformPoint(part.transform.TransformPoint(point));
                lowest=Mathf.Min(lowest,(anchor.rotation*Vector3.Scale(local,anchor.lossyScale)).y);
            }
            float supportY=anchor.position.y+lowest;
            float height=supportY-lowest*scale/anchor.lossyScale.x;
            return shadow+sun*((height-shadow.y)/sun.y);
        }
        static void DeclaredAssistFixture(int index,Vector3 position,Quaternion rotation,float scale)
        {
            var piece=harbor.Pieces[index];piece.Restore();piece.transform.SetPositionAndRotation(position,rotation);piece.transform.localScale=Vector3.one*scale;
            piece.body.position=position;piece.body.rotation=rotation;Physics.SyncTransforms();
        }
        static void RejectUnchanged(int index,string label)
        {
            var piece=harbor.Pieces[index];Vector3 position=piece.transform.position,scale=piece.transform.localScale;Quaternion rotation=piece.transform.rotation;
            Require(!AlignmentFor(index,out var ignored),label+" must not qualify for near-match assistance.");
            Require(Vector3.Distance(position,piece.transform.position)<.0001f&&Vector3.Distance(scale,piece.transform.localScale)<.0001f&&Quaternion.Angle(rotation,piece.transform.rotation)<.01f,"The geometry-only rejection must not move the source: "+label);
            Detail("Declared negative geometry rejected without mutation: "+label);
        }
        static Vector3 BlockedTargetPoint(Beach.BeachProp piece,Beach.BeachShadowDropAssist.Alignment alignment)
        {
            foreach(var part in piece.parts)
                foreach(var point in new[]{new Vector3(0,.9f,0),new Vector3(.9f,.9f,0),new Vector3(-.9f,.9f,0),new Vector3(.9f,0,0),new Vector3(-.9f,0,0),new Vector3(0,-.9f,0)})
                {
                    Vector3 local=piece.transform.InverseTransformPoint(part.transform.TransformPoint(part.center+Vector3.Scale(part.size*.5f,point)));
                    Vector3 target=alignment.position+alignment.rotation*Vector3.Scale(local,alignment.scale);
                    if(piece.parts.All(shape=>Vector3.Distance(shape.ClosestPoint(target),target)>.035f))return target;
                }
            throw new InvalidOperationException("Blocked-target test could not find a candidate interior clear of the actual held source.");
        }
        static void CheckAssistRejection()
        {
            bool enabled=harbor.placementAssistEnabled;
            try
            {
                NativeNearSource(0);harbor.placementAssistEnabled=false;int before=harbor.AssistedDrops;
                Require(game.Drop(),"Disabling assistance must retain ordinary valid release.");
                Require(harbor.AssistedDrops==before&&Quaternion.Angle(harbor.Pieces[0].transform.rotation,harbor.assistAnchors[0].rotation)>1,"The disabled option must preserve the original approximate rotation and never count assistance.");
            }
            finally{harbor.placementAssistEnabled=enabled;}
            Reset();
            for(int index=0;index<harbor.Pieces.Length;index++)
            {
                var anchor=harbor.assistAnchors[index];Vector3 near=FixtureNearRoot(index,.95f);Quaternion nearRotation=anchor.rotation*Quaternion.Euler(0,3,0);
                DeclaredAssistFixture(index,near,nearRotation,.95f);
                Require(AlignmentFor(index,out var positive),"The explicitly declared near geometry fixture must qualify before its negative variations are tested.");
                DeclaredAssistFixture(index,near,anchor.rotation*Quaternion.Euler(0,35,0),.95f);RejectUnchanged(index,"wrong rotation source "+index);
                DeclaredAssistFixture(index,near+harbor.target.right*.7f,nearRotation,.95f);RejectUnchanged(index,"distant placement source "+index);
                DeclaredAssistFixture(index,near,nearRotation,.55f);RejectUnchanged(index,"undersized source "+index);
            }
            Reset();var piece=harbor.Pieces[0];var alignment=NativeNearSource(0);
            Vector3 position=piece.transform.position,size=piece.transform.localScale;Quaternion original=piece.transform.rotation;
            Vector3 blockedPoint=BlockedTargetPoint(piece,alignment);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Validation physical blocker inside aligned target";
            blocker.transform.position=blockedPoint;blocker.transform.localScale=Vector3.one*.02f;
            try
            {
                Physics.SyncTransforms();Require(game.ValidPlacement(piece,out var reason),"The blocker must leave the current near pose clear: "+reason);
                Require(!game.TryApplyDropAlignment(piece,alignment),"A real collision inside the target must refuse the small alignment sweep.");
                Require(Vector3.Distance(piece.transform.position,position)<.001f&&Vector3.Distance(piece.transform.localScale,size)<.001f&&Quaternion.Angle(piece.transform.rotation,original)<.1f,"A blocked alignment must restore the exact current held pose.");
                Require(harbor.AssistedDrops==0&&game.held==piece,"A refused candidate must stay held without counting assistance.");
                Detail("Actual candidate collision refused at "+blocker.transform.position+" while preserving the valid held pose.");
            }
            finally{UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();game.Cancel(true);}
        }
        static void CheckMatching()
        {
            var original=boat;int reveals=harbor.RevealCount;ApproachBoard();Fixtures();
            bool recorded=false;
            for(int i=0;i<480&&!harbor.Solved;i++)
            {
                Step();
                if(!recorded&&harbor.Matching&&harbor.Charge>.35f&&!harbor.Solved)
                {
                    Require(harbor.Pieces.All(p=>p.gameObject.activeInHierarchy&&p.placement.Ready),"The actual visible source pieces must settle before their shadows charge the board.");
                    foreach(var sample in harbor.samples)
                    {
                        bool blocked=game.lighting.TryGetDirectShadowCaster(harbor.target.TransformPoint(new Vector3(sample.point.x,sample.point.y,0)),harbor.target.forward,out var caster);
                        report.matchingRays.Add(new Trace{point=sample.point,expectedShadow=sample.inside,blocked=blocked,caster=caster==null?"fixed/none":caster.title});
                    }
                    Capture("MatchingBoatSilhouette",game.view.transform.position,harbor.target.position);recorded=true;
                }
            }
            Require(recorded,"The real source shadows must form the board silhouette before materialization begins.");
            Require(harbor.Pieces.All(p=>!p.gameObject.activeInHierarchy),"The completed source pieces must be consumed into the same visible boat instead of remaining as duplicate silhouettes.");
            Require(harbor.Solved&&harbor.Coverage>=.86f&&harbor.Clearance>=.88f,"Actual projected silhouette did not match: coverage="+harbor.Coverage+" hull="+harbor.HullCoverage+" canopy="+harbor.CanopyCoverage+" clear="+harbor.Clearance+" charge="+harbor.Charge+" poses="+string.Join("; ",harbor.Pieces.Select(p=>p.transform.position+"/"+p.transform.eulerAngles+" Ready="+p.placement.Ready)));
            Require(harbor.Materializing&&boat.Prop.gameObject.activeInHierarchy&&boat==original,"The same boat must emerge immediately at the solved board.");
            Require(harbor.RevealCount==reveals+1&&game.GetComponentsInChildren<Beach.BeachBoat>(true).Length==1,"One matching result must reveal one existing boat.");
            Require(Vector3.Distance(boat.transform.position,harbor.boatReleaseAnchor.position)<.03f,"The first visible boat must occupy the board emergence anchor.");
            float startSize=boat.transform.localScale.magnitude;
            Require(startSize>boat.Prop.homeScale.magnitude*1.5f,"The board boat must begin visibly larger than the miniature it becomes.");
            RecordReveal("Completed board becomes the same boat");Capture("BoardBoatEmergence",game.view.transform.position,harbor.target.position);
            Look(boat.HullBounds.center);game.Interact();Require(game.held==null,"A boat still materializing must not be grabbed before native release and settling.");
            Step(17);RecordReveal("Shrinking off the upright board");
            Require(boat.transform.localScale.magnitude<startSize&&boat.Prop.body.isKinematic,"The board boat must visibly shrink before the gravity drop.");
            Capture("BoardBoatShrinking",game.view.transform.position,harbor.target.position);
            bool gravityObserved=false,fallObserved=false;float previousY=boat.transform.position.y;
            for(int i=0;i<480&&harbor.Materializing;i++)
            {
                Step();
                if(!boat.Prop.body.isKinematic&&boat.Prop.body.useGravity)
                {
                    if(!gravityObserved){RecordReveal("Released to native Rigidbody gravity");Capture("BoardBoatGravityDrop",game.view.transform.position,boat.transform.position);gravityObserved=true;}
                    if(boat.Prop.body.linearVelocity.y<-.05f&&boat.transform.position.y<previousY-.001f)fallObserved=true;
                }
                previousY=boat.transform.position.y;
            }
            Require(gravityObserved&&fallObserved,"The miniature must actually fall under native Rigidbody gravity, not teleport or follow a scripted landing arc.");
            WaitForLanding();
            Require(boat==original&&Vector3.Distance(boat.transform.localScale,boat.Prop.homeScale)<.005f,"The landed miniature must be the original boat at its normal grab scale.");
            Require(harbor.Pieces.All(p=>!p.gameObject.activeInHierarchy),"Consumed source pieces must remain hidden after the boat lands.");
            RecordReveal("Native supported catch-tray landing");Capture("MaterializedMiniBoat",game.view.transform.position,boat.Prop.transform.position+Vector3.up*.25f);
            Step(120);Require(harbor.RevealCount==reveals+1,"Remaining solved cannot repeatedly materialize boats.");
            // Ordinary camera selection, not a direct Held assignment.
            bool grabbed=false;
            foreach(var part in boat.Prop.parts)
            {
                if(!part.enabled)continue;Look(part.bounds.center);game.UpdateAim();game.Interact();
                if(game.held==boat.Prop){grabbed=true;break;}
            }
            Require(grabbed&&boat.Prop.placement.Held,"Materialized miniature boat must be selectable with the ordinary camera-ray interaction.");
            game.Cancel();Require(game.held==null&&boat.Prop.gameObject.activeInHierarchy,"Cancelling boat pickup must keep the solved boat available.");
            Detail("Real upright-board silhouette; native source Ready; same boat board emergence, visible shrink, actual gravity drop and supported tray landing; normal camera-ray pickup/cancel.");
        }
        static void CheckReset()
        {
            var original=boat;Fixtures();WaitForSolve();Step(12);
            Require(harbor.Materializing,"Mid-reveal reset must interrupt the actual shrink/drop sequence.");
            game.checkpoint=2;game.ResetZone();Step(10);
            Require(!harbor.Solved&&!harbor.Materializing&&!boat.gameObject.activeInHierarchy,"Reset during the board reveal must cancel it and hide the same boat.");
            Require(Vector3.Distance(boat.transform.position,boat.Prop.homePosition)<.005f&&Vector3.Distance(boat.transform.localScale,boat.Prop.homeScale)<.005f,"Mid-reveal reset must restore the miniature pose and scale.");
            foreach(var p in harbor.Pieces)
                Require(p.gameObject.activeInHierarchy&&p.placement.Ready&&Vector3.Distance(p.transform.position,p.homePosition)<.005f&&Vector3.Distance(p.transform.localScale,p.homeScale)<.005f,"Mid-reveal reset must reactivate the source pieces at their supported home poses.");
            Fixtures();WaitForSolve();WaitForLanding();
            game.checkpoint=2;game.ResetZone();
            Require(!harbor.Solved&&harbor.Charge==0&&!boat.Prop.gameObject.activeInHierarchy,"Zone retry must clear completion and hide boat.");
            Require(boat==original&&game.GetComponentsInChildren<Beach.BeachBoat>(true).Length==1,"Reset must reuse one boat.");
            foreach(var p in harbor.Pieces)
                Require(p.gameObject.activeInHierarchy&&p.placement.Ready&&Vector3.Distance(p.transform.position,p.homePosition)<.005f&&Vector3.Distance(p.transform.localScale,p.homeScale)<.005f,"Active, supported source home state not restored.");
            Detail("Mid-reveal and completed-zone resets restored both source homes, hidden mini boat and unsolved challenge, with no duplicated boat or lingering reveal.");
        }
        static void Look(Vector3 point)
        {
            Vector3 delta=point-game.view.transform.position;game.yaw=Mathf.Atan2(delta.x,delta.z);game.pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude);game.SyncView();
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var host=new GameObject("Harbor validation camera "+name);var camera=host.AddComponent<Camera>();camera.CopyFrom(game.view);camera.enabled=false;
            var texture=new RenderTexture(1600,900,24);Texture2D pixels=null;var active=RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();string path=Path.Combine(output,name+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());report.images.Add(path);
            }
            finally{RenderTexture.active=active;camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(host);}
        }
        static void Log(string text,string stack,LogType kind)
        {
            string phase=SessionState.GetString(Key+"Phase","");if(phase!="pending"&&phase!="running")return;
            if(kind!=LogType.Error&&kind!=LogType.Exception&&kind!=LogType.Assert)return;
            if(report==null)bootErrors.Add(text+"\n"+stack);else report.errors.Add(text+"\n"+stack);
        }
        static void Finish(string failure)
        {
            if(finished)return;finished=true;output=output??SessionState.GetString(Key+"Output",Path.GetFullPath("Validation/BeachShadowHarbor"));
            if(report==null)report=new Report{unity=Application.unityVersion,sceneHashBefore=SessionState.GetString(Key+"Hash","")};report.failure=failure;
            if(configured)Physics.simulationMode=previousSimulation;
            try
            {
                report.sceneHashAfter=File.Exists(SceneFile())?Hash(SceneFile()):"missing";report.sceneUnchanged=report.sceneHashBefore==report.sceneHashAfter;
                report.passed=failure==null&&report.checks.Count==7&&report.checks.All(c=>c.passed)&&report.errors.Count==0&&report.sceneUnchanged;
                Require(writable||SessionState.GetBool(Key+"Writable",false),"No fresh output folder was reserved.");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"shadow-harbor-results.json"),JsonUtility.ToJson(report,true));Debug.Log("BEACH_SHADOW_HARBOR_FINISHED passed="+report.passed+" checks="+report.checks.Count+" errors="+report.errors.Count+" output="+output);
            }
            catch(Exception error){report.passed=false;Debug.LogError("BEACH_SHADOW_HARBOR_REPORT_FAILED "+error);}
            SessionState.SetString(Key+"Phase","");SessionState.SetBool(Key+"Writable",false);int code=report.passed?0:1;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Detail(string message){if(current!=null)current.details.Add(message);}
        static string SceneFile()=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",ScenePath));
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static string Argument(string name){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:null;}
    }
}
