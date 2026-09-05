using System;
using Elemental.Authoring;
using Elemental.Authoring.Editor;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthBackwardLocomotionTests
    {
        [Test]
        public void BackwardRunRecipeIsSearchableAndKeepsExistingCatalogEntries()
        {
            var library = ScriptableObject.CreateInstance<MotionLibraryAsset>();
            var clip = new AnimationClip();
            var existing = new MotionClipRecipe { stableId = "user.motion", nominalSpeed = 3.17f };
            library.clips.Add(existing);
            try
            {
                Assert.That(EarthBackwardRunSetup.EnsureBackwardRunRecipe(library, clip), Is.True);
                Assert.That(EarthBackwardRunSetup.EnsureBackwardRunRecipe(library, clip), Is.False);
                Assert.That(library.clips.Count, Is.EqualTo(2));
                Assert.That(library.clips[0], Is.SameAs(existing));
                Assert.That(existing.nominalSpeed, Is.EqualTo(3.17f));
                MotionClipRecipe recipe = library.clips[1];
                Assert.That(recipe.clip, Is.SameAs(clip));
                Assert.That(recipe.role, Is.EqualTo(MotionClipRole.Locomotion));
                Assert.That(recipe.semantic, Is.EqualTo(MotionSemantic.RunBackward));
                Assert.That(recipe.nominalSpeed, Is.EqualTo(6f));
                Assert.That(recipe.nominalDirection, Is.EqualTo(180f));
                Assert.That(recipe.loop, Is.True);
            }
            finally
            {
                Undo.ClearUndo(library);
                UnityEngine.Object.DestroyImmediate(library);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void DedicatedBackwardImportRestoresFullOriginalTakeWithoutChangingOtherSettings()
        {
            var clip = new ModelImporterClipAnimation
            {
                name = "Authored Backward Run", firstFrame = 8f, lastFrame = 19f,
                cycleOffset = .3f, loopTime = true
            };
            var original = new ModelImporterClipAnimation { firstFrame = 0f, lastFrame = 46f };
            Assert.That(EarthBackwardRunSetup.RestoreFullTakeRange(clip, original), Is.True);
            Assert.That(clip.firstFrame, Is.EqualTo(0f));
            Assert.That(clip.lastFrame, Is.EqualTo(46f));
            Assert.That(clip.name, Is.EqualTo("Authored Backward Run"));
            Assert.That(clip.cycleOffset, Is.EqualTo(.3f));
            Assert.That(clip.loopTime, Is.True);
            Assert.That(EarthBackwardRunSetup.RestoreFullTakeRange(clip, original), Is.False);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SignedTravelReachesBackwardRunAndSettlesWithoutPositiveSpeedSubstitution(int fps)
        {
            var state = new EarthLocomotionBlendState();
            float2 value = default;
            for (int i = 0; i < fps; i++)
                value = EarthLocomotionBlend.Step(ref state, new float3(0f, 0f, 7.2f), math.up(), math.forward(), true, 1f / fps);
            Assert.That(value.y, Is.GreaterThan(7f));

            for (int i = 0; i < fps; i++)
                value = EarthLocomotionBlend.Step(ref state, new float3(0f, 0f, -7.2f), math.up(), math.forward(), true, 1f / fps);
            Assert.That(value.y, Is.LessThan(-7f), "Backward run at -6 must be reachable.");
            Assert.That(value.x, Is.EqualTo(0f).Within(0.0001f));
            float firstStopped = EarthLocomotionBlend.Step(ref state, float3.zero, math.up(), math.forward(), false, 1f / fps).y;
            Assert.That(firstStopped, Is.LessThan(-5f), "A stop must filter the signed target, not instantly snap it to zero.");
            for (int i = 0; i < fps; i++)
                value = EarthLocomotionBlend.Step(ref state, float3.zero, math.up(), math.forward(), false, 1f / fps);
            Assert.That(value.y, Is.EqualTo(0f).Within(0.005f));
        }

        [Test]
        public void LateralTravelOnRotatedPlanetHasNoForwardBlendBias()
        {
            var state = new EarthLocomotionBlendState();
            float3 up = new float3(1f, 0f, 0f);
            float3 facing = new float3(0f, 1f, 0f);
            float3 velocity = math.cross(up, facing) * -4f + up * 9f;
            float2 value = default;
            for (int i = 0; i < 60; i++)
                value = EarthLocomotionBlend.Step(ref state, velocity, up, facing, true, 1f / 60f);
            Assert.That(value.x, Is.EqualTo(-4f).Within(0.005f));
            Assert.That(value.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void BackwardRunInsertionPreservesExistingChildrenAndIsIdempotent()
        {
            var tree = NewTree();
            var walk = new AnimationClip();
            var run = new AnimationClip();
            try
            {
                tree.children = new[] { new ChildMotion
                {
                    motion = walk, position = new Vector2(0f, -2f), timeScale = 0.8f,
                    cycleOffset = 0.27f, mirror = true
                } };
                Assert.That(EarthBackwardRunSetup.EnsureBackwardRunChild(tree, run), Is.True);
                Assert.That(EarthBackwardRunSetup.EnsureBackwardRunChild(tree, run), Is.False);
                Assert.That(tree.children.Length, Is.EqualTo(2));
                Assert.That(tree.children[0].motion, Is.SameAs(walk));
                Assert.That(tree.children[0].position, Is.EqualTo(new Vector2(0f, -2f)));
                Assert.That(tree.children[0].timeScale, Is.EqualTo(0.8f));
                Assert.That(tree.children[0].cycleOffset, Is.EqualTo(0.27f));
                Assert.That(tree.children[0].mirror, Is.True);
                Assert.That(tree.children[1].position, Is.EqualTo(new Vector2(0f, -6f)));
                Assert.That(tree.children[1].timeScale, Is.EqualTo(1f));
                Assert.That(tree.children[1].mirror, Is.False);
            }
            finally
            {
                Undo.ClearUndo(tree);
                UnityEngine.Object.DestroyImmediate(tree);
                UnityEngine.Object.DestroyImmediate(walk);
                UnityEngine.Object.DestroyImmediate(run);
            }
        }

        [Test]
        public void OccupiedBackwardRunCoordinateIsNotOverwritten()
        {
            var tree = NewTree();
            var authored = new AnimationClip();
            var incoming = new AnimationClip();
            try
            {
                tree.AddChild(authored, new Vector2(0f, -6f));
                Assert.Throws<InvalidOperationException>(() => EarthBackwardRunSetup.EnsureBackwardRunChild(tree, incoming));
                Assert.That(tree.children.Length, Is.EqualTo(1));
                Assert.That(tree.children[0].motion, Is.SameAs(authored));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
                UnityEngine.Object.DestroyImmediate(authored);
                UnityEngine.Object.DestroyImmediate(incoming);
            }
        }

        private static BlendTree NewTree() => new BlendTree
        {
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false
        };
    }
}
