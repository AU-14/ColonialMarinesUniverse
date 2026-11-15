using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntingGroundMapTest
{

    [Test]
    public async Task TeleporterDialogRejectsResponsesFromThePreviousActor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var teleporter = em.SpawnEntity("CMUHunterShipTeleporterYautjaShip", map.GridCoords);
            var destination = em.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            var first = em.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            var second = em.SpawnEntity(null, map.GridCoords.Offset(new Vector2(2, 0)));
            try
            {
                em.EnsureComponent<YautjaTechAuthorizedComponent>(first);
                em.EnsureComponent<YautjaTechAuthorizedComponent>(second);
                var target = em.EnsureComponent<YautjaHuntTeleportDestinationComponent>(destination);
                target.Kind = YautjaHuntTeleporterKind.Ship;
                target.Id = "dialog-regression";
                em.GetComponent<YautjaHuntTeleporterComponent>(teleporter).DestinationId = target.Id;
                var firstPosition = em.GetComponent<TransformComponent>(first).Coordinates;
                var secondPosition = em.GetComponent<TransformComponent>(second).Coordinates;
                var firstStep = new StepTriggeredOnEvent(teleporter, first);
                em.EventBus.RaiseLocalEvent(teleporter, ref firstStep);
                var secondStep = new StepTriggeredOnEvent(teleporter, second);
                em.EventBus.RaiseLocalEvent(teleporter, ref secondStep);

                BoundUserInterfaceMessage[] staleResponses =
                [
                    new DialogConfirmBuiMsg(),
                    new DialogOptionBuiMsg(0),
                    new DialogInputBuiMsg("stale"),
                ];
                foreach (var response in staleResponses)
                {
                    response.Actor = first;
                    response.UiKey = DialogUiKey.Key;
                    em.EventBus.RaiseLocalEvent(teleporter, (object) response);
                    Assert.That(em.HasComponent<DialogComponent>(teleporter), Is.True);
                    Assert.That(em.GetComponent<TransformComponent>(second).Coordinates, Is.EqualTo(secondPosition));
                }
                em.EventBus.RaiseLocalEvent(teleporter, new BoundUIClosedEvent(DialogUiKey.Key, teleporter, first));
                Assert.That(em.HasComponent<DialogComponent>(teleporter), Is.True);

                em.EventBus.RaiseLocalEvent(teleporter, new DialogConfirmBuiMsg
                {
                    Actor = second,
                    UiKey = DialogUiKey.Key,
                });
                Assert.That(em.GetComponent<TransformComponent>(second).Coordinates,
                    Is.EqualTo(em.GetComponent<TransformComponent>(destination).Coordinates));
                Assert.That(em.GetComponent<TransformComponent>(first).Coordinates, Is.EqualTo(firstPosition));
                Assert.That(em.HasComponent<DialogComponent>(teleporter), Is.False);
            }
            finally
            {
                foreach (var uid in new[] { teleporter, destination, first, second })
                    em.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static readonly ResPath DesertMoonCavesPath = new("/Maps/CMU14/HuntingGrounds/desert_moon_caves.yml");

    [Test]
    public async Task DesertMoonShipTeleporterLandsOnOpenDestination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var origin = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var turf = entMan.System<TurfSystem>();

            Assert.That(loader.TryLoadMap(
                DesertMoonCavesPath,
                out var map,
                out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Null);

            var destination = entMan.QueryEntities<YautjaHuntTeleportDestinationComponent, TransformComponent>()
                .Where(destination =>
                    destination.Item1.Comp.Kind == YautjaHuntTeleporterKind.Ship &&
                    destination.Item1.Comp.Id == "desert_moon")
                .Select(destination => destination.Item2.Owner)
                .Single();
            var teleporter = entMan.SpawnEntity(null, origin.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", origin.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "desert_moon";

                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var actual = transform.GetMapCoordinates(hunter);
                var expected = transform.GetMapCoordinates(destination);
                Assert.That(actual.MapId, Is.EqualTo(expected.MapId));
                Assert.That(actual.Position, Is.EqualTo(expected.Position));

                var destinationXform = entMan.GetComponent<TransformComponent>(destination);
                Assert.That(destinationXform.GridUid, Is.Not.Null);
                Assert.That(entMan.TryGetComponent(destinationXform.GridUid!.Value, out MapGridComponent? grid), Is.True);
                Assert.That(mapSystem.TryGetTileRef(
                    destinationXform.GridUid.Value,
                    grid!,
                    destinationXform.Coordinates,
                    out var tileRef), Is.True);
                Assert.That(tileRef.Tile.IsEmpty, Is.False);
                Assert.That(turf.IsTileBlocked(tileRef, CollisionGroup.MobMask), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

}
