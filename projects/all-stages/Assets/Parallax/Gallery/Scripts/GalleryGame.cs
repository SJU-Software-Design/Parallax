using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryGame : MonoBehaviour, IObjectInteractionHost<GalleryProp>
    {
        public const string Version="0.25.0";
        public bool conceptLayout, exhibitionPreview;
        public bool conceptCapture;
        static readonly Vector3[] ConceptCheckpoints={new Vector3(0,0,3),new Vector3(0,0,16.8f),new Vector3(0,0,31),new Vector3(0,0,45)};
        public Vector3[] SpawnPoints => conceptLayout?ConceptCheckpoints:GalleryWorld.Checkpoints;
        float WorldHalfWidth => conceptLayout?GalleryConceptWorld.Width/2:GalleryWorld.Width/2;
        float WorldLength => conceptLayout?GalleryConceptWorld.Length:GalleryWorld.Length;
        float WorldRoof => conceptLayout?GalleryConceptWorld.Roof:GalleryLighting.Roof;
        public const float Speed=FirstPersonMotor.Speed, Gravity=FirstPersonMotor.Gravity, JumpHeight=FirstPersonMotor.JumpHeight, Reach=InteractionRaycast.MaximumDistance;
        public CharacterController controller;
        public Camera view;
        public GalleryLighting lighting;
        public GalleryProp[] props;
        public Font font;
        public GalleryProp held, aimed;
        public GalleryMirror mirror, aimedMirror;
        public GalleryLightGate lightGate;
        public bool adjustingMirror;
        public Player player;
        public float yaw, pitch=-.08f, exposure, elapsed, verticalVelocity;
        public float holdDistance { get => Interaction.Depth; private set => Interaction.Depth=value; }
        public float referenceDistance => Interaction.ReferenceDistance;
        public float referenceScale => Interaction.ReferenceScale;
        public bool HasPlacementSurface { get; private set; }
        public bool PlacementBlocked { get; private set; }
        public string PlacementObstacle { get; private set; }
        public Quaternion heldRotation { get => Interaction.Rotation; set => Interaction.Rotation=value; }
        public bool active, started, won, safe=true, invalid, automated, showHints;
        public bool visitedUpper;
        public int checkpoint, respawns, grabs, drops;
        public string notice, invalidReason;
        public float noticeUntil;
        public PlacementLesson lesson;
        public GalleryOrcusExit orcusExit;
        public GalleryStageTransition stageTransition;
        public GalleryMuseumShadowCourse shadowCourse;
        public GalleryPortraitGate portraitGate;
        public GalleryNikeLight nikeLight;
        float sunExposureTime;
        public string RoomTitle => conceptLayout?new[]{"01  명화 전시실","02  빛과 그림자","03  시점의 방","04  성당 · 두 손 사이의 문"}[checkpoint]:GalleryWorld.Titles[checkpoint];
        public bool InShadowCourse => conceptLayout&&player!=null&&player.feet.z<29;
        public bool MovableNike => nikeLight!=null&&nikeLight.statue!=null;
        public bool NikeCanvasPuzzle => nikeLight!=null&&nikeLight.canvas!=null;
        public bool NikeKeyPuzzle => shadowCourse!=null&&shadowCourse.nikeSocket!=null;
        public string NikeSocketHint => NikeKeyPuzzle&&held==shadowCourse.nikeSocket.canvas.portablePainting&&
            shadowCourse.nikeSocket.Targeted(view)?shadowCourse.nikeSocket.FitHint(held)??"크기가 맞아요 · 왼쪽 클릭으로 액자를 끼우세요.":null;
        public string Objective => checkpoint==0?"청동 조형물을 키워 그늘로 빛 구간을 건너세요":checkpoint==1?
            (NikeCanvasPuzzle&&nikeLight.canvas.Drawing?"붓결을 따라 니케의 그림이 완성되고 있어요":
                NikeCanvasPuzzle&&nikeLight.canvas.Fusing?"니케가 그림으로 변하고 있어요":
                NikeKeyPuzzle&&shadowCourse.NikeUnlocked?"열린 아치문으로 이동하세요":
                NikeKeyPuzzle&&nikeLight.canvas.Complete?"액자 크기를 맞춰 문 중앙의 홈에 끼우세요":
                NikeCanvasPuzzle&&nikeLight.canvas.portablePainting!=null&&nikeLight.canvas.Complete&&
                !nikeLight.canvas.shadeMarks.activeSelf?"그림을 들어 문 앞의 빛을 가리세요":
                shadowCourse!=null&&shadowCourse.NikeUnlocked?"발자국을 따라 열린 문으로 이동하세요":
                NikeCanvasPuzzle&&!nikeLight.LightDiscovered?"빈 캔버스와 주변 전시물을 살펴보세요":
                NikeCanvasPuzzle?"니케의 그림자를 벽 액자의 윤곽에 맞추세요":
                MovableNike?"니케를 옮겨 날개 그림자를 문 앞까지 보내세요":"전시 조명을 옮겨 니케의 그림자를 문 앞까지 보내세요"):checkpoint==2?
            (portraitGate!=null&&(portraitGate.Performing||portraitGate.Opened)?portraitGate.Status:"양쪽 벽의 작품을 보고 눈·코·입으로 얼굴을 만들어 보세요"):
            (orcusExit!=null&&orcusExit.IsReadyForEntry?"계단을 올라 두 손 사이의 도서관으로 들어가세요":
             orcusExit!=null&&orcusExit.cathedralActivation!=null?orcusExit.cathedralActivation.Status:
                "손 사이에 다른 공간이 보이는 조각을 확대해 내려놓으세요");
        public string RoomHint => checkpoint==0?
            "접힌 청동판을 잡고, 먼 바닥을 향해 크기를 키워 보세요.\n빛 구간 앞에 내려놓고 넓어진 그늘로 건넙니다.\n낮은 받침의 옆으로 돌아 그늘에 들어가세요.":checkpoint==1?
            (NikeCanvasPuzzle&&!nikeLight.LightDiscovered?"캔버스에는 니케와 닮은 윤곽만 남아 있어요.\n주변 전시물도 집어서 옮길 수 있습니다.\n전시물의 받침 주변을 조금 더 살펴보세요.":
                NikeKeyPuzzle?"니케의 그림자 전체를 금색 윤곽 안에 넣고 내려놓으세요.\n붓질로 완성된 그림이 튀어나오면 집어 드세요.\n문 중앙의 홈을 보고 앞뒤로 움직여 크기를 맞춘 뒤\n클릭하면 액자가 갈라지며 문이 열려요.":
                NikeCanvasPuzzle?"니케를 그림과 합친 뒤 액자를 들어 빛을 가리세요.":
                MovableNike?"니케를 옮겨 문 앞 표시를 그림자로 덮으세요.\n문이 열리면 출구 앞의 빛을 피해 건너세요.":
                "전시 조명을 옮기면 니케가 만드는 그림자가 달라집니다.\n전원 구역에 내려놓아 문 앞 표시를 그늘로 덮어 보세요."):checkpoint==2?
            "왼쪽의 원작과 오른쪽의 입체 전시를 살펴보세요.\n눈은 위에, 코는 가운데, 입은 아래에 놓습니다.\n작품을 내려놓고 얼굴을 잠깐 바라보면\n조각들이 커튼에 스며들고, 웃는 얼굴이 천천히 길을 열어요.":
            "두 손 사이에 작은 도서관이 보입니다.\n조각을 키워 '충분히 커졌어요' 안내가 나오면 내려놓으세요.\n마지막 크기와 약간의 기울기는 자동으로 맞춰 줍니다. 손이 열리면 계단을 올라 들어가세요.";
        public bool Transitioning => stageTransition!=null&&stageTransition.IsTransitioning;
        public bool PortraitPerforming => portraitGate!=null&&portraitGate.Performing;
        public Vector3 GrabLocalPoint => Interaction.GrabLocalPoint;
        public Vector3 HeldDirection => Interaction.HeldDirection;
        public PerspectivePlacement.Result PlacementDiagnostic => Interaction.LastPlacement;
        public bool ProjectionApplied => Interaction.ProjectionApplied;
        public float PlayerOverlapReduction => Interaction.OverlapReduction;
        readonly List<Vector3> placementSamples=new List<Vector3>();
        readonly RaycastHit[] placementHits=new RaycastHit[64];
        struct HoldShape { public Vector3 center,half; public Quaternion rotation; }
        struct HoldPose { public Vector3 position,direction; public Quaternion rotation; public float depth,scale; }
        readonly List<HoldShape> holdShapes=new List<HoldShape>();
        bool initialPlacement;
        PlayerMovementAudio movementAudio;
        MuseumAudio museumAudio;
        readonly FirstPersonMotor motor=new FirstPersonMotor { GroundFilter=PlacementSupport.IsUsableGround };
        readonly PlayerControls controls=new PlayerControls();
        bool gameJumpHeld, pendingDrop;

        void Awake()
        {
            FirstPersonMotor.Configure(controller,view);movementAudio=PlayerMovementAudio.Attach(controller);PlayerControls.ConfigureCamera(view);
            museumAudio=MuseumAudio.Install(gameObject,view.transform);
            var arguments=Environment.GetCommandLineArgs();
            bool mirrorTest=Array.IndexOf(arguments,"-gallery-mirror-test")>=0;
            bool placementTest=Array.IndexOf(arguments,"-placement-test")>=0;
            bool museumTest=Array.IndexOf(arguments,"-gallery-museum-test")>=0;
            bool conceptInteractionTest=Array.IndexOf(arguments,"-gallery-concept-interaction-test")>=0;
            bool mechanicsTest=Array.IndexOf(arguments,"-gallery-mechanics-test")>=0;
            bool cathedralTest=Array.IndexOf(arguments,"-gallery-cathedral-test")>=0;
            bool comfortTest=Array.IndexOf(arguments,"-gallery-comfort-test")>=0;
            bool shadowGameplayTest=Array.IndexOf(arguments,"-gallery-shadow-gameplay-test")>=0;
            bool portraitPerformanceTest=Array.IndexOf(arguments,"-gallery-portrait-performance-test")>=0;
            bool selectedAudioTest=Array.IndexOf(arguments,"-gallery-selected-audio-test")>=0;
            bool cathedralRumbleTest=Array.IndexOf(arguments,"-gallery-cathedral-rumble-test")>=0;
            bool nikeDiscoveryTest=Array.IndexOf(arguments,"-gallery-nike-discovery-test")>=0;
            conceptCapture=Array.IndexOf(arguments,"-gallery-concept-capture")>=0;
            automated=selectedAudioTest||nikeDiscoveryTest||cathedralRumbleTest||portraitPerformanceTest||shadowGameplayTest||comfortTest||cathedralTest||mechanicsTest||conceptCapture||conceptInteractionTest||placementTest||mirrorTest||museumTest||Array.IndexOf(arguments,"-gallery-perspective-test")>=0;
            Application.runInBackground=automated;Application.targetFrameRate=automated?90:-1;
            QualitySettings.vSyncCount=automated?0:1;QualitySettings.antiAliasing=2;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","AppleGothic","Arial"},32);
#else
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Arial"},32);
#endif
            player=new Player(new Vector2(controller.transform.position.x,controller.transform.position.z));
            player.feet=controller.transform.position;
            foreach(var prop in props){prop.InitializePhysics();prop.placement.SetPlayer(controller);museumAudio.RegisterProp(prop.body,prop.title);}
            foreach(var prop in props)prop.placement.ResetPlaced();
            nikeLight=GetComponent<GalleryNikeLight>();lighting.Rebuild();SyncView();
            orcusExit=GetComponentInChildren<GalleryOrcusExit>();
            shadowCourse=GetComponentInChildren<GalleryMuseumShadowCourse>();portraitGate=GetComponentInChildren<GalleryPortraitGate>();
            if(portraitGate!=null)
            {
                portraitGate.AbsorptionStarted+=OnPortraitAbsorption;
                portraitGate.ReddeningStarted+=OnPortraitReddening;
                portraitGate.PerformanceReset+=OnPortraitReset;
            }
            if(orcusExit!=null)orcusExit.Entered+=BeginOrcusTransition;
            gameObject.AddComponent<GalleryHud>().game=this;
            gameObject.AddComponent<HeldPlacementDiagnosticsOverlay>().game=this;
            lesson=gameObject.AddComponent<PlacementLesson>();lesson.Configure(font,()=>active&&!won&&(!conceptLayout||checkpoint==0),()=>held==null?null:held.placement,()=>player.feet,()=>drops,()=>safe,GalleryLighting.ToSun);
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            Time.timeScale=0;
            if(selectedAudioTest)new GameObject("Museum selected audio validation").AddComponent<GallerySelectedAudioValidation>().game=this;
            else if(nikeDiscoveryTest)new GameObject("Nike light discovery validation").AddComponent<GalleryNikeDiscoveryValidation>().game=this;
            else if(cathedralRumbleTest)new GameObject("Cathedral hand opening and rumble validation").AddComponent<GalleryCathedralRumbleValidation>().game=this;
            else if(portraitPerformanceTest)new GameObject("Portrait curtain performance validation").AddComponent<GalleryPortraitPerformanceValidation>().game=this;
            else if(shadowGameplayTest)new GameObject("Museum shadow gameplay validation").AddComponent<GalleryShadowGameplayValidation>().game=this;
            else if(comfortTest)new GameObject("Museum comfort validation").AddComponent<GalleryComfortValidation>().game=this;
            else if(cathedralTest)new GameObject("Cathedral portal validation").AddComponent<GalleryCathedralValidation>().game=this;
            else if(mechanicsTest)new GameObject("Museum mechanics validation").AddComponent<GalleryMuseumMechanicsValidation>().game=this;
            else if(conceptCapture)gameObject.AddComponent<GalleryConceptCapture>().game=this;
            else if(conceptInteractionTest)gameObject.AddComponent<GalleryConceptInteractionValidation>().game=this;
            else if(placementTest)gameObject.AddComponent<PlacementValidation>();
            else if(mirrorTest)gameObject.AddComponent<GalleryMirrorValidation>().game=this;
            else if(museumTest)new GameObject("Museum runtime validation").AddComponent<GalleryMuseumValidation>().game=this;
            else if(automated)gameObject.AddComponent<GalleryPerspectiveValidation>().game=this;
        }
        void OnDisable() { if(held!=null&&interaction!=null)interaction.Cancel(true);museumAudio?.SetGameplayRunning(false); }
        void OnDestroy()
        {
            if(portraitGate!=null)
            {
                portraitGate.AbsorptionStarted-=OnPortraitAbsorption;
                portraitGate.ReddeningStarted-=OnPortraitReddening;
                portraitGate.PerformanceReset-=OnPortraitReset;
            }
            Time.timeScale=1;
        }
        void OnPortraitAbsorption(){museumAudio?.SetPortraitSilence(true);}
        void OnPortraitReddening(){MuseumAudio.TryPlay("face.smile",portraitGate,portraitGate.transform.position);}
        void OnPortraitReset(){MuseumAudio.StopOwner(portraitGate);museumAudio?.SetPortraitSilence(false);}
        public void Tell(string text,float duration=4) { notice=text;noticeUntil=Time.unscaledTime+duration; }
        public void Resume()
        {
            if(Transitioning)return;
            if(won)ResetAll();ClearControlInput();PlayerControls.ConfigureCamera(view);active=started=true;Time.timeScale=1;
            museumAudio?.SetCheckpoint(checkpoint);museumAudio?.SetGameplayRunning(true);
            if(!automated)MuseumAudio.TryPlay("ui.confirm",this,view.transform.position);
            if(!automated){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        public void Pause() { if(Transitioning)return;ClearControlInput();active=false;Time.timeScale=0;museumAudio?.SetGameplayRunning(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        void ClearControlInput()
        {
            motor.ClearInput();controls.Reset();pendingDrop=false;gameJumpHeld=false;
        }
        void ResetControlState()
        {
            motor.Reset();movementAudio?.Reset();controls.Reset();pendingDrop=false;gameJumpHeld=false;verticalVelocity=0;
        }
        void OnApplicationFocus(bool focused) { if(!focused&&!automated&&!Transitioning)Pause(); }
        public void ResetAll()
        {
            if(held!=null)Cancel(true);foreach(var p in props)p.Restore();
            adjustingMirror=false;if(mirror!=null)mirror.Restore();if(lightGate!=null)lightGate.Restore();
            checkpoint=respawns=grabs=drops=0;elapsed=exposure=sunExposureTime=0;won=visitedUpper=false;lesson?.Restart();
            orcusExit?.ResetEntrance();
            orcusExit?.cathedralActivation?.ResetToRest();
            PlacePlayer(SpawnPoints[0]);yaw=0;pitch=-.08f;lighting.Synchronize();SyncView();
            shadowCourse?.ResetProgress();portraitGate?.ResetGate();nikeLight?.ResetLight();
            museumAudio?.ResetPlayback();
        }
        public void ResetZone()
        {
            if(held!=null)Cancel(true);
            foreach(var p in props) if(p.zone==checkpoint)p.Restore();
            if(checkpoint==3){orcusExit?.ResetEntrance();orcusExit?.cathedralActivation?.ResetToRest();}
            adjustingMirror=false;if(checkpoint==1){if(mirror!=null)mirror.Restore();if(lightGate!=null)lightGate.Restore();}
            PlacePlayer(SpawnPoints[checkpoint]);exposure=sunExposureTime=0;lighting.Synchronize();nikeLight?.ResetLight();
            shadowCourse?.ResetProgress(checkpoint);if(checkpoint==2)portraitGate?.ResetGate();
            Tell("현재 구역의 전시물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
        }
        public void StartMirrorDemo()
        {
            if(conceptLayout){StartConceptDemo(1);return;}
            ResetAll();checkpoint=1;PlacePlayer(SpawnPoints[1]);Resume();
            Tell(exhibitionPreview?"니케 전시실 · 실물 스캔과 채광을 둘러보세요.":"거울 전시실 체험 · 거울 클릭 후 Q/E로 회전 · H 힌트",7);
        }
        public void StartOrcusDemo()
        {
            if(conceptLayout){StartConceptDemo(3);return;}
            if(exhibitionPreview){Tell("출구 조각은 실제 모델을 준비하고 있습니다.");return;}
            ResetAll();checkpoint=3;visitedUpper=true;
            PlacePlayer(GalleryWorld.OrcusDemoStart);yaw=0;pitch=-.08f;SyncView();Resume();
            Tell("오르쿠스 전시 · 가까이에서 바람 소리를 듣고, 조각을 키워 입 안으로 들어가 보세요. H 힌트",9);
        }
        public void StartConceptDemo(int zone)
        {
            ResetAll();checkpoint=Mathf.Clamp(zone,0,3);
            bool nikeDemo=checkpoint==1&&NikeCanvasPuzzle;
            PlacePlayer(nikeDemo?GalleryNikeCanvas.DemoPosition:checkpoint==3?new Vector3(0,0,49.1f):
                checkpoint==2?new Vector3(0,0,30.2f):SpawnPoints[checkpoint]);
            yaw=0;pitch=0;SyncView();Resume();Tell("구역 바로 체험 · H 힌트 · R 현재 구역 초기화 · Shift+R 처음부터",7);
        }
        public void BeginOrcusTransition(GalleryOrcusExit entrance)
        {
            if(Transitioning||!active||won)return;
            if(held!=null)Cancel(true);
            ClearControlInput();adjustingMirror=false;
            entrance.cathedralActivation?.ClearRumble();
            var runner=new GameObject("Museum to library passage");
            stageTransition=runner.AddComponent<GalleryStageTransition>();
            if(!stageTransition.Begin(this,entrance))
            {
                entrance.ResetEntrance();Destroy(runner);
                Tell("도서관 연결을 준비할 수 없습니다. 미술관 최신 실행본을 다시 빌드해 주세요.",8);return;
            }
            active=false;won=true;Time.timeScale=1;
            museumAudio?.SetGameplayRunning(false);
        }
        public void PlacePlayer(Vector3 point)
        {
            ResetControlState();
            controller.enabled=false;controller.transform.position=point+Vector3.up*.025f;controller.enabled=true;
            player.feet=controller.transform.position;player.grounded=false;
            Physics.SyncTransforms();SyncView();
        }
        public void Respawn()
        {
            if(held!=null)Cancel(true);
            adjustingMirror=false;
            var spawn=SpawnPoints[checkpoint];
            if(!CanStand(spawn))
            {
                bool found=false;
                for(float r=.5f;r<5&&!found;r+=.5f)for(int a=0;a<16;a++)
                {
                    Vector3 p=spawn+new Vector3(Mathf.Cos(a*Mathf.PI/8),0,Mathf.Sin(a*Mathf.PI/8))*r;
                    if(CanStand(p)&&FootSafe(p)){spawn=p;found=true;break;}
                }
                if(!found)foreach(var p in props)if(p.zone==checkpoint)p.Restore();
            }
            PlacePlayer(spawn);exposure=sunExposureTime=0;respawns++;Tell("빛에 노출되어 현재 구역의 쉼터로 돌아왔습니다.");
        }
        public bool CanStand(Vector3 feet)
        {
            foreach(var hit in Physics.OverlapCapsule(feet+controller.center-Vector3.up*(controller.height*.5f-controller.radius),feet+controller.center+Vector3.up*(controller.height*.5f-controller.radius),controller.radius-.012f,~0,QueryTriggerInteraction.Ignore))
                if(hit!=controller&&PlacementSupport.IsUsableGround(hit))return false;
            return true;
        }
        public void SyncView()
        {
            view.transform.position=controller.transform.position+Vector3.up*1.55f;
            view.transform.rotation=Quaternion.LookRotation(IndoorRules.Look(yaw,pitch));
            if(player!=null)player.feet=controller.transform.position;
        }
        void Update()
        {
            if(Transitioning)return;
            if(automated)return;
            if(PlayerControls.PausePressed){if(active){MuseumAudio.TryPlay("ui.cancel",this,view.transform.position);Pause();}else Resume();return;}
            if(!active||won)return;
            if(Cursor.lockState!=CursorLockMode.Locked){Pause();return;}
            if(Input.GetKeyDown(KeyCode.R)){if(Input.GetKey(KeyCode.LeftShift))ResetAll();else ResetZone();return;}
            if(Input.GetKeyDown(KeyCode.H))showHints=!showHints;
            var frame=controls.Read(Time.deltaTime,aimed!=null,held!=null);
            motor.SmoothKeyboardInput=!controls.UsingGamepad;
            Interaction.PickupAssistRadius=controls.UsingGamepad?.1f:.05f;
            if(held!=null&&frame.RotateHeld)
            {
                Vector2 turn=PlayerControls.ObjectRotationDegrees(frame.Look);RotateHeld(turn.x,turn.y);
            }
            else PlayerControls.ApplyLook(ref yaw,ref pitch,frame.Look);
            SyncView();UpdateAim();
            if(frame.CancelPressed){if(held!=null)MuseumAudio.TryPlay("ui.cancel",this,view.transform.position);Cancel();}
            if(Input.GetKeyDown(KeyCode.F))MatchSurfaceDepth();
            if(held!=null)
            {
                float turn=(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0);
                float tilt=(Input.GetKey(KeyCode.X)?1:0)-(Input.GetKey(KeyCode.Z)?1:0);
                RotateHeld(turn*75*Time.deltaTime,tilt*75*Time.deltaTime);
                if(Input.GetKeyDown(KeyCode.C))heldRotation=Quaternion.Euler(0,Mathf.Round(heldRotation.eulerAngles.y/15)*15,0);
            }
            else if(adjustingMirror&&mirror!=null)
            {
                if(Input.GetMouseButtonDown(1)||Vector3.Distance(view.transform.position,mirror.transform.position)>Reach)adjustingMirror=false;
                else mirror.Turn(((Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0))*28*Time.deltaTime);
            }
            if(frame.GrabPressed){if(held!=null)pendingDrop=true;else Interact();}
            gameJumpHeld=frame.JumpHeld;
            Vector2 direction=new Vector2(Mathf.Cos(yaw)*frame.Move.x+Mathf.Sin(yaw)*frame.Move.y,
                -Mathf.Sin(yaw)*frame.Move.x+Mathf.Cos(yaw)*frame.Move.y);
            Tick(Time.deltaTime,direction,frame.JumpPressed);
        }
        void LateUpdate()
        {
            if(automated||!active||won)return;
            SyncView();AdvanceHeld(Time.deltaTime);
            if(pendingDrop){pendingDrop=false;Drop();}
            if(held==null)UpdateAim();
        }
        public void UpdateAim() { if(PortraitPerforming){aimed=null;return;}Interaction.UpdateAim(); }
        public void AdvanceHeld(float dt) { Interaction.Advance(dt);UpdateHeldPose(); }
        public void Interact()
        {
            if(PortraitPerforming)return;
            if(held!=null){if(active&&!won)Drop();return;}
            var assistance=aimed==null?null:aimed.GetComponent<GalleryCathedralActivation>();
            if(assistance!=null&&assistance.Assisting){Tell("조각을 맞추고 있습니다. 잠시만 기다려 주세요.",1);return;}
            Interaction.Interact();
        }
        void CachePlacementSamples()
        {
            placementSamples.Clear();
            holdShapes.Clear();
            foreach(var part in held.parts)
            {
                Vector3 half=part.size*.5f;
                holdShapes.Add(new HoldShape {
                    center=Interaction.ShapePoint(held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center))),
                    half=Vector3.Scale(half,part.transform.lossyScale)/referenceScale,
                    rotation=Quaternion.Inverse(held.transform.rotation)*part.transform.rotation });
                for(int i=0;i<8;i++)
                    placementSamples.Add(Interaction.ShapePoint(held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center+
                        Vector3.Scale(half,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))))));
                // Faces and centres also catch a narrow obstruction inside the silhouette.
                foreach(var direction in new[]{Vector3.zero,Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
                    placementSamples.Add(Interaction.ShapePoint(held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center+Vector3.Scale(half,direction)))));
            }
        }
        public float RequestedScale => held==null?1:held.transform.localScale.x;
        // F remains a refresh shortcut; ordinary aiming already runs the same resolver every frame.
        public void MatchSurfaceDepth() { UpdateHeldPose(); }
        void ResolvePlacementDepth()
        {
            // Interior exhibit zones classify puzzles; they are not invisible walls.
            // Actual partitions, columns and floor still constrain every projected shape ray.
            var minimum=new Vector3(-WorldHalfWidth+.21f,.006f,.01f);var maximum=new Vector3(WorldHalfWidth-.21f,WorldRoof-.26f,WorldLength-.01f);
            Interaction.ResolveDepth(minimum,maximum,out bool surface);
            HasPlacementSurface=surface;
        }
        public void RotateHeld(float yawDegrees,float pitchDegrees)
        {
            if(held==null)return;
            heldRotation=Quaternion.AngleAxis(yawDegrees,Vector3.up)*heldRotation*Quaternion.AngleAxis(pitchDegrees,Vector3.right);
        }
        public void UpdateHeldPose()
        {
            if(held==null)return;
            Vector3 eye=view.transform.position;
            HoldPose start=new HoldPose {position=held.transform.position,rotation=held.transform.rotation,scale=held.transform.localScale.x};
            Vector3 oldAnchor=held.transform.TransformPoint(GrabLocalPoint)-eye;
            start.direction=oldAnchor.normalized;start.depth=oldAnchor.magnitude;
            Quaternion requestedRotation=heldRotation;
            ResolvePlacementDepth();
            if(!Interaction.PlacementFeasible)
            {
                PlacementBlocked=true;invalid=true;invalidReason="현재 크기로 조준한 위치에 놓을 공간이 없습니다.";
                held.Highlight(true,true);return;
            }
            if(Interaction.TryApplyPlacement())
            {
                initialPlacement=false;PlacementBlocked=false;invalidReason=Interaction.ReleaseRestriction;invalid=invalidReason!=null;
                PlacementObstacle=null;
                held.Highlight(true,invalid);lighting.Synchronize();return;
            }
            HoldPose target=PerspectivePose(eye,Interaction.HeldDirection,holdDistance,requestedRotation);
            HoldPose accepted=start;PlacementBlocked=false;PlacementObstacle=null;
            // Mounted pictures start with a tiny wall/boundary overlap. Detach only a short,
            // already collision-free distance on the original pickup ray before sweeping.
            bool detach=initialPlacement&&Vector3.Distance(start.position,target.position)<.6f&&
                Vector3.Angle(start.direction,target.direction)<.01f&&Quaternion.Angle(start.rotation,target.rotation)<.01f;
            if(detach)
            {
                ApplyPreview(target);
                if(ValidPlacement(held,out _)){accepted=target;initialPlacement=false;}else {ApplyPreview(start);PlacementBlocked=true;}
            }
            else
            {
                float localRadius=0;
                foreach(var point in placementSamples)localRadius=Mathf.Max(localRadius,(point-Interaction.ScaledGrabLocalPoint).magnitude);
                // Retract before turning, then extend on the new ray. A large object must not
                // drag its lower edge through the floor while aiming from far to near.
                float travelDepth=Mathf.Min(start.depth,target.depth);
                var path=new[]{PerspectivePose(eye,start.direction,travelDepth,start.rotation),
                    PerspectivePose(eye,target.direction,travelDepth,target.rotation),target};
                int budget=256;
                foreach(var destination in path)
                {
                    HoldPose segmentStart=accepted;
                    float motion=Mathf.Max(segmentStart.depth,destination.depth)*Vector3.Angle(segmentStart.direction,destination.direction)*Mathf.Deg2Rad+
                        Mathf.Abs(destination.depth-segmentStart.depth)+localRadius*(Mathf.Abs(destination.scale-segmentStart.scale)+
                        Mathf.Max(segmentStart.scale,destination.scale)*Quaternion.Angle(segmentStart.rotation,destination.rotation)*Mathf.Deg2Rad);
                    int required=Mathf.Max(1,Mathf.CeilToInt(motion/.025f)),steps=Mathf.Min(required,budget);
                    float acceptedT=0;
                    for(int i=1;i<=steps;i++)
                    {
                        float t=(float)i/required;HoldPose next=InterpolatePreview(eye,segmentStart,destination,t);
                        if(ClearTransition(accepted,next)){accepted=next;acceptedT=t;continue;}
                        float lo=acceptedT,hi=t;
                        for(int j=0;j<9;j++)
                        {
                            float middle=(lo+hi)*.5f;HoldPose candidate=InterpolatePreview(eye,segmentStart,destination,middle);
                            if(ClearTransition(accepted,candidate)){accepted=candidate;lo=middle;}else hi=middle;
                        }
                        PlacementBlocked=true;break;
                    }
                    budget-=steps;
                    if(steps<required)PlacementBlocked=true;
                    if(PlacementBlocked)break;
                }
                if(!PlacementBlocked)initialPlacement=false;
            }
            ApplyPreview(accepted);
            Physics.SyncTransforms();
            invalid=!ValidPlacement(held,out invalidReason);held.Highlight(true,invalid);lighting.Synchronize();
        }
        HoldPose PerspectivePose(Vector3 eye,Vector3 direction,float depth,Quaternion rotation)
        {
            float scale=referenceScale*depth/referenceDistance;
            return new HoldPose {position=eye+direction*depth-rotation*(Interaction.ScaledGrabLocalPoint*scale),direction=direction,
                depth=depth,scale=scale,rotation=rotation};
        }
        HoldPose InterpolatePreview(Vector3 eye,HoldPose from,HoldPose to,float t)
        {
            // Moving the camera changes the old anchor's depth without changing its
            // already accepted physical size. Start continuously at that actual pose;
            // rescaling the first sample to the new depth would grow it into the floor.
            Vector3 direction=Vector3.Slerp(from.direction,to.direction,t).normalized;
            float depth=Mathf.Lerp(from.depth,to.depth,t),scale=Mathf.Lerp(from.scale,to.scale,t);
            Quaternion rotation=Quaternion.Slerp(from.rotation,to.rotation,t);
            return new HoldPose {position=eye+direction*depth-rotation*(Interaction.ScaledGrabLocalPoint*scale),
                direction=direction,depth=depth,scale=scale,rotation=rotation};
        }
        void ApplyPreview(HoldPose pose)
        {
            held.transform.localScale=Interaction.ScaleAt(pose.scale);
            held.transform.SetPositionAndRotation(pose.position,pose.rotation);
        }
        bool ClearTransition(HoldPose from,HoldPose to)
        {
            foreach(var shape in holdShapes)
            {
                Vector3 a=from.position+from.rotation*(shape.center*from.scale),b=to.position+to.rotation*(shape.center*to.scale);
                Vector3 delta=b-a;float length=delta.magnitude;
                if(length<.000001f)continue;
                Vector3 half=Vector3.Max(Vector3.one*.001f,shape.half*Mathf.Min(from.scale,to.scale)-Vector3.one*.003f);
                int count=Physics.BoxCastNonAlloc(a,half,delta/length,placementHits,from.rotation*shape.rotation,length,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=placementHits[i];
                    if(hit.collider==controller||hit.collider.GetComponentInParent<GalleryProp>()==held)continue;
                    PlacementObstacle="sweep: "+hit.collider.name;return false;
                }
            }
            ApplyPreview(to);
            bool valid=ValidPlacement(held,out var reason);if(!valid)PlacementObstacle="end: "+reason;return valid;
        }
        public bool ValidPlacement(GalleryProp prop,out string reason) => ValidatePlacement(prop,out reason,false);
        bool ValidatePlacement(GalleryProp prop,out string reason,bool preview)
        {
            if(nikeLight!=null&&prop==nikeLight.lamp&&!nikeLight.CanPlace(out reason))return false;
            var b=prop.WorldBounds;
            float lo=0,hi=WorldLength;
            if(b.min.x< -WorldHalfWidth+.2f||b.max.x>WorldHalfWidth-.2f||b.min.z<lo||b.max.z>hi||b.min.y<-.012f||b.max.y>WorldRoof-.25f)
            {reason="벽·천장 또는 미술관 외곽을 벗어납니다.";return false;}
            foreach(var part in prop.parts)
            {
                Vector3 half=Vector3.Scale(part.size,part.transform.lossyScale)*.5f;
                half=new Vector3(Mathf.Abs(half.x),Mathf.Abs(half.y),Mathf.Abs(half.z));
                foreach(var other in Physics.OverlapBox(part.transform.TransformPoint(part.center),Vector3.Max(Vector3.one*.001f,half-Vector3.one*.001f),part.transform.rotation,~0,QueryTriggerInteraction.Ignore))
                {
                    if(other.GetComponentInParent<GalleryProp>()==prop)continue;
                    // Held layers have no player solver contact. Keep the preview
                    // on its current ray; player overlap still forbids release.
                    if(preview&&other==controller)continue;
                    // PhysX's character query uses an inflated volume. Refine it
                    // against the actual vertical capsule, not a disabled shape.
                    if(other==controller&&!IntersectsPlayer(part))continue;
                    reason=other==controller?"플레이어와 겹칩니다.":"다른 전시물이나 구조물과 겹칩니다.";return false;
                }
            }
            reason=null;return true;
        }
        bool IntersectsPlayer(BoxCollider box)
        {
            Quaternion inverse=Quaternion.Inverse(box.transform.rotation);
            Vector3 center=box.transform.TransformPoint(box.center);
            Vector3 a=inverse*(controller.transform.position+Vector3.up*controller.radius-center);
            Vector3 b=inverse*(controller.transform.position+Vector3.up*(controller.height-controller.radius)-center);
            Vector3 half=Vector3.Scale(box.size,box.transform.lossyScale)*.5f;
            float lo=0,hi=1;
            // Squared distance from a line segment to an AABB is convex.
            for(int i=0;i<28;i++)
            {
                float p=(2*lo+hi)/3,q=(lo+2*hi)/3;
                if(BoxDistance(Vector3.Lerp(a,b,p),half)<BoxDistance(Vector3.Lerp(a,b,q),half))hi=q;else lo=p;
            }
            return BoxDistance(Vector3.Lerp(a,b,(lo+hi)/2),half)<Mathf.Pow(controller.radius-.012f,2);
        }
        static float BoxDistance(Vector3 point,Vector3 half)
        {
            var d=Vector3.Max(Vector3.zero,new Vector3(Mathf.Abs(point.x),Mathf.Abs(point.y),Mathf.Abs(point.z))-half);
            return d.sqrMagnitude;
        }
        public bool Drop()
        {
            if(PortraitPerforming)return false;
            pendingDrop=false;
            var socket=shadowCourse!=null?shadowCourse.nikeSocket:null;
            if(socket==null||held!=socket.canvas.portablePainting||!socket.Targeted(view))return Interaction.Drop();
            UpdateHeldPose();
            string hint=socket.FitHint(held);
            if(hint!=null){Tell(hint,2);return false;}
            var key=held;
            if(!Interaction.Drop())return false;
            socket.BeginDock(key);
            return true;
        }
        public void Cancel(bool force=false) { pendingDrop=false;Interaction.Cancel(force); }
        public bool FootSafe(Vector3 feet)
        {
            // The Nike work area is safe while the visitor composes the shadow.
            if(conceptLayout&&feet.z>=29)return true;
            if(MovableNike&&feet.z>=14&&(NikeKeyPuzzle||feet.z<26.5f))return true;
            if(conceptLayout&&checkpoint>=2&&feet.z>=25.7f&&Mathf.Abs(feet.x)<WorldHalfWidth-.65f&&feet.y>=-.012f&&feet.y<.45f)return true;
            foreach(var offset in new[]{Vector3.zero,new Vector3(.12f,0,0),new Vector3(-.12f,0,0),new Vector3(0,0,.12f),new Vector3(0,0,-.12f)})
            {
                Vector3 p=feet+offset;Vector3 normal=Vector3.up;
                if(Physics.Raycast(p+Vector3.up*.08f,Vector3.down,out var floor,.18f,~0,QueryTriggerInteraction.Ignore)&&floor.collider!=controller&&PlacementSupport.IsUsableGround(floor.collider))
                {p=floor.point;normal=floor.normal;}
                if(lighting.IsSunlit(p,normal))return false;
            }
            return true;
        }
        public void Tick(float dt,Vector2 move,bool jump=false)
        {
            if(!active||won)return;dt=Mathf.Clamp(dt,0,.05f);elapsed+=dt;
            foreach(var prop in props)prop.placement.Tick(dt);
            if(lightGate!=null){lighting.Synchronize();lightGate.Tick(dt,lighting,controller);lighting.Synchronize();}
            Vector3 supportMotion=Vector3.zero;
            Vector2 localMove=new Vector2(move.x*Mathf.Cos(yaw)-move.y*Mathf.Sin(yaw),move.x*Mathf.Sin(yaw)+move.y*Mathf.Cos(yaw));
            motor.VerticalVelocity=verticalVelocity;
            motor.Step(controller,localMove,yaw,jump,gameJumpHeld||jump,dt,~0,supportMotion);
            verticalVelocity=motor.VerticalVelocity;
            movementAudio?.Tick(motor.Velocity,motor.Grounded,move.sqrMagnitude>.0001f,dt);
            player.feet=controller.transform.position;player.grounded=motor.Grounded;
            SyncView();if(automated&&held!=null)AdvanceHeld(dt);
            nikeLight?.SyncPose();UpdateExposure(dt);
            shadowCourse?.Tick(dt);portraitGate?.Tick(dt);
            lesson.Tick(dt);
            if(player.feet.z>36&&player.feet.z<41&&player.feet.y>1.9f)visitedUpper=true;
            int next=conceptLayout?(player.feet.z>44?3:player.feet.z>29?2:player.feet.z>14?1:0):player.feet.z>42.6f&&visitedUpper?3:player.feet.z>28?2:player.feet.z>15?1:0;
            if(conceptLayout&&!exhibitionPreview)
            {
                if(checkpoint==0&&shadowCourse!=null&&!shadowCourse.TutorialCleared)next=0;
                if(checkpoint==1&&shadowCourse!=null&&!shadowCourse.NikeCleared)next=Mathf.Min(next,1);
                if(portraitGate!=null&&!portraitGate.Opened)next=Mathf.Min(next,2);
            }
            if(next>checkpoint&&safe&&player.grounded){checkpoint=next;museumAudio?.SetCheckpoint(checkpoint);Tell("0"+(checkpoint+1)+" 구역 저장 · R로 이 구역만 다시 시작할 수 있습니다.",6);}
            if(!conceptLayout&&orcusExit==null&&checkpoint==3&&visitedUpper&&player.feet.z>53.05f&&Mathf.Abs(player.feet.x)<1.4f&&player.feet.y>4.15f&&player.feet.y<4.35f&&player.grounded&&safe)
            {won=true;Pause();}
            SyncView();
        }
        void UpdateExposure(float dt)
        {
            if(exhibitionPreview){safe=true;exposure=sunExposureTime=0;return;}
            lighting.Synchronize();safe=FootSafe(player.feet);
            if(safe){sunExposureTime=0;exposure=Mathf.Max(0,exposure-dt*1.7f);}
            else
            {
                sunExposureTime+=dt;
                // Small seams at a shade boundary do not immediately punish a
                // correct arrangement; crossing the whole lit band still hurts.
                if(!conceptLayout||checkpoint>=2||sunExposureTime>.12f)exposure=Mathf.Clamp01(exposure+dt*2.2f);
            }
            if(exposure>=1||player.feet.y< -3)Respawn();
        }
        ObjectInteraction<GalleryProp> interaction;
        ObjectInteraction<GalleryProp> Interaction => interaction??(interaction=new ObjectInteraction<GalleryProp>(this));
        Camera IObjectInteractionHost<GalleryProp>.Camera => view;
        CharacterController IObjectInteractionHost<GalleryProp>.Controller => controller;
        GalleryProp[] IObjectInteractionHost<GalleryProp>.Props => Array.FindAll(props,p=>p!=null&&p.gameObject.activeInHierarchy);
        GalleryProp IObjectInteractionHost<GalleryProp>.Held { get => held; set => held=value; }
        GalleryProp IObjectInteractionHost<GalleryProp>.Aimed { get => aimed; set => aimed=value; }
        int IObjectInteractionHost<GalleryProp>.InteractionMask => ~0;
        bool IObjectInteractionHost<GalleryProp>.CanInteract => active&&!won&&!PortraitPerforming;
        void IObjectInteractionHost<GalleryProp>.AimChanged(RaycastHit hit)
        {
            aimedMirror=hit.collider==null?null:hit.collider.GetComponentInParent<GalleryMirror>();
            if(mirror!=null)mirror.Highlight(adjustingMirror||aimedMirror==mirror);
        }
        bool IObjectInteractionHost<GalleryProp>.TrySpecialInteraction()
        {
            if(aimedMirror!=null)
            {
                adjustingMirror=!adjustingMirror;
                Tell(adjustingMirror?"Q/E로 거울 회전 · WASD로 이동 · 클릭 또는 오른쪽 클릭으로 조작 종료":"거울 각도를 고정했습니다.",5);
                return true;
            }
            adjustingMirror=false;return false;
        }
        void IObjectInteractionHost<GalleryProp>.CancelSpecialInteraction() { adjustingMirror=false; }
        void IObjectInteractionHost<GalleryProp>.StartPlacement()
        {
            invalid=false;CachePlacementSamples();initialPlacement=true;PlacementBlocked=false;held.BeginHold();
            UpdateHeldPose();
            grabs++;Tell("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기",6);
        }
        void IObjectInteractionHost<GalleryProp>.PrepareRelease() { UpdateHeldPose(); }
        bool IObjectInteractionHost<GalleryProp>.ValidatePreview(out string reason) => ValidatePlacement(held,out reason,true);
        bool IObjectInteractionHost<GalleryProp>.ValidateRelease(out string reason)
        { invalid=!ValidPlacement(held,out invalidReason);reason=invalidReason;if(invalid)MuseumAudio.TryPlay("ui.error",this,view.transform.position);return !invalid; }
        bool IObjectInteractionHost<GalleryProp>.IntersectsPlayer(BoxCollider part) => IntersectsPlayer(part);
        void IObjectInteractionHost<GalleryProp>.FinishInteraction(bool released) { invalid=false;if(released)drops++;lighting.Synchronize(); }

    }
}
