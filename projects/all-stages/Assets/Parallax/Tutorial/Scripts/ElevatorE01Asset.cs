using UnityEngine;

namespace ShadeLink.Tutorial
{
    // References baked by the E01 source importer. +Z points out of the entrance;
    // the floor is Y=0 and the cabin extends into -Z.
    public sealed class ElevatorE01Asset : MonoBehaviour
    {
        public Transform[] doors;
        public Vector3[] closedLocalPositions;
        public Vector3[] openLocalPositions;
        public Renderer backGlass;
        public Bounds cabinBounds;
        public Bounds doorSafetyBounds;
        public Bounds approachBounds;
        public Transform[] displayAnchors;
        public Transform cabinLightAnchor;
        public float floorY;
        public float thresholdZ;
    }
}
