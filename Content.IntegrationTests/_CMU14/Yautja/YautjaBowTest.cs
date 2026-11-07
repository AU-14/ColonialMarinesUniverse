using Content.Shared.Blocking.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Trigger.Components.Triggers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Yautja;
using Content.Server._RMC14.Scorch;
using Content.Server.Atmos.Components;
using Content.Server.Emp;
using Content.Server.Examine;
using Content.Server.Explosion.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Server.Verbs;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Access.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Slow;
using Content.Shared.Standing;
using Content.Shared._RMC14.Tether;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Armor;
using Content.Shared.Blocking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._RMC14.Medical.Refill;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Kitchen.Components;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._RMC14.Storage;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Robust.Client.GameObjects;
using ServerPointLightComponent = Robust.Server.GameObjects.PointLightComponent;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBowTest
{

    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> CMUYautjaPrototype = "CMUYautja";

    // cmss13 puts pred weapons in suit storage through the armor's allowed list, ss14 needs the item to
    // list suitStorage too. these checks are about the native equip slot, so mask that extra one off

    [Test]
    public async Task AlloySpikeAddsCmss13DamageBoostAgainstTurfAndBreachingTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var spike = entMan.SpawnEntity("CMUYautjaSpikeProjectile", MapCoordinates.Nullspace);
            var turfTarget = entMan.SpawnEntity("CMWallMetal", MapCoordinates.Nullspace);
            var breachingTarget = entMan.SpawnEntity("CMTable", MapCoordinates.Nullspace);
            var unflaggedTarget = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<DamageMultiplierFlagsComponent>(turfTarget).Flags, Is.EqualTo(DamageMultiplierFlag.Turf));
                Assert.That(entMan.GetComponent<DamageMultiplierFlagsComponent>(breachingTarget).Flags, Is.EqualTo(DamageMultiplierFlag.Breaching));
                Assert.That(entMan.HasComponent<DamageMultiplierFlagsComponent>(unflaggedTarget), Is.False);

                var baseDamage = new DamageSpecifier(entMan.GetComponent<ProjectileComponent>(spike).Damage);
                var turfDamage = damageable.TryChangeDamage(turfTarget, new DamageSpecifier(baseDamage), tool: spike);
                var breachingDamage = damageable.TryChangeDamage(breachingTarget, new DamageSpecifier(baseDamage), tool: spike);
                var unflaggedDamage = damageable.TryChangeDamage(unflaggedTarget, new DamageSpecifier(baseDamage), tool: spike);

                Assert.Multiple(() =>
                {
                    // CM-SS13 boosts by +25; CMU buffs alloy spikes to +250 on purpose so they breach walls quickly,
                    // which is why these expect 280 (30 base + 250) rather than 55.
                    Assert.That(turfDamage?.DamageDict["Piercing"],
                        Is.EqualTo((FixedPoint2) 280),
                        "CMU alloy spikes add a deliberate +250 against GLOB.damage_boost_turfs.");
                    Assert.That(breachingDamage?.DamageDict["Piercing"],
                        Is.EqualTo((FixedPoint2) 280),
                        "CMU alloy spikes add a deliberate +250 against GLOB.damage_boost_breaching.");
                    Assert.That(unflaggedDamage?.DamageDict["Piercing"],
                        Is.EqualTo((FixedPoint2) 30),
                        "CMSS13 alloy spike damage boost is target-category gated.");
                });
            }
            finally
            {
                foreach (var uid in new[] { spike, turfTarget, breachingTarget, unflaggedTarget })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRifleBoltAppliesXenoSideEffectsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var damageable = entMan.GetComponent<DamageableComponent>(xeno);
                var baseHeat = entMan.GetComponent<ProjectileComponent>(projectile).Damage.DamageDict["Heat"];

                Assert.That(baseHeat, Is.EqualTo((FixedPoint2) 55),
                    "CMSS13 /datum/ammo/energy/yautja/rifle/bolt sets BURN damage = 55.");
                Assert.That(entMan.System<DamageableSystem>().GetAllDamage((xeno, damageable)).DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.Zero));

                var ev = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(entMan.System<DamageableSystem>().GetAllDamage((xeno, damageable)).DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.New(41.25)),
                    "CMSS13 rifle bolts add damage * 0.75 extra BURN damage to xenos in on_hit_mob().");
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, xeno })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(10f)]
    [TestCase(30f)]
    public async Task YautjaIncendiaryPlasmaBoltsApplyCmss13FireStacksOnProjectileHit(float maximumStacks)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var pistolHumanProjectile = entMan.SpawnEntity("CMUYautjaPlasmaPistolIncendiaryBolt", map.GridCoords);
            var pistolXenoProjectile = entMan.SpawnEntity("CMUYautjaPlasmaPistolIncendiaryBolt", map.GridCoords);
            var rifleHumanProjectile = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", map.GridCoords);
            var rifleXenoProjectile = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", map.GridCoords);
            var humanPistolTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xenoPistolTarget = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var humanRifleTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var xenoRifleTarget = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(4, 0)));
            var xenoCarbineTarget = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(5, 0)));
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
            EntityUid? carbineProjectile = null;

            try
            {
                // Exercise the source formula as well as the current engine's fire-stack cap.
                foreach (var target in new[] { humanPistolTarget, xenoPistolTarget, humanRifleTarget, xenoRifleTarget, xenoCarbineTarget })
                    entMan.GetComponent<FlammableComponent>(target).MaximumFireStacks = maximumStacks;

                RaiseProjectileHit(entMan, pistolHumanProjectile, humanPistolTarget, hunter);
                RaiseProjectileHit(entMan, pistolXenoProjectile, xenoPistolTarget, hunter);
                RaiseProjectileHit(entMan, rifleHumanProjectile, humanRifleTarget, hunter);
                RaiseProjectileHit(entMan, rifleXenoProjectile, xenoRifleTarget, hunter);

                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                var coordinates = entMan.GetComponent<TransformComponent>(carbine).Coordinates;
                batterySystem.SetCharge((carbine, battery), 1);

                var ammo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(carbine, ammo);
                carbineProjectile = ammo.Ammo.Single().Entity;
                Assert.That(carbineProjectile, Is.Not.Null);
                Assert.That(entMan.GetComponent<MetaDataComponent>(carbineProjectile.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaPlasmaRifleBolt"),
                    "CMSS13 plasma carbine incendiary mode fires /datum/ammo/energy/yautja/rifle/bolt.");

                RaiseProjectileHit(entMan, carbineProjectile.Value, xenoCarbineTarget, hunter);

                Assert.Multiple(() =>
                {
                    AssertFireStacks(entMan,
                        humanPistolTarget,
                        20f,
                        "CMSS13 /datum/element/bullet_trait_incendiary human branch adds burn_stacks = 20 for plasma pistol incendiary bolts.");
                    AssertFireStacks(entMan,
                        xenoPistolTarget,
                        12f,
                        "CMSS13 xeno branch adds burn_stacks / 2 + floor(damage_actual / 4): 10 + floor(10 / 4) for pistol incendiary bolts.");
                    AssertFireStacks(entMan,
                        humanRifleTarget,
                        20f,
                        "CMSS13 rifle bolts use the same default incendiary burn_stacks = 20 on humans.");
                    AssertFireStacks(entMan,
                        xenoRifleTarget,
                        23f,
                        "CMSS13 xeno branch adds burn_stacks / 2 + floor(damage_actual / 4): 10 + floor(55 / 4) for rifle bolts.");
                    AssertFireStacks(entMan,
                        xenoCarbineTarget,
                        23f,
                        "CMSS13 carbine incendiary mode reuses rifle bolts, so xenos get 10 + floor(55 / 4) fire stacks.");
                });
            }
            finally
            {
                foreach (var uid in new[] { carbineProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[]
                         {
                             hunter,
                             pistolHumanProjectile,
                             pistolXenoProjectile,
                             rifleHumanProjectile,
                             rifleXenoProjectile,
                             humanPistolTarget,
                             xenoPistolTarget,
                             humanRifleTarget,
                             xenoRifleTarget,
                             xenoCarbineTarget,
                             carbine,
                         })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FiredDynamicEmpArrowTriggersCmss13EmpPulseFromBowPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid bracer = default;
        EntityUid? projectile = null;
        MapId mapId = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var wield = entMan.System<Content.Shared.Wieldable.SharedWieldableSystem>();

                mapSystem.CreateMap(out mapId);
                var coordinates = new MapCoordinates(Vector2.Zero, mapId);
                hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", coordinates);
                arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", coordinates);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Emp));

                var arrowComp = entMan.GetComponent<YautjaArrowComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                Assert.Multiple(() =>
                {
                    Assert.That(arrowComp.Activated, Is.True);
                    Assert.That(arrowComp.SelectedWarhead, Is.EqualTo(YautjaArrowWarhead.Emp));
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaEmpArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("EMP dynamic arrow"));
                });

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);

                var wieldable = entMan.GetComponent<WieldableComponent>(bow);
                Assert.That(wield.TryWield((bow, wieldable), hunter), Is.True);

                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var trigger = entMan.System<Content.Shared.Trigger.Systems.TriggerSystem>();
                var gunSystem = entMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                var gun = entMan.GetComponent<GunComponent>(bow);
                var target = entMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(Vector2.UnitX);
                Assert.That(gun.NextFire, Is.LessThanOrEqualTo(server.Timing.CurTime),
                    "The test waits out the normal equip/selection fire delay before asserting the live bow firing path.");
                var projectiles = gunSystem.AttemptShoot((bow, gun), hunter, target);

                Assert.That(projectiles, Is.Not.Null,
                    "A selected dynamic EMP arrow should create its EMP projectile through the real loaded-bow firing path.");
                projectile = projectiles!.Single();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(projectile.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaEmpArrowProjectile"),
                        "CMSS13 dynamic_warhead/change_warhead() stores the selected EMP ammo datum before bow load/fire.");
                    Assert.That(slots.GetItemOrNull(bow, "projectiles"), Is.Null,
                        "Firing the hunting bow should consume the selected dynamic arrow from the single internal arrow slot.");
                });

                bracerComp.Charge = 1500;
                trigger.Trigger(projectile.Value, hunter);

                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 500),
                    "CMSS13 dynamic EMP arrows use the EMP arrow ammo datum, so the fired projectile should drain a worn hunter bracer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();

                foreach (var uid in new[] { projectile, bow, arrow, bracer, hunter })
                {
                    if (uid is { } entity && !entMan.Deleted(entity))
                        entMan.DeleteEntity(entity);
                }

                if (mapId != default)
                    mapSystem.DeleteMap(mapId);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FiredSnareArrowUsesLiveBowShooterForCmss13TrapOwner()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await FireLoadedBowArrow(
            pair,
            "CMUYautjaSnareArrow",
            "CMUYautjaSnareArrowProjectile",
            "CMSS13 snare arrows should keep their snare ammo datum through the real loaded-bow firing path.",
            (entMan, projectile, hunter, target) =>
            {
                var hit = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref hit);

                var snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrapOwner, Is.EqualTo(hunter),
                        "The live bow path must propagate the firing hunter as ProjectileHitEvent.Shooter for CMSS13 trigger_snare() ownership.");
                    Assert.That(trap.TrappedMob, Is.EqualTo(target),
                        "The fired snare arrow should trap the hit target through the same trigger path as CMSS13.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True,
                        "CMSS13 trigger_snare() tethers the trapped target.");
                });
            });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileCanSnareYautjaTargetLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            EntityUid snare = default;

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(target);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.Armed, Is.False);
                    Assert.That(trap.TrapOwner, Is.EqualTo(hunter));
                    Assert.That(trap.TrappedMob, Is.EqualTo(target),
                        "CMSS13 /obj/item/arrow/snare/trigger_snare() does not inherit the placed hunting-trap Yautja Crossed() avoidance branch.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, target, snare })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileAutoDisarmsAfterCmss13ThirtySeconds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var appearance = entMan.System<SharedAppearanceSystem>();
                var timing = server.ResolveDependency<IGameTiming>();
                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

                var triggeredAt = timing.CurTime;
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrappedMob, Is.EqualTo(target));
                    Assert.That(trap.ReleaseAt - triggeredAt,
                        Is.EqualTo(TimeSpan.FromSeconds(30)).Within(TimeSpan.FromMilliseconds(50)),
                        "CMSS13 /obj/item/arrow/snare/trigger_snare() schedules disarm after 30 SECONDS.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                    Assert.That(appearance.TryGetData<bool>(snare, ToggleableVisuals.Enabled, out var active), Is.True);
                    Assert.That(active, Is.True);
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(30.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var appearance = entMan.System<SharedAppearanceSystem>();
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.Armed, Is.False);
                    Assert.That(trap.TrappedMob, Is.Null);
                    Assert.That(trap.ReleaseAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.False);
                    Assert.That(appearance.TryGetData<bool>(snare, ToggleableVisuals.Enabled, out var active), Is.True);
                    Assert.That(active, Is.False);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { projectile, hunter, target, snare })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileDisarmsWhenTetherDeletedLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            try
            {
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                var snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrappedMob, Is.EqualTo(target));
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                });

                entMan.RemoveComponent<RMCTetherComponent>(target);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.Armed, Is.False);
                    Assert.That(trap.TrappedMob, Is.Null);
                    Assert.That(trap.ReleaseAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.False);
                    Assert.That(appearance.TryGetData<bool>(snare, ToggleableVisuals.Enabled, out var active), Is.True);
                    Assert.That(active, Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, target })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileTrappedMobInteractResistsLikeCmss13AttackHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);
                trap.BreakFreeDelay = TimeSpan.FromSeconds(0.25);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrappedMob, Is.EqualTo(target));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                });

                var interact = new InteractHandEvent(target, snare);
                entMan.EventBus.RaiseLocalEvent(snare, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True,
                        "CMSS13 /obj/item/arrow/snare/attack_hand() returns after calling user.resist() when trapped_mob == user.");
                    Assert.That(trap.TrappedMob, Is.EqualTo(target),
                        "CMSS13 user.resist() does not instantly delete the tether; it starts the resist flow.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<DoAfterComponent>(target).DoAfters.Values.Count(active => !active.Cancelled && !active.Completed), Is.EqualTo(1));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var appearance = entMan.System<SharedAppearanceSystem>();
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrappedMob, Is.Null,
                        "CMSS13 snare arrows call apply_tether(..., resistible = TRUE); completing resist deletes the tether.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                    Assert.That(trap.ReleaseAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.False);
                    Assert.That(appearance.TryGetData<bool>(snare, ToggleableVisuals.Enabled, out var active), Is.True);
                    Assert.That(active, Is.False);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { projectile, hunter, target })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileNonTechBystanderCannotRecoverAnchoredSnareLikeCmss13AttackHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid bystander = default;
        EntityUid snare = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, -0.5f)));

                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));

                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);
                var interact = new InteractHandEvent(bystander, snare);
                entMan.EventBus.RaiseLocalEvent(snare, interact, true);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True,
                        "CMSS13 /obj/item/arrow/snare/attack_hand() on an anchored active snare only handles trapped-mob resist or Yautja-tech disarm; other users must not fall through into ordinary item pickup.");
                    Assert.That(trap.TrappedMob, Is.EqualTo(target));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                    Assert.That(hands.IsHolding(bystander, snare), Is.False);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { projectile, hunter, target, bystander, snare })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackDeploysInternalCannonsAndRetractsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                Assert.That(packComp.Cannon, Is.Not.Null,
                    "CMSS13 /obj/item/yautja_cannon_pack/Initialize() creates one internal dual plasma cannon.");
                var cannon = packComp.Cannon!.Value;
                var cannonMeta = entMan.GetComponent<MetaDataComponent>(cannon);

                Assert.Multiple(() =>
                {
                    Assert.That(cannonMeta.EntityPrototype?.ID, Is.EqualTo("CMUYautjaDualPlasmaCannons"));
                    Assert.That(packComp.CannonContainer, Is.Not.Null);
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True,
                        "Before activation, the source cannon lives inside the pack.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                });

                var deploy = new YautjaUsePlasmaCannonsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(pack, deploy);

                Assert.Multiple(() =>
                {
                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1950),
                        "CMSS13 cannon_internal() drains 50 power before placing cannons in hand.");
                    Assert.That(hands.IsHolding(hunter, cannon), Is.True,
                        "CMSS13 cannon_internal() puts the pack-created cannon in the user's active hand.");
                    Assert.That(packComp.CannonContainer.Contains(cannon), Is.False);
                    Assert.That(packComp.CannonsDeployed, Is.True);
                });

                var retract = new YautjaUsePlasmaCannonsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(pack, retract);

                Assert.Multiple(() =>
                {
                    Assert.That(retract.Handled, Is.True);
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1950),
                        "CMSS13 cannon_internal() does not drain charge when retracting already-deployed cannons.");
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False);
                    Assert.That(packComp.CannonContainer.Contains(cannon), Is.True,
                        "Retracting returns the same internal cannon to the pack.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackRetractsDroppedInternalCannonsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var getActions = new GetItemActionsEvent(actions, hunter, pack, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(pack, getActions);
                var action = getActions.Actions.Single();
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);

                Assert.Multiple(() =>
                {
                    Assert.That(packComp.CannonsDeployed, Is.True);
                    Assert.That(actionComp.Toggled, Is.True,
                        "CMSS13 cannon_internal() updates the plasma-cannons action icon to its deployed state.");
                    Assert.That(hands.IsHolding(hunter, cannon), Is.True);
                });

                Assert.That(hands.TryDrop(hunter, cannon, checkActionBlocker: false), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False);
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True,
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/cannon/dropped() forceMoves dropped cannons back into their source pack.");
                    Assert.That(packComp.CannonsDeployed, Is.False,
                        "CMSS13 cannon/dropped() clears source.cannons_deployed.");
                    Assert.That(actionComp.Toggled, Is.False,
                        "CMSS13 cannon/dropped() updates the plasma-cannons action icon to inactive.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackReturnsThrownInternalCannonsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var throwing = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var getActions = new GetItemActionsEvent(actions, hunter, pack, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(pack, getActions);
                var action = getActions.Actions.Single();
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);
                Assert.That(hands.IsHolding(hunter, cannon), Is.True);

                Assert.That(throwing.ThrowHeldItem(hunter, map.GridCoords.Offset(new Vector2(3, 0))), Is.False,
                    "A cannon linked to its pack cancels the throw before it can leave the hand.");

                Assert.Multiple(() =>
                {
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True,
                        "CMSS13 cannon/dropped() returns a thrown cannon to its source pack instead of leaving it on the map.");
                    Assert.That(packComp.CannonsDeployed, Is.False,
                        "Returning a thrown cannon clears source.cannons_deployed.");
                    Assert.That(actionComp.Toggled, Is.False,
                        "Returning a thrown cannon deactivates the pack action.");
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False);
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(cannon), Is.False,
                        "The returned cannon must not keep an active throw state.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackQDropRetractsInternalCannonsBeforeTheyReachTheFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var getActions = new GetItemActionsEvent(actions, hunter, pack, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(pack, getActions);
                var action = getActions.Actions.Single();
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);
                Assert.That(hands.IsHolding(hunter, cannon), Is.True);

                var qDropCoordinates = map.GridCoords.Offset(new Vector2(2, 0));
                Assert.That(hands.TryDrop(hunter, cannon, qDropCoordinates), Is.False,
                    "Q-drop must be intercepted before its target coordinates can pull the cannon back out of the pack.");

                Assert.Multiple(() =>
                {
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True,
                        "The linked cannon must be inside its source pack after Q-drop.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                    Assert.That(actionComp.Toggled, Is.False);
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackUnequipRetractsDeployedInternalCannonsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var getActions = new GetItemActionsEvent(actions, hunter, pack, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(pack, getActions);
                var action = getActions.Actions.Single();
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannon = packComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);
                Assert.That(packComp.CannonsDeployed, Is.True);

                Assert.That(inventory.TryUnequip(hunter, "back", force: true, silent: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, cannon), Is.False,
                        "CMSS13 keeps the cannon as pack-owned source gear; unequipping the source pack must not leave its deployed cannon in hand.");
                    Assert.That(packComp.CannonContainer!.Contains(cannon), Is.True,
                        "The internal cannon should return to its source pack when the pack leaves the back slot.");
                    Assert.That(packComp.CannonsDeployed, Is.False);
                    Assert.That(actionComp.Toggled, Is.False);
                    Assert.That(packComp.User, Is.Null,
                        "Unequipped cannon packs stop their wearer recharge association.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackActiveHandAndRoleGuardsMatchCmss13Ordering()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var activeBlockedHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var activeBlockedPack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var activeBlockedHeld = entMan.SpawnEntity("CMUYautjaCrowbar", map.GridCoords);
            var youngHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var youngPack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var thrallHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var thrallPack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

            try
            {
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                entMan.EnsureComponent<YautjaComponent>(activeBlockedHunter);
                Assert.That(inventory.TryEquip(activeBlockedHunter, activeBlockedPack, "back", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickup(activeBlockedHunter, activeBlockedHeld), Is.True);
                var activeBlockedComp = entMan.GetComponent<YautjaCannonPackComponent>(activeBlockedPack);
                var activeBlockedCannon = activeBlockedComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, activeBlockedPack, activeBlockedHunter, action, actionComp);

                Assert.Multiple(() =>
                {
                    Assert.That(activeBlockedComp.Charge, Is.EqualTo((FixedPoint2) 1950),
                        "CMSS13 cannon_internal() drains 50 power before checking whether the active hand is occupied.");
                    Assert.That(activeBlockedComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(activeBlockedHunter, activeBlockedCannon), Is.False,
                        "CMSS13 rejects deployment when get_active_hand() is non-null even if another hand is free.");
                    Assert.That(hands.GetActiveItem(activeBlockedHunter), Is.EqualTo(activeBlockedHeld));
                });

                PrepareCannonPackRoleGuardUser(entMan, inventory, youngHunter, youngPack);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(youngHunter);
                var youngComp = entMan.GetComponent<YautjaCannonPackComponent>(youngPack);
                var youngCannon = youngComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, youngPack, youngHunter, action, actionComp);

                Assert.Multiple(() =>
                {
                    Assert.That(youngComp.Charge, Is.EqualTo((FixedPoint2) 1950),
                        "CMSS13 checks young/thrall denial after the 50-power drain.");
                    Assert.That(youngComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(youngHunter, youngCannon), Is.False);
                });

                PrepareCannonPackRoleGuardUser(entMan, inventory, thrallHunter, thrallPack);
                var thrall = entMan.EnsureComponent<YautjaThrallComponent>(thrallHunter);
                thrall.Master = youngHunter;
                var thrallComp = entMan.GetComponent<YautjaCannonPackComponent>(thrallPack);
                var thrallCannon = thrallComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, thrallPack, thrallHunter, action, actionComp);

                Assert.Multiple(() =>
                {
                    Assert.That(thrallComp.Charge, Is.EqualTo((FixedPoint2) 1950),
                        "CMSS13 isthrall(user) denial also happens after the deploy-cost drain.");
                    Assert.That(thrallComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(thrallHunter, thrallCannon), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[]
                         {
                             activeBlockedHunter,
                             activeBlockedPack,
                             activeBlockedHeld,
                             youngHunter,
                             youngPack,
                             thrallHunter,
                             thrallPack,
                             action,
                         })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackRejectsIncapacitatedUserBeforeDrainLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var criticalHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var criticalPack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var deadHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var deadPack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

            try
            {
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                PrepareCannonPackRoleGuardUser(entMan, inventory, criticalHunter, criticalPack);
                PrepareCannonPackRoleGuardUser(entMan, inventory, deadHunter, deadPack);

                mobState.ChangeMobState(criticalHunter, MobState.Critical);
                mobState.ChangeMobState(deadHunter, MobState.Dead);

                var criticalComp = entMan.GetComponent<YautjaCannonPackComponent>(criticalPack);
                var deadComp = entMan.GetComponent<YautjaCannonPackComponent>(deadPack);
                var criticalCannon = criticalComp.Cannon!.Value;
                var deadCannon = deadComp.Cannon!.Value;

                RaiseUsePlasmaCannons(entMan, criticalPack, criticalHunter, action, actionComp);
                RaiseUsePlasmaCannons(entMan, deadPack, deadHunter, action, actionComp);

                Assert.Multiple(() =>
                {
                    Assert.That(criticalComp.Charge, Is.EqualTo((FixedPoint2) 2000),
                        "CMSS13 cannon_internal() returns immediately for is_mob_incapacitated() before the 50-power drain.");
                    Assert.That(deadComp.Charge, Is.EqualTo((FixedPoint2) 2000),
                        "Dead users are also incapacitated, so the cannon pack must not spend power.");
                    Assert.That(criticalComp.CannonsDeployed, Is.False);
                    Assert.That(deadComp.CannonsDeployed, Is.False);
                    Assert.That(hands.IsHolding(criticalHunter, criticalCannon), Is.False);
                    Assert.That(hands.IsHolding(deadHunter, deadCannon), Is.False);
                    Assert.That(criticalComp.CannonContainer!.Contains(criticalCannon), Is.True);
                    Assert.That(deadComp.CannonContainer!.Contains(deadCannon), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { criticalHunter, criticalPack, deadHunter, deadPack, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualPlasmaCannonsDrainSourcePackLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);
            var orphanCannons = entMan.SpawnEntity("CMUYautjaDualPlasmaCannons", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannons = packComp.Cannon!.Value;
                var tech = entMan.GetComponent<YautjaTechItemComponent>(cannons);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BatteryComponent>(cannons), Is.False,
                        "CMSS13 dual plasma cannons do not own charge; they spend their source cannon pack charge.");
                    Assert.That(tech.ShootDeniedPopup.Id, Is.EqualTo("cmu-yautja-spike-launcher-denied"),
                        "CMSS13 cannon/able_to_fire() tells non-tech users: You have no idea how this thing works!");
                });

                var coordinates = entMan.GetComponent<TransformComponent>(cannons).Coordinates;
                packComp.Charge = 2000;
                var takeAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(cannons, takeAmmo);

                Assert.Multiple(() =>
                {
                    Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(1),
                        "CMSS13 cannon/load_into_chamber() creates a projectile only after source.drain_power(user, charge_cost) succeeds.");
                    Assert.That(takeAmmo.Ammo[0].Entity, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(takeAmmo.Ammo[0].Entity!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaCasterLanceBolt"));
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1000),
                        "A successful dual-cannon shot drains 1000 charge from the source pack, not from a weapon-local battery.");
                });

                packComp.Charge = 500;
                var lowPower = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(cannons, lowPower);

                Assert.Multiple(() =>
                {
                    Assert.That(lowPower.Ammo, Is.Empty,
                        "CMSS13 cannon/has_ammunition() requires source.charge >= charge_cost.");
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 500),
                        "Failed source.drain_power() leaves pack charge unchanged.");
                });

                var orphanAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(orphanCannons, orphanAmmo);
                Assert.That(orphanAmmo.Ammo, Is.Empty,
                    "CMSS13 cannon/able_to_fire() and load_into_chamber() return false when the source pack link is missing.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack, orphanCannons })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualPlasmaCannonsRefundDeletedUnfiredProjectileLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannons = packComp.Cannon!.Value;
                var coordinates = entMan.GetComponent<TransformComponent>(cannons).Coordinates;

                packComp.Charge = 2000;
                var takeAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(cannons, takeAmmo);

                var projectile = takeAmmo.Ammo.Single().Entity!.Value;
                Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1000),
                    "CMSS13 cannon/load_into_chamber() drains the source pack before creating the projectile.");

                entMan.DeleteEntity(projectile);
                entMan.FlushEntities();

                Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 2000),
                    "CMSS13 dual plasma cannons delete_bullet(refund = 1) refunds charge_cost when an unfired chamber projectile is deleted.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualPlasmaCannonsDoNotRefundFiredProjectileDeletionLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var cannons = packComp.Cannon!.Value;
                var coordinates = entMan.GetComponent<TransformComponent>(cannons).Coordinates;

                packComp.Charge = 2000;
                var takeAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(cannons, takeAmmo);

                var projectile = takeAmmo.Ammo.Single().Entity!.Value;
                entMan.EventBus.RaiseLocalEvent(cannons, new AmmoShotEvent
                {
                    FiredProjectiles = new List<EntityUid> { projectile },
                });

                entMan.DeleteEntity(projectile);
                entMan.FlushEntities();

                Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1000),
                    "CMSS13 fired dual-cannon projectiles are not refund deletes; otherwise live shots would be free after impact/deletion.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, pack })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualPlasmaCannonsClientPredictionUsesPackPowerAndPreventsFloorDrop()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid pack = default;
        EntityUid cannon = default;
        EntityUid action = default;
        NetEntity hunterNet = default;
        NetEntity cannonNet = default;
        NetEntity packNet = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
                action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                server.PlayerMan.SetAttachedEntity(session, hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                packComp.Charge = 2000;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);

                cannon = packComp.Cannon!.Value;
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(cannon, Is.Not.EqualTo(default(EntityUid)));
                hunterNet = entMan.GetNetEntity(hunter);
                cannonNet = entMan.GetNetEntity(cannon);
                packNet = entMan.GetNetEntity(pack);
                Assert.That(cannonNet, Is.Not.EqualTo(default(NetEntity)),
                    "The deployed cannon must have a network entity before the client prediction check.");
            });

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var clientHunter = entMan.GetEntity(hunterNet);
                var clientCannon = entMan.GetEntity(cannonNet);
                var gun = entMan.GetComponent<GunComponent>(clientCannon);

                Assert.That(entMan.HasComponent<YautjaCannonPackLinkedCannonComponent>(clientCannon), Is.True,
                    "The deployed cannon must expose its source-pack link to the client for prediction.");

                var target = entMan.GetComponent<TransformComponent>(clientHunter).Coordinates.Offset(new Vector2(10, 0));
                var projectiles = entMan.System<SharedGunSystem>().AttemptShoot((clientCannon, gun), clientHunter, target);

                Assert.That(projectiles, Is.Not.Null,
                    "Client-predicted dual cannon fire must provide a projectile instead of falling through to the empty-ammo popup.");
                Assert.That(projectiles, Has.Count.EqualTo(1));

                var drop = new RMCItemDropAttemptEvent(false);
                entMan.EventBus.RaiseLocalEvent(clientCannon, ref drop);
                Assert.That(drop.Cancelled, Is.True,
                    "Dropping a deployed dual cannon must be intercepted before it reaches the floor.");
                Assert.That(entMan.System<SharedHandsSystem>().IsHolding(clientHunter, clientCannon), Is.False);

                var clientPack = entMan.GetEntity(packNet);
                var containers = entMan.System<SharedContainerSystem>();
                Assert.That(containers.TryGetContainingContainer((clientCannon, null, null), out var containing), Is.True);
                Assert.That(containing.Owner, Is.EqualTo(clientPack),
                    "The client-side drop interception must move the cannon into its source pack.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                foreach (var uid in new[] { hunter, pack, action })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentHolderDeploysAttachedWeaponAndRetractsItLikeCmss13()
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
            var holder = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 200;

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var install = new InteractUsingEvent(hunter, holder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var holderStored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
                Assert.That(install.Handled, Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                var gearActions = new GetItemActionsEvent(entMan.System<ActionContainerSystem>(), hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, gearActions);
                var gearActionIds = ActionPrototypeIds(entMan, gearActions.Actions);
                Assert.Multiple(() =>
                {
                    Assert.That(gearActionIds, Does.Not.Contain("CMUActionYautjaRemoveBracerAttachments"));
                    Assert.That(gearActionIds, Does.Contain("CMUActionYautjaToggleScimitar"),
                        "Installed gear deployment actions remain available on the worn bracer.");
                });
                Assert.That(gearComp.Container!.Contains(holder), Is.True,
                    "CMSS13 bracer attachment holder remains the installed item in the bracer.");
                Assert.That(gearComp.InstalledGear, Does.Contain(holder));
                Assert.That(holderStored.AttachedWeapon, Is.Not.Null,
                    "CMSS13 holder Initialize() creates the attached_weapon_type inside the holder.");

                var deployed = holderStored.AttachedWeapon!.Value;
                Assert.That(entMan.GetComponent<MetaDataComponent>(deployed).EntityPrototype?.ID, Is.EqualTo("CMUYautjaScimitar"));
                Assert.That(holderStored.AttachedContainer, Is.Not.Null);
                Assert.That(holderStored.AttachedContainer!.Contains(deployed), Is.True,
                    "Before deploy, the deployed weapon should be stored inside the holder item.");

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var deploy = new YautjaToggleScimitarActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                var deployedStored = entMan.GetComponent<YautjaStoredGearComponent>(deployed);
                Assert.Multiple(() =>
                {
                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, deployed), Is.True,
                        "Deploy should put CMSS13 attached_weapon_type in hand, not the holder item.");
                    Assert.That(hands.IsHolding(hunter, holder), Is.False,
                        "The holder item should stay installed in the bracer.");
                    Assert.That(gearComp.Container.Contains(holder), Is.True);
                    Assert.That(holderStored.AttachedContainer.Contains(deployed), Is.False);
                    Assert.That(holderStored.Deployed, Is.True);
                    Assert.That(deployedStored.AttachmentHolder, Is.EqualTo(holder));
                    Assert.That(deployedStored.Deployed, Is.True);
                    Assert.That(gearComp.InstalledGear, Does.Contain(holder));
                    Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(holder));
                });

                var retract = new YautjaToggleScimitarActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, retract);

                Assert.Multiple(() =>
                {
                    Assert.That(retract.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, deployed), Is.False);
                    Assert.That(hands.IsHolding(hunter, holder), Is.False);
                    Assert.That(gearComp.Container.Contains(holder), Is.True);
                    Assert.That(holderStored.AttachedContainer.Contains(deployed), Is.True,
                        "Retract should put the CMSS13 attached_weapon_type back inside its holder item.");
                    Assert.That(holderStored.Deployed, Is.False);
                    Assert.That(deployedStored.AttachmentHolder, Is.EqualTo(holder));
                    Assert.That(deployedStored.Deployed, Is.False);
                    Assert.That(gearComp.InstalledGear, Does.Contain(holder));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, holder, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerDestroyDeletesDeployedAttachmentWeaponLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid holder = default;
        EntityUid deployed = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                holder = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
                action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 200;

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

                var install = new InteractUsingEvent(
                    hunter,
                    holder,
                    bracer,
                    entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                var holderStored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
                deployed = holderStored.AttachedWeapon!.Value;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var deploy = new YautjaToggleScimitarActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                Assert.That(deploy.Handled, Is.True);
                Assert.That(hands.IsHolding(hunter, deployed), Is.True);
                Assert.That(holderStored.AttachedContainer!.Contains(deployed), Is.False);

                entMan.DeleteEntity(bracer);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(bracer), Is.True);
                    Assert.That(entMan.Deleted(holder), Is.True,
                        "CMSS13 hunter bracer Destroy() QDEL_NULLs installed bracer attachments.");
                    Assert.That(entMan.Deleted(deployed), Is.True,
                        "CMSS13 bracer attachment Destroy() QDEL_NULLs its attached_weapon even if deployed in-hand.");
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, holder, deployed, action })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowRequiresYautjaStrengthAndBeingHeldToLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var slots = entMan.System<ItemSlotsSystem>();

            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);
            var nonYautjaArrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var notHeldArrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var loadedArrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonYautja);
                Assert.That(hands.TryPickupAnyHand(nonYautja, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", nonYautjaArrow, nonYautja), Is.False);

                Assert.That(hands.IsHolding(nonYautja, bow), Is.True);
                Assert.That(hands.TryDrop(nonYautja, bow), Is.True);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(slots.TryInsert(bow, "projectiles", notHeldArrow, hunter), Is.False);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", loadedArrow, hunter), Is.True);

                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                Assert.That(itemSlots.Slots["projectiles"].Item, Is.EqualTo(loadedArrow));
            }
            finally
            {
                foreach (var uid in new[] { bow, nonYautjaArrow, notHeldArrow, loadedArrow, nonYautja, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppingLoadedHuntingBowEjectsArrowLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var slots = entMan.System<ItemSlotsSystem>();

            mapSystem.CreateMap(out var mapId);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", coordinates);
            var arrow = entMan.SpawnEntity("CMUYautjaArrow", coordinates);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);

                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                Assert.That(itemSlots.Slots["projectiles"].Item, Is.EqualTo(arrow));

                Assert.That(hands.TryDrop(hunter, bow), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(itemSlots.Slots["projectiles"].Item, Is.Null,
                        "CMSS13 /obj/item/weapon/gun/bow/dropped() calls unload() when a loaded bow is dropped.");
                    Assert.That(entMan.GetComponent<TransformComponent>(arrow).ParentUid, Is.Not.EqualTo(bow),
                        "The loaded arrow should be ejected from the bow slot instead of staying contained in the dropped bow.");
                });
            }
            finally
            {
                foreach (var uid in new[] { bow, arrow, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                mapSystem.DeleteMap(mapId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaSmartDiscUseCanFiddleWithoutActivatingLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid disc = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var random = server.ResolveDependency<IRobustRandom>();

            user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

            random.SetSeed(0);
            Assert.That(hands.TryPickupAnyHand(user, disc), Is.True);

            var use = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(disc, use);

            var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
            Assert.Multiple(() =>
            {
                Assert.That(use.Handled, Is.True,
                    "CMSS13 smartdisc attack_self() consumes a non-Yautja use even when the 75 percent fiddle chance returns early.");
                Assert.That(smartDisc.Active, Is.False,
                    "CMSS13 non-Yautja smartdisc attack_self() has a 75 percent early return before activate(user).");
                Assert.That(smartDisc.RogueTarget, Is.Null);
                Assert.That(smartDisc.CurrentTarget, Is.Null);
                Assert.That(entMan.GetComponent<ItemToggleComponent>(disc).Activated, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscFailedRetargetClearsStalePreyAndAcquiresNewArrival()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid disc = default;
        EntityUid departedPrey = default;
        EntityUid arrivingPrey = default;
        var retargetDelay = TimeSpan.FromSeconds(0.1);

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var toggle = entMan.System<ItemToggleSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
                departedPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5, 0)));

                BindRecallable(entMan, disc, hunter);

                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                smartDisc.RetargetDelay = retargetDelay;

                Assert.That(toggle.TrySetActive((disc, null), true, hunter, false), Is.True);
                Assert.That(smartDisc.CurrentTarget, Is.EqualTo(departedPrey));

                entMan.DeleteEntity(departedPrey);
            });

            await pair.RunTicksSync(pair.SecondsToTicks((float) retargetDelay.TotalSeconds + 0.05f));

            await server.WaitAssertion(() =>
            {
                var smartDisc = server.EntMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.That(smartDisc.Active, Is.True);
                Assert.That(smartDisc.CurrentTarget, Is.Null,
                    "A failed CMSS13-style target search must forget prey that has left before the next search window.");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var discCoordinates = transform.GetMapCoordinates(disc);
                arrivingPrey = entMan.SpawnEntity(
                    "CMMobHuman",
                    new MapCoordinates(discCoordinates.Position + new Vector2(4, 0), discCoordinates.MapId));
            });

            await pair.RunTicksSync(pair.SecondsToTicks((float) retargetDelay.TotalSeconds + 0.05f));

            await server.WaitAssertion(() =>
            {
                var smartDisc = server.EntMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.That(smartDisc.Active, Is.True);
                Assert.That(smartDisc.CurrentTarget, Is.EqualTo(arrivingPrey),
                    "After a failed search, the next retarget window must acquire prey that enters range later.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, disc, departedPrey, arrivingPrey })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscTargetingIgnoresFriendlyFactionLikeCmss13HostileDisc()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var faction = entMan.System<NpcFactionSystem>();
            var toggle = entMan.System<ItemToggleSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            var friendlyNonYautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var deadPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                BindRecallable(entMan, disc, hunter);
                faction.AddFaction((friendlyNonYautja, null), CMUYautjaPrototype);
                entMan.System<MobStateSystem>().ChangeMobState(deadPrey, MobState.Dead);

                Assert.That(toggle.TrySetActive((disc, null), true, hunter, false), Is.True);

                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                Assert.Multiple(() =>
                {
                    Assert.That(smartDisc.Active, Is.True);
                    Assert.That(smartDisc.CurrentTarget, Is.EqualTo(prey),
                        "CMSS13 smartdisc FindTarget() skips same-faction mobs before selecting a living non-Yautja target.");
                    Assert.That(smartDisc.CurrentTarget, Is.Not.EqualTo(friendlyNonYautja));
                    Assert.That(smartDisc.CurrentTarget, Is.Not.EqualTo(deadPrey),
                        "CMSS13 smartdisc FindTarget() skips DEAD mobs.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, disc, friendlyNonYautja, prey, deadPrey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaSmartDiscImpactIsCaughtWithoutThrowDamageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var physicsSystem = entMan.System<SharedPhysicsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<StaminaComponent>(hunter);
                var damageable = entMan.GetComponent<DamageableComponent>(hunter);
                var stamina = entMan.GetComponent<StaminaComponent>(hunter);
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;
                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                smartDisc.Active = true;
                smartDisc.Hits = 3;

                var hit = new ThrowDoHitEvent(disc, hunter, thrown);
                entMan.EventBus.RaiseLocalEvent(disc, ref hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, disc), Is.True,
                        "CMSS13 smartdisc launch_impact() makes a Yautja put the disc in hand.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.False,
                        "CMSS13 smartdisc launch_impact() sets throwing = FALSE after a successful catch.");
                    Assert.That(smartDisc.Active, Is.False,
                        "A caught local smart-disc should be returned to the inactive item state.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, damageable)).GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 smartdisc catch returns before parent launch_impact(), so throwforce damage is not applied to the Yautja catcher.");
                    Assert.That(stamina.StaminaDamage, Is.EqualTo(0f),
                        "CMSS13 smartdisc catch returns before parent launch_impact(), so local collide stamina is not applied to the Yautja catcher.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, disc })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumansCantGrabAYautjaThrownSmartDiscOutOfTheAir()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));

            var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
            thrown.Thrower = hunter;
            Assert.That(entMan.GetComponent<YautjaSmartDiscComponent>(disc).Active, Is.False, "the boomerang throw isn't the hunting mode");
            Assert.That(hands.TryPickupAnyHand(marine, disc), Is.False,
                "cmss13 only lets a throw mode catch take a flying disc, a plain click shouldn't");

            entMan.RemoveComponent<ThrownItemComponent>(disc);
            Assert.That(hands.TryPickupAnyHand(marine, disc), Is.True, "a disc lying on the floor is still loot");

            foreach (var uid in new[] { hunter, marine, disc })
            {
                if (!entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrownSmartDiscBoomerangIgnoresPreyOutsideCmss13FourTileSearch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            var distantPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(6, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                BindRecallable(entMan, disc, hunter);

                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;
                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                smartDisc.PendingThrowActivator = hunter;
                smartDisc.PendingThrowActivationAt = TimeSpan.Zero;

                entMan.System<YautjaSmartDiscSystem>().Update(0.5f);

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                Assert.Multiple(() =>
                {
                    Assert.That(smartDisc.Active, Is.False,
                        "CMSS13 smartdisc boomerang(user) never calls activate(); it remains item-level boomerang motion.");
                    Assert.That(smartDisc.SearchRange, Is.EqualTo(7f),
                        "The spawned hostile smart-disc search range remains CMSS13 ListTargets(7).");
                    Assert.That(smartDisc.CurrentTarget, Is.Null,
                        "CMSS13 item-level smartdisc find_target(user) calls listtargets(4), so prey beyond four tiles is ignored.");
                    Assert.That(smartDisc.ReturningToOwner, Is.True,
                        "CMSS13 boomerang() always follows with throw_atom(user, 12, SPEED_SLOW, user).");
                    Assert.That(physics.LinearVelocity.X, Is.LessThan(0f),
                        "With no source-valid prey found, the first local movement should be the return leg toward the owner.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, disc, distantPrey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrownSmartDiscBoomerangReturnsToOwnerAfterPreyLegLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var thrownSystem = entMan.System<ThrownItemSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                BindRecallable(entMan, disc, hunter);

                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;
                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                smartDisc.PendingThrowActivator = hunter;
                smartDisc.PendingThrowActivationAt = TimeSpan.Zero;

                entMan.System<YautjaSmartDiscSystem>().Update(0.5f);
                Assert.That(smartDisc.CurrentTarget, Is.EqualTo(prey));

                thrownSystem.StopThrow(disc, entMan.GetComponent<ThrownItemComponent>(disc));
                entMan.System<YautjaSmartDiscSystem>().Update(0.5f);

                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                var returnThrow = entMan.GetComponent<ThrownItemComponent>(disc);
                Assert.Multiple(() =>
                {
                    Assert.That(smartDisc.Active, Is.False,
                        "CMSS13 smartdisc boomerang(user) stays as an inactive thrown item across the prey pass.");
                    Assert.That(smartDisc.CurrentTarget, Is.Null,
                        "After the first throw_atom(get_turf(L), ...) completes, CMSS13 boomerang() has already queued throw_atom(user, ...).");
                    Assert.That(smartDisc.ReturningToOwner, Is.True,
                        "The source always schedules the owner return after the optional prey pass.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True,
                        "The local inactive boomerang return leg must be a fresh thrown item after StopThrow removes the first leg component.");
                    Assert.That(returnThrow.LandTime, Is.Not.Null);
                    Assert.That(returnThrow.LandTime, Is.GreaterThan(TimeSpan.Zero),
                        "The return leg must not inherit the inactive disc's zero active lifetime as an immediate stop time.");
                    Assert.That(physics.LinearVelocity.X, Is.LessThan(0f),
                        "With the owner to the left, the second boomerang leg should travel back toward the owner.");
                    Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                    Assert.That(activeVisual, Is.True,
                        "CMSS13 clear_boomerang() is timer-based and should not clear the visual when the first prey leg ends.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, disc, prey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CallDiscRecallsScreenwideDiscsWithCmss13PowerAndCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            // CMU14: recall reaches about one screen (15 tiles) in every direction, and the active disc is no longer
            // held to CM's stricter 7-tile range. Anything beyond the screen stays where it is.
            var nearbyInactiveDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(14, 0)));
            var activeInRangeDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(12, 6)));
            var activeOutOfRangeDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(20, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;

                var smartSystem = entMan.System<YautjaSmartDiscSystem>();
                var activeInRangeComp = entMan.GetComponent<YautjaSmartDiscComponent>(activeInRangeDisc);
                var activeOutOfRangeComp = entMan.GetComponent<YautjaSmartDiscComponent>(activeOutOfRangeDisc);
                activeInRangeComp.Active = true;
                activeInRangeComp.YautjaOwner = hunter;
                entMan.GetComponent<ItemToggleComponent>(activeInRangeDisc).Activated = true;
                activeOutOfRangeComp.Active = true;
                activeOutOfRangeComp.YautjaOwner = hunter;
                entMan.GetComponent<ItemToggleComponent>(activeOutOfRangeDisc).Activated = true;
                var activeOutOfRangeStart = transform.GetMapCoordinates(activeOutOfRangeDisc);

                Assert.That(smartSystem.TryCallDisc((bracer, bracerComp), hunter), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230),
                        "CMSS13 call_disc_internal() drains 70 power on successful use.");
                    Assert.That(hands.IsHolding(hunter, nearbyInactiveDisc), Is.True,
                        "an inactive smart-disc 14 tiles away is inside the screenwide recall range.");
                    Assert.That(hands.IsHolding(hunter, activeInRangeDisc), Is.True,
                        "an active smart-disc ~13 tiles away is also recalled - the active range is screenwide too.");
                    Assert.That(activeInRangeComp.Active, Is.False,
                        "Recalling a local active smart-disc returns it to the inactive held item state.");
                    Assert.That(hands.IsHolding(hunter, activeOutOfRangeDisc), Is.False,
                        "a disc 20 tiles away is past the screenwide range and stays in the field.");
                    Assert.That(transform.GetMapCoordinates(activeOutOfRangeDisc).Position, Is.EqualTo(activeOutOfRangeStart.Position));
                });

                Assert.That(smartSystem.TryCallDisc((bracer, bracerComp), hunter), Is.False);
                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230),
                    "CMSS13-style local call-disc cooldown blocks immediate repeat use before draining power.");

                bracerComp.NextCallDisc = TimeSpan.Zero;
                Assert.That(smartSystem.TryCallDisc((bracer, bracerComp), hunter), Is.True);
                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 160),
                    "After the cooldown expires, call-disc can spend another 70 power.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, nearbyInactiveDisc, activeInRangeDisc, activeOutOfRangeDisc })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveSmartDiscStopsAsRecoverableItemAfterMaxHitsLikeCmss13HostileReturn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var physicsSystem = entMan.System<SharedPhysicsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.5f, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var toggle = entMan.GetComponent<ItemToggleComponent>(disc);
                var physics = entMan.GetComponent<PhysicsComponent>(disc);
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;

                smartDisc.Active = true;
                smartDisc.YautjaOwner = hunter;
                smartDisc.CurrentTarget = prey;
                smartDisc.Hits = smartDisc.MaxHits - 1;
                toggle.Activated = true;
                physicsSystem.SetLinearVelocity(disc, new Vector2(3, 0), body: physics);
                physicsSystem.SetAngularVelocity(disc, 5f, body: physics);
                physicsSystem.SetBodyStatus(disc, physics, BodyStatus.InAir);

                var hit = new ThrowDoHitEvent(disc, prey, thrown);
                entMan.EventBus.RaiseLocalEvent(disc, ref hit);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(disc), Is.False,
                        "CMSS13 hostile smart-disc death/gib returns a pickup smart-disc item instead of losing the disc.");
                    Assert.That(smartDisc.Active, Is.False,
                        "Local item-based hostile-disc equivalent should stop after its max-hit lifetime.");
                    Assert.That(toggle.Activated, Is.False,
                        "Stopping the local active smart-disc must also deactivate item visuals/state.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.False,
                        "The recoverable returned item should no longer be in the active thrown-disc state.");
                    Assert.That(physics.LinearVelocity, Is.EqualTo(Vector2.Zero));
                    Assert.That(physics.AngularVelocity, Is.EqualTo(0f));
                    Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
                    Assert.That(hands.TryPickupAnyHand(hunter, disc, checkActionBlocker: false), Is.True,
                        "The returned local item must be immediately recoverable by the owner.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, disc, prey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpikeLauncherRechargeProcessCadenceRetriesLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid launcher = default;
        TimeSpan dueAtBoundary = default;
        TimeSpan failedDueAt = default;
        TimeSpan successAttemptStart = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var gun = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();

                launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", MapCoordinates.Nullspace);
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount((launcher, ammo), 11), Is.True);
                recharge.RechargeChance = 1f;
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);
                var timing = server.ResolveDependency<IGameTiming>();

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Count, Is.EqualTo(11),
                        "CMSS13 Initialize() sets last_regen = world.time, so losing a spike immediately after spawn cannot regenerate until after the first 10 second process window.");
                    Assert.That(recharge.NextCharge, Is.GreaterThan(timing.CurTime),
                        "CMSS13 spike launcher first regeneration threshold is last_regen + 100 deciseconds after map init.");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var timing = server.ResolveDependency<IGameTiming>();
                var gun = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount((launcher, ammo), 11), Is.True);
                recharge.RechargeChance = 1f;
                dueAtBoundary = timing.CurTime;
                recharge.NextCharge = dueAtBoundary;
                server.System<YautjaSpikeLauncherSystem>().Update(0);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Count, Is.EqualTo(11),
                        "CMSS13 uses world.time > last_regen + 100, so equality with the threshold is not due yet.");
                    Assert.That(recharge.NextCharge, Is.EqualTo(dueAtBoundary));
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var timing = server.ResolveDependency<IGameTiming>();
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                successAttemptStart = timing.CurTime;
                recharge.NextCharge = timing.CurTime - timing.TickPeriod;
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Count, Is.EqualTo(12),
                        "CMSS13 spike launcher regenerates once the strict threshold is exceeded and prob(70) succeeds.");
                    Assert.That(recharge.NextCharge, Is.GreaterThan(successAttemptStart),
                        "CMSS13 updates last_regen after a successful regeneration even when spikes reaches max_spikes.");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var timing = server.ResolveDependency<IGameTiming>();
                var gun = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount((launcher, ammo), 10), Is.True);
                recharge.RechargeChance = 0f;
                failedDueAt = timing.CurTime - TimeSpan.FromSeconds(1);
                recharge.NextCharge = failedDueAt;
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Count, Is.EqualTo(10),
                        "CMSS13 spike launcher process only increments spikes when prob(70) succeeds.");
                    Assert.That(recharge.NextCharge, Is.EqualTo(failedDueAt),
                        "CMSS13 failed prob(70) rolls do not update last_regen, so the next process tick can retry.");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var timing = server.ResolveDependency<IGameTiming>();
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                recharge.RechargeChance = 1f;
                successAttemptStart = timing.CurTime;
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<YautjaSpikeLauncherComponent>(launcher);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Count, Is.EqualTo(11),
                        "A due failed roll should retry on the next process tick and regenerate when the chance succeeds.");
                    Assert.That(recharge.NextCharge, Is.GreaterThanOrEqualTo(successAttemptStart + TimeSpan.FromSeconds(10)),
                        "CMSS13 updates last_regen to world.time only after a successful regeneration.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (launcher != default && !server.EntMan.Deleted(launcher))
                    server.EntMan.DeleteEntity(launcher);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpikeLauncherRefundsDeletedUnfiredSpikeButNotFiredSpikeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", map.GridCoords);
            EntityUid? unfiredProjectile = null;
            EntityUid? firedProjectile = null;

            try
            {
                var provider = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                Assert.That(provider.Count, Is.EqualTo(12));

                var firstAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(launcher).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(launcher, firstAmmo);
                unfiredProjectile = firstAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(unfiredProjectile, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(unfiredProjectile!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaSpikeProjectile"));
                    Assert.That(provider.Count, Is.EqualTo(11),
                        "CMSS13 spike launcher load_into_chamber() decrements spikes after creating a projectile.");
                });

                entMan.DeleteEntity(unfiredProjectile!.Value);
                unfiredProjectile = null;

                Assert.That(provider.Count, Is.EqualTo(12),
                    "CMSS13 spike launcher delete_bullet(refund = TRUE) returns an unfired prepared spike.");

                var secondAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(launcher).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(launcher, secondAmmo);
                firedProjectile = secondAmmo.Ammo.Single().Entity;
                Assert.That(provider.Count, Is.EqualTo(11));

                var fired = new AmmoShotEvent
                {
                    FiredProjectiles = [firedProjectile!.Value],
                };
                entMan.EventBus.RaiseLocalEvent(launcher, fired);
                entMan.DeleteEntity(firedProjectile.Value);
                firedProjectile = null;

                Assert.That(provider.Count, Is.EqualTo(11),
                    "CMSS13 only refunds deleted chamber projectiles; fired spikes keep the spent ammo count.");
            }
            finally
            {
                foreach (var uid in new[] { unfiredProjectile, firedProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, launcher })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterTechAuthorizedUsersFireAndSpendBracerPowerLikeCmss13Trait()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
            EntityUid? stunProjectile = null;
            EntityUid? lethalProjectile = null;

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);
                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);

                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;

                var bracerPower = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerPower.Charge = 200;

                var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;
                var stunAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                entMan.EventBus.RaiseLocalEvent(caster, ref stunAttempt);

                Assert.That(stunAttempt.Cancelled, Is.False,
                    "CMSS13 plasma caster able_to_fire() accepts source-linked users with TRAIT_YAUTJA_TECH, not only Yautja mobs.");

                var stunAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), userCoords, user);
                entMan.EventBus.RaiseLocalEvent(caster, stunAmmo);
                stunProjectile = stunAmmo.Ammo.Single().Entity;
                var casterPowerCost = entMan.GetComponent<YautjaCasterComponent>(caster).PowerCost;
                Assert.That(bracerPower.Charge, Is.EqualTo((FixedPoint2) 200 - casterPowerCost),
                    "CMSS13 plasma_caster/load_into_chamber() drains the caster's current charge_cost from the source bracer.");

                var lethalMode = new UniqueActionEvent(user);
                entMan.EventBus.RaiseLocalEvent(caster, lethalMode);

                Assert.Multiple(() =>
                {
                    Assert.That(lethalMode.Handled, Is.True);
                    Assert.That(entMan.GetComponent<YautjaCasterComponent>(caster).CurrentMode, Is.EqualTo(2),
                        "CMSS13 use_unique_action() switches stun mode to lethal mode for Yautja-tech users.");
                });

                var lethalAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                entMan.EventBus.RaiseLocalEvent(caster, ref lethalAttempt);
                Assert.That(lethalAttempt.Cancelled, Is.False);

                var lethalAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), userCoords, user);
                entMan.EventBus.RaiseLocalEvent(caster, lethalAmmo);
                lethalProjectile = lethalAmmo.Ammo.Single().Entity;
                Assert.That(bracerPower.Charge, Is.EqualTo(FixedPoint2.Zero),
                    "CMSS13 plasma_caster/load_into_chamber() drains plasma bolt charge_cost = 100 from source bracer.");
            }
            finally
            {
                foreach (var uid in new[] { stunProjectile, lethalProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { user, bracer, caster })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackExhaustedMainWeaponClaimDeniesLikeCmss13Category()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var mainIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(mainIndex, Is.GreaterThanOrEqualTo(0));

                var main = vendor.Sections[mainIndex];
                var primarySwordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                var rendingSwordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaRendingSword");
                Assert.That(primarySwordIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(rendingSwordIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(main.Choices, Is.Not.Null);
                Assert.That(main.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(main.Choices.Value.Amount, Is.EqualTo(1));

                var primarySwordsBefore = EntityPrototypeIds(entMan, "CMUYautjaClanSword").Count();
                var rendingSwordsBefore = EntityPrototypeIds(entMan, "CMUYautjaRendingSword").Count();

                Vend(entMan, rack, hunter, mainIndex, primarySwordIndex);

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaClanSword").Count(), Is.EqualTo(primarySwordsBefore + 1));

                Vend(entMan, rack, hunter, mainIndex, rendingSwordIndex);

                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1),
                    "CMSS13 handle_vend() refuses an exhausted vendor_buyable_categories entry without consuming another claim.");
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaRendingSword").Count(), Is.EqualTo(rendingSwordsBefore),
                    "CMSS13 handle_vend() refuses an exhausted buy category before spawning the item.");
            }
            finally
            {
                foreach (var uid in new[] { rack, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReadiedCombistickUsesCmss13TwoHandedDirectionalShieldBlock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var blocking = entMan.System<BlockingSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var frontAttacker = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, -1)));
            var backAttacker = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.System<SharedTransformSystem>().SetLocalRotation(hunter, Angle.Zero);

                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var weaponDamage = entMan.GetComponent<DamageableComponent>(combistick);
                var incoming = new DamageSpecifier { DamageDict = { ["Piercing"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);
                var unblockedPiercing = entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Piercing");
                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(combistick);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(combistick);
                sourceBlock.ReadiedBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;
                Assert.That(blocking.RaiseShield((combistick, blockingComponent), hunter), Is.True);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.LessThan(unblockedPiercing),
                        "An extended, readied combistick should use the CMSS13 two-handed directional shield path against front attacks.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((combistick, weaponDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.GreaterThan(FixedPoint2.Zero),
                        "A successful source shield block transfers damage into the defensive combistick.");
                });

                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);
                damageable.SetAllDamage((combistick, weaponDamage), FixedPoint2.Zero);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: backAttacker, tool: backAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(unblockedPiercing),
                        "CMSS13 SHIELD_DIRECTIONAL_TWOHANDS should not block attacks from behind.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((combistick, weaponDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero),
                        "Back attacks should not transfer damage into the combistick.");
                });

                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);
                damageable.SetAllDamage((combistick, weaponDamage), FixedPoint2.Zero);
                Assert.That(blocking.LowerShield((combistick, blockingComponent), hunter), Is.True);
                sourceBlock.ReadiedBlock = YautjaSourceShieldChance.None;
                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(unblockedPiercing),
                        "The located CMSS13 combistick source has no passive_block row; passive local blocking must remain disabled.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((combistick, weaponDamage)).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero),
                        "A passive combistick shield roll should not transfer damage.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, combistick, frontAttacker, backAttacker })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaSourceShieldBlockChanceGatesRuntimeBlocking()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shield = entMan.SpawnEntity("CMUYautjaBracerShield", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var shieldDamage = entMan.GetComponent<DamageableComponent>(shield);
                var incoming = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));
                var unblockedBlunt = entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt");
                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                Assert.That(entMan.HasComponent<BlockingUserComponent>(hunter), Is.True);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(unblockedBlunt),
                        "A failed CMSS13 shield_chance roll should leave the incoming damage unblocked.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((shield, shieldDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.Zero),
                        "The shield should only absorb damage after the chance roll succeeds.");
                });

                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);
                damageable.SetAllDamage((shield, shieldDamage), FixedPoint2.Zero);

                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.LessThan(unblockedBlunt),
                        "A successful passive source block should use the existing local passive block fraction.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((shield, shieldDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.GreaterThan(FixedPoint2.Zero),
                        "The shield should receive the transferred damage through its own passive block modifier.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, shield })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornBackYautjaShieldsRespectCmss13BlocksOnBackFalse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var blocking = entMan.System<BlockingSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shield = entMan.SpawnEntity("CMUYautjaClanShield", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var shieldDamage = entMan.GetComponent<DamageableComponent>(shield);
                var incoming = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));
                var unblockedBlunt = entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt");
                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.RaiseShield((shield, blockingComponent), hunter), Is.True);
                Assert.That(blockingComponent.IsRaised, Is.True);

                Assert.That(hands.TryDrop(hunter, shield), Is.True);
                Assert.That(inventory.TryEquip(hunter, shield, "back", silent: true, force: true), Is.True);
                Assert.That(blockingComponent.IsRaised, Is.False,
                    "Moving a readied Yautja shield out of the hand must clear active blocking before it reaches the back slot.");

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                Assert.That(sourceBlock.BlocksOnBack, Is.False,
                    "CMSS13 /obj/item/weapon/shield/riot/yautja sets blocks_on_back = FALSE.");
                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.ReadiedBlock = YautjaSourceShieldChance.None;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(unblockedBlunt),
                        "A Yautja shield with source blocks_on_back = FALSE must not passively block while worn on the back.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((shield, shieldDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.Zero),
                        "Back-slot shields that fail the source back-block gate should not receive transferred block damage.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, shield })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaDirectionalShieldOnlyBlocksReadiedAttacksFromFront()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var blocking = entMan.System<BlockingSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shield = entMan.SpawnEntity("CMUYautjaBracerShield", map.GridCoords);
            var frontAttacker = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, -1)));
            var backAttacker = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.System<SharedTransformSystem>().SetLocalRotation(hunter, Angle.Zero);
                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var shieldDamage = entMan.GetComponent<DamageableComponent>(shield);
                var incoming = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker);
                var unblockedBlunt = entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt");
                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.RaiseShield((shield, blockingComponent), hunter), Is.True);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                sourceBlock.ReadiedBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.LessThan(unblockedBlunt),
                        "A readied CMSS13 directional shield should block attacks from the facing cone.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((shield, shieldDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.GreaterThan(FixedPoint2.Zero),
                        "The shield should absorb part of a front attack after a successful readied source roll.");
                });

                damageable.SetAllDamage((hunter, userDamage), FixedPoint2.Zero);
                damageable.SetAllDamage((shield, shieldDamage), FixedPoint2.Zero);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: backAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((hunter, userDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(unblockedBlunt),
                        "A CMSS13 directional shield should not block attacks from behind even while readied.");
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((shield, shieldDamage)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.Zero),
                        "Back attacks should not transfer damage into the shield.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, shield, frontAttacker, backAttacker })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaShieldBashMatchesCmss13CooldownThrowAndDebuffs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<Content.Shared.StatusEffectNew.StatusEffectsSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var shield = entMan.SpawnEntity("CMUYautjaBracerShield", map.GridCoords);
            var firstTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var cooldownTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                RaiseShieldHit(entMan, shield, hunter, firstTarget);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(firstTarget), Is.True);
                    Assert.That(status.TryGetTime(firstTarget, "Dazed", out var dazed), Is.True);
                    Assert.That(dazed.EndEffectTime - timing.CurTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                    Assert.That(entMan.TryGetComponent(firstTarget, out RMCSlowdownComponent? slow), Is.True);
                    Assert.That(slow!.ExpiresAt - timing.CurTime, Is.EqualTo(TimeSpan.FromSeconds(5)).Within(TimeSpan.FromMilliseconds(50)));
                });

                RaiseShieldHit(entMan, shield, hunter, cooldownTarget);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(cooldownTarget), Is.False);
                    Assert.That(status.HasStatusEffect(cooldownTarget, "Dazed"), Is.False);
                    Assert.That(entMan.HasComponent<RMCSlowdownComponent>(cooldownTarget), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, shield, firstTarget, cooldownTarget })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackSpareEquipmentUsesCmss13VendorPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var brokeHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var fundedHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendorSystem = entMan.System<SharedCMAutomatedVendorSystem>();
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Spare Equipment");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var spare = vendor.Sections[sectionIndex];
                var falconIndex = spare.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFalconDrone");
                Assert.That(falconIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(spare.Choices, Is.Null);
                Assert.That(spare.Entries[falconIndex].Points, Is.EqualTo((int?) 20));

                var brokeUser = entMan.EnsureComponent<CMVendorUserComponent>(brokeHunter);
                vendorSystem.SetPoints((brokeHunter, brokeUser), 19);
                var fundedUser = entMan.EnsureComponent<CMVendorUserComponent>(fundedHunter);
                vendorSystem.SetPoints((fundedHunter, fundedUser), 20);

                var falconsBefore = EntityPrototypeIds(entMan, "CMUYautjaFalconDrone").Count();

                Vend(entMan, rack, brokeHunter, sectionIndex, falconIndex);
                Assert.That(brokeUser.Points, Is.EqualTo(19),
                    "CMSS13 handle_points() rejects a spare-equipment vend without deducting vendor_points when points are below item cost.");
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaFalconDrone").Count(), Is.EqualTo(falconsBefore));

                Vend(entMan, rack, fundedHunter, sectionIndex, falconIndex);
                Assert.That(fundedUser.Points, Is.Zero,
                    "CMSS13 handle_points() deducts the spare-equipment row cost from vendor_points on success.");
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaFalconDrone").Count(), Is.EqualTo(falconsBefore + 1));
            }
            finally
            {
                foreach (var uid in new[] { rack, brokeHunter, fundedHunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackEssentialsAndArmorConsumeSeparateCmss13Claims()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[sectionIndex];
                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                var armorIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaArmorBundle");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                AssertChoice(essentials.Entries[huntingIndex], "CMUYautjaEssentials", 1);
                AssertChoice(essentials.Entries[armorIndex], "CMUYautjaArmor", 1);

                entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, huntingIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                });

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(0));

                entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, armorIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                });

                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static async Task FireLoadedBowArrow(
        TestPair pair,
        string arrowPrototype,
        string expectedProjectilePrototype,
        string source,
        Action<IEntityManager, EntityUid, EntityUid, EntityUid> assertFiredProjectile,
        YautjaArrowWarhead? selectedWarhead = null)
    {
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? projectile = null;
        MapId mapId = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var wield = entMan.System<Content.Shared.Wieldable.SharedWieldableSystem>();

                mapSystem.CreateMap(out mapId);
                var coordinates = new MapCoordinates(Vector2.Zero, mapId);
                hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
                target = entMan.SpawnEntity("CMMobHuman", coordinates.Offset(Vector2.UnitX));
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", coordinates);
                arrow = entMan.SpawnEntity(arrowPrototype, coordinates);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True, source);

                if (selectedWarhead is { } warhead)
                {
                    entMan.EventBus.RaiseLocalEvent(
                        arrow,
                        new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), warhead));
                    Assert.That(entMan.GetComponent<YautjaArrowComponent>(arrow).SelectedWarhead, Is.EqualTo(warhead), source);
                }

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True, source);

                var wieldable = entMan.GetComponent<WieldableComponent>(bow);
                Assert.That(wield.TryWield((bow, wieldable), hunter), Is.True, source);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var gunSystem = entMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var gun = entMan.GetComponent<GunComponent>(bow);
                var targetCoords = entMan.GetComponent<TransformComponent>(target).Coordinates;

                Assert.That(gun.NextFire, Is.LessThanOrEqualTo(server.Timing.CurTime),
                    "The test waits out the normal equip/selection fire delay before asserting the live bow firing path.");
                var projectiles = gunSystem.AttemptShoot((bow, gun), hunter, targetCoords);

                Assert.That(projectiles, Is.Not.Null, source);
                projectile = projectiles!.Single();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(projectile.Value).EntityPrototype?.ID,
                        Is.EqualTo(expectedProjectilePrototype), source);
                    Assert.That(slots.GetItemOrNull(bow, "projectiles"), Is.Null,
                        "Firing the hunting bow should consume the selected arrow from the single internal arrow slot.");
                });

                assertFiredProjectile(entMan, projectile.Value, hunter, target);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();

                foreach (var uid in new[] { projectile, bow, arrow, target, hunter })
                {
                    if (uid is { } entity && !entMan.Deleted(entity))
                        entMan.DeleteEntity(entity);
                }

                if (mapId != default)
                    mapSystem.DeleteMap(mapId);
            });
        }
    }

    private static void RaiseProjectileHit(IEntityManager entMan, EntityUid projectile, EntityUid target, EntityUid shooter)
    {
        var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
        var damage = new DamageSpecifier(projectileComp.Damage);
        var hit = new ProjectileHitEvent(damage, target, shooter);
        entMan.EventBus.RaiseLocalEvent(projectile, ref hit);
    }

    private static void AssertFireStacks(IEntityManager entMan, EntityUid target, float fireStacks, string source)
    {
        var flammable = entMan.GetComponent<FlammableComponent>(target);

        Assert.Multiple(() =>
        {
            Assert.That(flammable.FireStacks, Is.EqualTo(MathF.Min(fireStacks, flammable.MaximumFireStacks)).Within(0.001f), source);
            Assert.That(flammable.OnFire, Is.True, source);
        });
    }

    private static void BindRecallable(IEntityManager entMan, EntityUid item, EntityUid hunter)
    {
        // CMU14: the gear-binding system was removed, so a Yautja claims the disc by naming it owner.
        entMan.GetComponent<YautjaSmartDiscComponent>(item).YautjaOwner = hunter;
    }

    private static string[] ActionPrototypeIds(IEntityManager entMan, IEnumerable<EntityUid> actions)
    {
        var ids = new List<string>();
        foreach (var action in actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID is { } id)
                ids.Add(id);
        }

        return ids.ToArray();
    }

    private static void RaiseUsePlasmaCannons(
        IEntityManager entMan,
        EntityUid pack,
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        var ev = new YautjaUsePlasmaCannonsActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
        entMan.EventBus.RaiseLocalEvent(pack, ev);
        Assert.That(ev.Handled, Is.True);
    }

    private static void PrepareCannonPackRoleGuardUser(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid hunter,
        EntityUid pack)
    {
        entMan.EnsureComponent<YautjaComponent>(hunter);
        Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);
    }

    private static IEnumerable<EntityUid> EntityPrototypeIds(IEntityManager entMan, string prototype)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                yield return uid;
        }
    }



















    public enum Cmss13ButcherOutputKind
    {
        StackSheet,
        SkeletonLimb,
        EffectDecal,
    }





    public readonly record struct MedicompPayloadHealingRow(float BloodlossModifier);

    public readonly record struct MedicompPayloadHyposprayRow(
        int TransferAmount,
        int MaxVolume,
        IReadOnlyDictionary<string, int> ExpectedReagents);

    public readonly record struct MedicompPayloadRow(
        string Id,
        string SourcePath,
        string Name,
        string Description,
        string Size,
        IReadOnlyCollection<string> ExpectedTags,
        bool YautjaMedicalItem = false,
        string? StackType = null,
        int StackCount = 0,
        int StackMaxCount = 0,
        MedicompPayloadHealingRow? Healing = null,
        MedicompPayloadHyposprayRow? Hypospray = null);















    public readonly record struct Cmss13ArmorStats(
        int Melee,
        int Bullet,
        int Bio,
        int ExplosionArmor,
        string DamagedExamineText);

    public readonly record struct Cmss13ProtectionStats(
        int Melee,
        int Bullet,
        int Bio,
        int ExplosionArmor);



    // cmss13 #12577 thrall rack: full, half (old) and light pieces in every material, all with the full set's stats.
    // the full mask rides with the full row, the half-mask with the half row, so each mask is spawned once





















    private static void AssertChoice(CMVendorEntry entry, string id, int amount)
    {
        Assert.That(entry.Choices, Is.Not.Null, $"{entry.Id.Id} should use source claim category {id}.");
        Assert.That(entry.Choices!.Value.Id, Is.EqualTo(id), $"{entry.Id.Id} source claim category");
        Assert.That(entry.Choices.Value.Amount, Is.EqualTo(amount), $"{entry.Id.Id} source claim amount");
    }

    private static void Vend(IEntityManager entMan, EntityUid rack, EntityUid user, int sectionIndex, int entryIndex)
    {
        entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, entryIndex, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    private static void RaiseDialogOption(IEntityManager entMan, EntityUid entity, EntityUid user, string optionText)
    {
        var dialog = entMan.GetComponent<DialogComponent>(entity);
        var optionIndex = dialog.Options
            .Select((option, index) => (option, index))
            .Single(pair => pair.option.Text == optionText)
            .index;

        entMan.EventBus.RaiseLocalEvent(entity, new DialogOptionBuiMsg(optionIndex)
        {
            Actor = user,
            UiKey = DialogUiKey.Key,
        });
    }

    private static FixedPoint2 DamageTotal(DamageSpecifier damage)
    {
        var total = FixedPoint2.Zero;
        foreach (var value in damage.DamageDict.Values)
            total += value;

        return total;
    }

    private static void RaiseShieldHit(IEntityManager entMan, EntityUid shield, EntityUid user, EntityUid target)
    {
        var hit = new MeleeHitEvent(new List<EntityUid> { target }, user, shield, new DamageSpecifier(), null);
        entMan.EventBus.RaiseLocalEvent(shield, hit);
    }
}
