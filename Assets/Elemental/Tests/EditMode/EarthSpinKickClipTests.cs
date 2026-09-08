using Elemental.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthSpinKickClipTests
    {
        [Test]
        public void KickUsesFullAuthoredExtensionAndRecovery()
        {
            Assert.That(EarthSpinKickClipAuthoring.SourceTime(0f, .5f, 1.1f, 2.03f), Is.Zero);
            Assert.That(EarthSpinKickClipAuthoring.SourceTime(.5f, .5f, 1.1f, 2.03f), Is.EqualTo(1.1f).Within(.001f));
            Assert.That(EarthSpinKickClipAuthoring.SourceTime(.75f, .5f, 1.1f, 2.03f), Is.GreaterThan(1.1f));
            Assert.That(EarthSpinKickClipAuthoring.SourceTime(1f, .5f, 1.1f, 2.03f), Is.EqualTo(2.03f));
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EarthSpinKickClipAuthoring.KickClipPath);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.isHumanMotion, Is.True);
            Assert.That(clip.length, Is.EqualTo(.78f).Within(.002f));
            var importer = (ModelImporter)AssetImporter.GetAtPath(EarthHumanoidMotionSetup.MmaKickPath);
            Assert.That(importer.clipAnimations[0].lastFrame,
                Is.EqualTo(importer.defaultClipAnimations[0].lastFrame), "Source kick was trimmed again.");
        }

        [Test]
        public void BakedFinisherIsHumanoidWithStationaryHorizontalBodyAndNoEvents()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EarthSpinKickClipAuthoring.ClipPath);
            Assert.That(clip, Is.Not.Null, "Run Elemental/Character/Bake Earth Spin Kick before installing combo content.");
            Assert.That(clip.isHumanMotion, Is.True);
            Assert.That(clip.length, Is.EqualTo(1.1f).Within(.002f));
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty,
                "Gameplay contact is generation-owned; imported animation events must not duplicate shots.");
            foreach (string axis in new[] { "x", "z" })
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis));
                Assert.That(curve, Is.Not.Null);
                foreach (Keyframe key in curve.keys)
                    Assert.That(key.value, Is.EqualTo(curve.Evaluate(0f)).Within(.0001f));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            Assert.That(settings.loopTime, Is.False);
            Assert.That(settings.loopBlendOrientation && settings.loopBlendPositionXZ && settings.loopBlendPositionY, Is.True);
        }

        [Test]
        public void BakedFinisherContainsFullContinuousBodyTurn()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EarthSpinKickClipAuthoring.ClipPath);
            Assert.That(clip, Is.Not.Null);
            var curves = new AnimationCurve[4];
            string[] axes = { "x", "y", "z", "w" };
            for (int i = 0; i < 4; i++)
            {
                curves[i] = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[i]));
                Assert.That(curves[i], Is.Not.Null);
            }
            Vector3 previous = Vector3.zero;
            float angle = 0f;
            for (int i = 0; i <= 120; i++)
            {
                float t = clip.length * i / 120f;
                var q = new Quaternion(curves[0].Evaluate(t), curves[1].Evaluate(t),
                    curves[2].Evaluate(t), curves[3].Evaluate(t)).normalized;
                Vector3 heading = Vector3.ProjectOnPlane(q * Vector3.forward, Vector3.up).normalized;
                if (i > 0) angle += Vector3.SignedAngle(previous, heading, Vector3.up);
                previous = heading;
            }
            Assert.That(Mathf.Abs(angle), Is.GreaterThan(300f));
        }
    }
}
