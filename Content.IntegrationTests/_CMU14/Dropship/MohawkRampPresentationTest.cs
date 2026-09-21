using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkRampPresentationTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    [TestCase("omaha_navy")]
    [TestCase("midway_navy")]
    public async Task LoweredRampReplicatesEachTileSprite(string variant)
    {
        var artVariant = variant.EndsWith("_navy") ? "mohawk_navy" : variant;
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        var parts = new Dictionary<Vector2, NetEntity>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            foreach (var part in entities.EntityQuery<MohawkRampSegmentComponent>().Where(p => p.Lower))
                parts.Add(entities.GetComponent<TransformComponent>(part.Owner).LocalPosition, entities.GetNetEntity(part.Owner));
            Assert.That(parts, Has.Count.EqualTo(12));
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true), Is.True);
        });
        await pair.RunSeconds(6);
        await pair.RunUntilSynced();

        async Task AssertSprites(bool deployed)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var stairs = (CMUZLevelHighGroundComponent) pair.Server.ResolveDependency<IPrototypeManager>()
                    .Index<EntityPrototype>("CMUMultiZStairs").Components["CMUZLevelHighGround"].Component;
                foreach (var (position, net) in parts)
                {
                    var uid = entities.GetEntity(net);
                    Assert.That(entities.GetComponent<TransformComponent>(uid).LocalPosition, Is.EqualTo(position));
                    Assert.That(entities.HasComponent<CMUZLevelHighGroundComponent>(uid), Is.EqualTo(deployed && position.Y == -3.5f),
                        "Only visible ramp tiles 10, 11 and 12 are stairs.");
                    if (deployed && position.Y == -3.5f)
                    {
                        Assert.That(entities.GetComponent<CMUZLevelHighGroundComponent>(uid).HeightCurve,
                            Is.EqualTo(stairs.HeightCurve), "Use the normal multi-Z staircase profile.");
                        Assert.That(entities.GetComponent<CMUZLevelHighGroundComponent>(uid).AllowVehicles, Is.False);
                    }
                    if (position.Y == -2.5f)
                    {
                        Assert.That(entities.GetComponent<PhysicsComponent>(uid).CanCollide, Is.EqualTo(deployed));
                        Assert.That(entities.HasComponent<ZLevelWallSupportComponent>(uid), Is.EqualTo(deployed));
                    }
                }
            });
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                entities.System<AppearanceSystem>().FrameUpdate(0f);
                var states = new HashSet<string>();
                // Keep the complete support surface shifted one tile aft.
                // Server map Sprite overrides do not reach connected clients.
                for (var row = 0; row < 3; row++)
                for (var column = 0; column < 3; column++)
                {
                    var position = new Vector2(column - 0.5f, row - 5.5f);
                    var uid = entities.GetEntity(parts[position]);
                    var sprite = entities.GetComponent<SpriteComponent>(uid);
                    var layer = sprite[MohawkVisuals.Layer];
                    var expected = $"ramp-{4 + row * 3 + column}-low";
                    Assert.That(layer.RsiState.ToString(), Is.EqualTo(expected), $"{variant} lowered ramp tile at {position}");
                    Assert.That(sprite.BaseRSI!.Path.ToString(), Does.EndWith($"/turf/{artVariant}/ramp.rsi"));
                    Assert.That(layer.Visible, Is.EqualTo(deployed));
                    Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero), "Each ramp sprite must align with its full tile.");
                    Assert.That(entities.GetComponent<TransformComponent>(uid).Anchored, Is.True);
                    Assert.That(entities.HasComponent<CMUZLevelHighGroundComponent>(uid), Is.EqualTo(deployed && row == 2));
                    if (deployed && row == 2)
                        Assert.That(entities.GetComponent<CMUZLevelHighGroundComponent>(uid).AllowVehicles, Is.False,
                            "The vehicle restriction must replicate to the client as well.");
                    Assert.That(entities.GetComponent<TransformComponent>(uid).LocalPosition, Is.EqualTo(position));
                    states.Add(layer.RsiState.ToString());
                }
                Assert.That(states, Has.Count.EqualTo(9), "Every tile in the lowered ramp has distinct artwork.");

                for (var column = 0; column < 3; column++)
                {
                    Assert.That(parts.ContainsKey(new Vector2(column - 0.5f, -6.5f)), Is.False,
                        "No invisible ramp or pickup area may extend past the visible bottom edge.");
                    var top = entities.GetEntity(parts[new Vector2(column - 0.5f, -2.5f)]);
                    var bulkhead = entities.GetComponent<SpriteComponent>(top);
                    Assert.That(bulkhead.BaseRSI!.Path.ToString(), Does.EndWith($"/turf/{artVariant}/walls.rsi"));
                    Assert.That(bulkhead[MohawkVisuals.Layer].RsiState.ToString(), Is.EqualTo("3,16"));
                    Assert.That(bulkhead[MohawkVisuals.Layer].Visible, Is.EqualTo(deployed));
                }
            });
        }

        await AssertSprites(true);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, false), Is.True);
        });
        await pair.RunSeconds(6);
        await pair.RunUntilSynced();
        await AssertSprites(false);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(90));
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true), Is.True);
        });
        await pair.RunSeconds(6);
        await pair.RunUntilSynced();
        await AssertSprites(true);
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
