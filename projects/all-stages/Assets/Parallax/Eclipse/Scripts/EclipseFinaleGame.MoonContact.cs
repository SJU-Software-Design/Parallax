using UnityEngine;

namespace ShadeLink.Eclipse
{
    public sealed partial class EclipseFinaleGame
    {
        const float FloorContactMinimumScale = .05f;
        // The real deck starts at -5; its rear quarter-metre remains clear.
        // Ordinary sky placement retains its original z >= 2 boundary.
        const float FloorContactMinimumZ = -4.75f;
        bool moonFloorConstrained;
        float moonContactRadiusFactor;
        public bool MoonFloorConstrained => held == FinaleItem.Moon && moonFloorConstrained;

        float MoonContactRadiusFactor
        {
            get
            {
                if (moonContactRadiusFactor > 0) return moonContactRadiusFactor;
                moonContactRadiusFactor = .5f;
                var mesh = moon != null ? moon.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh != null)
                    foreach (Vector3 vertex in mesh.vertices)
                        moonContactRadiusFactor = Mathf.Max(moonContactRadiusFactor, vertex.magnitude);
                return moonContactRadiusFactor;
            }
        }

        bool ResolveMoonFloorContact(Vector3 eye, Vector3 requested, float ratio,
            out Vector3 direction, out float minimum, out float depth)
        {
            direction = requested; minimum = depth = 0;
            if (ratio <= 0 || requested.y >= 0) return false;
            // Only ground-directed contact can use this smaller physical floor.
            // Depth and scale still change together, preserving the exact pickup
            // angular size. Prefer the original centred ray and its first real
            // floor/wall contact over moving a visible Moon outside the screen.
            if (MoonContactFitsView(requested, ratio) &&
                ResolveMoonPlacement(eye, requested, ratio, out minimum, out depth, FloorContactMinimumScale, FloorContactMinimumZ))
                return true;

            float minimumDepth = FloorContactMinimumScale / ratio;
            if (minimumDepth > 160 || minimumDepth > 48 / ratio) return false;
            Vector3 heading = Vector3.ProjectOnPlane(requested, Vector3.up);
            if (heading.sqrMagnitude < .000001f) heading = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (heading.sqrMagnitude < .000001f) heading = Vector3.forward;
            heading.Normalize();
            float floorY = (.020f + FloorContactMinimumScale * MoonContactRadiusFactor - eye.y) / minimumDepth;
            if (floorY >= 1) return false;
            float requestedElevation = Mathf.Asin(Mathf.Clamp(requested.y, -1, 1)) * Mathf.Rad2Deg;
            float lower = Mathf.Max(requestedElevation, Mathf.Asin(Mathf.Clamp(floorY, -1, 1)) * Mathf.Rad2Deg + .002f);
            float viewElevation = Mathf.Asin(Mathf.Clamp(view.transform.forward.y, -1, 1)) * Mathf.Rad2Deg;
            float upper = Mathf.Min(89, viewElevation + MoonContactViewMargin(ratio));
            if (lower > upper) return false;
            float previous = lower;
            for (int step = 0; step <= 12; step++)
            {
                float elevation = Mathf.Lerp(lower, upper, step / 12f);
                Vector3 candidate = MoonContactDirection(heading, elevation);
                if (ResolveMoonContactCandidate(eye, candidate, ratio, out minimum, out depth))
                {
                    if (step > 0)
                    {
                        float lo = previous, hi = elevation;
                        for (int i = 0; i < 10; i++)
                        {
                            float mid = (lo + hi) * .5f;
                            Vector3 trial = MoonContactDirection(heading, mid);
                            if (ResolveMoonContactCandidate(eye, trial, ratio, out float trialMinimum, out float trialDepth))
                            { hi = mid; candidate = trial; minimum = trialMinimum; depth = trialDepth; }
                            else lo = mid;
                        }
                    }
                    direction = candidate;
                    return true;
                }
                previous = elevation;
            }
            return false;
        }

        bool ResolveMoonContactCandidate(Vector3 eye, Vector3 direction, float ratio, out float minimum, out float depth)
        {
            minimum = depth = 0;
            if (!MoonContactFitsView(direction, ratio) ||
                !ResolveMoonPlacement(eye, direction, ratio, out minimum, out depth, FloorContactMinimumScale, FloorContactMinimumZ)) return false;
            // Last-resort direction correction stays at its nearest clear pose,
            // so passing a narrow wall edge cannot send it down a far sky ray.
            if (!MoonSphereClearAt(eye, direction, ratio, minimum)) return false;
            depth = minimum;
            return true;
        }

        float MoonContactViewMargin(float ratio)
        {
            float vertical = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfFrustum = Mathf.Atan(Mathf.Min(vertical, vertical * Mathf.Max(.01f, view.aspect))) * Mathf.Rad2Deg;
            float moonAngle = Mathf.Asin(Mathf.Clamp01(ratio * MoonContactRadiusFactor)) * Mathf.Rad2Deg;
            return Mathf.Max(0, halfFrustum - moonAngle - 1);
        }

        bool MoonContactFitsView(Vector3 direction, float ratio)
        {
            return Vector3.Angle(view.transform.forward, direction) <= MoonContactViewMargin(ratio);
        }

        bool MoonOverlapsPlayerCapsule()
        {
            Vector3 scale = body.transform.lossyScale;
            float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float capsuleRadius = body.radius * horizontalScale;
            float halfSegment = Mathf.Max(0, body.height * Mathf.Abs(scale.y) * .5f - capsuleRadius);
            Vector3 centre = body.transform.TransformPoint(body.center), up = body.transform.up;
            Vector3 nearest = centre + up * Mathf.Clamp(Vector3.Dot(moon.position - centre, up), -halfSegment, halfSegment);
            float clearance = capsuleRadius + body.skinWidth * horizontalScale +
                moon.localScale.x * MoonContactRadiusFactor + .005f;
            return (moon.position - nearest).sqrMagnitude < clearance * clearance;
        }

        static Vector3 MoonContactDirection(Vector3 heading, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return heading * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
        }
    }
}