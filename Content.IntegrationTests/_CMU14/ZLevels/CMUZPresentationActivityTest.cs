using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZPresentationActivityTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestZPresentationSprite
          components:
          - type: Sprite
            sprite: Mobs/Ghosts/ghost_human.rsi
            state: animated
          - type: CMUZPhysics

        - type: entity
          id: CMUTestZPresentationHeight
          components:
          - type: CMUZPhysics
            localPosition: 0.5
        """;

    [Test]
    public async Task GroundedPopulationDoesNotEnterIndexAndLocalChangesNeedNoFrameDiscovery()
    {
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var baseline = z.PresentationCandidateCount;
            var entities = new List<EntityUid>();
            try
            {
                for (var i = 0; i < 256; i++)
                    entities.Add(CEntMan.SpawnEntity("CMUTestZPresentationSprite", MapCoordinates.Nullspace));

                z.FrameUpdate(0f);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
                var uid = entities[0];
                z.SetZLocalPosition(uid, 0.5f);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline + 1));
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));

                z.SetZLocalPosition(uid, 0f);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
                AssertRenderedOffset(uid, Vector2.Zero);
            }
            finally
            {
                foreach (var uid in entities)
                    CEntMan.DeleteEntity(uid);
            }
            Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
        });
    }

    [Test]
    public async Task HeightBeforeSpriteAndSpriteReplacementRemainTrackedWithoutDiscovery()
    {
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var baseline = z.PresentationCandidateCount;
            var uid = CEntMan.SpawnEntity("CMUTestZPresentationHeight", MapCoordinates.Nullspace);
            try
            {
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline + 1));
                CEntMan.AddComponent<SpriteComponent>(uid);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));
                CEntMan.RemoveComponent<SpriteComponent>(uid);
                CEntMan.AddComponent<SpriteComponent>(uid);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));

                CEntMan.RemoveComponent<SpriteComponent>(uid);
                CEntMan.RemoveComponent<CMUZPhysicsComponent>(uid);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline),
                    "Removing physics must clean its reason even after the sprite has gone.");
            }
            finally
            {
                CEntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task RemovingOneEffectKeepsOtherReasonsAndDeletionReleasesTheEntry()
    {
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var baseline = z.PresentationCandidateCount;
            var target = CEntMan.SpawnEntity("CMUTestZPresentationHeight", MapCoordinates.Nullspace);
            var uid = CEntMan.SpawnEntity("CMUTestZPresentationSprite", MapCoordinates.Nullspace);
            try
            {
                z.SetZLocalPosition(uid, 0.5f);
                var projectile = CEntMan.AddComponent<CMUZLevelProjectileVisualOffsetComponent>(uid);
                projectile.Offset = new Vector2(0f, 0.5f);
                var predicted = CEntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                predicted.Offset = new Vector2(0f, 0.25f);
                var follower = CEntMan.AddComponent<CMUZVisualFollowerComponent>(uid);
                follower.Target = target;
                CEntMan.GetComponent<SpriteComponent>(uid).NoRotation = true;
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline + 2));
                AssertRenderedOffset(uid, new Vector2(0f, 1f));

                CEntMan.RemoveComponent<CMUZPhysicsComponent>(uid);
                AssertRenderedOffset(uid, new Vector2(0f, 0.625f));
                CEntMan.RemoveComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                AssertRenderedOffset(uid, new Vector2(0f, 0.875f));
                CEntMan.RemoveComponent<CMUZLevelProjectileVisualOffsetComponent>(uid);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));
                CEntMan.RemoveComponent<CMUZVisualFollowerComponent>(uid);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline + 1));
                AssertRenderedOffset(uid, Vector2.Zero);

                CEntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                CEntMan.DeleteEntity(uid);
                CEntMan.DeleteEntity(target);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
            }
            finally
            {
                if (CEntMan.EntityExists(uid))
                    CEntMan.DeleteEntity(uid);
                if (CEntMan.EntityExists(target))
                    CEntMan.DeleteEntity(target);
            }
        });
    }

    [Test]
    public async Task FollowerUsesCurrentTargetHeightAndDropsDeletedTargetWithoutDiscovery()
    {
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var baseline = z.PresentationCandidateCount;
            var first = CEntMan.SpawnEntity("CMUTestZPresentationHeight", MapCoordinates.Nullspace);
            var second = CEntMan.SpawnEntity("CMUTestZPresentationHeight", MapCoordinates.Nullspace);
            var uid = CEntMan.SpawnEntity("CMUTestZPresentationSprite", MapCoordinates.Nullspace);
            try
            {
                var follower = CEntMan.AddComponent<CMUZVisualFollowerComponent>(uid);
                follower.Target = first;
                CEntMan.GetComponent<SpriteComponent>(uid).NoRotation = true;
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));
                z.SetZLocalPosition(first, 0f);
                AssertRenderedOffset(uid, Vector2.Zero);
                z.SetZLocalPosition(second, 0.25f);
                follower.Target = second;
                AssertRenderedOffset(uid, new Vector2(0f, 0.1875f));
                CEntMan.DeleteEntity(second);
                AssertRenderedOffset(uid, Vector2.Zero);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline + 1));
            }
            finally
            {
                foreach (var entity in new[] { uid, first, second })
                    if (CEntMan.EntityExists(entity))
                        CEntMan.DeleteEntity(entity);
            }
            Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
        });
    }

    [Test]
    public async Task MovingBetweenMapsUsesCurrentViewportAndDeletingMapClearsActivity()
    {
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var baseline = z.PresentationCandidateCount;
            var maps = Client.System<SharedMapSystem>();
            var firstMap = maps.CreateMap(out var firstId, runMapInit: true);
            var secondMap = maps.CreateMap(out var secondId, runMapInit: true);
            var uid = CEntMan.SpawnEntity("CMUTestZPresentationSprite", new EntityCoordinates(firstMap, Vector2.Zero));
            try
            {
                z.SetZLocalPosition(uid, 0.5f);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f), firstId);
                Client.System<SharedTransformSystem>().SetMapCoordinates(uid, new MapCoordinates(Vector2.Zero, secondId));
                AssertRenderedOffset(uid, Vector2.Zero, firstId);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f), secondId);
                CEntMan.DeleteEntity(secondMap);
                Assert.That(CEntMan.EntityExists(uid), Is.False);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(baseline));
            }
            finally
            {
                foreach (var entity in new[] { uid, firstMap, secondMap })
                    if (CEntMan.EntityExists(entity))
                        CEntMan.DeleteEntity(entity);
            }
        });
    }

    [Test]
    public async Task ReplicationAndTinyLocalGroundSnapUpdateMembership()
    {
        EntityUid map = default;
        EntityUid network = default;
        EntityUid uid = default;
        NetEntity netUid = default;
        EntityUid? originalAttached = null;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                map = maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                maps.SetTile(map, grid, Vector2i.Zero, floor);
                var z = Server.System<CMUZLevelsSystem>();
                var networkEntity = z.CreateZNetwork();
                network = networkEntity;
                Assert.That(z.TryAddMapsIntoZNetwork(networkEntity, new() { [map] = 0 }), Is.True);
                uid = SEntMan.SpawnEntity("CMUTestZPresentationSprite", new EntityCoordinates(map, new Vector2(0.5f)));
                SEntMan.EnsureComponent<EyeComponent>(uid);
                originalAttached = ServerSession!.AttachedEntity;
                Server.PlayerMan.SetAttachedEntity(ServerSession, uid);
                netUid = SEntMan.GetNetEntity(uid);
                z.SetZLocalPosition(uid, 0.5f);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => AssertRenderedOffset(CEntMan.GetEntity(netUid), new Vector2(0f, 0.375f)));

            await Server.WaitAssertion(() => Server.System<CMUZLevelsSystem>().SetZLocalPosition(uid, 0f));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var clientUid = CEntMan.GetEntity(netUid);
                var z = Client.System<CMUClientZLevelsSystem>();
                var groundedCount = z.PresentationCandidateCount;
                AssertRenderedOffset(clientUid, Vector2.Zero);
                z.SetZLocalPosition(clientUid, 0.025f);
                z.SetZLocalPosition(clientUid, 0.005f);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(groundedCount + 1));

                // Exercise the actual MoveEvent ground-snap owner. This change is smaller than
                // the replication dirty threshold, but must remove the presentation entry now.
                var xform = CEntMan.GetComponent<TransformComponent>(clientUid);
                Client.System<SharedTransformSystem>().SetCoordinates(clientUid,
                    new EntityCoordinates(xform.ParentUid, xform.LocalPosition + new Vector2(0.1f, 0f)));
                Assert.That(CEntMan.GetComponent<CMUZPhysicsComponent>(clientUid).LocalPosition, Is.EqualTo(0f));
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(groundedCount));
                AssertRenderedOffset(clientUid, Vector2.Zero);
            });

            await Server.WaitAssertion(() => Server.System<CMUZLevelsSystem>().SetZLocalPosition(uid, 0.25f));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => AssertRenderedOffset(CEntMan.GetEntity(netUid), new Vector2(0f, 0.1875f)));
            await Server.WaitAssertion(() => SEntMan.RemoveComponent<CMUZPhysicsComponent>(uid));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => AssertRenderedOffset(CEntMan.GetEntity(netUid), Vector2.Zero));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession!, originalAttached);
                foreach (var entity in new[] { uid, network, map })
                    if (SEntMan.EntityExists(entity))
                        SEntMan.DeleteEntity(entity);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task EffectsChangedWhileDisabledAreReadyOnEnableWithoutDiscovery()
    {
        EntityUid uid = default;
        var originallyEnabled = true;
        try
        {
            await Server.WaitAssertion(() =>
            {
                originallyEnabled = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, false);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.CfgMan.GetCVar(CMUZLevelsCVars.Enabled), Is.False);
                uid = CEntMan.SpawnEntity("CMUTestZPresentationSprite", MapCoordinates.Nullspace);
                Client.System<CMUClientZLevelsSystem>().SetZLocalPosition(uid, 0.5f);
                AssertRenderedOffset(uid, Vector2.Zero);
            });
            await Server.WaitAssertion(() => Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.CfgMan.GetCVar(CMUZLevelsCVars.Enabled), Is.True);
                AssertRenderedOffset(uid, new Vector2(0f, 0.375f));
            });
        }
        finally
        {
            await Server.WaitAssertion(() => Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, originallyEnabled));
            await Pair.RunTicksSync(5);
            await Client.WaitPost(() =>
            {
                if (CEntMan.EntityExists(uid))
                    CEntMan.DeleteEntity(uid);
            });
        }
    }

    private void AssertRenderedOffset(EntityUid uid, Vector2 expected, MapId? mapId = null)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
        var initial = sprite.Offset;
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupProperty(v => v.Eye, new Eye
        {
            Position = new MapCoordinates(Vector2.Zero, mapId ?? CEntMan.GetComponent<TransformComponent>(uid).MapID),
        });
        viewport.Setup(v => v.Render()).Callback(() =>
            Assert.That(Vector2.Distance(sprite.Offset, initial + expected), Is.LessThan(0.00001f)));
        Client.System<CMUClientZLevelsSystem>().RenderViewport(viewport.Object);
        Assert.That(sprite.Offset, Is.EqualTo(initial));
    }
}
