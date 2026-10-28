using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Wall;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Interaction;

[TestFixture]
public sealed class CMUInteractionRangeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMURangeFixture
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      test:
        shape:
          !type:PhysShapeAabb
            bounds: "-0.1,-0.1,0.1,0.1"
        layer:
        - Impassable

- type: entity
  parent: CMURangeFixture
  id: CMURangeWall
  components:
  - type: Transform
    anchored: true
  - type: Fixtures
    fixtures:
      test:
        shape:
          !type:PhysShapeAabb
            bounds: "-0.45,-0.45,0.45,0.45"
        layer:
        - Impassable

- type: entity
  parent: CMURangeFixture
  id: CMURangeItem
  components:
  - type: Item
    size: Tiny

- type: entity
  parent: CMURangeFixture
  id: CMURangeMount
  components:
  - type: Sprite
    sprite: Objects/Tools/crowbar.rsi
    state: icon
  - type: WallMount
    arc: 90
    direction: 180
""";

    [Test]
    public async Task ObstructionTracksMovementAndNestedPredicatesStayIndependentOnBothSides()
    {
        var map = await Pair.CreateTestMap();
        NetEntity origin = default, target = default, blocker = default;
        await Server.WaitAssertion(() =>
        {
            origin = SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMURangeFixture", map.GridCoords));
            target = SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMURangeFixture", map.GridCoords.Offset(new Vector2(2, 0))));
            blocker = SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMURangeFixture", map.GridCoords.Offset(Vector2.UnitX)));
        });
        await Pair.RunUntilSynced();

        void Check(IEntityManager entities, bool blocked)
        {
            var system = entities.System<SharedInteractionSystem>();
            var from = entities.GetEntity(origin);
            var to = entities.GetEntity(target);
            var wall = entities.GetEntity(blocker);
            Assert.That(system.InRangeUnobstructed(from, to, 3, lagCompensate: false), Is.EqualTo(!blocked));
            Assert.That(system.InRangeUnobstructed(from, to, 1, lagCompensate: false), Is.False);
            Assert.That(system.InRangeUnobstructed(from, to, 3, predicate: uid => uid == wall, lagCompensate: false), Is.True);
            Assert.That(system.InRangeUnobstructed(from, to, 3, lagCompensate: false), Is.EqualTo(!blocked),
                "Exemptions must not persist into another query.");

            if (!blocked)
                return;

            var nested = false;
            bool Ignore(EntityUid uid)
            {
                if (uid != wall || nested)
                    return false;
                nested = true;
                Assert.That(system.InRangeUnobstructed(from, to, 3, predicate: id => id == wall, lagCompensate: false), Is.True);
                return false;
            }

            Assert.That(system.InRangeUnobstructed(from, to, 3, predicate: Ignore, lagCompensate: false), Is.False);
            Assert.That(nested, Is.True, "The nested query must execute inside the blocking fixture predicate.");
            var transform = entities.System<SharedTransformSystem>();
            var start = transform.GetMapCoordinates(from);
            var end = transform.GetMapCoordinates(to);
            Assert.That(system.InRangeUnobstructed(start, end, 3, predicate: uid => uid == from || uid == to), Is.False);
            Assert.That(system.InRangeUnobstructed(start, end, 3, predicate: uid => uid == from || uid == to || uid == wall), Is.True);
            Assert.That(system.InRangeUnobstructed(start, end, 0.1f, checkAccess: false), Is.True);
        }

        await Server.WaitAssertion(() => Check(SEntMan, true));
        await Client.WaitAssertion(() => Check(CEntMan, true));
        await Server.WaitAssertion(() => Server.System<SharedTransformSystem>().SetCoordinates(
            SEntMan.GetEntity(blocker), map.GridCoords.Offset(new Vector2(1, 2))));
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() => Check(SEntMan, false));
        await Client.WaitAssertion(() => Check(CEntMan, false));
    }

    [Test]
    public async Task EmbeddedItemsAndDirectionalWallMountsPreserveLiveExemptions()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, item = default, mount = default, mountUser = default;
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var system = Server.System<SharedInteractionSystem>();
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords.Offset(new Vector2(2, 0)), map.Tile.Tile);
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords.Offset(new Vector2(2, 2)), map.Tile.Tile);
            user = SEntMan.SpawnEntity(null, map.GridCoords);
            var wall = SEntMan.SpawnEntity("CMURangeWall", map.GridCoords.Offset(new Vector2(2, 0)));
            var wallPosition = transform.GetMapCoordinates(wall);
            item = SEntMan.SpawnEntity("CMURangeItem", SEntMan.GetComponent<TransformComponent>(wall).Coordinates);
            transform.SetCoordinates(user, transform.ToCoordinates(map.Grid.Owner, wallPosition.Offset(new Vector2(-2, 0))));
            Assert.That(system.InRangeUnobstructed(user, item, 3, lagCompensate: false), Is.True,
                "An item embedded in a colliding static wall remains reachable.");
            transform.SetCoordinates(item, SEntMan.GetComponent<TransformComponent>(wall).Coordinates.Offset(new Vector2(0.8f, 0)));
            Assert.That(system.InRangeUnobstructed(user, item, 3, lagCompensate: false), Is.False,
                "Moving the item clear of the wall must remove that wall's exemption.");

            var secondWall = SEntMan.SpawnEntity("CMURangeWall", map.GridCoords.Offset(new Vector2(2, 2)));
            var position = transform.GetMapCoordinates(secondWall);
            mount = SEntMan.SpawnEntity("CMURangeMount", SEntMan.GetComponent<TransformComponent>(secondWall).Coordinates);
            mountUser = SEntMan.SpawnEntity(null, SEntMan.GetComponent<TransformComponent>(secondWall).Coordinates.Offset(new Vector2(-2, 0)));
            var wallMount = SEntMan.GetComponent<WallMountComponent>(mount);
            wallMount.Direction = Angle.FromWorldVec(new Vector2(-1, 0));
            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.True);
            transform.SetCoordinates(mountUser, transform.ToCoordinates(map.Grid.Owner, position.Offset(new Vector2(2, 0))));
            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.False,
                "The opposite side of a directional mount must remain obstructed.");
            wallMount.Arc = Angle.FromDegrees(360);
            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.True);
        });
    }
}
