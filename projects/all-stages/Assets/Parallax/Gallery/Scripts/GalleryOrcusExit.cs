using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    /// <summary>A movable exhibit: the aperture becomes traversable only after enlargement and placement.</summary>
    public sealed class GalleryOrcusExit : MonoBehaviour
    {
        public const float MouthHalfWidth = .48f, MouthBottom = .20f, MouthTop = 1.23f, MouthPlane = -.53f;
        public GalleryGame game;
        public GalleryProp prop;
        public Renderer portal;
        // Legacy Orcus scenes keep their original opening. Other sculptures can
        // share the same placement/entry policy with an authored clear aperture.
        public bool customOpening, windEnabled = true;
        public float openingCenterX, openingHalfWidth = MouthHalfWidth;
        public float openingBottom = MouthBottom, openingTop = MouthTop, openingPlane = MouthPlane;
        public float approachStart = -1.35f;
        public float requiredScaleOverride, entryFootOffset = .025f;
        public GalleryCathedralActivation cathedralActivation;
        public float EntryCenterX => customOpening ? openingCenterX : 0;
        public float EntryHalfWidth => customOpening ? openingHalfWidth : MouthHalfWidth;
        public float EntryBottom => customOpening ? openingBottom : MouthBottom;
        public float EntryTop => customOpening ? openingTop : MouthTop;
        public float EntryPlane => customOpening ? openingPlane : MouthPlane;
        public bool Entering { get; private set; }
        public event Action<GalleryOrcusExit> Entered;
        public Vector3 MouthPosition => transform.TransformPoint(new Vector3(EntryCenterX, (EntryBottom + EntryTop) * .5f, EntryPlane));
        public Vector3 EntranceForward => -transform.forward;
        public float RequiredScale => requiredScaleOverride > 0 ? requiredScaleOverride : game == null || game.controller == null ? 2 :
            Mathf.Max((game.controller.height + .14f) / (EntryTop - EntryBottom),
                (game.controller.radius * 2 + .14f) / (EntryHalfWidth * 2));
        public bool ScaleAndPlacementReady => !Entering && game != null && prop != null && prop.placement != null &&
            prop.placement.Ready && !prop.placement.Held && game.held != prop &&
            Vector3.Dot(transform.up, Vector3.up) > .97f &&
            Mathf.Min(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= RequiredScale;
        public bool IsReadyForEntry => ScaleAndPlacementReady && (cathedralActivation == null || !cathedralActivation.Assisting && cathedralActivation.Opened);

        AudioSource wind;
        static AudioClip windClip;
        static int windUsers;
        bool ownsWind, tracked, paused;
        Vector3 previousFeet;
        MaterialPropertyBlock properties;
        float pulse;

        void Start() { EnsureWind(); }
        public void ResetEntrance() { Entering = false; tracked = false; pulse = 0; }

        public static float WindVolumeAtDistance(float distance)
        {
            float near = Mathf.Clamp01(1 - Mathf.Max(0, distance - .5f) / 13.5f);
            return .32f * near * near * (3 - 2 * near);
        }

        public bool CanEnterFrom(Vector3 fromFeet, Vector3 toFeet)
        {
            if (!IsReadyForEntry || game.controller == null || Vector3.Distance(fromFeet, toFeet) > 2) return false;
            Vector3 before = transform.InverseTransformPoint(fromFeet), after = transform.InverseTransformPoint(toFeet);
            // Walking across the front plane is intentional entry. A back/side approach and a teleport are not.
            if (before.z >= EntryPlane || after.z < EntryPlane || after.z <= before.z) return false;
            float fraction = (EntryPlane - before.z) / (after.z - before.z);
            Vector3 crossing = Vector3.Lerp(before, after, fraction);
            Vector3 head = transform.InverseTransformPoint(Vector3.Lerp(fromFeet, toFeet, fraction) + Vector3.up * game.controller.height);
            float radius = game.controller.radius / Mathf.Abs(transform.lossyScale.x);
            return Mathf.Abs(crossing.x - EntryCenterX) + radius <= EntryHalfWidth &&
                crossing.y >= EntryBottom - .035f && head.y <= EntryTop;
        }

        public bool TryEnter(Vector3 fromFeet, Vector3 toFeet)
        {
            if (!CanEnterFrom(fromFeet, toFeet)) return false;
            Entering = true; tracked = false;
            Entered?.Invoke(this);
            return true;
        }

        void Update()
        {
            if (game == null || game.controller == null) return;
            bool running = game.active && !game.won && Time.timeScale > 0 && !Entering;
            if (!running) tracked = false;
            else
            {
                Vector3 feet = game.controller.transform.position;
                if (tracked) TryEnter(previousFeet, feet);
                previousFeet = feet; tracked = true;
            }
            UpdateWind(running && !Entering);
        }

        void LateUpdate()
        {
            if (portal == null || game == null || game.view == null) return;
            float distance = Vector3.Distance(game.view.transform.position, MouthPosition);
            bool touched = game.aimed == prop || game.held == prop;
            pulse = Mathf.MoveTowards(pulse, touched ? 1 : 0, Time.unscaledDeltaTime * 2.5f);
            if (properties == null) properties = new MaterialPropertyBlock();
            portal.GetPropertyBlock(properties);
            properties.SetFloat("_Pulse", pulse);
            properties.SetFloat("_Near", Mathf.Clamp01(1 - distance / 8));
            portal.SetPropertyBlock(properties);
        }

        void EnsureWind()
        {
            if (!windEnabled) return;
            if (wind != null) return;
            if (windClip == null) windClip = MakeWindClip();
            windUsers++; ownsWind = true;
            var source = new GameObject("Orcus mouth · spatial wind");
            source.transform.SetParent(transform, false);
            source.transform.localPosition = new Vector3(0, .76f, MouthPlane);
            wind = source.AddComponent<AudioSource>();
            wind.clip = windClip; wind.loop = true; wind.playOnAwake = false;
            wind.spatialBlend = 1; wind.dopplerLevel = 0; wind.priority = 180;
            wind.minDistance = 1.2f; wind.maxDistance = 18;
            wind.rolloffMode = AudioRolloffMode.Linear; wind.volume = 0;
        }

        void UpdateWind(bool running)
        {
            if (!windEnabled)
            {
                if (wind != null && wind.isPlaying) wind.Stop();
                return;
            }
            EnsureWind();
            if (!running || game.view == null)
            {
                if (wind.isPlaying) { wind.Pause(); paused = true; }
                wind.volume = 0; return;
            }
            if (!wind.isPlaying)
            {
                if (paused) wind.UnPause(); else wind.Play();
                paused = false;
            }
            float target = WindVolumeAtDistance(Vector3.Distance(game.view.transform.position, MouthPosition));
            wind.volume = Mathf.MoveTowards(wind.volume, target, Time.unscaledDeltaTime * .3f);
        }

        static AudioClip MakeWindClip()
        {
            const int rate = 22050, count = rate * 4, seam = rate / 4;
            var random = new System.Random(73191);
            var raw = new float[count + seam];
            float fast = 0, slow = 0, maximum = .001f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)random.NextDouble() * 2 - 1;
                fast = fast * .91f + white * .09f; slow = slow * .996f + white * .004f;
                raw[i] = (fast * .82f + slow * 1.8f) * (.72f + .12f * Mathf.Sin(i * .00013f));
                maximum = Mathf.Max(maximum, Mathf.Abs(raw[i]));
            }
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float value = raw[i];
                if (i < seam) value = Mathf.Lerp(raw[count + i], value, Mathf.SmoothStep(0, 1, (float)i / seam));
                samples[i] = value / maximum * .72f;
            }
            var clip = AudioClip.Create("Orcus · seamless soft air", count, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        void OnDisable() { if (wind != null) { wind.Stop(); paused = false; } tracked = false; }
        void OnDestroy()
        {
            if (ownsWind && --windUsers == 0 && windClip != null) { Destroy(windClip); windClip = null; }
        }
    }
}
