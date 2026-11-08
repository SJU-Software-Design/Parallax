using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;

namespace ShadeLink.Editor
{
    // Applies the existing training-room finish to the separate key preview.
    public static class KeyShadowRoomFinish
    {
        const string PreviewPath = "Assets/Parallax/Tutorial/Scenes/KeyShadowPreview.unity";

        [MenuItem("Parallax/Update key tutorial room")]
        public static void UpdateOpenPreview()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PreviewPath || EditorApplication.isPlaying)
            {
                Debug.LogError("Open KeyShadowPreview in edit mode before updating the room.");
                return;
            }
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Apply(Scene scene)
        {
            GameObject Find(string name)
            {
                foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
                throw new System.InvalidOperationException("Missing key tutorial object: " + name);
            }
            void SetBox(string name, Vector3 position, Vector3 size)
            {
                var t = Find(name).transform; t.position = position; t.localScale = size;
            }
            void ShiftX(string name, float delta)
            {
                var t = Find(name).transform; t.position += Vector3.right * delta;
            }

            var left = Find("Room · left wall");
            var right = Find("Room · right wall");
            float leftShift = -13.2f - left.transform.position.x;
            float rightShift = 13.2f - right.transform.position.x;
            var wallMaterial = left.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(left);
            var replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            replacement.name = "Room · left wall";
            SceneManager.MoveGameObjectToScene(replacement, scene);
            replacement.transform.SetPositionAndRotation(new Vector3(-13.2f, 4.5f, 0), Quaternion.identity);
            replacement.transform.localScale = new Vector3(.35f, 9, 24);
            replacement.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            SetBox("Room · right wall", new Vector3(13.2f, 4.5f, 0), new Vector3(.35f, 9, 24));
            SetBox("Room · floor", new Vector3(0, -.12f, 0), new Vector3(27, .24f, 24));
            SetBox("Room · closed ceiling", new Vector3(0, 9.05f, 0), new Vector3(27, .3f, 24));
            SetBox("Room · back wall left of door", new Vector3(-7.55f, 4.5f, 11.7f), new Vector3(11.9f, 9, .35f));
            SetBox("Room · back wall right of door", new Vector3(7.55f, 4.5f, 11.7f), new Vector3(11.9f, 9, .35f));
            SetBox("Room · back wall behind door", new Vector3(0, 4.5f, 11.7f), new Vector3(3.2f, 9, .35f));
            SetBox("Room · front wall", new Vector3(0, 4.5f, -11.7f), new Vector3(27, 9, .35f));
            ShiftX("Left wall · white square paper", leftShift);
            ShiftX("Blue hand-drawn key · teeth point down", leftShift);
            ShiftX("Modern wall lamp · graphite housing", rightShift);
            ShiftX("Modern wall lamp · warm diffuser", rightShift);
            ShiftX("Only light source · modern wall lamp on right", rightShift);
            var lamp = Find("Only light source · modern wall lamp on right").GetComponent<Light>();
            lamp.range = 32f;
            lamp.intensity = 3f;
            var controller = Find("Tutorial player · shared movement and object handling").GetComponent<KeyShadowTutorialController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("roomMinimum").vector3Value = new Vector3(-13.2f, .02f, -11.7f);
            serialized.FindProperty("roomMaximum").vector3Value = new Vector3(13.2f, 8.85f, 11.7f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Fine floor seam" || root.name == "Key room · panel and tile finish") Object.DestroyImmediate(root);
            var rootFinish = new GameObject("Key room · panel and tile finish");
            SceneManager.MoveGameObjectToScene(rootFinish, scene);
            var shader = Resources.Load<Shader>("Tutorial/BrushedSteel");
            if (shader == null) throw new System.InvalidOperationException("Training room finish shader is missing.");
            Material Finish(Color color, Vector4 grain, float wear)
            {
                var m = new Material(shader) { color = color };
                m.SetFloat("_Metallic", .16f); m.SetFloat("_Glossiness", .31f);
                m.SetVector("_GrainScale", grain); m.SetFloat("_Wear", wear);
                return m;
            }
            var panel = Finish(new Color(.77f,.77f,.74f), new Vector4(36,5,36,0), .64f);
            var panelLight = Finish(new Color(.80f,.80f,.77f), new Vector4(36,5,36,0), .64f);
            var tile = Finish(new Color(.61f,.62f,.62f), new Vector4(24,18,7,0), .82f);
            var tileLight = Finish(new Color(.64f,.64f,.64f), new Vector4(24,18,7,0), .82f);
            void Decoration(string name, Vector3 position, Vector3 size, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                go.transform.SetParent(rootFinish.transform, false);
                go.transform.position = position; go.transform.localScale = size;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
            for (int x = 0; x < 7; x++) for (int z = 0; z < 6; z++)
                Decoration("Grey floor tile", new Vector3(-13.5f + (x+.5f)*27f/7f, -.008f, -12f + (z+.5f)*4f),
                    new Vector3(27f/7f-.032f, .024f, 3.968f), (x+z)%3 == 1 ? tileLight : tile);
            for (int i = 0; i < 9; i++) foreach (float side in new[] {-1f,1f})
                Decoration("Vertical wall panel", new Vector3(side*13.015f,4.63f,-12f+(i+.5f)*24f/9f),
                    new Vector3(.02f,8.72f,24f/9f-.038f), i%3==1 ? panelLight : panel);
            for (int i = 0; i < 10; i++) foreach (float side in new[] {-1f,1f})
                Decoration("Vertical wall panel", new Vector3(-13.5f+(i+.5f)*2.7f,4.63f,side*11.505f),
                    new Vector3(2.662f,8.72f,.02f), i%3==1 ? panelLight : panel);
        }
    }
}
