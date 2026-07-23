using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server._CMU14.Yautja;
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
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Slow;
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
using Content.Shared.Explosion.Components.OnTrigger;
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

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaBowTest
{
    [Test]
    public async Task YautjaBowArrowPrototypeSuiteExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            foreach (var id in Cmss13BowArrowPrototypeIds())
                Assert.That(prototypes.HasIndex<EntityPrototype>(id), Is.True, $"Missing {id}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArrowProjectileStatsMatchCmss13AmmoDatums()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaArrowProjectile", MapCoordinates.Nullspace);
            var snare = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", MapCoordinates.Nullspace);

            try
            {
                AssertArrowProjectileStats(entMan, arrow, 110, 20, 14);
                AssertArrowProjectileStats(entMan, snare, 30, 15, 7);
            }
            finally
            {
                if (!entMan.Deleted(arrow))
                    entMan.DeleteEntity(arrow);
                if (!entMan.Deleted(snare))
                    entMan.DeleteEntity(snare);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActiveWarheadProjectileStatsMatchCmss13AmmoDatums()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var explosive = entMan.SpawnEntity("CMUYautjaExplosiveArrowProjectile", MapCoordinates.Nullspace);
            var emp = entMan.SpawnEntity("CMUYautjaEmpArrowProjectile", MapCoordinates.Nullspace);

            try
            {
                AssertArrowProjectileStats(entMan, explosive, "Heat", 110, 20, 14);
                AssertArrowProjectileStats(entMan, emp, "Heat", 110, 20, 14);
            }
            finally
            {
                if (!entMan.Deleted(explosive))
                    entMan.DeleteEntity(explosive);
                if (!entMan.Deleted(emp))
                    entMan.DeleteEntity(emp);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRangedProjectileStatsMatchCmss13AmmoDatums()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spike = entMan.SpawnEntity("CMUYautjaSpikeProjectile", MapCoordinates.Nullspace);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistolBolt", MapCoordinates.Nullspace);
            var incendiary = entMan.SpawnEntity("CMUYautjaPlasmaPistolIncendiaryBolt", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", MapCoordinates.Nullspace);
            var casterStun = entMan.SpawnEntity("CMUYautjaCasterStunBolt", MapCoordinates.Nullspace);
            var casterImmobilizer = entMan.SpawnEntity("CMUYautjaCasterImmobilizerBolt", MapCoordinates.Nullspace);
            var casterLethal = entMan.SpawnEntity("CMUYautjaCasterLethalBolt", MapCoordinates.Nullspace);
            var casterEradicator = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", MapCoordinates.Nullspace);

            try
            {
                AssertProjectileStats(entMan,
                    spike,
                    "Piercing",
                    30,
                    50,
                    12,
                    "CMSS13 /datum/ammo/alloy_spike: damage = 30, penetration = ARMOR_PENETRATION_TIER_10, max_range = 12.");
                AssertProjectileStats(entMan,
                    pistol,
                    "Heat",
                    40,
                    null,
                    22,
                    "CMSS13 /datum/ammo/energy/yautja/pistol: BURN damage = 40, inherited base ammo max_range = 22.");
                AssertProjectileStats(entMan,
                    incendiary,
                    "Heat",
                    10,
                    null,
                    22,
                    "CMSS13 /datum/ammo/energy/yautja/pistol/incendiary: BURN damage = 10, inherited base ammo max_range = 22.");
                AssertProjectileStats(entMan,
                    rifle,
                    "Heat",
                    55,
                    50,
                    22,
                    "CMSS13 /datum/ammo/energy/yautja/rifle/bolt: BURN damage = 55, penetration = ARMOR_PENETRATION_TIER_10, inherited base ammo max_range = 22.");
                AssertProjectileStats(entMan,
                    casterStun,
                    "Heat",
                    0,
                    null,
                    22,
                    "CMSS13 /datum/ammo/energy/yautja/caster/bolt/single_stun: damage = 0, inherited base ammo max_range = 22.");
                AssertProjectileStats(entMan,
                    casterImmobilizer,
                    "Heat",
                    0,
                    null,
                    20,
                    "CMSS13 /datum/ammo/energy/yautja/caster/sphere/aoe_stun: damage = 0, max_range = 20.");
                AssertProjectileStats(entMan,
                    casterLethal,
                    "Heat",
                    75,
                    null,
                    22,
                    "CMSS13 /datum/ammo/energy/yautja/caster/bolt/single_lethal: damage = 75, inherited base ammo max_range = 22.");
                AssertProjectileStats(entMan,
                    casterEradicator,
                    "Heat",
                    55,
                    null,
                    8,
                    "CMSS13 /datum/ammo/energy/yautja/caster/aoe_lethal: damage = 55, max_range = 8.");
            }
            finally
            {
                foreach (var uid in new[]
                         {
                             spike, pistol, incendiary, rifle, casterStun, casterImmobilizer, casterLethal, casterEradicator
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
                    Assert.That(turfDamage?.DamageDict["Piercing"],
                        Is.EqualTo((FixedPoint2) 55),
                        "CMSS13 alloy spikes add bullet_trait_damage_boost +25 against GLOB.damage_boost_turfs.");
                    Assert.That(breachingDamage?.DamageDict["Piercing"],
                        Is.EqualTo((FixedPoint2) 55),
                        "CMSS13 alloy spikes add bullet_trait_damage_boost +25 against GLOB.damage_boost_breaching.");
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
    public async Task YautjaPlasmaProjectileEffectsMatchCmss13AmmoDatums()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistolBolt", MapCoordinates.Nullspace);
            var incendiary = entMan.SpawnEntity("CMUYautjaPlasmaPistolIncendiaryBolt", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifleBolt", MapCoordinates.Nullspace);
            var casterLethal = entMan.SpawnEntity("CMUYautjaCasterLethalBolt", MapCoordinates.Nullspace);
            var casterEradicator = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", MapCoordinates.Nullspace);

            try
            {
                AssertNoExplosionPayload(entMan,
                    pistol,
                    "CMSS13 /datum/ammo/energy/yautja/pistol defines damage/speed only and has no cell_explosion or explosive ammo behavior.");
                AssertNoExplosionPayload(entMan,
                    incendiary,
                    "CMSS13 /datum/ammo/energy/yautja/pistol/incendiary adds bullet_trait_incendiary only and does not inherit a plasma-pistol explosion.");
                AssertNoExplosionPayload(entMan,
                    rifle,
                    "CMSS13 /datum/ammo/energy/yautja/rifle/bolt adds incendiary/xeno side effects but no cell_explosion payload.");

                Assert.That(entMan.HasComponent<IgniteOnCollideComponent>(pistol), Is.False,
                    "CMSS13 standard plasma pistol bolts do not have bullet_trait_incendiary.");
                AssertIncendiaryPayload(entMan,
                    incendiary,
                    "CMSS13 incendiary pistol bolts add /datum/element/bullet_trait_incendiary.");
                AssertIncendiaryPayload(entMan,
                    rifle,
                    "CMSS13 rifle bolts add /datum/element/bullet_trait_incendiary.");

                AssertExplosionPayload(entMan,
                    casterLethal,
                    50,
                    50,
                    "CMSS13 /datum/ammo/energy/yautja/caster/bolt/single_lethal calls cell_explosion(..., 50, 50, EXPLOSION_FALLOFF_SHAPE_LINEAR).");
                AssertExplosionPayload(entMan,
                    casterEradicator,
                    170,
                    50,
                    "CMSS13 /datum/ammo/energy/yautja/caster/aoe_lethal calls cell_explosion(..., 170, 50, EXPLOSION_FALLOFF_SHAPE_LINEAR).");
            }
            finally
            {
                foreach (var uid in new[] { pistol, incendiary, rifle, casterLethal, casterEradicator })
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
                Assert.That(damageable.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.Zero));
                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(damageable.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(41.25)),
                    "CMSS13 rifle bolts add damage * 0.75 extra BURN damage to xenos in on_hit_mob().");
                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out var time), Is.True,
                    "CMSS13 rifle bolts call add_interference(30, 30) on xenos.");
                Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(TimeSpan.FromSeconds(30)));
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

    [Test]
    public async Task YautjaIncendiaryPlasmaBoltsApplyCmss13FireStacksOnProjectileHit()
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
                RaiseProjectileHit(entMan, pistolHumanProjectile, humanPistolTarget, hunter);
                RaiseProjectileHit(entMan, pistolXenoProjectile, xenoPistolTarget, hunter);
                RaiseProjectileHit(entMan, rifleHumanProjectile, humanRifleTarget, hunter);
                RaiseProjectileHit(entMan, rifleXenoProjectile, xenoRifleTarget, hunter);

                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                var coordinates = entMan.GetComponent<TransformComponent>(carbine).Coordinates;
                batterySystem.SetCharge(carbine, 1, battery);

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
    public async Task ExplosiveArrowPayloadMatchesCmss13CellExplosion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaExplosiveArrowProjectile", MapCoordinates.Nullspace);

            try
            {
                var explosive = entMan.GetComponent<ExplosiveComponent>(projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(explosive.ExplosionType.Id, Is.EqualTo("RMC"),
                        "CMSS13 cell_explosion uses CM-style burn/blast fallout, not the generic station explosion profile.");
                    Assert.That(explosive.TotalIntensity, Is.EqualTo(150));
                    Assert.That(explosive.MaxIntensity, Is.EqualTo(50));
                    Assert.That(explosive.MaxTileBreak, Is.EqualTo(1));
                });
            }
            finally
            {
                if (!entMan.Deleted(projectile))
                    entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpArrowPayloadMatchesCmss13Empulse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaEmpArrowProjectile", MapCoordinates.Nullspace);

            try
            {
                var emp = entMan.GetComponent<EmpOnTriggerComponent>(projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(emp.Range, Is.EqualTo(4));
                    Assert.That(emp.EnergyConsumption, Is.EqualTo(50000));
                    Assert.That(emp.DisableDuration, Is.EqualTo(10));
                });
            }
            finally
            {
                if (!entMan.Deleted(projectile))
                    entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FiredEmpArrowTriggersCmss13EmpPulseFromBowPath()
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
                arrow = entMan.SpawnEntity("CMUYautjaEmpArrowActive", coordinates);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);

                var wieldable = entMan.GetComponent<WieldableComponent>(bow);
                Assert.That(wield.TryWield(bow, wieldable, hunter), Is.True);

                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var trigger = entMan.System<Content.Server.Explosion.EntitySystems.TriggerSystem>();
                var gunSystem = entMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                var gun = entMan.GetComponent<GunComponent>(bow);
                var target = entMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(Vector2.UnitX);
                Assert.That(gun.NextFire, Is.LessThanOrEqualTo(server.Timing.CurTime),
                    "The test waits out the normal equip/selection fire delay before asserting the live bow firing path.");
                var projectiles = gunSystem.AttemptShoot((bow, gun), hunter, target);

                Assert.That(projectiles, Is.Not.Null,
                    "A loaded, wielded hunting bow should create a projectile through the real gun firing path.");
                projectile = projectiles!.Single();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(projectile.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaEmpArrowProjectile"),
                        "CMSS13 active EMP arrows should keep their EMP payload when fired from the loaded hunting bow.");
                    Assert.That(slots.GetItemOrNull(bow, "projectiles"), Is.Null,
                        "Firing the hunting bow should consume the single internal arrow slot like CMSS13 current_mag.use_round().");
                });

                bracerComp.Charge = 1500;
                trigger.Trigger(projectile.Value, hunter);

                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 500),
                    "CMSS13 EMP arrows call empulse(), which should reach a worn hunter bracer and drain 1000 charge.");
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
                Assert.That(wield.TryWield(bow, wieldable, hunter), Is.True);

                entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var trigger = entMan.System<Content.Server.Explosion.EntitySystems.TriggerSystem>();
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
    public async Task FiredExplosiveArrowUsesCmss13PayloadFromBowPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await FireLoadedBowArrow(
            pair,
            "CMUYautjaExplosiveArrowActive",
            "CMUYautjaExplosiveArrowProjectile",
            "CMSS13 active explosive arrows should keep their cell_explosion payload through the real loaded-bow firing path.",
            (entMan, projectile, _, _) =>
            {
                AssertExplosionPayload(
                    entMan,
                    projectile,
                    total: 150,
                    max: 50,
                    source: "CMSS13 explosive arrows use cell_explosion(get_turf(src), 150, 50, EXPLOSION_FALLOFF_SHAPE_LINEAR, null, CREATE_FLAMES, max_tile_break = 1).",
                    maxTileBreak: 1);
            });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FiredDynamicExplosiveArrowUsesCmss13PayloadFromBowPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await FireLoadedBowArrow(
            pair,
            "CMUYautjaDynamicArrow",
            "CMUYautjaExplosiveArrowProjectile",
            "CMSS13 dynamic_warhead/change_warhead() should preserve the selected explosive ammo datum through bow load/fire.",
            (entMan, projectile, _, _) =>
            {
                AssertExplosionPayload(
                    entMan,
                    projectile,
                    total: 150,
                    max: 50,
                    source: "CMSS13 dynamic explosive arrows reuse the explosive arrow ammo datum payload after selection.",
                    maxTileBreak: 1);
            },
            YautjaArrowWarhead.Explosive);

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
    public async Task HuntingBowReloadAndUnloadSoundsMatchCmss13ReloadSound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var slot = entMan.GetComponent<ItemSlotsComponent>(bow).Slots["projectiles"];
                const string cmss13ReloadSound = "/Audio/_RMC14/Weapons/Guns/Reload/m42a2.ogg";

                AssertSoundPath(slot.InsertSound!, cmss13ReloadSound);
                AssertSoundPath(slot.EjectSound!, cmss13ReloadSound);
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowFireSoundMatchesCmss13BowShot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var gun = entMan.GetComponent<GunComponent>(bow);

                AssertSoundPath(gun.SoundGunshot!, "/Audio/_CMU14/Yautja/bow_shot.ogg");
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowEmptyClickIsSilentLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var gun = entMan.GetComponent<GunComponent>(bow);

                Assert.That(gun.SoundEmpty, Is.Null,
                    "CMSS13 /obj/item/weapon/gun/bow/click_empty() only updates icon/item state and returns.");
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowStaticGunConfigAndArrowWarheadsMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);
            var standardArrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var explosiveArrow = entMan.SpawnEntity("CMUYautjaExplosiveArrowActive", MapCoordinates.Nullspace);
            var empArrow = entMan.SpawnEntity("CMUYautjaEmpArrow", MapCoordinates.Nullspace);
            var activeEmpArrow = entMan.SpawnEntity("CMUYautjaEmpArrowActive", MapCoordinates.Nullspace);
            var dynamicArrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
            var snareArrow = entMan.SpawnEntity("CMUYautjaSnareArrow", MapCoordinates.Nullspace);

            try
            {
                var bowMeta = entMan.GetComponent<MetaDataComponent>(bow);
                var bowItem = entMan.GetComponent<ItemComponent>(bow);
                var bowClothing = entMan.GetComponent<ClothingComponent>(bow);
                var bowGun = entMan.GetComponent<GunComponent>(bow);
                var bowResistance = entMan.GetComponent<ExplosionResistanceComponent>(bow);

                Assert.Multiple(() =>
                {
                    Assert.That(bowMeta.EntityName, Is.EqualTo("hunting bow"));
                    Assert.That(bowMeta.EntityDescription,
                        Is.EqualTo("An abnormal-sized weapon with an exceptionally tight string. Requires extraordinary strength to draw."));
                    Assert.That(bowItem.Size.Id, Is.EqualTo("Large"),
                        "CMSS13 /obj/item/weapon/gun/bow sets w_class = SIZE_LARGE.");
                    Assert.That(bowClothing.Slots, Is.EqualTo(SlotFlags.BACK),
                        "CMSS13 /obj/item/weapon/gun/bow sets flags_equip_slot = SLOT_BACK.");
                    Assert.That(bowGun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto),
                        "CMSS13 bow uses an internal one-shot magazine without burst/full-auto modes.");
                    Assert.That(bowGun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto),
                        "CMSS13 bow exposes only normal single-shot firing.");
                    Assert.That(bowGun.FireRate, Is.EqualTo(10f / 7f).Within(0.0001f),
                        "CMSS13 bow set_gun_config_values() sets fire_delay = FIRE_DELAY_TIER_7.");
                    Assert.That(bowGun.MinAngle.Degrees, Is.EqualTo(0).Within(0.0001),
                        "CMSS13 bow scatter = 0.");
                    Assert.That(bowGun.MaxAngle.Degrees, Is.EqualTo(0).Within(0.0001),
                        "CMSS13 bow scatter = 0.");
                    Assert.That(entMan.HasComponent<GunPointBlankComponent>(bow), Is.True,
                        "CMSS13 bow has GUN_CAN_POINTBLANK.");
                    Assert.That(entMan.HasComponent<WieldableComponent>(bow), Is.True);
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(bow), Is.True,
                        "CMSS13 bow has GUN_WIELDED_FIRING_ONLY and flags_item = TWOHANDED|ITEM_PREDATOR.");
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(bow), Is.True,
                        "CMSS13 bow sets flags_item = ITEM_PREDATOR.");
                    AssertNonCorrodible(entMan, bow);
                    Assert.That(bowResistance.DamageCoefficient, Is.EqualTo(0),
                        "CMSS13 bow sets explo_proof = TRUE.");
                });

                AssertCmss13ArrowInitialState(
                    entMan,
                    standardArrow,
                    "CMUYautjaArrowProjectile",
                    YautjaArrowWarhead.Standard,
                    YautjaArrowWarhead.Explosive,
                    YautjaArrowWarhead.Standard,
                    false,
                    false,
                    "CMSS13 base /obj/item/arrow starts inert with primary /datum/ammo/arrow and secondary /datum/ammo/arrow/expl.");
                AssertCmss13ArrowInitialState(
                    entMan,
                    explosiveArrow,
                    "CMUYautjaExplosiveArrowProjectile",
                    YautjaArrowWarhead.Standard,
                    YautjaArrowWarhead.Explosive,
                    YautjaArrowWarhead.Explosive,
                    true,
                    false,
                    "CMSS13 /obj/item/arrow/expl_active starts activated with /datum/ammo/arrow/expl.");
                AssertCmss13ArrowInitialState(
                    entMan,
                    empArrow,
                    "CMUYautjaArrowProjectile",
                    YautjaArrowWarhead.Standard,
                    YautjaArrowWarhead.Emp,
                    YautjaArrowWarhead.Standard,
                    false,
                    false,
                    "CMSS13 /obj/item/arrow/emp is inert until toggled and switches secondary_ammo to /datum/ammo/arrow/emp.");
                AssertCmss13ArrowInitialState(
                    entMan,
                    activeEmpArrow,
                    "CMUYautjaEmpArrowProjectile",
                    YautjaArrowWarhead.Standard,
                    YautjaArrowWarhead.Emp,
                    YautjaArrowWarhead.Emp,
                    true,
                    false,
                    "CMSS13 /obj/item/arrow/emp/active starts activated with /datum/ammo/arrow/emp.");
                AssertCmss13ArrowInitialState(
                    entMan,
                    dynamicArrow,
                    "CMUYautjaArrowProjectile",
                    YautjaArrowWarhead.Standard,
                    null,
                    YautjaArrowWarhead.Standard,
                    false,
                    true,
                    "CMSS13 /obj/item/arrow/dynamic_warhead starts inert and only chooses explosive/EMP after tgui selection.");
                AssertCmss13ArrowInitialState(
                    entMan,
                    snareArrow,
                    "CMUYautjaSnareArrowProjectile",
                    YautjaArrowWarhead.Snare,
                    null,
                    YautjaArrowWarhead.Snare,
                    true,
                    false,
                    "CMSS13 /obj/item/arrow/snare starts activated as a snare arrow.");
            }
            finally
            {
                foreach (var uid in new[]
                         {
                             bow,
                             standardArrow,
                             explosiveArrow,
                             empArrow,
                             activeEmpArrow,
                             dynamicArrow,
                             snareArrow,
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
    public async Task YautjaArrowMeleeProfileMatchesCmss13BaseArrow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<SharpComponent>(arrow), Is.True);
                    Assert.That(entMan.TryGetComponent<MeleeWeaponComponent>(arrow, out var melee), Is.True);
                    Assert.That(melee!.Damage.DamageDict["Piercing"], Is.EqualTo((FixedPoint2) 20));
                });
            }
            finally
            {
                if (!entMan.Deleted(arrow))
                    entMan.DeleteEntity(arrow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileTriggersSourceSnareOnHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            try
            {
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                var snares = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Where(uid => entMan.HasComponent<YautjaTrapComponent>(uid))
                    .ToList();

                Assert.That(snares, Has.Count.EqualTo(1));
                var snare = snares[0];
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.Armed, Is.False);
                    Assert.That(trap.TrapOwner, Is.EqualTo(hunter));
                    Assert.That(trap.TrappedMob, Is.EqualTo(target));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(transform.GetMapCoordinates(snare).Position,
                        Is.EqualTo(transform.GetMapCoordinates(target).Position));
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
    public async Task SnareArrowProjectileCanSnareShooterLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            EntityUid snare = default;

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), hunter, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.Multiple(() =>
                {
                    Assert.That(trap.Armed, Is.False);
                    Assert.That(trap.TrapOwner, Is.EqualTo(hunter));
                    Assert.That(trap.TrappedMob, Is.EqualTo(hunter),
                        "CMSS13 /obj/item/arrow/snare/trigger_snare(target) has no owner-immunity check for direct-hit snare arrows.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(hunter), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, snare })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileDoesNotExposeHuntingTrapConfigureVerbLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<VerbSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            EntityUid snare = default;

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));

                var localVerbs = verbs.GetLocalVerbs(snare, hunter, typeof(InteractionVerb), force: true);
                Assert.That(localVerbs.Select(verb => verb.Text), Does.Not.Contain("Configure Hunting Trap"),
                    "CMSS13 /obj/item/arrow/snare/attack_hand() has snare self-resist/Yautja disarm paths, but no hunting-trap configure verb.");
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
    public async Task SnareArrowProjectileAnchorsActiveTrapLikeCmss13()
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

            try
            {
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                var snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));

                Assert.That(entMan.GetComponent<TransformComponent>(snare).Anchored, Is.True);
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
    public async Task SnareArrowProjectileLogsCatchAndFreedLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                entMan.GetComponent<YautjaTrapComponent>(snare).BreakFreeDelay = TimeSpan.FromSeconds(0.25);

                var interact = new InteractHandEvent(target, snare);
                entMan.EventBus.RaiseLocalEvent(snare, interact);
                Assert.That(interact.Handled, Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);
                Assert.Multiple(() =>
                {
                    Assert.That(trap.TrappedMob, Is.Null);
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                });
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);

            Assert.Multiple(() =>
            {
                Assert.That(
                    messages,
                    Has.Some.Contains("was caught in a snare arrow").IgnoreCase,
                    $"CMSS13 /obj/item/arrow/snare/trigger_snare() logs '[target] was caught in \\a [src]' where [src] is snare arrow.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("was freed from a snare arrow").IgnoreCase,
                    $"CMSS13 /obj/item/arrow/snare/disarm() adds only the trapped mob attack_log '[target] was freed from \\a [src]' for snare release.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.None.Contains("snare arrow").And.Contains("Yautja hunting trap"),
                    $"Snare-arrow logs should not use the regular hunting-trap log subject.\nActual logs:\n{joinedMessages}");
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
    public async Task SnareArrowProjectilePopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                server.PlayerMan.SetAttachedEntity(session, target);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You get caught in snare arrow!"));
                    Assert.That(labels, Does.Not.Contain("The hunting trap snaps shut!"));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task SnareArrowProjectileDoesNotBroadcastHuntingTrapCatchLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                expectedBroadcast = $"A hunting trap has caught something in {areas.GetAreaName(snare)}!";
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.That(labels, Does.Not.Contain(expectedBroadcast),
                    "CMSS13 /obj/item/arrow/snare/trigger_snare() has no message_all_yautja() broadcast; only /obj/item/hunting_trap/trapMob() broadcasts trap catches.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task SnareArrowProjectileDisarmPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                Assert.That(trapSystem.TryDisarmTrap((snare, trap), hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("snare arrow is now disarmed."));
                    Assert.That(labels, Does.Not.Contain("You disarm the hunting trap."));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task SnareArrowProjectileYautjaInteractDoesNotShowHuntingTrapRecoveryPopupLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid snare = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ev = new ProjectileHitEvent(new DamageSpecifier(), target, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));

                var interact = new InteractHandEvent(hunter, snare);
                entMan.EventBus.RaiseLocalEvent(snare, interact);
                Assert.That(interact.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("snare arrow is now disarmed."));
                    Assert.That(labels, Does.Not.Contain("You recover the hunting trap."),
                        "CMSS13 /obj/item/arrow/snare/attack_hand() routes Yautja users through snare disarm text only, not the placed hunting-trap recover popup.");
                    Assert.That(labels, Does.Not.Contain("Вы забираете охотничью ловушку."),
                        "The local placed hunting-trap recovery popup must not leak through the snare-arrow hand interaction path.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task SnareArrowProjectileBlocksXenoHealLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xenoSystem = entMan.System<XenoSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.That(xenoSystem.CanHeal(xeno), Is.True);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(xenoSystem.CanHeal(xeno), Is.False);
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

    [Test]
    public async Task SnareArrowProjectileAppliesXenoInterferenceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False);

                var ev = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out var time), Is.True);
                Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(TimeSpan.FromSeconds(100)));
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

    [Test]
    public async Task SnareArrowProjectileForcesXenoNeedhelpEmoteLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var listener = entMan.System<YautjaTestSpeechListenerSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(xeno);
                listener.Emotes.Clear();

                var ev = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(listener.Emotes, Does.Contain((xeno, "XenoHelp")));
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

    [Test]
    public async Task SnareArrowProjectileForcesHumanPainEmoteLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var listener = entMan.System<YautjaTestSpeechListenerSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(human);
                listener.Emotes.Clear();

                var ev = new ProjectileHitEvent(new DamageSpecifier(), human, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.That(listener.Emotes, Does.Contain((human, "Scream")));
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, human })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileTriggerSoundMatchesCmss13TableHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var resources = server.ResolveDependency<IResourceManager>();
            var projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var ev = new ProjectileHitEvent(new DamageSpecifier(), human, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                var snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                var trap = entMan.GetComponent<YautjaTrapComponent>(snare);

                const string tableHitPath = "/Audio/_CMU14/Yautja/tablehit1.ogg";
                Assert.Multiple(() =>
                {
                    AssertSoundPath(trap.TriggerSound, tableHitPath);
                    Assert.That(resources.ContentFileExists(new ResPath(tableHitPath)), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, hunter, human })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowProjectileUsesActiveTrapVisualLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid projectile = default;
        EntityUid hunter = default;
        EntityUid human = default;
        EntityUid snare = default;
        NetEntity snareNet = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                projectile = entMan.SpawnEntity("CMUYautjaSnareArrowProjectile", map.GridCoords);
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ev = new ProjectileHitEvent(new DamageSpecifier(), human, hunter);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                snare = EntityPrototypeIds(entMan, "CMUYautjaSnareArrow")
                    .Single(uid => entMan.HasComponent<YautjaTrapComponent>(uid));
                snareNet = entMan.GetNetEntity(snare);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                Assert.That(entMan.TryGetEntity(snareNet, out var clientSnare), Is.True);

                var sprites = entMan.System<SpriteSystem>();
                var sprite = entMan.GetComponent<SpriteComponent>(clientSnare.Value);

                Assert.Multiple(() =>
                {
                    AssertVisibleLayer(sprites, clientSnare.Value, sprite, "snare-trap", "arrow_trap_active", true);
                    AssertVisibleLayer(sprites, clientSnare.Value, sprite, "tail", "tail", false);
                    AssertVisibleLayer(sprites, clientSnare.Value, sprite, "rod", "rod", false);
                    AssertVisibleLayer(sprites, clientSnare.Value, sprite, "tip", "tip", false);
                    AssertVisibleLayer(sprites, clientSnare.Value, sprite, "mark", "solution1", false);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { projectile, hunter, human, snare })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuiverStrapsFillWithCmss13BowAndArrows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var filled = entMan.SpawnEntity("CMUYautjaQuiverStrapFilled", MapCoordinates.Nullspace);
            var dynamic = entMan.SpawnEntity("CMUYautjaQuiverStrapDynamic", MapCoordinates.Nullspace);

            try
            {
                AssertQuiverContents(entMan, filled, "CMUYautjaArrow");
                AssertQuiverContents(entMan, dynamic, "CMUYautjaDynamicArrow");
            }
            finally
            {
                if (!entMan.Deleted(filled))
                    entMan.DeleteEntity(filled);
                if (!entMan.Deleted(dynamic))
                    entMan.DeleteEntity(dynamic);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingPouchStorageMatchesCmss13YautjaBackpackSourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storageSystem = entMan.System<SharedStorageSystem>();
            var pouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);
            var filler = new List<EntityUid>();

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(pouch);
                var clothing = entMan.GetComponent<ClothingComponent>(pouch);
                var item = entMan.GetComponent<ItemComponent>(pouch);
                var storage = entMan.GetComponent<StorageComponent>(pouch);
                var fixedSize = entMan.GetComponent<FixedItemSizeStorageComponent>(pouch);
                var limited = entMan.GetComponent<LimitedStorageComponent>(pouch);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("hunting pouch"));
                    Assert.That(meta.EntityDescription,
                        Is.EqualTo("A Yautja hunting pouch worn around the waist, made from a thick tanned hide. Capable of holding various devices and tools and used for the transport of trophies."));
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BELT), "CMSS13 flags_equip_slot = SLOT_WAIST");
                    Assert.That(item.Size.Id, Is.EqualTo("Large"), "Local item size preserves the belt-storage parent footprint while storage max size maps CMSS13 max_w_class.");
                    Assert.That(storage.MaxItemSize, Is.EqualTo("Normal"), "CMSS13 max_w_class = SIZE_MEDIUM");
                    Assert.That(fixedSize.Size, Is.EqualTo(new Vector2i(2, 2)), "Local pouch stores items in fixed 2x2 slots for source slot-count parity.");
                    Assert.That(limited.Limits, Has.Count.EqualTo(1), "CMSS13 storage_slots = 12 should be represented as a count limit.");
                    Assert.That(limited.Limits[0].Count, Is.EqualTo(12));
                    Assert.That(limited.Limits[0].Popup, Is.EqualTo("rmc-storage-limit-cant-fit"));
                });

                for (var i = 0; i < 12; i++)
                {
                    var gel = entMan.SpawnEntity("CMUYautjaCleanserGelVial", MapCoordinates.Nullspace);
                    filler.Add(gel);
                    Assert.That(storageSystem.Insert(pouch, gel, out _, storageComp: storage, playSound: false),
                        Is.True,
                        "CMSS13 hunting pouch has storage_slots = 12.");
                }

                var extra = entMan.SpawnEntity("CMUYautjaCleanserGelVial", MapCoordinates.Nullspace);
                filler.Add(extra);

                Assert.Multiple(() =>
                {
                    Assert.That(storage.StoredItems, Has.Count.EqualTo(12));
                    Assert.That(
                        storageSystem.CanInsert(pouch, extra, null, out var reason, storage),
                        Is.False,
                        "CMSS13 hunting pouch rejects a thirteenth medium-or-smaller item.");
                    Assert.That(reason, Is.EqualTo("comp-storage-insufficient-capacity"),
                        "The local 12 fixed-slot grid is full before LimitedStorage can provide its popup.");
                });
            }
            finally
            {
                foreach (var uid in filler.Append(pouch))
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaUtilityItemPrototypesMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in Cmss13CommunicatorRows())
                    {
                        var communicator = SpawnAndTrack(entMan, row.Id, spawned);
                        var meta = entMan.GetComponent<MetaDataComponent>(communicator);

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} CMSS13 source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} CMSS13 source description");
                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(communicator), Is.True,
                            $"{row.Id} maps CMSS13 flags_item = ITEM_PREDATOR.");
                        AssertNonCorrodible(entMan, communicator);
                    }

                    var relay = SpawnAndTrack(entMan, "CMUYautjaRelayBeacon", spawned);
                    AssertUtilityItem(
                        entMan,
                        relay,
                        "CMUYautjaRelayBeacon",
                        "relay beacon",
                        "A device covered in sacred text. It whirrs and beeps every couple of seconds.",
                        "Tiny",
                        sourceUnacidable: true);

                    var simpleRelay = SpawnAndTrack(entMan, "CMUYautjaSimpleRelayBeacon", spawned);
                    AssertUtilityItem(
                        entMan,
                        simpleRelay,
                        "CMUYautjaSimpleRelayBeacon",
                        "simple relay beacon",
                        "A device covered in sacred text. It whirrs and beeps every couple of seconds.",
                        "Tiny",
                        sourceUnacidable: true);

                    var cleanser = SpawnAndTrack(entMan, "CMUYautjaCleanserGelVial", spawned);
                    AssertUtilityItem(
                        entMan,
                        cleanser,
                        "CMUYautjaCleanserGelVial",
                        "cleanser gel vial",
                        "A small vial containing a liquid capable of dissolving the gear of the fallen whilst in the field.",
                        "Small",
                        sourceUnacidable: false,
                        blockPickup: false);

                    var lantern = SpawnAndTrack(entMan, "CMUYautjaLantern", spawned);
                    var lanternMeta = entMan.GetComponent<MetaDataComponent>(lantern);
                    var lanternItem = entMan.GetComponent<ItemComponent>(lantern);
                    var lanternClothing = entMan.GetComponent<ClothingComponent>(lantern);
                    var lanternLight = entMan.GetComponent<ServerPointLightComponent>(lantern);
                    var lanternBattery = entMan.GetComponent<BatteryComponent>(lantern);
                    var lanternRecharger = entMan.GetComponent<BatterySelfRechargerComponent>(lantern);
                    var lanternPrice = entMan.GetComponent<StaticPriceComponent>(lantern);
                    var lanternHandheld = entMan.GetComponent<HandheldLightComponent>(lantern);

                    Assert.That(lanternMeta.EntityName, Is.EqualTo("lantern"),
                        "CMSS13 youngblood/thrall rows use /obj/item/device/flashlight/lantern/yautja; the checked source tree lacks a subtype override, so the local equivalent should preserve inherited lantern name facts.");
                    Assert.That(lanternMeta.EntityDescription, Is.EqualTo("A mining lantern."),
                        "CMSS13 /obj/item/device/flashlight/lantern/yautja inherits the base mining-lantern description in this snapshot.");
                    Assert.That(lanternItem.Size.Id, Is.EqualTo("Small"),
                        "Local RMCFlashlightLantern is the closest available CMSS13 lantern equivalent; keep the inherited item size unless a source override is located.");
                    Assert.That(lanternClothing.Slots, Is.EqualTo(SlotFlags.BELT | SlotFlags.SUITSTORAGE),
                        "The local equivalent should stay wearable in the same flashlight/lantern carry slots.");
                    Assert.That(lanternLight.Enabled, Is.False);
                    Assert.That(lanternLight.Radius, Is.EqualTo(6f));
                    Assert.That(lanternLight.Color, Is.EqualTo(Color.FromHex("#FFC458FF")));
                    Assert.That(lanternBattery.MaxCharge, Is.EqualTo(600f));
                    Assert.That(lanternBattery.CurrentCharge, Is.EqualTo(600f));
                    Assert.That(lanternRecharger.AutoRecharge, Is.True);
                    Assert.That(lanternRecharger.AutoRechargeRate, Is.EqualTo(2f));
                    Assert.That(lanternPrice.Price, Is.EqualTo(40));
                    Assert.That(lanternHandheld.ToggleAction, Is.EqualTo("CMUActionYautjaToggleLantern"),
                        "CMSS13 /flashlight/lantern/yautja uses actions_yautja.dmi with pred_template framing instead of the generic flashlight HUD action.");
                    Assert.That(entMan.HasComponent<ItemTogglePointLightComponent>(lantern), Is.True);
                    Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(lantern, out var lanternTech), Is.True,
                        "Local CMUYautjaLantern represents the CMSS13 /lantern/yautja subtype used by Yautja rack rows and action HUD checks.");
                    Assert.That(lanternTech!.DamageMultiplier, Is.EqualTo(1f),
                        "The lantern is only marked for Yautja source mapping/action-HUD follow-up; it should not inherit weapon-style YautjaTechItem damage scaling.");
                    Assert.That(lanternTech!.BlockPickup, Is.False,
                        "The lantern is part of mandatory youngblood/thrall kit and should not punish non-Yautja pickup during ordinary flashlight handling.");
                    Assert.That(lanternTech.BlockUse, Is.False,
                        "CMSS13 /lantern/yautja remains a flashlight subtype; local Yautja tech marking should not block normal light toggling.");
                    Assert.That(lanternTech.BlockMelee, Is.False);
                    Assert.That(lanternTech.BlockThrow, Is.False);
                    Assert.That(lanternTech.BlockShoot, Is.False);

                    var hivebreaker = SpawnAndTrack(entMan, "CMUYautjaHivebreaker", spawned);
                    var hivebreakerMeta = entMan.GetComponent<MetaDataComponent>(hivebreaker);
                    var hivebreakerItem = entMan.GetComponent<ItemComponent>(hivebreaker);
                    var hivebreakerComp = entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker);
                    var hivebreakerPrice = entMan.GetComponent<StaticPriceComponent>(hivebreaker);

                    Assert.That(hivebreakerMeta.EntityName, Is.EqualTo("hivebreaker"),
                        "CMSS13 /obj/item/device/badblood_enthraller source name.");
                    Assert.That(hivebreakerMeta.EntityDescription,
                        Is.EqualTo("A device used by fallen Yautja to break a Xenomorph Hivemind and enthrall a serpent."),
                        "CMSS13 /obj/item/device/badblood_enthraller source description.");
                    Assert.That(hivebreakerItem.Size.Id, Is.EqualTo("Tiny"),
                        "CMSS13 /obj/item/device/badblood_enthraller w_class = SIZE_TINY.");
                    Assert.That(hivebreakerComp.Uses, Is.EqualTo(1),
                        "CMSS13 /obj/item/device/badblood_enthraller has var/uses = 1.");
                    Assert.That(hivebreakerComp.DoAfter, Is.EqualTo(TimeSpan.FromSeconds(3)),
                        "CMSS13 hivebreaker uses do_after(user, 3 SECONDS, ...).");
                    Assert.That(hivebreakerComp.RequireCritical, Is.True,
                        "CMSS13 hivebreaker requires thrall_target.stat == UNCONSCIOUS before enthralling.");
                    Assert.That(hivebreakerPrice.Price, Is.EqualTo(200),
                        "CMSS13 /obj/item/device/badblood_enthraller black_market_value = 200.");
                    AssertNonCorrodible(entMan, hivebreaker);
                    Assert.That(entMan.TryGetComponent<ExplosionResistanceComponent>(hivebreaker, out var hivebreakerExplosion), Is.True,
                        "CMSS13 /obj/item/device/badblood_enthraller explo_proof = TRUE.");
                    Assert.That(hivebreakerExplosion!.Worn, Is.False,
                        "CMSS13 explo_proof protects the item itself, not the wearer.");
                    var hivebreakerExplosionEvent = new GetExplosionResistanceEvent("RMC");
                    entMan.EventBus.RaiseLocalEvent(hivebreaker, ref hivebreakerExplosionEvent);
                    Assert.That(hivebreakerExplosionEvent.DamageCoefficient, Is.Zero,
                        "CMSS13 explo_proof = TRUE should make the local item take no explosion damage.");
                    Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(hivebreaker, out var hivebreakerTech), Is.True,
                        "CMSS13 /obj/item/device/badblood_enthraller flags_item = ITEM_PREDATOR.");
                    Assert.That(hivebreakerTech!.DamageMultiplier, Is.EqualTo(1f),
                        "The hivebreaker is a tiny device with source force = 1 and should not inherit weapon-style YautjaTechItem damage scaling.");
                    Assert.That(hivebreakerTech.BlockPickup, Is.True,
                        "CMSS13 ITEM_PREDATOR should keep hivebreaker pickup restricted to local Yautja-tech users.");
                    Assert.That(hivebreakerTech.BlockUse, Is.True,
                        "CMSS13 ITEM_PREDATOR should keep hivebreaker use restricted to local Yautja-tech users.");
                    Assert.That(hivebreakerTech.BlockMelee, Is.True);
                    Assert.That(hivebreakerTech.BlockThrow, Is.True);
                    Assert.That(hivebreakerTech.BlockShoot, Is.True);

                    foreach (var row in Cmss13MedicompRows())
                    {
                        var medicomp = SpawnAndTrack(entMan, row.Id, spawned);
                        var meta = entMan.GetComponent<MetaDataComponent>(medicomp);
                        var item = entMan.GetComponent<ItemComponent>(medicomp);
                        var clothing = entMan.GetComponent<ClothingComponent>(medicomp);
                        var storage = entMan.GetComponent<StorageComponent>(medicomp);
                        var limited = entMan.GetComponent<LimitedStorageComponent>(medicomp);

                        Assert.That(meta.EntityName, Is.EqualTo("medicomp"), $"{row.Id} CMSS13 source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo("A complex kit of alien tools and medicines."),
                            $"{row.Id} CMSS13 source description");
                        Assert.That(item.Size.Id, Is.EqualTo("Normal"), $"{row.Id} CMSS13 w_class = SIZE_MEDIUM");
                        Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.SUITSTORAGE), $"{row.Id} CMSS13 flags_equip_slot = SLOT_STORE");
                        Assert.That(storage.MaxItemSize, Is.EqualTo("Normal"), $"{row.Id} should accept source medicomp contents.");
                        Assert.That(limited.Limits, Has.Count.EqualTo(1), $"{row.Id} CMSS13 storage_slots = 12");
                        Assert.That(limited.Limits[0].Count, Is.EqualTo(12), $"{row.Id} CMSS13 storage_slots = 12");
                        Assert.That(limited.Limits[0].Popup, Is.EqualTo("rmc-storage-limit-cant-fit"));
                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(medicomp), Is.True,
                            $"{row.Id} maps CMSS13 flags_item = ITEM_PREDATOR.");
                        AssertMedicompContents(entMan, storage, row.ExpectedContents);
                    }

                    AssertBundle(prototypes, entMan, "CMUYautjaHuntingEquipmentBundle",
                    [
                        "CMUYautjaBodyMesh",
                        "CMUYautjaHuntingPouch",
                        "CMUYautjaMedicompFull",
                        "CMUYautjaRelayBeacon",
                        "CMUYautjaCleanserGelVial",
                    ]);
                    AssertBundle(prototypes, entMan, "CMUYautjaYoungbloodHuntingEquipmentBundle",
                    [
                        "CMUYautjaBodyMesh",
                        "CMUYautjaHuntingPouch",
                        "CMUYautjaMedicompFull",
                        "CMUYautjaLantern",
                    ]);
                    AssertBundle(prototypes, entMan, "CMUYautjaStrandedHuntingEquipmentBundle",
                    [
                        "CMUYautjaBodyMeshScalable",
                        "CMUYautjaHuntingPouch",
                        "CMUYautjaMedicompFull",
                        "CMUYautjaCleanserGelVial",
                    ]);
                    AssertBundle(prototypes, entMan, "CMUYautjaBloodedThrallEquipmentBundle",
                    [
                        "CMUYautjaSimpleRelayBeacon",
                        "CMUYautjaMedicompThrall",
                    ]);
                    AssertBundle(prototypes, entMan, "CMUYautjaBadBloodHuntingEquipmentBundle",
                    [
                        "CMUYautjaBodyMeshScalable",
                        "CMUYautjaHuntingPouch",
                        "CMUYautjaMedicompSurvivor",
                        "CMUYautjaCleanserGelVial",
                        "CMUYautjaHivebreaker",
                    ]);
                });
            }
            finally
            {
                DeleteFlamerChains(entMan);

                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var lanternPrototype = prototypes.Index<EntityPrototype>("CMUYautjaLantern");

            Assert.That(lanternPrototype.TryGetComponent<SpriteComponent>(out var lanternSprite, factory), Is.True);
            Assert.That(lanternSprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_RMC14/Objects/Tools/Light/lantern.rsi")));
            Assert.That(lanternSprite.AllLayers.Select(layer => layer.RsiState.Name).ToArray(),
                Is.EqualTo(new[] { "lantern", "lantern-on" }));
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHivebreaker", "_CMU14/Yautja/yautja_items.rsi", "emitter-xeno");

            var medicompPrototype = prototypes.Index<EntityPrototype>("CMUYautjaMedicomp");
            Assert.That(medicompPrototype.TryGetComponent<SpriteComponent>(out var medicompSprite, factory), Is.True);
            Assert.That(
                medicompSprite!.AllLayers.Select(layer => layer.RsiState.Name).ToArray(),
                Is.EqualTo(new[] { "medicomp", "medicomp", "medicomp_open" }),
                "CMSS13 /obj/item/storage/medicomp/update_icon() uses medicomp_open only when contents are empty and medicomp otherwise.");
            Assert.That(medicompPrototype.TryGetComponent<CMStorageVisualizerComponent>(out var medicompVisuals, factory), Is.True,
                "Local medicomp should map CMSS13 update_icon() through contents-based storage visuals.");
            Assert.That(medicompVisuals!.StorageClosed, Is.EqualTo("closedLayer"));
            Assert.That(medicompVisuals.StorageOpen, Is.EqualTo("openLayer"));
            Assert.That(medicompVisuals.StorageEmpty, Is.EqualTo("emptyLayer"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMedicalAndToolStorageFillsMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var toolbelt = entMan.SpawnEntity("CMUYautjaToolbelt", MapCoordinates.Nullspace);
            var toolbeltFilled = entMan.SpawnEntity("CMUYautjaToolbeltFilled", MapCoordinates.Nullspace);
            var herbalCase = entMan.SpawnEntity("CMUYautjaHerbalCase", MapCoordinates.Nullspace);

            try
            {
                var toolbeltMeta = entMan.GetComponent<MetaDataComponent>(toolbelt);
                var toolbeltFill = entMan.GetComponent<StorageFillComponent>(toolbeltFilled);
                var herbalStorage = entMan.GetComponent<StorageComponent>(herbalCase);
                var herbalFill = entMan.GetComponent<StorageFillComponent>(herbalCase);

                Assert.Multiple(() =>
                {
                    Assert.That(toolbeltMeta.EntityName, Is.EqualTo("alien toolbelt"),
                        "CMSS13 /obj/item/storage/belt/utility/pred source name.");
                    Assert.That(toolbeltMeta.EntityDescription,
                        Is.EqualTo("A modular belt with various clips. This version lacks any hunting functionality, and is commonly used by engineers to transport important tools."),
                        "CMSS13 /obj/item/storage/belt/utility/pred source description.");
                    AssertStorageFill(
                        toolbeltFill,
                        new Dictionary<string, int>
                        {
                            ["CMUYautjaScrewdriver"] = 1,
                            ["CMUYautjaWrench"] = 1,
                            ["CMUYautjaWelder"] = 1,
                            ["CMUYautjaCrowbar"] = 1,
                            ["CMUYautjaWirecutters"] = 1,
                            ["CableApcStack10"] = 1,
                            ["CMUYautjaMultitool"] = 1,
                        });

                    Assert.That(herbalStorage.Grid.GetArea(), Is.EqualTo(4),
                        "CMSS13 /obj/item/storage/herbal_case storage_slots = 4.");
                    AssertStorageFill(
                        herbalFill,
                        new Dictionary<string, int>
                        {
                            ["CMUYautjaAdvancedBruisePack"] = 2,
                            ["CMUYautjaAdvancedOintment"] = 2,
                        });
                });
            }
            finally
            {
                foreach (var uid in new[] { toolbelt, toolbeltFilled, herbalCase })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(
                prototypes,
                factory,
                "CMUYautjaToolbelt",
                "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi",
                "utilitybelt_pred");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCommunicatorChannelsAndKeysMatchCmss13Source()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var channelId in new[]
                             {
                                 "CMUYautja",
                                 "CMUYautjaOverseer",
                                 "CMUYautjaBadBlood",
                                 "CMUYautjaStranded",
                             })
                    {
                        Assert.That(prototypes.HasIndex<RadioChannelPrototype>(channelId), Is.True,
                            $"{channelId} should locally represent the CMSS13 Yautja communicator channel family.");
                    }

                    foreach (var row in Cmss13CommunicatorChannelRows())
                    {
                        var communicator = SpawnAndTrack(entMan, row.CommunicatorId, spawned);
                        var holder = entMan.GetComponent<EncryptionKeyHolderComponent>(communicator);

                        Assert.That(holder.KeyContainer.ContainedEntities, Has.Count.EqualTo(1),
                            $"{row.CommunicatorId} CMSS13 initial key/frequency mapping should be represented by exactly one local key.");

                        var containedKey = holder.KeyContainer.ContainedEntities.Single();
                        var containedKeyId = entMan.GetComponent<MetaDataComponent>(containedKey).EntityPrototype?.ID;
                        var containedKeyComp = entMan.GetComponent<EncryptionKeyComponent>(containedKey);

                        Assert.That(containedKeyId, Is.EqualTo(row.KeyId),
                            $"{row.CommunicatorId} should fill the local key equivalent for its CMSS13 frequency.");
                        Assert.That(containedKeyComp.Channels, Is.EquivalentTo(row.Channels),
                            $"{row.KeyId} channel list should map the CMSS13 source radio channel list.");
                        Assert.That(containedKeyComp.DefaultChannel, Is.EqualTo(row.DefaultChannel),
                            $"{row.KeyId} default channel should match the communicator's source frequency.");
                        Assert.That(holder.Channels, Is.EquivalentTo(row.Channels),
                            $"{row.CommunicatorId} active headset channels should be populated from its filled key.");
                        Assert.That(holder.DefaultChannel, Is.EqualTo(row.DefaultChannel),
                            $"{row.CommunicatorId} default headset channel should match its filled key.");

                        if (row.SourceKeyName is { } sourceName)
                        {
                            var keyMeta = entMan.GetComponent<MetaDataComponent>(containedKey);
                            Assert.That(keyMeta.EntityName, Is.EqualTo(sourceName), $"{row.KeyId} CMSS13 source key name");
                            Assert.That(keyMeta.EntityDescription, Is.EqualTo(row.SourceKeyDescription), $"{row.KeyId} CMSS13 source key description");
                        }
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMedicompPayloadItemsMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in Cmss13MedicompPayloadRows())
                    {
                        var uid = SpawnAndTrack(entMan, row.Id, spawned);
                        var meta = entMan.GetComponent<MetaDataComponent>(uid);
                        var item = entMan.GetComponent<ItemComponent>(uid);

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                            $"{row.Id} local prototype for CMSS13 {row.SourcePath} source type");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                            $"{row.Id} local description for CMSS13 {row.SourcePath} source type");
                        Assert.That(item.Size.Id, Is.EqualTo(row.Size),
                            $"{row.Id} local item size should preserve the source payload's storage role.");
                        AssertMedicompPayloadTags(entMan, uid, row.Id, row.ExpectedTags);
                        Assert.That(entMan.HasComponent<YautjaMedicalItemComponent>(uid), Is.EqualTo(row.YautjaMedicalItem),
                            $"{row.Id} local marker for Yautja-specific direct medical payloads.");

                        if (row.StackType is { } stackType)
                        {
                            var stack = entMan.GetComponent<StackComponent>(uid);
                            Assert.That(stack.StackTypeId, Is.EqualTo(stackType), $"{row.Id} stack type");
                            Assert.That(stack.Count, Is.EqualTo(row.StackCount), $"{row.Id} spawn stack count");
                            Assert.That(prototypes.Index<StackPrototype>(stackType).MaxCount, Is.EqualTo(row.StackMaxCount),
                                $"{row.Id} source medicomp fill count is represented by local stack capacity.");
                        }

                        if (row.Healing is { } healing)
                        {
                            var healingGun = entMan.GetComponent<YautjaHealingGunComponent>(uid);
                            var useDelay = entMan.GetComponent<UseDelayComponent>(uid);

                            Assert.That(useDelay.Delay, Is.EqualTo(TimeSpan.FromSeconds(2)),
                                "Local healing gun cooldown protects the source medicomp single-tool role.");
                            Assert.That(healingGun.RepairsFractures, Is.True,
                                "The local healing gun preserves the CMSS13 surgery-tool role for fracture repair.");
                            Assert.That(healingGun.DamageContainers, Is.EquivalentTo(new[] { "Biological" }));
                            Assert.That(healingGun.BloodlossModifier, Is.EqualTo(healing.BloodlossModifier));
                            Assert.That(healingGun.Damage.Empty, Is.False,
                                "The source medicomp healing gun should keep a concrete local healing payload.");
                        }

                        if (row.Hypospray is { } hypo)
                        {
                            var hypospray = entMan.GetComponent<HyposprayComponent>(uid);
                            var refillable = entMan.GetComponent<CMRefillableSolutionComponent>(uid);
                            var useDelay = entMan.GetComponent<UseDelayComponent>(uid);
                            var solutions = entMan.GetComponent<SolutionContainerManagerComponent>(uid);

                            Assert.That(hypospray.TransferAmount, Is.EqualTo((FixedPoint2) hypo.TransferAmount),
                                $"{row.Id} one-shot source injector dose mapping");
                            Assert.That(hypospray.InjectOnly, Is.True, $"{row.Id} source autoinjector is not a draw syringe.");
                            Assert.That(hypospray.OnlyAffectsMobs, Is.True);
                            Assert.That(hypospray.CanContainerDraw, Is.False);
                            Assert.That(useDelay.Delay, Is.EqualTo(TimeSpan.FromSeconds(0.2)));
                            Assert.That(refillable.Solution, Is.EqualTo("pen"));
                            Assert.That(refillable.Reagents.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value),
                                Is.EqualTo(hypo.ExpectedReagents.ToDictionary(kvp => kvp.Key, kvp => (FixedPoint2) kvp.Value)));
                            Assert.That(
                                solutionSystem.TryGetSolution((uid, solutions), "pen", out _, out var solution),
                                Is.True,
                                $"{row.Id} pen solution exists");
                            Assert.That(solution!.MaxVolume, Is.EqualTo((FixedPoint2) hypo.MaxVolume));
                            foreach (var (reagent, quantity) in hypo.ExpectedReagents)
                            {
                                Assert.That(solution.GetTotalPrototypeQuantity(reagent), Is.EqualTo((FixedPoint2) quantity),
                                    $"{row.Id} starts with CMSS13 source injector reagent {reagent}");
                            }
                        }
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaAdvancedBruisePack", "_CMU14/Yautja/yautja_items.rsi", "brute_herbs");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaAdvancedOintment", "_CMU14/Yautja/yautja_items.rsi", "burn_herbs");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHealingGun", "_CMU14/Yautja/healing_gun.rsi", "healing_gun");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHerbalCase", "_RMC14/Objects/Storage/surgical_case.rsi", "surgical_case_base");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaMedicomp", "_CMU14/Yautja/yautja_items.rsi", "medicomp");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PolishingRagPrototypeMatchesCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rag = entMan.SpawnEntity("CMUYautjaPolishingRag", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(rag);
                var item = entMan.GetComponent<ItemComponent>(rag);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("polishing rag"),
                        "CMSS13 /obj/item/reagent_container/glass/rag/polishing_rag source name.");
                    Assert.That(meta.EntityDescription, Is.EqualTo("An astonishingly fine, hand-tailored piece of exotic cloth."),
                        "CMSS13 /obj/item/reagent_container/glass/rag/polishing_rag source description.");
                    Assert.That(item.Size.Id, Is.EqualTo("Tiny"),
                        "Local tiny item size keeps the rag in the source lightweight utility-item role.");
                    Assert.That(entMan.HasComponent<YautjaPolishingRagComponent>(rag), Is.True,
                        "The local component represents CMSS13 polishing_rag/afterattack behavior.");
                });
            }
            finally
            {
                if (!entMan.Deleted(rag))
                    entMan.DeleteEntity(rag);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaPolishingRag", "_CMU14/Yautja/yautja_items.rsi", "polishing_rag");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerIdChipAndFabricatedMedicalItemsMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                var idChip = SpawnAndTrack(entMan, "CMUYautjaBracerIdChip", spawned);
                var idPrototype = prototypes.Index<EntityPrototype>("CMUYautjaBracerIdChip");
                var idItem = entMan.GetComponent<ItemComponent>(idChip);
                var idAccess = entMan.GetComponent<AccessComponent>(idChip);

                Assert.Multiple(() =>
                {
                    Assert.That(idPrototype.Name, Is.EqualTo("bracer ID chip"),
                        "CMSS13 /obj/item/card/id/bracer_chip source name");
                    Assert.That(idPrototype.Description, Is.EqualTo("A complex cypher chip embedded within a set of clan bracers."),
                        "CMSS13 /obj/item/card/id/bracer_chip source description");
                    Assert.That(idItem.Size.Id, Is.EqualTo("Tiny"), "CMSS13 bracer chip w_class = SIZE_TINY");
                    Assert.That(entMan.HasComponent<IdCardComponent>(idChip), Is.True,
                        "CMSS13 bracer chip remains an ID card.");
                    Assert.That(entMan.HasComponent<YautjaBracerIdChipComponent>(idChip), Is.True,
                        "Local embedded-bracer marker should stay on the source chip equivalent.");
                    Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(idChip, out var tech), Is.True,
                        "CMSS13 bracer chip flags_item includes ITEM_PREDATOR.");
                    Assert.That(tech!.BlockPickup, Is.False,
                        "The local embedded bracer chip remains deployable/retractable by bracer runtime.");
                    Assert.That(tech.BlockUse, Is.False);
                    Assert.That(tech.BlockMelee, Is.False);
                    Assert.That(tech.BlockThrow, Is.False);
                    Assert.That(tech.BlockShoot, Is.False);
                    Assert.That(idAccess.Groups, Is.EquivalentTo(new[] { "CMUYautjaAccessSecure" }),
                        "CMSS13 base bracer chip grants only ACCESS_YAUTJA_SECURE before rank-specific runtime updates.");
                    Assert.That(idAccess.Tags, Is.EquivalentTo(new[] { "CMUAccessYautjaSecure" }),
                        "MapInit should expand only the source-equivalent secure access tag.");
                });

                foreach (var row in Cmss13BracerFabricatedMedicalRows())
                {
                    var uid = SpawnAndTrack(entMan, row.LocalId, spawned);
                    var meta = entMan.GetComponent<MetaDataComponent>(uid);
                    var item = entMan.GetComponent<ItemComponent>(uid);

                    Assert.Multiple(() =>
                    {
                        Assert.That(meta.EntityName, Is.EqualTo(row.Payload.Name),
                            $"{row.LocalId} should map the bracer proc payload {row.SourceSpawnPath}");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Payload.Description),
                            $"{row.LocalId} local description for {row.SourceSpawnPath}");
                        Assert.That(item.Size.Id, Is.EqualTo(row.Payload.Size),
                            $"{row.LocalId} local item size for source-spawned payload");
                        AssertMedicompPayloadTags(entMan, uid, row.LocalId, row.Payload.ExpectedTags);
                        Assert.That(entMan.HasComponent<YautjaMedicalItemComponent>(uid), Is.EqualTo(row.Payload.YautjaMedicalItem),
                            $"{row.LocalId} local Yautja medical marker should match the source payload equivalent.");
                    });

                    if (row.Payload.StackType is { } stackType)
                    {
                        var stack = entMan.GetComponent<StackComponent>(uid);
                        Assert.Multiple(() =>
                        {
                            Assert.That(stack.StackTypeId, Is.EqualTo(stackType), $"{row.LocalId} source payload stack type");
                            Assert.That(stack.Count, Is.EqualTo(row.Payload.StackCount), $"{row.LocalId} source payload spawn count");
                            Assert.That(prototypes.Index<StackPrototype>(stackType).MaxCount, Is.EqualTo(row.Payload.StackMaxCount),
                                $"{row.LocalId} source payload stack max count");
                        });
                    }

                    if (row.Payload.Hypospray is { } hypo)
                    {
                        var hypospray = entMan.GetComponent<HyposprayComponent>(uid);
                        var refillable = entMan.GetComponent<CMRefillableSolutionComponent>(uid);
                        var useDelay = entMan.GetComponent<UseDelayComponent>(uid);
                        var solutions = entMan.GetComponent<SolutionContainerManagerComponent>(uid);

                        Assert.Multiple(() =>
                        {
                            Assert.That(hypospray.TransferAmount, Is.EqualTo((FixedPoint2) hypo.TransferAmount),
                                $"{row.LocalId} source autoinjector dose");
                            Assert.That(hypospray.InjectOnly, Is.True, $"{row.LocalId} source autoinjector inject-only behavior");
                            Assert.That(hypospray.OnlyAffectsMobs, Is.True);
                            Assert.That(hypospray.CanContainerDraw, Is.False);
                            Assert.That(useDelay.Delay, Is.EqualTo(TimeSpan.FromSeconds(0.2)));
                            Assert.That(refillable.Solution, Is.EqualTo("pen"));
                            Assert.That(refillable.Reagents.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value),
                                Is.EqualTo(hypo.ExpectedReagents.ToDictionary(kvp => kvp.Key, kvp => (FixedPoint2) kvp.Value)));
                            Assert.That(
                                solutionSystem.TryGetSolution((uid, solutions), "pen", out _, out var solution),
                                Is.True,
                                $"{row.LocalId} pen solution exists");
                            Assert.That(solution!.MaxVolume, Is.EqualTo((FixedPoint2) hypo.MaxVolume));
                            foreach (var (reagent, quantity) in hypo.ExpectedReagents)
                            {
                                Assert.That(solution.GetTotalPrototypeQuantity(reagent), Is.EqualTo((FixedPoint2) quantity),
                                    $"{row.LocalId} starts with CMSS13 source injector reagent {reagent}");
                            }
                        });
                    }
                }

                var bracer = SpawnAndTrack(entMan, "CMUYautjaBracer", spawned);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.IdChipPrototype.Id, Is.EqualTo("CMUYautjaBracerIdChip"));
                    Assert.That(bracerComp.StabilisingCrystalPrototype.Id, Is.EqualTo("CMUYautjaStabilisingCrystal"));
                    Assert.That(bracerComp.HumanStabilisingCrystalPrototype.Id, Is.EqualTo("CMUYautjaHumanStabilisingCrystal"));
                    Assert.That(bracerComp.HealingCapsulePrototype.Id, Is.EqualTo("CMUYautjaHealingCapsule"));
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaBracerIdChip", "_CMU14/HunterShip/obj/items/radio.rsi", "upp_key");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaStabilisingCrystal", "_RMC14/Objects/Medical/emergency_auto_injector.rsi", "autoinjector");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHumanStabilisingCrystal", "_RMC14/Objects/Medical/emergency_auto_injector.rsi", "autoinjector");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHealingCapsule", "Objects/Specific/Medical/medical.rsi", "medicated-suture");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundObservationPadStaticFactsMatchCmss13Source()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(pad);
                var item = entMan.GetComponent<ItemComponent>(pad);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("Hellhound Observation Pad"),
                        "CMSS13 /obj/item/device/houndcam source name.");
                    Assert.That(meta.EntityDescription,
                        Is.EqualTo("A portable camera console device, used for remotely overwatching Hellhounds."),
                        "CMSS13 /obj/item/device/houndcam source description.");
                    Assert.That(item.Size.Id, Is.EqualTo("Small"),
                        "CMSS13 /obj/item/device/houndcam w_class = SIZE_SMALL.");
                    Assert.That(entMan.TryGetComponent<MeleeWeaponComponent>(pad, out var melee), Is.True,
                        "CMSS13 /obj/item/device/houndcam force = 1 means the held item must have explicit weak melee damage.");
                    Assert.That(DamageTotal(melee!.Damage), Is.EqualTo((FixedPoint2) 1),
                        "CMSS13 /obj/item/device/houndcam force = 1.");
                    Assert.That(entMan.TryGetComponent<DamageOtherOnHitComponent>(pad, out var thrown), Is.True,
                        "CMSS13 /obj/item/device/houndcam throwforce = 1 means thrown-hit damage should not inherit the generic BaseItem collision value.");
                    Assert.That(DamageTotal(thrown!.Damage), Is.EqualTo((FixedPoint2) 1),
                        "CMSS13 /obj/item/device/houndcam throwforce = 1.");
                    AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, pad, "CMUYautjaHoundObservationPad");
                    AssertNonCorrodible(entMan, pad);
                });
            }
            finally
            {
                if (!entMan.Deleted(pad))
                    entMan.DeleteEntity(pad);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(
                prototypes,
                factory,
                "CMUYautjaHoundObservationPad",
                "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi",
                "houndpad");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMcasteStaticPrototypeParityTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var actions = entMan.System<ActionContainerSystem>();
            var spawned = new List<EntityUid>();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in Cmss13McasteItemRows())
                    {
                        Assert.That(prototypes.HasIndex<EntityPrototype>(row.Id), Is.True, $"Missing {row.Id}");
                        if (!prototypes.HasIndex<EntityPrototype>(row.Id))
                            continue;

                        var uid = SpawnAndTrack(entMan, row.Id, spawned);
                        var meta = entMan.GetComponent<MetaDataComponent>(uid);
                        var item = entMan.GetComponent<ItemComponent>(uid);

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} CMSS13 source name.");
                        if (row.CheckDescription)
                            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} CMSS13 source description.");
                        Assert.That(item.Size.Id, Is.EqualTo(row.Size), $"{row.Id} CMSS13 w_class local mapping.");

                        if (row.Slots is { } slots)
                        {
                            var clothing = entMan.GetComponent<ClothingComponent>(uid);
                            Assert.That(clothing.Slots, Is.EqualTo(slots), $"{row.Id} CMSS13 equip-slot mapping.");
                        }

                        if (row.Stats is { } stats)
                            AssertCmss13ArmorStats(entMan, uid, row.Id, stats);

                        if (row.ItemPredator)
                            AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, uid, row.Id, row.BlockPickup);

                        if (row.Unacidable)
                            AssertNonCorrodible(entMan, uid);
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaMcasteHerbContainer"))
                    {
                        var herbCase = entMan.GetComponent<StorageComponent>(SpawnAndTrack(entMan, "CMUYautjaMcasteHerbContainer", spawned));
                        Assert.That(herbCase.Grid.GetArea(), Is.EqualTo(4),
                            "CMSS13 /obj/item/storage/mcaste_herb_container storage_slots = 4.");
                        Assert.That(herbCase.Whitelist.Tags, Is.Not.Null,
                            "CMSS13 /obj/item/storage/mcaste_herb_container can_hold lists predator bruise packs and ointments.");
                        Assert.That(herbCase.Whitelist.Tags!, Does.Contain("CMUYautjaHerbalMedicine"),
                            "Local predator bruise packs and ointments share the CMUYautjaHerbalMedicine tag.");
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaMcasteHerbContainerFilled"))
                    {
                        var filledHerbs = entMan.GetComponent<StorageFillComponent>(SpawnAndTrack(entMan, "CMUYautjaMcasteHerbContainerFilled", spawned));
                        AssertStorageFill(filledHerbs, new Dictionary<string, int>
                        {
                            ["CMUYautjaAdvancedBruisePack"] = 2,
                            ["CMUYautjaAdvancedOintment"] = 2,
                        });
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaSoldierBracers"))
                    {
                        var hunter = SpawnAndTrack(entMan, "CMMobHuman", spawned);
                        entMan.EnsureComponent<YautjaComponent>(hunter);
                        var soldierBracers = SpawnAndTrack(entMan, "CMUYautjaSoldierBracers", spawned);
                        var ev = new GetItemActionsEvent(actions, hunter, soldierBracers, SlotFlags.GLOVES);
                        entMan.EventBus.RaiseLocalEvent(soldierBracers, ev);
                        var actionIds = ActionPrototypeIds(entMan, ev.Actions);

                        Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleWristBlades"),
                            "CMSS13 soldier bracer bracer_actions includes wristblade.");
                        Assert.That(actionIds, Does.Contain("CMUActionYautjaCreateStabilisingCrystal"),
                            "CMSS13 soldier bracer bracer_actions includes thwei.");
                        Assert.That(actionIds, Does.Contain("CMUActionYautjaCreateHealingCapsule"),
                            "CMSS13 soldier bracer bracer_actions includes capsule.");
                        Assert.That(actionIds, Does.Contain("CMUActionYautjaTranslator"),
                            "CMSS13 soldier bracer bracer_actions includes translator.");
                        Assert.That(actionIds, Does.Contain("CMUActionYautjaSelfDestruct"),
                            "CMSS13 soldier bracer bracer_actions includes self_destruct.");
                        Assert.That(actionIds, Is.EquivalentTo(new[]
                        {
                            "CMUActionYautjaToggleWristBlades",
                            "CMUActionYautjaCreateStabilisingCrystal",
                            "CMUActionYautjaCreateHealingCapsule",
                            "CMUActionYautjaTranslator",
                            "CMUActionYautjaSelfDestruct",
                        }), "CMSS13 soldier bracer replaces the inherited bracer_actions list instead of keeping normal hunter bracer actions.");
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaPoweredArmor"))
                    {
                        var poweredArmor = SpawnAndTrack(entMan, "CMUYautjaPoweredArmor", spawned);
                        var speedTier = entMan.GetComponent<RMCArmorSpeedTierComponent>(poweredArmor);
                        var speed = entMan.GetComponent<ClothingSpeedModifierComponent>(poweredArmor);
                        Assert.That(speedTier.SpeedTier, Is.EqualTo("heavy"),
                            "CMSS13 powered armor slowdown = SLOWDOWN_ARMOR_LOWHEAVY uses the local heavy speed tier.");
                        Assert.That(speed.WalkModifier, Is.EqualTo(0.64f),
                            "Local SLOWDOWN_ARMOR_LOWHEAVY mapping.");
                        Assert.That(speed.SprintModifier, Is.EqualTo(0.64f),
                            "Local SLOWDOWN_ARMOR_LOWHEAVY mapping.");
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaPoweredHelmet"))
                    {
                        var poweredHelmet = SpawnAndTrack(entMan, "CMUYautjaPoweredHelmet", spawned);
                        var resistance = entMan.GetComponent<ParasiteResistanceComponent>(poweredHelmet);
                        var helmetMask = entMan.GetComponent<YautjaMaskComponent>(poweredHelmet);
                        Assert.That(resistance.MaxCount, Is.EqualTo(100),
                            "CMSS13 powered helmet anti_hug = 100.");
                        Assert.That(helmetMask.Slots, Is.EqualTo(SlotFlags.HEAD),
                            "CMSS13 powered helmet grants visor/zoom actions while worn in WEAR_HEAD, not WEAR_FACE.");
                        Assert.That(helmetMask.RequiresYautjaWearer, Is.False,
                            "CMSS13 powered helmet base togglesight() uses TRAIT_YAUTJA_TECH/thrall gating and does not add the hunter-mask isyautja override.");
                        Assert.That(helmetMask.Drain, Is.EqualTo((FixedPoint2) 3),
                            "CMSS13 powered helmet process() drains 3 charge while visor goggles are active.");
                        Assert.That(helmetMask.ZoomOffset, Is.EqualTo(12f).Within(0.001f),
                            "CMSS13 powered helmet toggle_zoom() calls zoom(usr, 11, 12).");
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaMilitaryEncryptionKey"))
                    {
                        var encryptionKey = SpawnAndTrack(entMan, "CMUYautjaMilitaryEncryptionKey", spawned);
                        var key = entMan.GetComponent<EncryptionKeyComponent>(encryptionKey);
                        Assert.That(key.Channels, Does.Contain("CMUYautjaMilitary"),
                            "CMSS13 military encryption key uses RADIO_CHANNEL_YAUTJA_SPECOPS.");
                        Assert.That(key.DefaultChannel, Is.EqualTo("CMUYautjaMilitary"),
                            "Military communicator should default to the military Yautja channel.");
                    }

                    if (prototypes.HasIndex<EntityPrototype>("CMUYautjaCannonPack"))
                    {
                        var cannonPack = SpawnAndTrack(entMan, "CMUYautjaCannonPack", spawned);
                        var pack = entMan.GetComponent<YautjaCannonPackComponent>(cannonPack);
                        Assert.That(pack.Charge, Is.EqualTo((FixedPoint2) 2000),
                            "CMSS13 /obj/item/yautja_cannon_pack starts with charge = 2000.");
                        Assert.That(pack.MaxCharge, Is.EqualTo((FixedPoint2) 2000),
                            "CMSS13 /obj/item/yautja_cannon_pack charge_max = 2000.");
                        Assert.That(pack.Regen, Is.EqualTo((FixedPoint2) 200),
                            "CMSS13 /obj/item/yautja_cannon_pack charge_rate = 200.");
                        Assert.That(pack.DeployCost, Is.EqualTo((FixedPoint2) 50),
                            "CMSS13 cannon_internal() drains 50 power before deploying the plasma cannons.");
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var row in Cmss13McasteItemRows())
            {
                if (row.Sprite is { } sprite && row.State is { } state)
                    AssertPrototypeIconState(prototypes, factory, row.Id, sprite, state);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CannonPackWornActionMatchesCmss13BackpackAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var actions = entMan.System<ActionContainerSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var wornEvent = new GetItemActionsEvent(actions, hunter, pack, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(pack, wornEvent);
                var actionIds = ActionPrototypeIds(entMan, wornEvent.Actions);

                Assert.Multiple(() =>
                {
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaUsePlasmaCannons"),
                        "CMSS13 /obj/item/yautja_cannon_pack backpack_actions includes /datum/action/predator_action/pack/cannons while worn on the back.");
                    Assert.That(actionIds, Is.EquivalentTo(new[] { "CMUActionYautjaUsePlasmaCannons" }),
                        "The cannon pack source only grants the plasma-cannons backpack action.");
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
    public async Task CannonPackExamineAndLowPowerDrainUseCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pack = entMan.SpawnEntity("CMUYautjaCannonPack", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaUsePlasmaCannons", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                packComp.Charge = 40;

                Assert.That(examine.GetExamineText(pack, hunter).ToMarkup(),
                    Does.Contain("It currently has <bold>40/2000</bold> charge."),
                    "CMSS13 /obj/item/yautja_cannon_pack/get_examine_text() exposes current/max pack charge.");
                Assert.That(Loc.GetString(
                        "cmu-yautja-cannon-pack-drain-failed",
                        ("charge", 40),
                        ("max", 2000),
                        ("amount", 50)),
                    Is.EqualTo("Your pack lacks the energy. It only has <bold>40/2000</bold> remaining and needs <bold>50</bold>."),
                    "CMSS13 drain_power() warning bolds current/max and requested charge.");

                var deploy = new YautjaUsePlasmaCannonsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(pack, deploy);

                Assert.Multiple(() =>
                {
                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 40),
                        "CMSS13 drain_power() returns false without subtracting when charge is below the requested amount.");
                    Assert.That(packComp.CannonsDeployed, Is.False,
                        "Low pack charge stops cannon_internal() before deployment.");
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
    public async Task CannonPackRechargeUsesCmss13LevelMultipliers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var openSpaceMap = await pair.CreateTestMap();
        var groundMap = await pair.CreateTestMap();
        var mainshipMap = await pair.CreateTestMap();

        EntityUid openSpaceHunter = default;
        EntityUid openSpacePack = default;
        EntityUid groundHunter = default;
        EntityUid groundPack = default;
        EntityUid mainshipHunter = default;
        EntityUid mainshipPack = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var inventory = entMan.System<InventorySystem>();

                entMan.EnsureComponent<RMCPlanetComponent>(groundMap.Grid.Owner);
                var mainshipAreas = entMan.EnsureComponent<AreaGridComponent>(mainshipMap.Grid.Owner);
                areas.ReplaceArea(mainshipAreas, Vector2i.Zero, "RMCAreaAlmayer");

                openSpaceHunter = entMan.SpawnEntity("CMMobHuman", openSpaceMap.GridCoords);
                openSpacePack = entMan.SpawnEntity("CMUYautjaCannonPack", openSpaceMap.GridCoords);
                groundHunter = entMan.SpawnEntity("CMMobHuman", groundMap.GridCoords);
                groundPack = entMan.SpawnEntity("CMUYautjaCannonPack", groundMap.GridCoords);
                mainshipHunter = entMan.SpawnEntity("CMMobHuman", mainshipMap.GridCoords);
                mainshipPack = entMan.SpawnEntity("CMUYautjaCannonPack", mainshipMap.GridCoords);

                PrepareCannonPackRegen(entMan, inventory, openSpaceHunter, openSpacePack);
                PrepareCannonPackRegen(entMan, inventory, groundHunter, groundPack);
                PrepareCannonPackRegen(entMan, inventory, mainshipHunter, mainshipPack);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var openSpacePackComp = entMan.GetComponent<YautjaCannonPackComponent>(openSpacePack);
                var groundPackComp = entMan.GetComponent<YautjaCannonPackComponent>(groundPack);
                var mainshipPackComp = entMan.GetComponent<YautjaCannonPackComponent>(mainshipPack);

                Assert.Multiple(() =>
                {
                    Assert.That(openSpacePackComp.Charge, Is.EqualTo((FixedPoint2) 1200),
                        "CMSS13 cannon pack recharges by full charge_rate outside ground/mainship z-levels.");
                    Assert.That(groundPackComp.Charge, Is.EqualTo((FixedPoint2) 1033.333),
                        "CMSS13 is_ground_level() cannon pack recharge is charge_rate / 6.");
                    Assert.That(mainshipPackComp.Charge, Is.EqualTo((FixedPoint2) 1066.667),
                        "CMSS13 is_mainship_level() cannon pack recharge is charge_rate / 3.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[]
                         {
                             openSpaceHunter,
                             openSpacePack,
                             groundHunter,
                             groundPack,
                             mainshipHunter,
                             mainshipPack,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
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
    public async Task DualPlasmaCannonsLiveFireUsesSourcePackLanceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid pack = default;
        EntityUid action = default;
        EntityUid projectile = default;

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
                Assert.That(inventory.TryEquip(hunter, pack, "back", silent: true, force: true), Is.True);

                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
                packComp.Charge = 2000;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                RaiseUsePlasmaCannons(entMan, pack, hunter, action, actionComp);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var gunSystem = entMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);

                var cannons = packComp.Cannon!.Value;
                var gun = entMan.GetComponent<GunComponent>(cannons);
                var target = entMan.GetComponent<TransformComponent>(hunter).Coordinates.Offset(new Vector2(10, 0));
                var projectiles = gunSystem.AttemptShoot((cannons, gun), hunter, target);

                Assert.That(projectiles, Is.Not.Null,
                    "CMSS13 deployed dual cannons must pass through the real gun firing path, not only manual ammo events.");
                projectile = projectiles!.Single();

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(projectile).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaCasterLanceBolt"),
                        "CMSS13 dual plasma cannons use /datum/ammo/energy/yautja/caster/lance; local keeps this as a named lance-equivalent projectile.");
                    Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1000),
                        "A real live shot drains 1000 charge from the source pack.");
                    Assert.That(entMan.HasComponent<YautjaCannonPackProjectileRefundComponent>(projectile), Is.True,
                        "The projectile starts from the same source-pack refund bookkeeping before AmmoShotEvent marks it fired.");
                    AssertExplosionPayload(entMan,
                        projectile,
                        50,
                        50,
                        "Until the exact source lance datum is located, the local lance-equivalent keeps the closest located caster-lethal payload.");
                    Assert.That(entMan.HasComponent<RMCScorchEffectComponent>(projectile), Is.True,
                        "The current local lance-equivalent keeps the caster-lethal scorch payload for live-fire parity coverage.");
                });

                entMan.DeleteEntity(projectile);
                entMan.FlushEntities();

                Assert.That(packComp.Charge, Is.EqualTo((FixedPoint2) 1000),
                    "CMSS13 fired dual-cannon projectile cleanup does not refund the spent source-pack charge.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { projectile, hunter, pack, action })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaEngineeringToolsMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
            var spawned = new List<EntityUid>();

            try
            {
                var toolRows = new[]
                {
                    new Cmss13YautjaToolRow(
                        "CMUYautjaCrowbar",
                        "yautja crowbar",
                        "Used to remove floors and to pry open doors, made of an unusual alloy.",
                        "Small",
                        "bar",
                        null,
                        null),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaWrench",
                        "alien wrench",
                        "A wrench with many common uses. Made of some bizarre alien bones.",
                        "Small",
                        "wrench",
                        null,
                        null),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaWirecutters",
                        "alien wirecutters",
                        "This cuts wires, also flesh. Made of some razor-sharp animal teeth.",
                        "Small",
                        "wirescutter",
                        null,
                        null),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaScrewdriver",
                        "alien screwdriver",
                        "Some high-tech screwing abilities.",
                        "Small",
                        "screwdriver",
                        7,
                        null),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaMultitool",
                        "alien multitool",
                        "Top-notch alien tech for B&E through hacking.",
                        "Small",
                        "multitool",
                        null,
                        null),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaWelder",
                        "alien chem welding tool",
                        "A complex chemical welding device, keep away from youngblood.",
                        "Small",
                        "welder",
                        10,
                        150),
                    new Cmss13YautjaToolRow(
                        "CMUYautjaMetalChains",
                        "metal chains",
                        "The weld pattern tells you that these chains were made with heavy weights in mind, the sharp edge implies this was also made to pierce.",
                        "Normal",
                        "metal_chain",
                        null,
                        null,
                        BlockUse: false),
                };

                foreach (var row in toolRows)
                {
                    var tool = SpawnAndTrack(entMan, row.Id, spawned);
                    AssertCmss13YautjaToolFacts(entMan, solutionSystem, tool, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var (id, state) in new[]
                     {
                         ("CMUYautjaCrowbar", "bar"),
                         ("CMUYautjaWrench", "wrench"),
                         ("CMUYautjaWirecutters", "wirescutter"),
                         ("CMUYautjaScrewdriver", "screwdriver"),
                         ("CMUYautjaMultitool", "multitool"),
                         ("CMUYautjaWelder", "welder"),
                         ("CMUYautjaMetalChains", "metal_chain"),
                     })
            {
                AssertPrototypeIconState(prototypes, factory, id, "_CMU14/Yautja/yautja_items.rsi", state);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaUtilityToolStaticRemaindersMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in Cmss13CommunicatorRows())
                    {
                        var communicator = SpawnAndTrack(entMan, row.Id, spawned);
                        AssertStaticPrice(entMan, communicator, row.Id, 100,
                            "CMSS13 /obj/item/device/radio/headset/yautja black_market_value = 100.");
                    }

                    var relay = SpawnAndTrack(entMan, "CMUYautjaRelayBeacon", spawned);
                    AssertStaticPrice(entMan, relay, "CMUYautjaRelayBeacon", 100,
                        "CMSS13 /obj/item/device/yautja_teleporter black_market_value = 100.");

                    var simpleRelay = SpawnAndTrack(entMan, "CMUYautjaSimpleRelayBeacon", spawned);
                    AssertStaticPrice(entMan, simpleRelay, "CMUYautjaSimpleRelayBeacon", 100,
                        "CMSS13 thrall simple relay inherits the source relay value unless an override is located.");

                    var cleanser = SpawnAndTrack(entMan, "CMUYautjaCleanserGelVial", spawned);
                    AssertStaticPrice(entMan, cleanser, "CMUYautjaCleanserGelVial", 150,
                        "CMSS13 /obj/item/tool/yautja_cleaner black_market_value = 150.");
                    AssertYautjaTechPolicy(
                        entMan,
                        cleanser,
                        "CMUYautjaCleanserGelVial",
                        blockPickup: false,
                        blockUse: true,
                        blockMelee: true,
                        blockThrow: true,
                        blockShoot: true,
                        damageMultiplier: 1f,
                        "CMSS13 cleaner uses flags_item = ITEM_PREDATOR for use authorization, but force = 0 so local tech marking must not add weapon damage scaling.");

                    foreach (var id in new[]
                             {
                                 "CMUYautjaCrowbar",
                                 "CMUYautjaWrench",
                                 "CMUYautjaWirecutters",
                                 "CMUYautjaScrewdriver",
                                 "CMUYautjaMultitool",
                                 "CMUYautjaWelder",
                                 "CMUYautjaToolbelt",
                                 "CMUYautjaToolbeltFilled",
                                 "CMUYautjaMetalChains",
                             })
                    {
                        var tool = SpawnAndTrack(entMan, id, spawned);
                        AssertYautjaTechPolicy(
                            entMan,
                            tool,
                            id,
                            blockPickup: false,
                            blockUse: false,
                            blockMelee: false,
                            blockThrow: false,
                            blockShoot: false,
                            damageMultiplier: 1f,
                            "CMSS13 yaut_items.dm engineering tools, alien toolbelt and metal chains do not set flags_item = ITEM_PREDATOR; local YautjaTechItem is only a tracker/source marker here.");
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaGearStaticPricesMatchRemainingCmss13BlackMarketValues()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var (id, price, source) in Cmss13RemainingBlackMarketPriceRows())
                {
                    var uid = SpawnAndTrack(entMan, id, spawned);
                    AssertStaticPrice(entMan, uid, id, price, source);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task XenoSkullAndPeltTrophyPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in Cmss13XenoTrophyRows())
                {
                    var trophy = SpawnAndTrack(entMan, row.Id, spawned);
                    AssertCmss13XenoTrophyStaticFacts(entMan, trophy, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var row in Cmss13XenoTrophyRows())
            {
                AssertPrototypeIconState(
                    prototypes,
                    factory,
                    row.Id,
                    "_CMU14/Yautja/yautja_items.rsi",
                    row.SpriteState);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanBoneTrophyPrototypesMatchCmss13SkeletonLimbStaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in Cmss13HumanBoneTrophyRows())
                {
                    var trophy = SpawnAndTrack(entMan, row.Id, spawned);
                    AssertCmss13HumanBoneTrophyStaticFacts(entMan, trophy, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var row in Cmss13HumanBoneTrophyRows())
            {
                AssertPrototypeIconState(
                    prototypes,
                    factory,
                    row.Id,
                    "_CMU14/Yautja/yautja_items.rsi",
                    row.SpriteState);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ButcherOutputPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var row in Cmss13ButcherOutputRows())
                {
                    var output = SpawnAndTrack(entMan, row.Id, spawned);
                    AssertCmss13ButcherOutputStaticFacts(entMan, output, row);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var row in Cmss13ButcherOutputRows())
            {
                AssertPrototypeIconState(
                    prototypes,
                    factory,
                    row.Id,
                    row.SpritePath,
                    row.SpriteState);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentVendorRowsUseCmss13HolderItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                var holderRows = new (string Id, string Name, string Description, YautjaGearKind Kind, string DeployedPrototype)[]
                {
                    ("CMUYautjaWristBladesAttachment", "wristblade bracer attachment", "A pair of huge, serrated blades.", YautjaGearKind.WristBlades, "CMUYautjaWristBlades"),
                    ("CMUYautjaScimitarAttachment", "scimitar bracer attachment", "A pair of huge, serrated blades.", YautjaGearKind.Scimitar, "CMUYautjaScimitar"),
                    ("CMUYautjaScimitarAltAttachment", "scimitar bracer attachment", "A pair of huge, serrated blades.", YautjaGearKind.Scimitar, "CMUYautjaScimitarAlt"),
                    ("CMUYautjaChainGauntletsAttachment", "chain gauntlets", "Gauntlets made out of alien alloy, you could probably wrap some chains around this after it's been put into your bracer.", YautjaGearKind.ChainGauntlet, "CMUYautjaChainGauntlet"),
                    ("CMUYautjaBracerShieldAttachment", "shield bracer attachment", "A shield made of concentric metal alloy plates. The plates fold into one another for compact storage while still providing superior protection.", YautjaGearKind.Shield, "CMUYautjaBracerShield"),
                };

                foreach (var row in holderRows)
                {
                    var holder = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);
                    spawned.Add(holder);

                    var meta = entMan.GetComponent<MetaDataComponent>(holder);
                    var stored = entMan.GetComponent<YautjaStoredGearComponent>(holder);

                    Assert.Multiple(() =>
                    {
                        Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                            $"{row.Id} should use the CMSS13 /obj/item/bracer_attachments holder name.");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                            $"{row.Id} should use the CMSS13 /obj/item/bracer_attachments holder description.");
                        Assert.That(stored.Kind, Is.EqualTo(row.Kind),
                            $"{row.Id} should install into the bracer slot matching its CMSS13 attached_weapon_type.");
                        Assert.That(stored.DeployedPrototype?.Id, Is.EqualTo(row.DeployedPrototype),
                            $"{row.Id} should point at the local equivalent of CMSS13 attached_weapon_type.");
                        Assert.That(stored.Deployed, Is.False,
                            $"{row.Id} should represent the CMSS13 holder item, not an already-deployed attached_weapon.");
                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder does not set flags_item = ITEM_PREDATOR.");
                        Assert.That(entMan.HasComponent<MeleeWeaponComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder is not the deployed attached_weapon_type.");
                        Assert.That(entMan.HasComponent<ClothingComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder does not define flags_equip_slot.");
                        Assert.That(entMan.HasComponent<BlockingComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder should not inherit deployed shield blocking.");
                        Assert.That(entMan.HasComponent<YautjaShieldBashComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder should not inherit deployed shield bash.");
                        Assert.That(entMan.HasComponent<UnremoveableComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder should not inherit deployed shield NODROP.");
                        Assert.That(entMan.HasComponent<ItemSlotsComponent>(holder), Is.False,
                            $"{row.Id} CMSS13 /obj/item/bracer_attachments holder is not a local deployable weapon container.");
                    });
                }

                AssertBundle(prototypes, entMan, "CMUYautjaWristBladesBundle", new[]
                {
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });
                AssertBundle(prototypes, entMan, "CMUYautjaFearsomeScimitarsBundle", new[]
                {
                    "CMUYautjaScimitarAttachment",
                    "CMUYautjaScimitarAttachment",
                });
                AssertBundle(prototypes, entMan, "CMUYautjaSkeweringScimitarsBundle", new[]
                {
                    "CMUYautjaScimitarAltAttachment",
                    "CMUYautjaScimitarAltAttachment",
                });
                AssertBundle(prototypes, entMan, "CMUYautjaChainGauntletsBundle", new[]
                {
                    "CMUYautjaChainGauntletsAttachment",
                    "CMUYautjaChainGauntletsAttachment",
                    "CMUYautjaChainwhip",
                });

                var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
                spawned.Add(rack);
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");

                Assert.That(bracer.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaBracerShieldAttachment",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }), "CMSS13 regular rack vends /obj/item/bracer_attachments holders, not the deployed weapon paths.");
                AssertVendorRow(bracer, "CMUYautjaBracerShieldAttachment", "The Compact Shield", recommended: true);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaBracerShieldAttachment"), "CMUYautjaBracer", 1);
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaWristBladesAttachment", "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "wrist");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaScimitarAttachment", "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "scim");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaScimitarAltAttachment", "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "scim_alt");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaChainGauntletsAttachment", "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "metal_gauntlet");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaBracerShieldAttachment", "_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi", "bracer_shield_off");
        });

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
    public async Task QuiverStrapStorageSlotsMatchCmss13EightItemLimit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storageSystem = entMan.System<SharedStorageSystem>();
            var quiver = entMan.SpawnEntity("CMUYautjaQuiverStrap", MapCoordinates.Nullspace);
            var sourceFillItems = new List<EntityUid>
            {
                entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace),
            };

            for (var i = 0; i < 7; i++)
                sourceFillItems.Add(entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace));

            var extraArrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(quiver);
                foreach (var item in sourceFillItems)
                {
                    Assert.That(
                        storageSystem.Insert(quiver, item, out _, storageComp: storage, playSound: false),
                        Is.True,
                        "CMSS13 /obj/item/storage/belt/gun/quiver/full fills one bow plus storage_slots - 1 arrows.");
                }

                Assert.Multiple(() =>
                {
                    Assert.That(storage.StoredItems, Has.Count.EqualTo(8));
                    Assert.That(
                        storageSystem.CanInsert(quiver, extraArrow, null, out var reason, storage),
                        Is.False,
                        "CMSS13 quiver has storage_slots = 8, so a ninth bow/arrow item must be rejected.");
                    Assert.That(reason, Is.EqualTo("rmc-storage-limit-cant-fit"));
                    Assert.That(
                        storageSystem.Insert(quiver, extraArrow, out _, storageComp: storage, playSound: false),
                        Is.False);
                    Assert.That(storage.StoredItems, Has.Count.EqualTo(8));
                });
            }
            finally
            {
                foreach (var uid in sourceFillItems.Append(extraArrow).Append(quiver))
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuiverStrapOnlyStoresBowsAndArrowsLikeCmss13CanHold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storageSystem = entMan.System<SharedStorageSystem>();
            var quiver = entMan.SpawnEntity("CMUYautjaQuiverStrap", MapCoordinates.Nullspace);
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);
            var arrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var crowbar = entMan.SpawnEntity("Crowbar", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(quiver);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        storageSystem.CanInsert(quiver, bow, null, out _, storage),
                        Is.True,
                        "CMSS13 quiver can_hold includes /obj/item/weapon/gun/bow.");
                    Assert.That(
                        storageSystem.CanInsert(quiver, arrow, null, out _, storage),
                        Is.True,
                        "CMSS13 quiver can_hold includes /obj/item/arrow.");
                    Assert.That(
                        storageSystem.CanInsert(quiver, crowbar, null, out _, storage),
                        Is.False,
                        "CMSS13 quiver can_hold excludes non-bow, non-arrow items.");
                });
            }
            finally
            {
                foreach (var uid in new[] { quiver, bow, arrow, crowbar })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BowArrowQuiverItemsAreUnacidableLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sourceUnacidableItems = new[]
            {
                "CMUYautjaHuntingBow",
                "CMUYautjaArrow",
                "CMUYautjaExplosiveArrowActive",
                "CMUYautjaEmpArrow",
                "CMUYautjaEmpArrowActive",
                "CMUYautjaDynamicArrow",
                "CMUYautjaSnareArrow",
                "CMUYautjaQuiverStrap",
            };

            var spawned = sourceUnacidableItems
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var item in spawned)
                    {
                        var id = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID;
                        Assert.That(
                            entMan.TryGetComponent<CorrodibleComponent>(item, out var corrodible),
                            Is.True,
                            $"{id} should map CMSS13 unacidable = TRUE to a local Corrodible component.");
                        Assert.That(corrodible!.IsCorrodible, Is.False, $"{id} should not be acid-corrodible.");
                    }
                });
            }
            finally
            {
                foreach (var item in spawned)
                {
                    if (!entMan.Deleted(item))
                        entMan.DeleteEntity(item);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BowArrowQuiverItemsAreExplosionProofLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var sourceExplosionProofItems = new[]
            {
                "CMUYautjaHuntingBow",
                "CMUYautjaArrow",
                "CMUYautjaExplosiveArrowActive",
                "CMUYautjaEmpArrow",
                "CMUYautjaEmpArrowActive",
                "CMUYautjaDynamicArrow",
                "CMUYautjaSnareArrow",
                "CMUYautjaQuiverStrap",
            };

            var spawned = sourceExplosionProofItems
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var item in spawned)
                    {
                        var id = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID;
                        Assert.That(
                            entMan.TryGetComponent<ExplosionResistanceComponent>(item, out var resistance),
                            Is.True,
                            $"{id} should map CMSS13 explo_proof = TRUE to local explosion resistance.");
                        Assert.That(resistance!.Worn, Is.False, $"{id} should protect the item itself, not its wearer.");

                        var ev = new GetExplosionResistanceEvent("RMC");
                        entMan.EventBus.RaiseLocalEvent(item, ref ev);

                        Assert.That(
                            ev.DamageCoefficient,
                            Is.Zero,
                            $"{id} should take no local explosion damage like CMSS13 explo_proof = TRUE.");
                    }
                });
            }
            finally
            {
                foreach (var item in spawned)
                {
                    if (!entMan.Deleted(item))
                        entMan.DeleteEntity(item);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowEquipSlotMatchesCmss13BackFlag()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var clothing = entMan.GetComponent<ClothingComponent>(bow);
                Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK));
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowItemSizeMatchesCmss13LargeWClass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var item = entMan.GetComponent<ItemComponent>(bow);
                Assert.That(item.Size, Is.EqualTo("Large"));
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaArrowItemSizesMatchCmss13SmallWClass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrows = new[]
            {
                "CMUYautjaArrow",
                "CMUYautjaExplosiveArrowActive",
                "CMUYautjaEmpArrow",
                "CMUYautjaEmpArrowActive",
                "CMUYautjaDynamicArrow",
                "CMUYautjaSnareArrow",
            };
            var spawned = arrows
                .Select(id => (Id: id, Uid: entMan.SpawnEntity(id, MapCoordinates.Nullspace)))
                .ToArray();

            try
            {
                foreach (var (id, uid) in spawned)
                {
                    var item = entMan.GetComponent<ItemComponent>(uid);
                    Assert.That(item.Size, Is.EqualTo("Small"),
                        $"{id} should map CMSS13 /obj/item/arrow w_class = SIZE_SMALL.");
                }
            }
            finally
            {
                foreach (var (_, uid) in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowCanPointBlankLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                Assert.That(
                    entMan.HasComponent<GunPointBlankComponent>(bow),
                    Is.True,
                    "CMSS13 /obj/item/weapon/gun/bow has GUN_CAN_POINTBLANK.");
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowScatterMatchesCmss13Zero()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var gun = entMan.GetComponent<GunComponent>(bow);

                Assert.Multiple(() =>
                {
                    Assert.That(gun.MinAngle.Degrees, Is.EqualTo(0),
                        "CMSS13 /obj/item/weapon/gun/bow/set_gun_config_values() sets scatter = 0.");
                    Assert.That(gun.MaxAngle.Degrees, Is.EqualTo(0),
                        "CMSS13 /obj/item/weapon/gun/bow/set_gun_config_values() sets scatter = 0.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowRequiresWieldedFiringLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<WieldableComponent>(bow), Is.True,
                        "CMSS13 /obj/item/weapon/gun/bow flags_item includes TWOHANDED.");
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(bow), Is.True,
                        "CMSS13 /obj/item/weapon/gun/bow has GUN_WIELDED_FIRING_ONLY.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowUsesEmptyInternalArrowSlotLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);

            try
            {
                var provider = entMan.GetComponent<ContainerAmmoProviderComponent>(bow);
                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                var slot = itemSlots.Slots["projectiles"];

                Assert.Multiple(() =>
                {
                    Assert.That(provider.Container, Is.EqualTo("projectiles"),
                        "CMSS13 /obj/item/weapon/gun/bow has GUN_INTERNAL_MAG and current_mag = /obj/item/ammo_magazine/internal/bow.");
                    Assert.That(slot.StartingItem, Is.Null,
                        "CMSS13 /obj/item/weapon/gun/bow/Initialize() forces spawn_empty = TRUE.");
                    Assert.That(slot.HasItem, Is.False,
                        "CMSS13 /obj/item/weapon/gun/bow/Initialize() forces spawn_empty = TRUE.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowArrowsUseNoMuzzleFlashLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrows = new Dictionary<string, EntityUid>
            {
                ["CMUYautjaArrow"] = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace),
                ["CMUYautjaExplosiveArrowActive"] = entMan.SpawnEntity("CMUYautjaExplosiveArrowActive", MapCoordinates.Nullspace),
                ["CMUYautjaEmpArrow"] = entMan.SpawnEntity("CMUYautjaEmpArrow", MapCoordinates.Nullspace),
                ["CMUYautjaEmpArrowActive"] = entMan.SpawnEntity("CMUYautjaEmpArrowActive", MapCoordinates.Nullspace),
                ["CMUYautjaDynamicArrow"] = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace),
                ["CMUYautjaSnareArrow"] = entMan.SpawnEntity("CMUYautjaSnareArrow", MapCoordinates.Nullspace),
            };

            try
            {
                foreach (var (id, arrow) in arrows)
                {
                    Assert.That(
                        entMan.GetComponent<CartridgeAmmoComponent>(arrow).MuzzleFlash,
                        Is.Null,
                        $"{id} should fire without muzzle flash, matching CMSS13 /obj/item/weapon/gun/bow muzzle_flash = null.");
                }
            }
            finally
            {
                foreach (var arrow in arrows.Values)
                {
                    if (!entMan.Deleted(arrow))
                        entMan.DeleteEntity(arrow);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuiverStrapEquipSlotsMatchCmss13Flags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var quiver = entMan.SpawnEntity("CMUYautjaQuiverStrap", MapCoordinates.Nullspace);

            try
            {
                var clothing = entMan.GetComponent<ClothingComponent>(quiver);
                Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BELT | SlotFlags.SUITSTORAGE | SlotFlags.BACK));
            }
            finally
            {
                if (!entMan.Deleted(quiver))
                    entMan.DeleteEntity(quiver);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QuiverStrapMaxItemSizeMatchesCmss13LargeWClass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var quiver = entMan.SpawnEntity("CMUYautjaQuiverStrap", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(quiver);
                Assert.That(storage.MaxItemSize, Is.EqualTo("Large"));
            }
            finally
            {
                if (!entMan.Deleted(quiver))
                    entMan.DeleteEntity(quiver);
            }
        });

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
    public async Task HuntingBowNotHeldDeniedPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You need to hold hunting bow in your hand in order to nock inert arrow!"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowNotHeldCheckPrecedesStrengthLikeCmss13Attackby()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid nonYautja = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                nonYautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonYautja);
                server.PlayerMan.SetAttachedEntity(session, nonYautja);

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, nonYautja), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You need to hold hunting bow in your hand in order to nock inert arrow!"));
                    Assert.That(labels, Does.Not.Contain("You're not nearly strong enough to pull back hunting bow's drawstring!"));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, nonYautja })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowNonYautjaDeniedPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid nonYautja = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                nonYautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonYautja);
                server.PlayerMan.SetAttachedEntity(session, nonYautja);

                Assert.That(hands.TryPickupAnyHand(nonYautja, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, nonYautja), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You're not nearly strong enough to pull back hunting bow's drawstring!"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, nonYautja })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowNonArrowDeniedPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid crowbar = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                crowbar = entMan.SpawnEntity("Crowbar", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, crowbar), Is.True);

                var interact = new InteractUsingEvent(
                    hunter,
                    crowbar,
                    bow,
                    entMan.GetComponent<TransformComponent>(bow).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bow, interact);

                Assert.That(interact.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("That's not an arrow!"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, crowbar, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowAlreadyLoadedDeniedPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid loadedArrow = default;
        EntityUid extraArrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                loadedArrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);
                extraArrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", loadedArrow, hunter), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, extraArrow), Is.True);

                var interact = new InteractUsingEvent(
                    hunter,
                    extraArrow,
                    bow,
                    entMan.GetComponent<TransformComponent>(bow).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bow, interact);

                Assert.That(interact.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("hunting bow is already loaded!"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, loadedArrow, extraArrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
    public async Task HuntingBowLoadedVisualStateTracksCmss13LoadedIconWarhead()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitPost(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var rsiPath = new ResPath("/Textures/_CMU14/Yautja/bow.rsi");

            var prototype = prototypes.Index<EntityPrototype>("CMUYautjaHuntingBow");
            Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(rsiPath),
                "The Yautja hunting bow should use the imported CMSS13 bow.dmi RSI instead of the generic local bow art.");

            Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True,
                "CMSS13 bow.dmi literal loaded states should be imported as a local Yautja bow RSI.");

            foreach (var state in new[]
                     {
                         "unwielded",
                         "wielded",
                         "bow_loaded",
                         "bow_expl",
                         "bow_emp",
                         "bow_trap",
                         "arrow_trap",
                         "arrow_trap_active",
                     })
            {
                Assert.That(resource!.RSI.TryGetState(state, out _), Is.True,
                    $"CMSS13 /obj/item/weapon/gun/bow/update_icon() can select literal {state} visuals.");
            }

            var snarePrototype = prototypes.Index<EntityPrototype>("CMUYautjaSnareArrow");
            Assert.That(snarePrototype.TryGetComponent<SpriteComponent>(out var snareSprite, factory), Is.True);
            var snareTrapLayer = snareSprite!.AllLayers.Single(layer => layer.RsiState.Name == "arrow_trap_active");
            Assert.That(snareTrapLayer.Rsi?.Path, Is.EqualTo(rsiPath),
                "CMSS13 snare arrows should use the literal bow.dmi trap-active state instead of the generic local hunting-trap overlay.");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var slots = entMan.System<ItemSlotsSystem>();
            var appearance = entMan.System<SharedAppearanceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);
            var spawned = new List<EntityUid> { hunter, bow };

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);

                AssertBowLoadedIcon(appearance, bow, "none",
                    "CMSS13 /obj/item/weapon/gun/bow/update_icon() returns to item_state = bow when no arrow ammo is loaded.");

                AssertLoadedBowVisual("CMUYautjaArrow", "loaded", YautjaArrowWarhead.Standard, false);
                AssertLoadedBowVisual("CMUYautjaExplosiveArrowActive", "expl", YautjaArrowWarhead.Explosive, true);
                AssertLoadedBowVisual("CMUYautjaEmpArrowActive", "emp", YautjaArrowWarhead.Emp, true);
                AssertLoadedBowVisual("CMUYautjaSnareArrow", "trap", YautjaArrowWarhead.Snare, true);
                AssertDynamicLoadedBowVisual(YautjaArrowWarhead.Explosive, "expl");
                AssertDynamicLoadedBowVisual(YautjaArrowWarhead.Emp, "emp");
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }

            void AssertLoadedBowVisual(string arrowPrototype, string expectedLoadedIcon, YautjaArrowWarhead expectedWarhead, bool expectedActivated)
            {
                var arrow = entMan.SpawnEntity(arrowPrototype, MapCoordinates.Nullspace);
                spawned.Add(arrow);

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);
                AssertBowLoadedIcon(appearance, bow, expectedLoadedIcon,
                    $"CMSS13 bow update_icon() uses icon_state/item_state bow_[arrow.loaded_icon] for {arrowPrototype}.");
                AssertArrowState(entMan, arrow, expectedWarhead, expectedActivated);

                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                Assert.That(slots.TryEjectToHands(bow, itemSlots.Slots["projectiles"], hunter), Is.True);
                AssertBowLoadedIcon(appearance, bow, "none",
                    "CMSS13 bow unload() calls update_icon(), returning an unloaded bow to item_state = bow.");
                AssertArrowState(entMan, arrow, expectedWarhead, expectedActivated);
            }

            void AssertDynamicLoadedBowVisual(YautjaArrowWarhead warhead, string expectedLoadedIcon)
            {
                var arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
                spawned.Add(arrow);

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), warhead));

                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);
                AssertBowLoadedIcon(appearance, bow, expectedLoadedIcon,
                    $"CMSS13 dynamic arrows use their selected warhead ammo datum loaded_icon while loaded in the bow.");
                AssertArrowState(entMan, arrow, warhead, true);

                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                Assert.That(slots.TryEjectToHands(bow, itemSlots.Slots["projectiles"], hunter), Is.True);
                AssertBowLoadedIcon(appearance, bow, "none",
                    "CMSS13 bow unload() resets loaded bow visuals after dynamic arrows too.");
                AssertArrowState(entMan, arrow, warhead, true);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowNockPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You nock inert arrow onto hunting bow."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingBowUnloadPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);

                var itemSlots = entMan.GetComponent<ItemSlotsComponent>(bow);
                Assert.That(slots.TryEjectToHands(bow, itemSlots.Slots["projectiles"], hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You unload inert arrow from hunting bow."));
                    Assert.That(labels, Does.Not.Contain("The projectile falls out of hunting bow!"));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppingLoadedHuntingBowWarnsUserLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bow = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var slots = entMan.System<ItemSlotsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bow = entMan.SpawnEntity("CMUYautjaHuntingBow", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(hands.TryPickupAnyHand(hunter, bow), Is.True);
                Assert.That(slots.TryInsert(bow, "projectiles", arrow, hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var hands = server.EntMan.System<SharedHandsSystem>();
                Assert.That(hands.TryDrop(hunter, bow), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("The projectile falls out of hunting bow!"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { bow, arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMedicalStorageRuntimeCanHoldMatchesCmss13SourceLists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storageSystem = entMan.System<SharedStorageSystem>();
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string id)
            {
                var uid = entMan.SpawnEntity(id, MapCoordinates.Nullspace);
                spawned.Add(uid);
                return uid;
            }

            try
            {
                var medicomp = Spawn("CMUYautjaMedicomp");
                var medicompStorage = entMan.GetComponent<StorageComponent>(medicomp);

                foreach (var allowed in new[]
                         {
                             "CMUYautjaStabilizerGel",
                             "CMUYautjaHealingGun",
                             "CMUYautjaWoundClamp",
                             "CMUYautjaAutoInjector",
                             "CMUYautjaThrallAutoInjector",
                             "CMUYautjaAlienHealthAnalyzer",
                             "CMUYautjaHealingGel",
                             "CMUYautjaHerbalCase",
                         })
                {
                    var item = Spawn(allowed);
                    Assert.That(
                        storageSystem.CanInsert(medicomp, item, null, out _, medicompStorage),
                        Is.True,
                        $"CMSS13 /obj/item/storage/medicomp can_hold includes {allowed}'s source type.");
                }

                foreach (var rejected in new[]
                         {
                             "CMUYautjaCleanserGelVial",
                             "CMUYautjaScrewdriver",
                             "CMTraumaKit10",
                             "CMBurnKit10",
                         })
                {
                    var item = Spawn(rejected);
                    Assert.That(
                        storageSystem.CanInsert(medicomp, item, null, out _, medicompStorage),
                        Is.False,
                        $"CMSS13 /obj/item/storage/medicomp rejects unrelated {rejected}.");
                }

                var filledMedicomp = Spawn("CMUYautjaMedicomp");
                var filledMedicompStorage = entMan.GetComponent<StorageComponent>(filledMedicomp);
                for (var i = 0; i < 12; i++)
                {
                    var analyzer = Spawn("CMUYautjaAlienHealthAnalyzer");
                    Assert.That(
                        storageSystem.Insert(filledMedicomp, analyzer, out _, storageComp: filledMedicompStorage, playSound: false),
                        Is.True,
                        "CMSS13 /obj/item/storage/medicomp storage_slots = 12.");
                }

                var thirteenthAnalyzer = Spawn("CMUYautjaAlienHealthAnalyzer");
                Assert.That(
                    storageSystem.CanInsert(filledMedicomp, thirteenthAnalyzer, null, out _, filledMedicompStorage),
                    Is.False,
                    "CMSS13 /obj/item/storage/medicomp rejects a thirteenth allowed item.");

                var herbalCase = Spawn("CMUYautjaHerbalCase");
                var herbalStorage = entMan.GetComponent<StorageComponent>(herbalCase);

                foreach (var allowed in new[] { "CMUYautjaAdvancedBruisePack", "CMUYautjaAdvancedOintment" })
                {
                    var item = Spawn(allowed);
                    Assert.That(
                        storageSystem.CanInsert(herbalCase, item, null, out _, herbalStorage),
                        Is.True,
                        $"CMSS13 /obj/item/storage/herbal_case can_hold includes only predator advanced medicine; {allowed} should fit.");
                }

                foreach (var rejected in new[] { "CMTraumaKit10", "CMBurnKit10", "CMOintment10" })
                {
                    var item = Spawn(rejected);
                    Assert.That(
                        storageSystem.CanInsert(herbalCase, item, null, out _, herbalStorage),
                        Is.False,
                        $"CMSS13 /obj/item/storage/herbal_case rejects generic marine medicine {rejected}.");
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaArrowWarheadTogglesForYautjaTechUsers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var techUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techUser);
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(nonYautja));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(techUser));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaExplosiveArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("activated explosive arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(techUser));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaExplosiveArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("activated explosive arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));
                });
            }
            finally
            {
                foreach (var uid in new[] { arrow, nonYautja, techUser, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpArrowWarheadToggleUsesCmss13RuntimeName()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaEmpArrow", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaEmpArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("activated EMP arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert arrow"));
                });
            }
            finally
            {
                foreach (var uid in new[] { arrow, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnareArrowWarheadUseIsNoOpLikeCmss13ChangeWarhead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaSnareArrow", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaSnareArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("snare arrow"));
                });

                var nonYautjaUse = new UseInHandEvent(nonYautja);
                entMan.EventBus.RaiseLocalEvent(arrow, nonYautjaUse);
                Assert.Multiple(() =>
                {
                    Assert.That(nonYautjaUse.Handled, Is.False,
                        "CMSS13 /obj/item/arrow/snare/change_warhead() returns without the base Yautja-tech denial path.");
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaSnareArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("snare arrow"));
                });

                var yautjaUse = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(arrow, yautjaUse);
                Assert.Multiple(() =>
                {
                    Assert.That(yautjaUse.Handled, Is.False,
                        "CMSS13 /obj/item/arrow/snare/change_warhead() is a no-op for Yautja users too.");
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaSnareArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("snare arrow"));
                });
            }
            finally
            {
                foreach (var uid in new[] { arrow, nonYautja, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaArrowWarheadTogglePopupsUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You activate activated explosive arrow."));
            });

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You deactivate inert arrow."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaArrowWarheadDeniedPopupUsesCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaArrow", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, human);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(human));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You attempt to tweak inert arrow, but nothing happens."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { arrow, human })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicArrowWarheadSelectionAndDeactivationMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var techUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techUser);
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                Assert.That(meta.EntityName, Is.EqualTo("inert dynamic arrow"));

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(nonYautja), YautjaArrowWarhead.Explosive));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert dynamic arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(techUser), YautjaArrowWarhead.Explosive));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaExplosiveArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("explosive dynamic arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(techUser));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert dynamic arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Explosive));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaExplosiveArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("explosive dynamic arrow"));
                });

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert dynamic arrow"));
                });
            }
            finally
            {
                foreach (var uid in new[] { arrow, nonYautja, techUser, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicEmpArrowWarheadSelectionUsesCmss13RuntimeName()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var meta = entMan.GetComponent<MetaDataComponent>(arrow);
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Emp));

                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaEmpArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("EMP dynamic arrow"));
                });
            }
            finally
            {
                foreach (var uid in new[] { arrow, hunter })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicArrowWarheadDialogUsesCmss13TguiText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));

                Assert.That(entMan.TryGetComponent(arrow, out DialogComponent dialog), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Options));
                    Assert.That(dialog.Title, Is.EqualTo("Pick Warhead"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Which warhead do you wish to use?"));
                    Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Explosive", "EMP" }));
                    Assert.That(dialog.Options, Has.All.Matches<DialogOption>(option => option.Event is YautjaArrowWarheadSelectedEvent));
                });
            }
            finally
            {
                if (!entMan.Deleted(arrow))
                    entMan.DeleteEntity(arrow);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicArrowWarheadSelectionPopupUsesCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Explosive));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You change the warhead to Explosive."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicArrowWarheadDeactivationPopupUsesCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Explosive));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(arrow, new UseInHandEvent(hunter));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("You deactivate inert dynamic arrow."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DynamicArrowInvalidWarheadWarnsAndRemainsInertLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid arrow = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                arrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(
                    arrow,
                    new YautjaArrowWarheadSelectedEvent(entMan.GetNetEntity(hunter), YautjaArrowWarhead.Snare));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
                var meta = entMan.GetComponent<MetaDataComponent>(arrow);

                Assert.Multiple(() =>
                {
                    Assert.That(cartridge.Prototype.ToString(), Is.EqualTo("CMUYautjaArrowProjectile"));
                    Assert.That(meta.EntityName, Is.EqualTo("inert dynamic arrow"));
                });
            });

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                Assert.That(
                    popups.WorldLabels.Select(label => label.Text),
                    Does.Contain("There was an error with the warhead. Arrow remains inert."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { arrow, hunter })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackOffersCmss13BowAndSpareArrows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var ids = vendor.Sections
                    .SelectMany(section => section.Entries)
                    .Select(entry => entry.Id.Id)
                    .ToHashSet();

                Assert.That(ids, Does.Contain("CMUYautjaQuiverStrapFilled"));
                Assert.That(ids, Does.Contain("CMUYautjaArrow"));
                Assert.That(ids, Does.Contain("CMUYautjaSnareArrow"));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodRackOffersCmss13DynamicBowQuiver()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind.ToString(), Is.EqualTo("BadBlood"));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var ids = ranged.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ids, Is.EqualTo(new[]
                {
                    "CMUYautjaQuiverStrapDynamic",
                }), "CMSS13 badblood rack maps The Firm Bow to /obj/item/storage/belt/gun/quiver/dynamic.");

                AssertVendorRow(ranged, "CMUYautjaQuiverStrapDynamic", "The Firm Bow");
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodRackSpareArrowRowsUseCmss13DynamicWarhead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var arrowRows = spare.Entries
                    .Where(entry => entry.Name?.StartsWith("Arrow - ") == true)
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();

                Assert.That(spare.Choices, Is.Null);
                Assert.That(arrowRows, Is.EqualTo(new[]
                {
                    ("CMUYautjaDynamicArrow", "Arrow - Dynamic Warhead", (int?) 10, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 15, (int?) null),
                }));

                Assert.That(spare.Entries.Select(entry => entry.Id.Id), Does.Not.Contain("CMUYautjaArrow"),
                    "CMSS13 badblood spare equipment replaces the regular explosive spare arrow with a dynamic-warhead arrow.");
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodAndStrandedRackMainWeaponRowsUseCmss13DisplayNamesAndRecommendedFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var racks = new[]
            {
                entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", MapCoordinates.Nullspace),
            };

            try
            {
                var expectedRows = new (string Id, string Name)[]
                {
                    ("CMUYautjaClanSword", "The Primary Hunting Sword"),
                    ("CMUYautjaRendingSword", "The Rending Hunting Sword"),
                    ("CMUYautjaPiercingSword", "The Piercing Hunting Sword"),
                    ("CMUYautjaSeveringSword", "The Severing Hunting Sword"),
                    ("CMUYautjaCruelStaff", "The Taruulan Staff"),
                    ("CMUYautjaChainwhip", "The Sundering Chain-Whip"),
                    ("CMUYautjaDualWarScythe", "The Cleaving War-Scythe"),
                    ("CMUYautjaDoubleWarScythe", "The Ripping War-Scythe"),
                    ("CMUYautjaCombistick", "The Adaptive Combi-Stick"),
                    ("CMUYautjaWarAxe", "The Butchering War Axe"),
                    ("CMUYautjaWarGlaive", "The Lumbering Glaive"),
                    ("CMUYautjaCleavingGlaive", "The Imposing Glaive"),
                    ("CMUYautjaLongaxe", "The Crushing Longaxe"),
                };

                Assert.Multiple(() =>
                {
                    foreach (var rack in racks)
                    {
                        var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                        var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");

                        foreach (var row in expectedRows)
                            AssertVendorRow(main, row.Id, row.Name, recommended: true);
                    }
                });
            }
            finally
            {
                foreach (var rack in racks)
                {
                    if (!entMan.Deleted(rack))
                        entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(Cmss13RoleRackSectionRows))]
    public async Task YautjaRoleRackSectionRowsAndForbiddenRowsMatchCmss13Source(
        string rackPrototype,
        string sourceListName,
        string[] expectedSectionNames,
        RackSectionRows[] expectedRows,
        ForbiddenRackRow[] forbiddenRows)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity(rackPrototype, MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);

                Assert.That(
                    vendor.Sections.Select(section => section.Name).ToArray(),
                    Is.EqualTo(expectedSectionNames),
                    $"{rackPrototype} should mirror {sourceListName} section order and omit source-absent sections.");

                foreach (var expectedSection in expectedRows)
                {
                    var section = vendor.Sections.Single(section => section.Name == expectedSection.Section);
                    Assert.That(
                        section.Entries.Select(entry => entry.Id.Id).ToArray(),
                        Is.EqualTo(expectedSection.Rows.Select(row => row.Id).ToArray()),
                        $"{rackPrototype} {expectedSection.Section} should mirror {sourceListName} row ids.");

                    foreach (var expectedRow in expectedSection.Rows)
                    {
                        var entry = section.Entries.Single(entry => entry.Id.Id == expectedRow.Id);
                        Assert.That(
                            entry.Name,
                            Is.EqualTo(expectedRow.Name),
                            $"{rackPrototype} {expectedSection.Section} / {expectedRow.Id} should use the CMSS13 display row name.");
                    }
                }

                foreach (var forbiddenRow in forbiddenRows)
                {
                    var entries = forbiddenRow.Section is { } sectionName
                        ? vendor.Sections.Where(section => section.Name == sectionName).SelectMany(section => section.Entries)
                        : vendor.Sections.SelectMany(section => section.Entries);
                    var entriesArray = entries.ToArray();
                    var where = forbiddenRow.Section ?? "any section";

                    Assert.That(
                        entriesArray.Any(entry => entry.Id.Id == forbiddenRow.Id),
                        Is.False,
                        $"{rackPrototype} should not expose {forbiddenRow.Id} in {where}; {sourceListName} has no such row.");

                    if (forbiddenRow.Name != null)
                    {
                        Assert.That(
                            entriesArray.Any(entry => entry.Name == forbiddenRow.Name),
                            Is.False,
                            $"{rackPrototype} should not expose display row '{forbiddenRow.Name}' in {where}; {sourceListName} has no such row.");
                    }
                }
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(Cmss13RackMachineryRows))]
    public async Task YautjaGearRackMachineryFamilyMatchesCmss13SourceTypes(
        Cmss13RackMachineryRow row)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var prototype = prototypes.Index<EntityPrototype>(row.Prototype);

            Assert.Multiple(() =>
            {
                Assert.That(prototype.Name, Is.EqualTo(row.SourceName), $"{row.SourceType} name");
                Assert.That(prototype.Description, Is.EqualTo(row.SourceDescription), $"{row.SourceType} description");
                Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.SourceType} sprite");
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_vendor.rsi")),
                    $"{row.SourceType} imports CMSS13 pred_vendor.dmi");
                Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(row.InitialIconState),
                    $"{row.SourceType} initial icon_state");
                Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, $"{row.SourceType} icon");
                var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                Assert.That(rsiIcon.RsiState, Is.EqualTo(row.InitialIconState), $"{row.SourceType} icon state");
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity(row.Prototype, MapCoordinates.Nullspace);

            try
            {
                var gearRack = entMan.GetComponent<YautjaGearRackComponent>(rack);
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var entryIds = VendorEntryIds(vendor);

                Assert.Multiple(() =>
                {
                    Assert.That(gearRack.Kind, Is.EqualTo(row.Kind), $"{row.SourceType} local rack kind");
                    Assert.That(vendor.UiStyle, Is.EqualTo(CMVendorUiStyle.Yautja), $"{row.SourceType} VENDOR_THEME_YAUTJA");

                    foreach (var expected in row.ExpectedListRows)
                    {
                        Assert.That(entryIds, Does.Contain(expected),
                            $"{row.SourceType} get_listed_products() should mirror {row.SourceListName}");
                    }

                    foreach (var forbidden in row.ForbiddenListRows)
                    {
                        Assert.That(entryIds, Does.Not.Contain(forbidden),
                            $"{row.SourceType} should not expose rows from another CMSS13 source list");
                    }
                });
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElderYautjaGearRacksAutoConnectToCmss13ElderLeftRightStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid left = default;
        EntityUid right = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));

            left = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", map.GridCoords);
            right = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", map.GridCoords.Offset(new Vector2(1, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(left, YautjaGearRackVisuals.State, out var leftState), Is.True);
                Assert.That(leftState, Is.EqualTo(YautjaGearRackVisualState.Left));
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(right, YautjaGearRackVisuals.State, out var rightState), Is.True);
                Assert.That(rightState, Is.EqualTo(YautjaGearRackVisualState.Right));
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(left))
                entMan.DeleteEntity(left);
            if (!entMan.Deleted(right))
                entMan.DeleteEntity(right);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllYautjaRacksUseOnlyCmss13ClaimCategories()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var racks = Cmss13RackPrototypeIds()
                .Select(prototype => entMan.SpawnEntity(prototype, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                var expectedChoices = new Dictionary<string, int>
                {
                    ["CMUYautjaEssentials"] = 1,
                    ["CMUYautjaArmor"] = 1,
                    ["CMUYautjaPrimary"] = 1,
                    ["CMUYautjaBracer"] = 1,
                    ["CMUYautjaSupport"] = 2,
                    ["CMUYautjaRanged"] = 1,
                    ["CMUYautjaAccessory"] = 1,
                };

                Assert.Multiple(() =>
                {
                    foreach (var rack in racks)
                    {
                        var rackPrototype = entMan.GetComponent<MetaDataComponent>(rack).EntityPrototype?.ID ?? rack.ToString();
                        var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);

                        foreach (var section in vendor.Sections)
                        {
                            if (section.Choices is { } sectionChoices)
                                AssertCmss13VendorChoice(rackPrototype, section.Name, sectionChoices.Id, sectionChoices.Amount, expectedChoices);

                            foreach (var entry in section.Entries)
                            {
                                if (entry.Choices is not { } entryChoices)
                                    continue;

                                AssertCmss13VendorChoice(
                                    rackPrototype,
                                    $"{section.Name} / {entry.Id.Id}",
                                    entryChoices.Id,
                                    entryChoices.Amount,
                                    expectedChoices);
                            }
                        }
                    }
                });
            }
            finally
            {
                foreach (var rack in racks)
                {
                    if (!entMan.Deleted(rack))
                        entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackDeniesClaimCategoryOutsideCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var mainIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(mainIndex, Is.GreaterThanOrEqualTo(0));

                var main = vendor.Sections[mainIndex];
                var swordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                Assert.That(swordIndex, Is.GreaterThanOrEqualTo(0));

                const string localOnlyCategory = "CMULocalOnlyPredBuyCategory";
                main.Entries[swordIndex].Choices = (localOnlyCategory, 1);

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                var swordsBefore = EntityPrototypeIds(entMan, "CMUYautjaClanSword").Count();

                Vend(entMan, rack, hunter, mainIndex, swordIndex);

                Assert.That(user.Choices.GetValueOrDefault(localOnlyCategory), Is.Zero,
                    "CMSS13 handle_vend() rejects buying_category values that are not present in vendor_buyable_categories.");
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaClanSword").Count(), Is.EqualTo(swordsBefore),
                    "A local-only claim category must deny before spawning the vend item.");
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
    public async Task BadBloodRackClaimGroupsMatchCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind.ToString(), Is.EqualTo("BadBlood"));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Armor Set",
                    "Main Weapons (CHOOSE 1)",
                    "Bracer Attachments",
                    "Support Equipment (CHOOSE 2)",
                    "Ranged Equipment (CHOOSE 1)",
                    "Clothing Accessory (CHOOSE 1)",
                    "Spare Equipment",
                }));

                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                Assert.That(essentials.Choices, Is.Null);
                Assert.That(essentials.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaBadBloodHuntingEquipmentBundle",
                }));
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaBadBloodHuntingEquipmentBundle"), "CMUYautjaEssentials", 1);
                AssertBundle(prototypes, entMan, "CMUYautjaBadBloodHuntingEquipmentBundle", new[]
                {
                    "CMUYautjaBodyMeshScalable",
                    "CMUYautjaHuntingPouch",
                    "CMUYautjaMedicompSurvivor",
                    "CMUYautjaCleanserGelVial",
                    "CMUYautjaHivebreaker",
                });

                var armorBundles = new (string Id, string Name, string[] Bundle)[]
                {
                    ("CMUYautjaBadBloodArmorPatchworkBundle", "Patchwork Armor", new[] { "CMUYautjaBadBloodArmorPatchwork", "CMUYautjaMaskBadBloodPatchwork", "CMUYautjaBadBloodGreavesPatchwork" }),
                    ("CMUYautjaBadBloodArmorPatchworkAltBundle", "Patchwork Armor (Alt)", new[] { "CMUYautjaBadBloodArmorPatchworkAlt", "CMUYautjaMaskBadBloodPatchworkAlt", "CMUYautjaBadBloodGreavesPatchworkAlt" }),
                    ("CMUYautjaBadBloodArmorLunaticBundle", "Lunatic Armor", new[] { "CMUYautjaBadBloodArmorLunatic", "CMUYautjaMaskBadBloodLunatic", "CMUYautjaBadBloodGreavesLunatic" }),
                    ("CMUYautjaBadBloodArmorScavengerBundle", "Scavenger Armor", new[] { "CMUYautjaBadBloodArmorScavenger", "CMUYautjaMaskBadBloodScav", "CMUYautjaBadBloodGreavesScavenger" }),
                    ("CMUYautjaBadBloodArmorScavengerAltBundle", "Scavenger Armor (Alt)", new[] { "CMUYautjaBadBloodArmorScavengerAlt", "CMUYautjaMaskBadBloodScavAlt", "CMUYautjaBadBloodGreavesScavengerAlt" }),
                    ("CMUYautjaBadBloodArmorVenatorBundle", "Venator Armor", new[] { "CMUYautjaBadBloodArmorVenator", "CMUYautjaMaskBadBloodVenator", "CMUYautjaBadBloodGreavesVenator" }),
                    ("CMUYautjaBadBloodArmorCommandoBundle", "Commando Armor", new[] { "CMUYautjaBadBloodArmorCommando", "CMUYautjaMaskBadBloodCommando", "CMUYautjaBadBloodGreavesCommando" }),
                    ("CMUYautjaBadBloodArmorCommandoAltBundle", "Commando Armor (Alt)", new[] { "CMUYautjaBadBloodArmorCommandoAlt", "CMUYautjaMaskBadBloodCommandoAlt", "CMUYautjaBadBloodGreavesCommandoAlt" }),
                    ("CMUYautjaBadBloodArmorEmissaryBundle", "Emissary Armor", new[] { "CMUYautjaEmissaryArmorCamoConforming", "CMUYautjaMaskBadBloodEmissaryClassic", "CMUYautjaEmissaryGreavesCamoConforming" }),
                };
                var armor = vendor.Sections.Single(section => section.Name == "Armor Set");
                Assert.That(armor.Choices, Is.Not.Null);
                Assert.That(armor.Choices!.Value.Id, Is.EqualTo("CMUYautjaArmor"));
                Assert.That(armor.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(armor.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(armorBundles.Select(row => row.Id).ToArray()));
                Assert.That(armor.Entries.Select(entry => entry.Id.Id), Does.Not.Contain("CMUYautjaBadBloodArmorBaneBundle"),
                    "CMSS13 keeps Bane Armor commented out in cm_vending_equipment_badblood.");

                foreach (var row in armorBundles)
                {
                    AssertVendorRow(armor, row.Id, row.Name);
                    AssertBundle(prototypes, entMan, row.Id, row.Bundle);
                }

                var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(main.Choices, Is.Not.Null);
                Assert.That(main.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(main.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(main.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
                    "CMUYautjaCruelStaff",
                    "CMUYautjaChainwhip",
                    "CMUYautjaDualWarScythe",
                    "CMUYautjaDoubleWarScythe",
                    "CMUYautjaCombistick",
                    "CMUYautjaWarAxe",
                    "CMUYautjaWarGlaive",
                    "CMUYautjaCleavingGlaive",
                    "CMUYautjaLongaxe",
                }));
                AssertVendorRow(main, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaCruelStaff", "The Taruulan Staff", recommended: true);

                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                Assert.That(bracer.Choices, Is.Null);
                Assert.That(bracer.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaBracerShieldAttachment",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }));
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaWristBladesBundle"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaBracerShieldAttachment"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaSkeweringScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaChainGauntletsBundle"), "CMUYautjaPrimary", 1);

                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                Assert.That(support.Choices, Is.Not.Null);
                Assert.That(support.Choices!.Value.Id, Is.EqualTo("CMUYautjaSupport"));
                Assert.That(support.Choices.Value.Amount, Is.EqualTo(2));
                Assert.That(support.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaFalconDroneBadBlood",
                    "CMUYautjaClanShield",
                    "CMUYautjaHeavyClanArmor",
                    "CMUYautjaSpikeLauncher",
                    "CMUYautjaSmartDisc",
                }));
                AssertVendorRow(support, "CMUYautjaFalconDroneBadBlood", "The Agile Drone");
                AssertVendorRow(support, "CMUYautjaHeavyClanArmor", "The Formidable Plate Armor", replaceSlot: SlotFlags.OUTERCLOTHING);
                AssertVendorRow(support, "CMUYautjaSmartDisc", "The Purifying Smart-Disc");

                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ranged.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaQuiverStrapDynamic",
                }));
                AssertVendorRow(ranged, "CMUYautjaQuiverStrapDynamic", "The Firm Bow");

                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(capes.Choices, Is.Not.Null);
                Assert.That(capes.Choices!.Value.Id, Is.EqualTo("CMUYautjaAccessory"));
                Assert.That(capes.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(capes.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaCapeQuarter",
                    "CMUYautjaCapeThird",
                    "CMUYautjaCapeHalf",
                    "CMUYautjaCapePoncho",
                    "CMUYautjaCapeDamaged",
                    "CMUYautjaCapeFull",
                }));
                AssertVendorRow(capes, "CMUYautjaCapeDamaged", "Damaged-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeFull", "Full-Cape", replaceSlot: SlotFlags.BACK);

                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var spareRows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();
                Assert.That(spare.Choices, Is.Null);
                Assert.That(spareRows, Is.EqualTo(new[]
                {
                    ("CMUYautjaFalconDroneBadBlood", "Falcon Drone", (int?) 20, (int?) null),
                    ("CMUYautjaHuntingTrap", "Hunting Trap", (int?) 10, (int?) null),
                    ("CMUYautjaDynamicArrow", "Arrow - Dynamic Warhead", (int?) 10, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 15, (int?) null),
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodRackUsesCmss13BadBloodFalconRows()
    {
        const string badBloodFalcon = "CMUYautjaFalconDroneBadBlood";
        const string badBloodFalconDeployed = "CMUYautjaFalconDroneBadBloodDeployed";

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);
            EntityUid? falcon = null;
            EntityUid? deployed = null;

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");

                Assert.That(support.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    badBloodFalcon,
                    "CMUYautjaClanShield",
                    "CMUYautjaHeavyClanArmor",
                    "CMUYautjaSpikeLauncher",
                    "CMUYautjaSmartDisc",
                }), "CMSS13 cm_vending_equipment_badblood uses /obj/item/falcon_drone/badblood for The Agile Drone.");
                AssertVendorRow(support, badBloodFalcon, "The Agile Drone");

                var spareRows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();
                Assert.That(spareRows, Is.EqualTo(new[]
                {
                    (badBloodFalcon, "Falcon Drone", (int?) 20, (int?) null),
                    ("CMUYautjaHuntingTrap", "Hunting Trap", (int?) 10, (int?) null),
                    ("CMUYautjaDynamicArrow", "Arrow - Dynamic Warhead", (int?) 10, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 15, (int?) null),
                }), "CMSS13 Bad Blood spare Falcon Drone uses /obj/item/falcon_drone/badblood.");

                Assert.That(prototypes.HasIndex<EntityPrototype>(badBloodFalcon), Is.True,
                    "Bad Blood Falcon item prototype should mirror CMSS13 /obj/item/falcon_drone/badblood.");
                Assert.That(prototypes.HasIndex<EntityPrototype>(badBloodFalconDeployed), Is.True,
                    "Bad Blood Falcon deployed prototype should mirror CMSS13 /mob/hologram/falcon/badblood.");

                var falconPrototype = prototypes.Index<EntityPrototype>(badBloodFalcon);
                var deployedPrototype = prototypes.Index<EntityPrototype>(badBloodFalconDeployed);
                Assert.That(falconPrototype.Name, Is.EqualTo("falcon drone"));
                Assert.That(deployedPrototype.Name, Is.EqualTo("falcon drone"));

                falcon = entMan.SpawnEntity(badBloodFalcon, MapCoordinates.Nullspace);
                var falconComp = entMan.GetComponent<YautjaFalconDroneComponent>(falcon.Value);
                Assert.That(falconComp.DeployedPrototype.Id, Is.EqualTo(badBloodFalconDeployed));

                deployed = entMan.SpawnEntity(badBloodFalconDeployed, MapCoordinates.Nullspace);
                Assert.That(entMan.GetComponent<MetaDataComponent>(deployed.Value).EntityPrototype?.ID, Is.EqualTo(badBloodFalconDeployed));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (falcon is { } falconUid && !entMan.Deleted(falconUid))
                    entMan.DeleteEntity(falconUid);
                if (deployed is { } deployedUid && !entMan.Deleted(deployedUid))
                    entMan.DeleteEntity(deployedUid);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            var falconPrototype = prototypes.Index<EntityPrototype>(badBloodFalcon);
            var deployedPrototype = prototypes.Index<EntityPrototype>(badBloodFalconDeployed);
            Assert.That(falconPrototype.TryGetComponent<SpriteComponent>(out var falconSprite, factory), Is.True);
            Assert.That(falconSprite!.AllLayers.First().RsiState.Name, Is.EqualTo("falcon_drone_badblood"));
            Assert.That(deployedPrototype.TryGetComponent<SpriteComponent>(out var deployedSprite, factory), Is.True);
            Assert.That(deployedSprite!.AllLayers.First().RsiState.Name, Is.EqualTo("falcon_drone_badblood_active"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracerPrimaryHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentialsIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                var armorIndex = vendor.Sections.FindIndex(section => section.Name == "Armor Set");
                var mainIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracerIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                var supportIndex = vendor.Sections.FindIndex(section => section.Name == "Support Equipment (CHOOSE 2)");
                var rangedIndex = vendor.Sections.FindIndex(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var accessoryIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(essentialsIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mainIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bracerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(supportIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(rangedIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(accessoryIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[essentialsIndex];
                var armor = vendor.Sections[armorIndex];
                var main = vendor.Sections[mainIndex];
                var bracer = vendor.Sections[bracerIndex];
                var support = vendor.Sections[supportIndex];
                var ranged = vendor.Sections[rangedIndex];
                var accessory = vendor.Sections[accessoryIndex];

                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaBadBloodHuntingEquipmentBundle");
                var armorRowIndex = armor.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaBadBloodArmorPatchworkBundle");
                var swordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                var wristIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaWristBladesBundle");
                var scimitarIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle");
                var falconIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFalconDroneBadBlood");
                var shieldIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanShield");
                var bowIndex = ranged.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaQuiverStrapDynamic");
                var capeIndex = accessory.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeDamaged");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorRowIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(swordIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(wristIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(scimitarIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(falconIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(shieldIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bowIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(capeIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, essentialsIndex, huntingIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.Zero);

                Vend(entMan, rack, hunter, armorIndex, armorRowIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, mainIndex, swordIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, bracerIndex, wristIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));

                Vend(entMan, rack, bracerPrimaryHunter, bracerIndex, scimitarIndex);
                var bracerPrimaryUser = entMan.GetComponent<CMVendorUserComponent>(bracerPrimaryHunter);
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);

                Vend(entMan, rack, hunter, supportIndex, falconIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, supportIndex, shieldIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(2));

                Vend(entMan, rack, hunter, rangedIndex, bowIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, accessoryIndex, capeIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracerPrimaryHunter))
                    entMan.DeleteEntity(bracerPrimaryHunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElderRackClaimGroupsMatchCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUYautjaElderLoadoutVendor"), Is.True,
                "CMSS13 has a dedicated cm_vending_elder_yautja product list.");

            var rack = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind.ToString(), Is.EqualTo("Elder"));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Main Weapons (CHOOSE 1)",
                    "Bracer Attachments",
                    "Support Equipment (CHOOSE 2)",
                    "Ranged Equipment (CHOOSE 1)",
                    "Clothing Accessory (CHOOSE 1)",
                    "Spare Equipment",
                }));

                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                Assert.That(essentials.Choices, Is.Null);
                Assert.That(essentials.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaHuntingEquipmentBundle",
                    "CMUYautjaArmorBundle",
                }));
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle"), "CMUYautjaEssentials", 1);
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaArmorBundle"), "CMUYautjaArmor", 1);

                var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(main.Choices, Is.Not.Null);
                Assert.That(main.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(main.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(main.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
                    "CMUYautjaCruelStaff",
                    "CMUYautjaChainwhip",
                    "CMUYautjaDualWarScythe",
                    "CMUYautjaDoubleWarScythe",
                    "CMUYautjaCombistick",
                    "CMUYautjaWarAxe",
                    "CMUYautjaWarGlaive",
                    "CMUYautjaCleavingGlaive",
                    "CMUYautjaLongaxe",
                }));
                AssertVendorRow(main, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaCruelStaff", "The Taruulan Staff", recommended: true);

                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                Assert.That(bracer.Choices, Is.Null);
                Assert.That(bracer.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaBracerShieldAttachment",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }));
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaWristBladesBundle"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaBracerShieldAttachment"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaSkeweringScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaChainGauntletsBundle"), "CMUYautjaPrimary", 1);

                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                Assert.That(support.Choices, Is.Not.Null);
                Assert.That(support.Choices!.Value.Id, Is.EqualTo("CMUYautjaSupport"));
                Assert.That(support.Choices.Value.Amount, Is.EqualTo(2));
                Assert.That(support.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaFalconDrone",
                    "CMUYautjaClanShield",
                    "CMUYautjaAncientShield",
                    "CMUYautjaAncientShieldAlt",
                    "CMUYautjaHeavyClanArmor",
                    "CMUYautjaSpikeLauncher",
                    "CMUYautjaSmartDisc",
                }));
                AssertVendorRow(support, "CMUYautjaClanShield", "The Steadfast Shield");
                AssertVendorRow(support, "CMUYautjaAncientShield", "The Gilded Warlord\u2019s Aegis");
                AssertVendorRow(support, "CMUYautjaAncientShieldAlt", "The Dread Hunter\u2019s Bulwark");

                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ranged.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaPlasmaPistol",
                    "CMUYautjaQuiverStrapFilled",
                }));
                AssertVendorRow(ranged, "CMUYautjaPlasmaPistol", "The Swift Plasma Pistol");
                AssertVendorRow(ranged, "CMUYautjaQuiverStrapFilled", "The Firm Bow");

                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(capes.Choices, Is.Not.Null);
                Assert.That(capes.Choices!.Value.Id, Is.EqualTo("CMUYautjaAccessory"));
                Assert.That(capes.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(capes.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaCapeQuarter",
                    "CMUYautjaCapeThird",
                    "CMUYautjaCapeHalf",
                    "CMUYautjaCapePoncho",
                    "CMUYautjaCapeDamaged",
                    "CMUYautjaCapeFull",
                    "CMUYautjaCapeCeremonial",
                }));
                AssertVendorRow(capes, "CMUYautjaCapeDamaged", "Damaged-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeFull", "Full-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeCeremonial", "Ceremonial Cape", replaceSlot: SlotFlags.BACK);

                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var spareRows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();
                Assert.That(spare.Choices, Is.Null);
                Assert.That(spareRows, Is.EqualTo(new[]
                {
                    ("CMUYautjaFalconDrone", "Falcon Drone", (int?) 20, (int?) null),
                    ("CMUYautjaHuntingTrap", "Hunting Trap", (int?) 10, (int?) null),
                    ("CMUYautjaSmartDisc", "Smart-Disc", (int?) 20, (int?) null),
                    ("CMUYautjaArrow", "Arrow - Explosive", (int?) 10, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 15, (int?) null),
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElderRackRowsUseCmss13DisplayNamesAndRecommendedFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");

                Assert.Multiple(() =>
                {
                    AssertVendorRow(essentials, "CMUYautjaHuntingEquipmentBundle", "Hunting Equipment");
                    AssertVendorRow(essentials, "CMUYautjaArmorBundle", "Armor");

                    AssertVendorRow(main, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaRendingSword", "The Rending Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaPiercingSword", "The Piercing Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaSeveringSword", "The Severing Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCruelStaff", "The Taruulan Staff", recommended: true);
                    AssertVendorRow(main, "CMUYautjaChainwhip", "The Sundering Chain-Whip", recommended: true);
                    AssertVendorRow(main, "CMUYautjaDualWarScythe", "The Cleaving War-Scythe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaDoubleWarScythe", "The Ripping War-Scythe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCombistick", "The Adaptive Combi-Stick", recommended: true);
                    AssertVendorRow(main, "CMUYautjaWarAxe", "The Butchering War Axe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaWarGlaive", "The Lumbering Glaive", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCleavingGlaive", "The Imposing Glaive", recommended: true);
                    AssertVendorRow(main, "CMUYautjaLongaxe", "The Crushing Longaxe", recommended: true);

                    AssertVendorRow(bracer, "CMUYautjaWristBladesBundle", "Wrist Blades");
                    AssertVendorRow(bracer, "CMUYautjaBracerShieldAttachment", "The Compact Shield", recommended: true);
                    AssertVendorRow(bracer, "CMUYautjaFearsomeScimitarsBundle", "The Fearsome Scimitars", recommended: true);
                    AssertVendorRow(bracer, "CMUYautjaSkeweringScimitarsBundle", "The Skewering Scimitars", recommended: true);
                    AssertVendorRow(bracer, "CMUYautjaChainGauntletsBundle", "The Chain Gauntlets", recommended: true);

                    AssertVendorRow(support, "CMUYautjaFalconDrone", "The Agile Drone");
                    AssertVendorRow(support, "CMUYautjaClanShield", "The Steadfast Shield");
                    AssertVendorRow(support, "CMUYautjaAncientShield", "The Gilded Warlord\u2019s Aegis");
                    AssertVendorRow(support, "CMUYautjaAncientShieldAlt", "The Dread Hunter\u2019s Bulwark");
                    AssertVendorRow(support, "CMUYautjaHeavyClanArmor", "The Formidable Plate Armor", replaceSlot: SlotFlags.OUTERCLOTHING);
                    AssertVendorRow(support, "CMUYautjaSpikeLauncher", "The Fleeting Spike Launcher");
                    AssertVendorRow(support, "CMUYautjaSmartDisc", "The Purifying Smart-Disc");

                    AssertVendorRow(ranged, "CMUYautjaPlasmaPistol", "The Swift Plasma Pistol");
                    AssertVendorRow(ranged, "CMUYautjaQuiverStrapFilled", "The Firm Bow");

                    AssertVendorRow(capes, "CMUYautjaCapeQuarter", "Quarter-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeThird", "Third-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeHalf", "Half-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapePoncho", "Poncho", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeDamaged", "Damaged-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeFull", "Full-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeCeremonial", "Ceremonial Cape", replaceSlot: SlotFlags.BACK);
                });
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElderRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUYautjaElderLoadoutVendor"), Is.True,
                "CMSS13 has a dedicated cm_vending_elder_yautja product list.");

            var rack = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracerPrimaryHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentialsIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                var mainIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracerIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                var supportIndex = vendor.Sections.FindIndex(section => section.Name == "Support Equipment (CHOOSE 2)");
                var rangedIndex = vendor.Sections.FindIndex(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var accessoryIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(essentialsIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mainIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bracerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(supportIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(rangedIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(accessoryIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[essentialsIndex];
                var main = vendor.Sections[mainIndex];
                var bracer = vendor.Sections[bracerIndex];
                var support = vendor.Sections[supportIndex];
                var ranged = vendor.Sections[rangedIndex];
                var accessory = vendor.Sections[accessoryIndex];

                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                var armorIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaArmorBundle");
                var swordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                var wristIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaWristBladesBundle");
                var scimitarIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle");
                var falconIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFalconDrone");
                var ancientShieldIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaAncientShield");
                var bowIndex = ranged.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaQuiverStrapFilled");
                var ceremonialCapeIndex = accessory.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeCeremonial");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(swordIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(wristIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(scimitarIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(falconIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(ancientShieldIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bowIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(ceremonialCapeIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, essentialsIndex, huntingIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.Zero);

                Vend(entMan, rack, hunter, essentialsIndex, armorIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, mainIndex, swordIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, bracerIndex, wristIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));

                Vend(entMan, rack, bracerPrimaryHunter, bracerIndex, scimitarIndex);
                var bracerPrimaryUser = entMan.GetComponent<CMVendorUserComponent>(bracerPrimaryHunter);
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);

                Vend(entMan, rack, hunter, supportIndex, falconIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, supportIndex, ancientShieldIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(2));

                Vend(entMan, rack, hunter, rangedIndex, bowIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, accessoryIndex, ceremonialCapeIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracerPrimaryHunter))
                    entMan.DeleteEntity(bracerPrimaryHunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedElderRackUsesCmss13ElderInventory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorElderLeftSouthOffset0x16", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind.ToString(), Is.EqualTo("Elder"));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");

                Assert.That(support.Entries.Select(entry => entry.Id.Id), Does.Contain("CMUYautjaAncientShield"));
                Assert.That(support.Entries.Select(entry => entry.Id.Id), Does.Contain("CMUYautjaAncientShieldAlt"));
                Assert.That(capes.Entries.Select(entry => entry.Id.Id), Does.Contain("CMUYautjaCapeCeremonial"));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedRackBowRowsUseCmss13SurvivorPricing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind.ToString(), Is.EqualTo("Stranded"));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var arrowRows = spare.Entries
                    .Where(entry => entry.Name?.StartsWith("Arrow - ") == true)
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();

                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ranged.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaQuiverStrapFilled",
                }), "CMSS13 stranded/survivor rack maps The Firm Bow to /obj/item/storage/belt/gun/quiver/full.");
                AssertVendorRow(ranged, "CMUYautjaQuiverStrapFilled", "The Firm Bow");

                Assert.That(spare.Choices, Is.Null);
                Assert.That(arrowRows, Is.EqualTo(new[]
                {
                    ("CMUYautjaArrow", "Arrow - Explosive", (int?) 15, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 20, (int?) null),
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedRackClaimGroupsMatchCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var rack = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Main Weapons (CHOOSE 1)",
                    "Bracer Attachments",
                    "Support Equipment (CHOOSE 2)",
                    "Ranged Equipment (CHOOSE 1)",
                    "Clothing Accessory (CHOOSE 1)",
                    "Spare Equipment",
                }));

                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                Assert.That(essentials.Choices, Is.Null);
                Assert.That(essentials.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaStrandedHuntingEquipmentBundle",
                    "CMUYautjaStrandedArmorBundle",
                }));
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaStrandedHuntingEquipmentBundle"), "CMUYautjaEssentials", 1);
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaStrandedArmorBundle"), "CMUYautjaArmor", 1);
                AssertBundle(prototypes, entMan, "CMUYautjaStrandedHuntingEquipmentBundle", new[]
                {
                    "CMUYautjaBodyMeshScalable",
                    "CMUYautjaHuntingPouch",
                    "CMUYautjaMedicompFull",
                    "CMUYautjaCleanserGelVial",
                });
                AssertBundle(prototypes, entMan, "CMUYautjaStrandedArmorBundle", new[]
                {
                    "CMUYautjaClanArmorScalable",
                    "CMUYautjaMaskScalable",
                    "CMUYautjaMaskAccessory01Ebony",
                    "CMUYautjaClanGreavesScalable",
                });

                var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(main.Choices, Is.Not.Null);
                Assert.That(main.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(main.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(main.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
                    "CMUYautjaCruelStaff",
                    "CMUYautjaChainwhip",
                    "CMUYautjaDualWarScythe",
                    "CMUYautjaDoubleWarScythe",
                    "CMUYautjaCombistick",
                    "CMUYautjaWarAxe",
                    "CMUYautjaWarGlaive",
                    "CMUYautjaCleavingGlaive",
                    "CMUYautjaLongaxe",
                }));
                AssertVendorRow(main, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaRendingSword", "The Rending Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaPiercingSword", "The Piercing Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaSeveringSword", "The Severing Hunting Sword", recommended: true);
                AssertVendorRow(main, "CMUYautjaCruelStaff", "The Taruulan Staff", recommended: true);

                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                Assert.That(bracer.Choices, Is.Null);
                Assert.That(bracer.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaBracerShieldAttachment",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }));
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaWristBladesBundle"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaBracerShieldAttachment"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaSkeweringScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaChainGauntletsBundle"), "CMUYautjaPrimary", 1);
                AssertVendorRow(bracer, "CMUYautjaWristBladesBundle", "Wrist Blades");
                AssertVendorRow(bracer, "CMUYautjaBracerShieldAttachment", "The Compact Shield", recommended: true);
                AssertVendorRow(bracer, "CMUYautjaFearsomeScimitarsBundle", "The Fearsome Scimitars", recommended: true);
                AssertVendorRow(bracer, "CMUYautjaSkeweringScimitarsBundle", "The Skewering Scimitars", recommended: true);
                AssertVendorRow(bracer, "CMUYautjaChainGauntletsBundle", "The Chain Gauntlets", recommended: true);

                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                Assert.That(support.Choices, Is.Not.Null);
                Assert.That(support.Choices!.Value.Id, Is.EqualTo("CMUYautjaSupport"));
                Assert.That(support.Choices.Value.Amount, Is.EqualTo(2));
                Assert.That(support.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaFalconDrone",
                    "CMUYautjaClanShield",
                    "CMUYautjaHeavyClanArmor",
                    "CMUYautjaSpikeLauncher",
                    "CMUYautjaSmartDisc",
                }));
                AssertVendorRow(support, "CMUYautjaFalconDrone", "The Agile Drone");
                AssertVendorRow(support, "CMUYautjaClanShield", "The Steadfast Shield");
                AssertVendorRow(support, "CMUYautjaHeavyClanArmor", "The Formidable Plate Armor", replaceSlot: SlotFlags.OUTERCLOTHING);
                AssertVendorRow(support, "CMUYautjaSpikeLauncher", "The Fleeting Spike Launcher");
                AssertVendorRow(support, "CMUYautjaSmartDisc", "The Purifying Smart-Disc");

                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ranged.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaQuiverStrapFilled",
                }));
                AssertVendorRow(ranged, "CMUYautjaQuiverStrapFilled", "The Firm Bow");

                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(capes.Choices, Is.Not.Null);
                Assert.That(capes.Choices!.Value.Id, Is.EqualTo("CMUYautjaAccessory"));
                Assert.That(capes.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(capes.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaCapeQuarter",
                    "CMUYautjaCapeThird",
                    "CMUYautjaCapeHalf",
                    "CMUYautjaCapePoncho",
                    "CMUYautjaCapeDamaged",
                    "CMUYautjaCapeFull",
                }));
                AssertVendorRow(capes, "CMUYautjaCapeQuarter", "Quarter-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeThird", "Third-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeHalf", "Half-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapePoncho", "Poncho", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeDamaged", "Damaged-Cape", replaceSlot: SlotFlags.BACK);
                AssertVendorRow(capes, "CMUYautjaCapeFull", "Full-Cape", replaceSlot: SlotFlags.BACK);

                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var spareRows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();
                Assert.That(spare.Choices, Is.Null);
                Assert.That(spareRows, Is.EqualTo(new[]
                {
                    ("CMUYautjaFalconDrone", "Falcon Drone", (int?) 20, (int?) null),
                    ("CMUYautjaHuntingTrap", "Hunting Trap", (int?) 15, (int?) null),
                    ("CMUYautjaArrow", "Arrow - Explosive", (int?) 15, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 20, (int?) null),
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SharedYautjaMandatoryBundlesMatchCmss13SourceRows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = new[]
        {
            new MandatoryBundleRow(
                "CMUYautjaLoadoutVendor",
                "CMUYautjaHuntingEquipmentBundle",
                "CMUYautjaEssentials",
                ["CMUYautjaBodyMesh", "CMUYautjaHuntingPouch", "CMUYautjaMedicompFull", "CMUYautjaRelayBeacon", "CMUYautjaCleanserGelVial"]),
            new MandatoryBundleRow(
                "CMUYautjaLoadoutVendor",
                "CMUYautjaArmorBundle",
                "CMUYautjaArmor",
                ["CMUYautjaClanArmor", "CMUYautjaMask", "CMUYautjaMaskAccessory01Ebony", "CMUYautjaClanGreaves"]),
            new MandatoryBundleRow(
                "CMUYautjaYoungbloodLoadoutVendor",
                "CMUYautjaYoungbloodHuntingEquipmentBundle",
                "CMUYautjaEssentials",
                ["CMUYautjaBodyMesh", "CMUYautjaHuntingPouch", "CMUYautjaMedicompFull", "CMUYautjaLantern"]),
            new MandatoryBundleRow(
                "CMUYautjaYoungbloodLoadoutVendor",
                "CMUYautjaArmorBundle",
                "CMUYautjaArmor",
                ["CMUYautjaClanArmor", "CMUYautjaMask", "CMUYautjaMaskAccessory01Ebony", "CMUYautjaClanGreaves"]),
            new MandatoryBundleRow(
                "CMUYautjaStrandedLoadoutVendor",
                "CMUYautjaStrandedHuntingEquipmentBundle",
                "CMUYautjaEssentials",
                ["CMUYautjaBodyMeshScalable", "CMUYautjaHuntingPouch", "CMUYautjaMedicompFull", "CMUYautjaCleanserGelVial"]),
            new MandatoryBundleRow(
                "CMUYautjaStrandedLoadoutVendor",
                "CMUYautjaStrandedArmorBundle",
                "CMUYautjaArmor",
                ["CMUYautjaClanArmorScalable", "CMUYautjaMaskScalable", "CMUYautjaMaskAccessory01Ebony", "CMUYautjaClanGreavesScalable"]),
        };

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var racks = rows
                .Select(row => row.RackId)
                .Distinct()
                .ToDictionary(id => id, id => entMan.SpawnEntity(id, MapCoordinates.Nullspace));

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(racks[row.RackId]);
                        var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                        var entry = essentials.Entries.Single(entry => entry.Id.Id == row.BundleId);

                        Assert.That(essentials.Choices, Is.Null, $"{row.RackId} source section has no shared category; mandatory rows carry per-entry categories.");
                        AssertVendorRow(essentials, row.BundleId, row.BundleId.Contains("Armor") ? "Armor" : "Hunting Equipment", mandatory: true);
                        AssertChoice(entry, row.ChoiceId, 1);
                        AssertBundle(prototypes, entMan, row.BundleId, row.BundleIds);
                    }
                });
            }
            finally
            {
                foreach (var rack in racks.Values)
                {
                    if (!entMan.Deleted(rack))
                        entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackRowsExposeCmss13MandatoryRecommendedAndRegularFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rows = Cmss13YautjaRackPriorityRows().ToArray();
            var mandatoryField = typeof(CMVendorEntry).GetField("Mandatory");
            var racks = rows
                .Select(row => row.RackId)
                .Distinct()
                .ToDictionary(id => id, id => entMan.SpawnEntity(id, MapCoordinates.Nullspace));

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mandatoryField, Is.Not.Null,
                        "CMSS13 vendor rows distinguish VENDOR_ITEM_MANDATORY from VENDOR_ITEM_RECOMMENDED and VENDOR_ITEM_REGULAR.");

                    foreach (var row in rows)
                    {
                        var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(racks[row.RackId]);
                        var section = vendor.Sections.Single(section => section.Name == row.Section);
                        var entry = section.Entries.Single(entry => entry.Id.Id == row.EntryId);
                        var mandatory = mandatoryField?.GetValue(entry);

                        Assert.That(mandatory, Is.EqualTo(row.Mandatory),
                            $"{row.RackId} {row.Section}/{row.EntryId} maps CMSS13 VENDOR_ITEM_MANDATORY.");
                        Assert.That(entry.Recommended, Is.EqualTo(row.Recommended),
                            $"{row.RackId} {row.Section}/{row.EntryId} maps CMSS13 VENDOR_ITEM_RECOMMENDED.");
                    }
                });
            }
            finally
            {
                foreach (var rack in racks.Values)
                {
                    if (!entMan.Deleted(rack))
                        entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", MapCoordinates.Nullspace);
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracerPrimaryHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentialsIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                var mainIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracerIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                var supportIndex = vendor.Sections.FindIndex(section => section.Name == "Support Equipment (CHOOSE 2)");
                var rangedIndex = vendor.Sections.FindIndex(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var accessoryIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(essentialsIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mainIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bracerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(supportIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(rangedIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(accessoryIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[essentialsIndex];
                var main = vendor.Sections[mainIndex];
                var bracer = vendor.Sections[bracerIndex];
                var support = vendor.Sections[supportIndex];
                var ranged = vendor.Sections[rangedIndex];
                var accessory = vendor.Sections[accessoryIndex];

                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaStrandedHuntingEquipmentBundle");
                var armorIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaStrandedArmorBundle");
                var swordIndex = main.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                var wristIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaWristBladesBundle");
                var scimitarIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle");
                var falconIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFalconDrone");
                var shieldIndex = support.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanShield");
                var bowIndex = ranged.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaQuiverStrapFilled");
                var capeIndex = accessory.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeDamaged");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(swordIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(wristIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(scimitarIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(falconIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(shieldIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bowIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(capeIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, essentialsIndex, huntingIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.Zero);

                Vend(entMan, rack, hunter, essentialsIndex, armorIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, mainIndex, swordIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, bracerIndex, wristIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));

                Vend(entMan, rack, bracerPrimaryHunter, bracerIndex, scimitarIndex);
                var bracerPrimaryUser = entMan.GetComponent<CMVendorUserComponent>(bracerPrimaryHunter);
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
                Assert.That(bracerPrimaryUser.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);

                Vend(entMan, rack, hunter, supportIndex, falconIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, supportIndex, shieldIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaSupport"), Is.EqualTo(2));

                Vend(entMan, rack, hunter, rangedIndex, bowIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.EqualTo(1));

                Vend(entMan, rack, hunter, accessoryIndex, capeIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracerPrimaryHunter))
                    entMan.DeleteEntity(bracerPrimaryHunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodAndStrandedRackSegmentsAutoConnect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid badLeft = default;
        EntityUid badRight = default;
        EntityUid strandedLeft = default;
        EntityUid strandedRight = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(0, 1), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 1), new Tile(1));

            badLeft = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", map.GridCoords);
            badRight = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", map.GridCoords.Offset(new Vector2(1, 0)));
            strandedLeft = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", map.GridCoords.Offset(new Vector2(0, 1)));
            strandedRight = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", map.GridCoords.Offset(new Vector2(1, 1)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(badLeft, YautjaGearRackVisuals.State, out var badLeftState), Is.True);
                Assert.That(badLeftState, Is.EqualTo(YautjaGearRackVisualState.Left));
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(badRight, YautjaGearRackVisuals.State, out var badRightState), Is.True);
                Assert.That(badRightState, Is.EqualTo(YautjaGearRackVisualState.Right));
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(strandedLeft, YautjaGearRackVisuals.State, out var strandedLeftState), Is.True);
                Assert.That(strandedLeftState, Is.EqualTo(YautjaGearRackVisualState.Left));
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(strandedRight, YautjaGearRackVisuals.State, out var strandedRightState), Is.True);
                Assert.That(strandedRightState, Is.EqualTo(YautjaGearRackVisualState.Right));
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[] { badLeft, badRight, strandedLeft, strandedRight })
            {
                if (!entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackUsesCmss13SupportClaimLimit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");

                Assert.That(support.Choices, Is.Not.Null);
                Assert.That(support.Choices!.Value.Id, Is.EqualTo("CMUYautjaSupport"));
                Assert.That(support.Choices.Value.Amount, Is.EqualTo(2));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackSectionTitlesUseCmss13ChooseText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Does.Contain("Support Equipment (CHOOSE 2)"),
                    "CMSS13 regular Yautja rack labels the support claim section with the source choose count.");
                Assert.That(sectionNames, Does.Contain("Ranged Equipment (CHOOSE 1)"),
                    "CMSS13 regular Yautja rack labels the ranged claim section with the source choose count.");
                Assert.That(sectionNames, Does.Contain("Clothing Accessory (CHOOSE 1)"),
                    "CMSS13 regular Yautja rack labels the cape/accessory claim section with the source choose count.");
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackSupportClaimGroupMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                var ids = support.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(ids, Is.EqualTo(new[]
                {
                    "CMUYautjaFalconDrone",
                    "CMUYautjaClanShield",
                    "CMUYautjaHeavyClanArmor",
                    "CMUYautjaSpikeLauncher",
                    "CMUYautjaSmartDisc",
                }));

                var smartDisc = support.Entries.Single(entry => entry.Id.Id == "CMUYautjaSmartDisc");
                Assert.That(smartDisc.Name, Is.EqualTo("The Purifying Smart-Disc"));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackNearCompleteEquipmentRowsUseCmss13DisplayNames()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var main = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                var support = vendor.Sections.Single(section => section.Name == "Support Equipment (CHOOSE 2)");
                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");

                Assert.Multiple(() =>
                {
                    AssertVendorRow(essentials, "CMUYautjaHuntingEquipmentBundle", "Hunting Equipment");
                    AssertVendorRow(essentials, "CMUYautjaArmorBundle", "Armor");

                    AssertVendorRow(main, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaRendingSword", "The Rending Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaPiercingSword", "The Piercing Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaSeveringSword", "The Severing Hunting Sword", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCruelStaff", "The Taruulan Staff", recommended: true);
                    AssertVendorRow(main, "CMUYautjaChainwhip", "The Sundering Chain-Whip", recommended: true);
                    AssertVendorRow(main, "CMUYautjaDualWarScythe", "The Cleaving War-Scythe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaDoubleWarScythe", "The Ripping War-Scythe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCombistick", "The Adaptive Combi-Stick", recommended: true);
                    AssertVendorRow(main, "CMUYautjaWarAxe", "The Butchering War Axe", recommended: true);
                    AssertVendorRow(main, "CMUYautjaWarGlaive", "The Lumbering Glaive", recommended: true);
                    AssertVendorRow(main, "CMUYautjaCleavingGlaive", "The Imposing Glaive", recommended: true);
                    AssertVendorRow(main, "CMUYautjaLongaxe", "The Crushing Longaxe", recommended: true);

                    AssertVendorRow(bracer, "CMUYautjaBracerShieldAttachment", "The Compact Shield", recommended: true);

                    AssertVendorRow(support, "CMUYautjaFalconDrone", "The Agile Drone");
                    AssertVendorRow(support, "CMUYautjaClanShield", "The Steadfast Shield");
                    AssertVendorRow(support, "CMUYautjaHeavyClanArmor", "The Formidable Plate Armor", replaceSlot: SlotFlags.OUTERCLOTHING);
                    AssertVendorRow(support, "CMUYautjaSpikeLauncher", "The Fleeting Spike Launcher");
                    AssertVendorRow(support, "CMUYautjaSmartDisc", "The Purifying Smart-Disc");

                    AssertVendorRow(ranged, "CMUYautjaPlasmaPistol", "The Swift Plasma Pistol");
                    AssertVendorRow(ranged, "CMUYautjaQuiverStrapFilled", "The Firm Bow");

                    AssertVendorRow(capes, "CMUYautjaCapeQuarter", "Quarter-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeThird", "Third-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapeHalf", "Half-Cape", replaceSlot: SlotFlags.BACK);
                    AssertVendorRow(capes, "CMUYautjaCapePoncho", "Poncho", replaceSlot: SlotFlags.BACK);
                });
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmartDiscPrototypeMatchesCmss13StaticItemFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(disc);
                var item = entMan.GetComponent<ItemComponent>(disc);
                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var embeddable = entMan.GetComponent<EmbeddableProjectileComponent>(disc);
                var thrownDamage = entMan.GetComponent<DamageOtherOnHitComponent>(disc);
                var staminaCollide = entMan.GetComponent<StaminaDamageOnCollideComponent>(disc);
                var staminaEmbed = entMan.GetComponent<StaminaDamageOnEmbedComponent>(disc);
                var recallable = entMan.GetComponent<YautjaRecallableComponent>(disc);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("smart-disc"),
                        "CMSS13 /obj/item/explosive/grenade/spawnergrenade/smartdisc source name.");
                    Assert.That(meta.EntityDescription, Is.EqualTo("A strange piece of alien technology. It has many jagged, whirring blades and bizarre writing."),
                        "CMSS13 smartdisc source description.");
                    Assert.That(item.Size.Id, Is.EqualTo("Tiny"),
                        "CMSS13 smartdisc w_class = SIZE_TINY.");
                    Assert.That(smartDisc.ActiveTime, Is.EqualTo(TimeSpan.FromSeconds(30)),
                        "CMSS13 smartdisc det_time = 30 before prime().");
                    Assert.That(smartDisc.SearchRange, Is.EqualTo(7f),
                        "CMSS13 spawned hostile smartdisc ListTargets(7) target search radius.");
                    Assert.That(smartDisc.HitRange, Is.EqualTo(1f),
                        "CMSS13 spawned hostile smartdisc attacks adjacent targets with get_dist(src, target_mob) <= 1.");
                    Assert.That(smartDisc.MaxHits, Is.EqualTo(8),
                        "CMSS13 spawned hostile smartdisc lifetime = 8 local mapping.");
                    Assert.That(smartDisc.HitDelay, Is.EqualTo(TimeSpan.FromSeconds(1)),
                        "CMSS13 spawned hostile smartdisc turns_per_move = 1 local hit cadence mapping.");
                    Assert.That(embeddable.EmbedOnThrow, Is.False,
                        "CMSS13 smartdisc embeddable = FALSE.");
                    Assert.That(DamageTotal(thrownDamage.Damage), Is.EqualTo((FixedPoint2) 25),
                        "CMSS13 smartdisc throwforce = 25.");
                    Assert.That(staminaCollide.Damage, Is.EqualTo(25f),
                        "CMSS13 smartdisc throwforce has no separate stamina payload; keep local collide stamina aligned to throwforce.");
                    Assert.That(staminaEmbed.Damage, Is.EqualTo(0f),
                        "CMSS13 smartdisc embeddable = FALSE means no embed stamina payload.");
                    Assert.That(recallable.Range, Is.EqualTo(12f),
                        "CMSS13 boomerang throws back toward the user with range 12.");
                    Assert.That(entMan.TryGetComponent<MeleeWeaponComponent>(disc, out var melee), Is.True,
                        "CMSS13 smartdisc force = 15 means the held item must also be a melee weapon.");
                    Assert.That(melee!.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 15),
                        "CMSS13 smartdisc force = 15.");
                    AssertNonCorrodible(entMan, disc);

                    Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(disc, out var tech), Is.True,
                        "CMSS13 smartdisc flags_item = ITEM_PREDATOR.");
                    Assert.That(tech!.DamageMultiplier, Is.EqualTo(1f),
                        "CMSS13 smartdisc source force/throwforce should be represented by explicit smart-disc damage, not generic tech damage scaling.");
                });
            }
            finally
            {
                if (!entMan.Deleted(disc))
                    entMan.DeleteEntity(disc);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaSmartDisc", "_CMU14/Yautja/smart_disc.rsi", "icon");
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
    public async Task YautjaSmartDiscUsePrimesWithoutNearbyTargetLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var appearance = entMan.System<SharedAppearanceSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, disc), Is.True);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(disc, use);

                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                var toggle = entMan.GetComponent<ItemToggleComponent>(disc);
                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True,
                        "CMSS13 smartdisc attack_self() consumes a Yautja use before activate(user).");
                    Assert.That(toggle.Activated, Is.True,
                        "CMSS13 smartdisc attack_self() calls activate(user) without checking for nearby targets.");
                    Assert.That(smartDisc.Active, Is.True,
                        "Local held smart-disc activation maps CMSS13 activate(user) to an active disc state even when no prey is nearby.");
                    Assert.That(smartDisc.YautjaOwner, Is.EqualTo(hunter));
                    Assert.That(smartDisc.CurrentTarget, Is.Null,
                        "No target nearby should mean the primed disc has no prey, not that activation was denied.");
                    Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                    Assert.That(activeVisual, Is.True,
                        "CMSS13 activate(user) sets icon_state = initial(icon_state) + \"_active\".");
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
                faction.AddFaction((friendlyNonYautja, null), "CMUYautja");
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
                entMan.EventBus.RaiseLocalEvent(disc, hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, disc), Is.True,
                        "CMSS13 smartdisc launch_impact() makes a Yautja put the disc in hand.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.False,
                        "CMSS13 smartdisc launch_impact() sets throwing = FALSE after a successful catch.");
                    Assert.That(smartDisc.Active, Is.False,
                        "A caught local smart-disc should be returned to the inactive item state.");
                    Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
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
    public async Task FullHandedYautjaSmartDiscImpactSkipsThrowDamageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var leftFiller = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords);
            var rightFiller = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<StaminaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, leftFiller), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, rightFiller), Is.True);
                Assert.That(hands.TryGetEmptyHand(hunter, out _), Is.False);

                var damageable = entMan.GetComponent<DamageableComponent>(hunter);
                var stamina = entMan.GetComponent<StaminaComponent>(hunter);
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;

                var hit = new ThrowDoHitEvent(disc, hunter, thrown);
                entMan.EventBus.RaiseLocalEvent(disc, hit);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, disc), Is.False,
                        "A full-handed Yautja cannot put the smart-disc in hand.");
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True,
                        "CMSS13 launch_impact() only sets throwing = FALSE inside the successful put_in_hands() branch.");
                    Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 smartdisc launch_impact() still returns before parent impact for any Yautja hit, even when put_in_hands() fails.");
                    Assert.That(stamina.StaminaDamage, Is.EqualTo(0f),
                        "The local handled Yautja impact must skip generic throw-hit stamina even if the catcher has no free hands.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, leftFiller, rightFiller, disc })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrownSmartDiscBoomerangUsesTemporaryActiveVisualWithoutHostileStateLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid disc = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));

            entMan.EnsureComponent<YautjaComponent>(hunter);

            var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
            thrown.Thrower = hunter;
            var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
            smartDisc.PendingThrowActivator = hunter;
            smartDisc.PendingThrowActivationAt = TimeSpan.Zero;

            entMan.System<YautjaSmartDiscSystem>().Update(0.5f);

            Assert.Multiple(() =>
            {
                Assert.That(smartDisc.Active, Is.False,
                    "CMSS13 smartdisc boomerang(user) does not call activate(); it only sets icon_state = initial(icon_state) + \"_active\" temporarily.");
                Assert.That(smartDisc.CurrentTarget, Is.Null,
                    "CMSS13 boomerang() only throws toward a prey if find_target(user) found one, then always throws back toward the user.");
                Assert.That(smartDisc.YautjaOwner, Is.EqualTo(hunter));
                Assert.That(entMan.HasComponent<ThrownItemComponent>(disc), Is.True,
                    "CMSS13 boomerang() keeps the thrown return-to-owner movement without spawning the hostile smartdisc mob.");
                Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                Assert.That(activeVisual, Is.True,
                    "CMSS13 smartdisc boomerang() sets icon_state to disc_active for three seconds.");
            });
        });

        await pair.RunSeconds(1f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                Assert.That(activeVisual, Is.True,
                    "CMSS13 smartdisc boomerang() keeps icon_state = disc_active until clear_boomerang() runs after 3 SECONDS, even after return movement stops.");
            });
        });

        await pair.RunSeconds(3.2f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaSmartDiscComponent>(disc).Active, Is.False,
                    "CMSS13 clear_boomerang() restores only icon_state; it does not leave a hostile active disc running.");
                Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                Assert.That(activeVisual, Is.False,
                    "CMSS13 clear_boomerang() resets icon_state to initial(icon_state) after 3 SECONDS.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[] { hunter, disc })
            {
                if (uid != default && !entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrownSmartDiscBoomerangClientSpriteShowsTemporaryActiveStateLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid disc = default;
        NetEntity discNet = default;
        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
                discNet = entMan.GetNetEntity(disc);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.GetComponent<YautjaSmartDiscComponent>(disc).BoomerangVisualDuration = TimeSpan.FromSeconds(10);
            });

            await pair.RunTicksSync(5);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                Assert.That(entMan.TryGetEntity(discNet, out var clientDisc), Is.True);

                var sprites = entMan.System<SpriteSystem>();
                var sprite = entMan.GetComponent<SpriteComponent>(clientDisc.Value);

                Assert.That(sprites.TryGetLayer((clientDisc.Value, sprite), "base", out var layer, false), Is.True);
                Assert.That(layer.State.Name, Is.EqualTo("icon"));
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var thrown = entMan.EnsureComponent<ThrownItemComponent>(disc);
                thrown.Thrower = hunter;
                var smartDisc = entMan.GetComponent<YautjaSmartDiscComponent>(disc);
                smartDisc.PendingThrowActivator = hunter;
                smartDisc.PendingThrowActivationAt = TimeSpan.Zero;

                entMan.System<YautjaSmartDiscSystem>().Update(0.5f);
            });

            var sawActive = false;
            for (var i = 0; i < pair.SecondsToTicks(8f); i++)
            {
                await pair.RunTicksSync(1);

                await client.WaitPost(() =>
                {
                    var entMan = client.EntMan;
                    Assert.That(entMan.TryGetEntity(discNet, out var clientDisc), Is.True);

                    var sprites = entMan.System<SpriteSystem>();
                    var sprite = entMan.GetComponent<SpriteComponent>(clientDisc.Value);

                    Assert.That(sprites.TryGetLayer((clientDisc.Value, sprite), "base", out var layer, false), Is.True);
                    sawActive |= layer.State.Name == "active";
                });

                if (sawActive)
                    break;
            }

            Assert.That(sawActive, Is.True,
                "CMSS13 smartdisc boomerang() temporarily presents the smart-disc as disc_active on connected clients.");

            await pair.RunSeconds(10.2f);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                Assert.That(entMan.TryGetEntity(discNet, out var clientDisc), Is.True);

                var sprites = entMan.System<SpriteSystem>();
                var sprite = entMan.GetComponent<SpriteComponent>(clientDisc.Value);

                Assert.That(sprites.TryGetLayer((clientDisc.Value, sprite), "base", out var layer, false), Is.True);
                Assert.That(layer.State.Name, Is.EqualTo("icon"),
                    "CMSS13 clear_boomerang() restores the ordinary smart-disc icon state after the boomerang visual window.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, disc })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
    public async Task ThrownSmartDiscBoomerangFirstTravelsTowardPreyBeforeReturningLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var disc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

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
                        "CMSS13 smartdisc boomerang(user) does not call activate() even when find_target(user) finds prey.");
                    Assert.That(smartDisc.CurrentTarget, Is.EqualTo(prey),
                        "CMSS13 boomerang() first calls find_target(user) and throws toward the found prey.");
                    Assert.That(smartDisc.ReturningToOwner, Is.True,
                        "CMSS13 boomerang() always schedules the return throw toward the user after the prey pass.");
                    Assert.That(physics.LinearVelocity.X, Is.GreaterThan(0f),
                        "With prey to the right and the owner to the left, the first boomerang leg should travel toward prey before returning.");
                    Assert.That(appearance.TryGetData<bool>(disc, ToggleableVisuals.Enabled, out var activeVisual), Is.True);
                    Assert.That(activeVisual, Is.True);
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
    public async Task CallDiscCooldownAndActiveRangeMatchCmss13SmartDiscRecall()
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
            var nearbyInactiveDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(9, 0)));
            var activeInRangeDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(6, 0)));
            var activeOutOfRangeDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(8, 0)));

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
                        "CMSS13 call_disc_internal() recalls inactive smart-discs within range 10.");
                    Assert.That(hands.IsHolding(hunter, activeInRangeDisc), Is.True,
                        "CMSS13 active smart-disc local equivalent uses the stricter active recall range.");
                    Assert.That(activeInRangeComp.Active, Is.False,
                        "Recalling a local active smart-disc returns it to the inactive held item state.");
                    Assert.That(hands.IsHolding(hunter, activeOutOfRangeDisc), Is.False,
                        "Active smart-disc recall range is 7, so an active disc at 8 tiles remains in the field.");
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
                entMan.EventBus.RaiseLocalEvent(disc, hit);

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
    public async Task HarpoonPrototypeMatchesCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var harpoon = entMan.SpawnEntity("CMUYautjaHarpoon", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(harpoon);
                var item = entMan.GetComponent<ItemComponent>(harpoon);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(harpoon);
                var thrown = entMan.GetComponent<DamageOtherOnHitComponent>(harpoon);
                var embeddable = entMan.GetComponent<EmbeddableProjectileComponent>(harpoon);
                var throwRange = entMan.GetComponent<ItemThrowRangeComponent>(harpoon);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("large harpoon"),
                        "CMSS13 /obj/item/weapon/harpoon/yautja source name.");
                    Assert.That(meta.EntityDescription, Is.EqualTo("A huge metal spike with a hook at the end. It's carved with mysterious alien writing."),
                        "CMSS13 /obj/item/weapon/harpoon/yautja source description.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("harpoon"),
                        "CMSS13 /obj/item/weapon/harpoon/yautja item_state = \"harpoon\".");
                    Assert.That(entMan.HasComponent<SharpComponent>(harpoon), Is.True,
                        "CMSS13 /obj/item/weapon/harpoon/yautja sets sharp and edge.");
                    Assert.That(DamageTotal(melee.Damage), Is.EqualTo((FixedPoint2) 10),
                        "CMSS13 /obj/item/weapon/harpoon/yautja New() sets force = MELEE_FORCE_TIER_2.");
                    Assert.That(DamageTotal(thrown.Damage), Is.EqualTo((FixedPoint2) 30),
                        "CMSS13 /obj/item/weapon/harpoon/yautja New() sets throwforce = MELEE_FORCE_TIER_6.");
                    Assert.That(throwRange.Range, Is.EqualTo(4f),
                        "CMSS13 /obj/item/weapon/harpoon/yautja sets throw_range = 4.");
                    Assert.That(embeddable.EmbedOnThrow, Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja sets embeddable = FALSE.");
                    AssertNonCorrodible(entMan, harpoon);

                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(harpoon), Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja flags_item omits ITEM_PREDATOR.");
                    Assert.That(entMan.HasComponent<ClothingComponent>(harpoon), Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja does not set flags_equip_slot.");
                    Assert.That(entMan.HasComponent<YautjaRecallableComponent>(harpoon), Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja is not a chained recall weapon.");
                    Assert.That(entMan.HasComponent<LandAtCursorComponent>(harpoon), Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja does not set throw_speed = SPEED_VERY_FAST.");
                    Assert.That(entMan.HasComponent<DisarmMalusComponent>(harpoon), Is.False,
                        "CMSS13 /obj/item/weapon/harpoon/yautja does not inherit predator weapon disarm resistance.");
                });
            }
            finally
            {
                if (!entMan.Deleted(harpoon))
                    entMan.DeleteEntity(harpoon);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaHarpoon", "_CMU14/Yautja/weapons.rsi", "spike");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HarpoonManualThrowUsesCmss13FourTileRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var serverHands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var harpoon = entMan.SpawnEntity("CMUYautjaHarpoon", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(hunter, harpoon, checkActionBlocker: false), Is.True);

                var hunterHands = entMan.GetComponent<HandsComponent>(hunter);
                Assert.That(hunterHands.ThrowRange, Is.EqualTo(8f),
                    "This regression should be item-scoped; the normal local hand throw range remains unchanged.");

                Assert.That(serverHands.ThrowHeldItem(hunter, map.GridCoords.Offset(new Vector2(10, 0))), Is.True);

                var thrown = entMan.GetComponent<ThrownItemComponent>(harpoon);
                var flightTime = thrown.LandTime!.Value - thrown.ThrownTime!.Value;
                var expectedFlightTime = TimeSpan.FromSeconds(4f / hunterHands.BaseThrowspeed * ThrowingSystem.FlyTimePercentage);

                Assert.That(flightTime.TotalSeconds,
                    Is.EqualTo(expectedFlightTime.TotalSeconds).Within(0.001),
                    "CMSS13 /obj/item/weapon/harpoon/yautja sets throw_range = 4, so a far manual throw should clamp to four tiles before local friction compensation.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, harpoon })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallEquipmentSourcePrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                var chainshirt = SpawnAndTrack(entMan, "CMUYautjaThrallChainshirt", spawned);
                var chainshirtMeta = entMan.GetComponent<MetaDataComponent>(chainshirt);
                var chainshirtClothing = entMan.GetComponent<ClothingComponent>(chainshirt);

                Assert.Multiple(() =>
                {
                    Assert.That(chainshirtMeta.EntityName, Is.EqualTo("alien mesh suit"),
                        "CMSS13 /obj/item/clothing/under/chainshirt/thrall source name.");
                    Assert.That(chainshirtMeta.EntityDescription,
                        Is.EqualTo("A strange alloy weave in the form of a vest. It feels cold with an alien weight. It has been adapted for human physiology."),
                        "CMSS13 /obj/item/clothing/under/chainshirt/thrall source description.");
                    Assert.That(chainshirtClothing.Slots, Is.EqualTo(SlotFlags.INNERCLOTHING),
                        "CMSS13 thrall chainshirt inherits the chainshirt under-clothing slot.");
                    AssertCmss13ArmorStats(entMan, chainshirt, "CMUYautjaThrallChainshirt", new Cmss13ProtectionStats(10, 35, 40, 45));
                    AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, chainshirt, "CMUYautjaThrallChainshirt");
                    AssertNonCorrodible(entMan, chainshirt);

                    var gearBox = SpawnAndTrack(entMan, "CMUYautjaThrallGearBox", spawned);
                    var gearBoxMeta = entMan.GetComponent<MetaDataComponent>(gearBox);
                    var gearBoxFill = entMan.GetComponent<StorageFillComponent>(gearBox);

                    Assert.That(gearBoxMeta.EntityName, Is.EqualTo("alien box"),
                        "CMSS13 /obj/item/storage/box/bracer source name.");
                    Assert.That(gearBoxMeta.EntityDescription, Is.EqualTo("A strange, runed box."),
                        "CMSS13 /obj/item/storage/box/bracer source description.");
                    AssertStorageFill(gearBoxFill, new Dictionary<string, int>
                    {
                        ["CMUYautjaThrallBracer"] = 1,
                        ["CMUYautjaThrallAutoInjector"] = 3,
                    });

                    var thrallBracer = SpawnAndTrack(entMan, "CMUYautjaThrallBracer", spawned);
                    var thrallBracerMeta = entMan.GetComponent<MetaDataComponent>(thrallBracer);
                    var thrallBracerClothing = entMan.GetComponent<ClothingComponent>(thrallBracer);
                    Assert.That(thrallBracerMeta.EntityName, Is.EqualTo("thrall bracers"),
                        "CMSS13 /obj/item/clothing/gloves/yautja/thrall source name.");
                    Assert.That(thrallBracerMeta.EntityDescription,
                        Is.EqualTo("A pair of strange alien bracers, adapted for human biology."),
                        "CMSS13 /obj/item/clothing/gloves/yautja/thrall source description.");
                    Assert.That(thrallBracerClothing.Slots, Is.EqualTo(SlotFlags.GLOVES),
                        "CMSS13 thrall bracers inherit base Yautja bracer SLOT_HANDS.");
                    AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, thrallBracer, "CMUYautjaThrallBracer");
                    AssertNonCorrodible(entMan, thrallBracer);

                    AssertBundle(prototypes, entMan, "CMUYautjaThrallHuntingEquipmentBundle",
                    [
                        "CMUYautjaThrallChainshirt",
                        "CMUYautjaHuntingPouch",
                        "CMUYautjaLantern",
                        "CMUYautjaCommunicator",
                    ]);

                    var simpleRelay = SpawnAndTrack(entMan, "CMUYautjaSimpleRelayBeacon", spawned);
                    var simpleRelayPrice = entMan.GetComponent<StaticPriceComponent>(simpleRelay);
                    var simpleRelayBeacon = entMan.GetComponent<YautjaRelayBeaconComponent>(simpleRelay);
                    Assert.That(simpleRelayPrice.Price, Is.EqualTo(100),
                        "CMSS13 /obj/item/device/thrall_teleporter black_market_value = 100.");
                    Assert.That(simpleRelayBeacon.AllowCustomDestinations, Is.False,
                        "CMSS13 thrall teleporter only chooses from Yautja ship spawnpoints and does not expose custom destinations.");
                    Assert.That(simpleRelayBeacon.AllowedDestinations, Is.EqualTo(new[]
                    {
                        YautjaRelayDestinationKind.YautjaShip,
                    }), "CMSS13 thrall teleporter can only teleport back to the Yautja Ship.");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertPrototypeIconState(prototypes, factory, "CMUYautjaSimpleRelayBeacon", "_CMU14/HunterShip/obj/items/hunter/thrall_gear.rsi", "thrall_teleporter");
            AssertPrototypeIconState(prototypes, factory, "CMUYautjaThrallGearBox", "_CMU14/HunterShip/obj/structures/closet.rsi", "pred_coffin");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedScalableEquipmentMatchesCmss13DamagedStatsAndRepairText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = Cmss13StrandedScalableEquipmentRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var spawned = rows
                .Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        var meta = entMan.GetComponent<MetaDataComponent>(item);
                        var clothing = entMan.GetComponent<ClothingComponent>(item);
                        var armor = entMan.GetComponent<CMArmorComponent>(item);
                        var examineText = examine.GetExamineText(item, hunter).ToMarkup();

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} CMSS13 inherited source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} CMSS13 inherited source description");
                        Assert.That(clothing.Slots, Is.EqualTo(row.Slots), $"{row.Id} CMSS13 equip slot mapping");
                        Assert.That(armor.Melee, Is.EqualTo(row.Melee), $"{row.Id} CMSS13 armor_melee local tier mapping");
                        Assert.That(armor.Bullet, Is.EqualTo(row.Bullet), $"{row.Id} CMSS13 armor_bullet local tier mapping");
                        Assert.That(armor.Bio, Is.EqualTo(row.Bio), $"{row.Id} CMSS13 armor_bio local tier mapping");
                        Assert.That(armor.ExplosionArmor, Is.EqualTo(row.ExplosionArmor), $"{row.Id} CMSS13 armor_bomb local tier mapping");
                        Assert.That(examineText, Does.Contain(row.DamagedExamineText),
                            $"{row.Id} should expose the CMSS13 YAUTJA_REPAIR_DAMAGED examine line.");

                        if (row.SourceUnacidable)
                            AssertNonCorrodible(entMan, item);

                        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
                            $"{row.Id} source flags_item = ITEM_PREDATOR mapping");
                        if (tech != null)
                        {
                            Assert.That(tech.BlockPickup, Is.EqualTo(row.BlockPickup), $"{row.Id} source ITEM_PREDATOR pickup restriction");
                            Assert.That(tech.BlockUse, Is.True, $"{row.Id} source ITEM_PREDATOR use restriction");
                            Assert.That(tech.BlockMelee, Is.True, $"{row.Id} source ITEM_PREDATOR melee restriction");
                            Assert.That(tech.BlockThrow, Is.True, $"{row.Id} source ITEM_PREDATOR throw restriction");
                            Assert.That(tech.BlockShoot, Is.True, $"{row.Id} source ITEM_PREDATOR shoot restriction");
                        }

                        if (row.AntiHugMaxCount is { } antiHug)
                        {
                            Assert.That(entMan.TryGetComponent<ParasiteResistanceComponent>(item, out var resistance), Is.True,
                                $"{row.Id} CMSS13 anti_hug should map to local parasite resistance.");
                            Assert.That(resistance!.MaxCount, Is.EqualTo(antiHug),
                                $"{row.Id} CMSS13 scalable mask anti_hug = 30.");
                        }
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned.Append(hunter))
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StoneFlavorGearPrototypesMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var rows = Cmss13StoneFlavorGearRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = rows
                .Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        var meta = entMan.GetComponent<MetaDataComponent>(item);
                        var clothing = entMan.GetComponent<ClothingComponent>(item);

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} CMSS13 source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} CMSS13 source description");
                        Assert.That(clothing.Slots, Is.EqualTo(row.Slots), $"{row.Id} CMSS13 equip slot mapping");
                        AssertCmss13ArmorStats(entMan, item, row.Id, row.Stats);
                        AssertNonCorrodible(entMan, item);

                        if (row.AllowedStorage)
                            AssertYautjaBaseArmorAllowedStorage(entMan, item, row.Id);

                        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(item), Is.False,
                            $"{row.Id} should not invent ITEM_PREDATOR; CMSS13 flavor gear does not set flags_item.");

                        if (row.Id == "CMUYautjaStoneMask")
                        {
                            Assert.That(entMan.HasComponent<YautjaMaskComponent>(item), Is.False,
                                "CMSS13 /obj/item/clothing/mask/yautja_flavor is explicitly non-functional and is not a gas/yautja mask subtype.");
                            Assert.That(entMan.HasComponent<YautjaMaskAccessoryHolderComponent>(item), Is.False,
                                "CMSS13 stone flavor mask does not inherit valid_accessory_slots from /obj/item/clothing/mask/gas/yautja.");
                        }

                        if (row.AntiHugMaxCount is { } antiHug)
                        {
                            Assert.That(entMan.TryGetComponent<ParasiteResistanceComponent>(item, out var resistance), Is.True,
                                $"{row.Id} should keep the local mask anti-hug behavior if inherited from the mask base.");
                            Assert.That(resistance!.MaxCount, Is.EqualTo(antiHug), $"{row.Id} local anti-hug count");
                        }
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            foreach (var row in rows)
                AssertPrototypeIconState(prototypes, factory, row.Id, row.SpritePath, row.IconState);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScalableRepairReinforcedTextAndKnifeGreavesSpawnItemMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var scalableIds = Cmss13ScalableRepairPrototypeIds().ToArray();
        var knifeGreavesIds = Cmss13KnifeGreavesPrototypeIds().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var itemSlotsSystem = entMan.System<ItemSlotsSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var spawned = scalableIds
                .Concat(knifeGreavesIds)
                .Distinct()
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var id in scalableIds)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == id);

                        Assert.That(entMan.TryGetComponent<YautjaScalableRepairComponent>(item, out var repair), Is.True,
                            $"{id} should represent the CMSS13 repair_status var.");
                        repair!.Status = YautjaScalableRepairStatus.Reinforced;

                        var examineText = examine.GetExamineText(item, hunter).ToMarkup();
                        Assert.That(examineText, Does.Contain("It has been reinforced to be more protective."),
                            $"{id} should expose the CMSS13 YAUTJA_REPAIR_REINFORCED examine line.");
                    }

                    foreach (var id in knifeGreavesIds)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == id);

                        Assert.That(entMan.TryGetComponent<CMItemSlotsComponent>(item, out var slots), Is.True,
                            $"{id} should map CMSS13 spawn_item_type = /obj/item/weapon/yautja/knife to a local filled item slot.");
                        Assert.That(slots!.StartingItem, Is.EqualTo(new EntProtoId("CMUYautjaDuellingKnife")),
                            $"{id} should start with the local /obj/item/weapon/yautja/knife equivalent.");
                        Assert.That(entMan.TryGetComponent<ItemSlotsComponent>(item, out var itemSlots), Is.True,
                            $"{id} should expose the source boot knife through an item slot.");
                        Assert.That(itemSlotsSystem.TryGetSlot(item, "item", out var itemSlot, itemSlots), Is.True,
                            $"{id} should expose the source boot knife slot.");
                        Assert.That(itemSlot!.ContainerSlot?.ContainedEntity, Is.Not.Null,
                            $"{id} should spawn the source boot knife item into its slot.");

                        var contained = itemSlot.ContainerSlot!.ContainedEntity!.Value;
                        var containedId = entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID;
                        Assert.That(containedId, Is.EqualTo("CMUYautjaDuellingKnife"),
                            $"{id} should contain the local /obj/item/weapon/yautja/knife equivalent.");
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned.Append(hunter))
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaGreavesAllowedItemSlotsMatchCmss13Typecache()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var hunterGreavesIds = Cmss13HunterAllowedItemGreavesPrototypeIds().ToArray();
        var thrallGreavesIds = Cmss13ThrallAllowedItemGreavesPrototypeIds().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var itemSlotsSystem = entMan.System<ItemSlotsSystem>();
            var user = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var spawned = new List<EntityUid> { user };

            try
            {
                foreach (var id in hunterGreavesIds)
                    AssertGreavesSlotMatchesCmss13Typecache(
                        entMan,
                        itemSlotsSystem,
                        user,
                        id,
                        new[] { "CMUYautjaDuellingKnife", "CMUYautjaPlasmaPistol" },
                        new[] { "RMCM5Bayonet", "RMCWeaponPistolM77" },
                        map.GridCoords,
                        spawned);

                foreach (var id in thrallGreavesIds)
                    AssertGreavesSlotMatchesCmss13Typecache(
                        entMan,
                        itemSlotsSystem,
                        user,
                        id,
                        new[]
                        {
                            "RMCM5Bayonet",
                            "CMM11Knife",
                            "CMWeaponPistolM1984",
                            "RMCWeaponPistolB92FS",
                            "RMCStraightRazor",
                            "CMUYautjaDuellingKnife",
                        },
                        new[] { "CMUYautjaPlasmaPistol", "RMCWeaponPistolM77" },
                        map.GridCoords,
                        spawned);
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertGreavesSlotMatchesCmss13Typecache(
        IEntityManager entMan,
        ItemSlotsSystem itemSlotsSystem,
        EntityUid user,
        string greavesId,
        IReadOnlyCollection<string> acceptedIds,
        IReadOnlyCollection<string> rejectedIds,
        EntityCoordinates coordinates,
        ICollection<EntityUid> spawned)
    {
        var greaves = entMan.SpawnEntity(greavesId, coordinates);
        spawned.Add(greaves);

        Assert.That(entMan.TryGetComponent<ItemSlotsComponent>(greaves, out var itemSlots), Is.True,
            $"{greavesId} should map CMSS13 allowed_items_typecache to its local greaves item slot.");
        Assert.That(itemSlotsSystem.TryGetSlot(greaves, "item", out var slot, itemSlots), Is.True,
            $"{greavesId} should expose the CMSS13 boot-storage slot.");
        Assert.That(slot!.Whitelist, Is.Not.Null,
            $"{greavesId} should restrict the greaves item slot to the CMSS13 allowed_items_typecache.");

        if (slot.ContainerSlot?.ContainedEntity is { })
        {
            Assert.That(itemSlotsSystem.TryEject(greaves, slot, null, out var startingItem), Is.True,
                $"{greavesId} should allow removing its starting source knife before insertion checks.");
            if (startingItem is { } uid)
                spawned.Add(uid);
        }

        foreach (var acceptedId in acceptedIds)
        {
            var item = entMan.SpawnEntity(acceptedId, coordinates);
            spawned.Add(item);

            Assert.That(itemSlotsSystem.TryInsert(greaves, "item", item, user, itemSlots), Is.True,
                $"{greavesId} should accept {acceptedId} from the CMSS13 allowed_items_typecache local mapping.");
            Assert.That(itemSlotsSystem.TryEject(greaves, "item", null, out var ejected, itemSlots), Is.True,
                $"{greavesId} should eject {acceptedId} after validating insertion.");
            Assert.That(ejected, Is.EqualTo(item));
        }

        foreach (var rejectedId in rejectedIds)
        {
            var item = entMan.SpawnEntity(rejectedId, coordinates);
            spawned.Add(item);

            Assert.That(itemSlotsSystem.TryInsert(greaves, "item", item, user, itemSlots), Is.False,
                $"{greavesId} should reject {rejectedId} because it is outside the CMSS13 allowed_items_typecache local mapping.");
        }
    }

    [Test]
    public async Task MainHuntingSwordPrototypesMatchCmss13SourceStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var swords = new[]
            {
                entMan.SpawnEntity("CMUYautjaClanSword", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaRendingSword", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPiercingSword", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaSeveringSword", MapCoordinates.Nullspace),
            };

            try
            {
                AssertCmss13MainHuntingSword(entMan, swords[0], "clan sword");
                AssertCmss13MainHuntingSword(entMan, swords[1], "rending sword");
                AssertCmss13MainHuntingSword(entMan, swords[2], "piercing sword");
                AssertCmss13MainHuntingSword(entMan, swords[3], "severing sword");
            }
            finally
            {
                foreach (var sword in swords)
                {
                    if (!entMan.Deleted(sword))
                        entMan.DeleteEntity(sword);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClanShieldPrototypeMatchesCmss13SourceDescription()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shield = entMan.SpawnEntity("CMUYautjaClanShield", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(shield);
                var clothing = entMan.GetComponent<ClothingComponent>(shield);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("clan shield"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A large tribal shield made of a strange metal alloy. The face of the shield bears three skulls, two human, one alien."));
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK));
                });
            }
            finally
            {
                if (!entMan.Deleted(shield))
                    entMan.DeleteEntity(shield);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RackedMeleeAndShieldPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var weaponRows = Cmss13RackedMeleeRows().ToArray();
        var shieldRows = Cmss13RackedShieldRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = weaponRows
                .Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace))
                .Concat(shieldRows.Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace)))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in weaponRows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        AssertCmss13RackedMeleeStaticFacts(entMan, item, row);
                    }

                    foreach (var row in shieldRows)
                    {
                        var shield = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        AssertCmss13RackedShieldStaticFacts(entMan, shield, row);
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemainingYautjaWeaponVisualSoundAndEmbedFactsMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var visualRows = Cmss13WeaponVisualSoundRows().ToArray();
        var noEmbedRows = Cmss13WeaponNoEmbedRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = visualRows
                .Select(row => row.Id)
                .Concat(noEmbedRows.Select(row => row.Id))
                .Append("CMUYautjaHunterSpear")
                .Distinct()
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in visualRows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        AssertCmss13WeaponHeldSoundFacts(entMan, item, row);
                    }

                    foreach (var row in noEmbedRows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        AssertCmss13ThrownNoEmbed(entMan, item, row);
                    }

                    var spear = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == "CMUYautjaHunterSpear");
                    AssertCmss13HunterSpearRemainderFacts(entMan, spear);
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var row in visualRows)
                    AssertCmss13WeaponSpriteFacts(prototypes, factory, row);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavyClanArmorPrototypeMatchesCmss13FullArmorBasicFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var armor = entMan.SpawnEntity("CMUYautjaHeavyClanArmor", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(armor);
                var clothing = entMan.GetComponent<ClothingComponent>(armor);
                var speed = entMan.GetComponent<ClothingSpeedModifierComponent>(armor);
                var heldSpeed = entMan.GetComponent<HeldSpeedModifierComponent>(armor);
                var cmArmor = entMan.GetComponent<CMArmorComponent>(armor);
                var hasTech = entMan.TryGetComponent<YautjaTechItemComponent>(armor, out var tech);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("heavy clan armor"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A suit of armor with heavy padding. It looks old, yet functional."));
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.OUTERCLOTHING));
                    Assert.That(speed.WalkModifier, Is.EqualTo(0.75f),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets slowdown = 0.75.");
                    Assert.That(speed.SprintModifier, Is.EqualTo(0.75f),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets slowdown = 0.75.");
                    Assert.That(heldSpeed.MirrorClothingModifier, Is.True,
                        "Held heavy armor should keep the same local slowdown profile while carried.");
                    Assert.That(cmArmor.Melee, Is.EqualTo(50),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full armor_melee = CLOTHING_ARMOR_HIGH.");
                    Assert.That(cmArmor.Bullet, Is.EqualTo(50),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full armor_bullet = CLOTHING_ARMOR_HIGH.");
                    Assert.That(cmArmor.Bio, Is.EqualTo(50),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full armor_bio = CLOTHING_ARMOR_HIGH.");
                    Assert.That(cmArmor.ExplosionArmor, Is.EqualTo(55),
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full armor_bomb = CLOTHING_ARMOR_HIGHPLUS.");
                    Assert.That(hasTech, Is.True,
                        "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                    if (tech != null)
                    {
                        Assert.That(tech.BlockPickup, Is.True,
                            "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                        Assert.That(tech.BlockUse, Is.True,
                            "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                        Assert.That(tech.BlockMelee, Is.True,
                            "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                        Assert.That(tech.BlockThrow, Is.True,
                            "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                        Assert.That(tech.BlockShoot, Is.True,
                            "CMSS13 /obj/item/clothing/suit/armor/yautja/hunter/full sets flags_item = ITEM_PREDATOR.");
                    }
                    AssertNonCorrodible(entMan, armor);

                    var storage = entMan.GetComponent<AllowSuitStorageComponent>(armor);
                    Assert.That(storage.Whitelist.Components, Is.Not.Null,
                        "CMSS13 heavy clan armor allowed list maps to local suit-storage weapon components.");
                    Assert.That(storage.Whitelist.Components!, Does.Contain("Gun"),
                        "CMSS13 heavy clan armor allowed includes spike launchers and Yautja energy guns.");
                    Assert.That(storage.Whitelist.Components!, Does.Contain("MeleeWeapon"),
                        "CMSS13 heavy clan armor allowed includes /obj/item/weapon/yautja and /obj/item/weapon/twohanded/yautja.");
                    Assert.That(storage.Whitelist.Tags, Is.Not.Null,
                        "CMSS13 heavy clan armor allowed Yautja weapon subtypes should keep local weapon tag equivalents.");
                    Assert.That(storage.Whitelist.Tags!, Does.Contain("Knife"),
                        "CMSS13 heavy clan armor allowed includes /obj/item/weapon/yautja through the knife subtype.");
                    Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("Flashlight"),
                        "CMSS13 heavy clan armor allowed list does not include generic flashlight storage.");
                    Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCMacheteScabbard"),
                        "CMSS13 heavy clan armor allowed list does not include local generic machete scabbards.");
                    Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCScabbardKatana"),
                        "CMSS13 heavy clan armor allowed list does not include local generic katana scabbards.");
                });
            }
            finally
            {
                if (!entMan.Deleted(armor))
                    entMan.DeleteEntity(armor);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaArmorMaskAndGreavesFireIntensityResistanceMatchesCmss13Source()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = Cmss13FireIntensityResistanceRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = rows
                .Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        Assert.That(entMan.TryGetComponent<RMCImmuneToIgnitionComponent>(item, out var immunity), Is.True,
                            $"{row.Id} should map CMSS13 fire_intensity_resistance to local ignition immunity.");
                        Assert.That(immunity!.IntensityResistance, Is.EqualTo(row.IntensityResistance),
                            $"{row.Id} CMSS13 fire_intensity_resistance local mapping.");
                        Assert.That(immunity.ImmuneToDirectHits, Is.False,
                            $"{row.Id} CMSS13 fire_intensity_resistance should not block direct flamer/projectile ignition.");
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultMandatoryArmorAndMeshProtectionMatchesCmss13InheritedFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = Cmss13AdultMandatoryArmorAndMeshRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = rows
                .Select(row => entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var item = spawned.Single(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == row.Id);
                        var meta = entMan.GetComponent<MetaDataComponent>(item);
                        var clothing = entMan.GetComponent<ClothingComponent>(item);

                        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} CMSS13 source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} CMSS13 source description");
                        Assert.That(clothing.Slots, Is.EqualTo(row.Slots), $"{row.Id} CMSS13 equip slot mapping");
                        AssertCmss13ArmorStats(entMan, item, row.Id, row.Stats);
                        AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, item, row.Id, row.BlockPickup);

                        if (row.SourceUnacidable)
                            AssertNonCorrodible(entMan, item);

                        if (row.AntiHugMaxCount is { } antiHug)
                        {
                            Assert.That(entMan.TryGetComponent<ParasiteResistanceComponent>(item, out var resistance), Is.True,
                                $"{row.Id} CMSS13 anti_hug should map to local parasite resistance.");
                            Assert.That(resistance!.MaxCount, Is.EqualTo(antiHug),
                                $"{row.Id} CMSS13 anti_hug local mapping.");
                        }

                        if (row.SourceArmorAllowedList)
                        {
                            var storage = entMan.GetComponent<AllowSuitStorageComponent>(item);
                            Assert.That(storage.Whitelist.Components, Is.Not.Null,
                                $"{row.Id} CMSS13 armor allowed list maps to local suit-storage weapon components.");
                            Assert.That(storage.Whitelist.Components!, Does.Contain("Gun"),
                                $"{row.Id} CMSS13 armor allowed includes spike launchers and Yautja energy guns.");
                            Assert.That(storage.Whitelist.Components!, Does.Contain("MeleeWeapon"),
                                $"{row.Id} CMSS13 armor allowed includes Yautja melee weapons.");
                            Assert.That(storage.Whitelist.Tags, Is.Not.Null,
                                $"{row.Id} CMSS13 armor allowed Yautja weapon subtypes should keep local weapon tag equivalents.");
                            Assert.That(storage.Whitelist.Tags!, Does.Contain("Knife"),
                                $"{row.Id} CMSS13 armor allowed includes /obj/item/weapon/yautja through the knife subtype.");
                            Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("Flashlight"),
                                $"{row.Id} CMSS13 armor allowed list does not include generic flashlight storage.");
                            Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCMacheteScabbard"),
                                $"{row.Id} CMSS13 armor allowed list does not include local generic machete scabbards.");
                            Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCScabbardKatana"),
                                $"{row.Id} CMSS13 armor allowed list does not include local generic katana scabbards.");
                        }
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularScalableAndBadBloodArmorAllowedStorageMatchesCmss13BaseArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = Cmss13BaseYautjaArmorAllowedStorageIds().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = rows
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var item in spawned)
                    {
                        var id = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype!.ID;
                        AssertYautjaBaseArmorAllowedStorage(entMan, item, id);
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodArmorSetPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var rows = Cmss13BadBloodArmorSetRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var spawned = rows
                .SelectMany(row => new[] { row.ArmorId, row.MaskId, row.GreavesId })
                .Append("CMUYautjaBadBloodArmorBane")
                .Append("CMUYautjaMaskBadBloodBane")
                .Append("CMUYautjaBadBloodGreavesBane")
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        AssertBadBloodArmorPiece(entMan, examine, hunter, row.ArmorId, row.ArmorName, row.ArmorDescription, SlotFlags.OUTERCLOTHING, row.ArmorSprite, row.ArmorStats);
                        AssertBadBloodArmorPiece(entMan, examine, hunter, row.MaskId, row.MaskName, row.MaskDescription, SlotFlags.MASK | SlotFlags.SUITSTORAGE, row.MaskSprite, row.MaskStats, antiHugMaxCount: 30, blockPickup: false);
                        AssertBadBloodArmorPiece(entMan, examine, hunter, row.GreavesId, row.GreavesName, row.GreavesDescription, SlotFlags.FEET, row.GreavesSprite, row.GreavesStats);
                    }

                    Assert.That(
                        spawned.Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                        Does.Contain("CMUYautjaBadBloodArmorBane")
                            .And.Contain("CMUYautjaMaskBadBloodBane")
                            .And.Contain("CMUYautjaBadBloodGreavesBane"),
                        "CMSS13 defines Bad Blood Bane item subtypes, but keeps the Bane Armor rack row commented out.");
                });
            }
            finally
            {
                foreach (var uid in spawned.Append(hunter))
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var row in rows)
                {
                    AssertPrototypeIconState(prototypes, factory, row.ArmorId, row.ArmorSprite);
                    AssertPrototypeIconState(prototypes, factory, row.MaskId, row.MaskSprite);
                    AssertPrototypeIconState(prototypes, factory, row.GreavesId, row.GreavesSprite);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodEmissaryCamoVariantsMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var rows = Cmss13BadBloodEmissaryCamoRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = new List<EntityUid>();

            try
            {
                AssertBundle(prototypes, entMan, "CMUYautjaBadBloodArmorEmissaryBundle",
                [
                    "CMUYautjaEmissaryArmorCamoConforming",
                    "CMUYautjaMaskBadBloodEmissaryClassic",
                    "CMUYautjaEmissaryGreavesCamoConforming",
                ]);

                foreach (var row in rows)
                {
                    var armor = SpawnAndTrack(entMan, row.ArmorId, spawned);
                    var mask = SpawnAndTrack(entMan, row.MaskId, spawned);
                    var greaves = SpawnAndTrack(entMan, row.GreavesId, spawned);

                    AssertBadBloodEmissaryPiece(
                        entMan,
                        armor,
                        row.ArmorId,
                        "YM4 pattern clan armor",
                        "A suit of oversized armor built from M3 pattern plating and Smart-Gunner mesh, built for something larger than any normal man.",
                        SlotFlags.OUTERCLOTHING);
                    AssertBadBloodEmissaryPiece(
                        entMan,
                        mask,
                        row.MaskId,
                        "clan mask",
                        "A beautifully designed metallic face mask, both ornate and functional.",
                        SlotFlags.MASK | SlotFlags.SUITSTORAGE,
                        blockPickup: false);
                    AssertBadBloodEmissaryPiece(
                        entMan,
                        greaves,
                        row.GreavesId,
                        "clan combat boots",
                        "A pair of armored boots modified with human armor plating, though still scaled to fit a hunter.",
                        SlotFlags.FEET);
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var row in rows)
                {
                    AssertPrototypeIconState(prototypes, factory, row.ArmorId, row.ArmorSprite);
                    AssertPrototypeIconState(prototypes, factory, row.MaskId, row.MaskSprite);
                    AssertPrototypeIconState(prototypes, factory, row.GreavesId, row.GreavesSprite);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodEmissaryCamoConformingUsesCurrentMapCamouflageLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var cases = Cmss13BadBloodEmissaryRuntimeCamoRows().ToArray();

        foreach (var (mapCamo, expectedArmorSprite, expectedGreavesSprite) in cases)
        {
            EntityUid armor = default;
            EntityUid greaves = default;

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var camoSystem = entMan.System<ItemCamouflageSystem>();
                camoSystem.CurrentMapCamouflage = mapCamo;

                armor = entMan.SpawnEntity("CMUYautjaEmissaryArmorCamoConforming", MapCoordinates.Nullspace);
                greaves = entMan.SpawnEntity("CMUYautjaEmissaryGreavesCamoConforming", MapCoordinates.Nullspace);
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var appearance = entMan.System<SharedAppearanceSystem>();

                AssertCamoConformingRuntimeSprite(entMan, appearance, armor, mapCamo, expectedArmorSprite);
                AssertCamoConformingRuntimeSprite(entMan, appearance, greaves, mapCamo, expectedGreavesSprite);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                if (armor != default && !entMan.Deleted(armor))
                    entMan.DeleteEntity(armor);

                if (greaves != default && !entMan.Deleted(greaves))
                    entMan.DeleteEntity(greaves);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallArmorMaterialPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var rows = Cmss13ThrallArmorMaterialRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var spawned = rows
                .SelectMany(row => new[] { row.ArmorId, row.MaskId, row.GreavesId })
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        AssertBundle(prototypes, entMan, row.BundleId, new[]
                        {
                            row.ArmorId,
                            row.GreavesId,
                            row.MaskId,
                        });

                        AssertBadBloodArmorPiece(entMan, row.ArmorId, row.ArmorName, row.ArmorDescription, SlotFlags.OUTERCLOTHING, row.ArmorSprite);
                        AssertBadBloodArmorPiece(entMan, row.MaskId, row.MaskName, row.MaskDescription, SlotFlags.MASK | SlotFlags.SUITSTORAGE, row.MaskSprite, blockPickup: false);
                        AssertBadBloodArmorPiece(entMan, row.GreavesId, row.GreavesName, row.GreavesDescription, SlotFlags.FEET, row.GreavesSprite);
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var row in rows)
                {
                    AssertPrototypeIconState(prototypes, factory, row.ArmorId, row.ArmorSprite);
                    AssertPrototypeIconState(prototypes, factory, row.MaskId, row.MaskSprite);
                    AssertPrototypeIconState(prototypes, factory, row.GreavesId, row.GreavesSprite);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallArmorMaterialProtectionAndAllowedStorageMatchCmss13Source()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var rows = Cmss13ThrallArmorMaterialRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = rows
                .SelectMany(row => new[] { row.ArmorId, row.MaskId, row.GreavesId })
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        var armor = EntityPrototypeIds(entMan, row.ArmorId).Single();
                        var mask = EntityPrototypeIds(entMan, row.MaskId).Single();
                        var greaves = EntityPrototypeIds(entMan, row.GreavesId).Single();

                        AssertCmss13ArmorStats(entMan, armor, row.ArmorId, row.ArmorStats);
                        AssertCmss13ArmorStats(entMan, mask, row.MaskId, row.MaskStats);
                        AssertCmss13ArmorStats(entMan, greaves, row.GreavesId, row.GreavesStats);
                        Assert.That(entMan.GetComponent<ParasiteResistanceComponent>(mask).MaxCount, Is.EqualTo(row.MaskAntiHugMaxCount),
                            $"{row.MaskId} inherits CMSS13 /obj/item/clothing/mask/gas/yautja anti_hug = 5.");

                        var storage = entMan.GetComponent<AllowSuitStorageComponent>(armor);
                        Assert.That(storage.Whitelist.Components, Is.Not.Null, $"{row.ArmorId} CMSS13 allowed list maps to local suit-storage weapon components.");
                        Assert.That(storage.Whitelist.Components!, Does.Contain("Gun"), $"{row.ArmorId} CMSS13 allowed includes yautja guns and spike launchers.");
                        Assert.That(storage.Whitelist.Components!, Does.Contain("MeleeWeapon"), $"{row.ArmorId} CMSS13 allowed includes /obj/item/weapon.");
                        Assert.That(storage.Whitelist.Tags, Is.Not.Null, $"{row.ArmorId} CMSS13 allowed /obj/item/weapon should keep local weapon tag equivalents.");
                        Assert.That(storage.Whitelist.Tags!, Does.Contain("Knife"), $"{row.ArmorId} CMSS13 allowed includes /obj/item/weapon/yautja/knife through /obj/item/weapon.");
                    }
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallBracerMaterialPrototypesMatchCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        var rows = Cmss13BloodedThrallBracerMaterialRows().ToArray();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var bracers = rows
                .Select(row => entMan.SpawnEntity(row.BracerId, MapCoordinates.Nullspace))
                .ToArray();
            var wristBlades = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", MapCoordinates.Nullspace);
            var spawned = bracers
                .Append(wristBlades)
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var row in rows)
                    {
                        AssertBundle(prototypes, entMan, row.BundleId, new[]
                        {
                            row.BracerId,
                            "CMUYautjaWristBladesAttachment",
                            "CMUYautjaWristBladesAttachment",
                        });

                        AssertBloodedThrallBracerPiece(entMan, row.BracerId, row.Sprite, row.State);
                    }

                    var wristMeta = entMan.GetComponent<MetaDataComponent>(wristBlades);
                    var wristStored = entMan.GetComponent<YautjaStoredGearComponent>(wristBlades);
                    Assert.That(wristMeta.EntityName, Is.EqualTo("wristblade bracer attachment"),
                        "CMSS13 /obj/item/bracer_attachments/wristblades source name.");
                    Assert.That(wristMeta.EntityDescription, Is.EqualTo("A pair of huge, serrated blades."),
                        "CMSS13 /obj/item/bracer_attachments/wristblades source description.");
                    Assert.That(wristStored.Kind, Is.EqualTo(YautjaGearKind.WristBlades),
                        "CMSS13 /obj/item/bracer_attachments/wristblades installs the wrist blade attached_weapon_type.");
                    Assert.That(wristStored.DeployedPrototype?.Id, Is.EqualTo("CMUYautjaWristBlades"),
                        "CMSS13 /obj/item/bracer_attachments/wristblades should point at the deployed bracer weapon.");
                    Assert.That(wristStored.Deployed, Is.False,
                        "CMSS13 /obj/item/bracer_attachments/wristblades is the holder item, not the deployed weapon.");
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(wristBlades), Is.False,
                        "CMSS13 /obj/item/bracer_attachments/wristblades holder does not set flags_item = ITEM_PREDATOR.");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var row in rows)
                    AssertPrototypeIconState(prototypes, factory, row.BracerId, row.Sprite, row.State);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpikeLauncherPrototypeMatchesCmss13BasicGunFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(launcher);
                var item = entMan.GetComponent<ItemComponent>(launcher);
                var clothing = entMan.GetComponent<ClothingComponent>(launcher);
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);
                var gun = entMan.GetComponent<GunComponent>(launcher);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("spike launcher"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A compact Yautja device in the shape of a crescent. It can rapidly fire damaging spikes and automatically recharges."));
                    Assert.That(item.Size.Id, Is.EqualTo("Normal"),
                        "CMSS13 /obj/item/weapon/gun/launcher/spike sets w_class = SIZE_MEDIUM.");
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BELT | SlotFlags.BACK),
                        "CMSS13 /obj/item/weapon/gun/launcher/spike sets flags_equip_slot = SLOT_WAIST|SLOT_BACK.");
                    Assert.That(ammo.Proto, Is.EqualTo("CMUYautjaSpikeProjectile"));
                    Assert.That(ammo.Capacity, Is.EqualTo(12));
                    Assert.That(ammo.Count, Is.EqualTo(12));
                    Assert.That(recharge.RechargeCooldown, Is.EqualTo(10f),
                        "CMSS13 spike launcher regenerates after world.time > last_regen + 100, i.e. 10 seconds.");
                    Assert.That(recharge.RechargeChance, Is.EqualTo(0.70f),
                        "CMSS13 spike launcher process gates each due regeneration attempt with prob(70).");
                    Assert.That(recharge.StartWithCooldown, Is.True,
                        "CMSS13 spike launcher Initialize() sets last_regen = world.time.");
                    Assert.That(recharge.StrictCooldownBoundary, Is.True,
                        "CMSS13 spike launcher process uses world.time > last_regen + 100, not equality.");
                    Assert.That(recharge.AdvanceOnFailedRecharge, Is.False,
                        "CMSS13 failed prob(70) rolls leave last_regen unchanged.");
                    Assert.That(recharge.PreserveCooldownWhenFull, Is.True,
                        "CMSS13 tracks last_regen independently from current spike count.");
                    Assert.That(recharge.ResetOverdueCooldown, Is.False,
                        "CMSS13 firing a spike does not reset an overdue last_regen threshold.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto));
                    AssertSoundPath(gun.SoundGunshot!, "/Audio/_CMU14/Yautja/woodhit.ogg");
                    Assert.That(entMan.HasComponent<WieldableComponent>(launcher), Is.True);
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(launcher), Is.True,
                        "CMSS13 /obj/item/weapon/gun/launcher/spike sets flags_item = ITEM_PREDATOR|TWOHANDED.");
                    AssertNonCorrodible(entMan, launcher);
                });
            }
            finally
            {
                if (!entMan.Deleted(launcher))
                    entMan.DeleteEntity(launcher);
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
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount(launcher, 11, ammo), Is.True);
                recharge.RechargeChance = 1f;
                entMan.Dirty(launcher, recharge);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);
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
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount(launcher, 11, ammo), Is.True);
                recharge.RechargeChance = 1f;
                dueAtBoundary = timing.CurTime;
                recharge.NextCharge = dueAtBoundary;
                entMan.Dirty(launcher, recharge);
                server.System<RechargeBasicEntityAmmoSystem>().Update(0);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

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
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

                successAttemptStart = timing.CurTime;
                recharge.NextCharge = timing.CurTime - timing.TickPeriod;
                entMan.Dirty(launcher, recharge);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

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
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

                Assert.That(gun.UpdateBasicEntityAmmoCount(launcher, 10, ammo), Is.True);
                recharge.RechargeChance = 0f;
                failedDueAt = timing.CurTime - TimeSpan.FromSeconds(1);
                recharge.NextCharge = failedDueAt;
                entMan.Dirty(launcher, recharge);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

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
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

                recharge.RechargeChance = 1f;
                successAttemptStart = timing.CurTime;
                entMan.Dirty(launcher, recharge);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ammo = entMan.GetComponent<BasicEntityAmmoProviderComponent>(launcher);
                var recharge = entMan.GetComponent<RechargeBasicEntityAmmoComponent>(launcher);

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
    public async Task SpikeLauncherNonYautjaShootDenialMatchesCmss13AbleToFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", map.GridCoords);

            try
            {
                var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;
                var tech = entMan.GetComponent<YautjaTechItemComponent>(launcher);
                var attempt = new AttemptShootEvent(user, null, userCoords, userCoords);

                entMan.EventBus.RaiseLocalEvent(launcher, ref attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(tech.ShootDeniedPopup.Id, Is.EqualTo("cmu-yautja-spike-launcher-denied"));
                    Assert.That(attempt.Cancelled, Is.True);
                    Assert.That(attempt.Message, Is.EqualTo("You have no idea how this thing works!"),
                        "CMSS13 /obj/item/weapon/gun/launcher/spike/able_to_fire() shows this warning to non-Yautja users.");
                });
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                if (!entMan.Deleted(user))
                    entMan.DeleteEntity(user);

                if (!entMan.Deleted(launcher))
                    entMan.DeleteEntity(launcher);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpikeLauncherYautjaExamineShowsCmss13SpikeCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", map.GridCoords);

            try
            {
                var message = examine.GetExamineText(launcher, hunter).ToMarkup();

                Assert.That(message, Does.Contain("It currently has <bold>12/12</bold> spikes."),
                    "CMSS13 /obj/item/weapon/gun/launcher/spike/get_examine_text() shows Yautja users the current spike count.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(launcher))
                    entMan.DeleteEntity(launcher);
            }
        });

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
    public async Task SpikeLauncherNonYautjaExamineMatchesCmss13MechanicalDonut()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var launcher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", map.GridCoords);

            try
            {
                var message = examine.GetExamineText(launcher, human).ToMarkup();

                Assert.That(message, Is.EqualTo("Looks like some kind of...mechanical donut."),
                    "CMSS13 /obj/item/weapon/gun/launcher/spike/get_examine_text() hides the real description from non-Yautja users.");
            }
            finally
            {
                if (!entMan.Deleted(human))
                    entMan.DeleteEntity(human);

                if (!entMan.Deleted(launcher))
                    entMan.DeleteEntity(launcher);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRangedWeaponGunStatsMatchCmss13Defines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spike = entMan.SpawnEntity("CMUYautjaSpikeLauncher", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", MapCoordinates.Nullspace);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", MapCoordinates.Nullspace);

            try
            {
                AssertRangedGunStats(entMan,
                    spike,
                    10f / 6f,
                    6,
                    10,
                    1.25,
                    1,
                    "CMSS13 spike launcher set_gun_config_values(): FIRE_DELAY_TIER_6, SCATTER_AMOUNT_TIER_8/6, BASE_ACCURACY_MULT + HIT_ACCURACY_MULT_TIER_5.");
                AssertRangedGunStats(entMan,
                    rifle,
                    10f / 16f,
                    10,
                    10,
                    1.5,
                    1.5,
                    "CMSS13 plasma rifle set_gun_config_values(): FIRE_DELAY_TIER_4*2, SCATTER_AMOUNT_TIER_6, BASE_ACCURACY_MULT + HIT_ACCURACY_MULT_TIER_10.");
                AssertRangedGunStats(entMan,
                    pistol,
                    10f / 5f,
                    6,
                    10,
                    1.5,
                    1.35,
                    "CMSS13 plasma pistol standard mode: FIRE_DELAY_TIER_7, SCATTER_AMOUNT_TIER_8/6, BASE_ACCURACY_MULT + HIT_ACCURACY_MULT_TIER_10/7.");
                AssertRangedGunStats(entMan,
                    caster,
                    10f / 6f,
                    10,
                    10,
                    1,
                    7,
                    "CMSS13 plasma caster standard stun mode: FIRE_DELAY_TIER_6, SCATTER_AMOUNT_TIER_6, BASE_ACCURACY_MULT and source unwielded BASE_ACCURACY_MULT + FIRE_DELAY_TIER_6.");
            }
            finally
            {
                foreach (var uid in new[] { spike, rifle, pistol, caster })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaPistolPrototypeMatchesCmss13ChargeAndFireModeFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", MapCoordinates.Nullspace);
            var incendiaryBolt = entMan.SpawnEntity("CMUYautjaPlasmaPistolIncendiaryBolt", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(pistol);
                var item = entMan.GetComponent<ItemComponent>(pistol);
                var clothing = entMan.GetComponent<ClothingComponent>(pistol);
                var gun = entMan.GetComponent<GunComponent>(pistol);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(pistol);
                var battery = entMan.GetComponent<BatteryComponent>(pistol);
                var recharge = entMan.GetComponent<BatterySelfRechargerComponent>(pistol);
                var fireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(pistol);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("plasma pistol"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A plasma pistol capable of rapid fire. It has an integrated battery. Can be used to set fires, either to braziers or on people."));
                    Assert.That(item.Size.Id, Is.EqualTo("Normal"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol sets w_class = SIZE_MEDIUM.");
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BELT),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol sets flags_equip_slot = SLOT_WAIST.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaPistolBolt"));
                    Assert.That(ammo.FireCost, Is.EqualTo(1));
                    Assert.That(battery.MaxCharge, Is.EqualTo(40),
                        "CMSS13 plasma pistol has charge_time = 40.");
                    Assert.That(battery.CurrentCharge, Is.EqualTo(40),
                        "CMSS13 plasma pistol starts at its maximum charge_time.");
                    Assert.That(recharge.AutoRecharge, Is.True);
                    Assert.That(recharge.AutoRechargeRate, Is.EqualTo(1),
                        "CMSS13 process() restores one charge_time per tick until 40.");
                    Assert.That(fireModes.FireModes, Has.Count.EqualTo(2));
                    Assert.That(fireModes.FireModes[0].Prototype.Id, Is.EqualTo("CMUYautjaPlasmaPistolBolt"));
                    Assert.That(fireModes.FireModes[0].FireCost, Is.EqualTo(1));
                    Assert.That(fireModes.FireModes[1].Prototype.Id, Is.EqualTo("CMUYautjaPlasmaPistolIncendiaryBolt"));
                    Assert.That(fireModes.FireModes[1].FireCost, Is.EqualTo(5),
                        "CMSS13 incendiary mode sets shot_cost = 5.");
                    Assert.That(entMan.HasComponent<WieldableComponent>(pistol), Is.True);
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(pistol), Is.True,
                        "CMSS13 plasma pistol sets flags_item = ITEM_PREDATOR|IGNITING_ITEM|TWOHANDED.");
                    AssertIncendiaryPayload(entMan,
                        incendiaryBolt,
                        "CMSS13 incendiary plasma pistol mode switches to /datum/ammo/energy/yautja/pistol/incendiary.");
                    AssertNonCorrodible(entMan, pistol);
                });

                var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                try
                {
                    var toggleIncendiary = new UniqueActionEvent(user);
                    entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);

                    Assert.Multiple(() =>
                    {
                        Assert.That(toggleIncendiary.Handled, Is.True);
                        Assert.That(fireModes.CurrentFireMode, Is.EqualTo(1));
                        Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaPistolIncendiaryBolt"));
                        Assert.That(ammo.FireCost, Is.EqualTo(5));
                        Assert.That(gun.FireRate, Is.EqualTo(10f / 7f).Within(0.0001f),
                            "CMSS13 plasma pistol incendiary mode sets fire_delay = FIRE_DELAY_TIER_5.");
                    });

                    var toggleStandard = new UniqueActionEvent(user);
                    entMan.EventBus.RaiseLocalEvent(pistol, toggleStandard);

                    Assert.Multiple(() =>
                    {
                        Assert.That(toggleStandard.Handled, Is.True);
                        Assert.That(fireModes.CurrentFireMode, Is.EqualTo(0));
                        Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaPistolBolt"));
                        Assert.That(ammo.FireCost, Is.EqualTo(1));
                        Assert.That(gun.FireRate, Is.EqualTo(10f / 5f).Within(0.0001f),
                            "CMSS13 plasma pistol standard mode sets fire_delay = FIRE_DELAY_TIER_7.");
                    });
                }
                finally
                {
                    if (!entMan.Deleted(user))
                        entMan.DeleteEntity(user);
                }
            }
            finally
            {
                if (!entMan.Deleted(pistol))
                    entMan.DeleteEntity(pistol);
                if (!entMan.Deleted(incendiaryBolt))
                    entMan.DeleteEntity(incendiaryBolt);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRiflePrototypeMatchesCmss13ChargeAndStabilizerFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(rifle);
                var item = entMan.GetComponent<ItemComponent>(rifle);
                var clothing = entMan.GetComponent<ClothingComponent>(rifle);
                var gun = entMan.GetComponent<GunComponent>(rifle);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(rifle);
                var battery = entMan.GetComponent<BatteryComponent>(rifle);
                var recharge = entMan.GetComponent<BatterySelfRechargerComponent>(rifle);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("plasma rifle"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A long-barreled heavy plasma weapon. Intended for combat, not hunting. Has an integrated battery that allows for a functionally unlimited amount of shots to be discharged. Equipped with an internal gyroscopic stabilizer allowing its operator to fire the weapon one-handed if desired."));
                    Assert.That(item.Size.Id, Is.EqualTo("Huge"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmarifle sets w_class = SIZE_HUGE.");
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmarifle sets flags_equip_slot = SLOT_BACK.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaRifleBolt"));
                    Assert.That(ammo.FireCost, Is.EqualTo(7),
                        "CMSS13 plasma rifle load_into_chamber() subtracts 7 charge_time per shot.");
                    Assert.That(battery.MaxCharge, Is.EqualTo(100),
                        "CMSS13 plasma rifle has charge_time = 100.");
                    Assert.That(battery.CurrentCharge, Is.EqualTo(100),
                        "CMSS13 plasma rifle starts at its maximum charge_time.");
                    Assert.That(recharge.AutoRecharge, Is.True);
                    Assert.That(recharge.AutoRechargeRate, Is.EqualTo(1),
                        "CMSS13 plasma rifle process() restores one charge_time per tick until 100.");
                    Assert.That(entMan.HasComponent<WieldableComponent>(rifle), Is.True,
                        "CMSS13 plasma rifle still has flags_item = ITEM_PREDATOR|TWOHANDED.");
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(rifle), Is.False,
                        "CMSS13 plasma rifle description/source says the gyroscopic stabilizer allows firing one-handed.");
                    AssertNonCorrodible(entMan, rifle);
                });
            }
            finally
            {
                if (!entMan.Deleted(rifle))
                    entMan.DeleteEntity(rifle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCarbinePrototypeMatchesCmss13MilitaryCasteFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(carbine);
                var item = entMan.GetComponent<ItemComponent>(carbine);
                var clothing = entMan.GetComponent<ClothingComponent>(carbine);
                var plasma = entMan.GetComponent<YautjaPlasmaWeaponComponent>(carbine);
                var gun = entMan.GetComponent<GunComponent>(carbine);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(carbine);
                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                var recharge = entMan.GetComponent<BatterySelfRechargerComponent>(carbine);
                var fireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(carbine);
                var wieldable = entMan.GetComponent<WieldableComponent>(carbine);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("plasma carbine"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A short-barreled rapid-fire assault weapon only given to military caste soldiers, unsuitable for hunting actual prey. Features a deadly burst-fire mode, alongside incendiary or impact-explosive rounds. Although more accurate when wielded, it can be fired with one hand."));
                    Assert.That(item.Size.Id, Is.EqualTo("Large"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmacarbine sets w_class = SIZE_LARGE.");
                    Assert.That(item.RsiPath, Is.EqualTo("_CMU14/Yautja/pred_guns_inhands.rsi"),
                        "CMSS13 plasma carbine inherits Yautja pred gun hand visuals.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("plasmacarbine"),
                        "CMSS13 plasma carbine sets item_state = \"plasmacarbine\".");
                    Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmacarbine sets flags_equip_slot = SLOT_BACK.");
                    Assert.That(clothing.RsiPath, Is.EqualTo("_CMU14/Yautja/pred_guns_back.rsi"),
                        "CMSS13 plasma carbine inherits its back-slot item_state from the Yautja pred gun DMI family.");
                    Assert.That(clothing.EquippedPrefix, Is.EqualTo("plasmacarbine"),
                        "CMSS13 plasma carbine uses item_state = \"plasmacarbine\" for back-slot visuals.");
                    Assert.That(plasma.NonYautjaExamineText, Is.EqualTo("This thing looks like a rifle, but there's no mag or proper barrel. What the hell is it?"));
                    Assert.That(plasma.ShowFireMode, Is.True,
                        "CMSS13 plasma carbine get_examine_text() prints its current incendiary/impact-explosive mode for Yautja users.");
                    Assert.That(plasma.MinimumAmmoCharge, Is.EqualTo(1),
                        "CMSS13 plasma carbine has_ammunition() returns true when charge_time >= 1.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto | SelectiveFire.Burst),
                        "CMSS13 plasma carbine keeps burst fire: set_burst_amount(BURST_AMOUNT_TIER_2) and set_burst_delay(FIRE_DELAY_TIER_11).");
                    Assert.That(gun.BurstFireRate, Is.EqualTo(10f / 11f).Within(0.0001f),
                        "CMSS13 plasma carbine burst delay is FIRE_DELAY_TIER_11.");
                    Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaRifleBolt"));
                    Assert.That(ammo.FireCost, Is.EqualTo(1));
                    Assert.That(battery.MaxCharge, Is.EqualTo(40),
                        "CMSS13 plasma carbine has charge_time = 40.");
                    Assert.That(battery.CurrentCharge, Is.EqualTo(40),
                        "CMSS13 plasma carbine starts at its maximum charge_time.");
                    Assert.That(recharge.AutoRecharge, Is.True);
                    Assert.That(recharge.AutoRechargeRate, Is.EqualTo(1),
                        "CMSS13 process() restores one charge_time per tick until 40.");
                    Assert.That(fireModes.FireModes, Has.Count.EqualTo(2));
                    Assert.That(fireModes.FireModes[0].Prototype.Id, Is.EqualTo("CMUYautjaPlasmaRifleBolt"));
                    Assert.That(fireModes.FireModes[0].FireCost, Is.EqualTo(1));
                    Assert.That(fireModes.FireModes[0].PopupText, Is.EqualTo("plasma carbine will now fire incendiary plasma bolts."));
                    Assert.That(fireModes.FireModes[1].Prototype.Id, Is.EqualTo("CMUYautjaCasterLethalBolt"));
                    Assert.That(fireModes.FireModes[1].FireCost, Is.EqualTo(2),
                        "CMSS13 impact-explosive mode doubles shot_cost.");
                    Assert.That(fireModes.FireModes[1].PopupText, Is.EqualTo("plasma carbine will now fire impact-explosive plasma bolts."));
                    Assert.That(entMan.HasComponent<WieldableComponent>(carbine), Is.True,
                        "CMSS13 plasma carbine sets flags_item = ITEM_PREDATOR|TWOHANDED.");
                    Assert.That(wieldable.WieldedInhandPrefix, Is.EqualTo("plasmacarbine"),
                        "CMSS13 plasma carbine does not swap item_state when wielded.");
                    Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(carbine), Is.False,
                        "CMSS13 plasma carbine description/source says it can be fired with one hand.");
                    AssertNonCorrodible(entMan, carbine);
                    AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, carbine, "CMUYautjaPlasmaCarbine");
                });

                var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                try
                {
                    var toggleExplosive = new UniqueActionEvent(user);
                    entMan.EventBus.RaiseLocalEvent(carbine, toggleExplosive);

                    Assert.Multiple(() =>
                    {
                        Assert.That(toggleExplosive.Handled, Is.True);
                        Assert.That(fireModes.CurrentFireMode, Is.EqualTo(1));
                        Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaCasterLethalBolt"));
                        Assert.That(ammo.FireCost, Is.EqualTo(2));
                        Assert.That(gun.FireRate, Is.EqualTo(10f / 6f).Within(0.0001f),
                            "CMSS13 plasma carbine impact-explosive mode sets fire_delay = FIRE_DELAY_TIER_6.");
                    });

                    var toggleIncendiary = new UniqueActionEvent(user);
                    entMan.EventBus.RaiseLocalEvent(carbine, toggleIncendiary);

                    Assert.Multiple(() =>
                    {
                        Assert.That(toggleIncendiary.Handled, Is.True);
                        Assert.That(fireModes.CurrentFireMode, Is.EqualTo(0));
                        Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaPlasmaRifleBolt"));
                        Assert.That(ammo.FireCost, Is.EqualTo(1));
                        Assert.That(gun.FireRate, Is.EqualTo(10f / 8f).Within(0.0001f),
                            "CMSS13 plasma carbine incendiary mode sets fire_delay = FIRE_DELAY_TIER_8.");
                    });
                }
                finally
                {
                    if (!entMan.Deleted(user))
                        entMan.DeleteEntity(user);
                }
            }
            finally
            {
                if (!entMan.Deleted(carbine))
                    entMan.DeleteEntity(carbine);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var prototype = prototypes.Index<EntityPrototype>("CMUYautjaPlasmaCarbine");

            Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/pred_guns.rsi")),
                    "CMSS13 plasma carbine inherits the Yautja gun icon DMI.");
                Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("plasmacarbine"),
                    "CMSS13 plasma carbine sets icon_state = \"plasmacarbine\".");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCarbineYautjaExamineShowsCmss13ChargeAndMode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);

            try
            {
                var incendiary = examine.GetExamineText(carbine, hunter).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(incendiary, Does.Contain("It currently has <bold>40/40</bold> charge."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmacarbine/get_examine_text() shows Yautja users the current charge.");
                    Assert.That(incendiary, Does.Contain("It is set to fire incendiary plasma bolts."),
                        "CMSS13 plasma carbine source examine names the default incendiary fire mode.");
                });

                var toggleExplosive = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(carbine, toggleExplosive);

                var explosive = examine.GetExamineText(carbine, hunter).ToMarkup();

                Assert.That(explosive, Does.Contain("It is set to fire impact-explosive plasma bolts."),
                    "CMSS13 plasma carbine source examine names the impact-explosive fire mode.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(carbine))
                    entMan.DeleteEntity(carbine);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCarbineRechargingHumsAtMaximumChargeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid carbine = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                var batterySystem = entMan.System<BatterySystem>();
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
                previousAttached = session.AttachedEntity;
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                batterySystem.SetCharge(carbine, 39, battery);
                Assert.That(hands.TryPickupAnyHand(hunter, carbine), Is.True);
            });

            await pair.RunSeconds(1.2f);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Has.Some.Contains("plasma carbine hums as it achieves maximum charge."),
                    "CMSS13 plasma carbine process() shows '[src] hums as it achieves maximum charge.' when charge_time reaches 40 while held by a mob.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, carbine })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRifleAndPistolRechargingHumAtMaximumChargeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid rifle = default;
        EntityUid pistol = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                var batterySystem = entMan.System<BatterySystem>();
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);
                pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);
                previousAttached = session.AttachedEntity;
                server.PlayerMan.SetAttachedEntity(session, hunter);

                batterySystem.SetCharge(rifle, 99, entMan.GetComponent<BatteryComponent>(rifle));
                batterySystem.SetCharge(pistol, 39, entMan.GetComponent<BatteryComponent>(pistol));
                Assert.That(hands.TryPickupAnyHand(hunter, rifle), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, pistol), Is.True);
            });

            await pair.RunSeconds(1.2f);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Has.Some.Contains("plasma rifle hums as it achieves maximum charge."),
                        "CMSS13 plasma rifle process() shows '[src] hums as it achieves maximum charge.' when held by a mob near full charge.");
                    Assert.That(labels, Has.Some.Contains("plasma pistol hums as it achieves maximum charge."),
                        "CMSS13 plasma pistol process() shows '[src] hums as it achieves maximum charge.' when held by a mob near full charge.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, rifle, pistol })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCarbineRuntimeAmmoAndStatTableMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var fireModeSystem = entMan.System<BatteryWeaponFireModesSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", MapCoordinates.Nullspace);
            var explosiveCarbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", MapCoordinates.Nullspace);
            EntityUid? firstProjectile = null;
            EntityUid? secondProjectile = null;

            try
            {
                entMan.EnsureComponent<AccessComponent>(hunter).Tags.Add("CMUAccessYautjaSecure");

                var gun = entMan.GetComponent<GunComponent>(carbine);
                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                var fireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(carbine);
                var coordinates = entMan.GetComponent<TransformComponent>(carbine).Coordinates;

                Assert.Multiple(() =>
                {
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 9f).Within(0.0001f),
                        "CMSS13 plasma carbine set_gun_config_values() starts at FIRE_DELAY_TIER_9 before unique-action mode changes.");
                    AssertRangedGunStats(entMan,
                        carbine,
                        10f / 9f,
                        15,
                        10,
                        1.5,
                        1.4,
                        "CMSS13 plasma carbine set_gun_config_values(): FIRE_DELAY_TIER_9, SCATTER_AMOUNT_TIER_9/6 and BASE_ACCURACY_MULT + HIT_ACCURACY_MULT_TIER_10/8.");
                });

                batterySystem.SetCharge(carbine, 1, battery);

                var incendiaryAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(carbine, incendiaryAmmo);
                firstProjectile = incendiaryAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(firstProjectile, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(firstProjectile!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaPlasmaRifleBolt"),
                        "CMSS13 plasma carbine default mode creates /datum/ammo/energy/yautja/rifle/bolt.");
                    Assert.That(entMan.HasComponent<ServerPointLightComponent>(firstProjectile.Value), Is.True,
                        "CMSS13 load_into_chamber() calls projectile.set_light(1).");
                    Assert.That(battery.CurrentCharge, Is.EqualTo(0),
                        "CMSS13 plasma carbine default shot_cost subtracts 1 after creating the projectile.");
                });

                entMan.DeleteEntity(firstProjectile.Value);
                firstProjectile = null;

                Assert.That(battery.CurrentCharge, Is.EqualTo(1),
                    "CMSS13 delete_bullet(projectile, refund = TRUE) refunds the default shot_cost for an unfired prepared projectile.");

                var explosiveBattery = entMan.GetComponent<BatteryComponent>(explosiveCarbine);
                var explosiveFireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(explosiveCarbine);
                var explosiveCoordinates = entMan.GetComponent<TransformComponent>(explosiveCarbine).Coordinates;
                Assert.That(fireModeSystem.TrySetFireMode(explosiveCarbine, explosiveFireModes, 1), Is.True);
                Assert.That(explosiveFireModes.CurrentFireMode, Is.EqualTo(1));

                batterySystem.SetCharge(explosiveCarbine, 1, explosiveBattery);

                var explosiveAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), explosiveCoordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(explosiveCarbine, explosiveAmmo);
                secondProjectile = explosiveAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(secondProjectile, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(secondProjectile!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaCasterLethalBolt"),
                        "CMSS13 plasma carbine impact-explosive mode creates the caster single-lethal projectile.");
                    Assert.That(entMan.HasComponent<ServerPointLightComponent>(secondProjectile.Value), Is.True,
                        "CMSS13 load_into_chamber() also lights the impact-explosive projectile.");
                    Assert.That(explosiveBattery.CurrentCharge, Is.EqualTo(0),
                        "CMSS13 has_ammunition() allows charge_time >= 1, then load_into_chamber() subtracts the 2-charge explosive shot cost; local battery clamps at zero.");
                });

                entMan.DeleteEntity(secondProjectile.Value);
                secondProjectile = null;

                Assert.That(explosiveBattery.CurrentCharge, Is.EqualTo(1),
                    "CMSS13 delete_bullet(projectile, refund = TRUE) restores the locally clamped charge that was actually spent.");
            }
            finally
            {
                foreach (var uid in new[] { firstProjectile, secondProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, carbine, explosiveCarbine })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCarbinePreparedProjectilesKeepCmss13LiveSideEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var fireModeSystem = entMan.System<BatteryWeaponFireModesSystem>();
            var status = entMan.System<StatusEffectQuerySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new(1, 0)));
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
            var explosiveCarbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
            EntityUid? incendiaryProjectile = null;
            EntityUid? explosiveProjectile = null;

            try
            {
                var battery = entMan.GetComponent<BatteryComponent>(carbine);
                var coordinates = entMan.GetComponent<TransformComponent>(carbine).Coordinates;
                batterySystem.SetCharge(carbine, 1, battery);

                var incendiaryAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(carbine, incendiaryAmmo);
                incendiaryProjectile = incendiaryAmmo.Ammo.Single().Entity;
                Assert.That(incendiaryProjectile, Is.Not.Null);

                var xenoDamage = entMan.GetComponent<DamageableComponent>(xeno);
                Assert.That(xenoDamage.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.Zero));
                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False);

                var hit = new ProjectileHitEvent(new DamageSpecifier(), xeno, hunter);
                entMan.EventBus.RaiseLocalEvent(incendiaryProjectile!.Value, ref hit);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(incendiaryProjectile.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaPlasmaRifleBolt"),
                        "CMSS13 plasma carbine incendiary mode fires /datum/ammo/energy/yautja/rifle/bolt.");
                    AssertIncendiaryPayload(entMan,
                        incendiaryProjectile.Value,
                        "CMSS13 rifle-bolt carbine mode keeps bullet_trait_incendiary.");
                    Assert.That(xenoDamage.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(41.25)),
                        "CMSS13 rifle-bolt carbine mode keeps the rifle xeno extra-burn branch.");
                    Assert.That(status.TryGetTime(xeno, "YautjaInterference", out var time), Is.True,
                        "CMSS13 rifle-bolt carbine mode keeps the rifle xeno interference branch.");
                    Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(TimeSpan.FromSeconds(30)));
                });

                var explosiveBattery = entMan.GetComponent<BatteryComponent>(explosiveCarbine);
                var explosiveFireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(explosiveCarbine);
                var explosiveCoordinates = entMan.GetComponent<TransformComponent>(explosiveCarbine).Coordinates;
                Assert.That(fireModeSystem.TrySetFireMode(explosiveCarbine, explosiveFireModes, 1), Is.True);
                batterySystem.SetCharge(explosiveCarbine, 2, explosiveBattery);

                var explosiveAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), explosiveCoordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(explosiveCarbine, explosiveAmmo);
                explosiveProjectile = explosiveAmmo.Ammo.Single().Entity;
                Assert.That(explosiveProjectile, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(explosiveProjectile!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaCasterLethalBolt"),
                        "CMSS13 plasma carbine impact-explosive mode fires /datum/ammo/energy/yautja/caster/bolt/single_lethal.");
                    AssertExplosionPayload(entMan,
                        explosiveProjectile.Value,
                        50,
                        50,
                        "CMSS13 carbine impact-explosive mode keeps the caster single-lethal cell_explosion payload.");
                });
            }
            finally
            {
                foreach (var uid in new[] { incendiaryProjectile, explosiveProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, xeno, carbine, explosiveCarbine })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavyGelDefoliatorAndFuelTanksMatchCmss13MilitaryCasteFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
            var spawned = new List<EntityUid>();

            try
            {
                var defoliator = SpawnAndTrack(entMan, "CMUYautjaHeavyGelDefoliator", spawned);
                var exDefoliator = SpawnAndTrack(entMan, "CMUYautjaHeavyGelDefoliatorDeathsquad", spawned);
                var tank = SpawnAndTrack(entMan, "CMUYautjaDefoliatorFuelTank", spawned);
                var exTank = SpawnAndTrack(entMan, "CMUYautjaDefoliatorFuelTankDeathsquad", spawned);

                AssertDefoliatorWeapon(
                    entMan,
                    defoliator,
                    "CMUYautjaHeavyGelDefoliator",
                    "CMUYautjaDefoliatorFuelTank");
                AssertDefoliatorWeapon(
                    entMan,
                    exDefoliator,
                    "CMUYautjaHeavyGelDefoliatorDeathsquad",
                    "CMUYautjaDefoliatorFuelTankDeathsquad");

                AssertDefoliatorTank(
                    entMan,
                    solutionSystem,
                    tank,
                    "CMUYautjaDefoliatorFuelTank",
                    "gel defoliator fuel tank",
                    "A high-capacity heat-resistant tank of highly-flammable gel fuel for a heavy defoliator.",
                    "RMCNapalmUT",
                    100);
                AssertDefoliatorTank(
                    entMan,
                    solutionSystem,
                    exTank,
                    "CMUYautjaDefoliatorFuelTankDeathsquad",
                    "gel defoliator fuel tank (EX)",
                    "A high-capacity heat-resistant tank of terrifyingly powerful gelled plasma, capable of burning right through almost anything. Handle with extreme caution.",
                    "RMCNapalmEX",
                    100);
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();

            AssertDefoliatorWeaponPrototypeVisuals(prototypes, factory, "CMUYautjaHeavyGelDefoliator");
            AssertDefoliatorWeaponPrototypeVisuals(prototypes, factory, "CMUYautjaHeavyGelDefoliatorDeathsquad");
            AssertDefoliatorTankPrototypeVisuals(prototypes, factory, "CMUYautjaDefoliatorFuelTank");
            AssertDefoliatorTankPrototypeVisuals(prototypes, factory, "CMUYautjaDefoliatorFuelTankDeathsquad");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavyGelDefoliatorUsesExactCmss13WyAudioFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var defoliator = entMan.SpawnEntity("CMUYautjaHeavyGelDefoliator", MapCoordinates.Nullspace);

            try
            {
                var gun = entMan.GetComponent<GunComponent>(defoliator);
                var flamer = entMan.GetComponent<RMCFlamerAmmoProviderComponent>(defoliator);
                var slots = entMan.GetComponent<ItemSlotsComponent>(defoliator).Slots["gun_magazine"];
                var igniter = entMan.GetComponent<RMCIgniterComponent>(defoliator);
                var fl3 = protoMan.Index<SoundCollectionPrototype>("RMCFlamerFL3Shoot");
                var files = fl3.PickFiles.Select(path => path.ToString()).ToList();

                Assert.Multiple(() =>
                {
                    AssertSoundCollection(gun.SoundGunshot!, "RMCFlamerFL3Shoot");
                    Assert.That(files, Is.EquivalentTo(new[]
                    {
                        "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamethrower1.ogg",
                        "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamethrower2.ogg",
                        "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamethrower3.ogg",
                    }), "CMSS13 heavy gel defoliator fire_sound randomly picks the three WY flamethrower sounds.");
                    AssertSoundPath(igniter.IgniteSound!, "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamer_ignite.ogg");
                    AssertSoundPath(igniter.ExtinguishSound!, "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamer_extinguish.ogg");
                    AssertSoundPath(flamer.DryFireSound!, "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamer_dryfire.ogg");
                    AssertSoundPath(slots.InsertSound!, "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamer_reload.ogg");
                    AssertSoundPath(slots.EjectSound!, "/Audio/_RMC14/Weapons/Guns/Flamer/wy_flamer_unload.ogg");
                });
            }
            finally
            {
                if (!entMan.Deleted(defoliator))
                    entMan.DeleteEntity(defoliator);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavyGelDefoliatorLiveFireUsesCmss13TankRangeAndChemistry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var flamer = entMan.System<SharedRMCFlamerSystem>();
            var spawned = new List<EntityUid>();

            try
            {
                AssertDefoliatorLiveFire(
                    entMan,
                    flamer,
                    SpawnAndTrack(entMan, "CMUYautjaHeavyGelDefoliator", spawned),
                    map.GridCoords,
                    "RMCNapalmUT");
                AssertDefoliatorLiveFire(
                    entMan,
                    flamer,
                    SpawnAndTrack(entMan, "CMUYautjaHeavyGelDefoliatorDeathsquad", spawned),
                    map.GridCoords.Offset(new Vector2(0, 4)),
                    "RMCNapalmEX");
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task McasteWeaponsNonYautjaFireAndExamineWarningsMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
            var defoliator = entMan.SpawnEntity("CMUYautjaHeavyGelDefoliator", map.GridCoords);

            try
            {
                var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;
                var carbineAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                var defoliatorAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);

                entMan.EventBus.RaiseLocalEvent(carbine, ref carbineAttempt);
                entMan.EventBus.RaiseLocalEvent(defoliator, ref defoliatorAttempt);

                Assert.Multiple(() =>
                {
                    Assert.That(examine.GetExamineText(carbine, user).ToMarkup(),
                        Is.EqualTo("This thing looks like a rifle, but there's no mag or proper barrel. What the hell is it?"),
                        "CMSS13 plasma carbine non-Yautja examine hides the true weapon details.");
                    Assert.That(examine.GetExamineText(defoliator, user).ToMarkup(),
                        Is.EqualTo("Looks like some massively fucked up alien flamethrower."),
                        "CMSS13 defoliator non-Yautja examine hides the true weapon details.");
                    Assert.That(carbineAttempt.Cancelled, Is.True);
                    Assert.That(defoliatorAttempt.Cancelled, Is.True);
                    Assert.That(carbineAttempt.Message, Is.EqualTo("The weapon beeps and refuses to fire. Must be some sort of fancy grip safety!"),
                        "CMSS13 plasma carbine able_to_fire() uses the fancy grip safety warning.");
                    Assert.That(defoliatorAttempt.Message, Is.EqualTo("The weapon beeps and refuses to fire. Must be some sort of fancy grip safety!"),
                        "CMSS13 defoliator able_to_fire() uses the same fancy grip safety warning.");
                });
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                foreach (var uid in new[] { user, carbine, defoliator })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualPlasmaCannonsStaticGunConfigMatchesCmss13MilitaryCasteFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var cannons = entMan.SpawnEntity("CMUYautjaDualPlasmaCannons", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(cannons);
                var item = entMan.GetComponent<ItemComponent>(cannons);
                var gun = entMan.GetComponent<GunComponent>(cannons);
                var tech = entMan.GetComponent<YautjaTechItemComponent>(cannons);
                var linked = entMan.GetComponent<YautjaCannonPackLinkedCannonComponent>(cannons);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("dual plasma cannons"));
                    Assert.That(meta.EntityDescription,
                        Is.EqualTo("A pair of powerful, shoulder-mounted energy weapons that are remotely operated via bracers. Unlike normal plasma casters, they only feature one fire mode, and are designed to obliterate most targets without leaving any material behind."));
                    Assert.That(item.Size.Id, Is.EqualTo("Ginormous"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/cannon sets w_class = SIZE_HUGE; local Ginormous is the no-storage huge weapon footprint used by this deployed pack item.");
                    Assert.That(item.RsiPath, Is.EqualTo("_CMU14/Yautja/mcaste_gear.rsi"),
                        "CMSS13 dual plasma cannons hand/back item icons use hunter/mcaste_gear.dmi.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("plasma_cannons"),
                        "CMSS13 dual plasma cannons item_state = \"plasma_cannons\".");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 12f).Within(0.0001f),
                        "CMSS13 dual plasma cannons set fire_delay = FIRE_DELAY_TIER_2 * 6.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto));
                    AssertSoundPath(gun.SoundGunshot!, "/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_fire.wav");
                    Assert.That(tech.BlockPickup, Is.True);
                    Assert.That(tech.ShootDeniedPopup.Id, Is.EqualTo("cmu-yautja-spike-launcher-denied"));
                    Assert.That(linked.Projectile.Id, Is.EqualTo("CMUYautjaCasterLanceBolt"),
                        "CMSS13 source uses /datum/ammo/energy/yautja/caster/lance; local keeps a named lance-equivalent that currently inherits the closest located caster-lethal payload until the exact lance datum is located.");
                    Assert.That(linked.ChargeCost, Is.EqualTo((FixedPoint2) 1000),
                        "CMSS13 dual plasma cannons charge_cost = 1000.");
                });

                AssertRangedGunStats(entMan,
                    cannons,
                    10f / 12f,
                    10,
                    10,
                    1,
                    7,
                    "CMSS13 dual plasma cannons set_gun_config_values(): FIRE_DELAY_TIER_2*6, SCATTER_AMOUNT_TIER_6, BASE_ACCURACY_MULT and source unwielded BASE_ACCURACY_MULT + FIRE_DELAY_TIER_6.");
            }
            finally
            {
                if (!entMan.Deleted(cannons))
                    entMan.DeleteEntity(cannons);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var prototype = prototypes.Index<EntityPrototype>("CMUYautjaDualPlasmaCannons");

            Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/mcaste_gear.rsi")),
                    "CMSS13 dual plasma cannons use icons/obj/items/hunter/mcaste_gear.dmi.");
                Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("plasma_cannons"),
                    "CMSS13 dual plasma cannons icon_state = \"plasma_cannons\".");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRifleYautjaExamineShowsCmss13Charge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);

            try
            {
                var message = examine.GetExamineText(rifle, hunter).ToMarkup();

                Assert.That(message, Does.Contain("It currently has <bold>100/100</bold> charge."),
                    "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmarifle/get_examine_text() shows Yautja users the current charge.");
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
    public async Task PlasmaRifleLowPowerDeniesWithCmss13Warning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var batterySystem = entMan.System<BatterySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);

            try
            {
                var battery = entMan.GetComponent<BatteryComponent>(rifle);
                batterySystem.SetCharge(rifle, 6, battery);

                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(rifle);
                var userCoords = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                var attempt = new AttemptShootEvent(hunter, null, userCoords, userCoords);

                entMan.EventBus.RaiseLocalEvent(rifle, ref attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Shots, Is.EqualTo(0),
                        "CMSS13 plasma rifle cannot load a shot when charge_time < 7.");
                    Assert.That(attempt.Cancelled, Is.True);
                    Assert.That(attempt.Message, Is.EqualTo("The rifle does not have enough power remaining!"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmarifle/able_to_fire() uses this low-power warning.");
                });
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(rifle))
                    entMan.DeleteEntity(rifle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaPistolYautjaExamineShowsCmss13ChargeAndMode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);

            try
            {
                var standard = examine.GetExamineText(pistol, hunter).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(standard, Does.Contain("It currently has <bold>40/40</bold> charge."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol/get_examine_text() shows Yautja users the current charge.");
                    Assert.That(standard, Does.Contain("It is set to fire plasma bolts."),
                        "CMSS13 plasma pistol source examine names the standard fire mode.");
                });

                var toggleIncendiary = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);

                var incendiary = examine.GetExamineText(pistol, hunter).ToMarkup();

                Assert.That(incendiary, Does.Contain("It is set to fire incendiary plasma bolts."),
                    "CMSS13 plasma pistol source examine names the incendiary fire mode.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(pistol))
                    entMan.DeleteEntity(pistol);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaWeaponsNonYautjaExamineMatchesCmss13AlienDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(examine.GetExamineText(rifle, human).ToMarkup(),
                        Is.EqualTo("This thing looks like an alien rifle of some kind. Strange."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmarifle/get_examine_text() hides the true details from non-Yautja users.");
                    Assert.That(examine.GetExamineText(pistol, human).ToMarkup(),
                        Is.EqualTo("This thing looks like an alien gun of some kind. Strange."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol/get_examine_text() hides the true details from non-Yautja users.");
                });
            }
            finally
            {
                foreach (var uid in new[] { human, rifle, pistol })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterPrototypeMatchesCmss13ModeFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(caster);
                var item = entMan.GetComponent<ItemComponent>(caster);
                var gun = entMan.GetComponent<GunComponent>(caster);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(caster);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("plasma caster"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A powerful, shoulder-mounted energy weapon."));
                    Assert.That(item.Size.Id, Is.EqualTo("Huge"),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasma_caster sets w_class = SIZE_HUGE.");
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gun.SoundGunshot, Is.Null,
                        "Local caster plays the CMSS13 per-strength fire sound through YautjaCasterMode.");
                    Assert.That(ammo.Prototype, Is.EqualTo("CMUYautjaCasterStunBolt"),
                        "CMSS13 plasma caster starts with /datum/ammo/energy/yautja/caster/bolt/single_stun.");
                    Assert.That(entMan.HasComponent<UniqueActionComponent>(caster), Is.True,
                        "CMSS13 plasma caster uses use_unique_action() to toggle stun/lethal mode.");
                    Assert.That(casterComp.Modes, Has.Count.EqualTo(4));
                    Assert.That(casterComp.CurrentMode, Is.EqualTo(0));

                    AssertCasterMode(casterComp.Modes[0],
                        "cmu-yautja-caster-mode-stun",
                        "CMUYautjaCasterStunBolt",
                        30,
                        "/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasmacaster_fire.wav");
                    AssertCasterMode(casterComp.Modes[1],
                        "cmu-yautja-caster-mode-immobilizer",
                        "CMUYautjaCasterImmobilizerBolt",
                        150,
                        "/Audio/_CMU14/Yautja/Weapons/Plasma/pulse.wav");
                    AssertCasterMode(casterComp.Modes[2],
                        "cmu-yautja-caster-mode-lethal",
                        "CMUYautjaCasterLethalBolt",
                        100,
                        "/Audio/_CMU14/Yautja/Weapons/Plasma/pred_lasercannon.wav");
                    AssertCasterMode(casterComp.Modes[3],
                        "cmu-yautja-caster-mode-eradicator",
                        "CMUYautjaCasterEradicatorBolt",
                        1000,
                        "/Audio/_CMU14/Yautja/Weapons/Plasma/pulse.wav");
                });
            }
            finally
            {
                if (!entMan.Deleted(caster))
                    entMan.DeleteEntity(caster);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterModeSwitchingMatchesCmss13AttackSelfAndUniqueAction()
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

                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(caster);
                var gun = entMan.GetComponent<GunComponent>(caster);

                AssertCasterState(casterComp, ammo, 0, "CMUYautjaCasterStunBolt");
                Assert.That(gun.FireRate, Is.EqualTo(10f / 6f).Within(0.0001f),
                    "CMSS13 plasma caster stun bolts set fire_delay = FIRE_DELAY_TIER_6.");

                var strengthenStun = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, strengthenStun);
                Assert.Multiple(() =>
                {
                    Assert.That(strengthenStun.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 1, "CMUYautjaCasterImmobilizerBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 80f).Within(0.0001f),
                        "CMSS13 plasma immobilizers set fire_delay = FIRE_DELAY_TIER_2 * 8.");
                });

                var weakenStun = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, weakenStun);
                Assert.Multiple(() =>
                {
                    Assert.That(weakenStun.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 0, "CMUYautjaCasterStunBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 6f).Within(0.0001f));
                });

                var lethalMode = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, lethalMode);
                Assert.Multiple(() =>
                {
                    Assert.That(lethalMode.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 2, "CMUYautjaCasterLethalBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 18f).Within(0.0001f),
                        "CMSS13 plasma bolt sets fire_delay = FIRE_DELAY_TIER_6 * 3.");
                });

                var strengthenLethal = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, strengthenLethal);
                Assert.Multiple(() =>
                {
                    Assert.That(strengthenLethal.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 3, "CMUYautjaCasterEradicatorBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 120f).Within(0.0001f),
                        "CMSS13 plasma eradicator sets fire_delay = FIRE_DELAY_TIER_2 * 12.");
                });

                var weakenLethal = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, weakenLethal);
                Assert.Multiple(() =>
                {
                    Assert.That(weakenLethal.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 2, "CMUYautjaCasterLethalBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 18f).Within(0.0001f));
                });

                var stunMode = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, stunMode);
                Assert.Multiple(() =>
                {
                    Assert.That(stunMode.Handled, Is.True);
                    AssertCasterState(casterComp, ammo, 0, "CMUYautjaCasterStunBolt");
                    Assert.That(gun.FireRate, Is.EqualTo(10f / 6f).Within(0.0001f));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(caster))
                    entMan.DeleteEntity(caster);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterExamineUsesCmss13StrengthText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);

            try
            {
                var stun = examine.GetExamineText(caster, hunter).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(stun, Does.Contain("It is set to fire stun bolts."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasma_caster/get_examine_text() exposes only the current strength.");
                    Assert.That(stun, Does.Not.Contain("Power cost"),
                        "CMSS13 caster examine does not include local power-cost diagnostics.");
                });

                var lethalMode = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, lethalMode);

                var lethal = examine.GetExamineText(caster, hunter).ToMarkup();

                Assert.That(lethal, Does.Contain("It is set to fire plasma bolt."),
                    "CMSS13 caster examine follows strength after use_unique_action() switches to lethal mode.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(caster))
                    entMan.DeleteEntity(caster);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterNonYautjaExamineStillShowsCmss13StrengthText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);

            try
            {
                var stun = examine.GetExamineText(caster, human).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(stun, Does.Contain("It is set to fire stun bolts."),
                        "CMSS13 plasma caster non-Yautja examine keeps the current strength line.");
                    Assert.That(stun, Does.Not.Contain("This thing looks like a rifle, but there's no mag or proper barrel. What the hell is it?"),
                        "CMSS13 plasma caster does not use the plasma rifle hidden-description override.");
                    Assert.That(stun, Does.Not.Contain("This thing looks like an alien gun of some kind. Strange."),
                        "CMSS13 plasma caster does not use the plasma pistol hidden-description override.");
                });

                var lethalMode = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(caster, lethalMode);

                var lethal = examine.GetExamineText(caster, human).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(lethal, Does.Contain("It is set to fire plasma bolt."),
                        "CMSS13 plasma caster non-Yautja examine updates to the active strength after a mode swap.");
                    Assert.That(lethal, Does.Not.Contain("This thing looks like a rifle, but there's no mag or proper barrel. What the hell is it?"));
                    Assert.That(lethal, Does.Not.Contain("This thing looks like an alien gun of some kind. Strange."));
                });
            }
            finally
            {
                foreach (var uid in new[] { human, hunter, caster })
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
                bracerPower.Charge = 130;

                var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;
                var stunAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                entMan.EventBus.RaiseLocalEvent(caster, ref stunAttempt);

                Assert.That(stunAttempt.Cancelled, Is.False,
                    "CMSS13 plasma caster able_to_fire() accepts source-linked users with TRAIT_YAUTJA_TECH, not only Yautja mobs.");

                var stunAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), userCoords, user);
                entMan.EventBus.RaiseLocalEvent(caster, stunAmmo);
                stunProjectile = stunAmmo.Ammo.Single().Entity;
                Assert.That(bracerPower.Charge, Is.EqualTo((FixedPoint2) 100),
                    "CMSS13 plasma_caster/load_into_chamber() drains stun bolt charge_cost = 30 from source bracer.");

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
    public async Task YautjaCapeAccessoryPrototypesMatchCmss13EquipVisualAndUnacidableFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var capes = new Dictionary<string, (string Name, string Sprite)>
            {
                ["CMUYautjaCapeFull"] = ("yautja cape", "_CMU14/Yautja/cape_full.rsi"),
                ["CMUYautjaCapeCeremonial"] = ("yautja ceremonial cape", "_CMU14/Yautja/cape_ceremonial.rsi"),
                ["CMUYautjaCapeThird"] = ("yautja third-cape", "_CMU14/Yautja/cape_third.rsi"),
                ["CMUYautjaCapeHalf"] = ("yautja half-cape", "_CMU14/Yautja/cape_half.rsi"),
                ["CMUYautjaCapeQuarter"] = ("yautja quarter-cape", "_CMU14/Yautja/cape_quarter.rsi"),
                ["CMUYautjaCapePoncho"] = ("yautja poncho", "_CMU14/Yautja/cape_poncho.rsi"),
                ["CMUYautjaCapeDamaged"] = ("yautja damaged cape", "_CMU14/Yautja/cape_damaged.rsi"),
            };

            var spawned = capes.Keys
                .Select(id => entMan.SpawnEntity(id, MapCoordinates.Nullspace))
                .ToArray();

            try
            {
                Assert.Multiple(() =>
                {
                    foreach (var cape in spawned)
                    {
                        var meta = entMan.GetComponent<MetaDataComponent>(cape);
                        var clothing = entMan.GetComponent<ClothingComponent>(cape);
                        var id = meta.EntityPrototype!.ID;
                        var expected = capes[id];

                        Assert.That(meta.EntityName, Is.EqualTo(expected.Name), $"{id} source name");
                        Assert.That(meta.EntityDescription, Is.EqualTo("A battle-worn cape passed down by elder Yautja."), $"{id} source description");
                        Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK), $"{id} source SLOT_BACK mapping");
                        Assert.That(clothing.RsiPath, Is.EqualTo(expected.Sprite), $"{id} CMSS13 icon_state sprite mapping");
                        AssertNonCorrodible(entMan, cape);
                        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(cape, out var tech), Is.True,
                            $"{id} source ITEM_PREDATOR mapping");
                        if (tech != null)
                        {
                            Assert.That(tech.BlockPickup, Is.False, $"{id} cape pickup remains accessory-like");
                            Assert.That(tech.BlockUse, Is.False, $"{id} cape use remains accessory-like");
                            Assert.That(tech.BlockMelee, Is.False, $"{id} cape melee remains accessory-like");
                            Assert.That(tech.BlockThrow, Is.False, $"{id} cape throw remains accessory-like");
                            Assert.That(tech.BlockShoot, Is.False, $"{id} cape shoot remains accessory-like");
                        }
                    }
                });
            }
            finally
            {
                foreach (var cape in spawned)
                {
                    if (!entMan.Deleted(cape))
                        entMan.DeleteEntity(cape);
                }
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var capes = new Dictionary<string, ResPath>
            {
                ["CMUYautjaCapeFull"] = new("/Textures/_CMU14/Yautja/cape_full.rsi"),
                ["CMUYautjaCapeCeremonial"] = new("/Textures/_CMU14/Yautja/cape_ceremonial.rsi"),
                ["CMUYautjaCapeThird"] = new("/Textures/_CMU14/Yautja/cape_third.rsi"),
                ["CMUYautjaCapeHalf"] = new("/Textures/_CMU14/Yautja/cape_half.rsi"),
                ["CMUYautjaCapeQuarter"] = new("/Textures/_CMU14/Yautja/cape_quarter.rsi"),
                ["CMUYautjaCapePoncho"] = new("/Textures/_CMU14/Yautja/cape_poncho.rsi"),
                ["CMUYautjaCapeDamaged"] = new("/Textures/_CMU14/Yautja/cape_damaged.rsi"),
            };

            Assert.Multiple(() =>
            {
                foreach (var (id, spritePath) in capes)
                {
                    var prototype = prototypes.Index<EntityPrototype>(id);

                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(spritePath), $"{id} world sprite RSI");
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("icon"), $"{id} world icon state");
                    Assert.That(sprite.Color, Is.EqualTo(Color.FromHex("#654321")), $"{id} CMSS13 default cape color");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackRangedClaimGroupMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var ranged = vendor.Sections.Single(section => section.Name == "Ranged Equipment (CHOOSE 1)");
                var ids = ranged.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(ranged.Choices, Is.Not.Null);
                Assert.That(ranged.Choices!.Value.Id, Is.EqualTo("CMUYautjaRanged"));
                Assert.That(ranged.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ids, Is.EqualTo(new[]
                {
                    "CMUYautjaPlasmaPistol",
                    "CMUYautjaQuiverStrapFilled",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackClothingAccessoryClaimGroupMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var capes = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                var ids = capes.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(capes.Choices, Is.Not.Null);
                Assert.That(capes.Choices!.Value.Id, Is.EqualTo("CMUYautjaAccessory"));
                Assert.That(capes.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ids, Is.EqualTo(new[]
                {
                    "CMUYautjaCapeQuarter",
                    "CMUYautjaCapeThird",
                    "CMUYautjaCapeHalf",
                    "CMUYautjaCapePoncho",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackCapeRowsUseCmss13AccessoryClaim()
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
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var capes = vendor.Sections[sectionIndex];
                var quarterIndex = capes.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeQuarter");
                Assert.That(quarterIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(capes.Choices, Is.Not.Null);
                Assert.That(capes.Choices!.Value.Id, Is.EqualTo("CMUYautjaAccessory"));
                Assert.That(capes.Choices.Value.Amount, Is.EqualTo(1));

                entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, quarterIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                });

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaCape"), Is.Zero);
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
    public async Task AdultRackBracerAttachmentsSectionMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var attachments = vendor.Sections.Single(section => section.Name == "Bracer Attachments");
                var primary = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var attachmentIds = attachments.Entries.Select(entry => entry.Id.Id).ToArray();
                var attachmentNames = attachments.Entries.Select(entry => entry.Name).ToArray();
                var recommended = attachments.Entries.Select(entry => entry.Recommended).ToArray();
                var primaryIds = primary.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(attachments.Choices, Is.Null);
                Assert.That(attachmentIds, Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaBracerShieldAttachment",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }));
                Assert.That(attachmentNames, Is.EqualTo(new[]
                {
                    "Wrist Blades",
                    "The Compact Shield",
                    "The Fearsome Scimitars",
                    "The Skewering Scimitars",
                    "The Chain Gauntlets",
                }));
                Assert.That(recommended, Is.EqualTo(new[]
                {
                    false,
                    true,
                    true,
                    true,
                    true,
                }));

                Assert.That(primaryIds, Does.Not.Contain("CMUYautjaBracerShieldAttachment"));
                Assert.That(primaryIds, Does.Not.Contain("CMUYautjaScimitar"));
                Assert.That(primaryIds, Does.Not.Contain("CMUYautjaScimitarAlt"));
                Assert.That(primaryIds, Does.Not.Contain("CMUYautjaChainGauntlet"));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackBracerPrimaryAttachmentsUseMainWeaponClaimLikeCmss13()
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
                var bracerSectionIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                var mainSectionIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(bracerSectionIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mainSectionIndex, Is.GreaterThanOrEqualTo(0));

                var bracerSection = vendor.Sections[bracerSectionIndex];
                var mainSection = vendor.Sections[mainSectionIndex];
                var fearsomeIndex = bracerSection.Entries.FindIndex(entry => entry.Id == "CMUYautjaFearsomeScimitarsBundle");
                Assert.That(fearsomeIndex, Is.GreaterThanOrEqualTo(0));

                AssertCmss13PrimaryBracerClaim(bracerSection, "CMUYautjaFearsomeScimitarsBundle");
                AssertCmss13PrimaryBracerClaim(bracerSection, "CMUYautjaSkeweringScimitarsBundle");
                AssertCmss13PrimaryBracerClaim(bracerSection, "CMUYautjaChainGauntletsBundle");
                Assert.That(mainSection.Choices, Is.Not.Null);
                Assert.That(mainSection.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(mainSection.Choices!.Value.Amount, Is.EqualTo(1));

                var scimitars = new CMVendorVendBuiMsg(bracerSectionIndex, fearsomeIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                };
                entMan.EventBus.RaiseLocalEvent(rack, scimitars);

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
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

        static void AssertCmss13PrimaryBracerClaim(CMVendorSection bracerSection, string id)
        {
            var entry = bracerSection.Entries.FirstOrDefault(entry => entry.Id == id);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Choices, Is.Not.Null);
            Assert.That(entry.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
            Assert.That(entry.Choices!.Value.Amount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AdultRackWristBladesAndCompactShieldUseBracerClaimLikeCmss13()
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
                var bracerSectionIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                Assert.That(bracerSectionIndex, Is.GreaterThanOrEqualTo(0));

                var bracerSection = vendor.Sections[bracerSectionIndex];
                var wristIndex = bracerSection.Entries.FindIndex(entry => entry.Id == "CMUYautjaWristBladesBundle");
                Assert.That(wristIndex, Is.GreaterThanOrEqualTo(0));

                AssertCmss13BracerClaim(bracerSection, "CMUYautjaWristBladesBundle");
                AssertCmss13BracerClaim(bracerSection, "CMUYautjaBracerShieldAttachment");

                var wristBlades = new CMVendorVendBuiMsg(bracerSectionIndex, wristIndex, new())
                {
                    Actor = hunter,
                    UiKey = CMAutomatedVendorUI.Key,
                };
                entMan.EventBus.RaiseLocalEvent(rack, wristBlades);

                var user = entMan.GetComponent<CMVendorUserComponent>(hunter);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);
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

        static void AssertCmss13BracerClaim(CMVendorSection bracerSection, string id)
        {
            var entry = bracerSection.Entries.FirstOrDefault(entry => entry.Id == id);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Choices, Is.Not.Null);
            Assert.That(entry.Choices!.Value.Id, Is.EqualTo("CMUYautjaBracer"));
            Assert.That(entry.Choices!.Value.Amount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AdultRackBracerBundlesMatchCmss13MultiItemRows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            AssertBundle(prototypes, entMan, "CMUYautjaWristBladesBundle", new[]
            {
                "CMUYautjaWristBladesAttachment",
                "CMUYautjaWristBladesAttachment",
            });
            AssertBundle(prototypes, entMan, "CMUYautjaFearsomeScimitarsBundle", new[]
            {
                "CMUYautjaScimitarAttachment",
                "CMUYautjaScimitarAttachment",
            });
            AssertBundle(prototypes, entMan, "CMUYautjaSkeweringScimitarsBundle", new[]
            {
                "CMUYautjaScimitarAltAttachment",
                "CMUYautjaScimitarAltAttachment",
            });
            AssertBundle(prototypes, entMan, "CMUYautjaChainGauntletsBundle", new[]
            {
                "CMUYautjaChainGauntletsAttachment",
                "CMUYautjaChainGauntletsAttachment",
                "CMUYautjaChainwhip",
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerShieldUsesSourceInheritedYautjaShieldProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracerShield = entMan.SpawnEntity("CMUYautjaBracerShield", MapCoordinates.Nullspace);
            var clanShield = entMan.SpawnEntity("CMUYautjaClanShield", MapCoordinates.Nullspace);

            try
            {
                var bracerMeta = entMan.GetComponent<MetaDataComponent>(bracerShield);
                Assert.That(bracerMeta.EntityDescription, Is.EqualTo(
                    "A shield made of concentric metal alloy plates. The plates fold into one another for compact storage while still providing superior protection."));

                var bracerBlock = entMan.GetComponent<BlockingComponent>(bracerShield);
                var clanBlock = entMan.GetComponent<BlockingComponent>(clanShield);

                AssertDamageModifierEqual(bracerBlock.PassiveBlockDamageModifer, clanBlock.PassiveBlockDamageModifer);
                AssertDamageModifierEqual(bracerBlock.ActiveBlockDamageModifier, clanBlock.ActiveBlockDamageModifier);
                Assert.That(bracerBlock.PassiveBlockFraction, Is.EqualTo(clanBlock.PassiveBlockFraction));
                Assert.That(bracerBlock.ActiveBlockFraction, Is.EqualTo(clanBlock.ActiveBlockFraction));
            }
            finally
            {
                if (!entMan.Deleted(bracerShield))
                    entMan.DeleteEntity(bracerShield);
                if (!entMan.Deleted(clanShield))
                    entMan.DeleteEntity(clanShield);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaShieldsExposeCmss13BlockChanceMetadata()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shields = new[]
            {
                entMan.SpawnEntity("CMUYautjaBracerShield", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaClanShield", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaAncientShield", MapCoordinates.Nullspace),
            };

            try
            {
                foreach (var shield in shields)
                {
                    var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                    Assert.Multiple(() =>
                    {
                        Assert.That(sourceBlock.ShieldType, Is.EqualTo(YautjaSourceShieldType.Directional));
                        Assert.That(sourceBlock.ReadiedBlock, Is.EqualTo(YautjaSourceShieldChance.VeryHigh));
                        Assert.That((int) sourceBlock.ReadiedBlock, Is.EqualTo(40),
                            "CMSS13 code/__DEFINES/equipment.dm defines SHIELD_CHANCE_VHIGH as 40.");
                        Assert.That(sourceBlock.PassiveBlock, Is.EqualTo(YautjaSourceShieldChance.Medium));
                        Assert.That((int) sourceBlock.PassiveBlock, Is.EqualTo(20),
                            "CMSS13 code/__DEFINES/equipment.dm defines SHIELD_CHANCE_MED as 20.");
                        Assert.That(sourceBlock.BlocksOnBack, Is.False);
                    });
                }
            }
            finally
            {
                foreach (var shield in shields)
                {
                    if (!entMan.Deleted(shield))
                        entMan.DeleteEntity(shield);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CombistickExposesCmss13TwoHandedShieldProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);

            try
            {
                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(combistick);
                Assert.Multiple(() =>
                {
                    Assert.That(sourceBlock.ShieldType, Is.EqualTo(YautjaSourceShieldType.DirectionalTwoHands),
                        "CMSS13 /obj/item/weapon/yautja/chained/combistick sets shield_type = SHIELD_DIRECTIONAL_TWOHANDS.");
                    Assert.That(sourceBlock.ReadiedBlock, Is.EqualTo(YautjaSourceShieldChance.High),
                        "CMSS13 /obj/item/weapon/yautja/chained/combistick sets shield_chance = SHIELD_CHANCE_HIGH while extended.");
                    Assert.That((int) sourceBlock.ReadiedBlock, Is.EqualTo(30),
                        "CMSS13 code/__DEFINES/equipment.dm defines SHIELD_CHANCE_HIGH as 30.");
                    Assert.That(sourceBlock.PassiveBlock, Is.EqualTo(YautjaSourceShieldChance.None),
                        "The located source combistick exposes shield_chance, not a separate passive_block row.");
                    Assert.That(sourceBlock.ProjectileBlockFraction, Is.EqualTo(0.40f).Within(0.0001f),
                        "CMSS13 combistick shield_projectile_mult = PROJECTILE_BLOCK_PERC_40.");
                    Assert.That(sourceBlock.BlocksOnBack, Is.False,
                        "The checked Yautja combistick source does not opt into back-slot shield blocking.");
                });

                var blocking = entMan.GetComponent<BlockingComponent>(combistick);
                Assert.Multiple(() =>
                {
                    Assert.That(blocking.ActiveBlockFraction, Is.EqualTo(0.40f).Within(0.0001f),
                        "The local active transfer fraction uses the source projectile block percentage for this defensive combistick surface.");
                    Assert.That(blocking.PassiveBlockFraction, Is.EqualTo(0f),
                        "Without a located passive_block source row, folded/held passive local blocking must stay disabled.");
                    Assert.That(entMan.HasComponent<DamageableComponent>(combistick), Is.True,
                        "The local blocking transfer path requires the defensive combistick to be able to receive transferred damage.");
                });
            }
            finally
            {
                if (!entMan.Deleted(combistick))
                    entMan.DeleteEntity(combistick);
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
                entMan.GetComponent<TransformComponent>(hunter).LocalRotation = Angle.Zero;

                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var weaponDamage = entMan.GetComponent<DamageableComponent>(combistick);
                var incoming = new DamageSpecifier { DamageDict = { ["Piercing"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);
                var unblockedPiercing = userDamage.Damage.DamageDict["Piercing"];
                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(combistick);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(combistick);
                sourceBlock.ReadiedBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;
                Assert.That(blocking.StartBlocking(combistick, blockingComponent, hunter), Is.True);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Piercing"], Is.LessThan(unblockedPiercing),
                        "An extended, readied combistick should use the CMSS13 two-handed directional shield path against front attacks.");
                    Assert.That(weaponDamage.Damage.DamageDict["Piercing"], Is.GreaterThan(FixedPoint2.Zero),
                        "A successful source shield block transfers damage into the defensive combistick.");
                });

                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);
                damageable.SetAllDamage(combistick, weaponDamage, FixedPoint2.Zero);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: backAttacker, tool: backAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Piercing"], Is.EqualTo(unblockedPiercing),
                        "CMSS13 SHIELD_DIRECTIONAL_TWOHANDS should not block attacks from behind.");
                    Assert.That(weaponDamage.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.Zero),
                        "Back attacks should not transfer damage into the combistick.");
                });

                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);
                damageable.SetAllDamage(combistick, weaponDamage, FixedPoint2.Zero);
                Assert.That(blocking.StopBlocking(combistick, blockingComponent, hunter), Is.True);
                sourceBlock.ReadiedBlock = YautjaSourceShieldChance.None;
                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker, tool: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Piercing"], Is.EqualTo(unblockedPiercing),
                        "The located CMSS13 combistick source has no passive_block row; passive local blocking must remain disabled.");
                    Assert.That(weaponDamage.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.Zero),
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
    public async Task YautjaCombistickProjectileBlockUsesCmss13FortyPercentTwoHandChance()
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
            var random = server.ResolveDependency<IRobustRandom>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, -1)));
            var projectile = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0, -1)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                entMan.GetComponent<TransformComponent>(hunter).LocalRotation = Angle.Zero;

                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var weaponDamage = entMan.GetComponent<DamageableComponent>(combistick);
                var incoming = new DamageSpecifier { DamageDict = { ["Piercing"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: shooter, tool: projectile);
                var unblockedPiercing = userDamage.Damage.DamageDict["Piercing"];
                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(combistick);
                Assert.That(blocking.StartBlocking(combistick, blockingComponent, hunter), Is.True);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(combistick);
                sourceBlock.ReadiedBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;
                sourceBlock.ProjectileBlockFraction = 0.4f;

                random.SetSeed(0);
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: shooter, tool: projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Piercing"], Is.EqualTo(unblockedPiercing),
                        "With seed 0, a 40 percent projectile roll fails; CMSS13 combistick PROJECTILE_BLOCK_PERC_40 must reduce even a forced 100 percent base readied chance.");
                    Assert.That(weaponDamage.Damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.Zero),
                        "A failed projectile shield roll should not transfer damage into the combistick.");
                });

                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);
                damageable.SetAllDamage(combistick, weaponDamage, FixedPoint2.Zero);

                sourceBlock.ProjectileBlockFraction = 1f;
                random.SetSeed(0);
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: shooter, tool: projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Piercing"], Is.LessThan(unblockedPiercing),
                        "The same seeded roll should block once the projectile multiplier is no longer reducing the readied chance.");
                    Assert.That(weaponDamage.Damage.DamageDict["Piercing"], Is.GreaterThan(FixedPoint2.Zero),
                        "A successful projectile shield roll transfers damage into the defensive combistick.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, combistick, shooter, projectile })
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
                var unblockedBlunt = userDamage.Damage.DamageDict["Blunt"];
                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                Assert.That(entMan.HasComponent<BlockingUserComponent>(hunter), Is.True);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Blunt"], Is.EqualTo(unblockedBlunt),
                        "A failed CMSS13 shield_chance roll should leave the incoming damage unblocked.");
                    Assert.That(shieldDamage.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.Zero),
                        "The shield should only absorb damage after the chance roll succeeds.");
                });

                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);
                damageable.SetAllDamage(shield, shieldDamage, FixedPoint2.Zero);

                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;
                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Blunt"], Is.LessThan(unblockedBlunt),
                        "A successful passive source block should use the existing local passive block fraction.");
                    Assert.That(shieldDamage.Damage.DamageDict["Blunt"], Is.GreaterThan(FixedPoint2.Zero),
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
                var unblockedBlunt = userDamage.Damage.DamageDict["Blunt"];
                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.StartBlocking(shield, blockingComponent, hunter), Is.True);
                Assert.That(blockingComponent.IsBlocking, Is.True);

                Assert.That(hands.TryDrop(hunter, shield), Is.True);
                Assert.That(inventory.TryEquip(hunter, shield, "back", silent: true, force: true), Is.True);
                Assert.That(blockingComponent.IsBlocking, Is.False,
                    "Moving a readied Yautja shield out of the hand must clear active blocking before it reaches the back slot.");

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                Assert.That(sourceBlock.BlocksOnBack, Is.False,
                    "CMSS13 /obj/item/weapon/shield/riot/yautja sets blocks_on_back = FALSE.");
                sourceBlock.PassiveBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.ReadiedBlock = YautjaSourceShieldChance.None;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming));

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Blunt"], Is.EqualTo(unblockedBlunt),
                        "A Yautja shield with source blocks_on_back = FALSE must not passively block while worn on the back.");
                    Assert.That(shieldDamage.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.Zero),
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

                entMan.GetComponent<TransformComponent>(hunter).LocalRotation = Angle.Zero;
                var userDamage = entMan.GetComponent<DamageableComponent>(hunter);
                var shieldDamage = entMan.GetComponent<DamageableComponent>(shield);
                var incoming = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker);
                var unblockedBlunt = userDamage.Damage.DamageDict["Blunt"];
                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);

                Assert.That(hands.TryPickupAnyHand(hunter, shield), Is.True);
                var blockingComponent = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.StartBlocking(shield, blockingComponent, hunter), Is.True);

                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(shield);
                sourceBlock.ReadiedBlock = (YautjaSourceShieldChance) 100;
                sourceBlock.PassiveBlock = YautjaSourceShieldChance.None;

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: frontAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Blunt"], Is.LessThan(unblockedBlunt),
                        "A readied CMSS13 directional shield should block attacks from the facing cone.");
                    Assert.That(shieldDamage.Damage.DamageDict["Blunt"], Is.GreaterThan(FixedPoint2.Zero),
                        "The shield should absorb part of a front attack after a successful readied source roll.");
                });

                damageable.SetAllDamage(hunter, userDamage, FixedPoint2.Zero);
                damageable.SetAllDamage(shield, shieldDamage, FixedPoint2.Zero);

                damageable.TryChangeDamage(hunter, new DamageSpecifier(incoming), origin: backAttacker);

                Assert.Multiple(() =>
                {
                    Assert.That(userDamage.Damage.DamageDict["Blunt"], Is.EqualTo(unblockedBlunt),
                        "A CMSS13 directional shield should not block attacks from behind even while readied.");
                    Assert.That(shieldDamage.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.Zero),
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
            var status = entMan.System<StatusEffectQuerySystem>();
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
                    Assert.That(dazed!.Value.Item2 - dazed.Value.Item1, Is.EqualTo(TimeSpan.FromSeconds(3)));
                    Assert.That(entMan.TryGetComponent(firstTarget, out RMCSlowdownComponent? slow), Is.True);
                    Assert.That(slow!.ExpiresAt - timing.CurTime, Is.EqualTo(TimeSpan.FromSeconds(5)).Within(TimeSpan.FromMilliseconds(50)));
                });

                RaiseShieldHit(entMan, shield, hunter, cooldownTarget);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(cooldownTarget), Is.False);
                    Assert.That(status.TryGetTime(cooldownTarget, "Dazed", out _), Is.False);
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
    public async Task AdultRackMainWeaponsClaimGroupMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var mainWeapons = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var ids = mainWeapons.Entries.Select(entry => entry.Id.Id).ToArray();

                Assert.That(mainWeapons.Choices, Is.Not.Null);
                Assert.That(mainWeapons.Choices!.Value.Id, Is.EqualTo("CMUYautjaPrimary"));
                Assert.That(mainWeapons.Choices.Value.Amount, Is.EqualTo(1));
                Assert.That(ids, Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
                    "CMUYautjaCruelStaff",
                    "CMUYautjaChainwhip",
                    "CMUYautjaDualWarScythe",
                    "CMUYautjaDoubleWarScythe",
                    "CMUYautjaCombistick",
                    "CMUYautjaWarAxe",
                    "CMUYautjaWarGlaive",
                    "CMUYautjaCleavingGlaive",
                    "CMUYautjaLongaxe",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMobsStartWithCmss13TotalVendorPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunters = new[]
            {
                entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace),
            };

            try
            {
                foreach (var hunter in hunters)
                {
                    Assert.That(entMan.TryGetComponent(hunter, out CMVendorUserComponent? vendorUser), Is.True,
                        "CMSS13 Yautja gear presets call load_vendor_points() and set vendor_points to YAUTJA_TOTAL_BUY_POINTS.");
                    Assert.That(vendorUser!.Points, Is.EqualTo(50),
                        "CMSS13 code/__DEFINES/vendors.dm defines YAUTJA_TOTAL_BUY_POINTS as 50.");

                    var expectedChoices = Cmss13YautjaClaimCategoryLimits();
                    Assert.That(vendorUser.Choices.Count, Is.EqualTo(expectedChoices.Count),
                        "CMSS13 Yautja gear presets set vendor_buyable_categories to YAUTJA_CAN_BUY_ALL.");
                    foreach (var category in expectedChoices.Keys)
                    {
                        Assert.That(vendorUser.Choices.GetValueOrDefault(category), Is.Zero,
                            $"{category} should start with no local claims consumed.");
                    }
                }
            }
            finally
            {
                foreach (var hunter in hunters)
                {
                    if (!entMan.Deleted(hunter))
                        entMan.DeleteEntity(hunter);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackSpareEquipmentMatchesCmss13PointsRows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var rows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();

                Assert.That(spare.Choices, Is.Null);
                Assert.That(rows, Is.EqualTo(new[]
                {
                    ("CMUYautjaFalconDrone", "Falcon Drone", (int?) 20, (int?) null),
                    ("CMUYautjaHuntingTrap", "Hunting Trap", (int?) 10, (int?) null),
                    ("CMUYautjaArrow", "Arrow - Explosive", (int?) 10, (int?) null),
                    ("CMUYautjaSnareArrow", "Arrow - Snare", (int?) 15, (int?) null),
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(Cmss13PointSpareEquipmentRows))]
    public async Task YautjaRackSpareEquipmentRowsMatchCmss13SourceList(
        string rackPrototype,
        (string Id, string Name, int? Points, int? Amount)[] expectedRows)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity(rackPrototype, MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var spare = vendor.Sections.Single(section => section.Name == "Spare Equipment");
                var rows = spare.Entries
                    .Select(entry => (Id: entry.Id.Id, entry.Name, entry.Points, entry.Amount))
                    .ToArray();

                Assert.That(spare.Choices, Is.Null,
                    $"{rackPrototype} source spare-equipment rows are point-backed rows, not claim rows.");
                Assert.That(rows, Is.EqualTo(expectedRows),
                    $"{rackPrototype} spare rows should match antag_predator.dm one-for-one.");
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
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

    [TestCaseSource(nameof(Cmss13PointSpareEquipmentVendRows))]
    public async Task YautjaRackSpareEquipmentPointVendsUseCmss13VendorPoints(
        string rackPrototype,
        RackPointVendRow[] expectedRows)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var vendorSystem = entMan.System<SharedCMAutomatedVendorSystem>();
            var rack = entMan.SpawnEntity(rackPrototype, MapCoordinates.Nullspace);
            var brokeHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var fundedHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Spare Equipment");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var spare = vendor.Sections[sectionIndex];
                Assert.That(spare.Choices, Is.Null,
                    $"{rackPrototype} source spare-equipment rows use vendor_points rather than claim categories.");

                foreach (var row in expectedRows)
                {
                    var entryIndex = spare.Entries.FindIndex(entry => entry.Id.Id == row.Id);
                    Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0), $"{rackPrototype} missing spare row {row.Id}");
                    Assert.That(spare.Entries[entryIndex].Points, Is.EqualTo((int?) row.Points),
                        $"{rackPrototype} {row.Id} should use the CMSS13 point cost.");

                    var brokeUser = entMan.EnsureComponent<CMVendorUserComponent>(brokeHunter);
                    vendorSystem.SetPoints((brokeHunter, brokeUser), row.Points - 1);
                    var fundedUser = entMan.EnsureComponent<CMVendorUserComponent>(fundedHunter);
                    vendorSystem.SetPoints((fundedHunter, fundedUser), row.Points);

                    var entitiesBefore = EntityPrototypeIds(entMan, row.SpawnedId).Count();

                    Vend(entMan, rack, brokeHunter, sectionIndex, entryIndex);
                    Assert.That(brokeUser.Points, Is.EqualTo(row.Points - 1),
                        $"{rackPrototype} {row.Id} should reject insufficient vendor_points without deduction.");
                    Assert.That(EntityPrototypeIds(entMan, row.SpawnedId).Count(), Is.EqualTo(entitiesBefore),
                        $"{rackPrototype} {row.Id} should not spawn on insufficient vendor_points.");

                    Vend(entMan, rack, fundedHunter, sectionIndex, entryIndex);
                    Assert.That(fundedUser.Points, Is.Zero,
                        $"{rackPrototype} {row.Id} should deduct exactly the CMSS13 point cost.");
                    Assert.That(EntityPrototypeIds(entMan, row.SpawnedId).Count(), Is.EqualTo(entitiesBefore + 1),
                        $"{rackPrototype} {row.Id} should spawn the source-mapped spare item on success.");
                }
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
    public async Task AdultRackSpareEquipmentWithoutVendorPointsDeniesLikeCmss13DefaultZero()
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
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Spare Equipment");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var spare = vendor.Sections[sectionIndex];
                var falconIndex = spare.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFalconDrone");
                Assert.That(falconIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(spare.Entries[falconIndex].Points, Is.EqualTo((int?) 20));
                Assert.That(entMan.HasComponent<CMVendorUserComponent>(hunter), Is.False);

                var falconsBefore = EntityPrototypeIds(entMan, "CMUYautjaFalconDrone").Count();

                Vend(entMan, rack, hunter, sectionIndex, falconIndex);

                Assert.That(entMan.HasComponent<CMVendorUserComponent>(hunter), Is.False,
                    "CMSS13 treats a user with no earned vendor_points as a normal zero-point denial, not an exceptional state.");
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaFalconDrone").Count(), Is.EqualTo(falconsBefore));
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
    public async Task AdultRackSectionOrderMatchesCmss13RegularRack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Main Weapons (CHOOSE 1)",
                    "Bracer Attachments",
                    "Support Equipment (CHOOSE 2)",
                    "Ranged Equipment (CHOOSE 1)",
                    "Clothing Accessory (CHOOSE 1)",
                    "Spare Equipment",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackHuntingEquipmentBundleMatchesCmss13Essentials()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var bundle = entMan.SpawnEntity("CMUYautjaHuntingEquipmentBundle", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var entryIds = essentials.Entries.Select(entry => entry.Id.Id).ToArray();
                var huntingEntry = essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                var bundleComp = entMan.GetComponent<CMVendorBundleComponent>(bundle);
                var bundleIds = bundleComp.Bundle.Select(id => id.Id).ToArray();

                Assert.That(essentials.Choices, Is.Null);
                AssertChoice(huntingEntry, "CMUYautjaEssentials", 1);
                Assert.That(entryIds, Is.EqualTo(new[]
                {
                    "CMUYautjaHuntingEquipmentBundle",
                    "CMUYautjaArmorBundle",
                }));
                Assert.That(bundleIds, Is.EqualTo(new[]
                {
                    "CMUYautjaBodyMesh",
                    "CMUYautjaHuntingPouch",
                    "CMUYautjaMedicompFull",
                    "CMUYautjaRelayBeacon",
                    "CMUYautjaCleanserGelVial",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(bundle))
                    entMan.DeleteEntity(bundle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultRackArmorBundleMatchesCmss13Essentials()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var bundle = entMan.SpawnEntity("CMUYautjaArmorBundle", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var entryIds = essentials.Entries.Select(entry => entry.Id.Id).ToArray();
                var armorEntry = essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaArmorBundle");
                var bundleComp = entMan.GetComponent<CMVendorBundleComponent>(bundle);
                var bundleIds = bundleComp.Bundle.Select(id => id.Id).ToArray();

                Assert.That(essentials.Choices, Is.Null);
                AssertChoice(armorEntry, "CMUYautjaArmor", 1);
                Assert.That(entryIds, Is.EqualTo(new[]
                {
                    "CMUYautjaHuntingEquipmentBundle",
                    "CMUYautjaArmorBundle",
                }));
                Assert.That(bundleIds, Is.EqualTo(new[]
                {
                    "CMUYautjaClanArmor",
                    "CMUYautjaMask",
                    "CMUYautjaMaskAccessory01Ebony",
                    "CMUYautjaClanGreaves",
                }));
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(bundle))
                    entMan.DeleteEntity(bundle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdultArmorBundlePostVendorHookAppliesYautjaProfileGearLikeCmss13Prefs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var containers = entMan.System<SharedContainerSystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithArmor(YautjaGearMaterial.Bronze, 3)
                .WithMask(YautjaGearMaterial.Bone, 12)
                .WithMaskAccessory(2)
                .WithGreaves(YautjaGearMaterial.Silver, 2);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(yautja);

            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords.Offset(new Vector2(1, 0)), "CMUYautjaHunter", profile, station: null);

            try
            {
                ClearSlot(entMan, inventory, hunter, "outerClothing");
                ClearSlot(entMan, inventory, hunter, "mask");
                ClearSlot(entMan, inventory, hunter, "shoes");

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var armorIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaArmorBundle");
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, sectionIndex, armorIndex);

                AssertEquippedPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmorBronze3");
                AssertEquippedPrototype(entMan, inventory, hunter, "mask", "CMUYautjaMaskPred12Bone");
                AssertEquippedPrototype(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreavesSilver2");

                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out var mask), Is.True);
                Assert.That(containers.TryGetContainer(mask.Value, "cmu-yautja-mask-accessory", out var accessoryContainer), Is.True);
                Assert.That(accessoryContainer.ContainedEntities, Has.Count.EqualTo(1));
                Assert.That(
                    entMan.GetComponent<MetaDataComponent>(accessoryContainer.ContainedEntities[0]).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaMaskAccessory02Bone"));
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

    [Test]
    public async Task CapePostVendorHookKeepsSelectedSubtypeAndUsesDefaultColorLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithCapeStyle(YautjaCapeStyle.Full)
                .WithCapeColor(new Color((byte) 0x2a, (byte) 0x5c, (byte) 0x8a));
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(yautja);

            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords.Offset(new Vector2(1, 0)), "CMUYautjaHunter", profile, station: null);

            try
            {
                ClearSlot(entMan, inventory, hunter, "back");

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var quarterIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeQuarter");
                Assert.That(quarterIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, sectionIndex, quarterIndex);

                AssertEquippedPrototype(entMan, inventory, hunter, "back", "CMUYautjaCapeQuarter");
                Assert.That(inventory.TryGetSlotEntity(hunter, "back", out var cape), Is.True);
                Assert.That(entMan.GetComponent<YautjaCapeComponent>(cape.Value).Color,
                    Is.EqualTo(YautjaCharacterProfile.Default.CapeColor));
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

    [Test]
    public async Task HeavyClanArmorPostVendorHookAppliesProfileArmorMaterialLikeCmss13FullArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithArmor(YautjaGearMaterial.Bronze, 3);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(yautja);

            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords.Offset(new Vector2(1, 0)), "CMUYautjaHunter", profile, station: null);

            try
            {
                ClearSlot(entMan, inventory, hunter, "outerClothing");

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Support Equipment (CHOOSE 2)");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var armorIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaHeavyClanArmor");
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, sectionIndex, armorIndex);

                AssertEquippedPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaHeavyClanArmor");
                AssertEquippedVisualsMatchPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaHeavyClanArmorBronze");
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

    [Test]
    public async Task StrandedScalableArmorPostVendorHookKeepsScalableGearAndAppliesProfileVisuals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var containers = entMan.System<SharedContainerSystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithArmor(YautjaGearMaterial.Bronze, 3)
                .WithMask(YautjaGearMaterial.Bone, 12)
                .WithMaskAccessory(2)
                .WithGreaves(YautjaGearMaterial.Silver, 2);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(yautja);

            var rack = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", map.GridCoords);
            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords.Offset(new Vector2(1, 0)), "CMUYautjaHunter", profile, station: null);

            try
            {
                ClearSlot(entMan, inventory, hunter, "outerClothing");
                ClearSlot(entMan, inventory, hunter, "mask");
                ClearSlot(entMan, inventory, hunter, "shoes");

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var armorIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaStrandedArmorBundle");
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, sectionIndex, armorIndex);

                AssertEquippedPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmorScalable");
                AssertEquippedPrototype(entMan, inventory, hunter, "mask", "CMUYautjaMaskScalable");
                AssertEquippedPrototype(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreavesScalable");

                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "outerClothing", "CMUYautjaClanArmorBronze3");
                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "mask", "CMUYautjaMaskPred12Bone");
                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreavesSilver2");

                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out var mask), Is.True);
                Assert.That(entMan.HasComponent<YautjaScalableRepairComponent>(mask.Value), Is.True);
                Assert.That(containers.TryGetContainer(mask.Value, "cmu-yautja-mask-accessory", out var accessoryContainer), Is.True);
                Assert.That(accessoryContainer.ContainedEntities, Has.Count.EqualTo(1));
                Assert.That(
                    entMan.GetComponent<MetaDataComponent>(accessoryContainer.ContainedEntities[0]).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaMaskAccessory02Bone"));
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

    [Test]
    public async Task BadBloodArmorPostVendorHookIgnoresYautjaProfileLikeCmss13NoOp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();

            var yautja = YautjaCharacterProfile.Default
                .WithArmor(YautjaGearMaterial.Bronze, 3)
                .WithMask(YautjaGearMaterial.Bone, 12)
                .WithGreaves(YautjaGearMaterial.Silver, 2);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(yautja);

            var rack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", map.GridCoords);
            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords.Offset(new Vector2(1, 0)), "CMUYautjaHunter", profile, station: null);

            try
            {
                ClearSlot(entMan, inventory, hunter, "outerClothing");
                ClearSlot(entMan, inventory, hunter, "mask");
                ClearSlot(entMan, inventory, hunter, "shoes");

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Armor Set");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));

                var patchworkIndex = vendor.Sections[sectionIndex].Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaBadBloodArmorPatchworkBundle");
                Assert.That(patchworkIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, hunter, sectionIndex, patchworkIndex);

                AssertEquippedPrototype(entMan, inventory, hunter, "outerClothing", "CMUYautjaBadBloodArmorPatchwork");
                AssertEquippedPrototype(entMan, inventory, hunter, "mask", "CMUYautjaMaskBadBloodPatchwork");
                AssertEquippedPrototype(entMan, inventory, hunter, "shoes", "CMUYautjaBadBloodGreavesPatchwork");
                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "outerClothing", "CMUYautjaBadBloodArmorPatchwork");
                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "mask", "CMUYautjaMaskBadBloodPatchwork");
                AssertProfileVisualsPreservedOnScalableItem(entMan, inventory, hunter, "shoes", "CMUYautjaBadBloodGreavesPatchwork");
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

    private static string[] Cmss13BowArrowPrototypeIds()
    {
        return
        [
            "CMUYautjaHuntingBow",
            "CMUYautjaArrow",
            "CMUYautjaExplosiveArrowActive",
            "CMUYautjaEmpArrow",
            "CMUYautjaEmpArrowActive",
            "CMUYautjaDynamicArrow",
            "CMUYautjaSnareArrow",
            "CMUYautjaQuiverStrap",
            "CMUYautjaQuiverStrapFilled",
            "CMUYautjaQuiverStrapDynamic",
            "CMUYautjaArrowProjectile",
            "CMUYautjaExplosiveArrowProjectile",
            "CMUYautjaEmpArrowProjectile",
            "CMUYautjaSnareArrowProjectile",
        ];
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
                Assert.That(wield.TryWield(bow, wieldable, hunter), Is.True, source);
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

    private static void AssertQuiverContents(IEntityManager entMan, EntityUid quiver, string arrowPrototype)
    {
        var storage = entMan.GetComponent<StorageComponent>(quiver);
        var contents = storage.Container.ContainedEntities
            .Select(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID)
            .ToList();

        Assert.That(contents.Count(id => id == "CMUYautjaHuntingBow"), Is.EqualTo(1));
        Assert.That(contents.Count(id => id == arrowPrototype), Is.EqualTo(7));
        Assert.That(contents, Has.Count.EqualTo(8));
    }

    private static void AssertArrowProjectileStats(
        IEntityManager entMan,
        EntityUid projectile,
        int piercingDamage,
        int armorPiercing,
        float maxRange)
    {
        AssertArrowProjectileStats(entMan, projectile, "Piercing", piercingDamage, armorPiercing, maxRange);
    }

    private static void AssertArrowProjectileStats(
        IEntityManager entMan,
        EntityUid projectile,
        string damageType,
        int damage,
        int armorPiercing,
        float maxRange)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                entMan.GetComponent<ProjectileComponent>(projectile).Damage.DamageDict[damageType],
                Is.EqualTo((FixedPoint2) damage));
            Assert.That(entMan.GetComponent<CMArmorPiercingComponent>(projectile).Amount, Is.EqualTo(armorPiercing));
            Assert.That(entMan.GetComponent<ProjectileMaxRangeComponent>(projectile).Max, Is.EqualTo(maxRange));
        });
    }

    private static void AssertProjectileStats(
        IEntityManager entMan,
        EntityUid projectile,
        string damageType,
        int damage,
        int? armorPiercing,
        float maxRange,
        string source)
    {
        var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);

        Assert.Multiple(() =>
        {
            Assert.That(projectileComp.Damage.DamageDict.TryGetValue(damageType, out var value), Is.True, source);
            Assert.That(value, Is.EqualTo((FixedPoint2) damage), source);
            Assert.That(projectileComp.Damage.GetTotal(), Is.EqualTo((FixedPoint2) damage), source);

            if (armorPiercing is { } expectedArmorPiercing)
            {
                Assert.That(entMan.TryGetComponent(projectile, out CMArmorPiercingComponent? armor), Is.True, source);
                Assert.That(armor!.Amount, Is.EqualTo(expectedArmorPiercing), source);
            }
            else
            {
                Assert.That(entMan.HasComponent<CMArmorPiercingComponent>(projectile), Is.False, source);
            }

            Assert.That(entMan.GetComponent<ProjectileMaxRangeComponent>(projectile).Max, Is.EqualTo(maxRange), source);
        });
    }

    private static void AssertNoExplosionPayload(IEntityManager entMan, EntityUid projectile, string source)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<TriggerOnCollideComponent>(projectile), Is.False, source);
            Assert.That(entMan.HasComponent<ExplodeOnTriggerComponent>(projectile), Is.False, source);
            Assert.That(entMan.HasComponent<ExplosiveComponent>(projectile), Is.False, source);
            Assert.That(entMan.HasComponent<CMExplosionEffectComponent>(projectile), Is.False, source);
            Assert.That(entMan.HasComponent<RMCScorchEffectComponent>(projectile), Is.False, source);
        });
    }

    private static void AssertIncendiaryPayload(IEntityManager entMan, EntityUid projectile, string source)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<IgniteOnCollideComponent>(projectile), Is.False,
                $"{source} CMSS13 incendiary plasma fire stacks are target-specific; generic IgniteOnCollide would apply the same stacks to humans and xenos.");
            Assert.That(entMan.TryGetComponent(projectile, out YautjaIncendiaryPlasmaProjectileComponent? incendiary), Is.True, source);
            Assert.That(incendiary!.FireStacks, Is.EqualTo(20f), source);
            Assert.That(incendiary.XenoFireStackMultiplier, Is.EqualTo(0.5f), source);
            Assert.That(incendiary.XenoDamageStackDivisor, Is.EqualTo(4f), source);
        });
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
            Assert.That(flammable.FireStacks, Is.EqualTo(fireStacks).Within(0.001f), source);
            Assert.That(flammable.OnFire, Is.True, source);
        });
    }

    private static void AssertExplosionPayload(
        IEntityManager entMan,
        EntityUid projectile,
        int total,
        int max,
        string source,
        int? maxTileBreak = null)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<TriggerOnCollideComponent>(projectile), Is.True, source);
            Assert.That(entMan.HasComponent<ExplodeOnTriggerComponent>(projectile), Is.True, source);
            Assert.That(entMan.TryGetComponent(projectile, out ExplosiveComponent? explosive), Is.True, source);
            Assert.That(explosive!.ExplosionType.Id, Is.EqualTo("RMC"), source);
            Assert.That(explosive.TotalIntensity, Is.EqualTo(total), source);
            Assert.That(explosive.MaxIntensity, Is.EqualTo(max), source);
            if (maxTileBreak is { } expectedMaxTileBreak)
                Assert.That(explosive.MaxTileBreak, Is.EqualTo(expectedMaxTileBreak), source);
        });
    }

    private static void AssertVisibleLayer(
        SpriteSystem sprites,
        EntityUid uid,
        SpriteComponent sprite,
        string layerKey,
        string state,
        bool visible)
    {
        Assert.That(sprites.TryGetLayer((uid, sprite), layerKey, out var layer, false), Is.True, $"{layerKey} layer missing");
        Assert.That(layer!.State.Name, Is.EqualTo(state), $"{layerKey} state");
        Assert.That(layer.Visible, Is.EqualTo(visible), $"{layerKey} visibility");
    }

    private static void AssertSoundPath(SoundSpecifier sound, string path)
    {
        Assert.That(sound, Is.TypeOf<SoundPathSpecifier>());
        Assert.That(((SoundPathSpecifier) sound).Path.ToString(), Is.EqualTo(path));
    }

    private static void AssertSoundCollection(SoundSpecifier sound, string collection)
    {
        Assert.That(sound, Is.TypeOf<SoundCollectionSpecifier>());
        Assert.That(((SoundCollectionSpecifier) sound).Collection, Is.EqualTo(collection));
    }

    private static void AssertCasterMode(
        YautjaCasterMode mode,
        string name,
        string projectile,
        int powerCost,
        string fireSound)
    {
        Assert.Multiple(() =>
        {
            Assert.That(mode.Name.Id, Is.EqualTo(name));
            Assert.That(mode.Projectile.Id, Is.EqualTo(projectile));
            Assert.That(mode.PowerCost, Is.EqualTo((FixedPoint2) powerCost));
            AssertSoundPath(mode.FireSound, fireSound);
        });
    }

    private static void AssertCasterState(
        YautjaCasterComponent caster,
        ProjectileBatteryAmmoProviderComponent ammo,
        int mode,
        string projectile)
    {
        Assert.That(caster.CurrentMode, Is.EqualTo(mode));
        Assert.That(ammo.Prototype, Is.EqualTo(projectile));
    }

    private static void AssertRangedGunStats(
        IEntityManager entMan,
        EntityUid weapon,
        float fireRate,
        double scatterWielded,
        double scatterUnwielded,
        double accuracy,
        double accuracyUnwielded,
        string source)
    {
        var selective = entMan.GetComponent<RMCSelectiveFireComponent>(weapon);
        var accuracyComp = entMan.GetComponent<RMCWeaponAccuracyComponent>(weapon);

        Assert.Multiple(() =>
        {
            Assert.That(selective.BaseFireRate, Is.EqualTo(fireRate).Within(0.0001f), source);
            Assert.That(selective.ScatterWielded.Degrees, Is.EqualTo(scatterWielded).Within(0.0001), source);
            Assert.That(selective.ScatterUnwielded.Degrees, Is.EqualTo(scatterUnwielded).Within(0.0001), source);
            Assert.That(accuracyComp.AccuracyMultiplier, Is.EqualTo((FixedPoint2) accuracy), source);
            Assert.That(accuracyComp.AccuracyMultiplierUnwielded, Is.EqualTo((FixedPoint2) accuracyUnwielded), source);
        });
    }

    private static void AssertNonCorrodible(IEntityManager entMan, EntityUid item)
    {
        var id = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID;

        Assert.That(
            entMan.TryGetComponent<CorrodibleComponent>(item, out var corrodible),
            Is.True,
            $"{id} should map CMSS13 unacidable = TRUE to a local Corrodible component.");
        Assert.That(corrodible!.IsCorrodible, Is.False, $"{id} should not be acid-corrodible.");
    }

    private static void AssertYautjaTechItemBlocksLikeCmss13ItemPredator(
        IEntityManager entMan,
        EntityUid item,
        string id,
        bool blockPickup = true)
    {
        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
            $"{id} source flags_item = ITEM_PREDATOR mapping");
        Assert.That(tech!.BlockPickup, Is.EqualTo(blockPickup), $"{id} source ITEM_PREDATOR pickup restriction");
        Assert.That(tech.BlockUse, Is.True, $"{id} source ITEM_PREDATOR use restriction");
        Assert.That(tech.BlockMelee, Is.True, $"{id} source ITEM_PREDATOR melee restriction");
        Assert.That(tech.BlockThrow, Is.True, $"{id} source ITEM_PREDATOR throw restriction");
        Assert.That(tech.BlockShoot, Is.True, $"{id} source ITEM_PREDATOR shoot restriction");
    }

    private static void AssertDefoliatorWeapon(
        IEntityManager entMan,
        EntityUid weapon,
        string id,
        string startingTank)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(weapon);
        var item = entMan.GetComponent<ItemComponent>(weapon);
        var clothing = entMan.GetComponent<ClothingComponent>(weapon);
        var gun = entMan.GetComponent<GunComponent>(weapon);
        var wieldable = entMan.GetComponent<WieldableComponent>(weapon);
        var slots = entMan.GetComponent<ItemSlotsComponent>(weapon);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo("heavy gel defoliator"), $"{id} CMSS13 source name");
            Assert.That(meta.EntityDescription,
                Is.EqualTo("A high-power incendiary device used to rapidly expunge evidence of hives or dishonorable foes. Unsurprisingly, it is just as effective in direct combat, and lightweight enough to be fired with one hand."),
                $"{id} CMSS13 source description");
            Assert.That(item.RsiPath, Is.EqualTo("_CMU14/Yautja/pred_guns_inhands.rsi"),
                $"{id} CMSS13 item_icons left/right hand pred gun DMIs");
            Assert.That(item.HeldPrefix, Is.EqualTo("defoliator"),
                $"{id} CMSS13 item_state = \"defoliator\"");
            Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK),
                $"{id} CMSS13 flags_equip_slot = SLOT_BACK");
            Assert.That(clothing.RsiPath, Is.EqualTo("_CMU14/Yautja/pred_guns_back.rsi"),
                $"{id} CMSS13 item_icons WEAR_BACK uses guns_by_type/pred_guns.dmi");
            Assert.That(clothing.EquippedPrefix, Is.EqualTo("defoliator"),
                $"{id} CMSS13 item_state = \"defoliator\" drives the equipped back sprite.");
            Assert.That(entMan.HasComponent<RMCFlamerAmmoProviderComponent>(weapon), Is.True,
                $"{id} should use the local RMC flamer ammo provider for CMSS13 flamer behavior.");
            Assert.That(entMan.HasComponent<RMCIgniterComponent>(weapon), Is.True,
                $"{id} should expose CMSS13 ignite/extinguish flamer behavior.");
            Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(weapon), Is.False,
                $"{id} CMSS13 description says the defoliator is lightweight enough to fire one-handed.");
            Assert.That(entMan.HasComponent<WieldableComponent>(weapon), Is.True,
                $"{id} CMSS13 flags_item includes TWOHANDED even though firing does not require wield.");
            Assert.That(wieldable.WieldedInhandPrefix, Is.EqualTo("defoliator"),
                $"{id} CMSS13 item_state stays \"defoliator\" when wielded.");
            Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.FullAuto),
                $"{id} should preserve flamer continuous-fire behavior.");
            Assert.That(gun.AvailableModes, Is.EqualTo(SelectiveFire.FullAuto),
                $"{id} should not expose unrelated gun fire modes.");
            Assert.That(slots.Slots["gun_magazine"].StartingItem, Is.EqualTo(startingTank),
                $"{id} CMSS13 current_mag mapping");
        });

        AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, weapon, id);
    }

    private static void AssertDefoliatorTank(
        IEntityManager entMan,
        SharedSolutionContainerSystem solutionSystem,
        EntityUid tank,
        string id,
        string name,
        string description,
        string reagent,
        int quantity)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(tank);
        var flamerTank = entMan.GetComponent<RMCFlamerTankComponent>(tank);
        var solutions = entMan.GetComponent<SolutionContainerManagerComponent>(tank);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(name), $"{id} CMSS13 source name");
            Assert.That(meta.EntityDescription, Is.EqualTo(description), $"{id} CMSS13 source description");
            Assert.That(flamerTank.MaxRange, Is.EqualTo(8),
                $"{id} CMSS13 max_range = 8.");
            Assert.That(flamerTank.MaxIntensity, Is.EqualTo(70),
                $"{id} CMSS13 max_intensity = 70.");
            Assert.That(flamerTank.IgnoreReagentRange, Is.True,
                $"{id} CMSS13 Yautja tank max_range should drive live flamer reach instead of the local reagent radius.");
            Assert.That(entMan.HasComponent<RMCFlamerTankComponent>(tank), Is.True,
                $"{id} should be accepted by local flamer tank slots.");
            Assert.That(entMan.GetComponent<ItemComponent>(tank).Size.Id, Is.EqualTo("Normal"),
                $"{id} should preserve the local flamer-tank item footprint for CMSS13 tank storage.");
            Assert.That(entMan.HasComponent<SolutionContainerVisualsComponent>(tank), Is.False,
                $"{id} CMSS13 stripe_icon = FALSE means the local RMC fill stripe overlay should not be present.");
        });

        Assert.That(
            solutionSystem.TryGetSolution((tank, solutions), "rmc_flamer_tank", out _, out var solution),
            Is.True,
            $"{id} should expose the local flamer tank solution.");
        Assert.That(solution.Volume, Is.EqualTo((FixedPoint2) quantity),
            $"{id} CMSS13 max_rounds = 100 maps to a full 100-unit flamer solution.");
        Assert.That(solution.Contents, Has.Count.EqualTo(1), $"{id} should start with one CMSS13 flamer reagent.");
        Assert.That(solution.Contents[0].Reagent.Prototype, Is.EqualTo(reagent),
            $"{id} CMSS13 flamer_chem/caliber mapping.");
        Assert.That(solution.Contents[0].Quantity, Is.EqualTo((FixedPoint2) quantity),
            $"{id} should start full.");
    }

    private static void AssertDefoliatorLiveFire(
        IEntityManager entMan,
        SharedRMCFlamerSystem flamerSystem,
        EntityUid defoliator,
        EntityCoordinates coordinates,
        string reagent)
    {
        var transform = entMan.System<SharedTransformSystem>();
        transform.SetCoordinates(defoliator, coordinates);
        var start = coordinates;
        var target = coordinates.Offset(new Vector2(-8, 0));
        var flamer = entMan.GetComponent<RMCFlamerAmmoProviderComponent>(defoliator);
        var gun = entMan.GetComponent<GunComponent>(defoliator);
        DeleteFlamerChains(entMan);

        Assert.That(
            flamerSystem.TryGetPreviewTiles((defoliator, flamer), start, target, out var previewTiles),
            Is.True,
            "CMSS13 Yautja defoliator tanks define live flamer max_range = 8.");
        Assert.That(
            FarthestXReach(transform, start, previewTiles!),
            Is.EqualTo(8).Within(0.01f),
            "CMSS13 source tank max_range should control live fire range instead of the lower local reagent radius.");

        flamerSystem.ShootFlamer((defoliator, flamer), (defoliator, gun), null, start, target);

        try
        {
            var chains = new List<RMCFlamerChainComponent>();
            var query = entMan.EntityQueryEnumerator<RMCFlamerChainComponent>();
            while (query.MoveNext(out var _, out var chain))
                chains.Add(chain);

            Assert.That(chains, Has.Count.EqualTo(1), "Live defoliator firing should create one flamer chain.");
            Assert.Multiple(() =>
            {
                var chain = chains.Single();
                Assert.That(chain.Reagent, Is.EqualTo(reagent),
                    "CMSS13 regular and EX defoliator tanks should spread their configured flamer_chem.");
                Assert.That(chain.MaxIntensity, Is.EqualTo(70),
                    "CMSS13 Yautja defoliator tank max_intensity = 70 should be carried into the live flamer chain.");
                Assert.That(
                    FarthestXReach(transform, start, chain.Tiles),
                    Is.EqualTo(8).Within(0.01f),
                    "Live flamer chain should carry the CMSS13 eight-tile defoliator reach.");
            });
        }
        finally
        {
            DeleteFlamerChains(entMan);
        }
    }

    private static float FarthestXReach(
        SharedTransformSystem transform,
        EntityCoordinates start,
        IReadOnlyCollection<LineTile> tiles)
    {
        var startX = transform.ToMapCoordinates(start).Position.X;
        return startX - tiles.Select(tile => tile.Coordinates.Position.X).Min() + 0.5f;
    }

    private static void DeleteFlamerChains(IEntityManager entMan)
    {
        var query = entMan.EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (query.MoveNext(out var uid, out _))
            entMan.DeleteEntity(uid);
    }

    private static void AssertDefoliatorWeaponPrototypeVisuals(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);

        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
        Assert.Multiple(() =>
        {
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/pred_guns.rsi")),
                $"{id} CMSS13 icon = icons/obj/items/weapons/guns/guns_by_faction/pred.dmi");
            Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("defoliator"),
                $"{id} CMSS13 icon_state = \"defoliator\"");
        });
    }

    private static void AssertDefoliatorTankPrototypeVisuals(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);

        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
        Assert.Multiple(() =>
        {
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/pred_gun_ammo.rsi")),
                $"{id} CMSS13 icon = icons/obj/items/weapons/guns/ammo_by_faction/pred.dmi");
            Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("defoliator"),
                $"{id} CMSS13 icon_state = \"defoliator\"");
        });
    }

    private static void AssertCmss13ArmorStats(IEntityManager entMan, EntityUid item, string id, Cmss13ProtectionStats stats)
    {
        var armor = entMan.GetComponent<CMArmorComponent>(item);

        Assert.That(armor.Melee, Is.EqualTo(stats.Melee), $"{id} CMSS13 armor_melee local tier mapping");
        Assert.That(armor.Bullet, Is.EqualTo(stats.Bullet), $"{id} CMSS13 armor_bullet local tier mapping");
        Assert.That(armor.Bio, Is.EqualTo(stats.Bio), $"{id} CMSS13 armor_bio local tier mapping");
        Assert.That(armor.ExplosionArmor, Is.EqualTo(stats.ExplosionArmor), $"{id} CMSS13 armor_bomb local tier mapping");
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

    private static void PrepareCannonPackRegen(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid hunter,
        EntityUid pack)
    {
        PrepareCannonPackRoleGuardUser(entMan, inventory, hunter, pack);

        var packComp = entMan.GetComponent<YautjaCannonPackComponent>(pack);
        packComp.Charge = 1000;
        packComp.MaxCharge = 3000;
        packComp.Regen = 200;
    }

    private static void AssertBadBloodArmorPiece(
        IEntityManager entMan,
        string id,
        string name,
        string description,
        SlotFlags slots,
        string spritePath,
        bool blockPickup = true)
    {
        var item = EntityPrototypeIds(entMan, id).Single();
        var meta = entMan.GetComponent<MetaDataComponent>(item);
        var clothing = entMan.GetComponent<ClothingComponent>(item);

        Assert.That(meta.EntityName, Is.EqualTo(name), $"{id} CMSS13 source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(description), $"{id} CMSS13 source description");
        Assert.That(clothing.Slots, Is.EqualTo(slots), $"{id} CMSS13 equip slot mapping");
        Assert.That(clothing.RsiPath, Is.EqualTo(spritePath), $"{id} CMSS13 icon_state sprite mapping");
        AssertNonCorrodible(entMan, item);

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
            $"{id} source flags_item = ITEM_PREDATOR mapping");
        if (tech != null)
        {
            Assert.That(tech.BlockPickup, Is.EqualTo(blockPickup), $"{id} source ITEM_PREDATOR pickup restriction");
            Assert.That(tech.BlockUse, Is.True, $"{id} source ITEM_PREDATOR use restriction");
            Assert.That(tech.BlockMelee, Is.True, $"{id} source ITEM_PREDATOR melee restriction");
            Assert.That(tech.BlockThrow, Is.True, $"{id} source ITEM_PREDATOR throw restriction");
            Assert.That(tech.BlockShoot, Is.True, $"{id} source ITEM_PREDATOR shoot restriction");
        }
    }

    private static void AssertBadBloodArmorPiece(
        IEntityManager entMan,
        ExamineSystem examine,
        EntityUid hunter,
        string id,
        string name,
        string description,
        SlotFlags slots,
        string spritePath,
        Cmss13ArmorStats stats,
        float? antiHugMaxCount = null,
        bool blockPickup = true)
    {
        var item = EntityPrototypeIds(entMan, id).Single();
        var meta = entMan.GetComponent<MetaDataComponent>(item);
        var clothing = entMan.GetComponent<ClothingComponent>(item);
        var armor = entMan.GetComponent<CMArmorComponent>(item);
        var examineText = examine.GetExamineText(item, hunter).ToMarkup();

        Assert.That(meta.EntityName, Is.EqualTo(name), $"{id} CMSS13 source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(description), $"{id} CMSS13 source description");
        Assert.That(clothing.Slots, Is.EqualTo(slots), $"{id} CMSS13 equip slot mapping");
        Assert.That(clothing.RsiPath, Is.EqualTo(spritePath), $"{id} CMSS13 icon_state sprite mapping");
        Assert.That(armor.Melee, Is.EqualTo(stats.Melee), $"{id} CMSS13 armor_melee local tier mapping");
        Assert.That(armor.Bullet, Is.EqualTo(stats.Bullet), $"{id} CMSS13 armor_bullet local tier mapping");
        Assert.That(armor.Bio, Is.EqualTo(stats.Bio), $"{id} CMSS13 armor_bio local tier mapping");
        Assert.That(armor.ExplosionArmor, Is.EqualTo(stats.ExplosionArmor), $"{id} CMSS13 armor_bomb local tier mapping");
        Assert.That(examineText, Does.Contain(stats.DamagedExamineText),
            $"{id} should expose the CMSS13 YAUTJA_REPAIR_DAMAGED examine line.");
        AssertNonCorrodible(entMan, item);

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
            $"{id} source flags_item = ITEM_PREDATOR mapping");
        if (tech != null)
        {
            Assert.That(tech.BlockPickup, Is.EqualTo(blockPickup), $"{id} source ITEM_PREDATOR pickup restriction");
            Assert.That(tech.BlockUse, Is.True, $"{id} source ITEM_PREDATOR use restriction");
            Assert.That(tech.BlockMelee, Is.True, $"{id} source ITEM_PREDATOR melee restriction");
            Assert.That(tech.BlockThrow, Is.True, $"{id} source ITEM_PREDATOR throw restriction");
            Assert.That(tech.BlockShoot, Is.True, $"{id} source ITEM_PREDATOR shoot restriction");
        }

        if (antiHugMaxCount is { } antiHug)
        {
            Assert.That(entMan.TryGetComponent<ParasiteResistanceComponent>(item, out var resistance), Is.True,
                $"{id} CMSS13 anti_hug should map to local parasite resistance.");
            Assert.That(resistance!.MaxCount, Is.EqualTo(antiHug),
                $"{id} CMSS13 scalable mask anti_hug = 30.");
        }
    }

    private static void AssertBloodedThrallBracerPiece(
        IEntityManager entMan,
        string id,
        string spritePath,
        string state)
    {
        var item = EntityPrototypeIds(entMan, id).Single();
        var meta = entMan.GetComponent<MetaDataComponent>(item);
        var clothing = entMan.GetComponent<ClothingComponent>(item);

        Assert.That(meta.EntityName, Is.EqualTo("blooded thrall bracers"), $"{id} CMSS13 source name");
        Assert.That(meta.EntityDescription, Is.EqualTo("A pair of strange alien bracers, adapted for human biology. These contain additional features."),
            $"{id} CMSS13 source description");
        Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.GLOVES), $"{id} CMSS13 SLOT_HANDS mapping");
        Assert.That(clothing.RsiPath, Is.EqualTo(spritePath), $"{id} CMSS13 icon_state sprite mapping");
        AssertNonCorrodible(entMan, item);

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
            $"{id} source flags_item = ITEM_PREDATOR mapping");
        if (tech != null)
        {
            Assert.That(tech.BlockPickup, Is.True, $"{id} source ITEM_PREDATOR pickup restriction");
            Assert.That(tech.BlockUse, Is.True, $"{id} source ITEM_PREDATOR use restriction");
            Assert.That(tech.BlockMelee, Is.True, $"{id} source ITEM_PREDATOR melee restriction");
            Assert.That(tech.BlockThrow, Is.True, $"{id} source ITEM_PREDATOR throw restriction");
            Assert.That(tech.BlockShoot, Is.True, $"{id} source ITEM_PREDATOR shoot restriction");
        }
    }

    private static void AssertPrototypeIconState(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id,
        string spritePath)
    {
        AssertPrototypeIconState(prototypes, factory, id, spritePath, "icon");
    }

    private static void AssertPrototypeIconState(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id,
        string spritePath,
        string state)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);

        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, id);
        Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/" + spritePath)), $"{id} world sprite RSI");
        Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(state), $"{id} world icon state");
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

    private static EntityUid SpawnAndTrack(IEntityManager entMan, string prototype, List<EntityUid> spawned)
    {
        var uid = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        spawned.Add(uid);
        return uid;
    }

    private static void AssertUtilityItem(
        IEntityManager entMan,
        EntityUid uid,
        string id,
        string name,
        string description,
        string size,
        bool sourceUnacidable,
        bool blockPickup = true)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var item = entMan.GetComponent<ItemComponent>(uid);

        Assert.That(meta.EntityName, Is.EqualTo(name), $"{id} CMSS13 source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(description), $"{id} CMSS13 source description");
        Assert.That(item.Size.Id, Is.EqualTo(size), $"{id} CMSS13 w_class mapping");
        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(uid, out var tech), Is.True,
            $"{id} maps CMSS13 flags_item = ITEM_PREDATOR.");
        Assert.That(tech!.BlockPickup, Is.EqualTo(blockPickup), $"{id} local pickup policy.");

        if (sourceUnacidable)
            AssertNonCorrodible(entMan, uid);
    }

    private static void AssertStaticPrice(
        IEntityManager entMan,
        EntityUid uid,
        string id,
        int price,
        string source)
    {
        Assert.That(entMan.TryGetComponent<StaticPriceComponent>(uid, out var staticPrice), Is.True,
            $"{id} should expose a local StaticPrice for source black_market_value. {source}");
        Assert.That(staticPrice!.Price, Is.EqualTo(price), $"{id} source black_market_value. {source}");
    }

    private static void AssertYautjaTechPolicy(
        IEntityManager entMan,
        EntityUid uid,
        string id,
        bool blockPickup,
        bool blockUse,
        bool blockMelee,
        bool blockThrow,
        bool blockShoot,
        float damageMultiplier,
        string source)
    {
        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(uid, out var tech), Is.True,
            $"{id} local Yautja-tech marker. {source}");
        Assert.Multiple(() =>
        {
            Assert.That(tech!.BlockPickup, Is.EqualTo(blockPickup), $"{id} pickup policy. {source}");
            Assert.That(tech.BlockUse, Is.EqualTo(blockUse), $"{id} use policy. {source}");
            Assert.That(tech.BlockMelee, Is.EqualTo(blockMelee), $"{id} melee policy. {source}");
            Assert.That(tech.BlockThrow, Is.EqualTo(blockThrow), $"{id} throw policy. {source}");
            Assert.That(tech.BlockShoot, Is.EqualTo(blockShoot), $"{id} shoot policy. {source}");
            Assert.That(tech.DamageMultiplier, Is.EqualTo(damageMultiplier), $"{id} damage multiplier. {source}");
        });
    }

    private static void AssertCmss13YautjaToolFacts(
        IEntityManager entMan,
        SharedSolutionContainerSystem solutionSystem,
        EntityUid uid,
        Cmss13YautjaToolRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var item = entMan.GetComponent<ItemComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                $"{row.Id} CMSS13 yaut_items.dm source name");
            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                $"{row.Id} CMSS13 yaut_items.dm source description");
            Assert.That(item.Size.Id, Is.EqualTo(row.Size),
                $"{row.Id} CMSS13 w_class local mapping");
            Assert.That(item.HeldPrefix, Is.EqualTo(row.SpriteState),
                $"{row.Id} CMSS13 item_state local held-prefix mapping");
            Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(uid, out var tech), Is.True,
                $"{row.Id} local ITEM_PREDATOR/Yautja-tech marker");
            Assert.That(tech!.BlockUse, Is.EqualTo(row.BlockUse),
                $"{row.Id} local use policy for the source item family");
        });

        if (row.MeleeDamage is { } damage)
        {
            var melee = entMan.GetComponent<MeleeWeaponComponent>(uid);
            Assert.That(melee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) damage),
                $"{row.Id} CMSS13 force local mapping");
        }

        if (row.WelderFuelCapacity is { } capacity)
        {
            Assert.That(entMan.TryGetComponent<WelderComponent>(uid, out var welder), Is.True,
                $"{row.Id} should remain a local welder.");
            Assert.That(entMan.TryGetComponent<SolutionContainerManagerComponent>(uid, out var solutions), Is.True,
                $"{row.Id} should expose CMSS13 max_fuel through its fuel solution.");
            Assert.That(
                solutionSystem.TryGetSolution((uid, solutions), welder!.FuelSolutionName, out _, out var solution),
                Is.True,
                $"{row.Id} fuel solution exists");
            Assert.That(solution!.MaxVolume, Is.EqualTo((FixedPoint2) capacity),
                $"{row.Id} CMSS13 max_fuel local mapping");
            Assert.That(solution.GetTotalPrototypeQuantity(welder.FuelReagent), Is.EqualTo((FixedPoint2) capacity),
                $"{row.Id} should spawn with a full CMSS13 max_fuel charge.");
        }
    }

    private static void AssertCmss13XenoTrophyStaticFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13XenoTrophyRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var trophy = entMan.GetComponent<YautjaTrophyComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                $"{row.Id} CMSS13 yaut_items.dm source name");
            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                $"{row.Id} CMSS13 yaut_items.dm source description");
            Assert.That(trophy.Kind, Is.EqualTo(row.Kind),
                $"{row.Id} local trophy-kind marker for source item family");
        });

        AssertNonCorrodible(entMan, uid);
    }

    private static void AssertCmss13HumanBoneTrophyStaticFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13HumanBoneTrophyRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var trophy = entMan.GetComponent<YautjaTrophyComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                $"{row.Id} CMSS13 skeleton limb source name");
            Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                $"{row.Id} CMSS13 skeleton limb inherited source description");
            Assert.That(trophy.Kind, Is.EqualTo(row.Kind),
                $"{row.Id} local trophy-kind marker for source skeleton limb");
        });
    }

    private static void AssertCmss13ButcherOutputStaticFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13ButcherOutputRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);

        Assert.Multiple(() =>
        {
            if (row.Name.Length != 0)
            {
                Assert.That(meta.EntityName, Is.EqualTo(row.Name),
                    $"{row.Id} CMSS13 static source name before runtime victim-specific renames.");
            }

            if (row.Description.Length != 0)
            {
                Assert.That(meta.EntityDescription, Is.EqualTo(row.Description),
                    $"{row.Id} CMSS13 static source description.");
            }

            Assert.That(entMan.HasComponent<YautjaTrophyComponent>(uid), Is.False,
                $"{row.Id} is a butcher output row, not one of the CMSS13 scored trophy item rows.");
            Assert.That(entMan.HasComponent<YautjaTechItemComponent>(uid), Is.False,
                $"{row.Id} source butcher output does not set flags_item = ITEM_PREDATOR.");
        });

        switch (row.Kind)
        {
            case Cmss13ButcherOutputKind.StackSheet:
            {
                Assert.That(entMan.HasComponent<ItemComponent>(uid), Is.True,
                    $"{row.Id} CMSS13 source path is /obj/item/stack/sheet/animalhide/human.");
                Assert.That(entMan.TryGetComponent<StackComponent>(uid, out var stack), Is.True,
                    $"{row.Id} should stay stack-like for CMSS13 /obj/item/stack/sheet source parity.");
                Assert.That(stack!.StackTypeId, Is.EqualTo(row.StackType),
                    $"{row.Id} local stack type mapping for CMSS13 animalhide/human sheet.");
                break;
            }
            case Cmss13ButcherOutputKind.SkeletonLimb:
            {
                var item = entMan.GetComponent<ItemComponent>(uid);
                Assert.That(item.Size.Id, Is.EqualTo("Small"),
                    $"{row.Id} local item size for CMSS13 skeleton limb output.");
                break;
            }
            case Cmss13ButcherOutputKind.EffectDecal:
            {
                Assert.That(entMan.HasComponent<ItemComponent>(uid), Is.False,
                    $"{row.Id} CMSS13 source path is /obj/effect/decal/remains, not a pickup item.");
                Assert.That(entMan.HasComponent<StackComponent>(uid), Is.False,
                    $"{row.Id} CMSS13 remains decal is not a stack sheet.");
                break;
            }
            default:
                Assert.Fail($"Unhandled butcher output kind {row.Kind}");
                break;
        }
    }

    private static void AssertMedicompContents(
        IEntityManager entMan,
        StorageComponent storage,
        IReadOnlyDictionary<string, int> expected)
    {
        var actual = new Dictionary<string, int>();

        foreach (var contained in storage.Container.ContainedEntities)
        {
            var id = entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID;
            Assert.That(id, Is.Not.Null, "Medicomp contents should have source-mapped local prototypes.");

            var count = 1;
            if (entMan.TryGetComponent<StackComponent>(contained, out var stack))
                count = stack.Count;

            actual[id!] = actual.GetValueOrDefault(id!) + count;
        }

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static void AssertMedicompPayloadTags(
        IEntityManager entMan,
        EntityUid uid,
        string id,
        IReadOnlyCollection<string> expectedTags)
    {
        Assert.That(entMan.TryGetComponent<TagComponent>(uid, out var tags), Is.True,
            $"{id} should expose tags for CMSS13 medicomp/herbal can_hold mapping.");
        Assert.That(tags!.Tags.Select(tag => tag.Id), Is.SupersetOf(expectedTags), $"{id} source can_hold tags");
    }

    private static void AssertBowLoadedIcon(
        SharedAppearanceSystem appearance,
        EntityUid bow,
        string expected,
        string because)
    {
        Assert.That(appearance.TryGetData<string>(bow, YautjaBowVisuals.LoadedIcon, out var actual), Is.True, because);
        Assert.That(actual, Is.EqualTo(expected), because);
    }

    private static void AssertArrowState(
        IEntityManager entMan,
        EntityUid arrow,
        YautjaArrowWarhead expectedWarhead,
        bool expectedActivated)
    {
        var component = entMan.GetComponent<YautjaArrowComponent>(arrow);

        Assert.Multiple(() =>
        {
            Assert.That(component.SelectedWarhead, Is.EqualTo(expectedWarhead));
            Assert.That(component.Activated, Is.EqualTo(expectedActivated));
        });
    }

    private static void AssertCmss13ArrowInitialState(
        IEntityManager entMan,
        EntityUid arrow,
        string expectedProjectile,
        YautjaArrowWarhead expectedPrimary,
        YautjaArrowWarhead? expectedSecondary,
        YautjaArrowWarhead expectedSelected,
        bool expectedActivated,
        bool expectedDynamic,
        string source)
    {
        var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(arrow);
        var arrowComp = entMan.GetComponent<YautjaArrowComponent>(arrow);

        Assert.Multiple(() =>
        {
            Assert.That(cartridge.Prototype, Is.EqualTo(expectedProjectile), source);
            Assert.That(arrowComp.PrimaryWarhead, Is.EqualTo(expectedPrimary), source);
            Assert.That(arrowComp.SecondaryWarhead, Is.EqualTo(expectedSecondary), source);
            Assert.That(arrowComp.SelectedWarhead, Is.EqualTo(expectedSelected), source);
            Assert.That(arrowComp.Activated, Is.EqualTo(expectedActivated), source);
            Assert.That(arrowComp.Dynamic, Is.EqualTo(expectedDynamic), source);
        });
    }

    private static void AssertStorageFill(
        StorageFillComponent fill,
        IReadOnlyDictionary<string, int> expected)
    {
        var actual = new Dictionary<string, int>();

        foreach (var entry in fill.Contents)
        {
            Assert.That(entry.PrototypeId, Is.Not.Null, "StorageFill entries should use source-mapped local prototypes.");
            actual[entry.PrototypeId!.Value] = actual.GetValueOrDefault(entry.PrototypeId.Value) + entry.Amount;
        }

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static void AssertBadBloodEmissaryPiece(
        IEntityManager entMan,
        EntityUid item,
        string id,
        string name,
        string description,
        SlotFlags slots,
        bool blockPickup = true)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(item);
        var clothing = entMan.GetComponent<ClothingComponent>(item);

        Assert.That(meta.EntityName, Is.EqualTo(name), $"{id} CMSS13 source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(description), $"{id} CMSS13 source description");
        Assert.That(clothing.Slots, Is.EqualTo(slots), $"{id} CMSS13 equip slot mapping");
        AssertNonCorrodible(entMan, item);

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(item, out var tech), Is.True,
            $"{id} source ITEM_PREDATOR mapping");
        Assert.That(tech!.BlockPickup, Is.EqualTo(blockPickup), $"{id} local pickup policy.");
    }

    private static void AssertCamoConformingRuntimeSprite(
        IEntityManager entMan,
        SharedAppearanceSystem appearance,
        EntityUid item,
        CamouflageType expectedCamo,
        string expectedSprite)
    {
        var id = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID;

        Assert.That(entMan.TryGetComponent<ItemCamouflageComponent>(item, out var itemCamo), Is.True,
            $"{id} should use local ItemCamouflage to match CMSS13 camo_conforming Initialize().");
        Assert.That(itemCamo!.CamouflageVariations, Is.Not.Null, $"{id} CMSS13 camo_type sprite table");
        Assert.That(itemCamo.CamouflageVariations![expectedCamo], Is.EqualTo(new ResPath(expectedSprite)),
            $"{id} CMSS13 camo_type sprite mapping");

        Assert.That(appearance.TryGetData(item, ItemCamouflageVisuals.Camo, out CamouflageType actualCamo), Is.True,
            $"{id} should copy current map camouflage during MapInit.");
        Assert.That(actualCamo, Is.EqualTo(expectedCamo), $"{id} CMSS13 conforming map camouflage");
    }

    private static void AssertYautjaBaseArmorAllowedStorage(IEntityManager entMan, EntityUid item, string id)
    {
        var storage = entMan.GetComponent<AllowSuitStorageComponent>(item);

        Assert.That(storage.Whitelist.Components, Is.Not.Null,
            $"{id} CMSS13 base Yautja armor allowed list maps to local suit-storage weapon components.");
        Assert.That(storage.Whitelist.Components!, Does.Contain("Gun"),
            $"{id} CMSS13 base Yautja armor allowed includes harpoons, spike launchers and Yautja energy guns.");
        Assert.That(storage.Whitelist.Components!, Does.Contain("MeleeWeapon"),
            $"{id} CMSS13 base Yautja armor allowed includes /obj/item/weapon/yautja and /obj/item/weapon/twohanded/yautja.");
        Assert.That(storage.Whitelist.Tags, Is.Not.Null,
            $"{id} CMSS13 base Yautja armor allowed Yautja weapon subtypes should keep local weapon tag equivalents.");
        Assert.That(storage.Whitelist.Tags!, Does.Contain("Knife"),
            $"{id} CMSS13 base Yautja armor allowed includes /obj/item/weapon/yautja through the knife subtype.");
        Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("YautjaHuntingPouch"),
            $"{id} CMSS13 base Yautja armor allowed list does not include /obj/item/storage/backpack/yautja; only full/heavy armor does.");
        Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("Flashlight"),
            $"{id} CMSS13 base Yautja armor allowed list does not include generic flashlight storage.");
        Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCMacheteScabbard"),
            $"{id} CMSS13 base Yautja armor allowed list does not include local generic machete scabbards.");
        Assert.That(storage.Whitelist.Tags!, Does.Not.Contain("RMCScabbardKatana"),
            $"{id} CMSS13 base Yautja armor allowed list does not include local generic katana scabbards.");
    }

    private static void AssertBundle(
        IPrototypeManager prototypes,
        IEntityManager entMan,
        string bundlePrototype,
        string[] expected)
    {
        Assert.That(prototypes.HasIndex<EntityPrototype>(bundlePrototype), Is.True, $"Missing {bundlePrototype}");

        var bundle = entMan.SpawnEntity(bundlePrototype, MapCoordinates.Nullspace);

        try
        {
            var bundleComp = entMan.GetComponent<CMVendorBundleComponent>(bundle);
            var bundleIds = bundleComp.Bundle.Select(id => id.Id).ToArray();

            Assert.That(bundleIds, Is.EqualTo(expected));
        }
        finally
        {
            if (!entMan.Deleted(bundle))
                entMan.DeleteEntity(bundle);
        }
    }

    private static string[] Cmss13RackPrototypeIds()
    {
        return
        [
            "CMUYautjaLoadoutVendor",
            "CMUYautjaElderLoadoutVendor",
            "CMUYautjaYoungbloodLoadoutVendor",
            "CMUYautjaThrallLoadoutVendor",
            "CMUYautjaBloodedThrallLoadoutVendor",
            "CMUYautjaBadBloodLoadoutVendor",
            "CMUYautjaStrandedLoadoutVendor",
        ];
    }

    private static HashSet<string> VendorEntryIds(CMAutomatedVendorComponent vendor)
    {
        return vendor.Sections
            .SelectMany(section => section.Entries)
            .Select(entry => entry.Id.Id)
            .ToHashSet();
    }

    public readonly record struct MandatoryBundleRow(string RackId, string BundleId, string ChoiceId, string[] BundleIds);

    public readonly record struct Cmss13RackMachineryRow(
        string SourceType,
        string Prototype,
        YautjaGearRackKind Kind,
        string SourceName,
        string SourceDescription,
        string InitialIconState,
        string SourceListName,
        string[] ExpectedListRows,
        string[] ForbiddenListRows);

    public readonly record struct RackPriorityRow(
        string RackId,
        string Section,
        string EntryId,
        bool Mandatory,
        bool Recommended);

    public readonly record struct UtilityCommunicatorRow(string Id, string Name, string Description);

    public readonly record struct Cmss13CommunicatorChannelRow(
        string CommunicatorId,
        string KeyId,
        string[] Channels,
        string DefaultChannel,
        string? SourceKeyName = null,
        string? SourceKeyDescription = null);

    public readonly record struct Cmss13YautjaToolRow(
        string Id,
        string Name,
        string Description,
        string Size,
        string SpriteState,
        int? MeleeDamage,
        int? WelderFuelCapacity,
        bool BlockUse = false);

    public readonly record struct Cmss13XenoTrophyRow(
        string Id,
        string Name,
        string Description,
        string SpriteState,
        YautjaTrophyKind Kind);

    public readonly record struct Cmss13HumanBoneTrophyRow(
        string Id,
        string Name,
        string Description,
        string SpriteState,
        YautjaTrophyKind Kind);

    public readonly record struct Cmss13ButcherOutputRow(
        string Id,
        string Name,
        string Description,
        string SpritePath,
        string SpriteState,
        Cmss13ButcherOutputKind Kind,
        string StackType = "");

    public enum Cmss13ButcherOutputKind
    {
        StackSheet,
        SkeletonLimb,
        EffectDecal,
    }

    public readonly record struct Cmss13McasteItemRow(
        string Id,
        string Name,
        string Description,
        string Size,
        string Sprite,
        string State,
        SlotFlags? Slots,
        Cmss13ProtectionStats? Stats,
        bool ItemPredator,
        bool Unacidable,
        bool BlockPickup = true,
        bool CheckDescription = true);

    private static IEnumerable<(string Id, int Price, string Source)> Cmss13RemainingBlackMarketPriceRows()
    {
        yield return ("CMUYautjaClanArmor", 100, "CMSS13 /obj/item/clothing/suit/armor/yautja black_market_value = 100.");
        yield return ("CMUYautjaClanArmorScalable", 100, "CMSS13 scalable Yautja armor inherits the base armor black_market_value = 100.");
        yield return ("CMUYautjaHeavyClanArmor", 100, "CMSS13 full/plate Yautja armor inherits the base armor black_market_value = 100.");
        yield return ("CMUYautjaBadBloodArmorPatchwork", 100, "CMSS13 Bad Blood armor inherits the base armor black_market_value = 100.");
        yield return ("CMUYautjaBodyMesh", 50, "CMSS13 /obj/item/clothing/under/chainshirt/hunter black_market_value = 50.");
        yield return ("CMUYautjaBodyMeshScalable", 50, "CMSS13 scalable hunter chainshirt inherits black_market_value = 50.");
        yield return ("CMUYautjaClanGreaves", 50, "CMSS13 /obj/item/clothing/shoes/yautja black_market_value = 50.");
        yield return ("CMUYautjaClanGreavesScalable", 50, "CMSS13 scalable Yautja greaves inherit black_market_value = 50.");
        yield return ("CMUYautjaBadBloodGreavesPatchwork", 50, "CMSS13 Bad Blood greaves inherit black_market_value = 50.");
        yield return ("CMUYautjaHuntingPouch", 50, "CMSS13 /obj/item/storage/backpack/yautja black_market_value = 50.");
        yield return ("CMUYautjaMask", 100, "CMSS13 /obj/item/clothing/mask/gas/yautja black_market_value = 100.");
        yield return ("CMUYautjaMaskScalable", 100, "CMSS13 scalable Yautja mask inherits black_market_value = 100.");
        yield return ("CMUYautjaMaskBadBloodPatchwork", 100, "CMSS13 Bad Blood Yautja masks inherit black_market_value = 100.");
        yield return ("CMUYautjaMedicomp", 10, "CMSS13 /obj/item/storage/medicomp black_market_value = 10.");
        yield return ("CMUYautjaMedicompFull", 10, "CMSS13 filled medicomp inherits black_market_value = 10.");
        yield return ("CMUYautjaMedicompSurvivor", 10, "CMSS13 survivor medicomp inherits black_market_value = 10.");
        yield return ("CMUYautjaMedicompThrall", 10, "CMSS13 thrall medicomp inherits black_market_value = 10.");
    }

    private static IEnumerable<Cmss13RackMachineryRow> Cmss13RackMachineryRows()
    {
        const string rackDescription = "A gear rack for hunting.";

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/hunter",
            "CMUYautjaLoadoutVendor",
            YautjaGearRackKind.Adult,
            "Yautja Hunting Gear Rack",
            rackDescription,
            "pred_vendor_left",
            "cm_vending_equipment_yautja",
            [
                "CMUYautjaHuntingEquipmentBundle",
                "CMUYautjaArmorBundle",
                "CMUYautjaCruelStaff",
                "CMUYautjaPlasmaPistol",
                "CMUYautjaQuiverStrapFilled",
                "CMUYautjaArrow",
            ],
            [
                "CMUYautjaYoungbloodHuntingEquipmentBundle",
                "CMUYautjaThrallHuntingEquipmentBundle",
                "CMUYautjaBloodedThrallEquipmentBundle",
                "CMUYautjaBadBloodHuntingEquipmentBundle",
                "CMUYautjaStrandedHuntingEquipmentBundle",
                "CMUYautjaQuiverStrapDynamic",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/hunter/elder",
            "CMUYautjaElderLoadoutVendor",
            YautjaGearRackKind.Elder,
            "Yautja Elder Hunting Gear Rack",
            rackDescription,
            "pred_vendor_elder_left",
            "cm_vending_elder_yautja",
            [
                "CMUYautjaHuntingEquipmentBundle",
                "CMUYautjaArmorBundle",
                "CMUYautjaAncientShield",
                "CMUYautjaAncientShieldAlt",
                "CMUYautjaCapeCeremonial",
                "CMUYautjaSmartDisc",
            ],
            [
                "CMUYautjaYoungbloodHuntingEquipmentBundle",
                "CMUYautjaThrallHuntingEquipmentBundle",
                "CMUYautjaBloodedThrallEquipmentBundle",
                "CMUYautjaBadBloodHuntingEquipmentBundle",
                "CMUYautjaStrandedHuntingEquipmentBundle",
                "CMUYautjaQuiverStrapDynamic",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/young_blood",
            "CMUYautjaYoungbloodLoadoutVendor",
            YautjaGearRackKind.Youngblood,
            "Yautja Young Hunting Gear Rack",
            rackDescription,
            "pred_vendor_left",
            "cm_vending_young_yautja",
            [
                "CMUYautjaYoungbloodHuntingEquipmentBundle",
                "CMUYautjaArmorBundle",
                "CMUYautjaClanSword",
                "CMUYautjaWristBladesBundle",
            ],
            [
                "CMUYautjaCruelStaff",
                "CMUYautjaFalconDrone",
                "CMUYautjaPlasmaPistol",
                "CMUYautjaArrow",
                "CMUYautjaThrallHuntingEquipmentBundle",
                "CMUYautjaBloodedThrallEquipmentBundle",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/thrall",
            "CMUYautjaThrallLoadoutVendor",
            YautjaGearRackKind.Thrall,
            "Yautja Thrall Gear Rack",
            rackDescription,
            "pred_vendor_left",
            "cm_vending_thrall",
            [
                "CMUYautjaThrallHuntingEquipmentBundle",
                "CMUYautjaThrallArmorEbonyBundle",
                "CMUYautjaClanSword",
                "CMUYautjaLongaxe",
            ],
            [
                "CMUYautjaHuntingEquipmentBundle",
                "CMUYautjaArmorBundle",
                "CMUYautjaCruelStaff",
                "CMUYautjaWristBladesBundle",
                "CMUYautjaBloodedThrallEquipmentBundle",
                "CMUYautjaFalconDrone",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/thrall/blooded_thrall",
            "CMUYautjaBloodedThrallLoadoutVendor",
            YautjaGearRackKind.BloodedThrall,
            "Yautja Blooded Thrall Gear Rack",
            rackDescription,
            "pred_vendor_left",
            "cm_vending_blooded_thrall",
            [
                "CMUYautjaBloodedThrallEquipmentBundle",
                "CMUYautjaBloodedThrallBracerEbonyBundle",
                "CMUYautjaCapeQuarter",
                "CMUYautjaCapePoncho",
            ],
            [
                "CMUYautjaHuntingEquipmentBundle",
                "CMUYautjaArmorBundle",
                "CMUYautjaClanSword",
                "CMUYautjaWristBladesBundle",
                "CMUYautjaThrallHuntingEquipmentBundle",
                "CMUYautjaFalconDrone",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/hunter/survivor",
            "CMUYautjaStrandedLoadoutVendor",
            YautjaGearRackKind.Stranded,
            "Yautja Hunting Gear Rack",
            rackDescription,
            "pred_vendor_elder_left",
            "cm_vending_equipment_stranded_pred",
            [
                "CMUYautjaStrandedHuntingEquipmentBundle",
                "CMUYautjaStrandedArmorBundle",
                "CMUYautjaQuiverStrapFilled",
                "CMUYautjaFalconDrone",
                "CMUYautjaArrow",
            ],
            [
                "CMUYautjaBadBloodHuntingEquipmentBundle",
                "CMUYautjaBadBloodArmorPatchworkBundle",
                "CMUYautjaFalconDroneBadBlood",
                "CMUYautjaQuiverStrapDynamic",
                "CMUYautjaDynamicArrow",
            ]);

        yield return new Cmss13RackMachineryRow(
            "/obj/structure/machinery/cm_vending/clothing/yautja/hunter/survivor get_listed_products(JOB_BADBLOOD)",
            "CMUYautjaBadBloodLoadoutVendor",
            YautjaGearRackKind.BadBlood,
            "Yautja Hunting Gear Rack",
            rackDescription,
            "pred_vendor_elder_left",
            "cm_vending_equipment_badblood",
            [
                "CMUYautjaBadBloodHuntingEquipmentBundle",
                "CMUYautjaBadBloodArmorPatchworkBundle",
                "CMUYautjaQuiverStrapDynamic",
                "CMUYautjaFalconDroneBadBlood",
                "CMUYautjaDynamicArrow",
            ],
            [
                "CMUYautjaStrandedHuntingEquipmentBundle",
                "CMUYautjaStrandedArmorBundle",
                "CMUYautjaQuiverStrapFilled",
                "CMUYautjaFalconDrone",
                "CMUYautjaArrow",
            ]);
    }

    private static IEnumerable<UtilityCommunicatorRow> Cmss13CommunicatorRows()
    {
        const string communicatorDescription = "A strange Yautja device used for projecting the Yautja's voice to the others in its pack. Similar in function to a standard human radio.";

        yield return new UtilityCommunicatorRow("CMUYautjaCommunicator", "Communicator", communicatorDescription);
        yield return new UtilityCommunicatorRow("CMUYautjaOverseerCommunicator", "Overseer Communicator", communicatorDescription);
        yield return new UtilityCommunicatorRow(
            "CMUYautjaBadBloodCommunicator",
            "Modified Communicator",
            communicatorDescription + " This one has been modified in some way.");
        yield return new UtilityCommunicatorRow(
            "CMUYautjaStrandedCommunicator",
            "Damaged Communicator",
            communicatorDescription + " This one seems damaged and is transmitting on a different frequency.");
    }

    private static IEnumerable<Cmss13CommunicatorChannelRow> Cmss13CommunicatorChannelRows()
    {
        yield return new Cmss13CommunicatorChannelRow(
            "CMUYautjaCommunicator",
            "CMUYautjaEncryptionKey",
            ["CMUYautja"],
            "CMUYautja",
            "Yautja encryption key",
            "A complicated encryption device.");

        yield return new Cmss13CommunicatorChannelRow(
            "CMUYautjaOverseerCommunicator",
            "CMUYautjaOverseerEncryptionKey",
            ["CMUYautja", "CMUYautjaOverseer"],
            "CMUYautjaOverseer",
            "Yautja Overseer encryption key",
            "A complicated encryption device.");

        yield return new Cmss13CommunicatorChannelRow(
            "CMUYautjaBadBloodCommunicator",
            "CMUYautjaBadBloodEncryptionKey",
            ["CMUYautjaBadBlood"],
            "CMUYautjaBadBlood");

        yield return new Cmss13CommunicatorChannelRow(
            "CMUYautjaStrandedCommunicator",
            "CMUYautjaStrandedEncryptionKey",
            ["CMUYautjaStranded"],
            "CMUYautjaStranded");
    }

    private static IEnumerable<Cmss13XenoTrophyRow> Cmss13XenoTrophyRows()
    {
        yield return Skull("CMUYautjaQueenSkullTrophy", "Queen skull", "Skull of a prime hive ruler, mother to many.", "queen_skull");
        yield return Skull("CMUYautjaKingSkullTrophy", "King skull", "Skull of a militant hive ruler, lord of destruction.", "king_skull");
        yield return Skull("CMUYautjaDespoilerSkullTrophy", "Despoiler skull", "Skull of a decrepit wretch, the surface still stinging your hands.", "despoiler_skull");
        yield return Skull("CMUYautjaLurkerSkullTrophy", "Lurker skull", "Skull of a stealthy xenomorph, a nocturnal entity.", "lurker_skull");
        yield return Skull("CMUYautjaHunterSkullTrophy", "Hunter skull", "Skull of a stealthy xenomorph, an ambushing predator.", "hunter_skull");
        yield return Skull("CMUYautjaDeaconSkullTrophy", "Deacon skull", "Skull of an unusual xenomorph, a mysterious specimen.", "deacon_skull");
        yield return Skull("CMUYautjaCorroderSkullTrophy", "Corroder skull", "Skull of an acidic xenomorph, a boiling menace.", "corroder_skull");
        yield return Skull("CMUYautjaWarriorSkullTrophy", "Warrior skull", "Skull of a strong xenomorph, a swift fighter.", "warrior_skull");
        yield return Skull("CMUYautjaDefenderSkullTrophy", "Defender skull", "Skull of a sturdy xenomorph, a bulwark of the hive.", "defender_skull");
        yield return Skull("CMUYautjaPraetorianSkullTrophy", "Praetorian skull", "Skull of a strong xenomorph, jack of all trades, vanguard to the Queen.", "praetorian_skull");
        yield return Skull("CMUYautjaCrusherSkullTrophy", "Crusher skull", "Skull of a powerful xenomorph, capable of shattering defenses.", "crusher_skull");
        yield return Skull("CMUYautjaRavagerSkullTrophy", "Ravager skull", "Skull of a ferocious xenomorph, wielding unmatched destruction.", "ravager_skull");
        yield return Skull("CMUYautjaBoilerSkullTrophy", "Boiler skull", "Skull of a ranged xenomorph, known for explosive acid attacks.", "boiler_skull");
        yield return Skull("CMUYautjaCarrierSkullTrophy", "Carrier skull", "Skull of a diligent xenomorph, a lifeblood worker of the hive.", "carrier_skull");
        yield return Skull("CMUYautjaHivelordSkullTrophy", "Hivelord skull", "Skull of a nurturing xenomorph, devoted to hive construction.", "hivelord_skull");
        yield return Skull("CMUYautjaBurrowerSkullTrophy", "Burrower skull", "Skull of a digging xenomorph, master of subterranean assault.", "burrower_skull");
        yield return Skull("CMUYautjaDroneSkullTrophy", "Drone skull", "Skull of a weak but essential xenomorph, a hive worker.", "drone_skull");
        yield return Skull("CMUYautjaRunnerSkullTrophy", "Runner skull", "Skull of a swift and agile xenomorph, a terror on the prowl.", "runner_skull");
        yield return Skull("CMUYautjaSentinelSkullTrophy", "Sentinel skull", "Skull of an acidic xenomorph, skilled in ranged combat.", "sentinel_skull");
        yield return Skull("CMUYautjaSpitterSkullTrophy", "Spitter skull", "Skull of a highly acidic xenomorph, a venomous ranged attacker.", "spitter_skull");

        yield return Pelt("CMUYautjaQueenPeltTrophy", "Queen pelt", "The pelt of a prime hive ruler, mother to many.", "queen_pelt");
        yield return Pelt("CMUYautjaKingPeltTrophy", "King pelt", "The pelt of a militant hive ruler, lord of destruction.", "king_pelt");
        yield return Pelt("CMUYautjaDespoilerPeltTrophy", "Despoiler pelt", "The pelt of a decrepit wretch, the surface still stinging your hands.", "despoiler_pelt");
        yield return Pelt("CMUYautjaLurkerPeltTrophy", "Lurker pelt", "The pelt of a stealthy xenomorph, an ambushing predator.", "lurker_pelt");
        yield return Pelt("CMUYautjaHunterPeltTrophy", "Hunter pelt", "The pelt of a swift xenomorph, a fearsome ambushing predator.", "hunter_pelt");
        yield return Pelt("CMUYautjaDeaconPeltTrophy", "Deacon pelt", "The pelt of an unusual xenomorph, a mysterious and rare specimen.", "deacon_pelt");
        yield return Pelt("CMUYautjaCorroderPeltTrophy", "Corroder pelt", "The pelt of an acidic xenomorph, exuding caustic menace.", "corroder_pelt");
        yield return Pelt("CMUYautjaWarriorPeltTrophy", "Warrior pelt", "The pelt of a strong xenomorph, a fast and lethal fighter.", "warrior_pelt");
        yield return Pelt("CMUYautjaDefenderPeltTrophy", "Defender pelt", "The pelt of a sturdy xenomorph, a shield of the hive.", "defender_pelt");
        yield return Pelt("CMUYautjaPraetorianPeltTrophy", "Praetorian pelt", "The pelt of a versatile xenomorph, a vanguard to the Queen.", "praetorian_pelt");
        yield return Pelt("CMUYautjaCrusherPeltTrophy", "Crusher pelt", "The pelt of a powerful xenomorph, capable of shattering defenses.", "crusher_pelt");
        yield return Pelt("CMUYautjaRavagerPeltTrophy", "Ravager pelt", "The pelt of a ferocious xenomorph, wielding unmatched destruction.", "ravager_pelt");
        yield return Pelt("CMUYautjaBoilerPeltTrophy", "Boiler pelt", "The pelt of a ranged xenomorph, known for explosive acid attacks.", "boiler_pelt");
        yield return Pelt("CMUYautjaCarrierPeltTrophy", "Carrier pelt", "The pelt of a diligent xenomorph, a lifeblood worker of the hive.", "carrier_pelt");
        yield return Pelt("CMUYautjaHivelordPeltTrophy", "Hivelord pelt", "The pelt of a nurturing xenomorph, devoted to hive construction.", "hivelord_pelt");
        yield return Pelt("CMUYautjaBurrowerPeltTrophy", "Burrower pelt", "The pelt of a digging xenomorph, master of subterranean assault.", "burrower_pelt");
        yield return Pelt("CMUYautjaDronePeltTrophy", "Drone pelt", "The pelt of a weak but essential xenomorph, a hive worker.", "drone_pelt");
        yield return Pelt("CMUYautjaRunnerPeltTrophy", "Runner pelt", "The pelt of a swift and agile xenomorph, a terror on the prowl.", "runner_pelt");
        yield return Pelt("CMUYautjaSentinelPeltTrophy", "Sentinel pelt", "The pelt of an acidic xenomorph, skilled in ranged combat.", "sentinel_pelt");
        yield return Pelt("CMUYautjaSpitterPeltTrophy", "Spitter pelt", "The pelt of a highly acidic xenomorph, a venomous ranged attacker.", "spitter_pelt");
        yield return Pelt("CMUYautjaLarvaPeltTrophy", "Larva pelt", "The hide of a juvenile Xenomorph, a grim trophy from a fledgling that never reached its full potential.", "larva_pelt");

        static Cmss13XenoTrophyRow Skull(string id, string name, string desc, string state)
            => new(id, name, desc, state, YautjaTrophyKind.XenoSkull);

        static Cmss13XenoTrophyRow Pelt(string id, string name, string desc, string state)
            => new(id, name, desc, state, YautjaTrophyKind.XenoPelt);
    }

    private static IEnumerable<Cmss13HumanBoneTrophyRow> Cmss13HumanBoneTrophyRows()
    {
        const string desc = "A bone that appears to be of human origin.";

        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanSkullTrophy", "skull", desc, "skull2", YautjaTrophyKind.HumanSkull);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanLeftArmBoneTrophy", "arm bone", desc, "l_arm", YautjaTrophyKind.HumanLeftArmBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanRightArmBoneTrophy", "arm bone", desc, "r_arm", YautjaTrophyKind.HumanRightArmBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanLeftHandBoneTrophy", "hand bone", desc, "l_hand", YautjaTrophyKind.HumanLeftHandBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanRightHandBoneTrophy", "hand bone", desc, "r_hand", YautjaTrophyKind.HumanRightHandBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanLeftLegBoneTrophy", "leg bone", desc, "l_leg", YautjaTrophyKind.HumanLeftLegBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanRightLegBoneTrophy", "leg bone", desc, "r_leg", YautjaTrophyKind.HumanRightLegBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanLeftFootBoneTrophy", "foot bone", desc, "l_foot", YautjaTrophyKind.HumanLeftFootBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanRightFootBoneTrophy", "foot bone", desc, "r_foot", YautjaTrophyKind.HumanRightFootBone);
        yield return new Cmss13HumanBoneTrophyRow("CMUYautjaHumanRibcageTrophy", "ribcage", desc, "torso", YautjaTrophyKind.HumanRibcage);
    }

    private static IEnumerable<Cmss13ButcherOutputRow> Cmss13ButcherOutputRows()
    {
        const string yautjaItems = "_CMU14/Yautja/yautja_items.rsi";

        yield return new Cmss13ButcherOutputRow(
            "CMUYautjaHumanHide",
            "",
            "",
            "Objects/Materials/materials.rsi",
            "hide",
            Cmss13ButcherOutputKind.StackSheet,
            "CMUYautjaHumanHide");

        yield return new Cmss13ButcherOutputRow(
            "CMUYautjaHumanSpine",
            "skull",
            "A bone that appears to be of human origin.",
            yautjaItems,
            "spine",
            Cmss13ButcherOutputKind.SkeletonLimb);

        yield return new Cmss13ButcherOutputRow(
            "CMUYautjaHumanButcheredRemains",
            "",
            "",
            "_CMU14/HunterShip/effects/blood.rsi",
            "remains",
            Cmss13ButcherOutputKind.EffectDecal);

        yield return new Cmss13ButcherOutputRow(
            "CMUYautjaXenoButcheredRemains",
            "",
            "",
            "_CMU14/HunterShip/effects/blood.rsi",
            "remainsxeno",
            Cmss13ButcherOutputKind.EffectDecal);
    }

    private static IEnumerable<Cmss13McasteItemRow> Cmss13McasteItemRows()
    {
        const string mcaste = "_CMU14/Yautja/mcaste_gear.rsi";
        var bracerStats = new Cmss13ProtectionStats(20, 20, 20, 25);
        var poweredStats = new Cmss13ProtectionStats(35, 50, 35, 40);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaSoldierBracers",
            "militarized bracers",
            "A set of high-tech bracers that are relatively simple when compared to those used in hunting, forgoing most advanced functions in exchange for an auto-self destruct system that activates on death.",
            "Normal",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_ebony",
            SlotFlags.GLOVES,
            bracerStats,
            ItemPredator: true,
            Unacidable: true);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaMcasteHerbContainer",
            "herbs case",
            "A small case packed with Yautja trauma poultices and burn salves.",
            "Small",
            "_RMC14/Objects/Storage/surgical_case.rsi",
            "surgical_case_base",
            null,
            null,
            ItemPredator: false,
            Unacidable: false,
            CheckDescription: false);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaMcasteHerbContainerFilled",
            "herbs case",
            "A small case packed with Yautja trauma poultices and burn salves.",
            "Small",
            "_RMC14/Objects/Storage/surgical_case.rsi",
            "surgical_case_base",
            null,
            null,
            ItemPredator: false,
            Unacidable: false,
            CheckDescription: false);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaPoweredArmor",
            "Nracha-Dte power armor",
            "Produced only by artisans overseen directly by the Council of Ancients, the Nracha-Dte-Type is a powered suit of armor built for war rather than hunting. It is heavy, and absurdly protective.",
            "Normal",
            mcaste,
            "fullarmor_soldier",
            SlotFlags.OUTERCLOTHING,
            poweredStats,
            ItemPredator: true,
            Unacidable: true);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaPoweredArmorEnforcer",
            "Nracha-Dte command power armor",
            "Produced only by artisans overseen directly by the Council of Ancients, the Nracha-Dte-type is a powered suit of armor built for war rather than hunting. It is heavy, and absurdly protective. This one features a ceremonial pauldron labeling the wearer as an Enforcer.",
            "Normal",
            mcaste,
            "fullarmor_soldier_lead",
            SlotFlags.OUTERCLOTHING,
            poweredStats,
            ItemPredator: true,
            Unacidable: true);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaPoweredGreaves",
            "Nracha-Dte armored greaves",
            "The lower half of the M'talt-Type powered armor suit, used exclusively in battle against the most disdainful of dishonorable targets. Like the upper suit, there is very little damage it cannot shrug off.",
            "Normal",
            mcaste,
            "y-boots_powered",
            SlotFlags.FEET,
            poweredStats,
            ItemPredator: true,
            Unacidable: true);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaPoweredHelmet",
            "Nracha-Dte enclosed helmet",
            "A fully-enclosed combat helmet that is fitted around the entire head, rather than acting as a facemask. It nonetheless features the same heads-up display as most clan masks.",
            "Normal",
            mcaste,
            "helmet_powered",
            SlotFlags.HEAD,
            poweredStats,
            ItemPredator: true,
            Unacidable: true,
            BlockPickup: false);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaMilitaryCommunicator",
            "Military Communicator",
            "A strange Yautja device used for projecting the Yautja's voice to the others in its pack. Similar in function to a standard human radio.",
            "Small",
            null,
            null,
            SlotFlags.EARS,
            null,
            ItemPredator: true,
            Unacidable: true,
            BlockPickup: false);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaMilitaryEncryptionKey",
            "Yautja encryption key",
            "A complicated encryption device.",
            "Tiny",
            null,
            null,
            null,
            null,
            ItemPredator: false,
            Unacidable: false);

        yield return new Cmss13McasteItemRow(
            "CMUYautjaCannonPack",
            "plasma cannon pack",
            "A heavy back-mounted powerpack for supporting a set of dual plasma cannons. The pack's entire volume is taken up by capacitors and electronics used in operating the cannons, remotely linked to a bracer for operation.",
            "Normal",
            mcaste,
            "cannonpack",
            SlotFlags.BACK,
            null,
            ItemPredator: false,
            Unacidable: false);
    }

    public readonly record struct MedicompSourceRow(string Id, IReadOnlyDictionary<string, int> ExpectedContents);

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

    public readonly record struct BracerFabricatedMedicalRow(
        string LocalId,
        string SourceSpawnPath,
        MedicompPayloadRow Payload);

    private static IEnumerable<BracerFabricatedMedicalRow> Cmss13BracerFabricatedMedicalRows()
    {
        yield return new BracerFabricatedMedicalRow(
            "CMUYautjaStabilisingCrystal",
            "/obj/item/reagent_container/hypospray/autoinjector/yautja",
            new MedicompPayloadRow(
                "CMUYautjaStabilisingCrystal",
                "/obj/item/reagent_container/hypospray/autoinjector/yautja",
                "yautja autoinjector",
                "An alien autoinjector loaded with a strong trauma and burn treatment cocktail.",
                "Small",
                ["CMAutoInjector", "CMUYautjaMedicompItem"],
                YautjaMedicalItem: true,
                Hypospray: new MedicompPayloadHyposprayRow(
                    45,
                    135,
                    new Dictionary<string, int>
                    {
                        ["CMBicaridine"] = 45,
                        ["CMKelotane"] = 45,
                        ["CMTricordrazine"] = 45,
                    })));

        yield return new BracerFabricatedMedicalRow(
            "CMUYautjaHumanStabilisingCrystal",
            "/obj/item/reagent_container/hypospray/autoinjector/yautja/thrall",
            new MedicompPayloadRow(
                "CMUYautjaHumanStabilisingCrystal",
                "/obj/item/reagent_container/hypospray/autoinjector/yautja/thrall",
                "yautja autoinjector",
                "An alien autoinjector loaded with a strong trauma and burn treatment cocktail adapted for Yautja thralls.",
                "Small",
                ["CMAutoInjector", "CMUYautjaMedicompItem"],
                YautjaMedicalItem: true,
                Hypospray: new MedicompPayloadHyposprayRow(
                    45,
                    135,
                    new Dictionary<string, int>
                    {
                        ["CMBicaridine"] = 45,
                        ["CMKelotane"] = 45,
                        ["CMTricordrazine"] = 45,
                    })));

        yield return new BracerFabricatedMedicalRow(
            "CMUYautjaHealingCapsule",
            "/obj/item/tool/surgery/healing_gel",
            new MedicompPayloadRow(
                "CMUYautjaHealingCapsule",
                "/obj/item/tool/surgery/healing_gel",
                "healing gel",
                "A dense alien coagulant that knits together broad trauma.",
                "Small",
                ["Brutepack", "CMUYautjaMedicompItem"],
                StackType: "CMUYautjaHealingGel",
                StackCount: 2,
                StackMaxCount: 6));
    }

    private static IEnumerable<MedicompPayloadRow> Cmss13MedicompPayloadRows()
    {
        yield return new MedicompPayloadRow(
            "CMUYautjaAdvancedBruisePack",
            "/obj/item/stack/medical/advanced/bruise_pack/predator",
            "predator advanced trauma pack",
            "A compact alien poultice for closing savage wounds.",
            "Small",
            ["CMTraumaKit", "CMUYautjaHerbalMedicine"],
            YautjaMedicalItem: true);

        yield return new MedicompPayloadRow(
            "CMUYautjaAdvancedOintment",
            "/obj/item/stack/medical/advanced/ointment/predator",
            "predator advanced ointment",
            "A cool alien salve for sealing burns and caustic injuries.",
            "Small",
            ["CMBurnKit", "CMUYautjaHerbalMedicine"],
            YautjaMedicalItem: true);

        yield return new MedicompPayloadRow(
            "CMUYautjaHealingGel",
            "/obj/item/tool/surgery/healing_gel",
            "healing gel",
            "A dense alien coagulant that knits together broad trauma.",
            "Small",
            ["Brutepack", "CMUYautjaMedicompItem"],
            StackType: "CMUYautjaHealingGel",
            StackCount: 2,
            StackMaxCount: 6);

        yield return new MedicompPayloadRow(
            "CMUYautjaStabilizerGel",
            "/obj/item/tool/surgery/stabilizer_gel",
            "stabilizer gel",
            "A fast-setting alien gel for buying time through shock and blood loss.",
            "Small",
            ["Ointment", "CMUYautjaMedicompItem"],
            StackType: "CMUYautjaStabilizerGel",
            StackCount: 1,
            StackMaxCount: 6);

        yield return new MedicompPayloadRow(
            "CMUYautjaWoundClamp",
            "/obj/item/tool/surgery/wound_clamp",
            "wound clamp",
            "A predator surgical clamp made to seal catastrophic bleeding.",
            "Small",
            ["CMHemostat", "CMUYautjaMedicompItem"]);

        yield return new MedicompPayloadRow(
            "CMUYautjaHealingGun",
            "/obj/item/tool/surgery/healing_gun",
            "healing gun",
            "A compact alien applicator for rapid emergency treatment.",
            "Small",
            ["CMUYautjaMedicompItem"],
            Healing: new MedicompPayloadHealingRow(-30));

        yield return new MedicompPayloadRow(
            "CMUYautjaAlienHealthAnalyzer",
            "/obj/item/device/healthanalyzer/alien",
            "alien health analyzer",
            "A bio-scanner tuned for alien physiology and battlefield triage.",
            "Small",
            ["DiscreteHealthAnalyzer", "CMUYautjaMedicompItem"]);

        yield return new MedicompPayloadRow(
            "CMUYautjaAutoInjector",
            "/obj/item/reagent_container/hypospray/autoinjector/yautja",
            "yautja autoinjector",
            "An alien autoinjector loaded with a strong trauma and burn treatment cocktail.",
            "Small",
            ["CMAutoInjector", "CMUYautjaMedicompItem"],
            YautjaMedicalItem: true,
            Hypospray: new MedicompPayloadHyposprayRow(
                45,
                135,
                new Dictionary<string, int>
                {
                    ["CMBicaridine"] = 45,
                    ["CMKelotane"] = 45,
                    ["CMTricordrazine"] = 45,
                }));

        yield return new MedicompPayloadRow(
            "CMUYautjaThrallAutoInjector",
            "/obj/item/reagent_container/hypospray/autoinjector/yautja/thrall",
            "yautja autoinjector",
            "An alien autoinjector loaded with a strong trauma and burn treatment cocktail adapted for Yautja thralls.",
            "Small",
            ["CMAutoInjector", "CMUYautjaMedicompItem"],
            YautjaMedicalItem: true,
            Hypospray: new MedicompPayloadHyposprayRow(
                45,
                135,
                new Dictionary<string, int>
                {
                    ["CMBicaridine"] = 45,
                    ["CMKelotane"] = 45,
                    ["CMTricordrazine"] = 45,
                }));

        yield return new MedicompPayloadRow(
            "CMUYautjaHerbalCase",
            "/obj/item/storage/herbal_case",
            "herbs case",
            "A small case packed with Yautja trauma poultices and burn salves.",
            "Small",
            ["CMSurgicalCase", "CMUYautjaMedicompItem"]);
    }

    private static IEnumerable<MedicompSourceRow> Cmss13MedicompRows()
    {
        yield return new MedicompSourceRow(
            "CMUYautjaMedicompFull",
            new Dictionary<string, int>
            {
                ["CMUYautjaStabilizerGel"] = 1,
                ["CMUYautjaHealingGun"] = 1,
                ["CMUYautjaWoundClamp"] = 1,
                ["CMUYautjaAlienHealthAnalyzer"] = 1,
                ["CMUYautjaAutoInjector"] = 3,
                ["CMUYautjaHealingGel"] = 6,
            });

        yield return new MedicompSourceRow(
            "CMUYautjaMedicompThrall",
            new Dictionary<string, int>
            {
                ["CMUYautjaStabilizerGel"] = 1,
                ["CMUYautjaHealingGun"] = 1,
                ["CMUYautjaWoundClamp"] = 1,
                ["CMUYautjaAlienHealthAnalyzer"] = 1,
                ["CMUYautjaThrallAutoInjector"] = 3,
                ["CMUYautjaHealingGel"] = 6,
            });

        yield return new MedicompSourceRow(
            "CMUYautjaMedicompSurvivor",
            new Dictionary<string, int>
            {
                ["CMUYautjaStabilizerGel"] = 1,
                ["CMUYautjaHealingGun"] = 1,
                ["CMUYautjaWoundClamp"] = 1,
                ["CMUYautjaAlienHealthAnalyzer"] = 1,
                ["CMUYautjaAutoInjector"] = 3,
                ["CMUYautjaHealingGel"] = 6,
                ["CMUYautjaHerbalCase"] = 1,
            });
    }

    public readonly record struct FireIntensityResistanceRow(string Id, int IntensityResistance);

    private static IEnumerable<FireIntensityResistanceRow> Cmss13FireIntensityResistanceRows()
    {
        const int standard = 10;
        const int heavy = 20;

        yield return new FireIntensityResistanceRow("CMUYautjaAncientAlienArmor", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaClanArmor", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaClanArmorScalable", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaHeavyClanArmor", heavy);

        yield return new FireIntensityResistanceRow("CMUYautjaMask", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaMaskScalable", standard);

        yield return new FireIntensityResistanceRow("CMUYautjaAncientAlienGreaves", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaClanGreaves", standard);
        yield return new FireIntensityResistanceRow("CMUYautjaClanGreavesScalable", standard);

        foreach (var row in Cmss13BadBloodArmorSetRows())
        {
            yield return new FireIntensityResistanceRow(row.ArmorId, standard);
            yield return new FireIntensityResistanceRow(row.MaskId, standard);
            yield return new FireIntensityResistanceRow(row.GreavesId, standard);
        }

        foreach (var row in Cmss13ThrallArmorMaterialRows())
        {
            yield return new FireIntensityResistanceRow(row.ArmorId, standard);
            yield return new FireIntensityResistanceRow(row.MaskId, standard);
            yield return new FireIntensityResistanceRow(row.GreavesId, standard);
        }
    }

    public readonly record struct AdultMandatoryArmorAndMeshRow(
        string Id,
        string Name,
        string Description,
        SlotFlags Slots,
        Cmss13ProtectionStats Stats,
        bool SourceUnacidable,
        bool BlockPickup = true,
        float? AntiHugMaxCount = null,
        bool SourceArmorAllowedList = false);

    private static IEnumerable<AdultMandatoryArmorAndMeshRow> Cmss13AdultMandatoryArmorAndMeshRows()
    {
        // Local CMArmor has no laser, energy, rad or internaldamage fields, so this table covers
        // the CMSS13 protection tiers that have existing local equivalents.
        const int low = 10;
        const int mediumLow = 35;
        const int medium = 40;
        const int mediumHigh = 45;
        const int high = 50;

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaAncientAlienMesh",
            "ancient alien mesh suit",
            "A strange alloy weave in the form of a vest. It feels cold with an alien weight.",
            SlotFlags.INNERCLOTHING,
            new Cmss13ProtectionStats(low, mediumLow, medium, mediumHigh),
            false);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaBodyMesh",
            "body mesh",
            "A set of very fine chainlink in a meshwork for comfort and utility.",
            SlotFlags.INNERCLOTHING,
            new Cmss13ProtectionStats(low, medium, mediumHigh, high),
            false);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaAncientAlienArmor",
            "ancient alien armor",
            "Ancient armor made from a strange alloy. It feels cold with an alien weight.",
            SlotFlags.OUTERCLOTHING,
            new Cmss13ProtectionStats(mediumLow, medium, medium, mediumHigh),
            true,
            SourceArmorAllowedList: true);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaClanArmor",
            "clan armor",
            "A suit of armor with light padding. It looks old, yet functional.",
            SlotFlags.OUTERCLOTHING,
            new Cmss13ProtectionStats(mediumLow, mediumHigh, mediumHigh, high),
            true,
            SourceArmorAllowedList: true);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaMask",
            "clan mask",
            "A beautifully designed metallic face mask, both ornate and functional.",
            SlotFlags.MASK | SlotFlags.SUITSTORAGE,
            new Cmss13ProtectionStats(medium, high, mediumHigh, high),
            true,
            false,
            100);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaAncientAlienGreaves",
            "ancient alien greaves",
            "Greaves made from scraps of cloth and a strange alloy. They feel cold with an alien weight.",
            SlotFlags.FEET,
            new Cmss13ProtectionStats(mediumLow, mediumHigh, medium, mediumHigh),
            true);

        yield return new AdultMandatoryArmorAndMeshRow(
            "CMUYautjaClanGreaves",
            "clan greaves",
            "A pair of armored, perfectly balanced boots. Ideal for running through the jungle.",
            SlotFlags.FEET,
            new Cmss13ProtectionStats(mediumHigh, high, mediumHigh, high),
            true);
    }

    private static IEnumerable<RackPriorityRow> Cmss13YautjaRackPriorityRows()
    {
        foreach (var rack in new[]
                 {
                     "CMUYautjaLoadoutVendor",
                     "CMUYautjaElderLoadoutVendor",
                 })
        {
            yield return new RackPriorityRow(rack, "Essential Hunting Supplies", "CMUYautjaHuntingEquipmentBundle", true, false);
            yield return new RackPriorityRow(rack, "Essential Hunting Supplies", "CMUYautjaArmorBundle", true, false);
            yield return new RackPriorityRow(rack, "Main Weapons (CHOOSE 1)", "CMUYautjaClanSword", false, true);
            yield return new RackPriorityRow(rack, "Bracer Attachments", "CMUYautjaWristBladesBundle", true, false);
        }

        yield return new RackPriorityRow("CMUYautjaYoungbloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaYoungbloodHuntingEquipmentBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaYoungbloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaArmorBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaYoungbloodLoadoutVendor", "Main Weapons (CHOOSE 1)", "CMUYautjaClanSword", false, true);
        yield return new RackPriorityRow("CMUYautjaYoungbloodLoadoutVendor", "Bracer Attachments", "CMUYautjaWristBladesBundle", true, false);

        yield return new RackPriorityRow("CMUYautjaLoadoutVendor", "Support Equipment (CHOOSE 2)", "CMUYautjaFalconDrone", false, false);
        yield return new RackPriorityRow("CMUYautjaLoadoutVendor", "Spare Equipment", "CMUYautjaFalconDrone", false, false);

        yield return new RackPriorityRow("CMUYautjaThrallLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaThrallHuntingEquipmentBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaThrallLoadoutVendor", "Armor Material (CHOOSE 1)", "CMUYautjaThrallArmorEbonyBundle", false, true);
        yield return new RackPriorityRow("CMUYautjaThrallLoadoutVendor", "Main Weapons (CHOOSE 1)", "CMUYautjaClanSword", false, false);

        yield return new RackPriorityRow("CMUYautjaBloodedThrallLoadoutVendor", "Blooded Equipment", "CMUYautjaBloodedThrallEquipmentBundle", false, false);
        yield return new RackPriorityRow("CMUYautjaBloodedThrallLoadoutVendor", "Blooded Bracer Material (CHOOSE 1)", "CMUYautjaBloodedThrallBracerEbonyBundle", false, true);
        yield return new RackPriorityRow("CMUYautjaBloodedThrallLoadoutVendor", "Clothing Accessory (CHOOSE 1)", "CMUYautjaCapeQuarter", true, false);

        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaStrandedHuntingEquipmentBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaStrandedArmorBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Main Weapons (CHOOSE 1)", "CMUYautjaClanSword", false, true);
        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Bracer Attachments", "CMUYautjaWristBladesBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Support Equipment (CHOOSE 2)", "CMUYautjaFalconDrone", false, false);
        yield return new RackPriorityRow("CMUYautjaStrandedLoadoutVendor", "Spare Equipment", "CMUYautjaFalconDrone", false, false);

        yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Essential Hunting Supplies", "CMUYautjaBadBloodHuntingEquipmentBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Main Weapons (CHOOSE 1)", "CMUYautjaClanSword", false, true);
        yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Bracer Attachments", "CMUYautjaWristBladesBundle", true, false);
        yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Support Equipment (CHOOSE 2)", "CMUYautjaFalconDroneBadBlood", false, false);
        yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Spare Equipment", "CMUYautjaFalconDroneBadBlood", false, false);

        foreach (var id in Cmss13BadBloodArmorSetBundleIds())
            yield return new RackPriorityRow("CMUYautjaBadBloodLoadoutVendor", "Armor Set", id, true, false);
    }

    private static IEnumerable<string> Cmss13BadBloodArmorSetBundleIds()
    {
        yield return "CMUYautjaBadBloodArmorPatchworkBundle";
        yield return "CMUYautjaBadBloodArmorPatchworkAltBundle";
        yield return "CMUYautjaBadBloodArmorLunaticBundle";
        yield return "CMUYautjaBadBloodArmorScavengerBundle";
        yield return "CMUYautjaBadBloodArmorScavengerAltBundle";
        yield return "CMUYautjaBadBloodArmorVenatorBundle";
        yield return "CMUYautjaBadBloodArmorCommandoBundle";
        yield return "CMUYautjaBadBloodArmorCommandoAltBundle";
        yield return "CMUYautjaBadBloodArmorEmissaryBundle";
    }

    public readonly record struct StrandedScalableEquipmentRow(
        string Id,
        string Name,
        string Description,
        SlotFlags Slots,
        int Melee,
        int Bullet,
        int Bio,
        int ExplosionArmor,
        string DamagedExamineText,
        bool SourceUnacidable,
        bool BlockPickup = true,
        float? AntiHugMaxCount = null);

    private static IEnumerable<StrandedScalableEquipmentRow> Cmss13StrandedScalableEquipmentRows()
    {
        // Local CMArmor has no laser, energy, rad or internaldamage fields, so this table covers
        // the CMSS13 armor tiers that have existing local equivalents.
        const int low = 10;
        const int mediumLow = 35;
        const int medium = 40;
        const int mediumHigh = 45;
        const int high = 50;

        yield return new StrandedScalableEquipmentRow(
            "CMUYautjaBodyMeshScalable",
            "body mesh",
            "A set of very fine chainlink in a meshwork for comfort and utility.",
            SlotFlags.INNERCLOTHING,
            low,
            mediumLow,
            medium,
            mediumHigh,
            "It has been worn from long use and poor maintenance.",
            false);

        yield return new StrandedScalableEquipmentRow(
            "CMUYautjaClanArmorScalable",
            "clan armor",
            "A suit of armor with light padding. It looks old, yet functional.",
            SlotFlags.OUTERCLOTHING,
            mediumLow,
            medium,
            medium,
            high,
            "It has been damaged by long use and poor maintenance.",
            true);

        yield return new StrandedScalableEquipmentRow(
            "CMUYautjaMaskScalable",
            "clan mask",
            "A beautifully designed metallic face mask, both ornate and functional.",
            SlotFlags.MASK | SlotFlags.SUITSTORAGE,
            medium,
            mediumHigh,
            medium,
            high,
            "It has been worn from long use and poor maintenance.",
            true,
            false,
            30);

        yield return new StrandedScalableEquipmentRow(
            "CMUYautjaClanGreavesScalable",
            "clan greaves",
            "A pair of armored, perfectly balanced boots. Ideal for running through the jungle.",
            SlotFlags.FEET,
            medium,
            mediumHigh,
            medium,
            mediumHigh,
            "They have been damaged by long use and poor maintenance.",
            true);
    }

    public readonly record struct StoneFlavorGearRow(
        string Id,
        string Name,
        string Description,
        SlotFlags Slots,
        string SpritePath,
        string IconState,
        Cmss13ProtectionStats Stats,
        bool AllowedStorage = false,
        float? AntiHugMaxCount = null);

    private static IEnumerable<StoneFlavorGearRow> Cmss13StoneFlavorGearRows()
    {
        yield return new StoneFlavorGearRow(
            "CMUYautjaStoneArmor",
            "alien stone armor",
            "A suit of armor made entirely out of stone. Looks incredibly heavy.",
            SlotFlags.OUTERCLOTHING,
            "_CMU14/Yautja/armor_heavy_clan.rsi",
            "icon",
            new Cmss13ProtectionStats(40, 50, 45, 50),
            AllowedStorage: true);

        yield return new StoneFlavorGearRow(
            "CMUYautjaStoneMask",
            "alien stone mask",
            "A beautifully designed face mask, ornate but non-functional and made entirely of stone.",
            SlotFlags.MASK | SlotFlags.SUITSTORAGE,
            "_CMU14/Yautja/masks/pred_mask1_ebony.rsi",
            "icon",
            new Cmss13ProtectionStats(10, 10, 0, 10));

        yield return new StoneFlavorGearRow(
            "CMUYautjaStoneGreaves",
            "alien stone greaves",
            "A pair of armored, perfectly balanced boots. Perfect for running through cement because they're incredibly heavy.",
            SlotFlags.FEET,
            "_CMU14/Yautja/greaves_clan_2.rsi",
            "icon",
            new Cmss13ProtectionStats(40, 50, 45, 50));
    }

    private static IEnumerable<string> Cmss13ScalableRepairPrototypeIds()
    {
        yield return "CMUYautjaBodyMeshScalable";
        yield return "CMUYautjaClanArmorScalable";
        yield return "CMUYautjaMaskScalable";
        yield return "CMUYautjaClanGreavesScalable";

        foreach (var row in Cmss13BadBloodArmorSetRows())
        {
            yield return row.ArmorId;
            yield return row.MaskId;
            yield return row.GreavesId;
        }

        yield return "CMUYautjaBadBloodArmorBane";
        yield return "CMUYautjaMaskBadBloodBane";
        yield return "CMUYautjaBadBloodGreavesBane";
    }

    private static IEnumerable<string> Cmss13KnifeGreavesPrototypeIds()
    {
        yield return "CMUYautjaClanGreaves";
        yield return "CMUYautjaClanGreavesScalable";

        foreach (var row in Cmss13BadBloodArmorSetRows())
            yield return row.GreavesId;

        yield return "CMUYautjaBadBloodGreavesBane";

        foreach (var row in Cmss13ThrallArmorMaterialRows())
            yield return row.GreavesId;
    }

    private static IEnumerable<string> Cmss13HunterAllowedItemGreavesPrototypeIds()
    {
        yield return "CMUYautjaClanGreaves";
        yield return "CMUYautjaClanGreavesScalable";

        foreach (var row in Cmss13BadBloodArmorSetRows())
            yield return row.GreavesId;

        yield return "CMUYautjaBadBloodGreavesBane";
    }

    private static IEnumerable<string> Cmss13ThrallAllowedItemGreavesPrototypeIds()
    {
        foreach (var row in Cmss13ThrallArmorMaterialRows())
            yield return row.GreavesId;
    }

    public readonly record struct BadBloodArmorSetRow(
        string ArmorId,
        string MaskId,
        string GreavesId,
        string ArmorSprite,
        string MaskSprite,
        string GreavesSprite,
        Cmss13ArmorStats ArmorStats,
        Cmss13ArmorStats MaskStats,
        Cmss13ArmorStats GreavesStats,
        string ArmorName = "clan armor",
        string ArmorDescription = "A suit of armor with light padding. It looks old, yet functional.",
        string MaskName = "clan mask",
        string MaskDescription = "A beautifully designed metallic face mask, both ornate and functional.",
        string GreavesName = "clan greaves",
        string GreavesDescription = "A pair of armored, perfectly balanced boots. Ideal for running through the jungle.");

    public readonly record struct BadBloodEmissaryCamoRow(
        string ArmorId,
        string MaskId,
        string GreavesId,
        string ArmorSprite,
        string MaskSprite,
        string GreavesSprite);

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

    private static IEnumerable<BadBloodArmorSetRow> Cmss13BadBloodArmorSetRows()
    {
        // Local CMArmor has no laser, energy, rad or internaldamage fields, so this table covers
        // the CMSS13 scalable armor tiers that have existing local equivalents.
        const int mediumLow = 35;
        const int medium = 40;
        const int mediumHigh = 45;
        const int high = 50;

        var armorStats = new Cmss13ArmorStats(
            mediumLow,
            medium,
            medium,
            high,
            "It has been damaged by long use and poor maintenance.");
        var maskStats = new Cmss13ArmorStats(
            medium,
            mediumHigh,
            medium,
            high,
            "It has been worn from long use and poor maintenance.");
        var greavesStats = new Cmss13ArmorStats(
            medium,
            mediumHigh,
            medium,
            mediumHigh,
            "They have been damaged by long use and poor maintenance.");

        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorPatchwork",
            "CMUYautjaMaskBadBloodPatchwork",
            "CMUYautjaBadBloodGreavesPatchwork",
            "_CMU14/Yautja/armor_badblood_patchwork.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_patchwork.rsi",
            "_CMU14/Yautja/greaves_badblood_patchwork.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorPatchworkAlt",
            "CMUYautjaMaskBadBloodPatchworkAlt",
            "CMUYautjaBadBloodGreavesPatchworkAlt",
            "_CMU14/Yautja/armor_badblood_patchwork_alt.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_patchworkalt.rsi",
            "_CMU14/Yautja/greaves_badblood_patchwork_alt.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorLunatic",
            "CMUYautjaMaskBadBloodLunatic",
            "CMUYautjaBadBloodGreavesLunatic",
            "_CMU14/Yautja/armor_badblood_lunatic.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_lunatic.rsi",
            "_CMU14/Yautja/greaves_badblood_lunatic.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorScavenger",
            "CMUYautjaMaskBadBloodScav",
            "CMUYautjaBadBloodGreavesScavenger",
            "_CMU14/Yautja/armor_badblood_scavenger.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_scav.rsi",
            "_CMU14/Yautja/greaves_badblood_scavenger.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorScavengerAlt",
            "CMUYautjaMaskBadBloodScavAlt",
            "CMUYautjaBadBloodGreavesScavengerAlt",
            "_CMU14/Yautja/armor_badblood_scavenger_alt.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_scavalt.rsi",
            "_CMU14/Yautja/greaves_badblood_scavenger_alt.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorVenator",
            "CMUYautjaMaskBadBloodVenator",
            "CMUYautjaBadBloodGreavesVenator",
            "_CMU14/Yautja/armor_badblood_venator.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_venator.rsi",
            "_CMU14/Yautja/greaves_badblood_venator.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorCommando",
            "CMUYautjaMaskBadBloodCommando",
            "CMUYautjaBadBloodGreavesCommando",
            "_CMU14/Yautja/armor_badblood_commando.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_commando.rsi",
            "_CMU14/Yautja/greaves_badblood_commando.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaBadBloodArmorCommandoAlt",
            "CMUYautjaMaskBadBloodCommandoAlt",
            "CMUYautjaBadBloodGreavesCommandoAlt",
            "_CMU14/Yautja/armor_badblood_commando_alt.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_commandoalt.rsi",
            "_CMU14/Yautja/greaves_badblood_commando_alt.rsi",
            armorStats,
            maskStats,
            greavesStats);
        yield return new BadBloodArmorSetRow(
            "CMUYautjaEmissaryArmorCamoConforming",
            "CMUYautjaMaskBadBloodEmissaryClassic",
            "CMUYautjaEmissaryGreavesCamoConforming",
            "_CMU14/Yautja/armor_emissary_classic.rsi",
            "_CMU14/Yautja/masks/pred_mask_bb_emissary_classic.rsi",
            "_CMU14/Yautja/greaves_emissary_classic.rsi",
            armorStats,
            maskStats,
            greavesStats,
            "YM4 pattern clan armor",
            "A suit of oversized armor built from M3 pattern plating and Smart-Gunner mesh, built for something larger than any normal man.",
            "clan mask",
            "A beautifully designed metallic face mask, both ornate and functional.",
            "clan combat boots",
            "A pair of armored boots modified with human armor plating, though still scaled to fit a hunter.");
    }

    private static IEnumerable<BadBloodEmissaryCamoRow> Cmss13BadBloodEmissaryCamoRows()
    {
        foreach (var (suffix, camo) in new[]
                 {
                     ("Classic", "classic"),
                     ("Desert", "desert"),
                     ("Jungle", "jungle"),
                     ("Snow", "snow"),
                     ("Urban", "urban"),
                     ("CamoConforming", "classic"),
                 })
        {
            yield return new BadBloodEmissaryCamoRow(
                $"CMUYautjaEmissaryArmor{suffix}",
                suffix == "Classic" || suffix == "CamoConforming"
                    ? "CMUYautjaMaskBadBloodEmissaryClassic"
                    : $"CMUYautjaMaskBadBloodEmissary{suffix}",
                $"CMUYautjaEmissaryGreaves{suffix}",
                $"_CMU14/Yautja/armor_emissary_{camo}.rsi",
                $"_CMU14/Yautja/masks/pred_mask_bb_emissary_{camo}.rsi",
                $"_CMU14/Yautja/greaves_emissary_{camo}.rsi");
        }
    }

    private static IEnumerable<(CamouflageType Type, string ArmorSprite, string GreavesSprite)> Cmss13BadBloodEmissaryRuntimeCamoRows()
    {
        foreach (var (type, camo) in new[]
                 {
                     (CamouflageType.Classic, "classic"),
                     (CamouflageType.Desert, "desert"),
                     (CamouflageType.Jungle, "jungle"),
                     (CamouflageType.Snow, "snow"),
                     (CamouflageType.Urban, "urban"),
                 })
        {
            yield return (
                type,
                $"_CMU14/Yautja/armor_emissary_{camo}.rsi",
                $"_CMU14/Yautja/greaves_emissary_{camo}.rsi");
        }
    }

    private static IEnumerable<string> Cmss13BaseYautjaArmorAllowedStorageIds()
    {
        yield return "CMUYautjaAncientAlienArmor";
        yield return "CMUYautjaClanArmor";
        yield return "CMUYautjaClanArmorScalable";

        foreach (var material in new[] { "Bronze", "Silver", "Crimson", "Bone" })
        {
            yield return $"CMUYautjaClanArmor{material}";

            for (var i = 2; i <= 8; i++)
                yield return $"CMUYautjaClanArmor{material}{i}";
        }

        for (var i = 2; i <= 8; i++)
            yield return $"CMUYautjaClanArmor{i}";

        foreach (var row in Cmss13BadBloodArmorSetRows())
            yield return row.ArmorId;

        foreach (var row in Cmss13BadBloodEmissaryCamoRows())
            yield return row.ArmorId;
    }

    public readonly record struct ThrallArmorMaterialRow(
        string BundleId,
        string ArmorId,
        string MaskId,
        string GreavesId,
        string ArmorSprite,
        string MaskSprite,
        string GreavesSprite,
        string ArmorName = "alien armor",
        string ArmorDescription = "Armor made from a strange alloy. It feels cold with an alien weight. It has been adapted to carry both human and alien melee weaponry.",
        string MaskName = "alien mask",
        string MaskDescription = "A simplistic metallic face mask with advanced capabilities.",
        string GreavesName = "alien greaves",
        string GreavesDescription = "Greaves made from scraps of cloth and a strange alloy. They feel cold with an alien weight. They have been adapted for compatibility with human equipment.")
    {
        public Cmss13ProtectionStats ArmorStats => new(45, 45, 45, 45);
        public Cmss13ProtectionStats MaskStats => new(40, 45, 40, 45);
        public Cmss13ProtectionStats GreavesStats => new(35, 45, 40, 45);
        public float MaskAntiHugMaxCount => 5;
    }

    private static IEnumerable<ThrallArmorMaterialRow> Cmss13ThrallArmorMaterialRows()
    {
        yield return new ThrallArmorMaterialRow(
            "CMUYautjaThrallArmorEbonyBundle",
            "CMUYautjaThrallArmorEbony",
            "CMUYautjaMaskThrallEbony",
            "CMUYautjaThrallGreavesEbony",
            "_CMU14/Yautja/armor_thrall_ebony.rsi",
            "_CMU14/Yautja/masks/thrallmask_ebony.rsi",
            "_CMU14/Yautja/greaves_thrall_ebony.rsi");
        yield return new ThrallArmorMaterialRow(
            "CMUYautjaThrallArmorSilverBundle",
            "CMUYautjaThrallArmorSilver",
            "CMUYautjaMaskThrallSilver",
            "CMUYautjaThrallGreavesSilver",
            "_CMU14/Yautja/armor_thrall_silver.rsi",
            "_CMU14/Yautja/masks/thrallmask_silver.rsi",
            "_CMU14/Yautja/greaves_thrall_silver.rsi");
        yield return new ThrallArmorMaterialRow(
            "CMUYautjaThrallArmorGoldBundle",
            "CMUYautjaThrallArmorGold",
            "CMUYautjaMaskThrallGold",
            "CMUYautjaThrallGreavesGold",
            "_CMU14/Yautja/armor_thrall_gold.rsi",
            "_CMU14/Yautja/masks/thrallmask_gold.rsi",
            "_CMU14/Yautja/greaves_thrall_gold.rsi");
        yield return new ThrallArmorMaterialRow(
            "CMUYautjaThrallArmorCrimsonBundle",
            "CMUYautjaThrallArmorCrimson",
            "CMUYautjaMaskThrallCrimson",
            "CMUYautjaThrallGreavesCrimson",
            "_CMU14/Yautja/armor_thrall_crimson.rsi",
            "_CMU14/Yautja/masks/thrallmask_crimson.rsi",
            "_CMU14/Yautja/greaves_thrall_crimson.rsi");
        yield return new ThrallArmorMaterialRow(
            "CMUYautjaThrallArmorBoneBundle",
            "CMUYautjaThrallArmorBone",
            "CMUYautjaMaskThrallBone",
            "CMUYautjaThrallGreavesBone",
            "_CMU14/Yautja/armor_thrall_bone.rsi",
            "_CMU14/Yautja/masks/thrallmask_bone.rsi",
            "_CMU14/Yautja/greaves_thrall_bone.rsi");
    }

    public readonly record struct BloodedThrallBracerMaterialRow(
        string BundleId,
        string BracerId,
        string Sprite,
        string State);

    private static IEnumerable<BloodedThrallBracerMaterialRow> Cmss13BloodedThrallBracerMaterialRows()
    {
        yield return new BloodedThrallBracerMaterialRow(
            "CMUYautjaBloodedThrallBracerEbonyBundle",
            "CMUYautjaBloodedThrallBracer",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_ebony");
        yield return new BloodedThrallBracerMaterialRow(
            "CMUYautjaBloodedThrallBracerSilverBundle",
            "CMUYautjaBloodedThrallBracerSilver",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_silver");
        yield return new BloodedThrallBracerMaterialRow(
            "CMUYautjaBloodedThrallBracerGoldBundle",
            "CMUYautjaBloodedThrallBracerGold",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_bronze");
        yield return new BloodedThrallBracerMaterialRow(
            "CMUYautjaBloodedThrallBracerCrimsonBundle",
            "CMUYautjaBloodedThrallBracerCrimson",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_crimson");
        yield return new BloodedThrallBracerMaterialRow(
            "CMUYautjaBloodedThrallBracerBoneBundle",
            "CMUYautjaBloodedThrallBracerBone",
            "_CMU14/Yautja/bracer.rsi",
            "bracer_bone");
    }

    public readonly record struct Cmss13RackedMeleeRow(
        string Id,
        string SourcePath,
        string Name,
        string Description,
        string Size,
        SlotFlags? Slots,
        bool Sharp,
        float? AttackRate,
        float? DamageTotal,
        float? ThrowDamageTotal,
        bool ItemPredator = true,
        bool Unacidable = true,
        bool RequiresWield = false,
        float? WieldBonusDamageTotal = null,
        bool LandAtCursor = false,
        bool Recallable = false,
        bool ToggleTinyStorage = false,
        float? ToggleStorageDamageTotal = null);

    private static IEnumerable<Cmss13RackedMeleeRow> Cmss13RackedMeleeRows()
    {
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaChainwhip",
            "/obj/item/weapon/yautja/chain",
            "chainwhip",
            "A segmented, lightweight whip made of durable, acid-resistant metal. Not very common among Yautja Hunters, but still a dangerous weapon capable of shredding prey.",
            "Normal",
            SlotFlags.BELT,
            true,
            1.25f,
            30,
            25,
            Recallable: false);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDualWarScythe",
            "/obj/item/weapon/yautja/scythe",
            "dual war scythe",
            "A huge, incredibly sharp dual blade used for hunting dangerous prey. This weapon is commonly carried by Yautja who wish to disable and slice apart their foes.",
            "Large",
            SlotFlags.BACK | SlotFlags.BELT,
            true,
            null,
            30,
            25);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDoubleWarScythe",
            "/obj/item/weapon/yautja/scythe/alt",
            "double war scythe",
            "A huge, incredibly sharp double blade used for hunting dangerous prey. This weapon is commonly carried by Yautja who wish to disable and slice apart their foes.",
            "Large",
            SlotFlags.BACK | SlotFlags.BELT,
            true,
            null,
            30,
            25);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaCruelStaff",
            "/obj/item/weapon/yautja/sword/staff",
            "cruel staff",
            "A wicked and battered staff wrapped in worn crimson rags. A crescent shaped blade adorns the top, while the bottom is rounded and blunt.",
            "Large",
            SlotFlags.BACK,
            true,
            1,
            35,
            25);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaCombistick",
            "/obj/item/weapon/yautja/chained/combistick",
            "combi-stick",
            "A compact yet deadly personal weapon. Can be concealed when folded. Functions well as a throwing weapon or defensive tool. A common sight in Yautja packs due to its versatility.",
            "Large",
            SlotFlags.BACK,
            true,
            null,
            10,
            30,
            WieldBonusDamageTotal: 20,
            LandAtCursor: true,
            Recallable: true,
            ToggleTinyStorage: true,
            ToggleStorageDamageTotal: 5);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaWarAxe",
            "/obj/item/weapon/yautja/chained/war_axe",
            "war axe",
            "A swift weapon designed to gouge and gore the hunter's prey. A chain is attached to the hilt, allowing for a quick retrieval.",
            "Large",
            SlotFlags.BACK,
            true,
            null,
            30,
            30,
            LandAtCursor: true,
            Recallable: true);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaCeremonialDagger",
            "/obj/item/weapon/yautja/knife",
            "ceremonial dagger",
            "A viciously sharp dagger inscribed with ancient Yautja markings. Smells thickly of blood. Carried by some hunters.",
            "Tiny",
            SlotFlags.POCKET,
            true,
            null,
            25,
            20);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaWarGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive",
            "war glaive",
            "Two huge, powerful blades on a metallic pole. Mysterious writing is carved into the weapon.",
            "Large",
            SlotFlags.BACK,
            true,
            1f / 1.4f,
            10,
            10,
            RequiresWield: true,
            WieldBonusDamageTotal: 35);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaCleavingGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive/alt",
            "cleaving glaive",
            "A huge, powerful blade on a metallic pole. Mysterious writing is carved into the weapon.",
            "Large",
            SlotFlags.BACK,
            true,
            1f / 1.4f,
            10,
            10,
            RequiresWield: true,
            WieldBonusDamageTotal: 35);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaLongaxe",
            "/obj/item/weapon/twohanded/yautja/glaive/longaxe",
            "longaxe",
            "A frighteningly big axe. The blade edge is chipped and gnarled from thousands of bone-crushing blows.",
            "Large",
            SlotFlags.BACK,
            true,
            1f / 1.4f,
            10,
            10,
            RequiresWield: true,
            WieldBonusDamageTotal: 35);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaAncientWarGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive/damaged",
            "ancient war glaive",
            "A huge, powerful blade on a metallic pole. Mysterious writing is carved into the weapon. This one is ancient and has suffered serious acid damage, making it near-useless.",
            "Large",
            SlotFlags.BACK,
            true,
            1f / 1.4f,
            5,
            5,
            ItemPredator: false,
            RequiresWield: true,
            WieldBonusDamageTotal: 15);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDuellingBlade",
            "/obj/item/weapon/yautja/duelsword",
            "duelling blade",
            "A primitive yet deadly sword used in yautja rituals and duels. Though crude compared to their advanced weaponry, its sharp edge demands respect.",
            "Large",
            null,
            true,
            1f / 0.9f,
            30,
            5,
            ItemPredator: false,
            Unacidable: false);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDuellingClub",
            "/obj/item/weapon/yautja/duelclub",
            "duelling club",
            "A crude metal club adorned with a skull. Used as a non-lethal training weapon for young yautja honing their combat skills.",
            "Normal",
            null,
            false,
            null,
            30,
            7,
            ItemPredator: false,
            Unacidable: false,
            LandAtCursor: true);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDuellingHatchet",
            "/obj/item/weapon/yautja/duelaxe",
            "duelling hatchet",
            "A short ceremonial duelling hatchet. Designed for ritual combat or settling disputes among Yautja. It features a keen edge capable of cleaving flesh or bone. Though smaller than traditional Yautja weapons.",
            "Small",
            null,
            true,
            null,
            20,
            20,
            ItemPredator: false,
            Unacidable: false,
            LandAtCursor: true);
        yield return new Cmss13RackedMeleeRow(
            "CMUYautjaDuellingKnife",
            "/obj/item/weapon/yautja/duelknife",
            "duelling knife",
            "A length of leather-bound wood studded with razor-sharp teeth. How crude.",
            "Small",
            null,
            true,
            1f / 1.2f,
            25,
            30,
            ItemPredator: false,
            Unacidable: false);
    }

    public readonly record struct Cmss13RackedShieldRow(
        string Id,
        string SourcePath,
        string Name,
        string Description,
        SlotFlags? Slots,
        bool NoWearableSlot = false,
        bool NoDrop = false);

    public readonly record struct Cmss13WeaponVisualSoundRow(
        string Id,
        string SourcePath,
        string SpritePath,
        string SpriteState,
        string HeldPrefix,
        string? WieldedPrefix = null,
        string? MeleeHitSoundPath = null,
        string? MeleeHitSoundCollection = null,
        string? GunShotSoundPath = null);

    public readonly record struct Cmss13WeaponNoEmbedRow(
        string Id,
        string SourcePath,
        Cmss13NoEmbedLocalMapping LocalMapping = Cmss13NoEmbedLocalMapping.NoEmbeddableProjectileComponent);

    public enum Cmss13NoEmbedLocalMapping
    {
        NoEmbeddableProjectileComponent,
        ExplicitEmbedOnThrowFalse,
    }

    private static IEnumerable<Cmss13RackedShieldRow> Cmss13RackedShieldRows()
    {
        yield return new Cmss13RackedShieldRow(
            "CMUYautjaClanShield",
            "/obj/item/weapon/shield/riot/yautja",
            "clan shield",
            "A large tribal shield made of a strange metal alloy. The face of the shield bears three skulls, two human, one alien.",
            SlotFlags.BACK);
        yield return new Cmss13RackedShieldRow(
            "CMUYautjaAncientShield",
            "/obj/item/weapon/shield/riot/yautja/ancient",
            "ancient shield",
            "A large, ancient shield forged from an unknown golden alloy, gleaming with a luminous brilliance. Its worn surface and masterful craftsmanship hint at a forgotten purpose and a history lost to time.",
            SlotFlags.BACK);
        yield return new Cmss13RackedShieldRow(
            "CMUYautjaAncientShieldAlt",
            "/obj/item/weapon/shield/riot/yautja/ancient/alt",
            "ancient shield",
            "A large, ornately crafted shield forged from an unknown alloy. The colossal metal skull of a Xenomorph dominates the center, its jagged edges and hollow eyes giving it a fearsome presence. The masterful craftsmanship and weathered battle scars whisper of long-forgotten hunts and a legacy etched in blood.",
            SlotFlags.BACK);
        yield return new Cmss13RackedShieldRow(
            "CMUYautjaAncientShieldTemple",
            "/obj/item/weapon/shield/riot/yautja/ancient/temple",
            "ancient shield",
            "A large, ancient shield forged from an unknown alloy. Its time-worn surface and masterful craftsmanship hint at a forgotten purpose and a history lost to time.",
            SlotFlags.BACK);
        yield return new Cmss13RackedShieldRow(
            "CMUYautjaBracerShield",
            "/obj/item/weapon/shield/riot/yautja/bracer_shield",
            "bracer shield",
            "A shield made of concentric metal alloy plates. The plates fold into one another for compact storage while still providing superior protection.",
            null,
            NoWearableSlot: true,
            NoDrop: true);
    }

    private static IEnumerable<Cmss13WeaponVisualSoundRow> Cmss13WeaponVisualSoundRows()
    {
        const string weapons = "/Textures/_CMU14/Yautja/weapons.rsi";
        const string guns = "/Textures/_CMU14/Yautja/guns.rsi";

        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaChainwhip",
            "/obj/item/weapon/yautja/chain",
            weapons,
            "chainwhip",
            "chainwhip",
            MeleeHitSoundCollection: "CMUYautjaChainWhipHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaClanSword",
            "/obj/item/weapon/yautja/sword",
            weapons,
            "clan_sword",
            "clan_sword",
            MeleeHitSoundCollection: "CMUYautjaClanSwordHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaRendingSword",
            "/obj/item/weapon/yautja/sword/alt_1",
            weapons,
            "rending_sword",
            "rending_sword",
            MeleeHitSoundCollection: "CMUYautjaClanSwordHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaPiercingSword",
            "/obj/item/weapon/yautja/sword/alt_2",
            weapons,
            "piercing_sword",
            "piercing_sword",
            MeleeHitSoundCollection: "CMUYautjaClanSwordHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaSeveringSword",
            "/obj/item/weapon/yautja/sword/alt_3",
            weapons,
            "severing_sword",
            "severing_sword",
            MeleeHitSoundCollection: "CMUYautjaClanSwordHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDualWarScythe",
            "/obj/item/weapon/yautja/scythe",
            weapons,
            "dual_war_scythe",
            "dual_war_scythe",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDoubleWarScythe",
            "/obj/item/weapon/yautja/scythe/alt",
            weapons,
            "double_war_scythe",
            "double_war_scythe",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaCruelStaff",
            "/obj/item/weapon/yautja/sword/staff",
            weapons,
            "cruel_staff",
            "cruel_staff",
            MeleeHitSoundCollection: "CMUYautjaClanSwordHit");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaCombistick",
            "/obj/item/weapon/yautja/chained/combistick",
            weapons,
            "combistick",
            "combistick",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaWarAxe",
            "/obj/item/weapon/yautja/chained/war_axe",
            weapons,
            "war_axe",
            "war_axe",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaCeremonialDagger",
            "/obj/item/weapon/yautja/knife",
            weapons,
            "ceremonial_dagger",
            "ceremonial_dagger",
            MeleeHitSoundPath: "/Audio/Weapons/slash.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaClanShield",
            "/obj/item/weapon/shield/riot/yautja",
            weapons,
            "clan_shield",
            "clan_shield");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaAncientShield",
            "/obj/item/weapon/shield/riot/yautja/ancient",
            weapons,
            "ancient_shield",
            "ancient_shield");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaAncientShieldAlt",
            "/obj/item/weapon/shield/riot/yautja/ancient/alt",
            weapons,
            "ancient_shield_alt",
            "ancient_shield_alt");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaAncientShieldTemple",
            "/obj/item/weapon/shield/riot/yautja/ancient/temple",
            weapons,
            "ancient_shield_temple",
            "ancient_shield_temple");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaHunterSpear",
            "/obj/item/weapon/twohanded/yautja/spear",
            weapons,
            "hunter_spear",
            "hunter_spear",
            WieldedPrefix: "hunter_spear",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaWarGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive",
            weapons,
            "war_glaive",
            "war_glaive",
            WieldedPrefix: "war_glaive",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaCleavingGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive/alt",
            weapons,
            "cleaving_glaive",
            "cleaving_glaive",
            WieldedPrefix: "cleaving_glaive",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaAncientWarGlaive",
            "/obj/item/weapon/twohanded/yautja/glaive/damaged",
            weapons,
            "ancient_war_glaive",
            "war_glaive",
            WieldedPrefix: "war_glaive",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaLongaxe",
            "/obj/item/weapon/twohanded/yautja/glaive/longaxe",
            weapons,
            "longaxe",
            "longaxe",
            WieldedPrefix: "longaxe",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDuellingBlade",
            "/obj/item/weapon/yautja/duelsword",
            weapons,
            "duelling_blade",
            "duelling_blade",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDuellingClub",
            "/obj/item/weapon/yautja/duelclub",
            weapons,
            "duelling_club",
            "duelling_club",
            MeleeHitSoundPath: "/Audio/Weapons/genhit3.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDuellingHatchet",
            "/obj/item/weapon/yautja/duelaxe",
            weapons,
            "duelling_hatchet",
            "duelling_hatchet",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaDuellingKnife",
            "/obj/item/weapon/yautja/duelknife",
            weapons,
            "duelling_knife",
            "duelling_knife",
            MeleeHitSoundPath: "/Audio/Weapons/bladeslice.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaSpikeLauncher",
            "/obj/item/weapon/gun/launcher/spike",
            guns,
            "spike_launcher",
            "spike_launcher",
            WieldedPrefix: "spike_launcher",
            GunShotSoundPath: "/Audio/_CMU14/Yautja/woodhit.ogg");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaPlasmaRifle",
            "/obj/item/weapon/gun/energy/yautja/plasmarifle",
            guns,
            "plasma_rifle",
            "plasma_rifle",
            WieldedPrefix: "plasma_rifle",
            GunShotSoundPath: "/Audio/_CMU14/Yautja/Weapons/Plasma/pred_plasma_shot.wav");
        yield return new Cmss13WeaponVisualSoundRow(
            "CMUYautjaPlasmaPistol",
            "/obj/item/weapon/gun/energy/yautja/plasmapistol",
            guns,
            "plasma_pistol",
            "plasma_pistol",
            WieldedPrefix: "plasma_pistol",
            GunShotSoundPath: "/Audio/_CMU14/Yautja/Weapons/Plasma/pulse3.wav");
    }

    private static IEnumerable<Cmss13WeaponNoEmbedRow> Cmss13WeaponNoEmbedRows()
    {
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaChainwhip", "/obj/item/weapon/yautja/chain");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaClanSword", "/obj/item/weapon/yautja/sword");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaRendingSword", "/obj/item/weapon/yautja/sword/alt_1");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaPiercingSword", "/obj/item/weapon/yautja/sword/alt_2");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaSeveringSword", "/obj/item/weapon/yautja/sword/alt_3");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaDualWarScythe", "/obj/item/weapon/yautja/scythe");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaDoubleWarScythe", "/obj/item/weapon/yautja/scythe/alt");
        yield return new Cmss13WeaponNoEmbedRow(
            "CMUYautjaCombistick",
            "/obj/item/weapon/yautja/chained/combistick",
            Cmss13NoEmbedLocalMapping.ExplicitEmbedOnThrowFalse);
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaWarAxe", "/obj/item/weapon/yautja/chained/war_axe");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaWarGlaive", "/obj/item/weapon/twohanded/yautja/glaive");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaCleavingGlaive", "/obj/item/weapon/twohanded/yautja/glaive/alt");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaDuellingBlade", "/obj/item/weapon/yautja/duelsword");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaDuellingHatchet", "/obj/item/weapon/yautja/duelaxe");
        yield return new Cmss13WeaponNoEmbedRow("CMUYautjaDuellingKnife", "/obj/item/weapon/yautja/duelknife");
    }

    private static IEnumerable<TestCaseData> Cmss13PointSpareEquipmentRows()
    {
        yield return new TestCaseData(
            "CMUYautjaLoadoutVendor",
            new (string Id, string Name, int? Points, int? Amount)[]
            {
                ("CMUYautjaFalconDrone", "Falcon Drone", 20, null),
                ("CMUYautjaHuntingTrap", "Hunting Trap", 10, null),
                ("CMUYautjaArrow", "Arrow - Explosive", 10, null),
                ("CMUYautjaSnareArrow", "Arrow - Snare", 15, null),
            }).SetName("AdultRackSpareEquipmentRowsMatchCmss13SourceList");

        yield return new TestCaseData(
            "CMUYautjaElderLoadoutVendor",
            new (string Id, string Name, int? Points, int? Amount)[]
            {
                ("CMUYautjaFalconDrone", "Falcon Drone", 20, null),
                ("CMUYautjaHuntingTrap", "Hunting Trap", 10, null),
                ("CMUYautjaSmartDisc", "Smart-Disc", 20, null),
                ("CMUYautjaArrow", "Arrow - Explosive", 10, null),
                ("CMUYautjaSnareArrow", "Arrow - Snare", 15, null),
            }).SetName("ElderRackSpareEquipmentRowsMatchCmss13SourceList");

        yield return new TestCaseData(
            "CMUYautjaStrandedLoadoutVendor",
            new (string Id, string Name, int? Points, int? Amount)[]
            {
                ("CMUYautjaFalconDrone", "Falcon Drone", 20, null),
                ("CMUYautjaHuntingTrap", "Hunting Trap", 15, null),
                ("CMUYautjaArrow", "Arrow - Explosive", 15, null),
                ("CMUYautjaSnareArrow", "Arrow - Snare", 20, null),
            }).SetName("StrandedRackSpareEquipmentRowsMatchCmss13SourceList");

        yield return new TestCaseData(
            "CMUYautjaBadBloodLoadoutVendor",
            new (string Id, string Name, int? Points, int? Amount)[]
            {
                ("CMUYautjaFalconDroneBadBlood", "Falcon Drone", 20, null),
                ("CMUYautjaHuntingTrap", "Hunting Trap", 10, null),
                ("CMUYautjaDynamicArrow", "Arrow - Dynamic Warhead", 10, null),
                ("CMUYautjaSnareArrow", "Arrow - Snare", 15, null),
            }).SetName("BadBloodRackSpareEquipmentRowsMatchCmss13SourceList");
    }

    public readonly record struct RackPointVendRow(string Id, int Points, string SpawnedId);

    private static IEnumerable<TestCaseData> Cmss13PointSpareEquipmentVendRows()
    {
        yield return new TestCaseData(
            "CMUYautjaLoadoutVendor",
            new[]
            {
                new RackPointVendRow("CMUYautjaFalconDrone", 20, "CMUYautjaFalconDrone"),
                new RackPointVendRow("CMUYautjaHuntingTrap", 10, "CMUYautjaHuntingTrap"),
                new RackPointVendRow("CMUYautjaArrow", 10, "CMUYautjaArrow"),
                new RackPointVendRow("CMUYautjaSnareArrow", 15, "CMUYautjaSnareArrow"),
            }).SetName("AdultRackSpareEquipmentPointVendsUseCmss13VendorPoints");

        yield return new TestCaseData(
            "CMUYautjaElderLoadoutVendor",
            new[]
            {
                new RackPointVendRow("CMUYautjaFalconDrone", 20, "CMUYautjaFalconDrone"),
                new RackPointVendRow("CMUYautjaHuntingTrap", 10, "CMUYautjaHuntingTrap"),
                new RackPointVendRow("CMUYautjaSmartDisc", 20, "CMUYautjaSmartDisc"),
                new RackPointVendRow("CMUYautjaArrow", 10, "CMUYautjaArrow"),
                new RackPointVendRow("CMUYautjaSnareArrow", 15, "CMUYautjaSnareArrow"),
            }).SetName("ElderRackSpareEquipmentPointVendsUseCmss13VendorPoints");

        yield return new TestCaseData(
            "CMUYautjaStrandedLoadoutVendor",
            new[]
            {
                new RackPointVendRow("CMUYautjaFalconDrone", 20, "CMUYautjaFalconDrone"),
                new RackPointVendRow("CMUYautjaHuntingTrap", 15, "CMUYautjaHuntingTrap"),
                new RackPointVendRow("CMUYautjaArrow", 15, "CMUYautjaArrow"),
                new RackPointVendRow("CMUYautjaSnareArrow", 20, "CMUYautjaSnareArrow"),
            }).SetName("StrandedRackSpareEquipmentPointVendsUseCmss13VendorPoints");

        yield return new TestCaseData(
            "CMUYautjaBadBloodLoadoutVendor",
            new[]
            {
                new RackPointVendRow("CMUYautjaFalconDroneBadBlood", 20, "CMUYautjaFalconDroneBadBlood"),
                new RackPointVendRow("CMUYautjaHuntingTrap", 10, "CMUYautjaHuntingTrap"),
                new RackPointVendRow("CMUYautjaDynamicArrow", 10, "CMUYautjaDynamicArrow"),
                new RackPointVendRow("CMUYautjaSnareArrow", 15, "CMUYautjaSnareArrow"),
            }).SetName("BadBloodRackSpareEquipmentPointVendsUseCmss13VendorPoints");
    }

    public readonly record struct RackSectionRows(string Section, RackExpectedRow[] Rows);

    public readonly record struct RackExpectedRow(string Id, string Name);

    public readonly record struct ForbiddenRackRow(string Id, string Name, string Section = null);

    private static IEnumerable<TestCaseData> Cmss13RoleRackSectionRows()
    {
        yield return new TestCaseData(
            "CMUYautjaYoungbloodLoadoutVendor",
            "cm_vending_young_yautja",
            new[]
            {
                "Essential Hunting Supplies",
                "Main Weapons (CHOOSE 1)",
                "Bracer Attachments",
            },
            new[]
            {
                new RackSectionRows(
                    "Essential Hunting Supplies",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaYoungbloodHuntingEquipmentBundle", "Hunting Equipment"),
                        new RackExpectedRow("CMUYautjaArmorBundle", "Armor"),
                    }),
                new RackSectionRows(
                    "Main Weapons (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaClanSword", "The Primary Hunting Sword"),
                        new RackExpectedRow("CMUYautjaRendingSword", "The Rending Hunting Sword"),
                        new RackExpectedRow("CMUYautjaPiercingSword", "The Piercing Hunting Sword"),
                        new RackExpectedRow("CMUYautjaSeveringSword", "The Severing Hunting Sword"),
                        new RackExpectedRow("CMUYautjaChainwhip", "The Sundering Chain-Whip"),
                        new RackExpectedRow("CMUYautjaDualWarScythe", "The Cleaving War-Scythe"),
                        new RackExpectedRow("CMUYautjaDoubleWarScythe", "The Ripping War-Scythe"),
                        new RackExpectedRow("CMUYautjaCombistick", "The Adaptive Combi-Stick"),
                        new RackExpectedRow("CMUYautjaWarAxe", "The Butchering War Axe"),
                        new RackExpectedRow("CMUYautjaWarGlaive", "The Lumbering Glaive"),
                        new RackExpectedRow("CMUYautjaCleavingGlaive", "The Imposing Glaive"),
                        new RackExpectedRow("CMUYautjaLongaxe", "The Crushing Longaxe"),
                    }),
                new RackSectionRows(
                    "Bracer Attachments",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaWristBladesBundle", "Wrist Blades"),
                        new RackExpectedRow("CMUYautjaFearsomeScimitarsBundle", "The Fearsome Scimitars"),
                        new RackExpectedRow("CMUYautjaSkeweringScimitarsBundle", "The Skewering Scimitars"),
                        new RackExpectedRow("CMUYautjaChainGauntletsBundle", "The Chain Gauntlets"),
                    }),
            },
            new[]
            {
                new ForbiddenRackRow("CMUYautjaCruelStaff", "The Taruulan Staff"),
                new ForbiddenRackRow("CMUYautjaBracerShieldAttachment", "The Compact Shield"),
                new ForbiddenRackRow("CMUYautjaFalconDrone", "The Agile Drone", "Support Equipment (CHOOSE 2)"),
                new ForbiddenRackRow("CMUYautjaPlasmaPistol", "The Swift Plasma Pistol", "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaQuiverStrapFilled", null, "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaCapeQuarter", "Quarter-Cape", "Clothing Accessory (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaArrow", "Arrow - Explosive", "Spare Equipment"),
            }).SetName("YoungbloodRackSectionRowsAndForbiddenRowsMatchCmss13Source");

        yield return new TestCaseData(
            "CMUYautjaThrallLoadoutVendor",
            "cm_vending_thrall",
            new[]
            {
                "Essential Hunting Supplies",
                "Armor Material (CHOOSE 1)",
                "Main Weapons (CHOOSE 1)",
            },
            new[]
            {
                new RackSectionRows(
                    "Essential Hunting Supplies",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaThrallHuntingEquipmentBundle", "Hunting Equipment"),
                    }),
                new RackSectionRows(
                    "Armor Material (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaThrallArmorEbonyBundle", "Ebony"),
                        new RackExpectedRow("CMUYautjaThrallArmorSilverBundle", "Silver"),
                        new RackExpectedRow("CMUYautjaThrallArmorGoldBundle", "Gold"),
                        new RackExpectedRow("CMUYautjaThrallArmorCrimsonBundle", "Crimson"),
                        new RackExpectedRow("CMUYautjaThrallArmorBoneBundle", "Bone"),
                    }),
                new RackSectionRows(
                    "Main Weapons (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaClanSword", "The Primary Hunting Sword"),
                        new RackExpectedRow("CMUYautjaRendingSword", "The Rending Hunting Sword"),
                        new RackExpectedRow("CMUYautjaPiercingSword", "The Piercing Hunting Sword"),
                        new RackExpectedRow("CMUYautjaSeveringSword", "The Severing Hunting Sword"),
                        new RackExpectedRow("CMUYautjaChainwhip", "The Sundering Chain-Whip"),
                        new RackExpectedRow("CMUYautjaDualWarScythe", "The Cleaving War-Scythe"),
                        new RackExpectedRow("CMUYautjaDoubleWarScythe", "The Ripping War-Scythe"),
                        new RackExpectedRow("CMUYautjaCombistick", "The Adaptive Combi-Stick"),
                        new RackExpectedRow("CMUYautjaWarAxe", "The Butchering War Axe"),
                        new RackExpectedRow("CMUYautjaWarGlaive", "The Lumbering Glaive"),
                        new RackExpectedRow("CMUYautjaCleavingGlaive", "The Imposing Glaive"),
                        new RackExpectedRow("CMUYautjaLongaxe", "The Crushing Longaxe"),
                    }),
            },
            new[]
            {
                new ForbiddenRackRow("CMUYautjaCruelStaff", "The Taruulan Staff"),
                new ForbiddenRackRow("CMUYautjaBracerShieldAttachment", "The Compact Shield", "Bracer Attachments"),
                new ForbiddenRackRow("CMUYautjaFalconDrone", "The Agile Drone", "Support Equipment (CHOOSE 2)"),
                new ForbiddenRackRow("CMUYautjaPlasmaPistol", "The Swift Plasma Pistol", "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaCapeQuarter", "Quarter-Cape", "Clothing Accessory (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaArrow", "Arrow - Explosive", "Spare Equipment"),
            }).SetName("ThrallRackSectionRowsAndForbiddenRowsMatchCmss13Source");

        yield return new TestCaseData(
            "CMUYautjaBloodedThrallLoadoutVendor",
            "cm_vending_blooded_thrall",
            new[]
            {
                "Blooded Equipment",
                "Blooded Bracer Material (CHOOSE 1)",
                "Clothing Accessory (CHOOSE 1)",
            },
            new[]
            {
                new RackSectionRows(
                    "Blooded Equipment",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaBloodedThrallEquipmentBundle", "Blooded Equipment"),
                    }),
                new RackSectionRows(
                    "Blooded Bracer Material (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaBloodedThrallBracerEbonyBundle", "Ebony"),
                        new RackExpectedRow("CMUYautjaBloodedThrallBracerSilverBundle", "Silver"),
                        new RackExpectedRow("CMUYautjaBloodedThrallBracerGoldBundle", "Gold"),
                        new RackExpectedRow("CMUYautjaBloodedThrallBracerCrimsonBundle", "Crimson"),
                        new RackExpectedRow("CMUYautjaBloodedThrallBracerBoneBundle", "Bone"),
                    }),
                new RackSectionRows(
                    "Clothing Accessory (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaCapeQuarter", "Quarter-Cape"),
                        new RackExpectedRow("CMUYautjaCapeThird", "Third-Cape"),
                        new RackExpectedRow("CMUYautjaCapeHalf", "Half-Cape"),
                        new RackExpectedRow("CMUYautjaCapePoncho", "Poncho"),
                    }),
            },
            new[]
            {
                new ForbiddenRackRow("CMUYautjaClanSword", "The Primary Hunting Sword", "Main Weapons (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaBracerShieldAttachment", "The Compact Shield", "Bracer Attachments"),
                new ForbiddenRackRow("CMUYautjaPlasmaPistol", "The Swift Plasma Pistol", "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaCapeFull", "Full-Cape", "Clothing Accessory (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaArrow", "Arrow - Explosive", "Spare Equipment"),
            }).SetName("BloodedThrallRackSectionRowsAndForbiddenRowsMatchCmss13Source");

        yield return new TestCaseData(
            "CMUYautjaStrandedLoadoutVendor",
            "cm_vending_equipment_stranded_pred",
            new[]
            {
                "Essential Hunting Supplies",
                "Main Weapons (CHOOSE 1)",
                "Bracer Attachments",
                "Support Equipment (CHOOSE 2)",
                "Ranged Equipment (CHOOSE 1)",
                "Clothing Accessory (CHOOSE 1)",
                "Spare Equipment",
            },
            new[]
            {
                new RackSectionRows(
                    "Ranged Equipment (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaQuiverStrapFilled", "The Firm Bow"),
                    }),
                new RackSectionRows(
                    "Spare Equipment",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaFalconDrone", "Falcon Drone"),
                        new RackExpectedRow("CMUYautjaHuntingTrap", "Hunting Trap"),
                        new RackExpectedRow("CMUYautjaArrow", "Arrow - Explosive"),
                        new RackExpectedRow("CMUYautjaSnareArrow", "Arrow - Snare"),
                    }),
            },
            new[]
            {
                new ForbiddenRackRow("CMUYautjaPlasmaPistol", "The Swift Plasma Pistol", "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaSmartDisc", "Smart-Disc", "Spare Equipment"),
                new ForbiddenRackRow("CMUYautjaCapeCeremonial", "Ceremonial Cape", "Clothing Accessory (CHOOSE 1)"),
            }).SetName("StrandedRackSectionRowsAndForbiddenRowsMatchCmss13Source");

        yield return new TestCaseData(
            "CMUYautjaBadBloodLoadoutVendor",
            "cm_vending_equipment_badblood",
            new[]
            {
                "Essential Hunting Supplies",
                "Armor Set",
                "Main Weapons (CHOOSE 1)",
                "Bracer Attachments",
                "Support Equipment (CHOOSE 2)",
                "Ranged Equipment (CHOOSE 1)",
                "Clothing Accessory (CHOOSE 1)",
                "Spare Equipment",
            },
            new[]
            {
                new RackSectionRows(
                    "Armor Set",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaBadBloodArmorPatchworkBundle", "Patchwork Armor"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorPatchworkAltBundle", "Patchwork Armor (Alt)"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorLunaticBundle", "Lunatic Armor"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorScavengerBundle", "Scavenger Armor"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorScavengerAltBundle", "Scavenger Armor (Alt)"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorVenatorBundle", "Venator Armor"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorCommandoBundle", "Commando Armor"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorCommandoAltBundle", "Commando Armor (Alt)"),
                        new RackExpectedRow("CMUYautjaBadBloodArmorEmissaryBundle", "Emissary Armor"),
                    }),
                new RackSectionRows(
                    "Ranged Equipment (CHOOSE 1)",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaQuiverStrapDynamic", "The Firm Bow"),
                    }),
                new RackSectionRows(
                    "Spare Equipment",
                    new[]
                    {
                        new RackExpectedRow("CMUYautjaFalconDroneBadBlood", "Falcon Drone"),
                        new RackExpectedRow("CMUYautjaHuntingTrap", "Hunting Trap"),
                        new RackExpectedRow("CMUYautjaDynamicArrow", "Arrow - Dynamic Warhead"),
                        new RackExpectedRow("CMUYautjaSnareArrow", "Arrow - Snare"),
                    }),
            },
            new[]
            {
                new ForbiddenRackRow("CMUYautjaBadBloodArmorBaneBundle", "Bane Armor", "Armor Set"),
                new ForbiddenRackRow("CMUYautjaPlasmaPistol", "The Swift Plasma Pistol", "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaQuiverStrapFilled", null, "Ranged Equipment (CHOOSE 1)"),
                new ForbiddenRackRow("CMUYautjaSmartDisc", "Smart-Disc", "Spare Equipment"),
                new ForbiddenRackRow("CMUYautjaArrow", "Arrow - Explosive", "Spare Equipment"),
                new ForbiddenRackRow("CMUYautjaCapeCeremonial", "Ceremonial Cape", "Clothing Accessory (CHOOSE 1)"),
            }).SetName("BadBloodRackSectionRowsAndForbiddenRowsMatchCmss13Source");
    }

    private static IReadOnlyDictionary<string, int> Cmss13YautjaClaimCategoryLimits()
    {
        return new Dictionary<string, int>
        {
            ["CMUYautjaEssentials"] = 1,
            ["CMUYautjaArmor"] = 1,
            ["CMUYautjaPrimary"] = 1,
            ["CMUYautjaBracer"] = 1,
            ["CMUYautjaSupport"] = 2,
            ["CMUYautjaRanged"] = 1,
            ["CMUYautjaAccessory"] = 1,
        };
    }

    private static void AssertCmss13VendorChoice(
        string rackPrototype,
        string row,
        string id,
        int amount,
        IReadOnlyDictionary<string, int> expectedChoices)
    {
        if (!expectedChoices.TryGetValue(id, out var expectedAmount))
        {
            Assert.Fail(
                $"{rackPrototype} {row} uses local-only claim id {id}; expected one of {string.Join(", ", expectedChoices.Keys)}.");
            return;
        }

        Assert.That(amount, Is.EqualTo(expectedAmount), $"{rackPrototype} {row} source claim amount");
    }

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

    private static void ClearSlot(IEntityManager entMan, InventorySystem inventory, EntityUid user, string slot)
    {
        if (inventory.TryGetSlotEntity(user, slot, out var item) && !entMan.Deleted(item.Value))
            entMan.DeleteEntity(item.Value);
    }

    private static void AssertEquippedPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string slot,
        string prototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, slot, out var item), Is.True, $"{slot} has equipped item");
        Assert.That(entMan.GetComponent<MetaDataComponent>(item.Value).EntityPrototype?.ID, Is.EqualTo(prototype), slot);
    }

    private static void AssertProfileVisualsPreservedOnScalableItem(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string slot,
        string visualPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, slot, out var item), Is.True, $"{slot} has equipped item");

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var factory = IoCManager.Resolve<IComponentFactory>();
        var expected = prototypes.Index<EntityPrototype>(visualPrototype);

        Assert.That(expected.TryGetComponent<ItemComponent>(out var expectedItem, factory), Is.True, $"{visualPrototype} has item visuals");
        Assert.That(expected.TryGetComponent<ClothingComponent>(out var expectedClothing, factory), Is.True, $"{visualPrototype} has clothing visuals");

        var actualItem = entMan.GetComponent<ItemComponent>(item.Value);
        var actualClothing = entMan.GetComponent<ClothingComponent>(item.Value);

        Assert.That(actualItem.RsiPath, Is.EqualTo(expectedItem!.RsiPath), $"{slot} held visual RSI");
        Assert.That(actualItem.HeldPrefix, Is.EqualTo(expectedItem.HeldPrefix), $"{slot} held prefix");
        Assert.That(actualItem.InhandVisuals, Is.EqualTo(expectedItem.InhandVisuals), $"{slot} inhand visuals");
        Assert.That(actualClothing.RsiPath, Is.EqualTo(expectedClothing!.RsiPath), $"{slot} clothing visual RSI");
        Assert.That(actualClothing.EquippedPrefix, Is.EqualTo(expectedClothing.EquippedPrefix), $"{slot} equipped prefix");
        Assert.That(actualClothing.ClothingVisuals, Is.EqualTo(expectedClothing.ClothingVisuals), $"{slot} clothing visuals");
        Assert.That(entMan.HasComponent<YautjaScalableRepairComponent>(item.Value), Is.True, $"{slot} keeps scalable repair gameplay state");
    }

    private static void AssertEquippedVisualsMatchPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid user,
        string slot,
        string visualPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(user, slot, out var item), Is.True, $"{slot} has equipped item");

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var factory = IoCManager.Resolve<IComponentFactory>();
        var expected = prototypes.Index<EntityPrototype>(visualPrototype);

        Assert.That(expected.TryGetComponent<ItemComponent>(out var expectedItem, factory), Is.True, $"{visualPrototype} has item visuals");
        Assert.That(expected.TryGetComponent<ClothingComponent>(out var expectedClothing, factory), Is.True, $"{visualPrototype} has clothing visuals");

        var actualItem = entMan.GetComponent<ItemComponent>(item.Value);
        var actualClothing = entMan.GetComponent<ClothingComponent>(item.Value);

        Assert.That(actualItem.RsiPath, Is.EqualTo(expectedItem!.RsiPath), $"{slot} held visual RSI");
        Assert.That(actualItem.HeldPrefix, Is.EqualTo(expectedItem.HeldPrefix), $"{slot} held prefix");
        Assert.That(actualItem.InhandVisuals, Is.EqualTo(expectedItem.InhandVisuals), $"{slot} inhand visuals");
        Assert.That(actualClothing.RsiPath, Is.EqualTo(expectedClothing!.RsiPath), $"{slot} clothing visual RSI");
        Assert.That(actualClothing.EquippedPrefix, Is.EqualTo(expectedClothing.EquippedPrefix), $"{slot} equipped prefix");
        Assert.That(actualClothing.ClothingVisuals, Is.EqualTo(expectedClothing.ClothingVisuals), $"{slot} clothing visuals");
    }

    private static void AssertDamageModifierEqual(DamageModifierSet actual, DamageModifierSet expected)
    {
        Assert.That(actual.Coefficients, Is.EqualTo(expected.Coefficients));
        Assert.That(actual.FlatReduction, Is.EqualTo(expected.FlatReduction));
    }

    private static void AssertVendorRow(
        CMVendorSection section,
        string id,
        string name,
        bool? mandatory = null,
        bool recommended = false,
        SlotFlags? replaceSlot = null)
    {
        var entry = section.Entries.Single(entry => entry.Id.Id == id);

        Assert.That(entry.Name, Is.EqualTo(name), $"{id} display name");
        if (mandatory != null)
            Assert.That(typeof(CMVendorEntry).GetField("Mandatory")?.GetValue(entry), Is.EqualTo(mandatory), $"{id} mandatory flag");
        Assert.That(entry.Recommended, Is.EqualTo(recommended), $"{id} recommended flag");
        Assert.That(entry.Points, Is.Null, $"{id} regular equipment row should cost 0 source points");
        Assert.That(entry.Amount, Is.EqualTo((int?) 1), $"{id} source regular row vends one item/bundle");
        Assert.That(entry.ReplaceSlot, Is.EqualTo(replaceSlot), $"{id} replace slot");
    }

    private static void AssertCmss13MainHuntingSword(IEntityManager entMan, EntityUid sword, string sourceName)
    {
        const string sourceDescription =
            "An expertly crafted Yautja blade carried by hunters who wish to fight up close. Razor sharp and capable of cutting flesh into ribbons. Commonly carried by aggressive and lethal hunters.";

        var meta = entMan.GetComponent<MetaDataComponent>(sword);
        var item = entMan.GetComponent<ItemComponent>(sword);
        var clothing = entMan.GetComponent<ClothingComponent>(sword);
        var melee = entMan.GetComponent<MeleeWeaponComponent>(sword);
        var thrown = entMan.GetComponent<DamageOtherOnHitComponent>(sword);

        Assert.Multiple(() =>
        {
            Assert.That(meta.EntityName, Is.EqualTo(sourceName));
            Assert.That(meta.EntityDescription, Is.EqualTo(sourceDescription));
            Assert.That(entMan.HasComponent<SharpComponent>(sword), Is.True);
            Assert.That(item.Size.Id, Is.EqualTo("Large"),
                "CMSS13 /obj/item/weapon/yautja/sword sets w_class = SIZE_LARGE.");
            Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK),
                "CMSS13 /obj/item/weapon/yautja/sword sets flags_equip_slot = SLOT_BACK.");
            Assert.That(melee.AttackRate, Is.EqualTo(1f),
                "CMSS13 /obj/item/weapon/yautja/sword sets attack_speed = 1 SECONDS.");
            Assert.That(melee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 35),
                "CMSS13 /obj/item/weapon/yautja/sword sets force = MELEE_FORCE_TIER_7, and code/__DEFINES/combat.dm defines that as 35.");
            Assert.That(thrown.Damage.DamageDict["Slash"], Is.EqualTo((FixedPoint2) 25),
                "CMSS13 /obj/item/weapon/yautja/sword sets throwforce = MELEE_FORCE_TIER_5, and code/__DEFINES/combat.dm defines that as 25.");
        });
    }

    private static void AssertCmss13RackedMeleeStaticFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13RackedMeleeRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        var item = entMan.GetComponent<ItemComponent>(uid);
        var melee = entMan.GetComponent<MeleeWeaponComponent>(uid);

        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} {row.SourcePath} source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} {row.SourcePath} source description");
        Assert.That(item.Size.Id, Is.EqualTo(row.Size), $"{row.Id} {row.SourcePath} w_class local mapping");

        if (row.Slots is { } slots)
        {
            Assert.That(entMan.TryGetComponent<ClothingComponent>(uid, out var clothing), Is.True,
                $"{row.Id} {row.SourcePath} flags_equip_slot local mapping");
            Assert.That(clothing!.Slots, Is.EqualTo(slots), $"{row.Id} {row.SourcePath} flags_equip_slot local mapping");
        }
        else
        {
            Assert.That(entMan.HasComponent<ClothingComponent>(uid), Is.False,
                $"{row.Id} {row.SourcePath} should not inherit a local wearable slot.");
        }

        Assert.That(entMan.HasComponent<SharpComponent>(uid), Is.EqualTo(row.Sharp),
            $"{row.Id} {row.SourcePath} sharp/edge local mapping");
        if (row.ItemPredator)
            AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, uid, row.Id);
        else
            Assert.That(entMan.HasComponent<YautjaTechItemComponent>(uid), Is.False,
                $"{row.Id} {row.SourcePath} explicitly does not inherit flags_item = ITEM_PREDATOR.");

        if (row.Unacidable)
        {
            AssertNonCorrodible(entMan, uid);
        }
        else
        {
            if (entMan.TryGetComponent<CorrodibleComponent>(uid, out var corrodible))
                Assert.That(corrodible.IsCorrodible, Is.True, $"{row.Id} {row.SourcePath} does not set unacidable = TRUE.");
        }

        if (row.AttackRate is { } attackRate)
            Assert.That(melee.AttackRate, Is.EqualTo(attackRate).Within(0.0001f), $"{row.Id} {row.SourcePath} attack_speed local mapping");
        if (row.DamageTotal is { } damageTotal)
            Assert.That(DamageTotal(melee.Damage), Is.EqualTo((FixedPoint2) damageTotal), $"{row.Id} {row.SourcePath} force local mapping");

        if (row.ThrowDamageTotal is { } throwDamageTotal)
        {
            Assert.That(entMan.TryGetComponent<DamageOtherOnHitComponent>(uid, out var thrown), Is.True,
                $"{row.Id} {row.SourcePath} throwforce local mapping");
            Assert.That(DamageTotal(thrown!.Damage), Is.EqualTo((FixedPoint2) throwDamageTotal), $"{row.Id} {row.SourcePath} throwforce local mapping");
        }

        Assert.That(entMan.HasComponent<MeleeRequiresWieldComponent>(uid), Is.EqualTo(row.RequiresWield),
            $"{row.Id} {row.SourcePath} TWOHANDED/wield local mapping");
        if (row.WieldBonusDamageTotal is { } wieldBonusDamage)
        {
            Assert.That(entMan.TryGetComponent<IncreaseDamageOnWieldComponent>(uid, out var wield), Is.True,
                $"{row.Id} {row.SourcePath} force_wielded local mapping");
            Assert.That(DamageTotal(wield!.BonusDamage), Is.EqualTo((FixedPoint2) wieldBonusDamage), $"{row.Id} {row.SourcePath} force_wielded local mapping");
        }

        Assert.That(entMan.HasComponent<LandAtCursorComponent>(uid), Is.EqualTo(row.LandAtCursor),
            $"{row.Id} {row.SourcePath} SPEED_VERY_FAST throw behavior local mapping");
        Assert.That(entMan.HasComponent<YautjaRecallableComponent>(uid), Is.EqualTo(row.Recallable),
            $"{row.Id} {row.SourcePath} chained recall local mapping");

        if (row.ToggleTinyStorage)
        {
            Assert.That(entMan.TryGetComponent<ItemToggleSizeComponent>(uid, out var toggleSize), Is.True,
                $"{row.Id} {row.SourcePath} collapsed w_class local mapping");
            Assert.That(toggleSize!.ActivatedSize?.Id, Is.EqualTo("Large"), $"{row.Id} extended CMSS13 w_class = SIZE_LARGE");
            Assert.That(toggleSize.DeactivatedSize?.Id, Is.EqualTo("Tiny"), $"{row.Id} collapsed CMSS13 w_class = SIZE_TINY");
        }

        if (row.ToggleStorageDamageTotal is { } toggleStorageDamage)
        {
            Assert.That(entMan.TryGetComponent<ItemToggleMeleeWeaponComponent>(uid, out var toggleMelee), Is.True,
                $"{row.Id} {row.SourcePath} collapsed force local mapping");
            Assert.That(DamageTotal(toggleMelee!.ActivatedDamage!), Is.EqualTo((FixedPoint2) row.DamageTotal!.Value),
                $"{row.Id} extended CMSS13 force_unwielded local mapping");
            Assert.That(DamageTotal(toggleMelee.DeactivatedDamage!), Is.EqualTo((FixedPoint2) toggleStorageDamage),
                $"{row.Id} collapsed CMSS13 force_storage = MELEE_FORCE_TIER_1.");
        }
    }

    private static FixedPoint2 DamageTotal(DamageSpecifier damage)
    {
        var total = FixedPoint2.Zero;
        foreach (var value in damage.DamageDict.Values)
            total += value;

        return total;
    }

    private static void AssertCmss13RackedShieldStaticFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13RackedShieldRow row)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);

        Assert.That(meta.EntityName, Is.EqualTo(row.Name), $"{row.Id} {row.SourcePath} source name");
        Assert.That(meta.EntityDescription, Is.EqualTo(row.Description), $"{row.Id} {row.SourcePath} source description");
        AssertYautjaTechItemBlocksLikeCmss13ItemPredator(entMan, uid, row.Id);

        if (row.Slots is { } slots)
        {
            var clothing = entMan.GetComponent<ClothingComponent>(uid);
            Assert.That(clothing.Slots, Is.EqualTo(slots), $"{row.Id} {row.SourcePath} flags_equip_slot local mapping");
        }
        else
        {
            Assert.That(entMan.HasComponent<ClothingComponent>(uid), Is.False,
                $"{row.Id} {row.SourcePath} flags_equip_slot = NO_FLAGS should not expose a wearable slot.");
        }

        Assert.That(row.NoWearableSlot, Is.EqualTo(row.Slots == null), $"{row.Id} source NO_FLAGS slot marker");
        if (row.Id == "CMUYautjaBracerShield")
        {
            Assert.That(entMan.HasComponent<YautjaStoredGearComponent>(uid), Is.True,
                $"{row.Id} must enforce source NODROP through its retracting stored-gear lifecycle.");
            Assert.That(entMan.HasComponent<UnremoveableComponent>(uid), Is.False,
                $"{row.Id} must remain removable by its bracer during retraction.");
        }
        else
        {
            Assert.That(entMan.HasComponent<UnremoveableComponent>(uid), Is.EqualTo(row.NoDrop),
                $"{row.Id} source NODROP local mapping");
        }
    }

    private static void AssertCmss13WeaponHeldSoundFacts(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13WeaponVisualSoundRow row)
    {
        var item = entMan.GetComponent<ItemComponent>(uid);

        Assert.That(item.HeldPrefix, Is.EqualTo(row.HeldPrefix),
            $"{row.Id} {row.SourcePath} item_state local held-prefix mapping");

        if (row.WieldedPrefix is { } wieldedPrefix)
        {
            var wieldable = entMan.GetComponent<WieldableComponent>(uid);
            Assert.That(wieldable.WieldedInhandPrefix, Is.EqualTo(wieldedPrefix),
                $"{row.Id} {row.SourcePath} two-handed held-prefix mapping");
        }

        if (row.MeleeHitSoundPath is { } hitSoundPath)
        {
            var melee = entMan.GetComponent<MeleeWeaponComponent>(uid);
            Assert.That(melee.HitSound, Is.Not.Null, $"{row.Id} {row.SourcePath} hitsound local mapping");
            if (melee.HitSound == null)
                return;
            AssertSoundPath(melee.HitSound!, hitSoundPath);
        }

        if (row.MeleeHitSoundCollection is { } hitSoundCollection)
        {
            var melee = entMan.GetComponent<MeleeWeaponComponent>(uid);
            Assert.That(melee.HitSound, Is.Not.Null, $"{row.Id} {row.SourcePath} hitsound local mapping");
            if (melee.HitSound == null)
                return;
            AssertSoundCollection(melee.HitSound!, hitSoundCollection);
        }

        if (row.GunShotSoundPath is { } gunSoundPath)
        {
            var gun = entMan.GetComponent<GunComponent>(uid);
            Assert.That(gun.SoundGunshot, Is.Not.Null, $"{row.Id} {row.SourcePath} fire_sound local mapping");
            if (gun.SoundGunshot == null)
                return;
            AssertSoundPath(gun.SoundGunshot!, gunSoundPath);
        }
    }

    private static void AssertCmss13WeaponSpriteFacts(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        Cmss13WeaponVisualSoundRow row)
    {
        var prototype = prototypes.Index<EntityPrototype>(row.Id);

        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, row.Id);
        Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath(row.SpritePath)),
            $"{row.Id} {row.SourcePath} world sprite RSI local mapping");
        Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(row.SpriteState),
            $"{row.Id} {row.SourcePath} icon_state local mapping");
    }

    private static void AssertCmss13ThrownNoEmbed(
        IEntityManager entMan,
        EntityUid uid,
        Cmss13WeaponNoEmbedRow row)
    {
        var hasEmbeddable = entMan.TryGetComponent<EmbeddableProjectileComponent>(uid, out var embeddable);

        switch (row.LocalMapping)
        {
            case Cmss13NoEmbedLocalMapping.NoEmbeddableProjectileComponent:
                Assert.That(
                    hasEmbeddable,
                    Is.False,
                    $"{row.Id} {row.SourcePath} maps CMSS13 embeddable = FALSE by omitting local EmbeddableProjectile.");
                break;
            case Cmss13NoEmbedLocalMapping.ExplicitEmbedOnThrowFalse:
                Assert.That(
                    hasEmbeddable,
                    Is.True,
                    $"{row.Id} {row.SourcePath} maps CMSS13 embeddable = FALSE with explicit local EmbeddableProjectile.");
                Assert.That(
                    embeddable!.EmbedOnThrow,
                    Is.False,
                    $"{row.Id} {row.SourcePath} should disable embedding through embedOnThrow.");
                break;
            default:
                Assert.Fail($"{row.Id} {row.SourcePath} has unknown no-embed mapping {row.LocalMapping}.");
                break;
        }
    }

    private static void AssertCmss13HunterSpearRemainderFacts(IEntityManager entMan, EntityUid spear)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(spear);
        var item = entMan.GetComponent<ItemComponent>(spear);
        var clothing = entMan.GetComponent<ClothingComponent>(spear);
        var melee = entMan.GetComponent<MeleeWeaponComponent>(spear);
        var wield = entMan.GetComponent<IncreaseDamageOnWieldComponent>(spear);

        Assert.That(meta.EntityDescription, Is.EqualTo("A spear of exquisite design, used by an ancient civilisation."),
            "CMSS13 /obj/item/weapon/twohanded/yautja/spear source description.");
        Assert.That(item.Size.Id, Is.EqualTo("Large"),
            "CMSS13 /obj/item/weapon/twohanded/yautja inherits w_class = SIZE_LARGE.");
        Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.BACK),
            "CMSS13 /obj/item/weapon/twohanded/yautja inherits flags_equip_slot = SLOT_BACK.");
        Assert.That(entMan.HasComponent<YautjaTechItemComponent>(spear), Is.False,
            "CMSS13 hunter spear overrides flags_item to TWOHANDED|ADJACENT_CLICK_DELAY and drops ITEM_PREDATOR.");
        AssertNonCorrodible(entMan, spear);
        Assert.That(DamageTotal(melee.Damage), Is.EqualTo((FixedPoint2) 10),
            "CMSS13 hunter spear force = MELEE_FORCE_TIER_3 local mapping.");
        Assert.That(DamageTotal(wield.BonusDamage), Is.EqualTo((FixedPoint2) 35),
            "CMSS13 hunter spear force_wielded = MELEE_FORCE_TIER_7 local mapping.");
    }

    private static void RaiseShieldHit(IEntityManager entMan, EntityUid shield, EntityUid user, EntityUid target)
    {
        var hit = new MeleeHitEvent(new List<EntityUid> { target }, user, shield, new DamageSpecifier(), null);
        entMan.EventBus.RaiseLocalEvent(shield, hit);
    }
}
