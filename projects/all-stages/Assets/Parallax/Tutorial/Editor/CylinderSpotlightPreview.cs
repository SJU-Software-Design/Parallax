using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static class CylinderSpotlightPreview
    {
        const string ScenePath = "Assets/Parallax/Tutorial/Scenes/KeyShadowPreview.unity";
        const string ModelPath = "Assets/Parallax/Tutorial/Resources/Tutorial/CylinderSpotlight/CylinderSpotlightBlack.fbx";

        [MenuItem("Parallax/Install cylinder spotlight preview")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new System.Exception("Open KeyShadowPreview first.");
            if (scene.isDirty) throw new System.Exception("Save the scene before installing the spotlight.");
            var lightObject = GameObject.Find("Only light source · modern wall lamp on right");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (lightObject == null || prefab == null) throw new System.Exception("The existing light or spotlight model is missing.");
            if (GameObject.Find("Cylinder spotlight · visual") != null) throw new System.Exception("The preview already exists.");
            var root = new GameObject("Cylinder spotlight · visual");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.transform.SetParent(root.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new System.Exception("No spotlight mesh was imported.");
            var material = new Material(Shader.Find("Standard")) { color = new Color(.075f, .08f, .085f), name = "Spotlight · matte black" };
            AssetDatabase.CreateAsset(material, "Assets/Parallax/Tutorial/Resources/Tutorial/CylinderSpotlight/SpotlightBlack.mat");
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (max < .0001f) throw new System.Exception("The spotlight has empty bounds.");
            visual.transform.localScale *= .65f / max;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            visual.transform.position -= bounds.center;
            root.transform.position = lightObject.transform.position;
            foreach (var name in new[] { "Modern wall lamp · graphite housing", "Modern wall lamp · warm diffuser" })
            {
                var old = GameObject.Find(name);
                if (old != null) old.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Could not save the scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("CYLINDER_SPOTLIGHT_INSTALLED");
        }
    }
}
