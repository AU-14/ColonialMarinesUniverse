#pragma warning disable RA0002 // Arrange viewer-specific snapshots and hive changes through the real system.
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUTacticalMapRefreshTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task HiveChangesFilterContactsWithoutMutatingPublishedSnapshots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<TacticalMapSystem>();
            var hives = Server.System<SharedXenoHiveSystem>();
            var firstHive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var secondHive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var viewer = SEntMan.SpawnEntity(null, map.GridCoords);
            var friendly = SEntMan.SpawnEntity(null, map.GridCoords);
            var foreign = SEntMan.SpawnEntity(null, map.GridCoords);
            hives.SetHive(viewer, firstHive);
            hives.SetHive(friendly, firstHive);
            hives.SetHive(foreign, secondHive);
            var user = SEntMan.EnsureComponent<TacticalMapUserComponent>(viewer);
            user.Xenos = true;
            user.LiveUpdate = true;
            var source = new TacticalMapComponent();
            source.XenoBlips[friendly.Id] = new TacticalMapBlip { Indices = new(1, 2) };
            source.XenoBlips[foreign.Id] = new TacticalMapBlip { Indices = new(3, 4) };
            source.XenoStructureBlips[foreign.Id] = new TacticalMapBlip { Indices = new(5, 6) };

            system.UpdateUserData((viewer, user), source);
            var published = user.XenoBlips;
            Assert.That(published.Keys, Is.EquivalentTo(new[] { friendly.Id }));
            Assert.That(user.XenoStructureBlips, Is.Empty);

            hives.SetHive(viewer, secondHive);
            system.UpdateUserData((viewer, user), source);
            Assert.That(user.XenoBlips.Keys, Is.EquivalentTo(new[] { foreign.Id }));
            Assert.That(user.XenoStructureBlips.Keys, Is.EquivalentTo(new[] { foreign.Id }));
            Assert.That(published.Keys, Is.EquivalentTo(new[] { friendly.Id }),
                "Previously published states must remain immutable.");
            Assert.That(source.XenoBlips.Keys, Is.EquivalentTo(new[] { friendly.Id, foreign.Id }));

            var metadata = Server.System<MetaDataSystem>();
            metadata.SetEntityPaused(foreign, true);
            system.UpdateUserData((viewer, user), source);
            Assert.That(user.XenoBlips, Is.Empty,
                "Paused members remain excluded just as in the previous full-hive query.");
            Assert.That(user.XenoStructureBlips, Is.Empty);
            metadata.SetEntityPaused(foreign, false);
            system.UpdateUserData((viewer, user), source);
            Assert.That(user.XenoBlips.Keys, Is.EquivalentTo(new[] { foreign.Id }));
        });
    }
}
