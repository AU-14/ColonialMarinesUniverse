using System.Reflection;
using Content.Client.Atmos.Overlays;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Moq;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using static Content.Shared.Atmos.EntitySystems.SharedGasTileOverlaySystem;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUGasTileHeatPreparationTest : GameTest
{
    [Test]
    public async Task EmptyColdAndNullspacePassesDoNotTouchGraphicsOrViewportCache()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = CreateGrid(maps, mapId);
            using var render = new HeatRenderer();
            var viewport = Viewport(1, new Vector2i(672, 480), Matrix3x2.Identity);
            var bounds = new Box2(-2, -2, 3, 3);
            try
            {
                Assert.That(render.Prepare(viewport.Object, mapId, bounds), Is.False);
                var gas = CEntMan.AddComponent<GasTileOverlayComponent>(grid);
                SetGas(gas, Vector2i.Zero, 296f);
                SetGas(gas, Vector2i.One, 300f);
                Assert.That(render.Prepare(viewport.Object, mapId, bounds), Is.False);
                SetGas(gas, Vector2i.Zero, 1000f);
                Assert.That(render.Prepare(viewport.Object, MapId.Nullspace, bounds), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(render.Clyde.Invocations, Is.Empty, "No render targets may be allocated for empty heat.");
                    Assert.That(render.Handle.Invocations, Is.Empty, "No clear, target switch, shader, transform or draw is needed.");
                    Assert.That(render.Shader.Invocations, Is.Empty);
                    Assert.That(render.Overlay.RequestScreenTexture, Is.True,
                        "The ordinary BeforeDraw=false contract suppresses the copy without changing overlay policy.");
                });
                viewport.VerifyGet(v => v.RenderTarget, Times.Never);
                viewport.VerifyAdd(v => v.ClearCachedResources += It.IsAny<Action<ClearCachedViewportResourcesEvent>>(), Times.Never);
            }
            finally
            {
                CEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [Test]
    public async Task PreparedHeatIsReusedOnlyWithinTheCallAndOwnsOneTargetPerActiveViewport()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = CreateGrid(maps, mapId);
            var gas = CEntMan.AddComponent<GasTileOverlayComponent>(grid);
            using var render = new HeatRenderer();
            var size = new Vector2i(672, 480);
            var viewport = Viewport(0, size, Matrix3x2.Identity);
            var otherViewport = Viewport(1, size, Matrix3x2.CreateTranslation(13, 7));
            var near = new Box2(-2, -2, 3, 3);
            var far = new Box2(100, 100, 105, 105);
            try
            {
                SetGas(gas, Vector2i.Zero, 1000f);
                // Mutating after preparation proves that the callback consumes the prepared
                // geometry instead of performing a second ECS/chunk scan inside the target.
                render.BeforeMask = () => gas.Chunks.Clear();
                Assert.That(render.Prepare(viewport.Object, mapId, near), Is.True);
                Assert.That(render.Draws, Has.Length.EqualTo(1));
                Assert.That(render.Targets, Has.Count.EqualTo(1), "The old unused second target must not be allocated.");
                var firstCallback = render.Callbacks.Single();
                Assert.That(render.ClearColors.Single(), Is.EqualTo(new Color(0, 0, 0, 0)));
                var graphicsCalls = render.Handle.Invocations.Count;

                render.BeforeMask = null;
                Assert.That(render.Prepare(viewport.Object, mapId, near), Is.False, "Removed hot tiles must not replay the previous mask.");
                Assert.That(render.Handle.Invocations, Has.Count.EqualTo(graphicsCalls));

                SetGas(gas, Vector2i.One, 640f);
                Assert.That(render.Prepare(viewport.Object, mapId, near), Is.True);
                Assert.That(render.Targets, Has.Count.EqualTo(1));
                Assert.That(render.Callbacks.Last(), Is.SameAs(firstCallback));
                Assert.That(render.Draws.Last().Bounds, Is.EqualTo(new Box2(0.25f, 0.25f, 2.75f, 2.75f)));

                graphicsCalls = render.Handle.Invocations.Count;
                var coldCorner = new Box2(3, 3, 3.5f, 3.5f);
                Assert.That(render.Prepare(viewport.Object, mapId, coldCorner), Is.False,
                    "A camera over this grid's cold corner must exclude heat outside the enlarged local bounds.");
                Assert.That(render.Handle.Invocations, Has.Count.EqualTo(graphicsCalls));
                Assert.That(render.Prepare(otherViewport.Object, mapId, far), Is.False,
                    "An independently positioned viewport must prepare its own visibility.");
                Assert.That(render.Handle.Invocations, Has.Count.EqualTo(graphicsCalls));
                otherViewport.VerifyAdd(v => v.ClearCachedResources += It.IsAny<Action<ClearCachedViewportResourcesEvent>>(), Times.Never);
                Assert.That(render.Prepare(otherViewport.Object, mapId, near), Is.True);
                Assert.That(render.Targets, Has.Count.EqualTo(2));
                Assert.That(render.Callbacks.Last(), Is.SameAs(firstCallback));

                var larger = Target(new Vector2i(1345, 961));
                viewport.SetupGet(v => v.RenderTarget).Returns(larger.Object);
                Assert.That(render.Prepare(viewport.Object, mapId, near), Is.True);
                Assert.That(render.Targets, Has.Count.EqualTo(3));
                render.Targets[0].Verify(t => t.Dispose(), Times.Once);
                render.Targets[1].Verify(t => t.Dispose(), Times.Never);

                render.BeforeMask = () => throw new InvalidOperationException("Simulated render-target failure.");
                Assert.Throws<InvalidOperationException>(() => render.Prepare(viewport.Object, mapId, near));
                Assert.That(Field("_cmuHeatWorldHandle").GetValue(render.Overlay), Is.Null,
                    "The cached callback must release its borrowed handle even on failure.");
                render.BeforeMask = null;
                Assert.That(render.Prepare(viewport.Object, mapId, near), Is.True);
                Assert.That(render.Targets, Has.Count.EqualTo(3));

                viewport.Raise(v => v.ClearCachedResources += null, default(ClearCachedViewportResourcesEvent));
                render.Targets[2].Verify(t => t.Dispose(), Times.Once);
                render.Targets[1].Verify(t => t.Dispose(), Times.Never);
                render.Dispose();
                foreach (var target in render.Targets)
                    target.Verify(t => t.Dispose(), Times.Once);
            }
            finally
            {
                CEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [Test]
    public async Task RotatedMultipleGridsPreserveTileOrderSpillAndLastColdGridShaderMatrix()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var xform = Client.System<SharedTransformSystem>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var first = CreateGrid(maps, mapId);
            var second = CreateGrid(maps, mapId);
            xform.SetLocalPositionRotation(first, new Vector2(2, -1), Angle.FromDegrees(30));
            xform.SetLocalPositionRotation(second, new Vector2(-3, 2), Angle.FromDegrees(-15));
            CEntMan.AddComponent<GasTileOverlayComponent>(first);
            CEntMan.AddComponent<GasTileOverlayComponent>(second);
            using var render = new HeatRenderer();
            var size = new Vector2i(673, 481);
            var camera = Matrix3x2.CreateRotation(0.37f) * Matrix3x2.CreateScale(24, -24) *
                Matrix3x2.CreateTranslation(301.5f, 207.25f);
            var viewport = Viewport(3, size, camera);
            var bounds = new Box2(-30, -30, 30, 30);
            try
            {
                // Use the map system's actual ordering, then make its last valid gas grid cold.
                var grids = new List<Entity<MapGridComponent>>();
                maps.FindGridsIntersecting(mapId, bounds, ref grids);
                Assert.That(grids, Has.Count.EqualTo(2));
                var hot = CEntMan.GetComponent<GasTileOverlayComponent>(grids[0]);
                var cold = CEntMan.GetComponent<GasTileOverlayComponent>(grids[1]);
                // Insert the later chunk first to distinguish chunk order from spatial order.
                SetGas(hot, new Vector2i(ChunkSize, 0), 640f);
                SetGas(hot, new Vector2i(2, 0), 1000f);
                SetGas(hot, Vector2i.Zero, 500f);
                SetGas(hot, Vector2i.One, 300f);
                SetGas(cold, Vector2i.Zero, 296f);
                var rotatedBounds = new Box2Rotated(bounds, Angle.FromDegrees(17), Vector2.Zero);

                Assert.That(render.Overlay.CMUBeforeDrawHeat(viewport.Object, mapId,
                    rotatedBounds.CalcBoundingBox(), rotatedBounds, render.Handle.Object), Is.True);

                var draws = render.Draws;
                Assert.That(draws, Has.Length.EqualTo(3));
                Assert.That(draws.Select(d => d.Bounds.Center), Is.EqualTo(new[]
                {
                    new Vector2(ChunkSize + 0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(2.5f, 0.5f),
                }));
                foreach (var draw in draws)
                    Assert.That(draw.Bounds.Size, Is.EqualTo(new Vector2(2.5f)));
                Assert.That(draws.Select(d => d.Modulate), Is.EqualTo(new Color?[]
                {
                    new(0.2f, 0, 0),
                    new(200f / 1700f, 0, 0),
                    new(700f / 1700f, 0, 0),
                }));

                var transforms = render.Handle.Invocations.Where(i => i.Method.Name == "SetTransform")
                    .Select(i => (Matrix3x2) i.Arguments[0]).ToArray();
                var parameters = render.Shader.Invocations.Where(i => i.Method.Name == "SetParameterImpl" &&
                    Equals(i.Arguments[0], "grid_ent_from_viewport_local"))
                    .Select(i => (Matrix3x2) i.Arguments[1]).ToArray();
                Assert.That(transforms, Has.Length.EqualTo(2));
                Assert.That(parameters, Has.Length.EqualTo(2), "A cold final grid still sets the final distortion matrix.");
                for (var i = 0; i < grids.Count; i++)
                {
                    var gridToViewport = xform.GetWorldMatrix(grids[i]) * camera;
                    Assert.That(Matrix3x2.Invert(gridToViewport, out var inverse), Is.True);
                    Assert.That(transforms[i], Is.EqualTo(gridToViewport));
                    Assert.That(parameters[i], Is.EqualTo(Matrix3Helpers.CreateScale(size.X, -size.Y) * inverse));
                }
                var transformCalls = render.Handle.Invocations
                    .Where(i => i.Method.Name is "SetTransform" or "DrawTextureRectRegion")
                    .Select(i => i.Method.Name).ToArray();
                Assert.That(transformCalls, Is.EqualTo(new[]
                {
                    "SetTransform", "DrawTextureRectRegion", "DrawTextureRectRegion", "DrawTextureRectRegion", "SetTransform",
                }));
            }
            finally
            {
                CEntMan.DeleteEntity(mapUid);
            }
        });
    }

    private Entity<MapGridComponent> CreateGrid(SharedMapSystem maps, MapId mapId)
    {
        var grid = maps.CreateGridEntity(mapId);
        var floor = new Tile(Client.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
        maps.SetTile(grid, Vector2i.Zero, floor);
        maps.SetTile(grid, new Vector2i(3, 3), floor);
        return grid;
    }

    private static void SetGas(GasTileOverlayComponent component, Vector2i tile, float kelvin)
    {
        var index = GetGasChunkIndices(tile);
        if (!component.Chunks.TryGetValue(index, out var chunk))
        {
            chunk = new GasOverlayChunk(index);
            component.Chunks.Add(index, chunk);
        }

        var data = chunk.TileData;
        var offset = tile - chunk.Origin;
        data[offset.X + offset.Y * ChunkSize] = new GasOverlayData(0, Array.Empty<byte>(), new ThermalByte(kelvin));
    }

    private static Mock<IClydeViewport> Viewport(long id, Vector2i size, Matrix3x2 camera)
    {
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupGet(v => v.Id).Returns(id);
        viewport.SetupGet(v => v.RenderTarget).Returns(Target(size).Object);
        viewport.Setup(v => v.GetWorldToLocalMatrix()).Returns(camera);
        return viewport;
    }

    private static Mock<IRenderTexture> Target(Vector2i size)
    {
        var target = new Mock<IRenderTexture>();
        target.SetupGet(t => t.Size).Returns(size);
        target.SetupGet(t => t.Texture).Returns(new TestTexture(size));
        return target;
    }

    private static FieldInfo Field(string name) => typeof(GasTileHeatBlurOverlay)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);

    private sealed class TestTexture(Vector2i size) : Texture(size)
    {
        public override Color GetPixel(int x, int y) => throw new NotSupportedException();
    }

    private sealed class HeatRenderer : IDisposable
    {
        public readonly GasTileHeatBlurOverlay Overlay = new();
        public readonly Mock<IClyde> Clyde = new();
        public readonly Mock<ShaderInstance> Shader = new();
        public readonly Mock<DrawingHandleWorld> Handle = new(MockBehavior.Loose, new TestTexture(Vector2i.One));
        public readonly List<Mock<IRenderTexture>> Targets = new();
        public readonly List<Action> Callbacks = new();
        public readonly List<Color?> ClearColors = new();
        public Action BeforeMask;

        public (Box2 Bounds, Color? Modulate)[] Draws => Handle.Invocations
            .Where(i => i.Method.Name == "DrawTextureRectRegion")
            .Select(i => ((Box2) i.Arguments[1], (Color?) i.Arguments[2])).ToArray();

        public HeatRenderer()
        {
            // Only graphics boundaries are replaced; the production entrypoint performs real
            // map/grid lookup, thermal conversion, chunk traversal and camera preparation.
            Field("_clyde").SetValue(Overlay, Clyde.Object);
            Field("_shader").SetValue(Overlay, Shader.Object);
            Clyde.Setup(c => c.CreateRenderTarget(It.IsAny<Vector2i>(),
                    It.IsAny<RenderTargetFormatParameters>(), It.IsAny<TextureSampleParameters?>(), It.IsAny<string>()))
                .Returns<Vector2i, RenderTargetFormatParameters, TextureSampleParameters?, string>((size, _, _, _) =>
                {
                    var target = Target(size);
                    Targets.Add(target);
                    return target.Object;
                });
            Handle.Setup(h => h.RenderInRenderTarget(It.IsAny<IRenderTarget>(), It.IsAny<Action>(), It.IsAny<Color?>()))
                .Callback<IRenderTarget, Action, Color?>((_, action, color) =>
                {
                    Callbacks.Add(action);
                    ClearColors.Add(color);
                    BeforeMask?.Invoke();
                    action();
                });
        }

        public bool Prepare(IClydeViewport viewport, MapId map, Box2 bounds) =>
            Overlay.CMUBeforeDrawHeat(viewport, map, bounds, new Box2Rotated(bounds, Angle.Zero), Handle.Object);

        public void Dispose() => Overlay.Dispose();
    }
}
