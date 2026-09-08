namespace Elemental.Input.Gestures
{
    public static class EarthPlatformDrawFeedback
    {
        // Rejection-only allocation; successful input and preview keep their existing path.
        public static string ForArea(float area, float minimum, float maximum)
        {
            if (!float.IsFinite(area) || area <= 0)
                return "Platform outline has no usable area. Draw a closed outline over the surface.";
            if (area < minimum)
                return $"Platform footprint is too small ({area:0.00} m²). Draw a larger outline, at least {minimum:0.0} m².";
            if (area > maximum)
                return $"Platform footprint is too large ({area:0.0} m²). Draw a smaller outline, at most {maximum:0} m².";
            return null;
        }
    }
}
