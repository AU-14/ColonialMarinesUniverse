#pragma warning disable RA0002 // Arrange faction, credentials, and map ownership for authorization regressions.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.util;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class DropshipNavigationAccessTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("govfor", "opfor", "Govfor", "Opfor")]
    [TestCase("opfor", "govfor", "Opfor", "Govfor")]
    public async Task EnemyOfficersCannotLaunchNormallyEvenWithCapturedCredentials(
        string faction, string enemy, string ownAccess, string enemyAccess)
    {
        var ship = await Pair.CreateTestMap();
        var carrier = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SetForceOnForce();
            SEntMan.EnsureComponent<ShipFactionComponent>(carrier.GridCoords.EntityId).Faction = faction;
            SEntMan.EnsureComponent<ShuttleComponent>(ship.GridCoords.EntityId);
            var console = SEntMan.SpawnEntity("CMComputerDropshipNavigation" + ownAccess, ship.GridCoords);
            AssertPilotAccess(console, ownAccess, enemyAccess);
            var dropships = Server.System<SharedDropshipSystem>();
            var destination = SEntMan.SpawnEntity("CMDropshipDestinationHome", carrier.GridCoords);
            dropships.SetFactionController(destination, faction);
            var pilot = SEntMan.SpawnEntity("CMMobHuman", ship.GridCoords);
            var marine = SEntMan.EnsureComponent<MarineComponent>(pilot);
            marine.Faction = enemy;
            var officer = SProtoMan.EnumeratePrototypes<RankPrototype>().First(r => r.Paygrade?.StartsWith("O") == true);
            Server.System<SharedRankSystem>().SetRank(pilot, officer);
            var card = SEntMan.SpawnEntity("CMIDCardStandardDogtag", ship.GridCoords);
            var access = SEntMan.EnsureComponent<AccessComponent>(card);
            access.Tags.Clear();
            access.Tags.Add("AU14Access" + enemyAccess + "Pilot");
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(pilot, card), Is.True);

            void Launch() => SEntMan.EventBus.RaiseLocalEvent(console,
                new DropshipNavigationLaunchMsg(SEntMan.GetNetEntity(destination))
                { Actor = pilot, UiKey = DropshipNavigationUiKey.Key });
            void AssertDenied()
            {
                Assert.That(dropships.CanUseNavigation(console, pilot), Is.False);
                Launch();
                Assert.That(SEntMan.HasComponent<FTLComponent>(ship.GridCoords.EntityId), Is.False);
                Assert.That(SEntMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.Null);
            }

            AssertDenied();
            access.Tags.Clear();
            access.Tags.Add("AU14Access" + ownAccess + "Pilot");
            Assert.That(Server.System<AccessReaderSystem>().IsAllowed(pilot, console), Is.True,
                "A captured card passes the reader but must not bypass the hijack requirement.");
            AssertDenied();
            marine.Faction = faction;
            access.Tags.Clear();
            AssertDenied();
            access.Tags.Add("AU14Access" + ownAccess + "Pilot");
            Assert.That(dropships.CanUseNavigation(console, pilot), Is.True);
            Launch();
            Assert.That(SEntMan.HasComponent<FTLComponent>(ship.GridCoords.EntityId), Is.True);
            Assert.That(SEntMan.GetComponent<DropshipDestinationComponent>(destination).Ship,
                Is.EqualTo(ship.GridCoords.EntityId));
            Assert.That(SEntMan.GetComponent<DropshipComponent>(ship.GridCoords.EntityId).Crashed, Is.False);

            // Legacy human hijacks outside FoF unlock controls by removing their reader.
            // That must still work, while FoF's required hack cannot be skipped this way.
            marine.Faction = enemy;
            SEntMan.RemoveComponent<AccessReaderComponent>(console);
            Assert.That(dropships.CanUseNavigation(console, pilot), Is.False);
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
                .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>("DistressSignal"));
            Assert.That(dropships.CanUseNavigation(console, pilot), Is.True);
        });
    }

    [TestCase("govfor", "opfor", "Govfor", "Opfor")]
    [TestCase("opfor", "govfor", "Opfor", "Govfor")]
    public async Task PrebuiltGunshipIsAssignedAndLandsOnItsOwnCarrier(
        string faction, string enemy, string ownAccess, string enemyAccess)
    {
        var carrier = await Pair.CreateTestMap();
        var otherCarrier = await Pair.CreateTestMap();
        EntityUid gunship = default, home = default, otherHome = default;
        await Server.WaitAssertion(() =>
        {
            SetForceOnForce();
            SEntMan.EnsureComponent<ShipFactionComponent>(carrier.GridCoords.EntityId).Faction = faction;
            SEntMan.EnsureComponent<ShipFactionComponent>(otherCarrier.GridCoords.EntityId).Faction = enemy;
            var dropships = Server.System<SharedDropshipSystem>();
            home = SEntMan.SpawnEntity("CMDropshipDestinationHome", carrier.GridCoords);
            otherHome = SEntMan.SpawnEntity("CMDropshipDestinationHome", otherCarrier.GridCoords);
            dropships.SetFactionController(home, faction);
            dropships.SetFactionController(otherHome, enemy);
            Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/_RMC14/Shuttles/dynamic_gunship.yml"), out _, out var grids), Is.True);
            gunship = grids!.Single().Owner;
            Server.System<SharedMapSystem>().InitializeMap(SEntMan.GetComponent<TransformComponent>(gunship).MapID);
            var planet = new RMCPlanetMapPrototypeComponent { GovforInShip = true, OpforInShip = true };
            var spawning = Server.System<PlatoonSpawnRuleSystem>();
            typeof(PlatoonSpawnRuleSystem).GetMethod("PrepareLoadedShuttleGrid", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(spawning, [gunship, faction, planet, DropshipDestinationComponent.DestinationType.Dropship]);

            var children = SEntMan.GetComponent<TransformComponent>(gunship).ChildEnumerator;
            var controls = 0;
            var seats = 0;
            while (children.MoveNext(out var child))
            {
                if (SEntMan.HasComponent<DropshipNavigationComputerComponent>(child))
                {
                    controls++;
                    Assert.That(SEntMan.GetComponent<WhitelistedShuttleComponent>(child).Faction, Is.EqualTo(faction));
                    Assert.That(SEntMan.GetComponent<TacticalMapComputerComponent>(child).Faction, Is.EqualTo(faction).IgnoreCase);
                    AssertPilotAccess(child, ownAccess, enemyAccess);
                }
                if (SEntMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID == "CMUGunshipPilotSeat")
                {
                    seats++;
                    AssertPilotAccess(child, ownAccess, enemyAccess);
                }
            }
            Assert.That(controls, Is.EqualTo(1));
            Assert.That(seats, Is.EqualTo(1));
            var used = new HashSet<EntityUid>();
            var launched = typeof(PlatoonSpawnRuleSystem).GetMethod("TryFlyShuttleToDestination", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(spawning, [gunship, faction, DropshipDestinationComponent.DestinationType.Dropship, planet, used, new Random(1)]);
            Assert.That(launched, Is.True, "Initial deployment must not reject the gunship's own carrier.");
            Assert.That(used, Is.EquivalentTo(new[] { home }));
            Assert.That(SEntMan.HasComponent<FTLComponent>(gunship), Is.True);
        });
        await Pair.RunSeconds(130);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(gunship).MapUid, Is.EqualTo(carrier.MapUid));
            Assert.That(SEntMan.GetComponent<DropshipComponent>(gunship).Destination, Is.EqualTo(home));
            Assert.That(SEntMan.GetComponent<DropshipDestinationComponent>(home).Ship, Is.EqualTo(gunship));
            Assert.That(SEntMan.GetComponent<DropshipDestinationComponent>(otherHome).Ship, Is.Null);
        });
    }

    private void AssertPilotAccess(EntityUid console, string ownAccess, string enemyAccess)
    {
        var reader = SEntMan.GetComponent<AccessReaderComponent>(console);
        var access = Server.System<AccessReaderSystem>();
        foreach (var role in new[] { "FTL", "Pilot" })
        {
            Assert.That(access.IsAllowed(new List<ProtoId<AccessLevelPrototype>> { "AU14Access" + ownAccess + role },
                [], console, reader), Is.True, "Own " + role);
            Assert.That(access.IsAllowed(new List<ProtoId<AccessLevelPrototype>> { "AU14Access" + enemyAccess + role },
                [], console, reader), Is.False, "Enemy " + role);
        }
        Assert.That(access.IsAllowed([], [], console, reader), Is.False, "No credentials");
    }

    private void SetForceOnForce() => typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
        .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
}
