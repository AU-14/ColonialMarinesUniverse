using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Teleporter;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Maps;

[TestFixture]
public sealed class BosenmoriMapTest
{
    private static readonly ProtoId<GamePresetPrototype> DistressSignal = "DistressSignal";
    private static readonly ProtoId<GameMapPrototype> Bosenmori = "BosenmoriBasho";

    [Test]
    public async Task DistressMapLoadsLinkedLevelsWithoutOrphanedProjections()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var preset = prototypes.Index(DistressSignal);
            Assert.That(preset.SupportedPlanets, Does.Contain("AUPlanetBosenmoriBasho"));

            var mapPrototype = prototypes.Index(Bosenmori);
            var loader = server.System<MapLoaderSystem>();
            var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = true };
            Assert.That(loader.TryLoadMap(mapPrototype.MapPath, out var map, out var grids, options), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Empty);

            // Bosenmori uses actual Z levels instead of off-map projection rooms.
            Assert.That(mapPrototype.MapsAbove, Is.Not.Empty);
            Assert.That(mapPrototype.MapsBelow, Is.Not.Empty);
            var levels = new Dictionary<EntityUid, int> { [map!.Value.Owner] = 0 };
            for (var index = 0; index < mapPrototype.MapsAbove.Count; index++)
            {
                Assert.That(loader.TryLoadMap(mapPrototype.MapsAbove[index], out var upper, out _, options), Is.True);
                levels.Add(upper!.Value.Owner, index + 1);
            }
            for (var index = 0; index < mapPrototype.MapsBelow.Count; index++)
            {
                Assert.That(loader.TryLoadMap(mapPrototype.MapsBelow[index], out var lower, out _, options), Is.True);
                levels.Add(lower!.Value.Owner, -index - 1);
            }
            var zLevels = server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, levels), Is.True);
            foreach (var (level, offset) in levels)
            {
                Assert.That(zLevels.TryMapOffset(map.Value.Owner, offset, out var linked), Is.True);
                Assert.That(linked!.Value.Owner, Is.EqualTo(level), $"Level {offset} must be reachable from the ground map.");
            }

            var viewers = new Dictionary<string, List<RMCTeleporterViewerComponent>>();
            var query = server.EntMan.AllEntityQueryEnumerator<RMCTeleporterViewerComponent, TransformComponent>();
            while (query.MoveNext(out _, out var viewer, out var transform))
            {
                if (transform.MapUid is not { } viewerMap || !levels.ContainsKey(viewerMap))
                    continue;

                Assert.That(viewer.Id, Is.Not.Empty);
                if (!viewers.TryGetValue(viewer.Id, out var group))
                {
                    group = new List<RMCTeleporterViewerComponent>();
                    viewers.Add(viewer.Id, group);
                }

                group.Add(viewer);
            }

            foreach (var (id, group) in viewers)
            {
                Assert.That(group, Has.Count.EqualTo(2), $"Projection {id} must have both endpoints.");
                Assert.That(group.Count(viewer => viewer.ProjectionEnabled), Is.EqualTo(1),
                    $"Projection {id} must render in exactly one direction.");
            }

            foreach (var level in levels.Keys)
                server.EntMan.DeleteEntity(level);
            server.EntMan.DeleteEntity(network);
        });

        await pair.CleanReturnAsync();
    }
}
