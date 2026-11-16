using System;
using UnityEngine;

namespace ShadeLink.Beach
{
    // Real placed weight releases a mechanical catch. The complete roofed span
    // is raised from deep water; the player never has to align a rock shadow.
    public sealed class BeachCounterweightFinale : MonoBehaviour
    {
        public BeachGame game;
        public BeachProp rock;
        public Transform cradle,bridge;
        public BoxCollider cradleFloor,bridgeFloor;
        public Renderer[] bridgeCanopyRenderers;
        public Transform[] ropeStartPoints,ropeEndPoints,ropeGuides;
        public LineRenderer[] ropes;
        public Vector3 cradleRaisedPosition,bridgeRaisedPosition,bridgeNearPoint,bridgeFarPoint;
        public float travel=9.2f,liftSeconds=7,minimumLoadMass=280,roofClearance=4.2f,waterSurface=-.04f;
        public Vector3[] routePoints;
        public Renderer[] lamps;
        public Vector3 observationPoint=new Vector3(-8,.025f,77),pickupPoint=new Vector3(-6.2f,.025f,78);
        public Vector3 clearPoint=new Vector3(-2,.025f,80.3f),adjustmentPoint=new Vector3(-.5f,.025f,80.3f);
        public Vector2 reviewLookDegrees=new Vector2(40,-1);
        public float maximumEdgeAssist=.65f,maximumDropHeight=1.45f;
        [SerializeField] float liftProgress,loadStableTime;
        [SerializeField] bool ready,loadSupported,enoughWeight,carrying,loadLatched;
        [SerializeField] Renderer[] bridgeRenderers;
        [SerializeField] bool[] bridgeOriginalVisibility;
        readonly RaycastHit[] supportHits=new RaycastHit[64];
        readonly Collider[] overlaps=new Collider[96];
        Vector3 carrierPosition,pickupCarrierPosition;
        Quaternion carrierRotation,pickupCarrierRotation;
        bool initialized,initializedPickup,pickupCarrying,assistPending,movementBlocked;
        float pickupLiftProgress,recoveryCooldown;
        int assistedPlacements;


