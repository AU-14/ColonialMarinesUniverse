using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using Content.Client.CMU14.ZLevels.Lighting;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Candidate = Content.Client.CMU14.ZLevels.Lighting.CMUZLevelProjectedLightingSystem.ProjectedLightCandidate;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZProjectedLightingLifecycleTest : GameTest
{
    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public async Task MovingApertureRetainsItsEntity(int framesPerSecond)
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.SpawnLight(2, new Vector2(0.5f));
            var portal = scene.Maps.CreateGridEntity(scene.MapIds[2]);
            scene.Maps.SetTile(portal, new Vector2i(1, 0), scene.Floor);
            var created = scene.Lighting.TotalCreated;
            EntityUid? first = null;
            var candidates = new List<Candidate>(1);
            for (var frame = 0; frame < framesPerSecond * 3; frame++)
            {
                var translation = new Vector2(frame / (float) framesPerSecond * 0.2f, 0f);
                scene.Transform.SetCoordinates(portal, new EntityCoordinates(scene.Levels[2], translation));
                var center = translation + new Vector2(0.5f);
                candidates.Clear();
                candidates.Add(scene.Candidate(source, 2, 1, portal, Vector2i.Zero, center));
                scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, (uint) frame, 0.18f);
                var projected = scene.Projected().Single();
                first ??= projected;
                Assert.That(projected, Is.EqualTo(first.Value), "World motion must update data rather than create a key.");
                Assert.That(scene.Transform.GetWorldPosition(projected), Is.EqualTo(center));
            }

            Assert.That(scene.Lighting.TotalCreated - created, Is.EqualTo(1));
            Assert.That(scene.Entities.GetComponent<PointLightComponent>(first!.Value).CastShadows, Is.True);
        });
    }

    [Test]
    public async Task EveryIncomingDepthAndFadeSharesReceivingAndGlobalBudgets()
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var lower = scene.SpawnLight(0, new Vector2(0.5f));
            var upper = scene.SpawnLight(2, new Vector2(0.5f));
            var created = scene.Lighting.TotalCreated;
            var candidates = new List<Candidate>
            {
                scene.Candidate(lower, 0, 1, scene.Levels[1], Vector2i.Zero, new Vector2(0.5f), 0.9f),
                scene.Candidate(upper, 2, 1, scene.Levels[2], Vector2i.Zero, new Vector2(0.5f), 0.8f),
                scene.Candidate(upper, 2, 0, scene.Levels[2], Vector2i.Zero, new Vector2(0.5f), 0.7f),
            };
            scene.Lighting.ReconcileProjectedLights(candidates, 2, 3, 1, 0.18f);
            Assert.That(scene.Projected(), Has.Count.EqualTo(3));
            candidates.Clear();
            scene.Lighting.ReconcileProjectedLights(candidates, 2, 3, 2, 0.18f);
            Assert.That(scene.Projected(), Has.Count.EqualTo(3), "The old lights are still inside visibility grace.");

            // New portals compete with live fades. Reuse their entities instead of allocating above the cap.
            candidates.Add(scene.Candidate(upper, 2, 1, scene.Levels[2], Vector2i.One, new Vector2(1.5f), 1f));
            candidates.Add(scene.Candidate(lower, 0, 1, scene.Levels[1], new Vector2i(2, 0), new Vector2(2.5f, 0.5f), 0.9f));
            candidates.Add(scene.Candidate(upper, 2, 1, scene.Levels[2], new Vector2i(3, 0), new Vector2(3.5f, 0.5f), 0.8f));
            scene.Lighting.ReconcileProjectedLights(candidates, 2, 3, 3, 0.18f);
            Assert.That(scene.Projected(), Has.Count.EqualTo(3));
            Assert.That(scene.Projected().Count(uid => scene.Entities.GetComponent<TransformComponent>(uid).MapID == scene.MapIds[1]), Is.EqualTo(2));
            Assert.That(scene.Lighting.TotalCreated - created, Is.EqualTo(3));
            Assert.That(scene.Lighting.TotalReassigned, Is.GreaterThanOrEqualTo(2));

            scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, 4, 0.18f);
            Assert.That(scene.Projected(), Has.Count.EqualTo(1), "Reducing either live budget must take effect immediately.");
            var retained = scene.Entities.GetComponent<CMUProjectedLightComponent>(scene.Projected().Single());
            Assert.That(retained.PortalTile, Is.EqualTo(Vector2i.One));
            Assert.That(retained.OpeningCenter, Is.EqualTo(new Vector2(1.5f)), "No weighted overflow midpoint is an aperture.");
        });
    }

    [TestCase("floor")]
    [TestCase("intermediate")]
    [TestCase("source")]
    [TestCase("network")]
    [TestCase("map")]
    public async Task InvalidTransmissionIsRemovedWithoutVisibilityGrace(string invalidation)
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.SpawnLight(0, new Vector2(0.5f));
            var candidates = new List<Candidate> { scene.Candidate(source, 0, 2, scene.Levels[2], Vector2i.Zero, new Vector2(0.5f)) };
            scene.Lighting.ReconcileProjectedLights(candidates, 2, 4, 1, 1f);
            var projected = scene.Projected().Single();
            switch (invalidation)
            {
                case "floor":
                    scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], Vector2i.Zero, scene.Floor);
                    break;
                case "intermediate":
                    scene.Maps.SetTile(scene.Levels[1], scene.Grids[1], Vector2i.Zero, scene.Floor);
                    break;
                case "source":
                    scene.Entities.DeleteEntity(source);
                    break;
                case "network":
                    scene.Entities.RemoveComponent<CMUZLevelsNetworkComponent>(scene.Network);
                    break;
                case "map":
                    scene.Entities.DeleteEntity(scene.Levels[0]);
                    break;
            }

            candidates.Clear();
            scene.Lighting.ReconcileProjectedLights(candidates, 2, 4, 2, 1f);
            Assert.That(scene.Entities.Deleted(projected), Is.True);
            Assert.That(scene.Projected(), Is.Empty);
        });
    }

    [Test]
    public async Task MissingSelectedLightComponentIsRecreatedAndExpiredFadeIsDeleted()
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.SpawnLight(2, new Vector2(0.5f));
            var candidates = new List<Candidate> { scene.Candidate(source, 2, 1, scene.Levels[2], Vector2i.Zero, new Vector2(0.5f)) };
            scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, 1, 0.18f);
            var previous = scene.Projected().Single();
            scene.Entities.RemoveComponent<PointLightComponent>(previous);
            scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, 2, 0.18f);
            Assert.That(scene.Entities.Deleted(previous), Is.True);
            var current = scene.Projected().Single();
            var projected = scene.Entities.GetComponent<CMUProjectedLightComponent>(current);
            projected.LastActiveTime = Client.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(1);
            candidates.Clear();
            scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, 3, 0.18f);
            Assert.That(scene.Projected(), Is.Empty);
        });
    }

    [Test]
    public async Task EffectiveSourcePositionIncludesRotatedOffsetAndPreservesMaskedEligibility()
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.Entities.SpawnEntity("FlashlightLantern", new MapCoordinates(new Vector2(3, 4), scene.MapIds[2]));
            var light = scene.Entities.GetComponent<PointLightComponent>(source);
            var xform = scene.Entities.GetComponent<TransformComponent>(source);
            scene.Transform.SetLocalRotation(source, Angle.FromDegrees(90));
            scene.Lights.SetOffset(source, new Vector2(2, 0));
            scene.Lights.SetEnabled(source, true, light);
            Assert.That(light.LightMask, Is.Not.Null);
            Assert.That(scene.Lighting.TryBuildSourceLight((source, light, xform), scene.MapIds[2], 0f, out var result), Is.True);
            Assert.That(Vector2.Distance(result.WorldPosition, new Vector2(3, 6)), Is.LessThan(0.001f));
            scene.Lights.SetEnabled(source, false);
            Assert.That(scene.Lighting.TryBuildSourceLight((source, light, xform), scene.MapIds[2], 0f, out _), Is.False);
        });
    }

    [Test]
    public async Task LowerReceiverSourceBucketsAreIndependentAndDoNotRetainOldMaps()
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            scene.SpawnLight(1, new Vector2(0.5f));
            scene.SpawnLight(2, new Vector2(0.5f));
            var bounds = new Box2Rotated(Box2.CenteredAround(Vector2.Zero, new Vector2(20)), Angle.Zero);
            scene.Lighting.BuildSourceLightBuckets(bounds, 0f,
                (scene.Levels[2], scene.Entities.GetComponent<CMUZLevelMapComponent>(scene.Levels[2])),
                scene.Entities.GetComponent<MapComponent>(scene.Levels[2]), 2, 32, true, false, false, Array.Empty<int>());
            Assert.That(scene.Lighting.GetSourceLightCount(scene.MapIds[1]), Is.EqualTo(1),
                "The -1 map must source the -2 receiver with upward lower-source projection disabled.");

            scene.Lighting.BuildSourceLightBuckets(bounds, 0f,
                (scene.Levels[0], scene.Entities.GetComponent<CMUZLevelMapComponent>(scene.Levels[0])),
                scene.Entities.GetComponent<MapComponent>(scene.Levels[0]), 0, 32, false, false, false, Array.Empty<int>());
            Assert.That(scene.Lighting.SourceBucketCount, Is.EqualTo(1));
            scene.Lighting.CleanupAllProjectedLights();
            Assert.That(scene.Lighting.SourceBucketCount, Is.Zero);
        });
    }

    [Test]
    public async Task CollectionUsesRealAperturesAndChecksEachCrossedFloor()
    {
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.SpawnLight(0, new Vector2(0.5f));
            var light = scene.Entities.GetComponent<PointLightComponent>(source);
            scene.Lights.SetRadius(source, 2f, light);
            var transform = scene.Entities.GetComponent<TransformComponent>(source);
            Assert.That(scene.Lighting.TryBuildSourceLight((source, light, transform), scene.MapIds[0], 0f, out var built), Is.True);
            scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], Vector2i.Zero, scene.Floor);
            scene.Maps.SetTile(scene.Levels[1], scene.Grids[1], new Vector2i(1, 0), scene.Floor);
            scene.Lighting.CollectCandidates(new() { built },
                (scene.Levels[0], scene.Entities.GetComponent<CMUZLevelMapComponent>(scene.Levels[0])), scene.MapIds[0],
                scene.Levels[2], scene.MapIds[2], EntityUid.Invalid, MapId.Nullspace, -2, 0.5f, 0.1f, 1f, 12f, 0f, 0);
            Assert.That(scene.Lighting.CollectedCandidates, Is.Not.Empty);
            foreach (var candidate in scene.Lighting.CollectedCandidates)
            {
                Assert.That(candidate.PortalGrid, Is.EqualTo(scene.Levels[2]));
                Assert.That(candidate.PortalTile, Is.Not.EqualTo(Vector2i.Zero));
                Assert.That(candidate.PortalTile, Is.Not.EqualTo(new Vector2i(1, 0)), "Solid intermediate tiles block their own column.");
                Assert.That(CMUZLevelOpeningCache.IsOpeningTile((candidate.PortalGrid, scene.Grids[2]), candidate.PortalTile, scene.Maps, scene.Tiles), Is.True);
                Assert.That(candidate.ProjectedCenter, Is.EqualTo(candidate.OpeningCenter));
            }
        });
    }
}

