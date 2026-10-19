using Content.Shared._RMC14.Fireman;
using Content.Shared.DragDrop;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Linq;
using ServerHandsSystem = Content.Server.Hands.Systems.HandsSystem;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14: Yautja can aggressively grab, carry and throw xenomorphs, matching CM-SS13's grab gate at
// code/modules/mob/mob_grab.dm:82-89 - max_grab_size is the user's mob_size raised once by TRAIT_DEXTROUS and once
// by TRAIT_SUPER_STRONG, so a Yautja reaches MOB_SIZE_XENO (3). Castes at MOB_SIZE_BIG (Ravager, Praetorian) and
// MOB_SIZE_IMMOBILE (Crusher, Queen) exceed that and stay out of reach.
[TestFixture]
public sealed class YautjaXenoGrabTest
{
    [Test]
    public async Task HunterAggressivelyGrabsCarriableXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid xeno = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(hunter, xeno), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(hunter, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<FiremanCarrySystem>().IsAggressivelyGrabbed(xeno), Is.True,
                "A drone is MOB_SIZE_XENO, which a Yautja reaches via TRAIT_SUPER_STRONG and TRAIT_DEXTROUS."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterCannotAggressivelyGrabOversizedXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid xeno = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            xeno = entMan.SpawnEntity("CMXenoRavager", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(hunter, xeno), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(hunter, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<FiremanCarrySystem>().IsAggressivelyGrabbed(xeno), Is.False,
                "A Ravager is MOB_SIZE_BIG and exceeds the Yautja grab limit."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterCarriesAndThrowsGrabbedXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid xeno = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(hunter, xeno), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));

        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(hunter, ref toggle);
        });
        await pair.RunTicksSync(5);

        // drag the grabbed xeno onto yourself, the same way a player starts a fireman carry
        await server.WaitPost(() =>
        {
            var drag = new DragDropDraggedEvent(hunter, hunter);
            server.EntMan.EventBus.RaiseLocalEvent(xeno, ref drag);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(8));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<FiremanCarriableComponent>(xeno).BeingCarried, Is.True,
                "the drone was never carried");
            Assert.That(entMan.HasComponent<Content.Shared.Stunnable.KnockedDownComponent>(xeno), Is.True,
                "a carried xeno must be in the downed/grabbed pose, the same state an aggressive grab gives it");

            var hands = entMan.System<SharedHandsSystem>();
            var carryHand = hands.EnumerateHands(hunter).First(hand =>
                hands.GetHeldItem(hunter, hand) is { } held &&
                entMan.TryGetComponent(held, out VirtualItemComponent? virtualItem) &&
                virtualItem.BlockingEntity == xeno);
            hands.SetActiveHand(hunter, carryHand);

            var target = entMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(new System.Numerics.Vector2(3, 0));
            Assert.That(entMan.System<ServerHandsSystem>().ThrowHeldItem(hunter, target), Is.True,
                "a Yautja couldn't throw the drone it was carrying");
            Assert.That(entMan.GetComponent<FiremanCarriableComponent>(xeno).BeingCarried, Is.False);
        });

        // The carry's knockdown is removed with RemCompDeferred, so give it a tick before asserting the release.
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.HasComponent<Content.Shared.Stunnable.KnockedDownComponent>(xeno), Is.False,
                "the knockdown applied by the carry is released once the xeno is thrown"));

        await pair.CleanReturnAsync();
    }
}
