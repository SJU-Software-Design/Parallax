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
    // Separate CPU copies preserve original import flags, baked lighting and
    // saved-scene geometry. Runtime colliders cannot rely on a batched render mesh.
    public static class GalleryStaticExhibitCollisionAssets
    {
        const string Folder = "Assets/Parallax/Gallery/Resources/MuseumCollision";
        const string Scene = "Assets/Parallax/Gallery/Scenes/Gallery.unity";
        [Serializable] sealed class Entry
        { public string key, hierarchy, sourceAsset; public int vertices, submeshes; public bool sourceReadable; }
        [Serializable] sealed class Index
        { public string sourceScene = Scene; public Entry[] surfaces; }

        public static void PrepareSavedScene()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if (game == null) throw new InvalidOperationException("Saved museum has no GalleryGame.");
            int count = Prepare(game);
            Debug.Log("MUSEUM_STATIC_COLLISION_ASSETS_OK copies=" + count + " sceneAndImportersUnchanged=1");
        }

        public static int Prepare(GalleryGame game)
        {
            EnsureFolder(Folder);
            var entries = new List<Entry>();
            foreach (var filter in game.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!GalleryStaticExhibitCollision.Candidate(game, filter)) continue;
                Mesh source = filter.sharedMesh;
                string key = GalleryStaticExhibitCollision.ResourceKey(game, filter.transform);
                entries.Add(new Entry { key = key, hierarchy = GalleryStaticExhibitCollision.Path(game.transform, filter.transform),
                    sourceAsset = AssetDatabase.GetAssetPath(source), vertices = source.vertexCount,
                    submeshes = source.subMeshCount, sourceReadable = source.isReadable });
                Mesh copy = Copy(source); copy.name = key;
                string path = Folder + "/" + key + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(copy, path);
                else
                {
                    EditorUtility.CopySerialized(copy, existing);
                    UnityEngine.Object.DestroyImmediate(copy);
                    EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);
                }
            }
            string index = Folder + "/index.json";
            File.WriteAllText(index, JsonUtility.ToJson(new Index { surfaces = entries.ToArray() }, true));
            AssetDatabase.ImportAsset(index, ImportAssetOptions.ForceSynchronousImport);
            return entries.Count;
        }

        static Mesh Copy(Mesh source)
        {
            using (Mesh.MeshDataArray read = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData data = read[0];
                Mesh.MeshDataArray write = Mesh.AllocateWritableMeshData(1);
                var copy = new Mesh(); bool applied = false;
                try
                {
                    var target = write[0];
                    target.SetVertexBufferParams(data.vertexCount, source.GetVertexAttributes());
                    for (int stream = 0; stream < source.vertexBufferCount; stream++)
                        target.GetVertexData<byte>(stream).CopyFrom(data.GetVertexData<byte>(stream));
                    if (data.indexFormat == IndexFormat.UInt32)
                    {
                        var indices = data.GetIndexData<uint>(); target.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                        target.GetIndexData<uint>().CopyFrom(indices);
                    }
                    else
                    {
                        var indices = data.GetIndexData<ushort>(); target.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                        target.GetIndexData<ushort>().CopyFrom(indices);
                    }
                    target.subMeshCount = data.subMeshCount;
                    for (int sub = 0; sub < data.subMeshCount; sub++)
                        target.SetSubMesh(sub, data.GetSubMesh(sub), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    Mesh.ApplyAndDisposeWritableMeshData(write, copy,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    applied = true; copy.bounds = source.bounds; copy.UploadMeshData(false); return copy;
                }
                catch { UnityEngine.Object.DestroyImmediate(copy); throw; }
                finally { if (!applied) write.Dispose(); }
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
