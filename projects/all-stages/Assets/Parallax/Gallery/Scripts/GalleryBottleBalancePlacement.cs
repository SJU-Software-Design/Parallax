using UnityEngine;

namespace ShadeLink.Gallery
{
    // A deliberate receiver affordance, not a second scaling rule. The ordinary
    // perspective solver chooses scale/rotation first; this helper only proposes
    // a visible, collision-checked placement on the weighing dish at that size.
    public sealed class GalleryBottleBalancePlacement
    {
        public const string BottleForbiddenMessage = "병을 통째로 올릴 수는 없습니다!";
        public bool TargetedPan { get; private set; }
        public bool HasPreview { get; private set; }
        public Vector3 PreviewPosition { get; private set; }
        public string Hint { get; private set; }
        public float MaximumAdjustment = 3.0f;
        public float MaximumTargetDistance = 9f;

        readonly GalleryGame game;
        readonly GalleryBottleBalanceCourse course;

        public GalleryBottleBalancePlacement(GalleryGame game, GalleryBottleBalanceCourse course)
        { this.game = game; this.course = course; }

        public void Clear()
        { TargetedPan = HasPreview = false; Hint = null; PreviewPosition = Vector3.zero; }

        // Returns whether the visitor is addressing this receiver. A targeted
        // but invalid attempt returns true with a reason, so callers can keep
        // holding the object and explicitly reject the click instead of dropping
        // it onto the floor in front of the dish.
        public bool TryPreview(GalleryProp prop, out Vector3 position, out string reason)
        {
            Clear(); reason = null; position = prop != null ? prop.transform.position : Vector3.zero;
            if (game == null || game.view == null || course == null || course.Art == null || prop == null ||
                prop != course.Sculpture && prop != course.Bottle || prop.parts == null || prop.parts.Length == 0)
                return false;

            if(prop==course.Bottle)
            {
                foreach(var deniedPan in new[]{course.Art.leftPan,course.Art.rightPan})
                {
                    var deniedSupport=deniedPan.GetComponent<BoxCollider>();
                    Bounds deniedBounds=deniedSupport.bounds;
                    Vector3 deniedTop=new Vector3(deniedBounds.center.x,deniedBounds.max.y,deniedBounds.center.z);
                    if(!IntendsDish(prop,deniedSupport,deniedTop,prop.WorldBounds))continue;
                    TargetedPan=true;reason=Hint=BottleForbiddenMessage;return true;
                }
                return false;
            }
            Transform pan = course.Art.rightPan;
            if (pan == null) return false;
            var support = pan.GetComponent<BoxCollider>();
            if (support == null || !support.enabled || !support.gameObject.activeInHierarchy) return false;
            Bounds dish = support.bounds, piece = prop.WorldBounds;
            Vector3 top = new Vector3(dish.center.x, dish.max.y, dish.center.z);
            if (!IntendsDish(prop, support, top, piece)) return false;
            TargetedPan = true;

            // A centred sculpture can safely overhang the shallow dish a little.
            // In particular, rotating the same sculpture 45 degrees must not turn
            // its larger world AABB into an unexplained new size requirement.
            // Large objects still fail this local support-area check, and neither
            // the puzzle's target mass nor its target scale is consulted here.
            float overhang = Mathf.Min(.13f, Mathf.Min(dish.size.x, dish.size.z) * .12f);
            if (piece.size.x > dish.size.x + overhang * 2 || piece.size.z > dish.size.z + overhang * 2)
            { reason = Hint = "조각이 접시보다 큽니다. 크기를 줄여 주세요."; return true; }

            Vector3 candidate = prop.transform.position + top - new Vector3(piece.center.x, piece.min.y, piece.center.z) + Vector3.up * .012f;
            if (Vector3.Distance(candidate, prop.transform.position) > MaximumAdjustment)
            { reason = Hint = "조각을 저울 가까이 옮긴 뒤 접시 위에 놓아 주세요."; return true; }

            Vector3 original = prop.transform.position;
            try
            {
                prop.transform.position = candidate;
                Physics.SyncTransforms();
                if (!game.ValidPlacement(prop, out reason))
                { Hint = reason; return true; }
                prop.transform.position = original;
                Physics.SyncTransforms();

                // Lift first, translate at a common safe height, then lower
                // vertically onto the dish. This avoids a diagonal downward path
                // catching the rim when the player naturally aims at its edge.
                // Try a second modest clearance rather than demanding a precise
                // hand pose. Every segment still checks the full compound shape.
                if (!ClearApproach(prop, support, original, candidate, piece.min.y, top.y, .065f) &&
                    !ClearApproach(prop, support, original, candidate, piece.min.y, top.y, .16f))
                {
                    reason = Hint = "저울까지 옮길 공간이 부족합니다. 조각을 조금 들어 올려 주세요.";
                    return true;
                }
            }
            finally
            {
                // A preview never owns physics, scale, rotation or velocity.
                prop.transform.position = original;
                Physics.SyncTransforms();
            }

            PreviewPosition = position = candidate;
            HasPreview = true;
            Hint = "왼쪽 클릭: 현재 크기로 저울 위에 놓기";
            return true;
        }

