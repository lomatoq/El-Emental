using NUnit.Framework;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype.Tests
{
    public sealed class SonicG1SkeletonTests
    {
        [Test]
        public void NeutralPose_EvaluatesFiniteSymmetricFeet()
        {
            float[] qpos = NeutralPose();
            var rotations = new Quaternion[SonicG1Skeleton.JointCount];
            var positions = new Vector3[SonicG1Skeleton.JointCount];

            Assert.That(SonicG1Skeleton.TryEvaluate(qpos, rotations, positions), Is.True);
            Assert.That(positions[5].x, Is.EqualTo(positions[11].x).Within(0.001f));
            Assert.That(positions[5].y, Is.EqualTo(-positions[11].y).Within(0.001f));
            Assert.That(positions[5].z, Is.EqualTo(positions[11].z).Within(0.001f));
            for (int index = 0; index < rotations.Length; index++)
            {
                Assert.That(float.IsFinite(rotations[index].x), Is.True, $"rotation {index}");
                Assert.That(float.IsFinite(positions[index].x), Is.True, $"position {index}");
            }
        }

        [Test]
        public void CoordinateMap_PreservesRotatedVectorsAcrossHandednessChange()
        {
            Quaternion sourceRotation = Quaternion.AngleAxis(37f, new Vector3(.3f, -.7f, .2f).normalized);
            Vector3 sourceVector = new Vector3(.27f, -.51f, .83f);

            Vector3 mappedAfterRotation = SonicG1Skeleton.MapPositionToUnity(sourceRotation * sourceVector);
            Vector3 rotatedAfterMap =
                SonicG1Skeleton.MapRotationToUnity(sourceRotation) * SonicG1Skeleton.MapPositionToUnity(sourceVector);

            Assert.That(Vector3.Distance(mappedAfterRotation, rotatedAfterMap), Is.LessThan(0.00001f));
        }

        [Test]
        public void KneeAngle_ChangesOnlyItsDescendantChain()
        {
            float[] neutral = NeutralPose();
            float[] bent = NeutralPose();
            bent[SonicG1Skeleton.JointOffset + 3] = .55f;
            var neutralRotations = new Quaternion[SonicG1Skeleton.JointCount];
            var neutralPositions = new Vector3[SonicG1Skeleton.JointCount];
            var bentRotations = new Quaternion[SonicG1Skeleton.JointCount];
            var bentPositions = new Vector3[SonicG1Skeleton.JointCount];

            Assert.That(SonicG1Skeleton.TryEvaluate(neutral, neutralRotations, neutralPositions), Is.True);
            Assert.That(SonicG1Skeleton.TryEvaluate(bent, bentRotations, bentPositions), Is.True);
            Assert.That(Vector3.Distance(neutralPositions[5], bentPositions[5]), Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(neutralPositions[11], bentPositions[11]), Is.LessThan(.00001f));
        }

        [Test]
        public void NonFinitePose_IsRejected()
        {
            float[] qpos = NeutralPose();
            qpos[17] = float.NaN;
            Assert.That(
                SonicG1Skeleton.TryEvaluate(
                    qpos,
                    new Quaternion[SonicG1Skeleton.JointCount],
                    new Vector3[SonicG1Skeleton.JointCount]),
                Is.False);
        }

        private static float[] NeutralPose()
        {
            var qpos = new float[SonicG1Skeleton.PoseSize];
            qpos[2] = .78f;
            qpos[3] = 1f;
            return qpos;
        }
    }
}
