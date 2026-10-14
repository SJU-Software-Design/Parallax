using UnityEngine;
namespace ShadeLink.Beach
{
    public sealed class BeachWaterGateLink:MonoBehaviour
    {
        public BeachLightGate gate;Vector3 home;
        void Awake(){home=transform.position;}
        void Update(){transform.position=home+Vector3.up*gate.opening*4.5f;}
    }
}
