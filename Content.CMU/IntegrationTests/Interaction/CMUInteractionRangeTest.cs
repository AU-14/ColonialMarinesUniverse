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
  id: CMURangeWall
  components:
  # Transform must resolve before Physics: the anchor at spawn has to run before the body
  # is registered into a broadphase tree, or the wall ends up Dynamic and unanchored.
  - type: Transform
    anchored: true
  - type: Physics
    bodyType: Static
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
  - type: Physics
    bodyType: Static
    canCollide: true
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
        EntityUid wall = default, secondWall = default;
        WallMountComponent wallMount = default!;
        MapCoordinates wallPosition = default, mountPosition = default;

        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var maps = Server.System<SharedMapSystem>();
            // one connected floor, loose tiles split off into their own grids and unanchor the walls
            for (var x = 0; x <= 2; x++)
            {
                for (var y = 0; y <= 2; y++)
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords.Offset(new Vector2(x, y)), map.Tile.Tile);
            }
            user = SEntMan.SpawnEntity(null, map.GridCoords);
            wall = SEntMan.SpawnEntity("CMURangeWall", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            // Walls must spawn anchored over a plated tile; a failed anchor silently leaves
            // a Dynamic body the exemption lookups never see.
            Assert.That(SEntMan.GetComponent<TransformComponent>(wall).Anchored, Is.True);
            wallPosition = transform.GetMapCoordinates(wall);
            item = SEntMan.SpawnEntity("CMURangeItem", SEntMan.GetComponent<TransformComponent>(wall).Coordinates);
            transform.SetCoordinates(user, transform.ToCoordinates(map.Grid.Owner, wallPosition.Offset(new Vector2(-2, 0))));

            secondWall = SEntMan.SpawnEntity("CMURangeWall", map.GridCoords.Offset(new Vector2(2.5f, 2.5f)));
            Assert.That(SEntMan.GetComponent<TransformComponent>(secondWall).Anchored, Is.True);
            mountPosition = transform.GetMapCoordinates(secondWall);
            mount = SEntMan.SpawnEntity("CMURangeMount", SEntMan.GetComponent<TransformComponent>(secondWall).Coordinates);
            mountUser = SEntMan.SpawnEntity(null, SEntMan.GetComponent<TransformComponent>(secondWall).Coordinates.Offset(new Vector2(-2, 0)));
            wallMount = SEntMan.GetComponent<WallMountComponent>(mount);
            wallMount.Direction = Angle.FromWorldVec(new Vector2(-1, 0));
        });

        // The embedded-item and wall-mount exemptions both resolve through the entity
        // lookup broadphase, which only settles on the next update; querying in the
        // spawn frame reads a tree that does not know the walls yet.
        await Pair.RunUntilSynced();
        // Extra tick to let physics broadphase process anchoring/collision for embedded entities.
        await Server.WaitRunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var system = Server.System<SharedInteractionSystem>();

            Assert.That(system.InRangeUnobstructed(user, item, 3, lagCompensate: false), Is.True,
                "An item embedded in a colliding static wall remains reachable.");
            transform.SetCoordinates(item, SEntMan.GetComponent<TransformComponent>(wall).Coordinates.Offset(new Vector2(0.8f, 0)));
            Assert.That(system.InRangeUnobstructed(user, item, 3, lagCompensate: false), Is.False,
                "Moving the item clear of the wall must remove that wall's exemption.");

            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.True);
            transform.SetCoordinates(mountUser, transform.ToCoordinates(map.Grid.Owner, mountPosition.Offset(new Vector2(2, 0))));
            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.False,
                "The opposite side of a directional mount must remain obstructed.");
            wallMount.Arc = Angle.FromDegrees(360);
            Assert.That(system.InRangeUnobstructed(mountUser, mount, 3, lagCompensate: false), Is.True);
        });
    }
}
