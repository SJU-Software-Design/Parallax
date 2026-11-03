using System.IO;
using ShadeLink.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static class TutorialBuild
    {
        const string ScenePath = "Assets/Parallax/Tutorial/Scenes/Tutorial.unity";

        const string TablePath = "Assets/Parallax/Tutorial/Resources/Tutorial/WoodenPracticeTable.prefab";

        [MenuItem("Parallax/원근 튜토리얼 씬 준비")]
        public static void Prepare()
        {
            PrepareTable();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Perspective tutorial").AddComponent<TutorialGame>();
            var properties = new SerializedObject(game);
            properties.FindProperty("surfaceShader").objectReferenceValue = Shader.Find("Standard");
            properties.FindProperty("guideShader").objectReferenceValue = Shader.Find("Unlit/Color");
            properties.FindProperty("concreteShader").objectReferenceValue = Shader.Find("Parallax/Tutorial Concrete");
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        static void PrepareTable()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TablePath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Parallax/Library/Generated/OriginalKit/OpenWorktable_0_(2.2000,0.9000,1.7000).asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Parallax/Library/Generated/Stage/Rinku - table.mat");
            var table = new GameObject("Wooden practice table");
            try
            {
                var model = new GameObject("Rinku wooden worktable");
                model.transform.SetParent(table.transform, false);
                Bounds bounds = mesh.bounds;
                var scale = new Vector3(1.42f / bounds.size.x, TutorialGame.TableHeight / bounds.size.y,
                    .94f / bounds.size.z);
                var groundCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                model.transform.localScale = scale;
                model.transform.localPosition = -Vector3.Scale(groundCenter, scale);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = material;
                model.AddComponent<MeshCollider>().sharedMesh = mesh;
                PrefabUtility.SaveAsPrefabAsset(table, TablePath);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}
