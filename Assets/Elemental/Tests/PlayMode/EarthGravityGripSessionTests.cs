using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthGravityGripSessionTests
    {
        [Test]
        public void SessionKeepsFortyTargetsAndRejectsReusedGeneration()
        {
            var session = new EarthGravityGripSession(48);
            var objects = new GameObject[40];
            var targets = new SessionTarget[40];
            for (int index = 0; index < targets.Length; index++)
            {
                objects[index] = new GameObject($"Grip Target {index:00}");
                Rigidbody body = objects[index].AddComponent<Rigidbody>();
                body.isKinematic = false;
                targets[index] = new SessionTarget((uint)(index + 1), 1u, body);
                Assert.That(session.TryAdd(targets[index], 48), Is.True);
            }

            Assert.That(session.Count, Is.EqualTo(40));
            targets[11].Generation = 2u;
            Assert.That(session.GetTarget(11), Is.Null, "A pooled target reused for another generation must not remain captured.");
            session.RemoveAtSwapBack(11);
            Assert.That(session.Count, Is.EqualTo(39));
            session.ReleaseAll(EarthMagicGripKind.GravityWell);
            Assert.That(session.Count, Is.Zero);
            for (int index = 0; index < objects.Length; index++) Object.DestroyImmediate(objects[index]);
        }

        [Test]
        public void LegacyProfileWithMissingClusterFieldsMigratesToSafeDefaults()
        {
            EarthGravityWellProfile profile = ScriptableObject.CreateInstance<EarthGravityWellProfile>();
            JsonUtility.FromJsonOverwrite(
                "{\"maximumCapturedTargets\":0,\"clusterStiffness\":0," +
                "\"clusterDamping\":0,\"clusterOrbitRadius\":0," +
                "\"clusterAngularDamping\":0,\"clusterMaximumAcceleration\":0}",
                profile);

            Assert.That(profile.MaximumCapturedTargets, Is.EqualTo(48));
            Assert.That(profile.ClusterStiffness, Is.EqualTo(16f));
            Assert.That(profile.ClusterDamping, Is.EqualTo(5.5f));
            Assert.That(profile.ClusterOrbitRadius, Is.EqualTo(1.35f));
            Assert.That(profile.ClusterAngularDamping, Is.EqualTo(6.5f));
            Assert.That(profile.ClusterMaximumAcceleration, Is.EqualTo(62f));
            Object.DestroyImmediate(profile);
        }

        private sealed class SessionTarget : IEarthPhysicalTarget
        {
            private readonly uint _stableId;

            public SessionTarget(uint stableId, uint generation, Rigidbody body)
            {
                _stableId = stableId;
                Generation = generation;
                Body = body;
            }

            public uint Generation { get; set; }
            public Rigidbody Body { get; }
            public uint StableEarthId => _stableId;
            public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(_stableId, Generation);
            public float EarthMass => Body.mass;
            public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.WallPiece;
            public bool IsEarthTargetValid => Body != null;
            public void OnEarthMagicGrabbed(EarthMagicGripKind grip) { }
            public void OnEarthMagicReleased(EarthMagicGripKind grip) { }
        }
    }
}
