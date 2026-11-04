using UnityEngine;

namespace ShadeLink.Gallery
{
    // The scan keeps its original pose while small. A slight parting of the
    // palms makes a genuine human-sized opening after enlargement and settling.
    public sealed class GalleryCathedralActivation : MonoBehaviour
    {
        public GalleryOrcusExit entrance;
        public GalleryCathedralPortal portal;
        public SkinnedMeshRenderer[] morphTargets;
        public BoxCollider[] movingParts;
        public Vector3[] closedCenters, openedCenters;
        public Bounds closedOpening, openedOpening;
        [Min(.1f)] public float duration = .65f;
        public float Progress { get; private set; }
        public bool Opened => Progress >= .999f;
        public bool OpeningBlocked { get; private set; }
        Vector3[] probeCenters;
        bool openingApproved;

        void LateUpdate()
        {
            if (entrance == null || entrance.game == null || entrance.game.Transitioning) return;
            bool eligible = entrance.ScaleAndPlacementReady;
            if (!eligible) openingApproved = false;
            else if (!openingApproved) openingApproved = CanOpenAtCurrentPlacement();
            OpeningBlocked = eligible && !openingApproved;
            float target = eligible && !OpeningBlocked ? 1 : 0;
            float next = Mathf.MoveTowards(Progress, target, Time.unscaledDeltaTime / duration);
            if (Mathf.Approximately(next, Progress)) return;
            SetProgress(next);
        }

        public void ResetToRest() { openingApproved = false; OpeningBlocked = false; SetProgress(0); }

        public bool CanOpenAtCurrentPlacement()
        {
            if (entrance == null || entrance.game == null || entrance.prop == null || movingParts == null ||
                openedCenters == null || openedCenters.Length != movingParts.Length) return false;
            if (probeCenters == null || probeCenters.Length != movingParts.Length) probeCenters = new Vector3[movingParts.Length];
            // Probe the complete opened envelope before animating near a wall.
            // Queries ignore this prop's own shapes; no physics step occurs
            // while the temporary authored centres are being inspected.
            for (int i = 0; i < movingParts.Length; i++)
                if (movingParts[i] != null) { probeCenters[i] = movingParts[i].center; movingParts[i].center = openedCenters[i]; }
            try { return entrance.game.ValidPlacement(entrance.prop, out _); }
            finally
            {
                for (int i = 0; i < movingParts.Length; i++) if (movingParts[i] != null) movingParts[i].center = probeCenters[i];
                Physics.SyncTransforms();
            }
        }

        void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            float blend = Mathf.SmoothStep(0, 1, Progress);
            if (morphTargets != null)
                foreach (var renderer in morphTargets)
                    if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                        renderer.SetBlendShapeWeight(0, blend * 100);
            if (movingParts != null && closedCenters != null && openedCenters != null)
                for (int i = 0; i < movingParts.Length && i < closedCenters.Length && i < openedCenters.Length; i++)
                    if (movingParts[i] != null) movingParts[i].center = Vector3.Lerp(closedCenters[i], openedCenters[i], blend);
            if (portal != null && closedOpening.size.x > 0 && openedOpening.size.x > 0)
                portal.apertureBounds = new Bounds(Vector3.Lerp(closedOpening.center, openedOpening.center, blend),
                    Vector3.Lerp(closedOpening.size, openedOpening.size, blend));
            Physics.SyncTransforms();
        }
    }
}
