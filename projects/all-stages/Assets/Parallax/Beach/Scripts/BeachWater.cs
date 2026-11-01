using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachWater : MonoBehaviour
    {
        public Vector2 min,max;
        public Vector3 flow;
        public float surface=-.04f;
        public bool transportChannel;
        static BeachWater[] all;
        BeachGame game;
        public bool Contains(Vector3 p)=>p.x>=min.x&&p.x<=max.x&&p.z>=min.y&&p.z<=max.y;
        void Start(){all=FindObjectsByType<BeachWater>(FindObjectsSortMode.None);game=FindFirstObjectByType<BeachGame>();}
        public static bool InHazard(Vector3 p)
        {
            if(all==null)return false;
            foreach(var w in all)if(w!=null&&w.Contains(p)&&p.y<w.surface+.035f)return true;
            return false;
        }
        public static bool Supports(BeachProp p)
        {
            if(all==null||!p.buoyant||p.body==null||p.body.isKinematic)return false;
            var boat=p.GetComponent<BeachBoat>();var b=boat!=null?boat.HullBounds:p.WorldBounds;
            foreach(var w in all)if(w!=null&&w.Contains(b.center)&&b.min.y<w.surface+.015f&&b.max.y>w.surface)return true;
            return false;
        }
        public void Simulate(float dt)
        {
            if(game==null)game=FindFirstObjectByType<BeachGame>();
            if(game==null||!game.active||game.won)return;
            foreach(var p in game.props)
            {
                if(!p.gameObject.activeInHierarchy||p.body==null||p.body.isKinematic||!p.buoyant)continue;
                var boat=p.GetComponent<BeachBoat>();Bounds bounds=boat!=null?boat.HullBounds:p.WorldBounds;
                if(!Contains(bounds.center)||bounds.min.y>surface+.015f||bounds.max.y<surface-2)continue;
                float immersed=Mathf.Clamp01((surface-bounds.min.y)/Mathf.Max(.08f,bounds.size.y));
                p.floating=true;
                float lift=9.81f*immersed*2.8f-p.body.linearVelocity.y*3.8f;
                p.body.AddForce(Vector3.up*lift,ForceMode.Acceleration);
                Vector3 velocity=p.body.linearVelocity;
                Vector3 desired=Vector3.MoveTowards(new Vector3(velocity.x,0,velocity.z),boat!=null?boat.FlowFor(flow):flow,2.6f*dt);
                p.body.linearVelocity=new Vector3(desired.x,velocity.y,desired.z);
                p.body.angularVelocity*=Mathf.Exp(-2.6f*dt);
                if(transportChannel)
                {
                    float travel=Vector3.Dot(p.transform.position-p.lastDriftPosition,flow.normalized);
                    if(travel>0&&travel<.5f)p.driftDistance+=travel;
                }
                p.lastDriftPosition=p.transform.position;
            }
        }
        void FixedUpdate(){Simulate(Time.fixedDeltaTime);}
        void OnDestroy(){all=null;}
    }
}
