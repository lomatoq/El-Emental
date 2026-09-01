using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Read-only policy preview. Runtime requests are routed through the sole Animator writer.
    /// </summary>
    public sealed class EarthTransitionDebugWindow : EditorWindow
    {
        private EarthTransitionProfile _profile;
        private EarthTransitionDirector _director;
        private EarthMotionStateId _sourceState = EarthMotionStateId.Locomotion;
        private EarthMotionStateId _destinationState = EarthMotionStateId.TurnInPlace;
        private EarthMotionCategory _sourceCategory = EarthMotionCategory.Locomotion;
        private EarthMotionCategory _destinationCategory = EarthMotionCategory.Turn;
        private EarthAnimationTransitionPriority _requestPriority =
            EarthAnimationTransitionPriority.Locomotion;
        private EarthAnimationTransitionPriority _activePriority =
            EarthAnimationTransitionPriority.Idle;
        private float _sourcePhase01;
        private float _gaitPhase01;
        private float _destinationCycleSeconds = 1f;
        private float _landingContactSeconds;
        private float _predictedContactSeconds;
        private bool _hasLandingPrediction;
        private bool _mayInterrupt = true;
        private bool _forceRestart;
        private bool _requestInertialization = true;
        private string _destinationStatePath = "Base Layer.Turn In Place";

        [MenuItem("Elemental Suite/Animation/Earth Transition Debug")]
        public static void Open() =>
            GetWindow<EarthTransitionDebugWindow>("Earth Transitions");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Authored transition preview", EditorStyles.boldLabel);
            _profile = (EarthTransitionProfile)EditorGUILayout.ObjectField(
                "Transition profile",
                _profile,
                typeof(EarthTransitionProfile),
                false);
            _director = (EarthTransitionDirector)EditorGUILayout.ObjectField(
                "Runtime director",
                _director,
                typeof(EarthTransitionDirector),
                true);
            _destinationStatePath = EditorGUILayout.TextField(
                "Destination state path",
                _destinationStatePath);

            EditorGUILayout.Space();
            _sourceState = (EarthMotionStateId)EditorGUILayout.EnumPopup(
                "Source state",
                _sourceState);
            _destinationState = (EarthMotionStateId)EditorGUILayout.EnumPopup(
                "Destination state",
                _destinationState);
            _sourceCategory = (EarthMotionCategory)EditorGUILayout.EnumPopup(
                "Source category",
                _sourceCategory);
            _destinationCategory = (EarthMotionCategory)EditorGUILayout.EnumPopup(
                "Destination category",
                _destinationCategory);
            _requestPriority =
                (EarthAnimationTransitionPriority)EditorGUILayout.EnumPopup(
                    "Request priority",
                    _requestPriority);
            _activePriority =
                (EarthAnimationTransitionPriority)EditorGUILayout.EnumPopup(
                    "Active priority",
                    _activePriority);
            _sourcePhase01 = EditorGUILayout.Slider(
                "Source phase",
                _sourcePhase01,
                0f,
                1f);
            _gaitPhase01 = EditorGUILayout.Slider(
                "Gait phase",
                _gaitPhase01,
                0f,
                1f);
            _destinationCycleSeconds = EditorGUILayout.FloatField(
                "Destination cycle (s)",
                _destinationCycleSeconds);
            _landingContactSeconds = EditorGUILayout.FloatField(
                "Landing contact (s)",
                _landingContactSeconds);
            _predictedContactSeconds = EditorGUILayout.FloatField(
                "Predicted contact (s)",
                _predictedContactSeconds);
            _hasLandingPrediction = EditorGUILayout.Toggle(
                "Has landing prediction",
                _hasLandingPrediction);
            _mayInterrupt = EditorGUILayout.Toggle("May interrupt", _mayInterrupt);
            _forceRestart = EditorGUILayout.Toggle("Force restart", _forceRestart);
            _requestInertialization = EditorGUILayout.Toggle(
                "Request inertialization",
                _requestInertialization);

            EarthAnimationTransitionContext context = BuildContext();
            EditorGUILayout.Space();
            DrawResolvedPolicy(in context);
            DrawRuntimeDiagnostics();

            bool canRequest = Application.isPlaying && _director != null &&
                              !string.IsNullOrWhiteSpace(_destinationStatePath);
            using (new EditorGUI.DisabledScope(!canRequest))
            {
                if (GUILayout.Button("Request through EarthTransitionDirector"))
                {
                    int destinationHash = Animator.StringToHash(_destinationStatePath);
                    _director.RequestTransition(destinationHash, in context);
                }
            }
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Policy preview is pure in Edit Mode. Runtime preview routes the request " +
                    "through EarthTransitionDirector; this window never writes Animator state.",
                    MessageType.Info);
            }
        }

        private void DrawResolvedPolicy(in EarthAnimationTransitionContext context)
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "No profile assigned. Production remains on the legacy transition policy.",
                    MessageType.None);
                return;
            }
            if (!_profile.TryResolve(
                    in context,
                    out EarthTransitionRule rule,
                    out int pairIndex,
                    out bool fallback))
            {
                EditorGUILayout.HelpBox(
                    "Profile feature flag is OFF. The exact legacy policy remains authoritative.",
                    MessageType.Info);
                return;
            }

            EarthAnimationTransitionDecision decision =
                EarthTransitionRulePolicy.Resolve(in context, in rule);
            EditorGUILayout.LabelField(
                fallback ? "Resolution: generic fallback" : $"Resolution: pair {pairIndex}",
                EditorStyles.boldLabel);
            if (fallback)
            {
                EditorGUILayout.HelpBox(
                    "This exact request would use the generic fixed crossfade and emit its " +
                    "one-time development warning when executed.",
                    MessageType.Warning);
            }
            EditorGUILayout.LabelField("Family", rule.Family.ToString());
            EditorGUILayout.LabelField("Priority", rule.Priority.ToString());
            EditorGUILayout.LabelField("Half-life", $"{rule.HalfLifeSeconds:0.000} s");
            EditorGUILayout.LabelField(
                "Fallback duration",
                $"{rule.FallbackDurationSeconds:0.000} s");
            EditorGUILayout.LabelField("Gait rule", rule.GaitPhaseRule.ToString());
            EditorGUILayout.LabelField("Contact policy", rule.ContactPolicy.ToString());
            EditorGUILayout.LabelField("Cancel policy", rule.CancelPolicy.ToString());
            EditorGUILayout.LabelField("Body mask", rule.BodyMask.ToString());
            EditorGUILayout.LabelField("Foot release", rule.FootReleasePolicy.ToString());
            EditorGUILayout.LabelField(
                "Decision",
                $"{decision.Reason}; {decision.Kind}; duration {decision.DurationSeconds:0.000}s; " +
                $"target phase {decision.DestinationNormalizedTime:0.000}");
        }

        private void DrawRuntimeDiagnostics()
        {
            if (_director == null) return;
            EarthTransitionDirectorDiagnostics diagnostics = _director.Diagnostics;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bounded runtime diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Last resolution", diagnostics.LastResolution.ToString());
            EditorGUILayout.LabelField("Last pair", diagnostics.LastPairIndex.ToString());
            EditorGUILayout.LabelField("Queued", diagnostics.QueuedRequestCount.ToString());
            EditorGUILayout.LabelField(
                "Authored / fallback executions",
                $"{diagnostics.AuthoredPairExecutionCount} / " +
                $"{diagnostics.GenericFallbackExecutionCount}");
            EditorGUILayout.LabelField(
                "Queued / dequeued / rejected",
                $"{diagnostics.QueuedRequestCountTotal} / " +
                $"{diagnostics.DequeuedExecutionCount} / {diagnostics.QueueRejectionCount}");
        }

        private EarthAnimationTransitionContext BuildContext() =>
            new EarthAnimationTransitionContext(
                _sourceState,
                _destinationState,
                _sourceCategory,
                _destinationCategory,
                _requestPriority,
                _activePriority,
                _sourcePhase01,
                _gaitPhase01,
                _destinationCycleSeconds,
                _landingContactSeconds,
                _predictedContactSeconds,
                _hasLandingPrediction,
                _mayInterrupt,
                _forceRestart,
                _requestInertialization);
    }
}
