using System.Collections.Generic;
using ShadeLink.Gallery;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static class MuseumPedestalPlacement
    {
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial/MuseumPedestal";
        const string VisualName = "Museum pedestal 02";

        [MenuItem("Parallax/Replace tutorial table with museum pedestal")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play mode first."); return; }
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "KeyShadowPreview") { Debug.LogError("Open KeyShadowPreview first."); return; }
            var table = GameObject.Find("Starting object · ㅠ table");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MuseumPedestal02.fbx");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/MuseumPlinthe02_t.jpeg");
            if (model == null || texture == null) { Debug.LogError("Pedestal model or texture is missing."); return; }
            if (table == null) { PlaceStandalone(scene, model, texture); return; }
            if (table.transform.Find(VisualName) != null) { Debug.Log("Museum pedestal is already present."); return; }
            var tabletop = table.transform.Find("Long wooden tabletop");
            var surface = tabletop != null ? tabletop.GetComponent<Renderer>() : null;
            var collider = table.GetComponent<BoxCollider>();
            var prop = table.GetComponent<GalleryProp>();
            if (surface == null || collider == null || prop == null) { Debug.LogError("Table components are incomplete."); return; }
            if (Vector3.Distance(table.transform.lossyScale, Vector3.one) > .001f) { Debug.LogError("Table scale needs manual review before placement."); return; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/MuseumPedestal02.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "Museum Pedestal 02", mainTexture = texture };
                material.SetFloat("_Glossiness", .28f);
                AssetDatabase.CreateAsset(material, Folder + "/MuseumPedestal02.mat");
            }
            Vector3 surfaceCenter = surface.bounds.center;
            float baseY = table.transform.position.y;
            var children = new List<GameObject>();
            foreach (Transform child in table.transform) children.Add(child.gameObject);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace tutorial table with museum pedestal");
            Undo.RecordObject(table.transform, "Move pedestal root");
            Undo.RecordObject(collider, "Fit pedestal collider");
            Undo.RecordObject(prop, "Update pedestal prop");
            foreach (var child in children) { Undo.RecordObject(child, "Hide table parts"); child.SetActive(false); }
            table.transform.position = new Vector3(surfaceCenter.x, baseY + .575f, surfaceCenter.z);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            Undo.RegisterCreatedObjectUndo(visual, "Add museum pedestal");
            visual.name = VisualName;
            visual.transform.SetParent(table.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Undo.RevertAllDownToGroup(group); Debug.LogError("Pedestal has no renderer."); return; }
            Bounds bounds = LocalBounds(table.transform, renderers);
            if (bounds.size.y < .001f) { Undo.RevertAllDownToGroup(group); Debug.LogError("Pedestal has invalid bounds."); return; }
            visual.transform.localScale *= 1.1f / bounds.size.y;
            bounds = LocalBounds(table.transform, renderers);
            visual.transform.localPosition -= bounds.center;
            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                r.sharedMaterials = materials;
            }
            collider.center = Vector3.zero;
            collider.size = bounds.size;
            prop.title = "Museum pedestal";
            prop.Remember();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = table;
            Undo.CollapseUndoOperations(group);
            Debug.Log("Museum pedestal placed. Save the scene when ready.", table);
        }

        static void PlaceStandalone(Scene scene, GameObject model, Texture2D texture)
        {
            if (GameObject.Find(VisualName) != null) { Debug.Log("Museum pedestal is already present."); return; }
            var controller = Object.FindFirstObjectByType<KeyShadowTutorialController>();
            if (controller == null) { Debug.LogError("Tutorial controller is missing."); return; }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/MuseumPedestal02.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "Museum Pedestal 02", mainTexture = texture };
                material.SetFloat("_Glossiness", .28f);
                AssetDatabase.CreateAsset(material, Folder + "/MuseumPedestal02.mat");
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add museum pedestal");
            var root = new GameObject("Museum pedestal · movable");
            Undo.RegisterCreatedObjectUndo(root, "Add museum pedestal");
            root.transform.position = new Vector3(-2.443f, .575f, -1.575f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            Undo.RegisterCreatedObjectUndo(visual, "Add museum pedestal visual");
            visual.name = VisualName;
            visual.transform.SetParent(root.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Undo.RevertAllDownToGroup(group); Debug.LogError("Pedestal has no renderer."); return; }
            Bounds bounds = LocalBounds(root.transform, renderers);
            if (bounds.size.y < .001f) { Undo.RevertAllDownToGroup(group); Debug.LogError("Pedestal has invalid bounds."); return; }
            visual.transform.localScale *= 1.1f / bounds.size.y;
            bounds = LocalBounds(root.transform, renderers);
            visual.transform.localPosition -= bounds.center;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            var collider = Undo.AddComponent<BoxCollider>(root);
            collider.center = Vector3.zero;
            collider.size = bounds.size;
            var prop = Undo.AddComponent<GalleryProp>(root);
            prop.title = "Museum pedestal";
            prop.Remember();
            prop.InitializePhysics();
            Undo.RecordObject(controller, "Register museum pedestal");
            var serialized = new SerializedObject(controller);
            var props = serialized.FindProperty("props");
            int index = props.arraySize;
            props.InsertArrayElementAtIndex(index);
            props.GetArrayElementAtIndex(index).objectReferenceValue = prop;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);
            Debug.Log("Museum pedestal placed. Save the scene when ready.", root);
        }

        static Bounds LocalBounds(Transform root, Renderer[] renderers)
        {
            Bounds result = default;
            bool first = true;
            foreach (var renderer in renderers)
            {
                Bounds b = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    Vector3 point = root.InverseTransformPoint(renderer.transform.TransformPoint(b.center + Vector3.Scale(b.extents, sign)));
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }
    }
}
