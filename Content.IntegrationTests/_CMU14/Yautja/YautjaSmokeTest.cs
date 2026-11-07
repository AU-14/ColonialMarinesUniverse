using Content.Client.CMU14.UserInterface;
using Content.Server.Camera;
using Content.Shared.Camera;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Events;
using Content.Shared.Emp;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage.Components;
using System.Linq;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Medical.HUD.Components;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client.ContextMenu.UI;
using Content.Client._RMC14.Dialog;
using Content.Client.Popups;
using Content.Client.UserInterface.ControlExtensions;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.CMU14.Yautja;
using Content.Server.Atmos.Components;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Beam.Components;
using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Cuffs;
using Content.Server.Database;
using Content.Server.Doors.Systems;
using Content.Server.Emp;
using Content.Server.Examine;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Hands.Systems;
using Content.Server.Ghost.Roles;
using Content.Server.Mind;
using Content.Server.Physics.Controllers;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Power.Components;
using Content.Shared.Speech.Components;
using Content.Server.Verbs;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Vents;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Hide;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared._RMC14.Xenonids.Zoom;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Blocking;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Clothing.Components;
using Content.Shared.CombatMode;
using Content.Shared.CCVar;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Overlays;
using Content.Shared.Radio;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Body.Systems;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Tracker.Xeno;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Tether;
using Content.Shared.Speech;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Toggleable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;
namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaSmokeTest
{

    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Ghost.Roles.Raffles.GhostRoleRaffleSettingsPrototype> defaultPrototype = "default";

    [Test]
    public async Task CleanserGelRuntimeMatchesCmss13DissolveTimingAndGuards()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid cleaner = default;
        EntityUid target = default;
        EntityUid cloakedTarget = default;
        EntityUid stashedCleaner = default;
        TimeSpan dissolveStart = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            cleaner = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords);
            target = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new Vector2(1, 0)));
            cloakedTarget = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new Vector2(1, 1)));
            stashedCleaner = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords);

            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(hands.TryPickupAnyHand(hunter, cleaner), Is.True);

            var stashedInteract = new AfterInteractEvent(
                hunter,
                stashedCleaner,
                target,
                entMan.GetComponent<TransformComponent>(target).Coordinates,
                true);
            entMan.EventBus.RaiseLocalEvent(stashedCleaner, stashedInteract);

            Assert.Multiple(() =>
            {
                Assert.That(stashedInteract.Handled, Is.False,
                    "CMSS13 /obj/item/tool/yautja_cleaner/afterattack() returns if loc != user.");
                Assert.That(ActiveCleanserDoAfters(entMan, hunter), Is.EqualTo(0));
            });

            entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
            var cloakedInteract = new AfterInteractEvent(
                hunter,
                cleaner,
                cloakedTarget,
                entMan.GetComponent<TransformComponent>(cloakedTarget).Coordinates,
                true);
            entMan.EventBus.RaiseLocalEvent(cleaner, cloakedInteract);

            Assert.Multiple(() =>
            {
                Assert.That(cloakedInteract.Handled, Is.False,
                    "CMSS13 can_dissolve() rejects TRAIT_CLOAKED before starting the dissolve do_after.");
                Assert.That(ActiveCleanserDoAfters(entMan, hunter), Is.EqualTo(0));
            });

            entMan.RemoveComponent<EntityActiveInvisibleComponent>(hunter);

            var interact = new AfterInteractEvent(
                hunter,
                cleaner,
                target,
                entMan.GetComponent<TransformComponent>(target).Coordinates,
                true);
            entMan.EventBus.RaiseLocalEvent(cleaner, interact);
            dissolveStart = timing.CurTime;

            Assert.Multiple(() =>
            {
                Assert.That(interact.Handled, Is.True);
                Assert.That(ActiveCleanserDoAfters(entMan, hunter), Is.EqualTo(1));
                Assert.That(entMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaCleanserDoAfterEvent).Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(3)),
                    "CMSS13 cleaner uses do_after(user, 3 SECONDS, INTERRUPT_ALL, BUSY_ICON_HOSTILE).");
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var timing = server.ResolveDependency<IGameTiming>();
            var dissolving = entMan.GetComponent<YautjaDissolvingComponent>(target);

            Assert.Multiple(() =>
            {
                Assert.That(dissolving.DeleteAt - dissolveStart,
                    Is.EqualTo(TimeSpan.FromSeconds(18)).Within(TimeSpan.FromMilliseconds(200)),
                    "CMSS13 cleaner waits 3 seconds, then queues QDEL_IN(target, 15 SECONDS) after the gel is applied.");
                Assert.That(entMan.HasComponent<TimedCorrodingComponent>(target), Is.True,
                    "Local acid visuals/effects are an implementation detail, but the target must be marked as dissolving.");
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(15.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            Assert.That(entMan.Deleted(target) || entMan.IsQueuedForDeletion(target), Is.True,
                "CMSS13 cleaner deletes the target after the 15 second dissolve timer.");

            foreach (var uid in new[] { hunter, cleaner, cloakedTarget, stashedCleaner })
            {
                if (uid != default && !entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerRequiresBadBloodTechUserLikeCmss13AttackGate()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid ordinaryUser = default;
        EntityUid techNonBadBlood = default;
        EntityUid badBloodTech = default;
        EntityUid deniedHivebreaker = default;
        EntityUid techDeniedHivebreaker = default;
        EntityUid allowedHivebreaker = default;
        EntityUid ordinaryTarget = default;
        EntityUid techTarget = default;
        EntityUid allowedTarget = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                ordinaryUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                techNonBadBlood = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
                badBloodTech = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
                deniedHivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);
                techDeniedHivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords.Offset(new Vector2(0, 1)));
                allowedHivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords.Offset(new Vector2(0, 2)));
                ordinaryTarget = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                techTarget = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 1)));
                allowedTarget = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 2)));

                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techNonBadBlood);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(badBloodTech);
                entMan.EnsureComponent<NpcFactionMemberComponent>(badBloodTech).Factions.Add("CMUYautjaBadBlood");

                mobState.ChangeMobState(ordinaryTarget, MobState.Critical);
                mobState.ChangeMobState(techTarget, MobState.Critical);
                mobState.ChangeMobState(allowedTarget, MobState.Critical);

                var ordinaryInteract = new AfterInteractEvent(
                    ordinaryUser,
                    deniedHivebreaker,
                    ordinaryTarget,
                    entMan.GetComponent<TransformComponent>(ordinaryTarget).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(deniedHivebreaker, ordinaryInteract);

                var techNonBadBloodInteract = new AfterInteractEvent(
                    techNonBadBlood,
                    techDeniedHivebreaker,
                    techTarget,
                    entMan.GetComponent<TransformComponent>(techTarget).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(techDeniedHivebreaker, techNonBadBloodInteract);

                server.PlayerMan.SetAttachedEntity(session, allowedTarget);
                var badBloodInteract = new AfterInteractEvent(
                    badBloodTech,
                    allowedHivebreaker,
                    allowedTarget,
                    entMan.GetComponent<TransformComponent>(allowedTarget).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(allowedHivebreaker, badBloodInteract);

                Assert.Multiple(() =>
                {
                    Assert.That(ordinaryInteract.Handled, Is.False,
                        "CMSS13 hivebreaker rejects users without TRAIT_YAUTJA_TECH before starting do_after.");
                    Assert.That(techNonBadBloodInteract.Handled, Is.False,
                        "CMSS13 hivebreaker also requires user.faction == FACTION_YAUTJA_BADBLOOD.");
                    Assert.That(badBloodInteract.Handled, Is.True,
                        "A local tech-authorized Bad Blood user matches the CMSS13 TRAIT_YAUTJA_TECH plus FACTION_YAUTJA_BADBLOOD gate.");
                    Assert.That(ActiveHivebreakerDoAfters(entMan, ordinaryUser), Is.EqualTo(0));
                    Assert.That(ActiveHivebreakerDoAfters(entMan, techNonBadBlood), Is.EqualTo(0));
                    Assert.That(ActiveHivebreakerDoAfters(entMan, badBloodTech), Is.EqualTo(1));
                    Assert.That(entMan.GetComponent<DoAfterComponent>(badBloodTech).DoAfters.Values.Single(active =>
                        !active.Cancelled &&
                        !active.Completed &&
                        active.Args.Event is YautjaHivebreakerDoAfterEvent).Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(3)),
                        "CMSS13 hivebreaker starts a 3 second do_after after passing user, target and defeated-state gates.");
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

                foreach (var uid in new[]
                         {
                             ordinaryUser,
                             techNonBadBlood,
                             badBloodTech,
                             deniedHivebreaker,
                             techDeniedHivebreaker,
                             allowedHivebreaker,
                             ordinaryTarget,
                             techTarget,
                             allowedTarget,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerEmpActDrainsPowerAndDecloaksLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid wornBracer = default;
        EntityUid floorBracer = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                wornBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                floorBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, wornBracer, "gloves", silent: true, force: true), Is.True);

                var wornPower = entMan.GetComponent<YautjaBracerComponent>(wornBracer);
                wornPower.Charge = 1500;
                var wornEmp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10), null);
                entMan.EventBus.RaiseLocalEvent(hunter, ref wornEmp);

                var floorPower = entMan.GetComponent<YautjaBracerComponent>(floorBracer);
                floorPower.Charge = 600;
                var floorEmp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10), null);
                entMan.EventBus.RaiseLocalEvent(floorBracer, ref floorEmp);

                Assert.Multiple(() =>
                {
                    Assert.That(wornPower.Charge, Is.EqualTo((FixedPoint2) 500),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/emp_act(severity=1) drains 1000 charge from worn hunter bracers.");
                    Assert.That(wornEmp.Affected, Is.True,
                        "Hunter bracers should mark local EMP pulses as affected when they consume the pulse.");
                    Assert.That(floorPower.Charge, Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 hunter bracer EMP drain clamps charge at zero even when the bracer is not worn.");
                    Assert.That(floorEmp.Affected, Is.True);
                });
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False,
                    "CMSS13 hunter bracer emp_act() decloaks the wearer when the EMP hits a worn bracer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, wornBracer, floorBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task OrdinaryProjectileDecloakAbsorbsDamageLikeCmss13BulletHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid projectile = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.BulletDecloakChance = 1f;
                entMan.EnsureComponent<ProjectileComponent>(projectile);

                var hitDamage = new DamageSpecifier { DamageDict = { ["Piercing"] = 35 } };
                var ev = new ProjectileHitEvent(hitDamage, hunter, null);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 ordinary projectile decloak returns COMPONENT_CANCEL_BULLET_ACT, absorbing the bullet damage.");
                });

            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False,
                    "CMSS13 ordinary projectile bullet_hit() decloaks only on the prob(20) branch.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, projectile })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForcedProjectileDecloakKeepsDamageLikeCmss13BulletHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid projectile = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                entMan.GetComponent<YautjaBracerComponent>(bracer).BulletDecloakChance = 0f;
                entMan.EnsureComponent<ProjectileComponent>(projectile);

                var hitDamage = new DamageSpecifier { DamageDict = { ["Heat"] = 40 } };
                var ev = new ProjectileHitEvent(hitDamage, hunter, null);
                entMan.EventBus.RaiseLocalEvent(projectile, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 40),
                        "CMSS13 forced projectile decloak continues on to damage instead of cancelling bullet_act.");
                });

            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False,
                    "CMSS13 AMMO_ROCKET, AMMO_ENERGY and AMMO_ACIDIC projectiles always decloak the wearer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, projectile })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    [NonParallelizable]
    public async Task NonTechRandomBracerFunctionSlotsNineAndTenDelimbLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;
        HashSet<EntityUid> beforeAudio = new();

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
                // CMU14: the hunter bracer's new random shock draws from the same RNG stream this test
                // walks to reach the random-function slots, so it is switched off for the slot checks.
                comp.NonYautjaShockChance = 0f;

                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                beforeAudio = AudioEntities(entMan);

                entMan.EnsureComponent<YautjaBracerRandomSeedTestComponent>(bracer).Seed = 0;

                Assert.That(utility.TryCreateStabilisingCrystal((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2),
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();

                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                Assert.Multiple(() =>
                {
                    Assert.That(CountAttachedArms(body, user), Is.Zero,
                        "CMSS13 activate_random_verb() uses rand(1, 10), with slots 9 and 10 routing to delimb_user().");
                    Assert.That(AudioFileNamesAfter(entMan, beforeAudio),
                        Does.Contain("/Audio/CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav"),
                        "CMSS13 delimb_user() plays sound/weapons/wristblades_on.ogg at low volume.");
                });
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The device emits a strange noise and falls off... Along with your arms!");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();

                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { user, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechCloakedBracerShockDamagesLeftAndRightArmsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var inventory = entMan.System<InventorySystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);

                var leftArm = GetBodyPart(body, user, BodyPartType.Arm, BodyPartSymmetry.Left);
                var rightArm = GetBodyPart(body, user, BodyPartType.Arm, BodyPartSymmetry.Right);
                var leftBefore = entMan.GetComponent<BodyPartHealthComponent>(leftArm).Current;
                var rightBefore = entMan.GetComponent<BodyPartHealthComponent>(rightArm).Current;

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.User = user;
                comp.NonYautjaCloakShockChance = 1f;
                comp.NonYautjaShockChance = 0f;
                comp.NonYautjaDelimbChance = 0f;
                comp.NextNonYautjaCloakShock = TimeSpan.Zero;
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);

                utility.Update(0.1f);
                comp.NextNonYautjaCloakShock = TimeSpan.FromSeconds(30);

                var damage = entMan.System<DamageableSystem>().GetAllDamage(user);
                Assert.That(damage.DamageDict.TryGetValue("Heat", out var heat), Is.True);
                Assert.That(heat, Is.EqualTo((FixedPoint2) 10));
                Assert.That(entMan.GetComponent<BodyPartHealthComponent>(leftArm).Current, Is.EqualTo(leftBefore - (FixedPoint2) 5),
                    "CMSS13 shock_user() applies 5 BURN to l_arm.");
                Assert.That(entMan.GetComponent<BodyPartHealthComponent>(rightArm).Current, Is.EqualTo(rightBefore - (FixedPoint2) 5),
                    "CMSS13 shock_user() applies 5 BURN to r_arm.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { user, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerToggleLockPulledDeadHunterUsesCmss13UnlockDialogAndLog()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid hunterBracer = default;
        EntityUid victimBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                victimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                metadata.SetEntityName(hunter, "A'ke Ret");
                metadata.SetEntityName(victim, "Guan Thwei");
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(victim);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, victimBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(victim, MobState.Dead);
                Assert.That(pulling.TryStartPull(hunter, victim), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                var victimComp = entMan.GetComponent<YautjaBracerComponent>(victimBracer);
                Assert.That(victimComp.Locked, Is.True);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);

                var dialog = entMan.GetComponent<DialogComponent>(hunterBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog.Title, Is.EqualTo("Unlock Bracers"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to unlock this Yautja's bracer?"));
                    Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
                    Assert.That(victimComp.Locked, Is.True,
                        "CMSS13 attempt_toggle_lock() prompts before unlocking a grabbed dead hunter's bracer.");
                });

                RaiseDialogOption(entMan, hunterBracer, hunter, "Yes");
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You unlock the bracer.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var victimComp = entMan.GetComponent<YautjaBracerComponent>(victimBracer);
                Assert.That(victimComp.Locked, Is.False,
                    "CMSS13 attempt_toggle_lock() confirms by unlocking the grabbed dead hunter's currently worn bracer.");
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);
            Assert.That(
                messages.Any(message =>
                    message.Contains("unlocked the", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("Guan Thwei", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 logs '[key_name(user)] unlocked the [bracer.name] of [key_name(victim)].'\nActual logs:\n{joinedMessages}");
            Assert.That(
                messages,
                Has.None.Contains("unlocked Yautja bracer").IgnoreCase,
                $"The grabbed-dead-hunter path should use the CMSS13 interaction log phrase, not the old local generic bracer log.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, victim, hunterBracer, victimBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerToggleLockPulledDeadHunterRequiresSameBracerOnConfirm()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid hunterBracer = default;
        EntityUid originalVictimBracer = default;
        EntityUid replacementVictimBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                originalVictimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                replacementVictimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(victim);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, originalVictimBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(victim, MobState.Dead);
                Assert.That(pulling.TryStartPull(hunter, victim), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
                var dialog = entMan.GetComponent<DialogComponent>(hunterBracer);
                Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to unlock this Yautja's bracer?"));

                Assert.That(inventory.TryUnequip(victim, "gloves", silent: true, force: true), Is.True);
                var originalComp = entMan.GetComponent<YautjaBracerComponent>(originalVictimBracer);
                originalComp.Locked = true;
                entMan.Dirty(originalVictimBracer, originalComp);
                Assert.That(inventory.TryEquip(victim, replacementVictimBracer, "gloves", silent: true, force: true), Is.True);

                RaiseDialogOption(entMan, hunterBracer, hunter, "Yes");
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var originalComp = entMan.GetComponent<YautjaBracerComponent>(originalVictimBracer);
                var replacementComp = entMan.GetComponent<YautjaBracerComponent>(replacementVictimBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(originalComp.Locked, Is.True,
                        "CMSS13 requires victim.gloves to still be the bracer captured before the confirmation prompt.");
                    Assert.That(replacementComp.Locked, Is.True,
                        "A stale CMSS13 bracer unlock confirmation must not unlock a replacement bracer equipped after the prompt opens.");
                });
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);
            Assert.That(
                messages,
                Has.None.Contains("unlocked the").IgnoreCase,
                $"A stale CMSS13 dead-hunter bracer unlock confirmation should not write the interaction log.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, victim, hunterBracer, originalVictimBracer, replacementVictimBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentInstallRefusesThirdMixedKindAttachmentLikeCmss13()
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
            var leftAttachment = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var rightAttachment = entMan.SpawnEntity("CMUYautjaScimitarAltAttachment", MapCoordinates.Nullspace);
            var thirdAttachment = entMan.SpawnEntity("CMUYautjaBracerShieldAttachment", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, leftAttachment), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, rightAttachment), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installLeft = new InteractUsingEvent(hunter, leftAttachment, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installLeft);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installRight = new InteractUsingEvent(hunter, rightAttachment, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installRight);

                Assert.That(installLeft.Handled, Is.True);
                Assert.That(installRight.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(leftAttachment), Is.True);
                Assert.That(gearComp.Container.Contains(rightAttachment), Is.True);
                Assert.That(gearComp.InstalledGear.Count(gear =>
                    entMan.GetComponent<YautjaStoredGearComponent>(gear).DeployedPrototype != null), Is.EqualTo(2));

                Assert.That(hands.TryPickupAnyHand(hunter, thirdAttachment), Is.True);
                var installThird = new InteractUsingEvent(hunter, thirdAttachment, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installThird);

                Assert.Multiple(() =>
                {
                    Assert.That(installThird.Handled, Is.False,
                        "CMSS13 refuses any third bracer attachment once left_bracer_attachment and right_bracer_attachment are occupied, regardless of attachment subtype.");
                    Assert.That(hands.IsHolding(hunter, thirdAttachment), Is.True);
                    Assert.That(gearComp.Container.Contains(leftAttachment), Is.True);
                    Assert.That(gearComp.Container.Contains(rightAttachment), Is.True);
                    Assert.That(gearComp.Container.Contains(thirdAttachment), Is.False);
                    Assert.That(gearComp.InstalledGear, Does.Contain(leftAttachment));
                    Assert.That(gearComp.InstalledGear, Does.Contain(rightAttachment));
                    Assert.That(gearComp.InstalledGear, Does.Not.Contain(thirdAttachment));
                    Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(leftAttachment));
                    Assert.That(gearComp.SecondaryGear[YautjaGearKind.Scimitar], Is.EqualTo(rightAttachment));
                    Assert.That(gearComp.Gear, Does.Not.ContainKey(YautjaGearKind.Shield),
                        "Refusing the third attachment must not create a shield slot entry.");
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(leftAttachment))
                    entMan.DeleteEntity(leftAttachment);
                if (!entMan.Deleted(rightAttachment))
                    entMan.DeleteEntity(rightAttachment);
                if (!entMan.Deleted(thirdAttachment))
                    entMan.DeleteEntity(thirdAttachment);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentDeployConsumesCmss13FiftyPowerOnlyOnDeploy()
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
            var holder = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleWristBlades", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

                var bracerPower = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerPower.Charge = 300;

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var holderStored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
                var install = new InteractUsingEvent(hunter, holder, bracer, entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                Assert.That(install.Handled, Is.True);
                Assert.That(holderStored.AttachedWeapon, Is.Not.Null);
                var wristBlades = holderStored.AttachedWeapon.Value;
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(holder), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleWristBladesEvent(hunter, action, actionComp));

                Assert.Multiple(() =>
                {
                    Assert.That(hands.IsHolding(hunter, wristBlades), Is.True);
                    Assert.That(gearComp.Container.Contains(holder), Is.True);
                    Assert.That(bracerPower.Charge, Is.EqualTo((FixedPoint2) 250));
                });

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleWristBladesEvent(hunter, action, actionComp));

                Assert.Multiple(() =>
                {
                    Assert.That(gearComp.Container.Contains(holder), Is.True);
                    Assert.That(holderStored.AttachedContainer!.Contains(wristBlades), Is.True);
                    Assert.That(bracerPower.Charge, Is.EqualTo((FixedPoint2) 250));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(holder))
                    entMan.DeleteEntity(holder);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefaultBracerShieldCompletesDeployBlockRetractCycle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blocking = entMan.System<BlockingSystem>();
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var holder = entMan.SpawnEntity("CMUYautjaBracerShieldAttachment", map.GridCoords);
            var filler = entMan.SpawnEntity("CMUYautjaDuellingKnife", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleShield", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var install = new InteractUsingEvent(hunter, holder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                Assert.That(install.Handled, Is.True);
                var stored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
                Assert.That(stored.AttachedWeapon, Is.Not.Null);
                var shield = stored.AttachedWeapon!.Value;

                Assert.That(hands.TryPickup(hunter, filler, HandIdForLocation(hands, hunter, HandLocation.Left)), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleShieldActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Handled, Is.True);
                    AssertHeldInHandLocation(hands, hunter, shield, HandLocation.Right);
                    Assert.That(stored.Deployed, Is.True);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 250));
                });

                var blockingComp = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.RaiseShield((shield, blockingComp), hunter), Is.True,
                    "The deployed bracer shield must support active blocking.");
                Assert.That(blockingComp.IsRaised, Is.True);
                Assert.That(blocking.LowerShield((shield, blockingComp), hunter), Is.True);
                Assert.That(blockingComp.IsRaised, Is.False);

                var retract = new YautjaToggleShieldActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, retract);

                Assert.Multiple(() =>
                {
                    Assert.That(retract.Handled, Is.True);
                    Assert.That(gearComp.Container.Contains(holder), Is.True);
                    Assert.That(stored.AttachedContainer!.Contains(shield), Is.True);
                    Assert.That(stored.Deployed, Is.False);
                    Assert.That(hands.IsHolding(hunter, shield), Is.False);
                    Assert.That(entMan.HasComponent<UnremoveableComponent>(shield), Is.False,
                        "Stored-gear lifecycle rules must replace the global unremoveable flag.");
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(holder))
                    entMan.DeleteEntity(holder);
                if (!entMan.Deleted(filler))
                    entMan.DeleteEntity(filler);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentDeploysSelectedSidesLikeCmss13()
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
            var rightHolder = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var leftHolder = entMan.SpawnEntity("CMUYautjaScimitarAltAttachment", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, rightHolder), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, leftHolder), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installRight = new InteractUsingEvent(hunter, rightHolder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installRight);
                RaiseDialogOption(entMan, bracer, hunter, "Right");
                var installLeft = new InteractUsingEvent(hunter, leftHolder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installLeft);

                Assert.That(installRight.Handled, Is.True);
                Assert.That(installLeft.Handled, Is.True);

                var rightHand = HandIdForLocation(hands, hunter, HandLocation.Right);
                hands.TrySetActiveHand(hunter, rightHand);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                var rightWeapon = entMan.GetComponent<YautjaStoredGearComponent>(rightHolder).AttachedWeapon;
                var leftWeapon = entMan.GetComponent<YautjaStoredGearComponent>(leftHolder).AttachedWeapon;
                Assert.That(rightWeapon, Is.Not.Null);
                Assert.That(leftWeapon, Is.Not.Null);
                AssertHeldInHandLocation(hands, hunter, rightWeapon!.Value, HandLocation.Right);
                AssertHeldInHandLocation(hands, hunter, leftWeapon!.Value, HandLocation.Left);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(rightHolder))
                    entMan.DeleteEntity(rightHolder);
                if (!entMan.Deleted(leftHolder))
                    entMan.DeleteEntity(leftHolder);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentUseInHandRetractsPairLikeCmss13AttackSelf()
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
            var scimitarHolder = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var altScimitarHolder = entMan.SpawnEntity("CMUYautjaScimitarAltAttachment", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitarHolder), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitarHolder), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitarHolder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitarHolder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var scimitarStored = entMan.GetComponent<YautjaStoredGearComponent>(scimitarHolder);
                var altScimitarStored = entMan.GetComponent<YautjaStoredGearComponent>(altScimitarHolder);
                Assert.That(scimitarStored.AttachedWeapon, Is.Not.Null);
                Assert.That(altScimitarStored.AttachedWeapon, Is.Not.Null);
                var scimitar = scimitarStored.AttachedWeapon!.Value;
                var altScimitar = altScimitarStored.AttachedWeapon!.Value;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitarHolder), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitarHolder), Is.True);
                Assert.That(scimitarStored.AttachedContainer!.Contains(scimitar), Is.False);
                Assert.That(altScimitarStored.AttachedContainer!.Contains(altScimitar), Is.False);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(scimitar, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                    Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);
                    Assert.That(scimitarStored.AttachedContainer.Contains(scimitar), Is.True);
                    Assert.That(altScimitarStored.AttachedContainer.Contains(altScimitar), Is.True);
                    Assert.That(scimitarStored.Deployed, Is.False);
                    Assert.That(altScimitarStored.Deployed, Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(scimitarHolder))
                    entMan.DeleteEntity(scimitarHolder);
                if (!entMan.Deleted(altScimitarHolder))
                    entMan.DeleteEntity(altScimitarHolder);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentWeaponsForceDoorsLikeCmss13Afterattack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid scimitar = default;
        EntityUid action = default;
        EntityUid airlock = default;
        EntityUid resinDoor = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var doors = entMan.System<DoorSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
                action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);
                airlock = entMan.SpawnEntity("CMAirlock", map.GridCoords.Offset(new Vector2(1, 0)));
                resinDoor = entMan.SpawnEntity("DoorXenoResin", map.GridCoords.Offset(new Vector2(0, 1)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);

                var install = new InteractUsingEvent(hunter, scimitar, bracer, entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                Assert.That(install.Handled, Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));
                Assert.That(TryGetHandHolding(hands, hunter, scimitar, out var scimitarHand), Is.True);
                hands.TrySetActiveHand(hunter, scimitarHand);
                Assert.That(entMan.GetComponent<HandsComponent>(hunter).ActiveHandId, Is.EqualTo(scimitarHand));
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(scimitar));

                doors.SetState(airlock, DoorState.Closed);
                doors.SetState(resinDoor, DoorState.Closed);
                Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.HasComponent<ResinDoorComponent>(resinDoor), Is.True);

                var airlockForce = new InteractUsingEvent(hunter, scimitar, airlock, entMan.GetComponent<TransformComponent>(airlock).Coordinates);
                entMan.EventBus.RaiseLocalEvent(airlock, airlockForce);

                Assert.Multiple(() =>
                {
                    Assert.That(airlockForce.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.EqualTo(DoorState.Closed));
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.3f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(scimitar));

                var resinForceOpen = new InteractUsingEvent(hunter, scimitar, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceOpen);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.Not.EqualTo(DoorState.Closed));
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(resinForceOpen.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Closed));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.8f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.Not.EqualTo(DoorState.Closed));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.0f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var resinForceClosed = new InteractUsingEvent(hunter, scimitar, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceClosed);

                Assert.Multiple(() =>
                {
                    Assert.That(resinForceClosed.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Open));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.3f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.Not.EqualTo(DoorState.Open));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (scimitar != default && !entMan.Deleted(scimitar))
                    entMan.DeleteEntity(scimitar);
                if (action != default && !entMan.Deleted(action))
                    entMan.DeleteEntity(action);
                if (airlock != default && !entMan.Deleted(airlock))
                    entMan.DeleteEntity(airlock);
                if (resinDoor != default && !entMan.Deleted(resinDoor))
                    entMan.DeleteEntity(resinDoor);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerRemoveAttachmentsDropsInstalledPairWhenHandsFullLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var containers = entMan.System<SharedContainerSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAltAttachment", MapCoordinates.Nullspace);
            var fillerLeft = entMan.SpawnEntity("Crowbar", MapCoordinates.Nullspace);
            var fillerRight = entMan.SpawnEntity("CMMRE", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaRemoveBracerAttachments", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var nativeGear = gearComp.InstalledGear
                    .Where(uid => entMan.GetComponent<YautjaStoredGearComponent>(uid).DeployedPrototype == null)
                    .ToArray();
                Assert.That(nativeGear, Is.Not.Empty);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
                Assert.That(nativeGear.All(gearComp.InstalledGear.Contains), Is.True,
                    "Installing physical holders must preserve the native action registry.");
                Assert.That(nativeGear.All(gearComp.Container.Contains), Is.True);

                Assert.That(hands.TryPickupAnyHand(hunter, fillerLeft), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, fillerRight), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var remove = new YautjaRemoveBracerAttachmentsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, remove);

                Assert.That(remove.Handled, Is.True);
                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.False);
                Assert.That(containers.IsEntityInContainer(scimitar), Is.False);
                Assert.That(containers.IsEntityInContainer(altScimitar), Is.False);
                Assert.That(gearComp.InstalledGear, Does.Not.Contain(scimitar));
                Assert.That(gearComp.InstalledGear, Does.Not.Contain(altScimitar));
                Assert.That(gearComp.SecondaryGear.ContainsKey(YautjaGearKind.Scimitar), Is.False);
                Assert.That(nativeGear.All(gearComp.InstalledGear.Contains), Is.True,
                    "Removing physical attachment holders must preserve native bracer gear.");
                Assert.That(nativeGear.All(gearComp.Container.Contains), Is.True);
                Assert.That(nativeGear.All(gearComp.Gear.Values.Contains), Is.True,
                    "Removing physical holders must restore native primary gear mappings.");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(scimitar))
                    entMan.DeleteEntity(scimitar);
                if (!entMan.Deleted(altScimitar))
                    entMan.DeleteEntity(altScimitar);
                if (!entMan.Deleted(fillerLeft))
                    entMan.DeleteEntity(fillerLeft);
                if (!entMan.Deleted(fillerRight))
                    entMan.DeleteEntity(fillerRight);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerEnteringStorageDecloaksWearerLikeCmss13OnEnterStorage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid pouch = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var containers = entMan.System<SharedContainerSystem>();
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                pouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var invisible = entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                invisible.Opacity = 0.2f;
                var turnInvisible = entMan.EnsureComponent<EntityTurnInvisibleComponent>(hunter);
                turnInvisible.Enabled = true;

                var storage = entMan.GetComponent<StorageComponent>(pouch);
                Assert.That(containers.Insert(bracer, storage.Container, force: true), Is.True,
                    "This simulates CMSS13 /obj/item/clothing/gloves/yautja/hunter/on_enter_storage() for a worn bracer entering storage.");

                Assert.That(entMan.TryGetComponent(hunter, out EntityTurnInvisibleComponent? turnAfter) && turnAfter.Enabled, Is.False,
                    "CMSS13 hunter bracer on_enter_storage() forces decloak when the bracer was still located on a cloaked human.");
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False,
                    "Local cloak component removal is deferred, but CMSS13 storage-entry decloak should be complete after the next tick.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, bracer, pouch })
                {
                    if (uid == default)
                        continue;

                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeldBracerIdChipTogglesEmbeddedChipLikeCmss13Keybind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerIdChip", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, bracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(bracer));

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var deploy = new YautjaToggleBracerIdChipActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                Assert.Multiple(() =>
                {
                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(bracerComp.IdChipDeployed, Is.True);
                    Assert.That(bracerComp.IdChip, Is.Not.Null);
                    Assert.That(inventory.TryGetSlotEntity(hunter, "id", out var id), Is.True);
                    Assert.That(id, Is.EqualTo(bracerComp.IdChip));
                });

                var retract = new YautjaToggleBracerIdChipActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, retract);

                Assert.Multiple(() =>
                {
                    Assert.That(retract.Handled, Is.True);
                    Assert.That(bracerComp.IdChipDeployed, Is.False);
                    Assert.That(inventory.TryGetSlotEntity(hunter, "id", out _), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornBracerLinkThrallBracerSetsSourceLinkStateLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaLinkThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var link = new YautjaLinkThrallBracerActionEvent
                {
                    Performer = master,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(masterBracer, link);

                var masterBracerComp = entMan.GetComponent<YautjaBracerComponent>(masterBracer);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(link.Handled, Is.True);
                    Assert.That(thrallComp.BracerLinked, Is.True);
                    Assert.That(thrallComp.MasterBracer, Is.EqualTo(masterBracer));
                    Assert.That(thrallComp.ThrallBracer, Is.EqualTo(thrallBracer));
                    Assert.That(thrallBracerComp.Master, Is.EqualTo(master));
                    Assert.That(thrallBracerComp.MasterBracer, Is.EqualTo(masterBracer));
                    Assert.That(thrallBracerComp.User, Is.EqualTo(thrall));
                    Assert.That(thrallBracerComp.Linked, Is.True);
                    Assert.That(thrallBracerComp.Locked, Is.True);
                    Assert.That(masterBracerComp.User, Is.EqualTo(master));
                });
            }
            finally
            {
                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(masterBracer))
                    entMan.DeleteEntity(masterBracer);
                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CallCombiActionRecallsDroppedChainedWeaponWithCmss13PowerCost()
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
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaCallCombi", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);
                Assert.That(hands.TryDrop(hunter, combistick, map.GridCoords.Offset(new Vector2(6, 0)), checkActionBlocker: false), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var call = new YautjaCallCombiActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(combistick, call);

                var endCoords = transform.GetMapCoordinates(combistick);
                var hunterCoords = transform.GetMapCoordinates(hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(call.Handled, Is.True);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230));
                    Assert.That(hands.IsHolding(hunter, combistick), Is.True);
                    Assert.That(endCoords.MapId, Is.EqualTo(hunterCoords.MapId));
                    Assert.That(endCoords.Position, Is.EqualTo(hunterCoords.Position));
                    Assert.That(entMan.GetComponent<YautjaChainedWeaponComponent>(combistick).LinkedTo, Is.Null,
                        "CMSS13 recall() calls cleanup_chain() after successfully yanking the chained weapon.");
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                foreach (var uid in new[] { bracer, combistick, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletGuardMatchesCmss13ChargeWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
            action = entMan.SpawnEntity("CMUActionYautjaGuardChainGauntlet", MapCoordinates.Nullspace);

            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

            var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
            var actionComp = entMan.GetComponent<ActionComponent>(action);
            var ev = new YautjaGuardChainGauntletActionEvent
            {
                Performer = hunter,
                Action = (action, actionComp),
            };

            entMan.EventBus.RaiseLocalEvent(gauntlet, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(chain.GuardActive, Is.True);
                Assert.That(chain.PunchKnockback, Is.EqualTo(7));
                Assert.That(chain.GuardExpiresAt, Is.GreaterThan(TimeSpan.Zero));
            });

            var speed = entMan.GetComponent<TemporarySpeedModifiersComponent>(hunter);
            Assert.That(speed.Modifiers, Has.Count.EqualTo(1));
            Assert.That(speed.Modifiers[0].Walk, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(speed.Modifiers[0].Sprint, Is.EqualTo(1.3f).Within(0.001f));

            var previousExpiresAt = chain.GuardExpiresAt;
            var repeat = new YautjaGuardChainGauntletActionEvent
            {
                Performer = hunter,
                Action = (action, actionComp),
            };
            entMan.EventBus.RaiseLocalEvent(gauntlet, repeat);
            Assert.That(chain.GuardExpiresAt, Is.EqualTo(previousExpiresAt));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
            Assert.Multiple(() =>
            {
                Assert.That(chain.GuardActive, Is.False);
                Assert.That(chain.PunchKnockback, Is.EqualTo(5));
                Assert.That(entMan.HasComponent<TemporarySpeedModifiersComponent>(hunter), Is.False);
            });

            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
            if (!entMan.Deleted(gauntlet))
                entMan.DeleteEntity(gauntlet);
            if (!entMan.Deleted(action))
                entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletWrapsChainwhipOnceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
            var chainwhip = entMan.SpawnEntity("CMUYautjaChainwhip", MapCoordinates.Nullspace);
            var extraChainwhip = entMan.SpawnEntity("CMUYautjaChainwhip", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, chainwhip), Is.True);

                var coords = entMan.GetComponent<TransformComponent>(gauntlet).Coordinates;
                var wrap = new InteractUsingEvent(hunter, chainwhip, gauntlet, coords);
                entMan.EventBus.RaiseLocalEvent(gauntlet, wrap);

                Assert.Multiple(() =>
                {
                    Assert.That(wrap.Handled, Is.True);
                    Assert.That(entMan.Deleted(chainwhip) || entMan.IsQueuedForDeletion(chainwhip), Is.True);
                });

                Assert.That(hands.TryPickupAnyHand(hunter, extraChainwhip), Is.True);
                var repeat = new InteractUsingEvent(hunter, extraChainwhip, gauntlet, coords);
                entMan.EventBus.RaiseLocalEvent(gauntlet, repeat);

                Assert.Multiple(() =>
                {
                    Assert.That(repeat.Handled, Is.True);
                    Assert.That(entMan.Deleted(extraChainwhip) || entMan.IsQueuedForDeletion(extraChainwhip), Is.False);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (!entMan.Deleted(chainwhip))
                    entMan.DeleteEntity(chainwhip);
                if (!entMan.Deleted(extraChainwhip))
                    entMan.DeleteEntity(extraChainwhip);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletMeleeHitsBuildCmss13ComboAndTimeout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
            target = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            entMan.EnsureComponent<YautjaComponent>(hunter);
            var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);

            for (var i = 0; i < 4; i++)
            {
                var hit = new MeleeHitEvent(new List<EntityUid> { target }, hunter, gauntlet, new DamageSpecifier(), null);
                entMan.EventBus.RaiseLocalEvent(gauntlet, hit);
            }

            Assert.Multiple(() =>
            {
                Assert.That(chain.ComboCounter, Is.EqualTo(4));
                Assert.That(chain.ComboExpiresAt, Is.GreaterThan(TimeSpan.Zero));
            });

            var miss = new MeleeHitEvent(new List<EntityUid>(), hunter, gauntlet, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(gauntlet, miss);
            Assert.That(chain.ComboCounter, Is.EqualTo(4));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(15.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
            var hit = new MeleeHitEvent(new List<EntityUid> { target }, hunter, gauntlet, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(gauntlet, hit);

            Assert.Multiple(() =>
            {
                Assert.That(chain.ComboCounter, Is.EqualTo(1));
                Assert.That(chain.ComboExpiresAt, Is.GreaterThan(TimeSpan.Zero));
            });

            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
            if (!entMan.Deleted(gauntlet))
                entMan.DeleteEntity(gauntlet);
            if (!entMan.Deleted(target))
                entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletDisarmFinisherConsumesComboAndThrowsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var readyTarget = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                entMan.RemoveComponent<TackleableComponent>(target);
                entMan.RemoveComponent<RMCDisarmableComponent>(target);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 3;

                var notReady = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(target, ref notReady);
                Assert.Multiple(() =>
                {
                    Assert.That(notReady.Handled, Is.False);
                    Assert.That(chain.ComboCounter, Is.EqualTo(3));
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(target), Is.False);
                });

                chain.ComboCounter = 4;
                var ready = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(readyTarget, ref ready);

                Assert.Multiple(() =>
                {
                    Assert.That(ready.Handled, Is.True);
                    Assert.That(chain.ComboCounter, Is.Zero);
                    Assert.That(chain.ComboExpiresAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.HasComponent<ThrownItemComponent>(readyTarget), Is.True);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
                if (!entMan.Deleted(readyTarget))
                    entMan.DeleteEntity(readyTarget);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainedGauntletDisarmFinisherPullsTargetBackAfterDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid target = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var physics = entMan.System<SharedPhysicsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 4;
                chain.HasChain = true;

                var ev = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(target, ref ev);

                var body = entMan.GetComponent<PhysicsComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(physics.GetMapLinearVelocity(target, body).X, Is.GreaterThan(0));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.7f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var body = entMan.GetComponent<PhysicsComponent>(target);

                Assert.That(physics.GetMapLinearVelocity(target, body).X, Is.LessThan(0));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (target != default && !entMan.Deleted(target))
                    entMan.DeleteEntity(target);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletHelpFinisherConsumesFiveComboAndKnocksTargetDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid target = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 4;

                var notReady = new InteractUsingEvent(hunter, gauntlet, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
                entMan.EventBus.RaiseLocalEvent(target, notReady);

                Assert.Multiple(() =>
                {
                    Assert.That(notReady.Handled, Is.False);
                    Assert.That(chain.ComboCounter, Is.EqualTo(4));
                    Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False);
                });

                chain.ComboCounter = 5;
                var damageBefore = entMan.System<DamageableSystem>().GetAllDamage(target).GetTotal();

                var ready = new InteractUsingEvent(hunter, gauntlet, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
                entMan.EventBus.RaiseLocalEvent(target, ready);

                var damageAfter = entMan.System<DamageableSystem>().GetAllDamage(target).GetTotal();
                Assert.Multiple(() =>
                {
                    Assert.That(ready.Handled, Is.True);
                    Assert.That(chain.ComboCounter, Is.Zero);
                    Assert.That(chain.ComboExpiresAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True);
                    Assert.That(damageAfter, Is.GreaterThan(damageBefore));
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (target != default && !entMan.Deleted(target))
                    entMan.DeleteEntity(target);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletGrabExecutionKillsCriticalTargetAfterDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid aliveTarget = default;
        EntityUid criticalTarget = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                aliveTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                criticalTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

                Assert.That(pulling.TryStartPull(hunter, aliveTarget), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent<DoAfterComponent>(hunter, out var doAfter) ? doAfter.DoAfters.Count : 0, Is.Zero);
                    Assert.That(mobState.IsDead(aliveTarget), Is.False);
                });

                mobState.ChangeMobState(criticalTarget, MobState.Critical);
                var damageBefore = entMan.System<DamageableSystem>().GetAllDamage(criticalTarget).GetTotal();

                Assert.That(pulling.TryStartPull(hunter, criticalTarget), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Count, Is.EqualTo(1));
                    Assert.That(mobState.IsDead(criticalTarget), Is.False);
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(criticalTarget).GetTotal(), Is.EqualTo(damageBefore));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.0f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(mobState.IsDead(criticalTarget), Is.True);
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(criticalTarget).GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (aliveTarget != default && !entMan.Deleted(aliveTarget))
                    entMan.DeleteEntity(aliveTarget);
                if (criticalTarget != default && !entMan.Deleted(criticalTarget))
                    entMan.DeleteEntity(criticalTarget);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletGrabExecutionBlocksDuplicateExecutionsDuringSourceRecovery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid firstTarget = default;
        EntityUid secondTarget = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                firstTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                secondTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                mobState.ChangeMobState(firstTarget, MobState.Critical);
                mobState.ChangeMobState(secondTarget, MobState.Critical);

                Assert.That(pulling.TryStartPull(hunter, firstTarget), Is.True);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                Assert.Multiple(() =>
                {
                    Assert.That(chain.Executing, Is.True);
                    Assert.That(entMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Count, Is.EqualTo(1));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.0f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                Assert.Multiple(() =>
                {
                    Assert.That(mobState.IsDead(firstTarget), Is.True);
                    Assert.That(chain.Executing, Is.True);
                });

                var beforeDuplicate = entMan.TryGetComponent<DoAfterComponent>(hunter, out var doAfter) ? doAfter.DoAfters.Count : 0;
                pulling.TryStopPull(firstTarget, entMan.GetComponent<PullableComponent>(firstTarget));
                Assert.That(pulling.TryStartPull(hunter, secondTarget), Is.True);
                Assert.That(entMan.TryGetComponent<DoAfterComponent>(hunter, out var currentDoAfter) ? currentDoAfter.DoAfters.Count : 0, Is.EqualTo(beforeDuplicate));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.6f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                Assert.That(chain.Executing, Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (firstTarget != default && !entMan.Deleted(firstTarget))
                    entMan.DeleteEntity(firstTarget);
                if (secondTarget != default && !entMan.Deleted(secondTarget))
                    entMan.DeleteEntity(secondTarget);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletForcesDoorsLikeCmss13Afterattack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid airlock = default;
        EntityUid resinDoor = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var doors = entMan.System<DoorSystem>();
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                airlock = entMan.SpawnEntity("CMAirlock", map.GridCoords.Offset(new Vector2(1, 0)));
                resinDoor = entMan.SpawnEntity("DoorXenoResin", map.GridCoords.Offset(new Vector2(0, 1)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(gauntlet));

                doors.SetState(airlock, DoorState.Closed);
                doors.SetState(resinDoor, DoorState.Closed);
                Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.HasComponent<ResinDoorComponent>(resinDoor), Is.True);

                var airlockForce = new InteractUsingEvent(hunter, gauntlet, airlock, entMan.GetComponent<TransformComponent>(airlock).Coordinates);
                entMan.EventBus.RaiseLocalEvent(airlock, airlockForce);

                Assert.Multiple(() =>
                {
                    Assert.That(airlockForce.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.EqualTo(DoorState.Closed));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.3f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(gauntlet));
                Assert.That(entMan.HasComponent<ResinDoorComponent>(resinDoor), Is.True);

                var resinForceOpen = new InteractUsingEvent(hunter, gauntlet, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceOpen);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.Not.EqualTo(DoorState.Closed));
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).DamageDict.GetValueOrDefault("Structural"), Is.EqualTo((FixedPoint2) 100));
                    Assert.That(resinForceOpen.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Closed));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.8f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var resinDoorComp = entMan.GetComponent<DoorComponent>(resinDoor);
                Assert.That(resinDoorComp.State, Is.Not.EqualTo(DoorState.Closed));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.0f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var resinForceClosed = new InteractUsingEvent(hunter, gauntlet, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceClosed);

                Assert.Multiple(() =>
                {
                    Assert.That(resinForceClosed.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Open));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.3f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.Not.EqualTo(DoorState.Open));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (airlock != default && !entMan.Deleted(airlock))
                    entMan.DeleteEntity(airlock);
                if (resinDoor != default && !entMan.Deleted(resinDoor))
                    entMan.DeleteEntity(resinDoor);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletDoesNotForceResinDoorsInHarmIntentLikeCmss13Afterattack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid gauntlet = default;
        EntityUid airlock = default;
        EntityUid resinDoor = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var combatMode = entMan.System<SharedCombatModeSystem>();
                var doors = entMan.System<DoorSystem>();
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                airlock = entMan.SpawnEntity("CMAirlock", map.GridCoords.Offset(new Vector2(1, 0)));
                resinDoor = entMan.SpawnEntity("DoorXenoResin", map.GridCoords.Offset(new Vector2(0, 1)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(gauntlet));
                combatMode.SetInCombatMode(hunter, true);

                doors.SetState(airlock, DoorState.Closed);
                doors.SetState(resinDoor, DoorState.Closed);

                var resinForceOpen = new InteractUsingEvent(hunter, gauntlet, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceOpen);

                Assert.Multiple(() =>
                {
                    Assert.That(resinForceOpen.Handled, Is.False);
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Closed));
                });

                var airlockForce = new InteractUsingEvent(hunter, gauntlet, airlock, entMan.GetComponent<TransformComponent>(airlock).Coordinates);
                entMan.EventBus.RaiseLocalEvent(airlock, airlockForce);

                Assert.Multiple(() =>
                {
                    Assert.That(airlockForce.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.EqualTo(DoorState.Closed));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.3f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.Not.EqualTo(DoorState.Closed));
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage(airlock).DamageDict.GetValueOrDefault("Structural"), Is.EqualTo((FixedPoint2) 100));
                    Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Closed));
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (gauntlet != default && !entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
                if (airlock != default && !entMan.Deleted(airlock))
                    entMan.DeleteEntity(airlock);
                if (resinDoor != default && !entMan.Deleted(resinDoor))
                    entMan.DeleteEntity(resinDoor);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructCanBeArmedAfterRecoveringFromCritical()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(hunter, MobState.Critical);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter, TimeSpan.FromSeconds(30)), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);

                mobState.ChangeMobState(hunter, MobState.Alive);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter, TimeSpan.FromSeconds(30)), Is.True);
                Assert.That(bracerComp.SelfDestructArmed, Is.True);
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechSelfDestructUseDelimbsInsteadOfArmingLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;
        HashSet<EntityUid> beforeAudio = new();

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.NonYautjaWorkingChance = 1f;
                bracerComp.NonYautjaRandomFunctionChance = 0f;
                bracerComp.NonYautjaDelimbChance = 0f;
                // CMU14: the hunter bracer's random shock (yaut_bracers.dm:392-400) would stun the wearer and
                // break the 3s misuse do_after this test walks, which is the flow under test.
                bracerComp.NonYautjaShockChance = 0f;

                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2));
                beforeAudio = AudioEntities(entMan);

                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2),
                    "CMSS13 activate_suicide_internal() still waits for check_random_function()'s do_after before always_delimb resolves.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var body = entMan.System<SharedBodySystem>();
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(CountAttachedArms(body, user), Is.Zero,
                        "CMSS13 activate_suicide_internal() passes always_delimb=TRUE into check_random_function(), so a non-tech successful use attempt delimb-punishes instead of opening the self-destruct flow.");
                    Assert.That(bracerComp.SelfDestructArmed, Is.False);
                    Assert.That(entMan.HasComponent<DialogComponent>(bracer), Is.False);
                    Assert.That(AudioFileNamesAfter(entMan, beforeAudio),
                        Does.Contain("/Audio/CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav"),
                        "CMSS13 delimb_user() plays sound/weapons/wristblades_on.ogg.");
                });
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The device emits a strange noise and falls off... Along with your arms!");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();

                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { user, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructRemoteDeadVictimDetonationMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid boomer = default;
        EntityUid victim = default;
        EntityUid listener = default;
        EntityUid missingBracerVictim = default;
        EntityUid boomerBracer = default;
        EntityUid victimBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;
        var expectedArea = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                listener = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                missingBracerVictim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                boomerBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                victimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                metadata.SetEntityName(boomer, "A'ke Ret");
                metadata.SetEntityName(victim, "Guan Thwei");
                entMan.EnsureComponent<YautjaComponent>(boomer);
                entMan.EnsureComponent<YautjaComponent>(victim);
                entMan.EnsureComponent<YautjaComponent>(listener);
                Assert.That(inventory.TryEquip(boomer, boomerBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, victimBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(victim, MobState.Dead);
                mobState.ChangeMobState(missingBracerVictim, MobState.Dead);
                expectedArea = areas.GetAreaName(boomer);

                server.PlayerMan.SetAttachedEntity(session, boomer);
                Assert.That(pulling.TryStartPull(boomer, missingBracerVictim), Is.True);
                Assert.That(selfDestruct.TryOpenRemoteDeadVictimSelfDestructDialog(
                    (boomerBracer, entMan.GetComponent<YautjaBracerComponent>(boomerBracer)),
                    boomer,
                    missingBracerVictim), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "<b>This Human does not have a bracer attached.</b>");

            // CMU14: the broadcast goes to every Yautja except the actor, and a popup sent while the listener has
            // no session is dropped. Attach the listener before the detonation so the broadcast is observed - a
            // lingering label from an earlier attach would otherwise make the check pass for the wrong reason.
            await server.WaitPost(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, listener);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var pulling = entMan.System<PullingSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var boomerComp = entMan.GetComponent<YautjaBracerComponent>(boomerBracer);

                pulling.TryStopPull(missingBracerVictim, entMan.GetComponent<PullableComponent>(missingBracerVictim), boomer);
                Assert.That(pulling.TryStartPull(boomer, victim), Is.True);
                Assert.That(selfDestruct.TryOpenRemoteDeadVictimSelfDestructDialog((boomerBracer, boomerComp), boomer, victim), Is.True);
                var dialog = entMan.GetComponent<DialogComponent>(boomerBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog.Title, Is.EqualTo("Explosive Bracers"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to send this Yautja into the great hunting grounds?"));
                    Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
                    Assert.That(dialog.CloseAt, Is.Not.Null);
                });

                RaiseDialogOption(entMan, boomerBracer, boomer, "Yes");
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "A'ke Ret has triggered Guan Thwei's bracer's self-destruction sequence.",
                // The actor's own confirmation is private to them, so the listener must not receive it.
                "You activate the timer. May Guan Thwei's final hunt be swift.");
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var victimComp = entMan.GetComponent<YautjaBracerComponent>(victimBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(victimComp.SelfDestructArmed, Is.True);
                    Assert.That(victimComp.User, Is.EqualTo(victim));
                });
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);
            Assert.That(
                messages.Any(message =>
                    message.Contains("triggered the predator self-destruct sequence of", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("Guan Thwei", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains($"in {expectedArea}", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 logs '[key_name(boomer)] triggered the predator self-destruct sequence of [victim] ([victim.key]) in [A.name]'.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[]
                         {
                             boomer,
                             victim,
                             listener,
                             missingBracerVictim,
                             boomerBracer,
                             victimBracer,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructRemoteDeadVictimRequiresSameBracerOnConfirm()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid boomer = default;
        EntityUid victim = default;
        EntityUid listener = default;
        EntityUid boomerBracer = default;
        EntityUid originalVictimBracer = default;
        EntityUid replacementVictimBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                listener = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                boomerBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                originalVictimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                replacementVictimBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                metadata.SetEntityName(boomer, "A'ke Ret");
                metadata.SetEntityName(victim, "Guan Thwei");
                entMan.EnsureComponent<YautjaComponent>(boomer);
                entMan.EnsureComponent<YautjaComponent>(victim);
                entMan.EnsureComponent<YautjaComponent>(listener);
                Assert.That(inventory.TryEquip(boomer, boomerBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, originalVictimBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(victim, MobState.Dead);
                Assert.That(pulling.TryStartPull(boomer, victim), Is.True);
                server.PlayerMan.SetAttachedEntity(session, boomer);

                Assert.That(selfDestruct.TryOpenRemoteDeadVictimSelfDestructDialog(
                    (boomerBracer, entMan.GetComponent<YautjaBracerComponent>(boomerBracer)),
                    boomer,
                    victim), Is.True);
                var dialog = entMan.GetComponent<DialogComponent>(boomerBracer);
                Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to send this Yautja into the great hunting grounds?"));

                Assert.That(inventory.TryUnequip(victim, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, replacementVictimBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, listener);
                RaiseDialogOption(entMan, boomerBracer, boomer, "Yes");
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var originalComp = entMan.GetComponent<YautjaBracerComponent>(originalVictimBracer);
                var replacementComp = entMan.GetComponent<YautjaBracerComponent>(replacementVictimBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(originalComp.SelfDestructArmed, Is.False,
                        "CMSS13 requires victim.gloves to still be the bracer captured before the confirmation prompt.");
                    Assert.That(replacementComp.SelfDestructArmed, Is.False,
                        "CMSS13 must not arm a replacement bracer equipped after the remote self-destruct prompt opens.");
                });
            });

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(labels, Does.Not.Contain("A'ke Ret has triggered Guan Thwei's bracer's self-destruction sequence."),
                    $"A stale CMSS13 remote self-destruct confirmation should not broadcast after victim.gloves changes.\nActual labels:\n{joinedLabels}");
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);
            Assert.That(
                messages,
                Has.None.Contains("triggered the predator self-destruct sequence of").IgnoreCase,
                $"A stale CMSS13 remote self-destruct confirmation should not write the remote detonation attack log.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[]
                         {
                             boomer,
                             victim,
                             listener,
                             boomerBracer,
                             originalVictimBracer,
                             replacementVictimBracer,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructGuardsThrallAndXenoHostLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid thrall = default;
        EntityUid thrallBracer = default;
        EntityUid infected = default;
        EntityUid infectedBracer = default;
        EntityUid cancellingInfected = default;
        EntityUid cancellingBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                thrallBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                infected = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                infectedBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                cancellingInfected = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                cancellingBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(thrall);
                entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                entMan.EnsureComponent<YautjaComponent>(infected);
                entMan.EnsureComponent<VictimInfectedComponent>(infected);
                entMan.EnsureComponent<YautjaComponent>(cancellingInfected);

                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(infected, infectedBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(cancellingInfected, cancellingBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, thrall);
                var thrallBracerComp = entMan.GetComponent<YautjaBracerComponent>(thrallBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (thrallBracer, thrallBracerComp),
                    thrall,
                    TimeSpan.FromSeconds(30)), Is.False);
                Assert.That(thrallBracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The device is preventing you access of this feature as it detects you being a thrall.",
                "You set the timer. May your journey to the great hunting grounds be swift.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, infected);
                var infectedBracerComp = entMan.GetComponent<YautjaBracerComponent>(infectedBracer);
                Assert.That(selfDestruct.TryOpenSelfDestructDialog((infectedBracer, infectedBracerComp), infected), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(infectedBracer), Is.False);
                Assert.That(infectedBracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "Strange...something seems to be interfering with your bracer functions...",
                "You set the timer. May your journey to the great hunting grounds be swift.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

                var infectedBracerComp = entMan.GetComponent<YautjaBracerComponent>(infectedBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (infectedBracer, infectedBracerComp),
                    infected,
                    TimeSpan.FromSeconds(30)), Is.False);
                Assert.That(infectedBracerComp.SelfDestructArmed, Is.False);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, cancellingInfected);
                var cancellingBracerComp = entMan.GetComponent<YautjaBracerComponent>(cancellingBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (cancellingBracer, cancellingBracerComp),
                    cancellingInfected,
                    TimeSpan.FromSeconds(30)), Is.True);
                entMan.EnsureComponent<VictimInfectedComponent>(cancellingInfected);
                Assert.That(selfDestruct.TryCancelSelfDestruct((cancellingBracer, cancellingBracerComp), cancellingInfected), Is.True);
                Assert.That(cancellingBracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            // CMU14: cancelling a self-destruct prints nothing.
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[]
                         {
                             thrall,
                             thrallBracer,
                             infected,
                             infectedBracer,
                             cancellingInfected,
                             cancellingBracer,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [TestCase("Pen")]
    [TestCase("CMUYautjaHarpoon")]
    [TestCase("CMUYautjaSmartDisc")]
    public async Task ThrowHeldItemDoesNotPassThroughHunterShipWall(string itemPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid wall = default;
        EntityUid item = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var serverHands = entMan.System<HandsSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();

            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            wall = entMan.SpawnEntity("CMUHunterShipWallTurfClosedWallHuntershipHunterBase", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            item = entMan.SpawnEntity(itemPrototype, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            Assert.That(entMan.GetComponent<TransformComponent>(wall).Anchored, Is.True,
                "The wall must spawn anchored; an unanchored wall is a Dynamic body thrown items can shove aside.");

            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(hands.TryPickupAnyHand(hunter, item), Is.True);

            var behindWall = map.GridCoords.Offset(new Vector2(3.5f, 0.5f));
            Assert.That(serverHands.ThrowHeldItem(hunter, behindWall), Is.True,
                "HandsSystem accepts a legal throw; hunter-ship walls stop it through hard thrown-item collision.");
            Assert.That(entMan.HasComponent<ThrownItemComponent>(item), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var itemPosition = transform.GetMapCoordinates(item).Position;
            var wallPosition = transform.GetMapCoordinates(wall).Position;

            Assert.That(itemPosition.X, Is.LessThan(wallPosition.X + 0.5f),
                "The thrown item must not finish on the far side of the hunter-ship wall.");

            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
            if (!entMan.Deleted(wall))
                entMan.DeleteEntity(wall);
            if (!entMan.Deleted(item))
                entMan.DeleteEntity(item);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntTeleporterUsesConfiguredDestinationId()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var teleporter = entMan.SpawnEntity(null, map.GridCoords);
            var jungle = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(10, 0)));
            var desert = entMan.SpawnEntity("CMUYautjaHuntDestinationDesertMoon", map.GridCoords.Offset(new Vector2(20, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "desert_moon";

                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var hunterCoordinates = transform.GetMapCoordinates(hunter);
                var desertCoordinates = transform.GetMapCoordinates(desert);
                Assert.That(hunterCoordinates.MapId, Is.EqualTo(desertCoordinates.MapId));
                Assert.That(hunterCoordinates.Position, Is.EqualTo(desertCoordinates.Position));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(jungle))
                    entMan.DeleteEntity(jungle);
                if (!entMan.Deleted(desert))
                    entMan.DeleteEntity(desert);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersCarryFullPullTrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var firstPulled = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var secondPulled = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var teleporter = entMan.SpawnEntity(null, map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(10, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "jungle_moon";

                Assert.That(pulling.TryStartPull(hunter, firstPulled), Is.True);
                Assert.That(pulling.TryStartPull(firstPulled, secondPulled), Is.True);

                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var targetCoordinates = transform.GetMapCoordinates(destination);
                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, targetCoordinates);
                    AssertTeleportedTo(entMan, transform, firstPulled, targetCoordinates);
                    AssertTeleportedTo(entMan, transform, secondPulled, targetCoordinates);
                    Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.EqualTo(firstPulled));
                    Assert.That(entMan.GetComponent<PullerComponent>(firstPulled).Pulling, Is.EqualTo(secondPulled));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(firstPulled))
                    entMan.DeleteEntity(firstPulled);
                if (!entMan.Deleted(secondPulled))
                    entMan.DeleteEntity(secondPulled);
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersBreakExternalLoopPullLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var firstPulled = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var secondPulled = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                Assert.That(pulling.TryStartPull(hunter, firstPulled), Is.True);
                Assert.That(pulling.TryStartPull(firstPulled, secondPulled), Is.True);
                Assert.That(pulling.TryStartPull(secondPulled, hunter), Is.True);

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, firstPulled, destination);
                    AssertTeleportedTo(entMan, transform, secondPulled, destination);
                    Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.EqualTo(firstPulled));
                    Assert.That(entMan.GetComponent<PullerComponent>(firstPulled).Pulling, Is.EqualTo(secondPulled));
                    Assert.That(entMan.GetComponent<PullerComponent>(secondPulled).Pulling, Is.Null,
                        "CMSS13 trainteleport() breaks the leader's external pulledby link before walking the train, so a loop back to the leader is not restored.");
                    Assert.That(entMan.GetComponent<PullableComponent>(hunter).Puller, Is.Null);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(firstPulled))
                    entMan.DeleteEntity(firstPulled);
                if (!entMan.Deleted(secondPulled))
                    entMan.DeleteEntity(secondPulled);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersCarryUnanchoredBuckledUserStrapLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chair = entMan.SpawnEntity("CMUHunterShipPlacedCMChairNonFoldChairSouthOffset0x7", map.GridCoords);
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                Assert.That(buckle.TryBuckle(hunter, hunter, chair, popup: false), Is.True);
                Assert.That(entMan.GetComponent<BuckleComponent>(hunter).BuckledTo, Is.EqualTo(chair));

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, chair, destination);
                    Assert.That(entMan.GetComponent<BuckleComponent>(hunter).BuckledTo, Is.EqualTo(chair),
                        "CMSS13 trainteleport() adds an unanchored buckled object to the conga line instead of leaving the rider behind.");
                    Assert.That(transform.GetMapCoordinates(hunter).MapId, Is.EqualTo(destination.MapId));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(chair))
                    entMan.DeleteEntity(chair);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersUnbuckleLeaderFromAnchoredStrapLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chair = entMan.SpawnEntity("CMUHunterShipPlacedCMChairNonFoldChairSouthOffset0x7", map.GridCoords);
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                transform.AnchorEntity(chair);
                var chairStart = transform.GetMapCoordinates(chair);

                Assert.That(entMan.GetComponent<TransformComponent>(chair).Anchored, Is.True);
                Assert.That(buckle.TryBuckle(hunter, hunter, chair, popup: false), Is.True);
                Assert.That(entMan.GetComponent<BuckleComponent>(hunter).BuckledTo, Is.EqualTo(chair));

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, chair, chairStart);
                    Assert.That(entMan.GetComponent<BuckleComponent>(hunter).BuckledTo, Is.Null,
                        "CMSS13 trainteleport() calls buckled.unbuckle() when the leader starts on an anchored buckled object.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(chair))
                    entMan.DeleteEntity(chair);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersCarryPulledBuckledPassengerAndBreakPassengerPullLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var wheelchair = entMan.SpawnEntity("RMCWheelchair", map.GridCoords.Offset(new Vector2(1, 0)));
            var passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trailer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                Assert.That(pulling.TryStartPull(passenger, trailer), Is.True);
                Assert.That(buckle.TryBuckle(passenger, passenger, wheelchair, popup: false), Is.True);
                Assert.That(pulling.TryStartPull(hunter, wheelchair), Is.True);
                Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(wheelchair));
                Assert.That(entMan.GetComponent<PullerComponent>(passenger).Pulling, Is.EqualTo(trailer),
                    "The test must enter TeleportTrain() with the CMSS13 source state: a pulled object has a buckled mob that is pulling something else.");

                var trailerStart = transform.GetMapCoordinates(trailer);

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, wheelchair, destination);
                    Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(wheelchair));
                    Assert.That(transform.GetMapCoordinates(passenger).MapId, Is.EqualTo(destination.MapId));
                    Assert.That(transform.GetMapCoordinates(trailer).Position, Is.EqualTo(trailerStart.Position),
                        "CMSS13 trainteleport() stops a buckled passenger's own pulling chain when the pulled object is carrying them.");
                    Assert.That(entMan.GetComponent<PullerComponent>(passenger).Pulling, Is.Null,
                        "CMSS13 calls buckled_mob.stop_pulling() for pulled objects with buckled mobs; wheelchair-style trains do not continue.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(wheelchair))
                    entMan.DeleteEntity(wheelchair);
                if (!entMan.Deleted(passenger))
                    entMan.DeleteEntity(passenger);
                if (!entMan.Deleted(trailer))
                    entMan.DeleteEntity(trailer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersStopBeforePulledMobBuckledToAnchoredStrapLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var chair = entMan.SpawnEntity("CMUHunterShipPlacedCMChairNonFoldChairSouthOffset0x7", map.GridCoords.Offset(new Vector2(1, 0)));
            var trailer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                ForceAnchor(entMan, transform, chair);
                Assert.That(entMan.GetComponent<TransformComponent>(chair).Anchored, Is.True);

                var passengerStart = transform.GetMapCoordinates(passenger);
                var chairStart = transform.GetMapCoordinates(chair);
                var trailerStart = transform.GetMapCoordinates(trailer);

                Assert.That(buckle.TryBuckle(passenger, passenger, chair, popup: false), Is.True);
                ForcePullLink(entMan, passenger, trailer);
                ForcePullLink(entMan, hunter, passenger);
                Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(chair),
                    "The test must enter TeleportTrain() with the CMSS13 source state: the pulled mob is still buckled to an anchored object.");

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, chair, chairStart);
                    Assert.That(transform.GetMapCoordinates(passenger).MapId, Is.EqualTo(passengerStart.MapId));
                    Assert.That(transform.GetMapCoordinates(passenger).Position, Is.Not.EqualTo(destination.Position));
                    Assert.That(transform.GetMapCoordinates(trailer).MapId, Is.EqualTo(trailerStart.MapId));
                    Assert.That(transform.GetMapCoordinates(trailer).Position, Is.Not.EqualTo(destination.Position));
                    Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(chair),
                        "CMSS13 trainteleport() removes a pulled mob from the conga line when their buckled object is anchored.");
                    Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.Null,
                        "The local train should not keep pulling an anchored, unmoved blocker across maps after the source branch stops before it.");
                    Assert.That(entMan.GetComponent<PullerComponent>(passenger).Pulling, Is.EqualTo(trailer),
                        "CMSS13 stops the train before a pulled mob buckled to an anchored object, but does not call that mob's stop_pulling().");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(passenger))
                    entMan.DeleteEntity(passenger);
                if (!entMan.Deleted(chair))
                    entMan.DeleteEntity(chair);
                if (!entMan.Deleted(trailer))
                    entMan.DeleteEntity(trailer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersCarryPulledMobUnanchoredStrapAndContinueTrainLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var wheelchair = entMan.SpawnEntity("RMCWheelchair", map.GridCoords.Offset(new Vector2(1, 0)));
            var trailer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                Assert.That(buckle.TryBuckle(passenger, passenger, wheelchair, popup: false), Is.True);
                ForcePullLink(entMan, passenger, trailer);
                ForcePullLink(entMan, hunter, passenger);
                Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(wheelchair),
                    "The test must enter TeleportTrain() with the CMSS13 source state: the pulled mob is buckled to an unanchored object and still pulling the next train member.");

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    Assert.That(transform.GetMapCoordinates(passenger).MapId, Is.EqualTo(destination.MapId));
                    AssertTeleportedTo(entMan, transform, wheelchair, destination);
                    AssertTeleportedTo(entMan, transform, trailer, destination);
                    Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(wheelchair),
                        "CMSS13 trainteleport() adds the pulled mob's unanchored buckled object to the conga line.");
                    Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.EqualTo(passenger));
                    Assert.That(entMan.GetComponent<PullerComponent>(passenger).Pulling, Is.EqualTo(trailer),
                        "A pulled mob buckled to an unanchored object can keep extending the CMSS13 conga line.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(passenger))
                    entMan.DeleteEntity(passenger);
                if (!entMan.Deleted(wheelchair))
                    entMan.DeleteEntity(wheelchair);
                if (!entMan.Deleted(trailer))
                    entMan.DeleteEntity(trailer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaTeleportersStopAtAnchoredPulledEntityLikeCmss13Trainteleport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();
            var teleport = entMan.System<YautjaTeleportSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chair = entMan.SpawnEntity("RMCWheelchair", map.GridCoords.Offset(new Vector2(1, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                ForceAnchor(entMan, transform, chair);
                Assert.That(entMan.GetComponent<TransformComponent>(chair).Anchored, Is.True);

                var chairStart = transform.GetMapCoordinates(chair);

                ForcePullLink(entMan, hunter, chair);

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, chair, chairStart);
                    Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.Null,
                        "The local train should not preserve a stale pull link to an anchored blocker after teleporting the leader.");
                    Assert.That(entMan.GetComponent<PullableComponent>(chair).Puller, Is.Null);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(chair))
                    entMan.DeleteEntity(chair);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LoadedJungleMoonAcceptsHuntsmasterAndBloodingCalls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var youngbloodSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(flightConsole, new InteractHandEvent(hunter, flightConsole));
                RaiseDialogOption(entMan, flightConsole, hunter, "Jungle Moon");

                Assert.That(entMan.EntityQuery<YautjaHuntSpawnPointComponent>().Any(point => point.Kind == YautjaHuntSpawnKind.Prey), Is.True);
                Assert.That(entMan.EntityQuery<YautjaHuntTeleportDestinationComponent>().Any(destination => destination.Kind == YautjaHuntTeleporterKind.Young), Is.True);

                var initialGhostRoles = entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count();
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new InteractHandEvent(hunter, huntsmasterConsole));
                var huntDialog = entMan.GetComponent<DialogComponent>(huntsmasterConsole);
                var mixedSmall = huntDialog.Options
                    .Select(option => option.Event)
                    .OfType<YautjaHuntCallSelectedEvent>()
                    .Single(option => option.Id == "mixed_small");
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, mixedSmall);
                Assert.That(entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count(), Is.EqualTo(initialGhostRoles + 4));

                var preyRoles = new List<(EntityUid Uid, GhostRoleComponent Role)>();
                var preyQuery = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent>();
                while (preyQuery.MoveNext(out var preyUid, out var preyRole, out _))
                {
                    if (preyRole.RaffleConfig?.SettingsOverride != null)
                        preyRoles.Add((preyUid, preyRole));
                }

                Assert.That(preyRoles, Has.Count.EqualTo(4));
                Assert.That(preyRoles.All(entry =>
                    entry.Role.RaffleConfig?.SettingsOverride is { InitialDuration: 30, JoinExtendsDurationBy: 10, MaxDuration: 90 }), Is.True);

                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new InteractHandEvent(hunter, bloodingConsole));
                Assert.That(entMan.GetComponent<DialogComponent>(bloodingConsole).Title,
                    Is.EqualTo(Loc.GetString("cmu-yautja-hunt-console-blooding-title")));

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var option = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo");
                var huntConsole = entMan.System<YautjaHuntConsoleSystem>();
                var initialYoungbloodRoles = CountYoungbloodGhostRoles(entMan);
                Assert.That(huntConsole.TryCreateYoungbloodCall((bloodingConsole, blooding), hunter, option, bypassEligibility: true), Is.True);
                Assert.That(CountYoungbloodGhostRoles(entMan), Is.EqualTo(initialYoungbloodRoles + 1));

                var query = entMan.EntityQueryEnumerator<YautjaYoungbloodGhostRoleComponent>();
                var foundBypass = false;
                while (query.MoveNext(out _, out var youngblood))
                    foundBypass |= youngblood.BypassEligibility;

                Assert.That(foundBypass, Is.True);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(flightConsole))
                    entMan.DeleteEntity(flightConsole);
                if (!entMan.Deleted(huntsmasterConsole))
                    entMan.DeleteEntity(huntsmasterConsole);
                if (!entMan.Deleted(bloodingConsole))
                    entMan.DeleteEntity(bloodingConsole);
                if (!entMan.Deleted(youngbloodSpawn))
                    entMan.DeleteEntity(youngbloodSpawn);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterAndBloodingRepeatedSelectionsDoNotSpawnDuplicateRoles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var huntDestination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));
            var youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));
            var youngbloodSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var huntsmaster = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
                var huntOption = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_small");
                var huntEvent = new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), huntOption.Id);
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, huntEvent);

                var huntCoordinates = transform.GetMapCoordinates(huntDestination);
                Assert.That(CountGhostRolesAt(entMan, transform, huntCoordinates), Is.EqualTo(4));

                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, huntEvent);
                Assert.That(
                    CountGhostRolesAt(entMan, transform, huntCoordinates),
                    Is.EqualTo(4),
                    "CMSS13 starts the Huntsmaster global cooldown after a chosen call; repeated selections must not spawn another prey wave.");

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var bloodingOption = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo");
                var bloodingEvent = new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), bloodingOption.Id);
                entMan.EventBus.RaiseLocalEvent(bloodingConsole, bloodingEvent);
                Assert.That(CountYoungbloodGhostRoles(entMan), Is.EqualTo(1));

                entMan.EventBus.RaiseLocalEvent(bloodingConsole, bloodingEvent);
                Assert.That(
                    CountYoungbloodGhostRoles(entMan),
                    Is.EqualTo(1),
                    "CMSS13 starts the Blooding global cooldown after a chosen call; repeated selections must not spawn another youngblood wave.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, huntsmasterConsole, bloodingConsole, huntDestination, youngDestination, youngbloodSpawn })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FlightConsoleClientDialogSelectionReachesServer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        var jungle = -1;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var dialog = server.EntMan.GetComponent<DialogComponent>(console);
                jungle = dialog.Options
                    .Select((option, index) => (option, index))
                    .Single(pair => pair.option.Text == "Jungle Moon")
                    .index;
            });

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(jungle)
                {
                    Actor = hunter,
                    UiKey = DialogUiKey.Key,
                });
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var consoleComp = server.EntMan.GetComponent<YautjaHuntConsoleComponent>(console);
                Assert.That(consoleComp.DestinationId, Is.EqualTo("jungle_moon"));
                Assert.That(server.EntMan.EntityQuery<YautjaHuntSpawnPointComponent>()
                    .Any(point => point.Kind == YautjaHuntSpawnKind.Prey), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (console != default && !entMan.Deleted(console))
                    entMan.DeleteEntity(console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntConsoleOpensCmss13HuntOptionsAndStartsSelectedCall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(console).HuntCallOptions, Is.Not.Empty);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.Title, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-console-hunt-ground-title")));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain(Loc.GetString("cmu-yautja-hunt-call-mixed-small")));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain(Loc.GetString("cmu-yautja-hunt-call-serpents-small")));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain(Loc.GetString("cmu-yautja-hunt-call-elite-mixed-larger")));

                var mixedSmall = (YautjaHuntCallSelectedEvent) dialog.Options
                    .Single(option => option.Event is YautjaHuntCallSelectedEvent selected && selected.Id == "mixed_small")
                    .Event!;
                entMan.EventBus.RaiseLocalEvent(console, mixedSmall);

                var destinationCoordinates = transform.GetMapCoordinates(destination);
                Assert.That(CountGhostRolesAt(entMan, transform, destinationCoordinates), Is.EqualTo(4));

                entMan.EventBus.RaiseLocalEvent(console, mixedSmall);
                Assert.That(CountGhostRolesAt(entMan, transform, destinationCoordinates), Is.EqualTo(4));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(console))
                    entMan.DeleteEntity(console);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertTeleportedTo(IEntityManager entMan, SharedTransformSystem transform, EntityUid entity, MapCoordinates expected)
    {
        var actual = transform.GetMapCoordinates(entity);
        Assert.That(actual.MapId, Is.EqualTo(expected.MapId), $"{entMan.ToPrettyString(entity)}");
        Assert.That(actual.Position, Is.EqualTo(expected.Position), $"{entMan.ToPrettyString(entity)}");
    }

    private static void ForcePullLink(IEntityManager entMan, EntityUid puller, EntityUid pulled)
    {
        var pullerComp = entMan.EnsureComponent<PullerComponent>(puller);
        var pullableComp = entMan.EnsureComponent<PullableComponent>(pulled);
        typeof(PullerComponent)
            .GetField(nameof(PullerComponent.Pulling))!
            .SetValue(pullerComp, pulled);
        typeof(PullableComponent)
            .GetField(nameof(PullableComponent.Puller))!
            .SetValue(pullableComp, puller);
        entMan.Dirty(puller, pullerComp);
        entMan.Dirty(pulled, pullableComp);
    }

    private static void ForceAnchor(IEntityManager entMan, SharedTransformSystem transform, EntityUid entity)
    {
        var xform = entMan.GetComponent<TransformComponent>(entity);
        transform.AnchorEntity(entity, xform);

        if (!xform.Anchored)
        {
            typeof(TransformComponent)
                .GetField("_anchored", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(xform, true);
        }

        entMan.Dirty(entity, xform);
    }

    private static int CountGhostRolesAt(IEntityManager entMan, SharedTransformSystem transform, MapCoordinates expected)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            var actual = transform.GetMapCoordinates(uid);
            if (actual.MapId == expected.MapId && actual.Position == expected.Position)
                count++;
        }

        return count;
    }

    private static void RaiseDialogOption(IEntityManager entMan, EntityUid console, EntityUid hunter, string optionText)
    {
        var dialog = entMan.GetComponent<DialogComponent>(console);
        var optionIndex = dialog.Options
            .Select((option, index) => (option, index))
            .Single(pair => pair.option.Text == optionText)
            .index;

        entMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(optionIndex)
        {
            Actor = hunter,
            UiKey = DialogUiKey.Key,
        });
    }

    private static int CountYoungbloodGhostRoles(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodComponent>();
        while (query.MoveNext(out _, out _, out _, out _))
        {
            count++;
        }

        return count;
    }

    [Test]
    public async Task BloodingConsoleOpensYoungbloodOptionsAndSpawnsSelectedGroup()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            // CMU14: the boarding point must be a youngblood hunt spawn marker - the shared predator spawn marker no
            // longer carries YautjaHuntSpawnPoint, since it is a job spawn rather than a blooding boarding point. The
            // component is attached here the same way the HunterShip map overrides attach it.
            var shipSpawn = entMan.SpawnEntity("CMUYautjaYoungbloodSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            var shipSpawnPoint = entMan.EnsureComponent<YautjaHuntSpawnPointComponent>(shipSpawn);
            shipSpawnPoint.Kind = YautjaHuntSpawnKind.Youngblood;
            shipSpawnPoint.DestinationId = "jungle_moon";
            var huntDestination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));
            var youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(console).BloodingCallOptions, Is.Not.Empty);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.Title, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-console-blooding-title")));
                var soloText = Loc.GetString("cmu-yautja-blooding-call-solo");
                var packText = Loc.GetString("cmu-yautja-blooding-call-pack");
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain(soloText));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain(packText));

                var solo = (YautjaHuntCallSelectedEvent) dialog.Options
                    .Single(option => option.Text == soloText)
                    .Event!;
                entMan.EventBus.RaiseLocalEvent(console, solo);

                var shipSpawnCoordinates = transform.GetMapCoordinates(shipSpawn);
                var youngDestinationCoordinates = transform.GetMapCoordinates(youngDestination);
                var huntDestinationCoordinates = transform.GetMapCoordinates(huntDestination);
                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodComponent, MetaDataComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out _, out var meta), Is.True);
                Assert.That(meta.EntityPrototype?.ID, Is.EqualTo("CMUMobYautjaYoungblood"));
                Assert.That(ghostRole.RoleName, Is.EqualTo(Loc.GetString("cmu-yautja-youngblood-ghost-name")));

                var youngbloodCoordinates = transform.GetMapCoordinates(youngblood);
                Assert.That(youngbloodCoordinates.MapId, Is.EqualTo(shipSpawnCoordinates.MapId));
                Assert.That(youngbloodCoordinates.Position, Is.EqualTo(shipSpawnCoordinates.Position));
                Assert.That(youngbloodCoordinates.Position, Is.Not.EqualTo(youngDestinationCoordinates.Position));
                Assert.That(youngbloodCoordinates.Position, Is.Not.EqualTo(huntDestinationCoordinates.Position));

                entMan.EventBus.RaiseLocalEvent(console, solo);
                Assert.That(CountYoungbloodGhostRoles(entMan), Is.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(console))
                    entMan.DeleteEntity(console);
                if (!entMan.Deleted(shipSpawn))
                    entMan.DeleteEntity(shipSpawn);
                if (!entMan.Deleted(huntDestination))
                    entMan.DeleteEntity(huntDestination);
                if (!entMan.Deleted(youngDestination))
                    entMan.DeleteEntity(youngDestination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterAndBloodingRafflesUseIndependentCandidatesAndTimers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid huntDestination = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid huntPrey = default;
        EntityUid youngblood = default;
        ICommonSession? session = null;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitAssertion(() =>
            {
                server.CfgMan.SetCVar(CCVars.GhostQuickLottery, true);

                var entMan = server.EntMan;
                var ghostRoles = entMan.System<GhostRoleSystem>();
                session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                huntDestination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));
                shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
                youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var huntsmaster = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
                var mixedSmall = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_small");
                entMan.EventBus.RaiseLocalEvent(
                    huntsmasterConsole,
                    new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), mixedSmall.Id));

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(
                    bloodingConsole,
                    new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var preyQuery = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaHuntPreyComponent>();
                Assert.That(preyQuery.MoveNext(out huntPrey, out var preyRole, out _, out _), Is.True);

                var youngbloodQuery = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(youngbloodQuery.MoveNext(out youngblood, out var youngbloodRole, out _, out var metadata), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(huntPrey, Is.Not.EqualTo(youngblood));
                    Assert.That(preyRole.RaffleConfig, Is.Not.Null);
                    Assert.That(youngbloodRole.JobProto, Is.EqualTo("CMUYautjaYoungblood"));
                    Assert.That(youngbloodRole.ReregisterOnGhost, Is.False);
                    Assert.That(youngbloodRole.RaffleConfig, Is.Not.Null);
                    Assert.That(metadata.CallId, Is.EqualTo("youngblood_solo_experienced"));
                    Assert.That(metadata.BypassEligibility, Is.False);
                });

                var preySettings = preyRole.RaffleConfig!.SettingsOverride;
                var youngbloodSettings = youngbloodRole.RaffleConfig!.SettingsOverride;
                Assert.That(preySettings, Is.Not.Null);
                Assert.That(youngbloodSettings, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(preySettings!.InitialDuration, Is.EqualTo(30));
                    Assert.That(preySettings.JoinExtendsDurationBy, Is.EqualTo(10));
                    Assert.That(preySettings.MaxDuration, Is.EqualTo(90));
                    Assert.That(youngbloodSettings!.InitialDuration, Is.EqualTo(30));
                    Assert.That(youngbloodSettings.JoinExtendsDurationBy, Is.EqualTo(10));
                    Assert.That(youngbloodSettings.MaxDuration, Is.EqualTo(90));
                });

                server.PlayerMan.SetAttachedEntity(session, null);
                ghostRoles.Request(session, preyRole.Identifier);

                Assert.That(entMan.TryGetComponent(huntPrey, out GhostRoleRaffleComponent? preyRaffle), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(preyRaffle!.CurrentMembers, Does.Contain(session));
                    Assert.That(preyRaffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(1)));
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False,
                        "Joining a Huntsmaster prey raffle must not start the Blooding candidate timer.");
                    Assert.That(youngbloodRole.Taken, Is.False);
                });
            });

            await pair.RunSeconds(1.25f);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var preyRole = entMan.GetComponent<GhostRoleComponent>(huntPrey);
                var youngbloodRole = entMan.GetComponent<GhostRoleComponent>(youngblood);

                Assert.Multiple(() =>
                {
                    Assert.That(session!.AttachedEntity, Is.EqualTo(huntPrey));
                    Assert.That(preyRole.Taken, Is.True);
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(huntPrey), Is.False);
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);
                    Assert.That(youngbloodRole.Taken, Is.False);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(CCVars.GhostQuickLottery, CCVars.GhostQuickLottery.DefaultValue);
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);

                var entMan = server.EntMan;
                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
                foreach (var uid in new[]
                         {
                             hunter,
                             huntsmasterConsole,
                             bloodingConsole,
                             huntDestination,
                             shipSpawn,
                             youngDestination,
                             huntPrey,
                             youngblood,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingCandidateCanDeclineYoungbloodRaffle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid? previousAttached = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ghostRoles = entMan.System<GhostRoleSystem>();
            var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(console, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out var metadata), Is.True);
                Assert.That(metadata.BypassEligibility, Is.False);

                playtime.AddTimeToTracker(session, "CMJobRifleman", TimeSpan.FromHours(5));
                playtime.AddTimeToTracker(session, "CMJobSelectableXeno", TimeSpan.FromHours(5));

                server.PlayerMan.SetAttachedEntity(session, null);
                ghostRoles.Request(session, ghostRole.Identifier);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.That(raffle!.CurrentMembers, Does.Contain(session));

                ghostRoles.LeaveRaffle(session, ghostRole.Identifier);

                Assert.Multiple(() =>
                {
                    Assert.That(raffle.CurrentMembers, Does.Not.Contain(session));
                    Assert.That(raffle.CurrentMembers, Is.Empty);
                    Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(youngblood), Is.True);
                    Assert.That(ghostRole.Taken, Is.False);
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, console, shipSpawn, youngDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingCandidateTimeoutTakesYoungbloodRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid? previousAttached = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ghostRoles = entMan.System<GhostRoleSystem>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                var huntConsole = entMan.System<YautjaHuntConsoleSystem>();
                Assert.That(huntConsole.TryCreateYoungbloodCall((console, blooding), hunter, solo, bypassEligibility: true), Is.True);

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out var metadata), Is.True);

                server.PlayerMan.SetAttachedEntity(session, null);
                var info = ghostRoles.GetGhostRolesInfo(session)
                    .Single(entry => entry.Identifier == ghostRole.Identifier);
                Assert.That(info.Kind, Is.EqualTo(GhostRoleKind.RaffleReady));
                ghostRoles.Request(session, ghostRole.Identifier);

                Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.True);

                ghostRoles.Update(31f);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);
                    Assert.That(session.AttachedEntity, Is.EqualTo(youngblood));
                    Assert.That(ghostRole.Taken, Is.True);
                    Assert.That(metadata.SetupComplete, Is.True);
                    Assert.That(ghostRole.ReregisterOnGhost, Is.False);
                    Assert.That(ghostRoles.GetGhostRolesInfo(null), Has.None.Matches<GhostRoleInfo>(
                        entry => entry.Identifier == ghostRole.Identifier));
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, console, shipSpawn, youngDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingDisconnectedCandidateLeavesYoungbloodRaffle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid? previousAttached = null;
        SessionStatus? previousStatus = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ghostRoles = entMan.System<GhostRoleSystem>();
            var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;
            previousStatus = session.Status;

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(console, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out var metadata), Is.True);

                playtime.AddTimeToTracker(session, "CMJobRifleman", TimeSpan.FromHours(5));
                playtime.AddTimeToTracker(session, "CMJobSelectableXeno", TimeSpan.FromHours(5));

                server.PlayerMan.SetAttachedEntity(session, null);
                ghostRoles.Request(session, ghostRole.Identifier);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.That(raffle!.CurrentMembers, Does.Contain(session));

                server.PlayerMan.SetStatus(session, SessionStatus.Disconnected);
                ghostRoles.Update(0.1f);

                Assert.Multiple(() =>
                {
                    Assert.That(raffle.CurrentMembers, Does.Not.Contain(session));
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);
                    Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(youngblood), Is.True);
                    Assert.That(session.AttachedEntity, Is.Null);
                    Assert.That(ghostRole.Taken, Is.False);
                    Assert.That(metadata.SetupComplete, Is.False);
                });
            }
            finally
            {
                if (previousStatus is { } status && session.Status != status)
                    server.PlayerMan.SetStatus(session, status);

                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, console, shipSpawn, youngDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingDeadCandidateLeavesYoungbloodRaffleWithoutTakingRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid deadCandidate = default;
        EntityUid? previousAttached = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ghostRoles = entMan.System<GhostRoleSystem>();
            var mobState = entMan.System<MobStateSystem>();
            var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));
            deadCandidate = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(20, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(console, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out var metadata), Is.True);

                playtime.AddTimeToTracker(session, "CMJobRifleman", TimeSpan.FromHours(5));
                playtime.AddTimeToTracker(session, "CMJobSelectableXeno", TimeSpan.FromHours(5));

                server.PlayerMan.SetAttachedEntity(session, null);
                ghostRoles.Request(session, ghostRole.Identifier);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.That(raffle!.CurrentMembers, Does.Contain(session));

                mobState.ChangeMobState(deadCandidate, MobState.Dead);
                Assert.That(mobState.IsDead(deadCandidate), Is.True);
                server.PlayerMan.SetAttachedEntity(session, deadCandidate);

                ghostRoles.Update(0.1f);

                Assert.Multiple(() =>
                {
                    Assert.That(raffle.CurrentMembers, Does.Not.Contain(session));
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);
                    Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(youngblood), Is.True);
                    Assert.That(session.AttachedEntity, Is.EqualTo(deadCandidate));
                    Assert.That(ghostRole.Taken, Is.False);
                    Assert.That(metadata.SetupComplete, Is.False);
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, console, shipSpawn, youngDestination, deadCandidate })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingReconnectedCandidateCanRejoinAndTakeYoungbloodRaffle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid shipSpawn = default;
        EntityUid youngDestination = default;
        EntityUid youngblood = default;
        uint ghostRoleIdentifier = default;
        EntityUid? previousAttached = null;
        SessionStatus? previousStatus = null;
        ICommonSession? session = null;
        Task? reconnectLoad = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ghostRoles = entMan.System<GhostRoleSystem>();
                var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
                session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousStatus = session.Status;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
                youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(console, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var spawnedYoungblood, out var ghostRole, out _, out var metadata), Is.True);
                youngblood = spawnedYoungblood;
                ghostRoleIdentifier = ghostRole.Identifier;

                playtime.AddTimeToTracker(session!, "CMJobRifleman", TimeSpan.FromHours(5));
                playtime.AddTimeToTracker(session!, "CMJobSelectableXeno", TimeSpan.FromHours(5));

                server.PlayerMan.SetAttachedEntity(session!, null);
                ghostRoles.Request(session!, ghostRole.Identifier);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.That(raffle!.CurrentMembers, Does.Contain(session));

                server.PlayerMan.SetStatus(session!, SessionStatus.Disconnected);
                ghostRoles.Update(0.1f);

                Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);

                server.PlayerMan.SetStatus(session!, SessionStatus.InGame);
                reconnectLoad = server.ResolveDependency<UserDbDataManager>().GetLoadTask(session!);
            });

            Assert.That(reconnectLoad, Is.Not.Null);
            await reconnectLoad!;

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ghostRoles = entMan.System<GhostRoleSystem>();
                var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
                Assert.That(session, Is.Not.Null);

                playtime.AddTimeToTracker(session!, "CMJobRifleman", TimeSpan.FromHours(5));
                playtime.AddTimeToTracker(session!, "CMJobSelectableXeno", TimeSpan.FromHours(5));

                server.PlayerMan.SetAttachedEntity(session!, null);
                ghostRoles.Request(session!, ghostRoleIdentifier);

                var ghostRole = entMan.GetComponent<GhostRoleComponent>(youngblood);
                var metadata = entMan.GetComponent<YautjaYoungbloodGhostRoleComponent>(youngblood);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(raffle!.CurrentMembers, Does.Contain(session));
                    Assert.That(raffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(ghostRole.Taken, Is.False);
                    Assert.That(metadata.SetupComplete, Is.False);
                });

                ghostRoles.Update(31f);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(youngblood), Is.False);
                    Assert.That(session.AttachedEntity, Is.EqualTo(youngblood));
                    Assert.That(ghostRole.Taken, Is.True);
                    Assert.That(metadata.SetupComplete, Is.True);
                    Assert.That(ghostRole.ReregisterOnGhost, Is.False);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                if (session != null)
                {
                    if (previousStatus is { } status && session.Status != status)
                        server.PlayerMan.SetStatus(session, status);

                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
                }

                foreach (var uid in new[] { hunter, console, shipSpawn, youngDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreserveEscapeConsoleOpensAndClosesPreserveShutters()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var console = entMan.SpawnEntity(null, map.GridCoords);
            var shutter = entMan.SpawnEntity("CMUHunterShipObjStructureMachineryDoorPoddoorHybrisaOpenShuttersAlmayerPdoor1EastId1", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var consoleComp = entMan.EnsureComponent<YautjaHuntEscapeConsoleComponent>(console);
                entMan.EnsureComponent<YautjaPreserveShutterComponent>(shutter);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.Title, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(console).EntityName));
                var open = dialog.Options
                    .Select(option => option.Event)
                    .OfType<YautjaHuntEscapeActionSelectedEvent>()
                    .Single(ev => ev.Action == YautjaHuntEscapeAction.Open);
                entMan.EventBus.RaiseLocalEvent(console, open);

                Assert.That(consoleComp.Opened, Is.True);
                Assert.That(entMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Closed));

                var close = new YautjaHuntEscapeActionSelectedEvent(open.User, YautjaHuntEscapeAction.Close);
                entMan.EventBus.RaiseLocalEvent(console, close);

                Assert.That(consoleComp.Opened, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(console))
                    entMan.DeleteEntity(console);
                if (!entMan.Deleted(shutter))
                    entMan.DeleteEntity(shutter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneRelaysControllerMovementInput()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                drone = eye.Target!.Value;

                var controller = entMan.System<MoverController>();
                var input = entMan.GetComponent<InputMoverComponent>(hunter);
                var handleDir = typeof(SharedMoverController).GetMethod(
                    "HandleDirChange",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(handleDir, Is.Not.Null);
                handleDir!.Invoke(controller, new object[]
                {
                    new Entity<InputMoverComponent>(hunter, input),
                    Direction.East,
                    (ushort) 0,
                    true,
                });

                var droneInput = entMan.GetComponent<InputMoverComponent>(drone);
                Assert.That((droneInput.HeldMoveButtons & MoveButtons.Right) != 0, Is.True);
            });

            await pair.RunTicksSync(20);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var coordinates = transform.GetMapCoordinates(drone);
                var origin = transform.GetMapCoordinates(hunter);
                Assert.That(coordinates.Position.X, Is.GreaterThan(origin.Position.X + 0.05f));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (falcon != default && !entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
                if (drone != default && !entMan.Deleted(drone))
                    entMan.DeleteEntity(drone);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneReturnPrefersLeftEarThenRightEarThenHandsLikeCmss13Destroy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var spawned = new List<EntityUid>();

            void AssertReturn(string expectedSlot, bool blockLeft, bool blockRight)
            {
                var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
                spawned.AddRange(new[] { hunter, bracer, falcon });
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                if (blockLeft)
                {
                    var blocker = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
                    spawned.Add(blocker);
                    Assert.That(inventory.TryEquip(hunter, blocker, "ears", silent: true, force: true), Is.True);
                }

                if (blockRight)
                {
                    var blocker = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
                    spawned.Add(blocker);
                    Assert.That(inventory.TryEquip(hunter, blocker, "ears2", silent: true, force: true), Is.True);
                }

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
                var deployed = entMan.GetComponent<EyeComponent>(hunter).Target;
                Assert.That(deployed, Is.Not.Null);
                entMan.DeleteEntity(deployed!.Value);

                if (expectedSlot == "hands")
                {
                    Assert.That(hands.IsHolding(hunter, falcon), Is.True,
                        "CMSS13 /mob/hologram/falcon/Destroy() puts the parent drone in hands when both ears are occupied.");
                }
                else
                {
                    Assert.That(inventory.TryGetSlotEntity(hunter, expectedSlot, out var returned), Is.True);
                    Assert.That(returned, Is.EqualTo(falcon));
                }
            }

            try
            {
                AssertReturn("ears", blockLeft: false, blockRight: false);
                AssertReturn("ears2", blockLeft: true, blockRight: false);
                AssertReturn("hands", blockLeft: true, blockRight: true);
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
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
    public async Task FalconDroneSecondDeployCleansUpOldDrone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var firstFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
            var secondFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(firstFalcon, new UseInHandEvent(hunter));
                var eye = entMan.GetComponent<EyeComponent>(hunter);
                var firstDrone = eye.Target!.Value;

                entMan.EventBus.RaiseLocalEvent(secondFalcon, new UseInHandEvent(hunter));
                var secondDrone = eye.Target!.Value;

                Assert.That(secondDrone, Is.Not.EqualTo(firstDrone));
                Assert.That(entMan.IsQueuedForDeletion(firstDrone) || entMan.Deleted(firstDrone), Is.True);
                Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(secondDrone), Is.True);
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(firstFalcon))
                    entMan.DeleteEntity(firstFalcon);
                if (!entMan.Deleted(secondFalcon))
                    entMan.DeleteEntity(secondFalcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconRecallActionRestoresControllerEyeAndDeletesDrone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                var drone = eye.Target!.Value;
                var controller = entMan.GetComponent<YautjaFalconControllerComponent>(hunter);
                Assert.That(controller.RecallAction, Is.Not.Null);

                var action = controller.RecallAction!.Value;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(hunter, NewFalconRecallEvent(hunter, action, actionComp));

                Assert.That(eye.Target, Is.EqualTo(hunter));
                Assert.That(entMan.IsQueuedForDeletion(drone) || entMan.Deleted(drone), Is.True);
                Assert.That(entMan.HasComponent<RelayInputMoverComponent>(hunter), Is.False);
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneControlEndsWhenSourceBracerDropped()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                drone = eye.Target!.Value;
                Assert.That(entMan.GetComponent<YautjaFalconControllerComponent>(hunter).Drone, Is.EqualTo(drone));
                Assert.That(entMan.GetComponent<YautjaFalconDroneDeployedComponent>(drone).DroneItem, Is.EqualTo(falcon));

                Assert.That(inventory.TryUnequip(hunter, "gloves", silent: true, force: true), Is.True);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var eye = entMan.GetComponent<EyeComponent>(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(eye.Target, Is.EqualTo(hunter));
                    Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.False);
                    Assert.That(entMan.HasComponent<RelayInputMoverComponent>(hunter), Is.False);
                    Assert.That(entMan.IsQueuedForDeletion(drone) || entMan.Deleted(drone), Is.True);
                    Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out _), Is.False);
                    Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out var returned), Is.True,
                        "CMSS13 /mob/hologram/falcon/Destroy() returns the parent drone to WEAR_L_EAR before WEAR_R_EAR.");
                    Assert.That(returned, Is.EqualTo(falcon));
                    Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (falcon != default && !entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
                if (drone != default && !entMan.Deleted(drone))
                    entMan.DeleteEntity(drone);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SleepingHellhoundRequiresConfirmationBeforeWaking()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var sleeping = entMan.SpawnEntity("CMUHunterShipSleepingHellhound", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(sleeping, new InteractHandEvent(hunter, sleeping));

                Assert.That(entMan.IsQueuedForDeletion(sleeping), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(sleeping), Is.True);
                Assert.That(entMan.EntityQuery<YautjaHellhoundComponent>().Count(), Is.EqualTo(0));

                var dialog = entMan.GetComponent<DialogComponent>(sleeping);
                Assert.That(dialog.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.Title, Is.Not.Empty);
                Assert.That(dialog.Message.Text, Is.Not.Empty);
                Assert.That(dialog.ConfirmEvent, Is.Not.Null);

                entMan.EventBus.RaiseLocalEvent(sleeping, dialog.ConfirmEvent!, true);

                Assert.That(entMan.IsQueuedForDeletion(sleeping), Is.True);

                var query = entMan.EntityQueryEnumerator<YautjaHellhoundComponent, MetaDataComponent>();
                Assert.That(query.MoveNext(out var hellhoundUid, out var hellhound, out var meta), Is.True);
                Assert.That(query.MoveNext(out _, out _, out _), Is.False);
                Assert.That(meta.EntityPrototype?.ID, Is.EqualTo("CMUMobYautjaHellhound"));
                Assert.That(hellhound.YautjaOwner, Is.EqualTo(hunter));
                Assert.That(entMan.HasComponent<GhostRoleComponent>(hellhoundUid), Is.True);
                Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(hellhoundUid), Is.True);
                var ghostRole = entMan.GetComponent<GhostRoleComponent>(hellhoundUid);
                Assert.That(ghostRole.RaffleConfig, Is.Not.Null);
                Assert.That(ghostRole.RaffleConfig!.SettingsOverride, Is.Null);
                Assert.That(ghostRole.RaffleConfig.Settings, Is.EqualTo("default"));
                var session = server.PlayerMan.Sessions.Single();
                var previousAttached = session.AttachedEntity;
                try
                {
                    server.PlayerMan.SetAttachedEntity(session, null);
                    entMan.System<GhostRoleSystem>().Request(session, ghostRole.Identifier);
                    Assert.That(entMan.TryGetComponent(hellhoundUid, out GhostRoleRaffleComponent? raffle), Is.True);
                    Assert.Multiple(() =>
                    {
                        var defaults = server.ProtoMan.Index<Content.Shared.Ghost.Roles.Raffles.GhostRoleRaffleSettingsPrototype>(defaultPrototype).Settings;
                        Assert.That(raffle!.Countdown, Is.EqualTo(TimeSpan.FromSeconds(defaults.InitialDuration)));
                        Assert.That(raffle.JoinExtendsDurationBy, Is.EqualTo(TimeSpan.FromSeconds(defaults.JoinExtendsDurationBy)));
                        Assert.That(raffle.MaxDuration, Is.EqualTo(TimeSpan.FromSeconds(defaults.MaxDuration)));
                        Assert.That(raffle.CurrentMembers, Does.Contain(session));
                    });
                }
                finally
                {
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
                }
                Assert.That(entMan.HasComponent<Content.Server.Atmos.Components.BarotraumaComponent>(hellhoundUid), Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(hellhoundUid), Is.True);
                Assert.That(entMan.HasComponent<RespiratorComponent>(hellhoundUid), Is.False);
                Assert.That(entMan.TryGetComponent<SatiationComponent>(hellhoundUid, out var satiation) && satiation.Has("Hunger"), Is.False);
                Assert.That(entMan.TryGetComponent<SatiationComponent>(hellhoundUid, out var hydration) && hydration.Has("Thirst"), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(sleeping))
                    entMan.DeleteEntity(sleeping);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundRaffleWinnerIsTransferredIntoTheGhostRoleBody()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hellhound = default;
        EntityUid? previousAttached = null;
        var session = server.PlayerMan.Sessions.Single();

        try
        {
            await server.WaitAssertion(() =>
            {
                server.CfgMan.SetCVar(CCVars.GhostQuickLottery, true);

                var entMan = server.EntMan;
                hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
                var role = entMan.GetComponent<GhostRoleComponent>(hellhound);
                previousAttached = session.AttachedEntity;
                server.PlayerMan.SetAttachedEntity(session, null);

                entMan.System<GhostRoleSystem>().Request(session, role.Identifier);

                Assert.That(entMan.TryGetComponent(hellhound, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.That(raffle!.CurrentMembers, Does.Contain(session));
                Assert.That(raffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(1)));
            });

            await pair.RunSeconds(1.25f);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(session.AttachedEntity, Is.EqualTo(hellhound));
                    Assert.That(entMan.GetComponent<GhostRoleComponent>(hellhound).Taken, Is.True);
                    Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(hellhound), Is.False);
                    Assert.That(entMan.System<GhostRoleSystem>().GetGhostRolesInfo(null), Has.None.Matches<GhostRoleInfo>(
                        entry => entry.Identifier == entMan.GetComponent<GhostRoleComponent>(hellhound).Identifier));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(CCVars.GhostQuickLottery, CCVars.GhostQuickLottery.DefaultValue);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hellhound != default && !server.EntMan.Deleted(hellhound))
                    server.EntMan.DeleteEntity(hellhound);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundPullDoesNotTackleButDisarmCanTacklePulledMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.That(pulling.TryStartPull(hellhound, target), Is.True);
                Assert.That(entMan.GetComponent<PullerComponent>(hellhound).Pulling, Is.EqualTo(target));
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False);

                for (var i = 0; i < 5 && !entMan.HasComponent<KnockedDownComponent>(target); i++)
                {
                    var disarm = new CMDisarmEvent(hellhound);
                    entMan.EventBus.RaiseLocalEvent(target, ref disarm);
                    Assert.That(disarm.Handled, Is.True);
                }

                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True);
                Assert.That(entMan.GetComponent<PullerComponent>(hellhound).Pulling, Is.EqualTo(target));
            }
            finally
            {
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundCanRestPullReleaseAndDevourPulledMobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hellhound = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();

            hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            Assert.That(entMan.HasComponent<DevourableComponent>(target), Is.True);
            entMan.EnsureComponent<KnockedDownComponent>(target);

            var rest = new XenoRestActionEvent();
            entMan.EventBus.RaiseLocalEvent(hellhound, rest);
            Assert.That(rest.Handled, Is.True);
            Assert.That(entMan.HasComponent<XenoRestingComponent>(hellhound), Is.True);

            rest = new XenoRestActionEvent();
            entMan.EventBus.RaiseLocalEvent(hellhound, rest);
            Assert.That(rest.Handled, Is.True);
            Assert.That(entMan.HasComponent<XenoRestingComponent>(hellhound), Is.False);

            var hide = new XenoHideActionEvent();
            entMan.EventBus.RaiseLocalEvent(hellhound, hide);
            Assert.That(hide.Handled, Is.True);
            Assert.That(entMan.GetComponent<XenoHideComponent>(hellhound).Hiding, Is.True);

            Assert.That(pulling.TryStartPull(hellhound, target), Is.True);

            var puller = entMan.GetComponent<PullerComponent>(hellhound);
            Assert.That(puller.Pulling, Is.EqualTo(target));

            var pullable = entMan.GetComponent<PullableComponent>(target);
            Assert.That(pulling.TogglePull((target, pullable), hellhound), Is.True);
            Assert.That(entMan.GetComponent<PullerComponent>(hellhound).Pulling, Is.Null);

            Assert.That(pulling.TryStartPull(hellhound, target), Is.True);
            var activate = new ActivateInWorldEvent(hellhound, hellhound, true);
            entMan.EventBus.RaiseLocalEvent(hellhound, activate);
            Assert.That(activate.Handled, Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<DevouredComponent>(target), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (hellhound != default && !entMan.Deleted(hellhound))
                entMan.DeleteEntity(hellhound);
            if (target != default && !entMan.Deleted(target))
                entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTethersVictimAndSurvivesTrigger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapComp.Armed, Is.True);

                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
                Assert.That(entMan.IsQueuedForDeletion(trap), Is.False);
                Assert.That(trapComp.Armed, Is.False);
                Assert.That(trapComp.TrappedMob, Is.EqualTo(target));
                Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);

                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), hunter), Is.False);

                Assert.That(trapSystem.TryDisarmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapComp.TrappedMob, Is.Null);
                Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapBreakFreeAlertReleasesAfterDoAfterLikeCmss13ResistibleTether()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid trap = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var alerts = entMan.System<AlertsSystem>();
                var trapSystem = entMan.System<YautjaTrapSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                trapComp.BreakFreeDelay = TimeSpan.FromSeconds(0.25);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
                Assert.That(alerts.IsShowingAlert(target, "CMUYautjaTrapBreakFree"), Is.True);

                Assert.That(alerts.TryGet("CMUYautjaTrapBreakFree", out var alert), Is.True);
                Assert.That(alerts.ActivateAlert(target, alert!), Is.True,
                    "CMSS13 apply_tether(..., resistible = TRUE) lets the trapped mob resist the trap tether.");

                var doAfter = entMan.GetComponent<DoAfterComponent>(target);
                Assert.That(doAfter.DoAfters.Values.Count(active => !active.Cancelled && !active.Completed), Is.EqualTo(1));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var alerts = entMan.System<AlertsSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                    Assert.That(alerts.IsShowingAlert(target, "CMUYautjaTrapBreakFree"), Is.False);
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(trapComp.ReleaseAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.False);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, target, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapDisarmCancelsPendingBreakFreeDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alerts = entMan.System<AlertsSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                Assert.That(alerts.TryGet("CMUYautjaTrapBreakFree", out var alert), Is.True);
                Assert.That(alerts.ActivateAlert(target, alert!), Is.True);

                var doAfter = entMan.GetComponent<DoAfterComponent>(target);
                Assert.That(doAfter.DoAfters.Values.Count(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaTrapBreakFreeDoAfterEvent), Is.EqualTo(1));

                Assert.That(trapSystem.TryDisarmTrap((trap, trapComp), hunter), Is.True);

                Assert.That(doAfter.DoAfters.Values.Count(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaTrapBreakFreeDoAfterEvent), Is.EqualTo(0),
                    "External trap release should clear the break-free alert and cancel the pending resist doAfter UI.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapAnimalStepOnlySpringsAndDamagesLikeCmss13Crossed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var animal = entMan.SpawnEntity("MobMouse", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var ev = new StepTriggeredOnEvent(trap, animal);
                entMan.EventBus.RaiseLocalEvent(trap, ref ev);

                var damage = entMan.GetComponent<DamageableComponent>(animal);
                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.Armed, Is.False,
                        "CMSS13 /obj/item/hunting_trap/Crossed() sets armed = FALSE for isanimal() instead of calling trapMob().");
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(animal), Is.False);
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((animal, damage)).GetTotal(), Is.EqualTo((FixedPoint2) 20),
                        "CMSS13 /obj/item/hunting_trap/Crossed() applies simple_mob.health -= 20 to animals.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, animal, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapCarbonTriggerDoesNotDealDirectDamageOrStunLikeCmss13TrapMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();
            var status = entMan.System<StatusEffectQuerySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                var damage = entMan.GetComponent<DamageableComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.System<DamageableSystem>().GetAllDamage((target, damage)).GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                        "CMSS13 /obj/item/hunting_trap/trapMob(mob/living/carbon/C) applies tether, side effects and messages but no direct damage.");
                    Assert.That(status.TryGetTime(target, "Stun", out _), Is.False,
                        "CMSS13 /obj/item/hunting_trap/trapMob(mob/living/carbon/C) does not paralyze/stun carbon victims directly.");
                    Assert.That(trapComp.TrappedMob, Is.EqualTo(target));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapKeepsVictimInsideCmss13TetherRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid trap = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                var transform = entMan.System<SharedTransformSystem>();
                transform.SetCoordinates(target, map.GridCoords.Offset(new Vector2(3, 0)));
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                var transform = entMan.System<SharedTransformSystem>();
                var distance = Vector2.Distance(transform.GetMapCoordinates(trap).Position, transform.GetMapCoordinates(target).Position);

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.TrappedMob, Is.EqualTo(target),
                        "CMSS13 apply_tether() blocks movement past the hunting trap's tether range.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(distance, Is.LessThanOrEqualTo(2f));
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.True);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, target, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapDeletionCleansTetherLikeCmss13Destroy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid trap = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
                Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);

                entMan.DeleteEntity(trap);
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False,
                    "CMSS13 /obj/item/hunting_trap/Destroy() calls cleanup_tether(), deleting the active tether when the trap is destroyed.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, target, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapUseInHandArmsAfterCmss13SetupDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid trap = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, trap), Is.True);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(trap, use);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(trapComp.Armed, Is.False,
                        "CMSS13 /obj/item/hunting_trap/attack_self() arms only after a 3 second do_after.");
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.8f));

            await server.WaitAssertion(() =>
            {
                var trapComp = server.EntMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapComp.Armed, Is.False);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.6f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.Armed, Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.True);
                    Assert.That(hands.IsHolding(hunter, trap), Is.False,
                        "CMSS13 /obj/item/hunting_trap/attack_self() calls user.drop_held_item() after the 3 second setup completes.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTryArmFailsWhenUserRestrainedLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var cuffableSystem = entMan.System<CuffableSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var cuffs = entMan.SpawnEntity("Handcuffs", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.TryGetComponent(hunter, out CuffableComponent? cuffable), Is.True);
                Assert.That(cuffableSystem.TryAddNewCuffs(hunter, hunter, cuffs, cuffable), Is.True);
                Assert.That(cuffable!.CanStillInteract, Is.False);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.False,
                        "CMSS13 /obj/item/hunting_trap/attack_self() only arms when !user.is_mob_restrained().");
                    Assert.That(trapComp.Armed, Is.False);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, cuffs, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTechAuthorizedOwnerCanTriggerOwnTrapLikeCmss13Trait()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), user), Is.True);

                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), user), Is.True,
                    "CMSS13 /obj/item/hunting_trap/Crossed() has Yautja/badblood-hive avoidance, but no owner immunity for TRAIT_YAUTJA_TECH non-Yautja.");

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.TrappedMob, Is.EqualTo(user));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(user), Is.True);
                    Assert.That(trapComp.Armed, Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { user, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTechAuthorizedHandInteractDisarmsLikeCmss13Trait()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var interact = new InteractHandEvent(user, trap);
                entMan.EventBus.RaiseLocalEvent(trap, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True,
                        "CMSS13 /obj/item/hunting_trap/attack_hand() lets TRAIT_YAUTJA_TECH users disarm the trap.");
                    Assert.That(trapComp.Armed, Is.False);
                    Assert.That(trapComp.TrappedMob, Is.Null,
                        "A tech-authorized non-Yautja should not fall through to the armed human self-trigger branch.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(user), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, user, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapNonYautjaInteractWarnsAndTriggersLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid nonYautja = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                nonYautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, nonYautja);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var interaction = entMan.System<SharedInteractionSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                interaction.InteractHand(nonYautja, trap);
                Assert.That(trapComp.TrappedMob, Is.EqualTo(nonYautja));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You foolishly reach out for the hunting trap..."));
                    Assert.That(labels, Does.Contain("You get caught in the hunting trap!"));
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

                foreach (var uid in new[] { hunter, nonYautja, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaHarvestsHumanSkullOnceAndRecordsTrophyScore()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var trophies = entMan.System<YautjaTrophySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                mobState.ChangeMobState(target, MobState.Dead);

                Assert.That(trophies.TryHarvestTrophy(hunter, target, YautjaTrophyKind.HumanSkull, out var trophy), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(trophy).EntityPrototype?.ID, Is.EqualTo("CMUYautjaHumanSkullTrophy"));

                var trophyComp = entMan.GetComponent<YautjaTrophyComponent>(trophy);
                Assert.That(trophyComp.Kind, Is.EqualTo(YautjaTrophyKind.HumanSkull));
                Assert.That(trophyComp.SourceName, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(target).EntityName));
                Assert.That(trophyComp.Source, Is.Null, "Master keeps the trophy's source name without a networked corpse reference.");
                Assert.That(trophyComp.Hunter, Is.Null, "Master does not expose the hunter through the trophy's network state.");

                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);
                Assert.That(record.HumanSkulls, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(2));

                Assert.That(trophies.TryHarvestTrophy(hunter, target, YautjaTrophyKind.HumanSkull, out _), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PolishingRagPolishesHumanBoneAfterCmss13FiveSecondDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid techUser = default;
        EntityUid ordinaryUser = default;
        EntityUid humanBone = default;
        EntityUid xenoPelt = default;
        EntityUid rag = default;
        EntityUid ordinaryRag = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                techUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                ordinaryUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                humanBone = entMan.SpawnEntity("CMUYautjaHumanLeftArmBoneTrophy", map.GridCoords);
                xenoPelt = entMan.SpawnEntity("CMUYautjaRunnerPeltTrophy", map.GridCoords);
                rag = entMan.SpawnEntity("CMUYautjaPolishingRag", map.GridCoords);
                ordinaryRag = entMan.SpawnEntity("CMUYautjaPolishingRag", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techUser);

                var ordinaryInteract = new InteractUsingEvent(
                    ordinaryUser,
                    ordinaryRag,
                    humanBone,
                    entMan.GetComponent<TransformComponent>(humanBone).Coordinates);
                entMan.EventBus.RaiseLocalEvent(humanBone, ordinaryInteract);

                var peltInteract = new InteractUsingEvent(
                    hunter,
                    rag,
                    xenoPelt,
                    entMan.GetComponent<TransformComponent>(xenoPelt).Coordinates);
                entMan.EventBus.RaiseLocalEvent(xenoPelt, peltInteract);

                Assert.Multiple(() =>
                {
                    Assert.That(ordinaryInteract.Handled, Is.True,
                        "CMSS13 polishing_rag consumes attempts on bone trophies even when the user lacks TRAIT_YAUTJA_TECH.");
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanBone).Polished, Is.False,
                        "Non-tech users should fail the CMSS13 HAS_TRAIT(user, TRAIT_YAUTJA_TECH) gate.");
                    Assert.That(ActivePolishDoAfters(entMan, ordinaryUser), Is.Zero);
                    Assert.That(peltInteract.Handled, Is.True,
                        "pelts can't be polished, the rag answers with a popup instead of silently doing nothing.");
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(xenoPelt).Polished, Is.False);
                    Assert.That(ActivePolishDoAfters(entMan, hunter), Is.Zero);
                });

                var techInteract = new InteractUsingEvent(
                    techUser,
                    rag,
                    humanBone,
                    entMan.GetComponent<TransformComponent>(humanBone).Coordinates);
                entMan.EventBus.RaiseLocalEvent(humanBone, techInteract);

                var active = entMan.GetComponent<DoAfterComponent>(techUser).DoAfters.Values.Single(doAfter =>
                    !doAfter.Cancelled &&
                    !doAfter.Completed &&
                    doAfter.Args.Event is YautjaPolishTrophyDoAfterEvent);

                Assert.Multiple(() =>
                {
                    Assert.That(techInteract.Handled, Is.True);
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(5)),
                        "CMSS13 polishing_rag uses do_after(user, 5 SECONDS, INTERRUPT_MOVED, ...).");
                    Assert.That(active.Args.Target, Is.EqualTo(humanBone));
                    Assert.That(active.Args.Used, Is.EqualTo(rag));
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanBone).Polished, Is.False,
                        "CMSS13 only sets polished after the 5 second do_after completes.");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(humanBone).EntityName, Is.EqualTo("arm bone"));
                    Assert.That(entMan.TryGetComponent<YautjaTrophyRecordComponent>(techUser, out _), Is.False,
                        "Starting the source do_after should not grant polish score yet.");
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(5.25f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trophy = entMan.GetComponent<YautjaTrophyComponent>(humanBone);
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(techUser);

                Assert.Multiple(() =>
                {
                    Assert.That(trophy.Polished, Is.True);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(humanBone).EntityName, Is.EqualTo("polished arm bone"));
                    Assert.That(record.PolishedTrophies, Is.EqualTo(1));
                    Assert.That(record.Score, Is.EqualTo(1));
                    Assert.That(ActivePolishDoAfters(entMan, techUser), Is.Zero);
                });
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, techUser, ordinaryUser, humanBone, xenoPelt, rag, ordinaryRag })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RitualDuelWinRecordsTrophyCreditOnTargetDeath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var rituals = entMan.System<YautjaRitualSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(rituals.TryClaimCaptive(hunter, target), Is.False);
                Assert.That(rituals.TryClaimCaptive(hunter, target, bypassControlRequirement: true), Is.True);
                Assert.That(rituals.TryBeginDuel(hunter, target), Is.True);

                mobState.ChangeMobState(target, MobState.Dead);

                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);
                Assert.That(record.RitualDuelWins, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(5));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
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

    private static YautjaToggleWristBladesActionEvent NewToggleWristBladesEvent(
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        return new YautjaToggleWristBladesActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
    }

    private static string HandIdForLocation(SharedHandsSystem hands, EntityUid user, HandLocation location)
    {
        foreach (var handId in hands.EnumerateHands(user))
        {
            if (hands.TryGetHand(user, handId, out var hand) &&
                hand.Value.Location == location)
            {
                return handId;
            }
        }

        Assert.Fail($"No {location} hand found for {user}.");
        return string.Empty;
    }

    private static void AssertHeldInHandLocation(SharedHandsSystem hands, EntityUid user, EntityUid item, HandLocation location)
    {
        Assert.That(hands.IsHolding(user, item, out var handId), Is.True);
        Assert.That(hands.TryGetHand(user, handId, out var hand), Is.True);
        Assert.That(hand.Value.Location, Is.EqualTo(location));
    }

    private static bool TryGetHandHolding(SharedHandsSystem hands, EntityUid user, EntityUid item, out string handId)
    {
        return hands.IsHolding(user, item, out handId);
    }

    private static YautjaFalconRecallActionEvent NewFalconRecallEvent(
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        return new YautjaFalconRecallActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
    }

    private static int ActiveCleanserDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaCleanserDoAfterEvent)
            : 0;
    }

    private static int ActiveHivebreakerDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaHivebreakerDoAfterEvent)
            : 0;
    }

    private static int ActivePolishDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaPolishTrophyDoAfterEvent)
            : 0;
    }

    private static int ActiveBracerMisuseDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaBracerMisuseDoAfterEvent)
            : 0;
    }

    private static async Task AssertClientHasPopup(
        RobustIntegrationTest.ClientIntegrationInstance client,
        string expected,
        string? absent = null)
    {
        await client.WaitAssertion(() =>
        {
            var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
            var joinedLabels = string.Join("\n", labels);

            Assert.That(labels, Does.Contain(expected), $"Expected popup:\n{expected}\nActual labels:\n{joinedLabels}");
            if (absent != null)
                Assert.That(labels, Does.Not.Contain(absent), $"Stale popup should not be present:\n{absent}\nActual labels:\n{joinedLabels}");
        });
    }

    private static int CountAttachedArms(SharedBodySystem body, EntityUid user)
    {
        return body.GetBodyChildren(user)
            .Count(part => part.Component.PartType == BodyPartType.Arm);
    }

    private static EntityUid GetBodyPart(
        SharedBodySystem body,
        EntityUid user,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        foreach (var (partUid, part) in body.GetBodyChildren(user))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Expected {type} {symmetry} body part on {user}.");
        return default;
    }

    private static HashSet<EntityUid> AudioEntities(IEntityManager entMan)
    {
        var audio = new HashSet<EntityUid>();
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            audio.Add(uid);
        }

        return audio;
    }

    private static List<string> AudioFileNamesAfter(IEntityManager entMan, HashSet<EntityUid> before)
    {
        var audio = new List<string>();
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!before.Contains(uid))
                audio.Add(component.FileName);
        }

        return audio;
    }

}

