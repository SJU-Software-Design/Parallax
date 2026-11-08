using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    public static class TutorialLightBeamSetup
    {
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial/LightBeam";
        [MenuItem("Parallax/Add tutorial light beam")]
        public static void Add()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Parallax/Tutorial/Scenes/KeyShadowPreview.unity" || scene.isDirty)
                throw new System.Exception("Open and save KeyShadowPreview before adding the beam.");
            var lightObject = GameObject.Find("Only light source · modern wall lamp on right");
            var light = lightObject != null ? lightObject.GetComponent<Light>() : null;
            if (light == null || light.type != LightType.Spot) throw new System.Exception("The tutorial spotlight is missing.");
            if (light.transform.Find("Tutorial light beam") != null) throw new System.Exception("The beam is already present.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Parallax/Tutorial/Resources/Tutorial", "LightBeam");
            float length = Mathf.Min(light.range, 25f);
            Physics.SyncTransforms();
            foreach (var hit in Physics.RaycastAll(light.transform.position, light.transform.forward, light.range))
                if (hit.collider.gameObject.name == "Room · left wall") length = Mathf.Min(length, hit.distance - .05f);
            const int segments = 48, rings = 7;
            var vertices = new Vector3[(segments + 1) * rings];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * (rings - 1) * 6];
            float radiusFactor = Mathf.Tan(light.spotAngle * .5f * Mathf.Deg2Rad);
            for (int j = 0; j < rings; j++)
            {
                float t = (float)j / (rings - 1), z = length * t;
                float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 8)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - t) * 5));
                for (int i = 0; i <= segments; i++)
                {
                    float angle = 2 * Mathf.PI * i / segments;
                    int n = j * (segments + 1) + i;
                    vertices[n] = new Vector3(Mathf.Cos(angle) * z * radiusFactor, Mathf.Sin(angle) * z * radiusFactor, z);
                    colors[n] = new Color(1, 1, 1, fade);
                }
            }
            int k = 0;
            for (int j = 0; j < rings - 1; j++) for (int i = 0; i < segments; i++)
            {
                int a = j * (segments + 1) + i, b = a + segments + 1;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = a + 1;
                triangles[k++] = a + 1; triangles[k++] = b; triangles[k++] = b + 1;
            }
            var mesh = new Mesh { name = "Tutorial light beam cone", vertices = vertices, triangles = triangles, colors = colors };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/TutorialLightBeam.asset");
            var shader = Shader.Find("Parallax/Tutorial Light Beam");
            if (shader == null) throw new System.Exception("The beam shader was not imported.");
            var material = new Material(shader) { name = "Tutorial light beam · warm subtle" };
            AssetDatabase.CreateAsset(material, Folder + "/TutorialLightBeam.mat");
            var beam = new GameObject("Tutorial light beam");
            beam.transform.SetParent(light.transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TUTORIAL_LIGHT_BEAM_ADDED length=" + length);
        }
    }
}
