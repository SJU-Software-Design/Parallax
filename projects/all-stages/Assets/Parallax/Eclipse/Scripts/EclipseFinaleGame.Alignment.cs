using UnityEngine;

namespace ShadeLink.Eclipse
{
    public sealed partial class EclipseFinaleGame
    {
        MoonAlignmentSample moonReleaseSnapshot, savedMoonReleaseSnapshot;
        bool hasMoonReleaseSnapshot, savedHasMoonReleaseSnapshot;
        public bool MoonAlignmentConfirmed => hasMoonReleaseSnapshot && moonReleaseSnapshot.Complete;
        public bool HasMoonReleaseSnapshot => hasMoonReleaseSnapshot;
        public MoonAlignmentSample ReleasedMoonAlignment => moonReleaseSnapshot;
        public MoonAlignmentSample CurrentMoonAlignment => view == null || moon == null ? default :
            MoonAlignmentRules.Evaluate(view.transform.position, view.transform.forward, moon.position, MoonRadius,
                SunPosition, SunRadius, CoverageAt(view.transform.position));

        public string MoonAlignmentHint
        {
            get
            {
                if (MoonPlacementBlocked) return "이 방향에는 달을 놓을 공간이 없습니다. 조준 방향을 바꿔 보세요.";
                if (MoonFloorConstrained) return "바닥을 따라 달을 움직이고 있습니다. 위를 바라보면 다시 들어 올릴 수 있습니다.";
                MoonAlignmentSample sample = held == FinaleItem.Moon ? CurrentMoonAlignment : moonReleaseSnapshot;
                if (sample.Complete) return held == FinaleItem.Moon
                    ? "태양이 충분히 가려졌습니다. 달을 놓아 보세요." : "달이 태양을 가렸습니다.";
                return "태양을 조금 더 가려 보세요.";
            }
        }

        void ApplyMoonHeldAlignment(ref Vector3 target, float targetScale)
        {
            MoonAlignmentRules.AssistHeldTarget(view.transform.position, view.transform.forward,
                SunPosition, SunRadius, ref target, targetScale);
        }

        void BeginMoonAlignmentGrab()
        {
            savedMoonReleaseSnapshot = moonReleaseSnapshot;
            savedHasMoonReleaseSnapshot = hasMoonReleaseSnapshot;
            hasMoonReleaseSnapshot = false;
            moonReleaseSnapshot = default;
        }

        bool CaptureMoonAlignmentRelease()
        {
            // Store all observer/Sun/shape measurements NOW. Moving the camera,
            // walking, or lowering the cinematic Sun must not rejudge a solve.
            moonReleaseSnapshot = CurrentMoonAlignment;
            hasMoonReleaseSnapshot = true;
            return moonReleaseSnapshot.CanAnchor;
        }

        void CancelMoonAlignmentGrab()
        {
            moonReleaseSnapshot = savedMoonReleaseSnapshot;
            hasMoonReleaseSnapshot = savedHasMoonReleaseSnapshot;
        }

        void ResetMoonAlignment()
        {
            hasMoonReleaseSnapshot = savedHasMoonReleaseSnapshot = false;
            moonReleaseSnapshot = savedMoonReleaseSnapshot = default;
        }
    }
}
