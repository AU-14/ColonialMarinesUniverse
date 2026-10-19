using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;
using System.Linq;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14: a leap aimed at a deck must remember the grid and tile, not just a world position - a gunship that moves
// during the wind-up would otherwise be missed. This asserts the destination identity survives into the do-after.
[TestFixture]
public sealed class YautjaLeapGridDestinationTest
{
    [Test]
    public async Task LeapRemembersTheClickedGridAndTile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;
        EntityUid grid = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                grid = map.GridCoords.EntityId;
                Assert.That(entMan.HasComponent<MapGridComponent>(grid), Is.True, "the test map has a grid to aim at");

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.LeapWindup = TimeSpan.FromSeconds(5);

                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = new EntityCoordinates(grid, new Vector2(3, 2)),
                };
                entMan.EventBus.RaiseLocalEvent(hunter, ev);
                Assert.That(ev.Handled, Is.True, "the leap started");

                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                var leap = doAfter.DoAfters.Values
                    .Select(active => active.Args.Event)
                    .OfType<YautjaLeapDoAfterEvent>()
                    .Single();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetEntity(leap.DestinationGrid!.Value), Is.EqualTo(grid),
                        "the destination grid is carried through targeting");
                    Assert.That(leap.GridLocalPosition, Is.EqualTo(new Vector2(3, 2)),
                        "the grid-local tile is carried through targeting");
                    Assert.That(leap.ZOffset, Is.Zero, "a same-level aim has no Z offset");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (action != default && !entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            });

            await pair.CleanReturnAsync();
        }
    }
}
