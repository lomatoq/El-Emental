using Elemental.Presentation.MotionMatching;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Single parameter/state API for authored animation. When EAMM owns the
    /// output playable it mirrors every command to that controller; otherwise it
    /// transparently behaves like the ordinary Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private EarthAnimationGraph _graph;
        private float _landingPoseWeight = 1f;
        private int _finalContactPassCount;
        private int _lastContactGraphEvaluation = -1;

        public Animator Animator => animator;
        public bool IsUsable => HasGraph || CanDriveAnimator;
        public bool UsesPlayableGraph => HasGraph;
        public int FinalIkEvaluationCount => HasGraph ? _graph.FinalEvaluations : 0;
        public int FinalContactPassCount => HasGraph ? _finalContactPassCount : 0;
        public float LandingPoseWeight => _landingPoseWeight;

        private bool HasGraph => _graph != null && _graph.IsCreated;
        private bool CanDriveAnimator =>
            animator != null &&
            animator.enabled &&
            animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null;

        public void Configure(Animator configuredAnimator) => animator = configuredAnimator;

        internal void Attach(EarthAnimationGraph graph)
        {
            _graph = graph;
            _finalContactPassCount = 0;
            _lastContactGraphEvaluation = -1;
            _graph?.SetLandingPoseWeight(_landingPoseWeight);
        }

        internal void Detach(EarthAnimationGraph graph)
        {
            if (ReferenceEquals(_graph, graph)) _graph = null;
        }

        public void SetFloat(int hash, float value)
        {
            if (HasGraph) _graph.SetFloat(hash, value);
            else if (CanDriveAnimator) animator.SetFloat(hash, value);
        }

        public void SetLandingPoseWeight(float weight)
        {
            _landingPoseWeight = Mathf.Clamp01(weight);
            if (HasGraph) _graph.SetLandingPoseWeight(_landingPoseWeight);
        }

        // Counts goal submission passes, not Unity's unobservable internal solve count.
        public void RecordFinalContactPass()
        {
            if (!HasGraph || _lastContactGraphEvaluation == _graph.FinalEvaluations) return;
            _lastContactGraphEvaluation = _graph.FinalEvaluations;
            _finalContactPassCount++;
        }

        public void SetFloat(int hash, float value, float dampTime, float deltaTime)
        {
            // Preserve the existing Animator backend's damping. The playable
            // backend explicitly uses a first-order response; source parameters
            // already filtered by their owner use the immediate overload.
            if (HasGraph) _graph.SetFloat(hash, DampParameter(_graph.GetFloat(hash), value, dampTime, deltaTime));
            else if (CanDriveAnimator) animator.SetFloat(hash, value, dampTime, deltaTime);
        }

        public static float DampParameter(float current, float target, float responseSeconds, float deltaTime)
        {
            if (responseSeconds <= 0f) return target;
            return Mathf.Lerp(current, target,
                1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / responseSeconds));
        }

        public void SetBool(int hash, bool value)
        {
            if (HasGraph) _graph.SetBool(hash, value);
            else if (CanDriveAnimator) animator.SetBool(hash, value);
        }

        public bool GetBool(int hash) => HasGraph
            ? _graph.GetBool(hash)
            : CanDriveAnimator && animator.GetBool(hash);

        public float GetFloat(int hash) => HasGraph
            ? _graph.GetFloat(hash)
            : CanDriveAnimator ? animator.GetFloat(hash) : 0f;

        public void SetInteger(int hash, int value)
        {
            if (HasGraph) _graph.SetInteger(hash, value);
            else if (CanDriveAnimator) animator.SetInteger(hash, value);
        }

        public void SetTrigger(int hash)
        {
            if (HasGraph) _graph.SetTrigger(hash);
            else if (CanDriveAnimator) animator.SetTrigger(hash);
        }

        public void ResetTrigger(int hash)
        {
            if (HasGraph) _graph.ResetTrigger(hash);
            else if (CanDriveAnimator) animator.ResetTrigger(hash);
        }

        public void SetLayerWeight(int layer, float weight)
        {
            if (HasGraph) _graph.SetLayerWeight(layer, weight);
            else if (CanDriveAnimator) animator.SetLayerWeight(layer, weight);
        }

        public bool IsInTransition(int layer) => HasGraph
            ? _graph.IsInTransition(layer)
            : CanDriveAnimator && animator.IsInTransition(layer);

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) =>
            HasGraph
                ? _graph.GetCurrentAnimatorStateInfo(layer)
                : CanDriveAnimator ? animator.GetCurrentAnimatorStateInfo(layer) : default;

        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) =>
            HasGraph
                ? _graph.GetNextAnimatorStateInfo(layer)
                : CanDriveAnimator ? animator.GetNextAnimatorStateInfo(layer) : default;

        public void CrossFade(
            int stateHash,
            float normalizedDuration,
            int layer,
            float normalizedTime)
        {
            if (HasGraph)
                _graph.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);
            else if (CanDriveAnimator)
                animator.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);
        }

        public void CrossFadeInFixedTime(
            int stateHash,
            float duration,
            int layer,
            float fixedTime)
        {
            if (HasGraph)
                _graph.CrossFadeInFixedTime(stateHash, duration, layer, fixedTime);
            else if (CanDriveAnimator)
                animator.CrossFadeInFixedTime(stateHash, duration, layer, fixedTime);
        }

        public void Play(int stateHash, int layer, float normalizedTime)
        {
            if (HasGraph)
                _graph.Play(stateHash, layer, normalizedTime);
            else if (CanDriveAnimator)
                animator.Play(stateHash, layer, normalizedTime);
        }
    }
}
