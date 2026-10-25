#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14: leap targeting and multi-z behaviour.
//
// * A target the hunter cannot see (behind a wall, closed door or window) is refused silently: no popup, no wind-up,
//   no warning marker and no cooldown, so the ability stays ready.
// * Crossing a deck is real movement: the hunter runs the normal animated throw and rises through an opening under
//   z-physics, instead of being teleported to the other map. A solid deck refuses the leap outright.
[TestFixture]
public sealed class YautjaLeapValidationTest
{
    private static bool HasLeapDoAfter(IEntityManager entMan, EntityUid hunter)
    {
        return entMan.TryGetComponent<DoAfterComponent>(hunter, out var doAfter) &&
               doAfter.DoAfters.Values.Any(active => active.Args.Event is YautjaLeapDoAfterEvent);
    }

    private static bool Leap(IEntityManager entMan, EntityUid hunter, EntityUid action, EntityCoordinates target)
    {
        var actionComp = entMan.EnsureComponent<ActionComponent>(action);
        var ev = new YautjaLeapActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(hunter, ev);
        return ev.Handled;
    }

    private static EntityUid SpawnHunter(IEntityManager entMan, EntityCoordinates coords, float windup = 0.1f)
    {
        var hunter = entMan.SpawnEntity("CMUMobYautja", coords);
        var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
        yautja.LeapWindup = TimeSpan.FromSeconds(windup);
        yautja.LeapUpWindup = TimeSpan.FromSeconds(windup);
        yautja.LeapDownWindup = TimeSpan.FromSeconds(windup);
        yautja.LeapMaxRange = 7f;
        yautja.LeapThrowSpeed = 5f;
        return hunter;
    }