public sealed partial class YautjaTestSpeechListenerSystem : EntitySystem
{
    public readonly List<(EntityUid Source, string Message)> Spoken = new();
    public readonly List<(EntityUid Source, string Message, string? SpeechStyleClass)> StyledSpeech = new();
    public readonly List<(EntityUid Source, string EmoteId)> Emotes = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTestEmoteListenerComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
    }

    private void OnEmote(Entity<YautjaTestEmoteListenerComponent> ent, ref EmoteEvent ev)
    {
        Emotes.Add((ent, ev.Emote.ID));
    }

    private void OnSpoke(EntitySpokeEvent ev)
    {
        Spoken.Add((ev.Source, ev.Message));
        StyledSpeech.Add((ev.Source, ev.Message, EntityManager.GetComponentOrNull<RMCSpeechBubbleSpecificStyleComponent>(ev.Source)?.SpeechStyleClass));
    }
}

public sealed partial class YautjaTeleportMoveHookTestSystem : EntitySystem
{
    public readonly Dictionary<EntityUid, int> MoveCounts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YautjaTeleportMoveHookTestComponent, MoveEvent>(OnMove);
    }

    public void Watch(EntityUid uid)
    {
        EnsureComp<YautjaTeleportMoveHookTestComponent>(uid);
    }

    public void Reset()
    {
        MoveCounts.Clear();
    }

    private void OnMove(Entity<YautjaTeleportMoveHookTestComponent> ent, ref MoveEvent ev)
    {
        MoveCounts[ev.Sender] = MoveCounts.GetValueOrDefault(ev.Sender) + 1;
    }
}

