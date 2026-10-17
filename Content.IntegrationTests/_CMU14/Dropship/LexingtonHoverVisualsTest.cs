using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Server.Shuttles.Events;
using Content.Server._RMC14.Dropship;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using PointLightComponent = Robust.Server.GameObjects.PointLightComponent;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class LexingtonHoverVisualsTest
{
    [TestCase("/Maps/_RMC14/Shuttles/dynamic_gunship.yml")]
    public async Task HoverUsesHullArtworkAndThreeLocalNozzles(string path)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var loader = entities.System<MapLoaderSystem>();
            var transforms = entities.System<SharedTransformSystem>();
            Assert.That(loader.TryLoadMap(new ResPath(path), out var map, out var grids,
                DeserializationOptions.Default with { InitializeMaps = false }), Is.True);
            var grid = grids!.Single();
            entities.EnsureComponent<DropshipComponent>(grid);

            // A translated, quarter-turned ship must retain the authored local origin.
            transforms.SetCoordinates(grid, new EntityCoordinates(map!.Value.Owner, new Vector2(25, -13)));
            transforms.SetWorldRotation(grid, Angle.FromDegrees(90));
            var destination = entities.SpawnEntity(null, new EntityCoordinates(map.Value.Owner, Vector2.Zero));
            var ephemeral = entities.AddComponent<EphemeralDropshipDestinationComponent>(destination);
            ephemeral.TacticalHover = true;
            ephemeral.Footprint = new Vector2i(99, 99); // Deliberately unrelated landing clearance.
            var arrived = new DropshipRelayedEvent<FTLCompletedEvent>(
                new FTLCompletedEvent(grid.Owner, map.Value.Owner), grid.Owner);
            entities.EventBus.RaiseLocalEvent(destination, ref arrived);

            var hover = entities.GetComponent<DropshipTacticalHoverComponent>(grid);
            var shadowUid = hover.Shadow!.Value;
            var shadow = entities.GetComponent<DropshipTacticalHoverShadowComponent>(shadowUid);
            Assert.Multiple(() =>
            {
                Assert.That(shadow.Tiles, Is.Not.Empty);
                Assert.That(shadow.Parts.Any(p => p.Prototype == "CMNormandyWall100"), Is.True,
                    "The tapered nose must come from its transparent hull art.");
                Assert.That(shadow.Parts.Any(p => p.Prototype == "CMNormandyWall100" &&
                    p.Position == new Vector2(-.5f, 8.5f)), Is.True);
                Assert.That(shadow.HullBounds.Width, Is.LessThan(20),
                    "Landing clearance must not stretch the silhouette.");
                Assert.That(shadow.HullBounds.Center.X, Is.EqualTo(.5f).Within(.001f));
                Assert.That(transforms.GetWorldPosition(shadowUid), Is.EqualTo(transforms.GetWorldPosition(grid)));
                Assert.That(transforms.GetWorldRotation(shadowUid), Is.EqualTo(transforms.GetWorldRotation(grid)));
                Assert.That(hover.Downwashes, Has.Count.EqualTo(3));
                Assert.That(hover.NozzleLights, Has.Count.EqualTo(3));
            });

            var appearance = entities.GetComponent<DropshipTacticalHoverAppearanceComponent>(grid);
            Assert.That(appearance.DownwashOffsets[0].X, Is.LessThan(.5f));
            Assert.That(appearance.DownwashOffsets[1].X, Is.GreaterThan(.5f));
            Assert.That(appearance.DownwashOffsets[2], Is.EqualTo(new Vector2(.5f, 7.5f)));
            foreach (var uid in hover.Downwashes)
            {
                var wash = entities.GetComponent<DropshipTacticalHoverDownwashComponent>(uid);
                Assert.That(wash.JetExhaust, Is.True);
                Assert.That(appearance.DownwashOffsets, Does.Contain(wash.Offset));
                var expected = transforms.GetWorldPosition(grid) + transforms.GetWorldRotation(grid).RotateVec(wash.Offset);
                Assert.That(Vector2.Distance(transforms.GetWorldPosition(uid), expected), Is.LessThan(.001f));
                var light = entities.GetComponent<PointLightComponent>(uid);
                Assert.That(light.Enabled, Is.True);
                Assert.That(light.Color, Is.EqualTo(Color.FromHex("#49bfff")));
                Assert.That(light.Radius, Is.GreaterThan(2));
                Assert.That(light.Energy, Is.GreaterThan(0));
            }

            foreach (var uid in hover.NozzleLights)
            {
                var xform = entities.GetComponent<TransformComponent>(uid);
                Assert.That(xform.ParentUid, Is.EqualTo(grid.Owner), "Nozzle lights must travel on the actual ship.");
                Assert.That(appearance.DownwashOffsets, Does.Contain(xform.LocalPosition));
                var light = entities.GetComponent<PointLightComponent>(uid);
                Assert.That(light.Enabled, Is.True);
                Assert.That(light.Color, Is.EqualTo(Color.FromHex("#79dfff")));
                Assert.That(light.Energy, Is.GreaterThan(0));
                Assert.That(light.CastShadows, Is.False, "Nearby hull walls must not hide the nozzle glow.");
            }
            var lights = hover.NozzleLights.ToArray();

            entities.System<DropshipTacticalLandSystem>().EndTacticalHoverForReroute(grid);
            Assert.That(entities.HasComponent<DropshipTacticalHoverComponent>(grid), Is.False);
            Assert.That(hover.Shadow, Is.Null);
            Assert.That(hover.Downwashes, Is.Empty);
            Assert.That(hover.NozzleLights, Is.Empty);
            Assert.That(lights.All(entities.IsQueuedForDeletion), Is.True, "Ending hover must remove its ship lights.");
            entities.System<SharedMapSystem>().DeleteMap(map.Value.Comp.MapId);
        });
        await pair.CleanReturnAsync();
    }
}
