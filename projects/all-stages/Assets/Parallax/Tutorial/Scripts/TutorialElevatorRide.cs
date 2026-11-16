using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Tutorial
{
    public enum ElevatorPhase
    {
        IdleClosed, OpeningForBoarding, Boarding, ClosingForDeparture,
        Descending, Stopping, ArrivalChime, OpeningAtDestination, Arrived
    }

    // A reusable cabin. The route owner loads the destination while these
    // closed doors, their camera and the shaft remain visible across scene loads.
    public sealed class TutorialElevatorRide : MonoBehaviour
    {
        [Serializable]
        public struct TransferPose
        {
            public Vector3 localFeet;
            public Vector3 localEye;
            public Quaternion localViewRotation;
        }

        public event Action<TutorialElevatorRide> DepartureReady;
        public event Action<TutorialElevatorRide> ArrivedAtDestination;
        public event Action<ElevatorPhase, ElevatorPhase> PhaseChanged;
        public event Action RideReset;
        public TutorialElevatorAudio SelectedAudio { get; private set; }
        public ElevatorPhase Phase { get; private set; }
        public float DoorOpenFraction { get; private set; }
        public float RideElapsed { get; private set; }
        public bool DestinationReady { get; private set; }
        public string FloorLabel => atBasement ? "B1" : "1";
        public bool IsTravelling => Phase == ElevatorPhase.Descending || Phase == ElevatorPhase.Stopping;
        public ElevatorE01Asset Asset => asset;
        public Camera TransferCamera => transferCamera;
        public float BoardingDwell => boardingDwell;
        public bool ContainsPlayer => PlayerWhollyInside();
        public float PhaseElapsed => phaseElapsed;
        public float DescentDuration = 5.2f;
        public float DoorDuration = 2.1f;
        public float StopDuration = 1f;
        public float ArrivalBellLead = .45f;
        public bool AutomaticTick = true;
        public bool Paused { get; set; }
        public Vector3 WorldEntry => transform.TransformPoint(new Vector3(asset != null ? asset.cabinBounds.center.x : 0, .03f, 1.4f));
        public Vector3 WorldBoardingCenter => transform.TransformPoint(asset != null
            ? new Vector3(asset.cabinBounds.center.x, asset.floorY + .03f, asset.cabinBounds.center.z)
            : new Vector3(0, .03f, -1.2f));

        ElevatorE01Asset asset;
        CharacterController player;
        Camera playerCamera, transferCamera;
        readonly List<TextMesh> labels = new List<TextMesh>();
        readonly List<Transform> arrows = new List<Transform>();
        readonly List<Transform> shaftSegments = new List<Transform>();
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        readonly Collider[] obstructionHits = new Collider[32];
        readonly PlayerControls transferControls = new PlayerControls();
        Transform shaftRoot;
        Light cabinLight;
        Font displayFont;
        Material displayTextMaterial;
        float phaseElapsed, boardingDwell;
        bool atBasement, built;
        TransferPose retainedPose;
        float transferYaw, transferPitch;

        public void Initialize(ElevatorE01Asset model)
        {
            if (asset != null && asset != model) throw new InvalidOperationException("Elevator model is already bound");
            asset = model;
            if (asset == null || asset.doors == null || asset.doors.Length != 4 ||
                asset.closedLocalPositions == null || asset.openLocalPositions == null ||
                asset.closedLocalPositions.Length != 4 || asset.openLocalPositions.Length != 4)
                throw new InvalidOperationException("E01 needs four independent door leaves and baked open/closed positions");
            if (!built)
            {
                BuildDisplays();
                BuildCabinLighting();
                BuildShaft();
                PrepareGlass();
                built = true;
            }
            if (SelectedAudio == null) SelectedAudio = GetComponent<TutorialElevatorAudio>() ?? gameObject.AddComponent<TutorialElevatorAudio>();
            SelectedAudio.Initialize(this);
            ResetRide();
        }

        public void BindPlayer(CharacterController controller, Camera view)
        {
            player = controller;
            playerCamera = view;
            if (view != null)
            {
                var shake = view.GetComponent<TutorialElevatorViewShake>() ?? view.gameObject.AddComponent<TutorialElevatorViewShake>();
                shake.Ride = this;
            }
        }

        void Update()
        {
            if (AutomaticTick) Step(Time.deltaTime);
            if (transferCamera != null && !Paused && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked)
            {
                var input = transferControls.Read(Time.deltaTime, false, false);
                transferYaw += input.Look.x * Mathf.Rad2Deg;
                transferPitch = Mathf.Clamp(transferPitch - input.Look.y * Mathf.Rad2Deg, -82f, 82f);
                transferCamera.transform.localRotation = Quaternion.Euler(transferPitch, transferYaw, 0);
            }
        }

        public void Step(float dt)
        {
            if (Paused || asset == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            phaseElapsed += dt;
            switch (Phase)
            {
                case ElevatorPhase.IdleClosed:
                    if (PlayerInApproach()) ChangePhase(ElevatorPhase.OpeningForBoarding);
                    break;
                case ElevatorPhase.OpeningForBoarding:
                    MoveDoors(Mathf.MoveTowards(DoorOpenFraction, 1, dt / Mathf.Max(.1f, DoorDuration)));
                    if (DoorOpenFraction >= 1) ChangePhase(ElevatorPhase.Boarding);
                    break;
                case ElevatorPhase.Boarding:
                    boardingDwell = PlayerWhollyInside() && !DoorwayObstructed() ? boardingDwell + dt : 0;
                    if (boardingDwell >= .75f) ChangePhase(ElevatorPhase.ClosingForDeparture);
                    break;
                case ElevatorPhase.ClosingForDeparture:
                    if (DoorwayObstructed() || !PlayerWhollyInside())
                    {
                        boardingDwell = 0;
                        ChangePhase(ElevatorPhase.OpeningForBoarding);
                        break;
                    }
                    MoveDoors(Mathf.MoveTowards(DoorOpenFraction, 0, dt / Mathf.Max(.1f, DoorDuration)));
                    if (DoorOpenFraction <= 0)
                    {
                        retainedPose = CapturePose();
                        ChangePhase(ElevatorPhase.Descending);
                        DepartureReady?.Invoke(this);
                    }
                    break;
                case ElevatorPhase.Descending:
                    RideElapsed = Mathf.Min(DescentDuration, RideElapsed + dt);
                    atBasement = RideElapsed >= DescentDuration * .78f;
                    if (RideElapsed >= DescentDuration && DestinationReady) ChangePhase(ElevatorPhase.Stopping);
                    break;
                case ElevatorPhase.Stopping:
                    if (phaseElapsed >= StopDuration) ChangePhase(ElevatorPhase.ArrivalChime);
                    break;
                case ElevatorPhase.ArrivalChime:
                    if (phaseElapsed >= ArrivalBellLead) ChangePhase(ElevatorPhase.OpeningAtDestination);
                    break;
                case ElevatorPhase.OpeningAtDestination:
                    MoveDoors(Mathf.MoveTowards(DoorOpenFraction, 1, dt / Mathf.Max(.1f, DoorDuration)));
                    if (DoorOpenFraction >= 1)
                    {
                        ChangePhase(ElevatorPhase.Arrived);
                        ArrivedAtDestination?.Invoke(this);
                    }
                    break;
            }
            RefreshVisuals();
        }

        public void NotifyDestinationReady() { DestinationReady = true; }

        public void ResetRide()
        {
            EndTransferView();
            Phase = ElevatorPhase.IdleClosed;
            phaseElapsed = boardingDwell = RideElapsed = 0;
            DestinationReady = atBasement = false;
            Paused = false;
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            MoveDoors(0);
            RefreshVisuals();
            RideReset?.Invoke();
        }

        public TransferPose CapturePose()
        {
            var result = retainedPose;
            if (transferCamera == null && player != null)
                result.localFeet = transform.InverseTransformPoint(player.transform.position);
            var view = transferCamera != null ? transferCamera : playerCamera;
            if (view != null)
            {
                result.localEye = transform.InverseTransformPoint(view.transform.position);
                result.localViewRotation = Quaternion.Inverse(transform.rotation) * view.transform.rotation;
            }
            return result;
        }

        public void CaptureTransferView()
        {
            if (transferCamera != null) return;
            retainedPose = CapturePose();
            var go = new GameObject("Elevator scene-transfer view");
            go.transform.SetParent(transform, false);
            transferCamera = go.AddComponent<Camera>();
            if (playerCamera != null) transferCamera.CopyFrom(playerCamera);
            go.transform.localPosition = retainedPose.localEye;
            go.transform.localRotation = retainedPose.localViewRotation;
            go.AddComponent<AudioListener>();
            go.AddComponent<TutorialElevatorViewShake>().Ride = this;
            transferControls.Reset();
            var euler = retainedPose.localViewRotation.eulerAngles;
            transferYaw = euler.y;
            transferPitch = euler.x > 180 ? euler.x - 360 : euler.x;
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                var oldListener = playerCamera.GetComponent<AudioListener>();
                if (oldListener != null) oldListener.enabled = false;
            }
        }

        public void RestorePose(CharacterController destination, Camera view)
        {
            var pose = CapturePose();
            if (destination != null)
            {
                bool wasEnabled = destination.enabled;
                destination.enabled = false;
                destination.transform.position = transform.TransformPoint(pose.localFeet);
                destination.enabled = wasEnabled;
            }
            if (view != null)
            {
                view.transform.position = transform.TransformPoint(pose.localEye);
                view.transform.rotation = transform.rotation * pose.localViewRotation;
                view.enabled = true;
                var listener = view.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            BindPlayer(destination, view);
            retainedPose = pose;
            EndTransferView();
        }

        public void EndTransferView()
        {
            if (transferCamera == null) return;
            transferCamera.enabled = false;
            var listener = transferCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            Destroy(transferCamera.gameObject);
            transferCamera = null;
        }

        void ChangePhase(ElevatorPhase next)
        {
            var previous = Phase;
            Phase = next; phaseElapsed = 0;
            PhaseChanged?.Invoke(previous, next);
        }

        void MoveDoors(float value)
        {
            DoorOpenFraction = Mathf.Clamp01(value);
            float smooth = DoorOpenFraction * DoorOpenFraction * (3 - 2 * DoorOpenFraction);
            if (asset == null) return;
            for (int i = 0; i < 4; i++)
                if (asset.doors[i] != null)
                    asset.doors[i].localPosition = Vector3.Lerp(asset.closedLocalPositions[i], asset.openLocalPositions[i], smooth);
            Physics.SyncTransforms();
        }

        public bool PlayerInApproach()
        {
            return player != null && asset.approachBounds.Contains(transform.InverseTransformPoint(player.bounds.center));
        }

        public bool PlayerWhollyInside()
        {
            if (asset == null || player == null || !player.gameObject.activeInHierarchy) return false;
            Bounds local = BoundsFromWorld(player.bounds);
            Bounds interior = asset.cabinBounds;
            Vector3 center = transform.InverseTransformPoint(player.transform.TransformPoint(player.center));

            // The source mesh already stops the capsule at the walls/handrails.
            // Test its centre there so a passenger touching a wall still boards.
            // Only the doorway needs the entire capsule to be clear of the doors.
            Vector3 scale = player.transform.lossyScale;
            float radius = player.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0, player.height * Mathf.Abs(scale.y) * .5f - radius);
            Matrix4x4 toCabin = transform.worldToLocalMatrix;
            Vector3 depthProjection = new Vector3(toCabin.m20, toCabin.m21, toCabin.m22);
            float front = center.z + radius * depthProjection.magnitude +
                halfSegment * Mathf.Abs(Vector3.Dot(depthProjection, player.transform.up));
            return center.x >= interior.min.x && center.x <= interior.max.x &&
                center.z >= interior.min.z && center.z <= interior.max.z &&
                front <= asset.doorSafetyBounds.min.z - .015f &&
                local.min.y >= interior.min.y - .12f && local.max.y <= interior.max.y + .08f;
        }

        Bounds BoundsFromWorld(Bounds world)
        {
            Bounds result = new Bounds(transform.InverseTransformPoint(world.min), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(transform.InverseTransformPoint(new Vector3(
                (i & 1) == 0 ? world.min.x : world.max.x,
                (i & 2) == 0 ? world.min.y : world.max.y,
                (i & 4) == 0 ? world.min.z : world.max.z)));
            return result;
        }

        public bool DoorwayObstructed()
        {
            if (asset == null) return false;
            var b = asset.doorSafetyBounds;
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(b.center),
                Vector3.Scale(b.extents, transform.lossyScale), obstructionHits, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = obstructionHits[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (c == player || c.attachedRigidbody != null) return true;
            }
            return false;
        }

        void PrepareGlass()
        {
            if (asset.backGlass == null) return;
            var shader = Resources.Load<Shader>("Tutorial/ElevatorFrostedGlass");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "E01 frosted shaft glazing", color = new Color(.61f, .74f, .76f, .42f) };
            ownedObjects.Add(material);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .13f);
            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3); material.SetFloat("_Metallic", 0);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0); material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }
            asset.backGlass.sharedMaterial = material;
            asset.backGlass.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildDisplays()
        {
            if (asset.displayAnchors == null) return;
            displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont.RequestCharactersInTexture("1B", 96, FontStyle.Normal);
            var fontShader = Resources.Load<Shader>("Tutorial/ElevatorFloorText");
            displayTextMaterial = new Material(fontShader != null ? fontShader : displayFont.material.shader)
            { name = "Depth-tested one-sided elevator floor digits", mainTexture = displayFont.material.mainTexture };
            ownedObjects.Add(displayTextMaterial);
            Font.textureRebuilt += OnDisplayFontRebuilt;
            foreach (Transform anchor in asset.displayAnchors)
            {
                if (anchor == null) continue;
                var go = new GameObject("Floor display 1 to B1");
                go.transform.SetParent(anchor, false);
                go.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var label = go.AddComponent<TextMesh>();
                label.font = displayFont; label.fontSize = 96; label.characterSize = .012f;
                label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.color = new Color(.94f, .99f, 1); label.text = "1";
                label.GetComponent<MeshRenderer>().sharedMaterial = displayTextMaterial;
                labels.Add(label);
                var triangle = new GameObject("Descending indicator");
                triangle.transform.SetParent(anchor, false);
                triangle.transform.localPosition = new Vector3(-.15f, 0, 0);
                var mesh = new Mesh { name = "Downward floor indicator" };
                ownedObjects.Add(mesh);
                mesh.vertices = new[] { new Vector3(-.035f, .028f, 0), new Vector3(.035f, .028f, 0), new Vector3(0, -.028f, 0) };
                mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 }; mesh.RecalculateNormals();
                triangle.AddComponent<MeshFilter>().sharedMesh = mesh;
                triangle.AddComponent<MeshRenderer>().sharedMaterial = Solid("Unlit/Color", new Color(.65f, .88f, .94f));
                arrows.Add(triangle.transform);
            }
        }

        void OnDisplayFontRebuilt(Font rebuilt)
        {
            if (rebuilt == displayFont && displayTextMaterial != null)
                displayTextMaterial.mainTexture = rebuilt.material.mainTexture;
        }

        void BuildCabinLighting()
        {
            var go = new GameObject("Elevator neutral ceiling light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = asset.cabinLightAnchor != null
                ? transform.InverseTransformPoint(asset.cabinLightAnchor.position)
                : new Vector3(0, asset.cabinBounds.max.y - .25f, asset.cabinBounds.center.z);
            cabinLight = go.AddComponent<Light>(); cabinLight.type = LightType.Point;
            cabinLight.color = new Color(.91f, .96f, 1); cabinLight.intensity = 1.35f;
            cabinLight.range = 5; cabinLight.shadows = LightShadows.Soft;
        }

        Material Solid(string shader, Color color)
        {
            var s = shader == "Unlit/Color" ? Resources.Load<Shader>("Tutorial/ElevatorIndicator") : Shader.Find(shader);
            if (s == null) s = Shader.Find("Standard");
            var material = new Material(s) { color = color };
            ownedObjects.Add(material);
            return material;
        }

        void BuildShaft()
        {
            shaftRoot = new GameObject("Frosted wall shaft motion").transform;
            shaftRoot.SetParent(transform, false);
            float rear = asset.cabinBounds.min.z - .65f;
            var backing = Solid("Standard", new Color(.09f, .14f, .17f));
            var rail = Solid("Standard", new Color(.18f, .25f, .29f));
            var glow = Solid("Unlit/Color", new Color(.49f, .76f, .84f));
            Primitive("Shaft rear wall", shaftRoot, new Vector3(0, 2, rear - .5f), new Vector3(4.5f, 20, .1f), backing);
            for (int i = -1; i <= 1; i += 2)
                Primitive("Shaft guide rail", shaftRoot, new Vector3(i * .9f, 2, rear), new Vector3(.07f, 20, .1f), rail);
            for (int i = 0; i < 7; i++)
            {
                var band = new GameObject("Passing shaft landing " + i).transform;
                band.SetParent(shaftRoot, false);
                Primitive("Cross member", band, new Vector3(0, 0, rear), new Vector3(3, .13f, .15f), rail);
                Primitive("Passing shaft light", band, new Vector3(.45f, .3f, rear + .06f), new Vector3(.75f, .075f, .05f), glow);
                shaftSegments.Add(band);
            }
        }

        static void Primitive(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition; go.transform.localScale = localScale;
            Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }

        void RefreshVisuals()
        {
            foreach (var label in labels) label.text = FloorLabel;
            foreach (var arrow in arrows) arrow.gameObject.SetActive(IsTravelling);
            float t = Mathf.Clamp01(RideElapsed / Mathf.Max(.1f, DescentDuration));
            float distance = 18 * (t - Mathf.Sin(t * Mathf.PI * 2) / (Mathf.PI * 2));
            for (int i = 0; i < shaftSegments.Count; i++)
                shaftSegments[i].localPosition = Vector3.up * (Mathf.Repeat(i * 3 + distance + 6, 21) - 6);
            if (cabinLight != null)
                cabinLight.color = Color.Lerp(new Color(.91f, .96f, 1), new Color(1, .91f, .78f),
                    Phase >= ElevatorPhase.OpeningAtDestination ? DoorOpenFraction * .35f : 0);
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnDisplayFontRebuilt;
            EndTransferView();
            foreach (var owned in ownedObjects) if (owned != null) Destroy(owned);
        }
    }
}