/// <summary>Real client maps/components with the network membership normally supplied by replicated state.</summary>
internal sealed class CMUZProjectedLightingScene : IDisposable
{
    public readonly IEntityManager Entities;
    public readonly SharedMapSystem Maps;
    public readonly SharedTransformSystem Transform;
    public readonly SharedPointLightSystem Lights;
    public readonly CMUZLevelProjectedLightingSystem Lighting;
    public readonly ITileDefinitionManager Tiles;
    public readonly EntityUid[] Levels = new EntityUid[3];
    public readonly MapId[] MapIds = new MapId[3];
    public readonly MapGridComponent[] Grids = new MapGridComponent[3];
    public readonly EntityUid Network;
    public readonly Tile Floor;

    public CMUZProjectedLightingScene(IEntityManager entities, ITileDefinitionManager tiles)
    {
        Entities = entities;
        Maps = entities.System<SharedMapSystem>();
        Transform = entities.System<SharedTransformSystem>();
        Lights = entities.System<SharedPointLightSystem>();
        Lighting = entities.System<CMUZLevelProjectedLightingSystem>();
        Lighting.CleanupAllProjectedLights();
        Tiles = tiles;
        Floor = new Tile(tiles["Plating"].TileId);
        Network = entities.SpawnEntity(null, MapCoordinates.Nullspace);
        var network = entities.AddComponent<CMUZLevelsNetworkComponent>(Network);
        for (var depth = 0; depth < Levels.Length; depth++)
        {
            var map = Maps.CreateMap(out MapIds[depth], runMapInit: true);
            Levels[depth] = map;
            Grids[depth] = entities.EnsureComponent<MapGridComponent>(map);
            Maps.SetTile(map, Grids[depth], new Vector2i(8, 8), Floor);
            var zMap = entities.AddComponent<CMUZLevelMapComponent>(map);
            zMap.NetworkUid = Network;
            zMap.Depth = depth;
            // Install the topology snapshot a client normally receives from the server.
#pragma warning disable RA0002
            network.ZLevels.Add(depth, map);
            network.ZLevelByEntity.Add(map, depth);
#pragma warning restore RA0002
        }
    }

    public EntityUid SpawnLight(int depth, Vector2 position)
    {
        var uid = Entities.SpawnEntity(null, new MapCoordinates(position, MapIds[depth]));
        Entities.AddComponent<PointLightComponent>(uid);
        Lights.SetRadius(uid, 16f);
        return uid;
    }

    public Candidate Candidate(EntityUid source, int sourceDepth, int receivingDepth, EntityUid grid, Vector2i tile, Vector2 center, float energy = 1f) =>
        new(source, MapIds[sourceDepth], MapIds[receivingDepth], sourceDepth - receivingDepth,
            grid, tile, center, center, 4f, energy, Color.White, 1f, 6.8f, 0f);

    public List<EntityUid> Projected()
    {
        var result = new List<EntityUid>();
        var query = Entities.EntityQueryEnumerator<CMUProjectedLightComponent>();
        while (query.MoveNext(out var uid, out _))
            result.Add(uid);
        return result;
    }

    public void Dispose()
    {
        Lighting.CleanupAllProjectedLights();
        foreach (var map in Levels)
            if (!Entities.Deleted(map)) Entities.DeleteEntity(map);
        if (!Entities.Deleted(Network)) Entities.DeleteEntity(Network);
    }
}
