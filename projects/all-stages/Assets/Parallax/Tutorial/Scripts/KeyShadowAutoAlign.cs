using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Measures the carved passage against the ray from the lamp, independently
    // of the shadow's position on the wall or the sculpture's roll.
    public sealed class KeyShadowAutoAlign : System.IDisposable
    {
        const float ToleranceDegrees = 2f;
        const float AlignSeconds = .55f;
        // Imported ShadowKey_Branch mesh coordinates: the circular passage runs
        // along mesh Z. Unity's FBX conversion mirrors the source mesh's X.
        static readonly Vector3 PassageCenter = new Vector3(.15f, 3.71f, 0f);
        static readonly Quaternion[] RewardReferenceRotations =
        {
            Quaternion.Euler(17.381f, -78.266f, .65f),
            Quaternion.Euler(-18.534f, -258.966f, 8.73f)
        };
        readonly Transform sculpture, lightTransform, passageMesh;
        readonly PassageQuery passageQuery;
        float alignTime, previousAlignTime;
        int nearTarget = -1;
        bool aligning, completed;
        Quaternion startRotation, previousOutput, alignmentTarget;
        Quaternion completedBeamAdjustment;

        public KeyShadowAutoAlign(Transform root, Transform light, Transform mesh)
        {
            sculpture = root;
            lightTransform = light;
            passageMesh = mesh;
            var filter = mesh != null ? mesh.GetComponent<MeshFilter>() : null;
            if (filter != null && filter.sharedMesh != null) passageQuery = new PassageQuery(filter.sharedMesh);
        }

        public void Dispose() => passageQuery?.Dispose();

        static float RotationAngle(Quaternion a, Quaternion b)
        {
            Quaternion delta = a * Quaternion.Inverse(b);
            // atan2 stays accurate near zero and at the inclusive 2-degree edge.
            return 2f * Mathf.Atan2(new Vector3(delta.x, delta.y, delta.z).magnitude,
                Mathf.Abs(delta.w)) * Mathf.Rad2Deg;
        }

        bool TryTarget(Quaternion current, out Quaternion target, out int direction)
        {
            target = current;
            direction = 0;
            if (sculpture == null || lightTransform == null || passageMesh == null) return false;

            Quaternion inverseRoot = Quaternion.Inverse(sculpture.rotation);
            Vector3 center = inverseRoot * (passageMesh.TransformPoint(PassageCenter) - sculpture.position);
            Vector3 axis = (inverseRoot * passageMesh.TransformVector(Vector3.forward)).normalized;
            Vector3 towardLight = lightTransform.position - sculpture.position;
            Vector3 perpendicular = center - axis * Vector3.Dot(center, axis);
            float axialSquared = towardLight.sqrMagnitude - perpendicular.sqrMagnitude;
            if (axis.sqrMagnitude < .5f || axialSquared <= .000001f) return false;

            // In root coordinates the lamp must lie on the line through the
            // passage, not on the spotlight's central forward axis. Solve that
            // line/sphere intersection and apply the smallest rotation to it.
            float axial = Mathf.Sqrt(axialSquared);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < 2; i++)
            {
                Vector3 localLight = perpendicular + axis * (i == 0 ? -axial : axial);
                Quaternion candidate = Quaternion.FromToRotation(current * localLight, towardLight) * current;
                float angle = RotationAngle(current, candidate);
                if (angle >= nearest) continue;
                nearest = angle;
                target = candidate;
                direction = i;
            }
            return true;
        }

        public float NearestTargetAngle(Quaternion rotation, out int direction)
        {
            if (!TryTarget(rotation, out Quaternion target, out direction)) return float.PositiveInfinity;
            return RotationAngle(rotation, target);
        }

        public bool IsAligning => aligning;
        public bool HasPendingCompletion => completed;
        public bool PassageIsClear { get; private set; }
        public bool WithinCaptureRange { get; private set; }
        public float Progress => Mathf.Clamp01(alignTime / AlignSeconds);
        public static float AllowedAngle => ToleranceDegrees;

        public void Reset()
        {
            alignTime = previousAlignTime = 0f;
            nearTarget = -1;
            aligning = completed = false;
            PassageIsClear = WithinCaptureRange = false;
        }

        public bool CanCapture(Quaternion rotation)
        {
            return TryTarget(rotation, out Quaternion target, out _) && InCaptureRange(rotation, target);
        }

        bool InCaptureRange(Quaternion rotation, Quaternion target)
        {
            if (passageQuery == null)
                return WithinCaptureRange = RotationAngle(rotation, target) <= ToleranceDegrees + .0001f;

            // A finite passage admits oblique light too. A clear hole already
            // satisfies the puzzle even when its center axis is 5+ degrees off.
            PassageIsClear = ClearAt(rotation);
            WithinCaptureRange = PassageIsClear || ClearAt(Quaternion.RotateTowards(rotation, target, ToleranceDegrees));
            return WithinCaptureRange;
        }

        bool ClearAt(Quaternion rotation)
        {
            // Evaluate a proposed root rotation in the mesh's existing local
            // coordinates, without moving its rendered or physical transform.
            Quaternion toAppliedPose = sculpture.rotation * Quaternion.Inverse(rotation);
            Vector3 source = sculpture.position + toAppliedPose * (lightTransform.position - sculpture.position);
            return passageQuery.IsClear(passageMesh.InverseTransformPoint(source));
        }

        public bool TryConsumeCompletion(out int direction, out Quaternion beamAdjustment)
        {
            direction = nearTarget;
            beamAdjustment = completedBeamAdjustment;
            if (!completed) return false;
            completed = false;
            return true;
        }

        // Collision-rejected motion has not solved the puzzle. Retry from the
        // last accepted pose and let the player move clear of the sculpture.
        public void RejectAlignmentStep(Quaternion appliedRotation)
        {
            alignTime = previousAlignTime;
            previousOutput = appliedRotation;
            aligning = true;
            completed = false;
        }

        public Quaternion Tick(Quaternion current, float dt)
        {
            if (sculpture == null || lightTransform == null || passageMesh == null)
            { Reset(); return current; }
            if (completed) return current;
            if (aligning)
            {
                if (RotationAngle(current, previousOutput) > .15f)
                { Reset(); return current; }
                previousAlignTime = alignTime;
                alignTime += Mathf.Max(0f, dt);
                float t = Mathf.Clamp01(alignTime / AlignSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                previousOutput = Quaternion.Slerp(startRotation, alignmentTarget, eased);
                if (t >= 1f)
                {
                    aligning = false;
                    completed = true;
                    completedBeamAdjustment = alignmentTarget * Quaternion.Inverse(RewardReferenceRotations[nearTarget]);
                }
                return previousOutput;
            }
            if (!TryTarget(current, out Quaternion target, out int direction) ||
                !InCaptureRange(current, target)) return current;

            // Capture as soon as the player's applied rotation enters range;
            // there is no hidden requirement to release rotation or hold still.
            nearTarget = direction;
            aligning = true;
            alignTime = previousAlignTime = 0f;
            startRotation = previousOutput = current;
            alignmentTarget = target;
            return current;
        }

        // An isolated, invisible copy of the real mesh supplies fast native
        // triangle queries. Its location and excluded layer keep it out of
        // player contacts, pickup rays and the rendered scene.
        sealed class PassageQuery : System.IDisposable
        {
            static readonly Vector3 QueryOrigin = new Vector3(1000f, 1000f, 1000f);
            static readonly Vector3[] ProbeOffsets =
            {
                Vector3.zero, new Vector3(.035f, 0, 0), new Vector3(-.035f, 0, 0),
                new Vector3(0, .035f, 0), new Vector3(0, -.035f, 0)
            };
            readonly GameObject root;
            readonly MeshCollider collider;
            readonly float reach, depth;

            public PassageQuery(Mesh mesh)
            {
                root = new GameObject("Key passage · non-solid mesh query")
                { hideFlags = HideFlags.HideAndDontSave, layer = ShadeLink.Beach.BeachLighting.ShadowLayer };
                root.transform.position = QueryOrigin;
                collider = root.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                reach = mesh.bounds.size.magnitude + 1f;
                depth = Mathf.Max(Mathf.Abs(mesh.bounds.min.z), Mathf.Abs(mesh.bounds.max.z));
                Physics.SyncTransforms();
            }

            public bool IsClear(Vector3 source)
            {
                // The lamp must be beyond an end of the passage, rather than
                // viewing a stray empty region alongside the sculpture.
                if (Mathf.Abs(source.z - PassageCenter.z) <= depth) return false;
                int clear = 0;
                foreach (Vector3 offset in ProbeOffsets)
                {
                    Vector3 toHole = PassageCenter + offset - source;
                    Vector3 direction = toHole.normalized;
                    float distance = toHole.magnitude + reach;
                    Vector3 from = QueryOrigin + source;
                    // Check both sides, so reversed triangle winding or global
                    // backface-query settings cannot turn a blocker into a hole.
                    if (collider.Raycast(new Ray(from, direction), out _, distance) ||
                        collider.Raycast(new Ray(from + direction * distance, -direction), out _, distance)) continue;
                    if (++clear >= 3) return true;
                }
                return false;
            }

            public void Dispose()
            {
                if (root == null) return;
                root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(root);
                else Object.DestroyImmediate(root);
            }
        }
    }
}
