using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryLightGate : MonoBehaviour
    {
        public Transform sensor;
        public Renderer sensorFace;
        public BoxCollider panel;
        public Vector3 closedPosition;
        public bool illuminated;
        public float opening;
        public bool Open => opening > .96f;
        public void Restore()
        {
            opening = 0; illuminated = false; panel.transform.position = closedPosition;
            Physics.SyncTransforms();
        }
        public void Tick(float dt, GalleryLighting lighting, CharacterController player)
        {
            illuminated = lighting.mirror != null && lighting.IsReflectedLit(sensor.position, -lighting.mirror.ReflectedDirection);
            bool occupied = false;
            if (!illuminated && opening > 0)
            {
                Vector3 half = Vector3.Scale(panel.size, panel.transform.lossyScale) * .5f;
                foreach (var other in Physics.OverlapBox(closedPosition, half, panel.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (other == player || other.GetComponentInParent<GalleryProp>() != null) { occupied = true; break; }
            }
            opening = Mathf.MoveTowards(opening, illuminated || occupied ? 1 : 0, dt * 2.5f);
            panel.transform.position = closedPosition + Vector3.up * (3.8f * opening);
            if (sensorFace != null)
            {
                var block = new MaterialPropertyBlock();block.SetColor("_Color", illuminated ? new Color(.25f, 1, .72f) : new Color(1, .35f, .18f));
                sensorFace.SetPropertyBlock(block);
            }
            Physics.SyncTransforms();
        }
    }
}
