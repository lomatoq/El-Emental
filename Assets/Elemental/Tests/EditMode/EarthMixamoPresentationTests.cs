using System.Linq;
using Elemental.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMixamoPresentationTests
    {
        private const string CharacterPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string WalkPath = "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx";
        private const string WalkBackPath = "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx";
        private const string PushPath = "Assets/ThirdParty/Mixamo/X Bot@Lead Jab.fbx";
        private const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";

        [Test]
        public void LinebreakerImportsAsAValidSkinnedHumanoid()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                Is.GreaterThan(0), "The runtime character must deform; a rigid-part fallback is not acceptable.");

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CharacterPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.isValid, Is.True);
            Assert.That(avatar.isHuman, Is.True);
        }

        [TestCase(WalkPath, true)]
        [TestCase(WalkBackPath, true)]
        [TestCase(PushPath, false)]
        public void CuratedMixamoMotionsRemainValidHumanoidSources(string path, bool shouldLoop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther));
            Assert.That(importer.sourceAvatar, Is.Not.Null);
            Assert.That(importer.sourceAvatar.isValid, Is.True);
            Assert.That(importer.sourceAvatar.isHuman, Is.True,
                "Motion FBXs may keep their canonical Mixamo avatar; Mecanim retargets them onto Linebreaker at runtime.");

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
            Assert.That(clip, Is.Not.Null, path);
            Assert.That(clip.isHumanMotion, Is.True, path);

            if (!shouldLoop) return;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            Assert.That(clips, Is.Not.Empty, path);
            Assert.That(clips.All(candidate => candidate.loopTime), Is.True,
                "Walking clips must not freeze after their first pass.");
        }

        [Test]
        public void RuntimeControllerReferencesTheCuratedMixamoMotions()
        {
            string[] dependencies = AssetDatabase.GetDependencies(ControllerPath, true);
            Assert.That(dependencies, Does.Contain(WalkPath));
            Assert.That(dependencies, Does.Contain(WalkBackPath));
            Assert.That(dependencies, Does.Contain(PushPath));
        }

        [TestCase(EarthHumanoidMotionSetup.HardLandingPath)]
        [TestCase(EarthHumanoidMotionSetup.FallingRollPath)]
        public void CanonicalPhysicsClipsExtractRootTracksInsteadOfBakingThemIntoBones(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            Assert.That(clips, Is.Not.Empty, path);
            Assert.That(clips.All(candidate =>
                !candidate.lockRootRotation &&
                !candidate.lockRootHeightY &&
                !candidate.lockRootPositionXZ), Is.True,
                "PlanetMotor owns canonical motion; baked FBX root tracks visually detach the rig from its capsule.");
        }

        [Test]
        public void NeutralIdleAndSurfTransitionAreDistinctSubclips()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(
                    EarthHumanoidMotionSetup.StandToCrouchPath)
                .OfType<AnimationClip>()
                .Where(candidate => !candidate.name.StartsWith("__preview__"))
                .ToArray();
            Assert.That(clips.Any(candidate =>
                candidate.name == EarthHumanoidMotionSetup.NeutralIdleClipName && candidate.isLooping), Is.True);
            Assert.That(clips.Any(candidate =>
                candidate.name == "Standing Idle To Crouch" && !candidate.isLooping), Is.True);
        }
    }
}
