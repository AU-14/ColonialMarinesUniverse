using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.Battery;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CMU14.Emplacements;
using Content.Shared.Buckle;
using Content.Shared.CombatMode;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipDoorGunnerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task SeatedOperatorRequiresCombatModeAndLosesControlOnDismount()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.SpawnEntity("CMUM2CDoorGunDropship", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var aim = SEntMan.System<CMUDoorGunAimSystem>();
            var buckle = SEntMan.System<SharedBuckleSystem>();
            var transforms = SEntMan.System<SharedTransformSystem>();
            var combat = SEntMan.System<SharedCombatModeSystem>();
            combat.SetInCombatMode(user, false);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            Assert.That(buckle.TryBuckle(user, user, gun), Is.True);
            Assert.That(combat.IsInCombatMode(user), Is.False);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            combat.SetInCombatMode(user, true);
            Assert.That(aim.TryGetMount(user, out var controlled, out _), Is.True);
            Assert.That(controlled, Is.EqualTo(gun));

            var position = transforms.GetWorldPosition(gun);
            var forward = transforms.GetWorldRotation(gun).ToWorldVec();
            Assert.That(aim.TryGetAimOffset(gun, new EntityCoordinates(map.MapUid, position + forward * 5), out _), Is.True);
            Assert.That(aim.TryGetAimOffset(gun, new EntityCoordinates(map.MapUid, position - forward * 5), out _), Is.False);
            combat.SetInCombatMode(user, false);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            combat.SetInCombatMode(user, true);
            buckle.Unbuckle(user, user);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
        });
    }

    [Test]
    public async Task HeldDoorRejectsClosingOnServerAndClientUntilReleased()
    {
        var map = await Pair.CreateTestMap();
        EntityUid door = default;
        NetEntity doorNet = default;
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            door = SEntMan.SpawnEntity(null, map.GridCoords);
            var held = SEntMan.AddComponent<GunshipDoorGunnerHeldOpenComponent>(door);
            held.HeldOpen = true;
            SEntMan.Dirty(door, held);
            doorNet = SEntMan.GetNetEntity(door);
            AssertClosingCancelled(SEntMan, door, true);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClosingCancelled(CEntMan, CEntMan.GetEntity(doorNet), true));

        await Server.WaitAssertion(() =>
        {
            var held = SEntMan.GetComponent<GunshipDoorGunnerHeldOpenComponent>(door);
            held.HeldOpen = false;
            SEntMan.Dirty(door, held);
            AssertClosingCancelled(SEntMan, door, false);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClosingCancelled(CEntMan, CEntMan.GetEntity(doorNet), false));
    }

    private static void AssertClosingCancelled(IEntityManager entities, EntityUid door, bool expected)
    {
        var closing = new BeforeDoorClosedEvent(false);
        entities.EventBus.RaiseLocalEvent(door, closing);
        Assert.That(closing.Cancelled, Is.EqualTo(expected));
        var autoClose = new BeforeDoorAutoCloseEvent();
        entities.EventBus.RaiseLocalEvent(door, autoClose);
        Assert.That(autoClose.Cancelled, Is.EqualTo(expected));
    }
}
