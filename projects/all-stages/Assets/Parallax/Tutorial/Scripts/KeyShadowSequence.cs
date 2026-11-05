using System.Collections.Generic;
using ShadeLink.Gallery;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Tutorial
{
    /// <summary>Turns a matched live shadow into a grabbable key, then opens the door.</summary>
    public sealed class KeyShadowSequence : MonoBehaviour
    {
        enum Phase { Waiting, Reveal, Ready, TurnKey, OpenDoor, Complete }

        [SerializeField] KeyShadowMatcher matcher;
        [SerializeField] Transform receiver;
        [SerializeField] GalleryProp[] sourceProps;
        [SerializeField] KeyShadowTutorialController controller;
        [SerializeField] Transform doorPivot;
        [SerializeField] Vector3 lockPosition;

        const float RevealSeconds = 1.15f;
        const float TurnSeconds = .55f;
        const float DoorSeconds = 1.2f;
        const float InsertDistance = .40f;

        Phase phase;
        GalleryProp key;
        Material keyMaterial;
        float phaseTime;
        Vector3 keyPosition;
        Vector3 targetKeyScale = Vector3.one * .32f;
        Quaternion keyStartRotation;
        Quaternion keyTargetRotation;
        Quaternion doorStartRotation;
        Quaternion doorOpenRotation;
        bool subscribed;

        public void Configure(KeyShadowMatcher shadowMatcher, Transform wallTarget,
            GalleryProp[] sources, KeyShadowTutorialController gameplay, Transform hingedDoor,
            Vector3 insertionPoint)
        {
            matcher = shadowMatcher;
            receiver = wallTarget;
            sourceProps = sources;
            controller = gameplay;
            doorPivot = hingedDoor;
            lockPosition = insertionPoint;
            BindReleaseEvent();
        }

        void OnEnable() => BindReleaseEvent();

        void BindReleaseEvent()
        {
            if (controller == null || subscribed) return;
            controller.PropReleased += OnPropReleased;
            subscribed = true;
        }

        void OnDisable()
        {
            if (controller != null && subscribed) controller.PropReleased -= OnPropReleased;
            subscribed = false;
        }

        void Update()
        {
            if (phase == Phase.Waiting)
            {
                if (matcher != null && matcher.Matched) BeginReveal();
                return;
            }

            phaseTime += Time.deltaTime;
            if (phase == Phase.Reveal) TickReveal();
            else if (phase == Phase.TurnKey) TickKeyTurn();
            else if (phase == Phase.OpenDoor) TickDoorOpen();
        }

        void BeginReveal()
        {
            phase = Phase.Reveal;
            phaseTime = 0;
            controller.PrepareRewardTransition();
            foreach (GalleryProp prop in sourceProps)
                if (prop != null) prop.gameObject.SetActive(false);

            keyPosition = matcher.MatchedPosition + receiver.up * .36f;
            key = CreateKey(keyPosition);
            key.transform.localScale = Vector3.one * .015f;
            keyStartRotation = Quaternion.Euler(16f, 32f, 0f);
            key.transform.rotation = keyStartRotation;
            controller.Tell("두 그림자가 맞았습니다. 열쇠가 만들어집니다.", 3);
        }

        void TickReveal()
        {
            float t = Mathf.Clamp01(phaseTime / RevealSeconds);
            float eased = t * t * (3f - 2f * t);
            key.transform.position = keyPosition + receiver.up * (Mathf.Sin(t * Mathf.PI) * .18f);
            key.transform.localScale = Vector3.Lerp(Vector3.one * .015f, targetKeyScale, eased);
            key.transform.rotation = Quaternion.AngleAxis(360f * t, Vector3.up) * keyStartRotation;
            if (t < 1f) return;

            key.transform.SetPositionAndRotation(keyPosition, keyStartRotation);
            var collider = key.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(.34f, .76f, .10f);
            var body = key.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            key.title = "Forged key";
            key.minScale = .2f;
            // The reward appears suspended in front of the paper, so it has no
            // floor support. Mark that generated starting pose as its home anchor
            // so PlacementSupport keeps the real brass material visible.
            key.mountedAtHome = true;
            key.Remember();
            key.InitializePhysics();
            controller.RegisterProp(key);
            controller.SetInteractionLocked(false);
            controller.Tell("열쇠 획득 · 문고리의 구멍에 맞춰 놓으세요. Q/E로 방향을 돌릴 수 있습니다.", 8);
            phase = Phase.Ready;
        }

        GalleryProp CreateKey(Vector3 position)
        {
            var root = new GameObject("Reward key · turning into view");
            root.transform.position = position;
            keyMaterial = new Material(Shader.Find("Standard")) { name = "Forged key · warm brass" };
            keyMaterial.color = new Color(.76f, .52f, .20f);
            keyMaterial.SetFloat("_Metallic", .72f);
            keyMaterial.SetFloat("_Glossiness", .76f);

            AddKeyPart(root.transform, "Key shaft", PrimitiveType.Cube,
                new Vector3(0, -.065f, 0), new Vector3(.064f, .45f, .065f));
            AddKeyPart(root.transform, "Key bit", PrimitiveType.Cube,
                new Vector3(.045f, -.265f, 0), new Vector3(.13f, .07f, .075f));
            AddKeyPart(root.transform, "Key second notch", PrimitiveType.Cube,
                new Vector3(-.038f, -.17f, 0), new Vector3(.095f, .06f, .075f));
            var bow = new GameObject("Pierced round key bow");
            bow.transform.SetParent(root.transform, false);
            bow.transform.localPosition = new Vector3(0, .22f, 0);
            bow.AddComponent<MeshFilter>().sharedMesh = TorusMesh();
            var renderer = bow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = keyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return root.AddComponent<GalleryProp>();
        }

        void AddKeyPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = keyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        static Mesh TorusMesh()
        {
            const int around = 48, across = 16;
            const float major = .115f, minor = .031f;
            var vertices = new List<Vector3>((around + 1) * (across + 1));
            var triangles = new List<int>(around * across * 6);
            for (int i = 0; i <= around; i++)
            for (int j = 0; j <= across; j++)
            {
                float a = i * Mathf.PI * 2f / around;
                float b = j * Mathf.PI * 2f / across;
                vertices.Add(new Vector3((major + minor * Mathf.Cos(b)) * Mathf.Cos(a),
                    (major + minor * Mathf.Cos(b)) * Mathf.Sin(a), minor * Mathf.Sin(b)));
                if (i < around && j < across)
                {
                    int p = i * (across + 1) + j, q = p + across + 1;
                    triangles.Add(p); triangles.Add(p + 1); triangles.Add(q);
                    triangles.Add(p + 1); triangles.Add(q + 1); triangles.Add(q);
                }
            }
            var mesh = new Mesh { name = "Forged key · pierced bow" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnPropReleased(GalleryProp prop)
        {
            if (phase != Phase.Ready || prop != key) return;

            float distance = Vector3.Distance(prop.transform.position, lockPosition);
            if (distance > InsertDistance)
            {
                controller.Tell("열쇠를 문고리 구멍 가까이에 놓으세요.", 4);
                return;
            }

            float facing = Vector3.Angle(prop.transform.forward, doorPivot.forward);
            float upright = Vector3.Angle(prop.transform.up, Vector3.up);
            if (facing > 24f || upright > 24f)
            {
                controller.Tell("열쇠 방향이 맞지 않습니다. Q/E와 마우스 회전으로 맞춰 보세요.", 5);
                return;
            }

            float scale = prop.transform.localScale.y;
            if (scale < .42f || scale > 1.12f)
            {
                controller.Tell("열쇠 크기가 구멍과 맞지 않습니다. 거리를 조절해 크기를 맞추세요.", 5);
                return;
            }

            BeginKeyTurn();
        }

        void BeginKeyTurn()
        {
            phase = Phase.TurnKey;
            phaseTime = 0;
            controller.SetInteractionLocked(true);
            keyTargetRotation = doorPivot.rotation;
            key.transform.SetParent(doorPivot, true);
            key.transform.SetPositionAndRotation(lockPosition, keyTargetRotation);
            foreach (Collider collider in key.GetComponentsInChildren<Collider>()) collider.enabled = false;
            key.body.isKinematic = true;
            key.body.useGravity = false;
            keyStartRotation = key.transform.rotation;
            controller.Tell("열쇠가 자물쇠에 들어갔습니다.", 2);
        }

        void TickKeyTurn()
        {
            float t = Mathf.Clamp01(phaseTime / TurnSeconds);
            key.transform.rotation = Quaternion.AngleAxis(90f * t, doorPivot.forward) * keyStartRotation;
            if (t < 1f) return;

            phase = Phase.OpenDoor;
            phaseTime = 0;
            doorStartRotation = doorPivot.rotation;
            doorOpenRotation = Quaternion.AngleAxis(-100f, Vector3.up) * doorStartRotation;
            controller.Tell("자물쇠가 풀렸습니다.", 2);
        }

        void TickDoorOpen()
        {
            float t = Mathf.Clamp01(phaseTime / DoorSeconds);
            float eased = t * t * (3f - 2f * t);
            doorPivot.rotation = Quaternion.Slerp(doorStartRotation, doorOpenRotation, eased);
            if (t < 1f) return;

            phase = Phase.Complete;
            controller.SetInteractionLocked(false);
            controller.Tell("문이 열렸습니다. 통로로 나가세요.", 8);
        }

        void OnDestroy()
        {
            if (keyMaterial != null) Destroy(keyMaterial);
        }
    }
}
