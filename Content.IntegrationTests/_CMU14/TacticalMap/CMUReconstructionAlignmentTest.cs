using Content.Client._RMC14.TacticalMap;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [TestCase(1f, false)]
    [TestCase(2f, false)]
    [TestCase(1f, true)]
    [TestCase(2f, true)]
    public async Task ClassicMouseStrokeMatchesTheTerrainUnderTheCursor(float zoom, bool straight)
    {
        await Client.WaitAssertion(() =>
        {
            using var canvas = new TacticalMapControl { Drawing = true, IsCanvas = true, LineLimit = 64, StraightLineMode = straight };
            Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.AddChild(canvas);
            canvas.Measure(new Vector2(710, 910));
            canvas.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(710, 910)));
            var areas = new AreaGridComponent();
#pragma warning disable RA0002 // A known, asymmetric map extent independent of the conversion being tested.
            areas.Colors[new(-20, -30)] = Color.Gray;
            areas.Colors[new(50, 60)] = Color.Gray;
#pragma warning restore RA0002
            canvas.UpdateTexture((default, areas));
            canvas.LoadViewSettings(zoom, new Vector2(17, -23), null);
            var parameters = ((Vector2 Size, Vector2 TopLeft, float OverlayScale)) typeof(TacticalMapControl)
                .GetMethod("GetDrawParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(canvas, null)!;
            // Texture pixel (x, y) covers [minX+x,minX+x+1] by [maxY-y,maxY-y+1].
            Vector2 Screen(Vector2 world) => (parameters.TopLeft +
                new Vector2(world.X + 20, 61 - world.Y) * (parameters.Size / new Vector2(71, 91))) / canvas.UIScale;
            var start = new Vector2(2.25f, 3.75f);
            var end = new Vector2(7.5f, 9f);
            var from = Screen(start);
            var to = Screen(end);
            Dispatch(canvas, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down,
                default, true, from, from * canvas.UIScale));
            Dispatch(canvas, "MouseMove", new GUIMouseMoveEventArgs(to - from, canvas, to, default, to, to * canvas.UIScale));
            Dispatch(canvas, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up,
                default, true, to, to * canvas.UIScale));
            var stroke = canvas.Lines.Single();
            Assert.That(Vector2.Distance(stroke.WorldPoints![0], start), Is.LessThan(0.01f),
                "A classic stroke must start on the same fractional terrain position in 3D.");
            Assert.That(Vector2.Distance(stroke.WorldPoints[^1], end), Is.LessThan(0.01f));
            var projected = CMUReconDrawingCoordinates.ToCanvas(start, new(-20, -30), new(50, 60));
            Assert.That(Vector2.Distance((projected * parameters.OverlayScale + parameters.TopLeft) / canvas.UIScale, from),
                Is.LessThan(0.01f), "A 3D point must return to the same location on the classic texture.");

            // Drawing around an icon must identify its actual tile in both renderers.
            canvas.UpdateBlips([new TacticalMapBlip { Indices = new(2, 3), Color = Color.Green }]);
            var handle = new Mock<DrawingHandleScreen>(MockBehavior.Loose, canvas.Texture);
            Dispatch(canvas, "DrawBlips", handle.Object, null, canvas.Texture,
                null, null, null, null, null, null, parameters.TopLeft, parameters.OverlayScale, TimeSpan.Zero);
            var draw = handle.Invocations.Single(i => i.Method.Name == "DrawTextureRectRegion");
            var icon = (UIBox2) draw.Arguments[1];
            Assert.That(Vector2.Distance(icon.Center / canvas.UIScale, Screen(new(2.5f, 3.5f))), Is.LessThan(0.01f),
                "The icon must be centered on its tile, not offset by its zoom-dependent size.");
        });
    }
}
