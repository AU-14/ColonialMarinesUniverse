using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Climbing;

[TestFixture]
[TestOf(typeof(ClimbSystem))]
public sealed class CMUClimbObstructionTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task TableInsideWallCannotBeVaulted(bool dragging)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.7f, 0.5f));
            var destination = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            var user = SEntMan.SpawnEntity("MobHuman", origin);
            var climber = dragging ? SEntMan.SpawnEntity("MobHuman", origin) : user;
            var table = SEntMan.SpawnEntity("Table", destination);
            var wall = SEntMan.SpawnEntity("WallSolid", destination);
            var climb = Server.System<ClimbSystem>();

            Assert.That(climb.TryClimb(user, climber, table, out _), Is.False,
                "the near edge of a table must not let a vault enter the wall around its center");
            Assert.That(SEntMan.GetComponent<ClimbingComponent>(climber).IsClimbing, Is.False);

            SEntMan.DeleteEntity(wall);
            Assert.That(climb.TryClimb(user, climber, table, out _), Is.True,
                "the same table must remain reachable once the wall is removed");
        });
    }

    [Test]
    public async Task ClimbRechecksObstructionAfterInitialValidation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            var origin = map.GridCoords.Offset(new Vector2(0.6f, 0.5f));
            var destination = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            var climber = SEntMan.SpawnEntity("MobHuman", origin);
            var table = SEntMan.SpawnEntity("Table", destination);
            var climb = Server.System<ClimbSystem>();
            Assert.That(climb.CanVault(SEntMan.GetComponent<ClimbableComponent>(table), climber, table, out _), Is.True);
            SEntMan.SpawnEntity("WallSolid", destination);

            // Climb is also called directly and by do-after completion, after initial validation.
            climb.Climb(climber, climber, table);
            var climbing = SEntMan.GetComponent<ClimbingComponent>(climber);
            Assert.Multiple(() =>
            {
                Assert.That(climbing.IsClimbing, Is.False);
                Assert.That(climbing.NextTransition, Is.Null);
                Assert.That(climbing.DisabledFixtureMasks, Is.Empty);
            });
        });
    }
}
