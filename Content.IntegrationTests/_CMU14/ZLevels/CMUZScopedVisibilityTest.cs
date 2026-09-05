using Content.Client.CMU14.ZLevels.Core;
using Content.Client.CMU14.ZLevels.Culling;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Moq;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZScopedVisibilityTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CullingRestoresVisibilityAndTreeBeforeAnotherCamera(bool fail)
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var maps = Pair.Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var uid = Pair.Client.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var tree = Pair.Client.System<SpriteTreeSystem>();
                var found = new HashSet<Entity<SpriteComponent, TransformComponent>>();
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var bounds = new Box2(-10f, -10f, 10f, 10f);
                var mask = new CMUZVisibilityMask();
                mask.SetOpenings(mapId, bounds, new[] { new Box2(5f, 5f, 6f, 6f) }, complete: true);

                bool InTree()
                {
                    found.Clear();
                    tree.QueryAabb(found, mapId, bounds, approx: false);
                    return found.Any(entity => entity.Owner == uid);
                }

                Assert.That(InTree(), Is.True);
                var upperView = Viewport(mapId);
                upperView.Setup(view => view.Render()).Callback(() =>
                {
                    Assert.That(sprite.Visible, Is.False);
                    Assert.That(InTree(), Is.False);
                    if (fail)
                        throw new InvalidOperationException("Injected render failure");
                });
                if (fail)
                    Assert.Throws<InvalidOperationException>(() => zLevels.RenderViewport(upperView.Object, mask));
                else
                    zLevels.RenderViewport(upperView.Object, mask);

                Assert.Multiple(() =>
                {
                    Assert.That(sprite.Visible, Is.True);
                    Assert.That(InTree(), Is.True);
                });
                var directCamera = Viewport(mapId);
                directCamera.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.True));
                zLevels.RenderViewport(directCamera.Object);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task SemanticVisibilityChangesBetweenRendersRemainOwnedByAppearance()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var maps = Pair.Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var uid = Pair.Client.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            try
            {
                var sprites = Pair.Client.System<SpriteSystem>();
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var mask = new CMUZVisibilityMask();
                mask.SetOpenings(mapId, new Box2(-10f, -10f, 10f, 10f), Array.Empty<Box2>(), complete: true);
                var viewport = Viewport(mapId);
                viewport.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.False));

                zLevels.RenderViewport(viewport.Object, mask);
                Assert.That(sprite.Visible, Is.True);
                sprites.SetVisible((uid, sprite), false);
                zLevels.RenderViewport(viewport.Object, mask);
                Assert.That(sprite.Visible, Is.False);
                zLevels.RenderViewport(viewport.Object);
                Assert.That(sprite.Visible, Is.False);

                sprites.SetVisible((uid, sprite), true);
                viewport.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.True));
                mask.SetOpenings(mapId, new Box2(-10f, -10f, 10f, 10f), Array.Empty<Box2>(), complete: false);
                zLevels.RenderViewport(viewport.Object, mask);
                Assert.That(sprite.Visible, Is.True);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task CullingUsesPresentedBoundsAndActualCameraRotation()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var maps = Pair.Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var uid = Pair.Client.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            try
            {
                var sprites = Pair.Client.System<SpriteSystem>();
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var visual = Pair.Client.EntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                visual.Offset = new Vector2(0f, 8f);
                sprite.NoRotation = true;
                sprites.SetOffset((uid, sprite), new Vector2(4f, 0f));
                Pair.Client.System<SpriteTreeSystem>().QueueTreeUpdate(uid, sprite);
                zLevels.FrameUpdate(0f);
                var viewport = Viewport(mapId);
                viewport.Object.Eye!.Rotation = Angle.FromDegrees(90);
                // NoRotation rotates the appearance offset to (0,-4), and presentation contributes
                // the world-space projectile offset (0,8), so actual bounds are centered at (0,4).
                var mask = new CMUZVisibilityMask();
                mask.SetOpenings(mapId, new Box2(-20f, -20f, 20f, 20f),
                    new[] { new Box2(-1f, 3f, 1f, 5f) }, complete: true);
                viewport.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.True));
                zLevels.RenderViewport(viewport.Object, mask);

                mask.SetOpenings(mapId, new Box2(-20f, -20f, 20f, 20f),
                    new[] { new Box2(3f, -1f, 5f, 1f) }, complete: true);
                viewport.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.False));
                zLevels.RenderViewport(viewport.Object, mask);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite.Visible, Is.True);
                    Assert.That(sprite.Offset, Is.EqualTo(new Vector2(4f, 0f)));
                });
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task DeletedCandidateAndMismatchedMapLeaveNoCullingState()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var maps = Pair.Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var uid = Pair.Client.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var mask = new CMUZVisibilityMask();
                mask.SetOpenings(new MapId((int) mapId + 1000), new Box2(-10f, -10f, 10f, 10f),
                    Array.Empty<Box2>(), complete: true);
                var viewport = Viewport(mapId);
                viewport.Setup(view => view.Render()).Callback(() => Assert.That(sprite.Visible, Is.True));
                zLevels.RenderViewport(viewport.Object, mask);

                mask.SetOpenings(mapId, new Box2(-10f, -10f, 10f, 10f), Array.Empty<Box2>(), complete: true);
                viewport.Setup(view => view.Render()).Callback(() =>
                {
                    Assert.That(sprite.Visible, Is.False);
                    Pair.Client.EntMan.DeleteEntity(uid);
                });
                zLevels.RenderViewport(viewport.Object, mask);
                Assert.That(Pair.Client.EntMan.EntityExists(uid), Is.False);
                viewport.Setup(view => view.Render());
                Assert.DoesNotThrow(() => zLevels.RenderViewport(viewport.Object, mask));
                Assert.That(Pair.Client.System<CMUZLevelSpriteCullingSystem>().LastHidden, Is.Zero);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(map);
            }
        });
    }

    private static Mock<IClydeViewport> Viewport(MapId mapId)
    {
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupProperty(view => view.Eye, new Eye { Position = new MapCoordinates(Vector2.Zero, mapId) });
        return viewport;
    }
}
