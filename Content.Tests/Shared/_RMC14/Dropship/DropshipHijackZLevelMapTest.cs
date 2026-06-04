using System.Collections.Generic;
using System.Reflection;
using Content.Shared._RMC14.Dropship;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._RMC14.Dropship;

[TestFixture]
public sealed class DropshipHijackZLevelMapTest
{
    [Test]
    public void AddsConnectedZNetworkMapsForShipMap()
    {
        var shipMap = new EntityUid(1);
        var lowerMap = new EntityUid(2);
        var upperMap = new EntityUid(3);
        var higherMap = new EntityUid(4);

        var maps = new HashSet<EntityUid>();
        var connectedMaps = new[] { lowerMap, shipMap, upperMap, higherMap };

        AddShipMapAndConnectedZLevels(maps, shipMap, connectedMaps);

        Assert.That(maps, Is.EquivalentTo(new[] { shipMap, lowerMap, upperMap, higherMap }));
    }

    private static void AddShipMapAndConnectedZLevels(
        HashSet<EntityUid> maps,
        EntityUid map,
        IEnumerable<EntityUid> connectedMaps)
    {
        var method = typeof(SharedDropshipSystem).GetMethod(
            "AddShipMapAndConnectedZLevels",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        method!.Invoke(null, new object[] { maps, map, connectedMaps });
    }
}
