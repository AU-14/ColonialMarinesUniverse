using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Climbing.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using NUnit.Framework;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFalconClimbingTest
{
    [Test]
    public async Task FalconCanClimbStandardClimbableThroughInteractionRelay()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        // The test server has no TTS token and probes the voice catalog unconditionally.
        server.ResolveDependency<ILogManager>().GetSawmill("tts").Level = LogLevel.Fatal;

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid table = default;
        EntityCoordinates gridCoords = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = server.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                gridCoords = new EntityCoordinates(grid, 0, 0);

                var tileDefinitionManager = server.ResolveDependency<ITileDefinitionManager>();
                var plating = tileDefinitionManager["Plating"];
                mapSystem.SetTile(grid.Owner, grid.Comp, gridCoords, new Tile(plating.TileId));

                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", gridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", gridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", gridCoords);
                table = entMan.SpawnEntity("Table", gridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
                drone = entMan.GetComponent<YautjaFalconControllerComponent>(hunter).Drone;
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<ClimbingComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<InteractionRelayComponent>(hunter), Is.True);
                Assert.That(
                    entMan.GetComponent<InteractionRelayComponent>(hunter).RelayEntity,
                    Is.EqualTo(drone));
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var interaction = entMan.System<SharedInteractionSystem>();
                var tableCoordinates = entMan.GetComponent<TransformComponent>(table).Coordinates;
                interaction.UserInteraction(hunter, tableCoordinates, table, altInteract: true);
            });

            await server.WaitRunTicks(120);

            await server.WaitAssertion(() =>
            {
                var climbing = server.EntMan.GetComponent<ClimbingComponent>(drone);
                Assert.That(climbing.IsClimbing, Is.True);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.DeleteEntity(drone);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<InteractionRelayComponent>(hunter), Is.False);
                Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.False);
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