        public BeachProp Rock=>rock;
        public bool Ready=>ready;
        public bool LoadLatched=>loadLatched;
        public bool ReleaseLocked=>loadLatched;
        public float LiftProgress=>liftProgress;
        public bool LoadSupported=>loadSupported;
        public bool EnoughWeight=>enoughWeight;
        public bool Carrying=>carrying;
        public bool MovementBlocked=>movementBlocked;
        public string LastMovementObstacle {get;private set;}
        public bool PlayerOnBridgeNow=>PlayerOnBridge();
        public float RequiredMass=>minimumLoadMass;
        public int AssistedPlacements=>assistedPlacements;
        public Vector3 LastAssistOffset {get;private set;}
        public Vector3 LastShadeFailure {get;private set;}
        public string LastRouteFailure {get;private set;}
        public int LastRockShadeSamples {get;private set;}
        public Vector3 ObservationPoint=>observationPoint;
        public Vector3 PickupPoint=>pickupPoint;
        public Vector3[] RouteWaypoints=>routePoints??Array.Empty<Vector3>();
        public Vector3[] PreviewPoints=>Array.Empty<Vector3>();
        public bool PreviewVisible=>false;
        public BoxCollider BridgeFloor=>bridgeFloor;
        public Renderer[] BridgeCanopyRenderers=>bridgeCanopyRenderers??Array.Empty<Renderer>();
        public Vector3 BridgeNearPoint=>bridgeNearPoint;
        public Vector3 BridgeFarPoint=>bridgeFarPoint;
        public float RoofClearance=>roofClearance;
        public float WaterSurface=>waterSurface;
        public float CradleTop=>cradleFloor==null?cradle.position.y:cradleFloor.bounds.max.y;
        public Vector3 FinalRockOffset=>Vector3.down*Mathf.Max(0,travel)*(1-liftProgress);
        public Bounds BridgeVisualBounds
        {
            get
            {
                Bounds bounds=new Bounds(bridge==null?Vector3.zero:bridge.position,Vector3.zero);bool first=true;
                if(bridgeRenderers!=null)foreach(var r in bridgeRenderers)
                    if(r!=null){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                return bounds;
            }
        }
        public string Status=>ready?"다리 인양 완료 · 천막 아래로 건너 배 갑판에서 E 출항":
            loadLatched?"추 받침 ↓ · 천막 다리 전체 ↑ "+Mathf.RoundToInt(liftProgress*100)+"%":
            game!=null&&game.held==rock?"키운 바위의 밑부분을 옆 추 받침 안에 놓으세요":
            movementBlocked?"바위가 주변 구조물에 걸렸어요 · R로 다시 시도할 수 있어요":
            loadSupported&&!enoughWeight?"무게가 부족해요 · 바위를 원근으로 더 키워 놓으세요":
            loadStableTime>0?"바위가 추 받침에 안착하는 중이에요":"해안의 바위를 키워 옆 추 받침에 올려 보세요";
        public string Hint=>"바닷길은 천막까지 물속에 잠겨 있어요.\n"
            +"바위를 들고 경사로 오른쪽 모래사장에서 원근으로 키우세요.\n"
            +"옆 추 받침에 놓으면 받침은 내려가고 천막 다리가 함께 올라와요.\n"
            +"인양이 끝나면 천막 아래 길을 따라 배에 올라 E로 출항하세요.";
        public static BeachCounterweightFinale Build(BeachGame value)=>BeachCounterweightFinaleBuilder.Build(value.transform,value);

        // Called only while constructing the scene. Original visibility is
        // serialized so underwater hiding never loses the real render state.
        public void PrepareInitialGeometry()
        {
            CaptureBridgeVisibility();
            ApplyMechanism(0,false,false);
            SetLamps(false);
        }
        void CaptureBridgeVisibility()
        {
            if(bridge==null)return;
            if(bridgeRenderers!=null&&bridgeOriginalVisibility!=null&&bridgeRenderers.Length==bridgeOriginalVisibility.Length&&bridgeRenderers.Length>0)return;
            bridgeRenderers=bridge.GetComponentsInChildren<Renderer>(true);bridgeOriginalVisibility=new bool[bridgeRenderers.Length];
            for(int i=0;i<bridgeRenderers.Length;i++)bridgeOriginalVisibility[i]=bridgeRenderers[i]!=null&&bridgeRenderers[i].enabled;
        }
        bool UpdateBridgeVisibility(bool rebuild)
        {
            CaptureBridgeVisibility();bool changed=false;
            bool emerged=BridgeVisualBounds.max.y>waterSurface+.04f;
            for(int i=0;i<bridgeRenderers.Length;i++)
            {
                var r=bridgeRenderers[i];if(r==null)continue;
                // The player cannot dive. Fully submerged surfaces are hidden
                // behind the sea while all real physical colliders stay active.
                // Reveal the assembly once as its roof breaches the surface;
                // rebuilding every per-plank shadow proxy would cause stalls.
                bool visible=bridgeOriginalVisibility[i]&&emerged;
                if(r.enabled!=visible){r.enabled=visible;changed=true;}
            }
            if(changed&&rebuild&&Application.isPlaying&&game!=null&&game.lighting!=null)game.lighting.Rebuild();
            return changed;
        }
        public void Initialize()
        {
            if(initialized)return;
            if(game==null)game=GetComponentInParent<BeachGame>();
            if(rock==null||cradle==null||bridge==null||cradleFloor==null||bridgeFloor==null)
                throw new InvalidOperationException("Submerged hoist requires real rock, receiving floor and covered bridge floor.");
            initialized=true;rock.InitializePhysics();CaptureBridgeVisibility();ResetPuzzle();
        }
        public void ResetPuzzle()
        {
            ready=carrying=loadSupported=enoughWeight=loadLatched=initializedPickup=assistPending=movementBlocked=false;
            liftProgress=loadStableTime=recoveryCooldown=0;LastRouteFailure=LastMovementObstacle=null;
            LastRockShadeSamples=0;LastAssistOffset=Vector3.zero;
            if(rock!=null&&rock.body!=null)rock.Restore();ApplyMechanism(0,false);SetLamps(false);game?.lighting?.Synchronize();
        }
        public void OnPickedUp(BeachProp value)
        {
            if(value!=rock||loadLatched)return;
            initializedPickup=true;pickupCarrying=carrying;pickupLiftProgress=liftProgress;
            pickupCarrierPosition=carrierPosition;pickupCarrierRotation=carrierRotation;
            carrying=false;ready=loadSupported=enoughWeight=false;loadStableTime=0;assistPending=false;SetLamps(false);
        }
        public void OnInteractionFinished(bool released)
        {
            if(!initializedPickup)return;
            if(released){if(assistPending)assistedPlacements++;carrying=false;loadStableTime=0;}
            else{liftProgress=pickupLiftProgress;carrierPosition=pickupCarrierPosition;carrierRotation=pickupCarrierRotation;carrying=pickupCarrying;ApplyMechanism(liftProgress,carrying);}
            initializedPickup=assistPending=false;
        }
        Bounds LowestFootprint()
        {
            Bounds whole = rock.WorldBounds, result = whole;
            bool first = true;
            foreach (var part in rock.parts)
            {
                if (part == null || !part.enabled || part.isTrigger) continue;
                Bounds b = part.bounds;
                if (b.min.y > whole.min.y + .08f) continue;
                if (first) { result = b; first = false; } else result.Encapsulate(b);
            }
            return result;
        }

        float ReceiverSurfaceTop(Bounds foot)
        {
            float top = CradleTop;
            foreach (var shape in cradle.GetComponentsInChildren<BoxCollider>())
            {
                if (shape == null || !shape.enabled || shape.isTrigger) continue;
                Bounds b = shape.bounds;
                if (b.max.y > CradleTop + .25f || b.max.y < CradleTop - .01f) continue;
                if (b.max.x >= foot.min.x && b.min.x <= foot.max.x &&
                    b.max.z >= foot.min.z && b.min.z <= foot.max.z)
                    top = Mathf.Max(top, b.max.y);
            }
            return top;
        }

        public bool TryAssistRelease(BeachProp value)
        {
            assistPending = false; LastAssistOffset = Vector3.zero;
            if (loadLatched || value != rock || game == null || game.held != rock || cradleFloor == null ||
                !cradleFloor.enabled || !cradleFloor.gameObject.activeInHierarchy) return false;
            Bounds tray = cradleFloor.bounds, stone = rock.WorldBounds;
            if (!Finite(stone.center) || !Finite(stone.size) || !Finite(rock.transform.localScale)) return false;
            const float overhang = .40f, centreInset = .18f;
            if (stone.size.x > tray.size.x + overhang * 2 || stone.size.z > tray.size.z + overhang * 2) return false;
            float bottomError = stone.min.y - tray.max.y;
            if (bottomError < -.10f || bottomError > maximumDropHeight) return false;
            Bounds foot = LowestFootprint();
            // Permit modest real overhang, while keeping the body centre over
            // the physical tray. Keep the western edge clear of the nearby
            // ramp roof; the other edges permit modest overhang over deep sea.
            // Only outlying poses receive a small correction.
            float loX = Mathf.Max(tray.min.x + centreInset - stone.center.x,
                Mathf.Max(tray.min.x + .08f - foot.min.x, tray.min.x + .08f - stone.min.x));
            float hiX = Mathf.Min(tray.max.x - centreInset - stone.center.x,
                Mathf.Min(tray.max.x + overhang - foot.max.x, tray.max.x + overhang - stone.max.x));
            float loZ = Mathf.Max(tray.min.z + centreInset - stone.center.z,
                Mathf.Max(tray.min.z - overhang - foot.min.z, tray.min.z - overhang - stone.min.z));
            float hiZ = Mathf.Min(tray.max.z - centreInset - stone.center.z,
                Mathf.Min(tray.max.z + overhang - foot.max.z, tray.max.z + overhang - stone.max.z));
            if (loX > hiX || loZ > hiZ) return false;
            float dx = Mathf.Clamp(0, loX, hiX), dz = Mathf.Clamp(0, loZ, hiZ);
            foot.center += new Vector3(dx, 0, dz);
            float targetTop = ReceiverSurfaceTop(foot);
            Vector3 correction = new Vector3(dx, targetTop + .025f - stone.min.y, dz);
            if (new Vector2(correction.x, correction.z).magnitude > maximumEdgeAssist) return false;
            if (correction.sqrMagnitude < .000001f) return false;
            var alignment = new BeachShadowDropAssist.Alignment {
                position = rock.transform.position + correction,
                rotation = rock.transform.rotation, scale = rock.transform.localScale
            };
            // The shared sweep checks every part along this modest correction.
            if (!game.TryApplyDropAlignment(rock, alignment)) return false;
            LastAssistOffset = correction; assistPending = true; return true;
        }


        public void Tick(float dt)
        {
            if(!initialized)Initialize();
            if(game==null||!game.active||game.won)return;
            dt=Mathf.Clamp(dt,0,.05f);recoveryCooldown=Mathf.Max(0,recoveryCooldown-dt);
            if(game.held==rock){ready=loadSupported=enoughWeight=false;loadStableTime=0;UpdateRopes();SetLamps(false);return;}
            if(!Finite(rock.transform.position)||!Finite(rock.transform.localScale)){if(!loadLatched)RecoverRock();return;}
            loadSupported=SupportedByCradle();
            if(!loadLatched&&!loadSupported&&LostToWater())
            {
                Bounds wet=rock.WorldBounds;
                game.Audio?.Ending?.TryRockWater(rock,new Vector3(wet.center.x,waterSurface,wet.center.z));
                RecoverRock();return;
            }
            enoughWeight=loadSupported&&rock.body.mass>=minimumLoadMass;
            if(!loadLatched)
            {
                bool stable=loadSupported&&rock.placement!=null&&rock.placement.Ready&&
                    (rock.body.isKinematic||rock.body.IsSleeping()||(Mathf.Abs(rock.body.linearVelocity.y)<.25f&&rock.body.angularVelocity.magnitude<.6f));
                loadStableTime=stable?Mathf.Min(.4f,loadStableTime+dt):0;
                if(enoughWeight&&loadStableTime>=.25f)
                {
                    carrierPosition=cradle.InverseTransformPoint(rock.transform.position);carrierRotation=Quaternion.Inverse(cradle.rotation)*rock.transform.rotation;
                    rock.body.linearVelocity=Vector3.zero;rock.body.angularVelocity=Vector3.zero;rock.body.isKinematic=true;rock.body.useGravity=false;
                    rock.body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;PropReleasePhysics.Attach(rock.body).CancelPendingRelease();
                    carrying=loadLatched=true;game.Tell("무게가 걸렸어요 · 다리와 천막이 함께 올라옵니다.",7);
                }
            }
            float next=Mathf.MoveTowards(liftProgress,loadLatched?1:0,dt/Mathf.Max(.1f,liftSeconds));movementBlocked=false;
            if(carrying&&Mathf.Abs(next-liftProgress)>.000001f)
            {
                Vector3 nextCradle=cradleRaisedPosition-Vector3.up*travel*next;
                Vector3 nextStone=nextCradle+cradle.rotation*Vector3.Scale(carrierPosition,cradle.lossyScale);
                if(!PayloadClearAt(nextStone)){next=liftProgress;movementBlocked=true;}
            }
            liftProgress=next;ApplyMechanism(liftProgress,carrying);game.lighting?.Synchronize();
            // A ratchet catches the actual loaded mechanism. Shade is supplied
            // automatically by the same physical canopy and is not a target.
            ready=loadLatched&&liftProgress>=.999f&&!movementBlocked;SetLamps(ready);
        }
        void ApplyMechanism(float progress,bool moveStone,bool rebuildLighting=true)
        {
            if(cradle==null||bridge==null)return;
            cradle.position=cradleRaisedPosition-Vector3.up*Mathf.Max(0,travel)*progress;
            bridge.position=bridgeRaisedPosition-Vector3.up*Mathf.Max(0,travel)*(1-progress);
            if(moveStone&&rock!=null&&rock.body!=null)
            {
                Vector3 position=cradle.TransformPoint(carrierPosition);Quaternion rotation=cradle.rotation*carrierRotation;
                rock.transform.SetPositionAndRotation(position,rotation);rock.body.position=position;rock.body.rotation=rotation;
            }
            Physics.SyncTransforms();UpdateRopes();UpdateBridgeVisibility(rebuildLighting);
        }
        bool SupportedByCradle()
        {
            if (rock == null || rock.body == null || game.held == rock) return false;
            Bounds b = rock.WorldBounds, tray = cradleFloor.bounds;
            if (b.center.x < tray.min.x - .12f || b.center.x > tray.max.x + .12f ||
                b.center.z < tray.min.z - .12f || b.center.z > tray.max.z + .12f ||
                Mathf.Abs(b.min.y - tray.max.y) > .22f) return false;
            foreach (var part in rock.parts)
            {
                if (part == null || !part.enabled || part.isTrigger) continue;
                Vector3 half = Vector3.Scale(part.size, part.transform.lossyScale) * .5f;
                half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
                int count = Physics.BoxCastNonAlloc(part.transform.TransformPoint(part.center) + Vector3.up * .035f,
                    Vector3.Max(Vector3.one * .001f, half - Vector3.one * .003f),
                    Vector3.down, supportHits, part.transform.rotation, .22f, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (supportHits[i].collider != null && supportHits[i].normal.y > .5f &&
                        (supportHits[i].collider == cradleFloor || supportHits[i].collider.transform.IsChildOf(cradle)))
                        return true;
            }
            return false;
        }

        bool PayloadClearAt(Vector3 position)
        {
            LastMovementObstacle = null;
            Vector3 delta = position - rock.transform.position;
            foreach (var part in rock.parts)
            {
                if (part == null || !part.enabled) continue;
                Vector3 half = Vector3.Scale(part.size, part.transform.lossyScale) * .5f;
                half = Vector3.Max(Vector3.one * .001f, new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) - Vector3.one * .012f);
                int count = Physics.OverlapBoxNonAlloc(part.transform.TransformPoint(part.center) + delta,
                    half, overlaps, part.transform.rotation, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore);
                if (count >= overlaps.Length) { LastMovementObstacle = "Payload overlap buffer full"; return false; }
                for (int i = 0; i < count; i++)
                    if (overlaps[i] != null && overlaps[i].attachedRigidbody != rock.body &&
                        !overlaps[i].transform.IsChildOf(cradle))
                    {
                        LastMovementObstacle = (overlaps[i].transform.parent != null ? overlaps[i].transform.parent.name + "/" : "") + overlaps[i].name;
                        return false;
                    }
            }
            return true;
        }

        bool PlayerOnBridge()
        {
            if (game.controller == null || !game.controller.enabled) return false;
            foreach (var hit in Physics.RaycastAll(game.controller.transform.position + Vector3.up * .18f,
                Vector3.down, .5f, BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore))
                if (hit.normal.y > .6f && hit.collider.transform.IsChildOf(bridge)) return true;
            return false;
        }

        bool LostToWater()
        {
            Bounds b = rock.WorldBounds;
            return rock.transform.position.y < -3 || b.min.y < -.45f ||
                BeachWater.InHazard(new Vector3(b.center.x, b.min.y + .12f, b.center.z));
        }

        void RecoverRock()
        {
            if (game.held == rock) return;
            rock.Restore(); carrying = loadSupported = enoughWeight = ready = false;
            loadStableTime = 0;
            game.lighting?.Synchronize();
            if (recoveryCooldown <= 0)
            { game.Tell("바다에 떨어진 바위를 원래 해안가로 돌려놓았어요.", 4); recoveryCooldown = 4; }
        }

        public bool RouteShadeContinuous()
        {
            LastRockShadeSamples = 0;
            if (game?.lighting == null || routePoints == null || routePoints.Length < 2)
            { LastRouteFailure = "Missing route"; return false; }
            // Sample the actual walking corridor on both sides of EACH span.
            // World-X offsets miss the width of an east/west connector.
            for (int segment = 0; segment + 1 < routePoints.Length; segment++)
            {
                Vector3 a = routePoints[segment], b = routePoints[segment + 1];
                Vector3 horizontal = Vector3.ProjectOnPlane(b - a, Vector3.up);
                Vector3 side = horizontal.sqrMagnitude > .000001f ?
                    Vector3.Cross(Vector3.up, horizontal.normalized) : Vector3.right;
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .23f));
                for (int step = 0; step <= count; step++)
                    foreach (float across in new[] { -.22f, 0, .22f })
                    {
                        Vector3 q = Vector3.Lerp(a, b, (float)step / count) + side * across;
                        if (BeachWater.InHazard(q)) return FailRoute(q, "Water hazard");
                        if (!Physics.Raycast(q + Vector3.up * .16f, Vector3.down, out var hit, .46f,
                            BeachWorld.PhysicalMask, QueryTriggerInteraction.Ignore) ||
                            hit.collider == game.controller || hit.normal.y < .65f ||
                            !PlacementSupport.IsUsableGround(hit.collider))
                            return FailRoute(q, "Missing walkable support");
                        int blocking = Physics.OverlapCapsuleNonAlloc(hit.point + Vector3.up * .36f,
                            hit.point + Vector3.up * 1.45f, .30f, overlaps, BeachWorld.PhysicalMask,
                            QueryTriggerInteraction.Ignore);
                        if (blocking >= overlaps.Length) return FailRoute(q, "Walking clearance query overflow");
                        for (int i = 0; i < blocking; i++)
                            if (overlaps[i] != null && overlaps[i].attachedRigidbody == rock.body)
                                return FailRoute(q, "Rock blocks walking clearance");
                        Vector3 feet = hit.point + Vector3.up * .025f;
                        // Match the real player's centre and four foot samples,
                        // not merely a single shaded centre point.
                        if (!game.FootSafe(feet))
                            return FailRoute(q, "Player footprint reaches direct or reflected sunlight");
                        if (game.lighting.TryGetDirectShadowCaster(feet, hit.normal, out var owner) && owner == rock)
                            LastRockShadeSamples++;
                    }
            }

