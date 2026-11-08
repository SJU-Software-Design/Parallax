using System;
using System.Linq;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    // Installs the downloaded CC-BY model as independent, hinged door leaves.
    public static class OldWoodenDoorSetup
    {
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial/OldWoodenDoor";
        [MenuItem("그늘 잇기/Turn wooden door around")]
        public static void TurnAround()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var root = GameObject.Find("Old wooden double door · fixed frame");
            var sequence = UnityEngine.Object.FindFirstObjectByType<KeyShadowSequence>();
            if (root == null || sequence == null) throw new InvalidOperationException("Wooden door or sequence is missing.");
            var serialized = new SerializedObject(sequence);
            var left = (Transform)serialized.FindProperty("doorPivot").objectReferenceValue;
            var right = (Transform)serialized.FindProperty("otherDoorPivot").objectReferenceValue;
            if (left == null || right == null) throw new InvalidOperationException("Door hinges are missing.");
            Undo.RecordObject(root.transform, "Turn wooden door around");
            root.transform.RotateAround((left.position + right.position) * .5f, Vector3.up, 180f);
            foreach (var pivot in new[] { left, right })
            {
                var child = pivot.GetChild(0);
                Quaternion visualRotation = child.rotation;
                Undo.RecordObject(pivot, "Turn wooden door around");
                Undo.RecordObject(child, "Turn wooden door around");
                pivot.rotation = Quaternion.identity;
                child.rotation = visualRotation;
            }
            serialized.FindProperty("doorPivot").objectReferenceValue = right;
            serialized.FindProperty("otherDoorPivot").objectReferenceValue = left;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        [MenuItem("그늘 잇기/Install old wooden double door")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before installing the door.");
            if (SceneManager.GetActiveScene().name != "KeyShadowPreview") throw new InvalidOperationException("Open KeyShadowPreview first.");
            var oldLeaf = GameObject.Find("Freestanding white door leaf · hinge");
            var oldFrame = GameObject.Find("White door frame · fixed");
            var sequence = UnityEngine.Object.FindFirstObjectByType<KeyShadowSequence>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/DoorAnimated.fbx");
            if (oldLeaf == null || oldFrame == null || sequence == null || prefab == null)
                throw new InvalidOperationException("The original door, puzzle sequence or downloaded model was not found.");
            var source = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var renderers = source.GetComponentsInChildren<Renderer>();
                var left = renderers.FirstOrDefault(r => r.name == "Door left") as SkinnedMeshRenderer;
                var right = renderers.FirstOrDefault(r => r.name == "Door Right") as SkinnedMeshRenderer;
                var frame = renderers.FirstOrDefault(r => r.name == "Jamb" && r is MeshRenderer);
                if (left == null || right == null || frame == null) throw new InvalidOperationException("The FBX door parts are missing.");
                Bounds bounds = frame.bounds; bounds.Encapsulate(left.bounds); bounds.Encapsulate(right.bounds);
                if (bounds.size.z < .01f || bounds.size.y < .01f) throw new InvalidOperationException("Invalid imported model dimensions.");
                float sx = 2.74f / bounds.size.z, sy = 6.44f / bounds.size.y;
                string materialPath = Folder + "/OldWoodenDoor.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard")) { name = "Old wooden door · Ellie3D" };
                    material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Door_BaseColor.png");
                    material.SetFloat("_Metallic", .12f); material.SetFloat("_Glossiness", .3f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var parent = new GameObject("Old wooden double door · fixed frame");
                Undo.RegisterCreatedObjectUndo(parent, "Install wooden door");
                var leftPivot = new GameObject("Old wooden door · left hinge").transform;
                var rightPivot = new GameObject("Old wooden door · right hinge").transform;
                leftPivot.SetParent(parent.transform); rightPivot.SetParent(parent.transform);
                leftPivot.position = new Vector3(-1.37f, 0, 5.7f); rightPivot.position = new Vector3(1.37f, 0, 5.7f);
                AddPart(frame, parent.transform, bounds, sx, sy, material, "Frame", false);
                AddPart(left, left.bounds.center.z < right.bounds.center.z ? leftPivot : rightPivot, bounds, sx, sy, material, "Leaf A", true);
                AddPart(right, right.bounds.center.z < left.bounds.center.z ? leftPivot : rightPivot, bounds, sx, sy, material, "Leaf B", true);
                var serialized = new SerializedObject(sequence);
                serialized.FindProperty("doorPivot").objectReferenceValue = leftPivot;
                serialized.FindProperty("otherDoorPivot").objectReferenceValue = rightPivot;
                serialized.FindProperty("lockPosition").vector3Value = new Vector3(0f, 2.67f, 5.56f);
                serialized.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(oldLeaf); Undo.DestroyObjectImmediate(oldFrame);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = parent;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        static void AddPart(Renderer source, Transform parent, Bounds bounds, float sx, float sy, Material material, string name, bool collision)
        {
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); }
            else mesh = UnityEngine.Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = source.transform.TransformPoint(vertices[i]);
                Vector3 world = new Vector3((p.z - bounds.center.z) * sx, (p.y - bounds.min.y) * sy, 5.7f - p.x * sx);
                vertices[i] = world - parent.position;
            }
            mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            string path = Folder + "/Door " + name + ".asset";
            var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (previous == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, previous); UnityEngine.Object.DestroyImmediate(mesh); mesh = previous; }
            var part = new GameObject("Old wooden " + name); part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) { var box = part.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
        }
    }
}
