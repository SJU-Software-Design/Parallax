using System;
using UnityEngine;

namespace ShadeLink.Beach
{
    // The boat and its real shade canopy form one perspective-scaled prop.
    // Buoyancy deliberately measures only the hull, never the tall canopy.
    public sealed class BeachBoat : MonoBehaviour
    {
        public BeachProp Prop;
        public BeachGame Game;
        public BoxCollider[] HullColliders = new BoxCollider[0];
        public BoxCollider ArrivalPier;
        public LineRenderer MooringRope;
        public float ArrivalBowZ = 67.85f;
        public Vector3 DepartureMooring = new Vector3(2.5f,.65f,55.15f);
        public Vector3 ArrivalMooring = new Vector3(2.5f,.65f,68.8f);
        public bool Departed { get; private set; }
        public bool Arrived { get; private set; }
        public bool AtBerth { get; private set; }
        public float Travelled { get; private set; }
        Vector3 launchPosition,launchScale,lastPosition;
        Quaternion launchRotation;
        bool launchSaved,recovering;
        int observedRespawns;

        public Bounds HullBounds
        {
            get
            {
                Bounds bounds=new Bounds(transform.position,Vector3.zero);bool first=true;
                foreach(var part in HullColliders)
                {
                    if(part==null)continue;
                    Vector3 half=part.size*.5f;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 local=part.center+Vector3.Scale(half,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        Vector3 point=part.transform.TransformPoint(local);
                        if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
        }
        public bool IsAboard
        {
            get
            {
                EnsureReferences();
                if(Game==null||Game.controller==null||Game.player==null||!Game.player.grounded||Prop==null||Prop.placement==null||!Prop.placement.Ready)return false;
                var feet=Game.controller.transform.position;
                return Physics.Raycast(feet+Vector3.up*.18f,Vector3.down,out var hit,.42f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                    &&hit.collider.GetComponentInParent<BeachProp>()==Prop;
            }
        }
        public bool IsOnArrivalLanding
        {
            get
            {
                EnsureReferences();
                if(ArrivalPier==null||Game==null||Game.controller==null||Game.player==null||!Game.player.grounded)return false;
                Vector3 feet=Game.controller.transform.position;
                if(!Physics.Raycast(feet+Vector3.up*.18f,Vector3.down,out var hit,.42f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                    ||hit.normal.y<.6f||!PlacementSupport.IsUsableGround(hit.collider))return false;
                if(hit.collider==ArrivalPier)return true;
                // The pier meets the solid beach. A running landing may already
                // be on that short forecourt by the time the boat is sampled.
                Bounds landing=ArrivalPier.bounds;
                return hit.collider.GetComponentInParent<BeachProp>()==null
                    &&feet.x>=landing.min.x&&feet.x<=landing.max.x
                    &&feet.z>=landing.min.z&&feet.z<=landing.max.z+.6f;
            }
        }
        bool NearArrivalPier(float gap)
        {
            Bounds hull=HullBounds;
            if(ArrivalPier==null)return hull.max.z>=ArrivalBowZ&&Mathf.Abs(hull.center.x)<2.7f;
            Bounds pier=ArrivalPier.bounds;
            return hull.max.z>=pier.min.z-gap&&hull.min.z<=pier.max.z
                &&hull.max.x>=pier.min.x-gap&&hull.min.x<=pier.max.x+gap;
        }
        public string Status
        {
            get
            {
                EnsureReferences();
                if(!gameObject.activeInHierarchy)return "그림자를 완성하면 작은 배가 나타납니다";
                if(Arrived)return "도착 부두에 정박했습니다 · 그늘을 따라 내려 모래성으로 가세요";
                if(AtBerth)return "도착 부두에 닿았습니다 · 안전한 부두에 내리면 길이 열립니다";
                if(Departed)return "그늘 안에서 항해 중 · 앞쪽 도착 부두로 이동합니다";
                if(Prop!=null&&Prop.placement!=null&&Prop.placement.Held)return "배 전체를 원근으로 확대해 출항 부두 앞 물에 놓으세요";
                if(Prop!=null&&BeachWater.Supports(Prop))return IsAboard?"그늘 안에 승선했습니다 · E를 눌러 계류 밧줄을 풀고 출항":"계류 중 · 배의 그늘진 갑판에 오른 뒤 E로 출항";
                return "작은 배를 잡아 크게 만든 뒤 출항 부두 앞 물에 놓으세요";
            }
        }
        void EnsureReferences()
        {
            if(Prop==null)Prop=GetComponent<BeachProp>();
            if(Game==null)Game=GetComponentInParent<BeachGame>();
        }
        public Vector3 FlowFor(Vector3 current) => Departed&&!AtBerth?current:Vector3.zero;

        public void OnPickedUp()
        {
            Departed=Arrived=AtBerth=false;Travelled=0;launchSaved=false;
            if(MooringRope!=null)MooringRope.enabled=false;
        }
        public void OnReleased()
        {
            EnsureReferences();Departed=Arrived=AtBerth=false;Travelled=0;lastPosition=transform.position;launchSaved=false;
            SetMoored(true);
        }
        public void OnPropRestored()
        {
            Departed=Arrived=AtBerth=false;Travelled=0;launchSaved=false;lastPosition=transform.position;
            if(Prop!=null&&Prop.body!=null)Prop.body.constraints=RigidbodyConstraints.FreezeRotation;
            if(MooringRope!=null)MooringRope.enabled=false;
            if(Game!=null)observedRespawns=Game.respawns;
        }
        void SetMoored(bool moored)
        {
            if(Prop==null||Prop.body==null)return;
            Prop.body.constraints=RigidbodyConstraints.FreezeRotation|(moored?RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ:RigidbodyConstraints.None);
            if(moored&&!Prop.body.isKinematic)
            {
                Vector3 velocity=Prop.body.linearVelocity;Prop.body.linearVelocity=new Vector3(0,velocity.y,0);
            }
        }
        public bool TryLaunch()
        {
            EnsureReferences();
            if(Game==null||Prop==null||!gameObject.activeInHierarchy)return false;
            if(Arrived){Game.Tell("이미 도착 부두에 정박했습니다. 그늘을 따라 내려 주세요.");return false;}
            if(Departed)return true;
            if(Game.held!=null){Game.Tell("배를 내려놓고 갑판에 오른 뒤 출항하세요.");return false;}
            if(Prop.placement==null||!Prop.placement.Ready||!BeachWater.Supports(Prop))
            {Game.Tell("출항 부두 앞 물에 배를 놓고, 떠서 안착할 때까지 기다려 주세요.");return false;}
            if(Mathf.Abs(HullBounds.center.x)>5||HullBounds.center.z<54||HullBounds.center.z>60)
            {Game.Tell("출항 부두 앞 물길에서 출항할 수 있습니다. 배를 부두 가까이 놓아 주세요.");return false;}
            if(!IsAboard){Game.Tell("배의 갑판에 먼저 올라 주세요.");return false;}
            if(!Game.FootSafe(Game.player.feet)){Game.Tell("갑판의 실제 그늘 안으로 이동한 뒤 출항하세요.");return false;}
            launchPosition=transform.position;launchRotation=transform.rotation;launchScale=transform.localScale;launchSaved=true;
            lastPosition=transform.position;Travelled=0;Departed=true;AtBerth=Arrived=false;SetMoored(false);
            if(MooringRope!=null)MooringRope.enabled=false;
            Game.Tell("밧줄을 풀었습니다 · 배의 그늘 안에서 도착 부두까지 이동하세요.",6);
            return true;
        }
        public void Tick(float dt)
        {
            EnsureReferences();if(Prop==null||Game==null||!gameObject.activeInHierarchy)return;
            if(Game.respawns!=observedRespawns){observedRespawns=Game.respawns;HandleRespawn();}
            if(Prop.body==null||Prop.body.isKinematic){if(MooringRope!=null)MooringRope.enabled=false;return;}
            if(Departed&&!AtBerth)
            {
                float advance=transform.position.z-lastPosition.z;
                if(advance>0&&advance<.5f)Travelled+=advance;
                // Physics contact offsets, or a disembarked player at the edge,
                // can stop the bow just short of a single exact Z threshold.
                // A genuinely grounded landing also completes the voyage.
                bool landed=Travelled>1&&IsOnArrivalLanding&&NearArrivalPier(2);
                if(NearArrivalPier(.16f)||landed)
                {AtBerth=true;SetMoored(true);Game.Tell("도착 부두에 닿았습니다 · 그늘을 따라 배에서 내려 주세요.",6);}
            }
            // Launch already requires real shade. The ordinary exposure/water
            // rules stay active throughout travel; arrival must not require a
            // second same-frame shade sample after the rider has stepped ashore.
            if(AtBerth&&!Arrived&&Departed&&Travelled>1&&(IsAboard||IsOnArrivalLanding))
            {
                Arrived=true;
                if(Game.waterGate!=null)Game.waterGate.unlocked=true;
                Game.Tell("그늘 배로 도착했습니다 · 열린 길을 따라 모래성 축제로 가세요.",7);
            }
            lastPosition=transform.position;
            UpdateRope();
        }
        void UpdateRope()
        {
            if(MooringRope==null)return;
            bool visible=!Departed||AtBerth;
            visible=visible&&Prop!=null&&Prop.placement!=null&&!Prop.placement.Held&&BeachWater.Supports(Prop);
            MooringRope.enabled=visible;if(!visible)return;
            Vector3 anchor=AtBerth?ArrivalMooring:DepartureMooring;
            Vector3 attachment=transform.TransformPoint(AtBerth?new Vector3(.30f,.20f,.90f):new Vector3(.58f,.20f,-.80f));
            MooringRope.positionCount=3;MooringRope.SetPosition(0,anchor);MooringRope.SetPosition(1,(anchor+attachment)*.5f-Vector3.up*.12f);MooringRope.SetPosition(2,attachment);
        }
        public void ResetBoat()
        {
            EnsureReferences();if(Prop!=null)Prop.Restore();else OnPropRestored();
        }
        public void HandleRespawn()
        {
            EnsureReferences();
            if(recovering||!Departed||!launchSaved||Prop==null||(Arrived&&Game!=null&&Game.checkpoint>=3))return;
            recovering=true;
            bool undoArrival=Arrived;
            Vector3 position=launchPosition,scale=launchScale;Quaternion rotation=launchRotation;
            Prop.Restore();Prop.transform.SetPositionAndRotation(position,rotation);Prop.transform.localScale=scale;
            Prop.body.position=position;Prop.body.rotation=rotation;Prop.RefreshMass();Physics.SyncTransforms();Prop.Release();
            if(undoArrival&&Game!=null&&Game.waterGate!=null)Game.waterGate.Restore();
            observedRespawns=Game==null?0:Game.respawns;recovering=false;
        }
        public Action CaptureState()
        {
            bool departed=Departed,arrived=Arrived,berth=AtBerth,saved=launchSaved;
            float travelled=Travelled;Vector3 start=launchPosition,scale=launchScale,last=lastPosition;Quaternion rotation=launchRotation;
            return ()=>
            {
                Departed=departed;Arrived=arrived;AtBerth=berth;launchSaved=saved;Travelled=travelled;
                launchPosition=start;launchScale=scale;launchRotation=rotation;lastPosition=last;UpdateRope();
            };
        }
    }
}
