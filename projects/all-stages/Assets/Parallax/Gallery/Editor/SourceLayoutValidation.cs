using System;
using System.IO;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Parallax.Editor
{
    // Checks that a freshly imported source package retains scene and asset references.
    public static class SourceLayoutValidation
    {
        public static void Run()
        {
            string galleryPath = "Assets/Parallax/Gallery/Scenes/Gallery.unity";
            foreach (string scenePath in new[] { galleryPath,
                "Assets/Parallax/Legacy/Scenes/Indoor.unity",
                "Assets/Parallax/Legacy/Scenes/Courtyard.unity" })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int components = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new Exception("Missing script on " + transform.name + " in " + scenePath);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        components++;
                        var properties = new SerializedObject(component).GetIterator();
                        while (properties.Next(true))
                            if (properties.propertyType == SerializedPropertyType.ObjectReference &&
                                properties.objectReferenceInstanceIDValue != 0 && properties.objectReferenceValue == null)
                                throw new Exception("Missing reference: " + transform.name + "." + properties.propertyPath);
                    }
                }
                Debug.Log("SOURCE_SCENE_OK " + scenePath + " components=" + components);
            }
            EditorSceneManager.OpenScene(galleryPath, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryGame>();
            if (game == null || game.props == null || game.props.Length != 9 || game.lighting == null)
                throw new Exception("Gallery scene references are incomplete.");
            if (EditorBuildSettings.scenes.Length != 1 || !EditorBuildSettings.scenes[0].enabled ||
                EditorBuildSettings.scenes[0].path != galleryPath)
                throw new Exception("The default build scene must be Gallery.");
            foreach (string name in new[] { "Parallax/GallerySurface", "ShadeLink/Flat", "ShadeLink/WorldText", "ShadeLink/Finish" })
            {
                Shader shader = Shader.Find(name);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Invalid shader: " + name);
            }
            Directory.CreateDirectory("Validation/SourceLayout");
            File.WriteAllText("Validation/SourceLayout/result.txt",
                "PASS\nUnity: " + Application.unityVersion + "\nScenes: Gallery, Indoor, Courtyard\n" +
                "Missing scripts/references: 0\nGallery props: 9\nShader checks: 4\n" +
                "Build output: " + BuildPaths.Output("Parallax_Unity_v0.8.2_Gallery/Parallax.exe") + "\n");
            Debug.Log("SOURCE_LAYOUT_VALIDATION_PASS");
        }
    }
}
