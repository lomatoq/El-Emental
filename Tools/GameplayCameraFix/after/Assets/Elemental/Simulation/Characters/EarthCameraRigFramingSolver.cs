using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Pure Cinemachine third-person vertical-rig composition.</summary>
    public static class EarthCameraRigFramingSolver
    {
        public static float ResolveVerticalArmLength(
            float desiredCameraHeight,
            float trackingHeight,
            float shoulderHeight,
            float cameraDistance,
            float downwardPitchDegrees,
            float minimumArmLength,
            float maximumArmLength)
        {
            if (!math.isfinite(desiredCameraHeight) || !math.isfinite(trackingHeight) ||
                !math.isfinite(shoulderHeight) || !math.isfinite(cameraDistance) ||
                !math.isfinite(downwardPitchDegrees))
                return math.max(0f, minimumArmLength);

            float minimum = math.max(0f, minimumArmLength);
            float maximum = math.max(minimum, maximumArmLength);
            float radians = math.radians(math.clamp(downwardPitchDegrees, -80f, 80f));
            float armUp = math.cos(radians);
            if (math.abs(armUp) < .01f) return minimum;
            float distanceLift = math.max(0f, cameraDistance) * math.sin(radians);
            float arm = (desiredCameraHeight - trackingHeight - shoulderHeight - distanceLift) / armUp;
            return math.clamp(arm, minimum, maximum);
        }

        public static float ResolveCameraHeight(
            float trackingHeight,
            float shoulderHeight,
            float verticalArmLength,
            float cameraDistance,
            float downwardPitchDegrees)
        {
            float radians = math.radians(math.clamp(downwardPitchDegrees, -80f, 80f));
            return trackingHeight + shoulderHeight +
                   verticalArmLength * math.cos(radians) +
                   math.max(0f, cameraDistance) * math.sin(radians);
        }
    }
}
