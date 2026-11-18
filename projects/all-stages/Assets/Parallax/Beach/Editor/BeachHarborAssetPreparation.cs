using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    // Source packages remain separate from generated Built-in materials/prefabs.
    // This helper neither obtains Store entitlements nor imports a unitypackage.
    public static class BeachHarborAssetPreparation
    {
        public const string TableSource = "Assets/ThirdParty/HarborIslandTable";
        public const string RockSource = "Assets/ThirdParty/HarborIslandRocks";
        public const string PalmSource = "Assets/ThirdParty/HarborIslandPalms";
        public const string ResourceRoot = "Assets/Parallax/Beach/Resources/HarborIsland";
        public const string ResourcePrefix = "HarborIsland/";
        const string Materials = ResourceRoot + "/BuiltinMaterials";

        [Serializable] sealed class SourceFile { public string file = "", sha256 = ""; }
        [Serializable] sealed class SourceRecord
        {
            public string source = "", publisher = "", package = "", license = "";
            public SourceFile[] files = Array.Empty<SourceFile>();
        }
        [Serializable] sealed class PreparedItem
        {
            public string resource, source;
            public int meshFilters, triangles;
            public bool allMeshesReadable;
        }
        [Serializable] sealed class PreparationReport
        {
            public string generatedUtc;
            public string pipeline = "Built-in";
            public bool selectedStoreAssetsAcquired;
            public string[] missingSelectedAssets;
            public PreparedItem[] prepared;
            public string physicsOwnership = "Visual prefabs only; the island/harbor builder owns physical colliders.";
        }

        [MenuItem("Parallax/Harbor Island/Prepare available selected assets")]
        public static void PrepareAvailable()
        {
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MarkReadable(TableSource); MarkReadable(RockSource); MarkReadable(PalmSource);
            ConfigureTextures(TableSource); ConfigureTextures(RockSource); ConfigureTextures(PalmSource);
            var prepared = new List<PreparedItem> { PrepareTable() };
            prepared.AddRange(PrepareSet(RockSource, "Rock", false));
            prepared.AddRange(PrepareSet(PalmSource, "Palm", true));
            var missing = new List<string>();
            if(!prepared.Any(p => p.resource.StartsWith(ResourcePrefix + "Rock", StringComparison.Ordinal)))
                missing.Add("C01: Stylized Rocks Free Demo Pack (Unity Store 355500), original package/model data missing");
            if(!prepared.Any(p => p.resource.StartsWith(ResourcePrefix + "Palm", StringComparison.Ordinal)))
                missing.Add("P01: 3D Stylized Palm Trees FREE SAMPLE (Unity Store 323517), original package/model data missing");
            var report = new PreparationReport { generatedUtc = DateTime.UtcNow.ToString("o"),
                selectedStoreAssetsAcquired = missing.Count == 0, missingSelectedAssets = missing.ToArray(), prepared = prepared.ToArray() };
            File.WriteAllText(ResourceRoot + "/preparation.json", JsonUtility.ToJson(report, true) + "\n", new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("HARBOR_ASSET_PREPARED count=" + prepared.Count + " selectedStoreAssetsAcquired=" + report.selectedStoreAssetsAcquired);
            foreach(string item in missing) Debug.LogWarning("HARBOR_SELECTED_ASSET_MISSING " + item);
        }

        [MenuItem("Parallax/Harbor Island/Prepare all selected assets (strict)")]
        public static void Prepare()
        {
            PrepareAvailable();
            var report = JsonUtility.FromJson<PreparationReport>(File.ReadAllText(ResourceRoot + "/preparation.json"));
            if(!report.selectedStoreAssetsAcquired)
                throw new InvalidOperationException("Selected free Store source packages have not all been acquired/prepared: " + string.Join("; ", report.missingSelectedAssets));
        }

        static string[] AssetPaths(string root, string filter)
        {
            if(!AssetDatabase.IsValidFolder(root)) return Array.Empty<string>();
            return AssetDatabase.FindAssets(filter, new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        static void MarkReadable(string root)
        {
            foreach(string path in AssetPaths(root, "t:Model"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            }
        }

        static void ConfigureTextures(string root)
        {
            foreach(string path in AssetPaths(root, "t:Texture2D"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer == null) continue;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool normal = name.Contains("normal") || name.Contains("nor_gl") || name.EndsWith("_n", StringComparison.Ordinal);
                bool linear = normal || name.Contains("rough") || name.Contains("metal") || name.Contains("mask") || name.Contains("occlusion") || name.Contains("_arm");
                bool changed = importer.sRGBTexture == linear;
                if(changed) importer.sRGBTexture = !linear;
                if(normal && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
                // Read only the table roughness to pack Standard's smoothness alpha.
                if(root == TableSource && (name.Contains("rough") || name.Contains("metal")) && !importer.isReadable) { importer.isReadable = true; changed = true; }
                if(changed) importer.SaveAndReimport();
            }
        }

        // Copies actual source records verbatim; missing selected Store packs do
        // not receive an attribution that could imply they were used.
        public static void WriteCredits(string output)
        {
            Directory.CreateDirectory(output);
            string destination = Path.Combine(output, "ThirdPartyNotices", "HarborIsland");
            Directory.CreateDirectory(destination);
            var credits = new List<string> { "Harbor Island asset credits", "",
                "M01 Wooden Table 02 / Serhii Khromov / Poly Haven", "CC0 1.0 Universal",
                "https://polyhaven.com/a/wooden_table_02", "https://polyhaven.com/license", "",
                "Stylized Pirate Ship / Comeback", "Standard Unity Asset Store EULA",
                "https://assetstore.unity.com/packages/3d/vehicles/sea/stylized-pirate-ship-200192", "https://unity.com/legal/as-terms", "" };
            VerifySourceFiles(TableSource);
            CopyRequired(TableSource + "/LICENSE.txt", Path.Combine(destination, "M01-LICENSE.txt"));
            CopyRequired(TableSource + "/source.json", Path.Combine(destination, "M01-source.json"));
            CopyRequired("Assets/ThirdParty/StylizedPirateShip/source.json", Path.Combine(destination, "Ship-source.json"));
            if(StoreSourceVerified(RockSource, "355500"))
            {
                CopyRequired(RockSource + "/source.json", Path.Combine(destination, "C01-source.json"));
                credits.AddRange(new[] { "C01 Stylized Rocks Free Demo Pack / 3DMarkus", "Standard Unity Asset Store EULA",
                    "https://assetstore.unity.com/packages/3d/environments/landscapes/stylized-rocks-free-demo-pack-355500", "" });
            }
            if(StoreSourceVerified(PalmSource, "323517"))
            {
                CopyRequired(PalmSource + "/source.json", Path.Combine(destination, "P01-source.json"));
                credits.AddRange(new[] { "P01 3D Stylized Palm Trees (FREE SAMPLE) / Sara Bellio", "Standard Unity Asset Store EULA",
                    "https://assetstore.unity.com/packages/3d/vegetation/trees/3d-stylized-palm-trees-free-sample-323517", "" });
            }
            File.WriteAllLines(Path.Combine(output, "HarborIsland-CREDITS.txt"), credits, new System.Text.UTF8Encoding(false));
        }

        static void CopyRequired(string source, string destination)
        {
            if(!File.Exists(source)) throw new FileNotFoundException("Required Harbor Island attribution is missing", source);
            File.Copy(source, destination, true);
        }

        static bool StoreSourceVerified(string root, string productId)
        {
            string path = root + "/source.json";
            if(!File.Exists(path)) return false;
            var record = JsonUtility.FromJson<SourceRecord>(File.ReadAllText(path));
            if(record == null || !Uri.TryCreate(record.source, UriKind.Absolute, out var uri) || uri.Host != "assetstore.unity.com"
                || !uri.AbsolutePath.EndsWith("-" + productId, StringComparison.Ordinal)
                || record.license != "Standard Unity Asset Store EULA") return false;
            VerifySourceFiles(root); return true;
        }

        static bool IsSha(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        static void VerifySourceFiles(string root)
        {
            var record = JsonUtility.FromJson<SourceRecord>(File.ReadAllText(root + "/source.json"));
            if(record == null || record.files == null || record.files.Length == 0) throw new InvalidOperationException("No original asset hash records: " + root);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach(var file in record.files)
            {
                string path = Path.GetFullPath(Path.Combine(root, file.file ?? ""));
                if(!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !IsSha(file.sha256) || !File.Exists(path))
                    throw new InvalidOperationException("Unsafe or missing original asset record: " + root + "/" + file.file);
                using(var hash = SHA256.Create()) using(var stream = File.OpenRead(path))
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                    if(!actual.Equals(file.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Original asset hash mismatch: " + path);
                }
            }
        }

        static PreparedItem PrepareTable()
        {
            VerifySourceFiles(TableSource);
            const string modelPath = TableSource + "/wooden_table_02_2k.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if(source == null) throw new InvalidOperationException("Official M01 Wooden Table 02 source FBX is missing: " + modelPath);
            var material = new Material(RequireShader("Standard")) { name = "M01 Wooden Table 02 Built-in", color = Color.white };
            material.mainTexture = RequiredTexture(TableSource + "/textures/wooden_table_02_diff_2k.jpg");
            material.SetTexture("_BumpMap", RequiredTexture(TableSource + "/textures/wooden_table_02_nor_gl_2k.exr"));
            material.EnableKeyword("_NORMALMAP");
            var rough = RequiredTexture(TableSource + "/textures/wooden_table_02_rough_2k.jpg");
            var metal = RequiredTexture(TableSource + "/textures/wooden_table_02_metal_2k.exr");
            if(metal.width != rough.width || metal.height != rough.height) throw new InvalidOperationException("M01 metallic/roughness source resolutions differ.");
            var pixels = rough.GetPixels32(); var metallic = metal.GetPixels();
            for(int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(metallic[i].r) * 255), 0, 0, (byte)(255 - pixels[i].r));
            var packed = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true) { name = "M01 Standard metallic smoothness" };
            try { packed.SetPixels32(pixels); packed.Apply(); File.WriteAllBytes(Materials + "/TableMetallicSmoothness.png", packed.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(packed); }
            AssetDatabase.ImportAsset(Materials + "/TableMetallicSmoothness.png", ImportAssetOptions.ForceSynchronousImport);
            var packedImporter = (TextureImporter)AssetImporter.GetAtPath(Materials + "/TableMetallicSmoothness.png");
            if(packedImporter.sRGBTexture) { packedImporter.sRGBTexture = false; packedImporter.SaveAndReimport(); }
            material.SetTexture("_MetallicGlossMap", RequiredTexture(Materials + "/TableMetallicSmoothness.png"));
            material.SetFloat("_GlossMapScale", 1); material.EnableKeyword("_METALLICGLOSSMAP");
            material = Save(material, Materials + "/Table.mat");
            return SaveVisual(source, modelPath, "Table", false, material);
        }

        static IEnumerable<PreparedItem> PrepareSet(string root, string prefix, bool foliage)
        {
            if(!StoreSourceVerified(root, prefix == "Rock" ? "355500" : "323517")) yield break;
            string[] sources = AssetPaths(root, "t:Prefab").Where(p => !IsDemo(p)).ToArray();
            if(sources.Length == 0) sources = AssetPaths(root, "t:Model").Where(p => !IsDemo(p)).ToArray();
            int index = 0;
            foreach(string path in sources)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(model == null || model.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null)) continue;
                yield return SaveVisual(model, path, prefix + (++index).ToString("00"), foliage, null);
            }
        }

        static bool IsDemo(string path)
        {
            string value = path.ToLowerInvariant();
            return value.Contains("/demo/") || value.Contains("/demos/") || value.Contains("/example/") || value.Contains("/examples/") || value.Contains("/scenes/");
        }

        static PreparedItem SaveVisual(GameObject source, string sourcePath, string name, bool foliage, Material forced)
        {
            var root = new GameObject("HarborIsland " + name);
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform);
                model.name = source.name; model.transform.localPosition = Vector3.zero;
                foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) if(behaviour != null) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach(var transform in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                // Nature-package physics can fill the future tunnel. The assembled
                // island owns its deliberate shell/floor collision independently.
                foreach(var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for(int i = 0; i < materials.Length; i++) materials[i] = forced != null ? forced : ConvertMaterial(materials[i], name, foliage);
                    renderer.sharedMaterials = materials; renderer.receiveShadows = true;
                    renderer.shadowCastingMode = foliage ? ShadowCastingMode.TwoSided : ShadowCastingMode.On;
                }
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length == 0) throw new InvalidOperationException("No rendered geometry in " + sourcePath);
                var bounds = renderers[0].bounds;
                foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var filters = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                bool readable = filters.All(f => f.sharedMesh.isReadable);
                if(!readable) throw new InvalidOperationException("A shadow-query source mesh is not readable: " + sourcePath);
                PrefabUtility.SaveAsPrefabAsset(root, ResourceRoot + "/" + name + ".prefab");
                return new PreparedItem { resource = ResourcePrefix + name, source = sourcePath, meshFilters = filters.Length,
                    triangles = filters.Sum(f => f.sharedMesh.triangles.Length / 3), allMeshesReadable = readable };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Material ConvertMaterial(Material source, string prefix, bool palm)
        {
            if(source == null) throw new InvalidOperationException("A selected asset has an unassigned material: " + prefix);
            bool alpha = palm && (source.name.IndexOf("foliage", StringComparison.OrdinalIgnoreCase) >= 0 || source.name.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0 || Float(source, "_AlphaCutoffEnable", 0) > .5f || Float(source, "_AlphaClip", 0) > .5f);
            var shader = alpha ? FoliageShader() : RequireShader("Standard");
            var material = new Material(shader) { name = prefix + " " + source.name + " Built-in" };
            material.color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            material.mainTexture = Texture(source, "_BaseColorMap", "_BaseMap", "_MainTex");
            var normal = Texture(source, "_NormalMap", "_BumpMap");
            if(normal != null) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", Float(source, "_NormalScale", Float(source, "_BumpScale", 1))); material.EnableKeyword("_NORMALMAP"); }
            material.SetFloat("_Metallic", Float(source, "_Metallic", 0));
            material.SetFloat("_Glossiness", Float(source, "_Smoothness", Float(source, "_Glossiness", .25f)));
            var mask = Texture(source, "_MaskMap", "_MetallicGlossMap");
            if(!alpha && mask != null) { material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask); material.SetFloat("_GlossMapScale", 1); material.EnableKeyword("_METALLICGLOSSMAP"); }
            if(alpha) material.SetFloat("_Cutoff", Float(source, "_AlphaCutoff", Float(source, "_Cutoff", .45f)));
            string safe = string.Concat(source.name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            return Save(material, Materials + "/" + prefix + "_" + safe + "_" + (guid.Length >= 8 ? guid.Substring(0, 8) : "embedded") + ".mat");
        }

        static UnityEngine.Texture Texture(Material material, params string[] names)
        { foreach(string name in names) if(material.HasProperty(name) && material.GetTexture(name) != null) return material.GetTexture(name); return null; }
        static float Float(Material material, string name, float fallback) => material.HasProperty(name) ? material.GetFloat(name) : fallback;
        static Texture2D RequiredTexture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Missing selected texture: " + path);
        static Shader RequireShader(string name) => Shader.Find(name) ?? throw new InvalidOperationException("Required Built-in shader is missing: " + name);
        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); return existing;
        }

        static Shader FoliageShader()
        {
            const string path = Materials + "/HarborFoliage.shader";
            const string code = @"Shader ""Parallax/HarborIslandFoliage"" {
Properties { _MainTex (""Albedo"", 2D) = ""white"" {} _Color (""Tint"", Color) = (1,1,1,1) _Cutoff (""Alpha cutoff"", Range(0,1)) = .45 _BumpMap (""Normal"", 2D) = ""bump"" {} _BumpScale (""Normal strength"", Float) = 1 _Metallic (""Metallic"", Range(0,1)) = 0 _Glossiness (""Smoothness"", Range(0,1)) = .25 }
SubShader { Tags { ""Queue""=""AlphaTest"" ""RenderType""=""TransparentCutout"" } Cull Off LOD 200
CGPROGRAM
#pragma surface surf Standard fullforwardshadows addshadow alphatest:_Cutoff
#pragma target 3.0
#include ""UnityStandardUtils.cginc""
sampler2D _MainTex; sampler2D _BumpMap; fixed4 _Color; half _BumpScale; half _Metallic; half _Glossiness;
struct Input { float2 uv_MainTex; float2 uv_BumpMap; };
void surf (Input IN, inout SurfaceOutputStandard o) { fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color; o.Albedo = c.rgb; o.Alpha = c.a; o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale); o.Metallic = _Metallic; o.Smoothness = _Glossiness; }
ENDCG
} FallBack ""Transparent/Cutout/Diffuse"" }";
            if(!File.Exists(path) || File.ReadAllText(path) != code) { File.WriteAllText(path, code, new System.Text.UTF8Encoding(false)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if(shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Built-in palm foliage shader failed to compile.");
            return shader;
        }
    }
}