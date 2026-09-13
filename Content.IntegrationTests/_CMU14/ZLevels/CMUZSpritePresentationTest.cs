using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Moq;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZSpritePresentationTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task GroundedSpritesKeepCurrentPresentation(bool snapCardinals)
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var uid = Pair.Client.EntMan.Spawn("MobObserver");
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Pair.Client.System<SpriteSystem>();
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                sprites.SetSnapCardinals((uid, sprite), snapCardinals);
                sprites.SetOffset((uid, sprite), new Vector2(0.2f, 0.3f));
                sprites.SetDrawDepth((uid, sprite), (int) DrawDepth.BelowMobs);
                sprite.NoRotation = false;
                zLevels.SetZLocalPosition(uid, 0f);

                zLevels.Update(0f);
                zLevels.FrameUpdate(0f);
                var viewport = ViewportFor(uid);
                viewport.Setup(v => v.Render()).Callback(() => AssertPresentation(sprite, false, new Vector2(0.2f, 0.3f), DrawDepth.BelowMobs));
                zLevels.RenderViewport(viewport.Object);
                AssertPresentation(sprite, false, new Vector2(0.2f, 0.3f), DrawDepth.BelowMobs);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(uid);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AirbornePresentationRestoresLatestStateAfterRender(bool fail)
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var uid = Pair.Client.EntMan.Spawn("MobObserver");
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Pair.Client.System<SpriteSystem>();
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                zLevels.SetZLocalPosition(uid, 0.5f);
                zLevels.FrameUpdate(0f);

                // A posture/appearance owner changes presentation after discovery but before this pass.
                var offset = new Vector2(0.2f, 0.3f);
                sprites.SetOffset((uid, sprite), offset);
                sprites.SetDrawDepth((uid, sprite), (int) DrawDepth.BelowMobs);
                sprite.NoRotation = false;
                var viewport = ViewportFor(uid);
                viewport.Setup(v => v.Render()).Callback(() =>
                {
                    AssertPresentation(sprite, true, offset + new Vector2(0f, 0.375f), DrawDepth.OverMobs);
                    if (fail)
                        throw new InvalidOperationException("Injected render failure");
                });

                if (fail)
                    Assert.Throws<InvalidOperationException>(() => zLevels.RenderViewport(viewport.Object));
                else
                    zLevels.RenderViewport(viewport.Object);

                AssertPresentation(sprite, false, offset, DrawDepth.BelowMobs);
                zLevels.SetZLocalPosition(uid, 0f);
                viewport.Setup(v => v.Render()).Callback(() => AssertPresentation(sprite, false, offset, DrawDepth.BelowMobs));
                zLevels.RenderViewport(viewport.Object);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task ProjectileOffsetUsesEachViewportEyeAndRestoresBetweenCameras()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var uid = Pair.Client.EntMan.Spawn("MobObserver");
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var visual = Pair.Client.EntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                visual.Offset = new Vector2(0f, 0.75f);
                var offset = sprite.Offset;
                sprite.NoRotation = true;
                zLevels.FrameUpdate(0f);

                var first = ViewportFor(uid);
                first.Setup(v => v.Render()).Callback(() => Assert.That(sprite.Offset, Is.EqualTo(offset + visual.Offset)));
                zLevels.RenderViewport(first.Object);
                Assert.That(sprite.Offset, Is.EqualTo(offset));

                var second = ViewportFor(uid);
                second.Object.Eye!.Rotation = Angle.FromDegrees(90);
                second.Setup(v => v.Render()).Callback(() =>
                {
                    Assert.That(Vector2.Distance(sprite.Offset, offset + new Vector2(-0.75f, 0f)), Is.LessThan(0.0001f));
                });
                zLevels.RenderViewport(second.Object);
                Assert.That(sprite.Offset, Is.EqualTo(offset));

                Pair.Client.EntMan.RemoveComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                Assert.That(sprite.Offset, Is.EqualTo(offset));
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task PresentationMovesSpriteTreeBoundsOnlyForItsRenderPass()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var maps = Pair.Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var uid = Pair.Client.EntMan.SpawnEntity("MobObserver", new EntityCoordinates(map, Vector2.Zero));
            try
            {
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Pair.Client.System<SpriteSystem>();
                var tree = Pair.Client.System<SpriteTreeSystem>();
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var visual = Pair.Client.EntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid);
                visual.Offset = new Vector2(0f, 8f);
                sprite.NoRotation = true;
                var baseBounds = sprites.CalculateBounds((uid, sprite), Vector2.Zero, Angle.Zero, Angle.Zero).CalcBoundingBox();
                var elevatedBounds = baseBounds.Translated(visual.Offset);
                var found = new HashSet<Entity<SpriteComponent, TransformComponent>>();

                bool QueryContains(Box2 bounds)
                {
                    found.Clear();
                    tree.QueryAabb(found, mapId, bounds, approx: false);
                    return found.Any(entity => entity.Owner == uid);
                }

                Assert.That(QueryContains(baseBounds), Is.True);
                Assert.That(QueryContains(elevatedBounds), Is.False);
                zLevels.FrameUpdate(0f);
                var viewport = ViewportFor(uid);
                viewport.Setup(v => v.Render()).Callback(() =>
                {
                    Assert.That(QueryContains(elevatedBounds), Is.True);
                    Assert.That(QueryContains(baseBounds), Is.False);
                });
                zLevels.RenderViewport(viewport.Object);
                Assert.That(QueryContains(baseBounds), Is.True);
                Assert.That(QueryContains(elevatedBounds), Is.False);
            }
            finally
            {
                Pair.Client.EntMan.DeleteEntity(uid);
                Pair.Client.EntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task DisablingZLevelsDuringFlightLeavesNoSpriteModifier()
    {
        EntityUid uid = default;
        var offset = Vector2.Zero;
        var enabled = true;
        await Server.WaitAssertion(() => enabled = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled));
        try
        {
            await Pair.Client.WaitAssertion(() =>
            {
                uid = Pair.Client.EntMan.Spawn("MobObserver");
                var zLevels = Pair.Client.System<CMUClientZLevelsSystem>();
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                offset = sprite.Offset;
                zLevels.SetZLocalPosition(uid, 0.5f);
                zLevels.FrameUpdate(0f);
                zLevels.RenderViewport(ViewportFor(uid).Object);
            });
            await Server.WaitAssertion(() => Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, false));
            await Pair.RunTicksSync(5);
            await Pair.Client.WaitAssertion(() =>
            {
                Assert.That(Pair.Client.CfgMan.GetCVar(CMUZLevelsCVars.Enabled), Is.False);
                var sprite = Pair.Client.EntMan.GetComponent<SpriteComponent>(uid);
                var viewport = ViewportFor(uid);
                viewport.Setup(v => v.Render()).Callback(() => Assert.That(sprite.Offset, Is.EqualTo(offset)));
                Pair.Client.System<CMUClientZLevelsSystem>().RenderViewport(viewport.Object);
                Assert.That(sprite.Offset, Is.EqualTo(offset));
            });
        }
        finally
        {
            await Server.WaitAssertion(() => Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, enabled));
            await Pair.RunTicksSync(5);
            await Pair.Client.WaitAssertion(() =>
            {
                if (Pair.Client.EntMan.EntityExists(uid))
                    Pair.Client.EntMan.DeleteEntity(uid);
            });
        }
    }
    private Mock<IClydeViewport> ViewportFor(EntityUid uid)
    {
        var xform = Pair.Client.EntMan.GetComponent<TransformComponent>(uid);
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupProperty(v => v.Eye, new Eye { Position = new MapCoordinates(Vector2.Zero, xform.MapID) });
        return viewport;
    }

    private static void AssertPresentation(SpriteComponent sprite, bool noRotation, Vector2 offset, DrawDepth depth)
    {
        Assert.Multiple(() =>
        {
            Assert.That(sprite.NoRotation, Is.EqualTo(noRotation));
            Assert.That(sprite.Offset, Is.EqualTo(offset));
            Assert.That(sprite.DrawDepth, Is.EqualTo((int) depth));
        });
    }
}
