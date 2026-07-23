using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Server._CMU14.Yautja;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Vendors;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Roles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Inventory;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaYoungbloodTest
{
    [TestCase("mixed_small", "Multi Faction (small)", 4, 1.25f)]
    [TestCase("mixed_group", "Multi Faction (group)", 6, 1.4f)]
    [TestCase("mixed_large", "Multi Faction (large)", 8, 1.6f)]
    [TestCase("mixed_larger", "Multi Faction (larger)", 12, 1.8f)]
    [TestCase("serpents_small", "Serpents (small)", 4, 1f)]
    [TestCase("serpents_group", "Serpents (group)", 6, 1.2f)]
    [TestCase("serpents_large", "Serpents (large)", 8, 1.4f)]
    [TestCase("elite_mixed_small", "Elite Multi Faction (small)", 4, 1.5f)]
    [TestCase("elite_mixed_group", "Elite Multi Faction (group)", 6, 2f)]
    [TestCase("elite_mixed_large", "Elite Multi Faction (large)", 8, 2.5f)]
    [TestCase("elite_mixed_larger", "Elite Multi Faction (larger)", 12, 3f)]
    public async Task HuntsmasterCompatibilityRowsMatchPhase3Matrix(
        string id,
        string displayName,
        int count,
        float cooldownMultiplier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var console = prototypes.Index<EntityPrototype>("CMUHunterShipHuntsmastersConsole");

            Assert.That(console.TryGetComponent<YautjaHuntConsoleComponent>(out var comp, factory), Is.True);
            var option = comp!.HuntCallOptions.Single(option => option.Id == id);

            Assert.That(option.DisplayName, Is.EqualTo(displayName));
            Assert.That(option.SpawnCount, Is.EqualTo(count));
            Assert.That(option.CooldownMultiplier, Is.EqualTo(cooldownMultiplier).Within(0.001f));
            Assert.That(option.Spawns, Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("youngblood_solo", 1, 1, 0, 0, 5)]
    [TestCase("youngblood_solo_experienced", 1, 1, 7, 5, 5)]
    [TestCase("youngblood_three_inexperienced", 2, 3, 2, 0, 5)]
    [TestCase("youngblood_three_intermediate", 2, 3, 5, 2, 10)]
    [TestCase("youngblood_three_experienced", 2, 3, 10, 3, 20)]
    [TestCase("youngblood_three_mixed", 2, 3, 10, 0, 5)]
    [TestCase("youngblood_pack", 4, 6, 10, 0, 5)]
    public async Task BloodingCompatibilityRowsMatchPhase3Matrix(
        string id,
        int min,
        int max,
        int maximumYoungbloodHours,
        int rejectionYoungbloodHours,
        int requiredSquadAndXenoHours)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var console = prototypes.Index<EntityPrototype>("CMUHunterShipBloodingConsole");

            Assert.That(console.TryGetComponent<YautjaHuntConsoleComponent>(out var comp, factory), Is.True);
            var option = comp!.BloodingCallOptions.Single(option => option.Id == id);

            Assert.That(option.MinSpawnCount, Is.EqualTo(min));
            Assert.That(option.SpawnCount, Is.EqualTo(max));
            Assert.That(option.MaximumYoungbloodTime, Is.EqualTo(TimeSpan.FromHours(maximumYoungbloodHours)));
            Assert.That(option.RejectionYoungbloodTime, Is.EqualTo(TimeSpan.FromHours(rejectionYoungbloodHours)));
            Assert.That(option.RequiredSquadAndXenoTime, Is.EqualTo(TimeSpan.FromHours(requiredSquadAndXenoHours)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostRoleRequestAttemptCanRejectSpecificRoleEntity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var player = pair.Player;

        Assert.That(player, Is.Not.Null);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var role = entMan.EnsureComponent<GhostRoleComponent>(body);
            entMan.EnsureComponent<TestGhostRoleRequestGateComponent>(body);

            var ev = new GhostRoleRequestAttemptEvent(player!, body, role);
            entMan.EventBus.RaiseLocalEvent(body, ref ev);

            Assert.That(ev.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodJobUsesBaseBracerAndCommunicatorLoadout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var job = prototypes.Index<JobPrototype>("CMUYautjaYoungblood");
            var gear = prototypes.Index<StartingGearPrototype>("CMUYautjaYoungbloodGear");

            Assert.That(job.Hidden, Is.True);
            Assert.That(job.Whitelisted, Is.False);
            Assert.That(job.JobEntity, Is.EqualTo("CMUMobYautjaYoungblood"));
            Assert.That(gear.Equipment.Keys, Is.EquivalentTo(new[] { "ears", "gloves" }));
            Assert.That(gear.Equipment["ears"], Is.EqualTo("CMUYautjaCommunicator"));
            Assert.That(gear.Equipment["gloves"], Is.EqualTo("CMUYautjaBracer"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskEquipGrantsActionsHudAndLeavesVisorOffLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var actions = entMan.System<ActionContainerSystem>();
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);
            var mask = entMan.SpawnEntity("CMUYautjaMask", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(youngblood, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.User, Is.EqualTo(youngblood));
                Assert.That(maskComp.VisorEnabled, Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(youngblood), Is.False);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(youngblood), Is.True);

                Assert.That(maskComp.ToggleVisorAction, Is.Not.Null);
                var ev = new GetItemActionsEvent(actions, youngblood, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, ev);

                Assert.That(maskComp.ToggleVisorAction, Is.Not.Null);
                var actionComp = entMan.GetComponent<ActionComponent>(maskComp.ToggleVisorAction!.Value);
                Assert.That(actionComp.Toggled, Is.False,
                    "CMSS13 equipped() grants mask actions/HUDs but does not switch current_goggles from VISION_MODE_OFF.");
            }
            finally
            {
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
                if (!entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskVisorDisableKeepsHudUntilUnequipLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(wearer);
                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.False);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.True);
            });

            await server.WaitPost(() =>
            {
                RaiseToggleVisor(server.EntMan, wearer, mask);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.True);
            });

            await server.WaitPost(() =>
            {
                RaiseToggleVisor(server.EntMan, wearer, mask);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.False);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.True,
                    "CMSS13 togglesight() only changes the vision mode; mask HUDs are removed by dropped(), not by add_vision(OFF).");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                Assert.That(inventory.TryUnequip(wearer, "mask", force: true, silent: true), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, mask })
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
    public async Task MaskVisorToggleUsesCmss13SourceGuardsAndText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid nonYautjaTech = default;
        EntityUid nonYautjaTechMask = default;
        EntityUid looseMaskUser = default;
        EntityUid looseMask = default;
        EntityUid noBracerUser = default;
        EntityUid noBracerMask = default;
        EntityUid blockedEyesUser = default;
        EntityUid blockedEyesMask = default;
        EntityUid blockedEyesBracer = default;
        EntityUid glasses = default;
        EntityUid techUser = default;
        EntityUid techMask = default;
        EntityUid techBracer = default;
        EntityUid thrallUser = default;
        EntityUid thrallMask = default;
        EntityUid thrallBracer = default;
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

                nonYautjaTech = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                nonYautjaTechMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                looseMaskUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
                looseMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords.Offset(new Vector2(0, 1)));
                noBracerUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                noBracerMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords.Offset(new Vector2(1, 0)));
                blockedEyesUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                blockedEyesMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords.Offset(new Vector2(2, 0)));
                blockedEyesBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(2, 0)));
                glasses = entMan.SpawnEntity("ClothingEyesGlasses", map.GridCoords.Offset(new Vector2(2, 0)));
                techUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                techMask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords.Offset(new Vector2(3, 0)));
                techBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(3, 0)));
                thrallUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
                thrallMask = entMan.SpawnEntity("CMUYautjaMaskThrallEbony", map.GridCoords.Offset(new Vector2(4, 0)));
                thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords.Offset(new Vector2(4, 0)));

                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(nonYautjaTech);
                entMan.EnsureComponent<YautjaThrallComponent>(thrallUser);

                Assert.That(inventory.TryEquip(nonYautjaTech, nonYautjaTechMask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(noBracerUser, noBracerMask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(blockedEyesUser, blockedEyesMask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(blockedEyesUser, blockedEyesBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(blockedEyesUser, glasses, "eyes", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(techUser, techMask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(techUser, techBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrallUser, thrallMask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrallUser, thrallBracer, "gloves", silent: true, force: true), Is.True);

                entMan.EnsureComponent<YautjaComponent>(looseMaskUser);
                entMan.EnsureComponent<YautjaComponent>(noBracerUser);
                entMan.EnsureComponent<YautjaComponent>(blockedEyesUser);
                entMan.EnsureComponent<YautjaComponent>(techUser);

                Assert.That(entMan.GetComponent<YautjaMaskComponent>(nonYautjaTechMask).RequiresYautjaWearer, Is.True);
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(thrallMask).RequiresYautjaWearer, Is.False);
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(thrallMask).User, Is.EqualTo(thrallUser));
                Assert.That(entMan.HasComponent<YautjaThrallBracerComponent>(thrallBracer), Is.True);
                Assert.That(inventory.TryGetSlotEntity(thrallUser, "gloves", out var equippedThrallBracer), Is.True);
                Assert.That(equippedThrallBracer, Is.EqualTo(thrallBracer));
            });

            await UseVisorAndAssert(nonYautjaTech, nonYautjaTechMask, "You have no idea how to work this thing!", false);
            await UseVisorAndAssert(looseMaskUser, looseMask, "You must wear the clan mask!", false);
            await UseVisorAndAssert(noBracerUser, noBracerMask, "You must be wearing your bracers, as they have the power source.", false);
            await UseVisorAndAssert(blockedEyesUser, blockedEyesMask, "You need to remove your glasses first. Why are you even wearing these?", false);
            await UseVisorAndAssert(techUser, techMask, "Low-light vision module: activated.", true);
            await UseVisorAndAssert(techUser, techMask, "You deactivate your visor.", false);
            await UseVisorAndAssert(thrallUser, thrallMask, "Low-light vision module: activated.", true);
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
                             nonYautjaTech, nonYautjaTechMask, looseMaskUser, looseMask, noBracerUser,
                             noBracerMask, blockedEyesUser, blockedEyesMask, blockedEyesBracer, glasses,
                             techUser, techMask, techBracer, thrallUser, thrallMask, thrallBracer,
                         })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }

        return;

        async Task UseVisorAndAssert(EntityUid user, EntityUid mask, string expectedPopup, bool expectedEnabled)
        {
            await server.WaitPost(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, user);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                RaiseToggleVisor(server.EntMan, user, mask);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.EqualTo(expectedEnabled));
                Assert.That(entMan.HasComponent<NightVisionComponent>(user), Is.EqualTo(expectedEnabled));
            });

            await AssertYoungbloodClientHasPopup(client, expectedPopup);
        }
    }

    [Test]
    public async Task MaskProcessLowPowerShutsDownVisorLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid mask = default;
        EntityUid bracer = default;
        EntityUid? visorGlasses = null;
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
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                bracerComp.Charge = 2;
                bracerComp.Regen = 0;

                RaiseToggleVisor(entMan, hunter, mask);

                Assert.That(maskComp.Drain, Is.EqualTo((FixedPoint2) 3));
                Assert.That(maskComp.VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(hunter), Is.True);
                Assert.That(inventory.TryGetSlotEntity(hunter, "eyes", out visorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(visorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));
            });

            await pair.ReallyBeIdle(10);
            await pair.RunTicksSync(pair.SecondsToTicks(2.5f));
            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(hunter), Is.False);
                Assert.That(inventory.TryGetSlotEntity(hunter, "eyes", out _), Is.False);
                Assert.That(visorGlasses, Is.Not.Null);
                Assert.That(entMan.Deleted(visorGlasses!.Value), Is.True);
                Assert.That(entMan.GetComponent<YautjaBracerComponent>(bracer).Charge, Is.EqualTo((FixedPoint2) 2));
            });

            await AssertYoungbloodClientHasPopup(client, "Your bracers lack sufficient power to operate the visor.");
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

                foreach (var uid in new[] { hunter, mask, bracer, visorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskProcessDrainsOrdinaryThrallBracerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid thrall = default;
        EntityUid mask = default;
        EntityUid bracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMaskThrallEbony", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaThrallComponent>(thrall);

                Assert.That(inventory.TryEquip(thrall, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, mask, "mask", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(bracer);
                bracerComp.Charge = 10;

                RaiseToggleVisor(entMan, thrall, mask);

                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(thrall), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(2.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.GetComponent<YautjaThrallBracerComponent>(bracer).Charge, Is.EqualTo((FixedPoint2) 7));
                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(thrall), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { thrall, mask, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskZoomWorksWithoutActiveVisorLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.User, Is.EqualTo(wearer));

                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);
                Assert.That(maskComp.VisorEnabled, Is.False);

                var ev = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);

                Assert.That(ev.Handled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                var action = entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value);
                Assert.That(action.Toggled, Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskZoomPersistsOnMoveOrLookLikeUpstream()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var zoom = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);
                Assert.That(zoom.Handled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);

                transform.SetCoordinates(wearer, map.GridCoords.Offset(new Vector2(1, 0)));

                Assert.Multiple(() =>
                {
                    Assert.That(maskComp.Zoomed, Is.True,
                        "Upstream keeps mask zoom active while the wearer moves.");
                    Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                    Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value).Toggled, Is.True);
                    Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom,
                        Is.EqualTo(Vector2.One * maskComp.ZoomLevel));
                });

                transform.SetLocalRotation(wearer, Angle.FromDegrees(90));

                Assert.Multiple(() =>
                {
                    Assert.That(maskComp.Zoomed, Is.True,
                        "Upstream keeps mask zoom active when the wearer changes facing.");
                    Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                    Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value).Toggled, Is.True);
                    Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom,
                        Is.EqualTo(Vector2.One * maskComp.ZoomLevel));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskZoomRequiresMaskWornByPerformerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid outsider = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                outsider = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.User, Is.EqualTo(wearer));

                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var ev = RaiseToggleMaskZoom(entMan, outsider, mask, maskComp.ToggleZoomAction);

                Assert.That(ev.Handled, Is.False);
                Assert.That(maskComp.Zoomed, Is.False);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(outsider), Is.False);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, outsider, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskZoomUsesUpstreamOffsetCalculation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                entMan.GetComponent<TransformComponent>(wearer).LocalRotation = Angle.Zero;

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.ZoomLevel, Is.EqualTo(1.6f).Within(0.001f));
                Assert.That(maskComp.ZoomOffset, Is.EqualTo(14f).Within(0.001f));

                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var ev = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);

                Assert.That(ev.Handled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);
                var zoom = entMan.GetComponent<YautjaMaskZoomComponent>(wearer);
                var offset = zoom.Offset;
                var expectedOffset = (maskComp.ZoomOffset * maskComp.ZoomLevel - 1f) / 2f;
                Assert.That(offset.Length(), Is.EqualTo(expectedOffset).Within(0.001f));
                Assert.That(MathF.Abs(offset.X) + MathF.Abs(offset.Y), Is.EqualTo(expectedOffset).Within(0.001f));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [TestCase("CMUYautjaMask")]
    [TestCase("CMUYautjaMaskThrallEbony")]
    public async Task MaskZoomUsesUpstreamViewScale(string maskPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();
                const float expectedZoom = 1.6f;

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity(maskPrototype, map.GridCoords);

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.ZoomLevel, Is.EqualTo(expectedZoom).Within(0.001f));

                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var ev = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);

                Assert.That(ev.Handled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom,
                    Is.EqualTo(Vector2.One * expectedZoom));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskExternalUnzoomClearsActionStateLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);

                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var ev = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);

                Assert.That(ev.Handled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                var action = entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value);
                Assert.That(action.Toggled, Is.True);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom, Is.EqualTo(Vector2.One * maskComp.ZoomLevel));

                entMan.RemoveComponent<YautjaMaskZoomComponent>(wearer);

                Assert.That(maskComp.Zoomed, Is.False);
                Assert.That(action.Toggled, Is.False);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom, Is.EqualTo(Vector2.One));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YoungbloodBracerActionsMatchCmss13Restrictions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var actions = entMan.System<ActionContainerSystem>();
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(youngblood, bracer, "gloves", silent: true, force: true), Is.True);

                var ev = new GetItemActionsEvent(actions, youngblood, bracer, SlotFlags.GLOVES);
                entMan.EventBus.RaiseLocalEvent(bracer, ev);
                var actionIds = ActionPrototypeIds(entMan, ev.Actions);

                Assert.Multiple(() =>
                {
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaOpenBracerMenu"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleCloak"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaRecall"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaTranslator"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleWristBlades"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleScimitar"));
                    Assert.That(actionIds, Does.Contain("CMUActionYautjaToggleChainGauntlet"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaSelfDestruct"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleCaster"));
                    Assert.That(actionIds, Does.Not.Contain("CMUActionYautjaToggleShield"));
                });
            }
            finally
            {
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskVisorAndZoomActionsPlaySourceSoundsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid mask = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(wearer);

                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.False);
                AssertSoundPath(maskComp.ToggleVisorSound, "/Audio/_CMU14/Yautja/Equipment/pred_vision.wav");
                AssertSoundPath(maskComp.ZoomOnSound, "/Audio/_CMU14/Yautja/pred_zoom_on.ogg");
                AssertSoundPath(maskComp.ZoomOffSound, "/Audio/_CMU14/Yautja/pred_zoom_off.ogg");

                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                AssertActionPlaysSound(entMan, () => RaiseToggleVisor(entMan, wearer, mask), maskComp.ToggleVisorSound);
                Assert.That(maskComp.VisorEnabled, Is.True);
                AssertActionPlaysSound(entMan, () => RaiseToggleVisor(entMan, wearer, mask), maskComp.ToggleVisorSound);
                Assert.That(maskComp.VisorEnabled, Is.False);
                AssertActionPlaysSound(entMan, () => RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction), maskComp.ZoomOnSound);
                AssertActionPlaysSound(entMan, () => RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction), maskComp.ZoomOffSound);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, mask })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task McastePoweredHelmetSharesUpstreamMaskVisorAndZoomRuntime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid helmet = default;
        EntityUid? visorGlasses = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();
                const float expectedZoom = 1.6f;

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                helmet = entMan.SpawnEntity("CMUYautjaPoweredHelmet", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(wearer);

                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, helmet, "head", silent: true, force: true), Is.True);

                var helmetMask = entMan.GetComponent<YautjaMaskComponent>(helmet);
                Assert.That(helmetMask.Slots, Is.EqualTo(SlotFlags.HEAD),
                    "CMSS13 /obj/item/clothing/head/helmet/yautja grants helmet visor/zoom actions when worn in WEAR_HEAD.");
                Assert.That(helmetMask.User, Is.EqualTo(wearer));
                Assert.That(helmetMask.ZoomLevel, Is.EqualTo(expectedZoom).Within(0.001f),
                    "The powered helmet shares the normal upstream mask zoom scale.");
                Assert.That(helmetMask.ZoomOffset, Is.EqualTo(14f).Within(0.001f),
                    "The powered helmet shares the normal upstream mask zoom offset.");

                var getHeadActions = new GetItemActionsEvent(actions, wearer, helmet, SlotFlags.HEAD);
                entMan.EventBus.RaiseLocalEvent(helmet, getHeadActions);
                Assert.That(helmetMask.ToggleVisorAction, Is.Not.Null);
                Assert.That(helmetMask.ToggleZoomAction, Is.Not.Null);

                RaiseToggleVisor(entMan, wearer, helmet);

                Assert.That(helmetMask.VisorEnabled, Is.True);
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out visorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(visorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"),
                    "CMSS13 powered helmet add_vision(NVG) equips /obj/item/clothing/glasses/night/yautja like a mask.");
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);

                var zoomEv = RaiseToggleMaskZoom(entMan, wearer, helmet, helmetMask.ToggleZoomAction);

                Assert.That(zoomEv.Handled, Is.True);
                Assert.That(helmetMask.Zoomed, Is.True);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom,
                    Is.EqualTo(Vector2.One * expectedZoom));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, helmet, visorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task McastePoweredHelmetReequipsActiveVisorLikeCmss13Equipped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid helmet = default;
        EntityUid? originalVisorGlasses = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                helmet = entMan.SpawnEntity("CMUYautjaPoweredHelmet", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(wearer);

                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, helmet, "head", silent: true, force: true), Is.True);

                var helmetMask = entMan.GetComponent<YautjaMaskComponent>(helmet);
                RaiseToggleVisor(entMan, wearer, helmet);

                Assert.That(helmetMask.VisorEnabled, Is.True);
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out originalVisorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(originalVisorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);

                Assert.That(inventory.TryUnequip(wearer, "head", silent: true, force: true), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var helmetMask = entMan.GetComponent<YautjaMaskComponent>(helmet);

                Assert.That(helmetMask.VisorEnabled, Is.True,
                    "CMSS13 powered helmet dropped() removes HUD/glasses but does not switch current_goggles off.");
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out _), Is.False,
                    "CMSS13 powered helmet dropped() deletes the generated Yautja night-vision glasses.");
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.False);
                Assert.That(originalVisorGlasses, Is.Not.Null);
                Assert.That(entMan.Deleted(originalVisorGlasses!.Value), Is.True);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                Assert.That(inventory.TryEquip(wearer, helmet, "head", silent: true, force: true), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var helmetMask = entMan.GetComponent<YautjaMaskComponent>(helmet);

                Assert.That(helmetMask.User, Is.EqualTo(wearer));
                Assert.That(helmetMask.VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.True,
                    "CMSS13 powered helmet equipped() re-adds the helmet HUDs on WEAR_HEAD.");
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out var restoredGlasses), Is.True,
                    "CMSS13 powered helmet equipped() calls add_vision(user) when current_goggles is still active.");
                Assert.That(entMan.GetComponent<MetaDataComponent>(restoredGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, helmet, originalVisorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YautjaNightVisionGlassesPrototypeMatchesCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;
        var sourceSprite = new ResPath("/Textures/_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi");

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var glasses = prototypes.Index<EntityPrototype>("CMUYautjaNightVisionGlasses");

            Assert.That(glasses.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(sourceSprite));
            Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("visor_nvg"));
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var glasses = prototypes.Index<EntityPrototype>("CMUYautjaNightVisionGlasses");

            Assert.That(glasses.Name, Is.EqualTo("bio-mask nightvision"));
            Assert.That(glasses.Description, Is.EqualTo("A vision overlay generated by the Bio-Mask. Used for low-light conditions."));

            Assert.That(glasses.TryGetComponent<ClothingComponent>(out var clothing, factory), Is.True);
            Assert.That(clothing!.RsiPath, Is.EqualTo("_CMU14/HunterShip/obj/items/hunter/pred_gear.rsi"));
            Assert.That(clothing.Slots, Is.EqualTo(SlotFlags.EYES));

            Assert.That(glasses.TryGetComponent<ItemComponent>(out var item, factory), Is.True);
            Assert.That(item!.Size.Id, Is.EqualTo("Small"));
            Assert.That(item.StoredRotation, Is.EqualTo(0));

            Assert.That(glasses.TryGetComponent<NightVisionItemComponent>(out var nightVision, factory), Is.True);
            Assert.That(nightVision!.Toggleable, Is.False);
            Assert.That(nightVision.EnableOnEquip, Is.True);
            Assert.That(nightVision.ActionId, Is.Null, "CMSS13 /night/yautja sets actions_types = null.");
            Assert.That(nightVision.SlotFlags, Is.EqualTo(SlotFlags.EYES));
            Assert.That(nightVision.DefaultState, Is.EqualTo(NightVisionState.Full));
            Assert.That(nightVision.Green, Is.False);
            Assert.That(nightVision.BlockScopes, Is.True);
            Assert.That(nightVision.IgnoreUserOnlyHalf, Is.True);

            Assert.That(glasses.TryGetComponent<UnremoveableComponent>(out var unremoveable, factory), Is.True,
                "CMSS13 /night/yautja keeps NODROP|DELONDROP from the mask-created visor item.");
            Assert.That(unremoveable!.DeleteOnDrop, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskVisorNightVisionIsAppliedBySourceGlassesLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid mask = default;
        EntityUid? visorGlasses = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = entMan.ComponentFactory;

                var glassesPrototype = prototypes.Index<EntityPrototype>("CMUYautjaNightVisionGlasses");
                Assert.That(glassesPrototype.TryGetComponent<NightVisionItemComponent>(out var prototypeNightVision, factory), Is.True,
                    "CMSS13 add_vision(NVG) equips /obj/item/clothing/glasses/night/yautja, so the local eyes item should own the update-sight equivalent.");
                Assert.That(prototypeNightVision!.Toggleable, Is.False);
                Assert.That(prototypeNightVision.EnableOnEquip, Is.True);
                Assert.That(prototypeNightVision.SlotFlags, Is.EqualTo(SlotFlags.EYES));
                Assert.That(prototypeNightVision.DefaultState, Is.EqualTo(NightVisionState.Full));
                Assert.That(prototypeNightVision.Green, Is.False);
                Assert.That(prototypeNightVision.Mesons, Is.False);
                Assert.That(prototypeNightVision.BlockScopes, Is.True);
                Assert.That(prototypeNightVision.IgnoreUserOnlyHalf, Is.True);

                var maskPrototype = prototypes.Index<EntityPrototype>("CMUYautjaMask");
                Assert.That(maskPrototype.TryGetComponent<NightVisionItemComponent>(out _, factory), Is.False,
                    "CMSS13 masks toggle current_goggles and equip night/yautja glasses; the mask item itself is not the sight component.");

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(wearer);

                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);

                RaiseToggleVisor(entMan, wearer, mask);

                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionItemComponent>(mask), Is.False);
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out visorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(visorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));

                var glassesNightVision = entMan.GetComponent<NightVisionItemComponent>(visorGlasses.Value);
                Assert.That(glassesNightVision.User, Is.EqualTo(wearer));
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);

                var userNightVision = entMan.GetComponent<NightVisionComponent>(wearer);
                Assert.That(userNightVision.State, Is.EqualTo(NightVisionState.Full));
                Assert.That(userNightVision.Green, Is.False);
                Assert.That(userNightVision.Overlay, Is.False);
                Assert.That(userNightVision.SeeThroughContainers, Is.False);
                Assert.That(userNightVision.Mesons, Is.False);
                Assert.That(userNightVision.BlockScopes, Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, mask, visorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task MaskVisorCreatesAndDeletesYautjaNightGlassesLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid thrall = default;
        EntityUid bracer = default;
        EntityUid mask = default;
        EntityUid? visorGlasses = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMaskThrallEbony", map.GridCoords);
                entMan.EnsureComponent<YautjaThrallComponent>(thrall);

                Assert.That(inventory.TryEquip(thrall, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(thrall, mask, "mask", silent: true, force: true), Is.True);
                Assert.That(inventory.TryGetSlotEntity(thrall, "eyes", out _), Is.False);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.False);

                RaiseToggleVisor(entMan, thrall, mask);

                Assert.That(maskComp.VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(thrall), Is.True);
                Assert.That(inventory.TryGetSlotEntity(thrall, "eyes", out visorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(visorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));

                RaiseToggleVisor(entMan, thrall, mask);

                Assert.That(maskComp.VisorEnabled, Is.False);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                Assert.That(inventory.TryGetSlotEntity(thrall, "eyes", out _), Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(thrall), Is.False);
                Assert.That(visorGlasses, Is.Not.Null);
                Assert.That(entMan.Deleted(visorGlasses!.Value), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { thrall, bracer, mask, visorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YoungbloodCannotArmSelfDestructLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(youngblood, bracer, "gloves", silent: true, force: true), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), youngblood, TimeSpan.FromSeconds(1)), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskDropClearsVisorZoomActionsAndHudLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;
        EntityUid mask = default;
        EntityUid? visorGlasses = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var actions = entMan.System<ActionContainerSystem>();

                wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(wearer);

                Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                var getActions = new GetItemActionsEvent(actions, wearer, mask, SlotFlags.MASK);
                entMan.EventBus.RaiseLocalEvent(mask, getActions);
                Assert.That(maskComp.ToggleVisorAction, Is.Not.Null);
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                RaiseToggleVisor(entMan, wearer, mask);
                var zoomEv = RaiseToggleMaskZoom(entMan, wearer, mask, maskComp.ToggleZoomAction);
                Assert.That(zoomEv.Handled, Is.True);

                Assert.That(maskComp.User, Is.EqualTo(wearer));
                Assert.That(maskComp.VisorEnabled, Is.True);
                Assert.That(maskComp.Zoomed, Is.True);
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.True);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.True);
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out visorGlasses), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(visorGlasses!.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUYautjaNightVisionGlasses"));
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.True);
                Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleVisorAction!.Value).Toggled, Is.True);
                Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value).Toggled, Is.True);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom, Is.EqualTo(Vector2.One * maskComp.ZoomLevel));

                Assert.That(inventory.TryUnequip(wearer, "mask", silent: true, force: true), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);

                Assert.That(maskComp.User, Is.Null);
                Assert.That(maskComp.VisorEnabled, Is.False);
                Assert.That(maskComp.Zoomed, Is.False);
                Assert.That(entMan.HasComponent<NightVisionComponent>(wearer), Is.False);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(wearer), Is.False);
                Assert.That(inventory.TryGetSlotEntity(wearer, "eyes", out _), Is.False);
                Assert.That(visorGlasses, Is.Not.Null);
                Assert.That(entMan.Deleted(visorGlasses!.Value), Is.True);
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(wearer), Is.False);
                Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleVisorAction!.Value).Toggled, Is.False);
                Assert.That(entMan.GetComponent<ActionComponent>(maskComp.ToggleZoomAction!.Value).Toggled, Is.False);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(wearer).TargetZoom, Is.EqualTo(Vector2.One));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { wearer, bracer, mask, visorGlasses.GetValueOrDefault() })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YoungbloodRackContainsOnlyCmss13YoungItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                Assert.That(VendorEntryIds(vendor), Is.EquivalentTo(Cmss13YoungRackEntries()));
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
    public async Task YoungbloodRackClaimGroupsMatchCmss13YoungMatrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", MapCoordinates.Nullspace);
            EntityUid huntingBundle = default;
            EntityUid armorBundle = default;

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Main Weapons (CHOOSE 1)",
                    "Bracer Attachments",
                }));

                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var mainWeapons = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");

                Assert.That(essentials.Choices, Is.Null);
                Assert.That(essentials.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaYoungbloodHuntingEquipmentBundle",
                    "CMUYautjaArmorBundle",
                }));
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaYoungbloodHuntingEquipmentBundle"), "CMUYautjaEssentials", 1);
                AssertChoice(essentials.Entries.Single(entry => entry.Id.Id == "CMUYautjaArmorBundle"), "CMUYautjaArmor", 1);

                huntingBundle = entMan.SpawnEntity("CMUYautjaYoungbloodHuntingEquipmentBundle", MapCoordinates.Nullspace);
                armorBundle = entMan.SpawnEntity("CMUYautjaArmorBundle", MapCoordinates.Nullspace);
                AssertBundle(entMan, huntingBundle, new[]
                {
                    "CMUYautjaBodyMesh",
                    "CMUYautjaHuntingPouch",
                    "CMUYautjaMedicompFull",
                    "CMUYautjaLantern",
                });
                AssertBundle(entMan, armorBundle, new[]
                {
                    "CMUYautjaClanArmor",
                    "CMUYautjaMask",
                    "CMUYautjaMaskAccessory01Ebony",
                    "CMUYautjaClanGreaves",
                });

                AssertChoice(mainWeapons, "CMUYautjaPrimary", 1);
                Assert.That(mainWeapons.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
                    "CMUYautjaChainwhip",
                    "CMUYautjaDualWarScythe",
                    "CMUYautjaDoubleWarScythe",
                    "CMUYautjaCombistick",
                    "CMUYautjaWarAxe",
                    "CMUYautjaWarGlaive",
                    "CMUYautjaCleavingGlaive",
                    "CMUYautjaLongaxe",
                }));

                Assert.That(bracer.Choices, Is.Null);
                Assert.That(bracer.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaWristBladesBundle",
                    "CMUYautjaFearsomeScimitarsBundle",
                    "CMUYautjaSkeweringScimitarsBundle",
                    "CMUYautjaChainGauntletsBundle",
                }));
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaWristBladesBundle"), "CMUYautjaBracer", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaSkeweringScimitarsBundle"), "CMUYautjaPrimary", 1);
                AssertChoice(bracer.Entries.Single(entry => entry.Id.Id == "CMUYautjaChainGauntletsBundle"), "CMUYautjaPrimary", 1);
            }
            finally
            {
                foreach (var uid in new[] { rack, huntingBundle, armorBundle })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodRackRowsUseCmss13DisplayNamesAndRecommendedFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                var mainWeapons = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                var bracer = vendor.Sections.Single(section => section.Name == "Bracer Attachments");

                Assert.Multiple(() =>
                {
                    AssertVendorRow(essentials, "CMUYautjaYoungbloodHuntingEquipmentBundle", "Hunting Equipment");
                    AssertVendorRow(essentials, "CMUYautjaArmorBundle", "Armor");

                    AssertVendorRow(mainWeapons, "CMUYautjaClanSword", "The Primary Hunting Sword", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaRendingSword", "The Rending Hunting Sword", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaPiercingSword", "The Piercing Hunting Sword", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaSeveringSword", "The Severing Hunting Sword", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaChainwhip", "The Sundering Chain-Whip", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaDualWarScythe", "The Cleaving War-Scythe", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaDoubleWarScythe", "The Ripping War-Scythe", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaCombistick", "The Adaptive Combi-Stick", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaWarAxe", "The Butchering War Axe", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaWarGlaive", "The Lumbering Glaive", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaCleavingGlaive", "The Imposing Glaive", recommended: true);
                    AssertVendorRow(mainWeapons, "CMUYautjaLongaxe", "The Crushing Longaxe", recommended: true);

                    AssertVendorRow(bracer, "CMUYautjaWristBladesBundle", "Wrist Blades");
                    AssertVendorRow(bracer, "CMUYautjaFearsomeScimitarsBundle", "The Fearsome Scimitars", recommended: true);
                    AssertVendorRow(bracer, "CMUYautjaSkeweringScimitarsBundle", "The Skewering Scimitars", recommended: true);
                    AssertVendorRow(bracer, "CMUYautjaChainGauntletsBundle", "The Chain Gauntlets", recommended: true);
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
    public async Task YoungbloodRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", MapCoordinates.Nullspace);
            var youngblood = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentialsIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                var bracerIndex = vendor.Sections.FindIndex(section => section.Name == "Bracer Attachments");
                Assert.That(essentialsIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bracerIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[essentialsIndex];
                var bracer = vendor.Sections[bracerIndex];
                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaYoungbloodHuntingEquipmentBundle");
                var armorIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaArmorBundle");
                var wristIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaWristBladesBundle");
                var scimitarIndex = bracer.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaFearsomeScimitarsBundle");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(wristIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(scimitarIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, youngblood, essentialsIndex, huntingIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(youngblood);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);

                Vend(entMan, rack, youngblood, essentialsIndex, armorIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));

                Vend(entMan, rack, youngblood, bracerIndex, wristIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.Zero);

                Vend(entMan, rack, youngblood, bracerIndex, scimitarIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaPrimary"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaYoungbloodPrimary"), Is.Zero);
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipThrallGearRackUsesCmss13ThrallInventory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity(
                "CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorLeftSouthOffset0x16",
                MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind, Is.EqualTo(YautjaGearRackKind.Thrall));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                Assert.That(VendorEntryIds(vendor), Is.EquivalentTo(Cmss13ThrallRackEntries()));
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
    public async Task ThrallRackClaimGroupsMatchCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaThrallLoadoutVendor", MapCoordinates.Nullspace);
            var spawned = new List<EntityUid>();

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Essential Hunting Supplies",
                    "Armor Material (CHOOSE 1)",
                    "Main Weapons (CHOOSE 1)",
                }));

                var essentials = vendor.Sections.Single(section => section.Name == "Essential Hunting Supplies");
                Assert.That(essentials.Choices, Is.Null);
                Assert.That(essentials.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaThrallHuntingEquipmentBundle",
                }));
                AssertChoice(essentials.Entries.Single(), "CMUYautjaEssentials", 1);
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallHuntingEquipmentBundle"), new[]
                {
                    "CMUYautjaThrallChainshirt",
                    "CMUYautjaHuntingPouch",
                    "CMUYautjaLantern",
                    "CMUYautjaCommunicator",
                });

                var armor = vendor.Sections.Single(section => section.Name == "Armor Material (CHOOSE 1)");
                AssertChoice(armor, "CMUYautjaArmor", 1);
                Assert.That(armor.Entries.Select(entry => (entry.Id.Id, entry.Name, entry.Recommended)).ToArray(), Is.EqualTo(new[]
                {
                    ("CMUYautjaThrallArmorEbonyBundle", "Ebony", true),
                    ("CMUYautjaThrallArmorSilverBundle", "Silver", true),
                    ("CMUYautjaThrallArmorGoldBundle", "Gold", true),
                    ("CMUYautjaThrallArmorCrimsonBundle", "Crimson", true),
                    ("CMUYautjaThrallArmorBoneBundle", "Bone", true),
                }));
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallArmorEbonyBundle"), new[]
                {
                    "CMUYautjaThrallArmorEbony",
                    "CMUYautjaThrallGreavesEbony",
                    "CMUYautjaMaskThrallEbony",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallArmorSilverBundle"), new[]
                {
                    "CMUYautjaThrallArmorSilver",
                    "CMUYautjaThrallGreavesSilver",
                    "CMUYautjaMaskThrallSilver",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallArmorGoldBundle"), new[]
                {
                    "CMUYautjaThrallArmorGold",
                    "CMUYautjaThrallGreavesGold",
                    "CMUYautjaMaskThrallGold",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallArmorCrimsonBundle"), new[]
                {
                    "CMUYautjaThrallArmorCrimson",
                    "CMUYautjaThrallGreavesCrimson",
                    "CMUYautjaMaskThrallCrimson",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaThrallArmorBoneBundle"), new[]
                {
                    "CMUYautjaThrallArmorBone",
                    "CMUYautjaThrallGreavesBone",
                    "CMUYautjaMaskThrallBone",
                });

                var weapons = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");
                AssertChoice(weapons, "CMUYautjaAccessory", 1);
                Assert.That(weapons.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaClanSword",
                    "CMUYautjaRendingSword",
                    "CMUYautjaPiercingSword",
                    "CMUYautjaSeveringSword",
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
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }

            EntityUid SpawnBundle(string id)
            {
                var bundle = entMan.SpawnEntity(id, MapCoordinates.Nullspace);
                spawned.Add(bundle);
                return bundle;
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallRackWeaponRowsUseCmss13DisplayNamesAndRegularFlags()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaThrallLoadoutVendor", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var weapons = vendor.Sections.Single(section => section.Name == "Main Weapons (CHOOSE 1)");

                Assert.Multiple(() =>
                {
                    AssertVendorRow(weapons, "CMUYautjaClanSword", "The Primary Hunting Sword");
                    AssertVendorRow(weapons, "CMUYautjaRendingSword", "The Rending Hunting Sword");
                    AssertVendorRow(weapons, "CMUYautjaPiercingSword", "The Piercing Hunting Sword");
                    AssertVendorRow(weapons, "CMUYautjaSeveringSword", "The Severing Hunting Sword");
                    AssertVendorRow(weapons, "CMUYautjaChainwhip", "The Sundering Chain-Whip");
                    AssertVendorRow(weapons, "CMUYautjaDualWarScythe", "The Cleaving War-Scythe");
                    AssertVendorRow(weapons, "CMUYautjaDoubleWarScythe", "The Ripping War-Scythe");
                    AssertVendorRow(weapons, "CMUYautjaCombistick", "The Adaptive Combi-Stick");
                    AssertVendorRow(weapons, "CMUYautjaWarAxe", "The Butchering War Axe");
                    AssertVendorRow(weapons, "CMUYautjaWarGlaive", "The Lumbering Glaive");
                    AssertVendorRow(weapons, "CMUYautjaCleavingGlaive", "The Imposing Glaive");
                    AssertVendorRow(weapons, "CMUYautjaLongaxe", "The Crushing Longaxe");
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
    public async Task ThrallRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaThrallLoadoutVendor", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var essentialsIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                var armorIndex = vendor.Sections.FindIndex(section => section.Name == "Armor Material (CHOOSE 1)");
                var weaponsIndex = vendor.Sections.FindIndex(section => section.Name == "Main Weapons (CHOOSE 1)");
                Assert.That(essentialsIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(armorIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(weaponsIndex, Is.GreaterThanOrEqualTo(0));

                var essentials = vendor.Sections[essentialsIndex];
                var armor = vendor.Sections[armorIndex];
                var weapons = vendor.Sections[weaponsIndex];
                var huntingIndex = essentials.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaThrallHuntingEquipmentBundle");
                var ebonyIndex = armor.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaThrallArmorEbonyBundle");
                var swordIndex = weapons.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaClanSword");
                Assert.That(huntingIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(ebonyIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(swordIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, thrall, essentialsIndex, huntingIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(thrall);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaEssentials"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaThrallPrimary"), Is.Zero);

                Vend(entMan, rack, thrall, armorIndex, ebonyIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaArmor"), Is.EqualTo(1));

                Vend(entMan, rack, thrall, weaponsIndex, swordIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaThrallPrimary"), Is.Zero);
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(thrall))
                    entMan.DeleteEntity(thrall);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipBloodedThrallGearRackUsesCmss13BloodedThrallInventory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity(
                "CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorBloodedLeftSouthOffset0x16",
                MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.GetComponent<YautjaGearRackComponent>(rack).Kind, Is.EqualTo(YautjaGearRackKind.BloodedThrall));

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                Assert.That(VendorEntryIds(vendor), Is.EquivalentTo(Cmss13BloodedThrallRackEntries()));
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
    public async Task BloodedThrallRackClaimGroupsMatchCmss13Matrix()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaBloodedThrallLoadoutVendor", MapCoordinates.Nullspace);
            var spawned = new List<EntityUid>();

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionNames = vendor.Sections.Select(section => section.Name).ToArray();

                Assert.That(sectionNames, Is.EqualTo(new[]
                {
                    "Blooded Equipment",
                    "Blooded Bracer Material (CHOOSE 1)",
                    "Clothing Accessory (CHOOSE 1)",
                }));

                var equipment = vendor.Sections.Single(section => section.Name == "Blooded Equipment");
                Assert.That(equipment.Choices, Is.Null);
                Assert.That(equipment.Entries.Select(entry => entry.Id.Id).ToArray(), Is.EqualTo(new[]
                {
                    "CMUYautjaBloodedThrallEquipmentBundle",
                }));
                AssertChoice(equipment.Entries.Single(), "CMUYautjaRanged", 1);
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallEquipmentBundle"), new[]
                {
                    "CMUYautjaSimpleRelayBeacon",
                    "CMUYautjaMedicompThrall",
                });

                var bracers = vendor.Sections.Single(section => section.Name == "Blooded Bracer Material (CHOOSE 1)");
                AssertChoice(bracers, "CMUYautjaBracer", 1);
                Assert.That(bracers.Entries.Select(entry => (entry.Id.Id, entry.Name, entry.Recommended)).ToArray(), Is.EqualTo(new[]
                {
                    ("CMUYautjaBloodedThrallBracerEbonyBundle", "Ebony", true),
                    ("CMUYautjaBloodedThrallBracerSilverBundle", "Silver", true),
                    ("CMUYautjaBloodedThrallBracerGoldBundle", "Gold", true),
                    ("CMUYautjaBloodedThrallBracerCrimsonBundle", "Crimson", true),
                    ("CMUYautjaBloodedThrallBracerBoneBundle", "Bone", true),
                }));
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallBracerEbonyBundle"), new[]
                {
                    "CMUYautjaBloodedThrallBracer",
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallBracerSilverBundle"), new[]
                {
                    "CMUYautjaBloodedThrallBracerSilver",
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallBracerGoldBundle"), new[]
                {
                    "CMUYautjaBloodedThrallBracerGold",
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallBracerCrimsonBundle"), new[]
                {
                    "CMUYautjaBloodedThrallBracerCrimson",
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });
                AssertBundle(entMan, SpawnBundle("CMUYautjaBloodedThrallBracerBoneBundle"), new[]
                {
                    "CMUYautjaBloodedThrallBracerBone",
                    "CMUYautjaWristBladesAttachment",
                    "CMUYautjaWristBladesAttachment",
                });

                var accessory = vendor.Sections.Single(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                AssertChoice(accessory, "CMUYautjaAccessory", 1);
                Assert.That(accessory.Entries.Select(entry => (entry.Id.Id, entry.Name)).ToArray(), Is.EqualTo(new[]
                {
                    ("CMUYautjaCapeQuarter", "Quarter-Cape"),
                    ("CMUYautjaCapeThird", "Third-Cape"),
                    ("CMUYautjaCapeHalf", "Half-Cape"),
                    ("CMUYautjaCapePoncho", "Poncho"),
                }));
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
            }

            EntityUid SpawnBundle(string id)
            {
                var bundle = entMan.SpawnEntity(id, MapCoordinates.Nullspace);
                spawned.Add(bundle);
                return bundle;
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallRackVendsSourceClaimsSeparately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rack = entMan.SpawnEntity("CMUYautjaBloodedThrallLoadoutVendor", MapCoordinates.Nullspace);
            var thrall = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var equipmentIndex = vendor.Sections.FindIndex(section => section.Name == "Blooded Equipment");
                var bracerIndex = vendor.Sections.FindIndex(section => section.Name == "Blooded Bracer Material (CHOOSE 1)");
                var accessoryIndex = vendor.Sections.FindIndex(section => section.Name == "Clothing Accessory (CHOOSE 1)");
                Assert.That(equipmentIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(bracerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(accessoryIndex, Is.GreaterThanOrEqualTo(0));

                var equipment = vendor.Sections[equipmentIndex];
                var bracers = vendor.Sections[bracerIndex];
                var accessory = vendor.Sections[accessoryIndex];
                var equipmentEntryIndex = equipment.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaBloodedThrallEquipmentBundle");
                var ebonyBracerIndex = bracers.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaBloodedThrallBracerEbonyBundle");
                var capeIndex = accessory.Entries.FindIndex(entry => entry.Id.Id == "CMUYautjaCapeQuarter");
                Assert.That(equipmentEntryIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(ebonyBracerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(capeIndex, Is.GreaterThanOrEqualTo(0));

                Vend(entMan, rack, thrall, equipmentIndex, equipmentEntryIndex);
                var user = entMan.GetComponent<CMVendorUserComponent>(thrall);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaRanged"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBloodedThrallBracer"), Is.Zero);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBloodedThrallCape"), Is.Zero);

                Vend(entMan, rack, thrall, bracerIndex, ebonyBracerIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBracer"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBloodedThrallBracer"), Is.Zero);

                Vend(entMan, rack, thrall, accessoryIndex, capeIndex);
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaAccessory"), Is.EqualTo(1));
                Assert.That(user.Choices.GetValueOrDefault("CMUYautjaBloodedThrallCape"), Is.Zero);
            }
            finally
            {
                if (!entMan.Deleted(rack))
                    entMan.DeleteEntity(rack);
                if (!entMan.Deleted(thrall))
                    entMan.DeleteEntity(thrall);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingPouchUsesCmss13DirectionalWaistState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", MapCoordinates.Nullspace);

            try
            {
                var clothing = entMan.GetComponent<ClothingComponent>(pouch);
#pragma warning disable RA0002
                Assert.That(clothing.ClothingVisuals.TryGetValue("belt", out var layers), Is.True);
#pragma warning restore RA0002
                Assert.That(layers, Is.Not.Null);
                Assert.That(layers!.Single().State, Is.EqualTo("beltbag_w"));
            }
            finally
            {
                if (!entMan.Deleted(pouch))
                    entMan.DeleteEntity(pouch);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodGhostRoleTakeoverEquipsBaseGearForMaskPower()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var player = pair.Player;

        Assert.That(player, Is.Not.Null);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mind = entMan.System<MindSystem>();
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);
            entMan.EnsureComponent<YautjaYoungbloodGhostRoleComponent>(youngblood);

            try
            {
                var mindEnt = mind.CreateMind(player!.UserId, "Yautja Youngblood");
                mind.TransferTo(mindEnt.Owner, youngblood);

                AssertEquippedPrototype(entMan, inventory, youngblood, "gloves", "CMUYautjaBracer");
                AssertEquippedPrototype(entMan, inventory, youngblood, "ears", "CMUYautjaCommunicator");
            }
            finally
            {
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void SoloYoungbloodCallRejectsZeroHourCandidateLikeUpstream()
    {
        var option = YoungbloodOption("youngblood_solo");
        var result = YautjaYoungbloodSystem.CheckEligibility(
            option,
            adultWhitelisted: false,
            jobBanned: false,
            youngbloodTime: TimeSpan.Zero,
            squadTime: TimeSpan.FromHours(5),
            xenoTime: TimeSpan.FromHours(5));

        Assert.That(result.Allowed, Is.False);
        Assert.That(result.Reason, Is.EqualTo(YautjaYoungbloodRejection.MaximumYoungbloodTime));
    }

    [Test]
    public void ExperiencedCallRequiresSquadAndXenoHoursEvenWhenYoungbloodHoursAreEnough()
    {
        var option = YoungbloodOption("youngblood_solo_experienced");
        var result = YautjaYoungbloodSystem.CheckEligibility(
            option,
            adultWhitelisted: false,
            jobBanned: false,
            youngbloodTime: TimeSpan.FromHours(5),
            squadTime: TimeSpan.Zero,
            xenoTime: TimeSpan.Zero);

        Assert.That(result.Allowed, Is.False);
        Assert.That(result.Reason, Is.EqualTo(YautjaYoungbloodRejection.SquadAndXenoTime));
    }

    [Test]
    public async Task AdminBypassSpawnsYoungbloodGhostRoleWithoutEligibilityGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<YautjaHuntConsoleSystem>();
            var consoleUid = entMan.SpawnEntity("CMUHunterShipBloodingConsole", MapCoordinates.Nullspace);
            var console = entMan.GetComponent<YautjaHuntConsoleComponent>(consoleUid);
            console.DestinationId = "jungle_moon";
            entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", MapCoordinates.Nullspace);
            entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", MapCoordinates.Nullspace);

            var option = console.BloodingCallOptions.Single(option => option.Id == "youngblood_three_inexperienced");
            Assert.That(system.TryCreateYoungbloodCall((consoleUid, console), consoleUid, option, bypassEligibility: true), Is.True);

            var query = entMan.EntityQueryEnumerator<YautjaYoungbloodGhostRoleComponent, GhostRoleComponent>();
            Assert.That(query.MoveNext(out _, out var metadata, out var role), Is.True);
            Assert.That(metadata.BypassEligibility, Is.True);
            Assert.That(role.JobProto, Is.EqualTo("CMUYautjaYoungblood"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjacentAdultAndYoungbloodRacksRemainSeparateRuns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid adult = default;
        EntityUid young = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = entMan.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));

            adult = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            young = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", map.GridCoords.Offset(new Vector2(1, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var adultRack = entMan.GetComponent<YautjaGearRackComponent>(adult);
            var youngRack = entMan.GetComponent<YautjaGearRackComponent>(young);

            Assert.That(adultRack.Kind, Is.EqualTo(YautjaGearRackKind.Adult));
            Assert.That(youngRack.Kind, Is.EqualTo(YautjaGearRackKind.Youngblood));
            Assert.That(adultRack.PrimaryVendor, Is.EqualTo(adult));
            Assert.That(youngRack.PrimaryVendor, Is.EqualTo(young));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(YautjaHuntTeleporterKind.Ship, true, false, false, true)]
    [TestCase(YautjaHuntTeleporterKind.Ship, true, true, false, false)]
    [TestCase(YautjaHuntTeleporterKind.Ship, true, true, true, false)]
    [TestCase(YautjaHuntTeleporterKind.Young, true, false, false, true)]
    [TestCase(YautjaHuntTeleporterKind.Young, true, true, false, true)]
    [TestCase(YautjaHuntTeleporterKind.Young, true, true, true, true)]
    [TestCase(YautjaHuntTeleporterKind.Young, false, false, true, true)]
    [TestCase(YautjaHuntTeleporterKind.Young, false, false, false, false)]
    public void TeleporterPolicyMatchesYoungbloodTrial(
        YautjaHuntTeleporterKind kind,
        bool yautja,
        bool youngblood,
        bool techAuthorized,
        bool expected)
    {
        Assert.That(YautjaHuntTeleporterSystem.CanUse(kind, yautja, youngblood, techAuthorized), Is.EqualTo(expected));
    }

    [Test]
    public async Task YoungbloodTeleporterRequiresConfirmationBeforeDeploying()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var teleporter = entMan.SpawnEntity(null, map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Young;
                teleporterComp.DestinationId = "jungle_moon";
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(hunter);

                var before = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates, Is.EqualTo(before));

                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var after = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                Assert.That(after, Is.Not.EqualTo(before));
                Assert.That(after.EntityId, Is.EqualTo(entMan.GetComponent<TransformComponent>(destination).Coordinates.EntityId));
            }
            finally
            {
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipTeleporterRequiresConfirmationBeforeDeploying()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var teleporter = entMan.SpawnEntity(null, map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "jungle_moon";
                entMan.EnsureComponent<YautjaComponent>(hunter);

                var before = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates, Is.EqualTo(before));

                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var after = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                Assert.That(after, Is.Not.EqualTo(before));
                Assert.That(after.EntityId, Is.EqualTo(entMan.GetComponent<TransformComponent>(destination).Coordinates.EntityId));
            }
            finally
            {
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjacentShipTeleportersOnlyOpenOneConfirmation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var first = entMan.SpawnEntity(null, map.GridCoords);
            var second = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaHuntDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                foreach (var teleporter in new[] { first, second })
                {
                    var comp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                    comp.Kind = YautjaHuntTeleporterKind.Ship;
                    comp.DestinationId = "jungle_moon";
                }

                entMan.EnsureComponent<YautjaComponent>(hunter);

                var before = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                var firstEv = new StepTriggeredOnEvent(first, hunter);
                entMan.EventBus.RaiseLocalEvent(first, ref firstEv);
                var secondEv = new StepTriggeredOnEvent(second, hunter);
                entMan.EventBus.RaiseLocalEvent(second, ref secondEv);

                Assert.That(entMan.HasComponent<DialogComponent>(first), Is.True);
                Assert.That(entMan.HasComponent<DialogComponent>(second), Is.False);
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates, Is.EqualTo(before));
                Assert.That(entMan.GetComponent<TransformComponent>(destination).Coordinates, Is.Not.EqualTo(before));
            }
            finally
            {
                if (!entMan.Deleted(first))
                    entMan.DeleteEntity(first);
                if (!entMan.Deleted(second))
                    entMan.DeleteEntity(second);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipTeleportersHaveDialogUiForConfirmations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var id in new[] { "CMUHunterShipTeleporterYautjaShip", "CMUHunterShipTeleporterYautjaYoung" })
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<UserInterfaceComponent>(out _, factory), Is.True,
                    $"{id} opens a DialogBui confirmation when stepped on, so it must provide UserInterface.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodStudentMarkClaimsAndReleasesMentor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                var secondMentorClaim = marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Student, null);
                var releaseByOtherMentor = marks.TryClearMark(pupil, YautjaMarkKind.Student, otherMentor);
                var releaseByOwner = marks.TryClearMark(pupil, YautjaMarkKind.Student, mentor);

                Assert.That(secondMentorClaim, Is.False);
                Assert.That(releaseByOtherMentor, Is.False);
                Assert.That(releaseByOwner, Is.True);
                Assert.That(youngblood.Mentor, Is.Null);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(otherMentor))
                    entMan.DeleteEntity(otherMentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(otherBracer))
                    entMan.DeleteEntity(otherBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodStudentRepeatClaimUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid mentor = default;
        EntityUid otherMentor = default;
        EntityUid pupil = default;
        EntityUid bracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, otherMentor);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Student, null), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("has already been claimed by", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_youngblood() says '[target_youngblood] has already been claimed by [mentor.real_name].'\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { mentor, otherMentor, pupil, bracer, otherBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodStudentClaimTellsPupilCmss13GuidanceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid mentor = default;
        EntityUid pupil = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, pupil);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label =>
                        label.Contains("You have been marked as a Youngblood by", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("Focus on learning from your mentor", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("LOOC", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_youngblood() sends the pupil the full guidance paragraph after claiming them.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { mentor, pupil, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodMentorDeletionClearsStudentMarkLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                entMan.DeleteEntity(mentor);

                Assert.That(youngblood.Mentor, Is.Null,
                    "CMSS13 huntdata.clean_data() clears the pupil-side youngblood_set when the mentor huntdata is cleaned.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Student, mentor), Is.False,
                    "Deleting the mentor must remove the local Student mark instead of leaving stale mark ownership.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodRoundRestartClearsStudentMarkLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

                Assert.That(youngblood.Mentor, Is.Null,
                    "CMSS13 huntdata.clean_data() clears youngblood_marked/youngblood_set across round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Student, mentor), Is.False,
                    "Round restart cleanup must remove the local Student mark instead of leaving stale mark ownership.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingAuthorizesTechWithoutThrallConversion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);

                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(entMan.HasComponent<YautjaThrallComponent>(pupil), Is.False);
                Assert.That(entMan.GetComponent<YautjaYoungbloodComponent>(pupil).Blooded, Is.True);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingCannotBeRemovedLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);
                Assert.That(marks.TryClearMark(pupil, YautjaMarkKind.Blooded, mentor), Is.False,
                    "CMSS13 mark_blooded() has no mark_unblooded path; once a youngblood is blooded, the trial completion is permanent.");

                Assert.That(entMan.GetComponent<YautjaYoungbloodComponent>(pupil).Blooded, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingCannotBeOverwrittenLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);
                Assert.That(marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Blooded, null), Is.False,
                    "CMSS13 mark_blooded() returns when the target's hunter_data.blooded flag is already set, preserving the first blooding hunter.");

                Assert.That(youngblood.Blooded, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, otherMentor), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(otherMentor))
                    entMan.DeleteEntity(otherMentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(otherBracer))
                    entMan.DeleteEntity(otherBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingStoresReasonAndRepeatDenialLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
            AdminLogsEnabled = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid mentor = default;
        EntityUid otherMentor = default;
        EntityUid pupil = default;
        EntityUid bracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, otherMentor);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "claimed a worthy trophy"), Is.True);
                Assert.That(youngblood.BloodedBy, Is.EqualTo(mentor),
                    "CMSS13 stores hunter_data.blooded_set when mark_blooded() succeeds.");
                Assert.That(youngblood.BloodingReason, Is.EqualTo("claimed a worthy trophy"),
                    "CMSS13 stores hunter_data.blooded_reason for repeat-denial text.");

                Assert.That(marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Blooded, "second claim"), Is.False);
                Assert.That(youngblood.BloodedBy, Is.EqualTo(mentor));
                Assert.That(youngblood.BloodingReason, Is.EqualTo("claimed a worthy trophy"));
            });

            await pair.ReallyBeIdle(10);

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);

            Assert.That(
                messages.Any(message =>
                    message.Contains("has blooded", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains(" for ", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("claimed a worthy trophy", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 mark_blooded() logs '[hunter] has blooded [target] for [reason]'.\nActual logs:\n{joinedMessages}");

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label =>
                        label.Contains("has already been blooded by", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("claimed a worthy trophy", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_blooded() repeat denial says '[target] has already been blooded by [blooded_set.real_name] for [blooded_reason]'.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { mentor, otherMentor, pupil, bracer, otherBracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingHunterDeletionClearsBloodedByLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "survived the trial"), Is.True);
                Assert.That(youngblood.BloodedBy, Is.EqualTo(mentor));

                entMan.DeleteEntity(mentor);

                Assert.That(youngblood.Blooded, Is.True,
                    "CMSS13 clean_data() does not unblood the target when the blooding hunter is destroyed.");
                Assert.That(youngblood.BloodedBy, Is.Null,
                    "CMSS13 clean_data() clears newblood.hunter_data.blooded_set when the blooding hunter's huntdata is cleaned.");
                Assert.That(youngblood.BloodingReason, Is.EqualTo("survived the trial"),
                    "CMSS13 clean_data() leaves the target's blooded_reason intact on blooding-hunter cleanup.");
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True,
                    "CMSS13 keeps TRAIT_YAUTJA_TECH after blooding; cleanup only severs the stale reciprocal link.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.False,
                    "Deleting the blooding hunter must not leave local Blooded mark ownership pointing at a destroyed entity.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingTellsPupilCmss13GuidanceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid mentor = default;
        EntityUid pupil = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                server.PlayerMan.SetAttachedEntity(session, pupil);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "proved worthy"), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label =>
                        label.Contains("You are a Blooded Thrall", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("developing your reputation", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("Honor Code", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("LOOC", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_blooded() sends the target the full Blooded guidance paragraph after tech authorization.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { mentor, pupil, bracer })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteYoungbloodExecutionRequiresAdultReasonAndKillsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var youngbloods = entMan.System<YautjaYoungbloodSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var adult = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var otherPupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(adult);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaComponent>(otherPupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(otherPupil);

                Assert.That(youngbloods.TryExecuteYoungblood(adult, pupil, ""), Is.False);
                Assert.That(youngbloods.TryExecuteYoungblood(pupil, otherPupil, "reason"), Is.False);
                Assert.That(youngbloods.TryExecuteYoungblood(adult, pupil, "dishonored the trial"), Is.True);
                Assert.That(mobState.IsDead(pupil), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(adult))
                    entMan.DeleteEntity(adult);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(otherPupil))
                    entMan.DeleteEntity(otherPupil);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteYoungbloodExecutionDialogSelectsTargetAndRequiresReason()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var youngbloods = entMan.System<YautjaYoungbloodSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var adult = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(adult);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(adult, bracer, "gloves", silent: true, force: true), Is.True);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(youngbloods.TryOpenRemoteExecution((bracer, bracerComp), adult), Is.True);

                var dialog = entMan.GetComponent<DialogComponent>(bracer);
                Assert.That(dialog.DialogType, Is.EqualTo(DialogType.Options));
                Assert.That(dialog.Options, Is.Not.Empty);
                Assert.That(dialog.Options, Has.All.Matches<DialogOption>(option => option.Event is YautjaYoungbloodExecutionTargetSelectedEvent));

                var selected = dialog.Options.Single(option =>
                    option.Event is YautjaYoungbloodExecutionTargetSelectedEvent ev &&
                    ev.Target == entMan.GetNetEntity(pupil));
                entMan.EventBus.RaiseLocalEvent(bracer, selected.Event!);

                dialog = entMan.GetComponent<DialogComponent>(bracer);
                Assert.That(dialog.DialogType, Is.EqualTo(DialogType.Input));
                Assert.That(dialog.MinCharacterLimit, Is.EqualTo(1));
                Assert.That(dialog.InputEvent, Is.TypeOf<YautjaYoungbloodExecutionReasonEvent>());

                entMan.EventBus.RaiseLocalEvent(
                    bracer,
                    new YautjaYoungbloodExecutionReasonEvent(entMan.GetNetEntity(adult), entMan.GetNetEntity(pupil), "dishonored the trial"));

                Assert.That(mobState.IsDead(pupil), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(adult))
                    entMan.DeleteEntity(adult);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodRoundEndTextIncludesTrackedStatus()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var predatorRoundSystem = entMan.System<YautjaPredatorRoundSystem>();

            var rule = entMan.SpawnEntity("CMUYautjaPredatorRound", MapCoordinates.Nullspace);
            var alive = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var dead = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaYoungbloodComponent>(alive);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(dead);
                mobState.ChangeMobState(dead, Content.Shared.Mobs.MobState.Dead);

                var predatorRound = entMan.GetComponent<YautjaPredatorRoundComponent>(rule);
                predatorRoundSystem.TrackYoungblood((rule, predatorRound), alive);
                predatorRoundSystem.TrackYoungblood((rule, predatorRound), dead);

                var ev = new RoundEndTextAppendEvent();
                entMan.EventBus.RaiseEvent(EventSource.Local, ev);

                Assert.That(ev.Text, Does.Contain(entMan.GetComponent<MetaDataComponent>(alive).EntityName));
                Assert.That(ev.Text, Does.Contain(entMan.GetComponent<MetaDataComponent>(dead).EntityName));
                Assert.That(ev.Text, Does.Contain(Loc.GetString("cmu-yautja-youngblood-round-end-alive")));
                Assert.That(ev.Text, Does.Contain(Loc.GetString("cmu-yautja-youngblood-round-end-dead")));
            }
            finally
            {
                if (!entMan.Deleted(rule))
                    entMan.DeleteEntity(rule);
                if (!entMan.Deleted(alive))
                    entMan.DeleteEntity(alive);
                if (!entMan.Deleted(dead))
                    entMan.DeleteEntity(dead);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static YautjaHuntCallOption YoungbloodOption(string id)
    {
        return id switch
        {
            "youngblood_solo" => new YautjaHuntCallOption
            {
                Id = id,
                MaximumYoungbloodTime = TimeSpan.Zero,
                RejectionYoungbloodTime = TimeSpan.Zero,
                RequiredSquadAndXenoTime = TimeSpan.FromHours(5),
            },
            "youngblood_solo_experienced" => new YautjaHuntCallOption
            {
                Id = id,
                MaximumYoungbloodTime = TimeSpan.FromHours(7),
                RejectionYoungbloodTime = TimeSpan.FromHours(5),
                RequiredSquadAndXenoTime = TimeSpan.FromHours(5),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown Youngblood call"),
        };
    }

    private static void AssertEquippedPrototype(IEntityManager entMan, InventorySystem inventory, EntityUid wearer, string slot, string expected)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, $"Expected {expected} in {slot}.");
        Assert.That(entMan.GetComponent<MetaDataComponent>(equipped.Value).EntityPrototype?.ID, Is.EqualTo(expected));
    }

    private static HashSet<string> ActionPrototypeIds(IEntityManager entMan, IEnumerable<EntityUid> actions)
    {
        return actions
            .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
            .OfType<string>()
            .ToHashSet();
    }

    private static HashSet<string> VendorEntryIds(CMAutomatedVendorComponent vendor)
    {
        return vendor.Sections
            .SelectMany(section => section.Entries)
            .Select(entry => entry.Id.Id)
            .ToHashSet();
    }

    private static void AssertChoice(CMVendorSection section, string id, int amount)
    {
        Assert.That(section.Choices, Is.Not.Null, $"{section.Name} should use source claim category {id}.");
        Assert.That(section.Choices!.Value.Id, Is.EqualTo(id), $"{section.Name} source claim category");
        Assert.That(section.Choices.Value.Amount, Is.EqualTo(amount), $"{section.Name} source claim amount");
    }

    private static void AssertChoice(CMVendorEntry entry, string id, int amount)
    {
        Assert.That(entry.Choices, Is.Not.Null, $"{entry.Id.Id} should use source claim category {id}.");
        Assert.That(entry.Choices!.Value.Id, Is.EqualTo(id), $"{entry.Id.Id} source claim category");
        Assert.That(entry.Choices.Value.Amount, Is.EqualTo(amount), $"{entry.Id.Id} source claim amount");
    }

    private static void AssertVendorRow(
        CMVendorSection section,
        string id,
        string name,
        bool recommended = false)
    {
        var entry = section.Entries.Single(entry => entry.Id.Id == id);

        Assert.That(entry.Name, Is.EqualTo(name), $"{id} display name");
        Assert.That(entry.Recommended, Is.EqualTo(recommended), $"{id} recommended flag");
        Assert.That(entry.Points, Is.Null, $"{id} regular equipment row should cost 0 source points");
        Assert.That(entry.Amount, Is.EqualTo((int?) 1), $"{id} source regular row vends one item/bundle");
        Assert.That(entry.ReplaceSlot, Is.Null, $"{id} replace slot");
    }

    private static void AssertBundle(IEntityManager entMan, EntityUid bundle, string[] expected)
    {
        var bundleComp = entMan.GetComponent<CMVendorBundleComponent>(bundle);
        var bundleIds = bundleComp.Bundle.Select(id => id.Id).ToArray();

        Assert.That(bundleIds, Is.EqualTo(expected));
    }

    private static void Vend(IEntityManager entMan, EntityUid rack, EntityUid user, int sectionIndex, int entryIndex)
    {
        entMan.EventBus.RaiseLocalEvent(rack, new CMVendorVendBuiMsg(sectionIndex, entryIndex, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    private static void AssertActionPlaysSound(IEntityManager entMan, Action action, SoundSpecifier expected)
    {
        var before = AudioEntities(entMan);
        action();
        var audio = AudioFileNamesAfter(entMan, before);

        Assert.That(expected, Is.TypeOf<SoundPathSpecifier>());
        Assert.That(audio, Does.Contain(((SoundPathSpecifier) expected).Path.ToString()));
    }

    private static void AssertSoundPath(SoundSpecifier sound, string expected)
    {
        Assert.That(sound, Is.TypeOf<SoundPathSpecifier>());
        Assert.That(((SoundPathSpecifier) sound).Path.ToString(), Is.EqualTo(expected));
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

    private static void RaiseToggleVisor(IEntityManager entMan, EntityUid user, EntityUid mask)
    {
        var action = entMan.SpawnEntity("CMUActionYautjaToggleVisor", MapCoordinates.Nullspace);
        try
        {
            entMan.EventBus.RaiseLocalEvent(mask, new YautjaToggleVisorActionEvent
            {
                Performer = user,
                Action = (action, entMan.GetComponent<ActionComponent>(action)),
            });
        }
        finally
        {
            if (!entMan.Deleted(action))
                entMan.DeleteEntity(action);
        }
    }

    private static YautjaToggleMaskZoomActionEvent RaiseToggleMaskZoom(
        IEntityManager entMan,
        EntityUid user,
        EntityUid mask,
        EntityUid? actionEntity = null)
    {
        var action = actionEntity ?? entMan.SpawnEntity("CMUActionYautjaToggleMaskZoom", MapCoordinates.Nullspace);
        try
        {
            var ev = new YautjaToggleMaskZoomActionEvent
            {
                Performer = user,
                Action = (action, entMan.GetComponent<ActionComponent>(action)),
            };

            entMan.EventBus.RaiseLocalEvent(mask, ev);
            return ev;
        }
        finally
        {
            if (actionEntity == null && !entMan.Deleted(action))
                entMan.DeleteEntity(action);
        }
    }

    private static async Task AssertYoungbloodClientHasPopup(RobustIntegrationTest.ClientIntegrationInstance client, string expected)
    {
        await client.WaitAssertion(() =>
        {
            var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
            Assert.That(labels, Does.Contain(expected), $"Actual popups:\n{string.Join("\n", labels)}");
        });
    }

    private static string[] Cmss13YoungRackEntries()
    {
        return
        [
            "CMUYautjaYoungbloodHuntingEquipmentBundle",
            "CMUYautjaArmorBundle",
            "CMUYautjaClanSword",
            "CMUYautjaRendingSword",
            "CMUYautjaPiercingSword",
            "CMUYautjaSeveringSword",
            "CMUYautjaChainwhip",
            "CMUYautjaDualWarScythe",
            "CMUYautjaDoubleWarScythe",
            "CMUYautjaCombistick",
            "CMUYautjaWarAxe",
            "CMUYautjaWarGlaive",
            "CMUYautjaCleavingGlaive",
            "CMUYautjaLongaxe",
            "CMUYautjaWristBladesBundle",
            "CMUYautjaFearsomeScimitarsBundle",
            "CMUYautjaSkeweringScimitarsBundle",
            "CMUYautjaChainGauntletsBundle",
        ];
    }

    private static string[] Cmss13ThrallRackEntries()
    {
        return
        [
            "CMUYautjaThrallHuntingEquipmentBundle",
            "CMUYautjaThrallArmorEbonyBundle",
            "CMUYautjaThrallArmorSilverBundle",
            "CMUYautjaThrallArmorGoldBundle",
            "CMUYautjaThrallArmorCrimsonBundle",
            "CMUYautjaThrallArmorBoneBundle",
            "CMUYautjaClanSword",
            "CMUYautjaRendingSword",
            "CMUYautjaPiercingSword",
            "CMUYautjaSeveringSword",
            "CMUYautjaChainwhip",
            "CMUYautjaDualWarScythe",
            "CMUYautjaDoubleWarScythe",
            "CMUYautjaCombistick",
            "CMUYautjaWarAxe",
            "CMUYautjaWarGlaive",
            "CMUYautjaCleavingGlaive",
            "CMUYautjaLongaxe",
        ];
    }

    private static string[] Cmss13BloodedThrallRackEntries()
    {
        return
        [
            "CMUYautjaBloodedThrallEquipmentBundle",
            "CMUYautjaBloodedThrallBracerEbonyBundle",
            "CMUYautjaBloodedThrallBracerSilverBundle",
            "CMUYautjaBloodedThrallBracerGoldBundle",
            "CMUYautjaBloodedThrallBracerCrimsonBundle",
            "CMUYautjaBloodedThrallBracerBoneBundle",
            "CMUYautjaCapeQuarter",
            "CMUYautjaCapeThird",
            "CMUYautjaCapeHalf",
            "CMUYautjaCapePoncho",
        ];
    }
}

[RegisterComponent]
public sealed partial class TestGhostRoleRequestGateComponent : Component;

public sealed partial class TestGhostRoleRequestGateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TestGhostRoleRequestGateComponent, GhostRoleRequestAttemptEvent>(OnAttempt);
    }

    private static void OnAttempt(Entity<TestGhostRoleRequestGateComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
