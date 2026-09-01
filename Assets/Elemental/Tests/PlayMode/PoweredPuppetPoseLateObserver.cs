using System;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    /// <summary>
    /// Test-only observation seam that samples after every production presentation
    /// writer, including the powered-puppet bridge at execution order 1200.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    public sealed class PoweredPuppetPoseLateObserver : MonoBehaviour
    {
        private Transform[] _sources = Array.Empty<Transform>();
        private Transform[] _targets = Array.Empty<Transform>();
        private float[] _positionErrors = Array.Empty<float>();
        private float[] _rotationErrors = Array.Empty<float>();

        public int BindingCount => _sources.Length;
        public int LastCapturedFrame { get; private set; } = -1;

        public void Configure(Transform[] sources, Transform[] targets)
        {
            if (sources == null || targets == null || sources.Length == 0 ||
                sources.Length != targets.Length)
                throw new ArgumentException("Pose observer requires equal non-empty bindings.");
            _sources = (Transform[])sources.Clone();
            _targets = (Transform[])targets.Clone();
            _positionErrors = new float[_sources.Length];
            _rotationErrors = new float[_sources.Length];
            LastCapturedFrame = -1;
        }

        public Transform SourceAt(int index) => _sources[index];
        public Transform TargetAt(int index) => _targets[index];
        public float PositionErrorAt(int index) => _positionErrors[index];
        public float RotationErrorAt(int index) => _rotationErrors[index];

        private void LateUpdate()
        {
            for (int index = 0; index < _sources.Length; index++)
            {
                Transform source = _sources[index];
                Transform target = _targets[index];
                if (source == null || target == null)
                {
                    _positionErrors[index] = float.PositiveInfinity;
                    _rotationErrors[index] = float.PositiveInfinity;
                    continue;
                }
                _positionErrors[index] = Vector3.Distance(source.position, target.position);
                _rotationErrors[index] = Quaternion.Angle(source.rotation, target.rotation);
            }
            LastCapturedFrame = Time.frameCount;
        }
    }
}
