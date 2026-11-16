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
            public SurfaceProbe[] probeDiagnostics;
        }

        [Serializable] public sealed class SurfaceProbe
        {
            public string surface;
            public bool enabled, readable, hit;
            public int sampledTriangles, degenerateSamples, legacySmallSamples, raycasts;
            public double minimumCrossSquared, maximumCrossSquared;
        }
        public static Result Inspect(GalleryGame game)
        {
            var failures = new List<string>(); var surfaces = new List<string>();
            var probeDiagnostics = new List<SurfaceProbe>();
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
                var probe = new SurfaceProbe { surface = path };
                if (PhysicalSurfaceHit(shape, probe)) hits++;
                else failures.Add("No actual PhysX surface hit: " + path + " samples=" + probe.sampledTriangles +
                    " rays=" + probe.raycasts + " degenerate=" + probe.degenerateSamples + " maxCrossSquared=" + probe.maximumCrossSquared);
                if (!probe.hit || probe.legacySmallSamples > 0) probeDiagnostics.Add(probe);
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
                failures = failures.ToArray(), surfaces = surfaces.ToArray(), probeDiagnostics = probeDiagnostics.ToArray() };
        }

        static bool ClearLane(List<MeshCollider> shapes, Vector3 origin, Vector3 direction, float distance)
        {
            foreach (var shape in shapes)
                if (shape.Raycast(new Ray(origin, direction), out _, distance) ||
                    shape.Raycast(new Ray(origin + direction * distance, -direction), out _, distance)) return false;
            return true;
        }

        static bool PhysicalSurfaceHit(MeshCollider shape, SurfaceProbe probe)
        {
            Mesh mesh = shape.sharedMesh;
            probe.enabled = shape.enabled; probe.readable = mesh != null && mesh.isReadable;
            if (!probe.enabled || !probe.readable) return false;
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
                    // Cross magnitude is twice the face area, not a distance.
                    // A 0.7 mm inscription can have valid face cross-squared
                    // below 1e-8; skipping it means no PhysX query ever ran.
                    double ux = (double)b.x - a.x, uy = (double)b.y - a.y, uz = (double)b.z - a.z;
                    double vx = (double)c.x - a.x, vy = (double)c.y - a.y, vz = (double)c.z - a.z;
                    double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    double squared = nx * nx + ny * ny + nz * nz;
                    probe.sampledTriangles++;
                    if (probe.sampledTriangles == 1 || squared < probe.minimumCrossSquared) probe.minimumCrossSquared = squared;
                    probe.maximumCrossSquared = Math.Max(probe.maximumCrossSquared, squared);
                    if (squared < 1e-8) probe.legacySmallSamples++;
                    if (double.IsNaN(squared) || double.IsInfinity(squared) || squared <= 1e-24)
                    { probe.degenerateSamples++; continue; }
                    // Vector3.Normalize also zeroes normals shorter than 1e-5.
                    // Divide explicitly so a real thin triangle has a unit ray.
                    double length = Math.Sqrt(squared);
                    Vector3 normal = new Vector3((float)(nx / length), (float)(ny / length), (float)(nz / length));
                    Vector3 center = new Vector3((float)(((double)a.x + b.x + c.x) / 3),
                        (float)(((double)a.y + b.y + c.y) / 3), (float)(((double)a.z + b.z + c.z) / 3));
                    probe.raycasts++;
                    if (shape.Raycast(new Ray(center + normal * .06f, -normal), out _, .12f))
                    { probe.hit = true; return true; }
                    probe.raycasts++;
                    if (shape.Raycast(new Ray(center - normal * .06f, normal), out _, .12f))
                    { probe.hit = true; return true; }
                }
            }
            return false;
        }
    }
}
