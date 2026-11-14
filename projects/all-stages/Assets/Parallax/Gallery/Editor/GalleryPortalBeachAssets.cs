using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadeLink.Beach;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    /// <summary>Extracts the destination's real visible meshes without its gameplay, physics or global lighting.</summary>
    public static class GalleryPortalBeachAssets
    {
        public const string SourceScenePath = "Assets/Parallax/Beach/Scenes/Beach.unity";
        public const string PrefabPath = "Assets/Parallax/Gallery/Resources/MuseumPortal/BeachPreview.prefab";
        const string RootPath = "Assets/Parallax/Gallery/Resources/MuseumPortal/";
        const string DirectoryPath = RootPath + "Beach/";
        // The southeast shoreline frames the new C parasols and flowers against the real festival castle.
        // This is a scenic glimpse, not a checkpoint: actual entry still starts at BeachWorld.Checkpoints[0].
        public static readonly Vector3 SourceAperture = new Vector3(18.5f, 0, 79);
        public const float SourceYaw = -48.5f;

        sealed class Piece
        {
            public Mesh mesh;
            public int subMesh;
            public Matrix4x4 matrix;
            public Material material;
        }
        [Serializable] sealed class SourceRecord
        {
            public string sourceScene, sourceSceneSha256, description;
            public Vector3 sourceAperture, sourceEye;
            public float sourceYaw;
            public int sourceRenderers, sourceMeshes, meshGroups, triangles;
            public string[] originalMaterialPaths;
        }

        [MenuItem("Parallax/Prepare cathedral beach preview")]
        public static void Prepare()
        {
            if (!File.Exists(SourceScenePath))
                throw new InvalidOperationException("The actual Beach.unity destination must exist before its museum preview is extracted.");
            Shader surface = RequiredShader("Parallax/MuseumBeachPreviewSurface");
            Shader water = RequiredShader("Parallax/MuseumBeachPreviewWater");
            Shader sky = RequiredShader("Parallax/MuseumBeachPreviewSky");
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Scene previous = SceneManager.GetActiveScene();
            Scene sourceScene = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            Scene working = default;
            GameObject output = null;
            var readable = new Dictionary<Mesh, Mesh>();
            var converted = new Dictionary<Material, Material>();
            var pieces = new List<Piece>();
            try
            {
                var beach = sourceScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<BeachGame>(true)).FirstOrDefault();
                if (beach == null) throw new InvalidOperationException("The source destination scene has no BeachGame collection.");
                working = EditorSceneManager.NewPreviewScene();
                output = new GameObject("Beach preview · actual destination geometry, renderers only");
                SceneManager.MoveGameObjectToScene(output, working);
                output.layer = GalleryCathedralPortal.PreviewLayer;

                Quaternion viewRotation = Quaternion.Euler(0, SourceYaw, 0);
                Matrix4x4 frame = Matrix4x4.TRS(SourceAperture, viewRotation, Vector3.one);
                int sourceCount = 0;
                var materialPaths = new HashSet<string>();
                foreach (var renderer in beach.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponent<TextMesh>() != null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || renderer.sharedMaterials.Length == 0) continue;
                    // Preview/interaction overlays have no place in a render-only view of the destination.
                    if (renderer.gameObject.layer == BeachLighting.ShadowLayer ||
                        renderer.name.IndexOf("Placement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("Sun ray", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    var materials = renderer.sharedMaterials;
                    if (materials.Any(m => m == null || m.shader == null))
                        throw new InvalidOperationException("A visible beach mesh has a missing source material: " + renderer.name);
                    if (!readable.TryGetValue(filter.sharedMesh, out Mesh mesh))
                    {
                        // Unity's editor read-only API can copy an unreadable imported mesh without changing
                        // its importer. Only the combined preview mesh is saved; the original asset stays untouched.
                        mesh = ReadableCopy(filter.sharedMesh);
                        readable.Add(filter.sharedMesh, mesh);
                    }
                    Matrix4x4 transform = frame.inverse * filter.transform.localToWorldMatrix;
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    {
                        Material original = materials[Mathf.Min(subMesh, materials.Length - 1)];
                        if (!converted.TryGetValue(original, out Material material))
                        {
                            material = Convert(original, surface, water, frame, converted.Count);
                            converted.Add(original, material);
                            materialPaths.Add(AssetDatabase.GetAssetPath(original));
                        }
                        pieces.Add(new Piece { mesh = mesh, subMesh = subMesh, matrix = transform, material = material });
                    }
                    sourceCount++;
                }
                if (sourceCount < 100 || !converted.Keys.Any(m => m.shader.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    !converted.Keys.Any(m => m.shader.name == "Parallax/BeachFlower"))
                    throw new InvalidOperationException("The museum preview requires the latest real beach scenery, animated water and flower assets.");

                int groups = 0, triangles = 0;
                foreach (var group in pieces.GroupBy(p => p.material).OrderBy(g => g.Key.name, StringComparer.Ordinal))
                {
                    var mesh = new Mesh { name = "Beach portal · " + group.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(group.Select(p => new CombineInstance
                    { mesh = p.mesh, subMeshIndex = p.subMesh, transform = p.matrix }).ToArray(), true, true, false);
                    mesh.RecalculateBounds();
                    triangles += (int)(mesh.GetIndexCount(0) / 3);
                    string path = DirectoryPath + "BeachPreviewMesh_" + groups.ToString("D3") + ".asset";
                    mesh = Save(mesh, path);
                    var child = new GameObject("Original beach surfaces · " + group.Key.name);
                    child.transform.SetParent(output.transform, false);
                    child.layer = GalleryCathedralPortal.PreviewLayer;
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = group.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    groups++;
                }
                if (triangles < 10000 || groups < 8 ||
                    output.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                    output.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    output.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                    output.GetComponentsInChildren<Light>(true).Length != 0)
                    throw new InvalidOperationException("Beach preview geometry or render-only isolation validation failed.");

                var skyMaterial = new Material(sky) { name = "Beach preview · isolated coastal sky" };
                SetFrame(skyMaterial, frame);
                Save(skyMaterial, RootPath + "BeachPreviewSky.mat");
                PrefabUtility.SaveAsPrefabAsset(output, PrefabPath);
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var record = new SourceRecord
                    {
                        sourceScene = SourceScenePath,
                        sourceSceneSha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(SourceScenePath))).Replace("-", "").ToLowerInvariant(),
                        sourceAperture = SourceAperture, sourceEye = SourceAperture + Vector3.up * 1.85f,
                        sourceYaw = SourceYaw, sourceRenderers = sourceCount, sourceMeshes = readable.Count,
                        meshGroups = groups, triangles = triangles,
                        originalMaterialPaths = materialPaths.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                        description = "Render-only extraction of actual Beach.unity meshes and original texture maps. " +
                            "Scenic view from the southeast coast across the latest C parasols, flowers, sand festival and castle. " +
                            "Combined by source material. Independent daylight, sky and animated-water shaders do not alter museum lighting or shader globals. " +
                            "No gameplay scripts, player, collider, listener, reflection camera or global-light component is copied. " +
                            "Original third-party source credits and destination assets remain in the project."
                    };
                    File.WriteAllText(RootPath + "BeachPreview-source.json", JsonUtility.ToJson(record, true) + "\n");
                }
                AssetDatabase.SaveAssets();
                Debug.Log("CATHEDRAL_BEACH_PREVIEW_OK source=" + sourceCount + " groups=" + groups + " triangles=" + triangles +
                    " sourceEye=" + (SourceAperture + Vector3.up * 1.85f) + " yaw=" + SourceYaw);
            }
            finally
            {
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                foreach (var mesh in readable.Values) UnityEngine.Object.DestroyImmediate(mesh);
                if (working.IsValid() && working.isLoaded) EditorSceneManager.ClosePreviewScene(working);
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static Shader RequiredShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Coastal preview shader failed: " + name);
            return shader;
        }
        static Material Convert(Material source, Shader surface, Shader water, Matrix4x4 frame, int index)
        {
            string shaderName = source.shader.name;
            bool isWater = shaderName.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
            var material = new Material(isWater ? water : surface) { name = "Beach preview · " + source.name };
            foreach (string name in new[] { "_MainTex", "_BumpMap", "_OcclusionMap", "_MetallicGlossMap", "_SpecGlossMap", "_MainNormal", "_SecondNormal" })
                if (source.HasProperty(name) && material.HasProperty(name) && source.GetTexture(name) != null)
                {
                    material.SetTexture(name, source.GetTexture(name));
                    material.SetTextureScale(name, source.GetTextureScale(name));
                    material.SetTextureOffset(name, source.GetTextureOffset(name));
                }
            foreach (string name in new[] { "_Color", "_EmissionColor", "_DeepColor", "_ShallowColor", "_FoamColor" })
                if (source.HasProperty(name) && material.HasProperty(name)) material.SetColor(name, source.GetColor(name));
            foreach (string name in new[] { "_BumpScale", "_Metallic", "_Glossiness", "_Cutoff", "_Tiling", "_NormalTiling", "_NormalStrength", "_WaveHeight", "_WaterKind" })
                if (source.HasProperty(name) && material.HasProperty(name)) material.SetFloat(name, source.GetFloat(name));
            foreach (string name in new[] { "_Flow", "_Area" })
                if (source.HasProperty(name) && material.HasProperty(name)) material.SetVector(name, source.GetVector(name));
            if (isWater)
            {
                material.SetFloat("_SimpleWater", shaderName == "Parallax/BeachSimpleWater" ? 1 : 0);
                material.SetFloat("_HasNormalTextures", source.HasProperty("_MainNormal") && source.GetTexture("_MainNormal") != null ? 1 : 0);
            }
            else
            {
                material.SetFloat("_Projection", shaderName == "Parallax/BeachTexturedSurface" ? 1 : shaderName == "Parallax/BeachSand" ? 2 : 0);
                material.SetFloat("_AlphaClip", shaderName == "Parallax/BeachFlower" || source.IsKeywordEnabled("_ALPHATEST_ON") ? 1 : 0);
                if (shaderName == "Parallax/BeachFlower") material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            }
            SetFrame(material, frame);
            return Save(material, DirectoryPath + "BeachPreviewMaterial_" + index.ToString("D3") + ".mat");
        }
        static void SetFrame(Material material, Matrix4x4 frame)
        {
            material.SetVector("_SourceOrigin", frame.GetColumn(3));
            material.SetVector("_SourceX", frame.GetColumn(0));
            material.SetVector("_SourceY", frame.GetColumn(1));
            material.SetVector("_SourceZ", frame.GetColumn(2));
        }
        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            T stored = AssetDatabase.LoadAssetAtPath<T>(path);
            if (stored == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, stored);
            EditorUtility.SetDirty(stored);
            UnityEngine.Object.DestroyImmediate(value);
            return stored;
        }
        static Mesh ReadableCopy(Mesh source)
        {
            using (Mesh.MeshDataArray read = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData data = read[0];
                Mesh.MeshDataArray write = Mesh.AllocateWritableMeshData(1);
                var copy = new Mesh { name = source.name + " · temporary portal extraction" };
                bool applied = false;
                try
                {
                    Mesh.MeshData destination = write[0];
                    destination.SetVertexBufferParams(data.vertexCount, source.GetVertexAttributes());
                    for (int stream = 0; stream < source.vertexBufferCount; stream++)
                        destination.GetVertexData<byte>(stream).CopyFrom(data.GetVertexData<byte>(stream));
                    if (data.indexFormat == IndexFormat.UInt32)
                    {
                        var indices = data.GetIndexData<uint>();
                        destination.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                        destination.GetIndexData<uint>().CopyFrom(indices);
                    }
                    else
                    {
                        var indices = data.GetIndexData<ushort>();
                        destination.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                        destination.GetIndexData<ushort>().CopyFrom(indices);
                    }
                    destination.subMeshCount = data.subMeshCount;
                    for (int i = 0; i < data.subMeshCount; i++)
                        destination.SetSubMesh(i, data.GetSubMesh(i), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    Mesh.ApplyAndDisposeWritableMeshData(write, copy, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    applied = true; copy.bounds = source.bounds; copy.UploadMeshData(false); return copy;
                }
                catch { UnityEngine.Object.DestroyImmediate(copy); throw; }
                finally { if (!applied) write.Dispose(); }
            }
        }
    }
}
