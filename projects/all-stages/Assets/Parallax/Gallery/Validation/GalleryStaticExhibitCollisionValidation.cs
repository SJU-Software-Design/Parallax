using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Called by the integrated native-player validation; it does not move the
    // visitor or modify the puzzle's solved/door state.
    public static class GalleryStaticExhibitCollisionValidation
    {
        [Serializable] public sealed class Result
        {
            public bool passed, idempotent, noInteractiveAdditions, archLanesClear;
            public int exactSurfaces, physicalSurfaceHits, missingMeshes;
            public string[] failures, surfaces;
        }

        public static Result Inspect(GalleryGame game)
        {
            var failures = new List<string>(); var surfaces = new List<string>();
            var installer = GalleryStaticExhibitCollision.Install(game);
            Physics.SyncTransforms();
            int before = game.GetComponentsInChildren<Collider>(true).Length;
            bool idempotent = ReferenceEquals(installer, GalleryStaticExhibitCollision.Install(game)) &&
                before == game.GetComponentsInChildren<Collider>(true).Length;
            bool safe = true; int hits = 0;
            foreach (MeshCollider shape in installer.Added)
            {
                string path = GalleryStaticExhibitCollision.Path(game.transform, shape.transform); surfaces.Add(path);
                if (shape.isTrigger || shape.attachedRigidbody != null || shape.GetComponentInParent<GalleryProp>() != null ||
                    shape.gameObject.layer == GalleryCathedralPortal.PreviewLayer)
                { safe = false; failures.Add("Interactive/effect shape was changed: " + path); }
                if (PhysicalSurfaceHit(shape)) hits++;
                else failures.Add("No actual PhysX surface hit: " + path);
            }
            bool lanes = ClearLane(installer.Added, game.transform.TransformPoint(new Vector3(0, 1.1f, 13.3f)), game.transform.forward, 1.4f) &&
                ClearLane(installer.Added, game.transform.TransformPoint(new Vector3(0, 1.1f, 28.3f)), game.transform.forward, 1.4f);
            if (game.GetComponent<GalleryBottleBalanceCourse>() != null)
                lanes &= ClearLane(installer.Added, game.transform.TransformPoint(new Vector3(0, 1.1f, GalleryBottleBalanceLayout.EndZ - .7f)), game.transform.forward, 1.4f);
            if (!lanes) failures.Add("An added surface sealed the central arch lane.");
            if (!idempotent) failures.Add("Installing twice duplicated physical colliders.");
            foreach (string missing in installer.Missing) failures.Add("Missing readable source: " + missing);
            return new Result { passed = failures.Count == 0 && installer.Added.Count > 0,
                idempotent = idempotent, noInteractiveAdditions = safe, archLanesClear = lanes,
                exactSurfaces = installer.Added.Count, physicalSurfaceHits = hits, missingMeshes = installer.Missing.Count,
                failures = failures.ToArray(), surfaces = surfaces.ToArray() };
        }

        static bool ClearLane(List<MeshCollider> shapes, Vector3 origin, Vector3 direction, float distance)
        {
            foreach (var shape in shapes)
                if (shape.Raycast(new Ray(origin, direction), out _, distance) ||
                    shape.Raycast(new Ray(origin + direction * distance, -direction), out _, distance)) return false;
            return true;
        }

        static bool PhysicalSurfaceHit(MeshCollider shape)
        {
            Mesh mesh = shape.sharedMesh;
            if (!shape.enabled || mesh == null || !mesh.isReadable) return false;
            Vector3[] vertices = mesh.vertices;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                // Try several independent faces, including a later part of a
                // scanned surface, rather than trusting a non-null mesh field.
                int stride = Mathf.Max(3, triangles.Length / 24 / 3 * 3);
                for (int i = 0; i + 2 < triangles.Length; i += stride)
                {
                    Vector3 a = shape.transform.TransformPoint(vertices[triangles[i]]),
                        b = shape.transform.TransformPoint(vertices[triangles[i + 1]]),
                        c = shape.transform.TransformPoint(vertices[triangles[i + 2]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < .00000001f) continue;
                    normal.Normalize(); Vector3 center = (a + b + c) / 3;
                    if (shape.Raycast(new Ray(center + normal * .06f, -normal), out _, .12f) ||
                        shape.Raycast(new Ray(center - normal * .06f, normal), out _, .12f)) return true;
                }
            }
            return false;
        }
    }
}
