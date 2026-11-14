using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    /// <summary>Imports the credited public CC BY adaptation as independent visual parts.</summary>
    public static class GalleryDikeStatueAssets
    {
        const string ResourceRoot = "Assets/Parallax/Gallery/Resources/BottleBalance/Dike";
        const string AssetRoot = ResourceRoot + "/Statue";
        const string SourceRoot = "Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeStatue";
        const string GeometryRoot = SourceRoot + "/SourceGeometry";
        const string RigPath = ResourceRoot + "/Rig.json";

        #pragma warning disable CS0649 // JsonUtility populates these input records from the preserved manifest.
        [Serializable] sealed class SourceBounds { public float[] min, max, center, size; }
        [Serializable] sealed class SourcePart { public string file; public int vertices, triangles; public SourceBounds bounds; }
        [Serializable] sealed class SourceParts
        {
            public SourcePart StaticBody, RewardSword, ScaleBeam, ScaleSuspension;
            public SourcePart PanLeftBowl, PanRightBowl, PanLeftChains, PanRightChains;
        }
        [Serializable] sealed class SourceNormalization
        { public float[] center, axisY, rotationQuatXYZW; public float originalLength, maxReconstructionError; }
        [Serializable] sealed class SourceSword
        { public SourceNormalization normalization; public bool independentRigidMeshVerified, unityGravityTestCompleted; }
        [Serializable] sealed class SourceManifest
        {
            public string sourceModel, publicRedistribution, sourceGlbSHA256;
            public int sourceTriangleCount;
            public SourceParts parts;
            public SourceSword sword;
        }
        #pragma warning restore CS0649

        [Serializable] sealed class Rig
        {
            public Vector3 bodyMin, bodyMax, beamPivot, swordCenter, swordGrip;
            public Quaternion swordRotation;
            public float swordOriginalLength;
            public bool independentSwordVerified;
            public string coordinates = "Unity left-handed Y-up; mirrored source Z";
            public string verificationScope = "Independent sword geometry verified by removal; Unity gravity test performed separately by the game validation";
            public string sourceManifest = SourceRoot + "/game-parts-manifest.json";
        }
        [Serializable] sealed class PreparedPart
        {
            public string resource, sourceGeometry, prefab;
            public int vertices, triangles, submeshes;
            public Vector3 boundsMin, boundsMax;
            public bool hasCollider = false;
        }
        [Serializable] sealed class PreparedReport
        {
            public string sourceModel, publicRedistribution, sourceSHA256;
            public string license = "CC BY 4.0";
            public bool originalCreatorZipAcquired = false;
            public bool publicAdaptationAcquired = true;
            public bool independentSwordGeometryVerified;
            public bool providedReleaseAnimation = false;
            public bool providedRig = false;
            public string conversion = "OBJ vertices/normals mirrored on Z; source CCW indices reversed by shared ReadObj; source normals preserved";
            public string normalizedSword = "RewardSword is centered, Y-long-axis, unit length. Rig.json restores its exact source pose.";
            public PreparedPart[] parts;
        }

        [MenuItem("Parallax/Museum/Prepare Dike Statue Assets")]
        public static void Prepare()
        {
            SourceManifest source = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(SourceRoot + "/game-parts-manifest.json"));
            if (source == null || source.parts == null || source.sword == null
                || !source.sword.independentRigidMeshVerified || source.sword.normalization == null)
                throw new InvalidDataException("Dike statue requires the verified independent-sword source manifest.");

            EnsureFolder(AssetRoot + "/Meshes");
            EnsureFolder(AssetRoot + "/Materials");
            Material stone = BuildMaterial("DikeCreamStone", new Color(.86f, .835f, .77f), 0f, .47f, .22f);
            Material brass = BuildMaterial("DikeAntiqueBrass", new Color(.73f, .46f, .14f), .82f, .31f, .54f);
            Material blade = BuildMaterial("DikeSwordSteel", new Color(.66f, .70f, .72f), .94f, .23f, .58f);

            var entries = new List<PreparedPart>();
            entries.Add(PreparePart("StatueBody", "StaticBody", source.parts.StaticBody, new[] { stone }));
            entries.Add(PreparePart("RewardSword", "RewardSword", source.parts.RewardSword, new[] { brass, blade }));
            entries.Add(PreparePart("ScaleBeam", "ScaleBeam", source.parts.ScaleBeam, new[] { brass }));
            entries.Add(PreparePart("ScaleSuspension", "ScaleSuspension", source.parts.ScaleSuspension, new[] { brass }));
            entries.Add(PreparePart("PanLeftBowl", "PanLeftBowl", source.parts.PanLeftBowl, new[] { brass }));
            entries.Add(PreparePart("PanRightBowl", "PanRightBowl", source.parts.PanRightBowl, new[] { brass }));
            entries.Add(PreparePart("PanLeftChains", "PanLeftChains", source.parts.PanLeftChains, new[] { brass }));
            entries.Add(PreparePart("PanRightChains", "PanRightChains", source.parts.PanRightChains, new[] { brass }));

            SourceNormalization normalization = source.sword.normalization;
            Vector3 center = Vector(normalization.center);
            Vector3 axis = Vector(normalization.axisY).normalized;
            float[] q = normalization.rotationQuatXYZW;
            if (q == null || q.Length != 4 || normalization.originalLength <= 0f)
                throw new InvalidDataException("Dike source sword normalization is malformed.");
            var rig = new Rig
            {
                bodyMin = MirrorBoundsMin(source.parts.StaticBody.bounds),
                bodyMax = MirrorBoundsMax(source.parts.StaticBody.bounds),
                beamPivot = Mirror(Vector(source.parts.ScaleBeam.bounds.center)),
                swordCenter = Mirror(center),
                swordGrip = Mirror(center + axis * (normalization.originalLength * .36f)),
                swordRotation = new Quaternion(-q[0], -q[1], q[2], q[3]),
                swordOriginalLength = normalization.originalLength,
                independentSwordVerified = true
            };
            File.WriteAllText(RigPath, JsonUtility.ToJson(rig, true));
            AssetDatabase.ImportAsset(RigPath, ImportAssetOptions.ForceSynchronousImport);
            var report = new PreparedReport
            {
                sourceModel = source.sourceModel, publicRedistribution = source.publicRedistribution,
                sourceSHA256 = source.sourceGlbSHA256,
                independentSwordGeometryVerified = true, parts = entries.ToArray()
            };
            string reportPath = SourceRoot + "/prepared-statue-report.json";
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("DIKE_STATUE_ASSETS_PREPARED parts=" + entries.Count
                + " sourceTriangles=" + source.sourceTriangleCount + " independentSword=1 providedAnimation=0");
        }

        static PreparedPart PreparePart(string resource, string sourceName, SourcePart expected, Material[] materials)
        {
            if (expected == null) throw new InvalidDataException("Missing Dike source part " + sourceName);
            string sourcePath = GeometryRoot + "/" + sourceName + ".obj.txt";
            Mesh mesh = GalleryDikePropAssets.ReadObj(sourcePath, resource, false, true);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++) vertices[i].z = -vertices[i].z;
            for (int i = 0; i < normals.Length; i++) normals[i].z = -normals[i].z;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
            if (triangles != expected.triangles || normals.Length != vertices.Length)
                throw new InvalidDataException("Dike source geometry was not preserved for " + resource);
            if (materials.Length != mesh.subMeshCount)
                throw new InvalidDataException("Dike material/submesh mismatch for " + resource);
            if ((mesh.bounds.min - MirrorBoundsMin(expected.bounds)).sqrMagnitude > .00000001f
                || (mesh.bounds.max - MirrorBoundsMax(expected.bounds)).sqrMagnitude > .00000001f)
                throw new InvalidDataException("Dike source coordinate conversion changed bounds for " + resource);

            string meshPath = AssetRoot + "/Meshes/" + resource + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            var temporary = new GameObject(resource);
            string prefabPath = ResourceRoot + "/" + resource + ".prefab";
            try
            {
                temporary.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = temporary.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
            return new PreparedPart
            {
                resource = "BottleBalance/Dike/" + resource, sourceGeometry = sourcePath,
                prefab = prefabPath, vertices = mesh.vertexCount, triangles = triangles,
                submeshes = mesh.subMeshCount, boundsMin = mesh.bounds.min, boundsMax = mesh.bounds.max
            };
        }

        static Material BuildMaterial(string name, Color color, float metallic, float roughness, float reflection)
        {
            Shader shader = Shader.Find("Parallax/MuseumSurface");
            if (shader == null) throw new InvalidOperationException("MuseumSurface shader missing for Dike sculpture.");
            string assetPath = AssetRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else material.shader = shader;
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Roughness", roughness);
            material.SetFloat("_ReflectionStrength", reflection);
            material.SetFloat("_Stone", metallic > .1f ? 0f : .035f);
            material.SetFloat("_Sheen", metallic > .1f ? .62f : .30f);
            material.SetFloat("_AmbientStrength", 1f);
            material.SetFloat("_ExhibitLight", .12f);
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Vector3 Vector(float[] value)
        {
            if (value == null || value.Length != 3) throw new InvalidDataException("Malformed source vector.");
            return new Vector3(value[0], value[1], value[2]);
        }
        static Vector3 Mirror(Vector3 value) { return new Vector3(value.x, value.y, -value.z); }
        static Vector3 MirrorBoundsMin(SourceBounds bounds)
        { Vector3 min = Vector(bounds.min), max = Vector(bounds.max); return new Vector3(min.x, min.y, -max.z); }
        static Vector3 MirrorBoundsMax(SourceBounds bounds)
        { Vector3 min = Vector(bounds.min), max = Vector(bounds.max); return new Vector3(max.x, max.y, -min.z); }
        static void EnsureFolder(string path)
        {
            string[] elements = path.Split('/');
            string at = elements[0];
            for (int i = 1; i < elements.Length; i++)
            {
                string next = at + "/" + elements[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(at, elements[i]);
                at = next;
            }
        }
    }
}
