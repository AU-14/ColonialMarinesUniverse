using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client._RMC14.Dialog;
using Content.Client.Popups;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._CMU14.Yautja;
using Content.Server.Atmos.Components;
using Content.Server.Administration.Logs;
using Content.Server.Beam.Components;
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
using Content.Server.Speech.Components;
using Content.Server.Verbs;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.NightVision;
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
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Yautja;
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
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
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
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;
namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaSmokeTest
{
    private static readonly EntProtoId FalconDronePrototype = "CMUYautjaFalconDrone";
    private static readonly EntProtoId FalconDroneDeployedPrototype = "CMUYautjaFalconDroneDeployed";
    private static readonly ProtoId<JobPrototype> HellhoundJob = "CMUYautjaHellhound";

    private static readonly string[] ClanArmorLoadoutIds =
    {
        "CMUYautjaClanArmor",
        "CMUYautjaClanArmorBronze",
        "CMUYautjaClanArmorSilver",
        "CMUYautjaClanArmorCrimson",
        "CMUYautjaClanArmorBone",
    };

    [Test]
    public async Task DirectYautjaSpawnGetsCoreLoadout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                AssertEquipped(entMan, inventory, hunter, "ears", "CMUYautjaCommunicator");
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False,
                    "The falcon drone is rack gear and must not be part of the direct spawn loadout.");
                AssertEquipped(entMan, inventory, hunter, "gloves", "CMUYautjaBracer");
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                var bracerGear = entMan.GetComponent<YautjaGearContainerComponent>(bracer!.Value);
                Assert.That(bracerGear.Gear.ContainsKey(YautjaGearKind.Shield), Is.False,
                    "The wrist shield is rack gear and must not be part of the direct spawn loadout.");
                Assert.That(inventory.TryGetSlotEntity(hunter, "mask", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "back", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "outerClothing", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "jumpsuit", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "shoes", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "belt", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "pocket1", out _), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "pocket2", out _), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

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
    public async Task CleanserGelPopupsUseCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid nonTech = default;
        EntityUid cleaner = default;
        EntityUid nonTechCleaner = default;
        EntityUid target = default;
        EntityUid cloakedTarget = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                nonTech = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
                cleaner = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords);
                nonTechCleaner = entMan.SpawnEntity("CMUYautjaCleanserGelVial", map.GridCoords.Offset(new Vector2(0, 1)));
                target = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new Vector2(1, 0)));
                cloakedTarget = entMan.SpawnEntity("CMCrowbar", map.GridCoords.Offset(new Vector2(1, 1)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, cleaner), Is.True);
                Assert.That(hands.TryPickupAnyHand(nonTech, nonTechCleaner), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                var cloakedInteract = new AfterInteractEvent(
                    hunter,
                    cleaner,
                    cloakedTarget,
                    entMan.GetComponent<TransformComponent>(cloakedTarget).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(cleaner, cloakedInteract);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "It would not be safe to attempt this while cloaked!");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.RemoveComponent<EntityActiveInvisibleComponent>(hunter);

                var interact = new AfterInteractEvent(
                    hunter,
                    cleaner,
                    target,
                    entMan.GetComponent<TransformComponent>(target).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(cleaner, interact);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You begin to spread dissolving gel onto crowbar!",
                "You begin spreading bright blue gel over crowbar.");

            await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
            await AssertClientHasPopup(
                client,
                "You cover crowbar with dissolving gel!",
                "You cover crowbar in dissolving gel.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, nonTech);

                var denied = new AfterInteractEvent(
                    nonTech,
                    nonTechCleaner,
                    target,
                    entMan.GetComponent<TransformComponent>(target).Coordinates,
                    true);
                entMan.EventBus.RaiseLocalEvent(nonTechCleaner, denied);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You have no idea what this even does.",
                "You have no idea what this gel does.");

            await pair.RunTicksSync(pair.SecondsToTicks(14.9f));
            await AssertClientHasPopup(
                client,
                "crowbar crumbles into pieces!",
                "crowbar crumbles into residue.");
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

                foreach (var uid in new[] { hunter, nonTech, cleaner, nonTechCleaner, target, cloakedTarget })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
    public async Task CloakedYautjaDoesNotDecloakFromGenericDamageChangedEvent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
                var turnInvisible = entMan.EnsureComponent<EntityTurnInvisibleComponent>(hunter);
                turnInvisible.Enabled = true;

                var damageable = entMan.EnsureComponent<DamageableComponent>(hunter);
                var damage = new DamageSpecifier { DamageDict = { ["Slash"] = 5 } };
                entMan.EventBus.RaiseLocalEvent(hunter, new DamageChangedEvent(damageable, damage, true, null, null));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
                    Assert.That(turnInvisible.Enabled, Is.True,
                        "CMSS13 cloak does not listen to generic damage changes; bullets and explicit cloak-cancel events handle forced/10% decloak instead.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaPowerAlertUsesCmss13PowerbarHudStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alerts = entMan.System<AlertsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var power = entMan.System<YautjaPowerSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var alert = prototypes.Index<AlertPrototype>("CMUYautjaPower");
            var expectedStates = new[]
            {
                "powerbar100",
                "powerbar90",
                "powerbar80",
                "powerbar70",
                "powerbar60",
                "powerbar50",
                "powerbar40",
                "powerbar30",
                "powerbar20",
                "powerbar10",
            };

            Assert.Multiple(() =>
            {
                Assert.That(alert.MinSeverity, Is.EqualTo(0));
                Assert.That(alert.MaxSeverity, Is.EqualTo(9));

                for (var i = 0; i < expectedStates.Length; i++)
                {
                    var icon = AssertCmss13PowerbarIcon(alert, (short) i);
                    Assert.That(icon.RsiState, Is.EqualTo(expectedStates[i]), $"severity {i}");
                }
            });

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var bracerPower = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerPower.MaxCharge = 100;
                bracerPower.Charge = 100;

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var sourceThresholds = new (int Charge, short Severity, string State)[]
                {
                    (100, 0, "powerbar100"),
                    (91, 0, "powerbar100"),
                    (90, 1, "powerbar90"),
                    (81, 1, "powerbar90"),
                    (80, 2, "powerbar80"),
                    (71, 2, "powerbar80"),
                    (70, 3, "powerbar70"),
                    (61, 3, "powerbar70"),
                    (60, 4, "powerbar60"),
                    (51, 4, "powerbar60"),
                    (50, 5, "powerbar50"),
                    (41, 5, "powerbar50"),
                    (40, 6, "powerbar40"),
                    (31, 6, "powerbar40"),
                    (30, 7, "powerbar30"),
                    (21, 7, "powerbar30"),
                    (20, 8, "powerbar20"),
                    (11, 8, "powerbar20"),
                    (10, 9, "powerbar10"),
                    (0, 9, "powerbar10"),
                };

                foreach (var (charge, severity, state) in sourceThresholds)
                {
                    bracerPower.Charge = charge;
                    power.UpdateAlert((bracer, bracerPower));

                    Assert.That(YautjaPowerSystem.GetCmss13PowerAlertSeverity(charge, bracerPower.MaxCharge), Is.EqualTo(severity), $"{charge}% helper");
                    Assert.That(alerts.TryGetAlertState(hunter, alert.AlertKey, out var alertState), Is.True, $"{charge}% alert state");
                    Assert.That(alertState.Severity, Is.EqualTo(severity), $"{charge}% severity");
                    Assert.That(AssertCmss13PowerbarIcon(alert, alertState.Severity!.Value).RsiState, Is.EqualTo(state), $"{charge}% icon");
                }
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerPowerAlertUpdatesOnEquipDrainRegenAndUnequipLikeCmss13Display()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var alerts = entMan.System<AlertsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var power = entMan.System<YautjaPowerSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var alert = prototypes.Index<AlertPrototype>("CMUYautjaPower");

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var bracerPower = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerPower.MaxCharge = 100;
                bracerPower.Charge = 100;

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(alerts.TryGetAlertState(hunter, alert.AlertKey, out var equipped), Is.True);
                Assert.That(equipped.Severity, Is.EqualTo(0));
                Assert.That(equipped.DynamicMessage, Is.EqualTo("100 / 100"));

                power.RemovePower((bracer, bracerPower), 55);
                Assert.That(alerts.TryGetAlertState(hunter, alert.AlertKey, out var drained), Is.True);
                Assert.That(drained.Severity, Is.EqualTo(5));
                Assert.That(drained.DynamicMessage, Is.EqualTo("45 / 100"));

                power.RegenPower((bracer, bracerPower), 35);
                Assert.That(alerts.TryGetAlertState(hunter, alert.AlertKey, out var recharged), Is.True);
                Assert.That(recharged.Severity, Is.EqualTo(2));
                Assert.That(recharged.DynamicMessage, Is.EqualTo("80 / 100"));

                Assert.That(inventory.TryUnequip(hunter, "gloves", out _, silent: true, force: true), Is.True);
                Assert.That(alerts.TryGetAlertState(hunter, alert.AlertKey, out _), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterFamilyBracerChargeAndRegenMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracerIds = new[]
            {
                "CMUYautjaBracer",
                "CMUYautjaBracerRetro",
                "CMUYautjaBracerEbony",
                "CMUYautjaBracerSilver",
                "CMUYautjaBracerBronze",
                "CMUYautjaBracerCrimson",
                "CMUYautjaBracerBone",
                "CMUYautjaBracerLegacyDragon",
                "CMUYautjaBracerLegacySwamp",
                "CMUYautjaBracerLegacyEnforcer",
                "CMUYautjaBracerLegacyCollector",
                "CMUYautjaBloodedThrallBracer",
                "CMUYautjaBloodedThrallBracerSilver",
                "CMUYautjaBloodedThrallBracerGold",
                "CMUYautjaBloodedThrallBracerCrimson",
                "CMUYautjaBloodedThrallBracerBone",
            };
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var id in bracerIds)
                {
                    var bracer = entMan.SpawnEntity(id, MapCoordinates.Nullspace);
                    spawned.Add(bracer);
                    var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                    Assert.Multiple(() =>
                    {
                        Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 3000), $"{id} CMSS13 /obj/item/clothing/gloves/yautja/hunter charge");
                        Assert.That(bracerComp.MaxCharge, Is.EqualTo((FixedPoint2) 3000), $"{id} CMSS13 /obj/item/clothing/gloves/yautja/hunter charge_max");
                        Assert.That(bracerComp.Regen, Is.EqualTo((FixedPoint2) 60), $"{id} CMSS13 /obj/item/clothing/gloves/yautja charge_rate");
                    });
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
    public async Task HunterBracerDefaultInvisibilitySoundUsesCmss13ModernCloakAudio()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.InvisibilitySound, Is.EqualTo(YautjaInvisibilitySound.Modern));
                    AssertSoundPath(bracerComp.CloakOnSound, "/Audio/_CMU14/Yautja/pred_cloakon_modern.wav");
                    AssertSoundPath(bracerComp.CloakOffSound, "/Audio/_CMU14/Yautja/pred_cloakoff_modern.wav");
                });
            }
            finally
            {
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrdinaryThrallBracerPowerDefaultsMatchCmss13BaseBracerWithoutHunterActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                var bracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaBracerComponent>(bracer), Is.False,
                        "The ordinary thrall bracer maps CMSS13 base-bracer power vars without enabling the local hunter bracer action component.");
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 1500),
                        "CMSS13 /obj/item/clothing/gloves/yautja base charge inherited by /thrall.");
                    Assert.That(bracerComp.MaxCharge, Is.EqualTo((FixedPoint2) 1500),
                        "CMSS13 /obj/item/clothing/gloves/yautja base charge_max inherited by /thrall.");
                    Assert.That(bracerComp.Regen, Is.EqualTo((FixedPoint2) 60),
                        "CMSS13 /obj/item/clothing/gloves/yautja base charge_rate inherited by /thrall.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerProcessRechargeUsesCmss13LevelMultipliers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var openSpaceMap = await pair.CreateTestMap();
        var groundMap = await pair.CreateTestMap();
        var mainshipMap = await pair.CreateTestMap();

        EntityUid openSpaceHunter = default;
        EntityUid openSpaceBracer = default;
        EntityUid groundHunter = default;
        EntityUid groundBracer = default;
        EntityUid mainshipHunter = default;
        EntityUid mainshipBracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var inventory = entMan.System<InventorySystem>();
                var timing = server.ResolveDependency<IGameTiming>();

                entMan.EnsureComponent<RMCPlanetComponent>(groundMap.Grid.Owner);
                var mainshipAreas = entMan.EnsureComponent<AreaGridComponent>(mainshipMap.Grid.Owner);
                areas.ReplaceArea(mainshipAreas, Vector2i.Zero, "RMCAreaAlmayer");

                openSpaceHunter = entMan.SpawnEntity("CMMobHuman", openSpaceMap.GridCoords);
                openSpaceBracer = entMan.SpawnEntity("CMUYautjaBracer", openSpaceMap.GridCoords);
                groundHunter = entMan.SpawnEntity("CMMobHuman", groundMap.GridCoords);
                groundBracer = entMan.SpawnEntity("CMUYautjaBracer", groundMap.GridCoords);
                mainshipHunter = entMan.SpawnEntity("CMMobHuman", mainshipMap.GridCoords);
                mainshipBracer = entMan.SpawnEntity("CMUYautjaBracer", mainshipMap.GridCoords);

                PrepareBracerRegen(entMan, inventory, timing, openSpaceHunter, openSpaceBracer);
                PrepareBracerRegen(entMan, inventory, timing, groundHunter, groundBracer);
                PrepareBracerRegen(entMan, inventory, timing, mainshipHunter, mainshipBracer);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var openSpacePower = entMan.GetComponent<YautjaBracerComponent>(openSpaceBracer);
                var groundPower = entMan.GetComponent<YautjaBracerComponent>(groundBracer);
                var mainshipPower = entMan.GetComponent<YautjaBracerComponent>(mainshipBracer);

                Assert.Multiple(() =>
                {
                    Assert.That(openSpacePower.Charge, Is.EqualTo((FixedPoint2) 1060),
                        "CMSS13 bracers recharge by full charge_rate outside ground/mainship z-levels.");
                    Assert.That(groundPower.Charge, Is.EqualTo((FixedPoint2) 1010),
                        "CMSS13 is_ground_level() bracers recharge by charge_rate / 6.");
                    Assert.That(mainshipPower.Charge, Is.EqualTo((FixedPoint2) 1020),
                        "CMSS13 is_mainship_level() bracers recharge by charge_rate / 3.");
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
                             openSpaceBracer,
                             groundHunter,
                             groundBracer,
                             mainshipHunter,
                             mainshipBracer,
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
    public async Task BracerDrainPowerUsesCmss13LowPowerWarning()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var power = entMan.System<YautjaPowerSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 399;
                bracerComp.MaxCharge = 3000;

                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(power.TryRemovePower(hunter, 400), Is.False);
                Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 399),
                    "CMSS13 drain_power() leaves charge unchanged when charge < amount.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels.Any(label =>
                        label.Contains("Your bracers lack the energy. They have only") &&
                        label.Contains("399/3000") &&
                        label.Contains("need") &&
                        label.Contains("400")),
                    Is.True,
                    string.Join('\n', labels));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            });

            await pair.CleanReturnAsync();
        }
    }

    private static void PrepareBracerRegen(
        IEntityManager entMan,
        InventorySystem inventory,
        IGameTiming timing,
        EntityUid hunter,
        EntityUid bracer)
    {
        entMan.EnsureComponent<YautjaComponent>(hunter);
        Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

        var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
        bracerComp.Charge = 1000;
        bracerComp.MaxCharge = 3000;
        bracerComp.Regen = 60;
        bracerComp.NextRegen = timing.CurTime;
    }

    [Test]
    public async Task OrdinaryThrallBracerKeepsThrallOnlyActionSurfaceLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();

            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaThrallComponent>(thrall);

                var ev = new GetItemActionsEvent(actions, thrall, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaTransmitThrallMessage"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleBracerNotificationSound"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaOpenBracerMenu"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleCloak"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCallDisc"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaTranslator"));
                });
            }
            finally
            {
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerExamineShowsCmss13ChargeText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                var hunterPower = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                hunterPower.Charge = 1234;
                hunterPower.MaxCharge = 3000;

                var thrallPower = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallPower.Charge = 321;
                thrallPower.MaxCharge = 1500;

                Assert.Multiple(() =>
                {
                    Assert.That(examine.GetExamineText(hunterBracer, hunter).ToMarkup(),
                        Does.Contain("They currently have <bold>1234/3000</bold> charge."),
                        "CMSS13 /obj/item/clothing/gloves/yautja/get_examine_text() exposes current/max bracer charge.");
                    Assert.That(examine.GetExamineText(thrallBracer, hunter).ToMarkup(),
                        Does.Contain("They currently have <bold>321/1500</bold> charge."),
                        "CMSS13 /obj/item/clothing/gloves/yautja/thrall inherits the base bracer charge examine line.");
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(hunterBracer))
                    entMan.DeleteEntity(hunterBracer);

                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerExamineShowsCmss13AttachmentAndBadbloodLines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            var techViewer = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var nonTechViewer = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var leftAttachment = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", MapCoordinates.Nullspace);
            var leftWeapon = entMan.SpawnEntity("CMUYautjaWristBlades", MapCoordinates.Nullspace);
            var rightAttachment = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var rightWeapon = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techViewer);

                var bracer = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                bracer.BadBlood = true;

                var gear = entMan.GetComponent<YautjaGearContainerComponent>(hunterBracer);
                gear.Gear[YautjaGearKind.WristBlades] = leftAttachment;
                gear.SecondaryGear[YautjaGearKind.Scimitar] = rightAttachment;
                gear.InstalledGear.Add(leftAttachment);
                gear.InstalledGear.Add(rightAttachment);

                var leftStored = entMan.GetComponent<YautjaStoredGearComponent>(leftAttachment);
                leftStored.Bracer = hunterBracer;
                leftStored.Kind = YautjaGearKind.WristBlades;
                leftStored.AttachedWeapon = leftWeapon;

                var rightStored = entMan.GetComponent<YautjaStoredGearComponent>(rightAttachment);
                rightStored.Bracer = hunterBracer;
                rightStored.Kind = YautjaGearKind.Scimitar;
                rightStored.AttachedWeapon = rightWeapon;

                var techMarkup = examine.GetExamineText(hunterBracer, techViewer).ToMarkup();
                var nonTechMarkup = examine.GetExamineText(hunterBracer, nonTechViewer).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(techMarkup,
                        Does.Contain("The left bracer attachment is wrist blade."),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/get_examine_text() exposes the left attached weapon.");
                    Assert.That(techMarkup,
                        Does.Contain("The right bracer attachment is wrist scimitar."),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/get_examine_text() exposes the right attached weapon.");
                    Assert.That(techMarkup,
                        Does.Contain("This belongs to a bad-blood!"),
                        "CMSS13 only shows the badblood warning to users with Yautja tech access.");
                    Assert.That(nonTechMarkup, Does.Contain("The left bracer attachment is wrist blade."));
                    Assert.That(nonTechMarkup, Does.Contain("The right bracer attachment is wrist scimitar."));
                    Assert.That(nonTechMarkup, Does.Not.Contain("This belongs to a bad-blood!"));
                });
            }
            finally
            {
                entMan.DeleteEntity(techViewer);
                entMan.DeleteEntity(nonTechViewer);

                if (!entMan.Deleted(hunterBracer))
                    entMan.DeleteEntity(hunterBracer);

                if (!entMan.Deleted(leftAttachment))
                    entMan.DeleteEntity(leftAttachment);

                if (!entMan.Deleted(leftWeapon))
                    entMan.DeleteEntity(leftWeapon);

                if (!entMan.Deleted(rightAttachment))
                    entMan.DeleteEntity(rightAttachment);

                if (!entMan.Deleted(rightWeapon))
                    entMan.DeleteEntity(rightWeapon);
            }
        });

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
                var wornEmp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10));
                entMan.EventBus.RaiseLocalEvent(hunter, ref wornEmp);

                var floorPower = entMan.GetComponent<YautjaBracerComponent>(floorBracer);
                floorPower.Charge = 600;
                var floorEmp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10));
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
    public async Task HunterBracerEmpActShowsCmss13HissSparkText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid floorBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                floorBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var emp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10));
                entMan.EventBus.RaiseLocalEvent(hunter, ref emp);
                Assert.That(emp.Affected, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "Your bracers hiss and spark!");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var emp = new EmpPulseEvent(50000, false, false, TimeSpan.FromSeconds(10));
                entMan.EventBus.RaiseLocalEvent(floorBracer, ref emp);
                Assert.That(emp.Affected, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You hear a hiss and crackle!");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (bracer != default && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (floorBracer != default && !entMan.Deleted(floorBracer))
                    entMan.DeleteEntity(floorBracer);
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YautjaBracerUsesTwentyPercentOrdinaryProjectileDecloakChance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.BulletDecloakChance, Is.EqualTo(0.20f),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/proc/bullet_hit() uses prob(20) for ordinary projectiles.");
                    Assert.That(bracerComp.BulletDecloakAbsorbs, Is.True,
                        "CMSS13 bullet_hit() returns COMPONENT_CANCEL_BULLET_ACT when the ordinary projectile decloaks the wearer, defeating that one bullet.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
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
    public async Task YautjaBracerFabricatorCostsMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.StabilisingCrystalCost, Is.EqualTo((FixedPoint2) 400),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/proc/injectors_internal() drains 400 bracer charge.");
                    Assert.That(bracerComp.HumanStabilisingCrystalCost, Is.EqualTo((FixedPoint2) 400),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/proc/human_injectors_internal() drains 400 bracer charge.");
                    Assert.That(bracerComp.HealingCapsuleCost, Is.EqualTo((FixedPoint2) 600),
                        "CMSS13 /obj/item/clothing/gloves/yautja/hunter/proc/healing_capsule_internal() drains 600 bracer charge.");
                    Assert.That(bracerComp.StabilisingCrystalCooldown, Is.EqualTo(TimeSpan.FromMinutes(2)),
                        "CMSS13 stabilising crystal fabrication uses a shared 2 minute cooldown for both Yautja and human crystals.");
                    Assert.That(bracerComp.HealingCapsuleCooldown, Is.EqualTo(TimeSpan.FromMinutes(4)),
                        "CMSS13 healing capsule fabrication uses a 4 minute cooldown.");
                });
            }
            finally
            {
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerForceLocksOnEquipLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Locked = false;

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.User, Is.EqualTo(hunter));
                    Assert.That(bracerComp.Locked, Is.True,
                        "CMSS13 /obj/item/clothing/gloves/yautja/equipped(WEAR_HANDS) calls toggle_lock_internal(user, TRUE).");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerForcedUnequipUnlocksLikeCmss13Drop()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Locked = true;

                Assert.That(inventory.TryUnequip(hunter, "gloves", silent: true, force: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bracerComp.User, Is.Null);
                    Assert.That(bracerComp.Locked, Is.False,
                        "CMSS13 /obj/item/clothing/gloves/yautja/dropped() calls unlock_bracer() to prevent stuck nodrop bracers after forced removal.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechBracerUseStartsCmss13ThreeSecondMisuseDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
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
                var inventory = entMan.System<InventorySystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 0f;
                comp.NonYautjaDelimbChance = 0f;

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);

                var doAfter = entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaBracerMisuseDoAfterEvent);

                Assert.That(doAfter.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(3)),
                    "CMSS13 check_random_function() sets next_move and starts do_after(user, 3, INTERRUPT_ALL) before resolving non-tech bracer misuse.");
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
    public async Task NonTechBracerUseResolvesOnlyAfterCmss13MisuseDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 1f;
                comp.NonYautjaRandomFunctionChance = 0f;
                comp.NonYautjaDelimbChance = 0f;

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, user), Is.False,
                    "CMSS13 check_random_function() waits for do_after(user, 3, INTERRUPT_ALL) before returning FALSE to the requested bracer action.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();

                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, user), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
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
    public async Task NonTechSynthBracerUseUsesCmss13SynthMisuseChances()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid synth = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                synth = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<SynthComponent>(synth);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(synth, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, synth);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 0f;
                comp.SynthWorkingChance = 1f;
                comp.SynthRandomFunctionChance = 0f;
                comp.NonYautjaDelimbChance = 0f;

                Assert.That(utility.TryOpenTranslator((bracer, comp), synth), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, synth), Is.EqualTo(1));
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, synth), Is.False);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();

                Assert.That(ActiveBracerMisuseDoAfters(entMan, synth), Is.Zero);
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, synth), Is.True,
                    "CMSS13 check_random_function() uses synth-specific 40/4 working/random chances instead of the ordinary non-tech 20/10 bucket.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { synth, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechResearcherBracerUseUsesCmss13ResearcherMisuseChances()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid researcher = default;
        EntityUid bracer = default;
        EntityUid id = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                researcher = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                id = entMan.SpawnEntity("CMIDCardResearcher", map.GridCoords);

                Assert.That(inventory.TryEquip(researcher, id, "id", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(researcher, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, researcher);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 0f;
                comp.ResearcherWorkingChance = 1f;
                comp.ResearcherRandomFunctionChance = 0f;
                comp.NonYautjaDelimbChance = 0f;

                Assert.That(utility.TryOpenTranslator((bracer, comp), researcher), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, researcher), Is.EqualTo(1));
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, researcher), Is.False);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();

                Assert.That(ActiveBracerMisuseDoAfters(entMan, researcher), Is.Zero);
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, researcher), Is.True,
                    "CMSS13 check_random_function() uses researcher-specific 25/7 working/random chances instead of the ordinary non-tech 20/10 bucket.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { researcher, bracer, id })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechBracerUseChecksRandomFunctionBeforeWorkingChanceLikeCmss13()
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

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0.20f;
                comp.NonYautjaRandomFunctionChance = 0.10f;
                comp.NonYautjaDelimbChance = 0f;
                random.SetSeed(14);
                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The alien controls misread your touch.",
                "You accidentally trigger a bracer function.");
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
    public async Task NonTechBracerUseNoOpDoesNotShockLikeCmss13()
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

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0.20f;
                comp.NonYautjaRandomFunctionChance = 0.10f;
                comp.NonYautjaDelimbChance = 0f;
                random.SetSeed(0);
                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);

                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(entMan.GetComponent<DamageableComponent>(user).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                Assert.That(entMan.GetComponent<DamageableComponent>(user).TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                    "CMSS13 failed non-tech bracer rolls only show the no-op text after the misuse do_after; they do not shock or damage the user.");
            });
            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You fiddle with the buttons but nothing happens...",
                "The alien technology shocks you violently!");
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

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2),
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.75f));
            await server.WaitPost(() =>
            {
                var random = server.ResolveDependency<IRobustRandom>();
                random.SetSeed(0);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.75f));
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
                        Does.Contain("/Audio/_CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav"),
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
    public async Task NonTechRandomBracerFunctionSlotTwoOpensTrackerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid deadYautja = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                deadYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 3)));

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(deadYautja, MobState.Dead);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                random.SetSeed(1);

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True,
                        "CMSS13 activate_random_verb() slot 2 routes to track_gear_internal(user, TRUE).");
                    Assert.That(state!.TrackedGear.Single().Name, Is.EqualTo("deceased Yautja bio signature"));
                    Assert.That(comp.IdChipDeployed, Is.False,
                        "Local slot 2 must not use the old ID chip toggle random-function behavior.");
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

                foreach (var uid in new[] { user, bracer, deadYautja })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechRandomBracerFunctionSlotOneDeploysAttachmentsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid wristBladesHolder = default;
        EntityUid wristBlades = default;
        EntityUid? previousAttached = null;
        bool wasLocked = false;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                wristBladesHolder = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.NonYautjaWorkingChance = 0f;
                bracerComp.NonYautjaRandomFunctionChance = 1f;
                bracerComp.NonYautjaDelimbChance = 0f;
                bracerComp.Charge = 300;
                bracerComp.Regen = 0;
                wasLocked = bracerComp.Locked;

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var holderStored = entMan.GetComponent<YautjaStoredGearComponent>(wristBladesHolder);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.WristBlades, out var defaultWristBlades), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(defaultWristBlades), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, wristBladesHolder), Is.True);

                entMan.EnsureComponent<YautjaComponent>(user);
                var install = new InteractUsingEvent(user, wristBladesHolder, bracer, entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, user, "Left");
                entMan.RemoveComponent<YautjaComponent>(user);
                Assert.That(install.Handled, Is.True);
                Assert.That(holderStored.AttachedWeapon, Is.Not.Null);
                wristBlades = holderStored.AttachedWeapon.Value;

                random.SetSeed(11);

                Assert.That(utility.TryOpenTranslator((bracer, bracerComp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(gearComp.Container.Contains(wristBladesHolder), Is.True,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(hands.IsHolding(user, wristBlades), Is.True,
                        "CMSS13 activate_random_verb() slot 1 routes to attachment_internal(user, TRUE), not the local toggle-lock random-function behavior.");
                    Assert.That(gearComp.Container.Contains(wristBladesHolder), Is.True);
                    Assert.That(bracerComp.Locked, Is.EqualTo(wasLocked),
                        "Local slot 1 must not use the old toggle_lock() random-function behavior.");
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 250),
                        "CMSS13 deploy_bracer_attachments() drains 50 bracer power on deploy.");
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

                foreach (var uid in new[] { user, bracer, wristBladesHolder, wristBlades })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechRandomBracerFunctionSlotThreeTogglesCloakLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
                comp.Charge = 300;
                comp.Regen = 0;
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.False,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.75f));
            await server.WaitPost(() =>
            {
                var random = server.ResolveDependency<IRobustRandom>();
                random.SetSeed(5);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.75f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True,
                        "CMSS13 activate_random_verb() slot 3 routes to cloaker_internal(user, TRUE), not the local crystal fabricator.");
                    Assert.That(comp.Charge, Is.EqualTo((FixedPoint2) 250),
                        "CMSS13 cloaker_internal() drains 50 bracer power when enabling cloak.");
                    Assert.That(hands.GetActiveItem(user), Is.Null,
                        "Local slot 3 must not create a stabilising crystal item.");
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
    public async Task NonTechRandomBracerFunctionSlotFourDeploysCasterLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
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
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.NonYautjaWorkingChance = 0f;
                bracerComp.NonYautjaRandomFunctionChance = 1f;
                bracerComp.NonYautjaDelimbChance = 0f;
                bracerComp.Charge = 300;
                bracerComp.Regen = 0;

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Caster, out caster), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(caster), Is.True);

                random.SetSeed(12);

                Assert.That(utility.TryOpenTranslator((bracer, bracerComp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(gearComp.Container.Contains(caster), Is.True,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);

                var active = hands.GetActiveItem(user);
                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(active, Is.EqualTo(caster),
                        "CMSS13 activate_random_verb() slot 4 routes to caster_internal(user, TRUE), not the local human-crystal fabricator.");
                    Assert.That(gearComp.Container.Contains(caster), Is.False);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 250),
                        "CMSS13 caster_internal() drains 50 bracer power when deploying the caster.");
                    Assert.That(active, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(active!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaPlasmaCaster"));
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

                foreach (var uid in new[] { user, bracer, caster })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechRandomBracerFunctionSlotSevenOpensTranslatorLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;

                random.SetSeed(3);

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, user), Is.False,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(ui.IsUiOpen(bracer, YautjaTranslatorUIKey.Key, user), Is.True,
                        "CMSS13 activate_random_verb() slot 7 routes to translate_internal(user, TRUE).");
                    Assert.That(hands.GetActiveItem(user), Is.Null,
                        "Local slot 7 must not use the old healing capsule random-function behavior.");
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
    public async Task NonTechRandomBracerFunctionSlotEightRemovesAttachmentsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid scimitar = default;
        EntityUid altScimitar = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                scimitar = entMan.SpawnEntity("CMUYautjaScimitar", map.GridCoords);
                altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(user);
                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, altScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(user, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, user, "Left");
                var installAlt = new InteractUsingEvent(user, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
                Assert.That(gearComp.InstalledGear, Does.Contain(scimitar));
                Assert.That(gearComp.InstalledGear, Does.Contain(altScimitar));

                entMan.RemoveComponent<YautjaComponent>(user);
                server.PlayerMan.SetAttachedEntity(session, user);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.NonYautjaWorkingChance = 0f;
                bracerComp.NonYautjaRandomFunctionChance = 1f;
                bracerComp.NonYautjaDelimbChance = 0f;

                random.SetSeed(10);

                Assert.That(utility.TryOpenTranslator((bracer, bracerComp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(gearComp.Container.Contains(scimitar), Is.True,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(hands.IsHolding(user, scimitar), Is.True,
                        "CMSS13 activate_random_verb() slot 8 routes to remove_attachment_internal(user, TRUE).");
                    Assert.That(hands.IsHolding(user, altScimitar), Is.True);
                    Assert.That(gearComp.Container.Contains(scimitar), Is.False);
                    Assert.That(gearComp.Container.Contains(altScimitar), Is.False);
                    Assert.That(gearComp.InstalledGear, Does.Not.Contain(scimitar));
                    Assert.That(gearComp.InstalledGear, Does.Not.Contain(altScimitar));
                    Assert.That(gearComp.SecondaryGear.ContainsKey(YautjaGearKind.Scimitar), Is.False);
                    Assert.That(hands.GetActiveItem(user), Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(hands.GetActiveItem(user)!.Value).EntityPrototype?.ID,
                        Is.Not.EqualTo("CMUYautjaHealingCapsule"),
                        "Local slot 8 must not use the old healing capsule random-function behavior.");
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

                foreach (var uid in new[] { user, bracer, scimitar, altScimitar })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechRandomBracerFunctionSlotFiveCreatesStabilisingCrystalLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;

                random.SetSeed(2);

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(hands.GetActiveItem(user), Is.Null,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                var active = hands.GetActiveItem(user);
                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(active, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(active!.Value).EntityPrototype?.ID,
                        Is.EqualTo("CMUYautjaStabilisingCrystal"),
                        "CMSS13 activate_random_verb() slot 5 routes to injectors_internal(user, TRUE), not the local hunting-trap fabricator.");
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
    public async Task NonTechRandomBracerFunctionSlotSixCallsSmartDiscLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        EntityUid nearbyDisc = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var random = server.ResolveDependency<IRobustRandom>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                nearbyDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(9, 0)));

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.NonYautjaWorkingChance = 0f;
                comp.NonYautjaRandomFunctionChance = 1f;
                comp.NonYautjaDelimbChance = 0f;

                random.SetSeed(6);

                Assert.That(utility.TryOpenTranslator((bracer, comp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(hands.IsHolding(user, nearbyDisc), Is.False,
                    "CMSS13 check_random_function() delays random slot effects until after do_after(user, 3, INTERRUPT_ALL).");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.Zero);
                    Assert.That(hands.IsHolding(user, nearbyDisc), Is.True,
                        "CMSS13 activate_random_verb() slot 6 routes to call_disc_internal(user, TRUE).");
                    Assert.That(hands.GetActiveItem(user), Is.EqualTo(nearbyDisc),
                        "Local slot 6 must not use the old healing capsule random-function behavior.");
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

                foreach (var uid in new[] { user, bracer, nearbyDisc })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechCloakedBracerShockUsesCmss13PopupAndBurnDamage()
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

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, user);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.User = user;
                comp.NonYautjaCloakShockChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
                comp.NextNonYautjaCloakShock = TimeSpan.Zero;
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);

                utility.Update(0.1f);

                var damage = entMan.GetComponent<DamageableComponent>(user).Damage;
                Assert.That(damage.DamageDict.TryGetValue("Heat", out var heat), Is.True);
                Assert.That(heat, Is.EqualTo((FixedPoint2) 10));
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The hunting bracer beeps and sends a shock through your body!",
                "The alien technology shocks you violently!");
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
                comp.NonYautjaDelimbChance = 0f;
                comp.NextNonYautjaCloakShock = TimeSpan.Zero;
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);

                utility.Update(0.1f);

                var damage = entMan.GetComponent<DamageableComponent>(user).Damage;
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
    public async Task BracerToggleLockWearerPopupsMatchCmss13LockUnlockText()
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
        EntityUid techHuman = default;
        EntityUid hunterBracer = default;
        EntityUid humanBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                techHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                humanBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techHuman);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(techHuman, humanBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(hunterComp.Locked, Is.True);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "With an angry blare, the bracer releases your forearm.",
                "You unlock the bracer.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(hunterComp.Locked, Is.False);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The bracer clamps securely around your forearm and beeps in a comfortable, familiar way.",
                "You lock the bracer.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, techHuman);

                var humanComp = entMan.GetComponent<YautjaBracerComponent>(humanBracer);
                Assert.That(humanComp.Locked, Is.True);
                Assert.That(utility.TryToggleWornBracerLock((humanBracer, humanComp), techHuman), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The bracer beeps pleasantly, releasing its grip on your forearm.",
                "You unlock the bracer.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, techHuman);

                var humanComp = entMan.GetComponent<YautjaBracerComponent>(humanBracer);
                Assert.That(humanComp.Locked, Is.False);
                Assert.That(utility.TryToggleWornBracerLock((humanBracer, humanComp), techHuman), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "The bracer clamps painfully around your forearm and beeps angrily. It won't come off!",
                "You lock the bracer.");
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

                foreach (var uid in new[] { hunter, techHuman, hunterBracer, humanBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerToggleLockSourceDenialsAndAirReleaseSoundMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid criticalHunter = default;
        EntityUid criticalBracer = default;
        EntityUid ordinaryUser = default;
        EntityUid ordinaryBracer = default;
        EntityUid hunter = default;
        EntityUid hunterBracer = default;
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
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                criticalHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                criticalBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                ordinaryUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                ordinaryBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(criticalHunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(criticalHunter, criticalBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(ordinaryUser, ordinaryBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(criticalHunter, MobState.Critical);
                server.PlayerMan.SetAttachedEntity(session, criticalHunter);

                var criticalComp = entMan.GetComponent<YautjaBracerComponent>(criticalBracer);
                Assert.That(criticalComp.Locked, Is.True);
                Assert.That(utility.TryToggleWornBracerLock((criticalBracer, criticalComp), criticalHunter), Is.False,
                    "CMSS13 toggle_lock() rejects truthy usr.stat before toggling.");
                Assert.That(criticalComp.Locked, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You can't do that right now...");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, ordinaryUser);

                var ordinaryComp = entMan.GetComponent<YautjaBracerComponent>(ordinaryBracer);
                ordinaryComp.NonYautjaWorkingChance = 0f;
                ordinaryComp.NonYautjaRandomFunctionChance = 0f;
                ordinaryComp.NonYautjaDelimbChance = 0f;
                Assert.That(ordinaryComp.Locked, Is.True);
                Assert.That(utility.TryToggleWornBracerLock((ordinaryBracer, ordinaryComp), ordinaryUser), Is.False,
                    "CMSS13 toggle_lock() rejects users without TRAIT_YAUTJA_TECH before random bracer misuse behavior.");
                Assert.That(ordinaryComp.Locked, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You have no idea how to use this...");

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);

                var beforeUnlock = AudioEntities(entMan);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
                Assert.That(hunterComp.Locked, Is.False);
                Assert.That(AudioFileNamesAfter(entMan, beforeUnlock),
                    Does.Contain("/Audio/_RMC14/Medical/air_release.ogg"),
                    "CMSS13 unlock_bracer() plays sound/items/air_release.ogg at volume 15.");

                var beforeLock = AudioEntities(entMan);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
                Assert.That(hunterComp.Locked, Is.True);
                Assert.That(AudioFileNamesAfter(entMan, beforeLock),
                    Does.Contain("/Audio/_RMC14/Medical/air_release.ogg"),
                    "CMSS13 lock_bracer() plays sound/items/air_release.ogg at volume 15.");
            });
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

                foreach (var uid in new[] { criticalHunter, criticalBracer, ordinaryUser, ordinaryBracer, hunter, hunterBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechCloakedBracerShockShowsCmss13VisibleMessageToObservers()
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
        EntityUid observer = default;
        EntityUid bracer = default;
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
                var utility = entMan.System<YautjaBracerUtilitySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                observer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                metadata.SetEntityName(user, "Test Misuser");
                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, observer);

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.User = user;
                comp.NonYautjaCloakShockChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
                comp.NextNonYautjaCloakShock = TimeSpan.Zero;
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);

                utility.Update(0.1f);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.That(labels,
                    Does.Contain("The hunting bracer beeps and sends a shock through Test Misuser's body!"),
                    "CMSS13 shock_user() uses M.visible_message(\"[src] beeps and sends a shock through [M]'s body!\") so nearby observers should see the shock, not only the wearer.");
            });
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

                foreach (var uid in new[] { user, observer, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonTechCloakedBracerShockForcesPainScreamLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var listener = entMan.System<YautjaTestSpeechListenerSystem>();
            var utility = entMan.System<YautjaBracerUtilitySystem>();

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(user);
                listener.Emotes.Clear();

                var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                comp.User = user;
                comp.NonYautjaCloakShockChance = 1f;
                comp.NonYautjaDelimbChance = 0f;
                comp.NextNonYautjaCloakShock = TimeSpan.Zero;
                entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);

                utility.Update(0.1f);

                Assert.That(listener.Emotes, Does.Contain((user, "Scream")),
                    "CMSS13 shock_user() calls M.emote(\"scream\") when the shocked wearer feels pain.");
            }
            finally
            {
                foreach (var uid in new[] { user, bracer })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

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
    public async Task BracerToggleLockPulledDeadHunterTogglesUnlockedBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

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

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(victim);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(victim, victimBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(victim, MobState.Dead);
                Assert.That(pulling.TryStartPull(hunter, victim), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                var victimComp = entMan.GetComponent<YautjaBracerComponent>(victimBracer);
                victimComp.Locked = false;
                entMan.Dirty(victimBracer, victimComp);

                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.True);
                var dialog = entMan.GetComponent<DialogComponent>(hunterBracer);
                Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to unlock this Yautja's bracer?"));

                RaiseDialogOption(entMan, hunterBracer, hunter, "Yes");

                Assert.That(victimComp.Locked, Is.True,
                    "CMSS13 labels grabbed-dead-hunter confirmation as unlock, but calls toggle_lock_internal(), so an already-unlocked victim bracer becomes locked.");
            });
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
    public async Task BracerToggleLockPulledLivingHunterAndMissingBracerUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid livingTarget = default;
        EntityUid missingBracerTarget = default;
        EntityUid hunterBracer = default;
        EntityUid livingTargetBracer = default;
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
                livingTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                missingBracerTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                livingTargetBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(livingTarget);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(livingTarget, livingTargetBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(missingBracerTarget, MobState.Dead);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(pulling.TryStartPull(hunter, livingTarget), Is.True);
                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(hunterBracer), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You cannot unlock the bracer of a living hunter!");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var pulling = entMan.System<PullingSystem>();
                var utility = entMan.System<YautjaBracerUtilitySystem>();

                pulling.TryStopPull(livingTarget, entMan.GetComponent<PullableComponent>(livingTarget), hunter);
                Assert.That(pulling.TryStartPull(hunter, missingBracerTarget), Is.True);
                var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
                Assert.That(utility.TryToggleWornBracerLock((hunterBracer, hunterComp), hunter), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(hunterBracer), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "<b>This Human does not have a bracer attached.</b>");
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

                foreach (var uid in new[] { hunter, livingTarget, missingBracerTarget, hunterBracer, livingTargetBracer })
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
    public async Task BracerStoredGearDeploysAndRetractsSameEntity()
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
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.True);
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
    public async Task BracerAttachmentInstallPreservesAltScimitarOnDeploy()
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
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var originalScimitar), Is.True);
                Assert.That(originalScimitar, Is.Not.EqualTo(altScimitar));

                var interact = new InteractUsingEvent(
                    hunter,
                    altScimitar,
                    bracer,
                    entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, interact);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                Assert.That(interact.Handled, Is.True);
                Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(altScimitar));
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(altScimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(altScimitar))
                    entMan.DeleteEntity(altScimitar);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FirstBracerAttachmentInstallPromptsForSourceLeftRightSlot()
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
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var originalScimitar), Is.True);
                Assert.That(originalScimitar, Is.Not.EqualTo(altScimitar));

                var interact = new InteractUsingEvent(
                    hunter,
                    altScimitar,
                    bracer,
                    entMan.GetComponent<TransformComponent>(bracer).Coordinates);
                entMan.EventBus.RaiseLocalEvent(bracer, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(entMan.TryGetComponent(bracer, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Options));
                Assert.That(dialog.Options, Has.Count.EqualTo(2));
                Assert.That(dialog.Options[0].Text, Is.EqualTo("Right"));
                Assert.That(dialog.Options[1].Text, Is.EqualTo("Left"));
                Assert.That(gearComp.InstalledGear, Does.Not.Contain(altScimitar));
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(altScimitar), Is.False);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);

                entMan.EventBus.RaiseLocalEvent(bracer, new DialogOptionBuiMsg(0)
                {
                    Actor = hunter,
                    UiKey = DialogUiKey.Key,
                });

                Assert.That(gearComp.SecondaryGear.TryGetValue(YautjaGearKind.Scimitar, out var rightScimitar), Is.True);
                Assert.That(rightScimitar, Is.EqualTo(altScimitar));
                Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(originalScimitar));
                Assert.That(gearComp.InstalledGear, Does.Contain(altScimitar));
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(altScimitar))
                    entMan.DeleteEntity(altScimitar);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentInstallDeploysPairedScimitars()
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
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.False);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
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
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentInstallRefusesThirdAttachmentLikeCmss13()
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
            var firstScimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var secondScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var thirdScimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, firstScimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, secondScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installFirst = new InteractUsingEvent(hunter, firstScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installFirst);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installSecond = new InteractUsingEvent(hunter, secondScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installSecond);

                Assert.That(installFirst.Handled, Is.True);
                Assert.That(installSecond.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(firstScimitar), Is.True);
                Assert.That(gearComp.Container.Contains(secondScimitar), Is.True);

                Assert.That(hands.TryPickupAnyHand(hunter, thirdScimitar), Is.True);
                var installThird = new InteractUsingEvent(hunter, thirdScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installThird);

                Assert.Multiple(() =>
                {
                    Assert.That(installThird.Handled, Is.False);
                    Assert.That(hands.IsHolding(hunter, thirdScimitar), Is.True);
                    Assert.That(gearComp.Container.Contains(firstScimitar), Is.True);
                    Assert.That(gearComp.Container.Contains(secondScimitar), Is.True);
                    Assert.That(gearComp.Container.Contains(thirdScimitar), Is.False);
                    Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(firstScimitar));
                    Assert.That(gearComp.SecondaryGear[YautjaGearKind.Scimitar], Is.EqualTo(secondScimitar));
                    Assert.That(gearComp.InstalledGear, Does.Contain(firstScimitar));
                    Assert.That(gearComp.InstalledGear, Does.Contain(secondScimitar));
                    Assert.That(gearComp.InstalledGear, Does.Not.Contain(thirdScimitar));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(firstScimitar))
                    entMan.DeleteEntity(firstScimitar);
                if (!entMan.Deleted(secondScimitar))
                    entMan.DeleteEntity(secondScimitar);
                if (!entMan.Deleted(thirdScimitar))
                    entMan.DeleteEntity(thirdScimitar);
            }
        });

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
                Assert.That(gearComp.InstalledGear, Has.Count.EqualTo(2));

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
                    Assert.That(gearComp.Gear[YautjaGearKind.Shield], Is.Not.EqualTo(thirdAttachment));
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
    public async Task SecondMixedKindBracerAttachmentAutoFillsFreeSourceSlotWithoutPrompt()
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
            var firstAttachment = entMan.SpawnEntity("CMUYautjaScimitarAttachment", MapCoordinates.Nullspace);
            var secondAttachment = entMan.SpawnEntity("CMUYautjaBracerShieldAttachment", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, firstAttachment), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, secondAttachment), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installFirst = new InteractUsingEvent(hunter, firstAttachment, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installFirst);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(installFirst.Handled, Is.True);
                Assert.That(gearComp.Gear[YautjaGearKind.Scimitar], Is.EqualTo(firstAttachment));
                Assert.That(gearComp.InstalledGear, Does.Contain(firstAttachment));

                var installSecond = new InteractUsingEvent(hunter, secondAttachment, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installSecond);

                Assert.Multiple(() =>
                {
                    Assert.That(installSecond.Handled, Is.True);
                    Assert.That(entMan.HasComponent<DialogComponent>(bracer), Is.False,
                        "CMSS13 only prompts left/right when both source bracer attachment slots are empty; the second holder auto-fills the remaining side.");
                    Assert.That(hands.IsHolding(hunter, secondAttachment), Is.False);
                    Assert.That(gearComp.Container, Is.Not.Null);
                    Assert.That(gearComp.Container!.Contains(firstAttachment), Is.True);
                    Assert.That(gearComp.Container.Contains(secondAttachment), Is.True);
                    Assert.That(gearComp.InstalledGear, Does.Contain(secondAttachment));
                    Assert.That(gearComp.SecondaryGear[YautjaGearKind.Shield], Is.EqualTo(secondAttachment));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(firstAttachment))
                    entMan.DeleteEntity(firstAttachment);
                if (!entMan.Deleted(secondAttachment))
                    entMan.DeleteEntity(secondAttachment);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FreshBracerAttachmentDeployConsumesPowerButHasNoDefaultWeaponsLikeCmss13()
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
            var action = entMan.SpawnEntity("CMUActionYautjaToggleWristBlades", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerPower = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerPower.Charge = 300;

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.InstalledGear, Is.Empty);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.WristBlades, out var defaultWristBlades), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(defaultWristBlades), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var deploy = NewToggleWristBladesEvent(hunter, action, actionComp);
                entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                Assert.Multiple(() =>
                {
                    Assert.That(deploy.Handled, Is.True);
                    Assert.That(bracerPower.Charge, Is.EqualTo((FixedPoint2) 250),
                        "CMSS13 deploy_bracer_attachments() drains 50 before warning that no left/right attachments are installed.");
                    Assert.That(hands.IsHolding(hunter, defaultWristBlades), Is.False,
                        "CMSS13 fresh hunter bracers start with no left_bracer_attachment/right_bracer_attachment and should not deploy a default weapon.");
                    Assert.That(gearComp.Container.Contains(defaultWristBlades), Is.True);
                });
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
            var filler = entMan.SpawnEntity("CMUYautjaDuellingKnife", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleShield", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickup(hunter, filler, HandIdForLocation(hands, hunter, HandLocation.Left)), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                var shield = gearComp.Gear[YautjaGearKind.Shield];

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
                    Assert.That(entMan.GetComponent<YautjaStoredGearComponent>(shield).Deployed, Is.True);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 250));
                });

                var blockingComp = entMan.GetComponent<BlockingComponent>(shield);
                Assert.That(blocking.StartBlocking(shield, blockingComp, hunter), Is.True,
                    "The deployed bracer shield must support active blocking.");
                Assert.That(blockingComp.IsBlocking, Is.True);
                Assert.That(blocking.StopBlocking(shield, blockingComp, hunter), Is.True);
                Assert.That(blockingComp.IsBlocking, Is.False);

                var retract = new YautjaToggleShieldActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, retract);

                Assert.Multiple(() =>
                {
                    Assert.That(retract.Handled, Is.True);
                    Assert.That(gearComp.Container.Contains(shield), Is.True);
                    Assert.That(entMan.GetComponent<YautjaStoredGearComponent>(shield).Deployed, Is.False);
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
                if (!entMan.Deleted(filler))
                    entMan.DeleteEntity(filler);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShieldAttachmentUsesTheOtherFreeHandLikeCmss13()
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
            var holder = entMan.SpawnEntity("CMUYautjaBracerShieldAttachment", MapCoordinates.Nullspace);
            var filler = entMan.SpawnEntity("CMUYautjaDuellingKnife", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleShield", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var install = new InteractUsingEvent(hunter, holder, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, install);
                RaiseDialogOption(entMan, bracer, hunter, "Left");

                Assert.That(install.Handled, Is.True);
                var stored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
                Assert.That(stored.AttachedWeapon, Is.Not.Null);

                var leftHand = HandIdForLocation(hands, hunter, HandLocation.Left);
                Assert.That(hands.TryPickup(hunter, filler, leftHand), Is.True);

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
                    AssertHeldInHandLocation(hands, hunter, stored.AttachedWeapon!.Value, HandLocation.Right);
                    Assert.That(stored.Deployed, Is.True);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 250));
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
            var rightScimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var leftScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, rightScimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, leftScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installRight = new InteractUsingEvent(hunter, rightScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installRight);
                RaiseDialogOption(entMan, bracer, hunter, "Right");
                var installLeft = new InteractUsingEvent(hunter, leftScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installLeft);

                Assert.That(installRight.Handled, Is.True);
                Assert.That(installLeft.Handled, Is.True);

                var rightHand = HandIdForLocation(hands, hunter, HandLocation.Right);
                hands.TrySetActiveHand(hunter, rightHand);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                AssertHeldInHandLocation(hands, hunter, rightScimitar, HandLocation.Right);
                AssertHeldInHandLocation(hands, hunter, leftScimitar, HandLocation.Left);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(rightScimitar))
                    entMan.DeleteEntity(rightScimitar);
                if (!entMan.Deleted(leftScimitar))
                    entMan.DeleteEntity(leftScimitar);
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
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.False);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(scimitar, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                    Assert.That(hands.IsHolding(hunter, altScimitar), Is.False);
                    Assert.That(gearComp.Container.Contains(scimitar), Is.True);
                    Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
                });
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
                Assert.That(hands.TrySetActiveHand(hunter, scimitarHand), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(scimitar));

                doors.SetState(airlock, DoorState.Closed);
                doors.SetState(resinDoor, DoorState.Closed);
                Assert.That(entMan.GetComponent<DamageableComponent>(airlock).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.HasComponent<ResinDoorComponent>(resinDoor), Is.True);

                var airlockForce = new InteractUsingEvent(hunter, scimitar, airlock, entMan.GetComponent<TransformComponent>(airlock).Coordinates);
                entMan.EventBus.RaiseLocalEvent(airlock, airlockForce);

                Assert.Multiple(() =>
                {
                    Assert.That(airlockForce.Handled, Is.True);
                    Assert.That(entMan.GetComponent<DoorComponent>(airlock).State, Is.EqualTo(DoorState.Closed));
                    Assert.That(entMan.GetComponent<DamageableComponent>(airlock).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
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
                    Assert.That(entMan.GetComponent<DamageableComponent>(airlock).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
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
    public async Task BracerAttachmentWeaponsDoNotForceResinDoorsInHarmIntentLikeCmss13Afterattack()
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
                var combatMode = entMan.System<SharedCombatModeSystem>();
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
                Assert.That(hands.TrySetActiveHand(hunter, scimitarHand), Is.True);
                combatMode.SetInCombatMode(hunter, true);

                doors.SetState(airlock, DoorState.Closed);
                doors.SetState(resinDoor, DoorState.Closed);

                var resinForceOpen = new InteractUsingEvent(hunter, scimitar, resinDoor, entMan.GetComponent<TransformComponent>(resinDoor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(resinDoor, resinForceOpen);

                Assert.That(entMan.GetComponent<DoorComponent>(resinDoor).State, Is.EqualTo(DoorState.Closed));

                var airlockForce = new InteractUsingEvent(hunter, scimitar, airlock, entMan.GetComponent<TransformComponent>(airlock).Coordinates);
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
    public async Task BracerRemoveAttachmentsReturnsInstalledPairLikeCmss13()
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
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaRemoveBracerAttachments", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);
                Assert.That(gearComp.InstalledGear, Does.Contain(scimitar));
                Assert.That(gearComp.InstalledGear, Does.Contain(altScimitar));

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var remove = new YautjaRemoveBracerAttachmentsActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, remove);

                Assert.That(remove.Handled, Is.True);
                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(hands.IsHolding(hunter, altScimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.False);
                Assert.That(gearComp.InstalledGear, Does.Not.Contain(scimitar));
                Assert.That(gearComp.InstalledGear, Does.Not.Contain(altScimitar));
                Assert.That(gearComp.SecondaryGear.ContainsKey(YautjaGearKind.Scimitar), Is.False);
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
            }
        });

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
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);
            var fillerLeft = entMan.SpawnEntity("Crowbar", MapCoordinates.Nullspace);
            var fillerRight = entMan.SpawnEntity("CMMRE", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaRemoveBracerAttachments", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                var bracerCoords = entMan.GetComponent<TransformComponent>(bracer).Coordinates;
                var installScimitar = new InteractUsingEvent(hunter, scimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installScimitar);
                RaiseDialogOption(entMan, bracer, hunter, "Left");
                var installAlt = new InteractUsingEvent(hunter, altScimitar, bracer, bracerCoords);
                entMan.EventBus.RaiseLocalEvent(bracer, installAlt);

                Assert.That(installScimitar.Handled, Is.True);
                Assert.That(installAlt.Handled, Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(altScimitar), Is.True);

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
    public async Task BracerAttachmentMessagesUseSourceItemAndBracerNames()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);

            try
            {
                var bracerName = entMan.GetComponent<MetaDataComponent>(bracer).EntityName;
                var scimitarName = entMan.GetComponent<MetaDataComponent>(scimitar).EntityName;

                Assert.That(
                    Loc.GetString("cmu-yautja-bracer-attachment-installed", ("item", scimitar), ("bracer", bracer)),
                    Is.EqualTo($"You attach {scimitarName} to {bracerName}."));
                Assert.That(
                    Loc.GetString("cmu-yautja-bracer-attachment-removed", ("item", scimitar), ("bracer", bracer)),
                    Is.EqualTo($"You remove {scimitarName} from {bracerName}."));
                Assert.That(
                    Loc.GetString("cmu-yautja-bracer-attachments-retract-first"),
                    Is.EqualTo("Retract your attachments First!"));
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                entMan.DeleteEntity(bracer);
                entMan.DeleteEntity(scimitar);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void BracerAttachmentInstallAndRemoveSoundsMatchCmss13()
    {
        var component = new YautjaGearContainerComponent();

        Assert.Multiple(() =>
        {
            AssertSoundPath(component.InstallAttachmentSound, "/Audio/_CMU14/Yautja/Equipment/pred_attach.wav");
            AssertSoundPath(component.RemoveAttachmentSound, "/Audio/_RMC14/Machines/click.ogg");
        });
    }

    [Test]
    public async Task CombistickGrantsFoldActionOnlyInYautjaHands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var groundEvent = new GetItemActionsEvent(actions, hunter, combistick, SlotFlags.BACK);
                entMan.EventBus.RaiseLocalEvent(combistick, groundEvent);
                Assert.That(groundEvent.Actions, Is.Empty);

                var nonYautjaEvent = new GetItemActionsEvent(actions, nonYautja, combistick);
                entMan.EventBus.RaiseLocalEvent(combistick, nonYautjaEvent);
                Assert.That(nonYautjaEvent.Actions, Is.Empty);

                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);

                var heldEvent = new GetItemActionsEvent(actions, hunter, combistick);
                entMan.EventBus.RaiseLocalEvent(combistick, heldEvent);

                Assert.That(heldEvent.Actions.Select(action =>
                    entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID), Does.Contain("CMUActionYautjaFoldCombistick"));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(nonYautja))
                    entMan.DeleteEntity(nonYautja);
                if (!entMan.Deleted(combistick))
                    entMan.DeleteEntity(combistick);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FoldCombistickTogglesCmss13StorageState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var appearance = entMan.System<SharedAppearanceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaFoldCombistick", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);

                var stick = entMan.GetComponent<YautjaCombistickComponent>(combistick);
                var toggle = entMan.GetComponent<ItemToggleComponent>(combistick);
                var item = entMan.GetComponent<ItemComponent>(combistick);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(combistick);
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var sourceBlock = entMan.GetComponent<YautjaSourceShieldBlockComponent>(combistick);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Activated, Is.True);
                    Assert.That(sourceBlock.ReadiedBlock, Is.EqualTo(YautjaSourceShieldChance.High),
                        "CMSS13 combistick starts extended with shield_chance = SHIELD_CHANCE_HIGH.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("combistick"));
                    Assert.That(item.Size, Is.EqualTo("Large"),
                        "CMSS13 combistick starts extended with w_class = SIZE_LARGE.");
                    Assert.That(melee.Damage.DamageDict["Piercing"].Double(), Is.EqualTo(10).Within(0.001),
                        "CMSS13 combistick unique_action() extension uses force_unwielded = MELEE_FORCE_TIER_2 until attack_self() wields it.");
                });

                var fold = new YautjaFoldCombistickActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(combistick, fold);

                Assert.Multiple(() =>
                {
                    Assert.That(fold.Handled, Is.True);
                    Assert.That(toggle.Activated, Is.False);
                    Assert.That(item.HeldPrefix, Is.EqualTo("combistick_folded"));
                    Assert.That(item.Size, Is.EqualTo("Tiny"));
                    Assert.That(sourceBlock.ReadiedBlock, Is.EqualTo(YautjaSourceShieldChance.None),
                        "CMSS13 collapse sets combistick shield_chance = SHIELD_CHANCE_NONE.");
                    Assert.That(melee.Damage.DamageDict["Blunt"].Double(), Is.EqualTo(5).Within(0.001),
                        "CMSS13 folded combistick force_storage = MELEE_FORCE_TIER_1, mapped locally to 5 total damage.");
                    Assert.That(appearance.TryGetData<bool>(combistick, ToggleableVisuals.Enabled, out var enabled), Is.True);
                    Assert.That(enabled, Is.False);
                });

                var extend = new YautjaFoldCombistickActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(combistick, extend);

                Assert.Multiple(() =>
                {
                    Assert.That(extend.Handled, Is.True);
                    Assert.That(toggle.Activated, Is.True);
                    Assert.That(sourceBlock.ReadiedBlock, Is.EqualTo(YautjaSourceShieldChance.High),
                        "CMSS13 extending restores active_shield_chance = SHIELD_CHANCE_HIGH.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("combistick"));
                    Assert.That(item.Size, Is.EqualTo("Large"),
                        "CMSS13 combistick extends back to w_class = SIZE_LARGE.");
                    Assert.That(melee.Damage.DamageDict["Piercing"].Double(), Is.EqualTo(10).Within(0.001),
                        "CMSS13 extending restores force_unwielded = MELEE_FORCE_TIER_2; attack_self()/wield raises it to force_wielded.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(combistick))
                    entMan.DeleteEntity(combistick);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackerActionsAreGrantedToWornHunterBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var nonYautjaEvent = new GetItemActionsEvent(actions, nonYautja, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, nonYautjaEvent);
                var nonYautjaActionIds = nonYautjaEvent.Actions
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(nonYautjaActionIds, Does.Not.Contain("CMUActionYautjaTrackGear"));
                    Assert.That(nonYautjaActionIds, Does.Not.Contain("CMUActionYautjaAddTrackedItem"));
                    Assert.That(nonYautjaActionIds, Does.Not.Contain("CMUActionYautjaRemoveTrackedItem"));
                });

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    var migratedActionIds = new[]
                    {
                        "CMUActionYautjaChangeExplosionType",
                        "CMUActionYautjaRemoveBracerAttachments",
                        "CMUActionYautjaCreateHealingCapsule",
                        "CMUActionYautjaAddTrackedItem",
                        "CMUActionYautjaRemoveTrackedItem",
                        "CMUActionYautjaToggleBracerName",
                        "CMUActionYautjaToggleBracerNotificationSound",
                    };

                    Assert.That(actionIds, Does.Contain("CMUActionYautjaOpenBracerMenu"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleCloak"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaRecall"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCallDisc"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaTrackGear"),
                        "The worn bracer should keep the tracker action off the action bar.");
                    foreach (var migratedActionId in migratedActionIds)
                        Assert.That(actionIds, Does.Not.Contain(migratedActionId), $"{migratedActionId} belongs to the bracer menu.");
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaOpenMarkPanel"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaSelfDestruct"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaTranslator"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleBracerIdChip"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaLinkThrallBracer"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaTransmitThrallMessage"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCreateStabilisingCrystal"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCreateHumanStabilisingCrystal"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCreateHuntingTrap"));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(nonYautja);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMenuRoutesNotificationSoundCommandThroughGuardedUtility()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.NotificationSound = true;

                ui.RaiseUiMessage(bracer, YautjaBracerUIKey.Key,
                    new YautjaBracerPanelCommandMsg(YautjaBracerPanelCommand.ToggleBracerNotificationSound)
                    {
                        Actor = hunter,
                    });

                Assert.That(bracerComp.NotificationSound, Is.False,
                    "The worn-bracer BUI command must route through TryToggleNotificationSound.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackerReportsDeadYautjaBiosignaturesLikeCmss13TrackGearInternal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var deadYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 3)));
            var livingYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 4)));
            var action = entMan.SpawnEntity("CMUActionYautjaTrackGear", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(deadYautja, MobState.Dead);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTrackGearActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True);
                Assert.That(state!.TrackedGear, Has.Count.EqualTo(1));

                var signature = state.TrackedGear.Single();
                Assert.Multiple(() =>
                {
                    Assert.That(signature.Name, Is.EqualTo("deceased Yautja bio signature"));
                    Assert.That(signature.Distance, Is.EqualTo(3));
                    Assert.That(signature.Count, Is.EqualTo(1));
                    Assert.That(signature.Name, Does.Not.Contain(entMan.GetComponent<MetaDataComponent>(livingYautja).EntityName));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(deadYautja))
                    entMan.DeleteEntity(deadYautja);
                if (!entMan.Deleted(livingYautja))
                    entMan.DeleteEntity(livingYautja);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackerCountsOffMapSignaturesInsteadOfDroppingThem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var groundMap = await pair.CreateTestMap();
        var orbitMap = await pair.CreateTestMap();
        var lowOrbitMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            entMan.EnsureComponent<RMCPlanetComponent>(groundMap.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMMobHuman", groundMap.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", groundMap.GridCoords);
            var groundDead = entMan.SpawnEntity("CMUMobYautja", groundMap.GridCoords.Offset(new Vector2(0, 3)));
            var orbitDead = entMan.SpawnEntity("CMUMobYautja", orbitMap.GridCoords);
            var lowOrbitDead = entMan.SpawnEntity("CMUMobYautja", lowOrbitMap.GridCoords);
            var orbitGear = entMan.SpawnEntity("CMM11Knife", orbitMap.GridCoords);
            var lowOrbitGear = entMan.SpawnEntity("CMM11Knife", lowOrbitMap.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaTrackGear", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(orbitGear);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(lowOrbitGear);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(groundDead, MobState.Dead);
                mobState.ChangeMobState(orbitDead, MobState.Dead);
                mobState.ChangeMobState(lowOrbitDead, MobState.Dead);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTrackGearActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True);

                var readout = state!.TrackerReadout;
                Assert.Multiple(() =>
                {
                    Assert.That(readout.DeadHuntingGrounds, Is.EqualTo(1));
                    Assert.That(readout.DeadOrbit + readout.DeadLowOrbit, Is.EqualTo(2));
                    Assert.That(readout.GearHuntingGrounds, Is.EqualTo(0));
                    Assert.That(readout.GearOrbit + readout.GearLowOrbit, Is.EqualTo(2));
                    Assert.That(state.TrackedGear, Has.Count.EqualTo(1));
                    Assert.That(state.TrackedGear.Single().Name, Is.EqualTo("deceased Yautja bio signature"));
                    Assert.That(readout.GetCmss13ReadoutLines().First(), Does.Contain("<b>1</b> in the hunting grounds"));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(groundDead))
                    entMan.DeleteEntity(groundDead);
                if (!entMan.Deleted(orbitDead))
                    entMan.DeleteEntity(orbitDead);
                if (!entMan.Deleted(lowOrbitDead))
                    entMan.DeleteEntity(lowOrbitDead);
                if (!entMan.Deleted(orbitGear))
                    entMan.DeleteEntity(orbitGear);
                if (!entMan.Deleted(lowOrbitGear))
                    entMan.DeleteEntity(lowOrbitGear);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackerGroupsSameTileGearAndKeepsNamedClosestLikeCmss13GearFirstPass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var gearA = entMan.SpawnEntity("CMM11Knife", map.GridCoords.Offset(new Vector2(0, 3)));
            var gearB = entMan.SpawnEntity("CMM11Knife", map.GridCoords.Offset(new Vector2(0, 3)));
            var tiedDeadYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var action = entMan.SpawnEntity("CMUActionYautjaTrackGear", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(gearA);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(gearB);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(tiedDeadYautja, MobState.Dead);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTrackGearActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True);

                var readout = state!.TrackerReadout;
                var groupedGear = state.TrackedGear.SingleOrDefault(entry => entry.Count == 2);
                Assert.Multiple(() =>
                {
                    Assert.That(readout.GearHuntingGrounds, Is.EqualTo(2));
                    Assert.That(readout.DeadHuntingGrounds, Is.EqualTo(1));
                    Assert.That(readout.ClosestPresent, Is.True);
                    Assert.That(readout.ClosestName, Is.Not.Null);
                    Assert.That(readout.ClosestDistance, Is.EqualTo(3));
                    Assert.That(state.TrackedGear, Has.Count.EqualTo(2));
                    Assert.That(groupedGear, Is.Not.Null);
                    Assert.That(groupedGear!.Distance, Is.EqualTo(3));
                    Assert.That(groupedGear.Name, Does.Not.Contain("deceased Yautja bio signature"));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(gearA))
                    entMan.DeleteEntity(gearA);
                if (!entMan.Deleted(gearB))
                    entMan.DeleteEntity(gearB);
                if (!entMan.Deleted(tiedDeadYautja))
                    entMan.DeleteEntity(tiedDeadYautja);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackerSkipsAnchoredTrackedGearLikeCmss13TrackGearInternal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var anchoredGear = entMan.SpawnEntity("CMM11Knife", map.GridCoords.Offset(new Vector2(0, 2)));
            var looseGear = entMan.SpawnEntity("CMM11Knife", map.GridCoords.Offset(new Vector2(0, 5)));
            var unlinkedCombistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords.Offset(new Vector2(0, 4)));
            var action = entMan.SpawnEntity("CMUActionYautjaTrackGear", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(anchoredGear);
                entMan.EnsureComponent<YautjaTrackedItemComponent>(looseGear);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                ForceAnchor(entMan, transform, anchoredGear);
                Assert.That(entMan.GetComponent<TransformComponent>(anchoredGear).Anchored, Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTrackGearActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaBracerPanelState>(bracer, YautjaBracerUIKey.Key, out var state), Is.True);

                var readout = state!.TrackerReadout;
                var combistickName = entMan.GetComponent<MetaDataComponent>(unlinkedCombistick).EntityName;
                Assert.Multiple(() =>
                {
                    Assert.That(readout.GearHuntingGrounds, Is.EqualTo(1),
                        "CMSS13 track_gear_internal() continues when tracked_item.anchored before counting gear buckets.");
                    Assert.That(readout.ClosestPresent, Is.True);
                    Assert.That(readout.ClosestDistance, Is.EqualTo(5),
                        "Anchored tracked gear must not win the same-z closest-signature pass.");
                    Assert.That(state.TrackedGear, Has.Count.EqualTo(1));
                    Assert.That(state.TrackedGear.Single().Distance, Is.EqualTo(5));
                    Assert.That(state.TrackedGear.Select(entry => entry.Name), Does.Not.Contain(combistickName));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(anchoredGear))
                    entMan.DeleteEntity(anchoredGear);
                if (!entMan.Deleted(looseGear))
                    entMan.DeleteEntity(looseGear);
                if (!entMan.Deleted(unlinkedCombistick))
                    entMan.DeleteEntity(unlinkedCombistick);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void BracerTrackerAreaBucketRulesMatchCmss13ZLevelReadoutBuckets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea("almayer", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea("warship", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea("bush", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea(null, "RMCAreaAlmayerBriefing"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea("fax", "RMCAreaFaxExterior"), Is.False);

            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea("ert", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea("fax", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea("faxexterior", "RMCAreaSomething"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea(null, "RMCAreaSpace"), Is.True);
            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea("almayer", "RMCAreaAlmayerBriefing"), Is.False);

            Assert.That(YautjaBracerMenuSystem.IsCmss13MainshipTrackerArea("colony", "RMCAreaLv624Caves"), Is.False);
            Assert.That(YautjaBracerMenuSystem.IsCmss13LowOrbitTrackerArea("colony", "RMCAreaLv624Caves"), Is.False);
        });
    }

    [Test]
    public async Task BracerTrackerReadoutFormatsCmss13TrackGearInternalText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var loc = server.ResolveDependency<ILocalizationManager>();
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            });

            await server.WaitAssertion(() =>
            {
                var readout = new YautjaTrackerReadout(
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    true,
                    "smart-disc",
                    17,
                    1,
                    30,
                    "Briefing");

                var lines = readout.GetCmss13ReadoutLines();

                Assert.Multiple(() =>
                {
                    Assert.That(lines, Has.Count.EqualTo(3));
                    Assert.That(lines[0], Is.EqualTo("Your bracer shows a readout of deceased Yautja bio signatures, <b>1</b> in the hunting grounds, <b>2</b> in orbit, <b>3</b> in low orbit."));
                    Assert.That(lines[1], Is.EqualTo("Your bracer shows a readout of Yautja technology signatures, <b>4</b> in the hunting grounds, <b>5</b> in orbit, <b>6</b> in low orbit."));
                    Assert.That(lines[2], Is.EqualTo("The closest signature, a <b>smart-disc</b>, is approximately <b>10</b> paces <b>northeast</b> in <b>Briefing</b>."));
                });

                var direct = new YautjaTrackerReadout(0, 0, 0, 0, 0, 0, true, null, 0, 0, 0, null)
                    .GetCmss13ReadoutLines();
                Assert.That(direct.Single(), Is.EqualTo("You are directly on top of the signature."));

                var empty = new YautjaTrackerReadout(0, 0, 0, 0, 0, 0, false, null, 0, 0, 0, null)
                    .GetCmss13ReadoutLines();
                Assert.That(empty.Single(), Is.EqualTo("There are no signatures that require your attention."));

                var deadLowOrbitOnly = new YautjaTrackerReadout(0, 0, 2, 0, 0, 0, false, null, 0, 0, 0, null)
                    .GetCmss13ReadoutLines();
                Assert.That(deadLowOrbitOnly.Single(), Is.EqualTo("Your bracer shows a readout of deceased Yautja bio signatures, <b>2</b> in low orbit."));

                var gearOrbitOnly = new YautjaTrackerReadout(0, 0, 0, 0, 2, 0, false, null, 0, 0, 0, null)
                    .GetCmss13ReadoutLines();
                Assert.That(gearOrbitOnly.Single(), Is.EqualTo("Your bracer shows a readout of Yautja technology signatures, <b>2</b> in orbit."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (previousCulture != null)
                    server.ResolveDependency<ILocalizationManager>().SetCulture(previousCulture);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAddAndRemoveTrackedItemMatchCmss13ActiveHandRule()
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
            var item = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);
            var addAction = entMan.SpawnEntity("CMUActionYautjaAddTrackedItem", MapCoordinates.Nullspace);
            var removeAction = entMan.SpawnEntity("CMUActionYautjaRemoveTrackedItem", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, item), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(item));
                Assert.That(entMan.HasComponent<YautjaTechItemComponent>(item), Is.True);
                Assert.That(entMan.HasComponent<YautjaTrackedItemComponent>(item), Is.False);

                var addComp = entMan.GetComponent<ActionComponent>(addAction);
                var add = new YautjaAddTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (addAction, addComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, add);

                Assert.Multiple(() =>
                {
                    Assert.That(add.Handled, Is.True);
                    Assert.That(entMan.HasComponent<YautjaTrackedItemComponent>(item), Is.True);
                    Assert.That(entMan.HasComponent<YautjaTrackedItemComponent>(bracer), Is.False);
                    Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(item));
                });

                var removeComp = entMan.GetComponent<ActionComponent>(removeAction);
                var remove = new YautjaRemoveTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (removeAction, removeComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, remove);

                Assert.Multiple(() =>
                {
                    Assert.That(remove.Handled, Is.True);
                    Assert.That(entMan.HasComponent<YautjaTrackedItemComponent>(item), Is.False);
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(item), Is.True);
                    Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(item));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(item))
                    entMan.DeleteEntity(item);
                if (!entMan.Deleted(addAction))
                    entMan.DeleteEntity(addAction);
                if (!entMan.Deleted(removeAction))
                    entMan.DeleteEntity(removeAction);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackedItemEmptyHandWarningsUseCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var addAction = entMan.SpawnEntity("CMUActionYautjaAddTrackedItem", MapCoordinates.Nullspace);
                var add = new YautjaAddTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (addAction, entMan.GetComponent<ActionComponent>(addAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, add);
                entMan.DeleteEntity(addAction);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You need the item in your active hand to remove it from the tracker!",
                "You need the item in your active hand to add or remove it from the tracker!");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var removeAction = entMan.SpawnEntity("CMUActionYautjaRemoveTrackedItem", MapCoordinates.Nullspace);
                var remove = new YautjaRemoveTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (removeAction, entMan.GetComponent<ActionComponent>(removeAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, remove);
                entMan.DeleteEntity(removeAction);
            });

            await pair.ReallyBeIdle(10);
            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                const string expected = "You need the item in your active hand to remove it from the tracker!";

                Assert.That(labels.Any(label => label == expected || label == $"{expected} x2"), Is.True,
                    $"CMSS13 add_tracked_item/remove_tracked_item share the same active-hand source warning.\nActual labels:\n{joinedLabels}");
                Assert.That(labels, Does.Not.Contain("You need the item in your active hand to add or remove it from the tracker!"),
                    $"Old combined local tracker warning should not be present.\nActual labels:\n{joinedLabels}");
            });
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

                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerTrackedItemTransitionPopupsUseCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid item = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                item = entMan.SpawnEntity("CMM11Knife", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, item), Is.True);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var addAction = entMan.SpawnEntity("CMUActionYautjaAddTrackedItem", MapCoordinates.Nullspace);
                var add = new YautjaAddTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (addAction, entMan.GetComponent<ActionComponent>(addAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, add);
                entMan.DeleteEntity(addAction);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopupMatching(
                client,
                label => label.StartsWith("You add <b>", StringComparison.Ordinal) &&
                         label.EndsWith("</b> to the tracking system.", StringComparison.Ordinal),
                "You add <b>{item}</b> to the tracking system.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var addAction = entMan.SpawnEntity("CMUActionYautjaAddTrackedItem", MapCoordinates.Nullspace);
                var add = new YautjaAddTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (addAction, entMan.GetComponent<ActionComponent>(addAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, add);
                entMan.DeleteEntity(addAction);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopupMatching(
                client,
                label => label.EndsWith("is already being tracked.", StringComparison.Ordinal),
                "{item} is already being tracked.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var removeAction = entMan.SpawnEntity("CMUActionYautjaRemoveTrackedItem", MapCoordinates.Nullspace);
                var remove = new YautjaRemoveTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (removeAction, entMan.GetComponent<ActionComponent>(removeAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, remove);
                entMan.DeleteEntity(removeAction);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopupMatching(
                client,
                label => label.StartsWith("You remove <b>", StringComparison.Ordinal) &&
                         label.EndsWith("</b> from the tracking system.", StringComparison.Ordinal),
                "You remove <b>{item}</b> from the tracking system.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var removeAction = entMan.SpawnEntity("CMUActionYautjaRemoveTrackedItem", MapCoordinates.Nullspace);
                var remove = new YautjaRemoveTrackedItemActionEvent
                {
                    Performer = hunter,
                    Action = (removeAction, entMan.GetComponent<ActionComponent>(removeAction)),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, remove);
                entMan.DeleteEntity(removeAction);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopupMatching(
                client,
                label => label.EndsWith("isn't on the tracking system.", StringComparison.Ordinal),
                "{item} isn't on the tracking system.");
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

                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(item))
                    entMan.DeleteEntity(item);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerNotificationSoundToggleIsMenuOnlyWhileHandlerRemainsAvailable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerNotificationSound", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();
                Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleBracerNotificationSound"));

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(bracerComp.NotificationSound, Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleBracerNotificationSoundActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Handled, Is.True);
                    Assert.That(bracerComp.NotificationSound, Is.False);
                });

                toggle = new YautjaToggleBracerNotificationSoundActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Handled, Is.True);
                    Assert.That(bracerComp.NotificationSound, Is.True);
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
    public async Task BracerNameToggleIsMenuOnlyWhileHandlerControlsYautjaIdentity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var metadata = entMan.System<MetaDataSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var viewer = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerName", MapCoordinates.Nullspace);

            try
            {
                metadata.SetEntityName(hunter, "A'ke Ret");
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(viewer);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();
                Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleBracerName"));

                Assert.That(Identity.Name(hunter, entMan, viewer).Name, Is.EqualTo("A'ke Ret"));
                Assert.That(Identity.Name(hunter, entMan, nonYautja).Name, Is.EqualTo(Loc.GetString("cmu-yautja-identity-unknown")));

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                var yautja = entMan.GetComponent<YautjaComponent>(hunter);
                Assert.That(yautja.BracerNameActive, Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleBracerNameActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Handled, Is.True);
                    Assert.That(yautja.BracerNameActive, Is.False);
                    Assert.That(Identity.Name(hunter, entMan, viewer).Name, Is.EqualTo(Loc.GetString("cmu-yautja-identity-unknown")));
                });

                toggle = new YautjaToggleBracerNameActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(bracer, toggle);

                Assert.Multiple(() =>
                {
                    Assert.That(toggle.Handled, Is.True);
                    Assert.That(yautja.BracerNameActive, Is.True);
                    Assert.That(Identity.Name(hunter, entMan, viewer).Name, Is.EqualTo("A'ke Ret"));
                    Assert.That(bracerComp.User, Is.EqualTo(hunter));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(viewer);
                entMan.DeleteEntity(nonYautja);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerInitializesEmbeddedIdChipAndBadBloodVariantLikeCmss13Initialize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var containers = entMan.System<SharedContainerSystem>();

            var ordinaryBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var badBloodBracer = entMan.SpawnEntity("CMUYautjaBadBloodBracer", MapCoordinates.Nullspace);

            try
            {
                AssertEmbeddedIdChip(
                    entMan,
                    containers,
                    ordinaryBracer,
                    "CMUYautjaBracerIdChip",
                    expectedAccess: new[] { "CMUAccessYautjaSecure" },
                    expectedBadBlood: false);

                AssertEmbeddedIdChip(
                    entMan,
                    containers,
                    badBloodBracer,
                    "CMUYautjaBadBloodBracerIdChip",
                    expectedAccess: new[] { "CMUAccessYautjaBadBlood" },
                    expectedBadBlood: true);
            }
            finally
            {
                if (!entMan.Deleted(ordinaryBracer))
                    entMan.DeleteEntity(ordinaryBracer);
                if (!entMan.Deleted(badBloodBracer))
                    entMan.DeleteEntity(badBloodBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodBracerEquipSkipsCmss13AutoLockLikeSourceEquipped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var ordinaryHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var badBloodHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var badBloodFactionHunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ordinaryBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var badBloodBracer = entMan.SpawnEntity("CMUYautjaBadBloodBracer", MapCoordinates.Nullspace);
            var badBloodFactionBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(ordinaryHunter);
                entMan.EnsureComponent<YautjaComponent>(badBloodHunter);
                entMan.EnsureComponent<YautjaComponent>(badBloodFactionHunter);
                entMan.EnsureComponent<NpcFactionMemberComponent>(badBloodFactionHunter).Factions.Add("CMUYautjaBadBlood");

                var ordinary = entMan.GetComponent<YautjaBracerComponent>(ordinaryBracer);
                var badBlood = entMan.GetComponent<YautjaBracerComponent>(badBloodBracer);
                var badBloodFaction = entMan.GetComponent<YautjaBracerComponent>(badBloodFactionBracer);
                ordinary.Locked = false;
                badBlood.Locked = false;
                badBloodFaction.Locked = false;

                Assert.That(inventory.TryEquip(ordinaryHunter, ordinaryBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(badBloodHunter, badBloodBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(badBloodFactionHunter, badBloodFactionBracer, "gloves", silent: true, force: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(ordinary.User, Is.EqualTo(ordinaryHunter),
                        "CMSS13 equipped(WEAR_HANDS) still sets owner before auto-locking normal hunter bracers.");
                    Assert.That(ordinary.Locked, Is.True,
                        "CMSS13 normal hunter bracers call toggle_lock_internal(user, TRUE) when equipped in hands.");
                    Assert.That(badBlood.User, Is.EqualTo(badBloodHunter),
                        "CMSS13 badblood bracers still set owner before returning from equipped().");
                    Assert.That(badBlood.BadBlood, Is.True);
                    Assert.That(badBlood.Locked, Is.False,
                        "CMSS13 badblood bracers return before toggle_lock_internal(user, TRUE), so equip must not force-lock them.");
                    Assert.That(badBloodFaction.User, Is.EqualTo(badBloodFactionHunter),
                        "CMSS13 Bad Blood faction wearers still become the owner of ordinary bracers.");
                    Assert.That(badBloodFaction.BadBlood, Is.False,
                        "This branch is owner.faction == FACTION_YAUTJA_BADBLOOD, not the bracer badblood subtype.");
                    Assert.That(badBloodFaction.Locked, Is.False,
                        "CMSS13 owner.faction == FACTION_YAUTJA_BADBLOOD returns before toggle_lock_internal(user, TRUE).");
                });
            }
            finally
            {
                foreach (var uid in new[] { ordinaryHunter, badBloodHunter, badBloodFactionHunter, ordinaryBracer, badBloodBracer, badBloodFactionBracer })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
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

    private static void AssertEmbeddedIdChip(
        IEntityManager entMan,
        SharedContainerSystem containers,
        EntityUid bracer,
        string expectedPrototype,
        IReadOnlyCollection<string> expectedAccess,
        bool expectedBadBlood)
    {
        var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

        Assert.Multiple(() =>
        {
            Assert.That(bracerComp.BadBlood, Is.EqualTo(expectedBadBlood));
            Assert.That(bracerComp.IdChipPrototype.Id, Is.EqualTo(expectedPrototype));
            Assert.That(bracerComp.IdChip, Is.Not.Null);
            Assert.That(bracerComp.IdChipDeployed, Is.False);
        });

        var chip = bracerComp.IdChip!.Value;
        Assert.Multiple(() =>
        {
            Assert.That(entMan.Deleted(chip), Is.False);
            Assert.That(entMan.HasComponent<YautjaBracerIdChipComponent>(chip), Is.True);
            Assert.That(entMan.GetComponent<MetaDataComponent>(chip).EntityPrototype?.ID, Is.EqualTo(expectedPrototype));
            Assert.That(containers.TryGetContainer(bracer, bracerComp.IdChipContainerId, out var container), Is.True);
            Assert.That(((ContainerSlot) container!).ContainedEntity, Is.EqualTo(chip));
            Assert.That(
                entMan.GetComponent<AccessComponent>(chip).Tags.Select(tag => tag.Id),
                Is.EquivalentTo(expectedAccess));
        });
    }

    [Test]
    public async Task BracerIdChipActionIsPanelOnlyWhenWornButGrantedWhenHeldLikeCmss13Keybind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var wornBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, wornBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(heldBracer));

                var wornEvent = new GetItemActionsEvent(actions, hunter, wornBracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(wornBracer, wornEvent);
                var wornActionIds = wornEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                var heldEvent = new GetItemActionsEvent(actions, hunter, heldBracer);
                entMan.EventBus.RaiseLocalEvent(heldBracer, heldEvent);
                var heldActionIds = heldEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(wornActionIds, Does.Not.Contain("CMUActionYautjaToggleBracerIdChip"));
                    Assert.That(heldActionIds, Does.Contain("CMUActionYautjaToggleBracerIdChip"));
                    Assert.That(heldActionIds, Does.Not.Contain("CMUActionYautjaChangeExplosionType"));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(wornBracer))
                    entMan.DeleteEntity(wornBracer);
                if (!entMan.Deleted(heldBracer))
                    entMan.DeleteEntity(heldBracer);
            }
        });

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
    public async Task BracerIdChipAppliesCmss13OwnerRankAccessAndUserDataOnDeploy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var metadata = entMan.System<MetaDataSystem>();
            var inventory = entMan.System<InventorySystem>();

            var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerIdChip", MapCoordinates.Nullspace);
            var spawned = new List<EntityUid> { action };

            try
            {
                var actionComp = entMan.GetComponent<ActionComponent>(action);

                foreach (var row in new[]
                         {
                             (YautjaBracerOwnerRank.Unblooded, "CMUYautjaBracer", new[] { "CMUAccessYautjaSecure" }),
                             (YautjaBracerOwnerRank.Elite, "CMUYautjaBracer", new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite" }),
                             (YautjaBracerOwnerRank.Elder, "CMUYautjaBracer", new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder" }),
                             (YautjaBracerOwnerRank.Leader, "CMUYautjaBracer", new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder", "CMUAccessYautjaLeader" }),
                             (YautjaBracerOwnerRank.Admin, "CMUYautjaBracer", new[] { "CMUAccessYautjaSecure", "CMUAccessYautjaElite", "CMUAccessYautjaElder", "CMUAccessYautjaLeader", "CMUAccessYautjaAncient" }),
                             (YautjaBracerOwnerRank.Admin, "CMUYautjaBadBloodBracer", new[] { "CMUAccessYautjaBadBlood" }),
                         })
                {
                    var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                    var bracer = entMan.SpawnEntity(row.Item2, MapCoordinates.Nullspace);
                    spawned.Add(hunter);
                    spawned.Add(bracer);

                    metadata.SetEntityName(hunter, "A'ke Ret");
                    entMan.EnsureComponent<YautjaComponent>(hunter);

                    var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                    bracerComp.OwnerRank = row.Item1;
                    Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                    var deploy = new YautjaToggleBracerIdChipActionEvent
                    {
                        Performer = hunter,
                        Action = (action, actionComp),
                    };
                    entMan.EventBus.RaiseLocalEvent(bracer, deploy);

                    var chip = bracerComp.IdChip!.Value;
                    Assert.Multiple(() =>
                    {
                        Assert.That(deploy.Handled, Is.True, row.Item2);
                        Assert.That(bracerComp.IdChipDeployed, Is.True, row.Item2);
                        Assert.That(inventory.TryGetSlotEntity(hunter, "id", out var id), Is.True, row.Item2);
                        Assert.That(id, Is.EqualTo(chip), row.Item2);
                        Assert.That(entMan.GetComponent<IdCardComponent>(chip).FullName, Is.EqualTo("A'ke Ret"), row.Item2);
                        Assert.That(entMan.GetComponent<MetaDataComponent>(chip).EntityName, Does.Contain("A'ke Ret"), row.Item2);
                        Assert.That(
                            entMan.GetComponent<AccessComponent>(chip).Tags.Select(tag => tag.Id),
                            Is.EquivalentTo(row.Item3),
                            $"{row.Item2} {row.Item1} CMSS13 bracer_chip set_user_data access");
                    });

                    var retract = new YautjaToggleBracerIdChipActionEvent
                    {
                        Performer = hunter,
                        Action = (action, actionComp),
                    };
                    entMan.EventBus.RaiseLocalEvent(bracer, retract);

                    Assert.Multiple(() =>
                    {
                        Assert.That(retract.Handled, Is.True, row.Item2);
                        Assert.That(bracerComp.IdChipDeployed, Is.False, row.Item2);
                        Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var worn), Is.True, row.Item2);
                        Assert.That(worn, Is.EqualTo(bracer), row.Item2);
                        Assert.That(inventory.TryGetSlotEntity(hunter, "id", out _), Is.False, row.Item2);
                    });
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
    public async Task BracerLinkThrallActionIsPanelOnlyWhenWornButGrantedWhenHeldLikeCmss13Keybind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var wornBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, wornBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(heldBracer));

                var wornEvent = new GetItemActionsEvent(actions, hunter, wornBracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(wornBracer, wornEvent);
                var wornActionIds = wornEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                var heldEvent = new GetItemActionsEvent(actions, hunter, heldBracer);
                entMan.EventBus.RaiseLocalEvent(heldBracer, heldEvent);
                var heldActionIds = heldEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(wornActionIds, Does.Not.Contain("CMUActionYautjaLinkThrallBracer"));
                    Assert.That(heldActionIds, Does.Contain("CMUActionYautjaLinkThrallBracer"));
                    Assert.That(heldActionIds, Does.Not.Contain("CMUActionYautjaChangeExplosionType"));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(wornBracer))
                    entMan.DeleteEntity(wornBracer);
                if (!entMan.Deleted(heldBracer))
                    entMan.DeleteEntity(heldBracer);
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
    public async Task WornBracerLinkThrallInitializesCmss13VendorCategories()
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

                Assert.That(link.Handled, Is.True);
                Assert.That(entMan.TryGetComponent(thrall, out CMVendorUserComponent? vendorUser), Is.True,
                    "CMSS13 link_bracer() assigns thrall.vendor_buyable_categories = YAUTJA_CAN_BUY_ALL.");

                var expected = Cmss13YautjaClaimCategoryLimits();
                Assert.That(vendorUser!.Choices.Count, Is.EqualTo(expected.Count));
                foreach (var category in expected.Keys)
                {
                    Assert.That(vendorUser.Choices.GetValueOrDefault(category), Is.Zero,
                        $"{category} should start with no local thrall claims consumed.");
                }
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
    public async Task HeldBracerLinkThrallBracerConsumesKeybindButDoesNotLinkLikeCmss13Verb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaLinkThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(hands.TryPickupAnyHand(master, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(master), Is.EqualTo(heldBracer));
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var link = new YautjaLinkThrallBracerActionEvent
                {
                    Performer = master,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(heldBracer, link);

                var heldBracerComp = entMan.GetComponent<YautjaBracerComponent>(heldBracer);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(link.Handled, Is.True,
                        "CMSS13 bracer_hunter/link_bracer/down() consumes a held hunter bracer keybind path before link_bracer() refuses the unworn bracer.");
                    Assert.That(heldBracerComp.User, Is.Null);
                    Assert.That(thrallComp.BracerLinked, Is.False);
                    Assert.That(thrallComp.MasterBracer, Is.Null);
                    Assert.That(thrallComp.ThrallBracer, Is.Null);
                    Assert.That(thrallBracerComp.Linked, Is.False);
                    Assert.That(thrallBracerComp.MasterBracer, Is.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(heldBracer))
                    entMan.DeleteEntity(heldBracer);
                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerChangeExplosionTypeIsMenuOnlyForWornAndHeldHunterBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var wornBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, wornBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(heldBracer));

                var wornEvent = new GetItemActionsEvent(actions, hunter, wornBracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(wornBracer, wornEvent);
                var wornActionIds = wornEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                var heldEvent = new GetItemActionsEvent(actions, hunter, heldBracer);
                entMan.EventBus.RaiseLocalEvent(heldBracer, heldEvent);
                var heldActionIds = heldEvent.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(wornActionIds, Does.Not.Contain("CMUActionYautjaChangeExplosionType"));
                    Assert.That(heldActionIds, Does.Not.Contain("CMUActionYautjaChangeExplosionType"));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(wornBracer))
                    entMan.DeleteEntity(wornBracer);
                if (!entMan.Deleted(heldBracer))
                    entMan.DeleteEntity(heldBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerChangeExplosionTypeCyclesAndLocksSmallActiveSelfDestructLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var wornBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaChangeExplosionType", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, wornBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(heldBracer));

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var wornBracerComp = entMan.GetComponent<YautjaBracerComponent>(wornBracer);
                var heldBracerComp = entMan.GetComponent<YautjaBracerComponent>(heldBracer);

                Assert.That(wornBracerComp.SelfDestructExplosionType, Is.EqualTo(YautjaSelfDestructExplosionType.Small),
                    "CMSS13 hunter bracer defaults explosion_type to SD_TYPE_SMALL (1).");

                var switchWornToBig = new YautjaChangeExplosionTypeActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(wornBracer, switchWornToBig);

                var switchWornToSmall = new YautjaChangeExplosionTypeActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(wornBracer, switchWornToSmall);

                heldBracerComp.SelfDestructArmed = true;
                heldBracerComp.SelfDestructExplosionType = YautjaSelfDestructExplosionType.Small;
                var blockedHeldSmall = new YautjaChangeExplosionTypeActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(heldBracer, blockedHeldSmall);

                heldBracerComp.SelfDestructArmed = true;
                heldBracerComp.SelfDestructExplosionType = YautjaSelfDestructExplosionType.Big;
                var heldBigToSmall = new YautjaChangeExplosionTypeActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(heldBracer, heldBigToSmall);

                Assert.Multiple(() =>
                {
                    Assert.That(switchWornToBig.Handled, Is.True);
                    Assert.That(switchWornToSmall.Handled, Is.True);
                    Assert.That(blockedHeldSmall.Handled, Is.True,
                        "CMSS13 bracer_hunter/change_explosion_type/down() consumes a held hunter bracer keybind path before change_explosion_type() refuses active small self-destruct.");
                    Assert.That(heldBigToSmall.Handled, Is.True);
                    Assert.That(wornBracerComp.SelfDestructExplosionType, Is.EqualTo(YautjaSelfDestructExplosionType.Small));
                    Assert.That(heldBracerComp.SelfDestructExplosionType, Is.EqualTo(YautjaSelfDestructExplosionType.Small));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                foreach (var uid in new[] { wornBracer, heldBracer, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerInjectorsActionIsPanelOnlyForWornHunterBracerLikeCmss13SignalAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCreateStabilisingCrystal"),
                    "The worn hunter bracer should now expose injectors through the bracer panel instead of a standalone action-bar button.");
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
    public async Task BracerCapsuleActionIsMenuOnlyForWornHunterBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaCreateHealingCapsule"));
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
    public async Task BracerRecallActionIsLocalizedAndSoleDiscAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionNames = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid))
                    .Where(meta => meta.EntityPrototype?.ID is "CMUActionYautjaRecall" or "CMUActionYautjaCallDisc")
                    .ToDictionary(meta => meta.EntityPrototype!.ID, meta => meta.EntityName);

                Assert.Multiple(() =>
                {
                    Assert.That(actionNames, Contains.Key("CMUActionYautjaRecall"));
                    Assert.That(actionNames, Does.Not.ContainKey("CMUActionYautjaCallDisc"));
                });
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
    public async Task CallDiscActionRecallsNearbySmartDiscWithCmss13RangeAndPower()
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
            var nearbyDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(9, 0)));
            var outOfRangeDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(11, 0)));
            var action = entMan.SpawnEntity("CMUActionYautjaCallDisc", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 300;
                var outOfRangeStart = transform.GetMapCoordinates(outOfRangeDisc);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var call = new YautjaCallDiscActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(bracer, call);

                var outOfRangeEnd = transform.GetMapCoordinates(outOfRangeDisc);
                Assert.Multiple(() =>
                {
                    Assert.That(call.Handled, Is.True);
                    Assert.That(bracerComp.Charge, Is.EqualTo((FixedPoint2) 230));
                    Assert.That(hands.IsHolding(hunter, nearbyDisc), Is.True);
                    Assert.That(hands.IsHolding(hunter, outOfRangeDisc), Is.False);
                    Assert.That(outOfRangeEnd.MapId, Is.EqualTo(outOfRangeStart.MapId));
                    Assert.That(outOfRangeEnd.Position, Is.EqualTo(outOfRangeStart.Position));
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                foreach (var uid in new[] { bracer, nearbyDisc, outOfRangeDisc, action })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppedChainedWeaponGrantsCallCombiActionLikeCmss13SetupChain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedActionsSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, combistick), Is.True);

                Assert.That(hands.TryDrop(hunter, combistick, checkActionBlocker: false), Is.True);

                var actionIds = actions.GetActions(hunter)
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaCallCombi"));
                    Assert.That(entMan.GetComponent<YautjaChainedWeaponComponent>(combistick).LinkedTo, Is.EqualTo(hunter),
                        "CMSS13 /obj/item/weapon/yautja/chained/dropped() calls setup_chain(user), storing linked_to and granting /datum/action/predator_action/bracer/chained.");
                });
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                foreach (var uid in new[] { bracer, combistick })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
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
    public async Task BracerMessageActionIsPanelOnlyForWornHunterBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaTransmitThrallMessage"));
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
    public async Task BracerMessageDoesNotOpenWhenThrallIsNotWearingBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var thralls = entMan.System<YautjaThrallSystem>();
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, bracer, "gloves", silent: true, force: true), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.That(thralls.TryOpenMasterThrallTransmission((bracer, bracerComp), master), Is.False,
                    "CMSS13 bracer_message() refuses to prompt when the receiver is not wearing a Yautja bracer.");
                Assert.That(Loc.GetString("cmu-yautja-thrall-message-no-bracer-thrall"),
                    Is.EqualTo("Your thrall isn't wearing their bracer!"));
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMessageDoesNotOpenWhenMasterHasNoThrallLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var thralls = entMan.System<YautjaThrallSystem>();
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                Assert.That(inventory.TryEquip(master, bracer, "gloves", silent: true, force: true), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.That(thralls.TryOpenMasterThrallTransmission((bracer, bracerComp), master), Is.False,
                    "CMSS13 bracer_message() refuses to prompt when the messenger has no receiver.");
                Assert.That(Loc.GetString("cmu-yautja-thrall-message-none"),
                    Is.EqualTo("You have no one to message!"));
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                entMan.DeleteEntity(master);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallBracerMessageDoesNotOpenWhenMasterIsNotWearingBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();
            var loc = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = loc.DefaultCulture;
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaTransmitThrallMessage", MapCoordinates.Nullspace);

            try
            {
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallBracerComp.Linked = true;

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTransmitThrallMessageActionEvent
                {
                    Performer = thrall,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(thrallBracer, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(thrallBracer, YautjaThrallMessageUIKey.Key, thrall), Is.False,
                        "CMSS13 bracer_message() refuses to prompt when the master receiver is not wearing a Yautja bracer.");
                    Assert.That(Loc.GetString("cmu-yautja-thrall-message-no-bracer-master"),
                        Is.EqualTo("Your master isn't wearing their bracer!"));
                });
            }
            finally
            {
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);

                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMessagePlaysSenderAndReceiverNotificationSoundsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var thralls = entMan.System<YautjaThrallSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var masterBracerComp = entMan.GetComponent<YautjaBracerComponent>(masterBracer);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.That(thralls.TryOpenMasterThrallTransmission((masterBracer, masterBracerComp), master), Is.True);

                var beforeBoth = CountAudio(entMan);
                ui.RaiseUiMessage(masterBracer, YautjaThrallMessageUIKey.Key,
                    new YautjaThrallSendMessageMsg("both enabled") { Actor = master });
                Assert.That(CountAudio(entMan), Is.EqualTo(beforeBoth + 2),
                    "CMSS13 plays notification sounds on both the sender bracer and receiver bracer when both are enabled.");

                masterBracerComp.NotificationSound = false;
                thrallBracerComp.NotificationSound = true;

                var beforeReceiverOnly = CountAudio(entMan);
                ui.RaiseUiMessage(masterBracer, YautjaThrallMessageUIKey.Key,
                    new YautjaThrallSendMessageMsg("receiver only") { Actor = master });
                Assert.That(CountAudio(entMan), Is.EqualTo(beforeReceiverOnly + 1),
                    "CMSS13 checks receiver_gloves.notification_sound independently of the sender bracer.");
            }
            finally
            {
                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(masterBracer))
                    entMan.DeleteEntity(masterBracer);
                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMessageUsesPredBracerSoundInsteadOfMasterLockSoundLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var thralls = entMan.System<YautjaThrallSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var masterBracerComp = entMan.GetComponent<YautjaBracerComponent>(masterBracer);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                masterBracerComp.LockSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
                masterBracerComp.NotificationSound = true;
                thrallBracerComp.NotificationSound = false;

                Assert.That(thralls.TryOpenMasterThrallTransmission((masterBracer, masterBracerComp), master), Is.True);

                var before = AudioEntities(entMan);
                ui.RaiseUiMessage(masterBracer, YautjaThrallMessageUIKey.Key,
                    new YautjaThrallSendMessageMsg("sound source") { Actor = master });
                var audio = AudioFileNamesAfter(entMan, before);

                Assert.Multiple(() =>
                {
                    Assert.That(audio, Does.Contain("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav"),
                        "CMSS13 bracer_message() always plays sound/items/pred_bracer.ogg for bracer message notifications.");
                    Assert.That(audio, Does.Not.Contain("/Audio/Machines/button.ogg"),
                        "Message notifications must not accidentally use the local lock sound field.");
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
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMessageAcceptsAnyWornYautjaReceiverBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var thralls = entMan.System<YautjaThrallSystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var receiverBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, receiverBracer, "gloves", silent: true, force: true), Is.True);

                var masterBracerComp = entMan.GetComponent<YautjaBracerComponent>(masterBracer);
                Assert.That(thralls.TryOpenMasterThrallTransmission((masterBracer, masterBracerComp), master), Is.True,
                    "CMSS13 bracer_message() accepts any /obj/item/clothing/gloves/yautja on the receiver, not only the thrall subtype.");
            }
            finally
            {
                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(masterBracer))
                    entMan.DeleteEntity(masterBracer);
                if (!entMan.Deleted(receiverBracer))
                    entMan.DeleteEntity(receiverBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerMessageOpensForDeadThrallReceiverLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var thralls = entMan.System<YautjaThrallSystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
                mobState.ChangeMobState(thrall, MobState.Dead);

                var masterBracerComp = entMan.GetComponent<YautjaBracerComponent>(masterBracer);
                Assert.That(thralls.TryOpenMasterThrallTransmission((masterBracer, masterBracerComp), master), Is.True,
                    "CMSS13 bracer_message() uses hunter_data.thrall and does not reject a dead receiver before checking their worn bracer.");
            }
            finally
            {
                entMan.DeleteEntity(master);
                entMan.DeleteEntity(thrall);

                if (!entMan.Deleted(masterBracer))
                    entMan.DeleteEntity(masterBracer);
                if (!entMan.Deleted(thrallBracer))
                    entMan.DeleteEntity(thrallBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallBracerMessageUsesHunterDataWithoutLocalLinkFlagLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var master = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var masterBracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaTransmitThrallMessage", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(master);
                var thrallComp = entMan.EnsureComponent<YautjaThrallComponent>(thrall);
                thrallComp.Master = master;

                Assert.That(inventory.TryEquip(master, masterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.That(thrallBracerComp.Linked, Is.False);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaTransmitThrallMessageActionEvent
                {
                    Performer = thrall,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(thrallBracer, ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(thrallBracer, YautjaThrallMessageUIKey.Key, thrall), Is.True,
                        "CMSS13 bracer_message() chooses the master from hunter_data.thralled_set and does not require linked_bracer.");
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
    public async Task ChainGauntletGrantsGuardActionOnlyInYautjaHands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var nonYautja = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var groundEvent = new GetItemActionsEvent(actions, hunter, gauntlet, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(gauntlet, groundEvent);
                Assert.That(groundEvent.Actions, Is.Empty);

                var nonYautjaEvent = new GetItemActionsEvent(actions, nonYautja, gauntlet);
                entMan.EventBus.RaiseLocalEvent(gauntlet, nonYautjaEvent);
                Assert.That(nonYautjaEvent.Actions, Is.Empty);

                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

                var heldEvent = new GetItemActionsEvent(actions, hunter, gauntlet);
                entMan.EventBus.RaiseLocalEvent(gauntlet, heldEvent);

                Assert.That(heldEvent.Actions.Select(action =>
                    entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID), Does.Contain("CMUActionYautjaGuardChainGauntlet"));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(nonYautja))
                    entMan.DeleteEntity(nonYautja);
                if (!entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainGauntletBasePrototypeMatchesCmss13SourceStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(gauntlet);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(gauntlet);

                Assert.That(meta.EntityDescription, Is.EqualTo("Gauntlets made out of alien alloy, chains wrapped around it imply this was made for hand to hand combat, with some range."));
                Assert.That(melee.AttackRate, Is.EqualTo(1f));
            }
            finally
            {
                if (!entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WristBladesBasePrototypeMatchesCmss13SourceStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wristBlades = entMan.SpawnEntity("CMUYautjaWristBlades", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(wristBlades);
                var item = entMan.GetComponent<ItemComponent>(wristBlades);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(wristBlades);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("wrist blade"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A huge, serrated blade extending from metal gauntlets."));
                    Assert.That(item.Size.Id, Is.EqualTo("Huge"),
                        "CMSS13 /obj/item/weapon/bracer_attachment sets w_class = SIZE_HUGE.");
                    Assert.That(melee.AttackRate, Is.EqualTo(2f),
                        "CMSS13 /obj/item/weapon/bracer_attachment/wristblades sets attack_speed = 0.5 SECONDS.");
                });
            }
            finally
            {
                if (!entMan.Deleted(wristBlades))
                    entMan.DeleteEntity(wristBlades);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainwhipBasePrototypeMatchesCmss13SourceStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var chainwhip = entMan.SpawnEntity("CMUYautjaChainwhip", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(chainwhip);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(chainwhip);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("chainwhip"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A segmented, lightweight whip made of durable, acid-resistant metal. Not very common among Yautja Hunters, but still a dangerous weapon capable of shredding prey."));
                    Assert.That(melee.AttackRate, Is.EqualTo(1.25f),
                        "CMSS13 /obj/item/weapon/yautja/chain sets attack_speed = 0.8 SECONDS.");
                });
            }
            finally
            {
                if (!entMan.Deleted(chainwhip))
                    entMan.DeleteEntity(chainwhip);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WarScytheBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dualScythe = entMan.SpawnEntity("CMUYautjaDualWarScythe", MapCoordinates.Nullspace);
            var doubleScythe = entMan.SpawnEntity("CMUYautjaDoubleWarScythe", MapCoordinates.Nullspace);

            try
            {
                var dualMeta = entMan.GetComponent<MetaDataComponent>(dualScythe);
                var doubleMeta = entMan.GetComponent<MetaDataComponent>(doubleScythe);

                Assert.Multiple(() =>
                {
                    Assert.That(dualMeta.EntityName, Is.EqualTo("dual war scythe"));
                    Assert.That(dualMeta.EntityDescription, Is.EqualTo("A huge, incredibly sharp dual blade used for hunting dangerous prey. This weapon is commonly carried by Yautja who wish to disable and slice apart their foes."));
                    Assert.That(doubleMeta.EntityName, Is.EqualTo("double war scythe"));
                    Assert.That(doubleMeta.EntityDescription, Is.EqualTo("A huge, incredibly sharp double blade used for hunting dangerous prey. This weapon is commonly carried by Yautja who wish to disable and slice apart their foes."));
                });
            }
            finally
            {
                if (!entMan.Deleted(dualScythe))
                    entMan.DeleteEntity(dualScythe);
                if (!entMan.Deleted(doubleScythe))
                    entMan.DeleteEntity(doubleScythe);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CruelStaffBasePrototypeMatchesCmss13SourceDescription()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var staff = entMan.SpawnEntity("CMUYautjaCruelStaff", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(staff);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("cruel staff"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A wicked and battered staff wrapped in worn crimson rags. A crescent shaped blade adorns the top, while the bottom is rounded and blunt."));
                });
            }
            finally
            {
                if (!entMan.Deleted(staff))
                    entMan.DeleteEntity(staff);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChainedMainWeaponBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var combistick = entMan.SpawnEntity("CMUYautjaCombistick", MapCoordinates.Nullspace);
            var warAxe = entMan.SpawnEntity("CMUYautjaWarAxe", MapCoordinates.Nullspace);

            try
            {
                var combistickMeta = entMan.GetComponent<MetaDataComponent>(combistick);
                var warAxeMeta = entMan.GetComponent<MetaDataComponent>(warAxe);

                Assert.Multiple(() =>
                {
                    Assert.That(combistickMeta.EntityName, Is.EqualTo("combi-stick"));
                    Assert.That(combistickMeta.EntityDescription, Is.EqualTo("A compact yet deadly personal weapon. Can be concealed when folded. Functions well as a throwing weapon or defensive tool. A common sight in Yautja packs due to its versatility."));
                    Assert.That(warAxeMeta.EntityName, Is.EqualTo("war axe"));
                    Assert.That(warAxeMeta.EntityDescription, Is.EqualTo("A swift weapon designed to gouge and gore the hunter's prey. A chain is attached to the hilt, allowing for a quick retrieval."));
                });
            }
            finally
            {
                if (!entMan.Deleted(combistick))
                    entMan.DeleteEntity(combistick);
                if (!entMan.Deleted(warAxe))
                    entMan.DeleteEntity(warAxe);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GlaiveBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var warGlaive = entMan.SpawnEntity("CMUYautjaWarGlaive", MapCoordinates.Nullspace);
            var cleavingGlaive = entMan.SpawnEntity("CMUYautjaCleavingGlaive", MapCoordinates.Nullspace);
            var longaxe = entMan.SpawnEntity("CMUYautjaLongaxe", MapCoordinates.Nullspace);

            try
            {
                var warGlaiveMeta = entMan.GetComponent<MetaDataComponent>(warGlaive);
                var cleavingGlaiveMeta = entMan.GetComponent<MetaDataComponent>(cleavingGlaive);
                var longaxeMeta = entMan.GetComponent<MetaDataComponent>(longaxe);

                Assert.Multiple(() =>
                {
                    Assert.That(warGlaiveMeta.EntityName, Is.EqualTo("war glaive"));
                    Assert.That(warGlaiveMeta.EntityDescription, Is.EqualTo("Two huge, powerful blades on a metallic pole. Mysterious writing is carved into the weapon."));
                    Assert.That(cleavingGlaiveMeta.EntityName, Is.EqualTo("cleaving glaive"));
                    Assert.That(cleavingGlaiveMeta.EntityDescription, Is.EqualTo("A huge, powerful blade on a metallic pole. Mysterious writing is carved into the weapon."));
                    Assert.That(longaxeMeta.EntityName, Is.EqualTo("longaxe"));
                    Assert.That(longaxeMeta.EntityDescription, Is.EqualTo("A frighteningly big axe. The blade edge is chipped and gnarled from thousands of bone-crushing blows."));
                });
            }
            finally
            {
                if (!entMan.Deleted(warGlaive))
                    entMan.DeleteEntity(warGlaive);
                if (!entMan.Deleted(cleavingGlaive))
                    entMan.DeleteEntity(cleavingGlaive);
                if (!entMan.Deleted(longaxe))
                    entMan.DeleteEntity(longaxe);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DuellingWeaponBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blade = entMan.SpawnEntity("CMUYautjaDuellingBlade", MapCoordinates.Nullspace);
            var club = entMan.SpawnEntity("CMUYautjaDuellingClub", MapCoordinates.Nullspace);
            var hatchet = entMan.SpawnEntity("CMUYautjaDuellingHatchet", MapCoordinates.Nullspace);
            var knife = entMan.SpawnEntity("CMUYautjaDuellingKnife", MapCoordinates.Nullspace);

            try
            {
                var bladeMeta = entMan.GetComponent<MetaDataComponent>(blade);
                var clubMeta = entMan.GetComponent<MetaDataComponent>(club);
                var hatchetMeta = entMan.GetComponent<MetaDataComponent>(hatchet);
                var knifeMeta = entMan.GetComponent<MetaDataComponent>(knife);

                Assert.Multiple(() =>
                {
                    Assert.That(bladeMeta.EntityName, Is.EqualTo("duelling blade"));
                    Assert.That(bladeMeta.EntityDescription, Is.EqualTo("A primitive yet deadly sword used in yautja rituals and duels. Though crude compared to their advanced weaponry, its sharp edge demands respect."));
                    Assert.That(clubMeta.EntityName, Is.EqualTo("duelling club"));
                    Assert.That(clubMeta.EntityDescription, Is.EqualTo("A crude metal club adorned with a skull. Used as a non-lethal training weapon for young yautja honing their combat skills."));
                    Assert.That(hatchetMeta.EntityName, Is.EqualTo("duelling hatchet"));
                    Assert.That(hatchetMeta.EntityDescription, Is.EqualTo("A short ceremonial duelling hatchet. Designed for ritual combat or settling disputes among Yautja. It features a keen edge capable of cleaving flesh or bone. Though smaller than traditional Yautja weapons."));
                    Assert.That(knifeMeta.EntityName, Is.EqualTo("duelling knife"));
                    Assert.That(knifeMeta.EntityDescription, Is.EqualTo("A length of leather-bound wood studded with razor-sharp teeth. How crude."));
                });
            }
            finally
            {
                if (!entMan.Deleted(blade))
                    entMan.DeleteEntity(blade);
                if (!entMan.Deleted(club))
                    entMan.DeleteEntity(club);
                if (!entMan.Deleted(hatchet))
                    entMan.DeleteEntity(hatchet);
                if (!entMan.Deleted(knife))
                    entMan.DeleteEntity(knife);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RangedWeaponBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spikeLauncher = entMan.SpawnEntity("CMUYautjaSpikeLauncher", MapCoordinates.Nullspace);
            var plasmaRifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", MapCoordinates.Nullspace);
            var plasmaPistol = entMan.SpawnEntity("CMUYautjaPlasmaPistol", MapCoordinates.Nullspace);

            try
            {
                var spikeMeta = entMan.GetComponent<MetaDataComponent>(spikeLauncher);
                var rifleMeta = entMan.GetComponent<MetaDataComponent>(plasmaRifle);
                var pistolMeta = entMan.GetComponent<MetaDataComponent>(plasmaPistol);

                Assert.Multiple(() =>
                {
                    Assert.That(spikeMeta.EntityName, Is.EqualTo("spike launcher"));
                    Assert.That(spikeMeta.EntityDescription, Is.EqualTo("A compact Yautja device in the shape of a crescent. It can rapidly fire damaging spikes and automatically recharges."));
                    Assert.That(rifleMeta.EntityName, Is.EqualTo("plasma rifle"));
                    Assert.That(rifleMeta.EntityDescription, Is.EqualTo("A long-barreled heavy plasma weapon. Intended for combat, not hunting. Has an integrated battery that allows for a functionally unlimited amount of shots to be discharged. Equipped with an internal gyroscopic stabilizer allowing its operator to fire the weapon one-handed if desired."));
                    Assert.That(pistolMeta.EntityName, Is.EqualTo("plasma pistol"));
                    Assert.That(pistolMeta.EntityDescription, Is.EqualTo("A plasma pistol capable of rapid fire. It has an integrated battery. Can be used to set fires, either to braziers or on people."));
                });
            }
            finally
            {
                if (!entMan.Deleted(spikeLauncher))
                    entMan.DeleteEntity(spikeLauncher);
                if (!entMan.Deleted(plasmaRifle))
                    entMan.DeleteEntity(plasmaRifle);
                if (!entMan.Deleted(plasmaPistol))
                    entMan.DeleteEntity(plasmaPistol);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BowArrowBasePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bow = entMan.SpawnEntity("CMUYautjaHuntingBow", MapCoordinates.Nullspace);
            var arrow = entMan.SpawnEntity("CMUYautjaArrow", MapCoordinates.Nullspace);
            var explosiveArrow = entMan.SpawnEntity("CMUYautjaExplosiveArrowActive", MapCoordinates.Nullspace);
            var empInertArrow = entMan.SpawnEntity("CMUYautjaEmpArrow", MapCoordinates.Nullspace);
            var empArrow = entMan.SpawnEntity("CMUYautjaEmpArrowActive", MapCoordinates.Nullspace);
            var dynamicArrow = entMan.SpawnEntity("CMUYautjaDynamicArrow", MapCoordinates.Nullspace);
            var snareArrow = entMan.SpawnEntity("CMUYautjaSnareArrow", MapCoordinates.Nullspace);
            var quiver = entMan.SpawnEntity("CMUYautjaQuiverStrap", MapCoordinates.Nullspace);

            try
            {
                var bowMeta = entMan.GetComponent<MetaDataComponent>(bow);
                var arrowMeta = entMan.GetComponent<MetaDataComponent>(arrow);
                var explosiveArrowMeta = entMan.GetComponent<MetaDataComponent>(explosiveArrow);
                var empInertArrowMeta = entMan.GetComponent<MetaDataComponent>(empInertArrow);
                var empArrowMeta = entMan.GetComponent<MetaDataComponent>(empArrow);
                var dynamicArrowMeta = entMan.GetComponent<MetaDataComponent>(dynamicArrow);
                var snareArrowMeta = entMan.GetComponent<MetaDataComponent>(snareArrow);
                var quiverMeta = entMan.GetComponent<MetaDataComponent>(quiver);

                Assert.Multiple(() =>
                {
                    Assert.That(bowMeta.EntityName, Is.EqualTo("hunting bow"));
                    Assert.That(bowMeta.EntityDescription, Is.EqualTo("An abnormal-sized weapon with an exceptionally tight string. Requires extraordinary strength to draw."));
                    Assert.That(arrowMeta.EntityName, Is.EqualTo("inert arrow"));
                    Assert.That(arrowMeta.EntityDescription, Is.EqualTo("A heavy arrow made of a strange metal. Used by alien hunters with powerful bows."));
                    Assert.That(explosiveArrowMeta.EntityName, Is.EqualTo("activated explosive arrow"));
                    Assert.That(explosiveArrowMeta.EntityDescription, Is.EqualTo("A heavy arrow made of a strange metal. Used by alien hunters with powerful bows."));
                    Assert.That(empInertArrowMeta.EntityName, Is.EqualTo("inert arrow"));
                    Assert.That(empInertArrowMeta.EntityDescription, Is.EqualTo("A heavy arrow made of a strange metal. Used by alien hunters with powerful bows."));
                    Assert.That(empArrowMeta.EntityName, Is.EqualTo("activated emp arrow"));
                    Assert.That(empArrowMeta.EntityDescription, Is.EqualTo("A heavy arrow made of a strange metal. Used by alien hunters with powerful bows."));
                    Assert.That(dynamicArrowMeta.EntityName, Is.EqualTo("inert dynamic arrow"));
                    Assert.That(dynamicArrowMeta.EntityDescription, Is.EqualTo("A heavy arrow made of a strange metal. Used by alien hunters with powerful bows."));
                    Assert.That(snareArrowMeta.EntityName, Is.EqualTo("snare arrow"));
                    Assert.That(snareArrowMeta.EntityDescription, Is.EqualTo("A bow launched snare."));
                    Assert.That(quiverMeta.EntityName, Is.EqualTo("quiver strap"));
                    Assert.That(quiverMeta.EntityDescription, Is.EqualTo("A strap that can hold a bow with a quiver for arrows."));
                });
            }
            finally
            {
                if (!entMan.Deleted(bow))
                    entMan.DeleteEntity(bow);
                if (!entMan.Deleted(arrow))
                    entMan.DeleteEntity(arrow);
                if (!entMan.Deleted(explosiveArrow))
                    entMan.DeleteEntity(explosiveArrow);
                if (!entMan.Deleted(empInertArrow))
                    entMan.DeleteEntity(empInertArrow);
                if (!entMan.Deleted(empArrow))
                    entMan.DeleteEntity(empArrow);
                if (!entMan.Deleted(dynamicArrow))
                    entMan.DeleteEntity(dynamicArrow);
                if (!entMan.Deleted(snareArrow))
                    entMan.DeleteEntity(snareArrow);
                if (!entMan.Deleted(quiver))
                    entMan.DeleteEntity(quiver);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScimitarBasePrototypesMatchCmss13SourceStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);

            try
            {
                var scimitarMeta = entMan.GetComponent<MetaDataComponent>(scimitar);
                var altMeta = entMan.GetComponent<MetaDataComponent>(altScimitar);
                var scimitarMelee = entMan.GetComponent<MeleeWeaponComponent>(scimitar);
                var altMelee = entMan.GetComponent<MeleeWeaponComponent>(altScimitar);

                Assert.Multiple(() =>
                {
                    Assert.That(scimitarMeta.EntityName, Is.EqualTo("wrist scimitar"));
                    Assert.That(altMeta.EntityName, Is.EqualTo("wrist scimitar"));
                    Assert.That(scimitarMeta.EntityDescription, Is.EqualTo("A huge, serrated blade extending from metal gauntlets."));
                    Assert.That(altMeta.EntityDescription, Is.EqualTo("A huge, serrated blade extending from metal gauntlets."));
                    Assert.That(scimitarMelee.AttackRate, Is.EqualTo(1f),
                        "CMSS13 /obj/item/weapon/bracer_attachment/scimitar sets attack_speed = 1 SECONDS.");
                    Assert.That(altMelee.AttackRate, Is.EqualTo(1f),
                        "CMSS13 /obj/item/weapon/bracer_attachment/scimitar/alt sets attack_speed = 1 SECONDS.");
                    Assert.That(scimitarMelee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 25),
                        "CMSS13 /obj/item/weapon/bracer_attachment/scimitar sets force = MELEE_FORCE_TIER_5, and code/__DEFINES/combat.dm defines that as 25.");
                    Assert.That(altMelee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 25),
                        "CMSS13 /obj/item/weapon/bracer_attachment/scimitar/alt sets force = MELEE_FORCE_TIER_5, and code/__DEFINES/combat.dm defines that as 25.");
                });
            }
            finally
            {
                if (!entMan.Deleted(scimitar))
                    entMan.DeleteEntity(scimitar);
                if (!entMan.Deleted(altScimitar))
                    entMan.DeleteEntity(altScimitar);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PairedScimitarsUseCmss13SpeedBonus()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var melee = entMan.System<SharedMeleeWeaponSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var scimitar = entMan.SpawnEntity("CMUYautjaScimitar", MapCoordinates.Nullspace);
            var altScimitar = entMan.SpawnEntity("CMUYautjaScimitarAlt", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(melee.GetAttackRate(scimitar, hunter), Is.EqualTo(1f),
                    "CMSS13 /obj/item/weapon/bracer_attachment/scimitar starts at attack_speed = 1 SECONDS.");

                Assert.That(hands.TryPickupAnyHand(hunter, scimitar), Is.True);
                Assert.That(melee.GetAttackRate(scimitar, hunter), Is.EqualTo(1f),
                    "CMSS13 only applies speed_bonus_amount while another bracer_attachment is held.");

                Assert.That(hands.TryPickupAnyHand(hunter, altScimitar), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(melee.GetAttackRate(scimitar, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f),
                        "CMSS13 scimitar sets speed_bonus_amount = -0.4 SECONDS, reducing attack_speed from 1.0 to 0.6 seconds when paired.");
                    Assert.That(melee.GetAttackRate(altScimitar, hunter), Is.EqualTo(1f / 0.6f).Within(0.0001f),
                        "CMSS13 scimitar/alt inherits the same paired speed bonus.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(scimitar))
                    entMan.DeleteEntity(scimitar);
                if (!entMan.Deleted(altScimitar))
                    entMan.DeleteEntity(altScimitar);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerAttachmentWeaponsUseCmss13MeleeForceTierFour()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wristBlades = entMan.SpawnEntity("CMUYautjaWristBlades", MapCoordinates.Nullspace);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);

            try
            {
                var wristMelee = entMan.GetComponent<MeleeWeaponComponent>(wristBlades);
                var gauntletMelee = entMan.GetComponent<MeleeWeaponComponent>(gauntlet);

                Assert.Multiple(() =>
                {
                    Assert.That(wristMelee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 20),
                        "CMSS13 /obj/item/weapon/bracer_attachment/wristblades inherits force = MELEE_FORCE_TIER_4, and code/__DEFINES/combat.dm defines that as 20.");
                    Assert.That(gauntletMelee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 20),
                        "CMSS13 /obj/item/weapon/bracer_attachment/chain_gauntlets sets force = MELEE_FORCE_TIER_4, and code/__DEFINES/combat.dm defines that as 20.");
                });
            }
            finally
            {
                if (!entMan.Deleted(wristBlades))
                    entMan.DeleteEntity(wristBlades);
                if (!entMan.Deleted(gauntlet))
                    entMan.DeleteEntity(gauntlet);
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
    public async Task ChainedGauntletDisarmFinisherCreatesSourceHookBeamVisual()
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
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaChainGauntletBeam").Count(entMan.HasComponent<BeamComponent>), Is.EqualTo(0));

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 4;
                chain.HasChain = true;
                chain.ChainMessageChance = 0f;

                var ev = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(target, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(EntityPrototypeIds(entMan, "CMUYautjaChainGauntletBeam").Count(entMan.HasComponent<BeamComponent>), Is.GreaterThan(0));
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
    public async Task ChainedGauntletDisarmFinisherFiresSourceHookProjectileVisual()
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
                Assert.That(EntityPrototypeIds(entMan, "CMUYautjaChainGauntletHookProjectile").Count(entMan.HasComponent<ProjectileComponent>), Is.EqualTo(0));

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 4;
                chain.HasChain = true;
                chain.ChainMessageChance = 0f;

                var ev = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(target, ref ev);

                var hooks = EntityPrototypeIds(entMan, "CMUYautjaChainGauntletHookProjectile")
                    .Where(entMan.HasComponent<ProjectileComponent>)
                    .ToList();

                Assert.That(hooks, Has.Count.EqualTo(1));
                var hook = hooks[0];
                var projectile = entMan.GetComponent<ProjectileComponent>(hook);
                var body = entMan.GetComponent<PhysicsComponent>(hook);
                var velocity = physics.GetMapLinearVelocity(hook, body);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(chain.ChainHookProjectileMaxRange, Is.EqualTo(4f),
                        "CMSS13 /datum/ammo/yautja/gauntlet_hook sets max_range = 4.");
                    Assert.That(chain.ChainHookProjectileSpeed, Is.EqualTo(10f),
                        "CMSS13 gauntlet_hook inherits /datum/ammo shell_speed = AMMO_SPEED_TIER_1 = 1, converted locally as CM_PROJECTILE_SPEED * 10.");
                    Assert.That(projectile.Shooter, Is.EqualTo(hunter));
                    Assert.That(projectile.Weapon, Is.EqualTo(gauntlet));
                    Assert.That(entMan.GetComponent<ProjectileMaxRangeComponent>(hook).Max, Is.EqualTo(chain.ChainHookProjectileMaxRange));
                    Assert.That(velocity.X, Is.EqualTo(chain.ChainHookProjectileSpeed).Within(0.001f));
                    Assert.That(velocity.Y, Is.EqualTo(0f).Within(0.001f));
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
    public async Task ChainedGauntletDisarmFinisherCanSaySourceGetOverHereLine()
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
                var listener = entMan.System<YautjaTestSpeechListenerSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = 4;
                chain.HasChain = true;
                chain.ChainMessageChance = 1f;

                listener.Spoken.Clear();
                listener.StyledSpeech.Clear();
                var ev = new CMDisarmEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(target, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(listener.Spoken, Does.Contain((hunter, "GET OVER HERE!")));
                    Assert.That(listener.StyledSpeech, Does.Contain((hunter, "GET OVER HERE!", "yautjaChainSpeech")));
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
                var damageBefore = entMan.GetComponent<DamageableComponent>(target).TotalDamage;

                var ready = new InteractUsingEvent(hunter, gauntlet, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
                entMan.EventBus.RaiseLocalEvent(target, ready);

                var damageAfter = entMan.GetComponent<DamageableComponent>(target).TotalDamage;
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
                var damageBefore = entMan.GetComponent<DamageableComponent>(criticalTarget).TotalDamage;

                Assert.That(pulling.TryStartPull(hunter, criticalTarget), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Count, Is.EqualTo(1));
                    Assert.That(mobState.IsDead(criticalTarget), Is.False);
                    Assert.That(entMan.GetComponent<DamageableComponent>(criticalTarget).TotalDamage, Is.EqualTo(damageBefore));
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
                    Assert.That(entMan.GetComponent<DamageableComponent>(criticalTarget).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
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
    public async Task ChainGauntletFinishersExposeCmss13MessageAndSoundData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);

            try
            {
                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                Assert.Multiple(() =>
                {
                    Assert.That(chain.HelpFinisherMessage, Is.EqualTo("cmu-yautja-chain-gauntlet-help-message"));
                    Assert.That(chain.ExecutionMessage, Is.EqualTo("cmu-yautja-chain-gauntlet-execution-message"));
                    Assert.That(chain.SlamOverlayState, Is.EqualTo("slam"));
                    Assert.That(chain.SlamOverlayPrototype, Is.EqualTo("RMCEffectSlam"));
                    Assert.That(chain.ExecutionLiftDuration, Is.EqualTo(TimeSpan.FromSeconds(0.4)));
                    Assert.That(chain.ExecutionDropDuration, Is.EqualTo(TimeSpan.FromSeconds(0.4)));
                    Assert.That(chain.ExecutionLiftHeight, Is.EqualTo(2f));
                    Assert.That(chain.ForceAirlockDamage.DamageDict["Structural"], Is.EqualTo((FixedPoint2) 100));
                    AssertSoundPath(chain.HelpFinisherSound, "/Audio/_CMU14/Yautja/Weapons/ChainGauntlet/hit_punch.wav");
                    AssertSoundPath(chain.ExecutionTargetSound, "/Audio/_CMU14/Yautja/Weapons/Melee/bone_break1.wav");
                    AssertSoundPath(chain.ExecutionUserSound, "/Audio/_CMU14/Yautja/Voice/Roars/pred_roar5.wav");
                    AssertSoundPath(chain.ExecutionSlamSound, "/Audio/_CMU14/Yautja/Weapons/Melee/bang.wav");
                    AssertSoundPath(chain.ForceAirlockCrashSound, "/Audio/_RMC14/Effects/metal_crash.ogg");
                });
            }
            finally
            {
                entMan.DeleteEntity(gauntlet);
            }
        });

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
                Assert.That(entMan.GetComponent<DamageableComponent>(airlock).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
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
                    Assert.That(entMan.GetComponent<DamageableComponent>(airlock).Damage.DamageDict["Structural"], Is.EqualTo((FixedPoint2) 100));
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
                    Assert.That(entMan.GetComponent<DamageableComponent>(airlock).Damage.DamageDict["Structural"], Is.EqualTo((FixedPoint2) 100));
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
    public async Task ChainGauntletHelpFinisherFlicksSourceSlamOverlay()
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
                Assert.That(EntityPrototypeIds(entMan, "RMCEffectSlam").Count(), Is.Zero);

                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                chain.ComboCounter = chain.HelpFinisherComboRequired;
                var ready = new InteractUsingEvent(hunter, gauntlet, target, entMan.GetComponent<TransformComponent>(target).Coordinates);

                entMan.EventBus.RaiseLocalEvent(target, ready);

                var slams = EntityPrototypeIds(entMan, "RMCEffectSlam").ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(ready.Handled, Is.True);
                    Assert.That(slams, Has.Length.EqualTo(1));
                    Assert.That(entMan.GetComponent<MetaDataComponent>(slams[0]).EntityPrototype?.ID, Is.EqualTo(chain.SlamOverlayPrototype));
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
    public async Task ChainGauntletGrabExecutionFlicksSourceSlamOverlayAfterSourceLiftDelay()
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
                var mobState = entMan.System<MobStateSystem>();
                var pulling = entMan.System<PullingSystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", MapCoordinates.Nullspace);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, gauntlet), Is.True);
                mobState.ChangeMobState(target, MobState.Critical);

                Assert.That(EntityPrototypeIds(entMan, "RMCEffectSlam").Count(), Is.Zero);
                Assert.That(pulling.TryStartPull(hunter, target), Is.True);
                Assert.That(EntityPrototypeIds(entMan, "RMCEffectSlam").Count(), Is.Zero);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(1.0f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(EntityPrototypeIds(entMan, "RMCEffectSlam").Count(), Is.Zero);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.35f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var chain = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);
                var slams = EntityPrototypeIds(entMan, "RMCEffectSlam").ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(slams, Has.Length.EqualTo(1));
                    Assert.That(entMan.GetComponent<MetaDataComponent>(slams[0]).EntityPrototype?.ID, Is.EqualTo(chain.SlamOverlayPrototype));
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
    public async Task MedicompSpawnsReferenceHealingSet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medicomp = entMan.SpawnEntity("CMUYautjaMedicompFull", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(medicomp);
                var prototypes = storage.Container.ContainedEntities
                    .Select(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID)
                    .ToList();

                Assert.That(prototypes, Does.Contain("CMUYautjaHealingGun"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaWoundClamp"), Is.EqualTo(1));
                Assert.That(prototypes.Count(id => id == "CMUYautjaAutoInjector"), Is.EqualTo(3));
                Assert.That(prototypes.Count(id => id == "CMUYautjaHerbalCase"), Is.EqualTo(0));

                var healingGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaHealingGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(healingGelTotal, Is.EqualTo(6));

                var stabilizerGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaStabilizerGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(stabilizerGelTotal, Is.EqualTo(1));
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
    public async Task BracerSelfDestructCannotBeArmedWhileCritical()
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
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
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

                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2));
                beforeAudio = AudioEntities(entMan);

                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), user), Is.True);
                Assert.That(ActiveBracerMisuseDoAfters(entMan, user), Is.EqualTo(1));
                Assert.That(CountAttachedArms(body, user), Is.EqualTo(2),
                    "CMSS13 activate_suicide_internal() still waits for check_random_function()'s do_after before always_delimb resolves.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.25f));
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
                        Does.Contain("/Audio/_CMU14/Yautja/Weapons/WristBlades/wristblades_on.wav"),
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
    public async Task BracerSelfDestructPopupsUseCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid criticalHunter = default;
        EntityUid criticalBracer = default;
        EntityUid deadHunter = default;
        EntityUid deadBracer = default;
        EntityUid youngblood = default;
        EntityUid youngbloodBracer = default;
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
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                criticalHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                criticalBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                deadHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                deadBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(2, 0)));
                youngblood = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                youngbloodBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(3, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(criticalHunter);
                entMan.EnsureComponent<YautjaComponent>(deadHunter);
                entMan.EnsureComponent<YautjaComponent>(youngblood);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(youngblood);

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(criticalHunter, criticalBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(deadHunter, deadBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(youngblood, youngbloodBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    hunter,
                    TimeSpan.FromSeconds(30)), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You set the timer. May your journey to the great hunting grounds be swift.",
                "Bracer self-destruct armed. Detonation in 30 seconds.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                Assert.That(selfDestruct.TryCancelSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "Your bracers stop beeping.",
                "Bracer self-destruct cancelled.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, criticalHunter);
                mobState.ChangeMobState(criticalHunter, MobState.Critical);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (criticalBracer, entMan.GetComponent<YautjaBracerComponent>(criticalBracer)),
                    criticalHunter), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "As you fall into unconsciousness you fail to activate your self-destruct device before you collapse.",
                "The bracer does not answer a dying hunter.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, deadHunter);
                mobState.ChangeMobState(deadHunter, MobState.Dead);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (deadBracer, entMan.GetComponent<YautjaBracerComponent>(deadBracer)),
                    deadHunter), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "Little too late for that now!",
                "The bracer does not answer a dead hunter.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, youngblood);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (youngbloodBracer, entMan.GetComponent<YautjaBracerComponent>(youngbloodBracer)),
                    youngblood), Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "You don't yet understand how to use this.",
                "You do not yet understand how to use this.");
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
                             hunter,
                             bracer,
                             criticalHunter,
                             criticalBracer,
                             deadHunter,
                             deadBracer,
                             youngblood,
                             youngbloodBracer,
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
    public async Task BracerSelfDestructBroadcastsToYautjaLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid boomer = default;
        EntityUid listener = default;
        EntityUid bracer = default;
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
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                listener = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                metadata.SetEntityName(boomer, "A'ke Ret");
                entMan.EnsureComponent<YautjaComponent>(boomer);
                entMan.EnsureComponent<YautjaComponent>(listener);
                Assert.That(inventory.TryEquip(boomer, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, listener);

                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer,
                    TimeSpan.FromSeconds(30)), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "A'ke Ret has triggered their bracer's self-destruction sequence.",
                "You set the timer. May your journey to the great hunting grounds be swift.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                Assert.That(selfDestruct.TryCancelSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "A'ke Ret has cancelled their bracer's self-destruction sequence.",
                "Your bracers stop beeping.");
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

                foreach (var uid in new[] { boomer, listener, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructAdminLogsUseCmss13SourcePhrases()
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

        EntityUid boomer = default;
        EntityUid bracer = default;
        var expectedArea = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var inventory = entMan.System<InventorySystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                expectedArea = areas.GetAreaName(boomer);

                entMan.EnsureComponent<YautjaComponent>(boomer);
                Assert.That(inventory.TryEquip(boomer, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer,
                    TimeSpan.FromSeconds(30)), Is.True);
                Assert.That(selfDestruct.TryCancelSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer), Is.True);
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
                    messages.Any(message =>
                        message.Contains("triggered their predator self-destruct sequence", StringComparison.OrdinalIgnoreCase) &&
                        message.Contains($"in {expectedArea}", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 activate_suicide_internal() logs '[key_name(boomer)] triggered their predator self-destruct sequence in [area]'.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("has deactivated their Self-Destruct.").IgnoreCase,
                    $"CMSS13 activate_suicide_internal() logs '[key_name(boomer)] has deactivated their Self-Destruct.' on cancel.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.None.Contains("armed Yautja bracer self-destruct").IgnoreCase,
                    $"Self-arm logs should use the CMSS13 predator self-destruct wording instead of the old local subject.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.None.Contains("cancelled Yautja bracer self-destruct").IgnoreCase,
                    $"Cancel logs should use the CMSS13 Self-Destruct deactivation wording instead of the old local subject.\nActual logs:\n{joinedMessages}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { boomer, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructAdminAlertsExposeCmss13CancelLinkIntent()
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

        EntityUid boomer = default;
        EntityUid bracer = default;
        var expectedArea = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var inventory = entMan.System<InventorySystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                metadata.SetEntityName(boomer, "A'ke Ret");
                expectedArea = areas.GetAreaName(boomer);

                entMan.EnsureComponent<YautjaComponent>(boomer);
                Assert.That(inventory.TryEquip(boomer, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer,
                    TimeSpan.FromSeconds(30)), Is.True);
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Chat },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);

            Assert.Multiple(() =>
            {
                Assert.That(
                    messages,
                    Has.Some.Contains("ALERT:").And.Contains("triggered their predator self-destruct sequence").And.Contains("A'ke Ret").And.Contains(expectedArea).IgnoreCase,
                    $"CMSS13 activate_suicide_internal() sends a huge admin alert naming the hunter and area.\nActual chat logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("CLICK TO CANCEL THIS PRED SD").And.Contains("bracer").And.Contains("victim").IgnoreCase,
                    $"CMSS13 explode() sends admins a cancel link containing the bracer and victim references.\nActual chat logs:\n{joinedMessages}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { boomer, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructNotifiesGhostsLikeCmss13Explode()
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

        EntityUid boomer = default;
        EntityUid bracer = default;
        EntityUid ghost = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                boomer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                ghost = entMan.SpawnEntity("MobObserver", map.GridCoords.Offset(new Vector2(1, 0)));
                metadata.SetEntityName(boomer, "A'ke Ret");

                entMan.EnsureComponent<YautjaComponent>(boomer);
                Assert.That(inventory.TryEquip(boomer, bracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, ghost);

                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)),
                    boomer,
                    TimeSpan.FromSeconds(30)), Is.True);
            });

            await pair.ReallyBeIdle(10);
            await client.WaitAssertion(() =>
            {
                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg)
                    .ToList();
                var joinedMessages = string.Join("\n", history.Select(message => $"{message.Channel}: {message.Message}"));
                Assert.That(
                    history.Any(message =>
                        message.Channel == ChatChannel.Notifications &&
                        message.Message.Contains("Yautja self destruct", StringComparison.OrdinalIgnoreCase) &&
                        message.Message.Contains("A'ke Ret is self destructing to protect their honor!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 explode() calls notify_ghosts(header = \"Yautja self destruct\", message = \"[victim.real_name] is self destructing to protect their honor!\", action = NOTIFY_ORBIT).\nActual chat history:\n{joinedMessages}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { boomer, bracer, ghost })
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
                "You activate the timer. May Guan Thwei's final hunt be swift.");

            await server.WaitPost(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, listener);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "A'ke Ret has triggered Guan Thwei's bracer's self-destruction sequence.");

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
    public async Task BracerSelfDestructToggleUsesCmss13ConfirmationDialog()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), hunter), Is.True);
                AssertSelfDestructDialog(
                    entMan,
                    timing,
                    bracer,
                    "Detonate the bracers? Are you sure?\n\nNote: If you activate SD for any non-accidental reason during or after a fight, you commit to the SD. By initially activating the SD, you have accepted your impending death to preserve any lost honor.");
                Assert.That(bracerComp.SelfDestructArmed, Is.False);

                RaiseDialogOption(entMan, bracer, hunter, "No");
                Assert.That(entMan.HasComponent<DialogComponent>(bracer), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);

                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), hunter), Is.True);
                RaiseDialogOption(entMan, bracer, hunter, "Yes");
                Assert.That(bracerComp.SelfDestructArmed, Is.True);

                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), hunter), Is.True);
                AssertSelfDestructDialog(
                    entMan,
                    timing,
                    bracer,
                    "Are you sure you want to stop the countdown?");

                RaiseDialogOption(entMan, bracer, hunter, "No");
                Assert.That(bracerComp.SelfDestructArmed, Is.True);

                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), hunter), Is.True);
                RaiseDialogOption(entMan, bracer, hunter, "Yes");
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
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
            await AssertClientHasPopup(client, "Your bracers stop beeping.");
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

    [Test]
    public async Task BracerSelfDestructGuardsMaskFacehuggerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hugged = default;
        EntityUid huggedBracer = default;
        EntityUid facehugger = default;
        EntityUid cancellingHugged = default;
        EntityUid cancellingBracer = default;
        EntityUid cancellingFacehugger = default;
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

                hugged = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                huggedBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                facehugger = entMan.SpawnEntity("CMXenoParasite", map.GridCoords);
                cancellingHugged = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                cancellingBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                cancellingFacehugger = entMan.SpawnEntity("CMXenoParasite", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hugged);
                entMan.EnsureComponent<YautjaComponent>(cancellingHugged);

                Assert.That(inventory.TryEquip(hugged, huggedBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hugged, facehugger, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(cancellingHugged, cancellingBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, hugged);
                var huggedBracerComp = entMan.GetComponent<YautjaBracerComponent>(huggedBracer);
                Assert.That(selfDestruct.TryOpenSelfDestructDialog((huggedBracer, huggedBracerComp), hugged), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(huggedBracer), Is.False);
                Assert.That(huggedBracerComp.SelfDestructArmed, Is.False);
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

                var huggedBracerComp = entMan.GetComponent<YautjaBracerComponent>(huggedBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (huggedBracer, huggedBracerComp),
                    hugged,
                    TimeSpan.FromSeconds(30)), Is.False);
                Assert.That(huggedBracerComp.SelfDestructArmed, Is.False);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, cancellingHugged);
                var cancellingBracerComp = entMan.GetComponent<YautjaBracerComponent>(cancellingBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (cancellingBracer, cancellingBracerComp),
                    cancellingHugged,
                    TimeSpan.FromSeconds(30)), Is.True);
                Assert.That(inventory.TryEquip(cancellingHugged, cancellingFacehugger, "mask", silent: true, force: true), Is.True);
                Assert.That(selfDestruct.TryCancelSelfDestruct((cancellingBracer, cancellingBracerComp), cancellingHugged), Is.True);
                Assert.That(cancellingBracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "Your bracers stop beeping.");
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
                             hugged,
                             huggedBracer,
                             facehugger,
                             cancellingHugged,
                             cancellingBracer,
                             cancellingFacehugger,
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
    public async Task BracerSelfDestructGuardsHuntingPreserveLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid cancellingHunter = default;
        EntityUid cancellingBracer = default;
        EntityUid youngbloodHunter = default;
        EntityUid youngbloodBracer = default;
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

                entMan.EnsureComponent<YautjaHuntingGroundComponent>(map.MapUid);
                entMan.EnsureComponent<YautjaHuntingGroundComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                cancellingHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                cancellingBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                youngbloodHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                youngbloodBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(2, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(cancellingHunter);
                entMan.EnsureComponent<YautjaComponent>(youngbloodHunter);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(youngbloodHunter);

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(cancellingHunter, cancellingBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(youngbloodHunter, youngbloodBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, youngbloodHunter);
                var youngbloodBracerComp = entMan.GetComponent<YautjaBracerComponent>(youngbloodBracer);
                Assert.That(selfDestruct.TryArmSelfDestruct(
                    (youngbloodBracer, youngbloodBracerComp),
                    youngbloodHunter,
                    TimeSpan.FromSeconds(30)), Is.False);
                Assert.That(youngbloodBracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "Your bracer will not allow you to activate a self-destruction sequence in order to protect the hunting preserve.",
                "You don't yet understand how to use this.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, hunter);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryOpenSelfDestructDialog((bracer, bracerComp), hunter), Is.False);
                Assert.That(entMan.HasComponent<DialogComponent>(bracer), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(
                client,
                "Your bracer will not allow you to activate a self-destruction sequence in order to protect the hunting preserve.",
                "You set the timer. May your journey to the great hunting grounds be swift.");

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
                var session = server.PlayerMan.Sessions.Single();

                server.PlayerMan.SetAttachedEntity(session, cancellingHunter);
                var cancellingBracerComp = entMan.GetComponent<YautjaBracerComponent>(cancellingBracer);
                cancellingBracerComp.SelfDestructArmed = true;
                Assert.That(selfDestruct.TryCancelSelfDestruct((cancellingBracer, cancellingBracerComp), cancellingHunter), Is.False);
                Assert.That(cancellingBracerComp.SelfDestructArmed, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(labels, Does.Not.Contain("Your bracers stop beeping."),
                    $"Preserve-blocked cancellation should not show the CMSS13 cancel success text.\nActual labels:\n{joinedLabels}");
            });
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
                             hunter,
                             bracer,
                             cancellingHunter,
                             cancellingBracer,
                             youngbloodHunter,
                             youngbloodBracer,
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
    public async Task BracerSelfDestructCancelClearsLoopingAudioStreams()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var armStream = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var laughStream = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.True);

                bracerComp.SelfDestructArmStream = armStream;
                bracerComp.SelfDestructLaughStream = laughStream;

                Assert.That(selfDestruct.TryCancelSelfDestruct((bracer, bracerComp), hunter), Is.True);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
                Assert.That(bracerComp.SelfDestructArmStream, Is.Null);
                Assert.That(bracerComp.SelfDestructLaughStream, Is.Null);
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);

                if (!entMan.Deleted(armStream))
                    entMan.DeleteEntity(armStream);

                if (!entMan.Deleted(laughStream))
                    entMan.DeleteEntity(laughStream);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructExplosionMagnitudesMatchCmss13CellExplosion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var explosions = entMan.System<ExplosionSystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            var smallHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var smallBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var bigHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(10, 0)));
            var bigBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(10, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(smallHunter);
                entMan.EnsureComponent<YautjaComponent>(bigHunter);
                Assert.That(inventory.TryEquip(smallHunter, smallBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(bigHunter, bigBracer, "gloves", silent: true, force: true), Is.True);

                var smallComp = entMan.GetComponent<YautjaBracerComponent>(smallBracer);
                smallComp.SelfDestructExplosionType = YautjaSelfDestructExplosionType.Small;
                Assert.That(selfDestruct.TryArmSelfDestruct((smallBracer, smallComp), smallHunter, TimeSpan.Zero), Is.True);
                selfDestruct.Update(0);

                var smallExplosion = QueuedExplosions(explosions).SingleOrDefault();
                Assert.That(smallExplosion, Is.Not.Null,
                    "CMSS13 small self-destruct gib path still calls cell_explosion(T, 800, 550, EXPLOSION_FALLOFF_SHAPE_LINEAR).");
                Assert.That(smallExplosion!.TotalIntensity, Is.EqualTo(800));
                Assert.That(smallExplosion.Slope, Is.EqualTo(10),
                    "Local RMC explosion slope remains the CM-style falloff adapter value while preserving source total/max intensity facts.");
                Assert.That(smallExplosion.MaxTileIntensity, Is.EqualTo(550));

                ClearQueuedExplosions(explosions);

                var bigComp = entMan.GetComponent<YautjaBracerComponent>(bigBracer);
                bigComp.SelfDestructExplosionType = YautjaSelfDestructExplosionType.Big;
                Assert.That(selfDestruct.TryArmSelfDestruct((bigBracer, bigComp), bigHunter, TimeSpan.Zero), Is.True);
                selfDestruct.Update(0);

                var bigExplosion = QueuedExplosions(explosions).SingleOrDefault();
                Assert.That(bigExplosion, Is.Not.Null,
                    "CMSS13 big self-destruct calls cell_explosion(T, 600, 50, EXPLOSION_FALLOFF_SHAPE_LINEAR) on ground-level/shipped-large-SD maps.");
                Assert.That(bigExplosion!.TotalIntensity, Is.EqualTo(600));
                Assert.That(bigExplosion.Slope, Is.EqualTo(10),
                    "Local RMC explosion slope remains the CM-style falloff adapter value while preserving source total/max intensity facts.");
                Assert.That(bigExplosion.MaxTileIntensity, Is.EqualTo(50));
            }
            finally
            {
                foreach (var uid in new[] { smallHunter, smallBracer, bigHunter, bigBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructDefaultCountdownUsesCmss13DoAfterWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var random = server.ResolveDependency<IRobustRandom>();
                random.SetSeed(0);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.True);
                var countdown = bracerComp.SelfDestructAt - timing.CurTime;
                Assert.Multiple(() =>
                {
                    Assert.That(countdown, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(7.2)),
                        "CMSS13 explode() uses do_after(victim, rand(72, 80), ...), with BYOND deciseconds.");
                    Assert.That(countdown, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(8)),
                        "CMSS13 self-destruct countdown should stay inside the source rand(72, 80) decisecond window.");
                    Assert.That(countdown, Is.EqualTo(TimeSpan.FromSeconds(7.8)),
                        "With deterministic seed 0, local self-destruct should use the source randomized decisecond window rather than a fixed 8 second delay.");
                });
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
    public async Task YautjaButcherActionIsGrantedLikeCmss13Keybind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedActionsSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var actionIds = actions.GetActions(hunter)
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
                    .ToArray();

                Assert.That(actionIds, Does.Contain("CMUActionYautjaButcher"),
                    "CMSS13 /datum/keybinding/yautja/butcher calls H.butcher() directly from the Yautja keybind.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaButcherActionOpensAdjacentDeadTargetSelectionLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var farTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var action = entMan.SpawnEntity("CMUActionYautjaButcher", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                mobState.ChangeMobState(target, MobState.Dead);
                mobState.ChangeMobState(farTarget, MobState.Dead);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaButcherActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(hunter, ev);

                Assert.That(ev.Handled, Is.True);
                var dialog = entMan.GetComponent<DialogComponent>(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(dialog.DialogType, Is.EqualTo(DialogType.Options));
                    Assert.That(dialog.Options, Has.Count.EqualTo(1),
                        "Only the adjacent dead target is offered; the farther corpse is not.");
                    Assert.That(dialog.Options[0].Text, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(target).EntityName));
                    Assert.That(dialog.Options[0].Event, Is.TypeOf<YautjaButcherTargetSelectedEvent>());
                    Assert.That(entMan.TryGetComponent<DoAfterComponent>(hunter, out var doAfter), Is.True);
                    Assert.That(doAfter.DoAfters.Values.Any(active =>
                        !active.Cancelled && !active.Completed && active.Args.Event is YautjaButcherDoAfterEvent), Is.False,
                        "Selecting a target is a separate step in CMSS13; the action must not start butchering yet.");
                    Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(target).ButcheryProgress, Is.Zero);
                    Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(farTarget).ButcheryProgress, Is.Zero);
                });

                var targetSelected = (YautjaButcherTargetSelectedEvent) dialog.Options[0].Event!;
                entMan.EventBus.RaiseLocalEvent(hunter, targetSelected);

                var procedureDialog = entMan.GetComponent<DialogComponent>(hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(procedureDialog.Options, Has.Count.EqualTo(10),
                        "A human victim exposes Skin plus the nine CMSS13 delimb choices.");
                    Assert.That(procedureDialog.Options.All(option => option.Event is YautjaButcherProcedureSelectedEvent), Is.True);
                    Assert.That(procedureDialog.Options.Select(option =>
                        ((YautjaButcherProcedureSelectedEvent) option.Event!).Procedure),
                        Is.EqualTo(new[]
                        {
                            YautjaButcherProcedure.Skin,
                            YautjaButcherProcedure.Head,
                            YautjaButcherProcedure.RightHand,
                            YautjaButcherProcedure.LeftHand,
                            YautjaButcherProcedure.RightArm,
                            YautjaButcherProcedure.LeftArm,
                            YautjaButcherProcedure.RightFoot,
                            YautjaButcherProcedure.LeftFoot,
                            YautjaButcherProcedure.RightLeg,
                            YautjaButcherProcedure.LeftLeg,
                        }));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
                if (!entMan.Deleted(farTarget))
                    entMan.DeleteEntity(farTarget);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaButcherSkinStageStartsWithoutDirtyingServerOnlyState()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var trophies = entMan.System<YautjaTrophySystem>();
                var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                var target = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

                try
                {
                    entMan.EnsureComponent<YautjaComponent>(hunter);
                    mobState.ChangeMobState(target, MobState.Dead);

                    Assert.That(trophies.TryStartButcher(hunter, target, YautjaButcherProcedure.Skin), Is.True);
                    Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(target).ButcheryProgress, Is.EqualTo(1));
                }
                finally
                {
                    if (!entMan.Deleted(hunter))
                        entMan.DeleteEntity(hunter);
                    if (!entMan.Deleted(target))
                        entMan.DeleteEntity(target);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task YautjaLeapShowsWindupBeforeAnimatedThrow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.LeapThrowSpeed = 5f;
                yautja.LeapMaxRange = 7f;
                yautja.LeapWindup = TimeSpan.FromSeconds(0.2);

                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = map.GridCoords.Offset(new Vector2(6, 0)),
                };

                entMan.EventBus.RaiseLocalEvent(hunter, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.False);
                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                Assert.That(doAfter.DoAfters.Values.Any(active => active.Args.Event is YautjaLeapDoAfterEvent), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.4f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var thrown = entMan.GetComponent<ThrownItemComponent>(hunter);
                Assert.That(thrown.LandTime!.Value - thrown.ThrownTime!.Value, Is.GreaterThan(TimeSpan.FromSeconds(0.5)));
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (action != default && !entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaLeapSpawnsVisibleClampedLandingWarning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.LeapMaxRange = 7f;
                yautja.LeapWindup = TimeSpan.FromSeconds(1);
                Assert.That(yautja.LeapThrowSpeed, Is.LessThanOrEqualTo(12f));

                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = map.GridCoords.Offset(new Vector2(20, 0)),
                };

                entMan.EventBus.RaiseLocalEvent(hunter, ev);

                var warnings = EntityPrototypeIds(entMan, "CMUYautjaLeapWarning").ToList();
                Assert.That(warnings.Count, Is.EqualTo(1));

                var warningCoords = entMan.GetComponent<TransformComponent>(warnings[0]).Coordinates;
                Assert.That(warningCoords.Position.X, Is.EqualTo(map.GridCoords.Position.X + 7).Within(0.01f));
                Assert.That(warningCoords.Position.Y, Is.EqualTo(map.GridCoords.Position.Y).Within(0.01f));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjacentYautjaGearRacksUseCmss13ConnectedStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid left = default;
        EntityUid center = default;
        EntityUid right = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, 0), new Tile(1));

            left = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            center = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords.Offset(new Vector2(1, 0)));
            right = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords.Offset(new Vector2(2, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var leftVendor = entMan.GetComponent<CMAutomatedVendorComponent>(left);
            var centerVendor = entMan.GetComponent<CMAutomatedVendorComponent>(center);
            var rightVendor = entMan.GetComponent<CMAutomatedVendorComponent>(right);
            var leftXform = entMan.GetComponent<TransformComponent>(left);
            var centerXform = entMan.GetComponent<TransformComponent>(center);
            var rightXform = entMan.GetComponent<TransformComponent>(right);
            Assert.Multiple(() =>
            {
                Assert.That(leftVendor.UiStyle, Is.EqualTo(CMVendorUiStyle.Yautja), $"left pos={leftXform.Coordinates} map={leftXform.MapID}");
                Assert.That(centerVendor.UiStyle, Is.EqualTo(CMVendorUiStyle.Yautja), $"center pos={centerXform.Coordinates} map={centerXform.MapID}");
                Assert.That(rightVendor.UiStyle, Is.EqualTo(CMVendorUiStyle.Yautja), $"right pos={rightXform.Coordinates} map={rightXform.MapID}");
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(left, YautjaGearRackVisuals.State, out var leftState), Is.True);
                Assert.That(leftState, Is.EqualTo(YautjaGearRackVisualState.Left));
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(center, YautjaGearRackVisuals.State, out var centerState), Is.True);
                Assert.That(centerState, Is.EqualTo(YautjaGearRackVisualState.Centre), $"left={leftXform.Coordinates} center={centerXform.Coordinates} right={rightXform.Coordinates}");
                Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(right, YautjaGearRackVisuals.State, out var rightState), Is.True);
                Assert.That(rightState, Is.EqualTo(YautjaGearRackVisualState.Right));
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(left))
                entMan.DeleteEntity(left);
            if (!entMan.Deleted(center))
                entMan.DeleteEntity(center);
            if (!entMan.Deleted(right))
                entMan.DeleteEntity(right);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedGearRackWrappersKeepExactCmss13States()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid elderLeft = default;
        EntityUid elderRight = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, -1), new Tile(1));

            elderLeft = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorElderLeftSouthOffset0x16", map.GridCoords);
            elderRight = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorElderRightSouthOffset0x16", map.GridCoords.Offset(new Vector2(1, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, -1)));

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(elderLeft, YautjaGearRackVisuals.State, out _), Is.False);
                    Assert.That(appearance.TryGetData<YautjaGearRackVisualState>(elderRight, YautjaGearRackVisuals.State, out _), Is.False);
                    Assert.That(interaction.InRangeUnobstructed(hunter, elderLeft), Is.True);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(elderLeft))
                entMan.DeleteEntity(elderLeft);
            if (!entMan.Deleted(elderRight))
                entMan.DeleteEntity(elderRight);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedGearRackRunUsesSinglePrimaryVendor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid left = default;
        EntityUid centre = default;
        EntityUid right = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, -1), new Tile(1));

            left = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorLeftSouthVariant02Offset0x16", map.GridCoords);
            centre = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorCentreSouthVariant02Offset0x16", map.GridCoords.Offset(new Vector2(1, 0)));
            right = entMan.SpawnEntity("CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorRightSouthOffset0x16", map.GridCoords.Offset(new Vector2(2, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var interaction = entMan.System<SharedInteractionSystem>();
            var mind = entMan.System<MindSystem>();
            var roles = entMan.System<SharedRoleSystem>();
            var rackPieces = new[] { left, centre, right };

            Assert.Multiple(() =>
            {
                Assert.That(rackPieces.Count(entMan.HasComponent<ActivatableUIComponent>), Is.EqualTo(1));
                Assert.That(entMan.HasComponent<ActivatableUIComponent>(left), Is.True);
                Assert.That(entMan.HasComponent<ActivatableUIComponent>(centre), Is.False);
                Assert.That(entMan.HasComponent<ActivatableUIComponent>(right), Is.False);
                Assert.That(rackPieces.Count(entMan.HasComponent<ActivatableUIRequiresAccessComponent>), Is.EqualTo(0));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(left).PrimaryVendor, Is.EqualTo(left));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(centre).PrimaryVendor, Is.EqualTo(left));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(right).PrimaryVendor, Is.EqualTo(left));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(left).SegmentIndex, Is.EqualTo(0));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(centre).SegmentIndex, Is.EqualTo(1));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(right).SegmentIndex, Is.EqualTo(2));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(left).RunLength, Is.EqualTo(3));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(centre).RunLength, Is.EqualTo(3));
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(right).RunLength, Is.EqualTo(3));
            });

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, -1)));
            EntityUid? hunterMind = null;
            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<AccessComponent>(hunter).Tags.Add("CMUAccessYautjaSecure");
                var mindEnt = mind.CreateMind(null, entMan.GetComponent<MetaDataComponent>(hunter).EntityName);
                hunterMind = mindEnt.Owner;
                mind.TransferTo(mindEnt.Owner, hunter);
                roles.MindAddJobRole(mindEnt.Owner, jobPrototype: "CMUYautjaHunter");
                Assert.That(interaction.InRangeUnobstructed(hunter, left), Is.True);

                var allowed = new ActivatableUIOpenAttemptEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(left, allowed);
                Assert.That(allowed.Cancelled, Is.False);

                Assert.That(entMan.HasComponent<ActivatableUIComponent>(centre), Is.False);
                Assert.That(entMan.HasComponent<ActivatableUIComponent>(right), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (hunterMind is { } mindId && !entMan.Deleted(mindId))
                    entMan.DeleteEntity(mindId);
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(left))
                entMan.DeleteEntity(left);
            if (!entMan.Deleted(centre))
                entMan.DeleteEntity(centre);
            if (!entMan.Deleted(right))
                entMan.DeleteEntity(right);
        });

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

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var serverHands = entMan.System<HandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var wall = entMan.SpawnEntity("CMUHunterShipWallTurfClosedWallHuntershipHunterBase", map.GridCoords.Offset(new Vector2(1, 0)));
            var item = entMan.SpawnEntity(itemPrototype, map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, item), Is.True);

                var behindWall = map.GridCoords.Offset(new Vector2(3, 0));
                Assert.That(serverHands.ThrowHeldItem(hunter, behindWall), Is.False);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(hands.IsHolding(hunter, item), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(wall))
                    entMan.DeleteEntity(wall);
                if (!entMan.Deleted(item))
                    entMan.DeleteEntity(item);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerEquipmentVerbUsesSourceToggleLockDenials()
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
        EntityUid hunterBracer = default;
        EntityUid deadHunter = default;
        EntityUid deadHunterBracer = default;
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
                var verbs = entMan.System<VerbSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                hunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                deadHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                deadHunterBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(deadHunter);
                Assert.That(inventory.TryEquip(hunter, hunterBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(deadHunter, deadHunterBracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(deadHunter, MobState.Dead);
                mobState.ChangeMobState(hunter, MobState.Critical);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var deadBracerComp = entMan.GetComponent<YautjaBracerComponent>(deadHunterBracer);
                Assert.That(deadBracerComp.Locked, Is.True);

                var localVerbs = verbs.GetLocalVerbs(deadHunter, hunter, typeof(EquipmentVerb), force: true);
                var verb = localVerbs.Single(v => v.Text == "Unlock dead hunter bracer");
                verbs.ExecuteVerb(verb, hunter, deadHunter, forced: true);

                Assert.That(deadBracerComp.Locked, Is.True,
                    "CMSS13 toggle_lock() stat denial applies before dead hunter bracer equipment-verb toggles too.");
            });

            await pair.ReallyBeIdle(10);
            await AssertClientHasPopup(client, "You can't do that right now...");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();

                if (previousAttached is { } attached)
                    server.PlayerMan.SetAttachedEntity(session, attached);
                else
                    server.PlayerMan.SetAttachedEntity(session, null);

                if (previousCulture is { } culture)
                    loc.SetCulture(culture);

                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(hunterBracer))
                    entMan.DeleteEntity(hunterBracer);
                if (!entMan.Deleted(deadHunter))
                    entMan.DeleteEntity(deadHunter);
                if (!entMan.Deleted(deadHunterBracer))
                    entMan.DeleteEntity(deadHunterBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerEquipmentVerbsIgnoreNonYautjaItemTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var verbs = entMan.System<VerbSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var localVerbs = verbs.GetLocalVerbs(bracer, hunter, typeof(EquipmentVerb), force: true);
                Assert.That(localVerbs, Is.Empty);

                Assert.DoesNotThrow(() => verbs.GetLocalVerbs(bracer, hunter, new List<Type>
                {
                    typeof(InteractionVerb),
                    typeof(UtilityVerb),
                    typeof(InnateVerb),
                    typeof(AlternativeVerb),
                    typeof(ActivationVerb),
                    typeof(ExamineVerb),
                    typeof(Verb),
                    typeof(EquipmentVerb),
                }, force: true));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipChemDispenserHasSelfPoweredStorageNetwork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var dispenser = entMan.SpawnEntity("CMUHunterShipPlacedRMCChemDispenserGroundDispenserSouth", map.GridCoords);

            try
            {
                var dispenserComp = entMan.GetComponent<RMCChemicalDispenserComponent>(dispenser);
                var storage = entMan.GetComponent<RMCChemicalStorageComponent>(dispenser);
                var apc = entMan.GetComponent<ApcPowerReceiverComponent>(dispenser);
                var rmcPower = entMan.GetComponent<RMCPowerReceiverComponent>(dispenser);

                Assert.Multiple(() =>
                {
                    Assert.That(dispenserComp.Network.Id, Is.EqualTo("RMCChemStorageGround"));
                    Assert.That(storage.Network.Id, Is.EqualTo("RMCChemStorageGround"));
                    Assert.That(storage.Energy, Is.GreaterThan(FixedPoint2.Zero));
                    Assert.That(storage.BaseRecharge, Is.GreaterThan(FixedPoint2.Zero));
                    Assert.That(apc.NeedsPower, Is.False);
                    Assert.That(rmcPower.IdleLoad, Is.Zero);
                    Assert.That(rmcPower.ActiveLoad, Is.Zero);
                });
            }
            finally
            {
                if (!entMan.Deleted(dispenser))
                    entMan.DeleteEntity(dispenser);
            }
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
    public async Task HuntTeleporterWithoutDestinationDeniesBeforeConfirmation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var teleporter = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "missing_ground";

                var before = transform.GetMapCoordinates(hunter);
                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<DialogComponent>(teleporter), Is.False,
                        "A teleporter with no matching destination should deny immediately instead of opening a confirmation that can only fail after selection.");
                    Assert.That(transform.GetMapCoordinates(hunter), Is.EqualTo(before));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
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
    public async Task HunterShipChairBuckleFacesChairDirection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var eastRider = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var eastChair = entMan.SpawnEntity("CMUHunterShipPlacedCMChairNonFoldChairEast", map.GridCoords.Offset(new Vector2(1, 0)));
            var northRider = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
            var northChair = entMan.SpawnEntity("CMUHunterShipPlacedCMChairComfyComfychairNorth", map.GridCoords.Offset(new Vector2(1, 2)));

            try
            {
                Assert.That(buckle.TryBuckle(eastRider, eastRider, eastChair, popup: false), Is.True);
                Assert.That(buckle.TryBuckle(northRider, northRider, northChair, popup: false), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(transform.GetWorldRotation(eastRider).GetCardinalDir(), Is.EqualTo(Direction.East));
                    Assert.That(transform.GetWorldRotation(northRider).GetCardinalDir(), Is.EqualTo(Direction.North));
                });
            }
            finally
            {
                if (!entMan.Deleted(eastRider))
                    entMan.DeleteEntity(eastRider);
                if (!entMan.Deleted(eastChair))
                    entMan.DeleteEntity(eastChair);
                if (!entMan.Deleted(northRider))
                    entMan.DeleteEntity(northRider);
                if (!entMan.Deleted(northChair))
                    entMan.DeleteEntity(northChair);
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
    public async Task YautjaTeleportersRaiseMoveHooksForBuckledPassengerCarriedByPulledObjectLikeCmss13Trainteleport()
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
            var listener = entMan.System<YautjaTeleportMoveHookTestSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var wheelchair = entMan.SpawnEntity("RMCWheelchair", map.GridCoords.Offset(new Vector2(1, 0)));
            var passenger = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var destination = map.MapCoords.Offset(new Vector2(10, 0));

            try
            {
                Assert.That(buckle.TryBuckle(passenger, passenger, wheelchair, popup: false), Is.True);
                Assert.That(pulling.TryStartPull(hunter, wheelchair), Is.True);
                Assert.That(entMan.GetComponent<BuckleComponent>(passenger).BuckledTo, Is.EqualTo(wheelchair));

                listener.Reset();
                listener.Watch(hunter);
                listener.Watch(wheelchair);
                listener.Watch(passenger);

                Assert.That(teleport.TeleportTrain(hunter, destination), Is.True);

                Assert.Multiple(() =>
                {
                    AssertTeleportedTo(entMan, transform, hunter, destination);
                    AssertTeleportedTo(entMan, transform, wheelchair, destination);
                    Assert.That(transform.GetMapCoordinates(passenger).MapId, Is.EqualTo(destination.MapId));
                    Assert.That(listener.MoveCounts.GetValueOrDefault(hunter), Is.GreaterThanOrEqualTo(1),
                        "The train leader should receive the local movement hook equivalent to CMSS13 Moved().");
                    Assert.That(listener.MoveCounts.GetValueOrDefault(wheelchair), Is.GreaterThanOrEqualTo(1),
                        "A pulled strap object in the train should receive the local movement hook equivalent to CMSS13 Moved().");
                    Assert.That(listener.MoveCounts.GetValueOrDefault(passenger), Is.GreaterThanOrEqualTo(1),
                        "CMSS13 trainteleport() calls Moved() for every conga_line atom, including a buckled passenger carried by a pulled object.");
                });
            }
            finally
            {
                listener.Reset();
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(wheelchair))
                    entMan.DeleteEntity(wheelchair);
                if (!entMan.Deleted(passenger))
                    entMan.DeleteEntity(passenger);
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
    public async Task PlacedFlightConsoleOpensCmss13DestinationsAndLoadsSelectedGround()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(console).AvailableDestinations, Is.Not.Empty);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.Title, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-console-selection-title")));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Jungle Moon"));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Desert Moon"));

                var jungle = dialog.Options
                    .Select((option, index) => (option, index))
                    .Single(pair => pair.option.Text == "Jungle Moon")
                    .index;
                entMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(jungle)
                {
                    Actor = hunter,
                    UiKey = DialogUiKey.Key,
                });

                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(console).DestinationId, Is.EqualTo("jungle_moon"));
                var markedMaps = entMan.EntityQuery<YautjaHuntingGroundComponent, MapComponent>().Count();
                var markedGrids = entMan.EntityQuery<YautjaHuntingGroundComponent, MapGridComponent>().Count();
                Assert.Multiple(() =>
                {
                    Assert.That(markedMaps, Is.GreaterThanOrEqualTo(1),
                        "Loaded CMSS13 hunting-ground maps must be explicitly marked for preserve-only self-destruct guards.");
                    Assert.That(markedGrids, Is.GreaterThanOrEqualTo(1),
                        "Loaded CMSS13 hunting-ground grids must be explicitly marked for preserve-only self-destruct guards.");
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(console))
                    entMan.DeleteEntity(console);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LoadedJungleMoonAcceptsHuntsmasterAndBloodingCalls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
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
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(flightConsole, new InteractHandEvent(hunter, flightConsole));
                RaiseDialogOption(entMan, flightConsole, hunter, "Jungle Moon");

                Assert.That(entMan.EntityQuery<YautjaHuntSpawnPointComponent>().Any(point => point.Kind == YautjaHuntSpawnKind.Prey), Is.True);
                Assert.That(entMan.EntityQuery<YautjaHuntSpawnPointComponent>().Any(point => point.Kind == YautjaHuntSpawnKind.Youngblood), Is.True);
                Assert.That(entMan.EntityQuery<YautjaHuntTeleportDestinationComponent>().Any(destination => destination.Kind == YautjaHuntTeleporterKind.Young), Is.True);

                var initialGhostRoles = entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count();
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new InteractHandEvent(hunter, huntsmasterConsole));
                RaiseDialogOption(entMan, huntsmasterConsole, hunter, "Multi Faction (small)");
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

                var session = server.PlayerMan.Sessions.Single();
                var previousAttached = session.AttachedEntity;
                try
                {
                    server.CfgMan.SetCVar(CCVars.GhostQuickLottery, true);
                    server.PlayerMan.SetAttachedEntity(session, null);
                    var ghostRoles = entMan.System<GhostRoleSystem>();
                    var preyRole = preyRoles[0];
                    var info = ghostRoles.GetGhostRolesInfo(session)
                        .Single(entry => entry.Identifier == preyRoles[0].Role.Identifier);
                    Assert.That(info.Kind, Is.EqualTo(GhostRoleKind.RaffleReady));
                    ghostRoles.Request(session, preyRole.Role.Identifier);
                    Assert.That(entMan.TryGetComponent(preyRole.Uid, out GhostRoleRaffleComponent? raffle), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(raffle!.CurrentMembers, Does.Contain(session));
                        Assert.That(raffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(1)));
                    });

                    ghostRoles.Update(1.1f);
                    Assert.Multiple(() =>
                    {
                        Assert.That(session.AttachedEntity, Is.EqualTo(preyRole.Uid));
                        Assert.That(preyRole.Role.Taken, Is.True);
                        Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(preyRole.Uid), Is.False);
                        Assert.That(ghostRoles.GetGhostRolesInfo(null), Has.None.Matches<GhostRoleInfo>(
                            entry => entry.Identifier == preyRole.Role.Identifier));
                    });
                }
                finally
                {
                    server.CfgMan.SetCVar(CCVars.GhostQuickLottery, CCVars.GhostQuickLottery.DefaultValue);
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
                }

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
    public async Task HuntsmasterAndBloodingCallsWriteSourceShapedAdminLogs()
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

        EntityUid hunter = default;
        EntityUid flightConsole = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid shipSpawn = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var flight = entMan.GetComponent<YautjaHuntConsoleComponent>(flightConsole);
                var jungle = flight.AvailableDestinations.Single(candidate => candidate.Id == "jungle_moon");
                entMan.EventBus.RaiseLocalEvent(flightConsole, new YautjaHuntingGroundSelectedEvent(entMan.GetNetEntity(hunter), jungle.Id));
                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(flightConsole).DestinationId, Is.EqualTo("jungle_moon"));

                var initialGhostRoles = entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count();
                var huntsmaster = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
                var huntOption = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_small");
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), huntOption.Id));
                Assert.That(entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count(), Is.EqualTo(initialGhostRoles + 4));

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var option = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo");
                var huntConsole = entMan.System<YautjaHuntConsoleSystem>();
                Assert.That(huntConsole.TryCreateYoungbloodCall((bloodingConsole, blooding), hunter, option, bypassEligibility: true), Is.True);
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
                    Has.Some.Contains("spawned Jungle Moon (hunting grounds)").IgnoreCase,
                    $"CMSS13 flight-console admin output uses the hunting-ground spawn phrasing.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("triggered Multi Faction (small) inside the hunting grounds").IgnoreCase,
                    $"CMSS13 huntsmaster admin output uses the source hunt-call phrasing.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("has called Solo Youngblood (One member) (Youngblood ERT)").IgnoreCase,
                    $"CMSS13 blooding admin output uses the source youngblood-call phrasing.\nActual logs:\n{joinedMessages}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                foreach (var uid in new[] { hunter, flightConsole, huntsmasterConsole, bloodingConsole, shipSpawn })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterConsoleShowsUnavailablePopupWhenNoCallsAreConfigured()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                entMan.GetComponent<YautjaHuntConsoleComponent>(console).HuntCallOptions.Clear();

                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-hunt-console-hunt-ground-unavailable")));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (console != default && !entMan.Deleted(console))
                    entMan.DeleteEntity(console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingConsoleShowsUnavailablePopupWhenNoCallsAreConfigured()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                entMan.GetComponent<YautjaHuntConsoleComponent>(console).BloodingCallOptions.Clear();

                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain(Loc.GetString("cmu-yautja-hunt-console-blooding-unavailable")));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (console != default && !entMan.Deleted(console))
                    entMan.DeleteEntity(console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterAndBloodingConsolesShowCooldownPopupAfterCall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid huntDestination = default;
        EntityUid youngDestination = default;
        EntityUid youngbloodSpawn = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                huntDestination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));
                youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));
                youngbloodSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole).DestinationId = "jungle_moon";
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                var huntsmaster = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
                var huntOption = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_small");
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), huntOption.Id));
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new InteractHandEvent(hunter, huntsmasterConsole));

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var bloodingOption = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo");
                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), bloodingOption.Id));
                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new InteractHandEvent(hunter, bloodingConsole));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                var huntCooldownLabels = new HashSet<string>
                {
                    Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", "25:00")),
                    Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", "25:01")),
                };
                var bloodingCooldownLabels = new HashSet<string>
                {
                    Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", "40:00")),
                    Loc.GetString("cmu-yautja-hunt-console-cooldown", ("time", "40:01")),
                };

                Assert.Multiple(() =>
                {
                    Assert.That(labels.Any(huntCooldownLabels.Contains), Is.True, $"Expected Huntsmaster cooldown popup near 25 minutes.\nActual labels:\n{joinedLabels}");
                    Assert.That(labels.Any(bloodingCooldownLabels.Contains), Is.True, $"Expected Blooding cooldown popup near 40 minutes.\nActual labels:\n{joinedLabels}");
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

                foreach (var uid in new[] { hunter, huntsmasterConsole, bloodingConsole, huntDestination, youngDestination, youngbloodSpawn })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterAndBloodingCallsWithoutDestinationsShowUnavailablePopup()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                var huntsmaster = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
                var huntOption = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_small");
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), huntOption.Id));

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var bloodingOption = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo");
                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), bloodingOption.Id));

                Assert.That(entMan.EntityQuery<GhostTakeoverAvailableComponent>().Count(), Is.EqualTo(0));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels,
                        Does.Contain(Loc.GetString("cmu-yautja-hunt-console-hunt-ground-unavailable")),
                        $"Expected Huntsmaster unavailable popup when no hunting-ground destination/spawn markers exist.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels,
                        Does.Contain(Loc.GetString("cmu-yautja-hunt-console-blooding-unavailable")),
                        $"Expected Blooding unavailable popup when no youngblood destination/spawn markers exist.\nActual labels:\n{joinedLabels}");
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

                foreach (var uid in new[] { hunter, huntsmasterConsole, bloodingConsole })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid console = default;
        EntityUid? previousAttached = null;
        var jungle = -1;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                console = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
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
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (console != default && !entMan.Deleted(console))
                    entMan.DeleteEntity(console);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntsmasterAndBloodingRuntimeCallSizesUseConfiguredCounts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var huntConsole = entMan.System<YautjaHuntConsoleSystem>();

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
                var huntOption = huntsmaster.HuntCallOptions.Single(candidate => candidate.Id == "mixed_large");
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), huntOption.Id));

                var huntCoordinates = transform.GetMapCoordinates(huntDestination);
                Assert.That(
                    CountGhostRolesAt(entMan, transform, huntCoordinates),
                    Is.EqualTo(8),
                    "CMSS13 get_specific_call() uses the selected Huntsmaster call type's configured party size.");

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(bloodingConsole);
                var fixedPack = new YautjaHuntCallOption
                {
                    Id = "fixed_youngblood_pack",
                    DisplayName = "Fixed Youngblood Pack",
                    SpawnCount = 4,
                    MinSpawnCount = 4,
                };
                var initialYoungbloodRoles = CountYoungbloodGhostRoles(entMan);

                Assert.That(huntConsole.TryCreateYoungbloodCall((bloodingConsole, blooding), hunter, fixedPack, bypassEligibility: true), Is.True);
                Assert.That(
                    CountYoungbloodGhostRoles(entMan),
                    Is.EqualTo(initialYoungbloodRoles + 4),
                    "Blooding compatibility rows have source-shaped min/max pack sizes; runtime spawning must honor the selected option range.");
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
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Multi Faction (small)"));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Serpents (small)"));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Elite Multi Faction (larger)"));

                var mixedSmall = (YautjaHuntCallSelectedEvent) dialog.Options
                    .Single(option => option.Text == "Multi Faction (small)")
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

    [Test]
    public async Task Phase3ConsoleDialogPromptsUseCmss13TguiText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            var bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                entMan.EventBus.RaiseLocalEvent(flightConsole, new InteractHandEvent(hunter, flightConsole));
                AssertCmss13Dialog(entMan, flightConsole, "hunter flight console", "Which hunting grounds do you choose.");

                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new InteractHandEvent(hunter, huntsmasterConsole));
                AssertCmss13Dialog(entMan, huntsmasterConsole, "huntsmasters console", "What will you hunt today?");

                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new InteractHandEvent(hunter, bloodingConsole));
                AssertCmss13Dialog(entMan, bloodingConsole, "blooding console", "Available youngblood groups to awaken.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, flightConsole, huntsmasterConsole, bloodingConsole })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Phase3ConsoleDialogCancelUsesCmss13NoChoiceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid flightConsole = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();

                foreach (var console in new[] { flightConsole, huntsmasterConsole, bloodingConsole })
                {
                    entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
                    Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog),
                        $"{entMan.ToPrettyString(console)} should open a CMSS13 option dialog.");
                    Assert.That(dialog!.Options.Count > 0,
                        $"{entMan.ToPrettyString(console)} should expose selectable dialog options before cancel.");

                    ui.CloseUi(console, DialogUiKey.Key, hunter);
                }
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You have not chosen any hunting grounds."),
                        $"CMSS13 hunting_ground_selection/attack_hand() warns when tgui_input_list returns no choice.\nActual labels:\n{joined}");
                    Assert.That(labels, Does.Contain("You have not chosen any prey to hunt."),
                        $"CMSS13 hunt_ground_spawner/attack_hand() warns when tgui_input_list returns no choice.\nActual labels:\n{joined}");
                    Assert.That(labels, Does.Contain("You choose not to awaken any youngbloods."),
                        $"CMSS13 blooding_spawner/attack_hand() warns when tgui_input_list returns no choice.\nActual labels:\n{joined}");
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

                foreach (var uid in new[] { hunter, flightConsole, huntsmasterConsole, bloodingConsole })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodingConsoleYoungbloodDenialUsesCmss13SpecificText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid youngblood = default;
        EntityUid huntsmasterConsole = default;
        EntityUid bloodingConsole = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                youngblood = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
                bloodingConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(youngblood);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(youngblood);
                server.PlayerMan.SetAttachedEntity(session, youngblood);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new InteractHandEvent(youngblood, huntsmasterConsole));
                entMan.EventBus.RaiseLocalEvent(bloodingConsole, new InteractHandEvent(youngblood, bloodingConsole));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("This is not for you."),
                        $"CMSS13 /obj/structure/machinery/blooding_spawner/attack_hand() has a Blooding-specific youngblood denial.\nActual labels:\n{joined}");
                    Assert.That(labels.Count(label => label == Loc.GetString("cmu-yautja-hunt-console-denied")), Is.EqualTo(1),
                        "The generic youngblood console denial should still come from the Huntsmaster console only.");
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

                foreach (var uid in new[] { youngblood, huntsmasterConsole, bloodingConsole })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
        if (!transform.AnchorEntity(entity, xform))
        {
#pragma warning disable CS0618
            xform.Anchored = true;
#pragma warning restore CS0618
        }

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

    private static void AssertCmss13Dialog(IEntityManager entMan, EntityUid console, string title, string message)
    {
        Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(dialog!.Title, Is.EqualTo(title),
                "CMSS13 tgui_input_list passes [src] as the dialog title, which renders as the console name.");
            Assert.That(dialog.Message.Text, Is.EqualTo(message));
        });
    }

    private static void AssertSelfDestructDialog(
        IEntityManager entMan,
        IGameTiming timing,
        EntityUid bracer,
        string message)
    {
        Assert.That(entMan.TryGetComponent(bracer, out DialogComponent? dialog), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Options));
            Assert.That(dialog.Title, Is.EqualTo("Explosive Bracers"));
            Assert.That(dialog.Message.Text, Is.EqualTo(message));
            Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
            Assert.That(dialog.CloseAt, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(20)));
        });
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
    public async Task BloodingCallRequiresShipSideYoungbloodSpawnAndDoesNotUseGroundDestination()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<YautjaHuntConsoleSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var consoleUid = entMan.SpawnEntity("CMUHunterShipBloodingConsole", map.GridCoords);
            var console = entMan.GetComponent<YautjaHuntConsoleComponent>(consoleUid);
            console.DestinationId = "jungle_moon";
            var youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var option = console.BloodingCallOptions.Single(call => call.Id == "youngblood_solo");
                Assert.That(system.TryCreateYoungbloodCall((consoleUid, console), hunter, option, bypassEligibility: true), Is.False);
                Assert.That(CountYoungbloodGhostRoles(entMan), Is.EqualTo(0));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(consoleUid))
                    entMan.DeleteEntity(consoleUid);
                if (!entMan.Deleted(youngDestination))
                    entMan.DeleteEntity(youngDestination);
            }
        });

        await pair.CleanReturnAsync();
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
            var shipSpawn = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            var huntDestination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));
            var youngDestination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(16, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.GetComponent<YautjaHuntConsoleComponent>(console).BloodingCallOptions, Is.Not.Empty);

                entMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));

                Assert.That(entMan.TryGetComponent(console, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.Title, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-console-blooding-title")));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Solo Youngblood (One member)"));
                Assert.That(dialog.Options.Select(option => option.Text), Does.Contain("Youngblood Hunting Pack (Six members)"));

                var solo = (YautjaHuntCallSelectedEvent) dialog.Options
                    .Single(option => option.Text == "Solo Youngblood (One member)")
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
    public async Task BloodingCallStartsRaffledYoungbloodCandidateWindow()
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
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var blooding = entMan.GetComponent<YautjaHuntConsoleComponent>(console);
                var solo = blooding.BloodingCallOptions.Single(candidate => candidate.Id == "youngblood_solo_experienced");
                entMan.EventBus.RaiseLocalEvent(console, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), solo.Id));

                var query = entMan.EntityQueryEnumerator<GhostRoleComponent, GhostTakeoverAvailableComponent, YautjaYoungbloodGhostRoleComponent>();
                Assert.That(query.MoveNext(out var youngblood, out var ghostRole, out _, out var metadata), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(ghostRole.JobProto, Is.EqualTo("CMUYautjaYoungblood"));
                    Assert.That(ghostRole.ReregisterOnGhost, Is.False);
                    Assert.That(ghostRole.RaffleConfig, Is.Not.Null);
                    Assert.That(metadata.CallId, Is.EqualTo("youngblood_solo_experienced"));
                    Assert.That(metadata.BypassEligibility, Is.False);
                });

                var settings = ghostRole.RaffleConfig!.SettingsOverride;
                Assert.That(settings, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(settings!.InitialDuration, Is.EqualTo(30));
                    Assert.That(settings.JoinExtendsDurationBy, Is.EqualTo(10));
                    Assert.That(settings.MaxDuration, Is.EqualTo(90));
                });

                var playtimes = playtime.GetTrackerTimes(session);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

                server.PlayerMan.SetAttachedEntity(session, null);
                ghostRoles.Request(session, ghostRole.Identifier);

                Assert.That(entMan.TryGetComponent(youngblood, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(raffle!.CurrentMembers, Does.Contain(session));
                    Assert.That(raffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(raffle.CumulativeTime, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(raffle.JoinExtendsDurationBy, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(raffle.MaxDuration, Is.EqualTo(TimeSpan.FromSeconds(90)));
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

                var playtimes = playtime.GetTrackerTimes(session);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

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

                var playtimes = playtime.GetTrackerTimes(session);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

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

                var playtimes = playtime.GetTrackerTimes(session);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

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

                var playtimes = playtime.GetTrackerTimes(session!);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

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

                var playtimes = playtime.GetTrackerTimes(session!);
                playtimes["CMJobRifleman"] = TimeSpan.FromHours(5);
                playtimes["CMJobSelectableXeno"] = TimeSpan.FromHours(5);

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
    public async Task FalconDronePrototypesMatchCmss13SourceDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var falcon = prototypes.Index(FalconDronePrototype);
            var badBloodFalcon = prototypes.Index("CMUYautjaFalconDroneBadBlood");
            var deployed = prototypes.Index(FalconDroneDeployedPrototype);
            var badBloodDeployed = prototypes.Index("CMUYautjaFalconDroneBadBloodDeployed");
            var destroyed = prototypes.Index("CMUYautjaFalconDroneDestroyed");
            var disabled = prototypes.Index("CMUYautjaFalconDroneDisabled");
            var controlAction = prototypes.Index("CMUActionYautjaFalconControl");
            var recallAction = prototypes.Index("CMUActionYautjaFalconRecall");

            Assert.Multiple(() =>
            {
                Assert.That(falcon.Name, Is.EqualTo("falcon drone"));
                Assert.That(falcon.Description, Is.EqualTo("An agile drone used by Yautja to survey the hunting grounds."));
                Assert.That(deployed.Name, Is.EqualTo("falcon drone"));
                Assert.That(deployed.Description, Is.EqualTo("An agile drone used by Yautja to survey the hunting grounds."),
                    "CMSS13 /mob/hologram/falcon uses the same description as /obj/item/falcon_drone.");

                AssertPrototypeSpriteState(falcon, factory, "falcon_drone");
                AssertPrototypeSpriteState(badBloodFalcon, factory, "falcon_drone_badblood");
                AssertPrototypeSpriteState(deployed, factory, "falcon_drone_active");
                AssertPrototypeSpriteState(badBloodDeployed, factory, "falcon_drone_badblood_active");
                AssertPrototypeSpriteState(destroyed, factory, "falcon_drone_destroyed");
                AssertPrototypeSpriteState(disabled, factory, "falcon_drone_emped");
                AssertPrototypeActionIconState(controlAction, factory, "falcon_drone");
                AssertPrototypeActionIconState(recallAction, factory, "falcon_drone");
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                var falconUid = SpawnAndTrack(entMan, "CMUYautjaFalconDrone", spawned);
                var badBloodUid = SpawnAndTrack(entMan, "CMUYautjaFalconDroneBadBlood", spawned);
                var destroyedUid = SpawnAndTrack(entMan, "CMUYautjaFalconDroneDestroyed", spawned);
                var disabledUid = SpawnAndTrack(entMan, "CMUYautjaFalconDroneDisabled", spawned);
                var deployedUid = SpawnAndTrack(entMan, "CMUYautjaFalconDroneDeployed", spawned);
                var badBloodDeployedUid = SpawnAndTrack(entMan, "CMUYautjaFalconDroneBadBloodDeployed", spawned);

                AssertFalconItemSourceFacts(entMan, falconUid, "CMUYautjaFalconDroneDeployed");
                AssertFalconItemSourceFacts(entMan, badBloodUid, "CMUYautjaFalconDroneBadBloodDeployed");
                AssertFalconTrashSourceFacts(entMan, destroyedUid, "destroyed falcon drone", "The wreckage of a Yautja drone.");
                AssertFalconTrashSourceFacts(entMan, disabledUid, "disabled falcon drone", "An intact Yautja drone. The internal electronics are completely fried.");

                Assert.That(entMan.GetComponent<YautjaFalconDroneDeployedComponent>(deployedUid).ReturnDroneItemOnDelete, Is.True,
                    "CMSS13 /mob/hologram/falcon/Destroy() returns the parent item to an ear slot or hands.");
                Assert.That(entMan.GetComponent<YautjaFalconDroneDeployedComponent>(badBloodDeployedUid).ReturnDroneItemOnDelete, Is.True,
                    "CMSS13 Bad Blood falcon hologram inherits parent-drone return behavior.");
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
    public async Task FalconDroneControlActionIsGrantedWhenEquippedToEarLikeCmss13Keybind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, hunter, falcon, SlotFlags.EARS);
                entMan.EventBus.RaiseLocalEvent(falcon, ev);
                var actionIds = ev.Actions
                    .Select(actionUid => entMan.GetComponent<MetaDataComponent>(actionUid).EntityPrototype?.ID)
                    .ToArray();

                Assert.That(actionIds, Does.Contain("CMUActionYautjaFalconControl"),
                    "CMSS13 /obj/item/falcon_drone/equipped() grants /datum/action/predator_action/mask/control_falcon_drone on ear slots.");
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneControlActionDeploysObserverLikeCmss13ActionActivate()
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
            var mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaFalconControl", MapCoordinates.Nullspace);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaFalconControlActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(falcon, ev);

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                Assert.That(ev.Handled, Is.True);
                Assert.That(eye.Target, Is.Not.Null);
                Assert.That(eye.Target, Is.Not.EqualTo(hunter));
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False);

                var drone = eye.Target!.Value;
                var deployed = entMan.GetComponent<YautjaFalconDroneDeployedComponent>(drone);
                Assert.That(deployed.DroneItem, Is.EqualTo(falcon));
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
                if (!entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneControlActionDoesNotRequireClanMaskLikeCmss13PredatorAction()
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
            var action = entMan.SpawnEntity("CMUActionYautjaFalconControl", MapCoordinates.Nullspace);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaFalconControlActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(falcon, ev);

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.True,
                        "CMSS13 /datum/action/predator_action/mask/control_falcon_drone is in the mask namespace but does not set require_mask.");
                    Assert.That(eye.Target, Is.Not.Null);
                    Assert.That(eye.Target, Is.Not.EqualTo(hunter));
                    Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False,
                        "CMSS13 action_activate() calls linked_falcon_drone.control_falcon_drone() when only the bracer requirement is met.");
                    Assert.That(EntityPrototypeIds(entMan, "CMUYautjaFalconDroneDeployed").Count(), Is.EqualTo(1));
                });
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
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
                foreach (var deployed in EntityPrototypeIds(entMan, "CMUYautjaFalconDroneDeployed").ToArray())
                {
                    if (!entMan.Deleted(deployed))
                        entMan.DeleteEntity(deployed);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneControlActionAcceptsHeldBracerLikeCmss13PredatorAction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();

            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var heldBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaFalconControl", MapCoordinates.Nullspace);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, heldBracer), Is.True);
                Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(heldBracer));

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var ev = new YautjaFalconControlActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };
                entMan.EventBus.RaiseLocalEvent(falcon, ev);

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                Assert.That(ev.Handled, Is.True);
                Assert.That(eye.Target, Is.Not.Null);
                Assert.That(eye.Target, Is.Not.EqualTo(hunter));

                var drone = eye.Target!.Value;
                var deployed = entMan.GetComponent<YautjaFalconDroneDeployedComponent>(drone);
                var controller = entMan.GetComponent<YautjaFalconControllerComponent>(hunter);
                Assert.Multiple(() =>
                {
                    Assert.That(deployed.DroneItem, Is.EqualTo(falcon));
                    Assert.That(controller.SourceBracer, Is.EqualTo(heldBracer));
                    Assert.That(entMan.HasComponent<YautjaFalconSourceBracerComponent>(heldBracer), Is.True);
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(heldBracer))
                    entMan.DeleteEntity(heldBracer);
                if (!entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
                if (!entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneDeploysObserverForBracerUser()
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

                Assert.That(entMan.IsQueuedForDeletion(falcon), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False);

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                Assert.That(eye.Target, Is.Not.Null);
                Assert.That(eye.Target, Is.Not.EqualTo(hunter));

                var drone = eye.Target!.Value;
                var deployed = entMan.GetComponent<YautjaFalconDroneDeployedComponent>(drone);
                Assert.That(deployed.DroneItem, Is.EqualTo(falcon));
                Assert.That(entMan.GetComponent<MetaDataComponent>(drone).EntityPrototype?.ID, Is.EqualTo("CMUYautjaFalconDroneDeployed"));
                Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<RelayInputMoverComponent>(hunter), Is.True);
                Assert.That(entMan.GetComponent<RelayInputMoverComponent>(hunter).RelayEntity, Is.EqualTo(drone));
                Assert.That(entMan.HasComponent<MovementRelayTargetComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<InputMoverComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<CanMoveInAirComponent>(drone), Is.True);
                Assert.That(entMan.HasComponent<MovementAlwaysTouchingComponent>(drone), Is.True);
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
    public async Task FalconDroneReturnsToFreeEarSlotWithoutTouchingCommunicator()
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
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, communicator, "ears", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, falcon, "ears2", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));

                Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out var ears), Is.True);
                Assert.That(ears, Is.EqualTo(communicator));
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out _), Is.False);

                var controller = entMan.GetComponent<YautjaFalconControllerComponent>(hunter);
                var action = controller.RecallAction!.Value;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(hunter, NewFalconRecallEvent(hunter, action, actionComp));

                Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out ears), Is.True);
                Assert.That(ears, Is.EqualTo(communicator));
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out var ear2), Is.True);
                Assert.That(ear2, Is.EqualTo(falcon));
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(communicator))
                    entMan.DeleteEntity(communicator);
                if (!entMan.Deleted(falcon))
                    entMan.DeleteEntity(falcon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneReturnPrefersLeftEarThenRightEarThenHandsLikeCmss13Destroy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid leftBlocker = default;
        EntityUid rightBlocker = default;
        EntityUid leftPreferredFalcon = default;
        EntityUid rightFallbackFalcon = default;
        EntityUid handFallbackFalcon = default;
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
                leftBlocker = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
                rightBlocker = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
                leftPreferredFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
                rightFallbackFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);
                handFallbackFalcon = entMan.SpawnEntity("CMUYautjaFalconDrone", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(leftPreferredFalcon, new UseInHandEvent(hunter));
                RaiseFalconRecall(entMan, hunter);

                Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out var leftEar), Is.True);
                Assert.That(leftEar, Is.EqualTo(leftPreferredFalcon),
                    "CMSS13 /mob/hologram/falcon/Destroy() tries WEAR_L_EAR before WEAR_R_EAR when returning the parent drone.");
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                Assert.That(inventory.TryUnequip(hunter, "ears", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, leftBlocker, "ears", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(rightFallbackFalcon, new UseInHandEvent(hunter));
                RaiseFalconRecall(entMan, hunter);

                Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out var leftEar), Is.True);
                Assert.That(leftEar, Is.EqualTo(leftBlocker));
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out var rightEar), Is.True);
                Assert.That(rightEar, Is.EqualTo(rightFallbackFalcon),
                    "CMSS13 /mob/hologram/falcon/Destroy() falls back to WEAR_R_EAR when WEAR_L_EAR is occupied.");
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var hands = entMan.System<SharedHandsSystem>();

                Assert.That(inventory.TryUnequip(hunter, "ears2", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, rightBlocker, "ears2", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(handFallbackFalcon, new UseInHandEvent(hunter));
                RaiseFalconRecall(entMan, hunter);

                Assert.That(inventory.TryGetSlotEntity(hunter, "ears", out var leftEar), Is.True);
                Assert.That(leftEar, Is.EqualTo(leftBlocker));
                Assert.That(inventory.TryGetSlotEntity(hunter, "ears2", out var rightEar), Is.True);
                Assert.That(rightEar, Is.EqualTo(rightBlocker));
                Assert.That(hands.IsHolding(hunter, handFallbackFalcon), Is.True,
                    "CMSS13 /mob/hologram/falcon/Destroy() puts the parent drone in hands when both ears are occupied.");
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
                if (leftBlocker != default && !entMan.Deleted(leftBlocker))
                    entMan.DeleteEntity(leftBlocker);
                if (rightBlocker != default && !entMan.Deleted(rightBlocker))
                    entMan.DeleteEntity(rightBlocker);
                if (leftPreferredFalcon != default && !entMan.Deleted(leftPreferredFalcon))
                    entMan.DeleteEntity(leftPreferredFalcon);
                if (rightFallbackFalcon != default && !entMan.Deleted(rightFallbackFalcon))
                    entMan.DeleteEntity(rightFallbackFalcon);
                if (handFallbackFalcon != default && !entMan.Deleted(handFallbackFalcon))
                    entMan.DeleteEntity(handFallbackFalcon);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconDroneDeleteRestoresControllerEye()
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
                entMan.DeleteEntity(drone);

                Assert.That(eye.Target, Is.EqualTo(hunter));
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
    public async Task DisabledAndDestroyedFalconVariantsCannotDeploy()
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
            var destroyed = entMan.SpawnEntity("CMUYautjaFalconDroneDestroyed", map.GridCoords);
            var disabled = entMan.SpawnEntity("CMUYautjaFalconDroneDisabled", map.GridCoords);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                entMan.EventBus.RaiseLocalEvent(destroyed, new UseInHandEvent(hunter));
                entMan.EventBus.RaiseLocalEvent(disabled, new UseInHandEvent(hunter));

                Assert.That(entMan.HasComponent<YautjaFalconControllerComponent>(hunter), Is.False);

                var deployed = entMan.EntityQueryEnumerator<YautjaFalconDroneDeployedComponent>();
                Assert.That(deployed.MoveNext(out _, out _), Is.False);
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(destroyed))
                    entMan.DeleteEntity(destroyed);
                if (!entMan.Deleted(disabled))
                    entMan.DeleteEntity(disabled);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SleepingHellhoundProvidesDialogUiForWakeConfirmation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var prototype = prototypes.Index<EntityPrototype>("CMUHunterShipSleepingHellhound");

            Assert.That(prototype.TryGetComponent<UserInterfaceComponent>(out _, factory), Is.True,
                "The sleeping Hellhound must expose UserInterface so DialogBui can open the wake confirmation.");
        });

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
                        Assert.That(raffle!.Countdown, Is.EqualTo(TimeSpan.FromSeconds(30)));
                        Assert.That(raffle.JoinExtendsDurationBy, Is.EqualTo(TimeSpan.FromSeconds(10)));
                        Assert.That(raffle.MaxDuration, Is.EqualTo(TimeSpan.FromSeconds(90)));
                        Assert.That(raffle.CurrentMembers, Does.Contain(session));
                    });
                }
                finally
                {
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);
                }
                Assert.That(entMan.HasComponent<PressureImmunityComponent>(hellhoundUid), Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(hellhoundUid), Is.True);
                Assert.That(entMan.HasComponent<RespiratorComponent>(hellhoundUid), Is.False);
                Assert.That(entMan.HasComponent<HungerComponent>(hellhoundUid), Is.False);
                Assert.That(entMan.HasComponent<ThirstComponent>(hellhoundUid), Is.False);
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
    public async Task SleepingHellhoundLeftClickActivationOpensConfirmation()
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

                var activate = new ActivateInWorldEvent(hunter, sleeping, true);
                entMan.EventBus.RaiseLocalEvent(sleeping, activate);

                Assert.That(activate.Handled, Is.True,
                    "A left-click world activation must be consumed by the sleeping Hellhound.");
                Assert.That(entMan.HasComponent<DialogComponent>(sleeping), Is.True,
                    "A left-click on the sleeping Hellhound must open the wake confirmation dialog.");
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
    public async Task HellhoundGhostRoleUsesDefaultRaffleQueue()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);

            try
            {
                var role = entMan.GetComponent<GhostRoleComponent>(hellhound);
                Assert.That(role.RaffleConfig, Is.Not.Null);
                Assert.That(role.RaffleConfig!.Settings, Is.EqualTo("default"));

                server.PlayerMan.SetAttachedEntity(session, null);
                var info = entMan.System<GhostRoleSystem>().GetGhostRolesInfo(session)
                    .Single(entry => entry.Identifier == role.Identifier);
                Assert.That(info.Kind, Is.EqualTo(GhostRoleKind.RaffleReady));
                entMan.System<GhostRoleSystem>().Request(session, role.Identifier);

                Assert.That(entMan.TryGetComponent(hellhound, out GhostRoleRaffleComponent? raffle), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(raffle!.CurrentMembers, Does.Contain(session));
                    Assert.That(raffle.Countdown, Is.EqualTo(TimeSpan.FromSeconds(30)));
                    Assert.That(raffle.JoinExtendsDurationBy, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(raffle.MaxDuration, Is.EqualTo(TimeSpan.FromSeconds(90)));
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
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
    public async Task HoundPadListsActiveHellhoundsWithoutChangingUserEye()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(4, 0)));
            var otherHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(6, 0)));
            var deadHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(8, 0)));
            var mobState = entMan.System<MobStateSystem>();
            mobState.ChangeMobState(deadHellhound, MobState.Dead);
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);
            var ordinaryUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-2, 0)));

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                var ui = entMan.System<SharedUserInterfaceSystem>();

                var internalCamera = GetHoundPadInternalCamera(entMan, pad);
                var cameraComputer = entMan.GetComponent<RMCCameraComputerComponent>(internalCamera);
                Assert.That(cameraComputer.Title, Is.EqualTo("cmu-yautja-houndpad-interface-title"));
                Assert.That(cameraComputer.ViewportSize, Is.EqualTo(new Vector2i(672, 480)));
                Assert.That(cameraComputer.ProtoIds, Does.Contain("CMUMobYautjaHellhound"));
                Assert.That(cameraComputer.CameraIds, Does.Contain(entMan.GetNetEntity(hellhound)));
                Assert.That(cameraComputer.CameraIds, Does.Contain(entMan.GetNetEntity(otherHellhound)));
                Assert.That(cameraComputer.CameraIds, Does.Not.Contain(entMan.GetNetEntity(deadHellhound)));

                var firstCamera = entMan.GetComponent<RMCCameraComponent>(hellhound);
                var secondCamera = entMan.GetComponent<RMCCameraComponent>(otherHellhound);
                Assert.That(firstCamera.Id, Is.EqualTo("CMUMobYautjaHellhound"));
                Assert.That(secondCamera.Id, Is.EqualTo("CMUMobYautjaHellhound"));

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(ordinaryUser));
                Assert.That(ui.IsUiOpen(internalCamera, RMCCameraUiKey.Key, ordinaryUser), Is.False,
                    "CMSS13 houndcam attack_hand is Yautja gear; local non-tech users should not open the camera interface.");

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));
                Assert.That(ui.IsUiOpen(internalCamera, RMCCameraUiKey.Key, hunter), Is.True,
                    "CMSS13 /obj/item/device/houndcam/attack_hand calls internal_camera.tgui_interact(user); local use should open the camera UI.");
                Assert.That(ui.IsUiOpen(pad, RMCCameraUiKey.Key, hunter), Is.False);

                var eye = entMan.GetComponent<EyeComponent>(hunter);
                Assert.That(eye.Target, Is.Null);
                Assert.That(entMan.HasComponent<YautjaHoundWatchingComponent>(hunter), Is.False);
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(ordinaryUser))
                    entMan.DeleteEntity(ordinaryUser);
                if (!entMan.Deleted(pad))
                    entMan.DeleteEntity(pad);
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
                if (!entMan.Deleted(otherHellhound))
                    entMan.DeleteEntity(otherHellhound);
                if (!entMan.Deleted(deadHellhound))
                    entMan.DeleteEntity(deadHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundPadUseDoesNotPlayActivationSoundLikeCmss13AttackHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var internalCamera = GetHoundPadInternalCamera(entMan, pad);
                var beforeAudio = AudioEntities(entMan);

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));

                Assert.Multiple(() =>
                {
                    Assert.That(ui.IsUiOpen(internalCamera, RMCCameraUiKey.Key, hunter), Is.True,
                        "CMSS13 /obj/item/device/houndcam/attack_hand only delegates to internal_camera.tgui_interact(user).");
                    Assert.That(entMan.GetComponent<RMCCameraComputerComponent>(internalCamera).CameraIds,
                        Does.Contain(entMan.GetNetEntity(hellhound)));
                    Assert.That(AudioFileNamesAfter(entMan, beforeAudio), Is.Empty,
                        "CMSS13 houndcam attack_hand has no playsound() call.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, hellhound, pad })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundPadClearsSelectedFeedWhenHellhoundDiesLikeCmss13LiveCamera()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var session = server.PlayerMan.Sessions.Single();
            var previousAttached = session.AttachedEntity;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);

            try
            {
                server.PlayerMan.SetAttachedEntity(session, hunter);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var internalCamera = GetHoundPadInternalCamera(entMan, pad);
                var cameraComputer = entMan.GetComponent<RMCCameraComputerComponent>(internalCamera);
                var netHellhound = entMan.GetNetEntity(hellhound);

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));
                Assert.That(ui.IsUiOpen(internalCamera, RMCCameraUiKey.Key, hunter), Is.True,
                    "CMSS13 houndcam attack_hand delegates to the internal camera console UI.");

                ui.RaiseUiMessage(internalCamera, RMCCameraUiKey.Key, new RMCCameraWatchBuiMsg(netHellhound)
                {
                    Actor = hunter,
                });

                Assert.Multiple(() =>
                {
                    Assert.That(cameraComputer.CurrentCamera, Is.EqualTo(hellhound));
                    Assert.That(entMan.TryGetComponent<RMCCameraWatcherComponent>(hunter, out var watcher), Is.True);
                    Assert.That(watcher!.Overrides, Does.Contain(netHellhound),
                        "Selecting a CMSS13 hound camera switches the console to that live camera feed.");
                    Assert.That(entMan.GetComponent<EyeComponent>(hunter).Target, Is.Null);
                    Assert.That(entMan.HasComponent<YautjaHoundWatchingComponent>(hunter), Is.False);
                });

                mobState.ChangeMobState(hellhound, MobState.Dead);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<RMCCameraComponent>(hellhound), Is.False,
                        "Dead Hellhounds should no longer expose a live hound camera feed.");
                    Assert.That(cameraComputer.CameraIds, Does.Not.Contain(netHellhound));
                    Assert.That(cameraComputer.CurrentCamera, Is.Null,
                        "The houndpad internal camera console should drop the selected feed when the live Hellhound camera is removed.");
                    Assert.That(entMan.GetComponent<RMCCameraWatcherComponent>(hunter).Overrides,
                        Does.Not.Contain(netHellhound),
                        "CMSS13 camera consoles show static or clear when the selected camera is no longer usable instead of keeping a stale live feed subscription.");
                });
            }
            finally
            {
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                foreach (var uid in new[] { hunter, hellhound, pad })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundPadRestoresHellhoundFeedWhenHoundReturnsAliveLikeCmss13LiveList()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var internalCamera = GetHoundPadInternalCamera(entMan, pad);
                var cameraComputer = entMan.GetComponent<RMCCameraComputerComponent>(internalCamera);
                var netHellhound = entMan.GetNetEntity(hellhound);

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));
                Assert.That(cameraComputer.CameraIds, Does.Contain(netHellhound));

                mobState.ChangeMobState(hellhound, MobState.Dead);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<RMCCameraComponent>(hellhound), Is.False,
                        "Dead Hellhounds should leave the hound camera feed list like CMSS13 houndcam filtering of non-live feeds.");
                    Assert.That(cameraComputer.CameraIds, Does.Not.Contain(netHellhound));
                });

                mobState.ChangeMobState(hellhound, MobState.Alive);
                Assert.That(entMan.TryGetComponent<RMCCameraComponent>(hellhound, out var revivedCamera), Is.True,
                    "CMSS13 houndcam reads the live Hellhound set each time; a Hellhound returning to a live state must expose a live camera feed again.");
                Assert.Multiple(() =>
                {
                    Assert.That(revivedCamera!.Id, Is.EqualTo("CMUMobYautjaHellhound"));
                    Assert.That(revivedCamera.Rename, Is.False,
                        "Houndcam feeds should use the Hellhound mob name, not area-renamed security-camera labels.");
                });

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));
                AssertCameraEntry(entMan, cameraComputer, hellhound, "Hellhound");
            }
            finally
            {
                foreach (var uid in new[] { hunter, hellhound, pad })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundPadKeepsHellhoundCameraNamesAndRemovesMatchingNamesLikeCmss13InternalCamera()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var metadata = entMan.System<MetaDataSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var first = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var second = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));
            var duplicateA = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(3, 0)));
            var duplicateB = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(4, 0)));
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                metadata.SetEntityName(first, "A'ke Hellhound");
                metadata.SetEntityName(second, "N'dui Hellhound");
                metadata.SetEntityName(duplicateA, "Hellhound");
                metadata.SetEntityName(duplicateB, "Hellhound");

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));

                var internalCamera = GetHoundPadInternalCamera(entMan, pad);
                var cameraComputer = entMan.GetComponent<RMCCameraComputerComponent>(internalCamera);
                var firstCamera = entMan.GetComponent<RMCCameraComponent>(first);
                var secondCamera = entMan.GetComponent<RMCCameraComponent>(second);

                Assert.Multiple(() =>
                {
                    Assert.That(firstCamera.Rename, Is.False,
                        "CMSS13 houndcam internal camera lists live Hellhound mobs by their mob name, not an area-renamed security-camera label.");
                    Assert.That(secondCamera.Rename, Is.False,
                        "CMSS13 houndcam internal camera lists live Hellhound mobs by their mob name, not an area-renamed security-camera label.");
                    AssertCameraEntry(entMan, cameraComputer, first, "A'ke Hellhound");
                    AssertCameraEntry(entMan, cameraComputer, second, "N'dui Hellhound");
                    AssertCameraEntry(entMan, cameraComputer, duplicateA, "Hellhound");
                    AssertCameraEntry(entMan, cameraComputer, duplicateB, "Hellhound");
                    Assert.That(cameraComputer.CameraIds.Count, Is.EqualTo(4));
                    Assert.That(cameraComputer.CameraNames.Count, Is.EqualTo(4));
                });

                mobState.ChangeMobState(second, MobState.Dead);
                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<RMCCameraComponent>(second), Is.False,
                        "Dead Hellhounds should leave the hound camera feed list like CMSS13 houndcam filtering of non-live feeds.");
                    AssertCameraEntry(entMan, cameraComputer, first, "A'ke Hellhound");
                    Assert.That(cameraComputer.CameraIds, Does.Not.Contain(entMan.GetNetEntity(second)));
                    AssertCameraEntry(entMan, cameraComputer, duplicateA, "Hellhound");
                    AssertCameraEntry(entMan, cameraComputer, duplicateB, "Hellhound");
                    Assert.That(cameraComputer.CameraIds.Count, Is.EqualTo(3));
                    Assert.That(cameraComputer.CameraNames.Count, Is.EqualTo(3));
                });

                entMan.DeleteEntity(duplicateA);
                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));

                Assert.Multiple(() =>
                {
                    AssertCameraEntry(entMan, cameraComputer, first, "A'ke Hellhound");
                    AssertCameraEntry(entMan, cameraComputer, duplicateB, "Hellhound");
                    Assert.That(cameraComputer.CameraIds.Count, Is.EqualTo(2));
                    Assert.That(cameraComputer.CameraNames.Count, Is.EqualTo(2),
                        "Removing one duplicate Hellhound camera must remove the name at the same index, not all matching duplicate names.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, first, second, duplicateA, duplicateB, pad })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HoundPadOwnsInternalCameraComputerLikeCmss13InitializeDestroy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var pad = entMan.SpawnEntity("CMUYautjaHoundObservationPad", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(entMan.HasComponent<RMCCameraComputerComponent>(pad), Is.False,
                    "CMSS13 houndcam stores an internal camera computer instead of being the camera computer itself.");

                var internalCamera = EntityUid.Invalid;
                var query = entMan.EntityQueryEnumerator<RMCCameraComputerComponent, TransformComponent, MetaDataComponent>();
                while (query.MoveNext(out var uid, out _, out var xform, out var meta))
                {
                    if (xform.ParentUid != pad)
                        continue;

                    Assert.That(internalCamera, Is.EqualTo(EntityUid.Invalid),
                        "CMSS13 /obj/item/device/houndcam/Initialize() creates one internal camera computer.");
                    internalCamera = uid;
                    Assert.That(meta.EntityPrototype?.ID, Is.EqualTo("CMUYautjaHoundObservationPadInternalCamera"));
                }

                Assert.That(internalCamera, Is.Not.EqualTo(EntityUid.Invalid));

                var cameraComputer = entMan.GetComponent<RMCCameraComputerComponent>(internalCamera);
                Assert.That(cameraComputer.Title, Is.EqualTo("cmu-yautja-houndpad-interface-title"));
                Assert.That(cameraComputer.ViewportSize, Is.EqualTo(new Vector2i(672, 480)));
                Assert.That(cameraComputer.ProtoIds, Does.Contain("CMUMobYautjaHellhound"));

                entMan.EventBus.RaiseLocalEvent(pad, new UseInHandEvent(hunter));
                Assert.That(cameraComputer.CameraIds, Does.Contain(entMan.GetNetEntity(hellhound)));
                Assert.That(ui.IsUiOpen(internalCamera, RMCCameraUiKey.Key, hunter), Is.True,
                    "CMSS13 houndcam attack_hand delegates to internal_camera.tgui_interact(user).");
                Assert.That(ui.IsUiOpen(pad, RMCCameraUiKey.Key, hunter), Is.False);

                var internalTransform = entMan.GetComponent<TransformComponent>(internalCamera);
                Assert.That(internalTransform.ParentUid, Is.EqualTo(pad));

                entMan.DeleteEntity(pad);
                Assert.That(entMan.Deleted(internalCamera) || entMan.IsQueuedForDeletion(internalCamera), Is.True,
                    "CMSS13 /obj/item/device/houndcam/Destroy() QDEL_NULLs the internal camera computer.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, hellhound, pad })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundGhostRoleAllowsSpeechLikeCmss13XenoSay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);

            try
            {
                var ghostRole = entMan.GetComponent<GhostRoleComponent>(hellhound);
                Assert.That(ghostRole.AllowSpeech, Is.True,
                    "CMSS13 Hellhounds inherit live xeno say() behavior, so taken Hellhound ghost roles should not disable speech.");
            }
            finally
            {
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundHasCmss13CasteProfileAndAbilities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);

            try
            {
                var job = prototypes.Index(HellhoundJob);
                Assert.That(job.Name, Is.EqualTo("cmu-yautja-job-name-hellhound"));
                Assert.That(job.Supervisors, Is.EqualTo("cm-job-supervisors-nobody"));

                var xeno = entMan.GetComponent<XenoComponent>(hellhound);
                Assert.That(xeno.Tier, Is.EqualTo(0));
                Assert.That(xeno.Role, Is.EqualTo(HellhoundJob));
                Assert.That(xeno.ContributesToVictory, Is.False);
                Assert.That(xeno.CountedInSlots, Is.False);
                Assert.That(xeno.AutoAssignHive, Is.False);
                Assert.That(xeno.AccessLevels, Is.Empty);
                Assert.That(xeno.ActionIds, Does.Contain("ActionXenoRest"));
                Assert.That(xeno.ActionIds, Does.Contain("ActionXenoRegurgitate"));
                Assert.That(xeno.ActionIds, Does.Contain("ActionXenoHide"));
                Assert.That(xeno.ActionIds, Does.Contain("CMUActionYautjaHellhoundGorge"));
                Assert.That(xeno.ActionIds, Does.Contain("CMUActionYautjaHellhoundSenseOwner"));
                Assert.That(xeno.ActionIds, Does.Contain("ActionXenoZoom"));

                Assert.That(entMan.HasComponent<XenoDevourComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<XenoHideComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<XenoZoomComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<XenoLeapComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<VentCrawlerComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<TacticalMapIconComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<RMCCameraComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<PullerComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<PullWhitelistComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<SlowOnPullComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<HandsComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<GiveHandsComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<RMCAllowXenoPullToggleStopComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<RMCNightVisionVisibleComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<HideHealthBarComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<InternalsComponent>(hellhound), Is.False);
                Assert.That(entMan.HasComponent<BodyZoneTargetingComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<XenoFriendlyComponent>(hellhound), Is.False);
                Assert.That(entMan.HasComponent<HiveMemberComponent>(hellhound), Is.False);
                Assert.That(entMan.HasComponent<HiveTrackerComponent>(hellhound), Is.False);

                var faction = entMan.GetComponent<NpcFactionMemberComponent>(hellhound);
                Assert.That(faction.Factions.Select(id => id.ToString()), Does.Contain("CMUYautja"));
                Assert.That(faction.Factions.Select(id => id.ToString()), Does.Not.Contain("RMCXeno"));

                var eye = entMan.GetComponent<EyeComponent>(hellhound);
                Assert.That(eye.PvsScale, Is.EqualTo(1.5f).Within(0.01f));

                var nightVision = entMan.GetComponent<NightVisionComponent>(hellhound);
                Assert.That(nightVision.Alert?.ToString(), Is.EqualTo("XenoNightVision"));
                Assert.That(nightVision.State, Is.EqualTo(NightVisionState.Full));
                Assert.That(nightVision.Overlay, Is.True);

                var plasma = entMan.GetComponent<XenoPlasmaComponent>(hellhound);
                Assert.That(plasma.MaxPlasma, Is.EqualTo(0));

                var speech = entMan.GetComponent<SpeechComponent>(hellhound);
                Assert.That(speech.AllowedEmotes.Select(id => id.ToString()), Is.EquivalentTo(new[]
                {
                    "CMUYautjaHellhoundRoar",
                    "CMUYautjaHellhoundGrowl",
                    "CMUYautjaHellhoundHiss",
                }), "CMSS13 Hellhound uses its own no-keybind roar/growl/hiss emotes and is blacklisted from normal xeno needshelp.");

                Assert.Multiple(() =>
                {
                    var roar = prototypes.Index<EmotePrototype>("CMUYautjaHellhoundRoar");
                    var growl = prototypes.Index<EmotePrototype>("CMUYautjaHellhoundGrowl");
                    var hiss = prototypes.Index<EmotePrototype>("CMUYautjaHellhoundHiss");

                    Assert.That(roar.ChatMessages, Is.EqualTo(new[] { "rmc-emote-xeno-roar" }));
                    Assert.That(growl.ChatMessages, Is.EqualTo(new[] { "cmu-yautja-hellhound-emote-growl" }));
                    Assert.That(hiss.ChatMessages, Is.EqualTo(new[] { "rmc-emote-hiss" }));
                    Assert.That(roar.ChatTriggers, Is.Empty);
                    Assert.That(growl.ChatTriggers, Is.Empty);
                    Assert.That(hiss.ChatTriggers, Is.Empty);
                    Assert.That(speech.EmoteOverrides["XenoRoar"].ToString(), Is.EqualTo("CMUYautjaHellhoundRoar"));
                    Assert.That(speech.EmoteOverrides["Growl"].ToString(), Is.EqualTo("CMUYautjaHellhoundGrowl"));
                    Assert.That(speech.EmoteOverrides["Hiss"].ToString(), Is.EqualTo("CMUYautjaHellhoundHiss"));
                    Assert.That(prototypes.Index<EmotePrototype>("XenoHelp").ChatTriggers, Does.Contain("needshelp"));
                    Assert.That(speech.AllowedEmotes.Select(id => id.ToString()), Does.Not.Contain("XenoHelp"));
                    Assert.That(entMan.GetComponent<XenoComponent>(hellhound).EmoteSounds?.ToString(), Is.EqualTo("Xeno"),
                        "CMSS13 Hellhound source audio is ed209_20sec plus giant_lizard growl/hiss, but those assets are not present in the local source mirror yet.");
                });

                var chat = entMan.System<ChatSystem>();
                var listener = entMan.System<YautjaTestSpeechListenerSystem>();
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(hellhound);
                listener.Emotes.Clear();
                chat.TryEmoteWithChat(hellhound, "Growl", ChatTransmitRange.HideChat, forceEmote: true, ignoreActionBlocker: true);
                Assert.That(listener.Emotes, Does.Contain((hellhound, "CMUYautjaHellhoundGrowl")),
                    "Direct generic Growl emotes should use the Hellhound-specific emote datum.");

                listener.Emotes.Clear();
                chat.TrySendInGameICMessage(hellhound, "growl", InGameICChatType.Emote, ChatTransmitRange.HideChat, ignoreActionBlocker: true);
                Assert.That(listener.Emotes, Does.Contain((hellhound, "CMUYautjaHellhoundGrowl")),
                    "CMSS13 lets Hellhounds use the same growl key but resolves it to the Hellhound-specific emote datum.");

                var regen = entMan.GetComponent<XenoRegenComponent>(hellhound);
                Assert.That(regen.RestHealMultiplier, Is.EqualTo((FixedPoint2) 2.5));
                Assert.That(regen.StandHealingMultiplier, Is.EqualTo((FixedPoint2) 1.25));
                Assert.That(regen.CritHealMultiplier, Is.EqualTo((FixedPoint2) 1.25));
                Assert.That(regen.HealOffWeeds, Is.True);

                var tackle = entMan.GetComponent<TackleComponent>(hellhound);
                Assert.That(tackle.Min, Is.EqualTo(4));
                Assert.That(tackle.Max, Is.EqualTo(5));
                Assert.That(tackle.Chance, Is.EqualTo(0.4f));
                Assert.That(tackle.StunMin, Is.EqualTo(TimeSpan.FromSeconds(4)));
                Assert.That(tackle.StunMax, Is.EqualTo(TimeSpan.FromSeconds(4)));

                var combat = entMan.GetComponent<CombatModeComponent>(hellhound);
                Assert.That(combat.CanDisarm, Is.True);
                Assert.That(combat.BaseDisarmFailChance, Is.EqualTo(0));

                var slowOnPull = entMan.GetComponent<SlowOnPullComponent>(hellhound);
                Assert.That(GetSlowdownFor(slowOnPull, "Marine"), Is.EqualTo(0.3825f).Within(0.0001f));
                Assert.That(GetSlowdownFor(slowOnPull, "XenoLight"), Is.EqualTo(0.425f).Within(0.0001f));
                Assert.That(GetSlowdownFor(slowOnPull, "XenoHeavy"), Is.EqualTo(0.2475f).Within(0.0001f));

                var size = entMan.GetComponent<RMCSizeComponent>(hellhound);
                Assert.That(size.Size, Is.EqualTo(RMCSizes.SmallXeno));

                var thresholds = entMan.GetComponent<MobThresholdsComponent>(hellhound);
                Assert.That(thresholds.Thresholds[(FixedPoint2) 230], Is.EqualTo(MobState.Critical));
                Assert.That(thresholds.Thresholds[(FixedPoint2) 330], Is.EqualTo(MobState.Dead));
                Assert.That(thresholds.StateAlertDict.Keys, Does.Contain(MobState.Alive));
                Assert.That(thresholds.StateAlertDict.Keys, Does.Contain(MobState.Critical));
                Assert.That(thresholds.StateAlertDict.Keys, Does.Contain(MobState.Dead));
                Assert.That(thresholds.DisplayDamageInAlert, Is.True);

                var damageable = entMan.GetComponent<DamageableComponent>(hellhound);
                Assert.That(damageable.HealthBarThreshold, Is.Null);

                var movement = entMan.GetComponent<MovementSpeedModifierComponent>(hellhound);
                Assert.That(movement.BaseWalkSpeed, Is.EqualTo(5.55f).Within(0.01f));
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(10f).Within(0.01f));

                var melee = entMan.GetComponent<MeleeWeaponComponent>(hellhound);
                Assert.That(melee.AttackRate, Is.EqualTo(1.4f));
                Assert.That(melee.Damage.GetTotal(), Is.EqualTo((FixedPoint2) 22.5));

                var leap = entMan.GetComponent<XenoLeapComponent>(hellhound);
                var leapDamage = new DamageSpecifier(leap.Damage);
                Assert.That(leap.CanBeShieldBlocked, Is.False,
                    "CMSS13 /datum/action/xeno_action/activable/pounce/gorge sets can_be_shield_blocked = FALSE.");

                var armor = entMan.GetComponent<CMArmorComponent>(hellhound);
                Assert.That(armor.XenoArmor, Is.GreaterThanOrEqualTo(15));
                Assert.That(armor.ExplosionArmor, Is.GreaterThanOrEqualTo(10));

                var gorge = entMan.SpawnEntity("CMUActionYautjaHellhoundGorge", MapCoordinates.Nullspace);
                var senseOwner = entMan.SpawnEntity("CMUActionYautjaHellhoundSenseOwner", MapCoordinates.Nullspace);
                try
                {
                    var gorgeAction = entMan.GetComponent<ActionComponent>(gorge);
                    var senseOwnerAction = entMan.GetComponent<ActionComponent>(senseOwner);
                    var senseOwnerPrototype = prototypes.Index<EntityPrototype>("CMUActionYautjaHellhoundSenseOwner");
                    Assert.Multiple(() =>
                    {
                        Assert.That(leapDamage.GetTotal(), Is.EqualTo((FixedPoint2) 30),
                            "CMSS13 /datum/action/xeno_action/activable/pounce/gorge has gorge_damage = 30, separate from Hellhound melee damage.");
                        Assert.That(gorgeAction.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(5)),
                            "CMSS13 Hellhound Gorge xeno_cooldown = 5 SECONDS.");
                        Assert.That(gorgeAction.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());
                        Assert.That(((SpriteSpecifier.Rsi) gorgeAction.Icon!).RsiState, Is.EqualTo("headbite"),
                            "CMSS13 Hellhound Gorge action_icon_state = headbite.");
                        Assert.That(senseOwnerPrototype.SetName, Is.EqualTo("Find Owner"),
                            "CMSS13 /datum/action/xeno_action/onclick/sense_owner name = Find Owner.");
                        Assert.That(senseOwnerAction.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(1)),
                            "CMSS13 /datum/action/xeno_action/onclick/sense_owner xeno_cooldown = 1 SECONDS.");
                        Assert.That(senseOwnerAction.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());
                        Assert.That(((SpriteSpecifier.Rsi) senseOwnerAction.Icon!).RsiState, Is.EqualTo("mark_hosts"),
                            "CMSS13 /datum/action/xeno_action/onclick/sense_owner action_icon_state = mark_hosts.");
                    });
                }
                finally
                {
                    entMan.DeleteEntity(gorge);
                    entMan.DeleteEntity(senseOwner);
                }
            }
            finally
            {
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundGorgeIgnoresLeapProtectionLikeCmss13CanBeShieldBlockedFalse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            var protectedTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<XenoLeapingComponent>(hellhound);
                var protection = entMan.EnsureComponent<RMCLeapProtectionComponent>(protectedTarget);
                protection.FullProtection = true;

                var hitAttempt = new XenoLeapHitAttempt(hellhound);
                entMan.EventBus.RaiseLocalEvent(protectedTarget, ref hitAttempt);

                Assert.That(hitAttempt.Cancelled, Is.False,
                    "CMSS13 /datum/action/xeno_action/activable/pounce/gorge sets can_be_shield_blocked = FALSE, so Hellhound Gorge should ignore leap-protection blockers.");
            }
            finally
            {
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
                if (!entMan.Deleted(protectedTarget))
                    entMan.DeleteEntity(protectedTarget);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundExamineTextMatchesCmss13HumanAndYautjaBranches()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var loc = server.ResolveDependency<ILocalizationManager>();
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var examine = entMan.System<ExamineSystem>();
                var metadata = entMan.System<MetaDataSystem>();

                var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
                var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(2, 0)));
                var owner = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));

                try
                {
                    metadata.SetEntityName(owner, "A'ke Ret");

                    var humanText = examine.GetExamineText(hellhound, human).ToMarkup();
                    var unownedHunterText = examine.GetExamineText(hellhound, hunter).ToMarkup();

                    entMan.GetComponent<YautjaHellhoundComponent>(hellhound).YautjaOwner = owner;
                    var ownedHunterText = examine.GetExamineText(hellhound, hunter).ToMarkup();
                    var ownedHumanText = examine.GetExamineText(hellhound, human).ToMarkup();

                    Assert.Multiple(() =>
                    {
                        Assert.That(humanText,
                            Does.Contain("You can barely make out the symbols but it reads out"),
                            "CMSS13 Hellhound get_examine_text() shows humans the undeciphered symbol text.");
                        Assert.That(humanText, Does.Contain("ⵍⴻⴱⵔⵓ"));
                        Assert.That(unownedHunterText,
                            Does.Contain("It's not owned by anyone."),
                            "CMSS13 Hellhound get_examine_text() tells Yautja when the hound has no owner.");
                        Assert.That(ownedHunterText,
                            Does.Contain("Its owner is A'ke Ret!"),
                            "CMSS13 Hellhound get_examine_text() tells Yautja the owner's real_name.");
                        Assert.That(ownedHumanText,
                            Does.Not.Contain("Its owner is"),
                            "CMSS13 only exposes the owner line to Yautja examiners.");
                    });
                }
                finally
                {
                    foreach (var uid in new[] { hellhound, human, hunter, owner })
                    {
                        if (!entMan.Deleted(uid))
                            entMan.DeleteEntity(uid);
                    }
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (previousCulture != null)
                    server.ResolveDependency<ILocalizationManager>().SetCulture(previousCulture);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundSenseOwnerReportsDirectionImmediatelyLikeCmss13UseAbility()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid owner = default;
        EntityUid hellhound = default;
        EntityUid action = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
                owner = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
                action = entMan.SpawnEntity("CMUActionYautjaHellhoundSenseOwner", MapCoordinates.Nullspace);
                entMan.GetComponent<YautjaHellhoundComponent>(hellhound).YautjaOwner = owner;
                metadata.SetEntityName(owner, "A'ke Ret");
                server.PlayerMan.SetAttachedEntity(session, hellhound);

                var senseOwner = new YautjaHellhoundSenseOwnerActionEvent
                {
                    Performer = hellhound,
                    Action = (action, entMan.GetComponent<ActionComponent>(action)),
                };
                entMan.EventBus.RaiseLocalEvent(hellhound, senseOwner);
                Assert.That(senseOwner.Handled, Is.True);
            });

            await pair.ReallyBeIdle(10);
            await client.WaitAssertion(() =>
            {
                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg.Message)
                    .ToList();
                var joinedMessages = string.Join("\n", history);

                Assert.That(
                    history,
                    Has.Some.Contains("Your owner is 4 meters to the east."),
                    $"CMSS13 sense_owner/use_ability() sends the distance and direction text immediately in the same ability use.\nActual chat history:\n{joinedMessages}");
            });
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

                foreach (var uid in new[] { owner, hellhound, action })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HellhoundTacklesHumansLikeRunner()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.That(entMan.GetComponent<CombatModeComponent>(hellhound).CanDisarm, Is.True);
                Assert.That(entMan.HasComponent<TackleComponent>(hellhound), Is.True);
                Assert.That(entMan.HasComponent<TackleableComponent>(target), Is.True);

                for (var i = 0; i < 5; i++)
                {
                    var disarm = new CMDisarmEvent(hellhound);
                    entMan.EventBus.RaiseLocalEvent(target, ref disarm);
                    Assert.That(disarm.Handled, Is.True);
                }

                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True);
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
    public async Task HellhoundPullDoesNotTackleButDisarmCanTacklePulledMob()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var combatMode = entMan.System<Content.Server.CombatMode.CombatModeSystem>();
            var meleeSystem = entMan.System<Content.Server.Weapons.Melee.MeleeWeaponSystem>();
            var pulling = entMan.System<PullingSystem>();
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                Assert.That(pulling.TryStartPull(hellhound, target), Is.True);
                Assert.That(entMan.GetComponent<PullerComponent>(hellhound).Pulling, Is.EqualTo(target));
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False);

                combatMode.SetInCombatMode(hellhound, true);
                var weapon = entMan.GetComponent<MeleeWeaponComponent>(hellhound);
                for (var i = 0; i < 5 && !entMan.HasComponent<KnockedDownComponent>(target); i++)
                {
                    weapon.NextAttack = TimeSpan.Zero;
                    Assert.That(meleeSystem.AttemptDisarmAttack(hellhound, hellhound, weapon, target), Is.True);
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
    public async Task HellhoundDealsMoreMeleeDamageWhenTargetingLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bodyZone = entMan.System<Content.Server._CMU14.Medical.Anatomy.BodyParts.BodyZoneTargetingSystem>();
            var meleeSystem = entMan.System<Content.Server.Weapons.Melee.MeleeWeaponSystem>();
            var hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords);

            try
            {
                var melee = entMan.GetComponent<MeleeWeaponComponent>(hellhound);
                bodyZone.SelectZone((hellhound, null), TargetBodyZone.Chest);
                var chestDamage = meleeSystem.GetDamage(hellhound, hellhound, melee);

                bodyZone.SelectZone((hellhound, null), TargetBodyZone.LeftLeg);
                var legDamage = meleeSystem.GetDamage(hellhound, hellhound, melee);

                Assert.That(legDamage.GetTotal(), Is.GreaterThan(chestDamage.GetTotal()));
                Assert.That(legDamage.GetTotal().Float(), Is.EqualTo(chestDamage.GetTotal().Float() * 1.15f).Within(0.02f));
            }
            finally
            {
                if (!entMan.Deleted(hellhound))
                    entMan.DeleteEntity(hellhound);
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
    public async Task HuntingTrapBasePrototypeMatchesCmss13SourceDescription()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("hunting trap"));
                    Assert.That(meta.EntityDescription, Is.EqualTo("A bizarre Yautja device used for trapping and killing prey."),
                        "CMSS13 /obj/item/hunting_trap source description should stay source-shaped for the base local prototype.");
                });
            }
            finally
            {
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
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
    public async Task HuntingTrapTriggerShowsBreakFreeAlertLikeCmss13ResistibleTether()
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

                Assert.That(alerts.IsShowingAlert(target, "CMUYautjaTrapBreakFree"), Is.True,
                    "CMSS13 /obj/item/hunting_trap/trapMob() calls apply_tether(..., resistible = TRUE), so trapped mobs need a self-resist alert.");

                Assert.That(trapSystem.TryDisarmTrap((trap, trapComp), hunter), Is.True);

                Assert.That(alerts.IsShowingAlert(target, "CMUYautjaTrapBreakFree"), Is.False,
                    "The trap break-free alert must clear when the tether is removed.");
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
    public async Task HuntingTrapBreakFreeAlertRepeatedClickDoesNotCancelActiveResist()
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
                var activeBefore = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaTrapBreakFreeDoAfterEvent);

                Assert.That(alerts.ActivateAlert(target, alert!), Is.True,
                    "Repeated break-free clicks should be treated as the same active resist attempt, not as a cancel.");

                var activeAfter = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaTrapBreakFreeDoAfterEvent);
                Assert.That(activeAfter.Index, Is.EqualTo(activeBefore.Index),
                    "DoAfter duplicate handling cancels then blocks when both flags are enabled; the trap system must avoid restarting an already active break-free.");
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
    public async Task HuntingTrapAutoDisarmsAfterCmss13ThirtySeconds()
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
                var timing = server.ResolveDependency<IGameTiming>();

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var triggeredAt = timing.CurTime;
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.TrappedMob, Is.EqualTo(target));
                    Assert.That(trapComp.ReleaseAt - triggeredAt,
                        Is.EqualTo(TimeSpan.FromSeconds(30)).Within(TimeSpan.FromMilliseconds(50)),
                        "CMSS13 /obj/item/hunting_trap/trapMob() schedules disarm after duration = 30 SECONDS.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.True);
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(30.1f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.Armed, Is.False);
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(trapComp.ReleaseAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.False);
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
    public async Task HuntingTrapYautjaRecoverDisarmsBeforePickupLikeCmss13AttackHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);
            var fillerLeft = entMan.SpawnEntity("Crowbar", map.GridCoords.Offset(new Vector2(2, 0)));
            var fillerRight = entMan.SpawnEntity("CMMRE", map.GridCoords.Offset(new Vector2(3, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, fillerLeft), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, fillerRight), Is.True);
                Assert.That(hands.TryGetEmptyHand(hunter, out _), Is.False);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
                Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.True);

                Assert.That(trapSystem.TryRecoverTrap((trap, trapComp), hunter), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.TrappedMob, Is.Null,
                        "CMSS13 /obj/item/hunting_trap/attack_hand() calls disarm(user) for Yautja before the base hand pickup path.");
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.False);
                    Assert.That(hands.IsHolding(hunter, trap), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, trap, fillerLeft, fillerRight })
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
                    Assert.That(damage.TotalDamage, Is.EqualTo((FixedPoint2) 20),
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
                    Assert.That(damage.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
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
    public async Task HuntingTrapBuckledMobStepDoesNotTriggerLikeCmss13Crossed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var buckle = entMan.System<SharedBuckleSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var seat = entMan.SpawnEntity("RMCSeatHunter", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.HasComponent<BuckleComponent>(target), Is.True);
                Assert.That(entMan.HasComponent<StrapComponent>(seat), Is.True);
                Assert.That(buckle.TryBuckle(target, target, seat, popup: false), Is.True);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var ev = new StepTriggeredOnEvent(trap, target);
                entMan.EventBus.RaiseLocalEvent(trap, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BuckleComponent>(target).Buckled, Is.True);
                    Assert.That(trapComp.Armed, Is.True,
                        "CMSS13 /obj/item/hunting_trap/Crossed() checks !trap_mob.buckled before carbon and animal trigger branches.");
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(target), Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, target, seat, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapBadBloodHiveStepAvoidsBadBloodTrapLikeCmss13Crossed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hiveEnt = default;
        EntityUid xeno = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hive = entMan.System<SharedXenoHiveSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(8, 0)));
                hiveEnt = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(9, 0)));
                xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<NpcFactionMemberComponent>(hunter).Factions.Add("CMUYautjaBadBlood");
                hive.SetHive(xeno, hiveEnt);
                hive.SetHiveFactionAlly("CMUYautjaBadBlood", hiveEnt, true);
                server.PlayerMan.SetAttachedEntity(session, xeno);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var ev = new StepTriggerAttemptEvent { Source = trap, Tripper = xeno };
                entMan.EventBus.RaiseLocalEvent(trap, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Cancelled, Is.True,
                        "CMSS13 /obj/item/hunting_trap/Crossed() lets XENO_HIVE_YAUTJA_BADBLOOD avoid traps armed by FACTION_YAUTJA_BADBLOOD.");
                    Assert.That(trapComp.Armed, Is.True);
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(entMan.HasComponent<RMCTetherComponent>(xeno), Is.False);
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Has.Some.StartsWith("We carefully avoid stepping on the trap."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, hiveEnt, xeno, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

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
    public async Task HuntingTrapConfigureRangeDialogMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<VerbSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var localVerbs = verbs.GetLocalVerbs(trap, hunter, typeof(InteractionVerb), force: true);
                var configure = localVerbs.Single(verb => verb.Text == "Configure Hunting Trap");
                configure.Act!.Invoke();

                Assert.That(entMan.TryGetComponent(trap, out DialogComponent? dialog), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(dialog!.Title, Is.EqualTo("Hunting Trap Range"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Which range would you like to set the hunting trap to?"));
                    Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "2", "3", "4", "5", "6", "7" }));
                    Assert.That(dialog.Options, Has.All.Matches<DialogOption>(option => option.Event is YautjaTrapRangeSelectedEvent));
                });

                RaiseDialogOption(entMan, trap, hunter, "7");

                Assert.That(entMan.GetComponent<YautjaTrapComponent>(trap).TetherRange, Is.EqualTo(7f),
                    "CMSS13 /obj/item/hunting_trap/configure_trap() writes tether_range to the selected list value.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTriggerShowsDisarmedSpriteLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(appearance.TryGetData<bool>(trap, ToggleableVisuals.Enabled, out var armedVisual), Is.True);
                Assert.That(armedVisual, Is.True);

                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(trapComp.Armed, Is.False);
                    Assert.That(trapComp.TrappedMob, Is.EqualTo(target));
                    Assert.That(appearance.TryGetData<bool>(trap, ToggleableVisuals.Enabled, out var triggeredVisual), Is.True);
                    Assert.That(triggeredVisual, Is.False);
                });
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
    public async Task HuntingTrapArmedVisualCamouflagesOnDirtAndGrassLikeCmss13Dropped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var appearance = entMan.System<SharedAppearanceSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            EntityUid hunter = default;
            EntityUid dirtTrap = default;
            EntityUid grassTrap = default;

            try
            {
                mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero, new Tile(tileDefs["FloorDirt"].TileId));
                mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(tileDefs["FloorGrass"].TileId));

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                dirtTrap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);
                grassTrap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var dirtComp = entMan.GetComponent<YautjaTrapComponent>(dirtTrap);
                var grassComp = entMan.GetComponent<YautjaTrapComponent>(grassTrap);
                Assert.That(trapSystem.TryArmTrap((dirtTrap, dirtComp), hunter), Is.True);
                Assert.That(trapSystem.TryArmTrap((grassTrap, grassComp), hunter), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(appearance.TryGetData<string>(dirtTrap, ToggleableVisuals.Layer, out var dirtState), Is.True);
                    Assert.That(dirtState, Is.EqualTo("yauttrapdirt"),
                        "CMSS13 /obj/item/hunting_trap/dropped() sets icon_state = \"yauttrapdirt\" when an armed trap lands on /turf/open/gm/dirt.");

                    Assert.That(appearance.TryGetData<string>(grassTrap, ToggleableVisuals.Layer, out var grassState), Is.True);
                    Assert.That(grassState, Is.EqualTo("yauttrapgrass"),
                        "CMSS13 /obj/item/hunting_trap/dropped() sets icon_state = \"yauttrapgrass\" when an armed trap lands on /turf/open/gm/grass.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, dirtTrap, grassTrap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

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
    public async Task HuntingTrapTryArmFailsWhenUserCriticalLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                mobState.ChangeMobState(hunter, MobState.Critical);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.False,
                        "CMSS13 /obj/item/hunting_trap/attack_self() only arms when !user.stat.");
                    Assert.That(trapComp.Armed, Is.False);
                    Assert.That(entMan.GetComponent<TransformComponent>(trap).Anchored, Is.False);
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

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
    public async Task HuntingTrapNonTechUseInHandDeniedPopupUsesCmss13AttackSelfText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                user = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                server.PlayerMan.SetAttachedEntity(session, user);
                Assert.That(hands.TryPickupAnyHand(user, trap), Is.True);

                var use = new UseInHandEvent(user);
                entMan.EventBus.RaiseLocalEvent(trap, use);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(trapComp.Armed, Is.False);
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You don't know how to use this thing!"),
                        "CMSS13 /obj/item/hunting_trap/attack_self() uses a trap-specific non-tech warning.");
                    Assert.That(labels, Does.Not.Contain("The alien technology refuses to respond."));
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

                foreach (var uid in new[] { user, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTechAuthorizedCanArmAndKeepsDefaultFactionLikeCmss13Trait()
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
                entMan.EnsureComponent<NpcFactionMemberComponent>(user).Factions.Add("CMUYautjaBadBlood");

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(trapSystem.TryArmTrap((trap, trapComp), user), Is.True,
                        "CMSS13 /obj/item/hunting_trap/attack_self() gates use on TRAIT_YAUTJA_TECH, not species.");
                    Assert.That(trapComp.Armed, Is.True);
                    Assert.That(trapComp.ArmedFaction.ToString(), Is.EqualTo("CMUYautja"),
                        "CMSS13 only updates armed_faction inside isspeciesyautja(user), so tech-authorized non-Yautja keep the default faction.");
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
    public async Task HuntingTrapTechCannotConfigureFixedRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<VerbSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);

                var localVerbs = verbs.GetLocalVerbs(trap, user, typeof(InteractionVerb), force: true);
                Assert.That(localVerbs, Has.None.Matches<InteractionVerb>(verb => verb.Text == "Configure Hunting Trap"));

                entMan.EventBus.RaiseLocalEvent(trap,
                    new YautjaTrapRangeSelectedEvent(entMan.GetNetEntity(user), 7));

                Assert.That(entMan.GetComponent<YautjaTrapComponent>(trap).TetherRange, Is.EqualTo(2f),
                    "A direct range event must not widen the fixed two-tile tether.");
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
    public async Task HuntingTrapArmPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("hunting trap is now armed."));
                    Assert.That(labels, Does.Not.Contain("You arm the hunting trap."));
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
    public async Task HuntingTrapPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
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
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, target);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("You get caught in the hunting trap!"));
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
    public async Task HuntingTrapYautjaStepShowsAvoidanceNoticeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(8, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var ev = new StepTriggerAttemptEvent { Source = trap, Tripper = hunter };
                entMan.EventBus.RaiseLocalEvent(trap, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Cancelled, Is.True);
                    Assert.That(trapComp.TrappedMob, Is.Null);
                    Assert.That(trapComp.Armed, Is.True);
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain("You carefully avoid stepping on the trap."));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task HuntingTrapStepTriggerShowsObserverMessageLikeCmss13Crossed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid viewer = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(8, 0)));
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                viewer = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, viewer);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);

                var ev = new StepTriggeredOnEvent(trap, target);
                entMan.EventBus.RaiseLocalEvent(trap, ref ev);

                Assert.That(trapComp.TrappedMob, Is.EqualTo(target));
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Has.Some.Contains("gets caught in hunting trap."),
                    "CMSS13 /obj/item/hunting_trap/Crossed() calls viewer.show_message('[trap_target] gets caught in [src].') after trapMob().");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, target, viewer, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapDisarmPopupUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
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
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
                Assert.That(trapSystem.TryDisarmTrap((trap, trapComp), hunter), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Contain("hunting trap is now disarmed."));
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
    public async Task HuntingTrapAdminLogsUseCmss13SourceSubjects()
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
                Assert.That(trapSystem.TryDisarmTrap((trap, trapComp), hunter), Is.True);
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
                    Has.Some.Contains("has armed a hunting trap").IgnoreCase,
                    $"CMSS13 /obj/item/hunting_trap/attack_self() logs '[user] has armed \\a [src]' where [src] is hunting trap.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("was caught in a hunting trap").IgnoreCase,
                    $"CMSS13 /obj/item/hunting_trap/trapMob() logs '[target] was caught in \\a [src]' where [src] is hunting trap.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.Some.Contains("has disarmed a hunting trap").IgnoreCase,
                    $"CMSS13 /obj/item/hunting_trap/disarm() logs '[user] has disarmed \\a [src]' where [src] is hunting trap.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.None.Contains("was freed from a hunting trap").IgnoreCase,
                    $"CMSS13 /obj/item/hunting_trap/disarm() clears trapped_mob without adding the snare-only freed attack log.\nActual logs:\n{joinedMessages}");
                Assert.That(
                    messages,
                    Has.None.Contains("Yautja hunting trap"),
                    $"Regular hunting-trap logs should use the source item subject instead of the old generic Yautja hunting trap subject.\nActual logs:\n{joinedMessages}");
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
    public async Task HuntingTrapTriggerBroadcastsToYautjaLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
                expectedBroadcast = $"A hunting trap has caught something in {areas.GetAreaName(trap)}!";
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var popups = client.EntMan.System<PopupSystem>();
                var labels = popups.WorldLabels.Select(label => label.Text).ToList();
                Assert.That(labels, Does.Contain(expectedBroadcast));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

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
    public async Task HuntingTrapTriggerBroadcastOnlyReachesArmedFactionLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid regularHunter = default;
        EntityUid badBloodHunter = default;
        EntityUid target = default;
        EntityUid trap = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                regularHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                badBloodHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(regularHunter);
                entMan.EnsureComponent<YautjaComponent>(badBloodHunter);
                entMan.EnsureComponent<NpcFactionMemberComponent>(badBloodHunter).Factions.Add("CMUYautjaBadBlood");
                server.PlayerMan.SetAttachedEntity(session, regularHunter);
                expectedBroadcast = $"A hunting trap has caught something in {areas.GetAreaName(trap)}!";
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var trapSystem = entMan.System<YautjaTrapSystem>();
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);

                Assert.That(trapSystem.TryArmTrap((trap, trapComp), badBloodHunter), Is.True);
                Assert.That(trapComp.ArmedFaction.ToString(), Is.EqualTo("CMUYautjaBadBlood"));
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                Assert.That(labels, Does.Not.Contain(expectedBroadcast),
                    "CMSS13 /obj/item/hunting_trap/trapMob() broadcasts to list(armed_faction), so regular Yautja should not receive a badblood-armed trap alert.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { regularHunter, badBloodHunter, target, trap })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapTriggerSoundMatchesCmss13TableHit()
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
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), target), Is.True);

                AssertSoundPath(trapComp.TriggerSound, "/Audio/_CMU14/Yautja/tablehit1.ogg");
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
    public async Task HuntingTrapAppliesXenoSideEffectsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();
            var xenoSystem = entMan.System<XenoSystem>();
            var status = entMan.System<StatusEffectQuerySystem>();
            var listener = entMan.System<YautjaTestSpeechListenerSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(xeno);
                listener.Emotes.Clear();

                Assert.That(xenoSystem.CanHeal(xeno), Is.True);
                Assert.That(status.TryGetTime(xeno, "YautjaInterference", out _), Is.False);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), xeno), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(xenoSystem.CanHeal(xeno), Is.False);
                    Assert.That(status.TryGetTime(xeno, "YautjaInterference", out var time), Is.True);
                    Assert.That(time!.Value.Item2 - time.Value.Item1, Is.EqualTo(TimeSpan.FromSeconds(100)));
                    Assert.That(listener.Emotes, Does.Contain((xeno, "XenoHelp")));
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(xeno))
                    entMan.DeleteEntity(xeno);
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapForcesHumanPainEmoteLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trapSystem = entMan.System<YautjaTrapSystem>();
            var listener = entMan.System<YautjaTestSpeechListenerSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTestEmoteListenerComponent>(human);
                listener.Emotes.Clear();

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapSystem.TryTriggerTrap((trap, trapComp), human), Is.True);

                Assert.That(listener.Emotes, Does.Contain((human, "Scream")));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(human))
                    entMan.DeleteEntity(human);
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
        });

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
                Assert.That(trophyComp.Source, Is.EqualTo(target));
                Assert.That(trophyComp.Hunter, Is.EqualTo(hunter));

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
                    Assert.That(peltInteract.Handled, Is.False,
                        "CMSS13 polishing_rag only handles /obj/item/clothing/accessory/limb/skeleton targets, not xeno pelts.");
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

    private static void AssertEquipped(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string prototype)
    {
        AssertEquippedAny(entMan, inventory, wearer, slot, prototype);
    }

    private static void AssertEquippedAny(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        params string[] prototypes)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        Assert.That(equipped, Is.Not.Null, slot);

        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(prototypes, Does.Contain(meta.EntityPrototype?.ID), slot);
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

    private static void RaiseFalconRecall(IEntityManager entMan, EntityUid hunter)
    {
        var controller = entMan.GetComponent<YautjaFalconControllerComponent>(hunter);
        Assert.That(controller.RecallAction, Is.Not.Null);

        var action = controller.RecallAction!.Value;
        var actionComp = entMan.GetComponent<ActionComponent>(action);
        entMan.EventBus.RaiseLocalEvent(hunter, NewFalconRecallEvent(hunter, action, actionComp));
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

    private static EntityUid SpawnAndTrack(IEntityManager entMan, string prototype, ICollection<EntityUid> spawned)
    {
        var uid = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        spawned.Add(uid);
        return uid;
    }

    private static void AssertPrototypeSpriteState(EntityPrototype prototype, IComponentFactory factory, string state)
    {
        Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, prototype.ID);
        Assert.That(sprite!.AllLayers.First().RsiState.Name, Is.EqualTo(state), $"{prototype.ID} CMSS13 icon_state");
    }

    private static void AssertPrototypeActionIconState(EntityPrototype prototype, IComponentFactory factory, string state)
    {
        Assert.That(prototype.TryGetComponent<ActionComponent>(out var action, factory), Is.True, prototype.ID);
        Assert.That(action!.Icon, Is.TypeOf<SpriteSpecifier.Rsi>(), $"{prototype.ID} action icon");
        Assert.That(((SpriteSpecifier.Rsi) action.Icon!).RsiState, Is.EqualTo(state), $"{prototype.ID} CMSS13 action_icon_state");
    }

    private static void AssertFalconItemSourceFacts(IEntityManager entMan, EntityUid uid, string deployedPrototype)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        Assert.That(meta.EntityName, Is.EqualTo("falcon drone"));
        Assert.That(meta.EntityDescription, Is.EqualTo("An agile drone used by Yautja to survey the hunting grounds."));

        Assert.That(entMan.GetComponent<ClothingComponent>(uid).Slots, Is.EqualTo(SlotFlags.EARS),
            "CMSS13 /obj/item/falcon_drone flags_equip_slot = SLOT_EAR.");
        Assert.That(entMan.GetComponent<YautjaFalconDroneComponent>(uid).DeployedPrototype.Id, Is.EqualTo(deployedPrototype),
            "Bad Blood falcons deploy the Bad Blood hologram subtype.");
        Assert.That(entMan.HasComponent<ActiveListenerComponent>(uid), Is.True,
            "CMSS13 /obj/item/falcon_drone flags_atom includes USES_HEARING.");

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(uid, out var tech), Is.True,
            "CMSS13 /obj/item/falcon_drone flags_item includes ITEM_PREDATOR.");
        Assert.That(tech!.DamageMultiplier, Is.EqualTo(1f),
            "ITEM_PREDATOR marks ownership/access here; the source drone is not a damage-scaling weapon.");
        Assert.That(tech.BlockPickup, Is.True, "CMSS13 ITEM_PREDATOR local pickup restriction.");
        Assert.That(tech.BlockUse, Is.True, "CMSS13 ITEM_PREDATOR local use restriction.");
    }

    private static void AssertFalconTrashSourceFacts(IEntityManager entMan, EntityUid uid, string name, string description)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(uid);
        Assert.That(meta.EntityName, Is.EqualTo(name));
        Assert.That(meta.EntityDescription, Is.EqualTo(description));

        Assert.That(entMan.HasComponent<YautjaFalconDroneComponent>(uid), Is.False,
            "CMSS13 /obj/item/trash/falcon_drone is wreckage and cannot deploy.");
        Assert.That(entMan.HasComponent<ActiveListenerComponent>(uid), Is.False,
            "CMSS13 falcon trash rows do not inherit USES_HEARING.");

        Assert.That(entMan.TryGetComponent<YautjaTechItemComponent>(uid, out var tech), Is.True,
            "CMSS13 /obj/item/trash/falcon_drone flags_item includes ITEM_PREDATOR.");
        Assert.That(tech!.DamageMultiplier, Is.EqualTo(1f),
            "Falcon trash is ITEM_PREDATOR wreckage, not a damage-scaling weapon.");
        Assert.That(tech.BlockPickup, Is.True, "CMSS13 ITEM_PREDATOR local pickup restriction.");
        Assert.That(tech.BlockUse, Is.False, "Falcon trash has no active use surface.");
    }

    private static void AssertCameraEntry(
        IEntityManager entMan,
        RMCCameraComputerComponent cameraComputer,
        EntityUid camera,
        string expectedName)
    {
        var netCamera = entMan.GetNetEntity(camera);
        var index = -1;
        for (var i = 0; i < cameraComputer.CameraIds.Count; i++)
        {
            if (cameraComputer.CameraIds[i] == netCamera)
            {
                index = i;
                break;
            }
        }

        Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{expectedName} camera id should be present.");
        Assert.That(cameraComputer.CameraNames, Has.Count.GreaterThan(index),
            $"{expectedName} camera name should have the same index as its camera id.");
        Assert.That(cameraComputer.CameraNames[index], Is.EqualTo(expectedName));
    }

    private static EntityUid GetHoundPadInternalCamera(IEntityManager entMan, EntityUid pad)
    {
        var internalCamera = EntityUid.Invalid;
        var query = entMan.EntityQueryEnumerator<RMCCameraComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.ParentUid != pad)
                continue;

            Assert.That(internalCamera, Is.EqualTo(EntityUid.Invalid),
                "CMSS13 /obj/item/device/houndcam/Initialize() creates one internal camera computer.");
            internalCamera = uid;
        }

        Assert.That(internalCamera, Is.Not.EqualTo(EntityUid.Invalid),
            "CMSS13 /obj/item/device/houndcam/Initialize() should create an internal camera computer.");
        return internalCamera;
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

    private static async Task AssertClientHasPopupMatching(
        RobustIntegrationTest.ClientIntegrationInstance client,
        Predicate<string> predicate,
        string expectedDescription)
    {
        await client.WaitAssertion(() =>
        {
            var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
            var joinedLabels = string.Join("\n", labels);

            Assert.That(labels.Any(label => predicate(label)), Is.True,
                $"Expected popup matching:\n{expectedDescription}\nActual labels:\n{joinedLabels}");
        });
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

    private static float? GetSlowdownFor(SlowOnPullComponent component, string componentName)
    {
        foreach (var slowdown in component.Slowdowns)
        {
            if (slowdown.Whitelist.Components?.Contains(componentName) == true)
                return slowdown.Multiplier;
        }

        return null;
    }

    private static SpriteSpecifier.Rsi AssertCmss13PowerbarIcon(AlertPrototype alert, short severity)
    {
        var icon = alert.GetIcon(severity);
        Assert.That(icon, Is.TypeOf<SpriteSpecifier.Rsi>(), $"severity {severity}");

        var rsi = (SpriteSpecifier.Rsi) icon;
        Assert.That(rsi.RsiPath.ToString(), Does.EndWith("_CMU14/Yautja/hud_yautja.rsi"), $"severity {severity}");
        return rsi;
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

    private static int CountAudio(IEntityManager entMan)
    {
        return entMan.Count<AudioComponent>();
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

    private static QueuedExplosion[] QueuedExplosions(ExplosionSystem explosions)
    {
        var queuedField = typeof(ExplosionSystem).GetField("_queuedExplosions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(queuedField, Is.Not.Null);

        return ((IEnumerable<QueuedExplosion>) queuedField!.GetValue(explosions)!).ToArray();
    }

    private static void ClearQueuedExplosions(ExplosionSystem explosions)
    {
        var queuedField = typeof(ExplosionSystem).GetField("_queuedExplosions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(queuedField, Is.Not.Null);
        queuedField!.GetValue(explosions)!.GetType().GetMethod("Clear")!.Invoke(queuedField.GetValue(explosions), null);

        var queueField = typeof(ExplosionSystem).GetField("_explosionQueue",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(queueField, Is.Not.Null);
        queueField!.GetValue(explosions)!.GetType().GetMethod("Clear")!.Invoke(queueField.GetValue(explosions), null);
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
