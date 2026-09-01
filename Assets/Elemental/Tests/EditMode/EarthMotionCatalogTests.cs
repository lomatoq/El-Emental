using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMotionCatalogTests
    {
        private EarthMotionCatalog _catalog;

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
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
            Assert.That(inventory, Does.Contain("unique GUID+localFileId total=51"));
            EarthMotionCatalogBuildSummary summary =
                EarthMotionCatalogBuilder.Rebuild(_catalog);

            Assert.That(summary.ClipCount, Is.EqualTo(51));
            Assert.That(summary.CopiedCurveClipCount + summary.DerivedCurveClipCount,
                Is.EqualTo(51));
            Assert.That(summary.IdentityHash, Is.Not.Empty);
            Assert.That(_catalog.ClipCount, Is.EqualTo(51));
            var identities = new HashSet<string>(StringComparer.Ordinal);
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

            var errors = new List<string>();
            Assert.That(
                EarthMotionCatalogValidator.Validate(_catalog, errors),
                Is.EqualTo(EarthMotionCatalogValidationIssue.None),
                string.Join("\n", errors));
        }

        [Test]
        public void ConsecutiveRebuildIsDeterministicAndPreservesManualCorrection()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuildSummary first =
                EarthMotionCatalogBuilder.Rebuild(_catalog);
            string[] firstIdentityOrder = IdentityOrder(_catalog);
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
    }
}
