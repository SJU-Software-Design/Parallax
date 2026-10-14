using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachGame : MonoBehaviour
    {
        public const string Version="0.12.1";
        public const float Speed=3.6f, Gravity=17, JumpHeight=1.18f, Reach=11;
        public CharacterController controller;
        public Camera view;
        public BeachLighting lighting;
        public BeachProp[] props;
        public Font font;
        public BeachProp held, aimed;
        public BeachMirror mirror, aimedMirror;
        public BeachLightGate lightGate, waterGate;
        public bool castleRest;
        public bool adjustingMirror;
        public Player player;
        public float yaw, pitch=-.08f, exposure, elapsed, verticalVelocity;
        public float holdDistance { get; private set; }
        public float referenceDistance { get; private set; }
        public float referenceScale { get; private set; }
        public bool HasPlacementSurface { get; private set; }
        public bool PlacementBlocked { get; private set; }
        public string PlacementObstacle { get; private set; }
        public Quaternion heldRotation;
        public bool active, started, won, safe=true, invalid, automated, showHints;
        public bool visitedUpper;
        public int checkpoint, respawns, grabs, drops;
        public string notice, invalidReason;
        public float noticeUntil;
        Vector3 pickupPosition, pickupScale;
        Quaternion pickupRotation;
        Vector3 pickupVelocity, pickupAngularVelocity;
        bool pickupKinematic, pickupGravity, pickupSleeping,pickupFloating;
        float pickupDrift;Vector3 pickupLastDrift;
        PlacementSupport.Snapshot pickupPlacement;
        public PlacementLesson lesson;
        RaycastHit aimHit;
        public Vector3 GrabLocalPoint { get; private set; }
        readonly List<Vector3> placementSamples=new List<Vector3>();
        readonly RaycastHit[] placementHits=new RaycastHit[64];
        struct HoldShape { public Vector3 center,half; public Quaternion rotation; }
        struct HoldPose { public Vector3 position,direction; public Quaternion rotation; public float depth,scale; }
        readonly List<HoldShape> holdShapes=new List<HoldShape>();
        bool initialPlacement;
        float coyote, jumpBuffer;

        void Awake()
        {
            var arguments=Environment.GetCommandLineArgs();
            bool placementTest=Array.IndexOf(arguments,"-placement-test")>=0;
            bool waterTest=Array.IndexOf(arguments,"-beach-water-test")>=0;
            automated=placementTest||waterTest||Array.IndexOf(arguments,"-beach-test")>=0;
            Application.runInBackground=automated;Application.targetFrameRate=90;
            QualitySettings.vSyncCount=automated?0:1;QualitySettings.antiAliasing=2;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Arial"},32);
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
            if(placementTest)gameObject.AddComponent<PlacementValidation>();
            else if(waterTest)gameObject.AddComponent<BeachWaterValidation>().game=this;
            else if(automated)gameObject.AddComponent<BeachValidation>().game=this;
        }
        void OnDestroy() { Time.timeScale=1; }
        public void Tell(string text,float duration=4) { notice=text;noticeUntil=Time.unscaledTime+duration; }
        public void Resume()
        {
            if(won)ResetAll();active=started=true;Time.timeScale=1;
            if(!automated){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        public void Pause() { active=false;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        void OnApplicationFocus(bool focused) { if(!focused&&!automated)Pause(); }
        public void ResetAll()
        {
            if(held!=null)Cancel(true);foreach(var p in props)p.Restore();
            adjustingMirror=false;if(lightGate!=null)lightGate.Restore();if(waterGate!=null)waterGate.Restore();castleRest=false;
            checkpoint=respawns=grabs=drops=0;elapsed=exposure=0;won=visitedUpper=false;lesson?.Restart();
            PlacePlayer(BeachWorld.Checkpoints[0]);yaw=0;pitch=-.08f;lighting.Synchronize();SyncView();
        }
        public void ResetZone()
        {
            if(held!=null)Cancel(true);
            foreach(var p in props) if(p.zone==checkpoint)p.Restore();
            adjustingMirror=false;if(checkpoint==0&&lightGate!=null)lightGate.Restore();if(checkpoint==2&&waterGate!=null)waterGate.Restore();if(checkpoint==3)castleRest=false;
            PlacePlayer(BeachWorld.Checkpoints[checkpoint]);exposure=0;lighting.Synchronize();
            Tell("현재 구역의 사물과 위치를 초기화했습니다. 이전 구역은 유지됩니다.");
        }
        public void StartMirrorDemo()
        {
            ResetAll();Resume();
            Tell("작은 거울을 잡고 멀리 배치해 키우세요 · Q/E 회전 · H 힌트",7);
        }
        public void PlacePlayer(Vector3 point)
        {
            controller.enabled=false;controller.transform.position=point+Vector3.up*.025f;controller.enabled=true;
            player.feet=controller.transform.position;verticalVelocity=0;coyote=.12f;jumpBuffer=0;
            Physics.SyncTransforms();SyncView();
        }
        public void Respawn()
        {
            if(held!=null)Cancel(true);
            adjustingMirror=false;
            var spawn=castleRest&&checkpoint==3?BeachWorld.CastleRest:BeachWorld.Checkpoints[checkpoint];
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
            PlacePlayer(spawn);exposure=0;respawns++;Tell("빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.");
        }
        public bool CanStand(Vector3 feet)
        {
            foreach(var hit in Physics.OverlapCapsule(feet+Vector3.up*.28f,feet+Vector3.up*1.49f,.225f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
                if(hit!=controller)return false;
            return true;
        }
        public void SyncView()
        {
            view.transform.position=controller.transform.position+Vector3.up*Rules.EyeHeight;
            view.transform.rotation=Quaternion.LookRotation(IndoorRules.Look(yaw,pitch));
            if(player!=null)player.feet=controller.transform.position;
        }
        void Update()
        {
            if(automated)return;
            if(Input.GetKeyDown(KeyCode.Escape)){if(active)Pause();else Resume();}
            if(!active||won)return;
            if(Cursor.lockState!=CursorLockMode.Locked){Pause();return;}
            if(Input.GetKeyDown(KeyCode.R)){if(Input.GetKey(KeyCode.LeftShift))ResetAll();else ResetZone();return;}
            if(Input.GetKeyDown(KeyCode.H))showHints=!showHints;
            yaw+=Input.GetAxisRaw("Mouse X")*.023f;pitch=Mathf.Clamp(pitch+Input.GetAxisRaw("Mouse Y")*.023f,-1.35f,1.3f);
            SyncView();UpdateAim();
            if(Input.GetMouseButtonDown(1))Cancel();
            if(Input.GetKeyDown(KeyCode.F))MatchSurfaceDepth();
            if(held!=null)
            {
                float turn=(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0);
                float tilt=(Input.GetKey(KeyCode.X)?1:0)-(Input.GetKey(KeyCode.Z)?1:0);
                RotateHeld(turn*75*Time.deltaTime,tilt*75*Time.deltaTime);
                if(Input.GetKeyDown(KeyCode.C))heldRotation=Quaternion.Euler(0,Mathf.Round(heldRotation.eulerAngles.y/15)*15,0);
                UpdateHeldPose();
            }
            else if(adjustingMirror&&mirror!=null)
            {
                if(Vector3.Distance(view.transform.position,mirror.transform.position)>Reach)adjustingMirror=false;
                else mirror.Turn(((Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0))*28*Time.deltaTime);
            }
            if(Input.GetMouseButtonDown(0))Interact();
            float f=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0),r=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            Vector2 direction=new Vector2(Mathf.Sin(yaw)*f+Mathf.Cos(yaw)*r,Mathf.Cos(yaw)*f-Mathf.Sin(yaw)*r);
            Tick(Mathf.Min(Time.deltaTime,.05f),direction,Input.GetKeyDown(KeyCode.Space));
        }
        public void UpdateAim()
        {
            BeachProp previous=aimed;aimed=null;aimedMirror=null;
            if(held==null&&Physics.Raycast(view.transform.position,view.transform.forward,out aimHit,Reach,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
            {aimed=aimHit.collider.GetComponentInParent<BeachProp>();aimedMirror=null;}
            if(previous!=null&&previous!=held&&previous!=aimed)previous.Highlight(false,false);
            if(aimed!=null)aimed.Highlight(true,false);
            
        }
        public void Interact()
        {
            if(!active||won)return;
            if(held!=null){Drop();return;}
            UpdateAim();
            if(aimedMirror!=null){adjustingMirror=!adjustingMirror;Tell(adjustingMirror?"Q/E로 거울 회전 · WASD로 이동 · 클릭 또는 오른쪽 클릭으로 조작 종료":"거울 각도를 고정했습니다.",5);return;}
            adjustingMirror=false;if(aimed==null)return;
            if(Physics.Raycast(controller.transform.position+Vector3.up*.15f,Vector3.down,out var support,.3f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&support.collider.GetComponentInParent<BeachProp>()==aimed)
            {Tell("서 있는 발판는 잡을 수 없습니다. 먼저 다른 발판으로 이동하세요.");return;}
            foreach(var prop in props)
            {
                if(prop==aimed)continue;
                var bounds=prop.WorldBounds;
                if(Physics.Raycast(new Vector3(bounds.center.x,bounds.min.y+.04f,bounds.center.z),Vector3.down,out var below,.14f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)&&below.collider.GetComponentInParent<BeachProp>()==aimed)
                {Tell("위에 놓인 사물을 먼저 옮겨 주세요.");return;}
            }
            held=aimed;pickupPosition=held.transform.position;pickupRotation=held.transform.rotation;pickupScale=held.transform.localScale;
            held.InitializePhysics();pickupPlacement=held.placement.Capture();pickupFloating=held.floating;pickupDrift=held.driftDistance;pickupLastDrift=held.lastDriftPosition;
            pickupKinematic=held.body.isKinematic;pickupGravity=held.body.useGravity;pickupSleeping=held.body.IsSleeping();
            pickupVelocity=held.body.linearVelocity;pickupAngularVelocity=held.body.angularVelocity;
            GrabLocalPoint=held.transform.InverseTransformPoint(aimHit.point);
            referenceScale=held.transform.localScale.x;
            referenceDistance=Mathf.Max(.01f,aimHit.distance);
            holdDistance=referenceDistance;heldRotation=held.transform.rotation;invalid=false;
            CachePlacementSamples();
            initialPlacement=true;PlacementBlocked=false;
            held.BeginHold();
            UpdateHeldPose();
            grabs++;Tell("WASD로 들고 이동 · 시선으로 크기 조절 · 클릭으로 놓기",6);
        }
        void CachePlacementSamples()
        {
            placementSamples.Clear();
            holdShapes.Clear();
            foreach(var part in held.parts)
            {
                Vector3 half=part.size*.5f;
                holdShapes.Add(new HoldShape {
                    center=held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center)),
                    half=Vector3.Scale(half,part.transform.lossyScale)/referenceScale,
                    rotation=Quaternion.Inverse(held.transform.rotation)*part.transform.rotation });
                for(int i=0;i<8;i++)
                    placementSamples.Add(held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center+
                        Vector3.Scale(half,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
                // Faces and centres also catch a narrow obstruction inside the silhouette.
                foreach(var direction in new[]{Vector3.zero,Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
                    placementSamples.Add(held.transform.InverseTransformPoint(part.transform.TransformPoint(part.center+Vector3.Scale(half,direction))));
            }
        }
        public float RequestedScale => held==null?1:held.transform.localScale.x;
        // F remains a refresh shortcut; ordinary aiming already runs the same resolver every frame.
        public void MatchSurfaceDepth() { UpdateHeldPose(); }
        bool PlacementRay(Vector3 velocity,out float depth)
        {
            float length=velocity.magnitude;depth=48;bool found=false;
            if(length<.00001f)return false;
            int count=Physics.RaycastNonAlloc(view.transform.position,velocity/length,placementHits,48*length,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=placementHits[i];
                if(hit.collider==controller||hit.collider.GetComponentInParent<BeachProp>()==held)continue;
                depth=Mathf.Min(depth,(hit.distance-.02f)/length);found=true;
            }
            return found;
        }
        static void ClipDepth(float origin,float velocity,float minimum,float maximum,ref float lo,ref float hi)
        {
            if(Mathf.Abs(velocity)<.000001f)
            {if(origin<minimum||origin>maximum){lo=1;hi=0;}return;}
            float a=(minimum-origin)/velocity,b=(maximum-origin)/velocity;
            lo=Mathf.Max(lo,Mathf.Min(a,b));hi=Mathf.Min(hi,Mathf.Max(a,b));
        }
        void ResolvePlacementDepth()
        {
            Vector3 eye=view.transform.position,forward=view.transform.forward;
            float ratio=referenceScale/referenceDistance;
            float minDepth=held.minScale/ratio,maxDepth=Mathf.Min(48,held.maxScale/ratio);
            float roomMin=minDepth,roomMax=maxDepth;
            float zoneNear=BeachWorld.ZoneNear[held.zone],zoneFar=BeachWorld.ZoneFar[held.zone];
            bool surface=PlacementRay(forward,out float surfaceDepth);
            foreach(var local in placementSamples)
            {
                // Every point moves along a ray from the eye as distance and scale grow together.
                Vector3 velocity=forward+heldRotation*((local-GrabLocalPoint)*ratio);
                if(PlacementRay(velocity,out float contact)){surfaceDepth=Mathf.Min(surfaceDepth,contact);surface=true;}
                ClipDepth(eye.x,velocity.x,-13.79f,13.79f,ref roomMin,ref roomMax);
                ClipDepth(eye.y,velocity.y,BeachWorld.WaterBedY+.02f,15.74f,ref roomMin,ref roomMax);
                ClipDepth(eye.z,velocity.z,zoneNear+.01f,zoneFar-.01f,ref roomMin,ref roomMax);
            }
            HasPlacementSurface=surface;
            // Looking through an opening retains the last depth for free airborne placement.
            float target=surface?surfaceDepth:holdDistance;
            holdDistance=roomMin<=roomMax?Mathf.Clamp(target,roomMin,roomMax):Mathf.Clamp(target,minDepth,maxDepth);
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
            HoldPose target=PerspectivePose(eye,view.transform.forward,holdDistance,requestedRotation);
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
                foreach(var point in placementSamples)localRadius=Mathf.Max(localRadius,(point-GrabLocalPoint).magnitude);
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
            ApplyPreview(accepted);holdDistance=accepted.depth;heldRotation=accepted.rotation;
            Physics.SyncTransforms();
            invalid=!ValidPlacement(held,out invalidReason);held.Highlight(true,invalid);lighting.Synchronize();
        }
        HoldPose PerspectivePose(Vector3 eye,Vector3 direction,float depth,Quaternion rotation)
        {
            float scale=referenceScale*depth/referenceDistance;
            return new HoldPose {position=eye+direction*depth-rotation*(GrabLocalPoint*scale),direction=direction,
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
            return new HoldPose {position=eye+direction*depth-rotation*(GrabLocalPoint*scale),
                direction=direction,depth=depth,scale=scale,rotation=rotation};
        }
        void ApplyPreview(HoldPose pose)
        {
            held.transform.localScale=Vector3.one*pose.scale;
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
        public bool ValidPlacement(BeachProp prop,out string reason)
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
        public bool Drop()
        {
            if(held==null)return false;
            // Release exactly the displayed pose, without resolving another surface on this click.
            invalid=!ValidPlacement(held,out invalidReason);
            if(invalid){held.Highlight(true,true);Tell(invalidReason);return false;}
            held.Release();
            held.Highlight(false,false);held=null;aimed=null;invalid=false;drops++;Physics.SyncTransforms();lighting.Synchronize();return true;
        }
        public void Cancel(bool force=false)
        {
            adjustingMirror=false;if(held==null)return;
            Vector3 currentPosition=held.transform.position,currentScale=held.transform.localScale;
            Quaternion currentRotation=held.transform.rotation;
            held.transform.SetPositionAndRotation(pickupPosition,pickupRotation);held.transform.localScale=pickupScale;
            if(!force)
            {
                foreach(var part in held.parts)if(IntersectsPlayer(part))
                {
                    held.transform.SetPositionAndRotation(currentPosition,currentRotation);held.transform.localScale=currentScale;
                    Physics.SyncTransforms();Tell("잡기 전 위치에 서 있습니다. 옆으로 이동한 뒤 취소하세요.");return;
                }
            }
            foreach(var p in held.parts)p.enabled=true;
            if(held.isBall&&held.sphere!=null){held.sphere.enabled=!pickupKinematic;foreach(var part in held.parts)part.enabled=pickupKinematic;}
            held.body.position=pickupPosition;held.body.rotation=pickupRotation;held.RefreshMass();
            held.body.isKinematic=pickupKinematic;held.body.useGravity=pickupGravity;
            held.body.collisionDetectionMode=pickupKinematic?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.ContinuousDynamic;
            if(!pickupKinematic)
            {
                held.body.linearVelocity=pickupVelocity;held.body.angularVelocity=pickupAngularVelocity;
                if(pickupSleeping)held.body.Sleep();else held.body.WakeUp();
            }
            held.floating=pickupFloating;held.driftDistance=pickupDrift;held.lastDriftPosition=pickupLastDrift;held.placement.Restore(pickupPlacement);
            held.Highlight(false,false);held=null;aimed=null;invalid=false;Physics.SyncTransforms();lighting.Synchronize();
        }
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
            move=Vector2.ClampMagnitude(move,1);
            bool grounded=controller.isGrounded;
            if(!grounded&&verticalVelocity<=0&&Physics.SphereCast(controller.transform.position+Vector3.up*.29f,.20f,Vector3.down,out var ground,.14f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))grounded=ground.normal.y>.6f&&PlacementSupport.IsUsableGround(ground.collider);
            coyote=grounded?.10f:Mathf.Max(0,coyote-dt);jumpBuffer=jump?.12f:Mathf.Max(0,jumpBuffer-dt);
            if(grounded&&verticalVelocity<0)verticalVelocity=-2;
            if(jumpBuffer>0&&coyote>0){verticalVelocity=Mathf.Sqrt(2*Gravity*JumpHeight);jumpBuffer=coyote=0;}
            verticalVelocity-=Gravity*dt;
            Vector3 supportMotion=Vector3.zero;
            if(grounded&&Physics.Raycast(controller.transform.position+Vector3.up*.18f,Vector3.down,out var platformHit,.3f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
            {var platform=platformHit.collider.GetComponentInParent<BeachProp>();if(platform!=null&&platform.placement.Ready&&platform.floating&&!platform.body.isKinematic)supportMotion=Vector3.ProjectOnPlane(platform.body.linearVelocity,Vector3.up);}
            CollisionFlags flags=controller.Move((new Vector3(move.x*Speed,verticalVelocity,move.y*Speed)+supportMotion)*dt);
            if((flags&CollisionFlags.Above)!=0&&verticalVelocity>0)verticalVelocity=0;
            player.feet=controller.transform.position;player.grounded=controller.isGrounded;
            SyncView();if(held!=null)UpdateHeldPose();
            UpdateExposure(dt);
            lesson.Tick(dt);
            int next=player.feet.z>73&&waterGate.unlocked?3:player.feet.z>48?2:player.feet.z>24&&lightGate.unlocked?1:0;
            if(next>checkpoint&&safe&&player.grounded){checkpoint=next;Tell("0"+(checkpoint+1)+" 구역 저장 · R 구역 재시도",6);}
            if(checkpoint==3&&!castleRest&&Vector3.Distance(player.feet,BeachWorld.CastleRest)<1.6f&&safe&&player.grounded)
            {castleRest=true;Tell("모래성 중간 쉼터 저장 · 실패해도 여기서 다시 시작합니다.",6);}
            if(checkpoint==3&&lightGate.unlocked&&waterGate.unlocked&&Vector3.Distance(player.feet,BeachWorld.Exit)<1.35f&&player.grounded&&safe)
            {won=true;Pause();}
            SyncView();
        }
        void UpdateExposure(float dt)
        {
            lighting.Synchronize();safe=FootSafe(player.feet)&&!BeachWater.InHazard(player.feet);
            exposure=Mathf.Clamp01(exposure+dt*(safe?-1.2f:.72f));
            if(exposure>=1||player.feet.y< -3||BeachWater.InHazard(player.feet))Respawn();
        }
    }
}
