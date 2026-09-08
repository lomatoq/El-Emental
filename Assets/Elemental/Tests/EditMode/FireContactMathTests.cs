using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;

// Place under the project's EditMode test assembly. These tests have NOT
// been run in Unity here; test_reference.py is an independent numerical mirror.
public class FireContactMathTests
{
    private static FireContactPatch Wall() => new FireContactPatch
    {
        Point = float3.zero, Radius = 2f, Normal = new float3(0, 0, 1),
        Tangent = new float3(1, 0, 0), RecoveryDepth = 0.08f,
        SpreadFraction = 0.8f, Skin = 0.015f, Active = true
    };

    [Test]
    public void HeadOnImpactPreservesVisibleLateralMotion()
    {
        var wall = Wall();
        var p = new float3(0, 0, -0.5f);
        var v = new float3(0, 0, -10);
        bool hit = FireContactMath.ResolveSwept(wall, new float3(0, 0, 1),
            0, 1f / 60f, 0.035f, 1.3f, ref p, ref v);
        Assert.That(hit, Is.True);
        Assert.That(p.z, Is.EqualTo(0.05f).Within(1e-5f));
        Assert.That(math.length(v.xy), Is.GreaterThan(1f));
        Assert.That(v.z, Is.GreaterThanOrEqualTo(-1e-5f));
        Assert.That(math.length(v), Is.LessThanOrEqualTo(10.0001f));
    }

    [Test]
    public void InvalidatedContactCannotKeepAnInvisibleWall()
    {
        var wall = Wall(); wall.Active = false;
        var p = new float3(0, 0, -1); var v = new float3(0, 0, -10);
        Assert.That(FireContactMath.ResolveSwept(wall, new float3(0, 0, 1),
            0, 1f / 60f, 0.035f, 0, ref p, ref v), Is.False);
        Assert.That(p.z, Is.EqualTo(-1f));
    }

    [Test]
    public void FootprintIsFinite()
    {
        var wall = Wall(); wall.Radius = 0.3f;
        var p = new float3(2, 0, -1); var v = new float3(0, 0, -10);
        Assert.That(FireContactMath.ResolveSwept(wall, new float3(2, 0, 1),
            0, 1f / 60f, 0.035f, 0, ref p, ref v), Is.False);
    }

    [Test]
    public void FastCrossingTestsFootprintAtImpactNotAtEndpoint()
    {
        var wall = Wall(); wall.Radius = 0.3f;
        var p = new float3(2, 0, -1); var v = new float3(120, 0, -120);
        Assert.That(FireContactMath.ResolveSwept(wall, new float3(-2, 0, 1),
            0, 1f / 30f, 0, 0, ref p, ref v), Is.True);
    }

    [Test]
    public void ShallowOverlapRecoversButDeepBacksideDoesNotTeleport()
    {
        var wall = Wall();
        var p = new float3(0, 0, -0.01f); var v = new float3(0, 0, -1);
        Assert.That(FireContactMath.ResolveSwept(wall, p, 0, 0.01f, 0, 0, ref p, ref v));
        Assert.That(p.z, Is.EqualTo(0.015f).Within(0.0001f));
        p = new float3(0, 0, -2); v = new float3(0, 0, -1);
        Assert.That(FireContactMath.ResolveSwept(wall, p, 0, 0.01f, 0, 0, ref p, ref v), Is.False);
        Assert.That(p.z, Is.EqualTo(-2));
    }

    [Test]
    public void MovingWallVelocityIsResolvedInItsRelativeFrame()
    {
        var wall = Wall(); wall.SurfaceVelocity = new float3(0, 0, 2);
        var p = new float3(0, 0, -0.5f); var v = new float3(0, 0, -10);
        Assert.That(FireContactMath.ResolveSwept(wall, new float3(0, 0, 1),
            0, 0.05f, 0.035f, 0.7f, ref p, ref v));
        Assert.That(v.z, Is.GreaterThanOrEqualTo(2f - 0.0001f));
        Assert.That(p.z, Is.EqualTo(0.15f).Within(0.0001f));
    }
}
