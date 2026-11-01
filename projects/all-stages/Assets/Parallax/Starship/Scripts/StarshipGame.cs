using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipGame : MonoBehaviour, IObjectInteractionHost<StarshipProp>
    {
        public const string Version="0.13.1";
        public const float Speed=FirstPersonMotor.Speed, Gravity=FirstPersonMotor.Gravity, JumpHeight=FirstPersonMotor.JumpHeight, Reach=InteractionRaycast.MaximumDistance;
        public CharacterController controller;
        public Camera view;
        public StarshipLighting lighting;
        public StarshipProp[] props;
        public Font font;
        public StarshipProp held, aimed;
        public StarshipMirror mirror, aimedMirror;
        public StarshipLightGate lightGate, waterGate;
        public bool castleRest;
        public bool adjustingMirror;
        public Player player;
        public float yaw, pitch=-.08f, exposure, elapsed, verticalVelocity;
        public float holdDistance { get => Interaction.Depth; internal set => Interaction.Depth=value; }
        public float referenceDistance => Interaction.ReferenceDistance;
        public float referenceScale => Interaction.ReferenceScale;
        public bool HasPlacementSurface { get; internal set; }
        public bool PlacementBlocked { get; internal set; }
        public string PlacementObstacle { get; internal set; }
        public Quaternion heldRotation { get => Interaction.Rotation; set => Interaction.Rotation=value; }
        public bool active, started, won, safe=true, invalid, automated, showHints;
        public bool visitedUpper;
        public StarshipEnding ending;
        public int hintLevel;
        public int checkpoint, respawns, grabs, drops;
        public string notice, invalidReason;
        public float noticeUntil;
        public PlacementLesson lesson;
        public Vector3 GrabLocalPoint => Interaction.GrabLocalPoint;
        public Vector3 HeldDirection => Interaction.HeldDirection;
        PlayerMovementAudio movementAudio;
        readonly FirstPersonMotor motor=new FirstPersonMotor { GroundFilter=PlacementSupport.IsUsableGround };
        readonly PlayerControls controls=new PlayerControls();
        bool gameJumpHeld, pendingDrop;

        void Awake()
        {
            FirstPersonMotor.Configure(controller,view);movementAudio=PlayerMovementAudio.Attach(controller);PlayerControls.ConfigureCamera(view);
            automated=Array.IndexOf(Environment.GetCommandLineArgs(),"-starship-test")>=0;
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
            yaw=StarshipWorld.Yaws[0]*Mathf.Deg2Rad;lighting.Rebuild();SyncView();
            gameObject.AddComponent<StarshipHud>().game=this;
            lesson=gameObject.AddComponent<PlacementLesson>();
            lesson.Configure(font,()=>active&&!won&&ending.phase==StarshipEnding.Phase.None,()=>held==null?null:held.placement,()=>player.feet,()=>drops,()=>safe,StarshipLighting.ToSun);
            foreach(var prop in props){var floatingProp=prop;prop.placement.WaterSupport=()=>StarshipWater.Supports(floatingProp);}
            gameObject.AddComponent<StarshipPlacementPreview>().game=this;
            gameObject.AddComponent<StarshipAmbience>().game=this;
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;Time.timeScale=0;
            if(automated)gameObject.AddComponent<StarshipValidation>().game=this;
        }
        void OnDisable() { if(held!=null&&interaction!=null)interaction.Cancel(true); }
        void OnDestroy() { Time.timeScale=1; StarshipHold.Clear(this); }
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
            ending.Restore();StarshipHold.Clear(this);checkpoint=respawns=grabs=drops=0;elapsed=exposure=0;won=visitedUpper=false;lesson?.Restart();
            PlacePlayer(StarshipWorld.Checkpoints[0]);yaw=StarshipWorld.Yaws[0]*Mathf.Deg2Rad;pitch=-.08f;lighting.Synchronize();SyncView();
        }
        public void ResetZone()
        {
            if(held!=null)Cancel(true);
            foreach(var p in props) if(p.zone==checkpoint)p.Restore();
            adjustingMirror=false;if(checkpoint==1&&lightGate!=null)lightGate.Restore();if(checkpoint==3&&waterGate!=null)waterGate.Restore();if(checkpoint==3)castleRest=false;
            PlacePlayer(StarshipWorld.Checkpoints[checkpoint]);exposure=0;lighting.Synchronize();
            Tell("현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
        }
        public void StartMirrorDemo()
        {
            ResetAll();Resume();
            Tell("작은 거울을 잡고 멀리 배치해 키우세요 · Q/E 회전 · H 힌트",7);
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
            var spawn=castleRest&&checkpoint==3?StarshipWorld.CastleRest:StarshipWorld.Checkpoints[checkpoint];
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
            PlacePlayer(spawn);exposure=0;respawns++;Tell("빛·냉각수 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.");
        }
        public bool CanStand(Vector3 feet)
        {
            foreach(var hit in Physics.OverlapCapsule(feet+controller.center-Vector3.up*(controller.height*.5f-controller.radius),feet+controller.center+Vector3.up*(controller.height*.5f-controller.radius),controller.radius-.012f,StarshipWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
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
            if(ending.phase>=StarshipEnding.Phase.Drifting)return;
            if(Cursor.lockState!=CursorLockMode.Locked){Pause();return;}
            if(ending.phase==StarshipEnding.Phase.None&&Input.GetKeyDown(KeyCode.R)){if(Input.GetKey(KeyCode.LeftShift))ResetAll();else ResetZone();return;}
            if(Input.GetKeyDown(KeyCode.H)){hintLevel=(hintLevel+1)%3;showHints=hintLevel>0;}
            var frame=controls.Read(Time.deltaTime,aimed!=null,held!=null);
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
            if(ending.phase>=StarshipEnding.Phase.Drifting)return;
            SyncView();AdvanceHeld(Time.deltaTime);
            if(pendingDrop){pendingDrop=false;Drop();}
            if(held==null)UpdateAim();
        }
        public void UpdateAim() { Interaction.UpdateAim(); }
        public void AdvanceHeld(float dt) { Interaction.Advance(dt);UpdateHeldPose(); }
        public void Interact() { Interaction.Interact(); }
        void CachePlacementSamples() { StarshipHold.Begin(this); }
        public float RequestedScale => StarshipHold.RequestedScale(this);
        // F remains a refresh shortcut; ordinary aiming already runs the same resolver every frame.
        public void MatchSurfaceDepth() { UpdateHeldPose(); }
        
        
        void ResolvePlacementDepth() { StarshipHold.Resolve(this); }
        internal PerspectivePlacement.Result ResolveSharedPlacement(Bounds room) => Interaction.ResolvePlacement(
            room.min+new Vector3(.01f,.05f,.01f),room.max-Vector3.one*.01f);
        internal int SharedPlacementQueries => Interaction.PlacementQueries;
        internal string SharedReleaseRestriction => Interaction.ReleaseRestriction;
        internal bool TryApplySharedPlacement() => Interaction.TryApplyPlacement();
        public void RotateHeld(float yawDegrees,float pitchDegrees)
        {
            if(held==null)return;
            heldRotation=Quaternion.AngleAxis(yawDegrees,Vector3.up)*heldRotation*Quaternion.AngleAxis(pitchDegrees,Vector3.right);
        }
        public void UpdateHeldPose() { StarshipHold.Update(this); }
        
        
        
        
        public bool ValidPlacement(StarshipProp prop,out string reason) => ValidatePlacement(prop,out reason,false);
        bool ValidatePlacement(StarshipProp prop,out string reason,bool preview)
        {
            if(!StarshipWorld.InZone(prop.WorldBounds,prop.zone)){reason="현재 방의 배치 범위를 벗어납니다.";return false;}
            foreach(var part in prop.parts)
            {
                Vector3 half=Vector3.Scale(part.size,part.transform.lossyScale)*.5f;
                half=new Vector3(Mathf.Abs(half.x),Mathf.Abs(half.y),Mathf.Abs(half.z));
                foreach(var other in Physics.OverlapBox(part.transform.TransformPoint(part.center),Vector3.Max(Vector3.one*.001f,half-Vector3.one*.001f),part.transform.rotation,StarshipWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
                {
                    if(other.GetComponentInParent<StarshipProp>()==prop)continue;
                    // Held layers have no player solver contact. Keep the preview
                    // on its current ray; player overlap still forbids release.
                    if(preview&&other==controller)continue;
                    if(other==controller&&!IntersectsPlayer(part))continue;
                    reason=other==controller?"플레이어와 겹칩니다.":"다른 사물이나 구조물과 겹칩니다.";return false;
                }
            }
            reason=null;return true;
        }
        internal bool IntersectsPlayer(BoxCollider box)
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
                if(Physics.Raycast(p+Vector3.up*.08f,Vector3.down,out var floor,.18f,StarshipWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&floor.collider!=controller&&PlacementSupport.IsUsableGround(floor.collider))
                {p=floor.point;normal=floor.normal;}
                if(lighting.IsSunlit(p,normal))return false;
            }
            return true;
        }
        public void Tick(float dt,Vector2 move,bool jump=false)
        {
            if(!active||won)return;dt=Mathf.Clamp(dt,0,.05f);elapsed+=dt;
            if(ending.phase>=StarshipEnding.Phase.Drifting){ending.Tick(dt);return;}
            foreach(var prop in props)prop.placement.Tick(dt);
            lighting.Synchronize();if(lightGate!=null)lightGate.Tick(dt,lighting,controller);if(waterGate!=null)waterGate.Tick(dt,lighting,controller);
            Vector3 supportMotion=Vector3.zero;
            if((motor.Grounded||controller.isGrounded)&&Physics.Raycast(controller.transform.position+Vector3.up*.18f,Vector3.down,out var platformHit,.3f,StarshipWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&PlacementSupport.IsUsableGround(platformHit.collider))
            {var platform=platformHit.collider.GetComponentInParent<StarshipProp>();if(platform!=null&&platform.placement.Ready&&platform.floating&&!platform.body.isKinematic)supportMotion=Vector3.ProjectOnPlane(platform.body.linearVelocity,Vector3.up);}
            Vector2 localMove=new Vector2(move.x*Mathf.Cos(yaw)-move.y*Mathf.Sin(yaw),move.x*Mathf.Sin(yaw)+move.y*Mathf.Cos(yaw));
            motor.VerticalVelocity=verticalVelocity;
            motor.Step(controller,localMove,yaw,jump,gameJumpHeld||jump,dt,StarshipWorld.PhysicalMask,supportMotion);
            verticalVelocity=motor.VerticalVelocity;
            movementAudio?.Tick(motor.Velocity,motor.Grounded,move.sqrMagnitude>.0001f,dt);
            player.feet=controller.transform.position;player.grounded=motor.Grounded;
            SyncView();if(automated&&held!=null)AdvanceHeld(dt);
            if(ending.phase==StarshipEnding.Phase.None)UpdateExposure(dt);else{safe=true;exposure=0;}
            lesson.Tick(dt);
            ending.Tick(dt);
            if(ending.phase==StarshipEnding.Phase.None)
            {
                int next=StarshipWorld.RoomAt(player.feet);
                bool unlocked=next<=1||lightGate.unlocked;
                if(next==4)unlocked&=waterGate.unlocked&&castleRest;
                if(next>checkpoint&&next==checkpoint+1&&unlocked&&safe&&player.grounded)
                {checkpoint=next;Tell("0"+(checkpoint+1)+" 구역 저장 · R 구역 재시도",5);}
                if(checkpoint==3&&!castleRest&&Vector3.Distance(player.feet,StarshipWorld.CastleRest)<1.65f&&safe&&player.grounded)
                {castleRest=true;Tell("상층 정비 쉼터 저장 · 사용한 발판과 차광판을 회수해 재배치하세요.",6);}
                if(checkpoint==4)ending.CheckEntrance();
            }
            if(ending.phase<StarshipEnding.Phase.Drifting)SyncView();
        }
        void UpdateExposure(float dt)
        {
            lighting.Synchronize();safe=FootSafe(player.feet)&&!StarshipWater.InHazard(player.feet);
            exposure=Mathf.Clamp01(exposure+dt*(safe?-1.2f:.72f));
            if(exposure>=1||player.feet.y< -3||StarshipWater.InHazard(player.feet))Respawn();
        }
        ObjectInteraction<StarshipProp> interaction;
        ObjectInteraction<StarshipProp> Interaction => interaction??(interaction=new ObjectInteraction<StarshipProp>(this));
        Camera IObjectInteractionHost<StarshipProp>.Camera => view;
        CharacterController IObjectInteractionHost<StarshipProp>.Controller => controller;
        StarshipProp[] IObjectInteractionHost<StarshipProp>.Props => props;
        StarshipProp IObjectInteractionHost<StarshipProp>.Held { get => held; set => held=value; }
        StarshipProp IObjectInteractionHost<StarshipProp>.Aimed { get => aimed; set => aimed=value; }
        int IObjectInteractionHost<StarshipProp>.InteractionMask => StarshipWorld.PhysicalMask;
        bool IObjectInteractionHost<StarshipProp>.CanInteract => active&&!won;
        void IObjectInteractionHost<StarshipProp>.AimChanged(RaycastHit hit)
        {
            aimedMirror=null;
        }
        bool IObjectInteractionHost<StarshipProp>.TrySpecialInteraction()
        {
            if(aimedMirror!=null)
            {
                adjustingMirror=!adjustingMirror;
                Tell(adjustingMirror?"Q/E로 거울 회전 · WASD로 이동 · 클릭 또는 오른쪽 클릭으로 조작 종료":"거울 각도를 고정했습니다.",5);
                return true;
            }
            adjustingMirror=false;return false;
        }
        void IObjectInteractionHost<StarshipProp>.CancelSpecialInteraction() { adjustingMirror=false; }
        void IObjectInteractionHost<StarshipProp>.StartPlacement()
        {
            invalid=false;CachePlacementSamples();PlacementBlocked=false;held.BeginHold();
            UpdateHeldPose();
            grabs++;Tell("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기",6);
        }
        void IObjectInteractionHost<StarshipProp>.PrepareRelease() { UpdateHeldPose(); }
        bool IObjectInteractionHost<StarshipProp>.ValidatePreview(out string reason) => ValidatePlacement(held,out reason,true);
        bool IObjectInteractionHost<StarshipProp>.ValidateRelease(out string reason)
        { invalid=!ValidPlacement(held,out invalidReason);reason=invalidReason;return !invalid; }
        bool IObjectInteractionHost<StarshipProp>.IntersectsPlayer(BoxCollider part) => IntersectsPlayer(part);
        void IObjectInteractionHost<StarshipProp>.FinishInteraction(bool released) { invalid=false;StarshipHold.Clear(this);if(released)drops++;lighting.Synchronize(); }

    }
}

