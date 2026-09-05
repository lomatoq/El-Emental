using System.IO;
using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    // Explicitly armed, read-only diagnostic. It never starts/stops Play or sends input.
    [InitializeOnLoad]
    public static class EarthCharacterStartupProbe
    {
        private const string ArmedKey = "Elemental.StartupProbe.Armed";
        private static readonly StringBuilder Samples = new StringBuilder(16384);
        private static float _nextSample;
        private static bool _recording;

        static EarthCharacterStartupProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Sample;
        }

        [MenuItem("Elemental/Diagnostics/Arm Next Character Startup Probe")]
        public static void Arm()
        {
            SessionState.SetBool(ArmedKey, true);
            Debug.Log("[StartupProbe] Armed for the next Play session; no input or physics changes.");
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ArmedKey, false))
            {
                Samples.Clear();
                _nextSample = 0f;
                _recording = true;
                SessionState.SetBool(ArmedKey, false);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && _recording) Finish();
        }

        private static void Sample()
        {
            if (!_recording || !EditorApplication.isPlaying || Time.time < _nextSample) return;
            _nextSample = Time.time + 0.1f;
            foreach (PlanetMotor motor in Object.FindObjectsByType<PlanetMotor>(FindObjectsSortMode.None))
            {
                HumanoidCharacterPresentation presentation = motor.GetComponentInChildren<HumanoidCharacterPresentation>();
                EarthCharacterImpactTarget impact = motor.GetComponent<EarthCharacterImpactTarget>();
                HumanoidRagdollRig rig = motor.GetComponentInChildren<HumanoidRagdollRig>();
                if (presentation == null || motor.Body == null) continue;
                EarthAnimationDriver driver = presentation.GetComponent<EarthAnimationDriver>();
                EAMMBasePoseBridge bridge = presentation.GetComponent<EAMMBasePoseBridge>();
                Transform hips = presentation.Animator.GetBoneTransform(HumanBodyBones.Hips);
                AnimatorStateInfo state = driver.GetCurrentAnimatorStateInfo(0);
                string name = state.fullPathHash.ToString();
                foreach (string candidate in new[] { "Locomotion", "Jump", "Fall", "Land", "Moving Land", "Moving Land Back", "Hard Land", "Knockdown Recovery", "Turn In Place" })
                    if (state.IsName("Base Layer." + candidate)) name = candidate;
                Samples.AppendLine($"t={Time.time:F3} {motor.name} y={motor.Body.position.y:F3} vel={motor.Body.linearVelocity:F2} ground={motor.HasStableSupport} motor={motor.MotionState} phase={presentation.MotionPhase}/{presentation.LandingStyle} action={presentation.CurrentAuthoredAction} state={name}@{state.normalizedTime:F2} transition={driver.IsInTransition(0)} drop={presentation.LandingDropHeight:F3} air={presentation.LandingAirborneSeconds:F3} roll={presentation.LandingRollAllowed} extra={presentation.LandingExternalDeltaSpeed:F2} weight={presentation.LandingPoseStrength:F2} hits={impact?.AcceptedImpactCount} response={impact?.LastResponse} knocked={impact?.IsRecoverablyKnockedDown} ragdoll={rig?.IsRagdollActive} recovering={rig?.IsRecoveringToAnimation} hipsUp={(hips != null ? Vector3.Dot(hips.up, motor.LocalUp) : 0f):F2} eamm={bridge?.RuntimeStatus}/{bridge?.InitializationStatus}");
            }
            if (Time.time >= 5f) Finish();
        }

        private static void Finish()
        {
            _recording = false;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuildReports/RuntimeRescue"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "CharacterStartupProbe.log");
            File.WriteAllText(path, Samples.ToString());
            Debug.Log("[StartupProbe] Saved " + path);
        }
    }
}