[RegisterComponent]
public sealed partial class YautjaTeleportMoveHookTestComponent : Component;

[RegisterComponent]
public sealed partial class YautjaTestEmoteListenerComponent : Component;

// Seed the slot draw at completion so unrelated simulation RNG during the
// three-second DoAfter cannot choose a different function for these scenarios.
public sealed partial class YautjaBracerRandomSeedTestSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerRandomSeedTestComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<YautjaBracerRandomSeedTestComponent, YautjaBracerMisuseDoAfterEvent>(
            OnCompleted, before: [typeof(YautjaBracerUtilitySystem)]);
    }

    private void OnInit(Entity<YautjaBracerRandomSeedTestComponent> ent, ref ComponentInit args)
    {
        // CMU14: a hunter bracer randomly shocks a non-Yautja wearer (yaut_bracers.dm:392-400); the shock
        // stuns and so both breaks the 3s misuse do_after and draws from the shared RNG the seeded slot
        // tests walk. The component is added to the bracer under test, so switch it off here once.
        if (TryComp(ent.Owner, out YautjaBracerComponent? bracer))
            bracer.NonYautjaShockChance = 0f;
    }

    private void OnCompleted(Entity<YautjaBracerRandomSeedTestComponent> ent, ref YautjaBracerMisuseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        _random.SetSeed(ent.Comp.Seed);
        ent.Comp.Completed = true;
    }
}

[RegisterComponent]
public sealed partial class YautjaBracerRandomSeedTestComponent : Component
{
    public int Seed;
    public bool Completed;
}
