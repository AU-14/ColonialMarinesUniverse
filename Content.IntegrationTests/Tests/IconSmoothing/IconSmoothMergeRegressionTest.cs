using System.Numerics;
using Content.Client.IconSmoothing;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.IconSmoothing;
using Content.Shared.Sprite;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.IconSmoothing;

[TestFixture]
[TestOf(typeof(IconSmoothSystem))]
public sealed class IconSmoothMergeRegressionTest : GameTest
{
    private static readonly EntProtoId SmoothSource = "IconSmoothMergeSmoothSource";
    private static readonly EntProtoId SmoothCandidate = "IconSmoothMergeSmoothCandidate";
    private static readonly EntProtoId UnsmoothedSource = "IconSmoothMergeUnsmoothedSource";
    private static readonly EntProtoId UnsmoothedCandidate = "IconSmoothMergeUnsmoothedCandidate";
    private static readonly EntProtoId OrdinarySource = "IconSmoothMergeOrdinarySource";
    private static readonly EntProtoId OrdinaryCandidate = "IconSmoothMergeOrdinaryCandidate";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IconSmoothMergeBase
  abstract: true
  components:
  - type: Transform
    anchored: true
  - type: Sprite
    sprite: _RMC14/Structures/Xenos/xeno_weeds.rsi
    layers:
    - state: weed_dir0
  - type: IconSmooth
    key: merge_base
    base: weed_dir
    mode: CardinalFlags

- type: entity
  id: IconSmoothMergeSmoothSource
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_smooth_source
  - type: CMIconSmooth
    smooth: true

- type: entity
  id: IconSmoothMergeSmoothCandidate
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_smooth_candidate
  - type: CMIconSmooth

- type: entity
  id: IconSmoothMergeUnsmoothedSource
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_unsmoothed_source
  - type: CMIconSmooth
    smooth: false

- type: entity
  id: IconSmoothMergeUnsmoothedCandidate
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_unsmoothed_candidate
  - type: CMIconSmooth

- type: entity
  id: IconSmoothMergeOrdinarySource
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_ordinary_source

- type: entity
  id: IconSmoothMergeOrdinaryCandidate
  parent: IconSmoothMergeBase
  components:
  - type: IconSmooth
    key: merge_ordinary_candidate
";

    [Test]
    public async Task RmcCrossKeySmoothingAndRandomOverrideCompose()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var serverEntities = server.EntMan;
        var clientEntities = client.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid smoothSource = default;
        EntityUid smoothCandidate = default;
        EntityUid unsmoothedSource = default;
        EntityUid ordinarySource = default;
        EntityUid randomWeeds = default;
        var spawned = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var mapSystem = serverEntities.System<SharedMapSystem>();
            for (var x = -1; x <= 22; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            }

            (smoothSource, smoothCandidate) = SpawnPair(SmoothSource, SmoothCandidate, 0);
            (unsmoothedSource, _) = SpawnPair(UnsmoothedSource, UnsmoothedCandidate, 5);
            (ordinarySource, _) = SpawnPair(OrdinarySource, OrdinaryCandidate, 10);

            randomWeeds = Spawn("XenoWeeds", 20, 0);
            Spawn("XenoWeeds", 21, 0);
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientSmoothSource = ClientUid(smoothSource);
            var clientSmoothCandidate = ClientUid(smoothCandidate);
            Assert.Multiple(() =>
            {
                Assert.That(clientEntities.GetComponent<CMIconSmoothComponent>(clientSmoothSource).Smooth, Is.True);
                Assert.That(clientEntities.HasComponent<CMIconSmoothComponent>(clientSmoothCandidate), Is.True);
                Assert.That(clientEntities.GetComponent<TransformComponent>(clientSmoothSource).Anchored, Is.True);
                Assert.That(clientEntities.GetComponent<TransformComponent>(clientSmoothCandidate).Anchored, Is.True);
            });
            Assert.That(State(clientEntities, ClientUid(smoothSource)), Is.EqualTo("weed_dir4"),
                "Smooth=true permits the source to connect to an RMC candidate with a different key");
            Assert.That(State(clientEntities, ClientUid(unsmoothedSource)), Is.EqualTo("weed_dir0"),
                "Smooth=false retains ordinary key matching even when both entities have CMIconSmooth");
            Assert.That(State(clientEntities, ClientUid(ordinarySource)), Is.EqualTo("weed_dir0"),
                "ordinary IconSmooth entities with different keys do not connect");
            Assert.That(State(clientEntities, ClientUid(randomWeeds)), Is.EqualTo("weed_dir4"),
                "a non-overridden smoothing state remains visible before the full-neighbor transition");
        });

        await server.WaitPost(() =>
        {
            Spawn("XenoWeeds", 19, 0);
            Spawn("XenoWeeds", 20, 1);
            Spawn("XenoWeeds", 20, -1);
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var uid = ClientUid(randomWeeds);
            var random = clientEntities.GetComponent<RandomSpriteComponent>(uid);
            Assert.That(random.Selected.TryGetValue("0", out var selected), Is.True,
                "the live weed prototype selects its random base layer");
            Assert.That(State(clientEntities, uid), Is.EqualTo(selected.State),
                "IconSmoothingUpdatedEvent lets IconSmoothRandom replace the fully-connected weed_dir15 state");
            Assert.That(selected.State, Does.StartWith("weed"));
            Assert.That(selected.State, Does.Not.StartWith("weed_dir"));
        });

        await server.WaitPost(() =>
        {
            foreach (var uid in spawned)
                serverEntities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
        return;

        (EntityUid Source, EntityUid Candidate) SpawnPair(EntProtoId source, EntProtoId candidate, int x)
        {
            var uid = Spawn(source, x, 0);
            var candidateUid = Spawn(candidate, x + 1, 0);
            return (uid, candidateUid);
        }

        EntityUid Spawn(EntProtoId prototype, int x, int y)
        {
            var uid = serverEntities.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(x, y)));
            spawned.Add(uid);
            return uid;
        }

        EntityUid ClientUid(EntityUid serverUid)
        {
            return clientEntities.GetEntity(serverEntities.GetNetEntity(serverUid));
        }
    }

    private static string? State(IEntityManager entities, EntityUid uid)
    {
        var sprite = entities.GetComponent<SpriteComponent>(uid);
        return entities.System<SpriteSystem>().LayerGetRsiState((uid, sprite), 0).Name;
    }
}
