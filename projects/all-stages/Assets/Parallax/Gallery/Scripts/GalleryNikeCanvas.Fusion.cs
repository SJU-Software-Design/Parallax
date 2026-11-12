using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed partial class GalleryNikeCanvas
    {
        enum FusionPhase { Waiting, Absorbing, Painting, Ejecting, Complete }

        public static readonly Vector2 PaintingSize = new Vector2(1.56f, 2.08f);
        static readonly Vector3 PaintingHome = new Vector3(-4.48f, PaintingCenter, 26.05f);
        static readonly Vector3[] LandingOffsets =
        {
            Vector3.zero, Vector3.left * .9f, Vector3.right * .9f, Vector3.back * .85f,
            new Vector3(-.95f, 0, -.85f), new Vector3(.95f, 0, -.85f)
        };

        const float AbsorbSeconds = .62f, EjectSeconds = .48f, PaintingCenter = 1.12f, EmergingScale = 2.22f;
        public float BrushSeconds = 2.6f;
        public GalleryProp portablePainting;
        public Renderer fusionGlow, wallBacking;
        public AudioSource fusionAudio;

        FusionPhase fusionPhase;
        float phaseTime;
        Vector3 statueStart, statueScale;
        Quaternion statueRotation;
        AudioClip fusionClip, ejectBurstClip;
        bool selectedFusionAudio, localFusionPaused;
        MaterialPropertyBlock fusionProperties;

        float SafeBrushSeconds => Mathf.Max(.1f, BrushSeconds);
        public bool Drawing => fusionPhase == FusionPhase.Painting;
        public bool Fusing => fusionPhase == FusionPhase.Absorbing || Drawing || fusionPhase == FusionPhase.Ejecting;
        public float BrushProgress => Drawing ? Mathf.Clamp01(phaseTime / SafeBrushSeconds) :
            fusionPhase == FusionPhase.Ejecting || fusionPhase == FusionPhase.Complete ? 1 : 0;
        public float FusionProgress
        {
            get
            {
                if (fusionPhase == FusionPhase.Complete) return 1;
                float elapsed = fusionPhase == FusionPhase.Absorbing ? phaseTime :
                    Drawing ? AbsorbSeconds + phaseTime :
                    fusionPhase == FusionPhase.Ejecting ? AbsorbSeconds + SafeBrushSeconds + phaseTime : 0;
                return Mathf.Clamp01(elapsed / (AbsorbSeconds + SafeBrushSeconds + EjectSeconds));
            }
        }

        void InstallFusion(Transform parent, Material gold, Material walnut, Material linen, Material artwork)
        {
            var portable = Group(parent, "02 · 완성된 니케 그림", PaintingHome);
            portablePainting = portable.gameObject.AddComponent<GalleryProp>();
            portablePainting.title = portable.name;
            portablePainting.zone = 1;
            portablePainting.minScale = .45f;
            var frame = Group(portable, "Portable gilt frame", Vector3.zero);
            var size = PaintingSize;
            Frame(frame, size, gold, walnut);
            var backing = GalleryConceptArchitecture.Block(frame, "Solid canvas and stretcher",
                Vector3.zero, new Vector3(1.70f, 2.22f, .13f), linen, .02f);
            backing.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.70f, 2.22f, .13f);
            Face(frame, "Ejected Nike oil painting", size, artwork).transform.localPosition = Vector3.back * .071f;
            for (int side = -1; side <= 1; side += 2)
            {
                var support = GalleryConceptArchitecture.Block(portable, "Short walnut frame foot",
                    new Vector3(side * .62f, .055f - PaintingCenter, .08f), new Vector3(.16f, .11f, .48f), walnut, .02f);
                support.gameObject.AddComponent<BoxCollider>().size = new Vector3(.16f, .11f, .48f);
            }
            portablePainting.Remember();
            InitializePainting();
            portablePainting.placement.ResetPlaced();
            portable.gameObject.SetActive(false);

            wallBacking = Face(transform, "Empty recessed linen after fusion", Size, linen);
            wallBacking.transform.localPosition = Vector3.forward * .013f;
            wallBacking.gameObject.SetActive(false);
            var material = new Material(Shader.Find("Parallax/GalleryNikeFusion")) { name = "Nike canvas · fusion burst" };
            GalleryWorld.Materials.Add(material);
            fusionGlow = Face(transform, "Fusion flash and expanding light rings", new Vector2(8.4f, 8.4f), material);
            fusionGlow.transform.localPosition = Vector3.back * .17f;
            fusionGlow.gameObject.SetActive(false);
            fusionAudio = gameObject.AddComponent<AudioSource>();
            fusionAudio.playOnAwake = false;
            fusionAudio.spatialBlend = .55f;
            fusionAudio.minDistance = 4;
            fusionAudio.maxDistance = 18;
        }

        public void ResetPuzzle(GalleryGame game)
        {
            StopFusionAudio();
            if (portablePainting == null) { SetComplete(false); return; }
            if (fusionPhase != FusionPhase.Waiting)
            {
                ReplaceProp(game, portablePainting, statue);
                if (Array.IndexOf(game.props, statue) < 0)
                {
                    Array.Resize(ref game.props, game.props.Length + 1);
                    game.props[game.props.Length - 1] = statue;
                }
                statue.gameObject.SetActive(true);
                statue.Restore();
            }
            fusionPhase = FusionPhase.Waiting;
            phaseTime = 0;
            portablePainting.gameObject.SetActive(false);
            fusionGlow.gameObject.SetActive(false);
            wallBacking.gameObject.SetActive(false);
            SetArtworkReveal(0);
            SetComplete(false);
        }

        public void BeginFusion()
        {
            if (fusionPhase != FusionPhase.Waiting) return;
            fusionPhase = FusionPhase.Absorbing;
            phaseTime = 0;
            SetArtworkReveal(0);
            painting.gameObject.SetActive(false);
            statueStart = statue.transform.position;
            statueScale = statue.transform.localScale;
            statueRotation = statue.transform.rotation;
            statue.body.isKinematic = true;
            statue.body.useGravity = false;
            SetCollision(statue, false);
            SetGlow(0, -1);
            fusionGlow.transform.localPosition = Vector3.back * .17f;
            fusionGlow.gameObject.SetActive(true);
            selectedFusionAudio = MuseumAudio.Available;
            if (selectedFusionAudio)
            {
                if (ejectBurstClip == null) ejectBurstClip = MakeEjectBurstClip(SafeBrushSeconds);
                MuseumAudio.TryPlay("nike.shadow_fit", this, transform.position, .58f);
            }
            else
            {
                if (fusionClip == null) fusionClip = MakeFusionClip(SafeBrushSeconds);
                fusionAudio.PlayOneShot(fusionClip, .58f);
            }
        }

        public void TickFusion(float dt, GalleryGame game)
        {
            if (!Fusing) return;
            phaseTime += Mathf.Max(0, dt);
            if (fusionPhase == FusionPhase.Absorbing)
            {
                float t = Mathf.Clamp01(phaseTime / AbsorbSeconds);
                float pull = t * t;
                Vector3 destination = transform.position - Vector3.forward * .17f - Vector3.up * .18f;
                statue.transform.SetPositionAndRotation(Vector3.Lerp(statueStart, destination, pull) +
                    Vector3.up * (.90f * Mathf.Sin(t * Mathf.PI)), statueRotation * Quaternion.Euler(-18 * t, 115 * t, -12 * t));
                statue.transform.localScale = statueScale * Mathf.Lerp(1, .055f, pull);
                SetGlow(t, -1);
                if (t < 1) return;

                statue.gameObject.SetActive(false);
                // Paint on the wall before the portable frame is enabled.
                // The guide remains below the transparent brush layer.
                painting.gameObject.SetActive(true);
                fusionGlow.gameObject.SetActive(false);
                fusionPhase = FusionPhase.Painting;
                phaseTime -= AbsorbSeconds;
                if (selectedFusionAudio)
                    MuseumAudio.TryPlay("nike.paint", this, transform.position, .52f);
                game.Tell("그림자가 붓결을 따라 그림으로 남습니다.", SafeBrushSeconds + 1);
            }

            if (Drawing)
            {
                SetArtworkReveal(BrushProgress);
                if (BrushProgress < 1) return;

                SetArtworkReveal(1);
                painting.gameObject.SetActive(false);
                guide.gameObject.SetActive(false);
                wallBacking.gameObject.SetActive(true);
                portablePainting.gameObject.SetActive(true);
                InitializePainting();
                portablePainting.placement.SetPlayer(game.controller);
                portablePainting.Restore();
                SetCollision(portablePainting, false);
                fusionGlow.gameObject.SetActive(true);
                fusionPhase = FusionPhase.Ejecting;
                phaseTime -= SafeBrushSeconds;
                if (selectedFusionAudio)
                {
                    // Keep the exact previously approved frame-ejection timbre;
                    // the old inward-charge part is no longer played in this profile.
                    fusionAudio.PlayOneShot(ejectBurstClip, .58f);
                }
            }

            float flight = Mathf.Clamp01(phaseTime / EjectSeconds);
            float arc = Mathf.Sin(flight * Mathf.PI);
            float travel = 1 - (1 - flight) * (1 - flight);
            Vector3 opening = transform.position - Vector3.forward * .19f;
            Vector3 position = Vector3.Lerp(opening, PaintingHome, travel) + Vector3.up * (.72f * arc);
            portablePainting.transform.SetPositionAndRotation(position, Quaternion.Euler(-18 * arc, 13 * arc, -7 * arc));
            portablePainting.transform.localScale = Vector3.one * Mathf.Lerp(EmergingScale, 1, travel);
            Vector3 front = portablePainting.transform.TransformPoint(new Vector3(0, 0, -.14f));
            fusionGlow.transform.position = Vector3.Lerp(front, transform.position - Vector3.forward * .2f,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .65f, flight)));
            SetGlow(0, flight);
            if (flight < 1 || !TryLand(game)) return;

            fusionPhase = FusionPhase.Complete;
            fusionGlow.gameObject.SetActive(false);
            game.Tell(game.NikeKeyPuzzle ? "그림을 집어 문 중앙의 홈에 크기를 맞춰 끼우세요." : "그림을 세워 문 앞의 빛을 가리세요.", 7);
        }

        bool TryLand(GalleryGame game)
        {
            foreach (var offset in LandingOffsets)
            {
                Vector3 landing = PaintingHome + offset;
                portablePainting.transform.position = landing;
                Physics.SyncTransforms();
                if (!game.ValidPlacement(portablePainting, out _)) continue;
                portablePainting.body.position = landing;
                portablePainting.body.rotation = Quaternion.identity;
                SetCollision(portablePainting, true);
                Physics.SyncTransforms();
                portablePainting.placement.ResetPlaced();
                portablePainting.GetComponent<PropReleasePhysics>().PlayImpact(1.4f);
                ReplaceProp(game, statue, portablePainting);
                return true;
            }
            portablePainting.transform.position = PaintingHome + Vector3.up * .22f;
            return false;
        }

        static void ReplaceProp(GalleryGame game, GalleryProp from, GalleryProp to)
        {
            int index = Array.IndexOf(game.props, from);
            if (index >= 0) game.props[index] = to;
        }

        static void SetCollision(GalleryProp prop, bool enabled)
        {
            foreach (var part in prop.parts) part.enabled = enabled;
        }

        void InitializePainting()
        {
            portablePainting.InitializePhysics();
            portablePainting.placement.PreviewWhileHeld = false;
            portablePainting.placement.PreviewUntilSettled = false;
        }

        void SetGlow(float charge, float pulse)
        {
            fusionProperties ??= new MaterialPropertyBlock();
            fusionProperties.SetFloat("_Charge", charge);
            fusionProperties.SetFloat("_Pulse", pulse);
            fusionGlow.SetPropertyBlock(fusionProperties);
        }

        void SetArtworkReveal(float progress)
        {
            // This material is also used by the portable frame and the two
            // door halves, so they retain the fully painted artwork at 1.
            if (painting != null && painting.sharedMaterial != null)
                painting.sharedMaterial.SetFloat("_Reveal", Mathf.Clamp01(progress));
        }

        static AudioClip MakeFusionClip(float brushSeconds)
        {
            const int rate = 24000;
            float burstAt = AbsorbSeconds + brushSeconds;
            var samples = new float[Mathf.CeilToInt((burstAt + EjectSeconds) * rate)];
            var noise = new System.Random(612);
            float phase = 0, filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2 - 1, .22f);
                if (time < AbsorbSeconds)
                {
                    float rise = time / AbsorbSeconds;
                    phase += (80 + 240 * rise * rise) / rate;
                    samples[i] = Mathf.Sin(phase * 2 * Mathf.PI) * .20f * Mathf.Sin(rise * Mathf.PI / 2) * rise
                        + filtered * .16f * rise * rise;
                }
                else if (time >= burstAt)
                {
                    float burst = time - burstAt;
                    float decay = Mathf.Exp(-burst * 15) * Mathf.Clamp01(burst * 260);
                    samples[i] = (Mathf.Sin(2 * Mathf.PI * (82 * burst - 70 * burst * burst)) * .58f + filtered * .48f) * decay;
                }
            }
            var clip = AudioClip.Create("Nike canvas · inward charge and burst", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip MakeEjectBurstClip(float brushSeconds)
        {
            // Extract the original generator's tail instead of recreating its
            // noise/phase state, so the user's retained "poong" is sample-identical.
            var original = MakeFusionClip(brushSeconds);
            var whole = new float[original.samples];
            original.GetData(whole, 0);
            int start = Mathf.Clamp(Mathf.CeilToInt((AbsorbSeconds + brushSeconds) * original.frequency), 0, whole.Length - 1);
            int count = Mathf.Min(Mathf.CeilToInt(EjectSeconds * original.frequency), whole.Length - start);
            var samples = new float[count];
            Array.Copy(whole, start, samples, 0, count);
            var burst = AudioClip.Create("Nike canvas · retained frame ejection", count, 1, original.frequency, false);
            burst.SetData(samples, 0);
            if (Application.isPlaying) Destroy(original); else DestroyImmediate(original);
            return burst;
        }

        void StopFusionAudio()
        {
            MuseumAudio.StopOwner(this);
            if (fusionAudio != null) fusionAudio.Stop();
            selectedFusionAudio = localFusionPaused = false;
        }

        void LateUpdate()
        {
            // The preserved frame burst uses the original local source. Match
            // the selected bank's pause policy without restarting its samples.
            if (!selectedFusionAudio || !MuseumAudio.Available || fusionAudio == null) return;
            if (!MuseumAudio.Instance.GameplayRunning && fusionAudio.isPlaying)
            { fusionAudio.Pause(); localFusionPaused = true; }
            else if (MuseumAudio.Instance.GameplayRunning && localFusionPaused)
            { fusionAudio.UnPause(); localFusionPaused = false; }
        }

        void OnDisable() { StopFusionAudio(); }

        void OnDestroy()
        {
            StopFusionAudio();
            if (fusionClip != null) Destroy(fusionClip);
            if (ejectBurstClip != null) Destroy(ejectBurstClip);
        }
    }
}
