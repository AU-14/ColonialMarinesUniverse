using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.TileReactions;
using Content.Server.CMU14.Fire;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Fire;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Kitchen;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DamageOverlay;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Reflection;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class SeptemberRegressionTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<ReagentPrototype> WaterPrototype = "Water";

#pragma warning disable RA0002 // Arrange overlapping bonuses and hijack state at the system boundary.

#pragma warning restore RA0002

    [Test]
    public async Task ExtinguishingTileAlsoExtinguishesLooseItemsWithoutHotspots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var item = SEntMan.SpawnEntity("CMPaper", map.GridCoords);
            var flam = SEntMan.EnsureComponent<FlamabilityComponent>(item);
            var fire = SEntMan.System<AU14FireSpreadSystem>();
            Assert.That(fire.Ignite(item, flam), Is.True);
            var tile = SEntMan.System<TurfSystem>().GetTileRef(map.GridCoords)!.Value;
            var water = Server.ResolveDependency<IPrototypeManager>().Index<ReagentPrototype>(WaterPrototype);
            new ExtinguishTileReaction().TileReact(tile, water, FixedPoint2.New(1), SEntMan, null);
            Assert.That(flam.OnFire, Is.False);
            Assert.That(flam.FireVisualEntity, Is.Null);
            Assert.That(SEntMan.EntityExists(item), Is.True);
            SEntMan.DeleteEntity(item);
        });
    }

    [Test]
    public async Task BloodbursterKeepsPathogenHiveWhenInfectionHasNoSourceHive()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var larva = SEntMan.SpawnEntity("CMU14XenoBloodburster", map.GridCoords);
            var infection = SEntMan.EnsureComponent<VictimInfectedComponent>(victim);
            Assert.That(SEntMan.GetComponent<HiveMemberComponent>(larva).Hive, Is.EqualTo(hive));
            Assert.That(infection.Hive, Is.Null);
            SEntMan.System<SharedXenoParasiteSystem>().InsertLarva((victim, infection), larva);
            Assert.That(SEntMan.GetComponent<HiveMemberComponent>(larva).Hive, Is.EqualTo(hive));
            SEntMan.DeleteEntity(victim);
            if (SEntMan.EntityExists(larva)) SEntMan.DeleteEntity(larva);
            SEntMan.DeleteEntity(hive);
        });
    }

}
