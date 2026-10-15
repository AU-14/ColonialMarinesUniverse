using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZHighGroundQueryPruningTest : GameTest
{
    private static readonly FieldInfo HighGroundTiles = typeof(CMUSharedZLevelsSystem)
        .GetField("_profileZHighGroundTiles", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(CMUSharedZLevelsSystem), "_profileZHighGroundTiles");
    private static readonly FieldInfo SweepHighGroundChecks = typeof(CMUSharedZLevelsSystem)
        .GetField("_profileZMoveSnapSweepHighGroundChecks", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(CMUSharedZLevelsSystem), "_profileZMoveSnapSweepHighGroundChecks");

    private readonly List<EntityUid> _cleanup = new();
    private Entity<MapGridComponent> _grid;
    private EntityUid _body;
    private CMUZPhysicsComponent _bodyZ = default!;
    private CMUZLevelsSystem _z = default!;
    private bool? _originalProfiler;

    [TestCase(0.5f, 0.5f, 1)]
    [TestCase(0.1f, 0.5f, 2)]
    [TestCase(0.1f, 0.1f, 4)]
    [TestCase(-0.5f, -0.5f, 1)]
    [TestCase(-0.9f, -0.5f, 2)]
    [TestCase(-0.9f, -0.9f, 4)]
    public async Task GroundQueriesOnlyVisitTilesWithinSupportReach(float x, float y, int expectedTiles)
    {
        try
        {
            await Server.WaitAssertion(() =>
            {
                CreateScenario();
                var position = new Vector2(x, y);
                var before = (int) HighGroundTiles.GetValue(_z)!;
                AssertGround(position, 0f, false);
                var after = (int) HighGroundTiles.GetValue(_z)!;
                Assert.That(after - before, Is.EqualTo(expectedTiles),
                    "Center, edge, and corner samples should query only one, two, and four tiles respectively.");
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [TestCase(0, 0)]
    [TestCase(-3, -2)]
    public async Task PrunedQueriesPreserveCardinalFlatAndCornerSupport(int tileX, int tileY)
    {
        try
        {
            await Server.WaitAssertion(() =>
            {
                CreateScenario();
                var tile = new Vector2i(tileX, tileY);
                var origin = new Vector2(tileX, tileY);
                var directions = new[] { Direction.East, Direction.West, Direction.North, Direction.South };
                foreach (var direction in directions)
                {
                    var ramp = CreateSupport(tile, direction, corner: false, new[] { 0.25f, 0.75f });
                    var inside = direction switch
                    {
                        Direction.East => new Vector2(0.75f, 0.5f),
                        Direction.West => new Vector2(0.25f, 0.5f),
                        Direction.North => new Vector2(0.5f, 0.75f),
                        _ => new Vector2(0.5f, 0.25f),
                    };
                    var topEdge = direction switch
                    {
                        Direction.East => new Vector2(1.2f, 0.5f),
                        Direction.West => new Vector2(-0.2f, 0.5f),
                        Direction.North => new Vector2(0.5f, 1.2f),
                        _ => new Vector2(0.5f, -0.2f),
                    };
                    var outside = direction switch
                    {
                        Direction.East => new Vector2(1.36f, 0.5f),
                        Direction.West => new Vector2(-0.36f, 0.5f),
                        Direction.North => new Vector2(0.5f, 1.36f),
                        _ => new Vector2(0.5f, -0.36f),
                    };
                    AssertGround(origin + inside, -0.625f, true, $"{direction} ramp interior");
                    AssertGround(origin + topEdge, -0.75f, true, $"{direction} ramp neighboring top edge");
                    AssertGround(origin + outside, 0f, false, $"{direction} ramp outside support reach");
                    SEntMan.DeleteEntity(ramp);

                    var corner = CreateSupport(tile, direction, corner: true, new[] { 0.25f, 0.75f });
                    var diagonal = direction switch
                    {
                        Direction.East => new Vector2(1.2f, -0.2f),
                        Direction.West => new Vector2(-0.2f, 1.2f),
                        Direction.North => new Vector2(1.2f, 1.2f),
                        _ => new Vector2(-0.2f, -0.2f),
                    };
                    AssertGround(origin + diagonal, -0.75f, true, $"{direction} corner diagonal support");
                    var outsideDiagonal = diagonal.X > 1f
                        ? new Vector2(1.36f, diagonal.Y)
                        : new Vector2(-0.36f, diagonal.Y);
                    AssertGround(origin + outsideDiagonal, 0f, false, $"{direction} corner outside support reach");
                    SEntMan.DeleteEntity(corner);
                }

                var flat = CreateSupport(tile, Direction.North, corner: false, new[] { 0.6f, 0.6f });
                AssertGround(origin + new Vector2(0.5f), -0.6f, true, "Flat current tile");
                AssertGround(origin + new Vector2(1.2f), -0.6f, true, "Flat neighboring diagonal");
                AssertGround(origin + new Vector2(1.36f, 1.2f), 0f, false, "Flat outside support reach");
                SEntMan.DeleteEntity(flat);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [Test]
    public async Task ReflectedRampEdgePreservesFloatRoundingAcceptance()
    {
        try
        {
            await Server.WaitAssertion(() =>
            {
                CreateScenario();
                var justOutsideUnreflectedBound = MathF.BitDecrement(-0.35f);
                foreach (var direction in new[] { Direction.West, Direction.South })
                {
                    var ramp = CreateSupport(Vector2i.Zero, direction, corner: false, new[] { 0.25f, 0.75f });
                    var position = direction == Direction.West
                        ? new Vector2(justOutsideUnreflectedBound, 0.5f)
                        : new Vector2(0.5f, justOutsideUnreflectedBound);
                    Assert.That(1f - justOutsideUnreflectedBound, Is.EqualTo(1f + 0.35f),
                        "Reflection rounds this coordinate onto the accepted top-edge boundary.");
                    AssertGround(position, -0.75f, true, $"{direction} reflected edge");
                    SEntMan.DeleteEntity(ramp);
                }
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [Test]
    public async Task QueriesImmediatelyObserveAnchoringUnanchoringAndComponentChanges()
    {
        try
        {
            await Server.WaitAssertion(() =>
            {
                CreateScenario();
                var position = new Vector2(0.5f);
                AssertGround(position, 0f, false);
                var support = CreateSupport(Vector2i.Zero, Direction.North, corner: false, new[] { 0.6f, 0.6f });
                AssertGround(position, -0.6f, true);

                var transform = Server.System<SharedTransformSystem>();
                transform.Unanchor(support);
                AssertGround(position, 0f, false, "Unanchored high ground cannot support a body.");
                Assert.That(transform.AnchorEntity((support, SComp<TransformComponent>(support)), _grid, Vector2i.Zero), Is.True);
                AssertGround(position, -0.6f, true);

                SEntMan.RemoveComponent<CMUZLevelHighGroundComponent>(support);
                AssertGround(position, 0f, false, "Removing support must invalidate the next query immediately.");
                var replacement = SEntMan.AddComponent<CMUZLevelHighGroundComponent>(support);
                replacement.HeightCurve = new List<float> { 0.4f, 0.4f };
                replacement.Stick = true;
                AssertGround(position, -0.4f, true, "New support on an already anchored entity must be visible immediately.");
                SEntMan.DeleteEntity(support);
                AssertGround(position, 0f, false);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [TestCase(0f, 7)]
    [TestCase(-1f, 7)]
    [TestCase(1f, 0)]
    [TestCase(-4f, 0)]
    [TestCase(-5f, 0)]
    public async Task MovementSkipsSweptQueriesForIneligibleVerticalVelocity(float velocity, int expectedChecks)
    {
        try
        {
            await Server.WaitAssertion(() =>
            {
                CreateScenario();
                // This ramp is crossed by the samples, but stays below the upper-transition height.
                // Both the ordinary sweep and its velocity rejection must preserve the body's Z state.
                CreateSupport(Vector2i.Zero, Direction.East, corner: false, new[] { 0.1f, 0.8f });
                _z.SetZVelocity((_body, _bodyZ), velocity);
                var transform = Server.System<SharedTransformSystem>();
                var destination = new Vector2(1.5f, 0.5f);
                var before = (int) SweepHighGroundChecks.GetValue(_z)!;
                transform.SetCoordinates(_body, new EntityCoordinates(_grid.Owner, destination));
                var after = (int) SweepHighGroundChecks.GetValue(_z)!;

                Assert.Multiple(() =>
                {
                    Assert.That(after - before, Is.EqualTo(expectedChecks),
                        "Eligible movement keeps all seven intermediate samples; ineligible velocity needs none.");
                    Assert.That(Vector2.Distance(transform.GetWorldPosition(_body), destination), Is.LessThan(0.0001f));
                    Assert.That(SComp<TransformComponent>(_body).MapUid, Is.EqualTo(_grid.Owner));
                    Assert.That(_bodyZ.LocalPosition, Is.Zero);
                    Assert.That(_bodyZ.Velocity, Is.EqualTo(velocity));
                });
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    private void CreateScenario()
    {
        _originalProfiler = Server.CfgMan.GetCVar(Robust.Shared.CVars.ProfEnabled);
        Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, true);
        var maps = Server.System<SharedMapSystem>();
        var map = maps.CreateMap(runMapInit: true);
        _cleanup.Add(map);
        _grid = (map, SEntMan.EnsureComponent<MapGridComponent>(map));
        var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
        for (var x = -6; x <= 6; x++)
        for (var y = -6; y <= 6; y++)
            maps.SetTile(_grid, new Vector2i(x, y), floor);
        _z = Server.System<CMUZLevelsSystem>();
        var network = _z.CreateZNetwork();
        _cleanup.Add(network.Owner);
        Assert.That(_z.TryAddMapsIntoZNetwork(network, new() { [map] = 0 }), Is.True);
        _body = SEntMan.SpawnEntity(null, new EntityCoordinates(map, new Vector2(0.5f)));
        _cleanup.Add(_body);
        _bodyZ = SEntMan.AddComponent<CMUZPhysicsComponent>(_body);
    }

    private EntityUid CreateSupport(Vector2i tile, Direction direction, bool corner, float[] curve)
    {
        var uid = SEntMan.SpawnEntity(null,
            new EntityCoordinates(_grid.Owner, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)));
        var support = SEntMan.AddComponent<CMUZLevelHighGroundComponent>(uid);
        support.HeightCurve = curve.ToList();
        support.Stick = true;
        support.Corner = corner;
        var transform = Server.System<SharedTransformSystem>();
        transform.SetWorldRotation(uid, direction.ToAngle());
        Assert.That(transform.AnchorEntity((uid, SComp<TransformComponent>(uid)), _grid, tile), Is.True);
        return uid;
    }

    private void AssertGround(Vector2 position, float expectedDistance, bool expectedSticky, string reason = null)
    {
        var distance = _z.DistanceToGroundAtWorldPosition((_body, _bodyZ), position, out var sticky, maxFloors: 0);
        Assert.Multiple(() =>
        {
            Assert.That(distance, Is.EqualTo(expectedDistance).Within(0.0001f), reason);
            Assert.That(sticky, Is.EqualTo(expectedSticky), reason);
        });
    }

    private async Task CleanupScenario()
    {
        await Server.WaitPost(() =>
        {
            if (_originalProfiler is { } enabled)
                Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, enabled);
        });
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
    }
}
