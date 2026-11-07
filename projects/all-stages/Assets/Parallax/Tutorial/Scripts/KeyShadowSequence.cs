using ShadeLink.Gallery;
using UnityEngine;

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

        const float RevealSeconds = 1.6f;
        const float TurnSeconds = .55f;
        const float DoorSeconds = 1.2f;
        const float InsertDistance = .40f;
        // The imported model is centered and normalized to .76 units tall.
        const float KeyTipOffset = .38f;

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
        bool revealFailed;
        Vector3[] sourcePositions, sourceScales;
        Quaternion[] sourceRotations;
        public bool DoorIsOpen => phase == Phase.Complete;
        public bool TutorialCompleted { get; private set; }
        public event System.Action TutorialFinished;

        public void CompleteTutorial()
        {
            if (TutorialCompleted) return;
            TutorialCompleted = true;
            controller.Tell("튜토리얼 완료! 문 너머의 복도에 도착했습니다.", float.PositiveInfinity);
            TutorialFinished?.Invoke();
        }

        void Start()
        {
            if (controller != null && doorPivot != null)
                gameObject.AddComponent<KeyShadowDoorPortal>().Configure(this, controller.Camera, controller.Controller);
        }

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
                if (!revealFailed && matcher != null && matcher.Matched) BeginReveal();
                return;
            }

            phaseTime += Time.deltaTime;
            if (phase == Phase.Reveal) TickReveal();
            else if (phase == Phase.TurnKey) TickKeyTurn();
            else if (phase == Phase.OpenDoor) TickDoorOpen();
        }

        void BeginReveal()
        {
            var model = Resources.Load<GameObject>("Tutorial/ShadowKeyV4/ShadowKey_v4");
            var baseColor = Resources.Load<Texture2D>("Tutorial/ShadowKeyV4/ShadowKey_BaseColor");
            var metallic = Resources.Load<Texture2D>("Tutorial/ShadowKeyV4/ShadowKey_MetallicSmoothness");
            if (model == null || baseColor == null || metallic == null)
            {
                revealFailed = true;
                Debug.LogError("ShadowKey v4 model or material textures could not be loaded.", this);
                controller.Tell("열쇠 모델을 불러오지 못했습니다. Unity 콘솔을 확인하세요.", 8);
                return;
            }
            sourcePositions = new Vector3[sourceProps.Length];
            sourceScales = new Vector3[sourceProps.Length];
            sourceRotations = new Quaternion[sourceProps.Length];
            Vector3[] sourceCenters = new Vector3[sourceProps.Length];
            float donutSize = .6f;
            for (int i = 0; i < sourceProps.Length; i++)
            {
                var prop = sourceProps[i];
                if (prop == null) continue;
                sourcePositions[i] = prop.transform.position;
                sourceScales[i] = prop.transform.localScale;
                sourceRotations[i] = prop.transform.rotation;
                sourceCenters[i] = VisualCenter(prop.transform);
                if (i == 1 && prop.TryGetComponent<Renderer>(out var donutRenderer))
                {
                    Vector3 s = donutRenderer.bounds.size;
                    donutSize = Mathf.Max(s.x, s.y, s.z);
                }
            }
            phase = Phase.Reveal;
            phaseTime = 0;
            controller.PrepareRewardTransition();
            for (int i = 0; i < sourceProps.Length; i++)
            {
                var prop = sourceProps[i];
                if (prop == null) continue;
                prop.transform.SetPositionAndRotation(sourcePositions[i], sourceRotations[i]);
                prop.transform.localScale = sourceScales[i];
                foreach (var collider in prop.GetComponentsInChildren<Collider>()) collider.enabled = false;
                if (prop.TryGetComponent<Rigidbody>(out var body)) { body.isKinematic = true; body.useGravity = false; }
            }

            keyPosition = (sourceCenters[0] + sourceCenters[1]) * .5f;
            key = CreateKey(keyPosition, model, baseColor, metallic);
            targetKeyScale = Vector3.one * Mathf.Max(.2f, donutSize / .76f);
            key.transform.localScale = Vector3.one * .015f;
            Vector3 towardPlayer = Vector3.ProjectOnPlane(controller.Camera.transform.position - keyPosition, Vector3.up);
            keyStartRotation = towardPlayer.sqrMagnitude > .001f
                ? Quaternion.LookRotation(towardPlayer, Vector3.up) : Quaternion.identity;
            key.transform.rotation = keyStartRotation;
            controller.Tell("두 그림자가 맞았습니다. 열쇠가 만들어집니다.", 3);
        }

        void TickReveal()
        {
            float t = Mathf.Clamp01(phaseTime / RevealSeconds);
            float absorb = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .72f));
            for (int i = 0; i < sourceProps.Length; i++)
            {
                var prop = sourceProps[i];
                if (prop == null || !prop.gameObject.activeSelf) continue;
                prop.transform.position = Vector3.Lerp(sourcePositions[i], keyPosition, absorb);
                prop.transform.rotation = Quaternion.AngleAxis(220f * absorb * (i == 0 ? 1 : -1), Vector3.up) * sourceRotations[i];
                prop.transform.localScale = Vector3.Lerp(sourceScales[i], sourceScales[i] * .01f, absorb);
                if (absorb >= 1f) prop.gameObject.SetActive(false);
            }
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .35f) / .65f));
            key.transform.position = keyPosition + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .18f);
            key.transform.localScale = Vector3.Lerp(Vector3.one * .015f, targetKeyScale, appear);
            key.transform.rotation = Quaternion.AngleAxis(360f * t, Vector3.up) * keyStartRotation;
            if (t < 1f) return;

            key.transform.SetPositionAndRotation(keyPosition, keyStartRotation);
            var collider = key.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(.32f, .76f, .08f);
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

        static Vector3 VisualCenter(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return prop.position;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        GalleryProp CreateKey(Vector3 position, GameObject model, Texture2D baseColor, Texture2D metallic)
        {
            var root = new GameObject("Reward key · turning into view");
            root.transform.position = position;
            keyMaterial = new Material(Shader.Find("Standard")) { name = "ShadowKey v4 · warm brass", color = Color.white };
            keyMaterial.mainTexture = baseColor;
            keyMaterial.SetTexture("_MetallicGlossMap", metallic);
            keyMaterial.SetFloat("_Glossiness", 1f);
            keyMaterial.EnableKeyword("_METALLICGLOSSMAP");
            var visual = Instantiate(model, root.transform);
            visual.name = "ShadowKey v4 model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0, 0, -90);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = keyMaterial;
            var filters = visual.GetComponentsInChildren<MeshFilter>();
            float topMin = float.PositiveInfinity, topMax = float.NegativeInfinity;
            float bottomMin = float.PositiveInfinity, bottomMax = float.NegativeInfinity;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (p.y > bounds.center.y - root.transform.position.y + bounds.size.y * .25f) { topMin = Mathf.Min(topMin, p.x); topMax = Mathf.Max(topMax, p.x); }
                    if (p.y < bounds.center.y - root.transform.position.y - bounds.size.y * .25f) { bottomMin = Mathf.Min(bottomMin, p.x); bottomMax = Mathf.Max(bottomMax, p.x); }
                }
            }
            if (topMax - topMin < bottomMax - bottomMin) visual.transform.localRotation = Quaternion.Euler(0, 0, 90);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float factor = .76f / Mathf.Max(.001f, bounds.size.y);
            visual.transform.localScale *= factor;
            visual.transform.localPosition = -(bounds.center - root.transform.position) * factor;
            return root.AddComponent<GalleryProp>();
        }

        void OnPropReleased(GalleryProp prop)
        {
            if (phase != Phase.Ready || prop != key) return;

            Vector3 tipPosition = prop.transform.TransformPoint(Vector3.down * KeyTipOffset);
            float distance = Mathf.Min(Vector3.Distance(prop.transform.position, lockPosition),
                Vector3.Distance(tipPosition, lockPosition));
            if (distance > InsertDistance)
            {
                controller.Tell("열쇠를 문고리 구멍 가까이에 놓으세요.", 4);
                return;
            }

            float facing = Vector3.Angle(prop.transform.forward, doorPivot.forward);
            float upright = Vector3.Angle(prop.transform.up, Vector3.up);
            float inserted = Vector3.Angle(-prop.transform.up, doorPivot.forward);
            if ((facing > 24f || upright > 24f) && inserted > 24f)
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
            Vector3 faceDirection = Vector3.ProjectOnPlane(key.transform.forward, doorPivot.forward);
            if (faceDirection.sqrMagnitude < .001f) faceDirection = doorPivot.up;
            keyTargetRotation = Quaternion.LookRotation(faceDirection.normalized, -doorPivot.forward);
            float tipDistance = KeyTipOffset * key.transform.lossyScale.y;
            key.transform.SetParent(doorPivot, true);
            key.transform.SetPositionAndRotation(lockPosition - doorPivot.forward * tipDistance, keyTargetRotation);
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
