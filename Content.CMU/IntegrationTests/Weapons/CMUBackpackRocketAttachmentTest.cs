using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Backpack;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUBackpackRocketAttachmentTest : GameTest
{
    private const string RocketSlot = "cmu-aslot-backpack";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task AttachingAndRemovingLauncherUseTheirConfiguredDelays()
    {
        var map = await Pair.CreateTestMap();
        EntityUid marine = default;
        EntityUid backpack = default;
        EntityUid launcher = default;
        await Server.WaitAssertion(() =>
        {
            marine = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            backpack = SEntMan.SpawnAtPosition("CMSatchelMarine", map.GridCoords);
            launcher = SEntMan.SpawnAtPosition("RuMCWeaponLauncherDisposableFolded", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(marine, launcher), Is.True);

            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Server.System<AttachableHolderSystem>().StartAttach((backpack, holder), launcher, marine, RocketSlot);
        });

        await Pair.RunSeconds(1.9f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.False);
        });

        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(marine, backpack), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(backpack, new UseInHandEvent(marine));
        });

        await Pair.RunSeconds(3.9f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.True);
        });

        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.False);
        });
    }

    [Test]
    public async Task WornLauncherCanOnlyBeRemovedByAnotherMarineFromBehind()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid thief = default;
        EntityUid backpack = default;
        await Server.WaitAssertion(() =>
        {
            wearer = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            thief = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            backpack = SEntMan.SpawnAtPosition("CMSatchelMarine", map.GridCoords);
            var launcher = SEntMan.SpawnAtPosition("RuMCWeaponLauncherDisposableFolded", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, backpack, "back", silent: true, force: true), Is.True);
            Assert.That(Server.System<AttachableHolderSystem>().Attach(
                (backpack, SEntMan.GetComponent<AttachableHolderComponent>(backpack)), launcher, wearer, RocketSlot), Is.True);

            SEntMan.EventBus.RaiseLocalEvent(wearer, new InteractHandEvent(wearer, wearer));
            SEntMan.EventBus.RaiseLocalEvent(wearer, new InteractHandEvent(thief, wearer));
        });

        await Pair.RunSeconds(4.1f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(thief, map.GridCoords.Offset(new Vector2(1, 0)));
            SEntMan.EventBus.RaiseLocalEvent(wearer, new InteractHandEvent(thief, wearer));
        });

        await Pair.RunSeconds(4.1f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(thief, map.GridCoords.Offset(new Vector2(-1, 0)));
            SEntMan.EventBus.RaiseLocalEvent(wearer, new InteractHandEvent(thief, wearer));
        });

        await Pair.RunSeconds(3.9f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.True);
        });

        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(backpack);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((backpack, holder), RocketSlot, out _), Is.False);
        });
    }
}
