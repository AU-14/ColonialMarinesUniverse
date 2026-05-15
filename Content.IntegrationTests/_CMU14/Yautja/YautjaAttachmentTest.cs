using System.Numerics;
using System.Linq;
using Content.Server.Explosion.Components;
using Content.Server._RMC14.Scorch;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Explosion.Components;
using Content.Shared._RMC14.CombatMode;
using Content.Shared._RMC14.Medical.Refill;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Wieldable.Components;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions.Components;
using Content.Shared.Blocking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.Components.OnTrigger;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction.Events;
using Content.Shared.Standing;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using ClientSpriteComponent = Robust.Client.GameObjects.SpriteComponent;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaAttachmentTest
{
    [Test]
    public async Task StoredGearDeploysAndRetractsSameEntity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var scimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var deploy = new YautjaToggleScimitarActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);
                Assert.That(entMan.GetComponent<YautjaStoredGearComponent>(scimitar).Deployed, Is.True);
                Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(scimitar));

                var retract = new YautjaToggleScimitarActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, retract);

                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.True);
                Assert.That(entMan.GetComponent<YautjaStoredGearComponent>(scimitar).Deployed, Is.False);
                Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(scimitar));
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StoredGearAutoRetractsInsteadOfDropping()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.MapCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", map.MapCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var scimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);

                Assert.That(hands.TryDrop(hunter, scimitar), Is.False);
                AssertRetracted(entMan, hands, hunter, scimitar, gearComp);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);

                Assert.That(hands.TryDrop(hunter, scimitar, checkActionBlocker: false), Is.False);
                AssertRetracted(entMan, hands, hunter, scimitar, gearComp);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);

                var fall = new FellDownThrowAttemptEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(scimitar, ref fall);

                Assert.That(fall.Cancelled, Is.True);
                AssertRetracted(entMan, hands, hunter, scimitar, gearComp);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscActivationDropsSpinsAndTargetsReachableMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid disc = default;
        FixedPoint2 startingDamage = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var toggle = entMan.System<ItemToggleSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.2f, 0f)));
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);
                startingDamage = entMan.GetComponent<DamageableComponent>(prey).TotalDamage;

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, disc), Is.True);

                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.That(toggle.TrySetActive((disc, toggleComp), true, hunter, false), Is.True);

                Assert.That(hands.IsHolding(hunter, disc), Is.False);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True);

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                Assert.That(physics.LinearVelocity.LengthSquared(), Is.GreaterThan(0f));
                Assert.That(MathF.Abs(physics.AngularVelocity), Is.GreaterThan(0f));
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                Assert.That(hands.IsHolding(hunter, disc), Is.False);

                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.That(smart.Active, Is.True);
                Assert.That(smart.YautjaOwner, Is.EqualTo(hunter));
                Assert.That(smart.CurrentTarget == prey || smart.Hits > 0, Is.True);

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                Assert.That(smart.Hits > 0 || physics.LinearVelocity.LengthSquared() > 0f, Is.True);
            });

            await server.WaitRunTicks(45);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var damageable = entMan.GetComponent<DamageableComponent>(prey);
                var physics = entMan.GetComponent<PhysicsComponent>(disc);

                Assert.That(smart.Active, Is.True, $"hits={smart.Hits} target={smart.CurrentTarget} damage={damageable.TotalDamage} velocity={physics.LinearVelocity.LengthSquared()} angular={physics.AngularVelocity}");
                Assert.That(smart.Hits, Is.GreaterThanOrEqualTo(2));
                Assert.That(damageable.TotalDamage, Is.GreaterThan(startingDamage));
                Assert.That(MathF.Abs(physics.AngularVelocity), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (prey.IsValid() && !entMan.Deleted(prey))
                    entMan.DeleteEntity(prey);

                if (disc.IsValid() && !entMan.Deleted(disc))
                    entMan.DeleteEntity(disc);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscUseInHandHotkeyActivatesOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid disc = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.2f, 0f)));
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, disc), Is.True);

                Assert.That(hands.TryUseItemInHand(hunter), Is.True);

                Assert.That(hands.IsHolding(hunter, disc), Is.False);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True);

                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.That(smart.Active, Is.True);
                Assert.That(toggleComp.Activated, Is.True);
                Assert.That(smart.YautjaOwner, Is.EqualTo(hunter));
                Assert.That(smart.CurrentTarget, Is.EqualTo(prey));

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                Assert.That(physics.LinearVelocity.LengthSquared(), Is.GreaterThan(0f));
                Assert.That(MathF.Abs(physics.AngularVelocity), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (prey.IsValid() && !entMan.Deleted(prey))
                    entMan.DeleteEntity(prey);

                if (disc.IsValid() && !entMan.Deleted(disc))
                    entMan.DeleteEntity(disc);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveSmartDiscCanBeRecoveredWithoutReactivating()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid disc = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var toggle = entMan.System<ItemToggleSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4f, 0f)));
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, disc), Is.True);

                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.That(toggle.TrySetActive((disc, toggleComp), true, hunter, false), Is.True);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);

                Assert.That(smart.Active, Is.True);
                Assert.That(toggleComp.Activated, Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var hands = server.EntMan.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(hunter, disc), Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);

                Assert.That(hands.IsHolding(hunter, disc), Is.True);
                Assert.That(smart.Active, Is.False);
                Assert.That(toggleComp.Activated, Is.False);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (prey.IsValid() && !entMan.Deleted(prey))
                    entMan.DeleteEntity(prey);

                if (disc.IsValid() && !entMan.Deleted(disc))
                    entMan.DeleteEntity(disc);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscActivatedFromPocketLeavesInventoryAndHunts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bodyMesh = default;
        EntityUid prey = default;
        EntityUid disc = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var containers = entMan.System<SharedContainerSystem>();
                var inventory = entMan.System<InventorySystem>();
                var toggle = entMan.System<ItemToggleSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bodyMesh = entMan.SpawnEntity("CMUYautjaBodyMesh", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4f, 0f)));
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bodyMesh, "jumpsuit", silent: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, disc, "pocket1", silent: true), Is.True);
                Assert.That(containers.IsEntityInContainer(disc), Is.True);

                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.That(toggle.TrySetActive((disc, toggleComp), true, hunter, false), Is.True);

                Assert.That(containers.IsEntityInContainer(disc), Is.False);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True);

                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.That(smart.Active, Is.True);
                Assert.That(smart.YautjaOwner, Is.EqualTo(hunter));
                Assert.That(smart.CurrentTarget, Is.EqualTo(prey));
            });

            await server.WaitRunTicks(20);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var physics = entMan.GetComponent<PhysicsComponent>(disc);

                Assert.That(smart.Active, Is.True);
                Assert.That(smart.Hits > 0 || physics.LinearVelocity.LengthSquared() > 0f, Is.True);
                Assert.That(MathF.Abs(physics.AngularVelocity), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                foreach (var uid in new[] { hunter, bodyMesh, prey, disc })
                {
                    if (uid.IsValid() && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaActivatingSmartDiscTurnsItAgainstThem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid marine = default;
        EntityUid disc = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var toggle = entMan.System<ItemToggleSystem>();

                marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

                var tech = entMan.GetComponent<YautjaTechItemComponent>(disc);
                Assert.That(tech.BlockPickup, Is.False);
                Assert.That(tech.BlockUse, Is.False);
                Assert.That(hands.TryPickupAnyHand(marine, disc), Is.True);

                var toggleComp = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.That(toggle.TrySetActive((disc, toggleComp), true, marine, false), Is.True);

                var smart = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.That(hands.IsHolding(marine, disc), Is.False);
                Assert.That(smart.Active, Is.True);
                Assert.That(smart.RogueTarget, Is.EqualTo(marine));
                Assert.That(smart.RogueActivator, Is.EqualTo(marine));
                Assert.That(smart.CurrentTarget, Is.EqualTo(marine));

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                Assert.That(physics.LinearVelocity.LengthSquared(), Is.GreaterThan(0f));
                Assert.That(MathF.Abs(physics.AngularVelocity), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (marine.IsValid() && !entMan.Deleted(marine))
                    entMan.DeleteEntity(marine);

                if (disc.IsValid() && !entMan.Deleted(disc))
                    entMan.DeleteEntity(disc);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PortedWeaponsSpawnAndRejectNonYautjaPickup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var weaponIds = new[]
            {
                "CMUYautjaHarpoon",
                "CMUYautjaChainwhip",
                "CMUYautjaClanSword",
                "CMUYautjaRendingSword",
                "CMUYautjaPiercingSword",
                "CMUYautjaSeveringSword",
                "CMUYautjaDualWarScythe",
                "CMUYautjaDoubleWarScythe",
                "CMUYautjaCruelStaff",
                "CMUYautjaCombistick",
                "CMUYautjaWarAxe",
                "CMUYautjaCeremonialDagger",
                "CMUYautjaClanShield",
                "CMUYautjaAncientShield",
                "CMUYautjaAncientShieldAlt",
                "CMUYautjaAncientShieldTemple",
                "CMUYautjaHunterSpear",
                "CMUYautjaWarGlaive",
                "CMUYautjaCleavingGlaive",
                "CMUYautjaAncientWarGlaive",
                "CMUYautjaLongaxe",
                "CMUYautjaDuellingBlade",
                "CMUYautjaDuellingClub",
                "CMUYautjaDuellingHatchet",
                "CMUYautjaDuellingKnife",
                "CMUYautjaSpikeLauncher",
                "CMUYautjaPlasmaRifle",
                "CMUYautjaPlasmaPistol",
            };

            var marine = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            try
            {
                foreach (var weaponId in weaponIds)
                {
                    var weapon = entMan.SpawnEntity(weaponId, MapCoordinates.Nullspace);
                    try
                    {
                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(weapon), Is.True, weaponId);
                        Assert.That(hands.TryPickupAnyHand(marine, weapon), Is.False, weaponId);
                        Assert.That(hands.TryPickupAnyHand(hunter, weapon), Is.True, weaponId);
                    }
                    finally
                    {
                        if (!entMan.Deleted(weapon))
                            entMan.DeleteEntity(weapon);
                    }
                }
            }
            finally
            {
                entMan.DeleteEntity(marine);
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PortedNormalWeaponsHaveUsableCombatComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var wielding = entMan.System<Content.Server.Wieldable.WieldableSystem>();

            var meleeWeaponIds = new[]
            {
                "CMUYautjaHarpoon",
                "CMUYautjaChainwhip",
                "CMUYautjaClanSword",
                "CMUYautjaRendingSword",
                "CMUYautjaPiercingSword",
                "CMUYautjaSeveringSword",
                "CMUYautjaDualWarScythe",
                "CMUYautjaDoubleWarScythe",
                "CMUYautjaCruelStaff",
                "CMUYautjaCombistick",
                "CMUYautjaWarAxe",
                "CMUYautjaCeremonialDagger",
                "CMUYautjaHunterSpear",
                "CMUYautjaWarGlaive",
                "CMUYautjaCleavingGlaive",
                "CMUYautjaAncientWarGlaive",
                "CMUYautjaLongaxe",
                "CMUYautjaDuellingBlade",
                "CMUYautjaDuellingClub",
                "CMUYautjaDuellingHatchet",
                "CMUYautjaDuellingKnife",
            };

            foreach (var weaponId in meleeWeaponIds)
            {
                var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                var weapon = entMan.SpawnEntity(weaponId, MapCoordinates.Nullspace);

                try
                {
                    entMan.EnsureComponent<YautjaComponent>(hunter);
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(weapon), Is.True, weaponId);
                    Assert.That(entMan.HasComponent<MeleeWeaponComponent>(weapon), Is.True, weaponId);

                    var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
                    Assert.That(melee.Damage.GetTotal(), Is.GreaterThan(FixedPoint2.Zero), weaponId);
                    Assert.That(hands.TryPickupAnyHand(hunter, weapon), Is.True, weaponId);

                    if (entMan.TryGetComponent(weapon, out WieldableComponent wieldable))
                    {
                        Assert.That(entMan.HasComponent<MeleeRequiresWieldComponent>(weapon), Is.True, weaponId);

                        var attempt = new AttemptMeleeEvent(hunter);
                        entMan.EventBus.RaiseLocalEvent(weapon, ref attempt);
                        Assert.That(attempt.Cancelled, Is.True, weaponId);

                        Assert.That(wielding.TryWield(weapon, wieldable, hunter), Is.True, weaponId);

                        attempt = new AttemptMeleeEvent(hunter);
                        entMan.EventBus.RaiseLocalEvent(weapon, ref attempt);
                        Assert.That(attempt.Cancelled, Is.False, weaponId);
                    }

                    if (entMan.HasComponent<LandAtCursorComponent>(weapon))
                        Assert.That(entMan.HasComponent<DamageOtherOnHitComponent>(weapon), Is.True, weaponId);
                }
                finally
                {
                    if (!entMan.Deleted(hunter))
                        entMan.DeleteEntity(hunter);

                    if (!entMan.Deleted(weapon))
                        entMan.DeleteEntity(weapon);
                }
            }

            var shieldIds = new[]
            {
                "CMUYautjaClanShield",
                "CMUYautjaAncientShield",
                "CMUYautjaAncientShieldAlt",
                "CMUYautjaAncientShieldTemple",
            };

            foreach (var shieldId in shieldIds)
            {
                var shield = entMan.SpawnEntity(shieldId, MapCoordinates.Nullspace);
                try
                {
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(shield), Is.True, shieldId);
                    Assert.That(entMan.HasComponent<BlockingComponent>(shield), Is.True, shieldId);
                    Assert.That(entMan.HasComponent<MeleeWeaponComponent>(shield), Is.False, shieldId);
                }
                finally
                {
                    if (!entMan.Deleted(shield))
                        entMan.DeleteEntity(shield);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CombiWeaponsHitLikePredatorGearButOtherStoredMeleeRemainsModerate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);
            var wristBlades = entMan.SpawnEntity("CMUYautjaWristBlades", MapCoordinates.Nullspace);

            try
            {
                var scimitarMelee = entMan.GetComponent<MeleeWeaponComponent>(scimitar);
                Assert.That(scimitarMelee.AttackRate, Is.EqualTo(1f).Within(0.001f));
                Assert.That(scimitarMelee.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(37)));
                Assert.That(scimitarMelee.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(7)));

                var scimitarThrow = entMan.GetComponent<DamageOtherOnHitComponent>(scimitar);
                Assert.That(scimitarThrow.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(scimitarThrow.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(6)));

                var combistickMelee = entMan.GetComponent<MeleeWeaponComponent>(combistick);
                Assert.That(combistickMelee.AttackRate, Is.EqualTo(0.95f).Within(0.001f));
                Assert.That(combistickMelee.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(33)));
                Assert.That(combistickMelee.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(8)));

                var combistickThrow = entMan.GetComponent<DamageOtherOnHitComponent>(combistick);
                Assert.That(combistickThrow.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(29)));
                Assert.That(combistickThrow.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(6)));

                var wristMelee = entMan.GetComponent<MeleeWeaponComponent>(wristBlades);
                Assert.That(wristMelee.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(31)));
                Assert.That(wristMelee.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(5)));
            }
            finally
            {
                if (!entMan.Deleted(scimitar))
                    entMan.DeleteEntity(scimitar);

                if (!entMan.Deleted(combistick))
                    entMan.DeleteEntity(combistick);

                if (!entMan.Deleted(wristBlades))
                    entMan.DeleteEntity(wristBlades);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMeleeWeaponsStayWithinWarriorHitBudget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var weaponIds = new[]
            {
                "CMUYautjaWristBlades",
                "CMUYautjaScimitar",
                "CMUYautjaChainGauntlet",
                "CMUYautjaHarpoon",
                "CMUYautjaChainwhip",
                "CMUYautjaClanSword",
                "CMUYautjaRendingSword",
                "CMUYautjaPiercingSword",
                "CMUYautjaSeveringSword",
                "CMUYautjaDualWarScythe",
                "CMUYautjaDoubleWarScythe",
                "CMUYautjaCruelStaff",
                "CMUYautjaCombistick",
                "CMUYautjaWarAxe",
                "CMUYautjaCeremonialDagger",
                "CMUYautjaHunterSpear",
                "CMUYautjaWarGlaive",
                "CMUYautjaCleavingGlaive",
                "CMUYautjaAncientWarGlaive",
                "CMUYautjaLongaxe",
                "CMUYautjaDuellingBlade",
                "CMUYautjaDuellingClub",
                "CMUYautjaDuellingHatchet",
                "CMUYautjaDuellingKnife",
            };

            foreach (var weaponId in weaponIds)
            {
                var weapon = entMan.SpawnEntity(weaponId, MapCoordinates.Nullspace);

                try
                {
                    var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
                    var damage = BruteDamage(melee.Damage);

                    if (entMan.TryGetComponent(weapon, out IncreaseDamageOnWieldComponent? wieldDamage))
                        damage += BruteDamage(wieldDamage.BonusDamage);

                    var multiplier = entMan.GetComponent<MeleeDamageMultiplierComponent>(weapon);
                    Assert.That(multiplier.Multiplier, Is.EqualTo(0f).Within(0.001f), $"{weaponId} should not inherit extra RMC xeno melee damage.");

                    var armorPiercing = entMan.GetComponent<CMArmorPiercingComponent>(weapon);
                    Assert.That(armorPiercing.Amount, Is.Zero, $"{weaponId} should not completely bypass Warrior xeno armor.");

                    var hits = HitsToKillWarrior(damage, armorPiercing.Amount, multiplier.Multiplier);
                    Assert.That(hits, Is.InRange(8, 15), $"{weaponId} should kill a Warrior in 8-15 hits, got {hits} from {damage} raw brute.");
                }
                finally
                {
                    if (!entMan.Deleted(weapon))
                        entMan.DeleteEntity(weapon);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static int HitsToKillWarrior(FixedPoint2 rawDamage, int armorPiercing, float xenoMultiplier)
    {
        const double warriorDeadThreshold = 600;
        const double yautjaMaxMeleeSkillMultiplier = 2;
        var warriorArmor = Math.Max(0, 20 - armorPiercing);
        var warriorArmorResist = Math.Pow(1.1, warriorArmor / 5.0);
        var effectiveDamage = rawDamage.Float() * yautjaMaxMeleeSkillMultiplier * (1 + xenoMultiplier) / warriorArmorResist;
        return (int) Math.Ceiling(warriorDeadThreshold / effectiveDamage);
    }

    private static FixedPoint2 BruteDamage(DamageSpecifier damage)
    {
        var total = FixedPoint2.Zero;

        if (damage.DamageDict.TryGetValue("Blunt", out var blunt))
            total += blunt;

        if (damage.DamageDict.TryGetValue("Slash", out var slash))
            total += slash;

        if (damage.DamageDict.TryGetValue("Piercing", out var piercing))
            total += piercing;

        return total;
    }

    [Test]
    public async Task PlasmaRifleRequiresWieldAndUsesRmcWieldHandling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var wielding = entMan.System<Content.Server.Wieldable.WieldableSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, rifle), Is.True);

                Assert.That(entMan.HasComponent<WieldableComponent>(rifle), Is.True);
                Assert.That(entMan.HasComponent<WieldDelayComponent>(rifle), Is.True);
                Assert.That(entMan.HasComponent<WieldableSpeedModifiersComponent>(rifle), Is.True);
                Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(rifle), Is.True);
                Assert.That(entMan.HasComponent<GunWieldBonusComponent>(rifle), Is.True);
                Assert.That(entMan.HasComponent<WieldedCrosshairComponent>(rifle), Is.True);

                var gun = entMan.GetComponent<GunComponent>(rifle);
                var shot = new ShotAttemptedEvent
                {
                    User = hunter,
                    Used = (rifle, gun),
                };

                entMan.EventBus.RaiseLocalEvent(rifle, ref shot);
                Assert.That(shot.Cancelled, Is.True);

                var wieldable = entMan.GetComponent<WieldableComponent>(rifle);
                Assert.That(wielding.TryWield(rifle, wieldable, hunter), Is.True);

                shot = new ShotAttemptedEvent
                {
                    User = hunter,
                    Used = (rifle, gun),
                };

                entMan.EventBus.RaiseLocalEvent(rifle, ref shot);
                Assert.That(shot.Cancelled, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(rifle))
                    entMan.DeleteEntity(rifle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingPouchCanOnlyBeWornOnBelt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bodyMesh = entMan.SpawnEntity("CMUYautjaBodyMesh", MapCoordinates.Nullspace);
            var backPouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);
            var beltPouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bodyMesh, "jumpsuit", silent: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, backPouch, "back", silent: true), Is.False);

                var canBelt = inventory.CanEquip(hunter, beltPouch, "belt", out var reason);
                Assert.That(canBelt, Is.True, reason);
                Assert.That(inventory.TryEquip(hunter, beltPouch, "belt", silent: true), Is.True);
                Assert.That(inventory.TryGetSlotEntity(hunter, "belt", out var beltItem), Is.True);
                Assert.That(beltItem, Is.EqualTo(beltPouch));
            }
            finally
            {
                foreach (var uid in new[] { hunter, bodyMesh, backPouch, beltPouch })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaHealingGunInstantlyHealsAndIsReusable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.System<DamageableSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1f, 0f)));
            var gun = entMan.SpawnEntity("CMUYautjaHealingGun", map.GridCoords);

            try
            {
                var blunt = prototypeManager.Index<DamageTypePrototype>("Blunt");
                var heat = prototypeManager.Index<DamageTypePrototype>("Heat");
                var asphyxiation = prototypeManager.Index<DamageTypePrototype>("Asphyxiation");
                var bloodloss = prototypeManager.Index<DamageTypePrototype>("Bloodloss");
                damage.TryChangeDamage(target,
                    new DamageSpecifier(blunt, 20) +
                    new DamageSpecifier(heat, 20) +
                    new DamageSpecifier(asphyxiation, 40) +
                    new DamageSpecifier(bloodloss, 45),
                    true);

                var targetDamage = entMan.GetComponent<DamageableComponent>(target);
                var before = targetDamage.TotalDamage;
                Assert.That(before, Is.GreaterThan(FixedPoint2.Zero));

                var crystal = entMan.SpawnEntity("CMUYautjaHumanStabilisingCrystal", MapCoordinates.Nullspace);
                try
                {
                    var crystalHealing = entMan.GetComponent<Content.Shared.Medical.Healing.HealingComponent>(crystal);
                    var gunHealing = entMan.GetComponent<YautjaHealingGunComponent>(gun);
                    Assert.That(gunHealing.BloodlossModifier, Is.EqualTo(crystalHealing.BloodlossModifier));
                    Assert.That(gunHealing.Damage.GetTotal(), Is.EqualTo(crystalHealing.Damage.GetTotal()));
                }
                finally
                {
                    if (!entMan.Deleted(crystal))
                        entMan.DeleteEntity(crystal);
                }

                var ev = new AfterInteractEvent(user, gun, target, entMan.GetComponent<TransformComponent>(target).Coordinates, true);
                entMan.EventBus.RaiseLocalEvent(gun, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(entMan.Deleted(gun), Is.False);
                Assert.That(targetDamage.TotalDamage, Is.LessThan(before));

                var afterFirst = targetDamage.TotalDamage;
                ev = new AfterInteractEvent(user, gun, target, entMan.GetComponent<TransformComponent>(target).Coordinates, true);
                entMan.EventBus.RaiseLocalEvent(gun, ev);

                Assert.That(ev.Handled, Is.False);
                Assert.That(targetDamage.TotalDamage, Is.EqualTo(afterFirst));
                Assert.That(entMan.Deleted(gun), Is.False);
            }
            finally
            {
                foreach (var uid in new[] { user, target, gun })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredatorMedicompSpawnsCmReferenceHealingSet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medicomp = entMan.SpawnEntity("CMUYautjaMedicomp", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(medicomp);
                var prototypes = storage.Container.ContainedEntities
                    .Select(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID)
                    .ToList();

                Assert.That(prototypes, Does.Contain("CMUYautjaStabilizerGel"));
                Assert.That(prototypes, Does.Contain("CMUYautjaHealingGun"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaWoundClamp"), Is.EqualTo(2));
                Assert.That(prototypes, Does.Contain("CMUYautjaAlienHealthAnalyzer"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaAutoInjector"), Is.EqualTo(6));
                var healingGel = storage.Container.ContainedEntities.Single(contained =>
                    entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaHealingGel");
                Assert.That(entMan.GetComponent<StackComponent>(healingGel).Count, Is.EqualTo(6));
                var stabilizerGel = storage.Container.ContainedEntities.Single(contained =>
                    entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaStabilizerGel");
                Assert.That(entMan.GetComponent<StackComponent>(stabilizerGel).Count, Is.EqualTo(3));
                Assert.That(prototypes.Count(id => id == "CMUYautjaHerbalCase"), Is.EqualTo(2));
            }
            finally
            {
                if (!entMan.Deleted(medicomp))
                    entMan.DeleteEntity(medicomp);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaHealingGunUsesCmSurgeryToolSkin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var prototypeManager = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            Assert.That(prototypeManager.TryIndex<EntityPrototype>("CMUYautjaHealingGun", out var prototype), Is.True);
            Assert.That(prototype!.TryGetComponent<ClientSpriteComponent>(out var sprite, componentFactory), Is.True);
            Assert.That(sprite.BaseRSI?.Path.ToString(), Does.EndWith("/Textures/_CMU14/Yautja/healing_gun.rsi"));
            Assert.That(sprite.BaseRSI?.TryGetState("healing_gun", out _), Is.True);

            Assert.That(prototype.TryGetComponent<ItemComponent>(out var item, componentFactory), Is.True);
            Assert.That(item.RsiPath, Is.EqualTo("_CMU14/Yautja/healing_gun.rsi"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaAutoInjectorUsesValidPenSolutionAndSingleFillVisual()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var injector = entMan.SpawnEntity("CMUYautjaAutoInjector", MapCoordinates.Nullspace);

            try
            {
                var hypo = entMan.GetComponent<HyposprayComponent>(injector);
                Assert.That(hypo.SolutionName, Is.EqualTo("pen"));
                Assert.That(hypo.TransferAmount, Is.EqualTo(FixedPoint2.New(45)));

                var refill = entMan.GetComponent<CMRefillableSolutionComponent>(injector);
                Assert.That(refill.Solution, Is.EqualTo("pen"));
                Assert.That(refill.Reagents["CMBicaridine"], Is.EqualTo(FixedPoint2.New(45)));
                Assert.That(refill.Reagents["CMKelotane"], Is.EqualTo(FixedPoint2.New(45)));
                Assert.That(refill.Reagents["CMTricordrazine"], Is.EqualTo(FixedPoint2.New(45)));

                var visuals = entMan.GetComponent<SolutionContainerVisualsComponent>(injector);
                Assert.That(visuals.MaxFillLevels, Is.EqualTo(1));
                Assert.That(visuals.ChangeColor, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(injector))
                    entMan.DeleteEntity(injector);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterCyclesCmModesAndProjectileProvider()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var yautjaCaster = entMan.GetComponent<YautjaCasterComponent>(caster);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(caster);

                Assert.That(yautjaCaster.Modes, Has.Count.EqualTo(4));
                Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaCasterStunBolt"));

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(yautjaCaster.CurrentMode, Is.EqualTo(1));
                Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaCasterImmobilizerBolt"));

                use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, use);
                Assert.That(yautjaCaster.CurrentMode, Is.EqualTo(2));
                Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaCasterLethalBolt"));
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(caster))
                    entMan.DeleteEntity(caster);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterLethalModesAreExplosive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var lethal = entMan.SpawnEntity("CMUYautjaCasterLethalBolt", MapCoordinates.Nullspace);
            var eradicator = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<TriggerOnCollideComponent>(lethal), Is.True);
                Assert.That(entMan.HasComponent<ExplodeOnTriggerComponent>(lethal), Is.True);
                Assert.That(entMan.HasComponent<CMExplosionEffectComponent>(lethal), Is.True);
                Assert.That(entMan.HasComponent<RMCScorchEffectComponent>(lethal), Is.True);
                var lethalExplosion = entMan.GetComponent<ExplosiveComponent>(lethal);
                Assert.That(lethalExplosion.ExplosionType, Is.EqualTo("RMC"));
                Assert.That(lethalExplosion.TotalIntensity, Is.EqualTo(120));
                Assert.That(lethalExplosion.MaxIntensity, Is.EqualTo(30));
                Assert.That(lethalExplosion.MaxTileBreak, Is.EqualTo(2));

                Assert.That(entMan.HasComponent<TriggerOnCollideComponent>(eradicator), Is.True);
                Assert.That(entMan.HasComponent<ExplodeOnTriggerComponent>(eradicator), Is.True);
                Assert.That(entMan.HasComponent<CMExplosionEffectComponent>(eradicator), Is.True);
                Assert.That(entMan.HasComponent<RMCScorchEffectComponent>(eradicator), Is.True);
                var eradicatorExplosion = entMan.GetComponent<ExplosiveComponent>(eradicator);
                Assert.That(eradicatorExplosion.ExplosionType, Is.EqualTo("RMC"));
                Assert.That(eradicatorExplosion.TotalIntensity, Is.EqualTo(320));
                Assert.That(eradicatorExplosion.MaxIntensity, Is.EqualTo(60));
                Assert.That(eradicatorExplosion.MaxTileBreak, Is.EqualTo(2));
            }
            finally
            {
                if (!entMan.Deleted(lethal))
                    entMan.DeleteEntity(lethal);

                if (!entMan.Deleted(eradicator))
                    entMan.DeleteEntity(eradicator);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaWeaponsUseGrandVisualEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var muzzle = entMan.SpawnEntity("CMUYautjaPlasmaMuzzleFlash", MapCoordinates.Nullspace);
            var impact = entMan.SpawnEntity("CMUYautjaPlasmaImpactEffect", MapCoordinates.Nullspace);
            var heavyImpact = entMan.SpawnEntity("CMUYautjaPlasmaHeavyImpactEffect", MapCoordinates.Nullspace);
            var eradicatorImpact = entMan.SpawnEntity("CMUYautjaPlasmaEradicatorImpactEffect", MapCoordinates.Nullspace);
            var smallShockWave = entMan.SpawnEntity("CMUYautjaPlasmaSmallShockWave", MapCoordinates.Nullspace);
            var heavyShockWave = entMan.SpawnEntity("CMUYautjaPlasmaHeavyShockWave", MapCoordinates.Nullspace);
            var eradicatorShockWave = entMan.SpawnEntity("CMUYautjaPlasmaEradicatorShockWave", MapCoordinates.Nullspace);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistolBolt", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", MapCoordinates.Nullspace);
            var lethal = entMan.SpawnEntity("CMUYautjaCasterLethalBolt", MapCoordinates.Nullspace);
            var eradicator = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<PointLightComponent>(muzzle).Radius, Is.GreaterThanOrEqualTo(3.25f));
                Assert.That(entMan.GetComponent<PointLightComponent>(impact).Radius, Is.GreaterThanOrEqualTo(4.25f));
                Assert.That(entMan.GetComponent<PointLightComponent>(heavyImpact).Radius, Is.GreaterThanOrEqualTo(5.5f));
                Assert.That(entMan.GetComponent<PointLightComponent>(eradicatorImpact).Radius, Is.GreaterThanOrEqualTo(7f));
                Assert.That(entMan.HasComponent<RMCExplosionShockWaveComponent>(impact), Is.True);
                Assert.That(entMan.HasComponent<RMCExplosionShockWaveComponent>(heavyImpact), Is.True);
                Assert.That(entMan.HasComponent<RMCExplosionShockWaveComponent>(eradicatorImpact), Is.True);
                Assert.That(entMan.GetComponent<RMCExplosionShockWaveComponent>(smallShockWave).Width, Is.GreaterThanOrEqualTo(0.65f));
                Assert.That(entMan.GetComponent<RMCExplosionShockWaveComponent>(heavyShockWave).Width, Is.GreaterThanOrEqualTo(0.95f));
                Assert.That(entMan.GetComponent<RMCExplosionShockWaveComponent>(eradicatorShockWave).Width, Is.GreaterThanOrEqualTo(1.35f));

                Assert.That(entMan.GetComponent<ProjectileComponent>(pistol).ImpactEffect, Is.EqualTo("CMUYautjaPlasmaImpactEffect"));
                Assert.That(entMan.GetComponent<ProjectileComponent>(rifle).ImpactEffect, Is.EqualTo("CMUYautjaPlasmaHeavyImpactEffect"));
                Assert.That(entMan.GetComponent<ProjectileComponent>(lethal).ImpactEffect, Is.EqualTo("CMUYautjaPlasmaHeavyImpactEffect"));
                Assert.That(entMan.GetComponent<ProjectileComponent>(eradicator).ImpactEffect, Is.EqualTo("CMUYautjaPlasmaEradicatorImpactEffect"));

                Assert.That(entMan.GetComponent<PointLightComponent>(rifle).Radius, Is.GreaterThanOrEqualTo(4.25f));
                Assert.That(entMan.GetComponent<PointLightComponent>(eradicator).Radius, Is.GreaterThanOrEqualTo(6.5f));

                var pistolExplosion = entMan.GetComponent<ExplosiveComponent>(pistol);
                Assert.That(pistolExplosion.ExplosionType, Is.EqualTo("RMC"));
                Assert.That(pistolExplosion.TotalIntensity, Is.EqualTo(24));
                var pistolEffect = entMan.GetComponent<CMExplosionEffectComponent>(pistol);
                Assert.That(pistolEffect.Explosion, Is.EqualTo("CMUYautjaPlasmaImpactEffect"));
                Assert.That(pistolEffect.ShockWave, Is.EqualTo("CMUYautjaPlasmaSmallShockWave"));
                Assert.That(pistolEffect.MaxShrapnel, Is.EqualTo(0));

                var rifleExplosion = entMan.GetComponent<ExplosiveComponent>(rifle);
                Assert.That(rifleExplosion.ExplosionType, Is.EqualTo("RMC"));
                Assert.That(rifleExplosion.TotalIntensity, Is.EqualTo(70));
                var rifleEffect = entMan.GetComponent<CMExplosionEffectComponent>(rifle);
                Assert.That(rifleEffect.Explosion, Is.EqualTo("CMUYautjaPlasmaHeavyImpactEffect"));
                Assert.That(rifleEffect.ShockWave, Is.EqualTo("CMUYautjaPlasmaHeavyShockWave"));

                var lethalEffect = entMan.GetComponent<CMExplosionEffectComponent>(lethal);
                Assert.That(lethalEffect.Explosion, Is.EqualTo("CMUYautjaPlasmaHeavyImpactEffect"));
                Assert.That(lethalEffect.ShockWave, Is.EqualTo("CMUYautjaPlasmaHeavyShockWave"));

                var eradicatorEffect = entMan.GetComponent<CMExplosionEffectComponent>(eradicator);
                Assert.That(eradicatorEffect.Explosion, Is.EqualTo("CMUYautjaPlasmaEradicatorImpactEffect"));
                Assert.That(eradicatorEffect.ShockWave, Is.EqualTo("CMUYautjaPlasmaEradicatorShockWave"));
            }
            finally
            {
                foreach (var uid in new[] { muzzle, impact, heavyImpact, eradicatorImpact, smallShockWave, heavyShockWave, eradicatorShockWave, pistol, rifle, lethal, eradicator })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static YautjaToggleScimitarActionEvent NewToggleScimitarEvent(
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        return new YautjaToggleScimitarActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
    }

    private static void AssertRetracted(
        IEntityManager entMan,
        SharedHandsSystem hands,
        EntityUid hunter,
        EntityUid gear,
        YautjaGearContainerComponent bracer)
    {
        Assert.That(hands.IsHolding(hunter, gear), Is.False);
        Assert.That(bracer.Container, Is.Not.Null);
        Assert.That(bracer.Container!.Contains(gear), Is.True);
        Assert.That(entMan.GetComponent<YautjaStoredGearComponent>(gear).Deployed, Is.False);
    }
}
