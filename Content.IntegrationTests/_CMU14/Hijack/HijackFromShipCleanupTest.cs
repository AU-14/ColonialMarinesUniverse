using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.util;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Hijack;

// BUG-676: a queen who starts the hijack from a dropship docked at the warship must still
// wipe the hive core and the xenos left behind on the planet
[TestFixture]
public sealed class HijackFromShipCleanupTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<GameMapPrototype> Bush = "USSBushRedux";
    private static readonly Robust.Shared.Prototypes.ProtoId<PlatoonPrototype> Hazops = "HAZOPS";
    private static readonly Robust.Shared.Prototypes.EntProtoId PlatoonSpawn = "PlatoonSpawn";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUHijackFromShipPlanet
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

    [TestCase("HiveCoreXeno")]
    [TestCase("CMU14PathogenBlightCore")]
    public async Task QueenHijackingFromTheShipClearsThePlanet(string coreProto)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid core = default, drone = default, queen = default, nav = default;

        await server.WaitAssertion(() =>
        {
            var config = server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            config.SetCVar(CCVars.FTLStartupTime, 0.5f);
            config.SetCVar(CCVars.FTLTravelTime, 1f);
            config.SetCVar(CCVars.FTLArrivalTime, 0.5f);

            var ticker = entities.System<GameTicker>();
            var platoons = entities.System<PlatoonSpawnRuleSystem>();
            platoons.SelectedGovforPlatoon = server.ProtoMan.Index(Hazops);
            platoons.SelectedOpforPlatoon = null;
            Assert.That(entities.System<AuRoundSystem>().SetPlanet("CMUHijackFromShipPlanet"), Is.True);
            var grids = ticker.LoadGameMap(server.ProtoMan.Index<GameMapPrototype>(Bush),
                out _, DeserializationOptions.Default with { InitializeMaps = true });
            foreach (var grid in grids)
                entities.EnsureComponent<ShipFactionComponent>(grid).Faction = "govfor";
            Assert.That(ticker.StartGameRule(PlatoonSpawn), Is.True);
        });

        // let the dropships finish docking at the Bush
        await pair.RunSeconds(5);

        var planet = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var dropship = entities.QueryEntities<DropshipComponent>()
                .First(d => !entities.HasComponent<MultiDeckDropshipComponent>(d.Owner)).Owner;
            nav = entities.QueryEntities<DropshipNavigationComputerComponent>()
                .First(n => entities.GetComponent<TransformComponent>(n.Owner).GridUid == dropship).Owner;

            entities.EnsureComponent<RMCPlanetComponent>(planet.MapUid);
            var hives = entities.System<SharedXenoHiveSystem>();
            var hive = entities.SpawnEntity("CMXenoHive", planet.GridCoords);
            core = entities.SpawnEntity(coreProto, planet.GridCoords);
            drone = entities.SpawnEntity("CMXenoDrone", planet.GridCoords);
            queen = entities.SpawnEntity("CMXenoQueen", entities.GetComponent<TransformComponent>(nav).Coordinates);
            hives.SetHive(core, hive);
            hives.SetHive(drone, hive);
            hives.SetHive(queen, hive);
            // a fresh queen is still maturing, and maturing queens may not hijack from the ship
            entities.RemoveComponent<XenoMaturingComponent>(queen);

            var msg = new DropshipHijackerInitiateBuiMsg { Actor = queen, UiKey = DropshipHijackerUiKey.Key };
            entities.EventBus.RaiseLocalEvent(nav, msg);
            Assert.That(entities.GetComponent<DropshipComponent>(dropship).Crashed, Is.True, "The hijack never started.");
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(core), Is.True, "The hive core on the planet survived the hijack.");
                Assert.That(entities.Deleted(drone), Is.True, "A xeno left on the planet survived the hijack.");
                Assert.That(entities.Deleted(queen), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
