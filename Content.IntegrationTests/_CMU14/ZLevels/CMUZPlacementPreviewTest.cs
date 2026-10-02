using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Placement.Modes;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZPlacementPreviewTest : GameTest
{
    [Test]
    public async Task PlacementStaysOnItsMapAndIsRestoredAfterFailedRender()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var otherMap = maps.CreateMap(out var otherMapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            var placement = Client.ResolveDependency<IPlacementManager>();
            var eyes = Client.ResolveDependency<IEyeManager>();
            var previousViewport = eyes.MainViewport;
            var previousMode = placement.CurrentMode;
            var mode = new PreviewMode((PlacementManager) placement, grid);
            var viewport = new Mock<IClydeViewport>();
            var eye = new Eye { Position = new MapCoordinates(Vector2.Zero, otherMapId) };
            viewport.SetupProperty(v => v.Eye, eye);
            var control = new Mock<IViewportControl>();
            control.Setup(v => v.ScreenToMap(It.IsAny<Vector2>())).Returns(() => viewport.Object.Eye!.Position);
            var zLevels = Client.System<CMUClientZLevelsSystem>();

            try
            {
                eyes.MainViewport = control.Object;
                placement.CurrentMode = mode;

                // Reproduce the real snap-grid conversion before it reaches any drawing handles.
                Assert.Throws<ArgumentException>(() => mode.Render(default));
                viewport.Setup(v => v.Render()).Callback(() => placement.CurrentMode?.Render(default));
                zLevels.RenderViewport(viewport.Object);
                Assert.That(placement.CurrentMode, Is.SameAs(mode));

                eye.Position = new MapCoordinates(Vector2.Zero, mapId);
                viewport.Setup(v => v.Render()).Callback(() =>
                {
                    Assert.That(placement.CurrentMode, Is.SameAs(mode));
                    var position = maps.MapToGrid(grid, eyes.ScreenToMap(Vector2.Zero));
                    Assert.That(position.EntityId, Is.EqualTo(grid.Owner));
                });
                zLevels.RenderViewport(viewport.Object);

                eye.Position = new MapCoordinates(Vector2.Zero, otherMapId);
                viewport.Setup(v => v.Render()).Throws(new InvalidOperationException("Injected render failure"));
                Assert.Throws<InvalidOperationException>(() => zLevels.RenderViewport(viewport.Object));
                Assert.That(placement.CurrentMode, Is.SameAs(mode));
                Assert.That(mode.MouseCoords.EntityId, Is.EqualTo(grid.Owner));
            }
            finally
            {
                placement.CurrentMode = previousMode;
                eyes.MainViewport = previousViewport;
                CEntMan.DeleteEntity(map);
                CEntMan.DeleteEntity(otherMap);
            }
        });
    }

    [Reflect(false)]
    private sealed class PreviewMode : SnapgridCenter
    {
        public PreviewMode(PlacementManager placement, Entity<MapGridComponent> grid) : base(placement)
        {
            Grid = grid.Comp;
            SnapSize = 1f;
            MouseCoords = new EntityCoordinates(grid, Vector2.Zero);
        }
    }
}
