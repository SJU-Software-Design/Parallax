using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    /// <summary>Derives the concept sword from the credited CC0 blade/grip and new hilt ornament.</summary>
    public static class GalleryDikeConceptSwordAssets
    {
        const string SourceRoot = "Assets/Parallax/Gallery/ThirdParty/BottleBalanceDikeSword";
        const string SourceFbx = SourceRoot + "/Original/sword.fbx";
        const string SourceTextures = SourceRoot + "/Original/EmbeddedTextures";
        const string ResourceRoot = "Assets/Parallax/Gallery/Resources/BottleBalance/Dike";
        const string WorkRoot = ResourceRoot + "/ConceptSword";

        sealed class SourcePart
        {
            public string name;
            public Vector3[] positions, normals;
            public Vector4[] tangents;
            public Vector2[] uv;
            public int[] triangles;
            public Bounds bounds;
        }
        [Serializable] sealed class Rig
        {
            public float gripY;
            public bool independentSwordVerified = true;
            public string source = "CC0 Izkeas Brescia Spadona blade + wrapped grip; project-authored gilded guard, medallion, rings and wheel pommel";
        }
        [Serializable] sealed class Report
        {
            public string source = SourceFbx;
            public string license = "CC0 1.0";
            public string sourceAuthor = "Izkeas";
            public string sourcePage = "https://izkeas.itch.io/realistic-medieval-long-sword";
            public string reference = "기획안/미술관_디케의저울_20261113/01_맵과기믹_도안.png (lower-right concept)";
            public string changes = "Preserved source blade and leather grip geometry, UVs and PBR maps with one uniform normalization. Replaced original straight guard and faceted pommel with project-authored beveled curled gold guard, circular medallion, wheel pommel and rings.";
            public int sourceBladeTriangles, sourceGripTriangles, outputTriangles, outputRenderers;
            public float gripY, sourceLength;
            public Vector3 normalizedBoundsMin, normalizedBoundsMax;
            public bool runtimeChanged = false;
            public bool oldRewardSwordOverwritten = false;
            public string normalConvention = "Original extracted normal PNGs preserved. Author does not label GL/DX; preview review required.";
        }

        [MenuItem("Parallax/Museum/Prepare Concept Sword And Isolated Preview")]
        public static void Prepare()
        {
            EnsureFolder(WorkRoot + "/Meshes");
            EnsureFolder(WorkRoot + "/Materials");
            EnsureFolder(WorkRoot + "/Textures");
            AssetDatabase.ImportAsset(SourceFbx, ImportAssetOptions.ForceSynchronousImport);
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (sourceAsset == null) throw new InvalidOperationException("Acquired Brescia Spadona FBX has not imported.");
            GameObject sourceInstance = UnityEngine.Object.Instantiate(sourceAsset);
            var output = new GameObject("ConceptSword");
            try
            {
                sourceInstance.hideFlags = HideFlags.HideAndDontSave;
                SourcePart blade = FindPart(sourceInstance, "blade", "SwordBlade");
                SourcePart grip = FindPart(sourceInstance, "hilt", "HiltMaterial");
                SourcePart guard = FindPart(sourceInstance, "Cube", "SwordGuard");
                SourcePart pommel = FindPart(sourceInstance, "pommel", "Pommel");
                Bounds allBounds = blade.bounds; allBounds.Encapsulate(grip.bounds); allBounds.Encapsulate(guard.bounds); allBounds.Encapsulate(pommel.bounds);

                // FBX object transforms are baked before choosing a proper rotation frame.
                // Direction comes from blade -> grip, so the complete source tip becomes -Y.
                Vector3 axisY = (grip.bounds.center - blade.bounds.center).normalized;
                Vector3 wideAxis = PrincipalAxis(guard.positions);
                Vector3 axisX = (wideAxis - axisY * Vector3.Dot(wideAxis, axisY)).normalized;
                if (Vector3.Dot(axisX, Vector3.right) < 0f) axisX = -axisX;
                Vector3 axisZ = Vector3.Cross(axisX, axisY).normalized;
                axisX = Vector3.Cross(axisY, axisZ).normalized;
                if (axisX.sqrMagnitude < .9f || axisZ.sqrMagnitude < .9f)
                    throw new InvalidOperationException("Sword source basis is degenerate.");

                var allPositions = new List<Vector3>();
                allPositions.AddRange(blade.positions); allPositions.AddRange(grip.positions);
                allPositions.AddRange(guard.positions); allPositions.AddRange(pommel.positions);
                Bounds projectedBounds = ProjectedBounds(allPositions, axisX, axisY, axisZ);
                float sourceLength = projectedBounds.size.y;
                if (sourceLength < .0001f) throw new InvalidOperationException("Sword source has no longitudinal extent.");
                Vector3 center = projectedBounds.center;
                Normalize(blade, axisX, axisY, axisZ, center, sourceLength);
                Normalize(grip, axisX, axisY, axisZ, center, sourceLength);
                Normalize(guard, axisX, axisY, axisZ, center, sourceLength);
                Normalize(pommel, axisX, axisY, axisZ, center, sourceLength);

                Material steel = BuildSourceMaterial("ConceptBladeSteel", "BladeColorTexture.png", "BladeNormalsTexture.png", "BladeRoughnessTexture.png", 1f, .72f);
                Material leather = BuildSourceMaterial("ConceptDarkLeather", "hiltColorTexture.png", "hiltNormalTexture.png", null, 0f, .25f);
                Material gold = PlainMetal("ConceptGildedHilt", new Color(.80f, .52f, .16f), .84f, .60f);
                Material paleGold = PlainMetal("ConceptGoldHighlights", new Color(.93f, .70f, .30f), .88f, .68f);
                Material engraving = PlainMetal("ConceptGoldEngraving", new Color(.30f, .14f, .035f), .78f, .44f);
                AddVisual(output.transform, "ExternalSteelBlade", MeshFrom(blade, "ConceptBlade"), steel);
                AddVisual(output.transform, "ExternalWrappedGrip", MeshFrom(grip, "ConceptGrip"), leather);

                float guardY = guard.bounds.center.y;
                float halfSpan = Mathf.Clamp(guard.bounds.size.x * .46f, .135f, .175f);
                float thickness = Mathf.Clamp(grip.bounds.size.x * .37f, .008f, .012f);
                Vector3 guardCenter = new Vector3(0f, guardY, grip.bounds.center.z);
                AddVisual(output.transform, "BeveledCurledCrossguard", CurledGuard(halfSpan, thickness, guardCenter), gold);
                float badgeRadius = Mathf.Clamp(grip.bounds.size.x * .72f, .023f, .034f);
                AddCoin(output.transform, "GuardMedallion", guardCenter, badgeRadius, .012f, gold, paleGold, engraving);

                float wheelRadius = Mathf.Clamp((.5f - grip.bounds.max.y + .014f) * .5f, .028f, .043f);
                Vector3 wheelCenter = new Vector3(0f, .5f - wheelRadius, grip.bounds.center.z);
                AddCoin(output.transform, "RoundWheelPommel", wheelCenter, wheelRadius, .012f, gold, paleGold, engraving);
                float gripRadius = Mathf.Clamp(Mathf.Max(grip.bounds.size.x, grip.bounds.size.z) * .52f, .009f, .020f);
                AddVisual(output.transform, "LowerGripGoldRing", Torus(new Vector3(0f, grip.bounds.min.y + .005f, grip.bounds.center.z), Vector3.up, gripRadius, .0026f, 64, 10), paleGold);
                AddVisual(output.transform, "UpperGripGoldRing", Torus(new Vector3(0f, grip.bounds.max.y - .004f, grip.bounds.center.z), Vector3.up, gripRadius * .94f, .0024f, 64, 10), paleGold);
                // A fine second ring gives the collars a machined edge instead of primitive caps.
                AddVisual(output.transform, "LowerGripRingInset", Torus(new Vector3(0f, grip.bounds.min.y + .010f, grip.bounds.center.z), Vector3.up, gripRadius * .99f, .0010f, 64, 8), engraving);

                Bounds finalBounds = RendererBounds(output);
                if (Mathf.Abs(finalBounds.size.y - 1f) > .006f)
                    throw new InvalidOperationException("Concept sword must retain the original proportional unit length: " + finalBounds.size);
                var rig = new Rig { gripY = grip.bounds.center.y - finalBounds.center.y };
                string rigPath = ResourceRoot + "/ConceptSwordRig.json";
                File.WriteAllText(rigPath, JsonUtility.ToJson(rig, true));
                AssetDatabase.ImportAsset(rigPath, ImportAssetOptions.ForceSynchronousImport);
                PrefabUtility.SaveAsPrefabAsset(output, ResourceRoot + "/ConceptSword.prefab");
                var report = new Report
                {
                    sourceBladeTriangles = blade.triangles.Length / 3, sourceGripTriangles = grip.triangles.Length / 3,
                    outputTriangles = TriangleCount(output), outputRenderers = output.GetComponentsInChildren<MeshRenderer>().Length,
                    sourceLength = sourceLength, gripY = rig.gripY,
                    normalizedBoundsMin = finalBounds.min, normalizedBoundsMax = finalBounds.max
                };
                File.WriteAllText(WorkRoot + "/concept-sword-prepared.json", JsonUtility.ToJson(report, true));
                AssetDatabase.ImportAsset(WorkRoot + "/concept-sword-prepared.json", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                Debug.Log("DIKE_CONCEPT_SWORD_PREPARED sourceBlade=" + report.sourceBladeTriangles + " sourceGrip=" + report.sourceGripTriangles
                    + " triangles=" + report.outputTriangles + " gripY=" + rig.gripY + " oldRewardUnchanged=1");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceInstance);
                UnityEngine.Object.DestroyImmediate(output);
            }
            CapturePreview();
        }

        public static void CapturePreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceRoot + "/ConceptSword.prefab");
            if (prefab == null) throw new InvalidOperationException("Prepare the concept sword before isolated preview.");
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(projectRoot, "Builds", "DikeSwordPreview");
            Directory.CreateDirectory(directory);
            Capture(prefab, Path.Combine(directory, "concept-sword-full.png"), false);
            Capture(prefab, Path.Combine(directory, "concept-sword-hilt.png"), true);
            Debug.Log("DIKE_CONCEPT_SWORD_PREVIEW " + directory);
        }

        static void Capture(GameObject prefab, string path, bool hiltOnly)
        {
            var preview = new PreviewRenderUtility();
            GameObject instance = null;
            Texture2D texture = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                preview.AddSingleGO(instance);
                preview.ambientColor = new Color(.42f, .43f, .46f);
                preview.lights[0].intensity = 1.6f;
                preview.lights[0].color = new Color(1f, .94f, .84f);
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                preview.lights[1].intensity = 1.1f;
                preview.lights[1].color = new Color(.72f, .84f, 1f);
                preview.lights[1].transform.rotation = Quaternion.Euler(320f, 145f, 0f);
                Camera camera = preview.camera;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(.075f, .085f, .105f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = hiltOnly ? .235f : .57f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 10f;
                Vector3 focus = hiltOnly ? new Vector3(0f, .31f, 0f) : Vector3.zero;
                camera.transform.position = focus + new Vector3(.09f, .025f, -2.5f);
                camera.transform.LookAt(focus, Vector3.up);
                int width = hiltOnly ? 1600 : 1200, height = hiltOnly ? 1000 : 1600;
                preview.BeginStaticPreview(new Rect(0, 0, width, height));
                preview.Render(true);
                texture = preview.EndStaticPreview();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                preview.Cleanup();
            }
        }

        static SourcePart FindPart(GameObject source, string transformName, string materialName)
        {
            MeshFilter found = null;
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                bool matches = filter.name.Equals(transformName, StringComparison.OrdinalIgnoreCase);
                if (renderer != null)
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null && material.name.IndexOf(materialName, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                if (!matches || filter.sharedMesh == null) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous source sword part: " + materialName);
                found = filter;
            }
            if (found == null) throw new InvalidOperationException("Missing acquired sword part: " + materialName);
            Mesh readable = ReadableCopy(found.sharedMesh);
            try
            {
                Vector3[] vertices = readable.vertices, normals = readable.normals;
                Vector4[] tangents = readable.tangents;
                Matrix4x4 matrix = found.transform.localToWorldMatrix, normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                float parity = matrix.determinant < 0f ? -1f : 1f;
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangent = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w * parity);
                }
                int[] triangles = readable.triangles;
                if (parity < 0f) for (int i = 0; i < triangles.Length; i += 3) { int t = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = t; }
                if (readable.uv.Length != vertices.Length || normals.Length != vertices.Length)
                    throw new InvalidOperationException("Source blade/grip must preserve UVs and normals.");
                return new SourcePart { name = materialName, positions = vertices, normals = normals, tangents = tangents,
                    uv = readable.uv, triangles = triangles, bounds = BoundsOf(vertices) };
            }
            finally { UnityEngine.Object.DestroyImmediate(readable); }
        }

        static void Normalize(SourcePart part, Vector3 x, Vector3 y, Vector3 z, Vector3 center, float length)
        {
            for (int i = 0; i < part.positions.Length; i++) part.positions[i] = (Project(part.positions[i], x, y, z) - center) / length;
            for (int i = 0; i < part.normals.Length; i++) part.normals[i] = Project(part.normals[i], x, y, z).normalized;
            for (int i = 0; i < part.tangents.Length; i++)
            { Vector4 t = part.tangents[i]; Vector3 p = Project(new Vector3(t.x, t.y, t.z), x, y, z).normalized; part.tangents[i] = new Vector4(p.x, p.y, p.z, t.w); }
            part.bounds = BoundsOf(part.positions);
        }
        static Vector3 Project(Vector3 p, Vector3 x, Vector3 y, Vector3 z) { return new Vector3(Vector3.Dot(p, x), Vector3.Dot(p, y), Vector3.Dot(p, z)); }
        static Bounds ProjectedBounds(List<Vector3> p, Vector3 x, Vector3 y, Vector3 z)
        { var values = new Vector3[p.Count]; for (int i = 0; i < p.Count; i++) values[i] = Project(p[i], x, y, z); return BoundsOf(values); }
        static Bounds BoundsOf(Vector3[] vertices)
        { if (vertices.Length == 0) throw new InvalidOperationException("Empty sword geometry."); Bounds b = new Bounds(vertices[0], Vector3.zero); foreach (Vector3 v in vertices) b.Encapsulate(v); return b; }
        static Vector3 PrincipalAxis(Vector3[] p)
        {
            Vector3 center = Vector3.zero; foreach (Vector3 v in p) center += v; center /= p.Length;
            float[,] covariance = new float[3, 3]; foreach (Vector3 point in p) { Vector3 d = point - center; for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) covariance[r, c] += d[r] * d[c]; }
            Vector3 axis = Vector3.right; for (int n = 0; n < 40; n++) axis = new Vector3(covariance[0, 0] * axis.x + covariance[0, 1] * axis.y + covariance[0, 2] * axis.z,
                covariance[1, 0] * axis.x + covariance[1, 1] * axis.y + covariance[1, 2] * axis.z,
                covariance[2, 0] * axis.x + covariance[2, 1] * axis.y + covariance[2, 2] * axis.z).normalized;
            return axis;
        }
        static Mesh MeshFrom(SourcePart part, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = part.positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = part.positions; mesh.normals = part.normals; mesh.uv = part.uv;
            mesh.triangles = part.triangles;
            if (part.tangents.Length == part.positions.Length) mesh.tangents = part.tangents; else mesh.RecalculateTangents();
            mesh.RecalculateBounds(); return mesh;
        }

        static Mesh ReadableCopy(Mesh source)
        {
            using (Mesh.MeshDataArray read = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData data = read[0]; Mesh.MeshDataArray write = Mesh.AllocateWritableMeshData(1); bool applied = false; var copy = new Mesh();
                try
                {
                    Mesh.MeshData destination = write[0]; destination.SetVertexBufferParams(data.vertexCount, source.GetVertexAttributes());
                    for (int stream = 0; stream < source.vertexBufferCount; stream++) destination.GetVertexData<byte>(stream).CopyFrom(data.GetVertexData<byte>(stream));
                    if (data.indexFormat == IndexFormat.UInt32) { var indices = data.GetIndexData<uint>(); destination.SetIndexBufferParams(indices.Length, IndexFormat.UInt32); destination.GetIndexData<uint>().CopyFrom(indices); }
                    else { var indices = data.GetIndexData<ushort>(); destination.SetIndexBufferParams(indices.Length, IndexFormat.UInt16); destination.GetIndexData<ushort>().CopyFrom(indices); }
                    destination.subMeshCount = data.subMeshCount;
                    for (int i = 0; i < data.subMeshCount; i++) destination.SetSubMesh(i, data.GetSubMesh(i), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    Mesh.ApplyAndDisposeWritableMeshData(write, copy, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices); applied = true; copy.bounds = source.bounds; copy.UploadMeshData(false); return copy;
                }
                catch { UnityEngine.Object.DestroyImmediate(copy); throw; }
                finally { if (!applied) write.Dispose(); }
            }
        }

        static Material BuildSourceMaterial(string name, string colorName, string normalName, string roughName, float metallic, float smoothness)
        {
            Texture2D color = CopyTexture(colorName, true, false, false), normal = CopyTexture(normalName, false, true, false);
            Material material = PlainMetal(name, Color.white, metallic, smoothness);
            material.SetTexture("_MainTex", color); material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f); material.EnableKeyword("_NORMALMAP");
            if (roughName != null)
            {
                Texture2D rough = CopyTexture(roughName, false, false, true); Color32[] values = rough.GetPixels32();
                for (int i = 0; i < values.Length; i++) values[i] = new Color32((byte)Mathf.RoundToInt(metallic * 255f), 0, 0, (byte)(255 - values[i].r));
                var packed = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
                string packedPath = WorkRoot + "/Textures/" + name + "_MetallicSmoothness.png";
                try { packed.SetPixels32(values); packed.Apply(false, false); File.WriteAllBytes(packedPath, packed.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(packed); }
                AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(packedPath, false, false, false);
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
                material.EnableKeyword("_METALLICGLOSSMAP"); material.SetFloat("_GlossMapScale", 1f);
                ConfigureTexture(AssetDatabase.GetAssetPath(rough), false, false, false);
            }
            EditorUtility.SetDirty(material); return material;
        }
        static Texture2D CopyTexture(string sourceName, bool srgb, bool normal, bool readable)
        {
            string source = SourceTextures + "/" + sourceName, target = WorkRoot + "/Textures/" + sourceName;
            if (!File.Exists(source)) throw new FileNotFoundException("Missing original extracted sword PBR map.", source);
            File.Copy(source, target, true); AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(target, srgb, normal, readable); return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }
        static void ConfigureTexture(string path, bool srgb, bool normal, bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Sword texture importer missing: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.isReadable = readable; importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
            importer.SaveAndReimport();
        }
        static Material PlainMetal(string name, Color color, float metallic, float smoothness)
        {
            string path = WorkRoot + "/Materials/" + name + ".mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard"); if (shader == null) throw new InvalidOperationException("Built-in Standard shader unavailable.");
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            material.SetColor("_Color", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Mode", 0f); material.SetInt("_SrcBlend", (int)BlendMode.One); material.SetInt("_DstBlend", (int)BlendMode.Zero); material.SetInt("_ZWrite", 1); material.renderQueue = -1;
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); EditorUtility.SetDirty(material); return material;
        }

        static void AddVisual(Transform parent, string name, Mesh generated, Material material)
        {
            string assetPath = WorkRoot + "/Meshes/" + name + ".asset"; generated.name = name;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, assetPath); }
            else
            {
                // Keep the asset GUID while notifying the native mesh/GPU buffers of every change.
                // CopySerialized alone can leave the same-process isolated preview on old buffers.
                mesh.Clear(false);
                mesh.indexFormat = generated.indexFormat;
                mesh.vertices = generated.vertices;
                mesh.normals = generated.normals;
                mesh.uv = generated.uv;
                mesh.tangents = generated.tangents;
                mesh.colors32 = generated.colors32;
                mesh.subMeshCount = generated.subMeshCount;
                for (int i = 0; i < generated.subMeshCount; i++)
                    mesh.SetTriangles(generated.GetTriangles(i), i, false);
                mesh.bounds = generated.bounds;
                mesh.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }
            var child = new GameObject(name); child.transform.SetParent(parent, false); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        static Mesh CurledGuard(float span, float halfWidth, Vector3 center)
        {
            var right = new List<Vector2>();
            for (int i = 0; i <= 30; i++) right.Add(Bezier(Vector2.zero, new Vector2(.055f, .025f), new Vector2(.115f, .013f), new Vector2(.150f, .005f), i / 30f));
            for (int i = 1; i <= 32; i++) right.Add(Bezier(new Vector2(.150f, .005f), new Vector2(.169f, .003f), new Vector2(.175f, .021f), new Vector2(.166f, .020f), i / 32f));
            var path = new List<Vector2>(); for (int i = right.Count - 1; i > 0; i--) path.Add(new Vector2(-right[i].x, right[i].y)); path.AddRange(right);
            var vertices = new List<Vector3>(); var indices = new List<int>();
            Vector2[] profile = { new Vector2(-.65f,-1),new Vector2(.65f,-1),new Vector2(1,-.65f),new Vector2(1,.65f),new Vector2(.65f,1),new Vector2(-.65f,1),new Vector2(-1,.65f),new Vector2(-1,-.65f) };
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p = path[i]; p.x *= span / .18f;
                Vector2 tangent = path[Mathf.Min(i+1,path.Count-1)] - path[Mathf.Max(i-1,0)]; tangent.x *= span / .18f; tangent.Normalize();
                Vector2 side = new Vector2(-tangent.y, tangent.x); float width = Mathf.Lerp(halfWidth, halfWidth * .59f, Mathf.Abs(p.x) / span);
                foreach (Vector2 point in profile) vertices.Add(center + new Vector3(p.x + side.x * point.x * width, p.y + side.y * point.x * width, point.y * .0105f));
                if (i == 0) continue; int a = (i-1)*8, b = i*8;
                for (int j = 0; j < 8; j++) { int next=(j+1)%8; indices.AddRange(new[]{a+j,b+next,b+j,a+j,a+next,b+next}); }
            }
            for (int j=1;j<7;j++){indices.AddRange(new[]{0,j+1,j});int a=(path.Count-1)*8;indices.AddRange(new[]{a,a+j,a+j+1});}
            return GeneratedMesh("CurledGuard", vertices, indices);
        }
        static Vector2 Bezier(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        { float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d; }

        static void AddCoin(Transform root, string name, Vector3 center, float radius, float halfDepth, Material gold, Material bright, Material dark)
        {
            AddVisual(root,name,Coin(center,radius,halfDepth),gold);
            foreach(int sign in new[]{-1,1})
            {
                Vector3 face=center+Vector3.forward*(sign*(halfDepth+.0004f)); string suffix=sign<0?"Front":"Back";
                AddVisual(root,name+suffix+"Rim",Torus(face,Vector3.forward,radius*.82f,.0014f,72,8),bright);
                AddVisual(root,name+suffix+"Inset",Torus(face+Vector3.forward*sign*.0003f,Vector3.forward,radius*.64f,.00065f,72,6),dark);
                AddVisual(root,name+suffix+"Boss",Coin(face,radius*.19f,.0013f),bright);
                for(int leaf=0;leaf<4;leaf++)
                {
                    float angle=leaf*Mathf.PI*.5f;Vector3 p=face+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius*.38f;
                    AddVisual(root,name+suffix+"Petal"+leaf,Torus(p+Vector3.forward*sign*.0009f,Vector3.forward,radius*.17f,.00075f,32,6),bright);
                }
            }
        }
        static Mesh Coin(Vector3 center,float radius,float halfDepth)
        {
            Vector2[] profile={new Vector2(0,-halfDepth),new Vector2(radius*.82f,-halfDepth),new Vector2(radius,-halfDepth*.58f),new Vector2(radius,halfDepth*.58f),new Vector2(radius*.82f,halfDepth),new Vector2(0,halfDepth)};
            var vertices=new List<Vector3>();var indices=new List<int>();const int steps=72;
            for(int r=0;r<profile.Length;r++)for(int i=0;i<=steps;i++){float a=i*Mathf.PI*2/steps;vertices.Add(center+new Vector3(Mathf.Cos(a)*profile[r].x,Mathf.Sin(a)*profile[r].x,profile[r].y));}
            for(int r=0;r<profile.Length-1;r++)for(int i=0;i<steps;i++){int a=r*(steps+1)+i,b=a+steps+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            return GeneratedMesh("BeveledRoundCoin",vertices,indices);
        }
        static Mesh Torus(Vector3 center,Vector3 normal,float radius,float tube,int segments,int sides)
        {
            Vector3 u=Vector3.Cross(normal,Mathf.Abs(normal.y)>.8f?Vector3.forward:Vector3.up).normalized,v=Vector3.Cross(normal,u).normalized;
            var vertices=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;Vector3 radial=u*Mathf.Cos(a)+v*Mathf.Sin(a);for(int j=0;j<=sides;j++){float b=j*Mathf.PI*2/sides;vertices.Add(center+radial*(radius+Mathf.Cos(b)*tube)+normal*Mathf.Sin(b)*tube);}}
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            return GeneratedMesh("FineGoldRing",vertices,indices);
        }
        static Mesh GeneratedMesh(string name,List<Vector3> vertices,List<int> triangles)
        { var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh; }
        static Bounds RendererBounds(GameObject root)
        { Renderer[] renderers=root.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("No sword renderers.");Bounds b=renderers[0].bounds;foreach(Renderer r in renderers)b.Encapsulate(r.bounds);return b; }
        static int TriangleCount(GameObject root)
        { int n=0;foreach(MeshFilter f in root.GetComponentsInChildren<MeshFilter>())for(int i=0;i<f.sharedMesh.subMeshCount;i++)n+=(int)f.sharedMesh.GetIndexCount(i)/3;return n; }
        static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1)); }
    }
}
