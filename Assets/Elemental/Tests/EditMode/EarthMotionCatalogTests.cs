using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMotionCatalogTests
    {
        private EarthMotionCatalog _catalog;
        private EarthTransitionProfile _transitionProfile;

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
            if (_transitionProfile != null)
                UnityEngine.Object.DestroyImmediate(_transitionProfile);
        }

        [Test]
        public void CuratedUnionBuildsExactlyFiftyOneUniqueProvenancedClips()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();

            Assert.That(
                EarthMotionCatalogBuilder.CollectCuratedClipCount(),
                Is.EqualTo(EarthMotionCatalog.ExpectedCuratedClipCount));
            string inventory = EarthMotionCatalogBuilder.DescribeCuratedInventory();
            Assert.That(
                inventory,
                Does.Contain(EarthHumanoidMotionSetup.KayKitDirectionalDodgePath));
            Assert.That(
                inventory,
                Does.Contain(EarthHumanoidMotionSetup.KayKitMovementBasicPath));
            for (int nameIndex = 0;
                 nameIndex < EarthMotionCatalogBuilder.CatalogSemanticClipNames.Length;
                 nameIndex++)
                Assert.That(
                    inventory,
                    Does.Contain(
                        $"{EarthMotionCatalogBuilder.CatalogSemanticClipPath} exact-name " +
                        $"'{EarthMotionCatalogBuilder.CatalogSemanticClipNames[nameIndex]}'"));
            Assert.That(inventory, Does.Contain("unique GUID+localFileId total=51"));
            EarthMotionCatalogBuildSummary summary =
                EarthMotionCatalogBuilder.Rebuild(_catalog);

            Assert.That(summary.ClipCount, Is.EqualTo(51));
            Assert.That(summary.StateBindingCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(summary.CopiedCurveClipCount + summary.DerivedCurveClipCount,
                Is.EqualTo(51));
            Assert.That(summary.IdentityHash, Is.Not.Empty);
            Assert.That(_catalog.ClipCount, Is.EqualTo(51));
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int selectedSemanticClipCount = 0;
            for (int index = 0; index < _catalog.ClipCount; index++)
            {
                EarthMotionClipProfile profile = _catalog.ClipAt(index);
                Assert.That(profile, Is.Not.Null, $"catalog index {index}");
                Assert.That(profile.Clip, Is.Not.Null, $"catalog index {index}");
                Assert.That(profile.AssetGuid, Is.Not.Empty, profile.Clip.name);
                Assert.That(profile.LocalFileId, Is.Not.Zero, profile.Clip.name);
                Assert.That(
                    identities.Add(profile.AssetGuid + ":" + profile.LocalFileId),
                    Is.True,
                    profile.Clip.name);
                Assert.That(profile.Provenance, Is.Not.EqualTo(EarthMotionProvenance.Unknown));
                Assert.That(profile.ProvenanceLabel, Is.Not.Empty);
                Assert.That(profile.SemanticAction,
                    Is.Not.EqualTo(EarthMotionSemanticAction.Unknown));
                Assert.That(profile.EnvironmentTags,
                    Is.Not.EqualTo(EarthMotionEnvironmentTag.None));
                if (string.Equals(
                        profile.SourceAssetPath,
                        EarthMotionCatalogBuilder.CatalogSemanticClipPath,
                        StringComparison.Ordinal) &&
                    Array.IndexOf(
                        EarthMotionCatalogBuilder.CatalogSemanticClipNames,
                        profile.Clip.name) >= 0)
                {
                    selectedSemanticClipCount++;
                    Assert.That(
                        profile.SemanticAction,
                        Is.EqualTo(EarthMotionSemanticAction.Cast));
                    Assert.That(
                        profile.AuthoredAction,
                        Is.EqualTo(EarthAuthoredActionId.MagicCast));
                    Assert.That(
                        profile.Style & EarthMotionStyle.Magic,
                        Is.EqualTo(EarthMotionStyle.Magic));
                    Assert.That(
                        profile.ActionTags & EarthMotionActionTag.Cast,
                        Is.EqualTo(EarthMotionActionTag.Cast));
                }
                for (int curveIndex = 0;
                     curveIndex < EarthAnimationClipMetadata.CurveCount;
                     curveIndex++)
                {
                    AnimationCurve curve = profile.Curve(curveIndex);
                    string curveLabel =
                        $"{profile.Clip.name}/{EarthAnimationClipMetadata.CurveName(curveIndex)}";
                    Assert.That(curve?.length ?? 0, Is.GreaterThan(0), curveLabel);
                    Keyframe[] keys = curve.keys;
                    for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                    {
                        Assert.That(
                            float.IsFinite(keys[keyIndex].time),
                            Is.True,
                            $"{curveLabel} key {keyIndex} time");
                        Assert.That(
                            float.IsFinite(keys[keyIndex].value),
                            Is.True,
                            $"{curveLabel} key {keyIndex} value");
                    }
                }
            }
            Assert.That(
                selectedSemanticClipCount,
                Is.EqualTo(5),
                "All five exact pre-existing KayKit magic identities must remain cataloged.");

            Assert.That(
                FindProfile(
                    _catalog,
                    EarthHumanoidMotionSetup.KayKitDirectionalDodgePath,
                    "T-Pose"),
                Is.Null);
            Assert.That(
                FindProfile(
                    _catalog,
                    EarthHumanoidMotionSetup.KayKitDirectionalDodgePath,
                    "Crawling"),
                Is.Null);
            Assert.That(
                FindProfile(
                    _catalog,
                    EarthHumanoidMotionSetup.KayKitDirectionalDodgePath,
                    "Crouching"),
                Is.Null);
            Assert.That(
                FindProfile(
                    _catalog,
                    EarthHumanoidMotionSetup.KayKitMovementBasicPath,
                    "T-Pose"),
                Is.Null);

            var errors = new List<string>();
            Assert.That(
                EarthMotionCatalogValidator.Validate(_catalog, errors),
                Is.EqualTo(EarthMotionCatalogValidationIssue.None),
                string.Join("\n", errors));
        }

        [Test]
        public void RuntimeStatesAndAuthoredTransitionPairResolveVerifiedCatalogProfiles()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuilder.Rebuild(_catalog);

            EarthMotionSemanticAction[] requiredRoles =
            {
                EarthMotionSemanticAction.Locomotion,
                EarthMotionSemanticAction.Jump,
                EarthMotionSemanticAction.Cast,
                EarthMotionSemanticAction.Impact,
                EarthMotionSemanticAction.Recovery
            };
            string[] requiredPaths =
            {
                "Base Layer.Locomotion",
                "Base Layer.Jump",
                "Earth Magic Upper Body.Earth Cast",
                "Impact Additive.Recoil",
                "Base Layer.Knockdown Recovery"
            };
            for (int roleIndex = 0; roleIndex < requiredRoles.Length; roleIndex++)
            {
                int bindingCount = 0;
                for (int index = 0; index < _catalog.StateBindingCount; index++)
                {
                    EarthMotionStateBinding binding = _catalog.StateBindingAt(index);
                    if (binding.SemanticRole != requiredRoles[roleIndex]) continue;
                    bindingCount++;
                    EarthMotionClipProfile exact =
                        _catalog.ClipAt(binding.ClipProfileIndexAt(0));
                    Assert.That(
                        _catalog.TryResolveControllerState(
                            binding.StateHash,
                            exact.Clip,
                            requiredRoles[roleIndex],
                            out EarthMotionStateResolution resolution),
                        Is.True,
                        binding.StatePath);
                    Assert.That(resolution.IsVerified, Is.True, binding.StatePath);
                    Assert.That(
                        resolution.Kind,
                        Is.EqualTo(EarthMotionStateResolutionKind.ExactActiveClip));
                    Assert.That(resolution.Profile, Is.SameAs(exact));
                }
                Assert.That(bindingCount, Is.GreaterThan(0), requiredRoles[roleIndex].ToString());
                Assert.That(
                    FindBinding(_catalog, requiredPaths[roleIndex]),
                    Is.Not.Null,
                    requiredPaths[roleIndex]);
            }

            EarthMotionStateBinding jump = FindBinding(_catalog, "Base Layer.Jump");
            Assert.That(jump, Is.Not.Null);
            Assert.That(jump.ClipProfileCount, Is.EqualTo(1));
            EarthMotionClipProfile jumpProfile =
                _catalog.ClipAt(jump.ClipProfileIndexAt(0));
            Assert.That(jumpProfile, Is.Not.Null);
            Assert.That(jumpProfile.Clip, Is.Not.Null);
            Assert.That(
                jumpProfile.SourceAssetPath,
                Is.EqualTo(EarthHumanoidMotionSetup.KayKitMovementBasicPath));
            Assert.That(
                jumpProfile.Clip.name,
                Is.EqualTo(EarthHumanoidMotionSetup.JumpStartClipName));
            Assert.That(
                jumpProfile.SemanticAction,
                Is.EqualTo(EarthMotionSemanticAction.Jump));
            Assert.That(jumpProfile.AssetGuid, Is.Not.Empty);
            Assert.That(jumpProfile.LocalFileId, Is.Not.Zero);

            EarthMotionStateBinding source = FindBinding(
                _catalog,
                "Base Layer.Locomotion");
            EarthMotionStateBinding destination = FindBinding(
                _catalog,
                "Base Layer.Knockdown Recovery");
            EarthNormalizedAnimationWindow disabled = default;
            var rule = new EarthTransitionRule(
                true,
                EarthTransitionFamily.PoseInertialized,
                EarthAnimationTransitionPriority.HeavyImpact,
                0.06f,
                0.1f,
                EarthTransitionGaitPhaseRule.FixedTarget,
                EarthTransitionContactPolicy.ReleaseBeforeBlend,
                EarthTransitionCancelPolicy.Always,
                in disabled,
                in disabled,
                0.55f,
                EarthTransitionBodyMask.FullBody,
                EarthTransitionFootReleasePolicy.ReleaseImmediately,
                0f,
                false);
            _transitionProfile = ScriptableObject.CreateInstance<EarthTransitionProfile>();
            _transitionProfile.Configure(
                true,
                false,
                4,
                0.08f,
                new[]
                {
                    new EarthTransitionPairOverride(
                        EarthMotionStateId.Locomotion,
                        EarthMotionStateId.KnockdownRecovery,
                        in rule)
                });
            EarthAnimationTransitionContext context = TransitionContext();

            Assert.That(
                EarthMotionTransitionCatalogResolver.TryResolveAuthoredPair(
                    _catalog,
                    _transitionProfile,
                    source.StateHash,
                    destination.StateHash,
                    in context,
                    out EarthVerifiedTransitionPair pair),
                Is.True);
            Assert.That(pair.IsVerified, Is.True);
            Assert.That(pair.PairIndex, Is.Zero);
            Assert.That(pair.Source.SemanticRole,
                Is.EqualTo(EarthMotionSemanticAction.Locomotion));
            Assert.That(pair.Destination.SemanticRole,
                Is.EqualTo(EarthMotionSemanticAction.Recovery));
            Assert.That(pair.Rule.Family, Is.EqualTo(EarthTransitionFamily.PoseInertialized));
        }

        [Test]
        public void ClosestFrontBackRecoveryKeepsCatalogProvenanceAndAuthoredMarkers()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuilder.Rebuild(_catalog);
            EarthMotionStateBinding recovery = FindBinding(
                _catalog,
                "Base Layer.Knockdown Recovery");
            EarthRecoveryMarkerProfile frontMarkers =
                new EarthRecoveryMarkerProfile(0.31f, 0.67f, 0.92f);
            EarthRecoveryMarkerProfile backMarkers =
                new EarthRecoveryMarkerProfile(0.36f, 0.72f, 0.96f);
            EarthRecoveryPoseFeature farFront = RecoveryFeature(-0.9f);
            EarthRecoveryPoseFeature closeFront = RecoveryFeature(0.82f);
            EarthRecoveryPoseFeature closeBack = RecoveryFeature(-0.74f);
            var database = new EarthRecoveryPoseDatabase(new[]
            {
                RecoveryCandidate(
                    10u,
                    recovery.StateHash,
                    EarthRecoveryOrientation.Front,
                    in farFront,
                    in frontMarkers),
                RecoveryCandidate(
                    20u,
                    recovery.StateHash,
                    EarthRecoveryOrientation.Front,
                    in closeFront,
                    in frontMarkers),
                RecoveryCandidate(
                    30u,
                    recovery.StateHash,
                    EarthRecoveryOrientation.Back,
                    in closeBack,
                    in backMarkers)
            });
            EarthRecoveryPoseMatchWeights weights = EarthRecoveryPoseMatchWeights.Default;
            EarthRecoveryPoseFeature currentFront = RecoveryFeature(0.8f);

            Assert.That(
                EarthRecoveryCatalogResolver.TryResolveClosest(
                    _catalog,
                    database,
                    EarthRecoveryOrientation.Front,
                    in currentFront,
                    in weights,
                    out EarthRecoveryCatalogMatch front),
                Is.True);
            Assert.That(front.IsVerified, Is.True);
            Assert.That(front.PoseMatch.Candidate.ClipId, Is.EqualTo(20u));
            Assert.That(front.Motion.SemanticRole,
                Is.EqualTo(EarthMotionSemanticAction.Recovery));
            Assert.That(front.Markers.FeetEnablePhase,
                Is.EqualTo(frontMarkers.FeetEnablePhase));
            Assert.That(front.Markers.ControlsEnablePhase,
                Is.EqualTo(frontMarkers.ControlsEnablePhase));
            Assert.That(front.Markers.ExitPhase, Is.EqualTo(frontMarkers.ExitPhase));

            EarthRecoveryClearanceResult clearance =
                new EarthRecoveryClearanceResult(
                    EarthRecoveryClearanceKind.BasePose,
                    0f,
                    true);
            var recoveryResult = new EarthRecoveryResult(
                front.PoseMatch.Candidate.Orientation,
                front.PoseMatch.Candidate.ClipId,
                front.PoseMatch.Candidate.AnimationStateId,
                front.PoseMatch.Candidate.EntryPhase,
                front.PoseMatch.Cost,
                float3.zero,
                float3.zero,
                quaternion.identity,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                in clearance,
                in frontMarkers,
                false);
            var coordinator = new EarthPhysicalAnimationCoordinator();
            Assert.That(
                coordinator.TryBeginFullRagdoll(
                    CharacterPhysicalMode.FullRagdoll,
                    1u),
                Is.True);
            Assert.That(
                coordinator.TryBeginPoseMatchedRecovery(
                    CharacterPhysicalMode.Recovery,
                    2u,
                    in recoveryResult),
                Is.True);
            Assert.That(
                coordinator.TryAdvancePoseMatchedRecovery(
                    CharacterPhysicalMode.Recovery,
                    frontMarkers.FeetEnablePhase,
                    true,
                    out EarthPhysicalAnimationOwnership feetOwnership),
                Is.True);
            Assert.That(feetOwnership.FeetEnabled, Is.True);
            Assert.That(feetOwnership.ControlsEnabled, Is.False);
            coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery,
                frontMarkers.ControlsEnablePhase,
                true,
                out EarthPhysicalAnimationOwnership controlOwnership);
            Assert.That(controlOwnership.ControlsEnabled, Is.True);
            Assert.That(controlOwnership.RecoveryExitReady, Is.False);
            coordinator.TryAdvancePoseMatchedRecovery(
                CharacterPhysicalMode.Recovery,
                frontMarkers.ExitPhase,
                true,
                out EarthPhysicalAnimationOwnership exitOwnership);
            Assert.That(exitOwnership.RecoveryExitReady, Is.True);

            EarthRecoveryPoseFeature currentBack = RecoveryFeature(-0.7f);
            Assert.That(
                EarthRecoveryCatalogResolver.TryResolveClosest(
                    _catalog,
                    database,
                    EarthRecoveryOrientation.Back,
                    in currentBack,
                    in weights,
                    out EarthRecoveryCatalogMatch back),
                Is.True);
            Assert.That(back.PoseMatch.Candidate.ClipId, Is.EqualTo(30u));
            Assert.That(back.Markers.ExitPhase, Is.EqualTo(backMarkers.ExitPhase));
            Assert.That(back.Motion.Profile.AssetGuid, Is.Not.Empty);
            Assert.That(back.Motion.Profile.LocalFileId, Is.Not.Zero);
        }

        [Test]
        public void RuntimeStateAndPairBindingHotPathAllocatesNoManagedMemory()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuilder.Rebuild(_catalog);
            EarthMotionStateBinding binding = FindBinding(
                _catalog,
                "Base Layer.Locomotion");
            AnimationClip clip = _catalog.ClipAt(binding.ClipProfileIndexAt(0)).Clip;
            EarthMotionStateBinding destination = FindBinding(
                _catalog,
                "Base Layer.Knockdown Recovery");
            EarthTransitionRule rule = EarthTransitionRule.FixedFallback(
                EarthAnimationTransitionPriority.HeavyImpact,
                0.1f);
            _transitionProfile = ScriptableObject.CreateInstance<EarthTransitionProfile>();
            _transitionProfile.Configure(
                true,
                false,
                4,
                0.08f,
                new[]
                {
                    new EarthTransitionPairOverride(
                        EarthMotionStateId.Locomotion,
                        EarthMotionStateId.KnockdownRecovery,
                        in rule)
                });
            EarthAnimationTransitionContext context = TransitionContext();
            _catalog.TryResolveControllerState(binding.StateHash, clip, out _);
            EarthMotionTransitionCatalogResolver.TryResolveAuthoredPair(
                _catalog,
                _transitionProfile,
                binding.StateHash,
                clip,
                destination.StateHash,
                null,
                in context,
                out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                if (!_catalog.TryResolveControllerState(
                        binding.StateHash,
                        clip,
                        out EarthMotionStateResolution resolution) ||
                    !resolution.IsVerified)
                    Assert.Fail("runtime state binding changed");
                if (!EarthMotionTransitionCatalogResolver.TryResolveAuthoredPair(
                        _catalog,
                        _transitionProfile,
                        binding.StateHash,
                        clip,
                        destination.StateHash,
                        null,
                        in context,
                        out EarthVerifiedTransitionPair pair) ||
                    !pair.IsVerified)
                    Assert.Fail("authored transition pair binding changed");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void ConsecutiveRebuildIsDeterministicAndPreservesManualCorrection()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuildSummary first =
                EarthMotionCatalogBuilder.Rebuild(_catalog);
            string[] firstIdentityOrder = IdentityOrder(_catalog);
            string[] firstStateBindingOrder = StateBindingOrder(_catalog);
            EarthMotionSemanticAction corrected = EarthMotionSemanticAction.Surf;

            var serialized = new SerializedObject(_catalog);
            SerializedProperty entry = serialized.FindProperty("clips")
                .GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("semanticAction").enumValueIndex = (int)corrected;
            entry.FindPropertyRelative("manualCorrections").intValue =
                (int)EarthMotionManualCorrection.SemanticAction;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EarthMotionCatalogBuildSummary second =
                EarthMotionCatalogBuilder.Rebuild(_catalog);

            Assert.That(second.IdentityHash, Is.EqualTo(first.IdentityHash));
            Assert.That(IdentityOrder(_catalog), Is.EqualTo(firstIdentityOrder));
            Assert.That(StateBindingOrder(_catalog), Is.EqualTo(firstStateBindingOrder));
            Assert.That(_catalog.ClipAt(0).SemanticAction, Is.EqualTo(corrected));
            Assert.That(
                _catalog.ClipAt(0).ManualCorrections,
                Is.EqualTo(EarthMotionManualCorrection.SemanticAction));
        }

        [Test]
        public void CatalogLookupHotPathAllocatesNoManagedMemory()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuilder.Rebuild(_catalog);
            EarthMotionClipProfile expected = _catalog.ClipAt(17);
            _catalog.TryFind(expected.AssetGuid, expected.LocalFileId, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                if (!_catalog.TryFind(
                        expected.AssetGuid,
                        expected.LocalFileId,
                        out EarthMotionClipProfile found) ||
                    !ReferenceEquals(found, expected))
                    Assert.Fail("deterministic catalog lookup changed");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void CatalogCurveContractIsTheExistingExactEight()
        {
            string[] expected =
            {
                "LeftFootContact",
                "RightFootContact",
                "LeftFootPhase",
                "RightFootPhase",
                "LandContact",
                "CanExit",
                "PelvisCompression",
                "RootEffort"
            };
            Assert.That(EarthAnimationClipMetadata.CurveCount, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
                Assert.That(EarthAnimationClipMetadata.CurveName(index), Is.EqualTo(expected[index]));
        }

        [Test]
        public void SimulationCatalogContractHasNoUnityObjectDependency()
        {
            string directory = Path.Combine(
                Application.dataPath,
                "Elemental/Simulation/Characters");
            string[] files = Directory.GetFiles(
                directory,
                "EarthMotionCatalog*.cs",
                SearchOption.TopDirectoryOnly);

            Assert.That(files, Is.Not.Empty);
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), file);
                Assert.That(source, Does.Not.Contain("AnimationClip"), file);
                Assert.That(source, Does.Not.Contain("ScriptableObject"), file);
            }
        }

        private static string[] IdentityOrder(EarthMotionCatalog catalog)
        {
            var identities = new string[catalog.ClipCount];
            for (int index = 0; index < identities.Length; index++)
            {
                EarthMotionClipProfile profile = catalog.ClipAt(index);
                identities[index] = profile.AssetGuid + ":" + profile.LocalFileId;
            }
            return identities;
        }

        private static EarthMotionStateBinding FindBinding(
            EarthMotionCatalog catalog,
            string statePath)
        {
            for (int index = 0; index < catalog.StateBindingCount; index++)
            {
                EarthMotionStateBinding binding = catalog.StateBindingAt(index);
                if (binding != null && string.Equals(
                        binding.StatePath,
                        statePath,
                        StringComparison.Ordinal))
                    return binding;
            }
            return null;
        }

        private static EarthMotionClipProfile FindProfile(
            EarthMotionCatalog catalog,
            string sourcePath,
            string clipName)
        {
            for (int index = 0; index < catalog.ClipCount; index++)
            {
                EarthMotionClipProfile profile = catalog.ClipAt(index);
                if (profile != null &&
                    string.Equals(profile.SourceAssetPath, sourcePath, StringComparison.Ordinal) &&
                    string.Equals(profile.Clip?.name, clipName, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

        private static string[] StateBindingOrder(EarthMotionCatalog catalog)
        {
            var order = new string[catalog.StateBindingCount];
            for (int bindingIndex = 0;
                 bindingIndex < catalog.StateBindingCount;
                 bindingIndex++)
            {
                EarthMotionStateBinding binding = catalog.StateBindingAt(bindingIndex);
                string value = binding.LayerIndex + ":" + binding.StatePath + ":" +
                               binding.StateHash + ":" + binding.SemanticRole;
                for (int profileIndex = 0;
                     profileIndex < binding.ClipProfileCount;
                     profileIndex++)
                    value += ":" + binding.ClipProfileIndexAt(profileIndex);
                order[bindingIndex] = value;
            }
            return order;
        }

        private static EarthAnimationTransitionContext TransitionContext() =>
            new EarthAnimationTransitionContext(
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.KnockdownRecovery,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.RagdollRecovery,
                EarthAnimationTransitionPriority.HeavyImpact,
                EarthAnimationTransitionPriority.Locomotion,
                0.25f,
                0.5f,
                1f,
                0f,
                0f,
                false,
                true,
                false,
                true);

        private static EarthRecoveryPoseCandidate RecoveryCandidate(
            uint clipId,
            int stateHash,
            EarthRecoveryOrientation orientation,
            in EarthRecoveryPoseFeature feature,
            in EarthRecoveryMarkerProfile markers) =>
            new EarthRecoveryPoseCandidate(
                clipId,
                stateHash,
                orientation,
                0.55f,
                in feature,
                float3.zero,
                in markers);

        private static EarthRecoveryPoseFeature RecoveryFeature(float x) =>
            new EarthRecoveryPoseFeature(
                new float3(x, 0.5f, 0.1f),
                new float3(x - 0.2f, 0.3f, 0.2f),
                new float3(x + 0.2f, 0.3f, 0.2f),
                new float3(x - 0.1f, 0f, 0.3f),
                new float3(x + 0.1f, 0f, 0.3f),
                new float3(0f, 0f, 1f));
    }
}
