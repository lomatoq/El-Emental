using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        public MonoBehaviour ConfiguredInputSource => inputSourceBehaviour;
        public Transform ConfiguredCameraFrame => cameraFrame;
        public void ConfigureOnlineInput(MonoBehaviour source, Transform frame)
        { ConfigureInputSource(source); cameraFrame = frame; }
        /// <summary>Replace, rather than extend, the host's remaining stun duration.</summary>
        public void ApplyReplicaStun(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 0f || seconds > .6f) return;
            bool wasStunned = IsImpactStunned;
            _impactStunUntil = Time.time + seconds;
            if (!wasStunned && seconds > 0f) ImpactStunBegan?.Invoke();
        }
    }
}
