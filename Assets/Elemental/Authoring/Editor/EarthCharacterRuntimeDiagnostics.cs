using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    internal static class EarthCharacterRuntimeDiagnostics
    {
        [MenuItem("Elemental Suite/Diagnostics/Dump Active Character")]
        private static void DumpActiveCharacter()
        {
            PlanetMotor motor = Object.FindAnyObjectByType<PlanetMotor>();
            HumanoidCharacterPresentation presentation =
                Object.FindAnyObjectByType<HumanoidCharacterPresentation>();
            ActiveRagdollPuppet puppet = Object.FindAnyObjectByType<ActiveRagdollPuppet>();
            Animator animator = presentation != null ? presentation.Animator :
                Object.FindAnyObjectByType<Animator>();
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            var report = new StringBuilder(768);
            report.Append("[EarthCharacterRuntime] play=").Append(EditorApplication.isPlaying);
            if (motor != null)
            {
                report.Append(" motorState=").Append(motor.MotionState)
                    .Append(" support=").Append(motor.HasStableSupport)
                    .Append(" grounded=").Append(motor.IsGrounded)
                    .Append(" speed=").Append(motor.Telemetry.Speed.ToString("F3"));
            }
            else report.Append(" motor=MISSING");
            if (body != null)
                report.Append(" bodyPos=").Append(body.position.ToString("F3"))
                    .Append(" velocity=").Append(body.linearVelocity.ToString("F3"));
            if (puppet != null)
                report.Append(" physicalMode=").Append(puppet.CurrentState.Mode)
                    .Append(" jointError=").Append(puppet.MaximumJointError.ToString("F3"));
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
                Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
                int skinned = 0;
                for (int index = 0; index < renderers.Length; index++)
                    if (renderers[index] is SkinnedMeshRenderer) skinned++;
                report.Append(" animatorEnabled=").Append(animator.enabled)
                    .Append(" initialized=").Append(animator.isInitialized)
                    .Append(" human=").Append(animator.isHuman)
                    .Append(" avatarValid=").Append(animator.avatar != null && animator.avatar.isValid)
                    .Append(" controller=").Append(animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.name : "MISSING")
                    .Append(" stateHash=").Append(state.fullPathHash)
                    .Append(" normalized=").Append(state.normalizedTime.ToString("F3"))
                    .Append(" clip=").Append(clips.Length > 0 && clips[0].clip != null
                        ? clips[0].clip.name : "NONE")
                    .Append(" renderers=").Append(renderers.Length)
                    .Append(" skinned=").Append(skinned);
            }
            else report.Append(" animator=MISSING");
            Debug.Log(report.ToString(), motor != null ? motor : presentation);
        }
    }
}
