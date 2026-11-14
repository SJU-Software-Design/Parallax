using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Tutorial.Editor
{
    // Converts the authorized, attributed glTF source into ordinary portable Unity assets.
    // No downloaded script is executed. Geometry is baked into entrance-facing coordinates.
    public static class ElevatorAssetPreparation
    {
        const string Root = "Assets/Parallax/Tutorial/ThirdParty/ElevatorE01";
        const string Resource = "Assets/Parallax/Tutorial/Resources/Tutorial/ElevatorE01.prefab";
        const float HorizontalScale = .90f, VerticalScale = .75f, SourceFloorY = .014581323f;
        [Serializable] sealed class Gltf { public Node[] nodes; public MeshDef[] meshes; public Accessor[] accessors; public View[] bufferViews; public MaterialDef[] materials; public TextureDef[] textures; public ImageDef[] images; public AnimationDef[] animations; }
        [Serializable] sealed class Node { public string name; public int[] children; public int mesh = -1; public float[] matrix, translation, rotation, scale; }
        [Serializable] sealed class MeshDef { public string name; public Primitive[] primitives; }
        [Serializable] sealed class Primitive { public Attributes attributes; public int indices = -1, material = -1; }
        [Serializable] sealed class Attributes { public int POSITION = -1, NORMAL = -1, TEXCOORD_0 = -1; }
        [Serializable] sealed class Accessor { public int bufferView, byteOffset, componentType, count; public string type; }
        [Serializable] sealed class View { public int byteOffset, byteStride, byteLength; }
        [Serializable] sealed class MaterialDef { public string name; public Pbr pbrMetallicRoughness; public float[] emissiveFactor; public Extensions extensions; }
        [Serializable] sealed class Pbr { public float[] baseColorFactor; public float metallicFactor = 1, roughnessFactor = 1; public TextureInfo baseColorTexture, metallicRoughnessTexture; }
        [Serializable] sealed class TextureInfo { public int index; public TextureExtensions extensions; }
        [Serializable] sealed class TextureExtensions { public TextureTransform KHR_texture_transform; }
        [Serializable] sealed class TextureTransform { public float[] offset, scale; }
        [Serializable] sealed class Extensions { public EmissiveStrength KHR_materials_emissive_strength; }
        [Serializable] sealed class EmissiveStrength { public float emissiveStrength = 1; }
        [Serializable] sealed class TextureDef { public int source; }
        [Serializable] sealed class ImageDef { public string uri; }
        [Serializable] sealed class AnimationDef { public Sampler[] samplers; public Channel[] channels; }
        [Serializable] sealed class Sampler { public int input, output; }
        [Serializable] sealed class Channel { public int sampler; public Target target; }
        [Serializable] sealed class Target { public int node; public string path; }
        struct Vertex
        {
            public Vector3 p, n; public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { p = Vector3.LerpUnclamped(a.p, b.p, t), n = Vector3.LerpUnclamped(a.n, b.n, t).normalized, uv = Vector2.LerpUnclamped(a.uv, b.uv, t) };
        }

        static Gltf gltf;
        static byte[] binary;
        static int[] parents;
        static Matrix4x4[] worlds;
        static bool[] worldReady;
        static readonly string[] DoorNames = { "LeftOutsideDoor_0", "RightOutsideDoor_1", "LeftInteriorDoor_8", "RightInteriorDoor_10" };
        static int triangleCount, carvedTriangleCount;

        [MenuItem("Parallax/Tutorial/Prepare E01 Elevator")]
        public static void Prepare()
        {
            string source = Path.Combine(Root, "Source");
            string json = File.ReadAllText(Path.Combine(source, "scene.gltf"));
            gltf = JsonUtility.FromJson<Gltf>(json);
            binary = File.ReadAllBytes(Path.Combine(source, "scene.bin"));
            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Path.GetDirectoryName(Resource));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            parents = new int[gltf.nodes.Length]; worlds = new Matrix4x4[gltf.nodes.Length]; worldReady = new bool[gltf.nodes.Length];
            for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            for (int i = 0; i < gltf.nodes.Length; i++) if (gltf.nodes[i].children != null) foreach (int child in gltf.nodes[i].children) parents[child] = i;
            var materials = PrepareMaterials(source);
            var glass = PrepareGlass();
            var root = new GameObject("E01 · attributed original elevator");
            try
            {
                var asset = root.AddComponent<ElevatorE01Asset>();
                var groups = new Dictionary<int, Transform>();
                triangleCount = carvedTriangleCount = 0;
                for (int i = 0; i < gltf.nodes.Length; i++)
                {
                    Node node = gltf.nodes[i];
                    if (node.name == null || !node.name.StartsWith("Object_", StringComparison.Ordinal) || node.mesh < 0 || node.mesh >= gltf.meshes.Length) continue;
                    int parent = parents[i];
                    string groupName = parent >= 0 ? gltf.nodes[parent].name : node.name;
                    // These are the author preview's 15m wall and 15x10m outdoor ground,
                    // not the elevator. Each game room supplies its own surrounding wall.
                    if (groupName == "Wall_2" || groupName == "Plane_16" || groupName == "ElevatorCallingButtons.007_13" || groupName == "ElevatorCallingButtons.006_14") continue;
                    if (!groups.TryGetValue(parent, out Transform group))
                    {
                        group = new GameObject(groupName).transform;
                        group.SetParent(root.transform, false); groups.Add(parent, group);
                    }
                    int primitiveIndex = 0;
                    foreach (Primitive primitive in gltf.meshes[node.mesh].primitives)
                    {
                        var go = new GameObject(node.name + "_" + primitiveIndex);
                        go.transform.SetParent(group, false);
                        bool mirror = gltf.materials[primitive.material].name == "Mirror";
                        bool carve = i == 12 || i == 30;
                        Mesh mesh = BuildMesh(i, primitive, carve);
                        mesh.name = node.name + "_" + primitiveIndex;
                        mesh = SaveAsset(mesh, Root + "/Meshes/" + mesh.name + ".asset");
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = go.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = mirror ? glass : materials[primitive.material];
                        if (mirror)
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                            asset.backGlass = renderer;
                            var collider = go.AddComponent<BoxCollider>();
                            collider.center = mesh.bounds.center;
                            collider.size = new Vector3(mesh.bounds.size.x, mesh.bounds.size.y, .065f);
                        }
                        else if (Array.IndexOf(DoorNames, groupName) >= 0)
                        {
                            var collider = go.AddComponent<BoxCollider>();
                            collider.center = mesh.bounds.center;
                            Vector3 size = mesh.bounds.size; size.z = Mathf.Max(.065f, size.z); collider.size = size;
                        }
                        else
                        {
                            // Source geometry supplies the actual floor, side walls, ceiling,
                            // front frame and handrail surfaces; the window was carved above.
                            go.AddComponent<MeshCollider>().sharedMesh = mesh;
                        }
                        primitiveIndex++;
                    }
                }
                asset.doors = new Transform[4]; asset.closedLocalPositions = new Vector3[4]; asset.openLocalPositions = new Vector3[4];
                for (int d = 0; d < DoorNames.Length; d++)
                {
                    int nodeIndex = Array.FindIndex(gltf.nodes, n => n.name == DoorNames[d]);
                    asset.doors[d] = groups[nodeIndex];
                    asset.closedLocalPositions[d] = Vector3.zero;
                    asset.openLocalPositions[d] = NormalizedVector(DoorDelta(nodeIndex));
                }
                // Centre containment at the walls; full capsule clearance is
                // checked separately at the actual closed-door sweep.
                asset.cabinBounds = new Bounds(new Vector3(-.035f, 1.40f, -1.215f), new Vector3(2.25f, 2.80f, 2.07f));
                // Four source doors span Z -0.10172..0.03595m when closed.
                // Include 3cm clearance either side instead of half the cabin.
                asset.doorSafetyBounds = new Bounds(new Vector3(0, 1.10f, -.033f), new Vector3(1.78f, 2.40f, .20f));
                asset.approachBounds = new Bounds(new Vector3(0, 1.25f, 1.70f), new Vector3(3.00f, 3.00f, 3.60f));
                asset.floorY = 0; asset.thresholdZ = 0;
                asset.displayAnchors = new[]
                {
                    Anchor(root.transform, "DisplayExterior", new Vector3(0, 2.464f, .184f), Quaternion.identity),
                    Anchor(root.transform, "DisplayInterior", new Vector3(0, 2.39f, -.185f), Quaternion.Euler(0, 180, 0))
                };
                // A matching dark housing above the inner door; the outer housing is original.
                var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                housing.name = "Interior floor display housing"; housing.transform.SetParent(root.transform, false);
                housing.transform.localPosition = new Vector3(0, 2.39f, -.145f); housing.transform.localScale = new Vector3(.52f, .23f, .06f);
                housing.GetComponent<Renderer>().sharedMaterial = materials[Array.FindIndex(gltf.materials, m => m.name == "DarkerMetal")];
                UnityEngine.Object.DestroyImmediate(housing.GetComponent<Collider>());
                asset.cabinLightAnchor = Anchor(root.transform, "CabinLightAnchor", new Vector3(0, 2.65f, -1.30f), Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(root, Resource);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(Resource, ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(Root + "/preparation.json", JsonUtility.ToJson(new PreparationAudit
                {
                    triangles = triangleCount, sourceTrianglesCarved = carvedTriangleCount,
                    sourceSha256 = "121d348338ff566df6a72925469ed64a0b54c2be782e80ddaad7748ed83029f2",
                    horizontalScale = HorizontalScale, verticalScale = VerticalScale,
                    prefab = Resource, doorCount = asset.doors.Length,
                    modifications = "Source glTF geometry transformed into +Z-facing entrance coordinates; preview wall/ground and original arrow glyphs omitted. A real rear-wall aperture is clipped behind the original mirror, whose actual source plane is now translucent frosted glass. Source door translations baked as four independent opening targets; added inner display housing/anchors and collision. No downloaded code or animation loop runs."
                }, true), Encoding.UTF8);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("E01_ASSET_PREPARED " + Resource + " triangles=" + triangleCount + " carved=" + carvedTriangleCount + " doors=4");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable] sealed class PreparationAudit { public string prefab, sourceSha256, modifications; public int triangles, sourceTrianglesCarved, doorCount; public float horizontalScale, verticalScale; }
        static Transform Anchor(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; t.localRotation = rotation; return t;
        }
        static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing;
        }
        static Material[] PrepareMaterials(string source)
        {
            var result = new Material[gltf.materials.Length];
            for (int i = 0; i < result.Length; i++)
            {
                MaterialDef definition = gltf.materials[i]; Pbr pbr = definition.pbrMetallicRoughness ?? new Pbr();
                var material = new Material(Shader.Find("Standard")) { name = definition.name };
                material.color = pbr.baseColorFactor == null ? Color.white : new Color(pbr.baseColorFactor[0], pbr.baseColorFactor[1], pbr.baseColorFactor[2], pbr.baseColorFactor[3]);
                material.SetFloat("_Metallic", pbr.metallicFactor); material.SetFloat("_Glossiness", 1 - pbr.roughnessFactor);
                // JsonUtility creates zero-filled nested reference objects for absent fields.
                // The actual source has baseColorTexture ONLY on Buttons, MR ONLY on Metal.
                if (definition.name == "Buttons") material.mainTexture = ImportTexture(source, 1, true);
                if (definition.name == "Metal")
                {
                    Texture2D packed = ImportTexture(source, 0, false);
                    var converted = new Texture2D(packed.width, packed.height, TextureFormat.RGBA32, true, true);
                    Color[] pixels = packed.GetPixels();
                    for (int k = 0; k < pixels.Length; k++) pixels[k] = new Color(pixels[k].b * pbr.metallicFactor, 0, 0, 1 - pixels[k].g * pbr.roughnessFactor);
                    converted.SetPixels(pixels); converted.Apply();
                    string texturePath = Root + "/Textures/Metal_UnityMetallicSmoothness.png";
                    File.WriteAllBytes(texturePath, converted.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(converted);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureTexture(texturePath, false, false);
                    material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    material.EnableKeyword("_METALLICGLOSSMAP"); material.SetFloat("_GlossMapScale", 1);
                    material.SetTextureScale("_MetallicGlossMap", new Vector2(4, 4)); material.SetTextureOffset("_MetallicGlossMap", new Vector2(0, -3));
                }
                if (definition.name == "Emission")
                {
                    // Preserve source emissive material, but a controlled ceiling light supplies illumination.
                    float strength = definition.extensions?.KHR_materials_emissive_strength?.emissiveStrength ?? 1;
                    material.SetColor("_EmissionColor", Color.white * strength); material.EnableKeyword("_EMISSION");
                }
                result[i] = SaveAsset(material, Root + "/Materials/" + definition.name + ".mat");
            }
            return result;
        }
        static Texture2D ImportTexture(string source, int textureIndex, bool srgb)
        {
            string file = gltf.images[gltf.textures[textureIndex].source].uri;
            string target = Root + "/Textures/" + Path.GetFileName(file);
            File.Copy(Path.Combine(source, file), target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(target, srgb, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }
        static void ConfigureTexture(string path, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureShape = TextureImporterShape.Texture2D; importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.isReadable = readable; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        static Material PrepareGlass()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                uint hash = (uint)(x * 374761393 + y * 668265263); hash = (hash ^ (hash >> 13)) * 1274126177;
                float grain = (hash & 255) / 255f;
                pixels[y * size + x] = new Color(.82f + grain * .06f, .91f + grain * .04f, .94f + grain * .03f, .27f + grain * .11f);
            }
            texture.SetPixels(pixels); texture.Apply(); string path = Root + "/Textures/FrostedGlassGrain.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); ConfigureTexture(path, true, false);
            var glass = new Material(Shader.Find("Standard")) { name = "Frosted translucent rear wall", color = Color.white };
            glass.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); glass.mainTextureScale = new Vector2(5, 5);
            glass.SetFloat("_Metallic", 0); glass.SetFloat("_Glossiness", .08f); glass.SetFloat("_Mode", 2);
            glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); glass.SetInt("_ZWrite", 0);
            glass.DisableKeyword("_ALPHATEST_ON"); glass.EnableKeyword("_ALPHABLEND_ON"); glass.DisableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue = 3000;
            return SaveAsset(glass, Root + "/Materials/FrostedGlass.mat");
        }
        static Mesh BuildMesh(int node, Primitive primitive, bool carve)
        {
            int count = gltf.accessors[primitive.attributes.POSITION].count; var vertices = new Vertex[count];
            Matrix4x4 world = World(node), normalMatrix = world.inverse.transpose;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = world.MultiplyPoint3x4(ReadVector(primitive.attributes.POSITION, i));
                Vector3 n = primitive.attributes.NORMAL >= 0 ? normalMatrix.MultiplyVector(ReadVector(primitive.attributes.NORMAL, i)) : Vector3.up;
                vertices[i] = new Vertex { p = NormalizedPoint(p), n = new Vector3(n.z / HorizontalScale, n.y / VerticalScale, n.x / HorizontalScale).normalized, uv = primitive.attributes.TEXCOORD_0 >= 0 ? new Vector2(Read(primitive.attributes.TEXCOORD_0, i, 0), 1 - Read(primitive.attributes.TEXCOORD_0, i, 1)) : Vector2.zero };
            }
            var output = new List<Vertex>();
            int indexCount = primitive.indices < 0 ? count : gltf.accessors[primitive.indices].count;
            for (int i = 0; i < indexCount; i += 3)
            {
                int a = primitive.indices < 0 ? i : (int)Read(primitive.indices, i, 0);
                int b = primitive.indices < 0 ? i + 2 : (int)Read(primitive.indices, i + 2, 0);
                int c = primitive.indices < 0 ? i + 1 : (int)Read(primitive.indices, i + 1, 0);
                var triangle = new List<Vertex> { vertices[a], vertices[b], vertices[c] };
                Vector3 normal = Vector3.Cross(vertices[b].p - vertices[a].p, vertices[c].p - vertices[a].p).normalized;
                if (carve && Mathf.Abs(normal.z) > .72f && vertices[a].p.z < -2.25f && vertices[b].p.z < -2.25f && vertices[c].p.z < -2.25f)
                {
                    // Subtract the original mirror rectangle from both sides of the solid rear wall.
                    // UVs/normals on retained fragments are linearly interpolated, not reprojected.
                    triangle = ClipAndKeepOutside(triangle, 0, -.94423f, true, output);
                    triangle = ClipAndKeepOutside(triangle, 0, .89680f, false, output);
                    triangle = ClipAndKeepOutside(triangle, 1, 1.08107f, true, output);
                    triangle = ClipAndKeepOutside(triangle, 1, 2.60292f, false, output);
                    if (triangle.Count >= 3) carvedTriangleCount++;
                    // Entirely outside triangles are already preserved by the clipping stages.
                }
                else AddPolygon(triangle, output);
            }
            var positions = new Vector3[output.Count]; var normals = new Vector3[output.Count]; var uv = new Vector2[output.Count]; var indices = new int[output.Count];
            for (int i = 0; i < output.Count; i++) { positions[i] = output[i].p; normals[i] = output[i].n; uv[i] = output[i].uv; indices[i] = i; }
            var mesh = new Mesh { indexFormat = output.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.normals = normals; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            triangleCount += indices.Length / 3; return mesh;
        }
        static List<Vertex> ClipAndKeepOutside(List<Vertex> input, int axis, float boundary, bool keepGreater, List<Vertex> output)
        {
            var inside = new List<Vertex>(); var outside = new List<Vertex>();
            if (input.Count == 0) return inside;
            Vertex a = input[input.Count - 1]; float da = (a.p[axis] - boundary) * (keepGreater ? 1 : -1); bool ai = da >= 0;
            foreach (Vertex b in input)
            {
                float db = (b.p[axis] - boundary) * (keepGreater ? 1 : -1); bool bi = db >= 0;
                if (ai != bi) { Vertex intersection = Vertex.Lerp(a, b, da / (da - db)); inside.Add(intersection); outside.Add(intersection); }
                if (bi) inside.Add(b); else outside.Add(b);
                a = b; da = db; ai = bi;
            }
            AddPolygon(outside, output); return inside;
        }
        static void AddPolygon(List<Vertex> polygon, List<Vertex> output)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i].p - polygon[0].p, polygon[i + 1].p - polygon[0].p).sqrMagnitude < 1e-12f) continue;
                output.Add(polygon[0]); output.Add(polygon[i]); output.Add(polygon[i + 1]);
            }
        }
        static Vector3 DoorDelta(int node)
        {
            Channel channel = Array.Find(gltf.animations[0].channels, c => c.target.node == node && c.target.path == "translation");
            Sampler sampler = gltf.animations[0].samplers[channel.sampler];
            Vector3 closed = SampleTranslation(sampler, 0), open = SampleTranslation(sampler, 3);
            return (parents[node] >= 0 ? World(parents[node]) : Matrix4x4.identity).MultiplyVector(open - closed);
        }
        static Vector3 SampleTranslation(Sampler sampler, float time)
        {
            int count = gltf.accessors[sampler.input].count, a = 0;
            while (a + 1 < count && Read(sampler.input, a + 1, 0) < time) a++;
            int b = Mathf.Min(a + 1, count - 1); float ta = Read(sampler.input, a, 0), tb = Read(sampler.input, b, 0);
            return Vector3.Lerp(ReadVector(sampler.output, a), ReadVector(sampler.output, b), tb > ta ? Mathf.Clamp01((time - ta) / (tb - ta)) : 0);
        }
        static Vector3 NormalizedPoint(Vector3 source) => new Vector3(source.z * HorizontalScale, (source.y - SourceFloorY) * VerticalScale, source.x * HorizontalScale);
        static Vector3 NormalizedVector(Vector3 source) => new Vector3(source.z * HorizontalScale, source.y * VerticalScale, source.x * HorizontalScale);
        static Matrix4x4 World(int node)
        {
            if (worldReady[node]) return worlds[node]; worldReady[node] = true;
            worlds[node] = parents[node] >= 0 ? World(parents[node]) * Local(gltf.nodes[node]) : Local(gltf.nodes[node]); return worlds[node];
        }
        static Matrix4x4 Local(Node node)
        {
            var matrix = Matrix4x4.identity;
            if (node.matrix != null && node.matrix.Length == 16) { for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) matrix[r, c] = node.matrix[c * 4 + r]; return matrix; }
            Vector3 s = node.scale == null ? Vector3.one : new Vector3(node.scale[0], node.scale[1], node.scale[2]);
            float x = node.rotation == null ? 0 : node.rotation[0], y = node.rotation == null ? 0 : node.rotation[1], z = node.rotation == null ? 0 : node.rotation[2], w = node.rotation == null ? 1 : node.rotation[3];
            matrix.m00 = (1 - 2 * y * y - 2 * z * z) * s.x; matrix.m10 = (2 * x * y + 2 * z * w) * s.x; matrix.m20 = (2 * x * z - 2 * y * w) * s.x;
            matrix.m01 = (2 * x * y - 2 * z * w) * s.y; matrix.m11 = (1 - 2 * x * x - 2 * z * z) * s.y; matrix.m21 = (2 * y * z + 2 * x * w) * s.y;
            matrix.m02 = (2 * x * z + 2 * y * w) * s.z; matrix.m12 = (2 * y * z - 2 * x * w) * s.z; matrix.m22 = (1 - 2 * x * x - 2 * y * y) * s.z;
            if (node.translation != null) { matrix.m03 = node.translation[0]; matrix.m13 = node.translation[1]; matrix.m23 = node.translation[2]; } return matrix;
        }
        static Vector3 ReadVector(int accessor, int index) => new Vector3(Read(accessor, index, 0), Read(accessor, index, 1), Read(accessor, index, 2));
        static float Read(int accessor, int index, int component)
        {
            Accessor a = gltf.accessors[accessor]; View v = gltf.bufferViews[a.bufferView];
            int size = a.componentType == 5126 || a.componentType == 5125 ? 4 : a.componentType == 5123 || a.componentType == 5122 ? 2 : 1;
            int dimensions = a.type == "VEC2" ? 2 : a.type == "VEC3" ? 3 : a.type == "VEC4" ? 4 : 1;
            int offset = v.byteOffset + a.byteOffset + index * (v.byteStride > 0 ? v.byteStride : size * dimensions) + component * size;
            switch (a.componentType) { case 5126: return BitConverter.ToSingle(binary, offset); case 5125: return BitConverter.ToUInt32(binary, offset); case 5123: return BitConverter.ToUInt16(binary, offset); case 5122: return BitConverter.ToInt16(binary, offset); default: return binary[offset]; }
        }
    }
}
