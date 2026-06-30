using System.Reflection;
using Content.Shared._RMC14.Power;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._RMC14.Power;

[TestFixture]
public sealed class RMCPowerGroupTest
{
    [Test]
    public void MapWithoutZNetworkUsesOwnPowerGroup()
    {
        var map = new EntityUid(1);

        Assert.That(TryResolvePowerGroup(map, null, out var powerGroup), Is.True);
        Assert.That(powerGroup, Is.EqualTo(map));
    }

    [Test]
    public void MapInZNetworkUsesNetworkPowerGroup()
    {
        var map = new EntityUid(1);
        var network = new EntityUid(2);

        Assert.That(TryResolvePowerGroup(map, network, out var powerGroup), Is.True);
        Assert.That(powerGroup, Is.EqualTo(network));
    }

    [Test]
    public void MissingMapHasNoPowerGroup()
    {
        Assert.That(TryResolvePowerGroup(null, null, out _), Is.False);
    }

    private static bool TryResolvePowerGroup(EntityUid? mapUid, EntityUid? networkUid, out EntityUid powerGroup)
    {
        var method = typeof(SharedRMCPowerSystem).GetMethod(
            "TryResolvePowerGroup",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        object?[] args = [mapUid, networkUid, default(EntityUid)];
        var result = (bool) method!.Invoke(null, args)!;
        powerGroup = (EntityUid) args[2]!;
        return result;
    }
}
