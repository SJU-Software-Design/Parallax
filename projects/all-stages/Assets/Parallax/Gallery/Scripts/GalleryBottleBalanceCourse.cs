using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    // Both vessel and sculpture use the ordinary perspective/physics controller.
    // Only containment, extraction and the explicitly signposted scale receiver
    // are special puzzle rules; camera movement never scales a fixed exhibit.
    public sealed class GalleryBottleBalanceCourse : MonoBehaviour
    {
        public const float MinimumBottleSize = .001f, MaximumBottleSize = float.PositiveInfinity;
        public const float ExtractionSize = 1.8f, ExtractionTilt = 27;
        public const float TargetSculptureScale = 1.65f, StableSecondsRequired = 2;
        public const float BalanceEnterSizeTolerance=.10f, BalanceKeepSizeTolerance=.13f, BalanceJitterGrace=.20f;
        public GalleryGame game;
        public GalleryBottleBalanceArt.View Art { get; private set; }
        public GalleryProp Bottle { get; private set; }
        public GalleryProp Sculpture { get; private set; }
        public GalleryBottleBalancePlacement Placement { get; private set; }
        public GalleryBottleBalanceDoor Door { get; private set; }
        public GalleryBottleBalanceKeyReward Reward { get; private set; }
        public GalleryBottleContentsMotion Contents { get; private set; }
        public bool Manipulating => game != null && game.held == Bottle;
        public bool Targeted => game != null && game.aimed == Bottle;
        public bool Extracted { get; private set; }
        public bool Extracting { get; private set; }
        public bool Solved { get; private set; }
        public float BottleSize => Bottle != null ? Bottle.transform.lossyScale.x : 1;
        public float BottleTilt => Bottle != null ? Mathf.Asin(Mathf.Clamp(-GalleryBottleBalanceVessel.MouthDirection(Bottle).y,-1,1))*Mathf.Rad2Deg : 0;
        public float StableSeconds { get; private set; }
        public float BalanceProgress => Solved ? 1 : Mathf.Clamp01(StableSeconds / StableSecondsRequired);
        public float RelativeLoad { get; private set; }
        public float BeamDegrees { get; private set; } = 12;
        public bool BalanceCaptured { get; private set; }
        public bool ExtractionBlocked => extractionBlocked;
        public string ExtractionBlocker { get; private set; }
        public Vector3 ExtractionExitPosition => GalleryBottleBalanceVessel.MouthWorld(Bottle)+GalleryBottleBalanceVessel.MouthDirection(Bottle)*.56f;
        public bool InRoom => game != null && game.player != null && game.player.feet.z >= GalleryBottleBalanceLayout.InsertAt && game.player.feet.z < GalleryBottleBalanceLayout.EndZ;
        public bool IsNewPiece(GalleryProp prop) => prop == Sculpture || prop == Bottle;
        public bool IsContained(GalleryProp prop) => prop == Sculpture && !Extracted;
        public Vector3 DemoPosition => new Vector3(0, 0, 30.2f);
        public BoxCollider ExitBarrier => Door != null ? Door.Barrier : null;
        public Transform ExitLeaf => Door != null ? Door.Root : null;
        public event Action GlassContact;
        public event Action SculptureReleased;
        public event Action BalanceCompleted;
        public event Action PassageOpened;
        public event Action<float,Vector3> GlassImpact;
        public GalleryDikeInsertionRumble InsertionRumble { get; private set; }
        public GalleryDikeAudio Sounds { get; private set; }
        public string Status => Solved ? (Reward!=null?Reward.Status:"디케가 놓아주는 검을 살펴보세요") :
            extractionBlocked ? "조각이 나올 수 있도록 병 입구 아래 공간을 비워 주세요" :
            Extracting ? "입구를 향해 움직이고 있어요 · 조금 더 기울여 보세요" : !Extracted ?
            (GalleryBottleBalanceVessel.MouthRadius(Bottle) < GalleryBottleContentsMotion.Radius+.02f ? "병을 옮겨 키우고 좁은 입구를 넓혀 보세요" : "입구가 넓어졌어요 · 병을 들어 아래로 기울여 보세요") :
            RelativeLoad < .01f ? "꺼낸 법전을 오른쪽 저울에 올려 보세요" :
            BalanceCaptured ? "수평이에요 · 조각을 내려놓고 잠깐 기다리세요" :
            RelativeLoad < 1 ? "오른쪽이 가벼워요 · 조각을 조금 키워 보세요" :
            "오른쪽이 무거워요 · 조각을 조금 줄여 보세요";
        public string Hint => !Extracted ?
            "병을 클릭하면 다른 사물처럼 자유롭게 옮길 수 있어요.\n가까이서 잡아 먼 표면에 투영하면 병이 커져요.\n안의 조각 크기는 그대로 유지돼요.\n입구가 넓어지면 병을 들어 우클릭으로 아래로 기울여요." :
            "법전을 잡아 원근으로 크기를 조절하세요.\n접시 모서리나 위쪽을 겨냥하면 놓을 위치가 보여요.\n수평을 2초 유지하면 디케가 빛나며 검을 놓아줘요.\n검을 집어 방 가운데 대리석 받침의 홈에 꽂으세요.";

        float opening, balanceUnsteadyTime;
        bool extractionBlocked;
        readonly List<GalleryProp> loads = new List<GalleryProp>();
        Transform roomRoot, previewRoot;
        Material previewMaterial;

        public static bool Requested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-gallery-bottle-balance-off") >= 0) return false;
            return Array.IndexOf(args, "-gallery-bottle-balance") >= 0 ||
                Application.productName.IndexOf("Bottle and Balance", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public static GalleryBottleBalanceCourse Install(GalleryGame game)
        {
            if (!game.conceptLayout || !(game.bottleBalanceEnabled || Requested())) return null;
            // An explicit legacy-path/test opt-out still overrides the saved scene flag.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gallery-bottle-balance-off") >= 0) return null;
            var existing = game.GetComponent<GalleryBottleBalanceCourse>();
            if (existing != null) return existing;
            GalleryBottleBalanceLayout.Extend(game);
            var course = game.gameObject.AddComponent<GalleryBottleBalanceCourse>();
            course.game = game;
            course.roomRoot = new GameObject("Bottle and balance · interactive exhibition").transform;
            course.roomRoot.SetParent(game.transform, false);
            course.Art = GalleryBottleBalanceArt.Create(course.roomRoot,
                GalleryDikeGalleryLayout.BottleStation,GalleryDikeGalleryLayout.ScaleStation);
            GalleryDikeExhibit.Install(course.Art);
            course.Bottle = GalleryBottleBalanceVessel.CreateMovableBottle(course.Art, course.roomRoot, 1);
            course.Art.catchPad.gameObject.SetActive(false);
            course.CreateSculpture();
            course.Contents=new GalleryBottleContentsMotion(course.Bottle,course.Sculpture,course.roomRoot);
            course.Contents.GlassImpact+=(speed,point)=>{course.GlassContact?.Invoke();course.GlassImpact?.Invoke(speed,point);};
            course.Door = GalleryBottleBalanceDoor.Create(course.roomRoot, course.Art.brass, course.Art.darkBrass);
            course.Reward=GalleryBottleBalanceKeyReward.Install(course,course.roomRoot,course.Art,course.Door);
            course.InsertionRumble=GalleryDikeInsertionRumble.Install(game,course.Reward);
            course.Sounds=GalleryDikeAudio.Install(course);
            course.Placement = new GalleryBottleBalancePlacement(game, course);
            course.Label("병을 옮겨 키우고 · 기울여 법전 꺼내기",GalleryDikeGalleryLayout.BottleInstruction,.095f,true);
            var props = new List<GalleryProp>(game.props ?? Array.Empty<GalleryProp>()) { course.Bottle, course.Sculpture };
            game.props = props.ToArray();
            course.ResetCourse();
            Debug.Log("BOTTLE_BALANCE_INSTALLED freeVessel=1 containedMotion=1 keyReward=1 length="+GalleryBottleBalanceLayout.AddedLength);
            return course;
        }
        public static Vector3 ShiftLegacy(Vector3 point)
        { if(point.z>=GalleryBottleBalanceLayout.InsertAt)point.z+=GalleryBottleBalanceLayout.AddedLength;return point; }
        public static float LegacyZ(float z)=>z>=GalleryBottleBalanceLayout.EndZ?z-GalleryBottleBalanceLayout.AddedLength:z>=GalleryBottleBalanceLayout.InsertAt?28.95f:z;

        void CreateSculpture()
        {
            var go = new GameObject("Bottle contents · removable law book");
            go.transform.SetParent(roomRoot,false);
            Vector3 collisionSize=Vector3.one*.54f;
            var model=Resources.Load<GameObject>("BottleBalance/Dike/LawBook");
            bool lawBook=model!=null;
            if(model==null)model=Resources.Load<GameObject>("BottleBalance/Models/TrefoilKnot");
            if(model==null)model=Resources.Load<GameObject>("BottleBalance/Models/BronzeLoop");
            if(model!=null)
            {
                var visual=Instantiate(model,go.transform,false);visual.name=lawBook?"CC0 encyclopedia · adapted law-book cover":"CC0 sculpture · external original mesh";
                Bounds bounds=new Bounds();bool first=true;
                foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh==null)continue;
                    Bounds b=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        Vector3 q=visual.transform.InverseTransformPoint(filter.transform.TransformPoint(point));
                        if(first){bounds=new Bounds(q,Vector3.zero);first=false;}else bounds.Encapsulate(q);
                    }
                }
                float normal=(lawBook ? .50f : .56f)/Mathf.Max(.001f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
                collisionSize=bounds.size*normal+Vector3.one*.012f;
                visual.transform.localScale=Vector3.one*normal;visual.transform.localPosition=-bounds.center*normal;
                if(!lawBook)foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=Art.brass;
                foreach(var collider in visual.GetComponentsInChildren<Collider>())Destroy(collider);
                if(lawBook)
                {
                    var cover=new GameObject("Law book · original LEX cover lettering").transform;
                    cover.SetParent(go.transform,false);cover.localPosition=Vector3.up*(collisionSize.y*.5f+.004f);
                    cover.localRotation=Quaternion.Euler(90,0,0);
                    var text=cover.gameObject.AddComponent<GalleryLabel>();text.caption="LEX\n법전";text.size=.058f;text.color=new Color(.95f,.78f,.36f);
                }
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh=GalleryBottleBalanceArt.CreateKnotMesh(.56f);
                go.AddComponent<MeshRenderer>().sharedMaterial=Art.brass;
            }
            go.AddComponent<BoxCollider>().size=collisionSize;
            Sculpture=go.AddComponent<GalleryProp>();
            Sculpture.title=lawBook?"병 속의 법전":"병 속 조각 · 법전 원본 준비 필요";Sculpture.zone=1;Sculpture.minScale=.35f;Sculpture.mountedAtHome=true;
            go.transform.position=GalleryBottleBalanceVessel.ContentWorldPosition(Bottle);
            Sculpture.Remember();Sculpture.InitializePhysics();Sculpture.placement.SetPlayer(game.controller);
        }
        void Label(string caption,Vector3 at,float size,bool floor)
        {
            var go=new GameObject("Balance exhibit label · "+caption);go.transform.SetParent(roomRoot,false);go.transform.localPosition=at;
            if(floor)go.transform.localRotation=Quaternion.Euler(90,0,0);
            var label=go.AddComponent<GalleryLabel>();label.caption=caption;label.size=size;label.color=new Color(.64f,.40f,.17f);
        }
        public void ResetCourse()
        {
            Extracted=Extracting=Solved=extractionBlocked=BalanceCaptured=false;
            balanceUnsteadyTime=0;
            StableSeconds=opening=RelativeLoad=0;BeamDegrees=12;
            if(Art==null||Bottle==null||Sculpture==null)return;
            Bottle.Restore();Bottle.placement.PreviewWhileHeld=false;Bottle.placement.PreviewUntilSettled=false;
            Sculpture.Restore();Sculpture.placement.PreviewUntilSettled=false;
            Sculpture.placement.PreviewWhileHeld=false;
            Sculpture.body.isKinematic=true;Sculpture.body.useGravity=false;
            Sculpture.transform.localScale=Vector3.one;
            Contents?.Reset();SyncContents();Reward?.Reset();
            InsertionRumble?.ResetPulse();
            Sounds?.ResetSounds();
            Art.SetBalance(BeamDegrees,0,false);Door?.SetOpening(0);Placement?.Clear();
            if(previewRoot!=null)previewRoot.gameObject.SetActive(false);
            Physics.SyncTransforms();
        }
        public void SyncContents()
        {
            if(Extracted||Sculpture==null||Bottle==null)return;
            Contents?.Synchronize(Time.deltaTime>0?Time.deltaTime:1f/90);
        }
        public bool ValidateBottleContents(GalleryProp prop,out string reason)
        {
            reason=null;
            if(prop!=Bottle||Extracted)return true;
            // The 8-panel shell's inscribed bulb clearance is .5343m, less
            // than the visible lathe radius. Reserve space for the actual proxy.
            float innerRadius=.50f*Mathf.Abs(prop.transform.lossyScale.x);
            if(innerRadius<GalleryBottleContentsMotion.Radius+.02f)
            {reason="병 안의 조각과 유리가 겹칩니다. 조각이 들어갈 공간을 남겨 주세요.";return false;}
            return true;
        }
        public bool RejectBottleOverPan(GalleryProp prop,out string reason)
        {
            reason=null;if(prop!=Bottle)return false;
            Bounds b=prop.WorldBounds;
            foreach(var receiver in new[]{Art.leftPan,Art.rightPan})
            {
                var pan=receiver.GetComponent<BoxCollider>().bounds;
                bool overlap=b.min.x<pan.max.x&&b.max.x>pan.min.x&&b.min.z<pan.max.z&&b.max.z>pan.min.z;
                if(overlap&&b.min.y<pan.max.y+.20f&&b.max.y>pan.min.y)
                {reason=GalleryBottleBalancePlacement.BottleForbiddenMessage;return true;}
            }
            return false;
        }
        public void RefreshPlacementPreview()
        {
            if(Placement==null||game.held==null||!game.active)
            {Placement?.Clear();if(previewRoot!=null)previewRoot.gameObject.SetActive(false);return;}
            if(!Placement.TryPreview(game.held,out var candidate,out _)||!Placement.HasPreview||game.held!=Sculpture)
            {if(previewRoot!=null)previewRoot.gameObject.SetActive(false);return;}
            if(previewRoot==null)CreatePreview();
            previewRoot.gameObject.SetActive(true);
            previewRoot.SetPositionAndRotation(candidate,Sculpture.transform.rotation);
            previewRoot.localScale=Sculpture.transform.lossyScale;
        }
        void CreatePreview()
        {
            previewRoot=new GameObject("Scale · intended placement preview").transform;previewRoot.SetParent(roomRoot,false);
            previewMaterial=new Material(Shader.Find("Parallax/BottleBalanceReceiverGhost")){name="Scale receiver · visible placement preview"};
            previewMaterial.SetColor("_Color",new Color(.20f,.85f,.48f,.25f));
            foreach(var source in Sculpture.GetComponentsInChildren<MeshFilter>())
            {
                if(source.sharedMesh==null)continue;
                var node=new GameObject("Receiver preview mesh");node.transform.SetParent(previewRoot,false);
                node.transform.localPosition=Sculpture.transform.InverseTransformPoint(source.transform.position);
                node.transform.localRotation=Quaternion.Inverse(Sculpture.transform.rotation)*source.transform.rotation;
                Vector3 world=source.transform.lossyScale,own=Sculpture.transform.lossyScale;
                node.transform.localScale=new Vector3(world.x/own.x,world.y/own.y,world.z/own.z);
                node.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=previewMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        public void Tick(float dt)
        {
            if(game==null||!game.active||dt<=0)return;
            if(!Extracted)
            {
                bool wide=GalleryBottleBalanceVessel.MouthRadius(Bottle)>=GalleryBottleContentsMotion.Radius+.02f;
                // The contents already collide with the real floor/walls.
                // A fixed sample ahead of the lip must not add a virtual cap.
                extractionBlocked=false;
                Contents.Tick(dt,wide);
                Vector3 local=Bottle.GetComponent<GalleryBottleBalanceVessel>().glassFrame.InverseTransformPoint(Sculpture.transform.position);
                Extracting=wide&&local.x>.48f;
                if(Contents.BeyondMouth)
                {
                    if(TryPreparePhysicsRelease())FinishPhysicalExtraction();
                    else extractionBlocked=true;
                }
            }
            TickBalance(dt);
            Reward?.Tick(dt);
            InsertionRumble?.Tick(dt);
            if(Reward!=null&&Reward.DoorReleaseReady&&opening<1)
            {
                opening=Mathf.Min(1,opening+dt/GalleryBottleBalanceDoor.OpeningSeconds);Door.SetOpening(opening);
                if(opening>=1)PassageOpened?.Invoke();
            }
            Sounds?.Tick(dt);
            RefreshPlacementPreview();
        }
        bool TryPreparePhysicsRelease()
        {
            ExtractionBlocker=null;
            Vector3 original=Sculpture.transform.position;
            // The released object's ordinary box is slightly more conservative
            // than its enclosing visual sphere. Resolve small real overlaps at
            // the handover, rather than rejecting empty space ahead of the lip.
            for(int pass=0;pass<5;pass++)
            {
                bool moved=false;
                foreach(var part in Sculpture.parts)
                {
                    Vector3 half=Vector3.Scale(part.size,part.transform.lossyScale)*.5f;
                    Vector3 centre=part.transform.TransformPoint(part.center);
                    foreach(var other in Physics.OverlapBox(centre,half,part.transform.rotation,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(other.GetComponentInParent<GalleryProp>()==Sculpture)continue;
                        if(!Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,
                            other,other.transform.position,other.transform.rotation,out Vector3 normal,out float depth)||depth<.002f)continue;
                        ExtractionBlocker=other.name;
                        Sculpture.transform.position+=normal*(depth+.003f);moved=true;
                        if(Vector3.Distance(original,Sculpture.transform.position)>.18f)
                        {Sculpture.transform.position=original;return false;}
                    }
                }
                if(!moved){ExtractionBlocker=null;return true;}
                Physics.SyncTransforms();
            }
            Sculpture.transform.position=original;return false;
        }
        void FinishPhysicalExtraction()
        {
            Extracting=false;Extracted=true;extractionBlocked=false;
            Sculpture.placement.PreviewUntilSettled=Sculpture.placement.PreviewWhileHeld=true;
            foreach(var part in Sculpture.parts)part.enabled=true;
            Sculpture.body.position=Sculpture.transform.position;Sculpture.body.rotation=Sculpture.transform.rotation;
            Sculpture.Release();Sculpture.body.linearVelocity=Contents.ReleaseVelocity;
            Sculpture.body.angularVelocity=Contents.AngularVelocity;SculptureReleased?.Invoke();
            game.Tell("조각을 꺼냈어요. 오른쪽 접시를 겨냥해 현재 크기로 올려 보세요.",7);
        }
        bool OnRightPan(GalleryProp prop)
        {
            if(prop==null||prop==Bottle||prop==game.held||prop.parts==null||prop.parts.Length==0||
                !prop.gameObject.activeInHierarchy||IsContained(prop))return false;
            Bounds b=prop.WorldBounds, pan=Art.rightPan.GetComponent<BoxCollider>().bounds;
            float gap=b.min.y-pan.max.y;
            if(Mathf.Abs(b.center.x-pan.center.x)>pan.extents.x+.08f||
                Mathf.Abs(b.center.z-pan.center.z)>pan.extents.z+.08f||gap<-.10f||gap>.10f)return false;
            // Read actual support under a footprint, so a small edge contact
            // does not disappear when the sculpture rocks or the dish moves.
            Vector3 half=new Vector3(Mathf.Min(b.extents.x*.8f,pan.extents.x*.9f),.008f,
                Mathf.Min(b.extents.z*.8f,pan.extents.z*.9f));
            foreach(var hit in Physics.BoxCastAll(new Vector3(b.center.x,b.min.y+.045f,b.center.z),half,
                Vector3.down,Quaternion.identity,.15f,~0,QueryTriggerInteraction.Ignore))
                if(hit.normal.y>.5f&&(hit.collider.transform==Art.rightPan||hit.collider.transform.IsChildOf(Art.rightPan)))return true;
            return false;
        }
        public void TickBalance(float dt)
        {
            if(Solved){BeamDegrees=Mathf.MoveTowards(BeamDegrees,0,dt*16);Art.SetBalance(BeamDegrees,1,true);return;}
            loads.Clear();float load=0;
            foreach(var prop in game.props)
            {
                if(!OnRightPan(prop))continue;loads.Add(prop);
                load+=Mathf.Pow(prop.transform.lossyScale.x/TargetSculptureScale,3);
            }
            RelativeLoad=load;float error=load-1;
            bool resting=loads.Count>0;
            foreach(var prop in loads)resting&=prop.body==null||prop.body.isKinematic||(prop.body.linearVelocity.sqrMagnitude<.20f&&prop.body.angularVelocity.sqrMagnitude<1);
            float sizeError=Mathf.Abs(Mathf.Pow(Mathf.Max(0,load),1f/3)-1);
            bool eligible=Extracted&&loads.Contains(Sculpture)&&game.held!=Sculpture;
            bool balanced=eligible&&resting&&sizeError<=(BalanceCaptured?BalanceKeepSizeTolerance:BalanceEnterSizeTolerance);
            if(!Extracted||game.held==Sculpture)
            {BalanceCaptured=false;StableSeconds=balanceUnsteadyTime=0;}
            else if(balanced)
            {BalanceCaptured=true;balanceUnsteadyTime=0;}
            else
            {
                balanceUnsteadyTime+=dt;
                if(balanceUnsteadyTime>BalanceJitterGrace){BalanceCaptured=false;StableSeconds=0;}
            }
            BeamDegrees=Mathf.MoveTowards(BeamDegrees,BalanceCaptured?0:Mathf.Clamp(-error*12,-15,15),dt*10);
            // Brief contact/velocity noise pauses progress; it cannot earn time
            // in midair, and sustained error still clears the two-second dwell.
            if(balanced&&Mathf.Abs(BeamDegrees)<1)StableSeconds+=dt;
            Vector3 before=Art.rightPan.position;Art.SetBalance(BeamDegrees,BalanceProgress,false);
            Vector3 change=Art.rightPan.position-before;
            foreach(var prop in loads){prop.transform.position+=change;if(prop.body!=null)prop.body.position=prop.transform.position;}
            if(StableSeconds<StableSecondsRequired)return;
            Solved=true;BalanceCompleted?.Invoke();Reward?.CompleteBalance();
            game.Tell("균형을 되찾았어요. 디케가 놓아준 검을 집어 방 가운데 대리석 받침에 꽂으세요.",7);
        }
        void OnDestroy(){if(previewMaterial!=null)Destroy(previewMaterial);}
    }
}
