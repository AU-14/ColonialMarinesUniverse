using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Tracker;
using Content.Shared._RMC14.Tracker.Xeno;
using Content.Shared._RMC14.Weapons.Ranged.Stacks;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Doom;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Light.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Throwing;
using System.Numerics;
using Robust.Shared.Timing;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUServerLogLifetimeTest : GameTest
{
    [Test]
    public async Task DestroyedBlockingItemReleasesVirtualHandImmediately()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var blocker = SEntMan.SpawnAtPosition("Pen", map.GridCoords);
            var virtualItems = Server.System<SharedVirtualItemSystem>();
            Assert.That(virtualItems.TrySpawnVirtualItemInHand(blocker, user, out var item), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().EnumerateHeld(user), Does.Contain(item!.Value));

            SEntMan.DeleteEntity(blocker);

            Assert.That(SEntMan.Deleted(item.Value), Is.True,
                "The fake hand item cannot survive until PVS with a deleted BlockingEntity.");
            Assert.That(Server.System<SharedHandsSystem>().EnumerateHeld(user), Does.Not.Contain(item.Value));
            Assert.That(virtualItems.TrySpawnVirtualItemInHand(blocker, user), Is.False);
        });
    }

    [Test]
    public async Task DeletedHitTargetPreservesGunBonusWithoutSerializingTheTarget()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.SpawnAtPosition("WeaponRifleXM88", map.GridCoords);
            var bullet = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var target = SEntMan.SpawnAtPosition("CMXenoDrone", map.GridCoords);
            var fired = new AmmoShotEvent { FiredProjectiles = new() { bullet } };
            SEntMan.EventBus.RaiseLocalEvent(gun, fired);
            var hit = new ProjectileHitEvent(new DamageSpecifier(), target);
            SEntMan.EventBus.RaiseLocalEvent(bullet, ref hit);
            var stacks = SEntMan.GetComponent<GunStacksActiveComponent>(gun);
            Assert.That(stacks.Hits, Is.EqualTo(1));
            Assert.That(stacks.LastHitEntity, Is.EqualTo(target));

            SEntMan.DeleteEntity(target);

            Assert.That(stacks.LastHitEntity, Is.Null);
            Assert.That(stacks.Hits, Is.EqualTo(1), "Deleting the target must not reset an earned hit bonus.");
            SEntMan.GetComponentState(SEntMan.EventBus, stacks, null, GameTick.Zero);
        });
    }

    [Test]
    public async Task DeletedTrackerTargetIsReleasedBeforeThePeriodicRefresh()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tracker = SEntMan.SpawnAtPosition("CMXenoDrone", map.GridCoords);
            var target = SEntMan.SpawnAtPosition("CMXenoDrone", map.GridCoords);
            SEntMan.EnsureComponent<RMCTrackableComponent>(target);
            var select = new LeaderTrackerSelectTargetEvent(SEntMan.GetNetEntity(target), "Queen");
            SEntMan.EventBus.RaiseLocalEvent(tracker, select);
            var component = SEntMan.GetComponent<HiveTrackerComponent>(tracker);
            Assert.That(component.Target, Is.EqualTo(target));

            SEntMan.DeleteEntity(target);

            Assert.That(component.Target, Is.Null);
            SEntMan.GetComponentState(SEntMan.EventBus, component, null, GameTick.Zero);
        });
    }

    [Test]
    public async Task DeletedNestedVictimIsReleasedBeforeDeferredNestDeletion()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var victim = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var nest = SEntMan.SpawnAtPosition("XenoNest", map.GridCoords);
            var surface = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var surfaceComp = SEntMan.EnsureComponent<XenoNestSurfaceComponent>(surface);
            var component = SEntMan.GetComponent<XenoNestComponent>(nest);
#pragma warning disable RA0002 // Seed an active relationship, then exercise real entity teardown.
            component.Nested = victim;
            component.Surface = surface;
            surfaceComp.Nests[Direction.North] = nest;
            SEntMan.EnsureComponent<XenoNestedComponent>(victim).Nest = nest;
