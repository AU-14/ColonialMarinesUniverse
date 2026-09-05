using System.Collections.Generic;
using System.Linq;
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
    public async Task DistressMapLoadsWithPairedDirectionalProjections()
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

            var viewers = new Dictionary<string, List<RMCTeleporterViewerComponent>>();
            var query = server.EntMan.AllEntityQueryEnumerator<RMCTeleporterViewerComponent, TransformComponent>();
            while (query.MoveNext(out _, out var viewer, out var transform))
            {
                if (transform.MapUid != map!.Value.Owner)
                    continue;

                Assert.That(viewer.Id, Is.Not.Empty);
                if (!viewers.TryGetValue(viewer.Id, out var group))
                {
                    group = new List<RMCTeleporterViewerComponent>();
                    viewers.Add(viewer.Id, group);
                }

                group.Add(viewer);
            }

            Assert.That(viewers, Is.Not.Empty, "The map must retain its projection links.");
            foreach (var (id, group) in viewers)
            {
                Assert.That(group, Has.Count.EqualTo(2), $"Projection {id} must have both endpoints.");
                Assert.That(group.Count(viewer => viewer.ProjectionEnabled), Is.EqualTo(1),
                    $"Projection {id} must render in exactly one direction.");
            }

            server.System<SharedMapSystem>().DeleteMap(map!.Value.Comp.MapId);
        });

        await pair.CleanReturnAsync();
    }
}
