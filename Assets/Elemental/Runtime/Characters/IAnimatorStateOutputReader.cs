using UnityEngine;

namespace Elemental.Runtime.Characters
{
    /// <summary>
    /// Runtime-facing read seam for the object that actually owns Animator
    /// controller evaluation. Runtime physics must not depend on Presentation,
    /// while a Playables graph must not be diagnosed through the stale legacy
    /// Animator controller clock.
    /// </summary>
    public interface IAnimatorStateOutputReader
    {
        bool OwnsAnimatorOutput { get; }
        AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer);
        AnimatorStateInfo GetNextAnimatorStateInfo(int layer);
        bool IsInTransition(int layer);
        void Evaluate(float deltaTime = 0f);
    }
}