        bool IntendsDish(GalleryProp prop, Collider support, Vector3 top, Bounds piece)
        {
            Camera camera = game.view;
            Vector3 eye = camera.transform.position;
            float distance = Vector3.Distance(eye, top);
            if (distance > MaximumTargetDistance || Vector3.Dot(camera.transform.forward, top - eye) <= 0) return false;
            if (prop != course.Bottle && Vector3.Distance(piece.center, top + Vector3.up * piece.extents.y) > MaximumAdjustment + .5f) return false;

            bool aimed = false;
            if (NearestVisible(new Ray(eye, camera.transform.forward), MaximumTargetDistance, prop, out var exact))
                aimed = IsSupport(exact.collider, support);

            // A small volume above the real edge covers the natural intention
            // of looking at the artwork's centre while its bottom meets the dish.
            Bounds intent = support.bounds;
            intent.Expand(new Vector3(.60f, 1.50f, .60f));
            if (!aimed) aimed = intent.IntersectRay(new Ray(eye, camera.transform.forward), out float along) && along <= MaximumTargetDistance;

            // When the thin edge projects to only a few pixels, the artwork's
            // visible bottom can still clearly point at it. Keep this assistance
            // local to the dish and verify the dish is not occluded below.
            if (!aimed)
            {
                Vector3 foot = camera.WorldToViewportPoint(new Vector3(piece.center.x, piece.min.y, piece.center.z));
                if (foot.z > camera.nearClipPlane && ScreenRect(camera, support.bounds, out Rect rect))
                {
                    rect.xMin -= .04f; rect.xMax += .04f;
                    rect.yMin -= .055f; rect.yMax += .055f;
                    aimed = rect.Contains(new Vector2(foot.x, foot.y));
                }
            }
            if (!aimed) return false;

            Vector3 toDish = top - eye;
            if (NearestVisible(new Ray(eye, toDish.normalized), toDish.magnitude + .08f, prop, out var visible))
                return IsSupport(visible.collider, support);
            return false;
        }

        bool NearestVisible(Ray ray, float distance, GalleryProp ignored, out RaycastHit result)
        {
            result = default; float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var owner=hit.collider.GetComponentInParent<GalleryProp>();
                if (hit.collider == game.controller || owner == ignored || hit.distance >= nearest) continue;
                // An artwork already on the dish must not hide the explanation
                // when someone tries to add the bottle. This only identifies a
                // forbidden attempt; it never authorizes movement through it.
                if(ignored==course.Bottle&&owner==course.Sculpture&&course.Extracted)continue;
                result = hit; nearest = hit.distance;
            }
            return result.collider != null;
        }

        static bool IsSupport(Collider collider, Collider support)
        { return collider == support || collider != null && collider.transform.IsChildOf(support.transform); }

        static bool ScreenRect(Camera camera, Bounds bounds, out Rect result)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = camera.WorldToViewportPoint(world);
                if (point.z <= camera.nearClipPlane) { result = default; return false; }
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            result = Rect.MinMaxRect(left, bottom, right, top);
            return true;
        }

        bool ClearApproach(GalleryProp prop, Collider support, Vector3 original, Vector3 candidate,
            float originalBottom, float surfaceTop, float clearance)
        {
            float safeBottom = Mathf.Max(originalBottom, surfaceTop + .012f) + clearance;
            Vector3 raised = original + Vector3.up * (safeBottom - originalBottom);
            Vector3 above = candidate + Vector3.up * (safeBottom - surfaceTop - .012f);
            return ClearSegment(prop, original, raised, support, true) &&
                ClearSegment(prop, raised, above, support, false) &&
                ClearSegment(prop, above, candidate, support, false);
        }

        bool ClearSegment(GalleryProp prop, Vector3 from, Vector3 to, Collider support, bool allowReceiverSeparation)
        {
            Vector3 motion = to - from;
            if (motion.sqrMagnitude < .000001f) return true;
            prop.transform.position = from;
            Physics.SyncTransforms();
            // Contact at the starting rim can be reported as a zero-distance
            // sweep hit. Permit only a tiny existing receiver contact to leave
            // straight upward; do not ignore the dish during translation/descent
            // or allow an object embedded deeply in it to escape through it.
            bool separating = allowReceiverSeparation && motion.y > 0 &&
                new Vector2(motion.x, motion.z).sqrMagnitude < .000001f &&
                prop.WorldBounds.min.y >= support.bounds.max.y - .022f;
            foreach (BoxCollider part in prop.parts)
            {
                if (part == null || !part.enabled) continue;
                Vector3 scale = part.transform.lossyScale;
                Vector3 half = Vector3.Scale(part.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * .5f;
                half = Vector3.Max(Vector3.one * .001f, half - Vector3.one * .004f);
                Vector3 centre = part.transform.TransformPoint(part.center);
                foreach (var hit in Physics.BoxCastAll(centre, half, motion.normalized, part.transform.rotation,
                    motion.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<GalleryProp>() == prop) continue;
                    if (separating && IsSupport(hit.collider, support) && hit.distance <= .022f) continue;
                    return false;
                }
            }
            return true;
        }
    }
}
