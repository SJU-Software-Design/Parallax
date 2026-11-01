using System;
using UnityEngine;

namespace ShadeLink
{
    public static class InteractionRaycast
    {
        public const float MaximumDistance = float.PositiveInfinity;

        public static T Pick<T>(Vector3 origin, Vector3 direction, Collider player, int mask,
            float radius, ref RaycastHit[] hits, ref RaycastHit[] assistHits, out RaycastHit selected) where T : Component
        {
            mask&=~(1<<HeldCollisionState.Layer);
            bool found = Nearest(origin, direction, player, null, mask, ref hits, out selected, out _);
            var exact = found ? selected.collider.GetComponentInParent<T>() : null;
            if (exact != null || radius <= 0) return exact;
            float distance = found ? selected.distance + .1f : MaximumDistance;
            int count;
            while(true)
            {
                count=Physics.SphereCastNonAlloc(origin,radius,direction,assistHits,distance,mask,QueryTriggerInteraction.Ignore);
                if(count<assistHits.Length)break;
                if(assistHits.Length>=4096)
                {
                    assistHits=Physics.SphereCastAll(origin,radius,direction,distance,mask,QueryTriggerInteraction.Ignore);
                    count=assistHits.Length;break;
                }
                Array.Resize(ref assistHits,assistHits.Length*2);
            }
            float nearest = float.PositiveInfinity;
            T target = null;
            for(int i=0;i<count;i++)
            {
                var candidate=assistHits[i];
                if (candidate.collider == null || candidate.collider == player || candidate.distance >= nearest) continue;
                var prop = candidate.collider.GetComponentInParent<T>();
                if (prop == null) continue;
                Vector3 ray = candidate.point - origin;
                if (ray.sqrMagnitude < .000001f) ray = candidate.collider.bounds.center - origin;
                if (!Nearest(origin, ray.normalized, player, null, mask, ref hits, out var visible, out _) ||
                    visible.collider.GetComponentInParent<T>() != prop) continue;
                target = prop; selected = candidate; nearest = candidate.distance;
            }
            return target;
        }

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
