using System;
using UnityEngine;

namespace ShadeLink
{
    public static class InteractionRaycast
    {
        public const float MaximumDistance = float.PositiveInfinity;

        // RaycastNonAlloc results are unsorted and a full buffer can omit the nearest
        // blocker. Grow it before choosing a hit, including for very distant targets.
        public static bool Nearest(Vector3 origin, Vector3 direction, Collider player, Transform held,
            int mask, ref RaycastHit[] hits, out RaycastHit selected, out int queries)
        {
            selected = default; queries = 0;
            int count;
            while (true)
            {
                count = Physics.RaycastNonAlloc(origin, direction, hits, MaximumDistance, mask, QueryTriggerInteraction.Ignore);
                queries++;
                if (count < hits.Length) break;
                if (hits.Length >= 4096)
                {
                    hits = Physics.RaycastAll(origin, direction, MaximumDistance, mask, QueryTriggerInteraction.Ignore);
                    queries++; count = hits.Length; break;
                }
                Array.Resize(ref hits, hits.Length * 2);
            }
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider == player ||
                    (held != null && hit.collider.transform.IsChildOf(held)) || hit.distance >= nearest) continue;
                selected = hit; nearest = hit.distance;
            }
            return selected.collider != null;
        }

        public static void ClipDepth(float origin, float velocity, float minimum, float maximum, ref float lo, ref float hi)
        {
            if (Mathf.Abs(velocity) < .000001f)
            { if (origin < minimum || origin > maximum) { lo = 1; hi = 0; } return; }
            float a = (minimum - origin) / velocity, b = (maximum - origin) / velocity;
            lo = Mathf.Max(lo, Mathf.Min(a, b)); hi = Mathf.Min(hi, Mathf.Max(a, b));
        }
    }
}
