using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUTankGrenadeAimTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task GrenadeKeepsBarrelDirectionWhileTurretIsStillTurning()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(tank, "primary", out var primary), Is.True);
            var turret = primary!.Item!.Value;
            var launcher = SEntMan.SpawnEntity("VehicleTankGrenadeLauncher", map.GridCoords);
            Assert.That(slots.TryInsert(turret, "turret-launcher", launcher, null), Is.True);
            var turretComp = SEntMan.GetComponent<VehicleTurretComponent>(turret);
#pragma warning disable RA0002 // Set a controlled mid-turn fixture without advancing the physics simulation.
            turretComp.WorldRotation = Angle.FromDegrees(45);
            turretComp.TargetRotation = turretComp.WorldRotation;
#pragma warning restore RA0002
            Server.System<VehicleTurretSystem>().Update(0);
            var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5, 5)));
            var origin = transform.GetMapCoordinates(launcher);
            var barrelDirection = transform.GetWorldRotation(turret).ToWorldVec();
            var cursorDirection = Angle.FromDegrees(40).RotateVec(barrelDirection);
            var cursor = new MapCoordinates(origin.Position + cursorDirection * 8, origin.MapId);

            var shots = Server.System<SharedGunSystem>().AttemptShoot(
                (launcher, SEntMan.GetComponent<GunComponent>(launcher)), gunner, transform.ToCoordinates(cursor));
            Assert.That(shots, Has.Count.EqualTo(1), "The launcher must actually fire a grenade.");
            var grenade = shots![0];
            var velocity = SEntMan.GetComponent<PhysicsComponent>(grenade).LinearVelocity;
            Assert.That(Vector2.Dot(Vector2.Normalize(velocity), barrelDirection), Is.GreaterThan(0.999f),
                "The fixed-distance controller must not turn a barrel-constrained shot back toward the cursor.");
            var stop = SEntMan.GetComponent<ProjectileFixedDistanceComponent>(grenade).TargetCoordinates!.Value;
            var flight = stop.Position - transform.GetWorldPosition(grenade);
            Assert.That(Vector2.Dot(Vector2.Normalize(flight), barrelDirection), Is.GreaterThan(0.999f),
                "The landing point and the initial grenade velocity must use the same final firing direction.");
            Assert.That(PreventsContact(grenade, tank), Is.True,
                "A launched grenade must ignore its own hull even though it is a thrown item, not a bullet.");
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(8, 8)));
            Assert.That(PreventsContact(grenade, wall), Is.False,
                "Filtering the launcher's hull must not let grenades arc through other impassable structures.");
        });
    }

    [Test]
    public async Task DiagonalLauncherMuzzleStaysAlignedWhenCoordinateParentRotates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(tank, "primary", out var primary), Is.True);
            var turret = primary!.Item!.Value;
            var launcher = SEntMan.SpawnEntity("VehicleTankGrenadeLauncher", map.GridCoords);
            Assert.That(slots.TryInsert(turret, "turret-launcher", launcher, null), Is.True);
            var turretComp = SEntMan.GetComponent<VehicleTurretComponent>(turret);
#pragma warning disable RA0002 // Set the same diagonal world pose before changing only the coordinate reference frame.
            turretComp.WorldRotation = Angle.FromDegrees(45);
            turretComp.TargetRotation = turretComp.WorldRotation;
#pragma warning restore RA0002
            Server.System<VehicleTurretSystem>().Update(0);
            var muzzle = Server.System<GunMuzzleOffsetSystem>();
            var muzzleComp = SEntMan.GetComponent<GunMuzzleOffsetComponent>(launcher);
            var aim = new EntityCoordinates(turret, new Vector2(8, 0));
            Assert.That(muzzle.TryGetMuzzleCoordinates(launcher, muzzleComp, aim, out var before, out _), Is.True);
            var worldBefore = transform.ToMapCoordinates(before).Position;
            var tankPosition = transform.GetWorldPosition(tank);
            var tankRotation = transform.GetWorldRotation(tank);

            transform.SetLocalRotation(map.Grid.Owner, Angle.FromDegrees(90));
            transform.SetWorldPosition(tank, tankPosition);
            transform.SetWorldRotation(tank, tankRotation);
            Assert.That(muzzle.TryGetMuzzleCoordinates(launcher, muzzleComp, aim, out var after, out _), Is.True);
            var worldAfter = transform.ToMapCoordinates(after).Position;
            Assert.That(Vector2.Distance(worldAfter, worldBefore), Is.LessThan(0.001f),
                "Changing the coordinate parent's rotation while preserving the tank's world pose must preserve the muzzle's world position.");
        });
    }

    [Test]
    public async Task MinigunRoundsPassDeadVictimsButStillHitLivingVictims()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.SpawnEntity("VehicleBulletLTAAAP", map.GridCoords);
            var firstVictim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var nextVictim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            Assert.That(PreventsContact(round, firstVictim), Is.False);
            Server.System<MobStateSystem>().ChangeMobState(firstVictim, MobState.Dead);
            Assert.That(PreventsContact(round, firstVictim), Is.True,
                "A killed victim must no longer absorb following LTAA rounds.");
            Assert.That(PreventsContact(round, nextVictim), Is.False,
                "The living target behind the corpse must remain hittable.");
        });
    }

    private bool PreventsContact(EntityUid projectile, EntityUid other)
    {
        var args = new PreventCollideEvent(projectile, other,
            SEntMan.GetComponent<PhysicsComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(other),
            SEntMan.GetComponent<FixturesComponent>(projectile).Fixtures.Values.First(),
            SEntMan.GetComponent<FixturesComponent>(other).Fixtures.Values.First(f => f.Hard));
        SEntMan.EventBus.RaiseLocalEvent(projectile, ref args);
        return args.Cancelled;
    }
}
