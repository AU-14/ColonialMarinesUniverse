#pragma warning disable RA0002 // Regression setup and assertions inspect vehicle and console state.

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.util;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
[TestOf(typeof(VehicleSystem))]
public sealed class VehicleInteriorFactionTest : GameTest
{

    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("VehicleAPCCommand", "opfor")]
    [TestCase("VehicleHumveeARC", "opfor")]
    [TestCase("VehicleSPPAPC", "opfor")]
    [TestCase("VehicleAPCCommand", "govfor")]
    [TestCase("VehicleHumveeARC", "govfor")]
    [TestCase("VehicleSPPAPC", "govfor")]
    public async Task DeployedVehicleKeepsSupplyingFactionForLazyInterior(string prototype, string faction)
    {
        var ship = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var vehicles = Server.System<VehicleSystem>();
            var transform = Server.System<SharedTransformSystem>();
            SEntMan.EnsureComponent<ShipFactionComponent>(ship.GridCoords.EntityId).Faction = faction;
            var destination = maps.CreateMap(runMapInit: true);
            // Entering another faction's location must not change the vehicle's command equipment.
            SEntMan.EnsureComponent<ShipFactionComponent>(destination).Faction = faction == "opfor" ? "govfor" : "opfor";
            var vehicle = SEntMan.SpawnEntity(prototype, ship.GridCoords);
            try
            {
                Assert.That(vehicles.TryGetInteriorMapId(vehicle, out _), Is.False);
                transform.SetCoordinates(vehicle, new EntityCoordinates(destination, Vector2.Zero));
                AssertInterior(vehicle, faction, prototype != "VehicleSPPAPC");
                AssertInterior(vehicle, faction, prototype != "VehicleSPPAPC"); // Re-entering must retain the same configuration.
            }
            finally
            {
                SEntMan.DeleteEntity(vehicle);
                SEntMan.DeleteEntity(destination);
            }
        });
    }

    private void AssertInterior(EntityUid vehicle, string faction, bool expectCommandAccess = true)
    {
        var vehicles = Server.System<VehicleSystem>();
        var access = Server.System<AccessReaderSystem>();
        Assert.That(vehicles.TryGetInteriorEntryCoordinates(vehicle, 0, out _), Is.True);
        Assert.That(vehicles.TryGetInteriorMapId(vehicle, out var interior), Is.True);

        var prefix = faction == "opfor" ? "AU14AccessOpfor" : "AU14AccessGovfor";
        var enemyPrefix = faction == "opfor" ? "AU14AccessGovfor" : "AU14AccessOpfor";
        var consoles = 0;
        var query = SEntMan.EntityQueryEnumerator<OverwatchConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var overwatch, out var transform))
        {
            if (transform.MapID != interior)
                continue;

            consoles++;
            Assert.That(overwatch.Group, Is.EqualTo(faction.ToUpperInvariant()));
        }
        Assert.That(consoles, Is.GreaterThanOrEqualTo(2), "Load the real vehicle command interior.");

        var readers = 0;
        var entities = SEntMan.EntityQueryEnumerator<TransformComponent>();
        while (entities.MoveNext(out var uid, out var transform))
        {
            if (transform.MapID != interior)
                continue;

            if (SEntMan.TryGetComponent<AccessReaderComponent>(uid, out var reader) &&
                reader.AccessLists.SelectMany(group => group).Any(id =>
                    id.Id == prefix + "Command" || id.Id == enemyPrefix + "Command"))
            {
                readers++;
                Assert.Multiple(() =>
                {
                    Assert.That(access.AreAccessTagsAllowed(new List<ProtoId<AccessLevelPrototype>> { prefix + "Command" }, reader),
                        Is.True, "The supplying faction's command staff must be allowed to use the console.");
                    Assert.That(access.AreAccessTagsAllowed(new List<ProtoId<AccessLevelPrototype>> { prefix }, reader),
                        Is.False, "Ordinary faction access must not bypass command restrictions.");
                    Assert.That(reader.AccessListsOriginal!.SelectMany(group => group).Select(id => id.Id),
                        Does.Contain(prefix + "Command"), "Inspect must show the assigned faction's command access.");
                    Assert.That(access.AreAccessTagsAllowed(new List<ProtoId<AccessLevelPrototype>> { enemyPrefix + "Command" }, reader),
                        Is.False, "Enemy command access must not work.");
                    Assert.That(reader.AccessListsOriginal.SelectMany(group => group).Any(id => id.Id.StartsWith(enemyPrefix)), Is.False);
                });
            }

            if (SEntMan.TryGetComponent<TacticalMapComputerComponent>(uid, out var tactical))
                Assert.That(tactical.Faction, Is.EqualTo(faction.ToUpperInvariant()).IgnoreCase);
            if (SEntMan.TryGetComponent<MarineCommunicationsComputerComponent>(uid, out var announcements))
                Assert.That(announcements.Faction, Is.EqualTo(faction));
            if (SEntMan.TryGetComponent<AU14CallsignConsoleComponent>(uid, out var callsigns))
                Assert.That(callsigns.Faction, Is.EqualTo(faction));
        }
        if (expectCommandAccess)
            Assert.That(readers, Is.Positive, "The actual command access readers must be checked.");
    }
}
