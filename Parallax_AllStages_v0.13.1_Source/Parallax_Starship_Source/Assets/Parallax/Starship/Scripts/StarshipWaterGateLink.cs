using UnityEngine;
namespace ShadeLink.Starship
{
    public sealed class StarshipWaterGateLink:MonoBehaviour
    {
        public StarshipLightGate gate;Vector3 home;
        void Awake(){home=transform.position;}
        void Update(){transform.position=home+Vector3.up*gate.opening*4.5f;}
    }
}
