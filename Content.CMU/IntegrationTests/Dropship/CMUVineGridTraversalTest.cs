using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUVineGridTraversalTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task PassingGridPicksUpLooseItemsButLeavesBothVineVariantsInPlace()
    {
        var map = await Pair.CreateTestMap();
        EntityUid light = default, heavy = default, looseItem = default;
        var position = new Vector2(0.5f, 0.5f);
        await Server.WaitAssertion(() =>
        {
            var transforms = SEntMan.System<SharedTransformSystem>();
            transforms.SetCoordinates(map.Grid, new EntityCoordinates(map.MapUid, new Vector2(-10, 0)));
            var coordinates = new EntityCoordinates(map.MapUid, position);
            light = SEntMan.SpawnAttachedTo("CMVinesLight", coordinates);
            heavy = SEntMan.SpawnAttachedTo("CMVinesHeavy", coordinates);
            looseItem = SEntMan.SpawnAttachedTo("CMWrench", coordinates);
            foreach (var vine in new[] { light, heavy })
            {
                var xform = SEntMan.GetComponent<TransformComponent>(vine);
                Assert.That(xform.Anchored, Is.False, "Vines over empty air cannot anchor to a floor tile.");
                Assert.That(xform.ParentUid, Is.EqualTo(map.MapUid));
            }
        });

        // Let the newly spawned bodies leave the initial physics move buffer before the grid passes.
        await RunSeconds(1);
        await Server.WaitAssertion(() => SEntMan.System<SharedTransformSystem>().SetCoordinates(map.Grid,
            new EntityCoordinates(map.MapUid, Vector2.Zero)));
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(looseItem).ParentUid, Is.EqualTo(map.Grid.Owner),
                "The passing grid must exercise ordinary pickup of loose objects.");
            foreach (var vine in new[] { light, heavy })
                Assert.That(SEntMan.GetComponent<TransformComponent>(vine).ParentUid, Is.EqualTo(map.MapUid));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(map.Grid,
                new EntityCoordinates(map.MapUid, new Vector2(10, 0)));
        });

        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var transforms = SEntMan.System<SharedTransformSystem>();
            foreach (var vine in new[] { light, heavy })
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(vine).ParentUid, Is.EqualTo(map.MapUid));
                Assert.That(Vector2.Distance(transforms.GetWorldPosition(vine), position), Is.LessThan(0.001f));
            }
            Assert.That(transforms.GetWorldPosition(looseItem).X, Is.GreaterThan(9f));
        });
    }
}
