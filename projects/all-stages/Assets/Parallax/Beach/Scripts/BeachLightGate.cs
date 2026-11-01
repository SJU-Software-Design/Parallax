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
        public BeachSolarReceiver[] receiverVisuals;
        public Vector3 openOffset=Vector3.up*5;
        public bool waterGate,illuminated,unlocked;
        public float charge,opening;
        public int LitSensors {get;private set;}
        bool[] sensorLit;
        public void Restore()
        {
            unlocked=illuminated=false;LitSensors=0;charge=opening=0;
            if(sensorLit!=null)System.Array.Clear(sensorLit,0,sensorLit.Length);
            panel.transform.position=closedPosition;UpdateReceivers();
        }
        public void Tick(float dt,BeachLighting lighting,CharacterController player)
        {
            if(!waterGate)
            {
                int count=sensors==null?0:sensors.Length;
                if(sensorLit==null||sensorLit.Length!=count)sensorLit=new bool[count];
                LitSensors=0;
                for(int i=0;i<count;i++)
                {
                    bool hit=game.mirror!=null&&sensors[i]!=null&&lighting.IsReflectedLit(sensors[i].position,(game.mirror.LightOrigin-sensors[i].position).normalized);
                    sensorLit[i]=hit;if(hit)LitSensors++;
                }
                // Mirror size already changes its reflected cone. The visible beam
                // hits, rather than a second hidden size condition, charge the gate.
                illuminated=count>0&&LitSensors==count;
                charge=unlocked?1:illuminated?Mathf.Min(1,charge+dt/.8f):Mathf.Max(0,charge-dt*2);
                if(charge>=1&&!unlocked){unlocked=true;game.Tell("두 수신기 연결 완료 · 게이트가 열렸습니다. 그늘을 이어 모래성으로 가세요.",7);}
                UpdateReceivers();
            }
            opening=Mathf.MoveTowards(opening,unlocked?1:0,dt*.7f);
            panel.transform.position=closedPosition+openOffset*opening;
        }
        void UpdateReceivers()
        {
            int count=sensors==null?0:sensors.Length;
            for(int i=0;i<count;i++)
            {
                bool lit=sensorLit!=null&&i<sensorLit.Length&&sensorLit[i];
                if(receiverVisuals!=null&&i<receiverVisuals.Length&&receiverVisuals[i]!=null)
                    receiverVisuals[i].SetState(lit,unlocked,charge);
                else if(sensorFaces!=null&&i<sensorFaces.Length&&sensorFaces[i]!=null)
                {
                    var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",lit?new Color(.05f,.8f,1):new Color(.8f,.16f,.03f));
                    sensorFaces[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
