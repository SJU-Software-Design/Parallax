using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachGame : MonoBehaviour, IObjectInteractionHost<BeachProp>
    {
        public const string Version="0.16.1";
        public const float Speed=FirstPersonMotor.Speed, Gravity=FirstPersonMotor.Gravity, JumpHeight=FirstPersonMotor.JumpHeight, Reach=InteractionRaycast.MaximumDistance;
        public CharacterController controller;
        public Camera view;
        public BeachLighting lighting;
        public BeachProp[] props;
        public Font font;
        public BeachProp held, aimed;
        public BeachMirror mirror, aimedMirror;
        public BeachLightGate lightGate, waterGate;
        public BeachShadowHarbor shadowHarbor;
        public BeachBoat boat;
        public BeachCastleRestoration castleRoute;
        public bool castleRest;
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
        public Vector3 GrabLocalPoint => Interaction.GrabLocalPoint;
        public Vector3 HeldDirection => Interaction.HeldDirection;
        readonly List<Vector3> placementSamples=new List<Vector3>();
        readonly RaycastHit[] placementHits=new RaycastHit[64];
        struct HoldShape { public Vector3 center,half; public Quaternion rotation; }
        struct HoldPose { public Vector3 position,direction; public Quaternion rotation; public float depth,scale; }
        readonly List<HoldShape> holdShapes=new List<HoldShape>();
        bool initialPlacement;
        PlayerMovementAudio movementAudio;
        readonly FirstPersonMotor motor=new FirstPersonMotor { GroundFilter=PlacementSupport.IsUsableGround };
        readonly PlayerControls controls=new PlayerControls();
        bool gameJumpHeld, pendingDrop,assistedDrop;
        BeachProp assistedPiece;

        void Awake()
        {
            FirstPersonMotor.Configure(controller,view);movementAudio=PlayerMovementAudio.Attach(controller);PlayerControls.ConfigureCamera(view);
            var arguments=Environment.GetCommandLineArgs();
            bool placementTest=Array.IndexOf(arguments,"-placement-test")>=0;
            bool waterTest=Array.IndexOf(arguments,"-beach-water-test")>=0;
            bool harborDemo=Array.IndexOf(arguments,"-beach-harbor-demo")>=0;
            bool castleDemo=Array.IndexOf(arguments,"-beach-castle-demo")>=0;
            automated=placementTest||waterTest||Array.IndexOf(arguments,"-beach-test")>=0;
            Application.runInBackground=automated;Application.targetFrameRate=automated?90:-1;
            QualitySettings.vSyncCount=automated?0:1;QualitySettings.antiAliasing=2;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","AppleGothic","Arial"},32);
#else
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Arial"},32);
#endif
            player=new Player(new Vector2(controller.transform.position.x,controller.transform.position.z));
            player.feet=controller.transform.position;
            foreach(var prop in props){prop.InitializePhysics();prop.placement.SetPlayer(controller);}
            foreach(var prop in props)prop.placement.ResetPlaced();
            lighting.Rebuild();SyncView();
            gameObject.AddComponent<BeachHud>().game=this;
            lesson=gameObject.AddComponent<PlacementLesson>();lesson.Configure(font,()=>active&&!won,()=>held==null?null:held.placement,()=>player.feet,()=>drops,()=>safe,BeachLighting.ToSun);
            foreach(var prop in props){var floatingProp=prop;prop.placement.WaterSupport=()=>BeachWater.Supports(floatingProp);}
            gameObject.AddComponent<BeachPlacementPreview>().game=this;
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            Time.timeScale=0;
            if(shadowHarbor!=null)shadowHarbor.InitializeOnGameReady();
            if(harborDemo)StartHarborDemo();
            if(castleDemo)StartCastleDemo();
            if(placementTest)gameObject.AddComponent<PlacementValidation>();
            else if(waterTest)gameObject.AddComponent<BeachWaterValidation>().game=this;
            else if(automated)gameObject.AddComponent<BeachValidation>().game=this;
        }
        void OnDisable() { if(held!=null&&interaction!=null)interaction.Cancel(true); }
        void OnDestroy() { Time.timeScale=1; }
        public void Tell(string text,float duration=4) { notice=text;noticeUntil=Time.unscaledTime+duration; }
        public void Resume()
        {
            if(won)ResetAll();ClearControlInput();PlayerControls.ConfigureCamera(view);active=started=true;Time.timeScale=1;
            if(!automated){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        public void Pause() { ClearControlInput();active=false;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        void ClearControlInput()
        {
            motor.ClearInput();controls.Reset();pendingDrop=false;gameJumpHeld=false;
        }
        void ResetControlState()
        {
            motor.Reset();movementAudio?.Reset();controls.Reset();pendingDrop=false;gameJumpHeld=false;verticalVelocity=0;
        }
        void OnApplicationFocus(bool focused) { if(!focused&&!automated)Pause(); }
        public void ResetAll()
        {
            if(held!=null)Cancel(true);foreach(var p in props)p.Restore();
            adjustingMirror=false;if(lightGate!=null)lightGate.Restore();if(waterGate!=null)waterGate.Restore();castleRest=false;
            if(shadowHarbor!=null)shadowHarbor.Restore();
            if(castleRoute!=null)castleRoute.Restore();
            checkpoint=respawns=grabs=drops=0;elapsed=exposure=0;won=visitedUpper=false;lesson?.Restart();
            PlacePlayer(BeachWorld.Checkpoints[0]);yaw=0;pitch=-.08f;lighting.Synchronize();SyncView();
        }
        public void ResetZone()
        {
            if(held!=null)Cancel(true);
            foreach(var p in props) if(p.zone==checkpoint)p.Restore();
            adjustingMirror=false;if(checkpoint==0&&lightGate!=null)lightGate.Restore();if(checkpoint==2&&waterGate!=null)waterGate.Restore();if(checkpoint==3)castleRest=false;
            if(checkpoint==2&&shadowHarbor!=null)shadowHarbor.Restore();
            if(checkpoint==3&&castleRoute!=null)castleRoute.Restore();
            PlacePlayer(BeachWorld.Checkpoints[checkpoint]);exposure=0;lighting.Synchronize();
            Tell("현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
        }
        public void StartMirrorDemo()
        {
            ResetAll();Resume();
            Tell("작은 거울을 잡고 멀리 배치해 키우세요 · Q/E 회전 · H 힌트",7);
        }
        public void StartHarborDemo()
        {
            // A separate local presentation entry point. Normal play still
            // starts at the original entrance and retains all four puzzles.
            ResetAll();
            if(lightGate!=null)
            {
                lightGate.unlocked=true;lightGate.charge=lightGate.opening=1;
                lightGate.panel.transform.position=lightGate.closedPosition+lightGate.openOffset;
            }
            checkpoint=2;PlacePlayer(BeachWorld.Checkpoints[2]);yaw=Mathf.PI*.5f;pitch=-.08f;SyncView();
            Tell("그림자 조선소 체험 · 오른쪽 제작 보드에서 배를 완성해 직접 집어 보세요. H 힌트",12);
        }
        public void PlacePlayer(Vector3 point)
        {
            ResetControlState();
            controller.enabled=false;controller.transform.position=point+Vector3.up*.025f;controller.enabled=true;
            player.feet=controller.transform.position;player.grounded=false;
            Physics.SyncTransforms();SyncView();
        }
        public void StartCastleDemo()
        {
            ResetAll();
            foreach(var gate in new[]{lightGate,waterGate})if(gate!=null)
            {
                gate.unlocked=true;gate.charge=gate.opening=1;
                gate.panel.transform.position=gate.closedPosition+gate.openOffset;
            }
            checkpoint=3;PlacePlayer(new Vector3(0,0,80.5f));yaw=0;pitch=.12f;SyncView();
            Tell("모래성 정면 복구 · 계단 조각 두 개를 각각 연결하고, 평평한 성벽에 파라솔을 설치하세요. H 힌트",12);
        }
        public void Respawn()
        {
            if(held!=null)Cancel(true);
            bool returnedBoat=checkpoint==2&&boat!=null&&boat.Departed;
            bool returnedCastle=checkpoint==3&&castleRoute!=null&&castleRoute.AtMidpoint;
            if(checkpoint==2&&boat!=null)boat.HandleRespawn();
            if(checkpoint==3&&castleRoute!=null)castleRoute.RecoverFragment();
            adjustingMirror=false;
            var spawn=castleRest&&checkpoint==3?BeachWorld.CastleRest:BeachWorld.Checkpoints[checkpoint];
            if(checkpoint==3&&!castleRest&&castleRoute!=null&&castleRoute.ReachedEntrance)spawn=BeachCastleRestoration.LowerStart;
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
            PlacePlayer(spawn);exposure=0;respawns++;Tell(returnedBoat?"쉼터로 복귀했습니다 · 배는 출발 위치에 다시 정박했습니다.":returnedCastle?"중간 성벽으로 복귀 · 복구한 첫 성벽은 유지됩니다.":"빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.");
        }
        public bool CanStand(Vector3 feet)
        {
            foreach(var hit in Physics.OverlapCapsule(feet+controller.center-Vector3.up*(controller.height*.5f-controller.radius),feet+controller.center+Vector3.up*(controller.height*.5f-controller.radius),controller.radius-.012f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
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
            if(automated)return;
            if(PlayerControls.PausePressed){if(active)Pause();else Resume();return;}
            if(!active||won)return;
            if(Cursor.lockState!=CursorLockMode.Locked){Pause();return;}
            if(Input.GetKeyDown(KeyCode.R)){if(Input.GetKey(KeyCode.LeftShift))ResetAll();else ResetZone();return;}
            if(Input.GetKeyDown(KeyCode.H))showHints=!showHints;
            var frame=controls.Read(Time.deltaTime,aimed!=null,held!=null);
            if(checkpoint==2&&held==null&&boat!=null&&Input.GetKeyDown(KeyCode.E))boat.TryLaunch();
            motor.SmoothKeyboardInput=!controls.UsingGamepad;
            Interaction.PickupAssistRadius=controls.UsingGamepad?.1f:.05f;
            if(held!=null&&frame.RotateHeld)
            {
                Vector2 turn=PlayerControls.ObjectRotationDegrees(frame.Look);RotateHeld(turn.x,turn.y);
            }
            else PlayerControls.ApplyLook(ref yaw,ref pitch,frame.Look);
            SyncView();UpdateAim();
            if(frame.CancelPressed)Cancel();
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
        public void UpdateAim()
        {
            if(shadowHarbor!=null&&shadowHarbor.Materializing)
            {
                if(aimed!=null)aimed.Highlight(false,false);aimed=null;return;
            }
            Interaction.UpdateAim();
        }
        public void AdvanceHeld(float dt) { Interaction.Advance(dt);UpdateHeldPose(); }
        public void Interact() { Interaction.Interact(); }
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
            float near=BeachWorld.ZoneNear[held.zone],far=BeachWorld.ZoneFar[held.zone];
            // A buoyant boat is aimed onto the water rather than underneath a
            // pier on the 2 m deep channel bed. Ordinary recovery keeps full depth.
            float minimumY=held.GetComponent<BeachBoat>()!=null?-.12f:BeachWorld.WaterBedY+.02f;
            var minimum=new Vector3(-13.79f,minimumY,near+.01f);var maximum=new Vector3(13.79f,15.74f,far-.01f);
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
                int count=Physics.BoxCastNonAlloc(a,half,delta/length,placementHits,from.rotation*shape.rotation,length,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=placementHits[i];
                    if(hit.collider==controller||hit.collider.GetComponentInParent<BeachProp>()==held)continue;
                    PlacementObstacle="sweep: "+hit.collider.name;return false;
                }
            }
            ApplyPreview(to);
            bool valid=ValidPlacement(held,out var reason);if(!valid)PlacementObstacle="end: "+reason;return valid;
        }
        public bool TryApplyDropAlignment(BeachProp piece,BeachShadowDropAssist.Alignment alignment)
        {
            if(piece==null||held!=piece||piece.placement==null||!piece.placement.Held)return false;
            HoldPose start=new HoldPose{position=piece.transform.position,rotation=piece.transform.rotation,scale=piece.transform.localScale.x};
            string previousObstacle=PlacementObstacle;bool accepted=false;
            try
            {
                float radius=0;foreach(var shape in holdShapes)radius=Mathf.Max(radius,shape.center.magnitude+shape.half.magnitude);
                float motion=Vector3.Distance(start.position,alignment.position)+radius*Mathf.Abs(start.scale-alignment.scale.x)
                    +radius*Mathf.Max(start.scale,alignment.scale.x)*Quaternion.Angle(start.rotation,alignment.rotation)*Mathf.Deg2Rad;
                int steps=Mathf.Max(1,Mathf.CeilToInt(motion/.025f));if(steps>96)return false;
                HoldPose from=start;
                for(int i=1;i<=steps;i++)
                {
                    float t=(float)i/steps;
                    HoldPose next=new HoldPose{position=Vector3.Lerp(start.position,alignment.position,t),
                        rotation=Quaternion.Slerp(start.rotation,alignment.rotation,t),scale=Mathf.Lerp(start.scale,alignment.scale.x,t)};
                    if(!ClearTransition(from,next))return false;from=next;
                }
                piece.body.position=piece.transform.position;piece.body.rotation=piece.transform.rotation;
                Physics.SyncTransforms();accepted=true;return true;
            }
            finally
            {
                if(!accepted)
                {
                    ApplyPreview(start);piece.body.position=start.position;piece.body.rotation=start.rotation;Physics.SyncTransforms();
                }
                PlacementObstacle=previousObstacle;
            }
        }
        public bool ValidPlacement(BeachProp prop,out string reason) => ValidatePlacement(prop,out reason,false);
        bool ValidatePlacement(BeachProp prop,out string reason,bool preview)
        {
            var b=prop.WorldBounds;
            float lo=BeachWorld.ZoneNear[prop.zone],hi=BeachWorld.ZoneFar[prop.zone];
            if(b.min.x< -13.8f||b.max.x>13.8f||b.min.z<lo||b.max.z>hi||b.min.y<BeachWorld.PropMinimumY||b.max.y>15.75f)
            {reason="해변 경계 또는 현재 구역을 벗어납니다.";return false;}
            foreach(var part in prop.parts)
            {
                Vector3 half=Vector3.Scale(part.size,part.transform.lossyScale)*.5f;
                half=new Vector3(Mathf.Abs(half.x),Mathf.Abs(half.y),Mathf.Abs(half.z));
                foreach(var other in Physics.OverlapBox(part.transform.TransformPoint(part.center),Vector3.Max(Vector3.one*.001f,half-Vector3.one*.001f),part.transform.rotation,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
                {
                    if(other.GetComponentInParent<BeachProp>()==prop)continue;
                    // Held layers have no player solver contact. Keep the preview
                    // on its current ray; player overlap still forbids release.
                    if(preview&&other==controller)continue;
                    // PhysX's character query uses an inflated volume. Refine it
                    // against the actual vertical capsule, not a disabled shape.
                    if(other==controller&&!IntersectsPlayer(part))continue;
                    reason=other==controller?"플레이어와 겹칩니다.":"다른 사물이나 구조물과 겹칩니다.";return false;
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
        public bool Drop() { pendingDrop=false;return Interaction.Drop(); }
        public void Cancel(bool force=false) { pendingDrop=false;Interaction.Cancel(force); }
        public bool FootSafe(Vector3 feet)
        {
            foreach(var offset in new[]{Vector3.zero,new Vector3(.12f,0,0),new Vector3(-.12f,0,0),new Vector3(0,0,.12f),new Vector3(0,0,-.12f)})
            {
                Vector3 p=feet+offset;Vector3 normal=Vector3.up;
                if(Physics.Raycast(p+Vector3.up*.08f,Vector3.down,out var floor,.18f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&floor.collider!=controller&&PlacementSupport.IsUsableGround(floor.collider))
                {p=floor.point;normal=floor.normal;}
                if(lighting.IsSunlit(p,normal))return false;
            }
            return true;
        }
        public void Tick(float dt,Vector2 move,bool jump=false)
        {
            if(!active||won)return;dt=Mathf.Clamp(dt,0,.05f);elapsed+=dt;
            foreach(var prop in props)prop.placement.Tick(dt);
            lighting.Synchronize();if(lightGate!=null)lightGate.Tick(dt,lighting,controller);if(waterGate!=null)waterGate.Tick(dt,lighting,controller);
            if(shadowHarbor!=null)shadowHarbor.Tick(dt);
            Vector3 supportMotion=Vector3.zero;
            if((motor.Grounded||controller.isGrounded)&&Physics.Raycast(controller.transform.position+Vector3.up*.18f,Vector3.down,out var platformHit,.3f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&PlacementSupport.IsUsableGround(platformHit.collider))
            {var platform=platformHit.collider.GetComponentInParent<BeachProp>();if(platform!=null&&platform.placement.Ready&&platform.floating&&!platform.body.isKinematic)supportMotion=Vector3.ProjectOnPlane(platform.body.linearVelocity,Vector3.up);}
            Vector2 localMove=new Vector2(move.x*Mathf.Cos(yaw)-move.y*Mathf.Sin(yaw),move.x*Mathf.Sin(yaw)+move.y*Mathf.Cos(yaw));
            motor.VerticalVelocity=verticalVelocity;
            motor.Step(controller,localMove,yaw,jump,gameJumpHeld||jump,dt,BeachWorld.PhysicalMask,supportMotion);
            verticalVelocity=motor.VerticalVelocity;
            movementAudio?.Tick(motor.Velocity,motor.Grounded,move.sqrMagnitude>.0001f,dt);
            player.feet=controller.transform.position;player.grounded=motor.Grounded;
            if(boat!=null)boat.Tick(dt);
            SyncView();if(automated&&held!=null)AdvanceHeld(dt);
            UpdateExposure(dt);
            lesson.Tick(dt);
            int next=player.feet.z>73&&waterGate.unlocked?3:player.feet.z>48?2:player.feet.z>24&&lightGate.unlocked?1:0;
            if(next>checkpoint&&safe&&player.grounded){checkpoint=next;Tell("0"+(checkpoint+1)+" 구역 저장 · R 구역 재시도",6);}
            if(castleRoute!=null)castleRoute.Tick(dt);
            if(checkpoint==3&&lightGate.unlocked&&waterGate.unlocked&&castleRoute!=null&&castleRoute.SecondRestored&&castleRoute.IsAtSummit(player.feet)&&player.grounded&&safe)
            {won=true;Debug.Log("BEACH_CLEAR v"+Version+" feet="+player.feet);Pause();}
            SyncView();
        }
        void UpdateExposure(float dt)
        {
            lighting.Synchronize();safe=FootSafe(player.feet)&&!BeachWater.InHazard(player.feet);
            exposure=Mathf.Clamp01(exposure+dt*(safe?-1.2f:.72f));
            if(exposure>=1||player.feet.y< -3||BeachWater.InHazard(player.feet))Respawn();
        }
        ObjectInteraction<BeachProp> interaction;
        ObjectInteraction<BeachProp> Interaction => interaction??(interaction=new ObjectInteraction<BeachProp>(this));
        Camera IObjectInteractionHost<BeachProp>.Camera => view;
        CharacterController IObjectInteractionHost<BeachProp>.Controller => controller;
        BeachProp[] IObjectInteractionHost<BeachProp>.Props => props;
        BeachProp IObjectInteractionHost<BeachProp>.Held { get => held; set => held=value; }
        BeachProp IObjectInteractionHost<BeachProp>.Aimed { get => aimed; set => aimed=value; }
        int IObjectInteractionHost<BeachProp>.InteractionMask => BeachWorld.PhysicalMask;
        bool IObjectInteractionHost<BeachProp>.CanInteract => active&&!won&&(shadowHarbor==null||!shadowHarbor.Materializing);
        void IObjectInteractionHost<BeachProp>.AimChanged(RaycastHit hit)
        {
            // Beach mirrors are movable props; aiming at their head must pick up
            // the whole prop rather than entering the fixed Gallery mirror mode.
            aimedMirror=null;
        }
        bool IObjectInteractionHost<BeachProp>.TrySpecialInteraction()
        {
            if(castleRoute!=null&&castleRoute.IsFixed(aimed))
            {Tell("복구한 성벽입니다 · 연결한 자리에 고정되어 있습니다.");return true;}
            if(aimedMirror!=null)
            {
                adjustingMirror=!adjustingMirror;
                Tell(adjustingMirror?"Q/E로 거울 회전 · WASD로 이동 · 클릭 또는 오른쪽 클릭으로 조작 종료":"거울 각도를 고정했습니다.",5);
                return true;
            }
            adjustingMirror=false;return false;
        }
        void IObjectInteractionHost<BeachProp>.CancelSpecialInteraction() { adjustingMirror=false; }
        void IObjectInteractionHost<BeachProp>.StartPlacement()
        {
            assistedDrop=false;assistedPiece=null;
            invalid=false;CachePlacementSamples();initialPlacement=true;PlacementBlocked=false;held.BeginHold();
            if(castleRoute!=null)castleRoute.PiecePickedUp(held);
            UpdateHeldPose();
            grabs++;Tell("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기",6);
        }
        void IObjectInteractionHost<BeachProp>.PrepareRelease()
        {
            UpdateHeldPose();assistedDrop=Interaction.PlacementFeasible&&shadowHarbor!=null&&shadowHarbor.TryAssistRelease(held);
            assistedPiece=assistedDrop?held:null;
        }
        bool IObjectInteractionHost<BeachProp>.ValidatePreview(out string reason) => ValidatePlacement(held,out reason,true);
        bool IObjectInteractionHost<BeachProp>.ValidateRelease(out string reason)
        { invalid=!ValidPlacement(held,out invalidReason);reason=invalidReason;return !invalid; }
        bool IObjectInteractionHost<BeachProp>.IntersectsPlayer(BoxCollider part) => IntersectsPlayer(part);
        void IObjectInteractionHost<BeachProp>.FinishInteraction(bool released)
        {
            invalid=false;if(released)drops++;
            if(released&&assistedDrop&&shadowHarbor!=null)
            {
                // The helper only runs for the two source pieces, and this
                // callback confirms the ordinary release actually succeeded.
                shadowHarbor.NotifyAssistApplied(assistedPiece);
                Tell("윤곽 근처의 조각을 살짝 정렬해 놓았습니다.",3);
            }
            assistedDrop=false;assistedPiece=null;lighting.Synchronize();
        }

    }
}
