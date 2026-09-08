using System.Collections.Generic;
using Elemental.Authoring;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class LocomotionCatalogAssetTests
    {
        [Test]
        public void EveryCatalogQueryExistsInTheShippedPoseBinary()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LocomotionClipCatalog>(LocomotionRhythmAuthoring.CatalogPath);
            var data = AssetDatabase.LoadAssetAtPath<global::MotionMatching.MotionMatchingData>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibraryData.asset");
            Assert.That(catalog, Is.Not.Null); Assert.That(data, Is.Not.Null);
            Assert.That(new global::MotionMatching.PoseSerializer().Deserialize(data.GetAssetPath(), data.name, data, out var poses), Is.True);
            try
            {
                foreach (var entry in catalog.Entries)
                {
                    Assert.That(poses.TryGetTag(entry.SourceQueryTag, out var tag), Is.True,
                        "Derived database is stale or baking failed: " + entry.SourceQueryTag);
                    int valid = 0;
                    for (int r = 0; r < tag.NumberRanges; r++)
                        for (int f = tag.GetStartRanges()[r]; f < tag.GetEndRanges()[r]; f++)
                            if (poses.IsPoseValidForPrediction(f)) valid++;
                    Assert.That(valid, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(entry.CycleSeconds / poses.FrameTime)),
                        "Every authored phase needs a valid future horizon: " + entry.SourceQueryTag);
                }
            }
            finally { poses.Dispose(); }
        }
        [Test]
        public void SearchableEammLocomotionUsesExactlyCurrentControllerChildren()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            var library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            Assert.That(controller, Is.Not.Null); Assert.That(library, Is.Not.Null);
            var expected = new HashSet<AnimationClip>();
            foreach (var state in controller.layers[0].stateMachine.states)
                if (state.state.name == "Locomotion" && state.state.motion is BlendTree tree)
                    foreach (var child in tree.children) if (child.motion is AnimationClip clip) expected.Add(clip);
            var actual = new HashSet<AnimationClip>();
            int recipes = 0;
            foreach (var recipe in library.clips)
                if (recipe.role is MotionClipRole.Idle or MotionClipRole.Locomotion)
                { actual.Add(recipe.clip); recipes++; }
            Assert.That(expected.Count, Is.GreaterThan(2));
            Assert.That(actual.SetEquals(expected), Is.True, "EAMM must not resurrect old locomotion clips behind an unchanged controller.");
            Assert.That(recipes, Is.EqualTo(expected.Count), "Idempotent sync must not accumulate duplicate recipes.");
        }
    }
}
