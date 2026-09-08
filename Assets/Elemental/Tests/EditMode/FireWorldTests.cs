using System;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class FireWorldTests
    {
        private static FireFieldNode Node(float x = 0) => FireFieldNode.Stream(
            new float3(x, 0, 0), new float3(x, 0, 2), new float3(0, 0, 12), new float3(0, 1, 0));
        [Test] public void DrainingKeepsIdentityUntilTailExpiresAndReuseRejectsOldHandle()
        {
            var world = new FireWorld(new FireWorldSettings(1, 0.85f, 0.1f, 24));
            FireFieldNode node = Node();
            Assert.That(world.TryCreate(7, 10, node, out var first));
            Assert.That(world.TryCreate(8, 10, node, out _), Is.False);
            Assert.That(world.Stop(first)); world.Advance(0.9f);
            var snapshot = new FirePresentationSnapshot();
            Assert.That(world.TryCopySnapshot(first, snapshot));
            Assert.That(snapshot.Lifecycle, Is.EqualTo(FireLifecycle.Draining));
            Assert.That(snapshot.Emits, Is.False);
            world.Stop(first); // repeated stop must not extend the tail
            world.Advance(0.06f);
            Assert.That(world.IsCurrent(first), Is.False);
            Assert.That(world.TryCreate(9, 10, node, out var second));
            Assert.That(second.Generation, Is.GreaterThan(first.Generation));
            Assert.That(world.Stop(first), Is.False);
        }
        [Test] public void BoundsRetainTailThenExpireOldEmitterPath()
        {
            var world = new FireWorld(FireWorldSettings.Default);
            world.TryCreate(1, 1, Node(), out var group);
            var nodes = new[] { Node(100) }; world.TrySetNodes(group, nodes, 1);
            var snapshot = new FirePresentationSnapshot(); world.TryCopySnapshot(group, snapshot);
            Assert.That(snapshot.BoundsMin.x, Is.LessThan(0));
            world.Advance(1.3f); world.TryCopySnapshot(group, snapshot);
            Assert.That(snapshot.BoundsMin.x, Is.GreaterThan(50));
        }
        [Test] public void InvalidNodeCommandIsAtomicAndSnapshotCannotMutateAuthority()
        {
            var world = new FireWorld(FireWorldSettings.Default);
            world.TryCreate(1, 5, Node(), out var group);
            var nodes = new[] { Node(20), Node(30) }; nodes[1].Radius = float.NaN;
            Assert.That(world.TrySetNodes(group, nodes, 2), Is.False);
            var snapshot = new FirePresentationSnapshot(); world.TryCopySnapshot(group, snapshot);
            Assert.That(snapshot.Nodes[0].A.x, Is.Zero);
            snapshot.Nodes[0] = Node(100);
            world.TryCopySnapshot(group, snapshot);
            Assert.That(snapshot.Nodes[0].A.x, Is.Zero);
            Assert.That(snapshot.NodeCount, Is.EqualTo(1));
        }
        [Test] public void SplittingNodesPreservesEnergyAndNormalizesDensity()
        {
            var world = new FireWorld(FireWorldSettings.Default);
            world.TryCreate(1, 3, Node(), out var group);
            var nodes = new[] { Node(1), Node(2), Node(3) }; world.TrySetNodes(group, nodes, 3);
            var snapshot = new FirePresentationSnapshot(); world.TryCopySnapshot(group, snapshot);
            Assert.That(snapshot.Energy, Is.EqualTo(3));
            float density = 0; for (int i = 0; i < snapshot.NodeCount; i++) density += snapshot.Nodes[i].Density;
            Assert.That(density, Is.EqualTo(1).Within(0.00001f));
        }
        [Test] public void PauseAndFullHitchTimeArePreserved()
        {
            var world = new FireWorld(FireWorldSettings.Default);
            world.Advance(0); Assert.That(world.Time, Is.Zero);
            world.Advance(0.1f); Assert.That(world.Time, Is.EqualTo(0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Advance(float.NaN));
        }
        [Test] public void SteadyStateBoundedCopiesAllocateNoManagedMemory()
        {
            var world = new FireWorld(FireWorldSettings.Default);
            world.TryCreate(1, 1, Node(), out var group);
            var nodes = new[] { Node() }; var snapshot = new FirePresentationSnapshot();
            for (int i = 0; i < 10; i++) { world.TrySetNodes(group, nodes, 1); world.TryCopySnapshot(group, snapshot); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            { world.Advance(0.001f); world.TrySetNodes(group, nodes, 1); world.TryCopySnapshot(group, snapshot); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
        [Test] public void NonFiniteNormalUsesFiniteNormalizedFallback()
        {
            float3 normal = FireContactMath.SafeNormal(new float3(float.NaN), new float3(float.PositiveInfinity));
            Assert.That(math.all(math.isfinite(normal)));
            Assert.That(math.length(normal), Is.EqualTo(1).Within(0.00001f));
        }
    }
}
