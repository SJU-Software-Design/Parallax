using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Editor
{
    /// <summary>One-time conversion of the cleaned external CC BY 4.0 scan.</summary>
    public static class GalleryCathedralImport
    {
        const string Folder = "Assets/Parallax/Gallery/Resources/MuseumScans/Cathedral";
        const string MeshPath = Folder + "/CathedralMesh.asset";
        const string PortalPath = Folder + "/CathedralPortalMesh.asset";
        const string CollisionPath = Folder + "/CathedralCollision.asset";

        public static void Ensure()
        {
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var portal = AssetDatabase.LoadAssetAtPath<Mesh>(PortalPath);
            if (body != null && portal != null && body.vertexCount == 531273 &&
                body.blendShapeCount == 1 && body.GetBlendShapeName(0) == "Open" &&
                portal.vertexCount == 98 && portal.blendShapeCount == 1)
            {
                if (new FileInfo(MeshPath).Length >= 95000000) ForceBinaryAssets();
                Debug.Log("CATHEDRAL_SCAN_REUSE bodyTriangles=754102 portalTriangles=96");
                return;
            }
            Run();
        }

        public static void Run()
        {
            // Changing this setting refreshes native assets. Do it before any
            // transient Mesh is constructed, then restore it after persistence.
            SerializationMode prior = EditorSettings.serializationMode;
            try
            {
                EditorSettings.serializationMode = SerializationMode.ForceBinary;
                Import();
            }
            finally { EditorSettings.serializationMode = prior; }
            ForceBinaryAssets();
        }

        public static void ForceBinaryAssets()
        {
            // The project intentionally uses ForceText. A later queued save can
            // override a temporary ForceBinary setting, so serialize these new
            // meshes explicitly after all normal saves have completed.
            AssetDatabase.SaveAssets();
            foreach (string path in new[] { MeshPath, PortalPath, CollisionPath })
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Require(mesh != null, "Cannot serialize missing native asset: " + path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
                EditorUtility.ClearDirty(mesh);
                // The untracked serializer only accepts transient objects.
                // Instantiate preserves every source vertex and blendshape;
                // the .meta GUID remains owned by the existing asset path.
                Mesh transient = UnityEngine.Object.Instantiate(mesh);
                transient.name = mesh.name;
                transient.hideFlags = HideFlags.DontUnloadUnusedAsset;
                try
                {
                    UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
                        new UnityEngine.Object[] { transient }, path, false);
                }
                finally { UnityEngine.Object.DestroyImmediate(transient); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Mesh reloaded = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Require(reloaded != null, "Explicit binary asset did not reload: " + path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reloaded, out string reloadedGuid, out long reloadedId);
                Require(guid == reloadedGuid, "Binary conversion changed the native asset GUID: " + path);
                Debug.Log("CATHEDRAL_BINARY_IDENTITY path=" + path + " guid=" + reloadedGuid +
                    " previousLocalId=" + localId + " binaryLocalId=" + reloadedId);
            }
            VerifyNativeAssets();
        }

        public static void VerifyNativeAssets()
        {
            VerifyBinary(MeshPath, 531273, 754102, 1);
            VerifyBinary(PortalPath, 98, 96, 1);
            VerifyBinary(CollisionPath, 181425, 365448, 0);
            Mesh body = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var delta = new Vector3[body.vertexCount];
            body.GetBlendShapeFrameVertices(0, 0, delta, null, null);
            float maximum = 0;
            foreach (Vector3 value in delta) maximum = Mathf.Max(maximum, value.magnitude);
            Require(Mathf.Abs(maximum - .08f) < .00001f, "The opening morph was lost or changed");
            Require(Resources.Load<Mesh>("MuseumScans/Cathedral/CathedralMesh") != null &&
                Resources.Load<Mesh>("MuseumScans/Cathedral/CathedralPortalMesh") != null,
                "Binary meshes cannot be loaded through their runtime resource keys");
            Debug.Log("CATHEDRAL_BINARY_RESTART_VERIFY_OK bodyVertices=531273 bodyTriangles=754102" +
                " morphMax=.08 portalVertices=98 portalTriangles=96 sourceDetailPreserved=true");
        }

        static void VerifyBinary(string path, int vertices, int triangles, int blends)
        {
            var file = new FileInfo(path);
            Require(file.Exists && file.Length < 95000000, "Native asset exceeds the 95MB safety limit: " + path);
            using (var stream = file.OpenRead())
            {
                byte[] header = new byte[5]; stream.Read(header, 0, header.Length);
                Require(Encoding.ASCII.GetString(header) != "%YAML", "Native asset reverted to text serialization: " + path);
            }
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(mesh != null && mesh.vertexCount == vertices && mesh.GetIndexCount(0) / 3 == triangles &&
                mesh.blendShapeCount == blends, "Binary mesh geometry or morph differs: " + path);
            Debug.Log("CATHEDRAL_BINARY_ASSET_OK path=" + path + " bytes=" + file.Length +
                " vertices=" + vertices + " triangles=" + triangles + " blends=" + blends);
        }

        static void Import()
        {
            string input = Argument("-cathedralData");
            if (string.IsNullOrEmpty(input))
                input = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../../../artifacts/museum-exit-assets-20261104/review/cathedral-processed.bin"));
            Require(File.Exists(input), "Processed source is missing: " + input +
                ". Normal checkouts use the committed native mesh assets; regeneration requires the scan conversion tool.");

            Mesh body, collision, portal;
            using (var stream = File.OpenRead(input))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                Require(Encoding.ASCII.GetString(reader.ReadBytes(8)) == "PARXCAT1", "Wrong processed scan header");
                int vertices = Count(reader, 1000000), indices = Count(reader, 4000000);
                int collisionVertices = Count(reader, 500000), collisionIndices = Count(reader, 1500000);
                Vector3[] positions = Points(reader, vertices), normals = Points(reader, vertices);
                Vector2[] uv = UV(reader, vertices);
                int[] triangles = Indices(reader, indices, vertices);
                Vector3[] delta = Points(reader, vertices), deltaNormals = Points(reader, vertices);
                body = new Mesh { name = "La Cathedrale - Migg scan - authentic rest and opening morph", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontUnloadUnusedAsset };
                body.vertices = positions; body.normals = normals; body.uv = uv;
                body.SetTriangles(triangles, 0, false);
                body.AddBlendShapeFrame("Open", 100, delta, deltaNormals, null);
                body.RecalculateBounds();
                Bounds animatedBounds = body.bounds;
                for (int i = 0; i < positions.Length; i++) animatedBounds.Encapsulate(positions[i] + delta[i]);
                body.bounds = animatedBounds;

                collision = new Mesh { name = "La Cathedrale - open pose collision derivative", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontUnloadUnusedAsset };
                collision.vertices = Points(reader, collisionVertices);
                collision.SetTriangles(Indices(reader, collisionIndices, collisionVertices), 0, false);
                collision.RecalculateBounds();

                int portalVertices = Count(reader, 2000), portalIndices = Count(reader, 6000);
                portal = new Mesh { name = "La Cathedrale - measured hand aperture with opening morph", indexFormat = IndexFormat.UInt16, hideFlags = HideFlags.DontUnloadUnusedAsset };
                portal.vertices = Points(reader, portalVertices);
                portal.uv = UV(reader, portalVertices);
                portal.SetTriangles(Indices(reader, portalIndices, portalVertices), 0, false);
                Vector3[] portalDelta = Points(reader, portalVertices);
                portal.RecalculateNormals();
                portal.AddBlendShapeFrame("Open", 100, portalDelta, null, null);
                portal.RecalculateBounds();
                Bounds portalBounds = portal.bounds;
                Vector3[] portalRest = portal.vertices;
                for (int i = 0; i < portalRest.Length; i++) portalBounds.Encapsulate(portalRest[i] + portalDelta[i]);
                portal.bounds = portalBounds;
                Require(stream.Position == stream.Length, "Unexpected trailing or missing processed scan data");
            }

            Require(body.vertexCount == 531273 && body.GetIndexCount(0) / 3 == 754102, "Unexpected cleaned body geometry");
            Require(Mathf.Abs(body.bounds.min.y) < .00001f && Mathf.Abs(body.bounds.max.y - 2) < .00001f,
                "Sculpture is not normalized to height two with base at zero");
            Require(collision.GetIndexCount(0) / 3 < 500000, "Collision derivative exceeds its cooking budget");
            Require(portal.vertexCount == 98 && portal.GetIndexCount(0) / 3 == 96, "Wrong measured aperture topology");

            // A blendshape text serialization is needlessly large. Only these
            // three new native assets are saved in binary; project settings are
            // restored immediately and no other asset is forced to reserialize.
            AssetDatabase.StartAssetEditing();
            try
            {
                Save(body, MeshPath); Save(portal, PortalPath); Save(collision, CollisionPath);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            var texture = AssetImporter.GetAtPath(Folder + "/CathedralAlbedo.jpg") as TextureImporter;
            if (texture != null)
            {
                texture.textureType = TextureImporterType.Default;
                texture.sRGBTexture = true; texture.mipmapEnabled = true;
                texture.isReadable = false; texture.maxTextureSize = 1024;
                texture.filterMode = FilterMode.Bilinear; texture.anisoLevel = 4;
                texture.textureCompression = TextureImporterCompression.CompressedHQ;
                texture.SaveAndReimport();
            }
            Debug.Log("CATHEDRAL_SCAN_IMPORT_OK vertices=531273 triangles=754102 blendShapes=1" +
                " collisionTriangles=365448 portalTriangles=96 source=" + input);
        }

        static void Save(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); }
            existing.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
        }

        static int Count(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            Require(count > 0 && count <= maximum, "Processed array count is outside its expected range");
            return count;
        }
        static float Number(BinaryReader reader)
        {
            float value = reader.ReadSingle();
            Require(!float.IsNaN(value) && !float.IsInfinity(value), "Processed source has a non-finite coordinate");
            return value;
        }
        static Vector3[] Points(BinaryReader reader, int count)
        {
            var values = new Vector3[count];
            for (int i = 0; i < count; i++) values[i] = new Vector3(Number(reader), Number(reader), Number(reader));
            return values;
        }
        static Vector2[] UV(BinaryReader reader, int count)
        {
            var values = new Vector2[count];
            for (int i = 0; i < count; i++) values[i] = new Vector2(Number(reader), Number(reader));
            return values;
        }
        static int[] Indices(BinaryReader reader, int count, int vertices)
        {
            Require(count % 3 == 0, "Triangle index count is not a multiple of three");
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadInt32();
                Require(values[i] >= 0 && values[i] < vertices, "Triangle index points outside its vertex array");
            }
            return values;
        }
        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("CATHEDRAL_SCAN: " + message); }
    }
}
