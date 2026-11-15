using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared.CMU14.Yautja;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using ServerHandsSystem = Content.Server.Hands.Systems.HandsSystem;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBodyThrowRangeTest : GameTest
{
    // zoomed out, the cursor can sit way past any sane throw. a carried body should still only go so far
    [Test]
    public async Task CarriedBodyThrowIsCapped()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hunter = default, victim = default;

        await Server.WaitPost(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            for (var x = -2; x < 60; x++)
            {
                for (var y = -2; y < 3; y++)
                    mapSystem.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            hunter = SSpawnAtPosition("CMUMobYautja", map.GridCoords);
            victim = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<PullingSystem>().TryStartPull(hunter, victim), Is.True);
        });
        await Pair.RunTicksSync(Pair.SecondsToTicks(5));

        await Server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            SEntMan.EventBus.RaiseLocalEvent(hunter, ref toggle);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var drag = new DragDropDraggedEvent(hunter, hunter);
            SEntMan.EventBus.RaiseLocalEvent(victim, ref drag);
        });
        await Pair.RunTicksSync(Pair.SecondsToTicks(8));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FiremanCarriableComponent>(victim).BeingCarried, Is.True, "the carry never started");
            var hands = Server.System<SharedHandsSystem>();
            var carryHand = hands.EnumerateHands(hunter).First(hand =>
                hands.GetHeldItem(hunter, hand) is { } held &&
                SEntMan.TryGetComponent(held, out VirtualItemComponent? virtualItem) &&
                virtualItem.BlockingEntity == victim);
            hands.SetActiveHand(hunter, carryHand);

            var far = SEntMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(new Vector2(50, 0));
            Assert.That(Server.System<ServerHandsSystem>().ThrowHeldItem(hunter, far), Is.True);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(6));

        await Server.WaitAssertion(() =>
        {
            var xform = Server.System<SharedTransformSystem>();
            var distance = (xform.GetWorldPosition(victim) - xform.GetWorldPosition(hunter)).Length();
            var cap = SEntMan.GetComponent<YautjaComponent>(hunter).CarriedThrowRange;
            var itemRange = SEntMan.GetComponent<HandsComponent>(hunter).ThrowRange;
            Assert.That(cap, Is.LessThan(itemRange));
            // a tile of slack for where the body ends up sliding to
            Assert.That(distance, Is.LessThanOrEqualTo(cap + 1f), "the body flew the full item throw range");
        });
    }
}
