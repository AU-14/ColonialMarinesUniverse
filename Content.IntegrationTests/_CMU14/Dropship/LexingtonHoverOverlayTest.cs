using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Client.CMU14.Dropship.TacticalLand;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Maps;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using PointLightComponent = Robust.Client.GameObjects.PointLightComponent;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class LexingtonHoverOverlayTest : GameTest
{
    [Test]
    public async Task WorldPassBuildsHullWithScreenHandleAndContinuesDrawingJets()
    {
        await Client.WaitAssertion(() =>
        {
            var mapUid = Client.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            var overlay = new DropshipTacticalHoverOverlay(CEntMan);
            Assert.That(overlay.Space, Is.EqualTo(OverlaySpace.WorldSpaceEntities),
                "The hover effects must be composited among sprites so floor weeds cannot cover them.");
            Assert.That(overlay.ZIndex, Is.GreaterThan((int) Content.Shared.DrawDepth.DrawDepth.HighFloorObjects));
            Assert.That(overlay.ZIndex, Is.LessThan((int) Content.Shared.DrawDepth.DrawDepth.Mobs));
            var white = new TestTexture(Vector2i.One);
            var world = new Mock<DrawingHandleWorld>(MockBehavior.Loose, white);
            var screen = new Mock<DrawingHandleScreen>(MockBehavior.Loose, white);
            var render = new Mock<IRenderHandle>();
            var viewport = new Mock<IClydeViewport>();
            var clyde = new Mock<IClyde>();
            var eye = new Mock<IEye>();
            var eyes = new Mock<IEyeManager>();
            eyes.SetupGet(e => e.CurrentEye).Returns(eye.Object);
            viewport.SetupGet(v => v.Eye).Returns(eye.Object);
            typeof(DropshipTacticalHoverOverlay).GetField("_eye", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(overlay, eyes.Object);
            var targets = new List<Mock<IRenderTexture>>();
            render.SetupGet(r => r.DrawingHandleWorld).Returns(world.Object);
            render.SetupGet(r => r.DrawingHandleScreen).Returns(screen.Object);
            screen.Setup(s => s.RenderInRenderTarget(It.IsAny<IRenderTarget>(), It.IsAny<Action>(), Color.Transparent))
                .Callback<IRenderTarget, Action, Color?>((_, draw, _) => draw());
            clyde.Setup(c => c.CreateRenderTarget(It.IsAny<Vector2i>(),
                    It.IsAny<RenderTargetFormatParameters>(), It.IsAny<TextureSampleParameters?>(), It.IsAny<string>()))
                .Returns<Vector2i, RenderTargetFormatParameters, TextureSampleParameters?, string>((size, _, _, _) =>
                {
                    var target = new Mock<IRenderTexture>();
                    target.SetupGet(t => t.Size).Returns(size);
                    target.SetupGet(t => t.Texture).Returns(new TestTexture(size));
                    targets.Add(target);
                    return target.Object;
                });
            typeof(DropshipTacticalHoverOverlay).GetField("_clyde", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(overlay, clyde.Object);

            try
            {
                var shadowUid = CEntMan.SpawnEntity(null, new EntityCoordinates(mapUid, Vector2.Zero));
                var shadow = CEntMan.AddComponent<DropshipTacticalHoverShadowComponent>(shadowUid);
                shadow.HullBounds = new Box2(-4, -4, 4, 10);
                var floor = (ContentTileDefinition) Client.ResolveDependency<ITileDefinitionManager>()["CMFloorPlating"];
                shadow.Tiles.Add(new DropshipShadowTile(new Vector2(.5f), floor.Sprite!.Value.ToString(), 0));
                shadow.Parts.Add(new DropshipShadowPart("CMNormandyWall100", new Vector2(-.5f, 8.5f), Angle.Zero));
                var jets = new List<EntityUid>();
                foreach (var offset in new[] { new Vector2(-2.5f, -1), new Vector2(3.5f, -1), new Vector2(.5f, 7.5f) })
                {
                    var uid = CEntMan.SpawnEntity("CMUGunshipTacticalHoverDownwash", new EntityCoordinates(mapUid, offset));
                    jets.Add(uid);
                    var jet = CEntMan.GetComponent<DropshipTacticalHoverDownwashComponent>(uid);
                    jet.Offset = offset;
                    jet.ManeuverThrust = GunshipHoverExhaust.NozzleThrust(Vector2.UnitY, 1f, offset - shadow.HullBounds.Center);
                }

                var bounds = new Box2(-20, -20, 20, 20);
                var args = CreateDrawArgs(overlay.Space, null, viewport.Object, render.Object,
                    new UIBox2i(0, 0, 640, 640), mapUid, mapId, bounds, new Box2Rotated(bounds, Angle.Zero));
                Assert.That(args.DrawingHandle, Is.SameAs(world.Object),
                    "The engine supplies a world handle for this pass; ScreenHandle would throw.");

                DrawOverlay(overlay, in args);
                Assert.Multiple(() =>
                {
                    Assert.That(targets, Has.Count.EqualTo(1));
                    Assert.That(screen.Invocations.Count(i => i.Method.Name == "DrawTextureRectRegion"), Is.EqualTo(2),
                        "The silhouette must include both the floor texture and transparent hull artwork.");
                    Assert.That(world.Invocations.Count(i => i.Method.Name == "DrawTextureRectRegion"), Is.EqualTo(12),
                        "Draw the shadow with eight soft samples, then all three jets.");
                });

                for (var quarter = 1; quarter <= 3; quarter++)
                {
                    var rotation = Angle.FromDegrees(quarter * 90);
                    var eyeRotation = Angle.FromDegrees(quarter * -30);
                    eye.SetupGet(e => e.Rotation).Returns(eyeRotation);
                    var transform = Client.System<SharedTransformSystem>();
                    transform.SetWorldRotation(shadowUid, rotation);
                    foreach (var uid in jets)
                        transform.SetWorldRotation(uid, rotation);
                    UpdateOverlay(overlay, new FrameEventArgs(10f));
                    foreach (var uid in jets)
                    {
                        var jet = CEntMan.GetComponent<DropshipTacticalHoverDownwashComponent>(uid);
                        var light = CEntMan.GetComponent<PointLightComponent>(uid);
                        var contact = GunshipHoverExhaust.GroundContactOffset(jet.ManeuverThrust, rotation, eyeRotation);
                        Assert.That(Vector2.Distance(rotation.RotateVec(light.Offset), contact), Is.LessThan(.0001f),
                            "Real point lights must follow the drawn impact through ship and camera turns.");
                    }
                    DrawOverlay(overlay, in args);
                }
                Assert.That(targets, Has.Count.EqualTo(1), "Rotating the gunship must reuse its cached silhouette.");

                overlay.Invalidate(shadowUid);
                targets[0].Verify(t => t.Dispose(), Times.Once);
                DrawOverlay(overlay, in args);
                Assert.That(targets, Has.Count.EqualTo(2), "New hull state must rebuild the silhouette.");
                overlay.Dispose();
                targets[1].Verify(t => t.Dispose(), Times.Once);
            }
            finally
            {
                overlay.Dispose();
                CEntMan.DeleteEntity(mapUid);
            }
        });
    }

    // OverlayDrawArgs is a ref struct, so ordinary reflection cannot construct or box it.
    // These accessors exercise the actual engine argument selection and overlay entrypoint.
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern OverlayDrawArgs CreateDrawArgs(
        OverlaySpace space,
        IViewportControl viewportControl,
        IClydeViewport viewport,
        IRenderHandle renderHandle,
        in UIBox2i viewportBounds,
        in EntityUid mapUid,
        in MapId mapId,
        in Box2 worldAabb,
        in Box2Rotated worldBounds);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Draw")]
    private static extern void DrawOverlay(DropshipTacticalHoverOverlay overlay, in OverlayDrawArgs args);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "FrameUpdate")]
    private static extern void UpdateOverlay(DropshipTacticalHoverOverlay overlay, FrameEventArgs args);

    private sealed class TestTexture(Vector2i size) : Texture(size)
    {
        public override Color GetPixel(int x, int y) => throw new NotSupportedException();
    }
}
