using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMixamoPresentationTests
    {
        private const string CharacterPath = "Assets/ThirdParty/Mixamo/X Bot.fbx";
        private const string WalkPath = "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx";
        private const string WalkBackPath = "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx";
        private const string PunchPath = "Assets/ThirdParty/Mixamo/X Bot@Punching.fbx";
        private const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";

        [Test]
        public void XBotImportsAsAValidSkinnedHumanoid()
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
        [TestCase(PunchPath, false)]
        public void CuratedMixamoMotionsOwnValidHumanoidAvatars(string path, bool shouldLoop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel),
                "Mixamo namespace differences make CopyFromOther an invalid import strategy for these files.");

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
            Assert.That(dependencies, Does.Contain(PunchPath));
        }
    }
}
