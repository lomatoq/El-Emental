using Elemental.Authoring;
using Elemental.Authoring.Editor.MotionMatching;
using Elemental.Presentation.MotionMatching;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode.MotionMatching
{
    public sealed class MotionLibraryValidationTests
    {
        [Test]
        public void EmptyLibrary_IsRejectedWithActionableErrors()
        {
            MotionLibraryAsset library = ScriptableObject.CreateInstance<MotionLibraryAsset>();
            try
            {
                var errors = MotionLibraryBuilder.Validate(library);
                Assert.That(errors, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(string.Join(" ", errors), Does.Contain("Source rig"));
                Assert.That(string.Join(" ", errors), Does.Contain("No clips"));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void ProductionPivot_IsBakedAsInPlaceRotation()
        {
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            Assert.That(library, Is.Not.Null);

            MotionClipRecipe pivot = null;
            for (int index = 0; index < library.clips.Count; index++)
            {
                MotionClipRecipe candidate = library.clips[index];
                if (candidate != null && candidate.stableId == "pivot.left.mixamo")
                {
                    pivot = candidate;
                    break;
                }
            }

            Assert.That(pivot, Is.Not.Null);
            Assert.That(pivot.role, Is.EqualTo(MotionClipRole.Pivot));
            Assert.That(pivot.nominalSpeed, Is.Zero,
                "A turn-in-place clip must not inject synthetic sideways translation into EAMM queries.");
            Assert.That(Mathf.Abs(pivot.nominalYaw), Is.GreaterThan(1f));
        }

        [Test]
        public void ProductionForwardWalkUsesTheRealMixamoTakeAndExcludesIncompatibleKayKitWalks()
        {
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            Assert.That(library, Is.Not.Null);

            MotionClipRecipe forward = library.clips.Find(recipe =>
                recipe != null && recipe.stableId == "walk.forward.mixamo");
            Assert.That(forward, Is.Not.Null);
            Assert.That(forward.clip, Is.Not.Null);
            Assert.That(forward.clip.name, Is.EqualTo("Walking"),
                "The one-frame XBot Walk Neutral helper is an idle seed, not searchable locomotion.");
            Assert.That(forward.clip.length, Is.GreaterThan(0.9f));
            Assert.That(AssetDatabase.GetAssetPath(forward.clip),
                Is.EqualTo("Assets/ThirdParty/Mixamo/X Bot@Walking.fbx"));

            foreach (string incompatible in new[]
                     {
                         "walk.forward.a", "walk.forward.b", "walk.forward.c",
                         "walk.backward.kaykit", "walk.crouch", "walk.sneak"
                     })
                Assert.That(library.clips.Exists(recipe =>
                    recipe != null && recipe.stableId == incompatible), Is.False,
                    $"{incompatible} has no matching production gameplay query and must not enter generic locomotion.");
        }

        [Test]
        public void ProductionBaseMotionsHaveExplicitSemanticQueryTags()
        {
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            Assert.That(library, Is.Not.Null);

            foreach (MotionClipRecipe recipe in library.clips)
            {
                if (recipe == null || recipe.role is MotionClipRole.Recovery or
                    MotionClipRole.Magic or MotionClipRole.Impact)
                    continue;

                string tag = MotionLibraryBuilder.ResolveQueryTag(recipe.role, recipe.semantic);
                Assert.That(tag, Is.Not.EqualTo(PlanetEAMMCharacterController.UnsearchableQueryTag),
                    $"{recipe.stableId}/{recipe.semantic} must not enter a generic locomotion pool.");
                if (recipe.role == MotionClipRole.Locomotion)
                    Assert.That(new[]
                    {
                        PlanetEAMMCharacterController.ForwardQueryTag,
                        PlanetEAMMCharacterController.BackwardQueryTag,
                        PlanetEAMMCharacterController.LeftQueryTag,
                        PlanetEAMMCharacterController.RightQueryTag
                    }, Does.Contain(tag));
            }

            MotionClipRecipe pivot = library.clips.Find(recipe =>
                recipe != null && recipe.role == MotionClipRole.Pivot);
            Assert.That(pivot, Is.Not.Null);
            Assert.That(MotionLibraryBuilder.ResolveQueryTag(pivot.role, pivot.semantic),
                Is.EqualTo(PlanetEAMMCharacterController.PivotQueryTag));
        }

        [Test]
        public void WorldSpaceFootSamplingFindsAlternatingPlantAndRejectsRaisedSwing()
        {
            const float dt = 1f / 30f;
            var left = new[]
            {
                new float3(-0.15f, 0f, 0f),
                new float3(-0.15f, 0.01f, 0f),
                new float3(-0.15f, 0.10f, 0.08f),
                new float3(-0.15f, 0.16f, 0.18f),
                new float3(-0.15f, 0.10f, 0.27f),
                new float3(-0.15f, 0.01f, 0.32f),
                new float3(-0.15f, 0f, 0.32f)
            };
            var right = new[]
            {
                new float3(0.15f, 0.16f, 0f),
                new float3(0.15f, 0.10f, 0.08f),
                new float3(0.15f, 0.01f, 0.14f),
                new float3(0.15f, 0f, 0.14f),
                new float3(0.15f, 0.01f, 0.14f),
                new float3(0.15f, 0.10f, 0.22f),
                new float3(0.15f, 0.16f, 0.32f)
            };

            Assert.That(MotionLibraryBuilder.DetectFootContact(left, 0, dt, false), Is.True);
            Assert.That(MotionLibraryBuilder.DetectFootContact(left, 1, dt, false), Is.False,
                "A low but rapidly rising swing sample must not become a plant solely from height.");
            Assert.That(MotionLibraryBuilder.DetectFootContact(left, 3, dt, false), Is.False);
            Assert.That(MotionLibraryBuilder.DetectFootContact(right, 3, dt, false), Is.True);
            Assert.That(MotionLibraryBuilder.DetectFootContact(right, 6, dt, false), Is.False);
        }

        [Test]
        public void WorldSpaceFootSamplingKeepsALoopSeamMinimumAsContact()
        {
            const float dt = 1f / 30f;
            var seamPlant = new[]
            {
                new float3(0f, 0f, 0f),
                new float3(0f, 0.06f, 0.04f),
                new float3(0f, 0.15f, 0.12f),
                new float3(0f, 0.07f, 0.20f),
                new float3(0f, 0f, 0.24f)
            };

            Assert.That(MotionLibraryBuilder.DetectFootContact(seamPlant, 0, dt, true), Is.True);
            Assert.That(MotionLibraryBuilder.DetectFootContact(seamPlant, 4, dt, true), Is.True);
            Assert.That(MotionLibraryBuilder.DetectFootContact(seamPlant, 2, dt, true), Is.False);
        }
    }
}
