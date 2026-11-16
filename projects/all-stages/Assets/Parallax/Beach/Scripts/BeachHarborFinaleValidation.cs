using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Beach
{
    // Native-player focused test. The initial fourth-zone entry and an adversarial
    // real-deck pre-completion probe are explicit fixtures, never completion flags.
    // Island grabbing, perspective growth, release, settling, canopy, walking and
    // launch thereafter use production commands and actual PhysX contacts.
    public sealed class BeachHarborFinaleValidation : MonoBehaviour
    {
        const float Dt=1f/60;
        static bool claimed;
        BeachGame game;
        BeachHarborFinale finale;
        BeachSkyShip ship;
        BeachWater[] waters;
        string output,phase="boot";
        int frame,initialRespawns,shadeSamples,shipCollisionRaycasts,shipBoxRaycasts;
        SimulationMode previousSimulation;
        bool simulationChanged;
        float maxScale,maximumCameraStep,maximumRiderDrift,maximumTurn,maximumReconnectHeightRise,maximumReconnectDisplacement;
        readonly List<string> checks=new List<string>(),details=new List<string>(),errors=new List<string>(),trace=new List<string>();
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope,scene,unity,failure,observedPriorFailure;
            public string[] checks,details,errors,fixtures;
            public int frames,shadeSamples,shipCollisionRaycasts,shipBoxRaycasts,respawns;
            public float maximumIslandScale,maximumCameraStep,maximumRiderDrift,maximumTurn,maximumReconnectHeightRise,maximumReconnectDisplacement;
        }
        static bool Requested()=>Array.IndexOf(Environment.GetCommandLineArgs(),"-beach-harbor-finale-validate")>=0;
        static string Argument(string key)
        {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!Requested())return;
            var node=new GameObject("Beach harbor island finale native validation");DontDestroyOnLoad(node);
            node.AddComponent<BeachHarborFinaleValidation>();
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var loaded=FindFirstObjectByType<BeachGame>();if(loaded!=null)loaded.automated=true;
            Application.runInBackground=true;
        }
        void Require(bool condition,string text)
        {
            if(!condition)throw new InvalidOperationException(text+"; phase="+phase+"; feet="+(game==null?Vector3.zero:game.player.feet)
                +"; held="+(game==null||game.held==null?"none":game.held.title)+"; notice="+(game==null?"":game.notice));
            checks.Add(text);Debug.Log("HARBOR_FINALE_PASS "+text);
        }
        void Ensure(bool condition,string text){if(!condition)throw new InvalidOperationException(text+"; phase="+phase);}
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            if(!Requested()||claimed){Destroy(gameObject);yield break;}claimed=true;
            output=Path.GetFullPath(Argument("-validation-output")??Path.Combine(Application.persistentDataPath,"BeachHarborFinaleValidation"));
            Directory.CreateDirectory(output);string reportPath=Path.Combine(output,"harbor-finale-results.json");
            if(File.Exists(reportPath)){Debug.LogError("Refusing to overwrite an existing harbor finale report");Application.Quit(2);yield break;}
            Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=SceneLoaded;
            string failure=null;var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object item=null;
                try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}
                catch(Exception ex){failure=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(item is IEnumerator nested){stack.Push(nested);continue;}yield return item;
            }
            if(failure!=null&&game!=null)try{Capture("99-failure");}catch(Exception ex){details.Add("Failure capture unavailable: "+ex.Message);}
            if(simulationChanged)Physics.simulationMode=previousSimulation;
            SceneManager.sceneLoaded-=SceneLoaded;Application.logMessageReceived-=Log;Time.timeScale=1;
            var report=new Report{passed=failure==null&&errors.Count==0&&checks.Count>0,scope="Focused native fourth-zone regression. Public StartHarborFinaleDemo is an explicit initial entry fixture opening earlier gates/checkpoint3. One pre-completion actual-deck pose probe and the initial safe-workstation pose are declared fixtures. Thereafter no completion flags, held assignment, island pose/scale assignment, Ready setter, kinematic settling shortcut, player teleport or checkpoint injection is used: public camera pickup/cancel, native movement, perspective placement, Drop/PhysX, public miniature controls that physically clear placement space before reconnecting after gravity settling, production canopy, continuous shade sampling/cave walking, actual deck boarding and launch are exercised. Pause/ResetZone/ResetAll are real production commands. This is not an unassisted first-to-fourth-puzzle playthrough, sound audition or OS Escape-key delivery test.",
                observedPriorFailure="Native v1 aligned the piers before placement; the real collision resolver stopped at island(-2.10,.48,95.08), scale3.143 instead of(-3,0,101.5), scale4.5. This test now uses the player's public miniature controls to clear that physical space, then reconnect after actual gravity settling. No collision exemption or completed-state injection is added.",
                scene=SceneManager.GetActiveScene().name,unity=Application.unityVersion,failure=failure,checks=checks.ToArray(),details=details.ToArray(),errors=errors.ToArray(),
                fixtures=new[]{"Initial public fourth-zone entry command before interaction","Pre-completion real-deck contact probe at (.4,3.06,126.8), followed by safe-workstation entry at (0,0,77.6)"},
                frames=frame,shadeSamples=shadeSamples,shipCollisionRaycasts=shipCollisionRaycasts,shipBoxRaycasts=shipBoxRaycasts,respawns=game==null?0:game.respawns-initialRespawns,maximumIslandScale=maxScale,maximumCameraStep=maximumCameraStep,maximumRiderDrift=maximumRiderDrift,maximumTurn=maximumTurn,maximumReconnectHeightRise=maximumReconnectHeightRise,maximumReconnectDisplacement=maximumReconnectDisplacement};
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));File.WriteAllLines(Path.Combine(output,"harbor-finale-trace.csv"),trace);
            Debug.Log("HARBOR_FINALE_VALIDATION "+(report.passed?"PASSED":"FAILED")+" checks="+checks.Count+" output="+output);Application.Quit(report.passed?0:1);
        }
        IEnumerator Run()
        {
            if(SceneManager.GetActiveScene().name!="Beach")
            {
                Require(Application.CanStreamedLevelBeLoaded("Beach"),"Real Beach scene is included in the Tutorial entry build");
                yield return SceneManager.LoadSceneAsync("Beach",LoadSceneMode.Single);
            }
            yield return null;yield return null;
            game=FindFirstObjectByType<BeachGame>();Ensure(game!=null,"BeachGame is missing");
            finale=game.harborFinale;ship=game.skyShip;Ensure(finale!=null&&ship!=null&&finale.Island!=null,"Finale island/ship is missing");
            game.automated=true;Application.runInBackground=true;game.StartHarborFinaleDemo();game.Resume();
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;simulationChanged=true;
            waters=FindObjectsByType<BeachWater>(FindObjectsSortMode.None);initialRespawns=game.respawns;
            trace.Add("frame,phase,playerX,playerY,playerZ,islandX,islandY,islandZ,islandScale,held,settled,frozen,canopy,ready,flight,respawns");
            Require(game.castleRoute==null&&game.props.Length==9&&game.props.Contains(finale.Island),"Retired castle is replaced by one registered interactive island in the strict nine-prop reset registry");
            Require(!finale.Ready&&!finale.GeometryFrozen&&finale.CanopyProgress==0&&!ship.Flying,"Fresh finale begins unsolved, unfrozen, unrolled and moored");
            Require(finale.Piers.Length==2&&finale.BridgeModels.Length==2&&finale.CaveEntrances.Length==2&&finale.RouteWaypoints.Length>=4,"Both miniature controls, real piers and two cave mouths are present");
            Require(ship.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh!=null&&f.sharedMesh.isReadable),"Ship meshes are readable for real shared-mesh shadow queries");
            Require(ship.GetComponentsInChildren<MeshFilter>().All(f=>f.GetComponent<Collider>()!=null),"Visible ship structures retain real colliders, including masts and railings");
            CheckShipCollision();
            Require(!finale.SetModelControl(-1,Vector2.zero,0)&&!finale.SetModelControl(2,Vector2.zero,0),"Public miniature control rejects unknown indices");
            phase="adversarial real-deck pre-completion probe";game.PlacePlayer(new Vector3(.4f,3.06f,126.8f));yield return Frames(6);
            Require(ship.IsAboard,"Pre-completion gate probe is actually grounded on the real ship deck");
            Require(!ship.TryLaunch()&&!ship.Flying&&game.controller.enabled,"An actual aboard empty-handed player cannot launch before the finale is Ready");
            phase="safe workstation entry fixture";game.PlacePlayer(new Vector3(0,0,77.6f));yield return Frames(8);
            Require(game.FootSafe(game.player.feet),"Permanent workstation shade protects discovery and manipulation");
            Capture("00-first-vista");
            var island=finale.Island;Vector3 home=island.transform.position,homeScale=island.transform.localScale;
            int[] registry=game.props.Select(p=>p.GetInstanceID()).ToArray();
            phase="public miniature controls";
            Require(finale.SetModelControl(0,new Vector2(.8f,0),18),"The first public miniature control accepts a real offset and rotation");
            Require(finale.SetModelControl(1,new Vector2(-.6f,0),-12),"The second public miniature control is independent");
            yield return Frames(30);Require(!finale.Ready&&!finale.GeometryFrozen&&finale.CanopyProgress==0,"Misaligned piers and an unplaced island cannot start canopy deployment");
            Require(finale.SetModelControl(0,new Vector2(-3,0),-38)&&finale.SetModelControl(1,Vector2.zero,finale.pierData[1].homeYaw),
                "Public miniature controls move both real piers clear of the island placement space before enlargement");
            phase="close native approach and public pickup";yield return Travel(new Vector3(0,0,80.1f));
            AimAndInteract(island);Require(game.held==island,"Camera-ray public Interact grabs the near miniature island itself");
            Require(game.referenceDistance<1.35f,"Natural close pickup provides enough perspective ratio within this room: reference="+game.referenceDistance);
            yield return Frames(50);game.Cancel();yield return Frames(8);
            Require(game.held==null&&Vector3.Distance(island.transform.position,home)<.015f&&Vector3.Distance(island.transform.localScale,homeScale)<.001f,"Ordinary cancel restores the same miniature without solving or copying the island");
            AimAndInteract(island);Require(game.held==island,"The same original island can be grabbed again after ordinary cancel");
            phase="public perspective enlargement and release";bool released=false;
            // Only camera targets change. Pose, scale, held state and solver flags
            // are never assigned; every attempt runs the real perspective resolver.
            foreach(var offset in new[]{Vector3.zero,new Vector3(0,-.12f,0),new Vector3(0,.10f,0),new Vector3(0,0,.8f),new Vector3(0,0,-.8f)})
            {
                Vector3 desiredRoot=finale.IslandSolutionPosition+offset;
                Vector3 desiredAnchor=desiredRoot+game.heldRotation*(game.GrabLocalPoint*finale.IslandSolutionScale);
                Look(desiredAnchor);yield return Frames(100);maxScale=Mathf.Max(maxScale,island.transform.localScale.x);
                details.Add("Release desiredRoot="+desiredRoot+" desiredAnchor="+desiredAnchor+" grabLocalPoint="+game.GrabLocalPoint+" referenceDistance="+game.referenceDistance
                    +" referenceScale="+game.referenceScale+" actual="+island.transform.position+" scale="+island.transform.localScale.x+" rotation="+game.heldRotation.eulerAngles
                    +" eye="+game.view.transform.position+" placement="+game.invalidReason+" obstacle="+game.PlacementObstacle);
                if(Vector3.Distance(island.transform.position,finale.IslandSolutionPosition)<1.6f&&Mathf.Abs(island.transform.localScale.x-finale.IslandSolutionScale)<.8f&&game.Drop()){released=true;break;}
            }
            Require(released&&game.held==null&&maxScale>homeScale.x*10,"Public perspective placement enlarges and releases the original island at the harbor without pose injection");
            Capture("00-after-perspective-release");
            phase="actual island settling before pier reconnection";
            for(int i=0;i<600&&!island.placement.Ready;i++){Step();if(i%20==19)yield return null;}
            Require(island.placement.Ready&&!finale.GeometryFrozen&&!finale.Ready,"The freely released island settles under actual gravity before reconnecting the real piers");
            Vector3 restingIsland=island.transform.position;
            details.Add("Before pier reconnection: island="+restingIsland+" scale="+island.transform.localScale.x+" velocity="+island.body.linearVelocity+" colliderBottomY="+island.WorldBounds.min.y);
            Require(finale.SetModelControl(0,Vector2.zero,0)&&finale.SetModelControl(1,Vector2.zero,0),"Actual public miniature controls reconnect both piers to the physically settled island");
            phase="actual pier reconnection and canopy";bool intermediate=false,paused=false;float previous=0;
            for(int i=0;i<1000&&!finale.Ready;i++)
            {
                Step();maximumReconnectHeightRise=Mathf.Max(maximumReconnectHeightRise,island.transform.position.y-restingIsland.y);
                maximumReconnectDisplacement=Mathf.Max(maximumReconnectDisplacement,Vector3.Distance(restingIsland,island.transform.position));
                Ensure(finale.CanopyProgress+.0001f>=previous,"Canopy progress regressed without reset");previous=finale.CanopyProgress;
                if(finale.CanopyProgress>0&&finale.CanopyProgress<.98f)
                {
                    intermediate=true;Ensure(finale.GeometryFrozen&&!finale.Ready,"Incomplete canopy must lock geometry but keep departure disabled");
                    Ensure(!ship.TryLaunch(),"Incomplete canopy allowed departure");
                    if(!paused)
                    {
                        float frozen=finale.CanopyProgress;Vector3 position=island.transform.position;game.Pause();game.Tick(.5f,Vector2.zero);
                        Require(!game.active&&Time.timeScale==0&&finale.CanopyProgress==frozen&&island.transform.position==position,"Pause freezes real canopy deployment and leaves the ordinary pause state available");
                        game.Resume();paused=true;
                    }
                }
                if(i%20==19)yield return null;
            }
            details.Add("After real pier reconnection: island="+island.transform.position+" scale="+island.transform.localScale.x+" maximumHeightRise="+maximumReconnectHeightRise+" maximumDisplacement="+maximumReconnectDisplacement
                +" placementReady="+island.placement.Ready+" connections="+string.Join("/",Array.ConvertAll(finale.ConnectionErrors,v=>v.ToString("F4",CultureInfo.InvariantCulture)))+" routeFailure="+finale.LastRouteFailure+" shadeFailure="+finale.LastShadeFailure);
            Require(island.placement.Ready&&finale.GeometryFrozen&&finale.Ready&&finale.CanopyProgress>=.999f,"Gravity-settled island and real aligned piers complete the full canopy before Ready");
            Require(intermediate&&paused,"Canopy has observable staged deployment and pausing, rather than an instant safety flag");
            details.Add("Final physical route: island="+island.transform.position+" colliderBottomY="+island.WorldBounds.min.y+" kinematicAfterLatch="+island.body.isKinematic+" caveMouths="+string.Join("/",Array.ConvertAll(finale.CaveEntrances,p=>p.ToString("F4"))));
            Require(!finale.SetModelControl(0,new Vector2(1,0),25)&&!finale.SetModelControl(1,new Vector2(1,0),25),"Completed geometry freezes both miniature controls");
            Require(registry.SequenceEqual(game.props.Select(p=>p.GetInstanceID()))&&game.props.Count(p=>p==island)==1,"The miniature becomes the traversed island without spawning a second interactive copy or changing the reset registry");
            Require(finale.CanopyRenderers.Length>0&&finale.CanopyRenderers.All(r=>r!=null&&r.enabled&&r.gameObject.activeInHierarchy),"Ready corresponds to actual deployed visible cloth renderers");
            phase="continuous real rendered shadow samples";CheckRouteShade();Capture("01-completed-real-harbor");
            Require(!ship.IsAboard&&!ship.TryLaunch(),"Even after solving, a shore-side player cannot launch remotely");
            phase="continuous cave and pier walking";
            foreach(var point in finale.RouteWaypoints)
            {
                yield return Travel(point,1000);
                details.Add("Actual route waypoint reached: target="+point+" feet="+game.player.feet+" grounded="+game.player.grounded+" footSafe="+game.FootSafe(game.player.feet));
            }
            Require(game.respawns==initialRespawns,"Actual continuous pier/cave/pier walking preserves heat and water safety");
            foreach(var mouth in finale.CaveEntrances)
                details.Add("Actual cave mouth="+mouth);
            Capture("02-route-at-final-gangway");
            phase="actual final-deck boarding";yield return Travel(new Vector3(.4f,3.06f,126.8f),300);
            RecordBoardContact("before precise boarding");game.yaw=0;game.SyncView();
            // Ordinary waypoint tolerance can stop at z126.69, still on the
            // static gangway ending126.7. Walk farther through the real motor;
            // never substitute distance for the production deck-owner ray.
            for(int i=0;i<120&&!ship.IsAboard;i++)
            {
                Step(Vector2.up*.2f);if(i%20==19){RecordBoardContact("precise boarding step "+(i+1));yield return null;}
            }
            RecordBoardContact("after precise boarding");
            Require(ship.IsAboard&&game.held==null&&!game.won,"The complete route ends with real deck contact and an empty-handed player");
            Vector3 shipHome=ship.transform.position,scaleHome=ship.transform.localScale;float fogHome=RenderSettings.fogDensity,farHome=game.view.farClipPlane;
            var ocean=game.transform.Find("수평선");Vector3 oceanHome=ocean==null?Vector3.zero:ocean.localScale;
            Require(ship.TryLaunch()&&ship.Flying&&!game.controller.enabled,"Ready, real boarding and public E-equivalent TryLaunch begin the actual flight");
            Require(!ship.TryLaunch(),"Duplicate departure requests are rejected");Vector3 rider=ship.transform.InverseTransformPoint(game.player.feet);
            phase="real twenty-four-second flight";bool flightPaused=false,lookChecked=false,departed=false;float previousFade=0;bool continuous=true,fadeMonotonic=true;
            for(int i=0;i<1460&&!game.won;i++)
            {
                Vector3 previousPosition=ship.transform.position,previousCamera=game.view.transform.position;Quaternion previousRotation=ship.transform.rotation;
                Step();maximumCameraStep=Mathf.Max(maximumCameraStep,Vector3.Distance(previousCamera,game.view.transform.position));
                maximumRiderDrift=Mathf.Max(maximumRiderDrift,Vector3.Distance(rider,ship.transform.InverseTransformPoint(game.player.feet)));
                maximumTurn=Mathf.Max(maximumTurn,Quaternion.Angle(previousRotation,ship.transform.rotation));
                continuous&=Vector3.Distance(previousPosition,ship.transform.position)<.7f&&!float.IsNaN(game.view.transform.rotation.x);
                fadeMonotonic&=ship.EndingFade+.0001f>=previousFade;previousFade=ship.EndingFade;departed|=ship.Departing;
                if(!lookChecked&&ship.FlightTime>5)
                {
                    var before=game.view.transform.rotation;float yaw=game.yaw;game.yaw-=Mathf.PI*.5f;game.SyncView();
                    Require(Quaternion.Angle(before,game.view.transform.rotation)>60,"Production mouse-look yaw remains independent during flight");game.yaw=yaw;game.SyncView();lookChecked=true;
                }
                if(!flightPaused&&ship.FlightTime>17)
                {
                    Vector3 position=ship.transform.position,view=game.view.transform.position;float time=ship.FlightTime,fade=ship.EndingFade;
                    game.Pause();game.Tick(1,Vector2.zero);
                    Require(!game.active&&Time.timeScale==0&&ship.transform.position==position&&game.view.transform.position==view&&ship.FlightTime==time&&ship.EndingFade==fade,"Ordinary pause freezes ship, detached camera, flight clock and ending fade");
                    game.Resume();flightPaused=true;Capture("03-flight-departure-view");
                }
                if(i%20==19)yield return null;
            }
            Require(continuous&&maximumTurn<3&&maximumCameraStep<3&&maximumRiderDrift<.004f,"Flight, camera and carried rider remain continuous: turn="+maximumTurn+" camera="+maximumCameraStep+" rider="+maximumRiderDrift);
            Require(lookChecked&&flightPaused&&departed&&fadeMonotonic,"Flight includes free look, pause, external departure camera and monotonic ending fade");
            Require(game.won&&!game.active&&ship.FlightTime==BeachSkyShip.FlightDuration&&ship.transform.position.y>90&&ship.EndingFade==1&&ship.EndingTitleAlpha==1,"Actual 24-second sky flight ends with paused END fade/title");
            phase="production full reset";game.ResetAll();
            Require(!game.won&&!ship.Flying&&game.controller.enabled&&!finale.Ready&&!finale.GeometryFrozen&&finale.CanopyProgress==0,"Real ResetAll restores controller, ship, island, miniature controls and canopy completion state");
            Require(Vector3.Distance(ship.transform.position,shipHome)<.001f&&ship.transform.localScale==scaleHome&&RenderSettings.fogDensity==fogHome&&game.view.farClipPlane==farHome&&(ocean==null||ocean.localScale==oceanHome),"Reset restores departure camera environment, horizon and the original moored ship");
            Require(Vector3.Distance(island.transform.position,island.homePosition)<.001f&&Vector3.Distance(island.transform.localScale,island.homeScale)<.001f&&game.checkpoint==0,"Full reset returns the same island miniature and player to the first beach zone");
            Require(!ship.TryLaunch(),"Reset cannot leave a stale departure latch");
        }
        void Step(Vector2 move=default)
        {
            game.Tick(Dt,move);foreach(var water in waters)water.Simulate(Dt);Physics.Simulate(Dt);Physics.SyncTransforms();
            foreach(var prop in game.props)if(prop.gameObject.activeInHierarchy)prop.placement.Tick(Dt);
            game.lighting.Synchronize();frame++;
            Ensure(game.respawns==initialRespawns,"Unexpected heat/water recovery interrupted native validation at "+game.player.feet);
            if(frame%30==0)trace.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9},{10},{11},{12:F4},{13},{14:F4},{15}",
                frame,phase,game.player.feet.x,game.player.feet.y,game.player.feet.z,finale.Island.transform.position.x,finale.Island.transform.position.y,finale.Island.transform.position.z,
                finale.Island.transform.localScale.x,game.held!=null,finale.Island.placement.Ready,finale.GeometryFrozen,finale.CanopyProgress,finale.Ready,ship.FlightTime,game.respawns));
        }
        IEnumerator Frames(int count){for(int i=0;i<count;i++){Step();if(i%20==19)yield return null;}}
        IEnumerator Travel(Vector3 target,int limit=500)
        {
            Vector3 start=game.player.feet;
            for(int i=0;i<limit;i++)
            {
                Vector2 delta=new Vector2(target.x-game.player.feet.x,target.z-game.player.feet.z);
                if(delta.magnitude<.13f&&Mathf.Abs(target.y-game.player.feet.y)<.24f&&game.player.grounded)yield break;
                if(delta.magnitude>.04f)game.yaw=Mathf.Atan2(delta.x,delta.y);
                Step(delta.magnitude<.04f?Vector2.zero:delta.normalized*Mathf.Sqrt(Mathf.Clamp01(delta.magnitude/.6f)));
                if(i%20==19)yield return null;
            }
            throw new InvalidOperationException("Native walk blocked from "+start+" to "+target+" actual="+game.player.feet+" phase="+phase);
        }
        void Look(Vector3 point)
        {Vector3 d=point-game.view.transform.position;game.yaw=Mathf.Atan2(d.x,d.z);game.pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude);game.SyncView();}
        void AimAndInteract(BeachProp target)
        {
            foreach(var part in target.parts.OrderBy(p=>Vector3.Distance(p.bounds.center,game.view.transform.position)))
            {
                if(!part.enabled)continue;Look(part.bounds.center);game.UpdateAim();if(game.aimed==target){game.Interact();return;}
            }
            throw new InvalidOperationException("Actual camera cannot target "+target.title+" from "+game.view.transform.position);
        }
        void RecordBoardContact(string label)
        {
            Vector3 origin=game.player.feet+Vector3.up*.18f;
            bool hit=Physics.Raycast(origin,Vector3.down,out var floor,.5f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            var owner=hit?floor.collider.GetComponentInParent<BeachSkyShip>():null;
            string path="none";
            if(hit){path=floor.collider.name;for(var t=floor.collider.transform.parent;t!=null;t=t.parent)path=t.name+"/"+path;}
            details.Add(label+": feet="+game.player.feet.ToString("F5")+" grounded="+game.player.grounded+" controller="+game.controller.enabled+" IsAboard="+ship.IsAboard
                +" floor="+path+" colliderId="+(hit?floor.collider.GetInstanceID():0)+" owner="+(owner==null?"static/non-ship":owner.name)
                +" point="+(hit?floor.point:Vector3.zero).ToString("F5")+" normal="+(hit?floor.normal:Vector3.zero).ToString("F5")+" distance="+(hit?floor.distance:-1));
        }
        void CheckShipCollision()
        {
            foreach(var source in ship.GetComponentsInChildren<MeshFilter>())
            {
                var physical=source.GetComponent<Collider>();
                Ensure(physical!=null&&physical.enabled&&!physical.isTrigger,"Visible ship mesh lacks enabled non-trigger collision: "+source.name);
                // PR42 deliberately uses authored box colliders on nine simple
                // structures and triangle meshes on the remaining structures.
                // Preserve that collision design and exercise both natively.
                if(physical is BoxCollider box)
                {
                    Vector3 face=box.transform.TransformPoint(box.center+Vector3.up*box.size.y*.5f),up=box.transform.up;
                    Ensure(box.Raycast(new Ray(face+up*.06f,-up),out var boxHit,.12f)&&boxHit.collider==box,"Native ray misses an authored ship box surface: "+source.name);shipBoxRaycasts++;continue;
                }
                var collider=physical as MeshCollider;
                Ensure(collider!=null&&collider.sharedMesh==source.sharedMesh,"Ship mesh collider does not use its rendered source: "+source.name);
                var vertices=source.sharedMesh.vertices;var indices=source.sharedMesh.triangles;float best=0;Vector3 center=Vector3.zero,normal=Vector3.zero;
                for(int i=0;i+2<indices.Length;i+=3)
                {
                    Vector3 a=source.transform.TransformPoint(vertices[indices[i]]),b=source.transform.TransformPoint(vertices[indices[i+1]]),c=source.transform.TransformPoint(vertices[indices[i+2]]);
                    Vector3 cross=Vector3.Cross(b-a,c-a);float area=cross.sqrMagnitude;if(area<=best)continue;
                    best=area;center=(a+b+c)/3;normal=cross;
                }
                Ensure(best>1e-14f,"Visible ship mesh has no nondegenerate native collision triangle: "+source.name);normal.Normalize();
                Ensure(collider.Raycast(new Ray(center+normal*.06f,-normal),out var hit,.12f)&&hit.collider==collider,"Native collider ray misses the actual source surface: "+source.name);shipCollisionRaycasts++;
            }
            Require(shipCollisionRaycasts>0&&shipBoxRaycasts>0,"Actual native rays hit every visible ship structure: triangle meshes="+shipCollisionRaycasts+" authored boxes="+shipBoxRaycasts);
        }
        void CheckRouteShade()
        {
            var points=finale.RouteWaypoints;
            for(int i=0;i+1<points.Length;i++)
            {
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[i],points[i+1])/.22f));
                for(int j=0;j<=count;j++)
                {
                    Vector3 point=Vector3.Lerp(points[i],points[i+1],(float)j/count);
                    var hits=Physics.RaycastAll(point+Vector3.up*.4f,Vector3.down,1.1f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                        .Where(h=>h.collider!=game.controller&&PlacementSupport.IsUsableGround(h.collider)&&h.normal.y>.5f).OrderBy(h=>h.distance).ToArray();
                    Ensure(hits.Length>0,"Visible route has no real supporting surface at "+point);
                    Vector3 feet=hits[0].point+Vector3.up*.025f;Ensure(game.FootSafe(feet),"Actual rendered-mesh shade has a gap at "+feet+" segment="+i);
                    Ensure(!BeachWater.InHazard(feet),"Route support remains in a water hazard at "+feet);shadeSamples++;
                }
            }
            Require(shadeSamples>40,"Dense real five-foot shadow/water/support samples cover the complete route, cave and joins: "+shadeSamples);
        }
        void Capture(string name)
        {
            var camera=game.view;var oldTarget=camera.targetTexture;var previous=RenderTexture.active;var texture=new RenderTexture(1280,720,24);Texture2D pixels=null;
            try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=previous;texture.Release();Destroy(texture);if(pixels!=null)Destroy(pixels);}
        }
    }
}
