using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Owns the one playable graph that composes the authored Animator, optional
    /// EAMM base pose and per-bone inertialization. It never owns root motion.
    /// </summary>
    internal sealed class EarthAnimationGraph : IDisposable
    {
        private NativeArray<TransformStreamHandle> _handles;
        private NativeArray<Quaternion> _eammLocalRotations;
        private NativeArray<float> _eammWeights;
        private NativeArray<float> _masterWeight;
        private NativeArray<byte> _contactGroups;
        private NativeArray<float> _footContacts;
        private NativeArray<int> _transitionSerial;
        private NativeArray<float> _halfLife;
        private NativeArray<EarthRotationInertializationState> _rotationStates;
        private NativeArray<int> _appliedTransitionSerial;
        private NativeArray<int> _finalCounters;
        private PlayableGraph _graph;
        private AnimatorControllerPlayable _controller;
        private AnimatorControllerPlayable _neutralController;
        private AnimationMixerPlayable _landingMixer;

        public bool IsCreated => _graph.IsValid();
        public float EammMasterWeight => _masterWeight.IsCreated ? _masterWeight[0] : 0f;
        public int BoneCount => _handles.IsCreated ? _handles.Length : 0;
        public int FinalEvaluations => _finalCounters.IsCreated ? _finalCounters[0] : 0;

        public void Create(
            Animator animator,
            Transform[] targetBones,
            byte[] contactGroups,
            string graphName)
        {
            Dispose();
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("EarthAnimationGraph requires an AnimatorController.");
            int count = targetBones?.Length ?? 0;
            if (count == 0) throw new InvalidOperationException("EarthAnimationGraph requires target bones.");

            _handles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _eammLocalRotations = new NativeArray<Quaternion>(count, Allocator.Persistent);
            _eammWeights = new NativeArray<float>(count, Allocator.Persistent);
            _masterWeight = new NativeArray<float>(1, Allocator.Persistent);
            _contactGroups = new NativeArray<byte>(count, Allocator.Persistent);
            _footContacts = new NativeArray<float>(2, Allocator.Persistent);
            _transitionSerial = new NativeArray<int>(1, Allocator.Persistent);
            _halfLife = new NativeArray<float>(1, Allocator.Persistent);
            _rotationStates = new NativeArray<EarthRotationInertializationState>(
                count,
                Allocator.Persistent);
            _appliedTransitionSerial = new NativeArray<int>(1, Allocator.Persistent);
            _finalCounters = new NativeArray<int>(1, Allocator.Persistent);
            _halfLife[0] = 0.08f;

            for (int i = 0; i < count; i++)
            {
                Transform target = targetBones[i];
                if (target != null)
                {
                    _handles[i] = animator.BindStreamTransform(target);
                    _eammLocalRotations[i] = target.localRotation;
                    _eammWeights[i] = 1f;
                }
                _contactGroups[i] = contactGroups != null && i < contactGroups.Length
                    ? contactGroups[i]
                    : (byte)0;
            }

            _graph = PlayableGraph.Create(graphName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _controller = AnimatorControllerPlayable.Create(
                _graph,
                animator.runtimeAnimatorController);
            // Landing strength is a pose amplitude, not playback speed. Keep a
            // grounded locomotion reference running at the same gait parameters
            // and blend the authored landing against it before inertialization.
            _neutralController = AnimatorControllerPlayable.Create(
                _graph,
                animator.runtimeAnimatorController);
            _neutralController.SetBool(Animator.StringToHash("Grounded"), true);
            _neutralController.Play(Animator.StringToHash("Base Layer.Locomotion"), 0, 0f);
            _landingMixer = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(_neutralController, 0, _landingMixer, 0);
            _graph.Connect(_controller, 0, _landingMixer, 1);
            SetLandingPoseWeight(1f);
            var job = new EarthInertializationJob
            {
                Handles = _handles,
                EammLocalRotations = _eammLocalRotations,
                EammBoneWeights = _eammWeights,
                EammMasterWeight = _masterWeight,
                ContactGroups = _contactGroups,
                FootContacts = _footContacts,
                TransitionSerial = _transitionSerial,
                HalfLifeSeconds = _halfLife,
                RotationStates = _rotationStates,
                AppliedTransitionSerial = _appliedTransitionSerial
            };
            AnimationScriptPlayable jobPlayable = AnimationScriptPlayable.Create(_graph, job, 1);
            _graph.Connect(_landingMixer, 0, jobPlayable, 0);
            jobPlayable.SetInputWeight(0, 1f);
            var finalJob = new EarthAnimationEvaluationJob { Counters = _finalCounters };
            AnimationScriptPlayable finalPlayable = AnimationScriptPlayable.Create(_graph, finalJob, 1);
            _graph.Connect(jobPlayable, 0, finalPlayable, 0);
            finalPlayable.SetInputWeight(0, 1f);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Earth Animation", animator);
            output.SetSourcePlayable(finalPlayable);
            _graph.Play();
        }

        public void SetEammTarget(int index, Quaternion localRotation, float weight = 1f)
        {
            if (!_eammLocalRotations.IsCreated || index < 0 || index >= _eammLocalRotations.Length) return;
            _eammLocalRotations[index] = localRotation;
            _eammWeights[index] = Mathf.Clamp01(weight);
        }

        public void SetEammMasterWeight(float weight)
        {
            if (_masterWeight.IsCreated) _masterWeight[0] = Mathf.Clamp01(weight);
        }

        public void SetFootContacts(bool left, bool right)
        {
            if (!_footContacts.IsCreated) return;
            _footContacts[0] = left ? 1f : 0f;
            _footContacts[1] = right ? 1f : 0f;
        }

        public void RequestInertialization(float transitionSeconds)
        {
            if (!_transitionSerial.IsCreated) return;
            _halfLife[0] = Mathf.Clamp(transitionSeconds * 0.45f, 0.025f, 0.18f);
            _transitionSerial[0]++;
        }

        public void SetFloat(int hash, float value)
        {
            if (_controller.IsValid()) _controller.SetFloat(hash, value);
            if (_neutralController.IsValid()) _neutralController.SetFloat(hash, value);
        }

        public void SetLandingPoseWeight(float weight)
        {
            if (!_landingMixer.IsValid()) return;
            float authoredWeight = Mathf.Clamp01(weight);
            _landingMixer.SetInputWeight(0, 1f - authoredWeight);
            _landingMixer.SetInputWeight(1, authoredWeight);
        }

        public void SetBool(int hash, bool value)
        {
            if (_controller.IsValid()) _controller.SetBool(hash, value);
            // Only the base locomotion state differs. Upper-body cast/impact
            // parameters and layer weights are shared by both mixer inputs.
            if (_neutralController.IsValid() &&
                hash != Animator.StringToHash("Grounded") &&
                hash != Animator.StringToHash("Surfing") &&
                hash != Animator.StringToHash("HardLanding"))
                _neutralController.SetBool(hash, value);
        }

        public bool GetBool(int hash) => _controller.IsValid() && _controller.GetBool(hash);

        public float GetFloat(int hash) => _controller.IsValid() ? _controller.GetFloat(hash) : 0f;

        public void SetInteger(int hash, int value)
        {
            if (_controller.IsValid()) _controller.SetInteger(hash, value);
            if (_neutralController.IsValid()) _neutralController.SetInteger(hash, value);
        }

        public void SetTrigger(int hash)
        {
            if (_controller.IsValid()) _controller.SetTrigger(hash);
            if (_neutralController.IsValid() && hash != Animator.StringToHash("Dodge"))
                _neutralController.SetTrigger(hash);
        }

        public void ResetTrigger(int hash)
        {
            if (_controller.IsValid()) _controller.ResetTrigger(hash);
            if (_neutralController.IsValid()) _neutralController.ResetTrigger(hash);
        }

        public void SetLayerWeight(int layer, float weight)
        {
            if (_controller.IsValid()) _controller.SetLayerWeight(layer, Mathf.Clamp01(weight));
            if (_neutralController.IsValid()) _neutralController.SetLayerWeight(layer, Mathf.Clamp01(weight));
        }

        public float GetLayerWeight(int layer) => _controller.IsValid() &&
            layer >= 0 && layer < _controller.GetLayerCount()
                ? _controller.GetLayerWeight(layer)
                : 0f;

        public bool IsInTransition(int layer) =>
            _controller.IsValid() && _controller.IsInTransition(layer);

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) =>
            _controller.IsValid() ? _controller.GetCurrentAnimatorStateInfo(layer) : default;

        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) =>
            _controller.IsValid() ? _controller.GetNextAnimatorStateInfo(layer) : default;

        public void CrossFade(int stateHash, float duration, int layer, float normalizedTime)
        {
            if (_controller.IsValid())
                _controller.CrossFade(stateHash, duration, layer, normalizedTime);
            if (_neutralController.IsValid() && layer > 0)
                _neutralController.CrossFade(stateHash, duration, layer, normalizedTime);
        }

        public void CrossFadeInFixedTime(int stateHash, float duration, int layer, float fixedTime)
        {
            if (_controller.IsValid())
                _controller.CrossFadeInFixedTime(stateHash, duration, layer, fixedTime);
            if (_neutralController.IsValid() && layer > 0)
                _neutralController.CrossFadeInFixedTime(stateHash, duration, layer, fixedTime);
        }

        public void Play(int stateHash, int layer, float normalizedTime)
        {
            if (_controller.IsValid()) _controller.Play(stateHash, layer, normalizedTime);
            if (_neutralController.IsValid() && layer > 0)
                _neutralController.Play(stateHash, layer, normalizedTime);
        }

        public void SyncParametersFrom(Animator animator)
        {
            if (!_controller.IsValid() || animator == null) return;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (_controller.IsParameterControlledByCurve(parameter.nameHash)) continue;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        SetFloat(parameter.nameHash, animator.GetFloat(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        SetInteger(parameter.nameHash, animator.GetInteger(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        SetBool(parameter.nameHash, animator.GetBool(parameter.nameHash));
                        break;
                }
            }
            int layerCount = Mathf.Min(animator.layerCount, _controller.GetLayerCount());
            for (int layer = 0; layer < layerCount; layer++)
                SetLayerWeight(layer, animator.GetLayerWeight(layer));
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _controller = default;
            _neutralController = default;
            _landingMixer = default;
            Dispose(ref _handles);
            Dispose(ref _eammLocalRotations);
            Dispose(ref _eammWeights);
            Dispose(ref _masterWeight);
            Dispose(ref _contactGroups);
            Dispose(ref _footContacts);
            Dispose(ref _transitionSerial);
            Dispose(ref _halfLife);
            Dispose(ref _rotationStates);
            Dispose(ref _appliedTransitionSerial);
            Dispose(ref _finalCounters);
        }

        private static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated) array.Dispose();
            array = default;
        }
    }
}
