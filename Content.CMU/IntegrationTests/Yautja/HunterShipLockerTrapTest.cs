using System.Linq;
using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Yautja;

// the hunter ship lockers used to pull in whoever stood next to them and drop them back out inside a full tile wall
[TestFixture]
public sealed class HunterShipLockerTrapTest
{
    private const string Locker = "CMUHunterShipMeatLockerFoodMeatx16";

    [Test]
    public async Task ClosingALockerLeavesTheNeighbourOutAndAnOpenLockerIsWalkable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storage = entMan.System<SharedEntityStorageSystem>();
            var center = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var locker = entMan.SpawnEntity(Locker, center);
            // standing on the next tile over, 0.75 used to reach them
            var neighbour = entMan.SpawnEntity("CMMobHuman", center.Offset(new Vector2(0.7f, 0)));
            var inside = entMan.SpawnEntity("CMMobHuman", center);

            storage.OpenStorage(locker);
            storage.CloseStorage(locker);

            var contents = entMan.GetComponent<EntityStorageComponent>(locker).Contents;
            Assert.That(contents.Contains(neighbour), Is.False, "only what's on the locker's own tile goes in");
            Assert.That(contents.Contains(inside), Is.True);

            storage.OpenStorage(locker);
            var mobMask = (int) CollisionGroup.MobMask;
            var blocking = entMan.GetComponent<FixturesComponent>(locker).Fixtures.Values
                .Where(fixture => fixture.Hard && (fixture.CollisionLayer & mobMask) != 0)
                .ToList();
            Assert.That(blocking, Is.Empty, "an open locker can't be a wall, that's where the person inside lands");

            entMan.DeleteEntity(neighbour);
            entMan.DeleteEntity(inside);
            entMan.DeleteEntity(locker);
        });

        await pair.CleanReturnAsync();
    }
}
