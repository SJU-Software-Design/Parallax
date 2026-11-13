using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ShadeLink.Gallery;

namespace ShadeLink.Editor
{
    // Copies only meshes crossing the insertion plane. Source assets/importers
    // and the saved Gallery scene are never modified. MeshUtility's editor-only
    // API explicitly supports meshes whose read/write flag is disabled:
    // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html
    public static class GalleryBottleBalanceReadableArchitecture
    {
        const string ScenePath = "Assets/Parallax/Gallery/Scenes/Gallery.unity";
        const string AssetFolder = "Assets/Parallax/Gallery/Resources/BottleBalance/ReadableArchitecture";
        const string IndexPath = "Assets/Parallax/Gallery/Resources/BottleBalance/ReadableArchitectureIndex.json";

        [Serializable] sealed class Index
        { public string sourceScene = ScenePath; public Entry[] entries; }
        [Serializable] sealed class Entry
        {
            public string key, identity, sourceAssetPath, sourceMeshName;
            public int sourceVertices, sourceSubmeshes;
            public bool sourceReadable;
            public Bounds sourceBounds;
        }

        public static void PrepareSavedScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GalleryGame game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if (game == null) throw new InvalidOperationException("Saved Gallery has no GalleryGame.");
            int count = Prepare(game);
            Debug.Log("BOTTLE_BALANCE_READABLE_ARCHITECTURE_OK copies=" + count + " sourceAssetsUnchanged=1");
        }

        public static int Prepare(GalleryGame game)
        {
            EnsureFolder(AssetFolder);
            var keys = new HashSet<string>();
            var entries = new List<Entry>();
            foreach (MeshFilter filter in game.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                Renderer renderer = filter.GetComponent<Renderer>();
                if (source == null || renderer == null || !Crosses(renderer.bounds)) continue;
                Copy(filter.transform, source, keys, entries);
            }
            foreach (MeshCollider collider in game.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == null || !Crosses(collider.bounds)) continue;
                Copy(collider.transform, collider.sharedMesh, keys, entries);
            }
            File.WriteAllText(IndexPath, JsonUtility.ToJson(new Index { entries = entries.ToArray() }, true));
            AssetDatabase.ImportAsset(IndexPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("BOTTLE_BALANCE_READABLE_INDEX " + IndexPath);
            return keys.Count;
        }

        static bool Crosses(Bounds bounds)
        {
            float at = GalleryBottleBalanceLayout.InsertAt;
            return bounds.min.z <= at + .002f && bounds.max.z >= at - .002f;
        }

        static void Copy(Transform owner, Mesh source, HashSet<string> keys, List<Entry> entries)
        {
            string key = GalleryBottleBalanceLayout.ReadableResourceKey(owner, source);
            if (!keys.Add(key)) return;
            entries.Add(new Entry
            {
                key = key, identity = GalleryBottleBalanceLayout.ReadableResourceIdentity(owner),
                sourceAssetPath = AssetDatabase.GetAssetPath(source), sourceMeshName = source.name,
                sourceVertices = source.vertexCount, sourceSubmeshes = source.subMeshCount,
                sourceReadable = source.isReadable, sourceBounds = source.bounds
            });
            Mesh copy = CpuCopy(source);
            copy.name = key;
            string assetPath = AssetFolder + "/" + key + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null) AssetDatabase.CreateAsset(copy, assetPath);
            else
            {
                EditorUtility.CopySerialized(copy, existing);
                UnityEngine.Object.DestroyImmediate(copy);
                copy = existing;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            if (!copy.isReadable || copy.vertexCount != source.vertexCount || copy.subMeshCount != source.subMeshCount)
                throw new InvalidOperationException("Readable copy does not preserve source mesh metadata: " + owner.name);
        }

        static Mesh CpuCopy(Mesh source)
        {
            using (Mesh.MeshDataArray read = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData data = read[0];
                Mesh.MeshDataArray write = Mesh.AllocateWritableMeshData(1);
                bool applied = false;
                var copy = new Mesh();
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
                    Mesh.ApplyAndDisposeWritableMeshData(write, copy,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    applied = true;
                    copy.bounds = source.bounds;
                    copy.UploadMeshData(false);
                    return copy;
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    throw;
                }
                finally { if (!applied) write.Dispose(); }
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
