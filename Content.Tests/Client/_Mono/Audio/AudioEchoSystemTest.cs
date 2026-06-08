using System.Numerics;
using System.Reflection;
using Content.Goobstation.Client.Audio;
using NUnit.Framework;

namespace Content.Tests.Client._Mono.Audio;

[TestFixture]
public sealed class AudioEchoSystemTest
{
    [TestCase(float.NaN, 0f)]
    [TestCase(float.PositiveInfinity, 0f)]
    [TestCase(0f, float.NegativeInfinity)]
    public void TileHitNormalRejectsNonFiniteRayHit(float x, float y)
    {
        var normal = InvokeGetTileHitNormal(new Vector2(x, y), Vector2.Zero, 1f);

        Assert.That(normal, Is.EqualTo(Vector2.Zero));
    }

    [TestCase(float.NaN)]
    [TestCase(0f)]
    [TestCase(-1f)]
    public void TileHitNormalRejectsInvalidTileSize(float tileSize)
    {
        var normal = InvokeGetTileHitNormal(new Vector2(0.5f, 0.5f), Vector2.Zero, tileSize);

        Assert.That(normal, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void TileHitNormalReturnsNearestCardinalSide()
    {
        var normal = InvokeGetTileHitNormal(new Vector2(0.1f, 0.5f), Vector2.Zero, 1f);

        Assert.That(normal, Is.EqualTo(new Vector2(-1f, 0f)));
    }

    private static Vector2 InvokeGetTileHitNormal(Vector2 rayHitPos, Vector2 tileOrigin, float tileSize)
    {
        var method = typeof(AreaEchoSystem).GetMethod(
            "GetTileHitNormal",
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        var target = method!.IsStatic ? null : new AreaEchoSystem();
        return (Vector2) method.Invoke(target, new object[] { rayHitPos, tileOrigin, tileSize })!;
    }
}
