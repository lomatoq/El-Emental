from pathlib import Path

root = Path(__file__).resolve().parent.parent
destination = root / 'Tools' / 'AnimationFinalOwnerPatch'
files = {}

def read(relative):
    files[relative] = (root / relative).read_text(encoding='utf-8-sig')
    return relative

def replace(path, old, new):
    assert old in files[path], (path, old[:100])
    files[path] = files[path].replace(old, new)

base = 'Assets/Elemental/Presentation/'
graph = read(base + 'MotionMatching/EarthAnimationGraph.cs')
for line in [
    '        private NativeArray<EarthFinalIkGoal> _finalGoals;\n',
    '        private NativeArray<Vector3> _finalKnees;\n',
    '        private NativeArray<Vector3> _finalPelvis;\n',
    '        public int FinalSolves => _finalCounters.IsCreated ? _finalCounters[1] : 0;\n',
    '            _finalGoals = new NativeArray<EarthFinalIkGoal>(4, Allocator.Persistent);\n',
    '            _finalKnees = new NativeArray<Vector3>(2, Allocator.Persistent);\n',
    '            _finalPelvis = new NativeArray<Vector3>(1, Allocator.Persistent);\n',
    '            Dispose(ref _finalGoals);\n', '            Dispose(ref _finalKnees);\n', '            Dispose(ref _finalPelvis);\n']:
    replace(graph, line, '')
replace(graph, 'new NativeArray<int>(2, Allocator.Persistent)', 'new NativeArray<int>(1, Allocator.Persistent)')
replace(graph, 'var finalJob = new EarthFinalContactJob\n            {\n                Goals = _finalGoals, KneePositions = _finalKnees,\n                PelvisOffset = _finalPelvis, Counters = _finalCounters\n            };',
    'var finalJob = new EarthAnimationEvaluationJob { Counters = _finalCounters };')
start = files[graph].index('        public void PrepareFinalContacts()')
end = files[graph].index('        public void SetEammMasterWeight', start)
files[graph] = files[graph][:start] + files[graph][end:]

job = read(base + 'MotionMatching/EarthFinalContactJob.cs')
files[job] = '''using Unity.Collections;
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
'''
bridge = read(base + 'MotionMatching/EAMMBasePoseBridge.cs')
replace(bridge, '            _animationGraph?.PrepareFinalContacts();\n', '')

driver = read(base + 'Animation/EarthAnimationDriver.cs')
replace(driver, '        private float _landingPoseWeight = 1f;',
    '        private float _landingPoseWeight = 1f;\n        private int _finalContactPassCount;\n        private int _lastContactGraphEvaluation = -1;')
replace(driver, '        public int FinalIkSolveCount => HasGraph ? _graph.FinalSolves : 0;',
    '        public int FinalContactPassCount => HasGraph ? _finalContactPassCount : 0;')
replace(driver, '            _graph = graph;\n',
    '            _graph = graph;\n            _finalContactPassCount = 0;\n            _lastContactGraphEvaluation = -1;\n')
start = files[driver].index('        public void SetFinalIkGoal')
end = files[driver].index('        public void SetFloat(int hash, float value, float dampTime', start)
files[driver] = files[driver][:start] + '''        // Counts goal submission passes, not Unity's unobservable internal solve count.
        public void RecordFinalContactPass()
        {
            if (!HasGraph || _lastContactGraphEvaluation == _graph.FinalEvaluations) return;
            _lastContactGraphEvaluation = _graph.FinalEvaluations;
            _finalContactPassCount++;
        }

''' + files[driver][end:]

feet = read(base + 'Animation/EarthFootContactController.cs')
replace(feet, '            // and its final HumanStream solve, rather than the pre-IK pose.',
    '            // and final OnAnimatorIK contact pass, rather than the pre-IK pose.')
replace(feet, '                if (_animationDriver == null || !_animationDriver.UsesPlayableGraph)\n                    animator.bodyPosition += motor.LocalUp.normalized * _pelvisOffset;\n', '')
replace(feet, '            using (ContactMarker.Auto()) EvaluateFootContacts();',
    '            using (ContactMarker.Auto()) EvaluateFootContacts();\n            if (Mathf.Max(_leftAppliedWeight, _rightAppliedWeight) > .001f)\n                _animationDriver?.RecordFinalContactPass();')
replace(feet, '            if (_animationDriver != null && _animationDriver.UsesPlayableGraph)\n                _animationDriver.SetFinalKneesAndPelvis(_leftHintWorld, _rightHintWorld, up * _pelvisOffset);\n', '')
start = files[feet].index('            if (_animationDriver != null && _animationDriver.UsesPlayableGraph)', files[feet].index('        private void ApplyFoot('))
end = files[feet].index('            // Humanoid IK goals', start)
files[feet] = files[feet][:start] + '''            // This callback runs after graph evaluation; it is the supported final
            // Humanoid goal owner for both authored and EAMM-composed base poses.
''' + files[feet][end:]
replace(feet, '            bool finalGraph = _animationDriver != null && _animationDriver.UsesPlayableGraph;\n', '')
replace(feet, 'finalGraph ? 0f : leftApplied', 'leftApplied')
replace(feet, 'finalGraph ? 0f : rightApplied', 'rightApplied')
replace(feet, 'finalGraph ? 0f : _leftAppliedWeight * 0.28f', '_leftAppliedWeight * 0.28f')
replace(feet, 'finalGraph ? 0f : _rightAppliedWeight * 0.28f', '_rightAppliedWeight * 0.28f')
replace(feet, '            if (finalGraph) return;\n', '')
replace(feet, '            if (_animationDriver == null || !_animationDriver.UsesPlayableGraph)\n                animator.bodyPosition += up * _pelvisOffset;',
    '            animator.bodyPosition += up * _pelvisOffset;')

presentation = read(base + 'Animation/HumanoidCharacterPresentation.cs')
start = files[presentation].index('            if (animationDriver != null && animationDriver.UsesPlayableGraph)', files[presentation].index('        private void OnAnimatorIK('))
end = files[presentation].index('            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);', start)
files[presentation] = files[presentation][:start] + files[presentation][end:]

probe = read(base + 'Animation/EarthAnimationPoseProbe.cs')
replace(probe, 'finalIkSolves', 'weightedContactPasses')
replace(probe, '_driver.FinalIkSolveCount', '_driver.FinalContactPassCount')
tests = read('Assets/Elemental/Tests/PlayMode/SeptemberAnimationRescueRuntimeTests.cs')
replace(tests, 'finalIkSolves', 'weightedContactPasses')
replace(tests, 'No final weighted contact solve was executed.', 'No final weighted OnAnimatorIK contact pass submitted goals.')
replace(tests, 'Only one weighted final Humanoid solve is allowed per graph evaluation.', 'Contact state may advance only once per graph evaluation.')

for relative, content in files.items():
    output = destination / relative
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(content, encoding='utf-8', newline='\n')
print('\n'.join(files))
