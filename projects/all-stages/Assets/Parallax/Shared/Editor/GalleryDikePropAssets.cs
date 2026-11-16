using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    /// <summary>Builds Dike prop prefabs from the credited, acquired CC0 source geometry.</summary>
    public static class GalleryDikePropAssets
    {
        const string DikeRoot = "Assets/Parallax/Gallery/Resources/BottleBalance/Dike";
        const string PropsRoot = DikeRoot + "/Props";
        const string TexturesRoot = PropsRoot + "/Textures";
        const string ThirdPartyRoot = "Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeProps";
        const string SourcesRoot = ThirdPartyRoot + "/SourceAssets";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Serializable] sealed class PreparedReport
        {
            public string preparedAtUtc;
            public string sourceManifest = ThirdPartyRoot + "/acquisition-manifest.json";
            public string sourceLicense = "CC0 1.0 / Poly Haven";
            public string texturePacking = "Standard metallic R and smoothness A=(1-source roughness); occlusion grayscale from original AO/ARM red; original OpenGL normals imported as NormalMap";
            public PreparedEntry[] entries;
        }
        [Serializable] sealed class PreparedEntry
        {
            public string resource, prefab, sourceObj;
            public int vertices, triangles, submeshes;
            public Vector3 boundsMin, boundsSize;
            public string[] materialNames;
            public bool visualOnly = true;
        }

        [MenuItem("Parallax/Museum/Prepare Dike Prop Assets")]
        public static void Prepare()
        {
            EnsureFolder(PropsRoot + "/Meshes");
            EnsureFolder(PropsRoot + "/Materials");
            EnsureFolder(TexturesRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material cover = BuildMaterial("BookCover", "BookCover_Albedo.png", "BookCover_NormalGL.png",
                "BookCover_Roughness.png", "BookCover_Metallic.png", "BookCover_ARM.png", true);
            Material paper = BuildMaterial("BookPaper", "BookPaper_Albedo.png", "BookPaper_NormalGL.png",
                "BookPaper_Roughness.png", null, "BookPaper_ARM.png", true);
            Material stone = BuildMaterial("SwordStone", "SwordStone_Albedo.png", "SwordStone_NormalGL.png",
                "SwordStone_Roughness.png", null, "SwordStone_AO.png", false);

            string bookSource = SourcesRoot + "/book_encyclopedia_set_01/LawBook_SourceBook14_Flat.obj";
            string stoneSource = SourcesRoot + "/rock_09/rock_09_Centered.obj";
            ObjData bookData = ReadObjData(bookSource);
            ObjData stoneData = ReadObjData(stoneSource);
            MirrorSourceZ(bookData);
            MirrorSourceZ(stoneData);
            if (bookData.materialNames.Count != 2 || stoneData.materialNames.Count != 1)
                throw new InvalidOperationException("Dike prop material partition changed unexpectedly.");

            // Source flat book is centered; its cover plane is X/Z and thickness is Y.
            // The source has already been rotated newXYZ=oldZXY, so the outside front cover faces +Y.
            Mesh bookMesh = SaveMesh(bookData, "LawBook", false);
            // Keep stone proportions and move the lowest vertex onto Y=0 for floor placement.
            Mesh stoneMesh = SaveMesh(stoneData, "SwordStone", true);
            var bookMaterials = new Material[bookData.materialNames.Count];
            for (int i = 0; i < bookMaterials.Length; i++)
            {
                string name = bookData.materialNames[i];
                if (name.EndsWith("_cover", StringComparison.Ordinal)) bookMaterials[i] = cover;
                else if (name.EndsWith("_paper", StringComparison.Ordinal)) bookMaterials[i] = paper;
                else throw new InvalidOperationException("Unexpected book source material: " + name);
            }
            SavePrefab("LawBook", bookMesh, bookMaterials);
            SavePrefab("SwordStone", stoneMesh, new[] { stone });
            AssetDatabase.SaveAssets();
            var report = new PreparedReport
            {
                preparedAtUtc = DateTime.UtcNow.ToString("O", Invariant),
                entries = new[]
                {
                    Entry("LawBook", bookSource, bookMesh, bookMaterials),
                    Entry("SwordStone", stoneSource, stoneMesh, new[] { stone })
                }
            };
            File.WriteAllText(ThirdPartyRoot + "/prepared-prop-report.json", JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ThirdPartyRoot + "/prepared-prop-report.json", ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("DIKE_PROP_ASSETS_PREPARED book=" + bookMesh.vertexCount + " stone=" + stoneMesh.vertexCount
                + " resources=BottleBalance/Dike/LawBook,BottleBalance/Dike/SwordStone");
        }

        static PreparedEntry Entry(string name, string source, Mesh mesh, Material[] materials)
        {
            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
            var names = new string[materials.Length];
            for (int i = 0; i < materials.Length; i++) names[i] = materials[i].name;
            return new PreparedEntry
            {
                resource = "BottleBalance/Dike/" + name,
                prefab = DikeRoot + "/" + name + ".prefab", sourceObj = source,
                vertices = mesh.vertexCount, triangles = triangles, submeshes = mesh.subMeshCount,
                boundsMin = mesh.bounds.min, boundsSize = mesh.bounds.size, materialNames = names
            };
        }

        static Material BuildMaterial(string name, string albedoName, string normalName,
            string roughnessName, string metallicName, string aoName, bool aoIsArm)
        {
            Texture2D albedo = ImportTexture(TexturesRoot + "/" + albedoName, true, false, false);
            Texture2D normal = ImportTexture(TexturesRoot + "/" + normalName, false, true, false);
            Texture2D roughness = ImportTexture(TexturesRoot + "/" + roughnessName, false, false, true);
            Texture2D metallic = metallicName == null ? null
                : ImportTexture(TexturesRoot + "/" + metallicName, false, false, true);
            Texture2D sourceAO = ImportTexture(TexturesRoot + "/" + aoName, false, false, true);
            string metallicPath = TexturesRoot + "/" + name + "_MetallicSmoothness.png";
            string aoPath = TexturesRoot + "/" + name + "_Occlusion.png";
            PackStandardMaps(roughness, metallic, sourceAO, metallicPath, aoPath);
            Texture2D packed = ImportTexture(metallicPath, false, false, false);
            Texture2D occlusion = ImportTexture(aoPath, false, false, false);
            // CPU-readable source maps are needed only during deterministic channel packing.
            ImportTexture(TexturesRoot + "/" + roughnessName, false, false, false);
            if (metallicName != null) ImportTexture(TexturesRoot + "/" + metallicName, false, false, false);
            ImportTexture(TexturesRoot + "/" + aoName, false, false, false);

            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Built-in Standard shader missing.");
            string materialPath = PropsRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else material.shader = shader;
            material.SetColor("_Color", Color.white);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_GlossMapScale", 1f);
            material.SetFloat("_Glossiness", .5f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void PackStandardMaps(Texture2D roughness, Texture2D metallic, Texture2D ao,
            string metallicPath, string aoPath)
        {
            int width = roughness.width, height = roughness.height;
            if ((metallic != null && (metallic.width != width || metallic.height != height))
                || ao.width != width || ao.height != height)
                throw new InvalidOperationException("Source PBR map dimensions must match for lossless channel packing.");
            Color32[] rough = roughness.GetPixels32();
            Color32[] metal = metallic == null ? null : metallic.GetPixels32();
            Color32[] sourceAO = ao.GetPixels32();
            var packed = new Color32[rough.Length];
            var occlusion = new Color32[rough.Length];
            for (int i = 0; i < rough.Length; i++)
            {
                packed[i] = new Color32(metal == null ? (byte)0 : metal[i].r, 0, 0, (byte)(255 - rough[i].r));
                byte a = sourceAO[i].r; // Original ARM red = AO; explicit rock AO is grayscale.
                occlusion[i] = new Color32(a, a, a, 255); // Standard samples occlusion from G.
            }
            WritePixels(metallicPath, width, height, packed);
            WritePixels(aoPath, width, height, occlusion);
        }

        static void WritePixels(string destination, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(destination, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        static Texture2D ImportTexture(string assetPath, bool srgb, bool normal, bool readable)
        {
            if (!File.Exists(assetPath)) throw new FileNotFoundException("Missing credited Dike source map.", assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer unavailable: " + assetPath);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            if (normal)
            {
                importer.convertToNormalmap = false;
                importer.flipGreenChannel = false; // The acquired maps explicitly use the OpenGL (+Y) convention.
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        sealed class ObjData
        {
            public readonly List<Vector3> positions = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<string> materialNames = new List<string>();
            public readonly List<List<int>> triangles = new List<List<int>>();
            public bool hasMissingNormals;
            public bool hasSourceUV;
        }

        /// <summary>Returns an unsaved mesh, preserving supplied normals and accepting source OBJ without UVs.</summary>
        public static Mesh ReadObj(string sourcePath)
        {
            return ReadObj(sourcePath, Path.GetFileNameWithoutExtension(sourcePath));
        }

        /// <summary>Shared source importer. Default converts OBJ CCW front faces to Unity CW; large meshes use UInt32.</summary>
        public static Mesh ReadObj(string sourcePath, string meshName, bool baseOnFloor = false, bool reverseWinding = true)
        {
            return CreateMesh(ReadObjData(sourcePath, reverseWinding), meshName, baseOnFloor);
        }

        static ObjData ReadObjData(string sourcePath, bool reverseWinding = true)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Missing acquired Dike geometry.", sourcePath);
            var sourcePositions = new List<Vector3>();
            var sourceNormals = new List<Vector3>();
            var sourceUV = new List<Vector2>();
            var data = new ObjData();
            var vertexMap = new Dictionary<string, int>();
            int materialIndex = -1;
            foreach (string raw in File.ReadLines(sourcePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens[0] == "v") sourcePositions.Add(new Vector3(Parse(tokens[1]), Parse(tokens[2]), Parse(tokens[3])));
                else if (tokens[0] == "vn") sourceNormals.Add(new Vector3(Parse(tokens[1]), Parse(tokens[2]), Parse(tokens[3])));
                else if (tokens[0] == "vt") sourceUV.Add(new Vector2(Parse(tokens[1]), Parse(tokens[2])));
                else if (tokens[0] == "usemtl")
                {
                    string name = line.Substring(7).Trim();
                    materialIndex = data.materialNames.IndexOf(name);
                    if (materialIndex < 0)
                    {
                        materialIndex = data.materialNames.Count;
                        data.materialNames.Add(name);
                        data.triangles.Add(new List<int>());
                    }
                }
                else if (tokens[0] == "f")
                {
                    if (tokens.Length < 4)
                        throw new InvalidDataException("OBJ face has fewer than three vertices: " + sourcePath);
                    if (materialIndex < 0)
                    {
                        materialIndex = data.materialNames.Count;
                        data.materialNames.Add("Default");
                        data.triangles.Add(new List<int>());
                    }
                    int a = Vertex(tokens[1], sourcePositions, sourceUV, sourceNormals, data, vertexMap);
                    for (int i = 2; i < tokens.Length - 1; i++)
                    {
                        int b = Vertex(tokens[i], sourcePositions, sourceUV, sourceNormals, data, vertexMap);
                        int c = Vertex(tokens[i + 1], sourcePositions, sourceUV, sourceNormals, data, vertexMap);
                        // OBJ/glTF uses counterclockwise front faces. Unity uses clockwise front faces.
                        data.triangles[materialIndex].Add(a);
                        data.triangles[materialIndex].Add(reverseWinding ? c : b);
                        data.triangles[materialIndex].Add(reverseWinding ? b : c);
                    }
                }
            }
            return data;
        }

        static int Vertex(string token, List<Vector3> sourcePositions, List<Vector2> sourceUV,
            List<Vector3> sourceNormals, ObjData target, Dictionary<string, int> map)
        {
            string[] indices = token.Split('/');
            int p = Index(indices[0], sourcePositions.Count);
            int t = indices.Length > 1 && indices[1].Length != 0 ? Index(indices[1], sourceUV.Count) : -1;
            int n = indices.Length > 2 && indices[2].Length != 0 ? Index(indices[2], sourceNormals.Count) : -1;
            string key = p + "/" + t + "/" + n;
            int found;
            if (map.TryGetValue(key, out found)) return found;
            found = target.positions.Count;
            target.positions.Add(sourcePositions[p]);
            target.uv.Add(t < 0 ? Vector2.zero : sourceUV[t]);
            target.normals.Add(n < 0 ? Vector3.zero : sourceNormals[n]);
            target.hasMissingNormals |= n < 0;
            target.hasSourceUV |= t >= 0;
            map.Add(key, found);
            return found;
        }

        static int Index(string raw, int count)
        {
            int value = int.Parse(raw, Invariant);
            int index = value > 0 ? value - 1 : count + value;
            if (index < 0 || index >= count) throw new InvalidDataException("OBJ index outside declared data.");
            return index;
        }
        static float Parse(string value) { return float.Parse(value, Invariant); }

        static void MirrorSourceZ(ObjData data)
        {
            // Pair the OBJ triangle reversal with RH-to-LH Z reflection.
            // The public parser leaves this coordinate conversion to its caller.
            for (int i = 0; i < data.positions.Count; i++)
            {
                Vector3 position = data.positions[i]; position.z = -position.z; data.positions[i] = position;
                Vector3 normal = data.normals[i]; normal.z = -normal.z; data.normals[i] = normal;
            }
        }

        static Mesh SaveMesh(ObjData data, string name, bool baseOnFloor)
        {
            Mesh mesh = CreateMesh(data, name, baseOnFloor);
            string path = PropsRoot + "/Meshes/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        static Mesh CreateMesh(ObjData data, string name, bool baseOnFloor)
        {
            if (data.positions.Count == 0) throw new InvalidDataException("Empty Dike prop geometry.");
            if (baseOnFloor)
            {
                float minimumY = float.PositiveInfinity;
                foreach (Vector3 position in data.positions) minimumY = Mathf.Min(minimumY, position.y);
                for (int i = 0; i < data.positions.Count; i++) data.positions[i] -= Vector3.up * minimumY;
            }
            var mesh = new Mesh { name = name, indexFormat = data.positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(data.positions);
            mesh.SetNormals(data.normals);
            mesh.SetUVs(0, data.uv);
            mesh.subMeshCount = data.triangles.Count;
            for (int i = 0; i < data.triangles.Count; i++) mesh.SetTriangles(data.triangles[i], i, false);
            mesh.RecalculateBounds();
            if (data.hasMissingNormals) mesh.RecalculateNormals();
            if (data.hasSourceUV) mesh.RecalculateTangents();
            if (baseOnFloor && Mathf.Abs(mesh.bounds.min.y) > .00001f)
                throw new InvalidOperationException("Sword stone base is not on Y=0.");
            return mesh;
        }

        static void SavePrefab(string name, Mesh mesh, Material[] materials)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                // Visual-only prefab: the runtime mechanic owns collision/interaction and insertion slot geometry.
                PrefabUtility.SaveAsPrefabAsset(root, DikeRoot + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
