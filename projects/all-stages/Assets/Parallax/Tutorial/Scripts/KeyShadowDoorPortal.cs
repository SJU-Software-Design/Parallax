using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Tutorial
{
    // The hallway lives away from the room and is visible only through the door.
    public sealed class KeyShadowDoorPortal : MonoBehaviour
    {
        const int CorridorLayer = 27;
        const float DoorZ = 5.70f;
        static readonly Vector3 Offset = new Vector3(0f, 0f, 94.30f);
        KeyShadowSequence sequence;
        Camera source, portalCamera;
        CharacterController character;
        GameObject hallway, surface, cameraObject;
        RenderTexture texture;
        Material surfaceMaterial, wallMaterial, floorMaterial;
        int originalMask;
        bool entered, maskChanged;
        float previousZ;

        public void Configure(KeyShadowSequence owner, Camera view, CharacterController player)
        {
            sequence = owner; source = view; character = player;
            if (source == null || character == null) { enabled = false; return; }
            var shader = Shader.Find("Parallax/TutorialDoorPortal");
            if (shader == null) { Debug.LogError("Tutorial door portal shader is missing."); enabled = false; return; }
            originalMask = source.cullingMask;
            source.cullingMask &= ~(1 << CorridorLayer);
            maskChanged = true;
            previousZ = character.transform.position.z;
            hallway = new GameObject("Tutorial corridor · separate space");
            wallMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(.045f, .048f, .052f) };
            floorMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(.025f, .027f, .03f) };
            Box("Corridor floor", new Vector3(0, -.12f, 106), new Vector3(3.2f, .24f, 12), floorMaterial);
            Box("Corridor left wall", new Vector3(-1.68f, 3.4f, 106), new Vector3(.16f, 6.8f, 12), wallMaterial);
            Box("Corridor right wall", new Vector3(1.68f, 3.4f, 106), new Vector3(.16f, 6.8f, 12), wallMaterial);
            Box("Corridor ceiling", new Vector3(0, 6.82f, 106), new Vector3(3.5f, .16f, 12), floorMaterial);
            Box("Corridor end", new Vector3(0, 3.4f, 112), new Vector3(3.5f, 6.8f, .16f), floorMaterial);
            texture = new RenderTexture(1024, 1024, 24) { name = "Tutorial door corridor" };
            texture.Create();
            surfaceMaterial = new Material(shader);
            surfaceMaterial.mainTexture = texture;
            surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Corridor visible through door";
            Destroy(surface.GetComponent<Collider>());
            surface.transform.SetPositionAndRotation(new Vector3(0, 3.225f, DoorZ + .012f), Quaternion.identity);
            surface.transform.localScale = new Vector3(2.74f, 6.44f, 1f);
            surface.GetComponent<Renderer>().sharedMaterial = surfaceMaterial;
            surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            surface.SetActive(false);
            cameraObject = new GameObject("Tutorial corridor preview camera");
            portalCamera = cameraObject.AddComponent<Camera>();
            portalCamera.enabled = false;
        }

        void Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = CorridorLayer; go.transform.SetParent(hallway.transform);
            go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        void LateUpdate()
        {
            if (source == null || sequence == null || entered) return;
            bool open = sequence.DoorIsOpen;
            if (surface.activeSelf != open) surface.SetActive(open);
            if (!open) { previousZ = character.transform.position.z; return; }
            portalCamera.CopyFrom(source);
            portalCamera.enabled = false;
            portalCamera.transform.SetPositionAndRotation(source.transform.position + Offset, source.transform.rotation);
            portalCamera.cullingMask = 1 << CorridorLayer;
            portalCamera.targetTexture = texture;
            portalCamera.aspect = source.aspect;
            portalCamera.clearFlags = CameraClearFlags.SolidColor;
            portalCamera.backgroundColor = Color.black;
            portalCamera.Render();
            Vector3 p = character.transform.position;
            if (previousZ < DoorZ && p.z >= DoorZ && Mathf.Abs(p.x) < 1.25f && p.y < 6f)
            {
                character.enabled = false;
                character.transform.position = p + Offset;
                character.enabled = true;
                source.cullingMask = originalMask | (1 << CorridorLayer);
                surface.SetActive(false);
                entered = true;
                sequence.CompleteTutorial();
            }
            previousZ = p.z;
        }

        void OnDestroy()
        {
            if (source != null && maskChanged) source.cullingMask = originalMask;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (surfaceMaterial != null) Destroy(surfaceMaterial);
            if (wallMaterial != null) Destroy(wallMaterial);
            if (floorMaterial != null) Destroy(floorMaterial);
            if (surface != null) Destroy(surface);
            if (hallway != null) Destroy(hallway);
            if (cameraObject != null) Destroy(cameraObject);
        }
    }
}
