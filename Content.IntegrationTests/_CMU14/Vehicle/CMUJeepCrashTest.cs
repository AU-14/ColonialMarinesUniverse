using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.CMU14.Vehicle.Jeep;
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
        // The jeep's wall smash: 30 hull, from 5 tiles/s, top speed 8.
        Assert.That(30f * CMUVehicleCrashComponent.Scale(5f, 5f, 8f, 1f / 3f), Is.EqualTo(10f).Within(0.01f));
        Assert.That(30f * CMUVehicleCrashComponent.Scale(6.5f, 5f, 8f, 1f / 3f), Is.EqualTo(20f).Within(0.01f));
        Assert.That(30f * CMUVehicleCrashComponent.Scale(8f, 5f, 8f, 1f / 3f), Is.EqualTo(30f).Within(0.01f));
        Assert.That(30f * CMUVehicleCrashComponent.Scale(12f, 5f, 8f, 1f / 3f), Is.EqualTo(30f).Within(0.01f));
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
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            var before = comp.EngineIntegrity;
            var side = new CMUVehicleCrashedEvent(30f, false);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref side);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(before), "A side or rear crash shouldn't touch the engine.");

            var front = new CMUVehicleCrashedEvent(30f, true);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref front);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(before - 30f * comp.EngineCrashShare).Within(0.01f));
        });
    }

    [Test]
    public async Task DirectHitsOnTheEngineBayWearTheEngineFully()
    {
        var jeep = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            // Facing south: the hood is toward -y.
            var ahead = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, -2.5f));
            var beside = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 2.5f, 0.75f));
            var behind = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, 3.5f));
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = 10;
            var damageable = SEntMan.System<DamageableSystem>();

            var hull = SEntMan.GetComponent<Content.Shared._RMC14.Vehicle.HardpointIntegrityComponent>(jeep);
            foreach (var origin in new EntityUid?[] { null, beside, behind })
            {
                var before = comp.EngineIntegrity;
                var hullBefore = hull.Integrity;
                damageable.TryChangeDamage(jeep, damage, origin: origin);
                Assert.That(comp.EngineIntegrity, Is.EqualTo(before - 10f * comp.EngineDamageShare).Within(0.01f),
                    "The engine soaks up a fifth of any hit.");
                Assert.That(hull.Integrity, Is.LessThan(hullBefore), "The rest goes on to the hull.");
            }

            var direct = comp.EngineIntegrity;
            var hullDirect = hull.Integrity;
            damageable.TryChangeDamage(jeep, damage, origin: ahead);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(direct - 10f * comp.EngineDirectHitShare).Within(0.01f),
                "A hit on the hood goes into the engine.");
            Assert.That(hull.Integrity, Is.EqualTo(hullDirect), "The engine soaks all of a direct hit.");
        });
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
            Assert.That(SEntMan.System<CMUJeepSystem>().IsHullWrecked(jeep), Is.True);
            Assert.That(CanRun(jeep), Is.False, "A wrecked hull should stop the jeep.");
            Assert.That(SEntMan.GetComponent<CMUJeepComponent>(jeep).Burning, Is.False, "A wrecked hull alone shouldn't set it alight.");
        });

        await RunSeconds(8f);
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

    [Test]
    public async Task GunFiresFromItsPlaceOnTheJeep()
    {
        var jeep = await SpawnJeep("CMUVehicleJeepGunner");
        foreach (var degrees in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            await Server.WaitAssertion(() =>
            {
                var xforms = SEntMan.System<SharedTransformSystem>();
                xforms.SetLocalRotation(jeep, Angle.FromDegrees(degrees));
                var gun = Gun(jeep);
                var origin = SEntMan.GetComponent<CMUVehicleGunOriginComponent>(gun);
                var (position, rotation) = xforms.GetWorldPositionRotation(jeep);
                var pivot = position + rotation.RotateVec(origin.Pivot);
                // Aim straight out to the jeep's right.
                var target = pivot + rotation.RotateVec(new System.Numerics.Vector2(5f, 0f));
                var map = xforms.GetMapCoordinates(jeep).MapId;
                var ev = new AttemptShootEvent(gun, null, xforms.ToCoordinates(new MapCoordinates(pivot, map)),
                    xforms.ToCoordinates(new MapCoordinates(target, map)));
                SEntMan.EventBus.RaiseLocalEvent(gun, ref ev);
                var from = xforms.ToMapCoordinates(ev.FromCoordinates).Position;
                var expected = pivot + rotation.RotateVec(new System.Numerics.Vector2(origin.Barrel, 0f));
                Assert.That((from - expected).Length(), Is.LessThan(0.01f), $"facing {degrees}");
            });
        }
    }
}
