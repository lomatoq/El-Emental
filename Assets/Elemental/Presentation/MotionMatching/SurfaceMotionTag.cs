using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    [DisallowMultipleComponent]
    public sealed class SurfaceMotionTag : MonoBehaviour
    {
        [SerializeField] private SurfaceMotionProfile profile;

        public SurfaceMotionProfile Profile => profile;

        public void Configure(SurfaceMotionProfile value) => profile = value;
    }
}
