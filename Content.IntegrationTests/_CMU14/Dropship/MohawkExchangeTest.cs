using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.Exchange;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.util;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkExchangeTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<GameMapPrototype> USSBushReduxPrototype = "USSBushRedux";
    private static readonly Robust.Shared.Prototypes.EntProtoId PlatoonSpawnPrototype = "PlatoonSpawn";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUMohawkExchangePlanet
          components:
          - type: RMCPlanetMapPrototype
            mapId: USSBushRedux
            govforinship: true
            opforinship: true
            govfordropships: 2
            opfordropships: 0
            govforfighters: 0
            opforfighters: 0
        """;

    [Test]
    public async Task GunshipHoldsUntilEmptyThenMohawkTakesItsPad()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid gunship = default;
        EntityUid computer = default;
        EntityUid pad = default;
        EntityUid originalOmaha = default;
        EntityUid passenger = default;
        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var platoons = entities.System<PlatoonSpawnRuleSystem>();
            // Pooled worlds retain the selected platoons when entities are cleared.
            platoons.SelectedGovforPlatoon = null;
            platoons.SelectedOpforPlatoon = null;
            platoons.SelectedGovforPlatoon = server.ProtoMan.Index<PlatoonPrototype>("USCM");
            Assert.That(entities.System<AuRoundSystem>().SetPlanet("CMUMohawkExchangePlanet"), Is.True);
            var grids = ticker.LoadGameMap(server.ProtoMan.Index<GameMapPrototype>(USSBushReduxPrototype),
                out _, DeserializationOptions.Default with { InitializeMaps = true });
            foreach (var grid in grids)
                entities.EnsureComponent<ShipFactionComponent>(grid).Faction = "govfor";
            var configuration = server.ResolveDependency<IConfigurationManager>();
            configuration.SetCVar(CCVars.FTLStartupTime, 0.5f);
            configuration.SetCVar(CCVars.FTLTravelTime, 1f);
            configuration.SetCVar(CCVars.FTLArrivalTime, 0.5f);
            Assert.That(ticker.StartGameRule(PlatoonSpawnPrototype), Is.True);

            var exchange = entities.QueryEntities<CMUDropshipMohawkExchangeComponent>().Single();
            gunship = exchange.Owner;
            Assert.That(exchange.Comp.Faction, Is.EqualTo("govfor"));
            Assert.That(exchange.Comp.MohawkPath.ToString(),
                Is.EqualTo("/Maps/CMU14/Dropships/Mohawk/omaha_deployment.yml"));
            Assert.That(entities.HasComponent<MultiDeckDropshipComponent>(gunship), Is.False,
                "The exchange offer belongs on the dynamic gunship, not the Mohawk.");
            originalOmaha = entities.QueryEntities<MultiDeckDropshipComponent>().Single().Owner;
            computer = entities.QueryEntities<DropshipNavigationComputerComponent>()
                .First(nav => entities.GetComponent<TransformComponent>(nav.Owner).GridUid == gunship).Owner;
        });
        await pair.RunSeconds(5);
        await server.WaitAssertion(() =>
        {
            var exchange = entities.GetComponent<CMUDropshipMohawkExchangeComponent>(gunship);
            Assert.That(exchange.Parked, Is.True, "The round-start parking flight must arm the exchange offer.");
            Assert.That(entities.GetComponent<DropshipComponent>(gunship).Destination, Is.Not.Null);
            pad = entities.GetComponent<DropshipComponent>(gunship).Destination!.Value;

            // A passenger is still aboard when the pilot requests the exchange.
            passenger = entities.SpawnEntity("MobHuman", new EntityCoordinates(gunship, new Vector2(0.5f, 0.5f)));
            entities.EventBus.RaiseLocalEvent(computer, new CMUDropshipMohawkExchangeMsg { Actor = passenger });
            Assert.That(exchange.Pending, Is.True, "The navigation computer message must request the exchange.");
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var exchange = entities.GetComponent<CMUDropshipMohawkExchangeComponent>(gunship);
            Assert.That(exchange.Departing, Is.False, "The gunship must not take off while anyone is aboard.");
            Assert.That(entities.GetComponent<DropshipDestinationComponent>(pad).Ship, Is.EqualTo(gunship));
            entities.DeleteEntity(passenger);
        });
        await pair.RunSeconds(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.Deleted(gunship), Is.True,
                "The empty gunship must fly back to the fleet and be removed.");
            var replacement = entities.QueryEntities<MultiDeckDropshipComponent>()
                .Select(mohawk => mohawk.Owner)
                .Where(mohawk => mohawk != originalOmaha)
                .ToArray();
            Assert.That(replacement, Has.Length.EqualTo(1), "The exchange must deliver exactly one Mohawk.");
            Assert.That(entities.GetComponent<DropshipComponent>(replacement[0]).Destination, Is.EqualTo(pad),
                "The replacement Mohawk must land on the gunship's vacated pad.");
            Assert.That(entities.GetComponent<DropshipDestinationComponent>(pad).Ship, Is.EqualTo(replacement[0]));
        });
        await pair.CleanReturnAsync();
    }
}