#pragma warning restore RA0002
            SEntMan.DeleteEntity(victim);

            Assert.That(component.Nested, Is.Null);
            Assert.That(surfaceComp.Nests.Values, Does.Not.Contain(nest));
            SEntMan.GetComponentState(SEntMan.EventBus, component, null, GameTick.Zero);
            SEntMan.GetComponentState(SEntMan.EventBus, surfaceComp, null, GameTick.Zero);
        });
    }

    [Test]
    public async Task DestroyedBlackfootSupportReleasesBothEndsOfLinks()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pad = SEntMan.SpawnAtPosition("CMUBlackfootLandingPad", map.GridCoords);
            var pump = SEntMan.SpawnAtPosition("CMUBlackfootFuelPump", map.GridCoords);
            var computer = SEntMan.SpawnAtPosition("CMUBlackfootFlightComputer", map.GridCoords);
            var padComp = SEntMan.GetComponent<BlackfootLandingPadComponent>(pad);
            var computerComp = SEntMan.GetComponent<BlackfootFlightComputerComponent>(computer);
            padComp.FuelPump = pump;
            padComp.Refueling = true;
            SEntMan.GetComponent<BlackfootFuelPumpComponent>(pump).LandingPad = pad;
            computerComp.LandingPad = pad;

            SEntMan.DeleteEntity(pump);
            Assert.That(padComp.FuelPump, Is.Null);
            Assert.That(padComp.Refueling, Is.False);
            SEntMan.GetComponentState(SEntMan.EventBus, padComp, null, GameTick.Zero);

            SEntMan.DeleteEntity(pad);
            Assert.That(computerComp.LandingPad, Is.Null);
            SEntMan.GetComponentState(SEntMan.EventBus, computerComp, null, GameTick.Zero);
        });
    }

    [Test]
    public async Task DoomedGroundFlareBurnsOutAndCompletesComponentRemoval()
    {
        var map = await Pair.CreateTestMap();
        EntityUid flare = default;
        await Server.WaitPost(() =>
        {
            flare = SEntMan.SpawnAtPosition("CMFlare", map.GridCoords);
            SEntMan.EnsureComponent<LightDoomedComponent>(flare);
        });
        await RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<LightDoomedComponent>(flare), Is.False);
            Assert.That(SEntMan.GetComponent<ExpendableLightComponent>(flare).Activated, Is.False);
        });
    }

    [Test]
    public async Task DeletedThrowerIsClearedBeforeSerializationAndReassignedItemsKeepTheirOwner()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var throwing = Server.System<ThrowingSystem>();
            var thrownSystem = Server.System<ThrownItemSystem>();
            var first = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var second = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var item = SEntMan.SpawnAtPosition("Pen", map.GridCoords);
            var reassigned = SEntMan.SpawnAtPosition("Pen", map.GridCoords);
            Assert.That(throwing.TryThrow(item, Vector2.UnitX * 5, user: first, recoil: false), Is.True);
            Assert.That(throwing.TryThrow(reassigned, Vector2.UnitY * 5, user: first, recoil: false), Is.True);
            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            var moved = SEntMan.GetComponent<ThrownItemComponent>(reassigned);
            thrownSystem.SetThrower((reassigned, moved), second);

            SEntMan.DeleteEntity(first);
            Assert.That(thrown.Thrower, Is.Null);
            Assert.That(moved.Thrower, Is.EqualTo(second), "Deleting a previous thrower must not clear a later assignment.");
            SEntMan.GetComponentState(SEntMan.EventBus, thrown, null, GameTick.Zero);
            SEntMan.GetComponentState(SEntMan.EventBus, moved, null, GameTick.Zero);

            SEntMan.DeleteEntity(second);
            Assert.That(moved.Thrower, Is.Null);
            SEntMan.GetComponentState(SEntMan.EventBus, moved, null, GameTick.Zero);
        });
    }

    [Test]
    public async Task DeletedUserCancelsTargetingAndReleasesTargets()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.System<SharedRMCTargetingSystem>();
            var equipment = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var user = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var target = SEntMan.SpawnAtPosition(null, map.GridCoords);
            targeting.Target(equipment, user, target, 30);

            SEntMan.DeleteEntity(user);

            Assert.That(SEntMan.HasComponent<TargetingComponent>(equipment), Is.False,
                "A targeting operation cannot retain its deleted user for network serialization or completion.");
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(target), Is.False,
                "Cancelling the operation must also remove the target's laser marker.");
        });
    }

    [Test]
    public async Task DeletingOneLaserPreservesAnotherWithoutRetainingItsSource()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var targeting = SEntMan.System<SharedRMCTargetingSystem>();
            var removed = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var remaining = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var user = SEntMan.SpawnAtPosition(null, map.GridCoords);
            var target = SEntMan.SpawnAtPosition(null, map.GridCoords);
            targeting.Target(removed, user, target, 30);
            targeting.Target(remaining, user, target, 30);
            targeting.Update(0.1f);

            SEntMan.DeleteEntity(removed);

            var marker = SEntMan.GetComponent<RMCTargetedComponent>(target);
            Assert.That(marker.TargetedBy, Is.EqualTo(new[] { remaining }));
            Assert.That(marker.AlphaMultipliers.Keys, Is.EqualTo(new[] { remaining }));
            SEntMan.GetComponentState(SEntMan.EventBus, marker, null, GameTick.Zero);

            SEntMan.DeleteEntity(target);
            Assert.That(SEntMan.HasComponent<TargetingComponent>(remaining), Is.False,
                "Deleting the last target must release the surviving targeting operation too.");
        });
    }

}
