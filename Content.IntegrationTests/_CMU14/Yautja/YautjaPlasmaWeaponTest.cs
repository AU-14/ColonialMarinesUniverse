using Content.Shared.Power.Components;
using Content.Shared.Trigger.Systems;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Client.Weapons.Ranged.Systems;
using Content.Server.CMU14.Yautja;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.CombatMode;
using Content.Shared.Projectiles;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPlasmaWeaponTest
{

    public enum CasterHandRemovalOperation
    {
        EquipSuitStorage,
        MoveToOtherHand,
        InsertIntoContainer,
    }

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
                batterySystem.SetCharge((pistol, battery), 4);

                var ammo = entMan.GetComponent<BatteryAmmoProviderComponent>(pistol);
                var coordinates = entMan.GetComponent<TransformComponent>(pistol).Coordinates;
                var takeAmmo = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), coordinates, hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, takeAmmo);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.FireCost, Is.EqualTo(5),
                        "CMSS13 plasma pistol incendiary mode sets shot_cost = 5.");
                    Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(1),
                        "CMSS13 /obj/item/weapon/gun/energy/yautja/plasmapistol/has_ammunition() returns TRUE when charge_time >= 1 even in incendiary mode.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, battery)), Is.EqualTo(0),
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
                batterySystem.SetCharge((rifle, rifleBattery), 100);

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
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(93),
                        "CMSS13 plasma rifle load_into_chamber() subtracts 7 charge_time before the projectile is fired.");
                });

                entMan.DeleteEntity(rifleProjectile!.Value);
                rifleProjectile = null;

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(100),
                    "CMSS13 /plasmarifle/delete_bullet(projectile, refund = TRUE) refunds 7 charge_time for an unfired prepared projectile.");

                batterySystem.SetCharge((rifle, rifleBattery), 100);

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

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(93),
                    "CMSS13 only passes refund = TRUE for deleted prepared projectiles; projectiles that were fired keep their spent charge.");

                var toggleIncendiary = new UniqueActionEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(pistol, toggleIncendiary);
                Assert.That(toggleIncendiary.Handled, Is.True);

                var pistolBattery = entMan.GetComponent<BatteryComponent>(pistol);
                batterySystem.SetCharge((pistol, pistolBattery), 40);

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
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(35),
                        "CMSS13 plasma pistol incendiary mode sets shot_cost = 5 and subtracts it after creating the projectile.");
                });

                entMan.DeleteEntity(pistolProjectile!.Value);
                pistolProjectile = null;

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(40),
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
                batterySystem.SetCharge((pistol, pistolBattery), 4);

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
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(0),
                        "CMSS13 plasma pistol subtracts shot_cost after creating the projectile; local battery clamps when less charge was available.");
                });

                entMan.DeleteEntity(pistolProjectile!.Value);
                pistolProjectile = null;

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(4),
                    "Refunding a locally clamped low-charge shot must restore only the charge that was actually spent, not create extra charge.");

                var carbineFireModes = entMan.GetComponent<BatteryWeaponFireModesComponent>(carbine);
                Assert.That(fireModeSystem.TrySetFireMode((carbine, carbineFireModes), 1), Is.True);

                var carbineBattery = entMan.GetComponent<BatteryComponent>(carbine);
                batterySystem.SetCharge((carbine, carbineBattery), 1);

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
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((carbine, carbineBattery)), Is.EqualTo(0),
                        "CMSS13 plasma carbine impact-explosive mode subtracts shot_cost after creating the projectile; local battery clamps when less charge was available.");
                });

                entMan.DeleteEntity(carbineProjectile!.Value);
                carbineProjectile = null;

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((carbine, carbineBattery)), Is.EqualTo(1),
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
                batterySystem.SetCharge((rifle, rifleBattery), 50);
                batterySystem.SetCharge((pistol, pistolBattery), 20);
                batterySystem.SetCharge((carbine, carbineBattery), 20);

                batterySystem.Update(0.5f);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(50),
                        "CMSS13 plasma rifle process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(20),
                        "CMSS13 plasma pistol process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((carbine, carbineBattery)), Is.EqualTo(20),
                        "CMSS13 plasma carbine process() increments charge_time by one only when the object process runs; half a local second should not create fractional charge.");
                });

                batterySystem.Update(0.5f);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(51),
                        "After one accumulated process tick, the rifle gains exactly one charge_time.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(21),
                        "After one accumulated process tick, the pistol gains exactly one charge_time.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((carbine, carbineBattery)), Is.EqualTo(21),
                        "After one accumulated process tick, the carbine gains exactly one charge_time.");
                });

                batterySystem.Update(2.25f);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(53),
                        "CMSS13 plasma rifle process() cannot gain fractional charge_time from leftover frame time.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((pistol, pistolBattery)), Is.EqualTo(23),
                        "CMSS13 plasma pistol process() cannot gain fractional charge_time from leftover frame time.");
                    Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((carbine, carbineBattery)), Is.EqualTo(23),
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
    public async Task PlasmaRechargeIntervalBoundariesDoNotDriftAcrossSubdividedFrames()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var batterySystem = entMan.System<BatterySystem>();
            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);

            try
            {
                var rifleBattery = entMan.GetComponent<BatteryComponent>(rifle);
                var rifleRecharger = entMan.GetComponent<BatterySelfRechargerComponent>(rifle);
                batterySystem.SetCharge((rifle, rifleBattery), 50);

                for (var i = 0; i < 60; i++)
                    batterySystem.Update(1f / 60f);

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(51),
                    "Exactly sixty 1/60-second frames must reach the first one-second recharge boundary.");

                for (var i = 60; i < 300; i++)
                    batterySystem.Update(1f / 60f);

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(55),
                    "Five seconds split into 300 frames must produce all five whole recharge intervals.");

                batterySystem.SetCharge((rifle, rifleBattery), 50);
                rifleRecharger.AutoRechargeAccumulatorSeconds = default;

                for (var i = 0; i < 100; i++)
                    batterySystem.Update(0.01f);

                Assert.That(entMan.System<Content.Shared.Power.EntitySystems.SharedBatterySystem>().GetCharge((rifle, rifleBattery)), Is.EqualTo(51),
                    "One hundred 0.01-second frames must reach the same one-second boundary without float drift.");


            }
            finally
            {
                foreach (var uid in new[] { rifle })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ContinuousBatteryRechargeUsesElapsedSimulationTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid cell = default;
        TimeSpan start = default;

        await server.WaitAssertion(() =>
        {
            cell = server.EntMan.SpawnEntity("PowerCellMicroreactor", MapCoordinates.Nullspace);
            server.System<BatterySystem>().SetCharge(cell, 50);
            start = timing.CurTime;
        });

        await server.WaitRunTicks(15);

        await server.WaitAssertion(() =>
        {
            var rate = server.EntMan.GetComponent<BatterySelfRechargerComponent>(cell).AutoRechargeRate;
            var elapsed = (float) (timing.CurTime - start).TotalSeconds;
            Assert.That(elapsed, Is.GreaterThan(0));
            Assert.That(server.System<BatterySystem>().GetCharge(cell),
                Is.EqualTo(50 + rate * elapsed).Within(0.001f),
                "Non-interval batteries use the current predicted charge model.");
            server.EntMan.DeleteEntity(cell);
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
                var invisibility = entMan.EnsureComponent<EntityTurnInvisibleComponent>(hunter);
                invisibility.Enabled = true;
                invisibility.RestrictWeapons = true;
                entMan.EventBus.RaiseLocalEvent(caster, ref linkedAttempt);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                Assert.That(linkedAttempt.Cancelled, Is.False,
                    $"The control caster has a CMSS13 source bracer, Yautja tech, enough source charge, and may fire while cloaked. " +
                    $"Message={linkedAttempt.Message ?? "<null>"}, " +
                    $"HasYautja={entMan.HasComponent<YautjaComponent>(hunter)}, " +
                    $"Source={stored.Bracer}, " +
                    $"SourceQueued={entMan.IsQueuedForDeletion(bracer)}, " +
                    $"SourceCharge={sourceBracer.Charge}, " +
                    $"Mode={casterComp.CurrentMode}, " +
                    $"ModeCost={casterComp.PowerCost}.");

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
    public async Task PlasmaCasterClientFireInputCooldownShowsNotReadyPopup()
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
        NetEntity hunterNet = default;
        NetEntity casterNet = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();

                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
                stored.Bracer = bracer;
                stored.Deployed = true;

                var hands = entMan.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(hunter, caster), Is.True);
                Assert.That(hands.IsHolding(hunter, caster, out var casterHand), Is.True);
                hands.TrySetActiveHand(hunter, casterHand);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(caster));
                entMan.System<SharedCombatModeSystem>().SetInCombatMode(hunter, true);

                hunterNet = entMan.GetNetEntity(hunter);
                casterNet = entMan.GetNetEntity(caster);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitPost(() =>
            {
                var entMan = client.EntMan;
                var clientHunter = entMan.GetEntity(hunterNet);
                var clientCaster = entMan.GetEntity(casterNet);
                var loc = client.ResolveDependency<ILocalizationManager>();
                var previousCulture = loc.DefaultCulture;
                var gun = entMan.GetComponent<GunComponent>(clientCaster);
                var timing = client.ResolveDependency<IGameTiming>();
                var inputManager = client.ResolveDependency<IInputManager>();
                var inputSystem = entMan.System<Robust.Client.GameObjects.InputSystem>();
                var gunSystem = entMan.System<GunSystem>();
                var key = gun.UseKey ? EngineKeyFunctions.Use : EngineKeyFunctions.UseSecondary;
                var keyId = inputManager.NetworkBindMap.KeyFunctionID(key);

                Assert.That(entMan.GetComponent<CombatModeComponent>(clientHunter).IsInCombatMode, Is.True);
                Assert.That(entMan.HasComponent<YautjaComponent>(clientHunter), Is.True);
                Assert.That(gunSystem.TryGetGun(clientHunter, out var activeGun), Is.True);
                Assert.That(activeGun.Owner, Is.EqualTo(clientCaster));
                Assert.That(timing.IsFirstTimePredicted, Is.True);

                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
                try
                {
                    var target = entMan.GetComponent<TransformComponent>(clientHunter).Coordinates;
                    entMan.RemoveComponent<YautjaComponent>(clientHunter);
                    List<EntityUid>? projectiles;
                    try
                    {
                        projectiles = entMan.System<SharedGunSystem>().AttemptShoot((clientCaster, gun), clientHunter, target);
                    }
                    finally
                    {
                        entMan.EnsureComponent<YautjaComponent>(clientHunter);
                    }

                    Assert.That(projectiles, Is.Null);
                    Assert.That(gun.NextFire, Is.GreaterThan(timing.CurTime),
                        "The test must enter the same client-side NextFire gate used by real fire input.");
                    var labelsBeforeInput = entMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                    Assert.That(labelsBeforeInput.Any(label => label.Contains("Plasma caster is not ready to fire")),
                        Is.False,
                        $"Cooldown setup must not create the popup being tested. Actual labels: {string.Join(" | ", labelsBeforeInput)}");

                    var keyDown = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction, keyId)
                    {
                        State = BoundKeyState.Down,
                        Coordinates = target,
                        Uid = clientHunter,
                    };
                    inputSystem.HandleInputCommand(client.Session, key, keyDown);
                    gunSystem.Update(0f);
                }
                finally
                {
                    var keyUp = new ClientFullInputCmdMessage(timing.CurTick, timing.TickFraction, keyId)
                    {
                        State = BoundKeyState.Up,
                        Coordinates = entMan.GetComponent<TransformComponent>(clientHunter).Coordinates,
                        Uid = clientHunter,
                    };
                    inputSystem.HandleInputCommand(client.Session, key, keyUp);

                    if (previousCulture != null)
                        loc.SetCulture(previousCulture);
                }
            });

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.ToList();
                var cooldownPopup = labels.SingleOrDefault(label =>
                    label.Text.Contains("Plasma caster is not ready to fire"));

                Assert.That(cooldownPopup, Is.Not.Null,
                    $"Expected a plasma caster cooldown popup. Actual labels: {string.Join(" | ", labels.Select(label => label.Text))}");
                Assert.That(cooldownPopup!.Text, Does.Not.Contain("[color="));
                Assert.That(cooldownPopup.Text, Does.Not.Contain("[/color]"));
                Assert.That(cooldownPopup.Type, Is.EqualTo(PopupType.SmallCaution));
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

                    var canDrop = hands.CanDrop(hunter, caster);

                    Assert.Multiple(() =>
                    {
                        Assert.That(canDrop, Is.True,
                            "A drop capability query must report feasibility without performing the drop.");
                        Assert.That(hands.IsHolding(hunter, caster), Is.True,
                            "A drop capability query must not remove the caster from the hunter's hand.");
                        Assert.That(gearComp.Container!.Contains(caster), Is.False,
                            "A drop capability query must not move the caster back into the bracer.");
                        Assert.That(stored.Deployed, Is.True,
                            "A drop capability query must not change deployed lifecycle state.");
                    });

                    var dropped = hands.TryDrop(hunter, caster);

                    Assert.Multiple(() =>
                    {
                        Assert.That(dropped, Is.True,
                            "The real floor-drop action should complete before its DroppedEvent performs the CMSS13 plasma_caster/dropped() deactivation equivalent.");
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

    [TestCase(CasterHandRemovalOperation.EquipSuitStorage)]
    [TestCase(CasterHandRemovalOperation.MoveToOtherHand)]
    [TestCase(CasterHandRemovalOperation.InsertIntoContainer)]
    public async Task PlasmaCasterActualNonFloorHandRemovalReturnsToSourceBracer(
        CasterHandRemovalOperation operation)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid caster = default;
        EntityUid containerOwner = default;
        EntityUid outerClothing = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var attachments = entMan.System<YautjaAttachmentSystem>();
                var containers = entMan.System<SharedContainerSystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Caster, out caster), Is.True);
                Assert.That(attachments.TryToggleCaster((bracer, gearComp), hunter), Is.True);
                Assert.That(hands.IsHolding(hunter, caster, out var casterHand), Is.True);

                var completed = operation switch
                {
                    CasterHandRemovalOperation.EquipSuitStorage => EquipCasterInSuitStorage(
                        entMan,
                        inventory,
                        hunter,
                        caster,
                        map.GridCoords,
                        out outerClothing),
                    CasterHandRemovalOperation.MoveToOtherHand => MoveCasterToOtherHand(
                        entMan,
                        hands,
                        hunter,
                        casterHand!),
                    CasterHandRemovalOperation.InsertIntoContainer => InsertCasterIntoContainer(
                        entMan,
                        containers,
                        hands,
                        hunter,
                        caster,
                        map.GridCoords.Offset(new Vector2(1, 0)),
                        out containerOwner),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
                };

                Assert.That(completed, Is.True,
                    $"The {operation} operation must actually remove the caster from its original hand before lifecycle correction runs.");
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var containers = entMan.System<SharedContainerSystem>();
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, caster), Is.False,
                        $"A completed {operation} must not leave a deployed caster in either hand.");
                    Assert.That(gearComp.Container!.Contains(caster), Is.True,
                        $"A completed {operation} must return the caster to its source bracer.");
                    Assert.That(stored.Deployed, Is.False,
                        $"A completed {operation} must clear deployed lifecycle state.");
                    Assert.That(containers.TryGetContainingContainer(caster, out var containing), Is.True);
                    Assert.That(containing!.Owner, Is.EqualTo(bracer),
                        $"A completed {operation} must not leave the caster in an inventory or arbitrary container.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, caster, containerOwner, outerClothing })
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
                var sourceCost = casterComp.PowerCost;
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
            var shooter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var projectile = entMan.SpawnEntity("CMUYautjaCasterStunBolt", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var predalien = entMan.SpawnEntity("CMUXenoAbomination", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                RaiseProjectileHit(entMan, projectile, human, shooter);
                AssertCasterStun(entMan, human, TimeSpan.FromSeconds(4),
                    "CMSS13 caster single_stun adds one second to stun_time for humans.");

                RaiseProjectileHit(entMan, projectile, xeno, shooter);
                AssertCasterStun(entMan, xeno, TimeSpan.FromSeconds(3 * 0.667),
                    "The caster's three-second base stun respects RMC's 0.667 xeno duration multiplier.");

                RaiseProjectileHit(entMan, projectile, yautja, shooter);
                AssertNoCasterStun(entMan, yautja,
                    "CMSS13 caster single_stun returns early for Yautja targets.");

                RaiseProjectileHit(entMan, projectile, predalien, shooter);
                AssertNoCasterStun(entMan, predalien,
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

                AssertCasterStun(entMan, human, TimeSpan.FromSeconds(6),
                    "CMSS13 plasma immobilizer area stun uses stun_time = 6 for normal carbon targets.");
                AssertCasterStun(entMan, xeno, TimeSpan.FromSeconds(6 * 0.667),
                    "The immobilizer's six-second area stun respects RMC's 0.667 xeno duration multiplier.");
                AssertNoCasterStun(entMan, yautja,
                    "The current master's regular hunter immunity also rejects immobilizer stun.");
                AssertNoCasterStun(entMan, predalien,
                    "CMSS13 plasma immobilizer skips predalien targets.");
                AssertNoCasterStun(entMan, outsideRange,
                    "CMSS13 plasma immobilizer only affects targets inside stun_range = 7.");

                ClearCasterStun(entMan, human);
                ClearCasterStun(entMan, xeno);

                var maxRange = new ProjectileFixedDistanceStopEvent();
                entMan.EventBus.RaiseLocalEvent(projectile, ref maxRange);

                AssertCasterStun(entMan, human, TimeSpan.FromSeconds(6),
                    "CMSS13 plasma immobilizer do_at_max_range() runs the same area stun as direct impact.");
                AssertNoCasterStun(entMan, yautja,
                    "Max-range immobilizer stun respects the same regular hunter immunity.");
                AssertNoCasterStun(entMan, predalien,
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

    [Test]
    public async Task PlasmaCasterEradicatorTriggersAtCmss13MaxRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", map.GridCoords);

            try
            {
                Assert.That(entMan.HasComponent<YautjaCasterEradicatorProjectileComponent>(projectile), Is.True,
                    "CMSS13 plasma eradicator has its own max-range/vehicle impact behavior.");

                var maxRange = new ProjectileFixedDistanceStopEvent();
                entMan.EventBus.RaiseLocalEvent(projectile, ref maxRange);

                Assert.That(entMan.IsQueuedForDeletion(projectile) || entMan.Deleted(projectile), Is.True,
                    "CMSS13 plasma eradicator do_at_max_range() detonates the projectile at max_range = 8.");
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
    public async Task PlasmaCasterEradicatorAppliesCmss13MultitileVehicleImpact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var projectile = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", map.GridCoords);
            var vehicle = entMan.SpawnEntity("VehicleTank", map.GridCoords);

            try
            {
                var mover = entMan.GetComponent<GridVehicleMoverComponent>(vehicle);
                var frame = entMan.GetComponent<HardpointIntegrityComponent>(vehicle);
                var before = frame.Integrity;

                RaiseProjectileHit(entMan, projectile, vehicle, null);

                Assert.Multiple(() =>
                {
                    Assert.That(mover.ImmobileUntil - server.Timing.CurTime,
                        Is.EqualTo(TimeSpan.FromSeconds(5)).Within(TimeSpan.FromMilliseconds(50)),
                        "CMSS13 plasma eradicator locks a multitile vehicle for vehicle_slowdown_time = 5 seconds.");
                    Assert.That(frame.Integrity, Is.LessThan(before),
                        "CMSS13 plasma eradicator applies ex_act(150, ..., 100) to a multitile vehicle.");
                });
            }
            finally
            {
                foreach (var uid in new[] { projectile, vehicle })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseProjectileHit(IEntityManager entMan, EntityUid projectile, EntityUid target, EntityUid? shooter)
    {
        var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
        var damage = new DamageSpecifier(projectileComp.Damage);
        var hit = new ProjectileHitEvent(damage, target, shooter);
        entMan.EventBus.RaiseLocalEvent(projectile, ref hit);
    }

    private static bool MoveCasterToOtherHand(
        IEntityManager entMan,
        SharedHandsSystem hands,
        EntityUid hunter,
        string casterHand)
    {
        var handsComp = entMan.GetComponent<HandsComponent>(hunter);
        var otherHand = handsComp.Hands.Keys.Single(hand => hand != casterHand);
        hands.TrySetActiveHand(hunter, otherHand);
        return hands.TryMoveHeldEntityToActiveHand(hunter, casterHand);
    }

    private static bool EquipCasterInSuitStorage(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid hunter,
        EntityUid caster,
        EntityCoordinates coordinates,
        out EntityUid outerClothing)
    {
        outerClothing = entMan.SpawnEntity("CMArmorM3Medium", coordinates);
        Assert.That(inventory.TryEquip(hunter, outerClothing, "outerClothing", silent: true, force: true), Is.True,
            "Suit-storage transfer setup must equip supporting outer clothing.");
        return inventory.TryEquip(hunter, caster, "suitstorage", silent: true, force: true);
    }

    private static bool InsertCasterIntoContainer(
        IEntityManager entMan,
        SharedContainerSystem containers,
        SharedHandsSystem hands,
        EntityUid hunter,
        EntityUid caster,
        EntityCoordinates coordinates,
        out EntityUid containerOwner)
    {
        containerOwner = entMan.SpawnEntity("CMCrowbar", coordinates);
        var container = containers.EnsureContainer<Container>(containerOwner, "cmu-yautja-plasma-caster-test");
        return hands.TryDropIntoContainer(hunter, caster, container);
    }

    private static void AssertCasterStun(
        IEntityManager entMan,
        EntityUid target,
        TimeSpan expectedDuration,
        string source,
        bool expectKnockdown = true)
    {
        var status = entMan.System<StatusEffectsSystem>();
        Assert.That(status.TryGetTime(target, SharedStunSystem.StunId, out var stun), Is.True, source);
        Assert.That(stun.EndEffectTime - stun.StartEffectTime, Is.EqualTo(expectedDuration).Within(TimeSpan.FromMilliseconds(1)), $"{source} Stun duration.");
        Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True, source);
        Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.EqualTo(expectKnockdown), source);

        if (!expectKnockdown)
        {
            Assert.That(status.HasStatusEffect(target, SharedStunSystem.ParalyzeId), Is.False, source);
            return;
        }

        if (entMan.HasComponent<CrawlerComponent>(target))
        {
            var knockedDown = entMan.GetComponent<KnockedDownComponent>(target);
            Assert.That(knockedDown.NextUpdate - stun.StartEffectTime, Is.EqualTo(expectedDuration), $"{source} Knockdown duration.");
        }
        else
        {
            Assert.That(status.TryGetTime(target, SharedStunSystem.ParalyzeId, out var paralyze), Is.True, source);
            Assert.That(paralyze.EndEffectTime - paralyze.StartEffectTime, Is.EqualTo(expectedDuration), $"{source} Paralysis duration.");
        }
    }

    private static void AssertNoCasterStun(IEntityManager entMan, EntityUid target, string source)
    {
        var status = entMan.System<StatusEffectsSystem>();
        Assert.That(status.HasStatusEffect(target, SharedStunSystem.StunId), Is.False, source);
        Assert.That(status.HasStatusEffect(target, SharedStunSystem.ParalyzeId), Is.False, source);
        Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False, source);
    }

    private static void ClearCasterStun(IEntityManager entMan, EntityUid target)
    {
        entMan.System<SharedStunSystem>().TryClearStunAndKnockdown(target);
    }
}
