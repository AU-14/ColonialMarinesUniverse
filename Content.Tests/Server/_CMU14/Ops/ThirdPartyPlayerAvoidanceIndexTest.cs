using System.Numerics;
using Content.Server._CMU14.Ops.ThirdParty;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Server._CMU14.Ops;

[TestFixture]
public sealed class ThirdPartyPlayerAvoidanceIndexTest
{
    [Test]
    public void FindsNearbyPlayersAcrossCellEdgesWithoutCrossingMaps()
    {
        var index = new ThirdPartyPlayerAvoidanceIndex();
        var firstMap = new MapId(1);
        var secondMap = new MapId(2);

        index.Add(firstMap, new Vector2(7.9f, -0.1f));
        index.Add(secondMap, Vector2.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(index.IsBlocked(firstMap, new Vector2(8.1f, 0.1f)), Is.True);
            Assert.That(index.IsBlocked(firstMap, new Vector2(16.1f, 0.1f)), Is.False);
            Assert.That(index.IsBlocked(firstMap, new Vector2(-8.2f, -0.1f)), Is.False);
            Assert.That(index.IsBlocked(new MapId(3), Vector2.Zero), Is.False);
        });
    }
}
