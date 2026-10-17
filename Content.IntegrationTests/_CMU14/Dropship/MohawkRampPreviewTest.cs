using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkRampPreviewTest
{
    [TestCase("midway", 0, false)]
    [TestCase("midway", 90, true)]
    [TestCase("midway_navy", 180, true)]
    [TestCase("omaha", 0, true)]
    public async Task DeployedRampPreviewsCabinFromGround(string variant, int degrees, bool landingPad)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var server = pair.Server;
        EntityUid ship = default;
        EntityUid lower = default;
        EntityUid camera = default;
        NetEntity cameraNet = default;
        NetEntity shipNet = default;
        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(CMUZLevelsCVars.Enabled, true);
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            lower = assembly.Decks[-1];
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);
            var lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            if (landingPad)
            {
                var pad = entities.EnsureComponent<MapGridComponent>(lowerMap);
                var tile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
                for (var x = -12; x <= 12; x++)
                for (var y = -12; y <= 12; y++)
                    maps.SetTile(lowerMap, pad, new Vector2i(x, y), tile);
            }
            // The sloped ramp row is at Y=-3.5; stand on the flat row within its 1.2-tile preview range.
            var approach = transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, -4.5f));
            camera = entities.SpawnEntity(null, new EntityCoordinates(lowerMap, approach.Position));
            entities.GetComponent<TransformComponent>(camera).GridTraversal = false;
            entities.EnsureComponent<EyeComponent>(camera);
            entities.System<ViewSubscriberSystem>().AddViewSubscriber(camera, pair.Player!);
            cameraNet = entities.GetNetEntity(camera);
            shipNet = entities.GetNetEntity(ship);
        });

        async Task AssertPreview(bool enabled)
        {
            await pair.RunSeconds(1);
            await pair.RunUntilSynced();
            await server.WaitAssertion(() =>
            {
                var viewer = server.EntMan.GetComponent<CMUZLevelViewerComponent>(camera);
                Assert.That(viewer.StairPreviewUp, Is.EqualTo(enabled));
                Assert.That(viewer.StairPreviewGrid,
                    Is.EqualTo(enabled && variant.StartsWith("midway") ? (EntityUid?) ship : null));
                if (enabled)
                {
                    Assert.That(viewer.StairPreviewPositionCount, Is.GreaterThan(0));
                    Assert.That(viewer.Eyes.Any(eye =>
                        server.EntMan.GetComponent<TransformComponent>(eye).MapUid ==
                        server.EntMan.GetComponent<TransformComponent>(ship).MapUid), Is.True,
                        "The cabin needs a server visibility subscription as well as a client preview.");
                }
            });
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var viewer = entities.GetComponent<CMUZLevelViewerComponent>(entities.GetEntity(cameraNet));
                Assert.That(viewer.StairPreviewUp, Is.EqualTo(enabled));
                Assert.That(viewer.StairPreviewGrid,
                    Is.EqualTo(enabled && variant.StartsWith("midway") ? (EntityUid?) entities.GetEntity(shipNet) : null));
            });
        }

        await AssertPreview(false);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<MohawkSystem>().SetRampDeployed(ship, true, true), Is.True));
        await AssertPreview(true);
        await server.WaitAssertion(() =>
        {
            var transform = server.EntMan.System<SharedTransformSystem>();
            transform.SetWorldPosition(camera,
                transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, -15.5f)).Position);
        });
        await AssertPreview(false);
        await server.WaitAssertion(() =>
        {
            var transform = server.EntMan.System<SharedTransformSystem>();
            transform.SetWorldPosition(camera,
                transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, -4.5f)).Position);
        });
        await AssertPreview(true);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<MohawkSystem>().SetRampDeployed(ship, false, true), Is.True));
        await AssertPreview(false);
        await server.WaitAssertion(() =>
        {
            server.EntMan.System<ViewSubscriberSystem>().RemoveViewSubscriber(camera, pair.Player!);
            server.EntMan.DeleteEntity(camera);
            server.EntMan.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }
}
