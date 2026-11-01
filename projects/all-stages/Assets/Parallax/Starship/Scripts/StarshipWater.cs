using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipWater : MonoBehaviour
    {
        public Vector2 min,max;
        public Vector3 flow;
        public float surface=-.04f;
        public bool transportChannel;
        public Vector3 transportOrigin;
        static StarshipWater[] all;
        StarshipGame game;
        public bool Contains(Vector3 p)=>p.x>=min.x&&p.x<=max.x&&p.z>=min.y&&p.z<=max.y;
        void Start(){all=FindObjectsByType<StarshipWater>(FindObjectsSortMode.None);game=FindFirstObjectByType<StarshipGame>();}
        public static bool InHazard(Vector3 p)
        {
            if(all==null)return false;
            foreach(var w in all)if(w!=null&&w.Contains(p)&&p.y<w.surface+.035f)return true;
            return false;
        }
        public static bool Supports(StarshipProp p)
        {
            if(all==null||!p.buoyant||p.body==null||p.body.isKinematic)return false;
            var b=p.WorldBounds;
            foreach(var w in all)if(w!=null&&w.Contains(b.center)&&b.min.y<w.surface+.015f&&b.max.y>w.surface)return true;
            return false;
        }
        public void Simulate(float dt)
        {
            if(game==null)game=FindFirstObjectByType<StarshipGame>();
            if(game==null||!game.active||game.won)return;
            foreach(var p in game.props)
            {
                if(p.body==null||p.body.isKinematic||!p.buoyant)continue;
                Bounds bounds=p.WorldBounds;
                if(!Contains(bounds.center)||bounds.min.y>surface+.015f||bounds.max.y<surface-2)continue;
                float immersed=Mathf.Clamp01((surface-bounds.min.y)/Mathf.Max(.08f,bounds.size.y));
                p.floating=true;
                float lift=9.81f*immersed*2.8f-p.body.linearVelocity.y*3.8f;
                p.body.AddForce(Vector3.up*lift,ForceMode.Acceleration);
                Vector3 velocity=p.body.linearVelocity;
                Vector3 desired=Vector3.MoveTowards(new Vector3(velocity.x,0,velocity.z),flow,2.6f*dt);
                p.body.linearVelocity=new Vector3(desired.x,velocity.y,desired.z);
                p.body.angularVelocity*=Mathf.Exp(-2.6f*dt);
                if(transportChannel)
                {
                    float travel=Vector3.Dot(p.transform.position-p.lastDriftPosition,flow.normalized);
                    if(travel>0&&travel<.5f)p.driftDistance+=travel;
                    if(p.placement.Ready&&Vector3.Dot(bounds.center-transportOrigin,flow.normalized)>7.0f&&p.driftDistance>6.5f&&!game.waterGate.unlocked)
                    {game.waterGate.unlocked=true;game.Tell("냉각수 운반 완료 · 반대편 사물을 회수해 재사용하세요. 정비 통로 잠금이 해제되었습니다.",7);}
                }
                p.lastDriftPosition=p.transform.position;
            }
        }
        void FixedUpdate(){Simulate(Time.fixedDeltaTime);}
        void OnDestroy(){all=null;}
    }
}
