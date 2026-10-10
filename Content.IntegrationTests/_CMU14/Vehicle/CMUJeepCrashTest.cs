using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
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
/// Crashes wear the jeep's wheels by how hard it hit and its engine a little, never its gun; a wrecked
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
    public async Task CrashWearsTheEngineALittle()
    {
        var jeep = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            var before = comp.EngineIntegrity;
            var ev = new CMUVehicleCrashedEvent(30f);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(before - 30f * comp.EngineCrashShare).Within(0.01f));
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
