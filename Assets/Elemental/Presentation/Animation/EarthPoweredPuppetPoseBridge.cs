using System;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Copies evaluated Humanoid bone poses into hidden, sibling-safe active
    /// puppet targets. It never writes a visible bone, foot, knee, hip or Animator.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class EarthPoweredPuppetPoseBridge : MonoBehaviour
    {
        [SerializeField] private Transform[] sourceBones = Array.Empty<Transform>();
        [SerializeField] private Transform[] poseTargets = Array.Empty<Transform>();

        private bool _bindingsValid;

        public int BindingCount => _bindingsValid ? sourceBones.Length : 0;
        public bool BindingsValid => _bindingsValid;

        public void Configure(Transform[] sources, Transform[] targets)
        {
            if (!TryValidate(sources, targets, out string failure))
                throw new ArgumentException(failure);
            sourceBones = (Transform[])sources.Clone();
            poseTargets = (Transform[])targets.Clone();
            _bindingsValid = true;
            CopyPose();
        }

        private void OnEnable()
        {
            _bindingsValid = TryValidate(sourceBones, poseTargets, out string failure);
            if (_bindingsValid) return;
            Debug.LogError($"[Elemental] {name} powered-puppet pose bridge disabled: {failure}", this);
            enabled = false;
        }

        private void LateUpdate() => CopyPose();

        private void CopyPose()
        {
            if (!_bindingsValid) return;
            int count = BindingCount;
            for (int index = 0; index < count; index++)
            {
                Transform source = sourceBones[index];
                Transform target = poseTargets[index];
                target.SetPositionAndRotation(source.position, source.rotation);
            }
        }

        private static bool TryValidate(
            Transform[] sources,
            Transform[] targets,
            out string failure)
        {
            int sourceCount = sources?.Length ?? 0;
            int targetCount = targets?.Length ?? 0;
            if (sourceCount == 0)
            {
                failure = "no source bones were assigned";
                return false;
            }
            if (sourceCount != targetCount)
            {
                failure = $"source/target count mismatch ({sourceCount}/{targetCount})";
                return false;
            }
            for (int index = 0; index < sourceCount; index++)
            {
                if (sources[index] == null || targets[index] == null)
                {
                    failure = $"binding {index} contains a missing source or target";
                    return false;
                }
                if (ReferenceEquals(sources[index], targets[index]) ||
                    sources[index].IsChildOf(targets[index]))
                {
                    failure = $"binding {index} would feed a visible source from its own target hierarchy";
                    return false;
                }
            }
            failure = string.Empty;
            return true;
        }
    }
}
