using UnityEngine;

namespace ShadeLink.Eclipse
{
    public enum MoonAlignmentFailure { None, InvalidGeometry, TooLow, IncompleteCoverage }

    public struct MoonAlignmentSample
    {
        public bool GeometryValid, CanAnchor, Complete;
        public float Coverage, RealRadiusRatio, EffectiveRadiusRatio;
        public float SolarRadiusDegrees, CentreErrorDegrees, ViewErrorDegrees, FloorClearance;
        public Vector3 ObserverPosition, SolarPosition, MoonPosition;
        public MoonAlignmentFailure Failure;
    }

    // Carried size follows the original perspective rule. A released Moon only
    // needs to cover enough of the Sun; a bright rim and larger moons are valid.
    public static class MoonAlignmentRules
    {
        public const float EffectiveSilhouetteFactor = .97f;
        public const float RequiredCoverage = .70f;
        public const float MinimumFloatClearance = .015f;
        const float AssistViewFraction = .60f;
        public const float AssistTurnFraction = .08f;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        public static float SolarAngularRadius(Vector3 eye, Vector3 sun, float solarRadius)
        {
            float distance = Vector3.Distance(eye, sun);
            if (!Finite(eye) || !Finite(sun) || !Finite(solarRadius) || solarRadius <= 0 || distance <= solarRadius) return 0;
            return Mathf.Asin(Mathf.Clamp01(solarRadius / distance));
        }

        public static void AssistHeldTarget(Vector3 eye, Vector3 viewForward, Vector3 sun, float solarRadius,
            ref Vector3 moonTarget, float scale)
        {
            float solarAngle = SolarAngularRadius(eye, sun, solarRadius);
            Vector3 toMoon = moonTarget - eye, toSun = sun - eye;
            float distance = toMoon.magnitude;
            if (solarAngle <= 0 || distance <= .001f || distance >= toSun.magnitude || viewForward.sqrMagnitude < .00001f) return;
            float effectiveAngle = Mathf.Asin(Mathf.Clamp01(scale * .5f * EffectiveSilhouetteFactor / distance));
            float ratio = effectiveAngle / solarAngle;
            if (ratio < .94f) return;
            float viewError = Vector3.Angle(viewForward, toSun) * Mathf.Deg2Rad;
            float assistCone = solarAngle * AssistViewFraction;
            if (viewError >= assistCone) return;
            float weight = Mathf.SmoothStep(0, 1, 1 - viewError / assistCone);
            Vector3 direction = Vector3.RotateTowards(toMoon / distance, toSun.normalized,
                solarAngle * AssistTurnFraction * weight, 0);
            moonTarget = eye + direction * distance;
        }

        public static MoonAlignmentSample Evaluate(Vector3 eye, Vector3 viewForward, Vector3 moon, float moonRadius,
            Vector3 sun, float solarRadius, float trueCoverage)
        {
            var result = new MoonAlignmentSample
            {
                ObserverPosition = eye, SolarPosition = sun, MoonPosition = moon,
                Coverage = Finite(trueCoverage) ? Mathf.Clamp01(trueCoverage) : 0,
                FloorClearance = moon.y - moonRadius,
                Failure = MoonAlignmentFailure.InvalidGeometry
            };
            Vector3 toMoon = moon - eye, toSun = sun - eye;
            float distance = toMoon.magnitude, sunDistance = toSun.magnitude;
            if (!Finite(eye) || !Finite(moon) || !Finite(sun) || !Finite(viewForward) ||
                !Finite(moonRadius) || !Finite(solarRadius) || moonRadius <= 0 || solarRadius <= 0 ||
                distance <= moonRadius || distance >= sunDistance || sunDistance <= solarRadius || viewForward.sqrMagnitude < .00001f) return result;
            float solarAngle = Mathf.Asin(Mathf.Clamp01(solarRadius / sunDistance));
            float realAngle = Mathf.Asin(Mathf.Clamp01(moonRadius / distance));
            float effectiveAngle = Mathf.Asin(Mathf.Clamp01(moonRadius * EffectiveSilhouetteFactor / distance));
            result.GeometryValid = true;
            result.RealRadiusRatio = realAngle / solarAngle;
            result.EffectiveRadiusRatio = effectiveAngle / solarAngle;
            result.SolarRadiusDegrees = solarAngle * Mathf.Rad2Deg;
            result.CentreErrorDegrees = Vector3.Angle(toMoon, toSun);
            result.ViewErrorDegrees = Vector3.Angle(viewForward, toSun);
            if (result.FloorClearance < MinimumFloatClearance) { result.Failure = MoonAlignmentFailure.TooLow; return result; }
            if (result.Coverage < RequiredCoverage)
            { result.Failure = MoonAlignmentFailure.IncompleteCoverage; return result; }
            result.CanAnchor = result.Complete = true;
            result.Failure = MoonAlignmentFailure.None;
            return result;
        }
    }
}
