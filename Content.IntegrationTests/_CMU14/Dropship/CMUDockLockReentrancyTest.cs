using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Dropship;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Gravity;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDockLockReentrancyTest : GameTest
{
    [Test]
    public async Task AutomaticReturnLocksEveryDockWhenClosingDropsAnItemOntoTheGrid()
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default, second = default, occupant = default, paper = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            maps.SetTile(map.Grid, new Vector2i(2, 0), map.Tile.Tile);
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid.Owner);
            Server.System<GravitySystem>().EnableGravity(map.Grid.Owner, gravity);
            first = SEntMan.SpawnEntity("CMAirlockShuttle", map.GridCoords);
            second = SEntMan.SpawnEntity("CMAirlockShuttle", map.GridCoords.Offset(new Vector2(2, 0)));
            var doors = Server.System<SharedDoorSystem>();
            foreach (var dock in new[] { first, second })
            {
                doors.OnPartialOpen(dock);
                doors.SetState(dock, DoorState.Open);
                doors.SetNextStateChange(dock, null);
            }
            occupant = SEntMan.SpawnEntity("MobHuman", SEntMan.GetComponent<TransformComponent>(first).Coordinates);
            paper = SEntMan.SpawnEntity("CMPaper", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(occupant, paper), Is.True);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var dropship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid.Owner);
#pragma warning disable RA0002 // Arrange a landed dropship immediately before its inactivity deadline.
            dropship.Destination = SEntMan.SpawnEntity(null, map.GridCoords);
#pragma warning restore RA0002
            var autoReturn = SEntMan.AddComponent<ThirdPartyDropshipAutoReturnComponent>(map.Grid.Owner);
            autoReturn.ReturnDestination = SEntMan.SpawnEntity(null, map.GridCoords);
            autoReturn.InactivityDelay = TimeSpan.Zero;
            autoReturn.ReturnDelay = TimeSpan.FromMinutes(1);
            Assert.That(SEntMan.GetComponent<GravityAffectedComponent>(occupant).Weightless, Is.False);
            Server.System<DropshipSystem>().Update(0);
            Assert.That(SEntMan.GetComponent<DoorComponent>(first).CurrentlyCrushing, Does.Contain(occupant));
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(occupant, paper), Is.False,
                "Closing must exercise the callback that drops a held item and changes grid children.");
            Assert.That(SEntMan.GetComponent<DoorBoltComponent>(first).BoltsDown, Is.True);
            Assert.That(SEntMan.GetComponent<DoorBoltComponent>(second).BoltsDown, Is.True);
        });
    }
}
