using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Animations;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Observes completed base-pose graph evaluations. Unity invokes OnAnimatorIK
    /// after graph processing; that callback owns final weighted Humanoid contacts.
    /// This job never sets IK goals or performs an extra HumanStream.SolveIK.
    /// </summary>
    public struct EarthAnimationEvaluationJob : IAnimationJob
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> Counters;
        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            if (stream.isHumanStream) Counters[0]++;
        }
    }
}