            LastRouteFailure = null; return true;
        }

        bool FailRoute(Vector3 point, string reason)
        { LastShadeFailure = point; LastRouteFailure = reason; return false; }

        void UpdateRopes()
        {
            if (ropes == null || ropeStartPoints == null || ropeEndPoints == null) return;
            int count = Mathf.Min(ropes.Length, Mathf.Min(ropeStartPoints.Length, ropeEndPoints.Length));
            for (int i = 0; i < count; i++)
            {
                if (ropes[i] == null || ropeStartPoints[i] == null || ropeEndPoints[i] == null) continue;
                ropes[i].useWorldSpace = true;
                if (ropeGuides != null && ropeGuides.Length > i * 2 + 1 &&
                    ropeGuides[i * 2] != null && ropeGuides[i * 2 + 1] != null)
                {
                    ropes[i].positionCount = 4;
                    ropes[i].SetPositions(new[] { ropeStartPoints[i].position, ropeGuides[i * 2].position,
                        ropeGuides[i * 2 + 1].position, ropeEndPoints[i].position });
                }
                else
                { ropes[i].positionCount = 2; ropes[i].SetPositions(new[] { ropeStartPoints[i].position, ropeEndPoints[i].position }); }
            }
        }

        void SetLamps(bool complete)
        {
            if (lamps == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_EmissionColor", complete ? new Color(.04f, .42f, .22f) :
                liftProgress >= .999f ? new Color(.28f, .11f, .015f) : Color.black);
            foreach (var lamp in lamps) if (lamp != null) lamp.SetPropertyBlock(block);
        }

        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
    }
}