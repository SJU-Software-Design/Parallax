using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Gallery;
using ShadeLink.Tutorial;

namespace ShadeLink.Editor
{
    public static class ShadowKeyBranchPreviewImport
    {
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial/ShadowKeyBranch";
        const string Request = "Temp/ShadowKeyBranchPreview.request";
        const string ObjectName = "ShadowKey - Branch Sculpture Preview";

        [InitializeOnLoadMethod]
        static void ResumeRequest()
        {
            if (File.Exists(Request)) EditorApplication.delayCall += Import;
        }

        [MenuItem("Parallax/Add branch sculpture preview")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "KeyShadowPreview") return;
            var existing = GameObject.Find(ObjectName);
            if (existing != null) { Selection.activeGameObject = existing; return; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ShadowKey_Branch.fbx");
            if (model == null) { Debug.LogError("Branch FBX has not imported."); return; }
            var controller = Object.FindFirstObjectByType<KeyShadowTutorialController>();
            if (controller == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add ShadowKey branch sculpture");
            var root = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Add branch sculpture");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            visual.transform.SetParent(root.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = CalculateRootLocalBounds(root.transform, renderers);
            visual.transform.localPosition -= bounds.center;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/SilverMetal.mat");
            if (material == null)
            {
                throw new FileNotFoundException("Tutorial silver material is missing.", Folder + "/SilverMetal.mat");
            }
            foreach (var renderer in renderers) renderer.sharedMaterial = material;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = bounds.size;
            root.transform.localScale = Vector3.one * .5f;
            root.transform.SetPositionAndRotation(new Vector3(2.1f,bounds.size.y*.25f+.025f,-3.6f),
                Quaternion.Euler(0,45,0));
            var prop = root.AddComponent<GalleryProp>();
            prop.title = "ShadowKey branch sculpture";
            prop.minScale = .1f;
            prop.Remember();
            prop.InitializePhysics();
            Undo.RecordObject(controller, "Register branch sculpture for interaction");
            var serialized = new SerializedObject(controller);
            var props = serialized.FindProperty("props");
            var registeredProps = new System.Collections.Generic.List<Object>();
            for (int i = 0; i < props.arraySize; i++)
            {
                Object existingProp = props.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existingProp != null) registeredProps.Add(existingProp);
            }
            props.arraySize = registeredProps.Count + 1;
            for (int i = 0; i < registeredProps.Count; i++)
                props.GetArrayElementAtIndex(i).objectReferenceValue = registeredProps[i];
            props.GetArrayElementAtIndex(registeredProps.Count).objectReferenceValue = prop;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save preview scene.");
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(root.transform.position, Quaternion.Euler(14,215,0),3.7f);
            Undo.CollapseUndoOperations(group);
            File.WriteAllText("Temp/ShadowKeyBranchPreview.result", "OK\n" + scene.path + "\n" + root.transform.position +
                "\nsize=" + bounds.size * .5f + "\nprops=" + props.arraySize);
            if (File.Exists(Request)) File.Delete(Request);
            Debug.Log("ShadowKey branch sculpture added and saved in " + scene.path, root);
        }

        static Bounds CalculateRootLocalBounds(Transform root, Renderer[] renderers)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                Bounds localBounds = renderer.localBounds;
                Vector3 half = localBounds.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = localBounds.center + Vector3.Scale(half, new Vector3(
                        (i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1,
                        (i & 4) == 0 ? -1 : 1));
                    Vector3 rootPoint = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(rootPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(rootPoint);
                }
            }
            return bounds;
        }
    }
}
