using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadeLink.Gallery;
using ShadeLink.Library;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    /// <summary>Builds a script-free preview from the same original shelf, book and table meshes used by Library.unity.</summary>
    public static class GalleryPortalLibraryAssets
    {
        const string DirectoryPath = "Assets/Parallax/Gallery/Resources/MuseumPortal/";
        const string PrefabPath = DirectoryPath + "LibraryPreview.prefab";
        sealed class Piece
        {
            public Mesh mesh;
            public int subMesh;
            public Matrix4x4 matrix;
            public Material material;
        }
        static readonly List<Piece> pieces = new List<Piece>();
        static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        static readonly List<Material> temporary = new List<Material>();

        [MenuItem("Parallax/Prepare cathedral library preview")]
        public static void Prepare()
        {
            if (!File.Exists(LibraryBuild.ScenePath))
                throw new InvalidOperationException("Prepare the actual Library scene before extracting its cathedral preview.");
            Shader shader = Shader.Find("Parallax/MuseumLibraryPreviewSurface");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The warm library-preview shader did not compile.");
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            pieces.Clear(); converted.Clear(); temporary.Clear();
            Scene previous = SceneManager.GetActiveScene();
            Scene sourceScene = SceneManager.GetSceneByPath(LibraryBuild.ScenePath);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(LibraryBuild.ScenePath, OpenSceneMode.Additive);
            Scene working = default;
            GameObject output = null;
            try
            {
                LibraryGame library = sourceScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<LibraryGame>(true)).FirstOrDefault();
                if (library == null) throw new InvalidOperationException("The actual Library scene has no LibraryGame collection to inspect.");
                LibraryProp shelf = Required(library, "reading-shelf-west");
                LibraryProp[] books = library.props.Where(p => p.objectId.StartsWith("reading-shelf-west-book-", StringComparison.Ordinal)).OrderBy(p => p.objectId).ToArray();
                if (books.Length != 12) throw new InvalidOperationException("The source library's twelve independent shelf books are required.");

                // Use a temporary scene for the bake. The Library scene and its control components are never edited or serialized into this prefab.
                // A preview scene is independent of the editor's unsaved/untitled working scene.
                // NewScene(Additive) refuses that common state and must never be worked around by saving or discarding the user's scene.
                working = EditorSceneManager.NewPreviewScene();
                Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
                Mesh sphere = PrimitiveMesh(PrimitiveType.Sphere);
                Material oak = library.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == "Rinku - structural oak");
                if (oak == null) throw new InvalidOperationException("The actual library's original oak atlas was not integrated.");
                Material plaster = library.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == "Warm library plaster");
                if (plaster == null) throw new InvalidOperationException("The actual library's plaster material is missing.");

                // The sculptural window looks into a compact reading alcove. Every bookcase and loose book is an instance of the actual destination meshes.
                foreach (float depth in new[] { 3.6f, 6.4f, 9.2f })
                {
                    AddShelf(shelf, books, new Vector3(-2.65f, 1.5f, depth), Quaternion.Euler(0, -90, 0), shader);
                    AddShelf(shelf, books, new Vector3(2.65f, 1.5f, depth), Quaternion.Euler(0, 90, 0), shader);
                }
                AddShelf(shelf, books, new Vector3(-1.2f, 1.5f, 11.25f), Quaternion.identity, shader);
                AddShelf(shelf, books, new Vector3(1.2f, 1.5f, 11.25f), Quaternion.identity, shader);

                AddProp(Required(library, "table-reading"), new Vector3(0, .369f, 7.9f), Quaternion.identity, Vector3.one * .82f, shader);
                AddProp(Required(library, "chair-a"), new Vector3(.25f, .495f, 9.45f), Quaternion.Euler(0, 180, 0), Vector3.one * .9f, shader);
                AddProp(Required(library, "reading-book-a"), new Vector3(-.35f, .777f, 7.8f), Quaternion.Euler(90, 0, 0), Vector3.one * .65f, shader);
                AddProp(Required(library, "reading-book-b"), new Vector3(.15f, .777f, 8.15f), Quaternion.Euler(90, 20, 0), Vector3.one * .65f, shader);

                Material timber = Convert(oak, shader);
                Material wall = Convert(plaster, shader);
                Material rug = Plain("Portal · oxblood runner", new Color(.19f, .075f, .034f), shader);
                Material brass = Plain("Portal · aged lamp brass", new Color(.46f, .29f, .10f), shader);
                Material lamp = Plain("Portal · warm opal lamps", new Color(.82f, .55f, .24f), shader);
                lamp.SetColor("_Emission", new Color(1.3f, .64f, .18f));
                SaveMaterial(lamp);
                Add(cube, 0, Matrix4x4.TRS(new Vector3(0, -.14f, 6.1f), Quaternion.identity, new Vector3(6.6f, .28f, 12.2f)), timber);
                Add(cube, 0, Matrix4x4.TRS(new Vector3(0, 3.94f, 6.1f), Quaternion.identity, new Vector3(6.6f, .22f, 12.2f)), timber);
                Add(cube, 0, Matrix4x4.TRS(new Vector3(0, .007f, 6), Quaternion.identity, new Vector3(2.15f, .014f, 11.8f)), rug);
                for (int side = -1; side <= 1; side += 2)
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(side * 3.25f, 1.9f, 6.1f), Quaternion.identity, new Vector3(.20f, 3.8f, 12.2f)), wall);
                Add(cube, 0, Matrix4x4.TRS(new Vector3(0, 1.9f, 12.08f), Quaternion.identity, new Vector3(6.6f, 3.8f, .20f)), wall);
                foreach (float depth in new[] { .85f, 3.6f, 7.2f, 10.6f })
                {
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(0, 3.72f, depth), Quaternion.identity, new Vector3(6.5f, .18f, .20f)), timber);
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(0, 3.52f, depth), Quaternion.identity, new Vector3(.035f, .36f, .035f)), brass);
                    Add(sphere, 0, Matrix4x4.TRS(new Vector3(0, 3.31f, depth), Quaternion.identity, new Vector3(.34f, .20f, .34f)), lamp);
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(0, 3.18f, depth), Quaternion.identity, new Vector3(.39f, .025f, .39f)), brass);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(side * 3.1f, .12f, 6), Quaternion.identity, new Vector3(.14f, .24f, 12)), timber);
                    Add(cube, 0, Matrix4x4.TRS(new Vector3(side * 3.1f, 3.56f, 6), Quaternion.identity, new Vector3(.14f, .18f, 12)), timber);
                }

                output = new GameObject("Library preview · original destination furniture, no gameplay components");
                SceneManager.MoveGameObjectToScene(output, working);
                int triangleCount = 0, groupIndex = 0;
                foreach (var group in pieces.GroupBy(p => p.material).OrderBy(g => g.Key.name, StringComparer.Ordinal))
                {
                    var combine = group.Select(p => new CombineInstance { mesh = p.mesh, subMeshIndex = p.subMesh, transform = p.matrix }).ToArray();
                    var mesh = new Mesh { name = "Library preview · " + group.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(combine, true, true, false); mesh.RecalculateBounds();
                    triangleCount += mesh.triangles.Length / 3;
                    string path = DirectoryPath + "LibraryPreviewMesh_" + groupIndex.ToString("D2") + ".asset";
                    Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (stored == null) { AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
                    else { EditorUtility.CopySerialized(mesh, stored); EditorUtility.SetDirty(stored); UnityEngine.Object.DestroyImmediate(mesh); }
                    var go = new GameObject("Original library surfaces · " + group.Key.name);
                    go.transform.SetParent(output.transform, false); go.layer = GalleryCathedralPortal.PreviewLayer;
                    go.AddComponent<MeshFilter>().sharedMesh = stored;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    groupIndex++;
                }
                if (groupIndex < 4 || triangleCount < 1000) throw new InvalidOperationException("Library preview extraction produced too little original geometry.");
                if (output.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || output.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("The library preview must contain renderers only.");
                output.layer = GalleryCathedralPortal.PreviewLayer;
                PrefabUtility.SaveAsPrefabAsset(output, PrefabPath);
                File.WriteAllText(DirectoryPath + "LibraryPreview-source.json",
                    "{\n  \"sourceScene\": \"" + LibraryBuild.ScenePath + "\",\n" +
                    "  \"sourceKit\": \"Medieval library kit by Rinku Choudhary\",\n" +
                    "  \"sourceUrl\": \"https://sketchfab.com/3d-models/medieval-library-kit-c018093ba87d45f28f30ec8acf2f6604\",\n" +
                    "  \"description\": \"Render-only reading alcove assembled from the actual destination's carved shelf, separate books, table, chair and oak texture. Geometry has been combined for one-camera rendering. No LibraryGame, player, colliders, AudioListener or lighting globals are included.\",\n" +
                    "  \"meshGroups\": " + groupIndex + ",\n  \"triangles\": " + triangleCount + "\n}\n");
                AssetDatabase.SaveAssets();
                Debug.Log("CATHEDRAL_LIBRARY_PREVIEW_OK groups=" + groupIndex + " triangles=" + triangleCount);
            }
            finally
            {
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                foreach (var material in temporary) if (material != null && !AssetDatabase.Contains(material)) UnityEngine.Object.DestroyImmediate(material);
                if (working.IsValid() && working.isLoaded) EditorSceneManager.ClosePreviewScene(working);
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                pieces.Clear(); converted.Clear(); temporary.Clear();
            }
        }

        static LibraryProp Required(LibraryGame library, string id)
        {
            var prop = library.props.FirstOrDefault(p => p.objectId == id);
            if (prop == null) throw new InvalidOperationException("The actual library is missing " + id);
            return prop;
        }
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type); Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(go); return mesh;
        }
        static void AddShelf(LibraryProp shelf, LibraryProp[] books, Vector3 at, Quaternion rotation, Shader shader)
        {
            Matrix4x4 frame = Matrix4x4.TRS(at, rotation, Vector3.one);
            CopyRenderers(shelf, shelf.transform, frame, shader);
            foreach (var book in books) CopyRenderers(book, shelf.transform, frame, shader);
        }
        static void AddProp(LibraryProp prop, Vector3 at, Quaternion rotation, Vector3 scale, Shader shader)
        {
            CopyRenderers(prop, prop.transform, Matrix4x4.TRS(at, rotation, scale), shader);
        }
        static void CopyRenderers(LibraryProp prop, Transform sourceFrame, Matrix4x4 destination, Shader shader)
        {
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.name.IndexOf("seal", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Matrix4x4 matrix = destination * sourceFrame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var slots = renderer.sharedMaterials;
                for (int subMesh = 0; subMesh < filter.sharedMesh.subMeshCount; subMesh++)
                {
                    Material source = slots[Mathf.Min(subMesh, slots.Length - 1)];
                    if (source == null) throw new InvalidOperationException("An original library mesh has no material.");
                    Add(filter.sharedMesh, subMesh, matrix, Convert(source, shader));
                }
            }
        }
        static void Add(Mesh mesh, int subMesh, Matrix4x4 matrix, Material material)
        {
            pieces.Add(new Piece { mesh = mesh, subMesh = subMesh, matrix = matrix, material = material });
        }
        static Material Convert(Material source, Shader shader)
        {
            if (converted.TryGetValue(source, out var known)) return known;
            var material = new Material(shader) { name = "Portal · " + source.name };
            material.color = source.HasProperty("_Color") ? source.color * new Color(1, .96f, .86f, 1) : Color.white;
            if (source.HasProperty("_MainTex"))
            {
                material.mainTexture = source.mainTexture;
                material.mainTextureScale = source.mainTextureScale; material.mainTextureOffset = source.mainTextureOffset;
            }
            material = SaveMaterial(material); converted.Add(source, material); return material;
        }
        static Material Plain(string name, Color color, Shader shader)
        {
            return SaveMaterial(new Material(shader) { name = name, color = color });
        }
        static Material SaveMaterial(Material material)
        {
            string name = material.name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string path = DirectoryPath + name + ".mat";
            var stored = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (stored == null) { AssetDatabase.CreateAsset(material, path); return material; }
            if (stored == material) { EditorUtility.SetDirty(material); return material; }
            EditorUtility.CopySerialized(material, stored); EditorUtility.SetDirty(stored);
            UnityEngine.Object.DestroyImmediate(material); return stored;
        }
    }
}
