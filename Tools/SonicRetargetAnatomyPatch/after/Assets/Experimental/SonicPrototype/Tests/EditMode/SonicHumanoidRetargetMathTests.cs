using NUnit.Framework;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype.Tests.EditMode
{
    public sealed class SonicHumanoidRetargetMathTests
    {
        [Test]
        public void NeutralSourcePreservesTheEvaluatedHumanoidReference()
        {
            Quaternion importedAvatarRest = Quaternion.Euler(90f, 0f, 0f);
            Quaternion evaluatedReference = Quaternion.Euler(3f, -7f, 2f);
            Quaternion result = SonicHumanoidRetargetMath.TargetLocal(
                Quaternion.identity,
                Quaternion.Euler(11f, 23f, -5f),
                evaluatedReference);

            Assert.That(Quaternion.Angle(result, evaluatedReference), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(result, importedAvatarRest), Is.GreaterThan(60f),
                "The imported Avatar T-pose is authoring metadata, not the evaluated base pose being replaced in OnAnimatorIK.");
        }

        [Test]
        public void NonCommutingRestAndHingeProduceAParentFrameDelta()
        {
            Quaternion sourceRest = Quaternion.Euler(17f, -21f, 9f);
            Quaternion sourceHinge = Quaternion.AngleAxis(38f, new Vector3(.2f, .9f, -.1f).normalized);
            Quaternion sourceCurrent = sourceRest * sourceHinge;
            Quaternion actual = SonicHumanoidRetargetMath.ParentFrameDelta(sourceCurrent, sourceRest);
            Quaternion expected = sourceRest * sourceHinge * Quaternion.Inverse(sourceRest);

            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(actual, sourceHinge), Is.GreaterThan(5f),
                "This regression must distinguish parent-frame pre-multiplication from a local post-multiply shortcut.");
        }

        [Test]
        public void BasisConjugationPreservesAngleAndMapsAxisIntoTargetParent()
        {
            Quaternion sourceParent = Quaternion.Euler(-13f, 31f, 8f);
            Quaternion targetParent = Quaternion.Euler(22f, -17f, 41f);
            Quaternion basis = SonicHumanoidRetargetMath.DeltaBasis(targetParent, sourceParent);
            Quaternion sourceDelta = Quaternion.AngleAxis(47f, Vector3.up);
            Quaternion targetReference = Quaternion.Euler(4f, 6f, -3f);
            Quaternion result = SonicHumanoidRetargetMath.TargetLocal(
                sourceDelta,
                basis,
                targetReference);
            Quaternion targetDelta = result * Quaternion.Inverse(targetReference);

            Assert.That(Quaternion.Angle(Quaternion.identity, targetDelta), Is.EqualTo(47f).Within(.001f));
            Vector3 expectedAxis = (basis * Vector3.up).normalized;
            Vector3 actualAxis = QuaternionToAxis(targetDelta);
            Assert.That(Mathf.Abs(Vector3.Dot(expectedAxis, actualAxis)), Is.GreaterThan(.9999f));
        }

        private static Vector3 QuaternionToAxis(Quaternion value)
        {
            value.ToAngleAxis(out _, out Vector3 axis);
            return axis.normalized;
        }
    }
}
