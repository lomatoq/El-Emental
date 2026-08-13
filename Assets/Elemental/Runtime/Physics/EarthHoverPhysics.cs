using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthHoverFrame
    {
        public EarthHoverFrame(Quaternion levelRotation, Vector3 localUp, float phase)
        {
            LevelRotation = levelRotation;
            LocalUp = localUp;
            Phase = phase;
        }

        public Quaternion LevelRotation { get; }
        public Vector3 LocalUp { get; }
        public float Phase { get; }
    }

    public static class EarthHoverPhysics
    {
        public static EarthHoverFrame Capture(Rigidbody body, Vector3 localUp, uint stableId)
        {
            Vector3 up = SafeUp(localUp, body != null ? body.transform.up : Vector3.up);
            Quaternion rotation = body != null ? body.rotation : Quaternion.identity;
            Quaternion level = Quaternion.FromToRotation(rotation * Vector3.up, up) * rotation;
            float phase = Hash01(stableId ^ 0xA53C91u) * Mathf.PI * 2f;
            return new EarthHoverFrame(level, up, phase);
        }

        public static Vector3 BobOffset(
            in EarthHoverFrame frame,
            Vector3 currentUp,
            float time,
            EarthHoverProfile profile)
        {
            Vector3 up = SafeUp(currentUp, frame.LocalUp);
            float amplitude = profile != null ? profile.BobAmplitude : 0.055f;
            float frequency = profile != null ? profile.BobFrequency : 1.25f;
            return up * (Mathf.Sin((time * frequency * Mathf.PI * 2f) + frame.Phase) * amplitude);
        }

        public static void Stabilize(
            Rigidbody body,
            in EarthHoverFrame frame,
            Vector3 currentUp,
            float time,
            EarthHoverProfile profile)
        {
            if (body == null || body.isKinematic) return;
            Vector3 up = SafeUp(currentUp, frame.LocalUp);
            Quaternion transported = Quaternion.FromToRotation(frame.LocalUp, up) * frame.LevelRotation;
            float yawDegrees = profile != null ? profile.IdleYawDegrees : 3.5f;
            float yawFrequency = profile != null ? profile.IdleYawFrequency : 0.45f;
            float yaw = Mathf.Sin((time * yawFrequency * Mathf.PI * 2f) + frame.Phase) * yawDegrees;
            Quaternion desired = Quaternion.AngleAxis(yaw, up) * transported;
            Quaternion error = desired * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            if (!float.IsFinite(axis.x) || axis.sqrMagnitude < 0.0001f) axis = Vector3.zero;
            else axis.Normalize();

            float strength = profile != null ? profile.OrientationStrength : 38f;
            float damping = profile != null ? profile.AngularDamping : 11f;
            float maximumAcceleration = profile != null ? profile.MaximumAngularAcceleration : 45f;
            Vector3 acceleration = (axis * (degrees * Mathf.Deg2Rad * strength)) -
                                   (body.angularVelocity * damping);
            acceleration = Vector3.ClampMagnitude(acceleration, maximumAcceleration);
            body.AddTorque(acceleration, ForceMode.Acceleration);
            float maximumSpeed = profile != null ? profile.MaximumAngularSpeed : 0.85f;
            if (body.angularVelocity.magnitude > maximumSpeed)
                body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, maximumSpeed);
        }

        private static Vector3 SafeUp(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.25f ? value.normalized : fallback.normalized;

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
