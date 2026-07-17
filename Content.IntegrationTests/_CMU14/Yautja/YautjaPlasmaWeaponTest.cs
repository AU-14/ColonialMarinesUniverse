using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Server._CMU14.Yautja;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components.OnTrigger;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPlasmaWeaponTest
{
    private static readonly string[] CasterStunStatuses = ["Stun", "KnockedDown"];

    [Test]
    public async Task PlasmaPistolIsHotIgnitionSourceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", MapCoordinates.Nullspace);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", MapCoordinates.Nullspace);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);
            var casterVariants = new[]
            {
                entMan.SpawnEntity("CMUYautjaPlasmaCasterRetro", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPlasmaCasterEbony", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPlasmaCasterSilver", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPlasmaCasterBronze", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPlasmaCasterCrimson", MapCoordinates.Nullspace),
                entMan.SpawnEntity("CMUYautjaPlasmaCasterBone", MapCoordinates.Nullspace),
            };

            try
            {
                var pistolHot = new IsHotEvent();
                entMan.EventBus.RaiseLocalEvent(pistol, pistolHot);
                var casterHot = new IsHotEvent();
                entMan.EventBus.RaiseLocalEvent(caster, casterHot);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<AlwaysHotComponent>(pistol), Is.True,
                        "CMSS13 plasma pistol has IGNITING_ITEM and heat_source = 1500.");
                    Assert.That(pistolHot.IsHot, Is.True,
                        "Local ignition checks use IsHotEvent for hot-source behavior.");
                    Assert.That(entMan.HasComponent<AlwaysHotComponent>(caster), Is.True,
                        "CMSS13 plasma caster has IGNITING_ITEM and heat_source = 1500.");
                    Assert.That(casterHot.IsHot, Is.True,
                        "Local ignition checks should treat the plasma caster as a hot source like CMSS13.");
                    foreach (var variant in casterVariants)
                    {
                        Assert.That(entMan.HasComponent<AlwaysHotComponent>(variant), Is.True,
                            "CMSS13 plasma caster material subtypes inherit the base caster IGNITING_ITEM and heat_source behavior.");
                    }

                    Assert.That(entMan.HasComponent<AlwaysHotComponent>(rifle), Is.False,
                        "CMSS13 plasma rifle lacks IGNITING_ITEM and should not be a hot source.");
                });
            }
            finally
            {
                if (!entMan.Deleted(pistol))
                    entMan.DeleteEntity(pistol);

                if (!entMan.Deleted(rifle))
                    entMan.DeleteEntity(rifle);

                if (!entMan.Deleted(caster))
                    entMan.DeleteEntity(caster);

                foreach (var variant in casterVariants)
                {
                    if (!entMan.Deleted(variant))
                        entMan.DeleteEntity(variant);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaPistolUniqueActionPopupUsesCmss13ModeText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid pistol = default;
        EntityUid? previousAttached = null;
        NetEntity hunterNet = default;
        NetEntity pistolNet = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                hunterNet = entMan.GetNetEntity(hunter);
                pistolNet = entMan.GetNetEntity(pistol);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitPost(() =>
            {
                var entMan = client.EntMan;
                var clientHunter = entMan.GetEntity(hunterNet);
                var clientPistol = entMan.GetEntity(pistolNet);

                var toggleIncendiary = new UniqueActionEvent(clientHunter);
                entMan.EventBus.RaiseLocalEvent(clientPistol, toggleIncendiary);
                Assert.That(toggleIncendiary.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("plasma pistol will now fire incendiary plasma bolts."),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol/use_unique_action() shows this source notice.");
                    Assert.That(labels.Any(label => label.Contains("incendiary plasma pistol bolt")), Is.False,
                        "The local generic fire-mode popup should not replace the CMSS13 source text.");
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

                foreach (var uid in new[] { hunter, pistol })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaPistolIncendiaryFiresWithOneChargeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);

            try
            {
                var toggleIncendiary = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);
                Assert.That(toggleIncendiary.Handled, Is.True);

                var battery = entMan.GetComponent<BatteryComponent>(pistol);
                batterySystem.SetCharge(pistol, 4, battery);

                var ammo = entMan.GetComponent<ProjectileBatteryAmmoProviderComponent>(pistol);
                var coordinates = entMan.GetComponent<TransformComponent>(pistol).Coordinates;
                var takeAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, takeAmmo);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.FireCost, Is.EqualTo(5),
                        "CMSS13 plasma pistol incendiary mode sets shot_cost = 5.");
                    Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(1),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol/has_ammunition() returns TRUE when charge_time >= 1 even in incendiary mode.");
                    Assert.That(battery.CurrentCharge, Is.EqualTo(0),
                        "CMSS13 load_into_chamber() subtracts shot_cost after creating the projectile; local battery charge clamps at zero.");
                });
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
    public async Task PlasmaRifleAndPistolRefundOnlyDeletedUnfiredProjectilesLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);
            EntityUid? rifleProjectile = null;
            EntityUid? firedRifleProjectile = null;
            EntityUid? pistolProjectile = null;

            try
            {
                var rifleBattery = entMan.GetComponent<BatteryComponent>(rifle);
                batterySystem.SetCharge(rifle, 100, rifleBattery);

                var rifleAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(rifle).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(rifle, rifleAmmo);
                rifleProjectile = rifleAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(rifleProjectile, Is.Not.Null);
                    Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(93),
                        "CMSS13 plasma rifle load_into_chamber() subtracts 7 charge_time before the projectile is fired.");
                });

                entMan.DeleteEntity(rifleProjectile!.Value);
                rifleProjectile = null;

                Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(100),
                    "CMSS13 /plasmarifle/delete_bullet(projectile, refund = TRUE) refunds 7 charge_time for an unfired prepared projectile.");

                batterySystem.SetCharge(rifle, 100, rifleBattery);

                var firedRifleAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(rifle).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(rifle, firedRifleAmmo);
                firedRifleProjectile = firedRifleAmmo.Ammo.Single().Entity;
                Assert.That(firedRifleProjectile, Is.Not.Null);

                var fired = new AmmoShotEvent
                {
                    FiredProjectiles = [firedRifleProjectile!.Value],
                };
                entMan.EventBus.RaiseLocalEvent(rifle, fired);
                entMan.DeleteEntity(firedRifleProjectile.Value);
                firedRifleProjectile = null;

                Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(93),
                    "CMSS13 only passes refund = TRUE for deleted prepared projectiles; projectiles that were fired keep their spent charge.");

                var toggleIncendiary = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);
                Assert.That(toggleIncendiary.Handled, Is.True);

                var pistolBattery = entMan.GetComponent<BatteryComponent>(pistol);
                batterySystem.SetCharge(pistol, 40, pistolBattery);

                var pistolAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(pistol).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, pistolAmmo);
                pistolProjectile = pistolAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(pistolProjectile, Is.Not.Null);
                    Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(35),
                        "CMSS13 plasma pistol incendiary mode sets shot_cost = 5 and subtracts it after creating the projectile.");
                });

                entMan.DeleteEntity(pistolProjectile!.Value);
                pistolProjectile = null;

                Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(40),
                    "CMSS13 /plasmapistol/delete_bullet(projectile, refund = TRUE) refunds the current shot_cost for an unfired prepared projectile.");
            }
            finally
            {
                foreach (var uid in new[] { rifleProjectile, firedRifleProjectile, pistolProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, rifle, pistol })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaLowChargeRefundOnlyRestoresActuallySpentChargeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var fireModeSystem = entMan.System<BatteryWeaponFireModesSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", map.GridCoords);
            EntityUid? pistolProjectile = null;
            EntityUid? carbineProjectile = null;

            try
            {
                var toggleIncendiary = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);
                Assert.That(toggleIncendiary.Handled, Is.True);

                var pistolBattery = entMan.GetComponent<BatteryComponent>(pistol);
                batterySystem.SetCharge(pistol, 4, pistolBattery);

                var pistolAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(pistol).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, pistolAmmo);
                pistolProjectile = pistolAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(pistolProjectile, Is.Not.Null);
                    Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(0),
                        "CMSS13 plasma pistol subtracts shot_cost after creating the projectile; local battery clamps when less charge was available.");
                });

                entMan.DeleteEntity(pistolProjectile!.Value);
                pistolProjectile = null;

                Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(4),
                    "Refunding a locally clamped low-charge shot must restore only the charge that was actually spent, not create extra charge.");

                var carbineFireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(carbine);
                Assert.That(fireModeSystem.TrySetFireMode(carbine, carbineFireModes, 1), Is.True);

                var carbineBattery = entMan.GetComponent<BatteryComponent>(carbine);
                batterySystem.SetCharge(carbine, 1, carbineBattery);

                var carbineAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    entMan.GetComponent<TransformComponent>(carbine).Coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(carbine, carbineAmmo);
                carbineProjectile = carbineAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(carbineProjectile, Is.Not.Null);
                    Assert.That(carbineBattery.CurrentCharge, Is.EqualTo(0),
                        "CMSS13 plasma carbine impact-explosive mode subtracts shot_cost after creating the projectile; local battery clamps when less charge was available.");
                });

                entMan.DeleteEntity(carbineProjectile!.Value);
                carbineProjectile = null;

                Assert.That(carbineBattery.CurrentCharge, Is.EqualTo(1),
                    "Low-charge carbine impact-explosive refund must restore only the single local charge that was actually spent.");
            }
            finally
            {
                foreach (var uid in new[] { pistolProjectile, carbineProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, pistol, carbine })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRiflePistolAndCarbineRechargeOnlyOnWholeCmss13ProcessTicks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", MapCoordinates.Nullspace);
            var carbine = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", MapCoordinates.Nullspace);

            try
            {
                var rifleBattery = entMan.GetComponent<BatteryComponent>(rifle);
                var pistolBattery = entMan.GetComponent<BatteryComponent>(pistol);
                var carbineBattery = entMan.GetComponent<BatteryComponent>(carbine);
                batterySystem.SetCharge(rifle, 50, rifleBattery);
                batterySystem.SetCharge(pistol, 20, pistolBattery);
                batterySystem.SetCharge(carbine, 20, carbineBattery);

                batterySystem.Update(0.5f);

                Assert.Multiple(() =>
                {
                    Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(50),
                        "CMSS13 plasma rifle process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                    Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(20),
                        "CMSS13 plasma pistol process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                    Assert.That(carbineBattery.CurrentCharge, Is.EqualTo(20),
                        "CMSS13 plasma carbine process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                });

                batterySystem.Update(0.5f);

                Assert.Multiple(() =>
                {
                    Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(51),
                        "After one accumulated process tick, the rifle gains exactly one charge_time.");
                    Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(21),
                        "After one accumulated process tick, the pistol gains exactly one charge_time.");
                    Assert.That(carbineBattery.CurrentCharge, Is.EqualTo(21),
                        "After one accumulated process tick, the carbine gains exactly one charge_time.");
                });

                batterySystem.Update(2.25f);

                Assert.Multiple(() =>
                {
                    Assert.That(rifleBattery.CurrentCharge, Is.EqualTo(53),
                        "CMSS13 plasma rifle process() cannot gain fractional charge_time from leftover frame time.");
                    Assert.That(pistolBattery.CurrentCharge, Is.EqualTo(23),
                        "CMSS13 plasma pistol process() cannot gain fractional charge_time from leftover frame time.");
                    Assert.That(carbineBattery.CurrentCharge, Is.EqualTo(23),
                        "CMSS13 plasma carbine process() cannot gain fractional charge_time from leftover frame time.");
                });
            }
            finally
            {
                foreach (var uid in new[] { rifle, pistol, carbine })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaRiflePistolAndCasterNonYautjaFireDenialMatchesCmss13AbleToFire()
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
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", map.GridCoords);
            var pistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);

            try
            {
                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;

                var userCoords = entMan.GetComponent<TransformComponent>(user).Coordinates;
                var rifleAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                var pistolAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);
                var casterAttempt = new AttemptShootEvent(user, null, userCoords, userCoords);

                entMan.EventBus.RaiseLocalEvent(rifle, ref rifleAttempt);
                entMan.EventBus.RaiseLocalEvent(pistol, ref pistolAttempt);
                entMan.EventBus.RaiseLocalEvent(caster, ref casterAttempt);

                Assert.Multiple(() =>
                {
                    Assert.That(rifleAttempt.Cancelled, Is.True);
                    Assert.That(pistolAttempt.Cancelled, Is.True);
                    Assert.That(casterAttempt.Cancelled, Is.True);
                    Assert.That(rifleAttempt.Message, Is.EqualTo("You have no idea how this thing works!"),
                        "CMSS13 plasma rifle able_to_fire() uses the shared no-tech warning.");
                    Assert.That(pistolAttempt.Message, Is.EqualTo("You have no idea how this thing works!"),
                        "CMSS13 plasma pistol able_to_fire() uses the shared no-tech warning.");
                    Assert.That(casterAttempt.Message, Is.EqualTo("You have no idea how this thing works!"),
                        "CMSS13 plasma caster able_to_fire() uses the shared no-tech warning when source exists but the user lacks Yautja tech.");
                });
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                foreach (var uid in new[] { user, rifle, pistol, bracer, caster })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterRequiresSourceBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, caster), Is.True);

                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;
                var sourceBracer = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(stored.Bracer, Is.EqualTo(bracer),
                        "The test must model CMSS13 plasma_caster/source before checking the firing guard.");
                    Assert.That(entMan.Deleted(bracer), Is.False);
                    Assert.That(sourceBracer.Charge, Is.GreaterThan((FixedPoint2) 30));
                    Assert.That(entMan.HasComponent<EntityTurnInvisibleComponent>(hunter), Is.False,
                        "Equipping a bracer while already uncloaked must not create a generic cloak weapon lock that hides the caster source-bracer guard.");
                });

                var linkedAttempt = new AttemptShootEvent(
                    hunter,
                    null,
                    entMan.GetComponent<TransformComponent>(hunter).Coordinates,
                    entMan.GetComponent<TransformComponent>(caster).Coordinates);
                entMan.EventBus.RaiseLocalEvent(caster, ref linkedAttempt);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                Assert.That(linkedAttempt.Cancelled, Is.False,
                    $"The control caster has a CMSS13 source bracer, Yautja tech and enough source charge. " +
                    $"Message={linkedAttempt.Message ?? "<null>"}, " +
                    $"HasYautja={entMan.HasComponent<YautjaComponent>(hunter)}, " +
                    $"Source={stored.Bracer}, " +
                    $"SourceQueued={entMan.IsQueuedForDeletion(bracer)}, " +
                    $"SourceCharge={sourceBracer.Charge}, " +
                    $"Mode={casterComp.CurrentMode}, " +
                    $"ModeCost={casterComp.Modes[casterComp.CurrentMode].PowerCost}.");

                stored.Bracer = null;

                var noSourceAttempt = new AttemptShootEvent(
                    hunter,
                    null,
                    entMan.GetComponent<TransformComponent>(hunter).Coordinates,
                    entMan.GetComponent<TransformComponent>(caster).Coordinates);
                entMan.EventBus.RaiseLocalEvent(caster, ref noSourceAttempt);

                Assert.That(noSourceAttempt.Cancelled, Is.True,
                    "CMSS13 plasma_caster/able_to_fire() returns immediately when source is null even if the user has Yautja tech and enough bracer charge.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, bracer, caster })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterDropDeactivatesAndReturnsToSourceBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid caster = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var attachments = entMan.System<YautjaAttachmentSystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var previousCulture = loc.DefaultCulture;
                var session = server.PlayerMan.Sessions.Single();
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                try
                {
                    previousAttached = session.AttachedEntity;

                    hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                    bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                    entMan.EnsureComponent<YautjaComponent>(hunter);
                    server.PlayerMan.SetAttachedEntity(session, hunter);

                    Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                    var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                    Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Caster, out caster), Is.True);
                    Assert.That(gearComp.Container, Is.Not.Null);

                    var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                    Assert.Multiple(() =>
                    {
                        Assert.That(gearComp.Container!.Contains(caster), Is.True);
                        Assert.That(stored.Bracer, Is.EqualTo(bracer));
                        Assert.That(stored.Deployed, Is.False);
                    });

                    Assert.That(attachments.TryToggleCaster((bracer, gearComp), hunter), Is.True);

                    Assert.Multiple(() =>
                    {
                        Assert.That(hands.IsHolding(hunter, caster), Is.True);
                        Assert.That(gearComp.Container!.Contains(caster), Is.False);
                        Assert.That(stored.Bracer, Is.EqualTo(bracer));
                        Assert.That(stored.Deployed, Is.True);
                    });

                    var dropped = hands.TryDrop(hunter, caster);

                    Assert.Multiple(() =>
                    {
                        Assert.That(dropped, Is.False,
                            "The local RMC drop-attempt hook cancels the ordinary floor drop after doing the CMSS13 plasma_caster/dropped() deactivation equivalent.");
                        Assert.That(hands.IsHolding(hunter, caster), Is.False,
                            "CMSS13 plasma_caster/dropped() forceMoves the caster back to its source instead of leaving it in hand.");
                        Assert.That(gearComp.Container!.Contains(caster), Is.True,
                            "CMSS13 plasma_caster/dropped() forceMoves the caster back to its source bracer.");
                        Assert.That(stored.Bracer, Is.EqualTo(bracer));
                        Assert.That(stored.Deployed, Is.False,
                            "CMSS13 plasma_caster/dropped() clears source.caster_deployed.");
                    });
                }
                finally
                {
                    if (previousCulture != null)
                        loc.SetCulture(previousCulture);
                }
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain("You deactivate your plasma caster."),
                    "CMSS13 plasma_caster/dropped() shows this source notice instead of the generic local gear retraction text.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, bracer, caster })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterRefundsDeletedUnfiredProjectileToSourceBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
            EntityUid? refundedProjectile = null;
            EntityUid? firedProjectile = null;

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 3000;
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                var sourceCost = casterComp.Modes[casterComp.CurrentMode].PowerCost;
                var coordinates = entMan.GetComponent<TransformComponent>(caster).Coordinates;

                var takeAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(caster, takeAmmo);
                refundedProjectile = takeAmmo.Ammo.Single().Entity;

                Assert.Multiple(() =>
                {
                    Assert.That(refundedProjectile, Is.Not.Null);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 3000 - sourceCost),
                        "CMSS13 plasma_caster/load_into_chamber() drains charge_cost from the source bracer before creating the projectile.");
                });

                entMan.DeleteEntity(refundedProjectile!.Value);
                refundedProjectile = null;

                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 3000),
                    "CMSS13 plasma_caster/delete_bullet(projectile, refund = TRUE) refunds charge_cost to the source bracer for an unfired prepared projectile.");

                bracerComp.Charge = 3000;
                var firedAmmo = new TakeAmmoEvent(
                    1,
                    new List<(EntityUid? Entity, IShootable Shootable)>(),
                    coordinates,
                    hunter);
                entMan.EventBus.RaiseLocalEvent(caster, firedAmmo);
                firedProjectile = firedAmmo.Ammo.Single().Entity;
                Assert.That(firedProjectile, Is.Not.Null);

                entMan.EventBus.RaiseLocalEvent(caster, new AmmoShotEvent
                {
                    FiredProjectiles = [firedProjectile!.Value],
                });

                entMan.DeleteEntity(firedProjectile.Value);
                firedProjectile = null;

                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 3000 - sourceCost),
                    "CMSS13 fired caster projectiles are deleted without refunding the spent source-bracer charge.");
            }
            finally
            {
                foreach (var uid in new[] { refundedProjectile, firedProjectile })
                {
                    if (uid is { } value && !entMan.Deleted(value))
                        entMan.DeleteEntity(value);
                }

                foreach (var uid in new[] { hunter, bracer, caster })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterSingleLethalIsHarmlessAgainstTerrainLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trigger = entMan.System<TriggerSystem>();
            var projectile = entMan.SpawnEntity("CMUYautjaCasterLethalBolt", map.GridCoords);
            var wall = entMan.SpawnEntity("CMWallMetal", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            try
            {
                var terrainTriggered = trigger.Trigger(projectile, wall);
                var mobTriggered = trigger.Trigger(projectile, human);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<ExplodeOnTriggerComponent>(projectile), Is.True,
                        "The single-lethal bolt still explodes on valid impact targets.");
                    Assert.That(terrainTriggered, Is.False,
                        "CMSS13 /datum/ammo/energy/yautja/caster/bolt/single_lethal explodes on impact but is harmless if it hits terrain.");
                    Assert.That(mobTriggered, Is.True,
                        "CMSS13 single-lethal bolts still explode on mob impact.");
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, wall, human })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterSingleStunBoltUsesCmss13TargetFiltersAndDurations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var shooter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var projectile = entMan.SpawnEntity("CMUYautjaCasterStunBolt", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var predalien = entMan.SpawnEntity("CMUXenoAbomination", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                RaiseProjectileHit(entMan, projectile, human, shooter);
                AssertCasterStun(status, human, TimeSpan.FromSeconds(4),
                    "CMSS13 caster single_stun adds one second to stun_time for humans.");

                RaiseProjectileHit(entMan, projectile, xeno, shooter);
                AssertCasterStun(status, xeno, TimeSpan.FromSeconds(3),
                    "CMSS13 caster single_stun uses its base stun_time for non-Yautja, non-predalien carbon targets.");

                RaiseProjectileHit(entMan, projectile, yautja, shooter);
                AssertNoCasterStun(status, yautja,
                    "CMSS13 caster single_stun returns early for Yautja targets.");

                RaiseProjectileHit(entMan, projectile, predalien, shooter);
                AssertNoCasterStun(status, predalien,
                    "CMSS13 caster single_stun returns early for predalien targets.");
            }
            finally
            {
                foreach (var uid in new[] { shooter, projectile, human, xeno, yautja, predalien })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlasmaCasterImmobilizerAppliesCmss13AreaStunOnHitAndMaxRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var shooter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var projectile = entMan.SpawnEntity("CMUYautjaCasterImmobilizerBolt", map.GridCoords);
            var hitObject = entMan.SpawnEntity("CMTable", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var predalien = entMan.SpawnEntity("CMUXenoAbomination", map.GridCoords.Offset(new Vector2(4, 0)));
            var outsideRange = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                RaiseProjectileHit(entMan, projectile, hitObject, shooter);

                AssertCasterStun(status, human, TimeSpan.FromSeconds(6),
                    "CMSS13 plasma immobilizer area stun uses stun_time = 6 for normal carbon targets.");
                AssertCasterStun(status, xeno, TimeSpan.FromSeconds(6),
                    "CMSS13 plasma immobilizer includes non-predalien xenos in orange(stun_range, projectile).");
                AssertCasterStun(status, yautja, TimeSpan.FromSeconds(4),
                    "CMSS13 plasma immobilizer reduces Yautja stun_time by two seconds instead of making them immune.");
                AssertNoCasterStun(status, predalien,
                    "CMSS13 plasma immobilizer skips predalien targets.");
                AssertNoCasterStun(status, outsideRange,
                    "CMSS13 plasma immobilizer only affects targets inside stun_range = 7.");

                ClearCasterStun(status, human);
                ClearCasterStun(status, xeno);
                ClearCasterStun(status, yautja);

                var maxRange = new ProjectileFixedDistanceStopEvent();
                entMan.EventBus.RaiseLocalEvent(projectile, ref maxRange);

                AssertCasterStun(status, human, TimeSpan.FromSeconds(6),
                    "CMSS13 plasma immobilizer do_at_max_range() runs the same area stun as direct impact.");
                AssertCasterStun(status, yautja, TimeSpan.FromSeconds(4),
                    "CMSS13 max-range immobilizer area stun keeps the same Yautja duration reduction.");
                AssertNoCasterStun(status, predalien,
                    "CMSS13 max-range immobilizer area stun still skips predaliens.");
            }
            finally
            {
                foreach (var uid in new[] { shooter, projectile, hitObject, human, xeno, yautja, predalien, outsideRange })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseProjectileHit(IEntityManager entMan, EntityUid projectile, EntityUid target, EntityUid shooter)
    {
        var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
        var damage = new DamageSpecifier(projectileComp.Damage);
        var hit = new ProjectileHitEvent(damage, target, shooter);
        entMan.EventBus.RaiseLocalEvent(projectile, ref hit);
    }

    private static void AssertCasterStun(
        StatusEffectQuerySystem status,
        EntityUid target,
        TimeSpan expectedDuration,
        string source)
    {
        foreach (var key in CasterStunStatuses)
        {
            Assert.That(status.TryGetTime(target, key, out var time), Is.True, $"{source} Missing {key}.");
            Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(expectedDuration), $"{source} {key} duration.");
        }
    }

    private static void AssertNoCasterStun(StatusEffectQuerySystem status, EntityUid target, string source)
    {
        foreach (var key in CasterStunStatuses)
        {
            Assert.That(status.TryGetTime(target, key, out _), Is.False, $"{source} Unexpected {key}.");
        }
    }

    private static void ClearCasterStun(StatusEffectQuerySystem status, EntityUid target)
    {
        foreach (var key in CasterStunStatuses)
        {
            status.TryRemoveStatusEffect(target, key);
        }
    }
}
