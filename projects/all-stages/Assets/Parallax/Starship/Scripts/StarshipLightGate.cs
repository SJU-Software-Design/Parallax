using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipLightGate : MonoBehaviour
    {
        public StarshipGame game;
        public BoxCollider panel;
        public Vector3 closedPosition;
        public Transform[] sensors;
        public Renderer[] sensorFaces;
        public bool waterGate,illuminated,unlocked;
        public float charge,opening;
        public int LitSensors {get;private set;}
        public void Restore(){unlocked=illuminated=false;LitSensors=0;charge=opening=0;panel.transform.position=closedPosition;}
        public void Tick(float dt,StarshipLighting lighting,CharacterController player)
        {
            if(!waterGate)
            {
                LitSensors=0;
                for(int i=0;i<sensors.Length;i++)
                {
                    bool hit=game.mirror!=null&&lighting.IsReflectedLit(sensors[i].position,(game.mirror.LightOrigin-sensors[i].position).normalized);
                    if(hit)LitSensors++;
                    var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",hit?new Color(.05f,.8f,1):new Color(.8f,.16f,.03f));sensorFaces[i].SetPropertyBlock(block);
                }
                // The indicators and lock use the same actual beam hits. Scale
                // already affects the reflected cone; no second size gate is needed.
                illuminated=sensors.Length>0&&LitSensors==sensors.Length;
                charge=unlocked?1:illuminated?Mathf.Min(1,charge+dt/.8f):Mathf.Max(0,charge-dt*2);
                if(charge>=1&&!unlocked){unlocked=true;game.Tell("두 센서 충전 완료 · 문이 고정되었습니다. 반사광 아래에도 그늘이 필요합니다.",7);}
            }
            opening=Mathf.MoveTowards(opening,unlocked?1:0,dt*.7f);
            panel.transform.position=closedPosition+Vector3.up*opening*5;
        }
    }
}
