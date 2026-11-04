using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    // Collision-only derivative. The original high-detail render mesh is read,
    // never edited; every vertex cluster stays inside one .005-unit voxel.
    public static class GalleryScanCollision
    {
        const string Output = "Assets/Parallax/Gallery/Resources/MuseumScans/NikeCollision.asset";
        const float Cell = .005f, DisplayScale = 3.65f;
        const int MaximumTriangles = 500000;
        const string Version = "NikeVoxelCollision-v1";

        struct Face : IEquatable<Face>
        {
            readonly int a, b, c;
            public Face(int first, int second, int third)
            {
                if (first > second) { int swap = first; first = second; second = swap; }
                if (second > third) { int swap = second; second = third; third = swap; }
                if (first > second) { int swap = first; first = second; second = swap; }
                a = first; b = second; c = third;
            }
            public bool Equals(Face other) { return a == other.a && b == other.b && c == other.c; }
            public override bool Equals(object value) { return value is Face other && Equals(other); }
            public override int GetHashCode() { unchecked { return ((a * 397) ^ b) * 397 ^ c; } }
        }

        public static void Ensure()
        {
            Mesh source = Resources.Load<Mesh>("MuseumScans/NikeMesh");
            Require(source != null && source.vertexCount >= 3 && source.subMeshCount == 1,
                "Original single-submesh Nike render asset is required");
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string signature = Version + ";cell=.005;guid=" + AssetDatabase.AssetPathToGUID(sourcePath) +
                ";hash=" + AssetDatabase.GetAssetDependencyHash(sourcePath) + ";vertices=" + source.vertexCount;
            int originalTriangles = checked((int)(source.GetIndexCount(0) / 3));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(Output);
            AssetImporter importer = AssetImporter.GetAtPath(Output);
            float bound = Mathf.Sqrt(3) * Cell * DisplayScale;
            if (existing != null && importer != null && importer.userData == signature &&
                existing.vertexCount >= 3 && existing.subMeshCount == 1 &&
                existing.GetIndexCount(0) >= 3 && existing.GetIndexCount(0) / 3 < MaximumTriangles)
            {
                Debug.Log("NIKE_COLLISION_REUSE originalTriangles=" + originalTriangles +
                    " collisionTriangles=" + existing.GetIndexCount(0) / 3 +
                    " cell=" + Cell + " maxPhysicalVoxelErrorBound=" + bound + "m");
                return;
            }

            Vector3[] vertices = source.vertices;
            int[] input = source.triangles;
            var clusters = new Dictionary<Vector3Int, int>(vertices.Length / 2);
            var sums = new List<Vector3>(vertices.Length / 2);
            var counts = new List<int>(vertices.Length / 2);
            var remap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                Require(Finite(vertex.x) && Finite(vertex.y) && Finite(vertex.z), "Original mesh contains a non-finite vertex");
                var key = new Vector3Int(Mathf.FloorToInt(vertex.x / Cell), Mathf.FloorToInt(vertex.y / Cell), Mathf.FloorToInt(vertex.z / Cell));
                if (!clusters.TryGetValue(key, out int cluster))
                { cluster = sums.Count; clusters.Add(key, cluster); sums.Add(Vector3.zero); counts.Add(0); }
                remap[i] = cluster;
                sums[cluster] += vertex;
                counts[cluster]++;
            }
            for (int i = 0; i < sums.Count; i++) sums[i] /= counts[i];
            float maximumErrorSquared = 0;
            for (int i = 0; i < vertices.Length; i++)
                maximumErrorSquared = Mathf.Max(maximumErrorSquared, (vertices[i] - sums[remap[i]]).sqrMagnitude);

            var output = new List<int>(Math.Min(input.Length, MaximumTriangles * 3));
            var unique = new HashSet<Face>();
            int discarded = 0;
            for (int i = 0; i < input.Length; i += 3)
            {
                int a = remap[input[i]], b = remap[input[i + 1]], c = remap[input[i + 2]];
                if (a == b || b == c || a == c ||
                    Vector3.Cross(sums[b] - sums[a], sums[c] - sums[a]).sqrMagnitude <= 1e-16f ||
                    !unique.Add(new Face(a, b, c)))
                { discarded++; continue; }
                output.Add(a); output.Add(b); output.Add(c);
            }
            int triangles = output.Count / 3;
            Require(triangles > 0 && triangles < MaximumTriangles,
                "Collision mesh must contain fewer than 500000 triangles; generated " + triangles);
            float measuredError = Mathf.Sqrt(maximumErrorSquared) * DisplayScale;
            Require(measuredError <= bound + .00001f, "Cluster displacement exceeds its voxel error bound");

            var collision = new Mesh { name = "Nike · voxel collision only", indexFormat = IndexFormat.UInt32 };
            collision.SetVertices(sums);
            collision.SetTriangles(output, 0, false);
            collision.RecalculateBounds();
            if (existing == null)
            {
                Require(AssetDatabase.LoadMainAssetAtPath(Output) == null, "Collision path contains another asset type");
                AssetDatabase.CreateAsset(collision, Output);
                existing = collision;
            }
            else
            {
                EditorUtility.CopySerialized(collision, existing);
                UnityEngine.Object.DestroyImmediate(collision);
            }
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            importer = AssetImporter.GetAtPath(Output);
            Require(importer != null, "Collision asset importer was not created");
            importer.userData = signature;
            AssetDatabase.WriteImportSettingsIfDirty(Output);
            Debug.Log("NIKE_COLLISION_OK originalTriangles=" + originalTriangles +
                " collisionTriangles=" + triangles + " collisionVertices=" + existing.vertexCount +
                " discardedFaces=" + discarded + " cell=" + Cell +
                " measuredMaxPhysicalVertexError=" + measuredError + "m maxPhysicalVoxelErrorBound=" + bound +
                "m sourceUnchanged=true output=" + Output);
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("NIKE_COLLISION: " + message); }
    }
}