    private static int LeapWarnings(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == "CMUYautjaLeapWarning")
                count++;
        }

        return count;
    }

    private static void AssertRefused(IEntityManager entMan, EntityUid hunter, bool handled)
    {
        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False,
                "an invalid target must not consume the action, so the ability stays ready with no cooldown");
            Assert.That(HasLeapDoAfter(entMan, hunter), Is.False,
                "an invalid target must not start a wind-up that could later complete");
            Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.False);
            Assert.That(LeapWarnings(entMan), Is.Zero, "a refused leap leaves no warning marker behind");
        });
    }

    private static void SetUpZNetwork(
        IEntityManager entMan,
        ITileDefinitionManager tileDefs,
        out EntityUid lower,
        out EntityUid upper,
        bool holeAboveHunter)
    {
        var maps = entMan.System<SharedMapSystem>();
        var plating = new Tile(tileDefs["Plating"].TileId);

        lower = maps.CreateMap(runMapInit: true);
        upper = maps.CreateMap(runMapInit: true);
        var lowerGrid = entMan.EnsureComponent<MapGridComponent>(lower);
        var upperGrid = entMan.EnsureComponent<MapGridComponent>(upper);

        for (var x = -3; x <= 6; x++)
        for (var y = -3; y <= 3; y++)
        {
            maps.SetTile(lower, lowerGrid, new Vector2i(x, y), plating);

            // With a hole: the upper deck is open above the hunter and along the first tiles of the rise, which is
            // the opening the leap passes through while it keeps moving horizontally.
            if (holeAboveHunter && x is >= -1 and <= 2 && y is >= -1 and <= 1)
                continue;

            maps.SetTile(upper, upperGrid, new Vector2i(x, y), plating);
        }

        var levels = entMan.System<CMUZLevelsSystem>();
        var network = levels.CreateZNetwork().Owner;
        Assert.That(levels.TryAddMapsIntoZNetwork(
            (network, entMan.GetComponent<CMUZLevelsNetworkComponent>(network)),
            new() { [lower] = 0, [upper] = 1 }), Is.True);
    }

    [Test]
    public async Task WallBetweenHunterAndTargetRefusesTheLeapSilently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;
        EntityUid wall = default;
        var handled = true;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = SpawnHunter(entMan, map.GridCoords);
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            wall = entMan.SpawnEntity("CMWallReinforced", map.GridCoords.Offset(new Vector2(2, 0)));

            handled = Leap(entMan, hunter, action, map.GridCoords.Offset(new Vector2(5, 0)));
            AssertRefused(entMan, hunter, handled);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates.Position, Is.EqualTo(Vector2.Zero),
                "the hunter must not move when the leap is refused");

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
            entMan.DeleteEntity(wall);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosedDoorBetweenHunterAndTargetRefusesTheLeapSilently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;
        EntityUid door = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = SpawnHunter(entMan, map.GridCoords);
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            // An airlock spawns closed.
            door = entMan.SpawnEntity("CMAirlock", map.GridCoords.Offset(new Vector2(2, 0)));

            var handled = Leap(entMan, hunter, action, map.GridCoords.Offset(new Vector2(5, 0)));
            AssertRefused(entMan, hunter, handled);
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
            entMan.DeleteEntity(door);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WindowBetweenHunterAndTargetRefusesTheLeapSilently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;
        EntityUid window = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = SpawnHunter(entMan, map.GridCoords);
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            window = entMan.SpawnEntity("RMCWindowCorsatReinforced", map.GridCoords.Offset(new Vector2(2, 0)));

            var handled = Leap(entMan, hunter, action, map.GridCoords.Offset(new Vector2(5, 0)));
            AssertRefused(entMan, hunter, handled);
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
            entMan.DeleteEntity(window);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisibleTargetStillStartsAndCompletesTheLeap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = SpawnHunter(entMan, map.GridCoords);
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            Assert.That(Leap(entMan, hunter, action, map.GridCoords.Offset(new Vector2(5, 0))), Is.True,
                "a visible target consumes the action and starts the leap");
            Assert.That(HasLeapDoAfter(entMan, hunter), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.True);
            Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates.Position.X, Is.GreaterThan(2f));

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpwardLeapRisesThroughTheOpeningInsteadOfTeleporting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid hunter = default;
        EntityUid action = default;
        MapId lowerMapId = default;
        MapId upperMapId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            SetUpZNetwork(entMan, server.ResolveDependency<ITileDefinitionManager>(),
                out lower, out upper, holeAboveHunter: true);
            lowerMapId = entMan.GetComponent<MapComponent>(lower).MapId;
            upperMapId = entMan.GetComponent<MapComponent>(upper).MapId;

            hunter = SpawnHunter(entMan, new EntityCoordinates(lower, 0.5f, 0.5f));
            entMan.EnsureComponent<CMUZLevelViewerComponent>(hunter).LookUp = true;
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            Assert.That(Leap(entMan, hunter, action, new EntityCoordinates(upper, new Vector2(4.5f, 0.5f))), Is.True,
                "an opening above the hunter allows the upward leap");
            Assert.That(HasLeapDoAfter(entMan, hunter), Is.True);
        });

        // Right after the wind-up the hunter must be flying, not already on the other map.
        await pair.RunTicksSync(pair.SecondsToTicks(0.18f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).MapID, Is.EqualTo(lowerMapId),
                    "the hunter is still climbing: crossing a deck is movement, not a teleport");
                Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.True,
                    "the leap is still the normal animated throw");
                Assert.That(entMan.HasComponent<CMUZFallingComponent>(hunter), Is.True,
                    "z-physics is carrying the hunter upward");
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xform = entMan.GetComponent<TransformComponent>(hunter);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapID, Is.EqualTo(upperMapId), "the hunter ends up on the upper deck");
                Assert.That(xform.Coordinates.Position.X, Is.EqualTo(4.5f).Within(0.8f),
                    "and lands on the tile it aimed at");
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpwardLeapThroughASolidDeckIsRefusedSilently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid hunter = default;
        EntityUid action = default;
        MapId lowerMapId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            SetUpZNetwork(entMan, server.ResolveDependency<ITileDefinitionManager>(),
                out lower, out upper, holeAboveHunter: false);
            lowerMapId = entMan.GetComponent<MapComponent>(lower).MapId;

            hunter = SpawnHunter(entMan, new EntityCoordinates(lower, 0.5f, 0.5f));
            entMan.EnsureComponent<CMUZLevelViewerComponent>(hunter).LookUp = true;
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var handled = Leap(entMan, hunter, action, new EntityCoordinates(upper, new Vector2(2.5f, 0.5f)));
            AssertRefused(entMan, hunter, handled);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<TransformComponent>(hunter).MapID, Is.EqualTo(lowerMapId),
                "a solid deck cannot be leapt through");

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DownwardLeapStillDropsThroughAnOpening()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid hunter = default;
        EntityUid action = default;
        MapId lowerMapId = default;
        MapId upperMapId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            SetUpZNetwork(entMan, server.ResolveDependency<ITileDefinitionManager>(),
                out lower, out upper, holeAboveHunter: true);
            lowerMapId = entMan.GetComponent<MapComponent>(lower).MapId;
            upperMapId = entMan.GetComponent<MapComponent>(upper).MapId;

            hunter = SpawnHunter(entMan, new EntityCoordinates(upper, 0.5f, 0.5f));
            entMan.EnsureComponent<CMUZLevelShooterComponent>(hunter).ShootDown = true;
            action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            Assert.That(Leap(entMan, hunter, action, new EntityCoordinates(lower, new Vector2(2.5f, 0.5f))), Is.True,
                "a hole under the hunter allows the downward leap");
            Assert.That(HasLeapDoAfter(entMan, hunter), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.18f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.True,
                "the downward leap is the normal animated throw, not a teleport");
            _ = lowerMapId;
            _ = upperMapId;
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<TransformComponent>(hunter).MapID, Is.EqualTo(lowerMapId),
                "the hunter drops through the opening to the deck below");

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }
}
