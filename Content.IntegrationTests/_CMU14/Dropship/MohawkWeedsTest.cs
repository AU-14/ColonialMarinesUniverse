using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkWeedsTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: XenoWeedsSource
  id: CMUMohawkTestWeedSource
  components:
  - type: XenoWeedsSpreading
    spreadDelay: 0.05
""";

    [TestCase("omaha", 0, true)]
    [TestCase("midway", 90, false)]
    [TestCase("omaha_navy", 180, false)]
    [TestCase("midway_navy", 270, true)]
    public async Task LoweredRampBlocksWeedsAcrossGridsAndDrawsAboveThem(string variant, int degrees, bool terrainOnMap)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        Entity<MapGridComponent> ground = default;
        Entity<MapGridComponent> lower = default;
        NetEntity groundNet = default;
        NetEntity lowerNet = default;
        NetEntity bellyNet = default;
        NetEntity oldWeedsNet = default;
        var rampTiles = new List<(NetEntity Segment, Vector2i Lower, Vector2i Ground)>();
        Vector2i blockedGrowth = default;
        Vector2i freeGrowth = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var weeds = entities.System<SharedXenoWeedsSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            transform.SetWorldPosition(ship, new Vector2(16, -8));
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);
            var lowerUid = assembly.Decks[-1];
            lower = new Entity<MapGridComponent>(lowerUid, entities.GetComponent<MapGridComponent>(lowerUid));
            lowerNet = entities.GetNetEntity(lower);
            var lowerTransform = entities.GetComponent<TransformComponent>(lower);
            ground = terrainOnMap
                ? new Entity<MapGridComponent>(lowerTransform.MapUid!.Value,
                    entities.EnsureComponent<MapGridComponent>(lowerTransform.MapUid.Value))
                : maps.CreateGridEntity(lowerTransform.MapID);
            // New grids default to mobile shuttles. This grid represents a fixed
            // landing pad, which must not be pushed away by the ramp bulkhead.
            if (!terrainOnMap)
            {
                entities.RemoveComponent<ShuttleComponent>(ground);
                entities.System<SharedPhysicsSystem>().SetBodyType(ground, BodyType.Static);
            }
            groundNet = entities.GetNetEntity(ground);
            var tile = new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(ground, GroundTile(new Vector2(x + 0.5f, y + 0.5f)), tile);

            foreach (var ramp in entities.EntityQuery<MohawkRampSegmentComponent>().Where(r => r.Lower))
            {
                var xform = entities.GetComponent<TransformComponent>(ramp.Owner);
                Assert.That(xform.Anchored, Is.True);
                var lowerTile = maps.LocalToTile(lower, lower, xform.Coordinates);
                var groundTile = GroundTile(xform.LocalPosition);
                rampTiles.Add((entities.GetNetEntity(ramp.Owner), lowerTile, groundTile));
                Assert.That(weeds.CanSpreadWeedsPopup(ground, groundTile, null, null), Is.True,
                    "A raised ramp must not block growth on the ground beneath it.");
            }
            Assert.That(rampTiles, Has.Count.EqualTo(12));

            var source = entities.SpawnEntity("CMUMohawkTestWeedSource",
                maps.GridTileToLocal(ground, ground, GroundTile(new Vector2(2.5f, -5.5f))));
            var oldWeeds = entities.SpawnEntity("XenoWeeds",
                maps.GridTileToLocal(ground, ground, GroundTile(new Vector2(-0.5f, -4.5f))));
            weeds.AssignSource(oldWeeds, source);
            oldWeedsNet = entities.GetNetEntity(oldWeeds);
            var belly = entities.EntityQuery<MetaDataComponent>().Single(m => m.EntityName == "underside fuel lines");
            bellyNet = entities.GetNetEntity(belly.Owner);
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true, true), Is.True);
            foreach (var (_, lowerTile, groundTile) in rampTiles)
            {
                Assert.That(weeds.CanSpreadWeedsPopup(lower, lowerTile, null, null), Is.False,
                    "Weeds must not spread onto the ramp's own grid.");
                Assert.That(weeds.CanSpreadWeedsPopup(ground, groundTile, null, null), Is.False,
                    "Terrain weeds must not grow through an overlapping lowered ramp.");
                Assert.That(weeds.CanSpreadWeedsPopup(ground, groundTile, null, null, source: true), Is.False);
                Assert.That(weeds.CanPlaceWeedsPopup(oldWeeds, ground,
                    maps.GridTileToLocal(ground, ground, groundTile), false), Is.False,
                    "Planting or upgrading an existing weed into a node must also respect the ramp.");
            }

            blockedGrowth = GroundTile(new Vector2(1.5f, -5.5f));
            freeGrowth = GroundTile(new Vector2(3.5f, -5.5f));
            Assert.That(weeds.CanSpreadWeedsPopup(ground, freeGrowth, null, null), Is.True,
                "Blocking must stop at the ramp's actual edge.");
            Vector2i GroundTile(Vector2 position)
                => maps.WorldToTile(ground, ground,
                    transform.ToMapCoordinates(new EntityCoordinates(lower, position)).Position);
        });
        await pair.RunSeconds(0.2f);
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            var weeds = pair.Server.EntMan.System<SharedXenoWeedsSystem>();
            var maps = pair.Server.EntMan.System<SharedMapSystem>();
            Assert.That(weeds.GetWeedsOnFloor(ground, maps.GridTileToLocal(ground, ground, blockedGrowth)), Is.Null,
                "An actual spreading node beside the ramp must leave its surface clear.");
            Assert.That(weeds.GetWeedsOnFloor(ground, maps.GridTileToLocal(ground, ground, freeGrowth)), Is.Not.Null,
                "The same node must still spread onto nearby unblocked ground.");
        });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            entities.System<AppearanceSystem>().FrameUpdate(0);
            var weeds = entities.System<SharedXenoWeedsSystem>();
            var groundUid = entities.GetEntity(groundNet);
            var clientGround = new Entity<MapGridComponent>(groundUid, entities.GetComponent<MapGridComponent>(groundUid));
            var lowerUid = entities.GetEntity(lowerNet);
            var clientLower = new Entity<MapGridComponent>(lowerUid, entities.GetComponent<MapGridComponent>(lowerUid));
            var oldWeeds = entities.GetComponent<SpriteComponent>(entities.GetEntity(oldWeedsNet));
            var belly = entities.GetComponent<SpriteComponent>(entities.GetEntity(bellyNet));
            Assert.That(belly.DrawDepth, Is.GreaterThan(oldWeeds.DrawDepth),
                "Existing ground weeds must draw underneath the fuel-line artwork.");
            Assert.That(belly.DrawDepth, Is.LessThan((int) DrawDepth.DeadMobs));
            foreach (var (net, lowerTile, groundTile) in rampTiles)
            {
                var ramp = entities.GetEntity(net);
                var sprite = entities.GetComponent<SpriteComponent>(ramp);
                Assert.That(sprite[MohawkVisuals.Layer].Visible, Is.True);
                Assert.That(sprite.DrawDepth, Is.GreaterThan(belly.DrawDepth));
                Assert.That(sprite.DrawDepth, Is.LessThan((int) DrawDepth.DeadMobs),
                    "Ramp occupants must remain visible above the ramp.");
                Assert.That(weeds.CanSpreadWeedsPopup(clientLower, lowerTile, null, null), Is.False);
                Assert.That(weeds.CanSpreadWeedsPopup(clientGround, groundTile, null, null), Is.False,
                    "Client prediction must see the same cross-grid blocker as the server.");
            }
        });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, false, true), Is.True);
            var weeds = entities.System<SharedXenoWeedsSystem>();
            foreach (var (_, _, groundTile) in rampTiles)
                Assert.That(weeds.CanSpreadWeedsPopup(ground, groundTile, null, null), Is.True,
                    "Retracting the ramp must allow growth on the uncovered ground again.");
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            entities.System<AppearanceSystem>().FrameUpdate(0);
            foreach (var (net, _, _) in rampTiles)
            {
                var ramp = entities.GetEntity(net);
                Assert.That(entities.HasComponent<BlockWeedsComponent>(ramp), Is.False);
                Assert.That(entities.GetComponent<SpriteComponent>(ramp)[MohawkVisuals.Layer].Visible, Is.False);
            }
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
