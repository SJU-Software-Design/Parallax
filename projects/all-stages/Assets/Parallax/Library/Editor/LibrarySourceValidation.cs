using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadeLink.Library;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class LibrarySourceValidation
    {
        [Serializable] sealed class Result
        {
            public bool passed;
            public string unity;
            public string[] scenes;
            public int checkedComponents, props, books, originalMeshInstances, shadowCasters;
        }
        public static void Run()
        {
            var result = new Result { unity = Application.unityVersion, scenes = new[] {
                "Assets/Parallax/Gallery/Scenes/Gallery.unity",
                "Assets/Parallax/Legacy/Scenes/Indoor.unity",
                "Assets/Parallax/Legacy/Scenes/Courtyard.unity", LibraryBuild.ScenePath } };
            foreach (string path in result.scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new Exception("Missing script: " + path + "/" + transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        result.checkedComponents++;
                        var property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceInstanceIDValue != 0 && property.objectReferenceValue == null)
                                throw new Exception("Missing reference: " + path + "/" + transform.name + "/" + property.propertyPath);
                    }
                }
            }
            var game = UnityEngine.Object.FindFirstObjectByType<LibraryGame>();
            if (game == null || game.controller == null || game.view == null || game.lighting == null)
                throw new Exception("Library scene controller references incomplete");
            result.props = game.props.Length; result.books = game.props.Count(p => p.kind == "Book");
            if (result.props != 49 || result.books != 40 || game.props.Select(p => p.objectId).Distinct().Count() != 49)
                throw new Exception("Library furniture/book inventory is incomplete");
            foreach (var prop in game.props)
            {
                if (prop.GetComponentsInChildren<LibraryProp>().Length != 1 || prop.parts.Length == 0)
                    throw new Exception("Independent prop/collider missing: " + prop.objectId);
                var originals = prop.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Rinku Choudhary"));
                if (!originals.Any()) throw new Exception("Original kit mesh missing: " + prop.objectId);
                foreach (var filter in originals)
                {
                    result.originalMeshInstances++;
                    if (filter.sharedMesh == null || filter.GetComponent<Renderer>().sharedMaterials.Any(m => m == null))
                        throw new Exception("Missing original mesh/material on " + prop.objectId);
                }
            }
            game.lighting.Rebuild(); result.shadowCasters = game.lighting.Count;
            if (result.shadowCasters != 124) throw new Exception("Unexpected caster inventory: " + result.shadowCasters);
            foreach (string name in new[] { "Parallax/LibrarySurface", "Parallax/GallerySurface", "ShadeLink/Flat", "ShadeLink/WorldText", "ShadeLink/Finish" })
            {
                var shader = Shader.Find(name);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader error: " + name);
            }
            foreach (string path in new[] { LibraryBuild.ScenePath, result.scenes[0] })
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path)) throw new Exception("Missing build scene: " + path);
            if (!File.Exists("Assets/ThirdParty/RinkuChoudhary/LibraryKit.fbx")) throw new Exception("Original FBX missing");
            result.passed = true;
            Directory.CreateDirectory("Validation/Library");
            File.WriteAllText("Validation/Library/source-validation.json", JsonUtility.ToJson(result, true));
            Debug.Log("LIBRARY_SOURCE_VALIDATION_PASS");
        }
    }
}
