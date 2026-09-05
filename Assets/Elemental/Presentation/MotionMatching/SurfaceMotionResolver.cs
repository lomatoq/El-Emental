using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    [DisallowMultipleComponent]
    public sealed class SurfaceMotionResolver : MonoBehaviour
    {
        private readonly RaycastHit[] _hits = new RaycastHit[4];
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private SurfaceMotionProfile fallback;

        public SurfaceMotionProfile Current { get; private set; }

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            Current = fallback;
        }

        private void LateUpdate()
        {
            if (motor == null || !motor.HasStableSupport)
            {
                Current = fallback;
                return;
            }

            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            int count = Physics.RaycastNonAlloc(
                motor.SupportFeetPoint(up) + up * 0.12f,
                -up,
                _hits,
                motor.GroundProbeDistance + 0.3f,
                motor.GroundMask,
                QueryTriggerInteraction.Ignore);
            SurfaceMotionProfile resolved = fallback;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || hit.distance >= nearest) continue;
                SurfaceMotionTag tag = hit.collider.GetComponentInParent<SurfaceMotionTag>();
                if (tag == null || tag.Profile == null) continue;
                nearest = hit.distance;
                resolved = tag.Profile;
            }

            Current = resolved;
        }
    }
}
