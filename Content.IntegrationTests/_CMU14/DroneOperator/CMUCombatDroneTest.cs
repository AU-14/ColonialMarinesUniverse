using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUCombatDroneTest
{
    [TestCase(0, 0, -1, true)]
    [TestCase(0, 1, 0, true)]
    [TestCase(0, -1, 0, true)]
    [TestCase(0, 1, 0.01f, false)]
    [TestCase(0, 0, 1, false)]
    [TestCase(90, 1, 0, true)]
    [TestCase(90, -1, 0, false)]
    [TestCase(180, 0, 1, true)]
    [TestCase(180, 0, -1, false)]
    [TestCase(270, -1, 0, true)]
    [TestCase(359, 0, -1, true)]
    [TestCase(0, 0, 0, false)]
    public void FiringArcIncludesSideBoundariesButRejectsRear(float heading, float x, float y, bool allowed)
    {
        Assert.That(CMUCombatDroneSystem.IsWithinFireArc(Angle.FromDegrees(heading), new Vector2(x, y)), Is.EqualTo(allowed));
    }

    [Test]
    public async Task AssemblePilotFireAndReloadWithIFF()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid user = default, tablet = default, hull = default, turret = default, ammo = default;
        EntityUid drone = default, mind = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            hull = entities.SpawnEntity("CMUCombatDroneHull", map.GridCoords.Offset(new Vector2(0.8f, 0)));
            turret = entities.SpawnEntity("CMUCombatDroneTurretAssembly", map.GridCoords);
            ammo = entities.SpawnEntity("CMUCombatDroneAmmoBox", map.GridCoords);
            entities.GetComponent<CMUCombatDroneHullComponent>(hull).AssemblyDelay = TimeSpan.FromSeconds(0.1);
            Assert.That(hands.TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, ammo, checkActionBlocker: false), Is.True);
            Interact(entities, user, ammo, hull);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(entities.GetComponent<CMUDroneOperatorComponent>(user).Drone, Is.Null,
                "Ammunition cannot activate a hull without a turret.");
            Assert.That(hands.TryDrop(user, ammo, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, turret, checkActionBlocker: false), Is.True);
            Interact(entities, user, turret, hull);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var slot = containers.GetContainer(hull, entities.GetComponent<CMUCombatDroneHullComponent>(hull).TurretContainerId);
            Assert.That(slot.ContainedEntities, Does.Contain(turret));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, ammo, checkActionBlocker: false), Is.True);
            Interact(entities, user, ammo, hull);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            drone = entities.GetComponent<CMUDroneOperatorComponent>(user).Drone!.Value;
            Assert.That(entities.EntityExists(hull), Is.False);
            Assert.That(entities.EntityExists(turret), Is.False);
            Assert.That(entities.GetComponent<CMUDroneControlTabletComponent>(tablet).LinkedDrone, Is.EqualTo(drone));
            Assert.That(entities.System<SharedContainerSystem>().GetContainer(drone, SharedGunSystem.MagazineSlot).ContainedEntities,
                Is.EqualTo(new[] { ammo }), "Assembly must transfer the supplied box, not spawn free ammunition.");
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));

            var minds = entities.System<SharedMindSystem>();
            mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, user);
            var use = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(tablet, use);
            Assert.That(use.Handled, Is.True);
        });

        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            Assert.That(entities.HasComponent<CMUDroneControlSessionComponent>(drone), Is.True);
            Assert.That(entities.HasComponent<CMURemotePilotingComponent>(user), Is.True);
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(drone, Angle.Zero);
            var coords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            var gun = entities.GetComponent<GunComponent>(drone);
            Assert.That(gun.FireRateModified, Is.EqualTo(4));
            Assert.That(gun.CameraRecoilScalarModified, Is.EqualTo(0.2f).Within(0.001f));
            var guns = entities.System<SharedGunSystem>();
            Assert.That(guns.AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, 5))), Is.Null);
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200), "Rejected rear shots must not consume ammunition.");
            var shots = guns.AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, -5)));
            Assert.That(shots, Has.Count.EqualTo(1));
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(199));
            var bullet = shots!.Single();
            Assert.That(entities.GetComponent<MetaDataComponent>(bullet).EntityPrototype!.ID, Is.EqualTo("BulletRifle10x24mm"));

            var iff = entities.System<GunIFFSystem>();
            iff.SetUserFaction((user, null), "GOVFOR");
            var friendlyCollision = new PreventCollideEvent(bullet, user,
                entities.GetComponent<PhysicsComponent>(bullet), entities.GetComponent<PhysicsComponent>(user), null!, null!);
            entities.EventBus.RaiseLocalEvent(bullet, ref friendlyCollision);
            Assert.That(friendlyCollision.Cancelled, Is.True, "Live projectiles must pass through a matching friendly IFF.");
            iff.SetUserFaction((user, null), "OPFOR");
            var hostileCollision = new PreventCollideEvent(bullet, user,
                entities.GetComponent<PhysicsComponent>(bullet), entities.GetComponent<PhysicsComponent>(user), null!, null!);
            entities.EventBus.RaiseLocalEvent(bullet, ref hostileCollision);
            Assert.That(hostileCollision.Cancelled, Is.False);

            var slots = entities.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject((drone, null), SharedGunSystem.MagazineSlot, null, out var ejected), Is.True);
            Assert.That(ejected, Is.EqualTo(ammo));
            var ap = entities.SpawnEntity("CMUCombatDroneAmmoBoxAP", map.GridCoords);
            Assert.That(slots.TryInsert((drone, null), SharedGunSystem.MagazineSlot, ap, null), Is.True);
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            var gun = entities.GetComponent<GunComponent>(drone);
            var shots = entities.System<SharedGunSystem>().AttemptShoot((drone, gun), drone, coords.Offset(new Vector2(0, -5)));
            Assert.That(shots, Has.Count.EqualTo(1));
            Assert.That(entities.GetComponent<MetaDataComponent>(shots!.Single()).EntityPrototype!.ID, Is.EqualTo("BulletRifle10x24mmAP"));
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(199));
            Assert.That(entities.System<SharedHandsSystem>().TryDrop(user, tablet, checkActionBlocker: false), Is.True);
        });

        // Hand drops are also detected by the controller's half-second session validator.
        await server.WaitRunTicks(40);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null);
            Assert.That(server.EntMan.HasComponent<CMUDroneControlSessionComponent>(drone), Is.False);
            Assert.That(server.EntMan.HasComponent<CMURemotePilotingComponent>(user), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireproofButAcidDamageNeedsWiresAndFrameDamageNeedsWelding()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid drone = default, user = default, cable = default, welder = default;
        float fuelBefore = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            drone = entities.SpawnEntity("CMUCombatDrone", map.GridCoords.Offset(new Vector2(0.8f, 0)));
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            cable = entities.SpawnEntity("RMCCableCoil30", map.GridCoords);
            welder = entities.SpawnEntity("CMWelder", map.GridCoords);
            entities.GetComponent<CMUCombatDroneComponent>(drone).RepairDelay = TimeSpan.FromSeconds(0.1);
            var fire = new RMCGetFireImmunityEvent(null);
            entities.EventBus.RaiseLocalEvent(drone, ref fire);
            Assert.That(fire.Immune, Is.True);
            Assert.That(fire.Ignite, Is.False);
            var ignite = new RMCIgniteAttemptEvent();
            entities.EventBus.RaiseLocalEvent(drone, ignite);
            Assert.That(ignite.Cancelled, Is.True);

            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 35);
            damage.DamageDict.Add("Slash", 5);
            damage.DamageDict.Add("Heat", 15);
            damage.DamageDict.Add("Caustic", 25);
            entities.System<DamageableSystem>().TryChangeDamage(drone, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.That(entities.HasComponent<CMUCombatDroneSparkingComponent>(drone), Is.True);
            AssertDamage(entities, drone, 40, 40);
            var examine = new ExaminedEvent(new FormattedMessage(), drone, user, true, false);
            entities.EventBus.RaiseLocalEvent(drone, examine);
            var description = examine.GetTotalMessage().ToString();
            Assert.That(description, Does.Contain("dented"));
            Assert.That(description, Does.Contain("Burnt wires"));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, cable, checkActionBlocker: false), Is.True);
            Interact(entities, user, cable, drone);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            AssertDamage(entities, drone, 40, 10);
            Assert.That(entities.System<SharedStackSystem>().GetCount((cable, null)), Is.EqualTo(25));
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder, checkActionBlocker: false), Is.True);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((welder, null), user), Is.True);
            fuelBefore = entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(welder).fuel.Float();
            Interact(entities, user, welder, drone);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            AssertDamage(entities, drone, 10, 10);
            Assert.That(entities.System<SharedToolSystem>().GetWelderFuelAndCapacity(welder).fuel.Float(), Is.LessThanOrEqualTo(fuelBefore - 5));
            Interact(entities, user, cable, drone);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            AssertDamage(entities, drone, 10, 0);
            Assert.That(entities.HasComponent<CMUCombatDroneSparkingComponent>(drone), Is.False);
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 190);
            entities.System<DamageableSystem>().TryChangeDamage(drone, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            Assert.That(entities.System<MobStateSystem>().IsDead(drone), Is.True);
            Interact(entities, user, welder, drone);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<MobStateSystem>().IsAlive(drone), Is.True,
                "A disabled drone must come back online when repaired below its damage threshold.");
            AssertDamage(server.EntMan, drone, 170, 0);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FieldPackContainsAssemblyKitAndDrivingDroneTakesRealAcidButNotFire()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid drone = default, fire = default;
        Vector2 before = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var pack = entities.SpawnEntity("CMUCombatDroneOperatorPackFilled", map.GridCoords);
            var contents = entities.GetComponent<StorageComponent>(pack).Container.ContainedEntities;
            Assert.That(contents.Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[] { "CMUDroneControlTablet", "CMUCombatDroneHull", "CMUCombatDroneTurretAssembly",
                    "CMUCombatDroneAmmoBox", "CMUCombatDroneAmmoBoxAP", "CMWelder", "RMCCableCoil30" }));
            drone = entities.SpawnEntity("CMUCombatDrone", map.GridCoords);
            before = entities.GetComponent<TransformComponent>(drone).Coordinates.Position;
            var input = entities.GetComponent<InputMoverComponent>(drone);
            input.HeldMoveButtons = MoveButtons.Right;
            var movement = new MoveInputEvent((drone, input), MoveButtons.None);
            entities.EventBus.RaiseLocalEvent(drone, ref movement);
            var turn = new ChangeDirectionAttemptEvent(drone);
            entities.EventBus.RaiseLocalEvent(drone, turn);
            Assert.That(turn.Cancelled, Is.True, "Cursor facing must not turn the hull.");
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coords = entities.GetComponent<TransformComponent>(drone).Coordinates;
            Assert.That(coords.X, Is.GreaterThan(before.X + 0.1f), "Driving input must actually move the drone.");
            Assert.That(entities.System<SharedTransformSystem>().GetWorldRotation(drone).Degrees, Is.EqualTo(90).Within(0.01));
            entities.GetComponent<InputMoverComponent>(drone).HeldMoveButtons = MoveButtons.None;
            fire = entities.SpawnEntity("RMCTileFire", coords);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            AssertDamage(entities, drone, 0, 0);
            entities.DeleteEntity(fire);
            var component = entities.GetComponent<CMUCombatDroneComponent>(drone);
            component.SparkIntervalMin = 0.05f;
            component.SparkIntervalMax = 0.1f;
            entities.SpawnEntity("XenoAcidSprayWeak", entities.GetComponent<TransformComponent>(drone).Coordinates);
        });

        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var damage = entities.System<DamageableSystem>().GetAllDamage((drone, null));
            Assert.That(damage.DamageDict["Heat"].Float(), Is.GreaterThan(0), "An actual xeno acid spray must burn the wiring.");
            Assert.That(entities.HasComponent<CMUCombatDroneSparkingComponent>(drone), Is.True);
            var query = entities.EntityQueryEnumerator<MetaDataComponent>();
            var sparks = 0;
            while (query.MoveNext(out _, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == "EffectSparks")
                    sparks++;
            }
            Assert.That(sparks, Is.GreaterThan(0));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DestroyedDroneRemainsLinkedWreckAndRealRepairsRestoreControl()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid drone = default, user = default, tablet = default, mind = default, welder = default, cable = default;
        string originalName = null!;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            drone = entities.SpawnEntity("CMUCombatDrone", map.GridCoords.Offset(new Vector2(0.8f, 0)));
            tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            var ammo = entities.SpawnEntity("CMUCombatDroneAmmoBoxAP", map.GridCoords);
            Assert.That(entities.System<ItemSlotsSystem>().TryInsert((drone, null), SharedGunSystem.MagazineSlot, ammo, null), Is.True);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            Interact(entities, user, tablet, drone);
            var minds = entities.System<SharedMindSystem>();
            mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, user);
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            originalName = entities.GetComponent<MetaDataComponent>(drone).EntityName;
            entities.GetComponent<CMUCombatDroneComponent>(drone).RepairDelay = TimeSpan.FromSeconds(0.1);

            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 250);
            damage.DamageDict.Add("Heat", 150);
            entities.System<DamageableSystem>().TryChangeDamage(drone, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(drone), Is.True, "Overkill must leave a recoverable wreck.");
            Assert.That(entities.GetComponent<CMUCombatDroneComponent>(drone).Wrecked, Is.True);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<MetaDataComponent>(drone).EntityName, Does.Contain("wreckage"));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null);
            Assert.That(entities.GetComponent<CMUDroneControlTabletComponent>(tablet).LinkedDrone, Is.EqualTo(drone));
            Assert.That(entities.GetComponent<CMUDroneOperatorComponent>(user).Drone, Is.EqualTo(drone));
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200));
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null, "A wreck cannot be piloted.");
            Assert.That(entities.System<SharedHandsSystem>().TryDrop(user, tablet, checkActionBlocker: false), Is.True);
            welder = entities.SpawnEntity("CMWelder", map.GridCoords);
            cable = entities.SpawnEntity("RMCCableCoil30", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, welder, checkActionBlocker: false), Is.True);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, cable, checkActionBlocker: false), Is.True);
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((welder, null), user), Is.True);
        });

        for (var repair = 0; repair < 4; repair++)
        {
            await server.WaitAssertion(() => Interact(entities, user, welder, drone));
            await server.WaitRunTicks(20);
        }
        await server.WaitAssertion(() =>
        {
            AssertDamage(entities, drone, 130, 150);
            Assert.That(entities.GetComponent<CMUCombatDroneComponent>(drone).Wrecked, Is.True);
        });
        for (var repair = 0; repair < 3; repair++)
        {
            await server.WaitAssertion(() => Interact(entities, user, cable, drone));
            await server.WaitRunTicks(20);
        }
        await server.WaitAssertion(() =>
        {
            AssertDamage(entities, drone, 130, 60);
            Assert.That(entities.GetComponent<CMUCombatDroneComponent>(drone).Wrecked, Is.False);
            Assert.That(entities.System<MobStateSystem>().IsAlive(drone), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(drone).EntityName, Is.EqualTo(originalName));
            Assert.That(entities.System<SharedHandsSystem>().TryDrop(user, cable, checkActionBlocker: false), Is.True);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            Assert.That(AmmoCount(entities, drone), Is.EqualTo(200), "Recovery must preserve the existing loaded box.");
        });
        await pair.CleanReturnAsync();
    }

    private static void Interact(IEntityManager entities, EntityUid user, EntityUid used, EntityUid target)
    {
        var interaction = new InteractUsingEvent(user, used, target, entities.GetComponent<TransformComponent>(target).Coordinates);
        entities.EventBus.RaiseLocalEvent(target, interaction);
        Assert.That(interaction.Handled, Is.True);
    }

    private static int AmmoCount(IEntityManager entities, EntityUid entity)
    {
        var ammo = new GetAmmoCountEvent();
        entities.EventBus.RaiseLocalEvent(entity, ref ammo);
        return ammo.Count;
    }

    private static void AssertDamage(IEntityManager entities, EntityUid drone, int frame, int wires)
    {
        var damage = entities.System<DamageableSystem>().GetAllDamage((drone, null));
        var component = entities.GetComponent<CMUCombatDroneComponent>(drone);
        Assert.That(CMUCombatDroneSystem.SumDamage(damage, component.FrameDamageTypes).Float(), Is.EqualTo(frame), "frame damage");
        Assert.That(CMUCombatDroneSystem.SumDamage(damage, component.WiringDamageTypes).Float(), Is.EqualTo(wires), "wiring damage");
    }
}
