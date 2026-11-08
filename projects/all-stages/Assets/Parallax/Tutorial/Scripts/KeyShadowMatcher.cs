using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Compatibility component for existing preview scenes and editor tools.
    // The old two-object puzzle has been removed; it performs no checks.
    public sealed class KeyShadowMatcher : MonoBehaviour
    {
        public bool Matched => false;
        public bool Matching => false;
        public float Coverage => 0f;
        public int TableShadowSamples => 0;
        public int DonutShadowSamples => 0;
        public void Configure(Light source, Transform receiver, params Transform[] casters) { }
        public void ResetMatch() { }
    }
}
