using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthStructureInteractionRuntimeTests
    {
        [UnityTest]
        public IEnumerator WallDrawnOnAStableSideRebindsToARealParentCellAfterFracture()
        {
            GameObject root = new GameObject("Attached Side Wall Runtime");
            root.SetActive(false);
            EarthWallPool wallPool = root.AddComponent<EarthWallPool>();
            wallPool.Configure(3, null, null);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            executor.Configure(null, null, root.transform, wallPool);
            root.SetActive(true);

            EarthWall parent = wallPool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3.4f,
                0.62f,
                11u);
            for (int tick = 0; tick < 42; tick++) yield return new WaitForFixedUpdate();
            Vector3 face = parent.transform.position + parent.transform.forward * (parent.Thickness * 0.5f);
            var path = new List<float3>
            {
                ToFloat3(face - parent.transform.right * 1.25f),
                ToFloat3(face + parent.transform.right * 1.25f)
            };
            Assert.That(executor.TryRaiseWallOnSurface(
                path,
                parent.transform.forward,
                0.38f,
                0.45f,
                12u,
                out EarthWall child,
                parent.WallId,
                EarthSurfaceKind.WallSide), Is.True);
            EarthStructureAttachment attachment = child.GetComponent<EarthStructureAttachment>();
            Assert.That(attachment, Is.Not.Null);
            Assert.That(attachment.ParentStructureId, Is.EqualTo(parent.WallId));
            Assert.That(Vector3.Angle(child.SurfaceUp, parent.transform.forward), Is.LessThan(1f),
                "A side-drawn wall must rise perpendicular to that face, not radial to the planet.");

            Assert.That(parent.ApplyStructureImpact(face, parent.transform.forward, 5200f), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(parent.IsCollapsing, Is.True);
            var activeParentCells = new IEarthPhysicalTarget[48];
            int activeParentCellCount = parent.CopyActiveTargetsNonAlloc(activeParentCells);
            Assert.That(activeParentCellCount, Is.GreaterThan(0),
                "A fractured support must publish its live cells to attachment and MMB consumers.");
            Assert.That(attachment.isActiveAndEnabled, Is.True,
                "The attachment solver must stay live while its child emerges.");
            Assert.That(attachment.ParentStructureId, Is.EqualTo(parent.WallId),
                "The attachment may not silently drop its parent before support transfer.");
            Assert.That(attachment.SupportHandle.IsValid, Is.True,
                "The child foundation must transfer to an actual surviving parent cell in the fracture tick.");
            FixedJoint joint = child.GetComponent<FixedJoint>();
            Assert.That(joint, Is.Not.Null);
            Assert.That(joint.connectedBody, Is.Not.Null);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IntersectingConstructionAlwaysDamagesTheOldWallAlongTheSweptVolume()
        {
            GameObject root = new GameObject("Construction Intersection Runtime");
            root.SetActive(false);
            EarthWallPool wallPool = root.AddComponent<EarthWallPool>();
            wallPool.Configure(3, null, null);
            root.SetActive(true);

            EarthWall oldWall = wallPool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3.2f,
                0.62f,
                21u);
            for (int tick = 0; tick < 42; tick++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            EarthWall crossing = wallPool.Acquire(
                new Vector3(0f, 24f, -3f),
                new Vector3(0f, 24f, 3f),
                Vector3.zero,
                3.2f,
                0.62f,
                22u);
            yield return new WaitForFixedUpdate();

            Assert.That(crossing, Is.Not.Null);
            Assert.That(oldWall.IsCollapsing, Is.True,
                "A new wall may not ghost through an old structure without a construction impact.");
            Assert.That(oldWall.ActiveFracturePieceCount, Is.GreaterThan(0));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CantileverPlatformBuildsOnWallSideAndPublishesAStableDeck()
        {
            GameObject root = new GameObject("Cantilever Platform Runtime");
            root.SetActive(false);
            EarthWallProfile wallProfile = ScriptableObject.CreateInstance<EarthWallProfile>();
            EarthPlatformProfile platformProfile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            EarthWallPool wallPool = root.AddComponent<EarthWallPool>();
            wallPool.Configure(3, null, null, wallProfile);
            EarthPlatformPool platformPool = root.AddComponent<EarthPlatformPool>();
            platformPool.Configure(3, null, platformProfile);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            executor.Configure(null, null, root.transform, wallPool);
            executor.ConfigureEarthExtensions(null, platformPool);
            root.SetActive(true);

            EarthWall parent = wallPool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                4.2f,
                0.65f,
                31u);
            for (int tick = 0; tick < 48; tick++) yield return new WaitForFixedUpdate();
            Vector3 face = parent.transform.position + parent.transform.forward * parent.Thickness * 0.5f;
            var path = new List<float3>
            {
                ToFloat3(face - parent.transform.right * 2f - parent.transform.up * 0.7f),
                ToFloat3(face + parent.transform.right * 2f - parent.transform.up * 0.7f),
                ToFloat3(face + parent.transform.right * 1.8f + parent.transform.up * 0.9f),
                ToFloat3(face - parent.transform.right * 1.7f + parent.transform.up * 0.9f)
            };
            Assert.That(executor.TryRaisePlatformOnSurface(
                path,
                parent.transform.forward,
                parent.transform.right,
                0.35f,
                32u,
                out EarthPlatform platform,
                parent.WallId,
                parent.Generation,
                EarthSurfaceKind.WallSide), Is.True);
            Assert.That(platform, Is.Not.Null);
            Assert.That(Vector3.Angle(platform.SurfaceUp, Vector3.up), Is.LessThan(1f),
                "A wall cantilever needs a gravity-level walkable deck, not a vertical cap.");
            EarthConstructionFrameRuntime frame = platform.GetComponent<EarthConstructionFrameRuntime>();
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.Frame.SupportId, Is.EqualTo(parent.WallId));
            Assert.That(frame.Frame.SupportGeneration, Is.EqualTo(parent.Generation));
            Assert.That(platform.GetComponent<EarthStructureAttachment>(), Is.Not.Null);

            for (int tick = 0; tick < 100 && !platform.IsEmergenceComplete; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(platform.IsEmergenceComplete, Is.True);
            Ray down = new Ray(platform.SurfaceTopPoint + platform.SurfaceUp * 1.5f, -platform.SurfaceUp);
            Assert.That(platform.TrySampleTopSurface(down, 4f, out Vector3 hit, out _), Is.True);
            Assert.That(Vector3.Dot(hit - face, parent.transform.forward), Is.GreaterThan(0.8f),
                "The playable deck must cantilever beyond the parent face.");

            Object.Destroy(root);
            Object.Destroy(wallProfile);
            Object.Destroy(platformProfile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SideAuthoredWallPreservesItsFrameDuringMagicPush()
        {
            GameObject root = new GameObject("Authored Wall Push Runtime");
            root.SetActive(false);
            EarthWallPool wallPool = root.AddComponent<EarthWallPool>();
            wallPool.Configure(2, null, null);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            executor.Configure(null, null, root.transform, wallPool);
            root.SetActive(true);
            var path = new List<float3>
            {
                new float3(-2f, 24f, 0f),
                new float3(2f, 24f, 0f)
            };
            Assert.That(executor.TryRaiseWallOnSurface(
                path,
                Vector3.forward,
                0.42f,
                0.5f,
                41u,
                out EarthWall wall), Is.True);
            for (int tick = 0; tick < 70 && !wall.IsEmergenceComplete; tick++)
                yield return new WaitForFixedUpdate();
            Quaternion authored = wall.transform.rotation;
            Assert.That(wall.OrientationMode, Is.EqualTo(ConstructionOrientationMode.PreserveAuthoredFrame));
            wall.ApplyMagicLaunchVelocity(Vector3.right, 9f);
            for (int tick = 0; tick < 20; tick++) yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(authored, wall.transform.rotation), Is.LessThan(1f));
            Assert.That(wall.Body.linearVelocity.magnitude, Is.GreaterThan(0.2f));

            Object.Destroy(root);
            yield return null;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
