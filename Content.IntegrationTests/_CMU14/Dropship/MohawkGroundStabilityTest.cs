using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkGroundStabilityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ParkedMidwayUndersideRemainsStill(bool shepherds)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid ship = default;
        EntityUid lower = default;
        var occupants = new Dictionary<EntityUid, Vector2>();
        await server.WaitAssertion(() =>
        {
            Assert.That(server.CfgMan.GetCVar(CCVars.SpaceWind), Is.False);
            server.CfgMan.SetCVar(CCVars.AtmosGridImpulse, true);
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var cabinMapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(cabinMapId,
                new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/midway.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            var ground = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var offset = Vector2.Zero;
            if (shepherds)
            {
                Assert.That(entities.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/CMU14/SheperdsMultiZ/SheperdsMultiZ0.yml"),
                    out var planet, out _, DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
                ground = planet!.Value.Owner;
                offset = new Vector2(71.5f, 61.5f);
                var decks = entities.System<MultiDeckDropshipSystem>();
                Assert.That(decks.TryGetLandingCoordinates(ship, new EntityCoordinates(ground, offset), out var landing), Is.True);
                entities.System<SharedTransformSystem>().SetCoordinates(ship, landing);
                decks.Synchronize((ship, entities.GetComponent<MultiDeckDropshipComponent>(ship)));
            }
            else
            {
                var grid = entities.EnsureComponent<MapGridComponent>(ground);
                var tiles = server.ResolveDependency<ITileDefinitionManager>();
                var floor = new Tile(tiles["FloorSteel"].TileId);
                for (var x = -10; x <= 10; x++)
                for (var y = -10; y <= 15; y++)
                    maps.SetTile(ground, grid, new Vector2i(x, y), floor);
                var air = new GasMixture(2500) { Temperature = Atmospherics.T20C };
                air.SetMoles(Gas.Oxygen, 21.8124f);
                air.SetMoles(Gas.Nitrogen, 82.10312f);
                entities.System<AtmosphereSystem>().SetMapAtmosphere(ground, false, air);
            }

            foreach (var position in new[] { new Vector2(0.5f, 0.5f), new Vector2(-2.5f, 1.5f), new Vector2(2.5f, -0.5f) })
            foreach (var prototype in new EntProtoId[] { "CMMobHuman", "Wrench" })
            {
                var occupant = entities.SpawnEntity(prototype, new EntityCoordinates(ground, position + offset));
                occupants.Add(occupant, entities.System<SharedTransformSystem>().GetWorldPosition(occupant));
            }
            server.CfgMan.SetCVar(RMCCVars.RMCAtmosTileEqualize, true);
        });

        // Let initial spawn contacts resolve before measuring movement while parked.
        // Boarding momentum is covered separately by DropshipBoardingMomentumTest.
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            foreach (var occupant in occupants.Keys.ToArray())
                occupants[occupant] = entities.System<SharedTransformSystem>().GetWorldPosition(occupant);
        });
        await pair.RunSeconds(10);
        await server.WaitAssertion(() =>
        {
            var transform = entities.System<SharedTransformSystem>();
            var body = entities.GetComponent<PhysicsComponent>(lower);
            Assert.That(body.LinearVelocity, Is.EqualTo(Vector2.Zero));
            Assert.That(body.AngularVelocity, Is.Zero);
            Assert.That(transform.GetWorldPosition(lower), Is.EqualTo(transform.GetWorldPosition(ship)));
            foreach (var (occupant, position) in occupants)
            {
                Assert.That(Vector2.Distance(transform.GetWorldPosition(occupant), position), Is.LessThan(0.05f),
                    $"{entities.GetComponent<MetaDataComponent>(occupant).EntityPrototype?.ID}: " +
                    $"{position} -> {transform.GetWorldPosition(occupant)}; " +
                    $"parent {entities.GetComponent<TransformComponent>(occupant).ParentUid}, " +
                    $"velocity {entities.GetComponent<PhysicsComponent>(occupant).LinearVelocity}");
                Assert.That(entities.GetComponent<PhysicsComponent>(occupant).LinearVelocity, Is.EqualTo(Vector2.Zero));
            }
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }
}
