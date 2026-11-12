using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink.Gallery
{
    public sealed class GalleryNikeDoorSocket : MonoBehaviour
    {
        public const float TargetScale = 1.22f;
        static readonly Vector2 Opening = (GalleryNikeCanvas.PaintingSize + Vector2.one * .30f) * TargetScale;
        const float ScaleTolerance = .14f, SeatSeconds = .21f, SplitSeconds = .13f;

        public GalleryNikeCanvas canvas;
        public Renderer[] rails;
        public GameObject[] splitPainting;
        public Renderer splitFlash;
        public AudioSource latchAudio;

        GalleryProp docking;
        Vector3 arrivalPosition, arrivalScale;
        Quaternion arrivalRotation;
        float time;
        bool seated;
        MaterialPropertyBlock feedback;
        AudioClip latchClip;
        int glowState = -1;

        public bool Seated => seated;
        public bool OpenReady => seated && time >= SeatSeconds + SplitSeconds;
        public Vector3 Center => transform.position;

        public static GalleryNikeDoorSocket Install(Transform parent, Transform[] leaves, GalleryNikeCanvas canvas,
            Material gold, Material walnut, Material lining)
        {
            var socketRoot = Group(parent, "Nike door · painting key recess", new Vector3(0, 1.93f, 28.50f));
            var socket = socketRoot.gameObject.AddComponent<GalleryNikeDoorSocket>();
            socket.canvas = canvas;
            socket.rails = new Renderer[4];
            socket.splitPainting = new GameObject[2];
            const float rim = .052f;
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var recess = Group(leaves[i], "Recess and brass guides · " + (i == 0 ? "left" : "right"),
                    leaves[i].InverseTransformPoint(socket.Center + Vector3.right * (side * Opening.x / 4)));
                Block(recess, "Dark felt inset", new Vector3(0, 0, .101f), new Vector3(Opening.x / 2 - .018f, Opening.y, .035f), lining);
                socket.rails[i * 2] = Block(recess, "Vertical brass socket rail", new Vector3(side * Opening.x / 4, 0, .045f),
                    new Vector3(rim, Opening.y + rim, .075f), gold);
                // One horizontal rail remains in each leaf's corner. Together they frame the rectangular seat.
                socket.rails[i * 2 + 1] = Block(recess, "Horizontal brass socket rail", new Vector3(0, side * Opening.y / 2, .045f),
                    new Vector3(Opening.x / 2, rim, .075f), gold);
                Block(recess, "Opposite brass rebate", new Vector3(0, -side * Opening.y / 2, .071f),
                    new Vector3(Opening.x / 2, rim * .69f, .050f), gold);

                Vector2 image = GalleryNikeCanvas.PaintingSize * TargetScale;
                var half = Group(leaves[i], i == 0 ? "Split painting · left half" : "Split painting · right half",
                    leaves[i].InverseTransformPoint(socket.Center + Vector3.right * (side * image.x / 4)));
                Block(half, "Split walnut backing", new Vector3(side * .045f, 0, 0),
                    new Vector3(image.x / 2 + .09f, Opening.y - .010f, .111f), walnut);
                ArtworkHalf(half, image, i, canvas.painting.sharedMaterial);
                foreach (int edge in new[] { -1, 1 })
                    Block(half, "Gilt top and bottom moulding", new Vector3(side * .045f, edge * (image.y / 2 + .079f), -.083f),
                        new Vector3(image.x / 2 + .09f, .146f, .087f), gold);
                Block(half, "Gilt outside moulding", new Vector3(side * (image.x / 4 + .079f), 0, -.083f),
                    new Vector3(.146f, image.y, .087f), gold);
                socket.splitPainting[i] = half.gameObject;
                half.gameObject.SetActive(false);
            }

            socket.splitFlash = Block(socketRoot, "Fine light in the split seam", Vector3.back * .157f,
                new Vector3(.022f, Opening.y * .94f, .024f), gold);
            socket.splitFlash.shadowCastingMode = ShadowCastingMode.Off;
            socket.splitFlash.gameObject.SetActive(false);
            socket.latchAudio = socketRoot.gameObject.AddComponent<AudioSource>();
            socket.latchAudio.playOnAwake = false;
            socket.latchAudio.spatialBlend = .6f;
            socket.latchAudio.minDistance = 3;
            return socket;
        }

        public bool Targeted(Camera view)
        {
            if (!canvas.Complete || docking != null || seated) return false;
            Vector3 origin = view.transform.position, direction = view.transform.forward;
            if (origin.z >= Center.z || direction.z < .02f) return false;
            float distance = (Center.z - origin.z) / direction.z;
            Vector3 offset = origin + direction * distance - Center;
            return Mathf.Abs(offset.x) <= Opening.x / 2 && Mathf.Abs(offset.y) <= Opening.y / 2;
        }

        public string FitHint(GalleryProp prop)
        {
            float scale = prop.transform.localScale.x;
            if (scale < TargetScale * (1 - ScaleTolerance)) return "홈보다 작아요. 문에서 조금 물러나 그림을 키우세요.";
            if (scale > TargetScale * (1 + ScaleTolerance)) return "홈보다 커요. 문에 조금 다가가 그림을 줄이세요.";
            Vector3 offset = prop.transform.position - Center;
            if (Mathf.Abs(offset.x) > .44f || Mathf.Abs(offset.y) > .46f || Mathf.Abs(offset.z) > .59f)
                return "문 중앙의 홈을 조준해 그림을 가까이 맞추세요.";
            return null;
        }

        public void BeginDock(GalleryProp prop)
        {
            docking = prop;
            arrivalPosition = prop.transform.position;
            arrivalRotation = prop.transform.rotation;
            arrivalScale = prop.transform.localScale;
            time = 0;
            PropReleasePhysics.Attach(prop.body).CancelPendingRelease();
            prop.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            prop.body.isKinematic = true;
            prop.body.useGravity = false;
            foreach (var part in prop.parts) part.enabled = false;
            SetGuideGlow(2);
        }

        public void Tick(float dt, GalleryGame game)
        {
            if (docking == null && !seated)
            {
                bool aiming = game.held == canvas.portablePainting && Targeted(game.view);
                SetGuideGlow(aiming ? FitHint(game.held) == null ? 2 : 1 : 0);
                return;
            }

            time += dt;
            if (docking != null)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / SeatSeconds));
                docking.transform.SetPositionAndRotation(Vector3.Lerp(arrivalPosition, Center, t), Quaternion.Slerp(arrivalRotation, Quaternion.identity, t));
                docking.transform.localScale = Vector3.Lerp(arrivalScale, Vector3.one * TargetScale, t);
                if (time < SeatSeconds) return;

                docking.gameObject.SetActive(false);
                var key = docking;
                game.props = Array.FindAll(game.props, prop => prop != key);
                docking = null;
                foreach (var half in splitPainting) half.SetActive(true);
                splitFlash.gameObject.SetActive(true);
                if (!MuseumAudio.TryPlay("nike.unlock", this, Center, .48f) && !MuseumAudio.Available)
                {
                    if (latchClip == null) latchClip = MakeLatchClip();
                    latchAudio.PlayOneShot(latchClip, .64f);
                }
                seated = true;
            }
            float remaining = 1 - Mathf.Clamp01((time - SeatSeconds) / SplitSeconds);
            if (splitFlash.gameObject.activeSelf)
            {
                feedback ??= new MaterialPropertyBlock();
                feedback.SetColor("_EmissionColor", new Color(1.7f, .77f, .24f) * remaining);
                splitFlash.SetPropertyBlock(feedback);
                if (remaining <= 0)
                {
                    splitFlash.gameObject.SetActive(false);
                    SetGuideGlow(0);
                }
            }
        }

        public void ResetSocket()
        {
            MuseumAudio.StopOwner(this);
            docking = null;
            seated = false;
            time = 0;
            foreach (var half in splitPainting) half.SetActive(false);
            splitFlash.gameObject.SetActive(false);
            latchAudio.Stop();
            SetGuideGlow(0);
        }

        void SetGuideGlow(int state)
        {
            if (glowState == state) return;
            glowState = state;
            feedback ??= new MaterialPropertyBlock();
            Color glow = state == 2 ? new Color(.15f, .55f, .33f) : state == 1 ?
                new Color(.39f, .18f, .048f) : new Color(.105f, .064f, .023f);
            feedback.SetColor("_EmissionColor", glow);
            foreach (var rail in rails) rail.SetPropertyBlock(feedback);
        }

        static void ArtworkHalf(Transform parent, Vector2 image, int side, Material material)
        {
            var mesh = new Mesh { name = "Nike door key · split art face" };
            float x = image.x / 4 - .007f, y = image.y / 2;
            mesh.vertices = new[] { new Vector3(-x, -y, 0), new Vector3(x, -y, 0), new Vector3(x, y, 0), new Vector3(-x, y, 0) };
            float u = side * .5f;
            mesh.uv = new[] { new Vector2(u, 0), new Vector2(u + .5f, 0), new Vector2(u + .5f, 1), new Vector2(u, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = Group(parent, "Half of the original oil painting", Vector3.back * .063f).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Transform Group(Transform parent, string name, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = position;
            return item;
        }

        static Renderer Block(Transform parent, string name, Vector3 position, Vector3 size, Material material) =>
            GalleryConceptArchitecture.Block(parent, name, position, size, material, .012f);

        static AudioClip MakeLatchClip()
        {
            const int rate = 24000;
            var samples = new float[rate / 4];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float a = Mathf.Sin(2 * Mathf.PI * 132 * t) * Mathf.Exp(-t * 33);
                float b = Mathf.Sin(2 * Mathf.PI * 710 * t) * Mathf.Exp(-t * 26);
                float click = Mathf.Max(0, t - .085f);
                float c = Mathf.Sin(2 * Mathf.PI * 940 * click) * Mathf.Exp(-click * 46);
                samples[i] = (.37f * a + .23f * b + .18f * c) * Mathf.Min(1, t * 600);
            }
            var clip = AudioClip.Create("Nike · brass lock and split", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnDestroy()
        {
            MuseumAudio.StopOwner(this);
            if (latchClip != null) Destroy(latchClip);
        }

        void OnDisable()
        {
            MuseumAudio.StopOwner(this);
            if (latchAudio != null) latchAudio.Stop();
        }
    }
}
