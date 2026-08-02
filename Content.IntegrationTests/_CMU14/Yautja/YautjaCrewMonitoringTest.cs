using System.Linq;
using Content.Server.Medical.CrewMonitoring;
using Content.Server._CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaCrewMonitoringTest
{
    [Test]
    public async Task AllMappedYautjaMonitorsUseTheSpecializedPrototype()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var ids = new[]
            {
                "CMUYautjaHunterShuttleHealthMonitor",
                "CMUHunterShipPlacedComputerCrewMonitoringCrewNorthOffset28x1",
                "CMUHunterShipPlacedComputerCrewMonitoringCrewNorthOffset4x1",
                "CMUHunterShipPlacedComputerCrewMonitoringCrewSouthOffset26x27",
                "CMUHunterShipPlacedComputerCrewMonitoringCrewSouthOffsetNeg2x32",
                "CMUHunterShipPlacedComputerCrewMonitoringSmallmonitorSouthOffset0x23",
            };

            foreach (var id in ids)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<YautjaCrewMonitoringConsoleComponent>(out _, factory), Is.True, id);
                if (id != "CMUYautjaHunterShuttleHealthMonitor")
                    Assert.That(prototype.Parents, Does.Contain("CMUYautjaHunterShuttleHealthMonitor"), id);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectCollectionIncludesYautjaAndDeadStateButExcludesHumans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid blooded = default;
        EntityUid youngblood = default;
        EntityUid badBlood = default;
        EntityUid human = default;
        EntityUid monitor = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            blooded = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            badBlood = entMan.SpawnEntity("CMUMobYautjaBadBlood", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)));
            monitor = entMan.SpawnEntity("CMUYautjaHunterShuttleHealthMonitor", map.GridCoords.Offset(new System.Numerics.Vector2(4, 0)));

            var damageable = entMan.GetComponent<DamageableComponent>(blooded);
            var damage = new DamageSpecifier();
            damage.DamageDict["Asphyxiation"] = 3;
            damage.DamageDict["Bloodloss"] = 2;
            damage.DamageDict["Poison"] = 7;
            damage.DamageDict["Radiation"] = 1;
            damage.DamageDict["Heat"] = 11;
            damage.DamageDict["Shock"] = 2;
            damage.DamageDict["Cold"] = 4;
            damage.DamageDict["Caustic"] = 3;
            damage.DamageDict["Blunt"] = 13;
            damage.DamageDict["Slash"] = 5;
            damage.DamageDict["Piercing"] = 2;
            entMan.System<DamageableSystem>().AddDamage(blooded, damageable, damage);

            entMan.System<MobStateSystem>().ChangeMobState(youngblood, MobState.Dead);
            entMan.System<YautjaCrewMonitoringConsoleSystem>().Refresh(monitor);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var component = entMan.GetComponent<CrewMonitoringConsoleComponent>(monitor);
            var statuses = component.ConnectedSensors.Values.ToArray();

            Assert.That(statuses, Has.Length.EqualTo(3));
            Assert.That(statuses.Any(status => status.OwnerUid == entMan.GetNetEntity(human)), Is.False);

            var bloodedStatus = statuses.Single(status => status.OwnerUid == entMan.GetNetEntity(blooded));
            Assert.Multiple(() =>
            {
                Assert.That(bloodedStatus.OxygenDamage, Is.EqualTo(5));
                Assert.That(bloodedStatus.ToxinDamage, Is.EqualTo(8));
                Assert.That(bloodedStatus.BurnDamage, Is.EqualTo(20));
                Assert.That(bloodedStatus.BruteDamage, Is.EqualTo(20));
                Assert.That(bloodedStatus.CanTrack, Is.True);
            });

            var deadStatus = statuses.Single(status => status.OwnerUid == entMan.GetNetEntity(youngblood));
            Assert.That(deadStatus.IsAlive, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpecializedMonitorPublishesCrewMonitoringState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid monitor = default;
        EntityUid viewer = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            monitor = entMan.SpawnEntity("CMUYautjaHunterShuttleHealthMonitor", map.GridCoords);
            viewer = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var ui = entMan.System<UserInterfaceSystem>();
            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, viewer), Is.True);
            entMan.System<YautjaCrewMonitoringConsoleSystem>().Refresh(monitor);
        });

        await server.WaitAssertion(() =>
        {
            var ui = server.EntMan.System<UserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<CrewMonitoringState>(monitor, CrewMonitoringUIKey.Key, out var state), Is.True);
            Assert.That(state!.Sensors, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }
}
