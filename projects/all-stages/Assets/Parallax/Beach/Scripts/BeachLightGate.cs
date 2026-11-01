using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachLightGate : MonoBehaviour
    {
        public BeachGame game;
        public BoxCollider panel;
        public Vector3 closedPosition;
        public Transform[] sensors;
        public Renderer[] sensorFaces;
        public bool waterGate,illuminated,unlocked;
        public float charge,opening;
        public void Restore(){unlocked=illuminated=false;charge=opening=0;panel.transform.position=closedPosition;}
        public void Tick(float dt,BeachLighting lighting,CharacterController player)
        {
            if(!waterGate)
            {
                illuminated=game.mirror!=null&&game.mirror.WorldWidth>=2.35f;
                for(int i=0;i<sensors.Length;i++)
                {
                    bool hit=lighting.IsReflectedLit(sensors[i].position,(game.mirror.LightOrigin-sensors[i].position).normalized);
                    illuminated&=hit;
                    var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",hit?new Color(.05f,.8f,1):new Color(.8f,.16f,.03f));sensorFaces[i].SetPropertyBlock(block);
                }
                charge=illuminated?Mathf.Min(1,charge+dt/.8f):Mathf.Max(0,charge-dt*2);
                if(charge>=1&&!unlocked){unlocked=true;game.Tell("두 센서 충전 완료 · 문이 고정되었습니다. 반사광 아래에도 그늘이 필요합니다.",7);}
            }
            opening=Mathf.MoveTowards(opening,unlocked?1:0,dt*.7f);
            panel.transform.position=closedPosition+Vector3.up*opening*5;
        }
    }
}
