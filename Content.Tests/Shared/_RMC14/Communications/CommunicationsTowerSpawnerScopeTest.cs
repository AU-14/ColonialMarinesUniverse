#nullable enable
using System.Reflection;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Communications;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._RMC14.Communications;

[TestFixture]
public sealed class CommunicationsTowerSpawnerScopeTest
{
    [Test]
    public void NonZLevelSpawnerUsesOwnMap()
    {
        var map = new EntityUid(1);

        Assert.That(TryGetSpawnerScope(map, null, out var scope), Is.True);
        Assert.That(scope, Is.EqualTo(map));
    }

    [Test]
    public void ZLevelSpawnerUsesNetwork()
    {
        var map = new EntityUid(1);
        var network = new EntityUid(2);
        var zMap = new CMUZLevelMapComponent
        {
            NetworkUid = network,
        };

        Assert.That(TryGetSpawnerScope(map, zMap, out var scope), Is.True);
        Assert.That(scope, Is.EqualTo(network));
    }

    [Test]
    public void ZLevelSpawnerWithoutNetworkWaits()
    {
        var map = new EntityUid(1);
        var zMap = new CMUZLevelMapComponent();

        Assert.That(TryGetSpawnerScope(map, zMap, out _), Is.False);
    }

    private static bool TryGetSpawnerScope(EntityUid? mapUid, CMUZLevelMapComponent? zMap, out EntityUid scope)
    {
        var method = typeof(CommunicationsTowerSystem).GetMethod(
            "TryGetSpawnerScope",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        object?[] args = [mapUid, zMap, default(EntityUid)];
        var result = (bool) method!.Invoke(null, args)!;
        scope = (EntityUid) args[2]!;
        return result;
    }
}