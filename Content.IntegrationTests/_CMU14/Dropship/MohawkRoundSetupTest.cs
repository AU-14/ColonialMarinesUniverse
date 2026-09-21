using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.util;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkRoundSetupTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUMohawkRoundSetupPlanet
          components:
          - type: RMCPlanetMapPrototype
            mapId: USSBushRedux
            govforinship: true
            opforinship: true
            govfordropships: 2
            opfordropships: 2
            govforfighters: 0
            opforfighters: 0
        """;

    [TestCase("USCM", "govfor", "/Maps/CMU14/Shuttles/alamo.yml")]
    [TestCase("HAZOPS", "opfor", "/Maps/CMU14/Shuttles/osprey.yml")]
    public async Task MidwayReplacesGunshipAndDeploysAllDecksToBush(string platoonId, string faction, string transport)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid ship = default;
        EntityUid carrierMap = default;
        EntityUid destination = default;
        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var platoons = entities.System<PlatoonSpawnRuleSystem>();
            // Pooled worlds retain the selected platoons when entities are cleared.
            platoons.SelectedGovforPlatoon = null;
            platoons.SelectedOpforPlatoon = null;
            var platoon = server.ProtoMan.Index<PlatoonPrototype>(platoonId);
            Assert.That(platoon.CompatibleDropships.Select(path => path.ToString()), Is.EquivalentTo(new[]
            {
                transport,
                "/Maps/CMU14/ShuttlesDropships/Mohawk/midway_deployment.yml",
            }));
            if (faction == "govfor")
                platoons.SelectedGovforPlatoon = platoon;
            else
                platoons.SelectedOpforPlatoon = platoon;
            Assert.That(entities.System<AuRoundSystem>().SetPlanet("CMUMohawkRoundSetupPlanet"), Is.True);
            var grids = ticker.LoadGameMap(server.ProtoMan.Index<GameMapPrototype>("USSBushRedux"),
                out var mapId, DeserializationOptions.Default with { InitializeMaps = true });
            foreach (var grid in grids)
                entities.EnsureComponent<ShipFactionComponent>(grid).Faction = faction;
            carrierMap = entities.System<SharedMapSystem>().GetMap(mapId);
            var configuration = server.ResolveDependency<IConfigurationManager>();
            configuration.SetCVar(CCVars.FTLStartupTime, 0.5f);
            configuration.SetCVar(CCVars.FTLTravelTime, 1f);
            configuration.SetCVar(CCVars.FTLArrivalTime, 0.5f);
            Assert.That(ticker.StartGameRule("PlatoonSpawn"), Is.True);
            var assembly = entities.EntityQuery<MultiDeckDropshipComponent>().Single();
            ship = assembly.Owner;
            Assert.That(entities.GetComponent<MetaDataComponent>(ship).EntityName, Is.EqualTo("Midway"));
            Assert.That(entities.EntityQuery<DropshipComponent>().Count(), Is.EqualTo(2));
            foreach (var dropship in entities.EntityQuery<DropshipComponent>())
                Assert.That(dropship.Destination, Is.Not.Null, "Both the transport and Midway need a hangar pad.");
            Assert.That(entities.GetComponent<DropshipComponent>(ship).Destination, Is.Not.Null,
                "Round setup must reserve a Bush landing pad for the replacement gunship.");
            destination = entities.GetComponent<DropshipComponent>(ship).Destination!.Value;
            Assert.That(entities.GetComponent<DropshipDestinationComponent>(destination).FactionController, Is.EqualTo(faction));
            var terminals = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .Where(nav => entities.GetComponent<TransformComponent>(nav.Owner).GridUid == ship).ToArray();
            Assert.That(terminals, Is.Not.Empty);
            foreach (var terminal in terminals)
                Assert.That(entities.GetComponent<WhitelistedShuttleComponent>(terminal.Owner).Faction, Is.EqualTo(faction));
            var containers = entities.System<SharedContainerSystem>();
            Assert.That(entities.EntityQuery<DropshipFabricatorComponent>().Any(), Is.True,
                "Bush must provide a fabricator for the empty equipment slots.");
            Assert.That(entities.System<DropshipFabricatorSystem>().Printables.Any(id => id.Id == "CMUMohawkM90Ammo"), Is.True,
                "The chin gun's ammunition must be obtainable through the normal fabricator.");
            foreach (var point in entities.GetComponent<DropshipComponent>(ship).AttachmentPoints)
            {
                // Empty mounts create their containers on first insertion.
                if (!entities.TryGetComponent<ContainerManagerComponent>(point, out var manager))
                    continue;
                foreach (var container in containers.GetAllContainers(point, manager))
                {
                    var items = container.ContainedEntities;
                    if (entities.GetComponent<MetaDataComponent>(point).EntityPrototype?.ID == "CMUMohawkM90Point" &&
                        container.ID == entities.GetComponent<DropshipWeaponPointComponent>(point).WeaponContainerSlotId)
                    {
                        Assert.That(items, Has.Count.EqualTo(1));
                        Assert.That(entities.GetComponent<MetaDataComponent>(items.Single()).EntityPrototype?.ID, Is.EqualTo("CMUMohawkM90"));
                    }
                    else
                        Assert.That(items, Is.Empty, "Configurable mounts and all ammunition slots must start empty.");
                }
            }
        });
        await pair.RunSeconds(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<FTLComponent>(ship).State, Is.EqualTo(FTLState.Cooldown),
                "A landed ship remains in engine cooldown after completing its flight.");
            var zLevels = entities.System<CMUZLevelsSystem>();
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            Assert.That(assembly.Decks.Keys, Is.EquivalentTo(new[] { -1, 1 }));
            var ground = entities.GetComponent<TransformComponent>(destination).MapUid;
            Assert.That(entities.GetComponent<TransformComponent>(assembly.Decks[-1]).MapUid, Is.EqualTo(ground));
            foreach (var deck in assembly.Decks.Values.Append(ship))
            {
                Assert.That(zLevels.IsSameZNetwork(entities.GetComponent<TransformComponent>(deck).MapUid, carrierMap), Is.True,
                    "Every deck must arrive on the carrier's matching Z level.");
            }
        });
        await pair.CleanReturnAsync();
    }
}
