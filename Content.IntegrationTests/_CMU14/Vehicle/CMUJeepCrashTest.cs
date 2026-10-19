using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Vehicle;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Crashes wear the jeep's wheels by how hard it hit, its engine only head on, never its gun; a wrecked
/// hull stops the jeep where it stands, and only a destroyed engine sets it alight. An ammo box used on
/// the gun jeep loads its gun, and each jeep's key fits it alone.
/// </summary>
[TestFixture]
public sealed class CMUJeepCrashTest : GameTest
{
    private async Task<EntityUid> SpawnJeep(string prototype = "CMUVehicleJeepCargo")
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return jeep;
    }

    private EntityUid Gun(EntityUid jeep)
    {
        return SEntMan.EntityQuery<RefillableByBulletBoxComponent>(true)
            .Select(c => c.Owner)
            .Single(uid => SEntMan.System<SharedTransformSystem>().GetParentUid(uid) is var parent &&
                           (parent == jeep || SEntMan.System<SharedTransformSystem>().GetParentUid(parent) == jeep));
    }

    private bool CanRun(EntityUid jeep)
    {
        var ev = new VehicleCanRunEvent((jeep, SEntMan.GetComponent<VehicleComponent>(jeep)));
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
        return ev.CanRun;
    }

    [Test]
    public void CrashDamageGrowsWithSpeed()
    {
        const float min = 2f, max = 10f, floor = 0.25f;
        float Scale(float speed)
            => CMUVehicleCrashComponent.Scale(speed, min, max, floor);

        Assert.That(Scale(min), Is.EqualTo(floor).Within(0.001f), "The slowest crash that counts does its floor.");
        Assert.That(Scale(max), Is.EqualTo(1f).Within(0.001f), "A top speed crash does all of it.");
        Assert.That(Scale(max * 2f), Is.EqualTo(1f).Within(0.001f), "Faster than top speed does no more.");

        var last = Scale(min);
        for (var speed = min + 0.5f; speed <= max; speed += 0.5f)
        {
            Assert.That(Scale(speed), Is.GreaterThan(last), $"A crash at {speed} should hit harder than a slower one.");
            last = Scale(speed);
        }
    }

    [Test]
    public void HeadOnMeansTheFrontTouches()
    {
        // The jeep's hull, facing south: the front is -y.
        var hull = new Box2(-0.45f, -1.05f, 0.45f, 1.05f);
        foreach (var degrees in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            var rotation = Angle.FromDegrees(degrees);
            Box2 At(float x0, float y0, float x1, float y1)
            {
                var a = rotation.RotateVec(new System.Numerics.Vector2(x0, y0));
                var b = rotation.RotateVec(new System.Numerics.Vector2(x1, y1));
                return Box2.FromTwoPoints(a, b);
            }

            var position = System.Numerics.Vector2.Zero;
            Assert.That(CMUVehicleCrashComponent.IsFrontContact(position, rotation, hull, At(-0.5f, -2f, 0.5f, -1.05f)), Is.True, $"wall ahead, {degrees}");
            Assert.That(CMUVehicleCrashComponent.IsFrontContact(position, rotation, hull, At(0f, -4f, 3f, -1.05f)), Is.True, $"wide obstacle met half on, {degrees}");
            Assert.That(CMUVehicleCrashComponent.IsFrontContact(position, rotation, hull, At(0.45f, -0.8f, 1.5f, 0.8f)), Is.False, $"beside, {degrees}");
            Assert.That(CMUVehicleCrashComponent.IsFrontContact(position, rotation, hull, At(-0.5f, 1.05f, 0.5f, 2f)), Is.False, $"behind, {degrees}");
        }
    }

    [Test]
    public async Task OnlyHeadOnCrashesWearTheEngine()
    {
        var jeep = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep);
            var before = comp.EngineIntegrity;
            var side = new CMUVehicleCrashedEvent(30f, false);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref side);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(before), "A side or rear crash shouldn't touch the engine.");

            var front = new CMUVehicleCrashedEvent(30f, true);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref front);
            Assert.That(comp.EngineIntegrity, Is.LessThan(before), "A head on crash wears the engine.");
        });
    }

    [Test]
    public async Task DirectHitsOnTheEngineBayWearTheEngineFully()
    {
        var jeep = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            // Facing south: the hood is toward -y.
            var ahead = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, -2.5f));
            var beside = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 2.5f, 0.75f));
            var behind = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, 3.5f));
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = 10;
            var damageable = SEntMan.System<DamageableSystem>();

            var hull = SEntMan.GetComponent<Content.Shared._RMC14.Vehicle.HardpointIntegrityComponent>(jeep);
            var indirect = 0f;
            foreach (var origin in new EntityUid?[] { null, beside, behind })
            {
                var before = comp.EngineIntegrity;
                var hullBefore = hull.Integrity;
                damageable.TryChangeDamage(jeep, damage, origin: origin);
                Assert.That(comp.EngineIntegrity, Is.LessThan(before), "The engine soaks up some of any hit.");
                Assert.That(hull.Integrity, Is.LessThan(hullBefore), "The rest goes on to the hull.");
                indirect = before - comp.EngineIntegrity;
            }

            var direct = comp.EngineIntegrity;
            var hullDirect = hull.Integrity;
            damageable.TryChangeDamage(jeep, damage, origin: ahead);
            Assert.That(direct - comp.EngineIntegrity, Is.GreaterThan(indirect),
                "A hit on the hood wears the engine more than one elsewhere.");
            Assert.That(hull.Integrity, Is.EqualTo(hullDirect), "The engine soaks all of a direct hit.");
        });
    }

    [Test]
    public async Task EngineSmokeReachesClientsAsItIsShot()
    {
        var jeep = await SpawnJeep();
        float smoke = 0f, heavy = 0f;
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            var ahead = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, -2.5f));
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = 5;
            var damageable = SEntMan.System<DamageableSystem>();
            smoke = comp.EngineSmokeFraction * comp.EngineMaxIntegrity;
            heavy = comp.EngineHeavySmokeFraction * comp.EngineMaxIntegrity;
            while (comp.EngineIntegrity >= smoke)
                damageable.TryChangeDamage(jeep, damage, origin: ahead);
        });
        await RunTicksSync(10);

        var client = CEntMan.GetComponent<CMUOpenVehicleComponent>(ToClientUid(jeep));
        Assert.That(client.EngineIntegrity, Is.LessThan(smoke).And.GreaterThanOrEqualTo(heavy),
            "Clients see the engine smoke as soon as the hits wear it down, before it burns.");
    }

    [Test]
    public async Task WreckedHullStopsTheJeepWithoutFire()
    {
        var jeep = await SpawnJeep();
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<HardpointSystem>().DamageVehicleHull(jeep, 1000f), Is.True);
        });
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<CMUOpenVehicleSystem>().IsHullWrecked(jeep), Is.True);
            Assert.That(CanRun(jeep), Is.False, "A wrecked hull should stop the jeep.");
            Assert.That(SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).Burning, Is.False, "A wrecked hull alone shouldn't set it alight.");
        });

        await RunSeconds((float) SEntMan.GetComponent<CMUVehicleWreckComponent>(jeep).Delay.TotalSeconds + 2f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(jeep), Is.False, "A wrecked hull alone shouldn't blow the jeep up."));
    }

    [Test]
    public async Task CrashesSpareTheGun()
    {
        var jeep = await SpawnJeep("CMUVehicleJeepGunner");
        await Server.WaitAssertion(() =>
        {
            var gun = Gun(jeep);
            var integrity = SEntMan.GetComponent<Content.Shared._RMC14.Vehicle.HardpointIntegrityComponent>(gun);
            var before = integrity.Integrity;
            Assert.That(SEntMan.System<HardpointSystem>().CMUDamageHullWheels(jeep, 50f), Is.True);
            Assert.That(integrity.Integrity, Is.EqualTo(before), "A crash shouldn't touch the gun.");
        });
    }

    [Test]
    public async Task AmmoBoxOnTheJeepLoadsItsGun()
    {
        var jeep = await SpawnJeep("CMUVehicleJeepGunner");
        EntityUid user = default;
        EntityUid gun = default;
        await Server.WaitPost(() =>
        {
            gun = Gun(jeep);
            var ammo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun);
            SEntMan.System<SharedGunSystem>().SetBallisticUnspawned((gun, ammo), 0);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, 1.45f, -0.3f));
            var box = SEntMan.SpawnEntity("VehicleAmmoBoxCupola", new EntityCoordinates(grid, 1.45f, -0.3f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, box), Is.True);
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(jeep).Coordinates, jeep);
        });
        await RunSeconds(3f);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun).Count, Is.GreaterThan(0),
                "An ammo box used on the gun jeep should load its gun."));
    }

    [Test]
    public async Task EachJeepsKeyFitsItAlone()
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;
        await Server.WaitPost(() =>
        {
            first = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            second = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 4.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var firstIgnition = SEntMan.GetComponent<CMUVehicleIgnitionComponent>(first);
            var secondIgnition = SEntMan.GetComponent<CMUVehicleIgnitionComponent>(second);
            Assert.That(firstIgnition.KeyCode, Is.Not.Zero);
            Assert.That(firstIgnition.KeyCode, Is.Not.EqualTo(secondIgnition.KeyCode));

            Assert.That(slots.TryEject(first, firstIgnition.KeySlot, null, out var firstKey), Is.True);
            Assert.That(slots.TryEject(second, secondIgnition.KeySlot, null, out var secondKey), Is.True);
            Assert.That(slots.TryInsert(second, CMUVehicleIgnitionComponent.SlotId, firstKey!.Value, null), Is.False,
                "Another jeep's key shouldn't fit.");
            Assert.That(slots.TryInsert(first, CMUVehicleIgnitionComponent.SlotId, firstKey.Value, null), Is.True);
            Assert.That(slots.TryInsert(second, CMUVehicleIgnitionComponent.SlotId, secondKey!.Value, null), Is.True);
        });
    }

    /// <summary>
    /// Where the client draws the gun's muzzle flash: the barrel tip on screen.
    /// </summary>
    private async Task<Vector2> FlashAt(EntityUid gun)
    {
        var at = Vector2.Zero;
        await Client.WaitAssertion(() =>
        {
            var muzzle = Client.System<VehicleTurretMuzzleOffsetSystem>();
            Assert.That(muzzle.TryGetGunPose(ToClientUid(gun), null, out var origin, out _), Is.True);
            at = Client.System<SharedTransformSystem>().ToMapCoordinates(origin).Position;
        });
        return at;
    }

    /// <summary>
    /// Swings the gun round to the jeep's front left, the way it follows the gunner's cursor, and
    /// gives the turret and the client time to catch up.
    /// </summary>
    private async Task<Vector2> AimAhead(EntityUid jeep)
    {
        var target = Vector2.Zero;
        await Server.WaitPost(() =>
        {
            var (position, rotation) = SEntMan.System<SharedTransformSystem>().GetWorldPositionRotation(jeep);
            // Facing south, ahead is -y. Far off, so the jeep and its gun see it at the same angle.
            target = position + rotation.RotateVec(new Vector2(30f, -40f));
            var turrets = SEntMan.System<VehicleTurretSystem>();
            Assert.That(turrets.TrySetTargetRotationWorld(Gun(jeep), (target - position).ToWorldAngle()), Is.True);
        });
        await RunSeconds(2f);
        return target;
    }

    [Test]
    public async Task GunnersBulletLeavesWhereTheMuzzleFlashShows()
    {
        var jeep = await SpawnJeep("CMUVehicleJeepGunner");
        EntityUid gunner = default;
        await Server.WaitPost(() =>
        {
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            gunner = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, 0.5f, 1.5f));
        });
        var target = await AimAhead(jeep);
        var flash = await FlashAt(Gun(jeep));

        await Server.WaitAssertion(() =>
        {
            var xforms = SEntMan.System<SharedTransformSystem>();
            var gun = Gun(jeep);
            var map = xforms.GetMapCoordinates(jeep).MapId;
            var shot = SEntMan.System<SharedGunSystem>().AttemptShoot((gun, SEntMan.GetComponent<GunComponent>(gun)), gunner,
                xforms.ToCoordinates(new MapCoordinates(target, map)));
            Assert.That(shot, Is.Not.Null.And.Not.Empty, "The gunner should fire.");
            var from = xforms.GetWorldPosition(shot![0]);
            Assert.That((from - flash).Length(), Is.LessThan(0.35f),
                $"The bullet should leave the barrel tip where the flash shows, {flash}, not {from}.");
        });
    }

    [Test]
    public async Task ShotsFollowTheBarrelTipRoundTheJeep()
    {
        var jeep = await SpawnJeep("CMUVehicleJeepGunner");
        var tips = new List<Vector2>();
        foreach (var degrees in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            await Server.WaitPost(() =>
                SEntMan.System<SharedTransformSystem>().SetLocalRotation(jeep, Angle.FromDegrees(degrees)));
            var target = await AimAhead(jeep);
            var flash = await FlashAt(Gun(jeep));

            await Server.WaitAssertion(() =>
            {
                var xforms = SEntMan.System<SharedTransformSystem>();
                var gun = Gun(jeep);
                var position = xforms.GetWorldPosition(jeep);
                var map = xforms.GetMapCoordinates(jeep).MapId;
                var ev = new AttemptShootEvent(gun, null, xforms.ToCoordinates(new MapCoordinates(position, map)),
                    xforms.ToCoordinates(new MapCoordinates(target, map)));
                SEntMan.EventBus.RaiseLocalEvent(gun, ref ev);
                var from = xforms.ToMapCoordinates(ev.FromCoordinates).Position;
                Assert.That((from - flash).Length(), Is.LessThan(0.05f), $"facing {degrees}: shots start where the flash shows.");
                tips.Add(from - position);
            });
        }

        Assert.That(tips.Distinct().Count(), Is.EqualTo(tips.Count), "The tip moves as the jeep turns.");
    }
}
